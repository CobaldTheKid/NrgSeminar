from __future__ import annotations

import json
from datetime import datetime, timezone
from pathlib import Path
from typing import Iterable

import numpy as np


def rotation_matrix_to_quaternion(matrix: np.ndarray) -> np.ndarray:
    m = matrix
    trace = float(m[0, 0] + m[1, 1] + m[2, 2])
    if trace > 0.0:
        s = 0.5 / np.sqrt(trace + 1.0)
        w = 0.25 / s
        x = (m[2, 1] - m[1, 2]) * s
        y = (m[0, 2] - m[2, 0]) * s
        z = (m[1, 0] - m[0, 1]) * s
    else:
        if m[0, 0] > m[1, 1] and m[0, 0] > m[2, 2]:
            s = 2.0 * np.sqrt(1.0 + m[0, 0] - m[1, 1] - m[2, 2])
            w = (m[2, 1] - m[1, 2]) / s
            x = 0.25 * s
            y = (m[0, 1] + m[1, 0]) / s
            z = (m[0, 2] + m[2, 0]) / s
        elif m[1, 1] > m[2, 2]:
            s = 2.0 * np.sqrt(1.0 + m[1, 1] - m[0, 0] - m[2, 2])
            w = (m[0, 2] - m[2, 0]) / s
            x = (m[0, 1] + m[1, 0]) / s
            y = 0.25 * s
            z = (m[1, 2] + m[2, 1]) / s
        else:
            s = 2.0 * np.sqrt(1.0 + m[2, 2] - m[0, 0] - m[1, 1])
            w = (m[1, 0] - m[0, 1]) / s
            x = (m[0, 2] + m[2, 0]) / s
            y = (m[1, 2] + m[2, 1]) / s
            z = 0.25 * s
    quat = np.array([x, y, z, w], dtype=np.float64)
    quat /= np.linalg.norm(quat)
    return quat


def write_alignment_json(
    output_path: str | Path,
    translation: Iterable[float],
    rotation_matrix: np.ndarray,
    scale: float,
    source_mesh_points_file: str,
    source_ply_file: str,
    fitness: float,
    rmse: float,
) -> None:
    rotation_matrix = np.asarray(rotation_matrix, dtype=np.float64).reshape(3, 3)
    payload = {
        "translation": [float(v) for v in translation],
        "rotation_matrix": [[float(v) for v in row] for row in rotation_matrix],
        "rotation_quaternion": [float(v) for v in rotation_matrix_to_quaternion(rotation_matrix)],
        "scale": float(scale),
        "source_mesh_points_file": str(source_mesh_points_file),
        "source_ply_file": str(source_ply_file),
        "fitness": float(fitness),
        "rmse": float(rmse),
        "created_utc": datetime.now(timezone.utc).isoformat(),
    }
    Path(output_path).write_text(json.dumps(payload, indent=2), encoding="utf-8")
