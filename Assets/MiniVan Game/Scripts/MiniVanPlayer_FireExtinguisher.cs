using Unity.Netcode;
using UnityEngine;

namespace MiniVanGame
{
    public partial class MiniVanPlayer
    {
        private const float ExtinguisherMaxCharge = 100f;
        private const float ExtinguisherCoolDegreesPerSecond = 18f;
        private const float ExtinguisherRange = 9f;
        private const float ExtinguisherAimRadius = 1.6f;

        private float extinguisherCharge = ExtinguisherMaxCharge;
        private ParticleSystem extinguisherParticles;
        private Transform extinguisherHeldVisual;
        private float nextExtinguisherCoolRpcTime;
        private bool extinguisherConsumeRequested;

        public bool HasFireExtinguisherInInventory()
        {
            return HasInventoryItem(MiniVanInventoryItem.FireExtinguisher);
        }

        public void RequestTakeFireExtinguisher(MiniVanFireExtinguisherPickup pickup)
        {
            if (!IsOwner || pickup == null || HasInventoryItem(MiniVanInventoryItem.FireExtinguisher))
            {
                return;
            }

            if (!pickup.IsInReach(transform.position))
            {
                return;
            }

            RequestTakeFireExtinguisherServerRpc(new NetworkObjectReference(pickup.NetworkObject));
        }

        private bool HandleFireExtinguisherDropInput()
        {
            if (!IsOwner || equipmentWindowOpen || IsDowned || currentSeat != null)
            {
                return false;
            }

            if (!IsSelectedInventoryItem(MiniVanInventoryItem.FireExtinguisher))
            {
                return false;
            }

            if (extinguisherCharge <= 0.001f)
            {
                return false;
            }

            RequestDropFireExtinguisherServerRpc(
                GetLooseItemDropPosition(),
                GetLooseItemDropRotation(),
                extinguisherCharge);
            if (!IsServer)
            {
                PredictClearInventoryItem(MiniVanInventoryItem.FireExtinguisher);
                extinguisherCharge = 0f;
                InvalidateStaticHeldVisuals();
                RefreshStaticHeldVisualsIfNeeded(true);
            }

            return true;
        }

        private void HandleFireExtinguisherUse()
        {
            if (!IsOwner)
            {
                return;
            }

            bool selected = GetInventorySlot(localSelectedSlot) == MiniVanInventoryItem.FireExtinguisher;
            // Allowed in the cabin — only block when on boards / dragging gear / towing.
            if (!selected || currentSkateboard != null || currentHoverboardM != null ||
                heldTowCube != null || gearDragActive)
            {
                StopFireExtinguisherLocal();
                return;
            }

            bool wantsSpray = Input.GetMouseButton(0) && extinguisherCharge > 0.001f;
            if (!wantsSpray)
            {
                StopFireExtinguisherLocal();
                return;
            }

            SetFireExtinguisherParticles(true);
            float coolBudget = ExtinguisherCoolDegreesPerSecond * Time.deltaTime;
            coolBudget = Mathf.Min(coolBudget, extinguisherCharge);
            if (coolBudget <= 0.0001f)
            {
                StopFireExtinguisherLocal();
                TryConsumeEmptyFireExtinguisher();
                return;
            }

            float applied = TryCoolAimedEngineLocal(coolBudget);
            if (applied > 0.0001f)
            {
                extinguisherCharge = Mathf.Max(0f, extinguisherCharge - applied);
            }
            else
            {
                // Still spend a little while spraying past the engine so the can empties if misused.
                extinguisherCharge = Mathf.Max(0f, extinguisherCharge - coolBudget * 0.15f);
            }

            if (extinguisherCharge <= 0.001f)
            {
                extinguisherCharge = 0f;
                StopFireExtinguisherLocal();
                TryConsumeEmptyFireExtinguisher();
            }
        }

        private void TryConsumeEmptyFireExtinguisher()
        {
            if (extinguisherConsumeRequested || extinguisherCharge > 0.001f)
            {
                return;
            }

            if (!HasInventoryItem(MiniVanInventoryItem.FireExtinguisher))
            {
                return;
            }

            extinguisherConsumeRequested = true;
            RequestConsumeEmptyFireExtinguisherServerRpc();
            InvalidateStaticHeldVisuals();
            RefreshStaticHeldVisualsIfNeeded(true);
        }

        private float TryCoolAimedEngineLocal(float degrees)
        {
            if (PlayerCamera == null || degrees <= 0f)
            {
                return 0f;
            }

            MiniVanVehicle vehicle = FindAimedVehicleEngine(
                PlayerCamera.transform.position,
                PlayerCamera.transform.forward);
            if (vehicle == null)
            {
                return 0f;
            }

            if (IsServer || !IsSpawned)
            {
                return vehicle.ServerApplyExtinguisherCool(degrees) ? degrees : 0f;
            }

            if (Time.time >= nextExtinguisherCoolRpcTime)
            {
                nextExtinguisherCoolRpcTime = Time.time + 0.05f;
                RequestExtinguisherCoolServerRpc(new NetworkObjectReference(vehicle.NetworkObject), degrees);
            }

            // Predict local drain; server is authoritative on temperature.
            return degrees;
        }

