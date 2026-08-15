using UnityEngine;

namespace MiniVanGame
{
    [DisallowMultipleComponent]
    public sealed class MiniVanDestructibleCrate : MonoBehaviour, IMiniVanGameModeInteractable
    {
        public int CrateId;
        [Min(1)] public int HitPoints = 3;
        [Range(5, 10)] public int CoinValue = 5;
        public GameObject CrateVisual;
        public Collider SolidCollider;
        public GameObject CoinVisual;

        private int health;
        private bool collected;

        public int CurrentHealth => health;
        public bool IsCollected => collected;

        private void Awake()
        {
            health = Mathf.Max(1, HitPoints);
            RefreshVisual();
        }

        public string GetPrompt(MiniVanPlayer player)
        {
            if (health > 0) return "Hit with the bat (" + health + ")";
            return string.Empty;
        }

        public void Interact(MiniVanPlayer player)
        {
            // Coins are collected by stepping onto them.
        }

        public void PrimaryAction(MiniVanPlayer player)
        {
            // Crates only accept damage from the server-authoritative bat hitbox.
        }

        public bool ServerApplyHit()
        {
            if (health <= 0) return false;
            health = Mathf.Max(0, health - 1);
            RefreshVisual();
            return true;
        }

        public bool ServerCollect(MiniVanPlayer player)
        {
            if (player == null || health > 0 || collected || !player.GameModeServerAddMoney(CoinValue)) return false;
            collected = true;
            RefreshVisual();
            return true;
        }

        public void ApplyNetworkState(int newHealth, int newCoinValue, bool newCollected)
        {
            health = Mathf.Clamp(newHealth, 0, Mathf.Max(1, HitPoints));
            CoinValue = Mathf.Clamp(newCoinValue, 5, 10);
            collected = newCollected;
            RefreshVisual();
        }

        private void RefreshVisual()
        {
            bool broken = health <= 0;
            if (CrateVisual != null) CrateVisual.SetActive(!broken);
            if (SolidCollider != null) SolidCollider.enabled = !broken;
            if (CoinVisual != null) CoinVisual.SetActive(broken && !collected);
        }
    }
}
