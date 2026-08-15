using UnityEngine;

namespace MiniVanGame
{
    public class MiniVanShrinkAndDestroy : MonoBehaviour
    {
        public float Lifetime = 1f;

        private Vector3 startScale;
        private float age;

        private void Awake()
        {
            startScale = transform.localScale;
        }

        private void Update()
        {
            age += Time.deltaTime;
            float lifetime = Mathf.Max(0.05f, Lifetime);
            float t = Mathf.Clamp01(age / lifetime);
            float scale = 1f - Mathf.SmoothStep(0f, 1f, t);
            transform.localScale = startScale * scale;

            if (age >= lifetime)
            {
                Destroy(gameObject);
            }
        }
    }
}
