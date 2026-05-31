using UnityEngine;

namespace GaussianExample
{
    public class SplatMeshLodSwitcher : MonoBehaviour
    {
        public enum DisplayMode
        {
            AutoSwitch,
            MeshOnly,
            SplatOnly,
            Both
        }

        [Header("Scene References")]
        [SerializeField] private GameObject meshRoot;
        [SerializeField] private GameObject splatRoot;
        [SerializeField] private Transform distanceTarget;
        [SerializeField] private Camera targetCamera;

        [Header("Mode")]
        [SerializeField] private DisplayMode displayMode = DisplayMode.AutoSwitch;
        [SerializeField] private bool updateEveryFrame = true;

        [Header("Distance Switch")]
        [SerializeField] private float switchDistance = 2.5f;
        [SerializeField] private float hysteresis = 0.25f;
        [SerializeField] private bool useRenderedBounds = true;

        [Header("Read Only")]
        [SerializeField] private float currentDistance;
        [SerializeField] private Vector3 currentReferencePoint;
        [SerializeField] private bool showingMesh;
        [SerializeField] private bool showingSplat = true;

        private Renderer[] meshRenderers;
        private Renderer[] splatRenderers;

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
            CacheRenderers();
        }

        private void Start()
        {
            ApplyCurrentMode(force: true);
        }

        private void Update()
        {
            if (!updateEveryFrame)
                return;

            ApplyCurrentMode(force: false);
        }

        public void RefreshNow()
        {
            ApplyCurrentMode(force: true);
        }

        private void CacheRenderers()
        {
            meshRenderers = meshRoot != null ? meshRoot.GetComponentsInChildren<Renderer>(true) : null;
            splatRenderers = splatRoot != null ? splatRoot.GetComponentsInChildren<Renderer>(true) : null;
        }

        private void ApplyCurrentMode(bool force)
        {
            if (meshRoot == null || splatRoot == null)
                return;

            if (displayMode == DisplayMode.MeshOnly)
            {
                SetVisible(meshVisible: true, splatVisible: false, force);
                return;
            }

            if (displayMode == DisplayMode.SplatOnly)
            {
                SetVisible(meshVisible: false, splatVisible: true, force);
                return;
            }

            if (displayMode == DisplayMode.Both)
            {
                SetVisible(meshVisible: true, splatVisible: true, force);
                return;
            }

            var cam = targetCamera != null ? targetCamera.transform : Camera.main != null ? Camera.main.transform : null;
            if (cam == null)
                return;

            currentReferencePoint = GetReferencePoint(cam.position);
            currentDistance = Vector3.Distance(cam.position, currentReferencePoint);

            bool nextShowSplat;
            if (showingSplat)
            {
                nextShowSplat = currentDistance <= switchDistance + hysteresis;
            }
            else
            {
                nextShowSplat = currentDistance < switchDistance - hysteresis;
            }

            SetVisible(meshVisible: !nextShowSplat, splatVisible: nextShowSplat, force);
        }

        private Vector3 GetReferencePoint(Vector3 cameraPosition)
        {
            if (!useRenderedBounds)
            {
                return distanceTarget != null ? distanceTarget.position : transform.position;
            }

            if (TryGetCombinedBounds(out var bounds))
            {
                return bounds.ClosestPoint(cameraPosition);
            }

            return distanceTarget != null ? distanceTarget.position : transform.position;
        }

        private bool TryGetCombinedBounds(out Bounds bounds)
        {
            bool hasBounds = false;
            bounds = default;

            hasBounds |= EncapsulateRenderers(meshRenderers, ref bounds);
            hasBounds |= EncapsulateRenderers(splatRenderers, ref bounds);

            return hasBounds;
        }

        private static bool EncapsulateRenderers(Renderer[] renderers, ref Bounds bounds)
        {
            if (renderers == null)
                return false;

            bool hasBounds = false;
            foreach (var renderer in renderers)
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

            return hasBounds;
        }

        private void SetVisible(bool meshVisible, bool splatVisible, bool force)
        {
            if (!force && showingMesh == meshVisible && showingSplat == splatVisible)
                return;

            meshRoot.SetActive(meshVisible);
            splatRoot.SetActive(splatVisible);
            showingMesh = meshVisible;
            showingSplat = splatVisible;
        }
    }
}
