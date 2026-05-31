using UnityEngine;
using GaussianSplatting.Runtime;

namespace GaussianExample
{
    public class SplatMeshCrossfadeLod : MonoBehaviour
    {
        public enum DisplayMode
        {
            AutoCrossfade,
            MeshOnly,
            SplatOnly,
            Both
        }

        private static readonly int FadeId = Shader.PropertyToID("_Fade");

        [Header("Scene References")]
        [SerializeField] private GameObject meshRoot;
        [SerializeField] private GaussianSplatRenderer splatRenderer;
        [SerializeField] private Transform distanceTarget;
        [SerializeField] private Camera targetCamera;

        [Header("Mode")]
        [SerializeField] private DisplayMode displayMode = DisplayMode.AutoCrossfade;
        [SerializeField] private bool updateEveryFrame = true;

        [Header("Distance Blend")]
        [SerializeField] private float splatOnlyDistance = 1.5f;
        [SerializeField] private float meshOnlyDistance = 3.0f;
        [SerializeField] private bool useRenderedBounds = true;

        [Header("Mesh Fade")]
        [SerializeField] private bool controlMeshFade = true;
        [SerializeField] private string meshFadeShaderName = "Custom/Mesh Dither Fade URP";
        [SerializeField] private float meshFadeStart = 0.0f;
        [SerializeField] private float meshFadeEnd = 0.85f;
        [SerializeField] private bool smoothMeshFade = true;

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

            meshFadeStart = Mathf.Clamp01(meshFadeStart);
            meshFadeEnd = Mathf.Clamp(meshFadeEnd, meshFadeStart + 0.0001f, 1f);
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
                return;
            }

            meshBlocks = new MaterialPropertyBlock[meshRenderers.Length];
            for (int i = 0; i < meshBlocks.Length; i++)
                meshBlocks[i] = new MaterialPropertyBlock();
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

            float t;
            if (meshOnlyDistance <= splatOnlyDistance)
            {
                t = currentDistance >= meshOnlyDistance ? 1f : 0f;
            }
            else
            {
                t = Mathf.InverseLerp(splatOnlyDistance, meshOnlyDistance, currentDistance);
            }

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

            if (!controlMeshFade || !meshActive || meshRenderers == null || meshBlocks == null)
                return;

            float ditherFade = alpha <= 0f ? 0f : Mathf.InverseLerp(meshFadeStart, meshFadeEnd, alpha);
            ditherFade = Mathf.Clamp01(ditherFade);
            if (smoothMeshFade)
                ditherFade = Mathf.SmoothStep(0f, 1f, ditherFade);

            for (int i = 0; i < meshRenderers.Length; i++)
            {
                var renderer = meshRenderers[i];
                if (renderer == null)
                    continue;

                renderer.GetPropertyBlock(meshBlocks[i]);
                meshBlocks[i].SetFloat(FadeId, ditherFade);
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
