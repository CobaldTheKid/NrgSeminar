using System;
using System.Globalization;
using UnityEngine;

namespace GaussianExample.Alignment
{
    [Serializable]
    public class AlignmentTransformData
    {
        public Vector3 translation;
        public Quaternion rotationQuaternion = Quaternion.identity;
        public Matrix4x4 rotationMatrix = Matrix4x4.identity;
        public float scale = 1f;
        public string sourceMeshPointsFile;
        public string sourcePlyFile;
        public float fitness;
        public float rmse;
        public string createdUtc;

        public static AlignmentTransformData FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("Alignment JSON is empty.", nameof(json));

            var data = new AlignmentTransformData
            {
                translation = ParseVector3(json, "translation"),
                rotationQuaternion = ParseQuaternion(json, "rotation_quaternion"),
                rotationMatrix = ParseMatrix3x3(json, "rotation_matrix"),
                scale = ParseFloatValue(json, "scale", 1f),
                sourceMeshPointsFile = ParseStringValue(json, "source_mesh_points_file"),
                sourcePlyFile = ParseStringValue(json, "source_ply_file"),
                fitness = ParseFloatValue(json, "fitness", 0f),
                rmse = ParseFloatValue(json, "rmse", 0f),
                createdUtc = ParseStringValue(json, "created_utc")
            };

            if (data.rotationQuaternion == Quaternion.identity && data.rotationMatrix != Matrix4x4.identity)
                data.rotationQuaternion = data.rotationMatrix.rotation;

            return data;
        }

        private static Vector3 ParseVector3(string json, string key)
        {
            float[] values = ParseFlatNumberArray(json, key, 3);
            return new Vector3(values[0], values[1], values[2]);
        }

        private static Quaternion ParseQuaternion(string json, string key)
        {
            float[] values = ParseFlatNumberArray(json, key, 4);
            return new Quaternion(values[0], values[1], values[2], values[3]);
        }

        private static Matrix4x4 ParseMatrix3x3(string json, string key)
        {
            string payload = ExtractBracketPayload(json, key);
            if (string.IsNullOrEmpty(payload))
                return Matrix4x4.identity;

            string[] rows = SplitTopLevel(payload);
            if (rows.Length < 3)
                return Matrix4x4.identity;

            float[] r0 = ParseCsvNumbers(TrimOuterBrackets(rows[0]), 3);
            float[] r1 = ParseCsvNumbers(TrimOuterBrackets(rows[1]), 3);
            float[] r2 = ParseCsvNumbers(TrimOuterBrackets(rows[2]), 3);

            var m = Matrix4x4.identity;
            m.m00 = r0[0]; m.m01 = r0[1]; m.m02 = r0[2];
            m.m10 = r1[0]; m.m11 = r1[1]; m.m12 = r1[2];
            m.m20 = r2[0]; m.m21 = r2[1]; m.m22 = r2[2];
            return m;
        }

        private static float[] ParseFlatNumberArray(string json, string key, int expectedCount)
        {
            string payload = ExtractBracketPayload(json, key);
            if (string.IsNullOrEmpty(payload))
                throw new FormatException($"Could not find numeric array '{key}' in alignment JSON.");
            return ParseCsvNumbers(payload, expectedCount);
        }

        private static string ExtractBracketPayload(string json, string key)
        {
            int keyIndex = json.IndexOf($"\"{key}\"", StringComparison.Ordinal);
            if (keyIndex < 0)
                return string.Empty;

            int start = json.IndexOf('[', keyIndex);
            if (start < 0)
                return string.Empty;

            int depth = 0;
            for (int i = start; i < json.Length; i++)
            {
                char ch = json[i];
                if (ch == '[')
                    depth++;
                else if (ch == ']')
                {
                    depth--;
                    if (depth == 0)
                        return json.Substring(start + 1, i - start - 1);
                }
            }

            return string.Empty;
        }

        private static string[] SplitTopLevel(string payload)
        {
            var parts = new System.Collections.Generic.List<string>();
            int depth = 0;
            int start = 0;
            for (int i = 0; i < payload.Length; i++)
            {
                char ch = payload[i];
                if (ch == '[') depth++;
                else if (ch == ']') depth--;
                else if (ch == ',' && depth == 0)
                {
                    parts.Add(payload.Substring(start, i - start).Trim());
                    start = i + 1;
                }
            }
            parts.Add(payload.Substring(start).Trim());
            return parts.ToArray();
        }

        private static string TrimOuterBrackets(string value)
        {
            value = value.Trim();
            if (value.Length >= 2 && value[0] == '[' && value[^1] == ']')
                return value.Substring(1, value.Length - 2);
            return value;
        }

        private static float[] ParseCsvNumbers(string csv, int expectedCount)
        {
            string[] parts = csv.Split(',');
            if (parts.Length < expectedCount)
                throw new FormatException($"Expected at least {expectedCount} numeric values, got {parts.Length}.");

            var result = new float[expectedCount];
            for (int i = 0; i < expectedCount; i++)
                result[i] = float.Parse(parts[i].Trim(), CultureInfo.InvariantCulture);
            return result;
        }

        private static float ParseFloatValue(string json, string key, float fallback)
        {
            int keyIndex = json.IndexOf($"\"{key}\"", StringComparison.Ordinal);
            if (keyIndex < 0)
                return fallback;
            int colon = json.IndexOf(':', keyIndex);
            if (colon < 0)
                return fallback;

            int start = colon + 1;
            while (start < json.Length && char.IsWhiteSpace(json[start])) start++;
            int end = start;
            while (end < json.Length && "-+0123456789.eE".IndexOf(json[end]) >= 0) end++;
            if (end <= start)
                return fallback;
            return float.Parse(json.Substring(start, end - start), CultureInfo.InvariantCulture);
        }

        private static string ParseStringValue(string json, string key)
        {
            int keyIndex = json.IndexOf($"\"{key}\"", StringComparison.Ordinal);
            if (keyIndex < 0)
                return string.Empty;
            int colon = json.IndexOf(':', keyIndex);
            if (colon < 0)
                return string.Empty;
            int firstQuote = json.IndexOf('"', colon + 1);
            if (firstQuote < 0)
                return string.Empty;
            int secondQuote = json.IndexOf('"', firstQuote + 1);
            if (secondQuote < 0)
                return string.Empty;
            return json.Substring(firstQuote + 1, secondQuote - firstQuote - 1);
        }
    }
}
