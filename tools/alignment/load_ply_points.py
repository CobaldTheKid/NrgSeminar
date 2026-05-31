from __future__ import annotations

import numpy as np
from plyfile import PlyData


def load_ply_points(path: str, opacity_threshold: float | None = None, max_points: int | None = None, rng_seed: int = 12345) -> np.ndarray:
    ply = PlyData.read(path)
    vertex = ply["vertex"]

    points = np.stack([
        np.asarray(vertex["x"], dtype=np.float64),
        np.asarray(vertex["y"], dtype=np.float64),
        np.asarray(vertex["z"], dtype=np.float64),
    ], axis=1)

    if opacity_threshold is not None and "opacity" in vertex.data.dtype.names:
        opacity = np.asarray(vertex["opacity"], dtype=np.float64)
        points = points[opacity >= opacity_threshold]

    if max_points is not None and len(points) > max_points:
        rng = np.random.default_rng(rng_seed)
        indices = rng.choice(len(points), size=max_points, replace=False)
        points = points[indices]

    return points
