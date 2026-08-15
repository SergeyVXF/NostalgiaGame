using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace MiniVanGame
{
    /// <summary>
    /// Static vanity mirror: camera is glued to the glass and never tracks the player.
    /// Only renders while someone is on the front side and nearby.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MiniVanWorldMirror : MonoBehaviour
    {
        [Header("Activation")]
        public float ActiveDistance = 12f;
        public int FrameSkip = 2;

        [Tooltip("Front faces the stand (-forward). Toggle if the image is on the wrong side.")]
        public bool InvertFrontNormal;

        [Header("Zoom")]
        [Range(8f, 70f)]
        public float MirrorFieldOfView = 32f;

        [Header("Camera on glass (static)")]
        [Tooltip("Along front normal: positive = toward the stand.")]
        public float CameraDistance = 0.08f;

        public float CameraHeightOffset;

        [Tooltip("Local offset on the glass (X right, Y up, Z glass-forward).")]
        public Vector3 CameraLocalOffset = Vector3.zero;

        [Header("Look target (static)")]
        [Tooltip("Height above StandPoint. If no StandPoint, uses glass + front * LookDistance.")]
        public float LookAtHeight = 1.2f;

        [Tooltip("Fallback distance along front when StandPoint is missing.")]
        public float LookDistance = 1.35f;

        [Tooltip("Extra world offset on the look point.")]
        public Vector3 LookAtWorldOffset = Vector3.zero;

        [Header("Clip / texture")]
        public int TextureWidth = 512;
        public int TextureHeight = 512;
        public float FarClipPlane = 25f;
        public float NearClipPlane = 0.02f;
        public Color BackSideColor = new Color(0.08f, 0.08f, 0.09f, 1f);

        [Header("Debug")]
        public bool DrawGizmos = true;

        private Renderer mirrorRenderer;
        private Camera reflectionCamera;
        private RenderTexture reflectionTexture;
        private Material mirrorMaterial;
        private Material backMaterial;
        private bool showingFront = true;
        private bool poseCached;
        private int frameCounter;
        private Transform standPoint;
        private Vector3 cachedCamPos;
        private Quaternion cachedCamRot;

        private void Awake()
        {
            mirrorRenderer = GetComponent<Renderer>();
            CacheStationRefs();
            EnsureResources();
            CacheStaticPose();
            ShowBackSide();
        }

        private void OnDestroy()
        {
            if (reflectionCamera != null)
            {
                Destroy(reflectionCamera.gameObject);
                reflectionCamera = null;
            }

            if (reflectionTexture != null)
            {
                reflectionTexture.Release();
                Destroy(reflectionTexture);
                reflectionTexture = null;
            }

            if (mirrorMaterial != null)
            {
                Destroy(mirrorMaterial);
                mirrorMaterial = null;
            }

            if (backMaterial != null && backMaterial.name == "VanityMirrorBackMat")
            {
                Destroy(backMaterial);
                backMaterial = null;
            }
        }

        private void OnDisable()
        {
            DisableReflectionTarget();
        }

        private void OnValidate()
        {
            poseCached = false;
        }

        private void LateUpdate()
        {
            if (mirrorRenderer == null)
            {
                return;
            }

            Camera viewer = FindViewerCamera();
            if (viewer == null)
            {
                ShowBackSide();
                DisableReflectionTarget();
                return;
            }

            CacheStationRefs();
            MiniVanFacePaintStation station = GetComponentInParent<MiniVanFacePaintStation>();
            if (station != null && station.IsSessionActive)
            {
                ShowBackSide();
                DisableReflectionTarget();
                return;
            }

            if (!IsViewerOnFrontSide(viewer.transform.position))
            {
                ShowBackSide();
                DisableReflectionTarget();
                return;
            }

            if ((viewer.transform.position - transform.position).sqrMagnitude > ActiveDistance * ActiveDistance)
            {
                ShowBackSide();
                DisableReflectionTarget();
                return;
            }

            EnsureResources();
            if (!poseCached)
            {
                CacheStaticPose();
            }

            // Keep transform locked — never follows the player.
            reflectionCamera.transform.SetPositionAndRotation(cachedCamPos, cachedCamRot);
            reflectionCamera.ResetProjectionMatrix();
            reflectionCamera.fieldOfView = MirrorFieldOfView;
            reflectionCamera.aspect = (float)TextureWidth / TextureHeight;
            reflectionCamera.nearClipPlane = NearClipPlane;
            reflectionCamera.farClipPlane = FarClipPlane;

            ShowFrontSide();
            reflectionCamera.targetTexture = reflectionTexture;

            frameCounter++;
            if (FrameSkip > 1 && (frameCounter % FrameSkip) != 0)
            {
                return;
            }

            RenderReflection();
        }

        private void CacheStationRefs()
        {
            MiniVanFacePaintStation station = GetComponentInParent<MiniVanFacePaintStation>();
            if (station != null && station.StandPoint != null)
            {
                standPoint = station.StandPoint;
            }
        }

        private void CacheStaticPose()
        {
            cachedCamPos = GetCameraWorldPosition();
            Vector3 lookAt = GetLookAtWorldPosition();
            Vector3 forward = lookAt - cachedCamPos;
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = GetFrontNormal();
            }

            cachedCamRot = Quaternion.LookRotation(forward.normalized, Vector3.up);
            poseCached = true;
        }

        private Vector3 GetFrontNormal()
        {
            Vector3 normal = -transform.forward;
            if (InvertFrontNormal)
            {
                normal = -normal;
            }

            return normal.normalized;
        }

        private bool IsViewerOnFrontSide(Vector3 viewerPos)
        {
            return Vector3.Dot(viewerPos - transform.position, GetFrontNormal()) > 0.02f;
        }

        private Vector3 GetCameraWorldPosition()
        {
            Vector3 front = GetFrontNormal();
            Vector3 pos = transform.position + front * CameraDistance;
            pos += Vector3.up * CameraHeightOffset;
            pos += transform.TransformVector(CameraLocalOffset);
            return pos;
        }

        private Vector3 GetLookAtWorldPosition()
        {
            Vector3 lookAt;
            if (standPoint != null)
            {
                lookAt = standPoint.position + Vector3.up * LookAtHeight;
            }
            else
            {
                lookAt = transform.position + GetFrontNormal() * LookDistance + Vector3.up * LookAtHeight;
            }

            return lookAt + LookAtWorldOffset;
        }

        private void ShowFrontSide()
        {
            if (showingFront && mirrorRenderer.sharedMaterial == mirrorMaterial)
            {
                return;
            }

            showingFront = true;
            if (mirrorMaterial != null)
            {
                mirrorRenderer.sharedMaterial = mirrorMaterial;
            }
        }

        private void ShowBackSide()
        {
            if (!showingFront && mirrorRenderer.sharedMaterial == backMaterial)
            {
                return;
            }

            showingFront = false;
            EnsureBackMaterial();
            mirrorRenderer.sharedMaterial = backMaterial;
        }

        private void DisableReflectionTarget()
        {
            if (reflectionCamera != null)
            {
                reflectionCamera.targetTexture = null;
            }
        }

        private void EnsureBackMaterial()
        {
            if (backMaterial != null)
            {
                return;
            }

#if UNITY_EDITOR
            Material asset = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/MiniVan Game/Materials/FacePaint/FacePaint_MirrorBack.mat");
            if (asset != null)
            {
                backMaterial = asset;
                return;
            }
#endif

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            backMaterial = new Material(shader) { name = "VanityMirrorBackMat" };
            if (backMaterial.HasProperty("_BaseColor"))
            {
                backMaterial.SetColor("_BaseColor", BackSideColor);
            }

            backMaterial.color = BackSideColor;
        }

        private void EnsureResources()
        {
            if (reflectionTexture == null)
            {
                reflectionTexture = new RenderTexture(TextureWidth, TextureHeight, 16, RenderTextureFormat.ARGB32)
                {
                    name = "VanityMirrorRT",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    antiAliasing = 1
                };
                reflectionTexture.Create();
            }

            if (reflectionCamera == null)
            {
                GameObject camGo = new GameObject("VanityReflectionCamera");
                camGo.hideFlags = HideFlags.DontSave;
                camGo.transform.SetParent(transform, false);
                reflectionCamera = camGo.AddComponent<Camera>();
                reflectionCamera.enabled = false;
                reflectionCamera.allowHDR = false;
                reflectionCamera.allowMSAA = false;
                reflectionCamera.useOcclusionCulling = true;
                reflectionCamera.clearFlags = CameraClearFlags.Skybox;
                reflectionCamera.targetTexture = reflectionTexture;

                UniversalAdditionalCameraData urpData = camGo.GetComponent<UniversalAdditionalCameraData>();
                if (urpData == null)
                {
                    urpData = camGo.AddComponent<UniversalAdditionalCameraData>();
                }

                urpData.renderType = CameraRenderType.Base;
                urpData.renderShadows = false;
                urpData.requiresColorOption = CameraOverrideOption.Off;
                urpData.requiresDepthOption = CameraOverrideOption.Off;
            }

            if (mirrorMaterial == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null)
                {
                    shader = Shader.Find("Unlit/Texture");
                }

                if (shader == null)
                {
                    shader = Shader.Find("Universal Render Pipeline/Lit");
                }

                mirrorMaterial = new Material(shader) { name = "VanityMirrorMat" };
                if (mirrorMaterial.HasProperty("_BaseColor"))
                {
                    mirrorMaterial.SetColor("_BaseColor", Color.white);
                }

                if (mirrorMaterial.HasProperty("_BaseMap"))
                {
                    mirrorMaterial.SetTexture("_BaseMap", reflectionTexture);
                }

                mirrorMaterial.mainTexture = reflectionTexture;
                mirrorMaterial.mainTextureScale = new Vector2(-1f, 1f);
                mirrorMaterial.mainTextureOffset = new Vector2(1f, 0f);
            }
            else
            {
                mirrorMaterial.mainTexture = reflectionTexture;
                if (mirrorMaterial.HasProperty("_BaseMap"))
                {
                    mirrorMaterial.SetTexture("_BaseMap", reflectionTexture);
                }
            }

            EnsureBackMaterial();
            reflectionCamera.targetTexture = reflectionTexture;
        }

        private void RenderReflection()
        {
            if (reflectionCamera == null || reflectionTexture == null)
            {
                return;
            }

            bool wasEnabled = mirrorRenderer.enabled;
            mirrorRenderer.enabled = false;
            reflectionCamera.Render();
            mirrorRenderer.enabled = wasEnabled;
        }

        private static Camera FindViewerCamera()
        {
            MiniVanPlayer local = MiniVanPlayer.LocalPlayer;
            if (local != null && local.PlayerCamera != null && local.PlayerCamera.enabled)
            {
                return local.PlayerCamera;
            }

            Camera main = Camera.main;
            return main != null && main.enabled ? main : null;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!DrawGizmos)
            {
                return;
            }

            CacheStationRefs();
            Vector3 camPos = GetCameraWorldPosition();
            Vector3 lookAt = GetLookAtWorldPosition();
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, transform.position + GetFrontNormal() * 0.5f);
            Gizmos.DrawWireSphere(camPos, 0.05f);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(lookAt, 0.08f);
            Gizmos.DrawLine(camPos, lookAt);
        }
#endif
    }
}
