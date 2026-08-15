using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MiniVanGame
{
    /// <summary>
    /// Wearable cosmetics: four server-authoritative equipment slots plus the window
    /// that lets the owner drag items from the inventory onto them.
    /// </summary>
    public partial class MiniVanPlayer
    {
        public const string EquipmentUiResource = "EquipmentUI/EquipmentHUD";

        private readonly NetworkVariable<int> networkEquipHead = new NetworkVariable<int>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<int> networkEquipCloak = new NetworkVariable<int>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<int> networkEquipBoots = new NetworkVariable<int>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<int> networkEquipBelt = new NetworkVariable<int>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly Transform[] equipmentVisuals = new Transform[MiniVanCosmeticCatalog.SlotCount];
        private readonly Renderer[][] equipmentRenderers = new Renderer[MiniVanCosmeticCatalog.SlotCount][];
        private readonly MiniVanInventoryItem[] equipmentVisualItems = new MiniVanInventoryItem[MiniVanCosmeticCatalog.SlotCount];

        private MiniVanEquipmentUi equipmentUi;
        private bool equipmentWindowOpen;

        public bool IsEquipmentWindowOpen => equipmentWindowOpen;

        public MiniVanInventoryItem GetEquippedItem(MiniVanEquipmentSlot slot)
        {
            return (MiniVanInventoryItem)GetEquipVariable((int)slot).Value;
        }

        /// <summary>Inventory content for UI code outside this class.</summary>
        public MiniVanInventoryItem GetInventoryItem(int slotIndex)
        {
            return GetInventorySlot(slotIndex);
        }

        /// <summary>Body material as currently rendered, including the runtime face paint swap.</summary>
        public Material GetBodyPreviewMaterial()
        {
            Renderer bodyRenderer = GetComponent<Renderer>();
            return bodyRenderer != null ? bodyRenderer.sharedMaterial : null;
        }

        /// <summary>Short hotbar label reused by the equipment window.</summary>
        public static string GetItemShortLabel(MiniVanInventoryItem item)
        {
            return GetInventoryLabel(item);
        }

        public void RequestEquipFromInventory(int inventorySlot, MiniVanEquipmentSlot slot)
        {
            if (IsOwner)
            {
                RequestEquipServerRpc(inventorySlot, (int)slot);
            }
        }

        public void RequestUnequip(MiniVanEquipmentSlot slot, int preferredInventorySlot)
        {
            if (IsOwner)
            {
                RequestUnequipServerRpc((int)slot, preferredInventorySlot);
            }
        }

        private void InitializeEquipment()
        {
            networkEquipHead.OnValueChanged += HandleEquipmentChanged;
            networkEquipCloak.OnValueChanged += HandleEquipmentChanged;
            networkEquipBoots.OnValueChanged += HandleEquipmentChanged;
            networkEquipBelt.OnValueChanged += HandleEquipmentChanged;

            for (int i = 0; i < equipmentVisualItems.Length; i++)
            {
                equipmentVisualItems[i] = MiniVanInventoryItem.None;
            }

            RefreshEquipmentVisuals();
        }

        private void ShutdownEquipment()
        {
            networkEquipHead.OnValueChanged -= HandleEquipmentChanged;
            networkEquipCloak.OnValueChanged -= HandleEquipmentChanged;
            networkEquipBoots.OnValueChanged -= HandleEquipmentChanged;
            networkEquipBelt.OnValueChanged -= HandleEquipmentChanged;

            for (int i = 0; i < equipmentVisuals.Length; i++)
            {
                if (equipmentVisuals[i] != null)
                {
                    Destroy(equipmentVisuals[i].gameObject);
                    equipmentVisuals[i] = null;
                }

                equipmentRenderers[i] = null;
                equipmentVisualItems[i] = MiniVanInventoryItem.None;
            }

            if (equipmentUi != null)
            {
                Destroy(equipmentUi.gameObject);
                equipmentUi = null;
            }

            equipmentWindowOpen = false;
        }

        private NetworkVariable<int> GetEquipVariable(int slotIndex)
        {
            switch (slotIndex)
            {
                case 1: return networkEquipCloak;
                case 2: return networkEquipBoots;
                case 3: return networkEquipBelt;
                default: return networkEquipHead;
            }
        }

        private void HandleEquipmentChanged(int previousValue, int newValue)
        {
            RefreshEquipmentVisuals();
            if (equipmentUi != null && equipmentWindowOpen)
            {
                equipmentUi.RefreshContents();
            }
        }

        private void RefreshEquipmentVisuals()
        {
            EnsurePlayerVisual();
            Transform headBone = GetPlayerHeadBone();
            for (int i = 0; i < MiniVanCosmeticCatalog.SlotCount; i++)
            {
                MiniVanInventoryItem item = (MiniVanInventoryItem)GetEquipVariable(i).Value;
                MiniVanEquipmentSlot slot = (MiniVanEquipmentSlot)i;
                Transform expectedParent = slot == MiniVanEquipmentSlot.Head && headBone != null
                    ? headBone
                    : transform;
                bool alreadyWorn = equipmentVisualItems[i] == item
                    && (item == MiniVanInventoryItem.None || (equipmentVisuals[i] != null && equipmentVisuals[i].parent == expectedParent));
                if (alreadyWorn)
                {
                    continue;
                }

                if (equipmentVisuals[i] != null)
                {
                    Destroy(equipmentVisuals[i].gameObject);
                    equipmentVisuals[i] = null;
                    equipmentRenderers[i] = null;
                }

                equipmentVisualItems[i] = item;
                if (item == MiniVanInventoryItem.None)
                {
                    continue;
                }

                Transform visual = MiniVanCosmeticVisual.Build(item, expectedParent, "Equipped_" + slot);
                MiniVanCosmeticCatalog.AttachToWearer(visual, slot, transform, headBone, item);
                equipmentVisuals[i] = visual;
                equipmentRenderers[i] = visual.GetComponentsInChildren<Renderer>(true);
            }

            UpdateEquipmentVisibility();
        }

        /// <summary>
        /// Cosmetics are always rendered on the character. The owner's own camera simply stops
        /// drawing them through a dedicated layer, otherwise a brim would cover the whole screen.
        /// </summary>
        private void UpdateEquipmentVisibility()
        {
            int hiddenLayer = MiniVanCosmeticCatalog.OwnerHiddenLayer;
            bool hideFromOwner = IsOwner && hiddenLayer >= 0;
            int targetLayer = hideFromOwner ? hiddenLayer : gameObject.layer;

            if (hideFromOwner && PlayerCamera != null)
            {
                PlayerCamera.cullingMask &= ~(1 << hiddenLayer);
            }

            for (int i = 0; i < equipmentRenderers.Length; i++)
            {
                Renderer[] renderers = equipmentRenderers[i];
                if (renderers == null)
                {
                    continue;
                }

                for (int r = 0; r < renderers.Length; r++)
                {
                    Renderer renderer = renderers[r];
                    if (renderer == null)
                    {
                        continue;
                    }

                    renderer.enabled = true;
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                }

                Transform visual = equipmentVisuals[i];
                if (visual != null && visual.gameObject.layer != targetLayer)
                {
                    SetLayerRecursively(visual.gameObject, targetLayer);
                }
            }
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            root.layer = layer;
            for (int i = 0; i < root.transform.childCount; i++)
            {
                SetLayerRecursively(root.transform.GetChild(i).gameObject, layer);
            }
        }

        private void UpdateEquipmentInput()
        {
            if (!IsOwner)
            {
                return;
            }

            if (equipmentWindowOpen && (IsDowned || IsZombieDead || IsFacePainting()))
            {
                SetEquipmentWindowOpen(false);
                return;
            }

            if (MiniVanKeyBindings.GetKeyDown(MiniVanKeyAction.Equipment))
            {
                SetEquipmentWindowOpen(!equipmentWindowOpen);
            }
        }

        public void SetEquipmentWindowOpen(bool open)
        {
            if (open == equipmentWindowOpen)
            {
                return;
            }

            equipmentWindowOpen = open;
            if (open)
            {
                EnsureEquipmentUi();
                if (equipmentUi != null)
                {
                    equipmentUi.SetVisible(true);
                    equipmentUi.RefreshContents();
                }

                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                return;
            }

            if (equipmentUi != null)
            {
                equipmentUi.SetVisible(false);
            }

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void EnsureEquipmentUi()
        {
            if (equipmentUi != null)
            {
                return;
            }

            MiniVanEquipmentUi prefab = Resources.Load<MiniVanEquipmentUi>(EquipmentUiResource);
            if (prefab == null)
            {
                Debug.LogWarning("[MiniVanPlayer] Equipment HUD prefab missing at Resources/" + EquipmentUiResource);
                return;
            }

            equipmentUi = Instantiate(prefab);
            equipmentUi.name = "EquipmentHUD";
            equipmentUi.Bind(this);
            EnsureEquipmentEventSystem();
        }

        private static void EnsureEquipmentEventSystem()
        {
            if (EventSystem.current != null ||
                FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include) != null)
            {
                return;
            }

            GameObject go = new GameObject("MiniVan Equipment EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
        }

        public bool TryPickupCosmetic(MiniVanCosmeticPickup pickup)
        {
            if (!IsOwner || pickup == null || !pickup.IsAvailable || IsDowned)
            {
                return false;
            }

            if (FindFirstEmptyInventorySlot() < 0)
            {
                return false;
            }

            RequestCosmeticPickupServerRpc(new NetworkObjectReference(pickup.NetworkObject));
            return true;
        }

        [ServerRpc]
        private void RequestCosmeticPickupServerRpc(NetworkObjectReference pickupReference, ServerRpcParams rpcParams = default)
        {
            if (!pickupReference.TryGet(out NetworkObject pickupObject))
            {
                return;
            }

            MiniVanCosmeticPickup pickup = pickupObject.GetComponent<MiniVanCosmeticPickup>();
            if (pickup == null || !pickup.IsAvailable || !pickup.IsInReach(transform.position))
            {
                return;
            }

            int slot = FindFirstEmptyInventorySlot();
            if (slot < 0 || !pickup.TryClaim())
            {
                return;
            }

            MiniVanInventoryItem item = pickup.CurrentItem();
            SetInventorySlot(slot, item);
            networkSelectedSlot.Value = slot;
            SetLocalInventorySlotClientRpc(slot, (int)item, BuildOwnerTarget());
            pickupObject.Despawn(true);
        }

        /// <summary>Q drops the selected cosmetic back into the world as a pickup.</summary>
        private bool HandleCosmeticDropInput()
        {
            if (!IsOwner || equipmentWindowOpen || IsDowned || currentSeat != null)
            {
                return false;
            }

            MiniVanInventoryItem item = GetInventorySlot(localSelectedSlot);
            if (!MiniVanCosmeticCatalog.IsCosmetic(item))
            {
                return false;
            }

            RequestDropCosmeticServerRpc(localSelectedSlot, GetLooseItemDropPosition(), GetLooseItemDropRotation());
            if (!IsServer)
            {
                PredictClearInventorySlot(localSelectedSlot);
            }

            return true;
        }

        [ServerRpc]
        private void RequestDropCosmeticServerRpc(int inventorySlot, Vector3 dropPosition, Quaternion dropRotation, ServerRpcParams rpcParams = default)
        {
            if (inventorySlot < 0 || inventorySlot > 3 || currentSeat != null)
            {
                return;
            }

            MiniVanInventoryItem item = GetInventorySlot(inventorySlot);
            if (!MiniVanCosmeticCatalog.IsCosmetic(item))
            {
                return;
            }

            if (MiniVanCosmeticPickup.ServerSpawn(item, dropPosition, dropRotation) == null)
            {
                return;
            }

            SetInventorySlot(inventorySlot, MiniVanInventoryItem.None);
            SetLocalInventorySlotClientRpc(inventorySlot, (int)MiniVanInventoryItem.None, BuildOwnerTarget());
        }

        [ServerRpc]
        private void RequestEquipServerRpc(int inventorySlot, int equipSlotIndex, ServerRpcParams rpcParams = default)
        {
            if (inventorySlot < 0 || inventorySlot > 3 ||
                equipSlotIndex < 0 || equipSlotIndex >= MiniVanCosmeticCatalog.SlotCount)
            {
                return;
            }

            MiniVanEquipmentSlot slot = (MiniVanEquipmentSlot)equipSlotIndex;
            MiniVanInventoryItem item = GetInventorySlot(inventorySlot);
            if (!MiniVanCosmeticCatalog.CanEquip(item, slot))
            {
                return;
            }

            // Swap: whatever was worn goes back into the cell the new item came from.
            MiniVanInventoryItem previous = (MiniVanInventoryItem)GetEquipVariable(equipSlotIndex).Value;
            GetEquipVariable(equipSlotIndex).Value = (int)item;
            SetInventorySlot(inventorySlot, previous);
        }

        [ServerRpc]
        private void RequestUnequipServerRpc(int equipSlotIndex, int preferredInventorySlot, ServerRpcParams rpcParams = default)
        {
            if (equipSlotIndex < 0 || equipSlotIndex >= MiniVanCosmeticCatalog.SlotCount)
            {
                return;
            }

            MiniVanInventoryItem item = (MiniVanInventoryItem)GetEquipVariable(equipSlotIndex).Value;
            if (item == MiniVanInventoryItem.None)
            {
                return;
            }

            int target = preferredInventorySlot;
            if (target < 0 || target > 3 || GetInventorySlot(target) != MiniVanInventoryItem.None)
            {
                target = FindFirstEmptyInventorySlot();
            }

            if (target < 0)
            {
                return;
            }

            GetEquipVariable(equipSlotIndex).Value = (int)MiniVanInventoryItem.None;
            SetInventorySlot(target, item);
        }
    }
}
