using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GaussianExample.Alignment.Editor
{
    [Serializable]
    public class MeshPointCloudExportData
    {
        public string sourceObjectName;
        public string coordinateSpace = "local";
        public int sampleCount;
        public string generatedUtc;
        public BoundsData bounds;
        public PointData[] points;
    }

    [Serializable]
    public class BoundsData
    {
        public PointData center;
        public PointData size;
    }

    [Serializable]
    public class PointData
    {
        public float x;
        public float y;
        public float z;

        public PointData() {}

        public PointData(Vector3 v)
        {
            x = v.x;
            y = v.y;
            z = v.z;
        }

        public Vector3 ToVector3() => new(x, y, z);
    }

    internal struct TriangleSample
    {
        public Vector3 a;
        public Vector3 b;
        public Vector3 c;
        public float cumulativeArea;
    }

    public static class MeshPointCloudExporter
    {
        private const int DefaultSeed = 12345;

        public static string Export(GameObject root, string assetRelativePath, int sampleCount = 20000, int seed = DefaultSeed)
        {
            if (root == null)
                throw new ArgumentNullException(nameof(root));
            if (string.IsNullOrWhiteSpace(assetRelativePath))
                throw new ArgumentException("Output path is required", nameof(assetRelativePath));
            if (!assetRelativePath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Output path must be inside Assets/", nameof(assetRelativePath));
            if (sampleCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(sampleCount), "Sample count must be positive");

            var triangles = CollectTriangles(root, out var localBounds);
            if (triangles.Count == 0)
                throw new InvalidOperationException($"No readable mesh triangles found under '{root.name}'.");

            var rng = new System.Random(seed);
            var points = new PointData[sampleCount];
            float totalArea = triangles[triangles.Count - 1].cumulativeArea;
            for (int i = 0; i < sampleCount; i++)
            {
                float pick = (float)(rng.NextDouble() * totalArea);
                int index = FindTriangleIndex(triangles, pick);
                var tri = triangles[index];
                points[i] = new PointData(SamplePoint(tri.a, tri.b, tri.c, rng));
            }

            var data = new MeshPointCloudExportData
            {
                sourceObjectName = root.name,
                sampleCount = sampleCount,
                generatedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                bounds = new BoundsData
                {
                    center = new PointData(localBounds.center),
                    size = new PointData(localBounds.size)
                },
                points = points
            };

            string absolutePath = Path.GetFullPath(assetRelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(absolutePath) ?? throw new InvalidOperationException("Invalid export folder"));
            File.WriteAllText(absolutePath, JsonUtility.ToJson(data, true));
            AssetDatabase.Refresh();
            return absolutePath;
        }

        private static List<TriangleSample> CollectTriangles(GameObject root, out Bounds localBounds)
        {
            var result = new List<TriangleSample>();
            localBounds = default;
            bool hasBounds = false;
            Matrix4x4 rootWorldToLocal = root.transform.worldToLocalMatrix;

            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null)
                    continue;
                AppendMeshTriangles(filter.sharedMesh, filter.transform.localToWorldMatrix, rootWorldToLocal, result, ref localBounds, ref hasBounds);
            }

            foreach (var skinned in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (skinned.sharedMesh == null)
                    continue;
                using var baked = new BakedMeshScope();
                skinned.BakeMesh(baked.Mesh);
                AppendMeshTriangles(baked.Mesh, skinned.transform.localToWorldMatrix, rootWorldToLocal, result, ref localBounds, ref hasBounds);
            }

            return result;
        }

        private static void AppendMeshTriangles(Mesh mesh, Matrix4x4 localToWorld, Matrix4x4 rootWorldToLocal, List<TriangleSample> triangles, ref Bounds bounds, ref bool hasBounds)
        {
            Vector3[] vertices = mesh.vertices;
            int[] indices = mesh.triangles;
            if (vertices == null || vertices.Length == 0 || indices == null || indices.Length < 3)
                return;

            Matrix4x4 localToRoot = rootWorldToLocal * localToWorld;
            float cumulativeArea = triangles.Count > 0 ? triangles[triangles.Count - 1].cumulativeArea : 0f;

            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 p = localToRoot.MultiplyPoint3x4(vertices[i]);
                if (!hasBounds)
                {
                    bounds = new Bounds(p, Vector3.zero);
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(p);
                }
            }

            for (int i = 0; i < indices.Length; i += 3)
            {
                Vector3 a = localToRoot.MultiplyPoint3x4(vertices[indices[i]]);
                Vector3 b = localToRoot.MultiplyPoint3x4(vertices[indices[i + 1]]);
                Vector3 c = localToRoot.MultiplyPoint3x4(vertices[indices[i + 2]]);
                float area = Vector3.Cross(b - a, c - a).magnitude * 0.5f;
                if (area <= 1e-9f)
                    continue;
                cumulativeArea += area;
                triangles.Add(new TriangleSample { a = a, b = b, c = c, cumulativeArea = cumulativeArea });
            }
        }

        private static int FindTriangleIndex(List<TriangleSample> triangles, float pick)
        {
            int low = 0;
            int high = triangles.Count - 1;
            while (low < high)
            {
                int mid = low + ((high - low) / 2);
                if (pick <= triangles[mid].cumulativeArea)
                    high = mid;
                else
                    low = mid + 1;
            }
            return low;
        }

        private static Vector3 SamplePoint(Vector3 a, Vector3 b, Vector3 c, System.Random rng)
        {
            float r1 = Mathf.Sqrt((float)rng.NextDouble());
            float r2 = (float)rng.NextDouble();
            return (1f - r1) * a + (r1 * (1f - r2)) * b + (r1 * r2) * c;
        }

        private sealed class BakedMeshScope : IDisposable
        {
            public Mesh Mesh { get; } = new() { name = "BakedMeshScope" };
            public void Dispose() => UnityEngine.Object.DestroyImmediate(Mesh);
        }
    }
}
