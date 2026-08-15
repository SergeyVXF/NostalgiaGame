using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;

namespace MiniVanGame
{
    public partial class MiniVanPlayer
    {
        private MiniVanSnesTelevision lookedAtSnesTelevision;
        private MiniVanSnesConsole lookedAtSnesConsole;
        private MiniVanSnesCartridge lookedAtSnesCartridge;
        private Transform lookedAtSnesPowerButton;
        private MiniVanSnesTelevision activeSnesTelevision;
        private MiniVanSnesTelevision heldSnesTelevision;
        private MiniVanSnesConsole heldSnesConsole;
        private MiniVanSnesCartridge heldSnesCartridge;

        private GameObject snesPlacementGhost;
        private float snesPlaceYaw;
        private static Material snesGhostGreenMaterial;

        public bool IsPlayingSnesTelevision() => activeSnesTelevision != null;
        public bool IsHoldingSnesCartridge() => heldSnesCartridge != null;
        public bool IsHoldingSnesConsole() => heldSnesConsole != null;
        public bool IsHoldingSnesTelevision() => heldSnesTelevision != null;

        public void NotifySnesTelevisionStopped(MiniVanSnesTelevision television)
        {
            if (activeSnesTelevision == television)
            {
                RestoreSnesCameraPose();
                activeSnesTelevision = null;
            }
        }

        private void HandleSnesTelevisionMode()
        {
            if (activeSnesTelevision == null)
            {
                return;
            }

            HandleWalkingLook();

            // Stream joypad to the server-hosted emulator.
            if (activeSnesTelevision.NetworkObject != null && activeSnesTelevision.NetworkObject.IsSpawned)
            {
                ushort mask = MiniVanSnesJoypad.SampleLocalBitmask();
                RequestSnesTelevisionInputServerRpc(
                    new NetworkObjectReference(activeSnesTelevision.NetworkObject),
                    mask);
            }

            if (MiniVanKeyBindings.GetKeyDown(MiniVanKeyAction.Interact))
            {
                StopSnesTelevision();
            }
        }

        private void StopSnesTelevisionIfNeeded()
        {
            if (activeSnesTelevision != null)
            {
                StopSnesTelevision();
            }
        }

        private void StopSnesTelevision()
        {
            if (activeSnesTelevision == null)
            {
                return;
            }

            MiniVanSnesTelevision tv = activeSnesTelevision;
            activeSnesTelevision = null;
            RestoreSnesCameraPose();
            if (tv.NetworkObject != null && tv.NetworkObject.IsSpawned)
            {
                RequestSnesTelevisionLeaveServerRpc(new NetworkObjectReference(tv.NetworkObject));
            }

            tv.NotifyLocalPlayEnded(this);
            NotifySnesTelevisionStopped(tv);
        }

        private bool TryStartSnesTelevision(MiniVanSnesTelevision television)
        {
            if (!IsOwner || television == null || activeSnesTelevision != null)
            {
                return false;
            }

            if (currentSeat != null || currentSkateboard != null || currentHoverboardM != null)
            {
                return false;
            }

            if (!television.IsPlayableForClients() || television.NetworkObject == null)
            {
                return false;
            }

            RequestSnesTelevisionJoinServerRpc(new NetworkObjectReference(television.NetworkObject));
            return true;
        }

        private void RestoreSnesCameraPose()
        {
            pitch = Mathf.Clamp(pitch, -82f, 82f);
            seatYaw = 0f;
            if (CameraRoot != null)
            {
                CameraRoot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            }

            if (PlayerCamera != null)
            {
                PlayerCamera.transform.localRotation = Quaternion.identity;
                PlayerCamera.transform.localPosition = Vector3.zero;
            }
        }

        private const float SnesTelevisionOutlineMaxSeconds = 15f;

        private MiniVanSnesTelevision snesTvOutlineTracked;
        private float snesTvOutlineLookStartTime = -1f;

