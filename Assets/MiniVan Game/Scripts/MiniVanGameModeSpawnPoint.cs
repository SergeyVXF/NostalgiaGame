using UnityEngine;

namespace MiniVanGame
{
    public sealed class MiniVanGameModeSpawnPoint : MonoBehaviour
    {
        public int SiteIndex = -1;
        public bool ExteriorOnly = true;

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.9f, 0.16f, 0.12f, 0.8f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up, 0.7f);
        }
    }
}
