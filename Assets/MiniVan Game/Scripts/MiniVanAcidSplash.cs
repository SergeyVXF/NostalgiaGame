using UnityEngine;

namespace MiniVanGame
{
    /// <summary>
    /// One-shot acid splash. Tune the ParticleSystem on the prefab.
    /// </summary>
    public sealed class MiniVanAcidSplash : MonoBehaviour
    {
        [Min(0.1f)] public float DestroyAfter = 2f;

        public static void Spawn(GameObject prefab, Vector3 point, Vector3 incomingVelocity)
        {
            if (prefab == null)
            {
                return;
            }

            Vector3 away = incomingVelocity.sqrMagnitude > 0.01f
                ? incomingVelocity.normalized
                : Vector3.up;
            Quaternion rotation = Quaternion.LookRotation(away, Vector3.up);
            GameObject instance = Instantiate(prefab, point, rotation);
            instance.name = prefab.name;
            MiniVanAcidSplash splash = instance.GetComponent<MiniVanAcidSplash>();
            if (splash == null)
            {
                splash = instance.AddComponent<MiniVanAcidSplash>();
            }

            splash.Play();
        }

        private void Start()
        {
            Play();
        }

        public void Play()
        {
            ParticleSystem[] systems = GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < systems.Length; i++)
            {
                if (systems[i] != null)
                {
                    systems[i].Play(true);
                }
            }

            Destroy(gameObject, DestroyAfter);
        }
    }
}