        private void ClearSnesHighlights()
        {
            lookedAtSnesTelevision?.SetHighlighted(false);
            lookedAtSnesConsole?.SetHighlighted(false);
            lookedAtSnesConsole?.SetPowerButtonHighlighted(false);
            lookedAtSnesCartridge?.SetHighlighted(false);
        }

        private void ApplySnesHighlight()
        {
            // With cart inserted: only cart + ON/OFF light up (never the whole console body).
            if (lookedAtSnesPowerButton != null && lookedAtSnesConsole != null)
            {
                ResetSnesTelevisionOutlineTracking();
                lookedAtSnesConsole.SetPowerButtonHighlighted(true);
                return;
            }

            if (lookedAtSnesCartridge != null)
            {
                ResetSnesTelevisionOutlineTracking();
                lookedAtSnesCartridge.SetHighlighted(true);
                return;
            }

            if (lookedAtSnesConsole != null && !lookedAtSnesConsole.HasInsertedCartridge())
            {
                ResetSnesTelevisionOutlineTracking();
                lookedAtSnesConsole.SetHighlighted(true);
                return;
            }

            if (lookedAtSnesTelevision != null)
            {
                if (snesTvOutlineTracked != lookedAtSnesTelevision)
                {
                    snesTvOutlineTracked = lookedAtSnesTelevision;
                    snesTvOutlineLookStartTime = Time.time;
                }

                bool showOutline = Time.time - snesTvOutlineLookStartTime < SnesTelevisionOutlineMaxSeconds;
                lookedAtSnesTelevision.SetHighlighted(showOutline);
                return;
            }

            ResetSnesTelevisionOutlineTracking();
        }

        private void ResetSnesTelevisionOutlineTracking()
        {
            snesTvOutlineTracked = null;
            snesTvOutlineLookStartTime = -1f;
        }

        private void UpdateSnesPlacementGhost()
        {
            if (!IsOwner)
            {
                return;
            }

            bool tvReady = heldSnesTelevision != null;
            bool consoleReady = heldSnesConsole != null &&
                                IsSelectedInventoryItem(MiniVanInventoryItem.SnesConsole);
            bool placing = (tvReady || consoleReady) && activeSnesTelevision == null;
            if (!placing)
            {
                HideSnesPlacementGhost();
                return;
            }

            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                snesPlaceYaw += scroll > 0f ? 15f : -15f;
            }

            if (PlayerCamera == null ||
                !TryGetSnesPlacePose(out Vector3 pos, out Quaternion rot))
            {
                HideSnesPlacementGhost();
                return;
            }

            GameObject source = heldSnesTelevision != null
                ? heldSnesTelevision.gameObject
                : heldSnesConsole.gameObject;
            EnsureSnesPlacementGhost(source);
            if (snesPlacementGhost != null)
            {
                float rootScale = 1f;
                if (heldSnesTelevision != null)
                {
                    rootScale = heldSnesTelevision.GhostRootScaleMultiplier;
                }

                snesPlacementGhost.transform.localScale = Vector3.one * rootScale;
                snesPlacementGhost.transform.SetPositionAndRotation(pos, rot);
                snesPlacementGhost.SetActive(true);
            }
        }