        private static MiniVanVehicle FindAimedVehicleEngine(Vector3 origin, Vector3 direction)
        {
            if (direction.sqrMagnitude < 0.0001f)
            {
                return null;
            }

            direction.Normalize();
            MiniVanVehicle[] vehicles = Object.FindObjectsByType<MiniVanVehicle>(FindObjectsSortMode.None);
            MiniVanVehicle best = null;
            float bestScore = float.MaxValue;

            for (int i = 0; i < vehicles.Length; i++)
            {
                MiniVanVehicle vehicle = vehicles[i];
                if (vehicle == null)
                {
                    continue;
                }

                Vector3 enginePoint = vehicle.GetEngineCoolWorldPoint();
                Vector3 toEngine = enginePoint - origin;
                float along = Vector3.Dot(toEngine, direction);
                if (along < 0.2f || along > ExtinguisherRange)
                {
                    continue;
                }

                float lateral = Vector3.Cross(direction, toEngine).magnitude;
                float allowed = ExtinguisherAimRadius * Mathf.Lerp(0.55f, 1.35f, along / ExtinguisherRange);
                if (lateral > allowed)
                {
                    continue;
                }

                float score = lateral + along * 0.05f;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = vehicle;
                }
            }

            return best;
        }

        private void StopFireExtinguisherLocal()
        {
            SetFireExtinguisherParticles(false);
        }

