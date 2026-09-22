"""
Servidor local del Compilador de Escenas Espaciales.

Corre 100% en localhost, sin internet (mismo criterio que Ollama en el resto
del proyecto) — cumple el requisito de "IA local sin conexion a servidor externo".

Uso:
    venv/Scripts/python.exe -m uvicorn server:app --host 0.0.0.0 --port 8765

Unity (o cualquier cliente) manda un POST multipart con la imagen del croquis a
/compilar-sala y recibe el Scene Graph en JSON.
"""

import cv2
import numpy as np
from fastapi import FastAPI, File, UploadFile
from fastapi.responses import JSONResponse

from scene_compiler import compilar_escena

app = FastAPI(title="Spatial Scene Compiler (InmobiliariaVR)")


@app.get("/salud")
def salud():
    return {"status": "ok", "mensaje": "Servidor local corriendo, sin conexion a internet."}


@app.post("/compilar-sala")
async def compilar_sala(archivo: UploadFile = File(...)):
    contenido = await archivo.read()
    arreglo = np.frombuffer(contenido, dtype=np.uint8)
    imagen_bgr = cv2.imdecode(arreglo, cv2.IMREAD_COLOR)

    if imagen_bgr is None:
        return JSONResponse(status_code=400, content={"error": "No se pudo leer la imagen enviada."})

    scene_graph = compilar_escena(imagen_bgr)
    return scene_graph
