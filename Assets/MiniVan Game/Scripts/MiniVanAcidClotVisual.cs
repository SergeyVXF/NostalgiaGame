using UnityEngine;

namespace MiniVanGame
{
    /// <summary>
    /// Low-poly acid glob used as the death pickup / held visual. No gameplay use yet.
    /// </summary>
    public static class MiniVanAcidClotVisual
    {
        public static readonly Color Acid = new Color(0.55f, 0.82f, 0.08f, 1f);
        public static readonly Color AcidHot = new Color(0.78f, 0.95f, 0.22f, 1f);

        public static GameObject Create(Transform parent, bool keepColliders)
        {
            GameObject root = new GameObject("AcidClot");
            if (parent != null)
            {
                root.transform.SetParent(parent, false);
            }

            Material mat = CreateAcidMaterial();
            AddBlob(root.transform, "Core", Vector3.zero, 0.16f, mat, keepColliders);
            AddBlob(root.transform, "LumpA", new Vector3(0.07f, 0.05f, 0.04f), 0.10f, mat, keepColliders);
            AddBlob(root.transform, "LumpB", new Vector3(-0.06f, -0.04f, 0.05f), 0.09f, mat, keepColliders);
            AddBlob(root.transform, "Drip", new Vector3(0.01f, -0.12f, 0.02f), 0.06f, mat, keepColliders);
            return root;
        }

        public static Material CreateAcidMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material mat = new Material(shader);
            mat.name = "M_AcidClot";
            mat.color = Acid;
            if (mat.HasProperty("_BaseColor"))
            {
                mat.SetColor("_BaseColor", Acid);
            }

            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", AcidHot * 2.4f);
            }

            if (mat.HasProperty("_Smoothness"))
            {
                mat.SetFloat("_Smoothness", 0.55f);
            }

            return mat;
        }

        private static void AddBlob(
            Transform parent,
            string name,
            Vector3 localPos,
            float radius,
            Material mat,
            bool keepColliders)
        {
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = name;
            sphere.transform.SetParent(parent, false);
            sphere.transform.localPosition = localPos;
            sphere.transform.localScale = Vector3.one * (radius * 2f);
            Renderer renderer = sphere.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = mat;
            }

            Collider collider = sphere.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = keepColliders;
            }
        }
    }
}
