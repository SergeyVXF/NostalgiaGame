using UnityEngine;

namespace MiniVanGame
{
    public class MiniVanRescueStopZone : MonoBehaviour
    {
        public enum StopZoneKind
        {
            SavePlace,
            Bunker
        }

        [Header("Zone")]
        public StopZoneKind ZoneKind;
        public float Radius = 30f;
        public bool UseForZombieCheck = true;

        [Header("Visual")]
        public bool ShowVisual = true;
        public Color SavePlaceColor = new Color(0.2f, 1f, 0.35f, 0.24f);
        public Color BunkerColor = new Color(0.15f, 0.55f, 1f, 0.24f);

        public Vector3 Position => transform.position;
        public float EffectiveRadius => Mathf.Max(0.5f, Radius);

        private void Awake()
        {
            EnsureVisual();
        }

        private void Start()
        {
            EnsureVisual();
        }

        private void OnValidate()
        {
            Radius = Mathf.Max(0.5f, Radius);
        }

        public void EnsureVisual()
        {
            if (!ShowVisual)
            {
                return;
            }

            MiniVanRescueStopZoneVisual.Ensure(transform, "Stop Zone Visual", EffectiveRadius, GetColor());
        }

        private Color GetColor()
        {
            return ZoneKind == StopZoneKind.Bunker ? BunkerColor : SavePlaceColor;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = GetColor();
            Gizmos.DrawWireSphere(Position, EffectiveRadius);
        }
    }
}
