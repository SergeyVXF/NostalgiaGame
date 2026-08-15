using Unity.Netcode;
using UnityEngine;

namespace MiniVanGame
{
    public partial class MiniVanPlayer
    {
        /// <summary>
        /// Q drops the selected held inventory item. The hot-potato bomb is excluded
        /// (E drops it, LMB throws it).
        /// </summary>
        private bool HandleHeldInventoryDropInput()
        {
            if (!IsOwner || equipmentWindowOpen || IsDowned || currentSeat != null)
            {
                return false;
            }

            MiniVanInventoryItem item = GetInventorySlot(localSelectedSlot);
            if (item == MiniVanInventoryItem.HotPotatoBomb)
            {
                return false;
            }

            if (heldTowCube != null)
            {
                MiniVanTowCube cube = heldTowCube;
                if (!IsServer)
                {
                    heldTowCube = null;
                    cube.BeginLocalDropPrediction(this);
                }

                RequestTowCubeDropServerRpc(new NetworkObjectReference(cube.NetworkObject));
                return true;
            }

            if (heldSkateboard != null && item == MiniVanInventoryItem.Skateboard)
            {
                MiniVanSkateboard board = heldSkateboard;
                if (!IsServer)
                {
                    heldSkateboard = null;
                    heldSkateboardSlot = -1;
                    PredictClearInventoryItem(MiniVanInventoryItem.Skateboard);
                    board.BeginLocalDropPrediction(this);
                }

                RequestSkateboardDropServerRpc(new NetworkObjectReference(board.NetworkObject));
                return true;
            }

            if (item == MiniVanInventoryItem.PanelkaKey)
            {
                return TryDropPanelkaKeyFromSlot(localSelectedSlot);
            }

            if (!CanDropSelectedHeldInventoryItem(item))
            {
                return false;
            }

            Vector3 dropPosition = GetLooseItemDropPosition();
            Quaternion dropRotation = GetLooseItemDropRotation();
            if (!IsServer)
            {
                PredictClearHeldInventoryItem(item);
            }

            RequestDropSelectedHeldItemServerRpc(localSelectedSlot, dropPosition, dropRotation);
            return true;
        }

        private static bool CanDropSelectedHeldInventoryItem(MiniVanInventoryItem item)
        {
            return item == MiniVanInventoryItem.Bat ||
                   item == MiniVanInventoryItem.TestBaton ||
                   item == MiniVanInventoryItem.Coffee ||
                   item == MiniVanInventoryItem.HolyCross ||
                   item == MiniVanInventoryItem.AspenStake ||
                   item == MiniVanInventoryItem.Flamethrower;
        }

        private void PredictClearHeldInventoryItem(MiniVanInventoryItem item)
        {
            PredictClearInventoryItem(item);
            if (item == MiniVanInventoryItem.Coffee)
            {
                hasCoffee = false;
                coffeeDrinkActive = false;
                coffeeDrinkTimer = 0f;
            }

            InvalidateStaticHeldVisuals();
            RefreshStaticHeldVisualsIfNeeded(true);
            UpdateHeldBatVisual();
            EnsureHeldCoffeeVisual();
            UpdateCoffeeVisual();
        }

        private bool TryDropSelectedPanelkaKey()
        {
            return TryDropPanelkaKeyFromSlot(localSelectedSlot);
        }

        private bool TryDropPanelkaKeyFromSlot(int slot)
        {
            slot = Mathf.Clamp(slot, 0, 3);
            if (GetInventorySlot(slot) != MiniVanInventoryItem.PanelkaKey)
            {
                return false;
            }

            if (!panelkaKeyIdsBySlot.TryGetValue(slot, out string keyId) || string.IsNullOrEmpty(keyId))
            {
                return false;
            }

            MiniVanApartmentKeyPickup.Restore(keyId, GetLooseItemDropPosition(), GetLooseItemDropRotation());
            panelkaKeyIdsBySlot.Remove(slot);
            SetInventorySlotNetworked(slot, MiniVanInventoryItem.None);
            return true;
        }

