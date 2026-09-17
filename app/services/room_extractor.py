"""Room contour extraction ("the compass and ruler" of the pipeline).

Ported from the original `SpatialSceneCompiler/scene_compiler.py` prototype.
Same pragmatic simplification as before: instead of reconstructing every wall
as an individual segment (fragile against hand-drawn, non-orthogonal lines),
we take the single largest contour in the sketch as the room's outer bounds.

New in this version: `build_wall_elements()` turns those bounds into four
`SceneElement`-shaped wall dicts (muro_norte/sur/este/oeste), matching the
exact style already used in `Assets/Resources/ScenePresets/preset_*.json`.
This means a compiled photo produces a full walled room (not just floating
furniture), consistent with what the presets already demonstrate in Unity.
"""

from __future__ import annotations

from dataclasses import dataclass
from typing import Optional, Tuple

import cv2
import numpy as np


@dataclass
class RoomContour:
    origin_px: Tuple[int, int]
    width_px: int
    height_px: int


class RoomExtractor:
    @staticmethod
    def detect_bounds(image_bgr: np.ndarray) -> Optional[RoomContour]:
        gray = cv2.cvtColor(image_bgr, cv2.COLOR_BGR2GRAY)
        blurred = cv2.GaussianBlur(gray, (5, 5), 0)
        edges = cv2.Canny(blurred, 40, 120)
        edges = cv2.dilate(edges, np.ones((3, 3), np.uint8), iterations=2)

        contours, _ = cv2.findContours(edges, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)
        if not contours:
            return None

        largest = max(contours, key=cv2.contourArea)
        x, y, w, h = cv2.boundingRect(largest)
        return RoomContour(origin_px=(x, y), width_px=w, height_px=h)

    @staticmethod
    def build_wall_elements(width_m: float, depth_m: float, height_m: float, wall_thickness_m: float = 0.2,
                             wall_color: str = "#FFFFFF") -> list:
        """Return 4 wall scene_elements (type='muro') framing a width_m x depth_m room,
        in the same shape/convention as Assets/Resources/ScenePresets/preset_*.json.
        """
        half_d = depth_m / 2.0
        material = {"type": "concrete", "color": wall_color}

        return [
            {
                "id": "muro_norte",
                "type": "muro",
                "position": {"x": 0, "y": 0, "z": depth_m},
                "rotation": {"x": 0, "y": 0, "z": 0},
                "scale": {"x": width_m, "y": height_m, "z": wall_thickness_m},
                "material": material,
            },
            {
                "id": "muro_sur",
                "type": "muro",
                "position": {"x": 0, "y": 0, "z": 0},
                "rotation": {"x": 0, "y": 0, "z": 0},
                "scale": {"x": width_m, "y": height_m, "z": wall_thickness_m},
                "material": material,
            },
            {
                "id": "muro_este",
                "type": "muro",
                "position": {"x": width_m, "y": 0, "z": half_d},
                "rotation": {"x": 0, "y": 0, "z": 0},
                "scale": {"x": wall_thickness_m, "y": height_m, "z": depth_m},
                "material": material,
            },
            {
                "id": "muro_oeste",
                "type": "muro",
                "position": {"x": 0, "y": 0, "z": half_d},
                "rotation": {"x": 0, "y": 0, "z": 0},
                "scale": {"x": wall_thickness_m, "y": height_m, "z": depth_m},
                "material": material,
            },
        ]