        private bool TryGetSnesPlacePose(out Vector3 position, out Quaternion rotation)
        {
            position = default;
            rotation = default;
            if (PlayerCamera == null)
            {
                return false;
            }

            Ray ray = new Ray(PlayerCamera.transform.position, PlayerCamera.transform.forward);
            if (!Physics.Raycast(ray, out RaycastHit hit, 5.5f, ~0, QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            if (Vector3.Dot(hit.normal, Vector3.up) < 0.35f)
            {
                return false;
            }

            position = hit.point;
            Vector3 flatForward = Vector3.ProjectOnPlane(PlayerCamera.transform.forward, Vector3.up);
            if (flatForward.sqrMagnitude < 0.001f)
            {
                flatForward = Vector3.forward;
            }

            rotation = Quaternion.LookRotation(flatForward.normalized, Vector3.up) *
                       Quaternion.Euler(0f, snesPlaceYaw, 0f);
            return true;
        }

        private void EnsureSnesPlacementGhost(GameObject source)
        {
            if (snesPlacementGhost != null && snesPlacementGhost.name == source.name + "_Ghost")
            {
                return;
            }

            HideSnesPlacementGhost();
            snesPlacementGhost = new GameObject(source.name + "_Ghost");
            snesPlacementGhost.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);

            MeshFilter[] filters = source.GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < filters.Length; i++)
            {
                MeshFilter filter = filters[i];
                if (filter == null || filter.sharedMesh == null)
                {
                    continue;
                }

                GameObject part = new GameObject(filter.name);
                part.transform.SetParent(snesPlacementGhost.transform, false);
                part.transform.localPosition = snesPlacementGhost.transform.InverseTransformPoint(filter.transform.position);
                part.transform.localRotation = Quaternion.Inverse(snesPlacementGhost.transform.rotation) * filter.transform.rotation;
                part.transform.localScale = DivideScale(filter.transform.lossyScale, snesPlacementGhost.transform.lossyScale);

                MeshFilter ghostFilter = part.AddComponent<MeshFilter>();
                ghostFilter.sharedMesh = filter.sharedMesh;
                MeshRenderer ghostRenderer = part.AddComponent<MeshRenderer>();
                ghostRenderer.sharedMaterial = GetSnesGhostGreenMaterial();
                ghostRenderer.shadowCastingMode = ShadowCastingMode.Off;
                ghostRenderer.receiveShadows = false;
            }
        }

        private static Vector3 DivideScale(Vector3 a, Vector3 b)
        {
            return new Vector3(
                Mathf.Abs(b.x) > 0.0001f ? a.x / b.x : a.x,
                Mathf.Abs(b.y) > 0.0001f ? a.y / b.y : a.y,
                Mathf.Abs(b.z) > 0.0001f ? a.z / b.z : a.z);
        }

        private void HideSnesPlacementGhost()
        {
            if (snesPlacementGhost != null)
            {
                Destroy(snesPlacementGhost);
                snesPlacementGhost = null;
            }
        }

