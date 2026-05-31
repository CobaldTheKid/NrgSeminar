from __future__ import annotations

import argparse
import itertools
import json
from pathlib import Path

import numpy as np
import open3d as o3d

from io_alignment_json import write_alignment_json
from load_ply_points import load_ply_points


def load_mesh_points(path: str) -> np.ndarray:
    data = json.loads(Path(path).read_text(encoding="utf-8"))
    points = data.get("points", [])
    return np.asarray([[p["x"], p["y"], p["z"]] for p in points], dtype=np.float64)


def euler_degrees_xyz_to_matrix(euler_deg: tuple[float, float, float]) -> np.ndarray:
    ex, ey, ez = np.radians(np.asarray(euler_deg, dtype=np.float64))

    cx, sx = np.cos(ex), np.sin(ex)
    cy, sy = np.cos(ey), np.sin(ey)
    cz, sz = np.cos(ez), np.sin(ez)

    rx = np.array([[1.0, 0.0, 0.0], [0.0, cx, -sx], [0.0, sx, cx]], dtype=np.float64)
    ry = np.array([[cy, 0.0, sy], [0.0, 1.0, 0.0], [-sy, 0.0, cy]], dtype=np.float64)
    rz = np.array([[cz, -sz, 0.0], [sz, cz, 0.0], [0.0, 0.0, 1.0]], dtype=np.float64)
    return rz @ ry @ rx


def apply_pretransform(points: np.ndarray, axis_scale: np.ndarray, euler_deg: tuple[float, float, float]) -> tuple[np.ndarray, np.ndarray]:
    axis_scale = np.asarray(axis_scale, dtype=np.float64)
    correction = euler_degrees_xyz_to_matrix(euler_deg) @ np.diag(axis_scale)
    transformed = (correction @ points.T).T
    return transformed, correction


def pca_basis(points: np.ndarray) -> tuple[np.ndarray, np.ndarray]:
    centroid = points.mean(axis=0)
    centered = points - centroid
    cov = centered.T @ centered / max(len(centered), 1)
    eigvals, eigvecs = np.linalg.eigh(cov)
    order = np.argsort(eigvals)[::-1]
    basis = eigvecs[:, order]
    if np.linalg.det(basis) < 0:
        basis[:, 2] *= -1.0
    return centroid, basis


def estimate_uniform_scale(source: np.ndarray, target: np.ndarray) -> float:
    src_radius = np.sqrt(np.mean(np.sum((source - source.mean(axis=0)) ** 2, axis=1)))
    tgt_radius = np.sqrt(np.mean(np.sum((target - target.mean(axis=0)) ** 2, axis=1)))
    if src_radius < 1e-8:
        return 1.0
    return float(tgt_radius / src_radius)


def proper_signed_permutation_matrices():
    base = np.eye(3)
    for perm in itertools.permutations(range(3)):
        permuted = base[:, perm]
        for signs in itertools.product([-1.0, 1.0], repeat=3):
            mat = permuted @ np.diag(signs)
            if np.linalg.det(mat) > 0.0:
                yield mat


def make_point_cloud(points: np.ndarray) -> o3d.geometry.PointCloud:
    pc = o3d.geometry.PointCloud()
    pc.points = o3d.utility.Vector3dVector(points)
    return pc


