using Unity.Netcode;
using UnityEngine;

namespace MiniVanGame
{
    public partial class MiniVanPlayer
    {
        private void ReportEnemyCombatHud(MiniVanZombie enemy)
        {
            if (enemy == null)
            {
                return;
            }

            string enemyName = enemy.EnemyDisplayName;
            float health = enemy.CurrentHealthPrecise;
            int maxHealth = Mathf.Max(1, enemy.MaxHealth);

            if (IsOwner || !IsSpawned)
            {
                MiniVanEnemyCombatHud.ShowSnapshot(enemyName, health, maxHealth, enemy);
            }

            if (IsServer && IsSpawned)
            {
                ShowEnemyCombatHudClientRpc(
                    enemyName,
                    health,
                    maxHealth,
                    enemy.NetworkObject != null && enemy.NetworkObject.IsSpawned
                        ? new NetworkObjectReference(enemy.NetworkObject)
                        : default,
                    BuildOwnerTarget());
            }
        }

        [ClientRpc]
        private void ShowEnemyCombatHudClientRpc(
            string enemyName,
            float health,
            int maxHealth,
            NetworkObjectReference enemyReference,
            ClientRpcParams clientRpcParams = default)
        {
            if (!IsOwner)
            {
                return;
            }

            MiniVanZombie enemy = null;
            if (enemyReference.TryGet(out NetworkObject netObj) && netObj != null)
            {
                enemy = netObj.GetComponent<MiniVanZombie>();
            }

            MiniVanEnemyCombatHud.ShowSnapshot(enemyName, health, maxHealth, enemy);
        }
    }
}
