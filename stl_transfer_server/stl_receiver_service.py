#!/usr/bin/env python3
"""
STL Transfer Server - Recibe STL de Quest y lanza Bambu Studio automáticamente
Autor: Alan Melgar
Fecha: 2026-09-19

Cómo usar:
  1. pip install -r requirements.txt
  2. python stl_receiver_service.py
  3. El servidor escuchará en http://0.0.0.0:5000
"""

from fastapi import FastAPI, UploadFile, File, Form, HTTPException
from fastapi.responses import JSONResponse
import os
import subprocess
import sys
import platform
from pathlib import Path
from datetime import datetime
import logging

# ===== CONFIGURACIÓN =====

# Carpeta donde guardar STLs
STL_FOLDER = Path.home() / "Documents" / "InmobiliariaVR_Exports" / "STL_para_slicear"

# Puerto del servidor
SERVER_PORT = 5000

# Si querés forzar una ruta específica de Bambu Studio, ponla aquí (deja "" para auto-detección).
BAMBU_STUDIO_PATH_MANUAL = ""


def _candidatos_bambu():
    """Lista de rutas candidatas de Bambu Studio según el SO."""
    so = platform.system()
    home = Path.home()
    if so == "Darwin":  # macOS
        return [
            "/Applications/Bambu Studio.app/Contents/MacOS/BambuStudio",
            "/Applications/BambuStudio.app/Contents/MacOS/BambuStudio",
        ]
    if so == "Windows":
        return [
            r"C:\Program Files\Bambu Studio\BambuStudio.exe",
            r"C:\Program Files (x86)\Bambu Studio\BambuStudio.exe",
            str(home / "AppData" / "Local" / "Programs" / "Bambu Studio" / "BambuStudio.exe"),
        ]
    # Linux: paquetes, AppImage, flatpak
    candidatos = [
        "/usr/bin/bambu-studio",
        "/usr/bin/BambuStudio",
        "/usr/local/bin/bambu-studio",
        "/opt/Bambu Studio/BambuStudio",
        "/opt/bambustudio/bambu-studio",
        "/var/lib/flatpak/exports/bin/com.bambulab.BambuStudio",
        str(home / ".local" / "share" / "flatpak" / "exports" / "bin" / "com.bambulab.BambuStudio"),
    ]
    # Buscar AppImage en carpetas comunes del usuario
    for carpeta in [home, home / "Downloads", home / "Applications", home / "Apps"]:
        try:
            if carpeta.exists():
                for f in carpeta.glob("*[Bb]ambu*.AppImage"):
                    candidatos.append(str(f))
        except Exception:
            pass
    return candidatos


def detectar_bambu_studio():
    """
    Detecta la ruta de Bambu Studio.
    Devuelve (ruta_o_None, es_flatpak_bool).
    """
    # 1. Ruta manual si se configuró
    if BAMBU_STUDIO_PATH_MANUAL and Path(BAMBU_STUDIO_PATH_MANUAL).exists():
        return BAMBU_STUDIO_PATH_MANUAL, False

    # 2. Candidatos conocidos
    for ruta in _candidatos_bambu():
        if Path(ruta).exists():
            es_flatpak = "flatpak" in ruta.lower() or ruta.endswith("com.bambulab.BambuStudio")
            return ruta, es_flatpak

    # 3. Buscar en PATH (which)
    import shutil
    for nombre in ["bambu-studio", "BambuStudio", "bambustudio"]:
        encontrado = shutil.which(nombre)
        if encontrado:
            return encontrado, False

    # 4. Flatpak instalado pero sin symlink directo
    if platform.system() == "Linux":
        try:
            r = subprocess.run(["flatpak", "info", "com.bambulab.BambuStudio"],
                               capture_output=True, timeout=5)
            if r.returncode == 0:
                return "com.bambulab.BambuStudio", True
        except Exception:
            pass

    return None, False


# Detectar al iniciar
BAMBU_STUDIO_PATH, BAMBU_ES_FLATPAK = detectar_bambu_studio()

# ===== LOGGING =====

logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(levelname)s - %(message)s',
    handlers=[
        logging.FileHandler("stl_transfer_server.log"),
        logging.StreamHandler()
    ]
)
logger = logging.getLogger(__name__)

# ===== FASTAPI APP =====

app = FastAPI(
    title="STL Transfer Server",
    description="Recibe archivos STL de Quest 3 y lanza Bambu Studio",
    version="1.0.0"
)

# Crear carpeta si no existe
STL_FOLDER.mkdir(parents=True, exist_ok=True)
logger.info(f"📁 Carpeta de STLs: {STL_FOLDER}")


def bambu_disponible():
    """True si detectamos alguna forma de abrir el archivo (Bambu Studio o abridor por defecto)."""
    return BAMBU_STUDIO_PATH is not None


def lanzar_archivo(filepath: str):
    """
    Abre el archivo STL. Estrategia:
      1. Si detectamos Bambu Studio (ruta / flatpak) -> lo abre directamente en Bambu Studio.
      2. Si no -> usa el abridor por defecto del SO (xdg-open / open / start), que abrirá el
         .stl con la aplicación asociada (puede ser otro slicer, pero al menos no falla).
    Devuelve (exito: bool, mensaje: str).
    """
    so = platform.system()
    try:
        if BAMBU_STUDIO_PATH is not None:
            if BAMBU_ES_FLATPAK:
                subprocess.Popen(["flatpak", "run", BAMBU_STUDIO_PATH, filepath])
            elif so == "Darwin" and BAMBU_STUDIO_PATH.endswith(".app"):
                subprocess.Popen(["open", "-a", BAMBU_STUDIO_PATH, filepath])
            else:
                subprocess.Popen([BAMBU_STUDIO_PATH, filepath])
            return True, f"Bambu Studio lanzado con: {Path(filepath).name}"

        # Fallback: abridor por defecto del sistema
        if so == "Windows":
            os.startfile(filepath)  # type: ignore[attr-defined]
        elif so == "Darwin":
            subprocess.Popen(["open", filepath])
        else:  # Linux
            subprocess.Popen(["xdg-open", filepath])
        return True, (f"Abierto con la app por defecto del sistema para .stl "
                      f"(Bambu Studio no fue detectado automáticamente)")
    except Exception as e:
        return False, f"No se pudo abrir el archivo: {e}"


