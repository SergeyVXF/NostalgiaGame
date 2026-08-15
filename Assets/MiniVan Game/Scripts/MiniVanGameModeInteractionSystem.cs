using System.Collections.Generic;
using UnityEngine;

namespace MiniVanGame
{
    public sealed class MiniVanGameModeInteractionSystem : MonoBehaviour
    {
        public float InteractionDistance = 4.5f;
        public float DoorInteractionDistance = 2.6f;
        public float RideAimRadius = 0.6f;
        public float RideProximityRadius = 2.4f;
        private IMiniVanGameModeInteractable current;
        private string prompt;
        private readonly RaycastHit[] hitBuffer = new RaycastHit[64];
        private readonly List<MonoBehaviour> behaviourBuffer = new List<MonoBehaviour>(8);
        private float nextScanTime;

        private static MiniVanGameModeInteractionSystem localInstance;

        public string CurrentPrompt => prompt;

        public static string GetLocalPrompt()
        {
            return localInstance != null ? localInstance.prompt : string.Empty;
        }

        public static bool HasLocalInteractable()
        {
            return localInstance != null && localInstance.current != null;
        }

        public static bool IsLocalWinchDashboardButton()
        {
            return localInstance != null && localInstance.current is MiniVanWinchDashboardButton;
        }

        public static bool IsLocalRoofAttach()
        {
            return localInstance != null && localInstance.current is MiniVanRoofAttachPoint;
        }

        private void OnEnable()
        {
            localInstance = this;
        }

        private void OnDisable()
        {
            if (localInstance == this)
            {
                localInstance = null;
            }

            SetCurrent(null);
        }

        private void Update()
        {
            MiniVanPlayer player = MiniVanPlayer.LocalPlayer;
            if (player == null)
            {
                SetCurrent(null);
                prompt = string.Empty;
                return;
            }

            bool interactDown = MiniVanKeyBindings.GetKeyDown(MiniVanKeyAction.Interact);
            bool primaryDown = Input.GetMouseButtonDown(0);
            bool secondaryDown = Input.GetMouseButtonDown(1);

            // Act on the already-highlighted target BEFORE rescanning on the same key frame.
            // Otherwise E can retarget away from RoofAttach and place/carry get out of sync.
            bool carryOwnsInteract =
                MiniVanBatteryCharger.GetCarriedBy(player) != null ||
                MiniVanWoodenBoard.GetCarriedBy(player) != null ||
                MiniVanWinchHook.GetCarriedBy(player) != null;

            if (!carryOwnsInteract && current != null)
            {
                if (interactDown)
                {
                    current.Interact(player);
                }
                else if (primaryDown)
                {
                    current.PrimaryAction(player);
                }
                else if (secondaryDown)
                {
                    current.Interact(player);
                }
            }

            bool forceScan = interactDown || primaryDown || secondaryDown;
            if (forceScan || Time.unscaledTime >= nextScanTime)
            {
                SetCurrent(FindInteractable(player));
                string nextPrompt = current != null ? current.GetPrompt(player) : string.Empty;
                // Zero-width prompts select the target without showing on-screen text.
                prompt = nextPrompt == "\u200B" ? string.Empty : nextPrompt;
                nextScanTime = Time.unscaledTime + 0.035f;
            }
        }

        private IMiniVanGameModeInteractable FindInteractable(MiniVanPlayer player)
        {
            if (player == null || player.PlayerCamera == null) return null;

            Ray ray = new Ray(
                player.PlayerCamera.transform.position,
                player.PlayerCamera.transform.forward);

            // Riding uses a third-person cam: the body blocks screen center and small ground
            // items are near impossible to hit with a thin ray, so aim with a thick sphere.
            RaycastHit[] hits;
            int hitCount;
            bool riding = player.IsRidingBoard;
            if (riding)
            {
                hits = Physics.SphereCastAll(
                    ray, RideAimRadius, InteractionDistance, ~0, QueryTriggerInteraction.Collide);
                hitCount = hits.Length;
            }
            else
            {
                hitCount = Physics.RaycastNonAlloc(
                    ray,
                    hitBuffer,
                    InteractionDistance,
                    ~0,
                    QueryTriggerInteraction.Collide);
                hits = hitBuffer;
            }

            IMiniVanGameModeInteractable best = null;
            float bestDistance = float.MaxValue;
            int bestPriority = int.MinValue;
            for (int i = 0; i < hitCount; i++)
            {
                if (player.ShouldIgnoreAimCollider(hits[i].collider))
                {
                    continue;
                }

                Consider(hits[i].collider, hits[i].distance, player,
                    ref best, ref bestDistance, ref bestPriority);
            }

            // Riding fallback: nothing under the (generous) crosshair — offer the nearest
            // interactable around the player so drive-by pickups still light up.
            if (best == null && riding)
            {
                Collider[] nearby = Physics.OverlapSphere(
                    player.transform.position, RideProximityRadius, ~0, QueryTriggerInteraction.Collide);
                for (int i = 0; i < nearby.Length; i++)
                {
                    if (player.ShouldIgnoreAimCollider(nearby[i]))
                    {
                        continue;
                    }

                    float distance = Vector3.Distance(
                        player.transform.position, nearby[i].transform.position);
                    Consider(nearby[i], distance, player,
                        ref best, ref bestDistance, ref bestPriority);
                }
            }

            // Carrying a wheel: aim wins (roof vs hub). If crosshair hits the van body,
            // fall back to whichever socket is more centered in view.
            MiniVanDetachedWheel carriedWheel = MiniVanDetachedWheel.GetCarriedBy(player);
            if (carriedWheel != null)
            {
                bool aimIsWheelTarget = best is MiniVanRoofAttachPoint || best is MiniVanWheelMountPoint;
                if (!aimIsWheelTarget)
                {
                    IMiniVanGameModeInteractable aimed =
                        MiniVanDetachedWheel.FindBestCarryInteractable(player, carriedWheel);
                    if (aimed != null)
                    {
                        best = aimed;
                    }
                }
            }

            return best;
        }

