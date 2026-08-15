using System.IO;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace MiniVanGame.EditorTools
{
    /// <summary>
    /// Builds the equipment window prefab, the slot silhouettes and the test hat pickup prefab.
    /// </summary>
    public static class MiniVanEquipmentUiBuilder
    {
        public const string ResourcesFolder = "Assets/MiniVan Game/Resources/EquipmentUI";
        public const string PrefabPath = ResourcesFolder + "/EquipmentHUD.prefab";
        public const string RawIconFolder = "Assets/MiniVan Game/Art/UI/EquipmentIcons";
        public const string CosmeticPickupPrefabPath = "Assets/MiniVan Game/Resources/MiniVan/CosmeticPickup.prefab";
        public const string LegacyHatPickupPrefabPath = "Assets/MiniVan Game/Resources/MiniVan/TestHatPickup.prefab";
        public const string NetworkPrefabsListPath = "Assets/DefaultNetworkPrefabs.asset";
        public const string CosmeticModelsFolder = "Assets/MiniVan Game/Resources/MiniVan/Cosmetics";
        public const string CosmeticMaterialsFolder = "Assets/MiniVan Game/Materials/Cosmetics";
        public const string CosmeticMeshFolder = "Assets/MiniVan Game/Models/Cosmetics";
        public const string BandanaPrefabPath = CosmeticModelsFolder + "/ZoroBandana.prefab";
        public const string BandanaMeshPath = CosmeticMeshFolder + "/ZoroBandana_Mesh.asset";
        public const string BandanaMaterialPath = CosmeticMaterialsFolder + "/ZoroBandana_Fabric.mat";

        private const int IconSize = 256;

        private static readonly string[] IconKeys = { "head", "cloak", "boots", "belt" };

        private static readonly Color WindowColor = new Color(0.05f, 0.06f, 0.08f, 0.94f);
        private static readonly Color CellColor = new Color(0.12f, 0.13f, 0.16f, 0.92f);
        private static readonly Color FrameColor = new Color(0.02f, 0.03f, 0.04f, 1f);

        [MenuItem("MiniVan Game/Equipment/Rebuild Everything")]
        public static void RebuildEverything()
        {
            BuildIcons();
            BuildHudPrefab();
            BuildBandanaModel();
            BuildCosmeticPickupPrefab();
        }

        /// <summary>
        /// Bakes the procedural bandana into editable assets: mesh, material and model prefab.
        /// Runtime code prefers this prefab, so edits made here show up in game.
        /// </summary>
        [MenuItem("MiniVan Game/Equipment/Rebuild Zoro Bandana Model")]
        public static void BuildBandanaModel()
        {
            EnsureFolder(CosmeticMaterialsFolder);
            EnsureFolder(CosmeticMeshFolder);
            EnsureFolder(CosmeticModelsFolder);

            Mesh source = MiniVanZoroBandanaMesh.Get();
            Mesh asset = AssetDatabase.LoadAssetAtPath<Mesh>(BandanaMeshPath);
            if (asset == null)
            {
                asset = new Mesh();
                AssetDatabase.CreateAsset(asset, BandanaMeshPath);
            }

            asset.Clear();
            asset.indexFormat = source.indexFormat;
            asset.vertices = source.vertices;
            asset.triangles = source.triangles;
            asset.RecalculateNormals();
            asset.RecalculateBounds();
            asset.name = "ZoroBandana_Mesh";
            EditorUtility.SetDirty(asset);

            Material material = AssetDatabase.LoadAssetAtPath<Material>(BandanaMaterialPath);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                {
                    shader = Shader.Find("Standard");
                }

                material = new Material(shader);
                Color fabric = new Color(0.09f, 0.30f, 0.17f);
                material.color = fabric;
                if (material.HasProperty("_BaseColor"))
                {
                    material.SetColor("_BaseColor", fabric);
                }

                if (material.HasProperty("_Smoothness"))
                {
                    material.SetFloat("_Smoothness", 0.12f);
                }

                if (material.HasProperty("_Metallic"))
                {
                    material.SetFloat("_Metallic", 0f);
                }

                AssetDatabase.CreateAsset(material, BandanaMaterialPath);
            }

            GameObject root = new GameObject("ZoroBandana");
            GameObject cloth = new GameObject("Bandana");
            cloth.transform.SetParent(root.transform, false);
            cloth.AddComponent<MeshFilter>().sharedMesh = asset;
            cloth.AddComponent<MeshRenderer>().sharedMaterial = material;

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, BandanaPrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();

            if (prefab != null)
            {
                EditorGUIUtility.PingObject(prefab);
            }

            Debug.Log("[Equipment] Zoro bandana model ready: " + BandanaPrefabPath);
        }

        [MenuItem("MiniVan Game/Equipment/Rebuild Slot Icons")]
        public static void BuildIcons()
        {
            EnsureFolder(ResourcesFolder);

            for (int i = 0; i < IconKeys.Length; i++)
            {
                string sourcePath = RawIconFolder + "/raw_" + IconKeys[i] + ".png";
                string targetPath = ResourcesFolder + "/icon_" + IconKeys[i] + ".png";
                Texture2D source = LoadReadableTexture(sourcePath);
                if (source == null)
                {
                    Debug.LogWarning("[Equipment] Missing raw icon: " + sourcePath);
                    continue;
                }

                // The generated art is black on white; turn the dark pixels into a white silhouette
                // with matching alpha so the UI can tint it freely.
                Texture2D output = new Texture2D(IconSize, IconSize, TextureFormat.RGBA32, false);
                for (int y = 0; y < IconSize; y++)
                {
                    for (int x = 0; x < IconSize; x++)
                    {
                        Color pixel = source.GetPixelBilinear((x + 0.5f) / IconSize, (y + 0.5f) / IconSize);
                        float luminance = pixel.r * 0.299f + pixel.g * 0.587f + pixel.b * 0.114f;
                        output.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(1f - luminance)));
                    }
                }

                output.Apply();
                File.WriteAllBytes(Path.GetFullPath(targetPath), output.EncodeToPNG());
                Object.DestroyImmediate(output);

                AssetDatabase.ImportAsset(targetPath, ImportAssetOptions.ForceUpdate);
                if (AssetImporter.GetAtPath(targetPath) is TextureImporter importer)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.alphaIsTransparency = true;
                    importer.mipmapEnabled = false;
                    importer.maxTextureSize = IconSize;
                    importer.isReadable = false;
                    importer.SaveAndReimport();
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[Equipment] Slot silhouettes rebuilt in " + ResourcesFolder);
        }

        [MenuItem("MiniVan Game/Equipment/Rebuild HUD Prefab")]
        public static void BuildHudPrefab()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[Equipment] Exit Play Mode before rebuilding the HUD prefab.");
                return;
            }

            EnsureFolder(ResourcesFolder);

            MiniVanEquipmentUi ui = Build();
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(ui.gameObject, PrefabPath);
            Object.DestroyImmediate(ui.gameObject);
            AssetDatabase.SaveAssets();

            if (prefab != null)
            {
                EditorGUIUtility.PingObject(prefab);
            }

            Debug.Log("[Equipment] HUD prefab ready: " + PrefabPath);
        }

        [MenuItem("MiniVan Game/Equipment/Rebuild Cosmetic Pickup Prefab")]
        public static void BuildCosmeticPickupPrefab()
        {
            EnsureFolder("Assets/MiniVan Game/Resources/MiniVan");

            if (AssetDatabase.LoadAssetAtPath<GameObject>(LegacyHatPickupPrefabPath) != null)
            {
                AssetDatabase.DeleteAsset(LegacyHatPickupPrefabPath);
            }

            GameObject root = new GameObject("CosmeticPickup");
            root.AddComponent<NetworkObject>();
            BoxCollider box = root.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = new Vector3(0f, 0.25f, 0f);
            box.size = new Vector3(0.5f, 0.5f, 0.5f);

            MiniVanCosmeticPickup pickup = root.AddComponent<MiniVanCosmeticPickup>();
            pickup.Item = MiniVanInventoryItem.ZoroBandana;

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, CosmeticPickupPrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();

            RegisterNetworkPrefab(prefab);

            if (prefab != null)
            {
                EditorGUIUtility.PingObject(prefab);
            }

            Debug.Log("[Equipment] Cosmetic pickup prefab ready: " + CosmeticPickupPrefabPath);
        }

        /// <summary>Adds the prefab to the default network prefabs list and drops dead entries.</summary>
        internal static void RegisterNetworkPrefab(GameObject prefab)
        {
            Object listAsset = AssetDatabase.LoadAssetAtPath<Object>(NetworkPrefabsListPath);
            if (listAsset == null || prefab == null)
            {
                return;
            }

            SerializedObject serialized = new SerializedObject(listAsset);
            SerializedProperty list = serialized.FindProperty("List");
            if (list == null || !list.isArray)
            {
                return;
            }

            for (int i = list.arraySize - 1; i >= 0; i--)
            {
                SerializedProperty entry = list.GetArrayElementAtIndex(i).FindPropertyRelative("Prefab");
                if (entry == null)
                {
                    continue;
                }

                if (entry.objectReferenceValue == null)
                {
                    list.DeleteArrayElementAtIndex(i);
                }
                else if (entry.objectReferenceValue == prefab)
                {
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    return;
                }
            }

            list.InsertArrayElementAtIndex(list.arraySize);
            SerializedProperty added = list.GetArrayElementAtIndex(list.arraySize - 1).FindPropertyRelative("Prefab");
            added.objectReferenceValue = prefab;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(listAsset);
            AssetDatabase.SaveAssets();
        }

        public static MiniVanEquipmentUi Build()
        {
            GameObject canvasGo = new GameObject("EquipmentHUD", typeof(RectTransform));
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 120;

            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            MiniVanEquipmentUi ui = canvasGo.AddComponent<MiniVanEquipmentUi>();
            ui.Canvas = canvas;

            RectTransform window = CreateImage(canvasGo.transform, "Window", WindowColor).rectTransform;
            window.anchorMin = new Vector2(1f, 0.5f);
            window.anchorMax = new Vector2(1f, 0.5f);
            window.pivot = new Vector2(1f, 0.5f);
            window.anchoredPosition = new Vector2(-48f, 0f);
            window.sizeDelta = new Vector2(470f, 780f);
            ui.Window = window;

            ui.TitleText = CreateText(window, "Title", "ЭКИПИРОВКА", 26, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 1f), new Vector2(0f, -34f), new Vector2(420f, 40f));

            Image frame = CreateImage(window, "PreviewFrame", FrameColor);
            RectTransform frameRect = frame.rectTransform;
            frameRect.anchorMin = new Vector2(0f, 1f);
            frameRect.anchorMax = new Vector2(0f, 1f);
            frameRect.pivot = new Vector2(0f, 1f);
            frameRect.anchoredPosition = new Vector2(26f, -70f);
            frameRect.sizeDelta = new Vector2(250f, 480f);

            GameObject previewGo = new GameObject("Preview", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            previewGo.transform.SetParent(frameRect, false);
            RectTransform previewRect = previewGo.GetComponent<RectTransform>();
            previewRect.anchorMin = Vector2.zero;
            previewRect.anchorMax = Vector2.one;
            previewRect.offsetMin = new Vector2(4f, 4f);
            previewRect.offsetMax = new Vector2(-4f, -4f);
            RawImage preview = previewGo.GetComponent<RawImage>();
            // Dark placeholder until the live render texture is attached at runtime.
            preview.color = new Color(0.1f, 0.11f, 0.13f, 1f);
            preview.raycastTarget = false;
            ui.PreviewImage = preview;

            MiniVanEquipmentCellView[] slots = new MiniVanEquipmentCellView[MiniVanCosmeticCatalog.SlotCount];
            for (int i = 0; i < slots.Length; i++)
            {
                MiniVanEquipmentSlot slot = (MiniVanEquipmentSlot)i;
                MiniVanEquipmentCellView cell = CreateCell(window, "Slot_" + slot,
                    MiniVanEquipmentCellView.CellKind.Equipment, i, 88f);

                RectTransform rect = (RectTransform)cell.transform;
                rect.anchorMin = new Vector2(1f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(1f, 1f);
                rect.anchoredPosition = new Vector2(-40f, -70f - i * 98f);

                cell.Silhouette.sprite = Resources.Load<Sprite>(MiniVanCosmeticCatalog.GetSlotIconResource(slot));
                cell.SlotNameLabel = CreateText(rect, "SlotName", MiniVanCosmeticCatalog.GetSlotName(slot), 12,
                    TextAnchor.LowerCenter, new Vector2(0.5f, 0f), new Vector2(0f, 5f), new Vector2(84f, 16f));
                cell.SlotNameLabel.rectTransform.pivot = new Vector2(0.5f, 0f);
                cell.SlotNameLabel.color = new Color(0.72f, 0.76f, 0.82f, 1f);

                slots[i] = cell;
            }

            ui.EquipmentSlots = slots;

            MiniVanEquipmentCellView[] cells = new MiniVanEquipmentCellView[4];
            for (int i = 0; i < cells.Length; i++)
            {
                MiniVanEquipmentCellView cell = CreateCell(window, "InventoryCell_" + i,
                    MiniVanEquipmentCellView.CellKind.Inventory, i, 84f);

                RectTransform rect = (RectTransform)cell.transform;
                rect.anchorMin = new Vector2(0.5f, 0f);
                rect.anchorMax = new Vector2(0.5f, 0f);
                rect.pivot = new Vector2(0.5f, 0f);
                rect.anchoredPosition = new Vector2(-147f + i * 98f, 96f);

                Text number = CreateText(rect, "Number", (i + 1).ToString(), 14, TextAnchor.MiddleCenter,
                    new Vector2(0.5f, 0f), new Vector2(0f, -16f), new Vector2(40f, 22f));
                number.color = new Color(0.65f, 0.68f, 0.74f, 1f);

                cells[i] = cell;
            }

            ui.InventoryCells = cells;

            ui.HintText = CreateText(window, "Hint", "Перетащи предмет из ячейки в отсек. I — закрыть.", 16,
                TextAnchor.MiddleCenter, new Vector2(0.5f, 0f), new Vector2(0f, 44f), new Vector2(430f, 30f));
            ui.HintText.color = new Color(0.7f, 0.73f, 0.79f, 1f);

            Image ghost = CreateImage(canvasGo.transform, "DragGhost", new Color(0.9f, 0.85f, 0.35f, 0.55f));
            ghost.raycastTarget = false;
            RectTransform ghostRect = ghost.rectTransform;
            ghostRect.anchorMin = new Vector2(0.5f, 0.5f);
            ghostRect.anchorMax = new Vector2(0.5f, 0.5f);
            ghostRect.pivot = new Vector2(0.5f, 0.5f);
            ghostRect.sizeDelta = new Vector2(84f, 84f);
            ui.DragGhost = ghostRect;
            ui.DragGhostLabel = CreateText(ghostRect, "Label", string.Empty, 18, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(84f, 30f));
            ghost.gameObject.SetActive(false);

            return ui;
        }

        private static MiniVanEquipmentCellView CreateCell(Transform parent, string name,
            MiniVanEquipmentCellView.CellKind kind, int index, float size)
        {
            Image background = CreateImage(parent, name, CellColor);
            background.rectTransform.sizeDelta = new Vector2(size, size);

            Outline outline = background.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.75f);
            outline.effectDistance = new Vector2(2f, 2f);

            GameObject iconGo = new GameObject("Silhouette", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            iconGo.transform.SetParent(background.transform, false);
            RectTransform iconRect = iconGo.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.sizeDelta = new Vector2(size * 0.72f, size * 0.72f);
            Image icon = iconGo.GetComponent<Image>();
            icon.color = new Color(1f, 1f, 1f, 0.22f);
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            icon.enabled = kind == MiniVanEquipmentCellView.CellKind.Equipment;

            Text label = CreateText(background.transform, "ItemLabel", string.Empty, 18, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, 30f));

            MiniVanEquipmentCellView cell = background.gameObject.AddComponent<MiniVanEquipmentCellView>();
            cell.Kind = kind;
            cell.Index = index;
            cell.Background = background;
            cell.Silhouette = icon;
            cell.ItemLabel = label;
            return cell;
        }

        private static Image CreateImage(Transform parent, string name, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static Text CreateText(Transform parent, string name, string value, int fontSize,
            TextAnchor anchor, Vector2 anchorPoint, Vector2 position, Vector2 size)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);

            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorPoint;
            rect.anchorMax = anchorPoint;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            Text text = go.GetComponent<Text>();
            text.text = value;
            text.fontSize = fontSize;
            text.alignment = anchor;
            text.color = Color.white;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (text.font == null)
            {
                text.font = Font.CreateDynamicFontFromOSFont("Arial", fontSize);
            }

            return text;
        }

        private static Texture2D LoadReadableTexture(string path)
        {
            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                if (!importer.isReadable || importer.textureCompression != TextureImporterCompression.Uncompressed)
                {
                    importer.textureType = TextureImporterType.Default;
                    importer.isReadable = true;
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.mipmapEnabled = false;
                    importer.SaveAndReimport();
                }
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            string leaf = Path.GetFileName(folder);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