# Reportar estado de Bambu Studio al iniciar
if BAMBU_STUDIO_PATH is not None:
    logger.info(f"🎨 Bambu Studio detectado: {BAMBU_STUDIO_PATH}"
                f"{' (flatpak)' if BAMBU_ES_FLATPAK else ''}")
else:
    logger.warning("⚠️  Bambu Studio no detectado automáticamente.")
    logger.warning("   Los STL se guardarán y se intentará abrirlos con la app por defecto (.stl).")
    logger.warning("   Para forzar Bambu Studio, edita BAMBU_STUDIO_PATH_MANUAL en este archivo.")
    logger.info(f"   SO detectado: {platform.system()}")


@app.get("/")
async def health_check():
    """Endpoint de health check"""
    return {
        "status": "ok",
        "server": "STL Transfer Server",
        "version": "1.0.0",
        "stl_folder": str(STL_FOLDER),
        "platform": platform.system(),
        "bambu_studio": BAMBU_STUDIO_PATH if BAMBU_STUDIO_PATH else "no detectado (usa app por defecto)"
    }


@app.post("/upload-stl")
async def upload_stl(file: UploadFile = File(...), object_name: str = Form(...)):
    """
    Endpoint principal para recibir STL de Quest.

    Parámetros:
    - file: Archivo STL (binary)
    - object_name: Nombre del objeto (sin espacios, ej: "silla_1")

    Responde con:
    {
        "status": "success|error",
        "message": "Descripción del resultado",
        "file": "Ruta completa del archivo guardado"
    }
    """

    try:
        # Validar que el archivo no esté vacío
        content = await file.read()
        if len(content) == 0:
            raise HTTPException(status_code=400, detail="Archivo vacío recibido")

        # Sanitizar nombre del objeto
        safe_name = object_name.replace(" ", "_").replace("/", "_").replace("\\", "_")
        filename = f"{safe_name}.stl"
        filepath = STL_FOLDER / filename

        # Guardar archivo
        with open(filepath, "wb") as f:
            f.write(content)

        file_size_kb = len(content) / 1024
        logger.info(f"✅ STL guardado: {filepath} ({file_size_kb:.1f}KB)")

        # Abrir el archivo (Bambu Studio si se detectó, o la app por defecto del sistema)
        launch_success, launch_message = lanzar_archivo(str(filepath))
        if launch_success:
            logger.info(f"🚀 {launch_message}")
        else:
            logger.warning(f"⚠️  {launch_message} (pero el STL se guardó correctamente)")

        return JSONResponse({
            "status": "success",
            "message": f"STL recibido ({file_size_kb:.1f}KB)",
            "file": str(filepath),
            "bambu_studio_launched": launch_success,
            "launch_message": launch_message,
            "timestamp": datetime.now().isoformat()
        })

    except Exception as e:
        error_msg = f"Error procesando STL: {str(e)}"
        logger.error(f"❌ {error_msg}")
        return JSONResponse({
            "status": "error",
            "message": error_msg,
            "timestamp": datetime.now().isoformat()
        }, status_code=500)


@app.post("/test-connection")
async def test_connection():
    """Endpoint para testear conexión desde Quest"""
    return JSONResponse({
        "status": "connected",
        "message": "Conexión exitosa con servidor STL",
        "server_ready": True,
        "stl_folder_exists": STL_FOLDER.exists(),
        "bambu_studio_found": bambu_disponible(),
        "bambu_studio_path": BAMBU_STUDIO_PATH if BAMBU_STUDIO_PATH else None,
        "platform": platform.system()
    })


# ===== MAIN =====

if __name__ == "__main__":
    import uvicorn

    print()
    print("=" * 60)
    print("🟢 STL Transfer Server")
    print("=" * 60)
    print(f"📡 Escuchando en: http://0.0.0.0:{SERVER_PORT}")
    print(f"📁 Guardando STLs en: {STL_FOLDER}")
    if BAMBU_STUDIO_PATH:
        print(f"🎨 Bambu Studio: {BAMBU_STUDIO_PATH}{' (flatpak)' if BAMBU_ES_FLATPAK else ''}")
    else:
        print(f"🎨 Bambu Studio: NO detectado -> se usará la app por defecto para .stl")
    print(f"🖥️  SO: {platform.system()}")
    print("=" * 60)
    print()

    # Verificaciones
    if BAMBU_STUDIO_PATH is None:
        print("⚠️  ADVERTENCIA: Bambu Studio no se detectó automáticamente.")
        print("   Los STL se guardarán y se intentarán abrir con la app por defecto para .stl.")
        print("   Si querés forzar Bambu Studio, edita la línea BAMBU_STUDIO_PATH_MANUAL arriba.")
        print()

    # Iniciar servidor
    uvicorn.run(app, host="0.0.0.0", port=SERVER_PORT)
