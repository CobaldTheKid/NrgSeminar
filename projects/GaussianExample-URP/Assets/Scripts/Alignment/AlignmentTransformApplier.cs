using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace GaussianExample.Alignment
{
    [ExecuteAlways]
    public class AlignmentTransformApplier : MonoBehaviour
    {
        [SerializeField] private TextAsset alignmentJson;
        [SerializeField] private Transform targetRoot;
        [SerializeField] private Transform referenceTransform;
        [SerializeField] private bool applyRelativeToReference = true;
        [SerializeField] private bool includeReferencePosition = true;
        [SerializeField] private bool includeReferenceRotation = false;
        [SerializeField] private bool includeReferenceScale = false;
        [SerializeField] private bool useReferenceRotationScaleForPosition = true;
        [SerializeField] private Vector3 additionalEulerOffset;
        [SerializeField] private Vector3 additionalPositionOffset;
        [SerializeField] private bool applyOnStart = true;
        [SerializeField] private bool applyInEditMode;

        [Header("Read Only")]
        [SerializeField] private Vector3 appliedPosition;
        [SerializeField] private Quaternion appliedRotation = Quaternion.identity;
        [SerializeField] private Vector3 appliedScale = Vector3.one;
        [SerializeField] private string sourceMeshPointsFile;
        [SerializeField] private string sourcePlyFile;
        [SerializeField] private float fitness;
        [SerializeField] private float rmse;

        private void Reset()
        {
            if (targetRoot == null)
                targetRoot = transform;
        }

        private void Start()
        {
            if (Application.isPlaying && applyOnStart)
                ApplyAlignmentNow();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying && applyInEditMode)
            {
                EditorApplication.delayCall -= DelayedApply;
                EditorApplication.delayCall += DelayedApply;
            }
        }

        private void DelayedApply()
        {
            if (this == null)
                return;
            if (!applyInEditMode || Application.isPlaying)
                return;
            ApplyAlignmentNow();
        }
#endif

        [ContextMenu("Apply Alignment Now")]
        public void ApplyAlignmentNow()
        {
            if (alignmentJson == null)
            {
                Debug.LogWarning("AlignmentTransformApplier has no alignment JSON assigned.", this);
                return;
            }

            var target = targetRoot != null ? targetRoot : transform;
            var data = AlignmentTransformData.FromJson(alignmentJson.text);

            Quaternion baseRotation = data.rotationQuaternion * Quaternion.Euler(additionalEulerOffset);
            Vector3 baseScale = Vector3.one * data.scale;
            Vector3 worldPosition = data.translation;
            Quaternion worldRotation = baseRotation;
            Vector3 worldScale = baseScale;

            if (applyRelativeToReference && referenceTransform != null)
            {
                Matrix4x4 positionReference = Matrix4x4.TRS(
                    includeReferencePosition ? referenceTransform.position : Vector3.zero,
                    useReferenceRotationScaleForPosition ? referenceTransform.rotation : Quaternion.identity,
                    useReferenceRotationScaleForPosition ? referenceTransform.lossyScale : Vector3.one);
                worldPosition = positionReference.MultiplyPoint3x4(worldPosition);

                if (includeReferenceRotation)
                    worldRotation = referenceTransform.rotation * worldRotation;

                if (includeReferenceScale)
                    worldScale = Vector3.Scale(referenceTransform.lossyScale, worldScale);
            }

            worldPosition += additionalPositionOffset;

            if (target.parent != null)
            {
                Matrix4x4 parentWorldToLocal = target.parent.worldToLocalMatrix;
                Matrix4x4 worldMatrix = Matrix4x4.TRS(worldPosition, worldRotation, worldScale);
                Matrix4x4 localMatrix = parentWorldToLocal * worldMatrix;
                DecomposeMatrix(localMatrix, out Vector3 localPosition, out Quaternion localRotation, out Vector3 localScale);
                target.localPosition = localPosition;
                target.localRotation = localRotation;
                target.localScale = localScale;
                appliedPosition = localPosition;
                appliedRotation = localRotation;
                appliedScale = localScale;
            }
            else
            {
                target.position = worldPosition;
                target.rotation = worldRotation;
                target.localScale = worldScale;
                appliedPosition = target.position;
                appliedRotation = target.rotation;
                appliedScale = target.localScale;
            }

            sourceMeshPointsFile = data.sourceMeshPointsFile;
            sourcePlyFile = data.sourcePlyFile;
            fitness = data.fitness;
            rmse = data.rmse;

#if UNITY_EDITOR
            if (!Application.isPlaying)
                EditorUtility.SetDirty(target);
#endif
        }

        private static void DecomposeMatrix(Matrix4x4 matrix, out Vector3 position, out Quaternion rotation, out Vector3 scale)
        {
            position = matrix.GetColumn(3);

            Vector3 x = matrix.GetColumn(0);
            Vector3 y = matrix.GetColumn(1);
            Vector3 z = matrix.GetColumn(2);

            float sx = x.magnitude;
            float sy = y.magnitude;
            float sz = z.magnitude;

            Vector3 nx = sx > 1e-8f ? x / sx : Vector3.right;
            Vector3 ny = sy > 1e-8f ? y / sy : Vector3.up;
            Vector3 nz = sz > 1e-8f ? z / sz : Vector3.forward;

            float handedness = Vector3.Dot(Vector3.Cross(nx, ny), nz);
            if (handedness < 0f)
            {
                sz = -sz;
                nz = -nz;
            }

            scale = new Vector3(sx, sy, sz);

            Matrix4x4 rotMatrix = Matrix4x4.identity;
            rotMatrix.SetColumn(0, new Vector4(nx.x, nx.y, nx.z, 0f));
            rotMatrix.SetColumn(1, new Vector4(ny.x, ny.y, ny.z, 0f));
            rotMatrix.SetColumn(2, new Vector4(nz.x, nz.y, nz.z, 0f));
            rotation = rotMatrix.rotation;
        }
    }
}