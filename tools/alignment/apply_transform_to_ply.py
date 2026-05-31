from __future__ import annotations

import argparse
import json
from pathlib import Path

import numpy as np
from plyfile import PlyData, PlyElement


def read_transform(path: str):
    data = json.loads(Path(path).read_text(encoding="utf-8"))
    translation = np.asarray(data["translation"], dtype=np.float64)
    rotation = np.asarray(data["rotation_matrix"], dtype=np.float64)
    scale = float(data["scale"])
    return translation, rotation, scale


def main() -> None:
    parser = argparse.ArgumentParser(description="Apply alignment transform to PLY positions")
    parser.add_argument("--input", required=True)
    parser.add_argument("--alignment", required=True)
    parser.add_argument("--output", required=True)
    args = parser.parse_args()

    translation, rotation, scale = read_transform(args.alignment)
    ply = PlyData.read(args.input)
    vertex = ply["vertex"]
    x = np.asarray(vertex["x"], dtype=np.float64)
    y = np.asarray(vertex["y"], dtype=np.float64)
    z = np.asarray(vertex["z"], dtype=np.float64)
    points = np.stack([x, y, z], axis=1)
    transformed = (rotation @ (points * scale).T).T + translation

    vertex.data["x"] = transformed[:, 0]
    vertex.data["y"] = transformed[:, 1]
    vertex.data["z"] = transformed[:, 2]

    PlyData([PlyElement.describe(vertex.data, "vertex")], text=False).write(args.output)
    print(f"Wrote transformed PLY to {args.output}")


if __name__ == "__main__":
    main()
