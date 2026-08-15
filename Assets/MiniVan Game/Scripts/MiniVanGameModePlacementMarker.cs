using UnityEngine;

namespace MiniVanGame
{
    public enum MiniVanGameModePlacementKind
    {
        PanelkaSmall,
        PanelkaMedium,
        PanelkaLarge,
        SmallHouse,
        SaveShop
    }

    public sealed class MiniVanGameModePlacementMarker : MonoBehaviour
    {
        public MiniVanGameModePlacementKind Kind;
        public int SiteIndex;
        public int Floors;
        public int Entrances;
        public int AccessibleEntrances;

        private void OnDrawGizmos()
        {
            Gizmos.color = Kind == MiniVanGameModePlacementKind.SmallHouse
                ? new Color(0.85f, 0.55f, 0.16f, 0.9f)
                : new Color(0.45f, 0.72f, 1f, 0.9f);
            Gizmos.DrawWireCube(transform.position + Vector3.up, new Vector3(4f, 2f, 4f));
        }
    }
}
