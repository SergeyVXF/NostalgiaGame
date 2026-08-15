using Unity.Netcode;
using UnityEngine;

namespace MiniVanGame
{
    public partial class MiniVanPlayer
    {
        private const float FlamethrowerBurstSeconds = 13.5f;
        private const float FlamethrowerRechargeSeconds = 10f;
        private const float FlamethrowerRange = 11f;
        private const float FlamethrowerConeRadius = 1.85f;

        private float flamethrowerCharge = 1f;
        private bool flamethrowerRecharging;
        private float flamethrowerRechargeTimer;
        private ParticleSystem flamethrowerParticles;
        private Transform flamethrowerHeldVisual;
        private bool flamethrowerFiringLocal;

        public bool HasFlamethrowerInInventory()
        {
            return HasInventoryItem(MiniVanInventoryItem.Flamethrower);
        }

        public void RequestTakeFlamethrower(MiniVanFlamethrowerRack rack = null)
        {
            if (!IsOwner || HasFlamethrowerInInventory())
            {
                return;
            }

            // Hide immediately for the local player; server confirms + syncs to others.
            if (rack != null)
            {
                rack.TryClaim();
            }

            Vector3 rackPos = rack != null ? rack.transform.position : transform.position;
            lastFlamethrowerRackPosition = rackPos;
            RequestTakeFlamethrowerServerRpc(rackPos);
        }

        private void HandleFlamethrowerUse()
        {
            if (!IsOwner)
            {
                return;
            }

            UpdateFlamethrowerCharge(Time.deltaTime);

            bool selected = GetInventorySlot(localSelectedSlot) == MiniVanInventoryItem.Flamethrower;
            if (!selected || currentSeat != null || currentSkateboard != null ||
                currentHoverboardM != null || heldTowCube != null || gearDragActive)
            {
                StopFlamethrowerLocal();
                return;
            }

            bool wantsFire = Input.GetMouseButton(0) && flamethrowerCharge > 0.001f && !flamethrowerRecharging;
            if (wantsFire)
            {
                flamethrowerFiringLocal = true;
                float drain = Time.deltaTime / FlamethrowerBurstSeconds;
                flamethrowerCharge = Mathf.Max(0f, flamethrowerCharge - drain);
                SetFlamethrowerParticles(true);
                ApplyFlamethrowerBurnLocal();

                if (flamethrowerCharge <= 0.001f)
                {
                    flamethrowerCharge = 0f;
                    flamethrowerRecharging = true;
                    flamethrowerRechargeTimer = 0f;
                    StopFlamethrowerLocal();
                }
            }
            else
            {
                StopFlamethrowerLocal();
            }
        }

        private void UpdateFlamethrowerCharge(float dt)
        {
            if (!flamethrowerRecharging)
            {
                return;
            }

            flamethrowerRechargeTimer += dt;
            flamethrowerCharge = Mathf.Clamp01(flamethrowerRechargeTimer / FlamethrowerRechargeSeconds);
            if (flamethrowerCharge >= 1f)
            {
                flamethrowerCharge = 1f;
                flamethrowerRecharging = false;
                flamethrowerRechargeTimer = 0f;
            }
        }

        private float nextFlamethrowerBurnRpcTime;

        private void ApplyFlamethrowerBurnLocal()
        {
            if (PlayerCamera == null || Time.time < nextFlamethrowerBurnRpcTime)
            {
                return;
            }

            nextFlamethrowerBurnRpcTime = Time.time + 0.06f;

            Vector3 origin = PlayerCamera.transform.position;
            Vector3 forward = PlayerCamera.transform.forward;

            if (IsServer || !IsSpawned)
            {
                MiniVanMeatMazeZone zone = MiniVanMeatMazeZone.Instance;
                if (zone == null)
                {
                    zone = FindFirstObjectByType<MiniVanMeatMazeZone>();
                }

                if (zone != null)
                {
                    zone.TryBurnBeam(origin, forward, FlamethrowerRange);
                }

                return;
            }

            RequestFlamethrowerBurnServerRpc(origin, forward);
        }

        private void StopFlamethrowerLocal()
        {
            flamethrowerFiringLocal = false;
            SetFlamethrowerParticles(false);
        }

