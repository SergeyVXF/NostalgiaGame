using UnityEngine;

namespace MiniVanGame
{
    public class MiniVanPizzaOven : MonoBehaviour
    {
        public float InteractRadius = 2.2f;
        public float CookSeconds = 60f;
        public float BurnSeconds = 80f;
        public Vector3 IndicatorLocalPosition = new Vector3(0f, 0.85f, 0.05f);
        public Vector3 LampLocalPosition = new Vector3(0.34f, 0.72f, 0.05f);
        public float LampScale = 0.12f;

        public bool HasPizza;
        public float CookStartTime = -999f;
        public MiniVanInventoryItem OvenItem = MiniVanInventoryItem.None;

        private TextMesh timerText;
        private Renderer lampRenderer;
        private Material lampMaterial;

        private void Awake()
        {
            EnsureCollider();
            EnsureIndicatorVisuals();
            UpdateIndicatorVisuals();
        }

        private void Update()
        {
            UpdateIndicatorVisuals();
        }

        public bool IsInRange(Vector3 worldPosition)
        {
            return Vector3.Distance(worldPosition, transform.position) <= InteractRadius;
        }

        public bool TryInsert(MiniVanInventoryItem item)
        {
            if (HasPizza || item != MiniVanInventoryItem.RawPizza)
            {
                return false;
            }

            HasPizza = true;
            OvenItem = MiniVanInventoryItem.RawPizza;
            CookStartTime = Time.time;
            UpdateIndicatorVisuals();
            return true;
        }

        public MiniVanInventoryItem TryTake()
        {
            if (!HasPizza)
            {
                return MiniVanInventoryItem.None;
            }

            float elapsed = Time.time - CookStartTime;
            MiniVanInventoryItem result = elapsed >= BurnSeconds ? MiniVanInventoryItem.BurnedPizza : elapsed >= CookSeconds ? MiniVanInventoryItem.CookedPizza : MiniVanInventoryItem.RawPizza;
            HasPizza = false;
            OvenItem = MiniVanInventoryItem.None;
            CookStartTime = -999f;
            UpdateIndicatorVisuals();
            return result;
        }

        public float GetCook01()
        {
            if (!HasPizza)
            {
                return 0f;
            }

            return Mathf.Clamp01((Time.time - CookStartTime) / Mathf.Max(0.1f, CookSeconds));
        }

        public bool IsBurning()
        {
            return HasPizza && Time.time - CookStartTime >= CookSeconds;
        }

        private void UpdateIndicatorVisuals()
        {
            EnsureIndicatorVisuals();

            if (!HasPizza)
            {
                SetTimerText("OVEN");
                SetLampColor(new Color(0.2f, 0.2f, 0.2f, 1f));
                return;
            }

            float elapsed = Time.time - CookStartTime;
            if (elapsed >= BurnSeconds)
            {
                SetTimerText("BURNED");
                SetLampColor(new Color(1f, 0.04f, 0.02f, 1f));
                return;
            }

            if (elapsed >= CookSeconds)
            {
                int burnIn = Mathf.Max(0, Mathf.CeilToInt(BurnSeconds - elapsed));
                SetTimerText("READY " + FormatSeconds(burnIn));
                SetLampColor(new Color(0.05f, 1f, 0.18f, 1f));
                return;
            }

            int readyIn = Mathf.Max(0, Mathf.CeilToInt(CookSeconds - elapsed));
            SetTimerText(FormatSeconds(readyIn));
            SetLampColor(new Color(1f, 0.82f, 0.08f, 1f));
        }

        private void EnsureIndicatorVisuals()
        {
            if (timerText == null)
            {
                Transform existingText = transform.Find("Oven Timer Text");
                GameObject textObject = existingText != null ? existingText.gameObject : new GameObject("Oven Timer Text");
                textObject.transform.SetParent(transform, false);
                textObject.transform.localPosition = IndicatorLocalPosition;
                textObject.transform.localScale = Vector3.one * 0.08f;
                timerText = textObject.GetComponent<TextMesh>();
                if (timerText == null)
                {
                    timerText = textObject.AddComponent<TextMesh>();
                }

                timerText.anchor = TextAnchor.MiddleCenter;
                timerText.alignment = TextAlignment.Center;
                timerText.fontSize = 64;
                timerText.characterSize = 0.18f;
                timerText.color = Color.white;
            }

            if (lampRenderer == null)
            {
                Transform existingLamp = transform.Find("Oven Ready Lamp");
                GameObject lampObject = existingLamp != null ? existingLamp.gameObject : GameObject.CreatePrimitive(PrimitiveType.Sphere);
                lampObject.name = "Oven Ready Lamp";
                lampObject.transform.SetParent(transform, false);
                lampObject.transform.localPosition = LampLocalPosition;
                lampObject.transform.localScale = Vector3.one * LampScale;

                Collider collider = lampObject.GetComponent<Collider>();
                if (collider != null)
                {
                    Destroy(collider);
                }

                lampRenderer = lampObject.GetComponent<Renderer>();
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null)
                {
                    shader = Shader.Find("Unlit/Color");
                }

                if (shader == null)
                {
                    shader = Shader.Find("Standard");
                }

                lampMaterial = new Material(shader);
                lampMaterial.name = "Oven Ready Lamp Runtime";
                if (lampRenderer != null)
                {
                    lampRenderer.material = lampMaterial;
                }
            }

            if (timerText != null && Camera.main != null)
            {
                Vector3 toCamera = Camera.main.transform.position - timerText.transform.position;
                if (toCamera.sqrMagnitude > 0.001f)
                {
                    timerText.transform.rotation = Quaternion.LookRotation(-toCamera.normalized, Vector3.up);
                }
            }
        }

        private void SetTimerText(string text)
        {
            if (timerText != null)
            {
                timerText.text = text;
            }
        }

        private void SetLampColor(Color color)
        {
            if (lampMaterial == null)
            {
                return;
            }

            lampMaterial.color = color;
            if (lampMaterial.HasProperty("_BaseColor"))
            {
                lampMaterial.SetColor("_BaseColor", color);
            }
        }

        private static string FormatSeconds(int seconds)
        {
            int minutes = Mathf.Max(0, seconds) / 60;
            int remainder = Mathf.Max(0, seconds) % 60;
            return minutes + ":" + remainder.ToString("00");
        }

        private void EnsureCollider()
        {
            Collider existing = GetComponent<Collider>();
            if (existing != null)
            {
                return;
            }

            BoxCollider box = gameObject.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(1.2f, 1.2f, 1.2f);
        }
    }
}
