using UnityEngine;

namespace MiniVanGame
{
    [RequireComponent(typeof(Collider))]
    public class MiniVanPassengerBlocker : MonoBehaviour
    {
        public bool BlocksPassengers = true;

        private void Reset()
        {
            ConfigureCollider();
        }

        private void Awake()
        {
            ConfigureCollider();
        }

        private void ConfigureCollider()
        {
            Collider blocker = GetComponent<Collider>();
            if (blocker != null)
            {
                blocker.isTrigger = true;
            }
        }
    }
}