        private static Material GetSnesGhostGreenMaterial()
        {
            if (snesGhostGreenMaterial != null)
            {
                return snesGhostGreenMaterial;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            snesGhostGreenMaterial = new Material(shader)
            {
                name = "SnesPlacementGhostGreen",
                color = new Color(0.15f, 1f, 0.3f, 0.45f)
            };
            if (snesGhostGreenMaterial.HasProperty("_BaseColor"))
            {
                snesGhostGreenMaterial.SetColor("_BaseColor", new Color(0.15f, 1f, 0.3f, 0.45f));
            }

            if (snesGhostGreenMaterial.HasProperty("_Surface"))
            {
                snesGhostGreenMaterial.SetFloat("_Surface", 1f);
            }

            snesGhostGreenMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            snesGhostGreenMaterial.SetOverrideTag("RenderType", "Transparent");
            return snesGhostGreenMaterial;
        }

        private bool HandleSnesInteractionInput()
        {
            if (!IsOwner || IsPlayingSnesTelevision())
            {
                return false;
            }

            bool cartSelected = heldSnesCartridge != null &&
                                IsSelectedInventoryItem(MiniVanInventoryItem.SnesCartridge);
            bool consoleSelected = heldSnesConsole != null &&
                                   IsSelectedInventoryItem(MiniVanInventoryItem.SnesConsole);

            // Drop held SNES gear with Q.
            if (MiniVanKeyBindings.GetKeyDown(MiniVanKeyAction.Drop))
            {
                if (heldSnesTelevision != null)
                {
                    MiniVanSnesTelevision tv = heldSnesTelevision;
                    if (!IsServer)
                    {
                        Vector3 dropPos = transform.position + transform.forward * 0.8f + Vector3.up * 0.35f;
                        heldSnesTelevision = null;
                        HideSnesPlacementGhost();
                        tv.BeginLocalReleasePrediction(this, dropPos, transform.rotation, physicalDrop: true);
                    }

                    RequestSnesTelevisionDropServerRpc();
                    return true;
                }

                if (consoleSelected)
                {
                    MiniVanSnesConsole console = heldSnesConsole;
                    if (!IsServer)
                    {
                        Vector3 dropPos = transform.position + transform.forward * 0.8f + Vector3.up * 0.35f;
                        heldSnesConsole = null;
                        PredictClearInventoryItem(MiniVanInventoryItem.SnesConsole);
                        HideSnesPlacementGhost();
                        console.BeginLocalReleasePrediction(this, dropPos, transform.rotation, physicalDrop: true);
                    }

                    RequestSnesConsoleDropServerRpc();
                    return true;
                }

                if (cartSelected)
                {
                    MiniVanSnesCartridge cart = heldSnesCartridge;
                    if (!IsServer)
                    {
                        Vector3 dropPos = transform.position + transform.forward * 0.8f + Vector3.up * 0.35f;
                        heldSnesCartridge = null;
                        PredictClearInventoryItem(MiniVanInventoryItem.SnesCartridge);
                        cart.BeginLocalDropPrediction(this, dropPos, transform.rotation);
                    }

                    RequestSnesCartridgeDropServerRpc();
                    return true;
                }
            }

            if (!MiniVanKeyBindings.GetKeyDown(MiniVanKeyAction.Interact))
            {
                return false;
            }

            // Power button has priority when looked at.
            if (lookedAtSnesPowerButton != null && lookedAtSnesConsole != null && !cartSelected &&
                !consoleSelected && heldSnesTelevision == null)
            {
                RequestSnesConsoleTogglePowerServerRpc(new NetworkObjectReference(lookedAtSnesConsole.NetworkObject));
                return true;
            }

            // Insert cartridge into console.
            if (cartSelected && lookedAtSnesConsole != null)
            {
                RequestSnesCartridgeInsertServerRpc(new NetworkObjectReference(lookedAtSnesConsole.NetworkObject));
                return true;
            }

            // Remove inserted cartridge.
            if (lookedAtSnesCartridge != null && lookedAtSnesCartridge.IsInserted && heldSnesCartridge == null)
            {
                RequestSnesCartridgeEjectServerRpc(new NetworkObjectReference(lookedAtSnesCartridge.NetworkObject));
                return true;
            }

            // Place held TV / console.
            if (heldSnesTelevision != null)
            {
                if (TryGetSnesPlacePose(out Vector3 pos, out Quaternion rot))
                {
                    MiniVanSnesTelevision tv = heldSnesTelevision;
                    if (!IsServer)
                    {
                        heldSnesTelevision = null;
                        HideSnesPlacementGhost();
                        tv.BeginLocalReleasePrediction(this, pos, rot, physicalDrop: false);
                    }

                    RequestSnesTelevisionPlaceServerRpc(pos, rot);
                }

                return true;
            }

            if (consoleSelected)
            {
                if (TryGetSnesPlacePose(out Vector3 pos, out Quaternion rot))
                {
                    MiniVanSnesConsole console = heldSnesConsole;
                    if (!IsServer)
                    {
                        heldSnesConsole = null;
                        PredictClearInventoryItem(MiniVanInventoryItem.SnesConsole);
                        HideSnesPlacementGhost();
                        console.BeginLocalReleasePrediction(this, pos, rot, physicalDrop: false);
                    }

                    RequestSnesConsolePlaceServerRpc(pos, rot);
                }

                return true;
            }

            // Play on TV (server-hosted emulator).
            if (lookedAtSnesTelevision != null && lookedAtSnesTelevision.IsPlayableForClients())
            {
                TryStartSnesTelevision(lookedAtSnesTelevision);
                return true;
            }

            // Pickup.
            if (lookedAtSnesCartridge != null && lookedAtSnesCartridge.IsAvailable)
            {
                RequestSnesCartridgePickupServerRpc(new NetworkObjectReference(lookedAtSnesCartridge.NetworkObject));
                return true;
            }

            if (lookedAtSnesConsole != null && lookedAtSnesConsole.CanPickup)
            {
                RequestSnesConsolePickupServerRpc(new NetworkObjectReference(lookedAtSnesConsole.NetworkObject));
                return true;
            }

            if (lookedAtSnesTelevision != null && lookedAtSnesTelevision.IsAvailable)
            {
                RequestSnesTelevisionPickupServerRpc(new NetworkObjectReference(lookedAtSnesTelevision.NetworkObject));
                return true;
            }

            return false;
        }

        private string GetSnesInteractionPrompt()
        {
            if (IsPlayingSnesTelevision())
            {
                return null; // dedicated HUD
            }

            bool cartSelected = heldSnesCartridge != null &&
                                IsSelectedInventoryItem(MiniVanInventoryItem.SnesCartridge);
            bool consoleSelected = heldSnesConsole != null &&
                                   IsSelectedInventoryItem(MiniVanInventoryItem.SnesConsole);

            if (lookedAtSnesPowerButton != null && lookedAtSnesConsole != null &&
                !cartSelected && !consoleSelected && heldSnesTelevision == null)
            {
                return lookedAtSnesConsole.GetPowerButtonPrompt();
            }

            if (cartSelected && lookedAtSnesConsole != null)
            {
                return "E - insert cartridge";
            }

            if (heldSnesTelevision != null)
            {
                return MiniVanSnesTelevision.PromptPlace;
            }

            if (consoleSelected)
            {
                return "E - place  |  scroll - rotate  |  Q - drop";
            }

            if (cartSelected)
            {
                return "Q - drop cartridge";
            }

            if (lookedAtSnesCartridge != null)
            {
                return lookedAtSnesCartridge.GetPrompt(this);
            }

            if (lookedAtSnesConsole != null)
            {
                // Don't show "pick up" while a cart is inserted — cart prompt already covers remove.
                if (lookedAtSnesConsole.HasInsertedCartridge())
                {
                    return null;
                }

                return lookedAtSnesConsole.GetPrompt(this);
            }

            if (lookedAtSnesTelevision != null)
            {
                return lookedAtSnesTelevision.GetPrompt(this);
            }

            return null;
        }

        private void DrawSnesControlsHint()
        {
            const float pad = 14f;
            const float width = 210f;
            const float lineHeight = 22f;

            string[] lines =
            {
                "SNES CONTROLS",
                "",
                "W  —  Up",
                "S  —  Down",
                "A  —  Left",
                "D  —  Right",
                "",
                "H  —  A",
                "J  —  B",
                "K  —  X",
                "L  —  Y",
                "",
                "U  —  L",
                "I  —  R",
                "",
                "Z  —  Start",
                "X  —  Select",
                "",
                "E  —  Exit TV",
            };

            float height = pad * 2f + lines.Length * lineHeight;
            Rect box = new Rect(Screen.width - width - 16f, Screen.height - height - 16f, width, height);

            Color prev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.62f);
            GUI.Box(box, GUIContent.none);
            GUI.color = prev;

            GUIStyle title = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 15,
                fontStyle = FontStyle.Bold
            };
            title.normal.textColor = Color.white;