        private void Consider(
            Collider hitCollider,
            float distance,
            MiniVanPlayer player,
            ref IMiniVanGameModeInteractable best,
            ref float bestDistance,
            ref int bestPriority)
        {
            Transform target = hitCollider != null ? hitCollider.transform : null;
            while (target != null)
            {
                behaviourBuffer.Clear();
                target.GetComponents(behaviourBuffer);
                for (int b = 0; b < behaviourBuffer.Count; b++)
                {
                    if (!(behaviourBuffer[b] is IMiniVanGameModeInteractable interactable))
                        continue;

                    string candidatePrompt = interactable.GetPrompt(player);
                    if (string.IsNullOrEmpty(candidatePrompt))
                        continue;

                    float allowedDistance = interactable is MiniVanApartmentDoor
                        ? Mathf.Min(InteractionDistance, DoorInteractionDistance)
                        : InteractionDistance;
                    if (distance > allowedDistance)
                        continue;

                    int priority = GetInteractablePriority(interactable);
                    if (priority > bestPriority ||
                        (priority == bestPriority && distance < bestDistance))
                    {
                        best = interactable;
                        bestDistance = distance;
                        bestPriority = priority;
                    }
                }
                target = target.parent;
            }
        }

        private static int GetInteractablePriority(IMiniVanGameModeInteractable interactable)
        {
            // Cable plugs must win over the charger body — otherwise the chair steals E.
            if (interactable is MiniVanBridgeCableEnd)
                return 50;
            if (interactable is MiniVanWheelMountPoint)
                return 48;
            if (interactable is MiniVanRoofAttachPoint)
                return 48;
            if (interactable is MiniVanBridgeCableSocket)
                return 40;
            if (interactable is MiniVanWinchDashboardButton)
                return 35;
            if (interactable is MiniVanBatteryCharger)
                return 10;
            return 0;
        }

        private void SetCurrent(IMiniVanGameModeInteractable next)
        {
            if (ReferenceEquals(current, next))
            {
                return;
            }

            if (current is MiniVanGameModeGateLever oldLever)
            {
                oldLever.SetHighlighted(false);
            }

            if (current is MiniVanWinchDashboardButton oldWinchButton)
            {
                oldWinchButton.SetHighlighted(false);
            }

            if (current is MiniVanRoofAttachPoint oldRoof)
            {
                oldRoof.SetHighlighted(false);
            }

            if (current is MiniVanWinchHook oldHook)
            {
                oldHook.SetHighlighted(false);
            }

            if (current is MiniVanCraneControl oldCraneControl)
            {
                oldCraneControl.SetHighlighted(false);
            }

            current = next;

            if (current is MiniVanGameModeGateLever newLever)
            {
                newLever.SetHighlighted(true);
            }

            if (current is MiniVanWinchDashboardButton newWinchButton)
            {
                newWinchButton.SetHighlighted(true);
            }

            if (current is MiniVanRoofAttachPoint newRoof)
            {
                newRoof.SetHighlighted(true);
            }

            if (current is MiniVanWinchHook newHook)
            {
                newHook.SetHighlighted(true);
            }

            if (current is MiniVanCraneControl newCraneControl)
            {
                newCraneControl.SetHighlighted(true);
            }
        }

        private void OnGUI()
        {
            // While riding, the center HUD already includes this prompt (with step-off hint).
            MiniVanPlayer player = MiniVanPlayer.LocalPlayer;
            if (player != null && player.IsOwner && player.IsRidingBoard)
            {
                return;
            }

            if (!string.IsNullOrEmpty(prompt))
            {
                Color previous = GUI.color;
                if (current is MiniVanRoofAttachPoint)
                {
                    GUI.color = new Color(0.35f, 1f, 0.45f, 1f);
                }

                GUI.Box(new Rect(Screen.width * 0.5f - 170f, Screen.height - 118f, 340f, 34f), prompt);
                GUI.color = previous;
            }
        }
    }
}
