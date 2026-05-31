using UnityEngine;
using GaussianSplatting.Runtime;

namespace GaussianExample
{
    public class SplatMeshTransparentCrossfadeLod : MonoBehaviour
    {
        public enum DisplayMode
        {
            AutoCrossfade,
            MeshOnly,
            SplatOnly,
            Both
        }

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [Header("Scene References")]
        [SerializeField] private GameObject meshRoot;
        [SerializeField] private GaussianSplatRenderer splatRenderer;
        [SerializeField] private Transform distanceTarget;
        [SerializeField] private Camera targetCamera;

        [Header("Mode")]
        [SerializeField] private DisplayMode displayMode = DisplayMode.AutoCrossfade;
        [SerializeField] private bool updateEveryFrame = true;

        [Header("Distance Blend")]
        [SerializeField] private float splatOnlyDistance = 3.0f;
        [SerializeField] private float meshOnlyDistance = 7.0f;
        [SerializeField] private bool useRenderedBounds = true;

        [Header("Mesh Transparent Fade")]
        [SerializeField] private bool controlMeshAlpha = true;
        [SerializeField] private bool smoothMeshFade = true;
        [SerializeField] private float meshAlphaMin = 0.0f;
        [SerializeField] private float meshAlphaMax = 1.0f;

        [Header("Splat Fade")]
        [SerializeField] private bool controlSplatOpacity = true;
        [SerializeField] private float splatOpacityMin = 0.0f;
        [SerializeField] private float splatOpacityMax = 1.0f;

        [Header("Read Only")]
        [SerializeField] private float currentDistance;
        [SerializeField] private Vector3 currentReferencePoint;
        [SerializeField] private float meshBlend;
        [SerializeField] private float splatBlend = 1.0f;

        private Renderer[] meshRenderers;
        private MaterialPropertyBlock[] meshBlocks;
        private Color[] baseColors;
        private Color[] fallbackColors;

        private void Awake()
        {
            if (distanceTarget == null)
                distanceTarget = transform;

            if (targetCamera == null)
                targetCamera = Camera.main;

            CacheRenderers();
        }

        private void OnValidate()
        {
            if (meshOnlyDistance < splatOnlyDistance)
                meshOnlyDistance = splatOnlyDistance;

            meshAlphaMin = Mathf.Clamp01(meshAlphaMin);
            meshAlphaMax = Mathf.Clamp(meshAlphaMax, meshAlphaMin, 1f);
            splatOpacityMin = Mathf.Max(0f, splatOpacityMin);
            splatOpacityMax = Mathf.Max(splatOpacityMin, splatOpacityMax);
            CacheRenderers();
        }

        private void Start()
        {
            ApplyCurrentMode();
        }

        private void Update()
        {
            if (!updateEveryFrame)
                return;

            ApplyCurrentMode();
        }

        public void RefreshNow()
        {
            ApplyCurrentMode();
        }

        private void CacheRenderers()
        {
            meshRenderers = meshRoot != null ? meshRoot.GetComponentsInChildren<Renderer>(true) : null;
            if (meshRenderers == null)
            {
                meshBlocks = null;
                baseColors = null;
                fallbackColors = null;
                return;
            }

            meshBlocks = new MaterialPropertyBlock[meshRenderers.Length];
            baseColors = new Color[meshRenderers.Length];
            fallbackColors = new Color[meshRenderers.Length];

            for (int i = 0; i < meshRenderers.Length; i++)
            {
                meshBlocks[i] = new MaterialPropertyBlock();
                var renderer = meshRenderers[i];
                var material = renderer != null ? renderer.sharedMaterial : null;
                baseColors[i] = material != null && material.HasProperty(BaseColorId)
                    ? material.GetColor(BaseColorId)
                    : Color.white;
                fallbackColors[i] = material != null && material.HasProperty(ColorId)
                    ? material.GetColor(ColorId)
                    : Color.white;
            }
        }