            GUIStyle row = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 14
            };
            row.normal.textColor = new Color(0.92f, 0.94f, 0.96f, 1f);

            float y = box.y + pad;
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (string.IsNullOrEmpty(line))
                {
                    y += lineHeight * 0.45f;
                    continue;
                }

                GUI.Label(new Rect(box.x + pad, y, width - pad * 2f, lineHeight), line, i == 0 ? title : row);
                y += lineHeight;
            }
        }

        #region ServerRpcs

        private MiniVanSnesTelevision FindCarriedTelevisionServer()
        {
            MiniVanSnesTelevision[] items = FindObjectsByType<MiniVanSnesTelevision>(FindObjectsSortMode.None);
            for (int i = 0; i < items.Length; i++)
            {
                if (items[i] != null && items[i].CarriedByClientId == OwnerClientId)
                {
                    return items[i];
                }
            }

            return null;
        }

        private MiniVanSnesConsole FindCarriedConsoleServer()
        {
            MiniVanSnesConsole[] items = FindObjectsByType<MiniVanSnesConsole>(FindObjectsSortMode.None);
            for (int i = 0; i < items.Length; i++)
            {
                if (items[i] != null && items[i].CarriedByClientId == OwnerClientId)
                {
                    return items[i];
                }
            }

            return null;
        }

        private MiniVanSnesCartridge FindCarriedCartridgeServer()
        {
            MiniVanSnesCartridge[] items = FindObjectsByType<MiniVanSnesCartridge>(FindObjectsSortMode.None);
            for (int i = 0; i < items.Length; i++)
            {
                if (items[i] != null && items[i].CarriedByClientId == OwnerClientId)
                {
                    return items[i];
                }
            }

            return null;
        }

        [ServerRpc]
        private void RequestSnesTelevisionPickupServerRpc(NetworkObjectReference reference, ServerRpcParams rpcParams = default)
        {
            if (!reference.TryGet(out NetworkObject obj))
            {
                return;
            }

            MiniVanSnesTelevision tv = obj.GetComponent<MiniVanSnesTelevision>();
            if (tv == null || !tv.TryPickupServer(this))
            {
                return;
            }

            SetHeldSnesTelevisionClientRpc(reference, BuildOwnerTarget());
        }

        [ServerRpc]
        private void RequestSnesTelevisionPlaceServerRpc(Vector3 position, Quaternion rotation, ServerRpcParams rpcParams = default)
        {
            MiniVanSnesTelevision tv = FindCarriedTelevisionServer();
            if (tv == null || !tv.TryDropOrPlaceServer(this, position, rotation))
            {
                return;
            }

            ClearHeldSnesTelevisionClientRpc(BuildOwnerTarget());
        }

        [ServerRpc]
        private void RequestSnesTelevisionDropServerRpc(ServerRpcParams rpcParams = default)
        {
            MiniVanSnesTelevision tv = FindCarriedTelevisionServer();
            if (tv == null)
            {
                return;
            }

            Vector3 dropPos = transform.position + transform.forward * 0.8f + Vector3.up * 0.35f;
            if (tv.TryDropOrPlaceServer(this, dropPos, transform.rotation, physicalDrop: true))
            {
                ClearHeldSnesTelevisionClientRpc(BuildOwnerTarget());
            }
        }

        [ClientRpc]
        private void SetHeldSnesTelevisionClientRpc(NetworkObjectReference reference, ClientRpcParams clientRpcParams = default)
        {
            if (reference.TryGet(out NetworkObject obj))
            {
                heldSnesTelevision = obj.GetComponent<MiniVanSnesTelevision>();
                snesPlaceYaw = 0f;
            }
        }

        [ClientRpc]
        private void ClearHeldSnesTelevisionClientRpc(ClientRpcParams clientRpcParams = default)
        {
            heldSnesTelevision = null;
            HideSnesPlacementGhost();
        }

        [ServerRpc]
        private void RequestSnesConsolePickupServerRpc(NetworkObjectReference reference, ServerRpcParams rpcParams = default)
        {
            if (!reference.TryGet(out NetworkObject obj))
            {
                return;
            }

            MiniVanSnesConsole console = obj.GetComponent<MiniVanSnesConsole>();
            int empty = FindFirstEmptyInventorySlot();
            if (console == null || empty < 0 || !console.TryPickupServer(this))
            {
                return;
            }

            SetInventorySlot(empty, MiniVanInventoryItem.SnesConsole);
            SetHeldSnesConsoleClientRpc(reference, empty, BuildOwnerTarget());
        }

        [ServerRpc]
        private void RequestSnesConsolePlaceServerRpc(Vector3 position, Quaternion rotation, ServerRpcParams rpcParams = default)
        {
            MiniVanSnesConsole console = FindCarriedConsoleServer();
            if (console == null || !console.TryPlaceServer(this, position, rotation))
            {
                return;
            }

            ClearSnesConsoleInventorySlot();
            ClearHeldSnesConsoleClientRpc(BuildOwnerTarget());
        }

        [ServerRpc]
        private void RequestSnesConsoleDropServerRpc(ServerRpcParams rpcParams = default)
        {
            MiniVanSnesConsole console = FindCarriedConsoleServer();
            if (console == null)
            {
                return;
            }

            Vector3 dropPos = transform.position + transform.forward * 0.8f + Vector3.up * 0.3f;
            if (console.TryDropServer(this, dropPos, transform.rotation))
            {
                ClearSnesConsoleInventorySlot();
                ClearHeldSnesConsoleClientRpc(BuildOwnerTarget());
            }
        }

        [ServerRpc]
        private void RequestSnesConsoleTogglePowerServerRpc(NetworkObjectReference reference, ServerRpcParams rpcParams = default)
        {
            if (!reference.TryGet(out NetworkObject obj))
            {
                return;
            }

            MiniVanSnesConsole console = obj.GetComponent<MiniVanSnesConsole>();
            if (console != null && console.IsInRange(transform.position))
            {
                console.TogglePowerServer();
            }
        }

        [ClientRpc]
        private void SetHeldSnesConsoleClientRpc(NetworkObjectReference reference, int slot, ClientRpcParams clientRpcParams = default)
        {
            if (reference.TryGet(out NetworkObject obj))
            {
                heldSnesConsole = obj.GetComponent<MiniVanSnesConsole>();
                snesPlaceYaw = 0f;
            }
        }

        [ClientRpc]
        private void ClearHeldSnesConsoleClientRpc(ClientRpcParams clientRpcParams = default)
        {
            heldSnesConsole = null;
            HideSnesPlacementGhost();
        }

        private void ClearSnesConsoleInventorySlot()
        {
            int slot = FindInventorySlot(MiniVanInventoryItem.SnesConsole);
            if (slot >= 0)
            {
                SetInventorySlot(slot, MiniVanInventoryItem.None);
            }
        }

        [ServerRpc]
        private void RequestSnesCartridgePickupServerRpc(NetworkObjectReference reference, ServerRpcParams rpcParams = default)
        {
            if (!reference.TryGet(out NetworkObject obj))
            {
                return;
            }

            MiniVanSnesCartridge cart = obj.GetComponent<MiniVanSnesCartridge>();
            int empty = FindFirstEmptyInventorySlot();
            if (cart == null || empty < 0 || !cart.TryPickupServer(this))
            {
                return;
            }

            SetInventorySlot(empty, MiniVanInventoryItem.SnesCartridge);
            SetHeldSnesCartridgeClientRpc(reference, BuildOwnerTarget());
        }

        [ServerRpc]
        private void RequestSnesCartridgeDropServerRpc(ServerRpcParams rpcParams = default)
        {
            MiniVanSnesCartridge cart = FindCarriedCartridgeServer();
            if (cart == null)
            {
                return;
            }

            Vector3 dropPos = transform.position + transform.forward * 0.7f + Vector3.up * 0.35f;
            if (cart.TryDropServer(this, dropPos, transform.rotation))
            {
                ClearSnesCartridgeInventorySlot();
                ClearHeldSnesCartridgeClientRpc(BuildOwnerTarget());
            }
        }

        [ServerRpc]
        private void RequestSnesCartridgeInsertServerRpc(NetworkObjectReference consoleReference, ServerRpcParams rpcParams = default)
        {
            MiniVanSnesCartridge cart = FindCarriedCartridgeServer();
            if (cart == null || !consoleReference.TryGet(out NetworkObject consoleObject))
            {
                return;
            }

            MiniVanSnesConsole console = consoleObject.GetComponent<MiniVanSnesConsole>();
            if (console == null || !console.TryInsertCartridgeServer(this, cart))
            {
                return;
            }

            ClearSnesCartridgeInventorySlot();
            ClearHeldSnesCartridgeClientRpc(BuildOwnerTarget());
        }

        [ServerRpc]
        private void RequestSnesCartridgeEjectServerRpc(NetworkObjectReference cartReference, ServerRpcParams rpcParams = default)
        {
            if (!cartReference.TryGet(out NetworkObject obj))
            {
                return;
            }

            MiniVanSnesCartridge cart = obj.GetComponent<MiniVanSnesCartridge>();
            if (cart == null)
            {
                return;
            }

            // Find console that has it.
            MiniVanSnesConsole[] consoles = FindObjectsByType<MiniVanSnesConsole>(FindObjectsSortMode.None);
            for (int i = 0; i < consoles.Length; i++)
            {
                if (consoles[i] != null && consoles[i].InsertedCartridgeId == cart.NetworkObjectId)
                {
                    int empty = FindFirstEmptyInventorySlot();
                    if (empty < 0)
                    {
                        return;
                    }

                    if (consoles[i].TryRemoveCartridgeServer(this))
                    {
                        SetInventorySlot(empty, MiniVanInventoryItem.SnesCartridge);
                        SetHeldSnesCartridgeClientRpc(cartReference, BuildOwnerTarget());
                    }

                    return;
                }
            }
        }

        [ClientRpc]
        private void SetHeldSnesCartridgeClientRpc(NetworkObjectReference reference, ClientRpcParams clientRpcParams = default)
        {
            if (reference.TryGet(out NetworkObject obj))
            {
                heldSnesCartridge = obj.GetComponent<MiniVanSnesCartridge>();
            }
        }

        [ClientRpc]
        private void ClearHeldSnesCartridgeClientRpc(ClientRpcParams clientRpcParams = default)
        {
            heldSnesCartridge = null;
        }

        private void ClearSnesCartridgeInventorySlot()
        {
            int slot = FindInventorySlot(MiniVanInventoryItem.SnesCartridge);
            if (slot >= 0)
            {
                SetInventorySlot(slot, MiniVanInventoryItem.None);
            }
        }

        [ServerRpc]
        private void RequestSnesTelevisionJoinServerRpc(NetworkObjectReference reference, ServerRpcParams rpcParams = default)
        {
            if (!reference.TryGet(out NetworkObject obj))
            {
                return;
            }

            MiniVanSnesTelevision tv = obj.GetComponent<MiniVanSnesTelevision>();
            if (tv == null || !tv.ServerTryBeginPlay(this, out int port))
            {
                return;
            }

            ConfirmSnesTelevisionJoinClientRpc(reference, port, BuildOwnerTarget());
        }

        [ServerRpc]
        private void RequestSnesTelevisionLeaveServerRpc(NetworkObjectReference reference, ServerRpcParams rpcParams = default)
        {
            if (!reference.TryGet(out NetworkObject obj))
            {
                return;
            }

            MiniVanSnesTelevision tv = obj.GetComponent<MiniVanSnesTelevision>();
            tv?.ServerEndPlay(OwnerClientId);
        }

        [ServerRpc]
        private void RequestSnesTelevisionInputServerRpc(
            NetworkObjectReference reference,
            ushort joypadMask,
            ServerRpcParams rpcParams = default)
        {
            if (!reference.TryGet(out NetworkObject obj))
            {
                return;
            }

            MiniVanSnesTelevision tv = obj.GetComponent<MiniVanSnesTelevision>();
            tv?.ServerSetJoypad(OwnerClientId, joypadMask);
        }

        [ClientRpc]
        private void ConfirmSnesTelevisionJoinClientRpc(
            NetworkObjectReference reference,
            int port,
            ClientRpcParams clientRpcParams = default)
        {
            if (!reference.TryGet(out NetworkObject obj))
            {
                return;
            }

            MiniVanSnesTelevision tv = obj.GetComponent<MiniVanSnesTelevision>();
            if (tv == null)
            {
                return;
            }

            activeSnesTelevision = tv;
            verticalVelocity = 0f;
            tv.NotifyLocalPlayStarted(this, port);
        }

        #endregion
    }
}