        private void SetFireExtinguisherParticles(bool enabled)
        {
            EnsureFireExtinguisherParticles();
            if (extinguisherParticles == null)
            {
                return;
            }

            if (enabled)
            {
                if (!extinguisherParticles.isPlaying)
                {
                    extinguisherParticles.Play();
                }
            }
            else if (extinguisherParticles.isPlaying)
            {
                extinguisherParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        private void EnsureFireExtinguisherParticles()
        {
            if (extinguisherParticles != null)
            {
                return;
            }

            Transform parent = PlayerCamera != null ? PlayerCamera.transform : transform;
            GameObject fx = new GameObject("FireExtinguisherFX");
            fx.transform.SetParent(parent, false);
            fx.transform.localPosition = new Vector3(0.28f, -0.18f, 0.55f);
            fx.transform.localRotation = Quaternion.identity;

            extinguisherParticles = fx.AddComponent<ParticleSystem>();
            var main = extinguisherParticles.main;
            main.loop = true;
            main.startLifetime = 0.55f;
            main.startSpeed = 11f;
            main.startSize = 0.22f;
            main.startColor = new Color(0.85f, 0.95f, 1f, 0.85f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 120;
            main.gravityModifier = 0.35f;

            var emission = extinguisherParticles.emission;
            emission.rateOverTime = 70f;

            var shape = extinguisherParticles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 7f;
            shape.radius = 0.03f;

            var colorOverLifetime = extinguisherParticles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 1f, 1f), 0f),
                    new GradientColorKey(new Color(0.7f, 0.88f, 1f), 0.45f),
                    new GradientColorKey(new Color(0.55f, 0.7f, 0.85f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0.9f, 0f),
                    new GradientAlphaKey(0.45f, 0.55f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = gradient;

            var renderer = fx.GetComponent<ParticleSystemRenderer>();
            Shader particleShader = Shader.Find("Particles/Standard Unlit");
            if (particleShader == null)
            {
                particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            }

            if (particleShader != null && renderer != null)
            {
                renderer.sharedMaterial = new Material(particleShader)
                {
                    color = new Color(0.85f, 0.95f, 1f, 0.8f)
                };
            }

            extinguisherParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private void UpdateFireExtinguisherHeldVisual()
        {
            bool show = IsOwner &&
                        GetInventorySlot(localSelectedSlot) == MiniVanInventoryItem.FireExtinguisher &&
                        extinguisherCharge > 0.001f;

            if (!show)
            {
                if (extinguisherHeldVisual != null)
                {
                    extinguisherHeldVisual.gameObject.SetActive(false);
                }

                return;
            }

            EnsureFireExtinguisherHeldVisual();
            extinguisherHeldVisual.gameObject.SetActive(true);
        }

        private void EnsureFireExtinguisherHeldVisual()
        {
            if (extinguisherHeldVisual != null)
            {
                return;
            }

            Transform parent = PlayerCamera != null ? PlayerCamera.transform : transform;
            GameObject visual = new GameObject("FireExtinguisherHeld");
            visual.transform.SetParent(parent, false);
            visual.transform.localPosition = new Vector3(0.32f, -0.28f, 0.48f);
            visual.transform.localRotation = Quaternion.Euler(12f, -8f, 18f);
            visual.transform.localScale = Vector3.one * 0.85f;
            MiniVanFireExtinguisherPickup.EnsureBuiltVisual(visual);
            Collider[] cols = visual.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < cols.Length; i++)
            {
                if (cols[i] != null)
                {
                    Object.Destroy(cols[i]);
                }
            }

            extinguisherHeldVisual = visual.transform;
        }

        private void DrawFireExtinguisherChargeBar()
        {
            if (!IsOwner || GetInventorySlot(localSelectedSlot) != MiniVanInventoryItem.FireExtinguisher)
            {
                return;
            }

            float width = 220f;
            float height = 16f;
            Rect back = new Rect(Screen.width * 0.5f - width * 0.5f, Screen.height - 118f, width, height);
            DrawSolidRect(back, new Color(0f, 0f, 0f, 0.7f));

            float t = Mathf.Clamp01(extinguisherCharge / ExtinguisherMaxCharge);
            Color fillColor = t > 0.25f
                ? new Color(0.25f, 0.75f, 1f, 0.95f)
                : new Color(1f, 0.35f, 0.15f, 0.95f);
            DrawSolidRect(new Rect(back.x + 2f, back.y + 2f, (back.width - 4f) * t, back.height - 4f), fillColor);

            GUIStyle style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12,
                fontStyle = FontStyle.Bold
            };
            style.normal.textColor = Color.white;
            GUI.Label(
                new Rect(back.x, back.y - 18f, back.width, 18f),
                "EXTINGUISHER  " + Mathf.RoundToInt(extinguisherCharge) + "/100",
                style);
        }

        [ServerRpc]
        private void RequestTakeFireExtinguisherServerRpc(
            NetworkObjectReference pickupReference,
            ServerRpcParams rpcParams = default)
        {
            if (HasInventoryItem(MiniVanInventoryItem.FireExtinguisher))
            {
                return;
            }

            if (!pickupReference.TryGet(out NetworkObject pickupObject))
            {
                return;
            }

            MiniVanFireExtinguisherPickup pickup = pickupObject.GetComponent<MiniVanFireExtinguisherPickup>();
            if (pickup == null || !pickup.IsInReach(transform.position))
            {
                return;
            }

            int emptySlot = FindFirstEmptyInventorySlot();
            if (emptySlot < 0)
            {
                return;
            }

            if (!pickup.TryClaim(out float charge))
            {
                return;
            }

            SetInventorySlot(emptySlot, MiniVanInventoryItem.FireExtinguisher);
            networkSelectedSlot.Value = emptySlot;
            SetLocalInventorySlotClientRpc(emptySlot, (int)MiniVanInventoryItem.FireExtinguisher, BuildOwnerTarget());
            SetExtinguisherChargeClientRpc(charge, BuildOwnerTarget());
        }

        [ServerRpc]
        private void RequestDropFireExtinguisherServerRpc(
            Vector3 dropPosition,
            Quaternion dropRotation,
            float charge,
            ServerRpcParams rpcParams = default)
        {
            if (currentSeat != null)
            {
                return;
            }

            int slot = FindInventorySlot(MiniVanInventoryItem.FireExtinguisher);
            if (slot < 0)
            {
                return;
            }

            float clampedCharge = Mathf.Clamp(charge, 0f, ExtinguisherMaxCharge);
            if (clampedCharge <= 0.001f)
            {
                return;
            }

            if (MiniVanFireExtinguisherPickup.ServerSpawn(dropPosition, dropRotation, clampedCharge) == null)
            {
                return;
            }

            SetInventorySlot(slot, MiniVanInventoryItem.None);
            SetLocalInventorySlotClientRpc(slot, (int)MiniVanInventoryItem.None, BuildOwnerTarget());
            SetExtinguisherChargeClientRpc(0f, BuildOwnerTarget());
        }

        [ServerRpc]
        private void RequestConsumeEmptyFireExtinguisherServerRpc(ServerRpcParams rpcParams = default)
        {
            int slot = FindInventorySlot(MiniVanInventoryItem.FireExtinguisher);
            if (slot < 0)
            {
                return;
            }

            SetInventorySlot(slot, MiniVanInventoryItem.None);
            SetLocalInventorySlotClientRpc(slot, (int)MiniVanInventoryItem.None, BuildOwnerTarget());
            SetExtinguisherChargeClientRpc(0f, BuildOwnerTarget());
        }

        [ServerRpc]
        private void RequestExtinguisherCoolServerRpc(
            NetworkObjectReference vehicleReference,
            float degrees,
            ServerRpcParams rpcParams = default)
        {
            if (!HasInventoryItem(MiniVanInventoryItem.FireExtinguisher))
            {
                return;
            }

            if (!vehicleReference.TryGet(out NetworkObject vehicleObject))
            {
                return;
            }

            MiniVanVehicle vehicle = vehicleObject.GetComponent<MiniVanVehicle>();
            if (vehicle == null)
            {
                return;
            }

            vehicle.ServerApplyExtinguisherCool(Mathf.Clamp(degrees, 0f, 5f));
        }

        [ClientRpc]
        private void SetExtinguisherChargeClientRpc(float charge, ClientRpcParams clientRpcParams = default)
        {
            extinguisherCharge = Mathf.Clamp(charge, 0f, ExtinguisherMaxCharge);
            extinguisherConsumeRequested = false;
            if (extinguisherCharge <= 0.001f)
            {
                StopFireExtinguisherLocal();
                if (extinguisherHeldVisual != null)
                {
                    extinguisherHeldVisual.gameObject.SetActive(false);
                }
            }
        }
    }
}
