using System.IO;
using UnityEditor;
using UnityEngine;

namespace GaussianExample.Alignment.Editor
{
    public class AlignmentExportWindow : EditorWindow
    {
        private GameObject targetObject;
        private int sampleCount = 20000;
        private string outputFolder = "Assets/AlignmentData";
        private string outputFileName = string.Empty;

        [MenuItem("Tools/Alignment/Export Mesh Point Cloud")]
        public static void ShowWindow()
        {
            var window = GetWindow<AlignmentExportWindow>("Alignment Export");
            window.minSize = new Vector2(420, 180);
            window.SyncFromSelection();
        }

        private void OnFocus() => SyncFromSelection();

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Mesh Point Cloud Export", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
            {
                targetObject = (GameObject)EditorGUILayout.ObjectField("Target Object", targetObject, typeof(GameObject), true);
                sampleCount = EditorGUILayout.IntField("Sample Count", sampleCount);
                outputFolder = EditorGUILayout.TextField("Output Folder", outputFolder);
                outputFileName = EditorGUILayout.TextField("Output File Name", outputFileName);

                EditorGUILayout.Space();
                if (GUILayout.Button("Use Current Selection"))
                    SyncFromSelection();

                using (new EditorGUI.DisabledScope(targetObject == null || sampleCount <= 0))
                {
                    if (GUILayout.Button("Export Mesh Point Cloud"))
                        Export();
                }
            }
        }

        private void SyncFromSelection()
        {
            if (Selection.activeGameObject != null)
            {
                targetObject = Selection.activeGameObject;
                if (string.IsNullOrWhiteSpace(outputFileName))
                    outputFileName = $"{targetObject.name}.meshpoints.json";
            }
        }

        private void Export()
        {
            if (targetObject == null)
            {
                EditorUtility.DisplayDialog("Alignment Export", "Select a scene object or prefab instance first.", "OK");
                return;
            }

            if (sampleCount <= 0)
            {
                EditorUtility.DisplayDialog("Alignment Export", "Sample count must be greater than zero.", "OK");
                return;
            }

            string safeName = string.IsNullOrWhiteSpace(outputFileName) ? $"{targetObject.name}.meshpoints.json" : outputFileName;
            if (!safeName.EndsWith(".json"))
                safeName += ".json";

            string assetRelativePath = Path.Combine(outputFolder, safeName).Replace('\\', '/');
            try
            {
                string absolute = MeshPointCloudExporter.Export(targetObject, assetRelativePath, sampleCount);
                EditorUtility.RevealInFinder(absolute);
                Debug.Log($"Exported mesh point cloud to {assetRelativePath}", targetObject);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Mesh point cloud export failed: {ex.Message}", targetObject);
                EditorUtility.DisplayDialog("Alignment Export Failed", ex.Message, "OK");
            }
        }
    }
}