def main() -> None:
    parser = argparse.ArgumentParser(description="Compute mesh-to-splat alignment transform")
    parser.add_argument("--mesh-points", required=True, help="Path to exported mesh point cloud JSON")
    parser.add_argument("--ply", required=True, help="Path to source Gaussian PLY")
    parser.add_argument("--output", required=True, help="Output alignment_transform.json path")
    parser.add_argument("--opacity-threshold", type=float, default=None)
    parser.add_argument("--max-mesh-points", type=int, default=20000)
    parser.add_argument("--max-splat-points", type=int, default=20000)
    parser.add_argument("--icp-threshold", type=float, default=None)
    parser.add_argument("--seed", type=int, default=12345)
    parser.add_argument("--mesh-axis-scale", type=float, nargs=3, metavar=("SX", "SY", "SZ"), default=(1.0, 1.0, 1.0))
    parser.add_argument("--mesh-pre-rotate-euler", type=float, nargs=3, metavar=("RX", "RY", "RZ"), default=(0.0, 0.0, 0.0))
    args = parser.parse_args()

    mesh_points = load_mesh_points(args.mesh_points)
    splat_points = load_ply_points(args.ply, opacity_threshold=args.opacity_threshold, max_points=args.max_splat_points, rng_seed=args.seed)

    if args.max_mesh_points is not None and len(mesh_points) > args.max_mesh_points:
        rng = np.random.default_rng(args.seed)
        mesh_points = mesh_points[rng.choice(len(mesh_points), size=args.max_mesh_points, replace=False)]

    if len(mesh_points) < 8 or len(splat_points) < 8:
        raise RuntimeError("Not enough points for alignment.")

    mesh_points_corrected, mesh_correction = apply_pretransform(mesh_points, np.asarray(args.mesh_axis_scale, dtype=np.float64), tuple(args.mesh_pre_rotate_euler))

    mesh_centroid, mesh_basis = pca_basis(mesh_points_corrected)
    splat_centroid, splat_basis = pca_basis(splat_points)
    uniform_scale = estimate_uniform_scale(mesh_points_corrected, splat_points)
    mesh_scaled = mesh_points_corrected * uniform_scale
    mesh_scaled_centroid = mesh_scaled.mean(axis=0)

    if args.icp_threshold is not None:
        icp_threshold = args.icp_threshold
    else:
        bbox_diag = np.linalg.norm(splat_points.max(axis=0) - splat_points.min(axis=0))
        icp_threshold = max(bbox_diag * 0.05, 1e-3)

    target_pc = make_point_cloud(splat_points)
    source_scaled_pc = make_point_cloud(mesh_scaled)

    best = None
    for signed_perm in proper_signed_permutation_matrices():
        rotation_init = splat_basis @ signed_perm @ mesh_basis.T
        translation_init = splat_centroid - rotation_init @ mesh_scaled_centroid

        init = np.eye(4)
        init[:3, :3] = rotation_init
        init[:3, 3] = translation_init

        reg = o3d.pipelines.registration.registration_icp(
            source_scaled_pc,
            target_pc,
            icp_threshold,
            init,
            o3d.pipelines.registration.TransformationEstimationPointToPoint(),
        )

        candidate = {
            "fitness": float(reg.fitness),
            "rmse": float(reg.inlier_rmse),
            "transform": reg.transformation.copy(),
        }

        if best is None:
            best = candidate
        elif candidate["fitness"] > best["fitness"] + 1e-9:
            best = candidate
        elif abs(candidate["fitness"] - best["fitness"]) <= 1e-9 and candidate["rmse"] < best["rmse"]:
            best = candidate

    assert best is not None
    solved_rotation_corrected = best["transform"][:3, :3]
    final_rotation_original = solved_rotation_corrected @ mesh_correction
    final_translation = best["transform"][:3, 3]

    write_alignment_json(
        output_path=args.output,
        translation=final_translation,
        rotation_matrix=final_rotation_original,
        scale=uniform_scale,
        source_mesh_points_file=args.mesh_points,
        source_ply_file=args.ply,
        fitness=best["fitness"],
        rmse=best["rmse"],
    )

    print(f"Wrote alignment to {args.output}")
    print(f"fitness={best['fitness']:.6f} rmse={best['rmse']:.6f} scale={uniform_scale:.6f}")
    print(f"mesh_axis_scale={tuple(float(v) for v in args.mesh_axis_scale)}")
    print(f"mesh_pre_rotate_euler={tuple(float(v) for v in args.mesh_pre_rotate_euler)}")
    print(f"mesh_correction_matrix=\n{mesh_correction}")
    print(f"final_rotation_original=\n{final_rotation_original}")
    print(f"final_translation={final_translation}")


if __name__ == "__main__":
    main()
