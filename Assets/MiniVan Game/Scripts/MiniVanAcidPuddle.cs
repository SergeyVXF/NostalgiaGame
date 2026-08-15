using UnityEngine;

namespace MiniVanGame
{
    /// <summary>
    /// Ground acid puddle. Tune the ParticleSystem on the prefab.
    /// </summary>
    public sealed class MiniVanAcidPuddle : MonoBehaviour
    {
        [Min(0.2f)] public float Lifetime = 6f;

        private void Start()
        {
            ParticleSystem[] systems = GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < systems.Length; i++)
            {
                if (systems[i] != null && !systems[i].isPlaying)
                {
                    systems[i].Play(true);
                }
            }

            Destroy(gameObject, Lifetime);
        }
    }
}
