using Unity.Netcode;
using UnityEngine;

namespace MiniVanGame
{
    public partial class MiniVanPlayer
    {
        private readonly NetworkVariable<int> gameModeMoney = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public int GameModeMoney => gameModeMoney.Value;

        public MiniVanInventoryItem GameModeSelectedItem
        {
            get
            {
                int slot = IsOwner ? localSelectedSlot : networkSelectedSlot.Value;
                return GetInventorySlot(Mathf.Clamp(slot, 0, 3));
            }
        }

        public void GameModeRequestSellSelected()
        {
            if (!IsSpawned || IsServer)
            {
                GameModeServerTrySellSelected(out _, out _);
            }
            else
            {
                GameModeSellSelectedServerRpc();
            }
        }

        public void GameModeRequestOpenGate(int gateId)
        {
            if (!IsSpawned || IsServer)
            {
                GameModeServerTryOpenGate(gateId);
            }
            else
            {
                GameModeOpenGateServerRpc(gateId);
            }
        }

        public void GameModeServerTryOpenHornGate(Vector3 hornPosition)
        {
            if (IsSpawned && !IsServer)
            {
                return;
            }

            MiniVanGameModeGateController gate =
                MiniVanGameModeGateController.FindHornGate(hornPosition);
            if (gate == null)
            {
                return;
            }

            OpenGameModeGateNetworked(gate);
        }

        public void GameModeRequestCrateAction(int crateId, bool collect)
        {
            if (!IsSpawned || IsServer)
            {
                GameModeServerApplyCrateAction(crateId, collect);
            }
            else
            {
                GameModeCrateActionServerRpc(crateId, collect);
            }
        }

        [ServerRpc]
        private void GameModeSellSelectedServerRpc()
        {
            GameModeServerTrySellSelected(out _, out _);
        }

        [ServerRpc]
        private void GameModeOpenGateServerRpc(int gateId)
        {
            GameModeServerTryOpenGate(gateId);
        }

        [ServerRpc]
        private void GameModeCrateActionServerRpc(int crateId, bool collect)
        {
            GameModeServerApplyCrateAction(crateId, collect);
        }

        private void GameModeServerApplyCrateAction(int crateId, bool collect)
        {
            MiniVanDestructibleCrate[] crates = FindObjectsByType<MiniVanDestructibleCrate>(FindObjectsSortMode.None);
            for (int i = 0; i < crates.Length; i++)
            {
                MiniVanDestructibleCrate crate = crates[i];
                if (crate == null || crate.CrateId != crateId) continue;
                bool changed = collect ? crate.ServerCollect(this) : crate.ServerApplyHit();
                if (changed && IsSpawned)
                {
                    GameModeCrateStateClientRpc(crate.CrateId, crate.CurrentHealth, crate.CoinValue, crate.IsCollected);
                }
                return;
            }
        }

        private void GameModeServerTryOpenGate(int gateId)
        {
            MiniVanGameModeGateController gate = MiniVanGameModeGateController.FindGate(gateId);
            if (gate == null || !gate.CanUseLever(this))
            {
                return;
            }

            OpenGameModeGateNetworked(gate);
        }

        private void OpenGameModeGateNetworked(MiniVanGameModeGateController gate)
        {
            gate.OpenLocal();
            if (IsSpawned)
            {
                GameModeGateOpenedClientRpc(gate.GateId);
            }
        }

        [ClientRpc]
        private void GameModeGateOpenedClientRpc(int gateId)
        {
            MiniVanGameModeGateController gate = MiniVanGameModeGateController.FindGate(gateId);
            if (gate != null)
            {
                gate.OpenLocal();
            }
        }

        [ClientRpc]
        private void GameModeCrateStateClientRpc(int crateId, int healthValue, int coinValue, bool isCollected)
        {
            MiniVanDestructibleCrate[] crates = FindObjectsByType<MiniVanDestructibleCrate>(FindObjectsSortMode.None);
            for (int i = 0; i < crates.Length; i++)
            {
                if (crates[i] != null && crates[i].CrateId == crateId)
                {
                    crates[i].ApplyNetworkState(healthValue, coinValue, isCollected);
                    return;
                }
            }
        }

        public bool GameModeServerAddMoney(int amount)
        {
            if (amount <= 0 || (IsSpawned && !IsServer))
            {
                return false;
            }

            gameModeMoney.Value = Mathf.Max(0, gameModeMoney.Value + amount);
            return true;
        }

