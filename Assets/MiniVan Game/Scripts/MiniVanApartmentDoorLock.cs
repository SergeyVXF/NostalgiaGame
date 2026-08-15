using UnityEngine;

namespace MiniVanGame
{
    [DisallowMultipleComponent]
    public sealed class MiniVanApartmentDoorLock : MonoBehaviour
    {
        [SerializeField] private string requiredKeyId;
        [SerializeField] private bool locked;
        [SerializeField] private bool consumeKey = true;

        public string RequiredKeyId => requiredKeyId;
        public bool IsLocked => locked && !string.IsNullOrEmpty(requiredKeyId);

        public void Configure(string keyId, bool shouldLock, bool shouldConsumeKey = true)
        {
            requiredKeyId = keyId ?? string.Empty;
            locked = shouldLock && !string.IsNullOrEmpty(requiredKeyId);
            consumeKey = shouldConsumeKey;
        }

        public string GetPrompt(MiniVanPlayer player)
        {
            return IsLocked ? "Use apartment key" : string.Empty;
        }

        /// <summary>
        /// Latch opens by hand from the apartment side and stays open afterwards.
        /// </summary>
        public void UnlockFromInside()
        {
            locked = false;
        }

        public bool TryUnlock(MiniVanPlayer player)
        {
            if (!IsLocked)
                return true;
            if (player == null ||
                !player.TryUseSelectedApartmentKey(requiredKeyId, consumeKey))
            {
                return false;
            }

            locked = false;
            return true;
        }
    }
}
