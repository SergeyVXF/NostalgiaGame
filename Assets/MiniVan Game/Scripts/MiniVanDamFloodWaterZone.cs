using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MiniVanGame
{
    /// <summary>
    /// Gameplay water volume for the flooded central channel. While flooded it:
    ///  - slows the player (x PlayerSlowDivisor)
    ///  - slows the minivan (40% if &lt; half submerged, 80% if more)
    ///  - drains the minivan battery faster (x BatteryDrainMultiplier)
    ///  - applies a blue URP Volume look when the local camera is under the water surface
    /// Drains over time when the pump runs and the dam is closed, then disables.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class MiniVanDamFloodWaterZone : MonoBehaviour
    {
        [Header("Effects")]
        public float PlayerSlowDivisor = 2.5f;
        [Tooltip("Speed reduction when less than half of the minivan is underwater (0.4 = 40% slower).")]
        [Range(0f, 0.95f)] public float VehiclePartialSlowFraction = 0.4f;
        [Tooltip("Speed reduction when half or more of the minivan is underwater (0.8 = 80% slower).")]
        [Range(0f, 0.95f)] public float VehicleFullSlowFraction = 0.8f;
        /// <summary>Battery drains 50% faster while the minivan is in water.</summary>
        public float BatteryDrainMultiplier = 1.5f;
        [Obsolete("Use VehiclePartialSlowFraction / VehicleFullSlowFraction.")]
        public float VehicleSlowDivisor = 5f;

        [Header("Visual")]
        public Renderer WaterRenderer;
        public Transform WaterSurface;
        [Tooltip("How far the visible water drops in world meters while draining.")]
        public float SurfaceDrainDrop = 8f;

        [Header("Underwater Look (URP Volume)")]
        public bool EnableUnderwaterVolume = true;
        public Color UnderwaterColorFilter = new Color(0.18f, 0.48f, 0.92f, 1f);
        [Range(-100f, 100f)] public float UnderwaterSaturation = -10f;
        [Range(-100f, 100f)] public float UnderwaterContrast = 15f;
        [Range(0f, 1f)] public float UnderwaterVignetteIntensity = 0.45f;
        public float UnderwaterBlendSpeed = 8f;
        public float UnderwaterSurfaceBias = 0.05f;
        public float UnderwaterVolumePriority = 100f;

        public bool IsFlooded { get; private set; } = true;

        private readonly HashSet<MiniVanPlayer> playersInside = new HashSet<MiniVanPlayer>();
        private readonly HashSet<MiniVanVehicle> vehiclesInside = new HashSet<MiniVanVehicle>();
        private BoxCollider zone;
        private Vector3 surfaceStartLocalPos;
        private Vector3 rendererStartLocalPos;
        private Vector3 rendererStartWorldPos;
        private bool hasRendererStart;
        private float drainProgress;
        private float surfaceStartWorldY;

        private Volume underwaterVolume;
        private VolumeProfile underwaterProfile;
        private ColorAdjustments colorAdjustments;
        private Vignette vignette;

        private void Awake()
        {
            zone = GetComponent<BoxCollider>();
            zone.isTrigger = true;
            CaptureWaterVisualStarts();

            if (EnableUnderwaterVolume)
            {
                EnsureUnderwaterVolume();
            }
        }

        private void CaptureWaterVisualStarts()
        {
            if (WaterRenderer == null)
            {
                Transform modeled = FindNamedTransform(transform.root, "Modeled_Channel_Water_Surface");
                if (modeled != null)
                {
                    WaterRenderer = modeled.GetComponent<Renderer>();
                    if (WaterSurface == null)
                    {
                        WaterSurface = modeled;
                    }
                }
            }

            if (WaterSurface != null)
            {
                surfaceStartLocalPos = WaterSurface.localPosition;
                surfaceStartWorldY = GetWaterSurfaceY();
            }

            if (WaterRenderer != null)
            {
                rendererStartLocalPos = WaterRenderer.transform.localPosition;
                rendererStartWorldPos = WaterRenderer.transform.position;
                hasRendererStart = true;
                if (WaterSurface == null)
                {
                    WaterSurface = WaterRenderer.transform;
                    surfaceStartLocalPos = WaterSurface.localPosition;
                }

                surfaceStartWorldY = GetWaterSurfaceY();
            }
        }

        private static Transform FindNamedTransform(Transform root, string objectName)
        {
            if (root == null)
            {
                return null;
            }

            if (root.name == objectName)
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindNamedTransform(root.GetChild(i), objectName);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private void LateUpdate()
        {
            UpdateUnderwaterLook();
            RefreshImmersedPlayersSurface();
            RefreshVehiclesInWater();
        }

        private void RefreshImmersedPlayersSurface()
        {
            if (!IsFlooded || playersInside.Count == 0)
            {
                return;
            }

            float surfaceY = GetWaterSurfaceY();
            foreach (MiniVanPlayer player in playersInside)
            {
                if (player != null)
                {
                    player.SetDamWaterSurfaceY(surfaceY);
                }
            }
        }

        private void RefreshVehiclesInWater()
        {
            if (!IsFlooded || zone == null || !zone.enabled)
            {
                ClearVehicleEffectsOnly();
                return;
            }

            float surfaceY = GetWaterSurfaceY();
            Bounds queryBounds = zone.bounds;

            Collider[] hits = Physics.OverlapBox(
                queryBounds.center,
                queryBounds.extents,
                zone.transform.rotation,
                ~0,
                QueryTriggerInteraction.Collide);

            HashSet<MiniVanVehicle> currentlyAffected = new HashSet<MiniVanVehicle>();
            for (int i = 0; i < hits.Length; i++)
            {
                Collider hit = hits[i];
                if (hit == null)
                {
                    continue;
                }

                MiniVanVehicle vehicle = hit.GetComponentInParent<MiniVanVehicle>();
                if (vehicle == null || currentlyAffected.Contains(vehicle))
                {
                    continue;
                }

                float submersion01 = GetVehicleSubmersion01(vehicle, surfaceY);
                if (submersion01 < 0.05f)
                {
                    continue;
                }

                currentlyAffected.Add(vehicle);
                float slowFraction = submersion01 < 0.5f ? VehiclePartialSlowFraction : VehicleFullSlowFraction;
                float keepSpeed = Mathf.Clamp(1f - slowFraction, 0.05f, 1f);
                float divisor = 1f / keepSpeed;
                vehicle.SetDamWaterEffect(divisor, BatteryDrainMultiplier, submersion01);
            }

            foreach (MiniVanVehicle vehicle in vehiclesInside)
            {
                if (vehicle != null && !currentlyAffected.Contains(vehicle))
                {
                    vehicle.ClearDamWaterEffect();
                }
            }

            vehiclesInside.Clear();
            foreach (MiniVanVehicle vehicle in currentlyAffected)
            {
                vehiclesInside.Add(vehicle);
            }
        }

        private static float GetVehicleSubmersion01(MiniVanVehicle vehicle, float surfaceY)
        {
            if (vehicle == null)
            {
                return 0f;
            }

            Bounds bounds = new Bounds(vehicle.transform.position, Vector3.one * 0.5f);
            bool hasBounds = false;
            Collider[] colliders = vehicle.GetComponentsInChildren<Collider>();
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider == null || !collider.enabled || collider.isTrigger)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = collider.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(collider.bounds);
                }
            }

            if (!hasBounds)
            {
                Renderer[] renderers = vehicle.GetComponentsInChildren<Renderer>();
                for (int i = 0; i < renderers.Length; i++)
                {
                    Renderer renderer = renderers[i];
                    if (renderer == null || !renderer.enabled)
                    {
                        continue;
                    }

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

            if (!hasBounds)
            {
                return 0f;
            }

            float height = Mathf.Max(0.05f, bounds.max.y - bounds.min.y);
            return Mathf.Clamp01((surfaceY - bounds.min.y) / height);
        }

        private void ClearVehicleEffectsOnly()
        {
            foreach (MiniVanVehicle vehicle in vehiclesInside)
            {
                if (vehicle != null)
                {
                    vehicle.ClearDamWaterEffect();
                }
            }

            vehiclesInside.Clear();
        }

        private void OnDisable()
        {
            ClearAllEffects();
            SetUnderwaterWeight(0f, true);
        }

        private void OnDestroy()
        {
            if (underwaterProfile != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(underwaterProfile);
                }
                else
                {
                    DestroyImmediate(underwaterProfile);
                }

                underwaterProfile = null;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsFlooded) return;
            MiniVanPlayer player = other.GetComponentInParent<MiniVanPlayer>();
            if (player != null && playersInside.Add(player))
            {
                player.SetDamWaterSlow(PlayerSlowDivisor);
                player.SetDamWaterSurfaceY(GetWaterSurfaceY());
            }
        }

        private void OnTriggerExit(Collider other)
        {
            MiniVanPlayer player = other.GetComponentInParent<MiniVanPlayer>();
            if (player != null && playersInside.Remove(player))
            {
                player.ClearDamWaterSlow();
            }
        }

        /// <summary>0..1 while draining; lowers Modeled_Channel_Water_Surface over time.</summary>
        public void SetDrainProgress(float progress)
        {
            drainProgress = Mathf.Clamp01(progress);
            float drop = GetEffectiveDrainDrop();

            // Primary visual: modeled channel water mesh.
            if (hasRendererStart && WaterRenderer != null)
            {
                Vector3 world = rendererStartWorldPos;
                world.y = rendererStartWorldPos.y - drop * drainProgress;
                WaterRenderer.transform.position = world;
            }
            else if (WaterRenderer != null)
            {
                Vector3 local = rendererStartLocalPos;
                local.y = rendererStartLocalPos.y - drop * drainProgress;
                WaterRenderer.transform.localPosition = local;
            }

            // Secondary / legacy thin plane (parent or separate).
            if (WaterSurface != null &&
                (WaterRenderer == null || WaterSurface != WaterRenderer.transform))
            {
                Vector3 pos = surfaceStartLocalPos;
                pos.y = surfaceStartLocalPos.y - drop * drainProgress;
                WaterSurface.localPosition = pos;
            }
        }

        private float GetEffectiveDrainDrop()
        {
            float drop = Mathf.Max(0.5f, SurfaceDrainDrop);
            if (WaterRenderer != null)
            {
                // Ensure the whole water volume sinks below the channel.
                drop = Mathf.Max(drop, WaterRenderer.bounds.size.y + 2f);
            }

            return drop;
        }

        /// <summary>Fully drained: disable effects and hide water.</summary>
        public void SetDrained()
        {
            IsFlooded = false;
            ClearAllEffects();
            SetUnderwaterWeight(0f, true);
            SetDrainProgress(1f);

            if (WaterRenderer != null)
            {
                WaterRenderer.enabled = false;
                WaterRenderer.gameObject.SetActive(false);
            }

            if (WaterSurface != null)
            {
                WaterSurface.gameObject.SetActive(false);
                Transform waterParent = WaterSurface.parent;
                if (waterParent != null && waterParent.name.Contains("Flood_Water"))
                {
                    waterParent.gameObject.SetActive(false);
                }
            }

            if (zone != null)
            {
                zone.enabled = false;
            }

            // Disable leftover water colliders in case any were added.
            Transform root = transform.root;
            DisableNamedColliders(root, "Modeled_Channel_Water_Surface");
            DisableNamedColliders(root, "Flood_Water_Surface_In_Depression");
        }

        private static void DisableNamedColliders(Transform root, string objectName)
        {
            Transform target = FindNamedTransform(root, objectName);
            if (target == null)
            {
                return;
            }

            target.gameObject.SetActive(false);
            Collider[] colliders = target.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                {
                    colliders[i].enabled = false;
                }
            }
        }

        private void ClearAllEffects()
        {
            foreach (MiniVanPlayer player in playersInside)
            {
                if (player != null) player.ClearDamWaterSlow();
            }
            playersInside.Clear();

            foreach (MiniVanVehicle vehicle in vehiclesInside)
            {
                if (vehicle != null) vehicle.ClearDamWaterEffect();
            }
            vehiclesInside.Clear();
        }

        private void UpdateUnderwaterLook()
        {
            if (!EnableUnderwaterVolume || !IsFlooded)
            {
                SetUnderwaterWeight(0f, false);
                return;
            }

            if (underwaterVolume == null)
            {
                EnsureUnderwaterVolume();
            }

            bool underwater = IsLocalCameraUnderwater();
            if (underwater)
            {
                EnsureLocalCameraPostProcessing();
            }

            SetUnderwaterWeight(underwater ? 1f : 0f, false);
        }

        private bool IsLocalCameraUnderwater()
        {
            MiniVanPlayer localPlayer = MiniVanPlayer.LocalPlayer;

            // Channel is shallow: while the body is in the flood trigger, show the blue look.
            // Otherwise players walk "in water" with the camera above the surface and see nothing.
            if (localPlayer != null && playersInside.Contains(localPlayer))
            {
                return true;
            }

            Camera camera = ResolveLocalCamera();
            if (camera == null)
            {
                return false;
            }

            Vector3 cameraPosition = camera.transform.position;
            float surfaceY = GetWaterSurfaceY();
            if (cameraPosition.y >= surfaceY - UnderwaterSurfaceBias)
            {
                return false;
            }

            if (WaterRenderer != null && WaterRenderer.enabled && WaterRenderer.gameObject.activeInHierarchy)
            {
                Bounds waterBounds = WaterRenderer.bounds;
                waterBounds.Expand(0.35f);
                if (waterBounds.Contains(cameraPosition))
                {
                    return true;
                }
            }

            if (zone == null)
            {
                return false;
            }

            Bounds bounds = zone.bounds;
            return cameraPosition.x >= bounds.min.x && cameraPosition.x <= bounds.max.x &&
                   cameraPosition.z >= bounds.min.z && cameraPosition.z <= bounds.max.z;
        }

        private float GetWaterSurfaceY()
        {
            if (WaterRenderer != null && WaterRenderer.enabled && WaterRenderer.gameObject.activeInHierarchy)
            {
                return WaterRenderer.bounds.max.y;
            }

            if (WaterSurface != null)
            {
                Renderer surfaceRenderer = WaterSurface.GetComponentInChildren<Renderer>();
                if (surfaceRenderer != null && surfaceRenderer.enabled)
                {
                    return surfaceRenderer.bounds.max.y;
                }

                return WaterSurface.position.y;
            }

            return zone != null ? zone.bounds.max.y : transform.position.y;
        }

        private static Camera ResolveLocalCamera()
        {
            MiniVanPlayer localPlayer = MiniVanPlayer.LocalPlayer;
            if (localPlayer != null)
            {
                if (localPlayer.PlayerCamera != null)
                {
                    return localPlayer.PlayerCamera;
                }

                Camera childCamera = localPlayer.GetComponentInChildren<Camera>();
                if (childCamera != null)
                {
                    return childCamera;
                }
            }

            Camera main = Camera.main;
            if (main != null)
            {
                return main;
            }

            Camera[] cameras = Camera.allCameras;
            for (int i = 0; i < cameras.Length; i++)
            {
                if (cameras[i] != null && cameras[i].enabled && cameras[i].gameObject.activeInHierarchy)
                {
                    return cameras[i];
                }
            }

            return null;
        }

        private void EnsureLocalCameraPostProcessing()
        {
            Camera camera = ResolveLocalCamera();
            if (camera == null)
            {
                return;
            }

            UniversalAdditionalCameraData cameraData = camera.GetComponent<UniversalAdditionalCameraData>();
            if (cameraData == null)
            {
                cameraData = camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            }

            if (!cameraData.renderPostProcessing)
            {
                cameraData.renderPostProcessing = true;
            }
        }

        private void SetUnderwaterWeight(float target, bool instant)
        {
            if (underwaterVolume == null)
            {
                return;
            }

            if (instant || UnderwaterBlendSpeed <= 0.01f)
            {
                underwaterVolume.weight = target;
                return;
            }

            underwaterVolume.weight = Mathf.MoveTowards(
                underwaterVolume.weight,
                target,
                UnderwaterBlendSpeed * Time.deltaTime);
        }

        private void EnsureUnderwaterVolume()
        {
            const string volumeObjectName = "DamUnderwaterVolume";
            Transform existing = transform.Find(volumeObjectName);
            GameObject volumeObject;
            if (existing != null)
            {
                volumeObject = existing.gameObject;
            }
            else
            {
                volumeObject = new GameObject(volumeObjectName);
                volumeObject.transform.SetParent(transform, false);
                volumeObject.transform.localPosition = Vector3.zero;
            }

            underwaterVolume = volumeObject.GetComponent<Volume>();
            if (underwaterVolume == null)
            {
                underwaterVolume = volumeObject.AddComponent<Volume>();
            }

            underwaterVolume.isGlobal = true;
            underwaterVolume.priority = UnderwaterVolumePriority;
            underwaterVolume.weight = 0f;

            if (underwaterProfile == null)
            {
                underwaterProfile = ScriptableObject.CreateInstance<VolumeProfile>();
                underwaterProfile.name = "DamUnderwaterProfile_Runtime";
                ConfigureUnderwaterProfile(underwaterProfile);
            }
            else
            {
                ConfigureUnderwaterProfile(underwaterProfile);
            }

            underwaterVolume.profile = underwaterProfile;
            EnsureLocalCameraPostProcessing();
        }

        private void ConfigureUnderwaterProfile(VolumeProfile profile)
        {
            if (!profile.TryGet(out colorAdjustments))
            {
                colorAdjustments = profile.Add<ColorAdjustments>(true);
            }

            colorAdjustments.active = true;
            colorAdjustments.colorFilter.Override(UnderwaterColorFilter);
            colorAdjustments.saturation.Override(UnderwaterSaturation);
            colorAdjustments.contrast.Override(UnderwaterContrast);
            colorAdjustments.postExposure.Override(-0.35f);

            if (!profile.TryGet(out vignette))
            {
                vignette = profile.Add<Vignette>(true);
            }

            vignette.active = true;
            vignette.color.Override(new Color(0.02f, 0.12f, 0.35f, 1f));
            vignette.intensity.Override(UnderwaterVignetteIntensity);
            vignette.smoothness.Override(0.45f);
            vignette.rounded.Override(true);
        }
    }
}