        public bool GameModeServerSpendMoney(int amount)
        {
            if (amount < 0 || (IsSpawned && !IsServer) || gameModeMoney.Value < amount)
            {
                return false;
            }

            gameModeMoney.Value -= amount;
            return true;
        }

        public bool GameModeServerTrySellSelected(out MiniVanInventoryItem soldItem, out int price)
        {
            soldItem = MiniVanInventoryItem.None;
            price = 0;
            if (IsSpawned && !IsServer)
            {
                return false;
            }

            int slot = Mathf.Clamp(networkSelectedSlot.Value, 0, 3);
            MiniVanInventoryItem selected = GetInventorySlot(slot);
            if (selected == MiniVanInventoryItem.None || selected == MiniVanInventoryItem.PanelkaKey)
            {
                return false;
            }

            price = MiniVanGameModeEconomy.GetSalePrice(selected);
            if (price <= 0)
            {
                return false;
            }

            soldItem = selected;
            SetInventorySlot(slot, MiniVanInventoryItem.None);
            gameModeMoney.Value += price;
            ServerRemoveSoldWorldObject(selected);
            if (IsSpawned)
            {
                GameModeSoldClientRpc((int)selected, slot);
            }
            else
            {
                UpdateHeldBatVisual();
                RefreshStaticHeldVisualsIfNeeded(true);
            }
            return true;
        }

        private void ServerRemoveSoldWorldObject(MiniVanInventoryItem item)
        {
            NetworkObject networkObject = null;
            if (item == MiniVanInventoryItem.Skateboard && heldSkateboard != null)
            {
                networkObject = heldSkateboard.NetworkObject;
                heldSkateboard = null;
                heldSkateboardSlot = -1;
            }
            else if (item == MiniVanInventoryItem.HoverboardM && heldHoverboardM != null)
            {
                networkObject = heldHoverboardM.NetworkObject;
                heldHoverboardM = null;
                heldHoverboardMSlot = -1;
            }
            else if (item == MiniVanInventoryItem.HotPotatoBomb && heldHotPotatoBomb != null)
            {
                networkObject = heldHotPotatoBomb.NetworkObject;
                heldHotPotatoBomb = null;
                heldHotPotatoBombSlot = -1;
            }

            if (networkObject == null)
            {
                return;
            }

            if (networkObject.IsSpawned)
            {
                networkObject.Despawn(true);
            }
            else
            {
                Destroy(networkObject.gameObject);
            }
        }

        [ClientRpc]
        private void GameModeSoldClientRpc(int itemValue, int slot)
        {
            MiniVanInventoryItem item = (MiniVanInventoryItem)itemValue;
            if (!IsServer)
            {
                SetInventorySlot(slot, MiniVanInventoryItem.None);
            }

            if (item == MiniVanInventoryItem.Coffee)
            {
                hasCoffee = false;
            }
            if (item == MiniVanInventoryItem.Skateboard)
            {
                heldSkateboard = null;
                heldSkateboardSlot = -1;
            }
            if (item == MiniVanInventoryItem.HoverboardM)
            {
                heldHoverboardM = null;
                heldHoverboardMSlot = -1;
            }
            if (item == MiniVanInventoryItem.HotPotatoBomb)
            {
                heldHotPotatoBomb = null;
                heldHotPotatoBombSlot = -1;
            }

            UpdateHeldBatVisual();
            InvalidateStaticHeldVisuals();
            RefreshStaticHeldVisualsIfNeeded(true);
        }
    }

    public static class MiniVanGameModeEconomy
    {
        public static int GetSalePrice(MiniVanInventoryItem item)
        {
            switch (item)
            {
                case MiniVanInventoryItem.ZombieTorso:
                case MiniVanInventoryItem.ZombieHead:
                    return 15;
                case MiniVanInventoryItem.TraceHead:
                    return 8;
                case MiniVanInventoryItem.ZombieArm:
                case MiniVanInventoryItem.ZombieLeg:
                    return 10;
                case MiniVanInventoryItem.Bat:
                case MiniVanInventoryItem.RollingPin:
                case MiniVanInventoryItem.Grater:
                    return 8;
                case MiniVanInventoryItem.Skateboard:
                case MiniVanInventoryItem.HoverboardM:
                case MiniVanInventoryItem.HotPotatoBomb:
                    return 20;
                case MiniVanInventoryItem.RawPizza:
                case MiniVanInventoryItem.CookedPizza:
                case MiniVanInventoryItem.BoxedPizza:
                    return 18;
                case MiniVanInventoryItem.None:
                case MiniVanInventoryItem.PanelkaKey:
                    return 0;
                default:
                    return 5;
            }
        }
    }
}