        private void SetFlamethrowerParticles(bool enabled)
        {
            EnsureFlamethrowerParticles();
            if (flamethrowerParticles == null)
            {
                return;
            }

            if (enabled)
            {
                if (!flamethrowerParticles.isPlaying)
                {
                    flamethrowerParticles.Play();
                }
            }
            else if (flamethrowerParticles.isPlaying)
            {
                flamethrowerParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        private void EnsureFlamethrowerParticles()
        {
            if (flamethrowerParticles != null)
            {
                return;
            }

            Transform parent = PlayerCamera != null ? PlayerCamera.transform : transform;
            GameObject fx = new GameObject("FlamethrowerFX");
            fx.transform.SetParent(parent, false);
            fx.transform.localPosition = new Vector3(0.25f, -0.15f, 0.55f);
            fx.transform.localRotation = Quaternion.identity;

            flamethrowerParticles = fx.AddComponent<ParticleSystem>();
            var main = flamethrowerParticles.main;
            main.loop = true;
            main.startLifetime = 0.35f;
            main.startSpeed = 14f;
            main.startSize = 0.35f;
            main.startColor = new Color(1f, 0.45f, 0.05f, 1f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 80;
            main.gravityModifier = 0.05f;

            var emission = flamethrowerParticles.emission;
            emission.rateOverTime = 55f;

            var shape = flamethrowerParticles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 9f;
            shape.radius = 0.05f;

            var colorOverLifetime = flamethrowerParticles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 0.85f, 0.2f), 0f),
                    new GradientColorKey(new Color(1f, 0.25f, 0.02f), 0.55f),
                    new GradientColorKey(new Color(0.2f, 0.05f, 0.02f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0.95f, 0f),
                    new GradientAlphaKey(0.7f, 0.45f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = gradient;

            var renderer = fx.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            Shader particleShader = Shader.Find("Particles/Standard Unlit");
            if (particleShader == null)
            {
                particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            }

            if (particleShader != null)
            {
                renderer.sharedMaterial = new Material(particleShader)
                {
                    color = new Color(1f, 0.4f, 0.05f, 1f)
                };
            }

            flamethrowerParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private void UpdateFlamethrowerHeldVisual()
        {
            bool show = IsOwner &&
                        GetInventorySlot(localSelectedSlot) == MiniVanInventoryItem.Flamethrower &&
                        currentSeat == null;

            if (!show)
            {
                if (flamethrowerHeldVisual != null)
                {
                    flamethrowerHeldVisual.gameObject.SetActive(false);
                }

                return;
            }

            EnsureFlamethrowerHeldVisual();
            flamethrowerHeldVisual.gameObject.SetActive(true);
        }

        private void EnsureFlamethrowerHeldVisual()
        {
            if (flamethrowerHeldVisual != null)
            {
                return;
            }

            Transform parent = PlayerCamera != null ? PlayerCamera.transform : transform;
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "FlamethrowerHeld";
            Object.Destroy(visual.GetComponent<Collider>());
            visual.transform.SetParent(parent, false);
            visual.transform.localPosition = new Vector3(0.28f, -0.22f, 0.42f);
            visual.transform.localRotation = Quaternion.Euler(8f, 0f, 0f);
            visual.transform.localScale = new Vector3(0.12f, 0.12f, 0.55f);

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            Material material = new Material(shader)
            {
                color = new Color(0.18f, 0.18f, 0.2f, 1f)
            };
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", new Color(0.18f, 0.18f, 0.2f, 1f));
            }

            visual.GetComponent<Renderer>().sharedMaterial = material;
            flamethrowerHeldVisual = visual.transform;
        }

        private void DrawFlamethrowerRechargeBar()
        {
            if (!IsOwner || GetInventorySlot(localSelectedSlot) != MiniVanInventoryItem.Flamethrower)
            {
                return;
            }

            float width = 220f;
            float height = 16f;
            Rect back = new Rect(Screen.width * 0.5f - width * 0.5f, Screen.height - 118f, width, height);
            DrawSolidRect(back, new Color(0f, 0f, 0f, 0.7f));

            Color fillColor = flamethrowerRecharging
                ? new Color(1f, 0.55f, 0.1f, 0.95f)
                : new Color(0.2f, 0.85f, 0.25f, 0.95f);
            DrawSolidRect(new Rect(back.x + 2f, back.y + 2f, (back.width - 4f) * flamethrowerCharge, back.height - 4f), fillColor);

            GUIStyle style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12,
                fontStyle = FontStyle.Bold
            };
            style.normal.textColor = Color.white;
            string label = flamethrowerRecharging ? "RECHARGING" : "FLAMETHROWER";
            GUI.Label(new Rect(back.x, back.y - 18f, back.width, 18f), label, style);
        }

        [ServerRpc]
        private void RequestTakeFlamethrowerServerRpc(Vector3 rackPosition, ServerRpcParams rpcParams = default)
        {
            if (HasInventoryItem(MiniVanInventoryItem.Flamethrower))
            {
                return;
            }

            int emptySlot = FindFirstEmptyInventorySlot();
            if (emptySlot < 0)
            {
                return;
            }

            lastFlamethrowerRackPosition = rackPosition;
            SetInventorySlot(emptySlot, MiniVanInventoryItem.Flamethrower);
            networkSelectedSlot.Value = emptySlot;
            SetLocalInventorySlotClientRpc(emptySlot, (int)MiniVanInventoryItem.Flamethrower, BuildOwnerTarget());
            HideFlamethrowerRackClientRpc(rackPosition);
        }

        [ClientRpc]
        private void HideFlamethrowerRackClientRpc(Vector3 rackPosition)
        {
            MiniVanFlamethrowerRack.HideNearestAt(rackPosition, 8f);
        }

        [ServerRpc]
        private void RequestFlamethrowerBurnServerRpc(Vector3 origin, Vector3 direction, ServerRpcParams rpcParams = default)
        {
            MiniVanMeatMazeZone zone = MiniVanMeatMazeZone.Instance;
            if (zone == null)
            {
                zone = FindFirstObjectByType<MiniVanMeatMazeZone>();
            }

            if (zone == null)
            {
                return;
            }

            zone.TryBurnBeam(origin, direction, FlamethrowerRange);
        }
    }
}
