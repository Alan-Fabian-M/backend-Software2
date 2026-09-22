# STL Transfer Server

Servidor simple para recibir archivos STL de Quest 3 por WiFi y lanzar automáticamente Bambu Studio.

## 🚀 Instalación Rápida

### 1. Instalar dependencias
```bash
pip3 install -r requirements.txt
```

### 2. Ejecutar servidor

**Linux/Mac:**
```bash
chmod +x start_server.sh
./start_server.sh
```

**O directamente:**
```bash
python3 stl_receiver_service.py
```

Deberías ver:
```
============================================================
🟢 STL Transfer Server
============================================================
📡 Escuchando en: http://0.0.0.0:5000
📁 Guardando STLs en: /home/alan/Documents/InmobiliariaVR_Exports/STL_para_slicear
🎨 Bambu Studio: /opt/Bambu Studio/BambuStudio (ajusta según SO)
🖥️  SO: Linux
============================================================
```

## 📝 Configuración

Si necesitas cambiar algo, edita estas líneas en `stl_receiver_service.py`:

```python
# Carpeta donde guardar STLs
STL_FOLDER = Path.home() / "Documents" / "InmobiliariaVR_Exports" / "STL_para_slicear"

# Ruta a Bambu Studio (se detecta automáticamente por SO)
BAMBU_STUDIO_PATH = "/opt/Bambu Studio/BambuStudio"  # Linux
BAMBU_STUDIO_PATH = "/Applications/Bambu Studio.app/Contents/MacOS/BambuStudio"  # macOS
BAMBU_STUDIO_PATH = r"C:\Program Files\Bambu Studio\BambuStudio.exe"  # Windows

# Puerto del servidor
SERVER_PORT = 5000
```

## 🧪 Testear desde tu máquina

### Opción A: Usando curl
```bash
curl -F "file=@modelo.stl" -F "object_name=silla_1" http://localhost:5000/upload-stl
```

### Opción B: Test de conexión
```bash
curl http://localhost:5000/
```

## 📁 Estructura de carpetas

Una vez que ejecutes el servidor, se creará automáticamente:
```
~/Documents/
└── InmobiliariaVR_Exports/
    └── STL_para_slicear/
        ├── silla_1.stl        (STLs que envía Quest)
        ├── mesa_1.stl
        └── ...
```

## 🔧 Endpoints disponibles

### `GET /`
Health check - verifica que el servidor esté activo

```bash
curl http://localhost:5000/
```

Respuesta:
```json
{
  "status": "ok",
  "server": "STL Transfer Server",
  "version": "1.0.0",
  "stl_folder": "...",
  "platform": "Linux"
}
```

### `POST /upload-stl`
Recibe STL y lanza Bambu Studio

**Parámetros:**
- `file`: Archivo STL (binary)
- `object_name`: Nombre del objeto (ej: "silla_1")

**Respuesta exitosa (200):**
```json
{
  "status": "success",
  "message": "STL recibido (245.3KB)",
  "file": "/home/alan/Documents/InmobiliariaVR_Exports/STL_para_slicear/silla_1.stl",
  "bambu_studio_launched": true,
  "launch_message": "Bambu Studio lanzado con: silla_1.stl",
  "timestamp": "2026-09-19T..."
}
```

### `POST /test-connection`
Verifica que el servidor y Bambu Studio están listos

```bash
curl -X POST http://localhost:5000/test-connection
```

Respuesta:
```json
{
  "status": "connected",
  "message": "Conexión exitosa con servidor STL",
  "server_ready": true,
  "stl_folder_exists": true,
  "bambu_studio_found": true,
  "platform": "Linux"
}
```

## 📋 Logs

El servidor crea un archivo `stl_transfer_server.log` con:
- Todos los STLs recibidos
- Errores
- Intentos de lanzar Bambu Studio
- Timestamps

```bash
# Ver logs en tiempo real
tail -f stl_transfer_server.log
```

## ⚠️ Troubleshooting

### "Bambu Studio no encontrado"
El servidor está guardando STLs correctamente pero no puede lanzar Bambu Studio.

**Soluciones:**
1. Verifica que Bambu Studio está instalado
2. Encuentra la ruta correcta:
   - Linux: `which BambuStudio` o `locate BambuStudio`
   - macOS: `/Applications/Bambu Studio.app/Contents/MacOS/BambuStudio`
   - Windows: `C:\Program Files\Bambu Studio\BambuStudio.exe`
3. Edita `BAMBU_STUDIO_PATH` en `stl_receiver_service.py`

### "Error de conexión desde Quest"
El servidor no está escuchando en el puerto correcto.

**Soluciones:**
1. Verifica que el servidor está corriendo
2. Obtén tu IP local: `python3 get_server_ip.py`
3. Verifica firewall: puede estar bloqueando puerto 5000
   - Opción: cambiar puerto en `SERVER_PORT = 5000` a otro número (ej: 8000)

## 🔄 Flujo Completo

```
Quest 3 (VR)
   ↓
User presiona "Enviar a Bambu Lab"
   ↓
STLTransferClient.cs exporta STL
   ↓
POST http://IP_PC:5000/upload-stl
   ↓
[Este Servidor]
   ├─ Guarda STL en ~/Documents/InmobiliariaVR_Exports/STL_para_slicear/
   └─ Lanza: BambuStudio modelo.stl
   ↓
Bambu Studio se abre con el STL
   ↓
User presiona "Print"
   ↓
MQTT/FTPS → Impresora (código existente en Quest)
   ↓
🖨️ ¡A imprimir!
```

## 📞 Soporte

Si algo no funciona:
1. Revisa el archivo `stl_transfer_server.log`
2. Ejecuta `/test-connection` desde tu navegador para verificar estado
3. Verifica que Quest está en la misma red WiFi
