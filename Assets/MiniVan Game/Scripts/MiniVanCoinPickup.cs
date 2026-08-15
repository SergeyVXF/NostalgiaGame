// Gameplay mode coin auto-pickup.
using UnityEngine;

namespace MiniVanGame
{
    [DisallowMultipleComponent]
    public sealed class MiniVanCoinPickup : MonoBehaviour
    {
        public MiniVanDestructibleCrate Owner;

        private void OnTriggerEnter(Collider other)
        {
            MiniVanPlayer player = other != null
                ? other.GetComponentInParent<MiniVanPlayer>()
                : null;
            if (Owner != null && player != null && Owner.CurrentHealth <= 0 && !Owner.IsCollected)
            {
                player.GameModeRequestCrateAction(Owner.CrateId, true);
            }
        }
    }
}