        private void ApplyCurrentMode()
        {
            if (meshRoot == null || splatRenderer == null)
                return;

            if (displayMode == DisplayMode.MeshOnly)
            {
                ApplyMeshVisibility(1f);
                ApplySplatVisibility(0f);
                return;
            }

            if (displayMode == DisplayMode.SplatOnly)
            {
                ApplyMeshVisibility(0f);
                ApplySplatVisibility(1f);
                return;
            }

            if (displayMode == DisplayMode.Both)
            {
                ApplyMeshVisibility(1f);
                ApplySplatVisibility(1f);
                return;
            }

            var cam = targetCamera != null ? targetCamera.transform : Camera.main != null ? Camera.main.transform : null;
            if (cam == null)
                return;

            currentReferencePoint = GetReferencePoint(cam.position);
            currentDistance = Vector3.Distance(cam.position, currentReferencePoint);

            float t = meshOnlyDistance <= splatOnlyDistance
                ? (currentDistance >= meshOnlyDistance ? 1f : 0f)
                : Mathf.InverseLerp(splatOnlyDistance, meshOnlyDistance, currentDistance);

            splatBlend = 1f - t;
            meshBlend = t;

            ApplyMeshVisibility(meshBlend);
            ApplySplatVisibility(splatBlend);
        }

        private Vector3 GetReferencePoint(Vector3 cameraPosition)
        {
            if (!useRenderedBounds)
                return distanceTarget != null ? distanceTarget.position : transform.position;

            if (TryGetCombinedBounds(out var bounds))
                return bounds.ClosestPoint(cameraPosition);

            return distanceTarget != null ? distanceTarget.position : transform.position;
        }

        private bool TryGetCombinedBounds(out Bounds bounds)
        {
            bounds = default;
            bool hasBounds = false;

            if (meshRenderers != null)
            {
                foreach (var renderer in meshRenderers)
                {
                    if (renderer == null)
                        continue;

                    if (!hasBounds)
                    {
                        bounds = renderer.bounds;
                        hasBounds = true;
                    }
                    else
                    {
                        bounds.Encapsulate(renderer.bounds);
                    }
                }
            }

            return hasBounds;
        }

        private void ApplyMeshVisibility(float alpha)
        {
            bool meshActive = alpha > 0.0001f;
            if (meshRoot.activeSelf != meshActive)
                meshRoot.SetActive(meshActive);

            if (!controlMeshAlpha || !meshActive || meshRenderers == null || meshBlocks == null)
                return;

            float visibleAlpha = Mathf.Clamp01(alpha);
            if (smoothMeshFade)
                visibleAlpha = Mathf.SmoothStep(0f, 1f, visibleAlpha);
            visibleAlpha = Mathf.Lerp(meshAlphaMin, meshAlphaMax, visibleAlpha);

            for (int i = 0; i < meshRenderers.Length; i++)
            {
                var renderer = meshRenderers[i];
                if (renderer == null)
                    continue;

                renderer.GetPropertyBlock(meshBlocks[i]);

                var baseColor = baseColors[i];
                baseColor.a = visibleAlpha;
                meshBlocks[i].SetColor(BaseColorId, baseColor);

                var fallbackColor = fallbackColors[i];
                fallbackColor.a = visibleAlpha;
                meshBlocks[i].SetColor(ColorId, fallbackColor);

                renderer.SetPropertyBlock(meshBlocks[i]);
            }
        }

        private void ApplySplatVisibility(float alpha)
        {
            float opacity = controlSplatOpacity
                ? Mathf.Lerp(splatOpacityMin, splatOpacityMax, Mathf.Clamp01(alpha))
                : 1f;

            splatRenderer.m_OpacityScale = opacity;

            bool splatActive = alpha > 0.0001f;
            if (splatRenderer.gameObject.activeSelf != splatActive)
                splatRenderer.gameObject.SetActive(splatActive);
        }
    }
}
