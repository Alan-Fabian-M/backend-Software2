# ⚠️ Superseded — ver `backend/`

**Este prototipo quedó reemplazado el 2026-09-16 por el servicio en `backend/`
(`POST /api/v1/compilar-sala`).**

Motivo: este prototipo devolvía un schema JSON (`class_label`/`prefab_id`/
`transform.position`/`properties.color_hex`) que quedó incompatible con el
pipeline nuevo de generación dinámica de escenas en Unity (`SceneGenerator.cs`
+ `PrefabDatabase` + Furniture Mega Pack). En vez de mantener dos backends
con dos schemas distintos, la lógica de detección y OCR de este prototipo
(YOLO + OpenCV + Tesseract, ya validada de punta a punta con una foto real
el 2026-09-10) fue **portada a `backend/app/services/`**, con el mismo
comportamiento pero devolviendo el schema que `SceneGenerator.cs` espera.

Ver `backend/README.md` (endpoint `/api/v1/compilar-sala`) y, en el proyecto
de Claude "Software2 - Parcial1", el doc `dataset-yolo-obb-y-conflicto-schema.md`
para el detalle de esta decisión.

`CroquisSceneCompilerController.cs` en Unity ya apunta al nuevo backend.

Esta carpeta se deja como referencia histórica del prototipo original; no
hace falta correr `server.py` de acá para nada nuevo.

---

# Spatial Scene Compiler (prototipo original, histórico)

Servidor local (Python) que lee una foto de un croquis y devuelve un Scene Graph en JSON con la posición de los muebles, para que Unity arme la habitación automáticamente.

Arquitectura propuesta por el equipo: la IA se usa solo como "los ojos" (detección de objetos), y todo lo demás (escala, coordenadas) es matemática determinista — ver [`scene_graph_schema.md`](scene_graph_schema.md) para el detalle de los 4 pilares y los riesgos técnicos identificados.

## Piezas

- **YOLOv8n** (`ultralytics`) — detecta muebles (sofá, mesa, tv, cama, silla, etc.) en la foto. ~0.24s por imagen en CPU, sin GPU dedicada.
- **OpenCV** — encuentra el contorno de la sala (ancho/alto en píxeles).
- **Tesseract OCR** (`pytesseract`) — busca texto de dimensiones escrito en el croquis (ej. "4.20m") para calcular la escala real.

Todo corre en `localhost`, sin conexión a internet (mismo criterio que Ollama en el resto del proyecto).

## Instalación

1. Python 3.11+.
2. Instalar [Tesseract OCR para Windows](https://github.com/UB-Mannheim/tesseract/wiki) (o `winget install --id UB-Mannheim.TesseractOCR -e`). Si se instala en una ruta distinta a `C:\Program Files\Tesseract-OCR\tesseract.exe`, ajustar `scene_compiler.py`.
3. Crear el entorno virtual e instalar dependencias:

```bash
python -m venv venv
venv/Scripts/python.exe -m pip install -r requirements.txt
```

## Uso

```bash
venv/Scripts/python.exe -m uvicorn server:app --host 127.0.0.1 --port 8765
```

Unity (`CroquisSceneCompilerController.cs`, pestaña "Croquis" en `Sala_MVP`) manda la foto por HTTP a `http://localhost:8765/compilar-sala` y recibe el JSON.

## Limitación conocida

No hay modelo YOLO-OBB (orientación) público con clases de muebles — la rotación de cada mueble siempre viene en 0°, se ajusta a mano en VR con el menú de mover/rotar que ya existe en el proyecto.

## Pendiente

Probar con un croquis real dibujado a mano por el equipo (la letra manuscrita es el punto más incierto del OCR, todavía no validado — se probó solo con una foto de stock).
