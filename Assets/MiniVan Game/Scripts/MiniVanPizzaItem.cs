using UnityEngine;

namespace MiniVanGame
{
    public class MiniVanPizzaItem : MonoBehaviour
    {
        public MiniVanInventoryItem Item = MiniVanInventoryItem.Flour;
        public MiniVanPizzaItemType Type = MiniVanPizzaItemType.Ingredient;
        public float PickupRadius = 2.2f;
        public bool CanHoldInHands = true;
        public bool CanPutInInventory = true;

        private Renderer[] renderers;
        private Material[][] originalMaterials;
        private Material outlineMaterial;
        private bool outlined;

        public bool IsAvailable => gameObject.activeInHierarchy;

        private void Awake()
        {
            CacheRenderers();
            EnsureCollider();
        }

        private void OnDisable()
        {
            SetOutlined(false);
        }

        public bool CanPickup(Vector3 worldPosition)
        {
            return CanPutInInventory && Vector3.Distance(worldPosition, transform.position) <= PickupRadius;
        }

        public void SetOutlined(bool value)
        {
            if (outlined == value)
            {
                return;
            }

            CacheRenderers();
            outlined = value;

            if (renderers == null)
            {
                return;
            }

            if (value)
            {
                Material outline = GetOutlineMaterial();
                for (int i = 0; i < renderers.Length; i++)
                {
                    if (renderers[i] == null)
                    {
                        continue;
                    }

                    Material[] source = renderers[i].sharedMaterials;
                    Material[] highlighted = new Material[source.Length + 1];
                    for (int j = 0; j < source.Length; j++)
                    {
                        highlighted[j] = source[j];
                    }

                    highlighted[highlighted.Length - 1] = outline;
                    renderers[i].sharedMaterials = highlighted;
                }
            }
            else if (originalMaterials != null)
            {
                for (int i = 0; i < renderers.Length; i++)
                {
                    if (renderers[i] != null && i < originalMaterials.Length)
                    {
                        renderers[i].sharedMaterials = originalMaterials[i];
                    }
                }
            }
        }

        private void CacheRenderers()
        {
            if (renderers != null)
            {
                return;
            }

            renderers = GetComponentsInChildren<Renderer>(true);
            originalMaterials = new Material[renderers.Length][];
            for (int i = 0; i < renderers.Length; i++)
            {
                originalMaterials[i] = renderers[i] != null ? renderers[i].sharedMaterials : new Material[0];
            }
        }

        private void EnsureCollider()
        {
            if (GetComponentInChildren<Collider>() != null)
            {
                return;
            }

            BoxCollider box = gameObject.AddComponent<BoxCollider>();
            box.isTrigger = false;
            box.size = Vector3.one * 0.35f;
        }

        private Material GetOutlineMaterial()
        {
            if (outlineMaterial != null)
            {
                return outlineMaterial;
            }

            outlineMaterial = Resources.Load<Material>("Panelka/ThinWhiteOutline");
            if (outlineMaterial != null)
            {
                outlineMaterial = new Material(outlineMaterial)
                {
                    name = "Interactive Item Thin White Outline"
                };
                MiniVanSnesOutline.ApplyOutlineSettings(outlineMaterial, Color.white);
                return outlineMaterial;
            }

            Shader shader = Shader.Find("MiniVanGame/ThinWhiteOutline");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            outlineMaterial = new Material(shader != null ? shader : Shader.Find("Standard"));
            outlineMaterial.name = "Interactive Item Thin White Outline";
            MiniVanSnesOutline.ApplyOutlineSettings(outlineMaterial, Color.white);
            return outlineMaterial;
        }
    }
}
