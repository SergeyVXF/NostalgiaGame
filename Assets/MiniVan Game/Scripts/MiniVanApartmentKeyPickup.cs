using UnityEngine;

namespace MiniVanGame
{
    [DisallowMultipleComponent]
    public sealed class MiniVanApartmentKeyPickup :
        MonoBehaviour,
        IMiniVanGameModeInteractable
    {
        [SerializeField] private string keyId;

        public string KeyId => keyId;

        public void Configure(string value)
        {
            keyId = value ?? string.Empty;
        }

        public string GetPrompt(MiniVanPlayer player)
        {
            return "Take apartment key";
        }

        public void Interact(MiniVanPlayer player)
        {
            Take(player);
        }

        public void PrimaryAction(MiniVanPlayer player)
        {
            Take(player);
        }

        private void Take(MiniVanPlayer player)
        {
            if (player != null && player.TryPickupApartmentKey(keyId))
                gameObject.SetActive(false);
        }

        public static bool Restore(string keyId, Vector3 position, Quaternion rotation)
        {
            if (string.IsNullOrEmpty(keyId))
            {
                return false;
            }

            MiniVanApartmentKeyPickup[] pickups = Object.FindObjectsByType<MiniVanApartmentKeyPickup>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < pickups.Length; i++)
            {
                MiniVanApartmentKeyPickup pickup = pickups[i];
                if (pickup == null || pickup.KeyId != keyId)
                {
                    continue;
                }

                pickup.transform.SetPositionAndRotation(position, rotation);
                pickup.gameObject.SetActive(true);
                return true;
            }

            return false;
        }
    }
}
