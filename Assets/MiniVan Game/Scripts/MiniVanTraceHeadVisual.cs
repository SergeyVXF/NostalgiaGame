using UnityEngine;

namespace MiniVanGame
{
    /// <summary>
    /// Dropped TraceZombie skull. Burns as 20% of a regular zombie part.
    /// </summary>
    public static class MiniVanTraceHeadVisual
    {
        public static readonly Color Bone = new Color(0.76f, 0.66f, 0.46f, 1f);
        public static readonly Color Socket = new Color(0.06f, 0.06f, 0.07f, 1f);

        public static GameObject Create(Transform parent, bool keepColliders)
        {
            GameObject root = new GameObject("TraceHead");
            if (parent != null)
            {
                root.transform.SetParent(parent, false);
            }

            Material bone = CreateLit(Bone, 0.12f);
            Material voidMat = CreateLit(Socket, 0.04f);
            AddCube(root.transform, "Skull", Vector3.zero, new Vector3(0.42f, 0.42f, 0.42f), bone, keepColliders);
            AddCube(root.transform, "EyeL", new Vector3(-0.09f, 0.05f, 0.21f), new Vector3(0.10f, 0.10f, 0.04f), voidMat, keepColliders);
            AddCube(root.transform, "EyeR", new Vector3(0.09f, 0.05f, 0.21f), new Vector3(0.10f, 0.10f, 0.04f), voidMat, keepColliders);
            AddCube(root.transform, "Mouth", new Vector3(0f, -0.08f, 0.21f), new Vector3(0.16f, 0.07f, 0.04f), voidMat, keepColliders);
            return root;
        }

        private static Material CreateLit(Color color, float smoothness)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material mat = new Material(shader);
            mat.color = color;
            if (mat.HasProperty("_BaseColor"))
            {
                mat.SetColor("_BaseColor", color);
            }

            if (mat.HasProperty("_Smoothness"))
            {
                mat.SetFloat("_Smoothness", smoothness);
            }

            return mat;
        }

        private static void AddCube(
            Transform parent,
            string name,
            Vector3 localPos,
            Vector3 scale,
            Material mat,
            bool keepColliders)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = localPos;
            cube.transform.localScale = scale;
            Renderer renderer = cube.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = mat;
            }

            Collider collider = cube.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = keepColliders;
            }
        }
    }
}