        [ServerRpc]
        private void RequestDropSelectedHeldItemServerRpc(
            int inventorySlot,
            Vector3 dropPosition,
            Quaternion dropRotation,
            ServerRpcParams rpcParams = default)
        {
            if (currentSeat != null || inventorySlot < 0 || inventorySlot > 3)
            {
                return;
            }

            MiniVanInventoryItem item = GetInventorySlot(inventorySlot);
            if (item == MiniVanInventoryItem.HotPotatoBomb || !CanDropSelectedHeldInventoryItem(item))
            {
                return;
            }

            bool dropped;
            switch (item)
            {
                case MiniVanInventoryItem.Bat:
                case MiniVanInventoryItem.TestBaton:
                    dropped = ServerDropBatWeapon(item, dropPosition, dropRotation);
                    break;
                case MiniVanInventoryItem.Coffee:
                    dropped = ServerDropCoffee(dropPosition, dropRotation);
                    break;
                case MiniVanInventoryItem.HolyCross:
                    dropped = MiniVanHolyCrossPickup.ServerSpawn(dropPosition, dropRotation) != null;
                    break;
                case MiniVanInventoryItem.AspenStake:
                    dropped = MiniVanAspenStakePickup.ServerSpawn(dropPosition, dropRotation) != null;
                    break;
                case MiniVanInventoryItem.Flamethrower:
                    dropped = true;
                    RestoreFlamethrowerRack(lastFlamethrowerRackPosition);
                    break;
                default:
                    dropped = false;
                    break;
            }

            if (!dropped)
            {
                return;
            }

            SetInventorySlot(inventorySlot, MiniVanInventoryItem.None);
            SetLocalInventorySlotClientRpc(inventorySlot, (int)MiniVanInventoryItem.None, BuildOwnerTarget());

            if (item == MiniVanInventoryItem.Coffee)
            {
                hasCoffee = false;
                SetCoffeeHeldClientRpc(false, -1);
            }

            if (item == MiniVanInventoryItem.AspenStake)
            {
                networkAspenStakeHitsLeft.Value = 0;
                offlineAspenStakeHitsLeft = 0;
            }

            if (item == MiniVanInventoryItem.HolyCross)
            {
                localHolyCrossActive = false;
                if (IsSpawned)
                {
                    networkHolyCrossActive.Value = false;
                }
            }
        }

        private bool ServerDropBatWeapon(MiniVanInventoryItem item, Vector3 dropPosition, Quaternion dropRotation)
        {
            bool testBaton = item == MiniVanInventoryItem.TestBaton;
            if (claimedBatReference.TryGet(out NetworkObject batObject))
            {
                MiniVanBatPickup claimed = batObject.GetComponent<MiniVanBatPickup>();
                if (claimed != null && claimed.IsTestBaton == testBaton)
                {
                    claimed.ServerRelease(dropPosition, dropRotation);
                    claimedBatReference = default;
                    return true;
                }
            }

            MiniVanBatPickup spawned = MiniVanBatPickup.ServerSpawn(dropPosition, dropRotation, testBaton);
            claimedBatReference = default;
            return spawned != null;
        }

        private bool ServerDropCoffee(Vector3 dropPosition, Quaternion dropRotation)
        {
            if (claimedCoffeeReference.TryGet(out NetworkObject mugObject))
            {
                CoffeeMugPickup claimed = mugObject.GetComponent<CoffeeMugPickup>();
                if (claimed != null)
                {
                    claimed.ServerRelease(dropPosition, dropRotation);
                    claimedCoffeeReference = default;
                    return true;
                }
            }

            CoffeeMugPickup[] mugs = FindObjectsByType<CoffeeMugPickup>(FindObjectsSortMode.None);
            for (int i = 0; i < mugs.Length; i++)
            {
                CoffeeMugPickup mug = mugs[i];
                if (mug == null || mug.IsAvailable)
                {
                    continue;
                }

                mug.ServerRelease(dropPosition, dropRotation);
                claimedCoffeeReference = default;
                return true;
            }

            claimedCoffeeReference = default;
            return false;
        }

        private void RestoreFlamethrowerRack(Vector3 rackPosition)
        {
            MiniVanFlamethrowerRack.RestoreNearestAt(rackPosition, 8f);
            RestoreFlamethrowerRackClientRpc(rackPosition);
        }

        [ClientRpc]
        private void RestoreFlamethrowerRackClientRpc(Vector3 rackPosition)
        {
            if (IsServer)
            {
                return;
            }

            MiniVanFlamethrowerRack.RestoreNearestAt(rackPosition, 8f);
        }

