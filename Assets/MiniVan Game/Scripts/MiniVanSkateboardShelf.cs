using UnityEngine;

namespace MiniVanGame
{
    public class MiniVanSkateboardShelf : MonoBehaviour
    {
        public Transform AnchorPoint;
        public float InteractRadius = 1.45f;
        public Vector3 DefaultLocalAnchorPosition = Vector3.zero;
        public Vector3 DefaultLocalShelfSize = new Vector3(1.55f, 0.08f, 0.42f);

        private Material shelfMaterial;

        private void Awake()
        {
            EnsureShelfSetup();
        }

        private void OnEnable()
        {
            EnsureShelfSetup();
        }

        public Vector3 AnchorPosition
        {
            get
            {
                EnsureShelfSetup();
                return AnchorPoint != null ? AnchorPoint.position : transform.TransformPoint(DefaultLocalAnchorPosition);
            }
        }

        public Quaternion AnchorRotation
        {
            get
            {
                EnsureShelfSetup();
                return AnchorPoint != null ? AnchorPoint.rotation : transform.rotation;
            }
        }

        public bool IsInRange(Vector3 worldPosition)
        {
            return Vector3.Distance(worldPosition, AnchorPosition) <= InteractRadius;
        }

        public MiniVanSkateboard FindStoredSkateboard()
        {
            MiniVanSkateboard[] skateboards = MiniVanSceneScan.Get<MiniVanSkateboard>();
            MiniVanSkateboard best = null;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < skateboards.Length; i++)
            {
                MiniVanSkateboard skateboard = skateboards[i];
                if (skateboard == null || !skateboard.IsOnShelf.Value)
                {
                    continue;
                }

                float distance = Vector3.Distance(skateboard.transform.position, AnchorPosition);
                if (distance < bestDistance && distance <= InteractRadius + 0.8f)
                {
                    best = skateboard;
                    bestDistance = distance;
                }
            }

            return best;
        }

        public MiniVanHoverboardM FindStoredHoverboardM()
        {
            MiniVanHoverboardM[] hoverboards = MiniVanSceneScan.Get<MiniVanHoverboardM>();
            MiniVanHoverboardM best = null;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < hoverboards.Length; i++)
            {
                MiniVanHoverboardM hoverboard = hoverboards[i];
                if (hoverboard == null || !hoverboard.IsOnShelf.Value)
                {
                    continue;
                }

                float distance = Vector3.Distance(hoverboard.transform.position, AnchorPosition);
                if (distance < bestDistance && distance <= InteractRadius + 0.8f)
                {
                    best = hoverboard;
                    bestDistance = distance;
                }
            }

            return best;
        }

        public bool HasStoredBoard()
        {
            return FindStoredSkateboard() != null || FindStoredHoverboardM() != null;
        }

        public void EnsureShelfSetup()
        {
            EnsureAnchor();
            EnsureVisual();
            EnsureTrigger();
        }

        private void EnsureAnchor()
        {
            if (AnchorPoint != null)
            {
                AnchorPoint.localPosition = DefaultLocalAnchorPosition;
                AnchorPoint.localRotation = Quaternion.identity;
                return;
            }

            Transform existing = transform.Find("Skateboard Shelf Anchor");
            if (existing != null)
            {
                existing.localPosition = DefaultLocalAnchorPosition;
                existing.localRotation = Quaternion.identity;
                AnchorPoint = existing;
                return;
            }

            GameObject anchor = new GameObject("Skateboard Shelf Anchor");
            anchor.transform.SetParent(transform, false);
            anchor.transform.localPosition = DefaultLocalAnchorPosition;
            anchor.transform.localRotation = Quaternion.identity;
            AnchorPoint = anchor.transform;
        }

        private void EnsureVisual()
        {
            Transform existing = transform.Find("Skateboard Shelf Visual");
            if (existing != null)
            {
                existing.localPosition = DefaultLocalAnchorPosition + new Vector3(0f, -0.08f, 0f);
                existing.localRotation = Quaternion.identity;
                existing.localScale = DefaultLocalShelfSize;
                EnsureVisualMaterial(existing.GetComponent<Renderer>());
                return;
            }

            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "Skateboard Shelf Visual";
            visual.transform.SetParent(transform, false);
            visual.transform.localPosition = DefaultLocalAnchorPosition + new Vector3(0f, -0.08f, 0f);
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = DefaultLocalShelfSize;

            Collider visualCollider = visual.GetComponent<Collider>();
            if (visualCollider != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(visualCollider);
                }
                else
                {
                    DestroyImmediate(visualCollider);
                }
            }

            Renderer renderer = visual.GetComponent<Renderer>();
            EnsureVisualMaterial(renderer);
        }

        private void EnsureVisualMaterial(Renderer renderer)
        {
            if (renderer == null)
            {
                return;
            }

            if (shelfMaterial == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null)
                {
                    shader = Shader.Find("Unlit/Color");
                }
                if (shader == null)
                {
                    shader = Shader.Find("Standard");
                }

                shelfMaterial = new Material(shader);
                shelfMaterial.color = new Color(0.18f, 0.19f, 0.18f, 1f);
            }

            renderer.sharedMaterial = shelfMaterial;
        }

        private void EnsureTrigger()
        {
            BoxCollider trigger = GetComponent<BoxCollider>();
            if (trigger == null)
            {
                trigger = gameObject.AddComponent<BoxCollider>();
            }

            trigger.isTrigger = true;
            trigger.center = DefaultLocalAnchorPosition;
            trigger.size = new Vector3(1.9f, 1.35f, 1.05f);
        }
    }
}
