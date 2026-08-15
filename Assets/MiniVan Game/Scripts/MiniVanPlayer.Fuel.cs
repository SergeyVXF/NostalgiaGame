using Unity.Netcode;
using UnityEngine;

namespace MiniVanGame
{
    public partial class MiniVanPlayer
    {
        private MiniVanFuelFurnace lookedAtFuelFurnace;

        private void UpdateFuelLookTarget()
        {
            lookedAtFuelFurnace = currentSeat == null && currentSkateboard == null && currentHoverboardM == null
                ? FindLookedAtFuelFurnace()
                : null;
        }

        private MiniVanFuelFurnace FindLookedAtFuelFurnace()
        {
            return FindLookedAtPizzaComponent<MiniVanFuelFurnace>(3.2f);
        }

        private bool HandleFuelInteractionInput()
        {
            if (lookedAtFuelFurnace == null || !MiniVanKeyBindings.GetKeyDown(MiniVanKeyAction.Interact))
            {
                return false;
            }

            MiniVanInventoryItem selected = GetInventorySlot(localSelectedSlot);
            float value = MiniVanFuelRules.GetFuelLiters(selected);
            if (value <= 0f)
            {
                SetPizzaStatus(selected == MiniVanInventoryItem.Water ? "Water does not burn" : "This item is not fuel");
                return true;
            }

            MiniVanVehicle vehicle = lookedAtFuelFurnace.Vehicle;
            if (vehicle == null)
            {
                return true;
            }

            RequestBurnFuelServerRpc(new NetworkObjectReference(vehicle.NetworkObject), localSelectedSlot);
            return true;
        }

        [ServerRpc]
        private void RequestBurnFuelServerRpc(NetworkObjectReference vehicleReference, int slotIndex,
            ServerRpcParams rpcParams = default)
        {
            if (!vehicleReference.TryGet(out NetworkObject vehicleObject))
            {
                SendFuelResultClientRpc("Furnace unavailable");
                return;
            }

            MiniVanVehicle vehicle = vehicleObject.GetComponent<MiniVanVehicle>();
            MiniVanInventoryItem item = GetInventorySlot(slotIndex);
            if (vehicle == null || vehicle.FuelFurnace == null ||
                !vehicle.FuelFurnace.IsInRange(transform.position) || MiniVanFuelRules.GetFuelLiters(item) <= 0f)
            {
                SendFuelResultClientRpc("Cannot burn this item");
                return;
            }

            if (!vehicle.ServerTryAddFuel(item, out float added))
            {
                SendFuelResultClientRpc("Fuel tank is full");
                return;
            }

            SetInventorySlot(slotIndex, MiniVanInventoryItem.None);
            SendFuelResultClientRpc("Burned " + GetInventoryLabel(item) + "  +" + added.ToString("0") + " L");
        }

        [ClientRpc]
        private void SendFuelResultClientRpc(string message)
        {
            if (IsOwner)
            {
                SetPizzaStatus(message);
            }
        }

        private void DrawFuelInteractionGui()
        {
            if (lookedAtFuelFurnace == null || currentSeat != null)
            {
                return;
            }

            MiniVanInventoryItem selected = GetInventorySlot(localSelectedSlot);
            float liters = MiniVanFuelRules.GetFuelLiters(selected);
            string prompt = liters > 0f
                ? "E - burn " + GetInventoryLabel(selected) + "  (+" + liters.ToString("0") + " L)"
                : "Hold combustible fuel and press E";
            DrawPizzaSmallPrompt(prompt);
        }
    }
}
