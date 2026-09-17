"""Scale detection via OCR ("the reader" of the pipeline).

Ported from `SpatialSceneCompiler/scene_compiler.py`. One important fix from
that prototype: it hardcoded a Windows path to the Tesseract binary
(`C:\\Program Files\\Tesseract-OCR\\tesseract.exe`), which would silently
fail on this Linux dev machine. This version relies on the `tesseract`
binary being available on PATH instead -- install it via the system package
manager (e.g. `sudo dnf install tesseract` on Fedora, `sudo apt install
tesseract-ocr` on Debian/Ubuntu) before running the OCR-dependent endpoint.
"""

from __future__ import annotations

import re
from dataclasses import dataclass

import cv2
import numpy as np
import pytesseract

_DIMENSION_PATTERN = re.compile(r"(\d+[.,]?\d*)\s*(m|cm|mts?)\b", re.IGNORECASE)


@dataclass
class ScaleResult:
    pixels_per_meter: float
    confidence: float
    source: str


class ScaleDetector:
    @staticmethod
    def detect(image_bgr: np.ndarray, room_width_px: float) -> ScaleResult:
        """Look for a number+unit ('4.20m', '420cm') written on the sketch and
        cross it with the room's pixel width to get pixels_per_meter.
        """
        gray = cv2.cvtColor(image_bgr, cv2.COLOR_BGR2GRAY)
        _, binary = cv2.threshold(gray, 150, 255, cv2.THRESH_BINARY)

        try:
            text = pytesseract.image_to_string(binary, config="--psm 11")
        except pytesseract.TesseractNotFoundError:
            return ScaleResult(
                pixels_per_meter=0.0,
                confidence=0.0,
                source="tesseract binary not found on PATH -- install it to enable OCR scale detection",
            )

        matches = _DIMENSION_PATTERN.findall(text)
        if not matches or not room_width_px:
            return ScaleResult(
                pixels_per_meter=0.0,
                confidence=0.0,
                source="no readable dimension text found in the sketch",
            )

        value_str, unit = matches[0]
        value = float(value_str.replace(",", "."))
        meters = value if unit.lower().startswith("m") and unit.lower() != "cm" else value / 100.0

        if meters <= 0:
            return ScaleResult(pixels_per_meter=0.0, confidence=0.0, source="invalid dimension value")

        pixels_per_meter = room_width_px / meters
        return ScaleResult(
            pixels_per_meter=round(pixels_per_meter, 2),
            confidence=0.7,
            source=f"OCR:{value_str}{unit} vs room_width:{room_width_px}px",
        )
