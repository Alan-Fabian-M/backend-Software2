"""
Compilador de Escenas Espaciales — nucleo del pipeline.

Flujo: imagen del croquis -> (YOLOv8 + OpenCV + Tesseract OCR, todo local/offline)
-> Scene Graph en JSON (ver scene_graph_schema.md) que Unity consume para armar la sala.

Cada sensor es intencionalmente chico y especializado (en vez de un solo modelo de
vision-lenguaje pesado) porque en pruebas un modelo de vision general (moondream)
tardo 3+ minutos por imagen en esta CPU sin GPU dedicada. YOLOv8n corre en ~0.24s.

Formato de salida (schema v2, propuesto por Alan): ver scene_graph_schema.md.
Cubre SOLO muebles (deteccion via YOLO/COCO) -- paredes y ventanas quedan
fuera de este schema a proposito: no hay clase COCO para "wall"/"window", asi
que detectarlos por separado (con posicion/rotacion propia) es una fase 2
aparte con mas riesgo tecnico (deteccion de lineas por pared individual, sin
clase COCO para ventanas). Por ahora "sala.bounds" da el contorno general
nomas (un solo rectangulo), no cada pared.
"""

import re
import time

import cv2
import numpy as np
import pytesseract
from ultralytics import YOLO

# En Windows, pytesseract necesita la ruta al binario de Tesseract instalado
pytesseract.pytesseract.tesseract_cmd = r"C:\Program Files\Tesseract-OCR\tesseract.exe"

# Mapeo de clases COCO (lo que YOLO reconoce) a los "class_label"/"prefab_id"
# que usa Unity. prefab_id == class_label por ahora (coincide con el nombre
# de los GameObjects ya armados en las escenas: Sofa, Mesa, TV, Cama, etc.)
CLASE_YOLO_A_TIPO = {
    "couch": "sofa",
    "chair": "silla",
    "bed": "cama",
    "dining table": "mesa",
    "tv": "tv",
    "potted plant": "planta",
    "refrigerator": "heladera",
    "sink": "lavamanos",
    "toilet": "inodoro",
}

# Heuristica simple de material tipico por tipo de mueble, para el campo
# properties.material_type -- no viene de un clasificador real, es una
# convencion basica por categoria (mejora cosmetica, no una deteccion de IA).
MATERIAL_TIPICO = {
    "sofa": "tela",
    "silla": "madera",
    "cama": "tela",
    "mesa": "madera",
    "tv": "plastico",
    "planta": "ceramica",
    "heladera": "metal",
    "lavamanos": "ceramica",
    "inodoro": "ceramica",
}

CONFIANZA_MINIMA_MUEBLE = 0.35

_modelo_yolo = None


def _get_modelo():
    global _modelo_yolo
    if _modelo_yolo is None:
        _modelo_yolo = YOLO("yolov8n.pt")
    return _modelo_yolo


def _color_promedio_hex(imagen_bgr, bbox):
    """Color dominante aproximado (promedio de pixeles) del recorte del mueble detectado."""
    x1, y1, x2, y2 = [int(v) for v in bbox]
    x1, y1 = max(x1, 0), max(y1, 0)
    x2, y2 = min(x2, imagen_bgr.shape[1]), min(y2, imagen_bgr.shape[0])
    if x2 <= x1 or y2 <= y1:
        return "#CCCCCC"

    recorte = imagen_bgr[y1:y2, x1:x2]
    b, g, r = [int(round(c)) for c in cv2.mean(recorte)[:3]]
    return "#{:02X}{:02X}{:02X}".format(r, g, b)


def detectar_muebles(imagen_bgr):
    """YOLO como 'los ojos': encuentra que muebles hay, donde estan y su color aproximado."""
    modelo = _get_modelo()
    resultados = modelo(imagen_bgr, verbose=False)

    muebles = []
    for r in resultados:
        for box in r.boxes:
            clase_yolo = modelo.names[int(box.cls[0])]
            confianza = float(box.conf[0])
            if clase_yolo not in CLASE_YOLO_A_TIPO or confianza < CONFIANZA_MINIMA_MUEBLE:
                continue

            x1, y1, x2, y2 = [float(v) for v in box.xyxy[0]]
            centro_px = ((x1 + x2) / 2.0, (y1 + y2) / 2.0)
            tipo = CLASE_YOLO_A_TIPO[clase_yolo]

            muebles.append({
                "tipo": tipo,
                "clase_yolo": clase_yolo,
                "centro_px": centro_px,
                "confianza_deteccion": round(confianza, 2),
                "color_hex": _color_promedio_hex(imagen_bgr, (x1, y1, x2, y2)),
                "material_type": MATERIAL_TIPICO.get(tipo, "desconocido"),
            })

    return muebles


def detectar_contorno_sala(imagen_bgr):
    """
    OpenCV busca el contorno principal del croquis (las paredes) para saber
    el ancho/alto de la sala en pixeles. Simplificacion pragmatica: en vez de
    reconstruir cada pared como segmento individual (fragil con lineas a mano
    alzada), se toma el rectangulo/contorno mas grande del dibujo.
    """
    gris = cv2.cvtColor(imagen_bgr, cv2.COLOR_BGR2GRAY)
    borroso = cv2.GaussianBlur(gris, (5, 5), 0)
    bordes = cv2.Canny(borroso, 40, 120)
    bordes = cv2.dilate(bordes, np.ones((3, 3), np.uint8), iterations=2)

    contornos, _ = cv2.findContours(bordes, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)
    if not contornos:
        return None

    contorno_mas_grande = max(contornos, key=cv2.contourArea)
    x, y, ancho_px, alto_px = cv2.boundingRect(contorno_mas_grande)

    return {
        "bbox_px": (x, y, ancho_px, alto_px),
        "ancho_px": ancho_px,
        "alto_px": alto_px,
    }


