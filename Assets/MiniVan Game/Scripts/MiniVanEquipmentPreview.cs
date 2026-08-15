using UnityEngine;

namespace MiniVanGame
{
    /// <summary>
    /// Live character preview for the equipment window: the player visual wearing
    /// equipped cosmetics, filmed by its own camera into a render texture.
    /// It lives far above the level so no gameplay camera can ever see it.
    /// </summary>
    public sealed class MiniVanEquipmentPreview : MonoBehaviour
    {
        private const float StageHeight = 6000f;

        private readonly Transform[] cosmetics = new Transform[MiniVanCosmeticCatalog.SlotCount];
        private readonly MiniVanInventoryItem[] cosmeticItems = new MiniVanInventoryItem[MiniVanCosmeticCatalog.SlotCount];

        private Transform stage;
        private Transform model;
        private Transform previewVisual;
        private Transform previewHeadBone;
        private Camera previewCamera;
        private RenderTexture texture;
        private Renderer bodyRenderer;

        public RenderTexture Texture => texture;

        public static MiniVanEquipmentPreview Create(MiniVanPlayer player)
        {
            GameObject go = new GameObject("MiniVan Equipment Preview");
            MiniVanEquipmentPreview preview = go.AddComponent<MiniVanEquipmentPreview>();
            preview.Build(player);
            return preview;
        }

        private void Build(MiniVanPlayer player)
        {
            transform.position = new Vector3(0f, StageHeight, 0f);
            stage = transform;

            model = new GameObject("Model").transform;
            model.SetParent(stage, false);
            model.localRotation = Quaternion.Euler(0f, 180f, 0f);

            if (!TryBuildPlayerVisual(player))
            {
                BuildCapsuleFallback(player != null ? player.GetBodyPreviewMaterial() : null);
            }

            texture = new RenderTexture(320, 614, 16, RenderTextureFormat.ARGB32)
            {
                name = "MiniVanEquipmentPreviewRT",
                antiAliasing = 2
            };
            texture.Create();

            GameObject cameraObject = new GameObject("PreviewCamera");
            cameraObject.transform.SetParent(stage, false);
            cameraObject.transform.localPosition = new Vector3(0f, 0.1f, -3.2f);
            cameraObject.transform.localRotation = Quaternion.identity;

            previewCamera = cameraObject.AddComponent<Camera>();
            previewCamera.orthographic = true;
            previewCamera.orthographicSize = 1.35f;
            previewCamera.nearClipPlane = 0.05f;
            previewCamera.farClipPlane = 20f;
            previewCamera.clearFlags = CameraClearFlags.SolidColor;
            previewCamera.backgroundColor = new Color(0.07f, 0.08f, 0.1f, 1f);
            previewCamera.targetTexture = texture;
            previewCamera.enabled = false;

            GameObject lightObject = new GameObject("PreviewLight");
            lightObject.transform.SetParent(stage, false);
            lightObject.transform.localPosition = new Vector3(-1.4f, 1.8f, -2.2f);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 12f;
            light.intensity = 3.4f;
            light.color = new Color(1f, 0.97f, 0.9f);
        }

        private bool TryBuildPlayerVisual(MiniVanPlayer player)
        {
            GameObject prefab = player != null ? player.PlayerVisualPrefab : null;
            if (prefab == null)
            {
                prefab = Resources.Load<GameObject>("MiniVan/Player/MiniVanPlayerVisual");
            }

            if (prefab == null)
            {
                return false;
            }

            GameObject visual = Object.Instantiate(prefab, model, false);
            visual.name = "PreviewBody";
            visual.transform.localPosition = player != null
                ? player.PlayerVisualLocalPosition
                : new Vector3(0f, -0.99f, 0f);
            visual.transform.localRotation = Quaternion.identity;
            float scale = player != null ? Mathf.Max(0.01f, player.PlayerVisualUniformScale) : 1.234568f;
            visual.transform.localScale = Vector3.one * scale;
            previewVisual = visual.transform;
            previewHeadBone = previewVisual.Find("Body/Head");

            Animator animator = visual.GetComponent<Animator>();
            if (animator != null)
            {
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }

            return true;
        }