        /// <summary>
        /// Drops the item in an inventory slot the same way Q does for the selected cell.
        /// Used by the equipment window when a cell is dragged outside the UI.
        /// The hot-potato bomb cannot be thrown this way.
        /// </summary>
        public bool TryDropInventorySlot(int slot)
        {
            if (!IsOwner || IsDowned || currentSeat != null || slot < 0 || slot > 3)
            {
                return false;
            }

            MiniVanInventoryItem item = GetInventorySlot(slot);
            if (item == MiniVanInventoryItem.None || item == MiniVanInventoryItem.HotPotatoBomb)
            {
                return false;
            }

            if (TryDropPizzaItemFromSlot(slot))
            {
                return true;
            }

            if (MiniVanCosmeticCatalog.IsCosmetic(item))
            {
                RequestDropCosmeticServerRpc(slot, GetLooseItemDropPosition(), GetLooseItemDropRotation());
                if (!IsServer)
                {
                    PredictClearInventorySlot(slot);
                }

                return true;
            }

            if (item == MiniVanInventoryItem.Winch)
            {
                Vector3 dropPosition = GetLooseItemDropPosition();
                Quaternion dropRotation = GetLooseItemDropRotation();
                if (!IsServer)
                {
                    PredictClearInventoryItem(MiniVanInventoryItem.Winch);
                }

                RequestDropSelectedWinchServerRpc(dropPosition, dropRotation);
                return true;
            }

            if (item == MiniVanInventoryItem.FireExtinguisher)
            {
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

            if (item == MiniVanInventoryItem.Defibrillator)
            {
                RequestDropDefibrillatorServerRpc(GetLooseItemDropPosition(), GetLooseItemDropRotation());
                if (!IsServer)
                {
                    PredictClearInventoryItem(MiniVanInventoryItem.Defibrillator);
                    InvalidateStaticHeldVisuals();
                    RefreshStaticHeldVisualsIfNeeded(true);
                }

                return true;
            }

            if (item == MiniVanInventoryItem.Stretcher)
            {
                Vector3 dropPosition = GetLooseItemDropPosition();
                Quaternion dropRotation = GetLooseItemDropRotation();
                if (!IsServer)
                {
                    PredictClearInventoryItem(MiniVanInventoryItem.Stretcher);
                }

                RequestDropSelectedStretcherServerRpc(dropPosition, dropRotation);
                return true;
            }

            if (item == MiniVanInventoryItem.AntonLocator)
            {
                Vector3 dropPosition = GetLooseItemDropPosition();
                Quaternion dropRotation = GetLooseItemDropRotation();
                if (!IsServer)
                {
                    PredictClearInventoryItem(MiniVanInventoryItem.AntonLocator);
                    InvalidateStaticHeldVisuals();
                    RefreshStaticHeldVisualsIfNeeded(true);
                }

                RequestDropSelectedAntonLocatorServerRpc(dropPosition, dropRotation);
                return true;
            }

            if (item == MiniVanInventoryItem.SnesConsole && heldSnesConsole != null)
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

            if (item == MiniVanInventoryItem.SnesCartridge && heldSnesCartridge != null)
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

            if (item == MiniVanInventoryItem.HoverboardM && heldHoverboardM != null)
            {
                MiniVanHoverboardM board = heldHoverboardM;
                if (!IsServer)
                {
                    heldHoverboardM = null;
                    heldHoverboardMSlot = -1;
                    PredictClearInventoryItem(MiniVanInventoryItem.HoverboardM);
                    board.BeginLocalDropPrediction(this);
                }

                RequestHoverboardMDropServerRpc(new NetworkObjectReference(board.NetworkObject));
                return true;
            }

            if (item == MiniVanInventoryItem.Skateboard && heldSkateboard != null)
            {
                MiniVanSkateboard board = heldSkateboard;
                if (!IsServer)
                {
                    heldSkateboard = null;
                    heldSkateboardSlot = -1;
                    PredictClearInventoryItem(MiniVanInventoryItem.Skateboard);
                    board.BeginLocalDropPrediction(this);
                }

                RequestSkateboardDropServerRpc(new NetworkObjectReference(board.NetworkObject));
                return true;
            }

            if (item == MiniVanInventoryItem.PanelkaKey)
            {
                return TryDropPanelkaKeyFromSlot(slot);
            }

            if (!CanDropSelectedHeldInventoryItem(item))
            {
                return false;
            }

            Vector3 heldDropPosition = GetLooseItemDropPosition();
            Quaternion heldDropRotation = GetLooseItemDropRotation();
            if (!IsServer)
            {
                PredictClearHeldInventoryItem(item);
            }

            RequestDropSelectedHeldItemServerRpc(slot, heldDropPosition, heldDropRotation);
            return true;
        }
    }
}
