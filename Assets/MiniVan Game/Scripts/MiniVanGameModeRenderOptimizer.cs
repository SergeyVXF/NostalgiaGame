using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace MiniVanGame
{
    /// <summary>
    /// Game-mode performance settings. Lives on an existing host
    /// (WorldGenerator / ManualBuilder), not as a separate hierarchy object.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-80)]
    public sealed class MiniVanGameModeRenderOptimizer : MonoBehaviour
    {
        [Header("Frame")]
        [Min(30)] public int TargetFrameRate = 60;

        [Header("Camera")]
        [Min(100f)] public float ExteriorCameraFarClip = 2500f;
        [Min(60f)] public float NearPanelkaCameraFarClip = 2500f;
        [Min(40f)] public float InteriorCameraFarClip = 800f;
        [Min(20f)] public float PanelkaProximityDistance = 120f;

        [Header("URP / Shadows (runtime, restored on destroy)")]
        public bool ApplyUrpPreset = true;
        [Min(10f)] public float ExteriorShadowDistance = 35f;
        [Min(10f)] public float NearPanelkaShadowDistance = 22f;
        [Min(5f)] public float InteriorShadowDistance = 14f;
        [Range(1, 4)] public int ShadowCascadeCount = 2;
        [Range(0, 8)] public int MaxAdditionalLights = 2;

        [Header("Soft Horizon Fade")]
        public bool SoftDistanceFog = true;
        [Range(0.05f, 0.9f)] public float FogStartNormalized = 0.45f;
        [Range(0.5f, 0.98f)] public float FogEndNormalized = 0.95f;
        public Color FogColor = new Color(0.62f, 0.70f, 0.78f, 1f);

        [Header("Terrain (Unity-recommended knobs)")]
        public bool CapTerrainDistances = true;
        [Tooltip("Must exceed camera far clip + tile size, otherwise tiles pop out in view.")]
        [Min(100f)] public float TerrainActiveDistance = 4000f;
        [Min(20f)] public float TerrainBasemapDistance = 200f;
        [Tooltip("5-8 keeps silhouettes clean; higher values visibly warp geometry.")]
        [Min(1f)] public float TerrainPixelError = 8f;
        [Min(0)] public int TerrainHeightmapMaxLod;
        public bool TerrainDrawTreesAndFoliage = false;
        public bool TerrainCastShadows = false;
        [Min(0)] public int TerrainShadowCasterBudget = 2;

        [Header("Panelka interior LOD")]
        [Tooltip("Hide furnished apartment interiors when far / outside (keep facade).")]
        public bool PanelkaInteriorLod = true;
        [Min(20f)] public float PanelkaInteriorLodDistance = 70f;

        [Header("Small prop cull")]
        public bool CullDistantSmallRenderers = true;
        [Min(20f)] public float SmallRendererCullDistance = 300f;
        [Min(0.1f)] public float SmallRendererMaxExtent = 2.2f;
        [Min(0.05f)] public float CullRefreshInterval = 0.35f;

        private static MiniVanGameModeRenderOptimizer instance;

        private MiniVanGameModeInteriorZone[] interiorZones;
        private Terrain[] terrains;
        private readonly List<Transform> panelkaAnchors = new List<Transform>(16);
        private readonly List<Renderer> smallRenderers = new List<Renderer>(1024);
        private readonly List<bool> smallRendererDefaultEnabled = new List<bool>(1024);
        private readonly List<Vector3> smallRendererCenters = new List<Vector3>(1024);
        private readonly List<Renderer> panelkaInteriorRenderers = new List<Renderer>(2048);
        private readonly List<bool> panelkaInteriorDefaultEnabled = new List<bool>(2048);
        private readonly List<Vector3> panelkaInteriorCenters = new List<Vector3>(2048);

        private float nextCullTime;
        private float nextModeCheckTime;
        private float nextAnchorRefreshTime;
        private int cullPassIndex;
        private bool lastInside;
        private CameraMode lastCameraMode = CameraMode.Unset;

        private bool capturedUrp;
        private float originalShadowDistance;
        private int originalCascadeCount;
        private int originalAdditionalLights;
        private float originalLodBias;
        private int originalPixelLights;
        private float originalQualityShadowDistance;

        private enum CameraMode
        {
            Unset = 0,
            Exterior = 1,
            NearPanelka = 2,
            Interior = 3
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoBootstrapAfterSceneLoad()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            Scene active = SceneManager.GetActiveScene();
            if (!active.IsValid() || !active.isLoaded)
            {
                return;
            }

            string sceneName = active.name;
            if (sceneName == null ||
                (sceneName.IndexOf("Game", System.StringComparison.OrdinalIgnoreCase) < 0 &&
                 sceneName.IndexOf("Panelka", System.StringComparison.OrdinalIgnoreCase) < 0 &&
                 sceneName.IndexOf("MiniVan", System.StringComparison.OrdinalIgnoreCase) < 0))
            {
                return;
            }

            EnsureOnHost();
        }

        /// <summary>
        /// Attaches to WorldGenerator / ManualBuilder / any existing instance.
        /// Does not create a dedicated "Render Optimizer" GameObject.
        /// </summary>
        public static MiniVanGameModeRenderOptimizer EnsureOnHost()
        {
            if (instance != null)
            {
                return instance;
            }

            MiniVanGameModeRenderOptimizer existing =
                FindFirstObjectByType<MiniVanGameModeRenderOptimizer>(
                    FindObjectsInactive.Include);
            if (existing != null)
            {
                instance = existing;
                if (!existing.enabled)
                {
                    existing.enabled = true;
                }

                return existing;
            }

            MiniVanGameModeWorldGenerator world =
                FindFirstObjectByType<MiniVanGameModeWorldGenerator>(
                    FindObjectsInactive.Include);
            if (world != null)
            {
                instance = world.gameObject.AddComponent<MiniVanGameModeRenderOptimizer>();
                return instance;
            }

            MiniVanPanelkaManualBuilder manual =
                FindFirstObjectByType<MiniVanPanelkaManualBuilder>(
                    FindObjectsInactive.Include);
            if (manual != null)
            {
                instance = manual.gameObject.AddComponent<MiniVanGameModeRenderOptimizer>();
                return instance;
            }

            return null;
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(this);
                return;
            }

            instance = this;
            ApplyFrameSettings();
            CaptureAndApplyUrpPreset();
            DisableSpareSceneCameras();
            CacheInteriorZones();
            CachePanelkaAnchors();
            CacheTerrains();
            ApplyTerrainCapsImmediate();
            CacheSmallRenderers();
            CachePanelkaInteriorLod();
            ApplySoftFog(inside: false);
        }

        private void OnDestroy()
        {
            RestoreUrpPreset();
            RestoreSmallRenderers();
            RestorePanelkaInteriorLod();
            if (instance == this)
            {
                instance = null;
            }
        }

        private void OnDisable()
        {
            RestoreSmallRenderers();
            RestorePanelkaInteriorLod();
            RestoreTerrainRendering();
        }

        private void RestoreTerrainRendering()
        {
            if (terrains == null)
            {
                return;
            }

            for (int i = 0; i < terrains.Length; i++)
            {
                if (terrains[i] != null && !terrains[i].enabled)
                {
                    terrains[i].enabled = true;
                }
            }
        }

        private void Update()
        {
            float now = Time.unscaledTime;
            Camera camera = ResolveCamera();
            Vector3 focus = camera != null
                ? camera.transform.position
                : transform.position;

            if (now >= nextModeCheckTime)
            {
                nextModeCheckTime = now + 0.2f;
                // Scene-wide scans are expensive; keep them rare. Rebuilds call
                // RefreshCullTargets explicitly, so this is only a safety net.
                if (now >= nextAnchorRefreshTime)
                {
                    nextAnchorRefreshTime = now + 15f;
                    CachePanelkaAnchors();
                    CacheInteriorZones();
                }

                UpdateCameraAndShadows(camera, focus);
            }

            if (now >= nextCullTime)
            {
                nextCullTime = now + Mathf.Max(0.05f, CullRefreshInterval);
                UpdateDistanceCull(focus);
            }
        }

        public void RefreshCullTargets()
        {
            CacheInteriorZones();
            CachePanelkaAnchors();
            CacheTerrains();
            ApplyTerrainCapsImmediate();
            CacheSmallRenderers();
            CachePanelkaInteriorLod();
        }

        private void ApplyFrameSettings()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = Mathf.Max(30, TargetFrameRate);
        }

        private void CaptureAndApplyUrpPreset()
        {
            if (!ApplyUrpPreset)
            {
                return;
            }

            UniversalRenderPipelineAsset urp =
                GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urp == null)
            {
                return;
            }

            if (!capturedUrp)
            {
                originalShadowDistance = urp.shadowDistance;
                originalCascadeCount = urp.shadowCascadeCount;
                originalAdditionalLights = urp.maxAdditionalLightsCount;
                originalLodBias = QualitySettings.lodBias;
                originalPixelLights = QualitySettings.pixelLightCount;
                originalQualityShadowDistance = QualitySettings.shadowDistance;
                capturedUrp = true;
            }

            urp.shadowCascadeCount = Mathf.Clamp(ShadowCascadeCount, 1, 4);
            urp.maxAdditionalLightsCount = Mathf.Clamp(MaxAdditionalLights, 0, 8);
            QualitySettings.lodBias = Mathf.Min(QualitySettings.lodBias, 1.1f);
            QualitySettings.pixelLightCount = Mathf.Min(QualitySettings.pixelLightCount, 2);
            ApplyShadowDistance(ExteriorShadowDistance);
        }

        private void RestoreUrpPreset()
        {
            if (!capturedUrp)
            {
                return;
            }

            UniversalRenderPipelineAsset urp =
                GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urp != null)
            {
                urp.shadowDistance = originalShadowDistance;
                urp.shadowCascadeCount = originalCascadeCount;
                urp.maxAdditionalLightsCount = originalAdditionalLights;
            }

            QualitySettings.lodBias = originalLodBias;
            QualitySettings.pixelLightCount = originalPixelLights;
            QualitySettings.shadowDistance = originalQualityShadowDistance;
            capturedUrp = false;
        }

        private void ApplyShadowDistance(float distance)
        {
            QualitySettings.shadowDistance = distance;
            UniversalRenderPipelineAsset urp =
                GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urp != null)
            {
                urp.shadowDistance = distance;
            }
        }

        private void DisableSpareSceneCameras()
        {
            Camera[] cameras = FindObjectsByType<Camera>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < cameras.Length; i++)
            {
                Camera camera = cameras[i];
                if (camera == null)
                {
                    continue;
                }

                if (camera.GetComponentInParent<MiniVanPlayer>() != null)
                {
                    continue;
                }

                if (camera.cameraType == CameraType.Game &&
                    camera.targetTexture == null &&
                    camera.gameObject.name == "Main Camera")
                {
                    camera.enabled = false;
                }
            }
        }

        private static Camera ResolveCamera()
        {
            MiniVanPlayer player = MiniVanPlayer.LocalPlayer;
            if (player != null && player.PlayerCamera != null)
            {
                return player.PlayerCamera;
            }

            Camera main = Camera.main;
            if (main != null && main.enabled)
            {
                return main;
            }

            Camera[] cameras = FindObjectsByType<Camera>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < cameras.Length; i++)
            {
                if (cameras[i] != null && cameras[i].enabled)
                {
                    return cameras[i];
                }
            }

            return null;
        }

        private void CacheInteriorZones()
        {
            interiorZones = FindObjectsByType<MiniVanGameModeInteriorZone>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
        }

        private void CachePanelkaAnchors()
        {
            panelkaAnchors.Clear();
            MiniVanPanelkaStage1Generator[] generators =
                FindObjectsByType<MiniVanPanelkaStage1Generator>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < generators.Length; i++)
            {
                if (generators[i] != null)
                {
                    panelkaAnchors.Add(generators[i].transform);
                }
            }

            MiniVanPanelkaManualBuilder[] builders =
                FindObjectsByType<MiniVanPanelkaManualBuilder>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < builders.Length; i++)
            {
                if (builders[i] != null)
                {
                    panelkaAnchors.Add(builders[i].transform);
                }
            }
        }

        private void CacheTerrains()
        {
            terrains = FindObjectsByType<Terrain>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
        }

        private void ApplyTerrainCapsImmediate()
        {
            if (!CapTerrainDistances)
            {
                return;
            }

            if (terrains == null || terrains.Length == 0)
            {
                CacheTerrains();
            }

            for (int i = 0; i < terrains.Length; i++)
            {
                ApplyTerrainPreset(terrains[i], castShadows: false);
            }
        }

        private void ApplyTerrainPreset(Terrain terrain, bool castShadows)
        {
            if (terrain == null)
            {
                return;
            }

            // Matches Unity terrain guidance: raise pixel error, cut basemap distance,
            // avoid TwoSided shadows on large tile grids, keep drawInstanced on.
            terrain.drawInstanced = true;
            terrain.drawTreesAndFoliage = TerrainDrawTreesAndFoliage;
            if (!TerrainDrawTreesAndFoliage)
            {
                terrain.treeDistance = 0f;
                terrain.detailObjectDistance = 0f;
                terrain.detailObjectDensity = 0f;
                terrain.treeBillboardDistance = 0f;
                terrain.treeMaximumFullLODCount = 0;
            }

            terrain.heightmapPixelError = TerrainPixelError;
            terrain.heightmapMaximumLOD = TerrainHeightmapMaxLod;
            terrain.basemapDistance = TerrainBasemapDistance;
            terrain.shadowCastingMode = castShadows && TerrainCastShadows
                ? ShadowCastingMode.On
                : ShadowCastingMode.Off;
            terrain.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        private void CacheSmallRenderers()
        {
            smallRenderers.Clear();
            smallRendererDefaultEnabled.Clear();
            smallRendererCenters.Clear();
            if (!CullDistantSmallRenderers)
            {
                return;
            }

            Renderer[] renderers = FindObjectsByType<Renderer>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || renderer is ParticleSystemRenderer)
                {
                    continue;
                }

                if (renderer.GetComponentInParent<MiniVanPlayer>() != null ||
                    renderer.GetComponentInParent<MiniVanVehicle>() != null ||
                    renderer.GetComponentInParent<Terrain>() != null)
                {
                    continue;
                }

                Bounds bounds = renderer.bounds;
                float maxExtent = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
                if (maxExtent > SmallRendererMaxExtent)
                {
                    continue;
                }

                string name = renderer.name;
                if (name.StartsWith("FacadeWall_", System.StringComparison.Ordinal) ||
                    name.StartsWith("Solid_", System.StringComparison.Ordinal))
                {
                    continue;
                }

                smallRenderers.Add(renderer);
                smallRendererDefaultEnabled.Add(renderer.enabled);
                smallRendererCenters.Add(bounds.center);
            }
        }

        private void UpdateCameraAndShadows(Camera camera, Vector3 focus)
        {
            bool inside = IsInsideInterior(focus);
            bool nearPanelka = !inside && IsNearPanelka(focus);
            CameraMode mode = inside
                ? CameraMode.Interior
                : nearPanelka
                    ? CameraMode.NearPanelka
                    : CameraMode.Exterior;

            float far = ExteriorCameraFarClip;
            float shadowDistance = ExteriorShadowDistance;
            if (mode == CameraMode.Interior)
            {
                far = InteriorCameraFarClip;
                shadowDistance = InteriorShadowDistance;
            }
            else if (mode == CameraMode.NearPanelka)
            {
                far = NearPanelkaCameraFarClip;
                shadowDistance = NearPanelkaShadowDistance;
            }

            if (camera != null)
            {
                // Smooth the far-clip change so distant geometry fades via fog
                // instead of popping when the mode switches.
                camera.farClipPlane = Mathf.MoveTowards(
                    camera.farClipPlane, far, 600f * 0.2f);
            }

            if (ApplyUrpPreset)
            {
                ApplyShadowDistance(shadowDistance);
            }

            if (mode != lastCameraMode || inside != lastInside)
            {
                ApplySoftFog(inside);
                lastInside = inside;
                lastCameraMode = mode;
            }
        }

        private bool IsInsideInterior(Vector3 worldPosition)
        {
            if (interiorZones == null)
            {
                CacheInteriorZones();
            }

            if (interiorZones == null)
            {
                return false;
            }

            for (int i = 0; i < interiorZones.Length; i++)
            {
                MiniVanGameModeInteriorZone zone = interiorZones[i];
                if (zone != null && zone.Contains(worldPosition))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsNearPanelka(Vector3 worldPosition)
        {
            float maxSqr = PanelkaProximityDistance * PanelkaProximityDistance;
            for (int i = 0; i < panelkaAnchors.Count; i++)
            {
                Transform anchor = panelkaAnchors[i];
                if (anchor == null)
                {
                    continue;
                }

                if ((anchor.position - worldPosition).sqrMagnitude <= maxSqr)
                {
                    return true;
                }
            }

            return false;
        }

        private void UpdateDistanceCull(Vector3 focus)
        {
            // One pass per tick keeps the worst-case frame cost low.
            cullPassIndex = (cullPassIndex + 1) % 3;
            switch (cullPassIndex)
            {
                case 0:
                    if (CapTerrainDistances)
                    {
                        CullTerrains(focus);
                    }

                    break;
                case 1:
                    if (CullDistantSmallRenderers)
                    {
                        CullSmallRenderers(focus);
                    }

                    break;
                default:
                    if (PanelkaInteriorLod)
                    {
                        CullPanelkaInteriors(focus);
                    }

                    break;
            }
        }

        private void CullTerrains(Vector3 focus)
        {
            if (terrains == null || terrains.Length == 0)
            {
                CacheTerrains();
            }

            float activeSqr = TerrainActiveDistance * TerrainActiveDistance;

            // Pick a few nearest tiles that may cast shadows.
            int budget = Mathf.Max(0, TerrainShadowCasterBudget);
            float[] nearestSqr = budget > 0 ? new float[budget] : null;
            Terrain[] nearest = budget > 0 ? new Terrain[budget] : null;
            if (nearestSqr != null)
            {
                for (int i = 0; i < budget; i++)
                {
                    nearestSqr[i] = float.PositiveInfinity;
                }
            }

            for (int i = 0; i < terrains.Length; i++)
            {
                Terrain terrain = terrains[i];
                if (terrain == null)
                {
                    continue;
                }

                Vector3 center = GetTerrainCenter(terrain);
                float sqr = (center - focus).sqrMagnitude;
                bool shouldBeActive = sqr <= activeSqr;

                // Only toggle rendering; the GameObject (and TerrainCollider)
                // must stay active or characters fall through distant tiles.
                if (terrain.enabled != shouldBeActive)
                {
                    terrain.enabled = shouldBeActive;
                }

                if (!shouldBeActive)
                {
                    continue;
                }

                if (nearestSqr != null)
                {
                    for (int slot = 0; slot < budget; slot++)
                    {
                        if (sqr < nearestSqr[slot])
                        {
                            for (int shift = budget - 1; shift > slot; shift--)
                            {
                                nearestSqr[shift] = nearestSqr[shift - 1];
                                nearest[shift] = nearest[shift - 1];
                            }

                            nearestSqr[slot] = sqr;
                            nearest[slot] = terrain;
                            break;
                        }
                    }
                }

                if (terrain.shadowCastingMode != ShadowCastingMode.Off)
                {
                    terrain.shadowCastingMode = ShadowCastingMode.Off;
                }
            }

            if (nearest != null && TerrainCastShadows)
            {
                for (int i = 0; i < nearest.Length; i++)
                {
                    if (nearest[i] != null)
                    {
                        nearest[i].shadowCastingMode = ShadowCastingMode.On;
                    }
                }
            }
        }

        private static Vector3 GetTerrainCenter(Terrain terrain)
        {
            Vector3 center = terrain.transform.position;
            TerrainData data = terrain.terrainData;
            if (data != null)
            {
                center += new Vector3(data.size.x * 0.5f, 0f, data.size.z * 0.5f);
            }

            return center;
        }

        private void CachePanelkaInteriorLod()
        {
            panelkaInteriorRenderers.Clear();
            panelkaInteriorDefaultEnabled.Clear();
            panelkaInteriorCenters.Clear();
            if (!PanelkaInteriorLod)
            {
                return;
            }

            MiniVanPanelkaStage1Generator[] generators =
                FindObjectsByType<MiniVanPanelkaStage1Generator>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int g = 0; g < generators.Length; g++)
            {
                if (generators[g] == null || generators[g].ExteriorOnlyLocked)
                {
                    continue;
                }

                Renderer[] renderers =
                    generators[g].GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < renderers.Length; i++)
                {
                    Renderer renderer = renderers[i];
                    if (renderer == null || !IsPanelkaInteriorRenderer(renderer.transform))
                    {
                        continue;
                    }

                    panelkaInteriorRenderers.Add(renderer);
                    panelkaInteriorDefaultEnabled.Add(renderer.enabled);
                    panelkaInteriorCenters.Add(renderer.bounds.center);
                }
            }
        }

        private static bool IsPanelkaInteriorRenderer(Transform transform)
        {
            Transform cursor = transform;
            while (cursor != null)
            {
                string name = cursor.name;
                if (name.StartsWith("FacadeWall_", System.StringComparison.Ordinal) ||
                    name.StartsWith("Solid_", System.StringComparison.Ordinal) ||
                    name.Contains("APARTMENT_LAYOUT_SHELL") ||
                    name.StartsWith("Glass_", System.StringComparison.Ordinal) ||
                    name == "Breakable_Glass")
                {
                    return false;
                }

                if (name.StartsWith("FURNISHED_", System.StringComparison.Ordinal) ||
                    name.StartsWith("ROOM_", System.StringComparison.Ordinal) ||
                    name.Contains("CONTENT__EDIT_THIS_PREFAB") ||
                    name.StartsWith("Landing_Furnishing_", System.StringComparison.Ordinal))
                {
                    return true;
                }

                cursor = cursor.parent;
            }

            return false;
        }

        private void CullPanelkaInteriors(Vector3 focus)
        {
            bool inside = IsInsideInterior(focus);
            float cullSqr = PanelkaInteriorLodDistance * PanelkaInteriorLodDistance;
            for (int i = 0; i < panelkaInteriorRenderers.Count; i++)
            {
                Renderer renderer = panelkaInteriorRenderers[i];
                if (renderer == null || !panelkaInteriorDefaultEnabled[i])
                {
                    continue;
                }

                bool visible = inside ||
                               (panelkaInteriorCenters[i] - focus).sqrMagnitude <= cullSqr;
                if (renderer.enabled != visible)
                {
                    renderer.enabled = visible;
                }
            }
        }

        private void RestorePanelkaInteriorLod()
        {
            for (int i = 0; i < panelkaInteriorRenderers.Count; i++)
            {
                Renderer renderer = panelkaInteriorRenderers[i];
                if (renderer != null)
                {
                    renderer.enabled = panelkaInteriorDefaultEnabled[i];
                }
            }
        }

        private void CullSmallRenderers(Vector3 focus)
        {
            float cullSqr = SmallRendererCullDistance * SmallRendererCullDistance;
            for (int i = 0; i < smallRenderers.Count; i++)
            {
                Renderer renderer = smallRenderers[i];
                if (renderer == null || !smallRendererDefaultEnabled[i])
                {
                    continue;
                }

                bool visible = (smallRendererCenters[i] - focus).sqrMagnitude <= cullSqr;
                if (renderer.enabled != visible)
                {
                    renderer.enabled = visible;
                }
            }
        }

        private void RestoreSmallRenderers()
        {
            for (int i = 0; i < smallRenderers.Count; i++)
            {
                Renderer renderer = smallRenderers[i];
                if (renderer != null)
                {
                    renderer.enabled = smallRendererDefaultEnabled[i];
                }
            }
        }

        private void ApplySoftFog(bool inside)
        {
            if (!SoftDistanceFog || inside)
            {
                RenderSettings.fog = false;
                return;
            }

            float far = lastCameraMode == CameraMode.NearPanelka
                ? NearPanelkaCameraFarClip
                : ExteriorCameraFarClip;
            far = Mathf.Max(100f, far);
            float startN = Mathf.Min(FogStartNormalized, FogEndNormalized - 0.05f);
            float endN = Mathf.Max(FogEndNormalized, startN + 0.05f);

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = far * startN;
            RenderSettings.fogEndDistance = far * endN;
            RenderSettings.fogColor = FogColor;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            TargetFrameRate = Mathf.Max(30, TargetFrameRate);
            if (!Application.isPlaying)
            {
                return;
            }

            ApplyFrameSettings();
            ApplySoftFog(lastInside);
        }
#endif
    }
}