        private void BuildCapsuleFallback(Material bodyMaterial)
        {
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "PreviewBody";
            body.transform.SetParent(model, false);
            Object.Destroy(body.GetComponent<Collider>());
            bodyRenderer = body.GetComponent<Renderer>();
            if (bodyRenderer != null && bodyMaterial != null)
            {
                bodyRenderer.sharedMaterial = bodyMaterial;
            }
        }

        public void SetActive(bool active)
        {
            if (previewCamera != null)
            {
                previewCamera.enabled = active;
            }

            gameObject.SetActive(true);
        }

        /// <summary>Rebuilds the worn models so the preview matches the equipment slots.</summary>
        public void Refresh(MiniVanPlayer player)
        {
            if (player == null || model == null)
            {
                return;
            }

            SyncAppearance(player);

            Transform headBone = previewHeadBone;
            Transform wearerRoot = previewVisual != null ? previewVisual : model;
            for (int i = 0; i < MiniVanCosmeticCatalog.SlotCount; i++)
            {
                MiniVanEquipmentSlot slot = (MiniVanEquipmentSlot)i;
                MiniVanInventoryItem item = player.GetEquippedItem(slot);
                Transform expectedParent = slot == MiniVanEquipmentSlot.Head && headBone != null
                    ? headBone
                    : wearerRoot;
                if (cosmeticItems[i] == item && (item == MiniVanInventoryItem.None || (cosmetics[i] != null && cosmetics[i].parent == expectedParent)))
                {
                    continue;
                }

                if (cosmetics[i] != null)
                {
                    Object.Destroy(cosmetics[i].gameObject);
                    cosmetics[i] = null;
                }

                cosmeticItems[i] = item;
                if (item == MiniVanInventoryItem.None)
                {
                    continue;
                }

                Transform visual = MiniVanCosmeticVisual.Build(item, expectedParent, "Preview_" + slot);
                MiniVanCosmeticCatalog.AttachToWearer(visual, slot, wearerRoot, headBone, item);
                cosmetics[i] = visual;
            }
        }

        public void SetYaw(float degrees)
        {
            if (model != null)
            {
                model.localRotation = Quaternion.Euler(0f, 180f + degrees, 0f);
            }
        }

        private void SyncAppearance(MiniVanPlayer player)
        {
            if (previewVisual != null)
            {
                Transform liveRoot = player.GetPlayerVisualRoot();
                if (liveRoot != null)
                {
                    CopyRendererMaterials(liveRoot, previewVisual);
                    return;
                }
            }

            if (bodyRenderer != null)
            {
                Material material = player.GetBodyPreviewMaterial();
                if (material != null && bodyRenderer.sharedMaterial != material)
                {
                    bodyRenderer.sharedMaterial = material;
                }
            }
        }

        private static void CopyRendererMaterials(Transform liveRoot, Transform previewRoot)
        {
            Renderer[] live = liveRoot.GetComponentsInChildren<Renderer>(true);
            Renderer[] preview = previewRoot.GetComponentsInChildren<Renderer>(true);
            for (int p = 0; p < preview.Length; p++)
            {
                Renderer previewRenderer = preview[p];
                if (previewRenderer == null)
                {
                    continue;
                }

                string name = previewRenderer.gameObject.name;
                for (int l = 0; l < live.Length; l++)
                {
                    Renderer liveRenderer = live[l];
                    if (liveRenderer == null || liveRenderer.gameObject.name != name)
                    {
                        continue;
                    }

                    previewRenderer.sharedMaterial = liveRenderer.sharedMaterial;
                    break;
                }
            }
        }

        private void OnDestroy()
        {
            if (previewCamera != null)
            {
                previewCamera.targetTexture = null;
            }

            if (texture != null)
            {
                texture.Release();
                Object.Destroy(texture);
                texture = null;
            }
        }
    }
}
