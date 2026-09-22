# Esquema del Scene Graph (JSON) — v2

> Manual de instrucciones que el servidor Python genera y que Unity consume para armar la habitación. Sistema métrico (metros). **v2**: estructura propuesta por Alan (más explícita que la v1 original — agrega `prefab_id`, `room_info.bounds`, `properties` y deja la puerta abierta a `topology.parent_id`).

```json
{
  "metadata": {
    "project_name": "InmobiliariaVR",
    "version": "2.0",
    "units": "meters",
    "image_resolution": { "width": 800, "height": 800 },
    "scale_factor_px_to_m": 0.011111,
    "processing_time_ms": 240,
    "escala_confianza": 0.0,
    "escala_fuente": "no se encontro texto de dimension legible en el croquis"
  },
  "room_info": {
    "type": "detectado_desde_croquis",
    "bounds": { "width_m": 4.0, "length_m": 4.0, "height_m": 2.5 }
  },
  "scene_elements": [
    {
      "id": "obj_000",
      "class_label": "cama",
      "prefab_id": "cama",
      "confidence": 0.7,
      "transform": {
        "position": { "x": 3.18, "y": 0.0, "z": 1.68 },
        "rotation": { "x": 0.0, "y": 0.0, "z": 0.0 },
        "scale": { "x": 1.0, "y": 1.0, "z": 1.0 }
      },
      "properties": {
        "color_hex": "#B16B4A",
        "material_type": "tela"
      }
    }
  ]
}
```

## Qué genera cada pieza

- **`metadata`**: info general del procesamiento (resolución de la imagen, escala píxeles→metros, tiempo que tardó, y qué tan confiable fue la lectura de escala vía OCR).
- **`room_info.bounds`**: el contorno general de la sala (OpenCV) — **un solo rectángulo**, no cada pared por separado (ver limitación abajo).
- **`scene_elements`**: un elemento por cada mueble que detectó YOLO.
  - **`class_label`** / **`prefab_id`**: hoy son iguales (coinciden con el nombre del GameObject ya armado en Unity: `Sofa`, `Mesa`, `TV`, `Cama`, `Silla`). Se separan como campos distintos a propósito, para el día que se necesite un catálogo de prefabs más específico (ej. `sofa_3_cuerpos` vs `sofa_individual`) sin tener que tocar la clase de detección.
  - **`confidence`**: qué tan segura está la detección de YOLO (0 a 1). Unity podría usar esto para ignorar detecciones dudosas (`< 0.5`) en vez de ubicarlas igual.
  - **`transform.rotation`**: siempre `0,0,0` — ver limitación de OBB abajo.
  - **`properties.color_hex`**: color promedio de los píxeles del mueble detectado (no es IA, es un promedio simple del recorte) — Unity lo aplica directo al material.
  - **`properties.material_type`**: heurística por categoría (`sofa`→tela, `mesa`→madera, etc.), no una detección real.

## Riesgos técnicos identificados (algunos ya confirmados, otros pendientes)

1. **Orientación de muebles (OBB) — confirmado, sin resolver:** los modelos YOLO-OBB públicos no tienen clases de muebles entrenadas (están pensados para aviones/barcos/vehículos en imágenes satelitales). No hay forma de sacar `rotation` real de YOLO hoy. El usuario ajusta la rotación a mano en VR con el menú de mover/rotar que ya existe (`FurnitureManipulatorVR`) — no bloquea el flujo, solo lo hace menos automático.
2. **OCR en croquis a mano — sin validar con un croquis real:** Tesseract funciona bien con texto impreso; con letra manuscrita la tasa de acierto es incierta. Todavía no se probó con un boceto real dibujado por el equipo, solo con fotos de stock (que no tienen texto de dimensiones).
3. **Detección de paredes/ventanas individuales — NO implementado, fase 2 aparte:** el JSON de Alan contempla `class_label: "wall"` / `"window"` como `scene_elements` con su propia posición y rotación. Hoy **no lo generamos** — YOLO no tiene clases COCO para elementos arquitectónicos, y `room_info.bounds` da solo el contorno general de toda la sala, no cada pared. Implementarlo necesitaría detección de líneas por segmento individual (Hough Transform más fino) y algo aparte para ventanas (sin clase COCO tampoco). Es más trabajo y más incertidumbre que la detección de muebles, que ya está validada.