_PATRON_DIMENSION = re.compile(r"(\d+[.,]?\d*)\s*(m|cm|mts?)\b", re.IGNORECASE)


def detectar_escala(imagen_bgr, ancho_sala_px):
    """
    OCR: busca un numero con unidad ('4.20m', '420cm') escrito en el croquis
    y lo cruza con el ancho de la sala en pixeles para sacar pixeles_por_metro.
    Motor geometrico: esta es la division que convierte pixeles en metros reales.
    """
    gris = cv2.cvtColor(imagen_bgr, cv2.COLOR_BGR2GRAY)
    _, binaria = cv2.threshold(gris, 150, 255, cv2.THRESH_BINARY)

    texto = pytesseract.image_to_string(binaria, config="--psm 11")
    coincidencias = _PATRON_DIMENSION.findall(texto)

    if not coincidencias or not ancho_sala_px:
        return {
            # 0 en vez de null: mas facil de parsear desde JsonUtility en Unity.
            # confianza=0 es la señal real de "no se detecto escala", no este campo.
            "pixeles_por_metro": 0.0,
            "confianza": 0.0,
            "fuente": "no se encontro texto de dimension legible en el croquis",
        }

    valor_str, unidad = coincidencias[0]
    valor = float(valor_str.replace(",", "."))
    metros = valor if unidad.lower().startswith("m") and unidad.lower() != "cm" else valor / 100.0

    if metros <= 0:
        return {"pixeles_por_metro": 0.0, "confianza": 0.0, "fuente": "valor de dimension invalido"}

    pixeles_por_metro = ancho_sala_px / metros
    return {
        "pixeles_por_metro": round(pixeles_por_metro, 2),
        "confianza": 0.7,
        "fuente": f"OCR:{valor_str}{unidad} vs ancho_sala:{ancho_sala_px}px",
    }


ANCHO_SALA_DEFECTO_M = 4.0
ALTO_SALA_DEFECTO_M = 4.0
ALTO_TECHO_M = 2.5


def compilar_escena(imagen_bgr):
    """Punto de entrada: imagen -> Scene Graph JSON (dict de Python), schema v2."""
    t0 = time.time()

    alto_img_px, ancho_img_px = imagen_bgr.shape[:2]

    muebles_px = detectar_muebles(imagen_bgr)
    contorno = detectar_contorno_sala(imagen_bgr)

    if contorno:
        ancho_px, alto_px = contorno["ancho_px"], contorno["alto_px"]
        origen_px = contorno["bbox_px"][:2]
    else:
        ancho_px, alto_px = ancho_img_px, alto_img_px
        origen_px = (0, 0)

    escala = detectar_escala(imagen_bgr, ancho_px)

    if escala["pixeles_por_metro"]:
        px_por_m = escala["pixeles_por_metro"]
        ancho_m = round(ancho_px / px_por_m, 2)
        alto_m = round(alto_px / px_por_m, 2)
    else:
        # Sin lectura de escala confiable: no inventamos numeros raros, usamos
        # un tamano de sala por defecto razonable (mismo que Sala_MVP) y avisamos
        # con confianza 0 para que Unity pueda pedirle confirmacion al usuario.
        ancho_m, alto_m = ANCHO_SALA_DEFECTO_M, ALTO_SALA_DEFECTO_M
        px_por_m = ancho_px / ancho_m if ancho_px else 1.0

    scene_elements = []
    for i, m in enumerate(muebles_px):
        px, py = m["centro_px"]
        x_m = round((px - origen_px[0]) / px_por_m, 2)
        z_m = round((alto_px - (py - origen_px[1])) / px_por_m, 2)  # invierte Y de imagen a Z de Unity

        scene_elements.append({
            "id": f"obj_{i:03d}",
            "class_label": m["tipo"],
            "prefab_id": m["tipo"],  # coincide con el nombre de GameObject ya usado en Unity
            "confidence": m["confianza_deteccion"],
            "transform": {
                "position": {"x": x_m, "y": 0.0, "z": z_m},
                "rotation": {"x": 0.0, "y": 0.0, "z": 0.0},  # sin modelo OBB de muebles, se ajusta a mano en VR
                "scale": {"x": 1.0, "y": 1.0, "z": 1.0},
            },
            "properties": {
                "color_hex": m["color_hex"],
                "material_type": m["material_type"],
            },
        })

    tiempo_total_ms = round((time.time() - t0) * 1000)

    return {
        "metadata": {
            "project_name": "InmobiliariaVR",
            "version": "2.0",
            "units": "meters",
            "image_resolution": {"width": ancho_img_px, "height": alto_img_px},
            "scale_factor_px_to_m": round(1.0 / px_por_m, 6) if px_por_m else 0.0,
            "processing_time_ms": tiempo_total_ms,
            "escala_confianza": escala["confianza"],
            "escala_fuente": escala["fuente"],
        },
        "room_info": {
            "type": "detectado_desde_croquis",
            "bounds": {"width_m": ancho_m, "length_m": alto_m, "height_m": ALTO_TECHO_M},
        },
        "scene_elements": scene_elements,
    }
