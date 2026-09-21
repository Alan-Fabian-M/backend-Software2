# ✅ Informe de Funcionamiento — Backend de Visión (InmobiliariaVR)

> **Fecha:** 21 de septiembre de 2026
> **Rama:** `feature/entrenamiento-dataset`
> **Modelo en producción:** `YOLO/best_floorplancad.pt` (FloorPlanCAD, 16 clases)

Este informe documenta que el backend de visión artificial **funciona de punta a
punta**: recibe la foto de un plano por HTTP, corre los tres canales de percepción
(YOLO + OpenCV + OCR) y devuelve un Scene Graph JSON válido que Unity consume.

---

## 1. Estado del servicio

| Verificación | Resultado |
| :--- | :---: |
| Servidor FastAPI (uvicorn) levanta sin errores | ✅ |
| `GET /api/v1/health` responde `200` | ✅ |
| Swagger UI operativa en `/docs` | ✅ |
| Modelo `best_floorplancad.pt` carga (task `detect`, 16 clases) | ✅ |
| Tesseract OCR detectado (v5.5.3) | ✅ |
| Suite de tests (`pytest`) | ✅ 31 passed |

---

## 2. Prueba end-to-end vía HTTP (`POST /api/v1/compilar-sala`)

Se subieron imágenes reales por HTTP (campo `archivo`, el que usa el controller
de Unity). Todas respondieron `200 OK` con Scene Graph JSON válido.

| Imagen | Estilo | HTTP | Sala (escala) | Conf. escala | Muebles |
| :--- | :--- | :---: | :--- | :---: | :---: |
| `croquis_test.png` | CAD multi-unidad | 200 | 2.2 × 2.03 m | 0.5 (mm) | 15 |
| `cuarto 3x3` | Esquemático | 200 | 2.85 × 3.51 m | 0.5 (mm) | 1 |
| `simplee.jpg` | Ilustrado | 200 | 4.25 × 5.12 m | **0.7** (con unidad) | 1 |
| `colorful_3133` (CubiCasa val) | CAD | 200 | 4.0 × 4.0 m | 0.0 (sin cotas) | 2 |

### Ejemplo de respuesta (`colorful_3133`, verificada en Swagger UI)

- `Code 200`, `content-type: application/json`, `server: uvicorn`.
- 4 muros (`muro_norte/sur/este/oeste`) + 2 puertas dobles (conf. 0.90 y 0.78).
- El JSON de `scene_elements` es exactamente el formato que consume
  `SceneGenerator.cs` en Unity.

---

## 3. Los tres canales de percepción

| Canal | Función | Estado |
| :--- | :--- | :---: |
| **YOLO** (`furniture_detector`) | Detecta muebles/puertas/ventanas | ✅ Excelente en CAD (15 muebles); limitado en estilos no-CAD (ver §4) |
| **OpenCV** (`room_extractor`) | Contorno de la sala → 4 muros | ✅ Detecta habitación real (no la imagen entera) |
| **OCR** (`scale_detector`) | Lee cotas → escala px→metros | ✅ Con unidad (`4,25 m`→0.7) y sin unidad (`2200`→0.5, asumido mm) |

Mejoras aplicadas en esta rama para soportar planos CAD:
- Binarización *polarity-aware* (texto claro sobre fondo oscuro) + filtro de
  trazos de dibujo antes del OCR.
- Detección de sala por espacio encerrado (evita tomar toda la imagen).
- Fix de muro duplicado (se excluye `wall`→`muro` en YOLO).

---

## 4. Limitación conocida (pendiente de fine-tuning)

El modelo actual detecta muebles **muy bien en planos CAD**, pero **no reconoce
mobiliario en dibujos ilustrados/esquemáticos** (out-of-distribution): en esos
estilos solo detecta las puertas. Las detecciones de "cama" de baja confianza en
esos casos son *fantasmas* sobre texto/adornos, no el mueble real.

**Solución planificada:** fine-tuning por transfer learning con imágenes de
estilos variados. El código (Fases 3-5) ya está listo y probado en
`training/build_mixed_dataset.py`, `training/train_yolo.py` (flags de fine-tuning)
y `training/validate_finetune.py`. Guía completa en `training/README_TRAINING.md`
(sección 5). Falta el paso manual: recolectar y etiquetar ~150 imágenes nuevas.

---

## 5. Conclusión

**El backend funciona correctamente.** Sirve de forma confiable planos estilo CAD
(el caso principal), con los tres canales operativos y el endpoint respondiendo
`200` con JSON válido para Unity. La detección de muebles en estilos no-CAD es la
única mejora pendiente, y su pipeline de entrenamiento ya está codificado a la
espera del dataset.

---

*Informe generado el 21/09/2026 — InmobiliariaVR Vision Backend v0.2.0*
