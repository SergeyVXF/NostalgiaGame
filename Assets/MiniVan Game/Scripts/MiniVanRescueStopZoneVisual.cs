using UnityEngine;
using UnityEngine.Rendering;

namespace MiniVanGame
{
    public static class MiniVanRescueStopZoneVisual
    {
        public static void Ensure(Transform parent, string objectName, float radius, Color color)
        {
            if (parent == null || radius <= 0.01f)
            {
                return;
            }

            Transform existing = parent.Find(objectName);
            GameObject visualObject;
            if (existing != null)
            {
                visualObject = existing.gameObject;
            }
            else
            {
                visualObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                visualObject.name = objectName;
                visualObject.transform.SetParent(parent, false);
                Collider collider = visualObject.GetComponent<Collider>();
                if (collider != null)
                {
                    if (Application.isPlaying)
                    {
                        Object.Destroy(collider);
                    }
                    else
                    {
                        Object.DestroyImmediate(collider);
                    }
                }
            }

            visualObject.transform.localPosition = Vector3.up * 0.035f;
            visualObject.transform.localRotation = Quaternion.identity;
            visualObject.transform.localScale = new Vector3(radius * 2f, 0.015f, radius * 2f);
            visualObject.layer = parent.gameObject.layer;

            Renderer renderer = visualObject.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = CreateZoneMaterial(objectName + " Material", color);
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        private static Material CreateZoneMaterial(string materialName, Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            Material material = new Material(shader);
            material.name = materialName;
            Color resolvedColor = new Color(color.r, color.g, color.b, Mathf.Clamp01(color.a));
            material.color = resolvedColor;
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", resolvedColor);
            }

            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            return material;
        }
    }
}
