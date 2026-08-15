using UnityEngine;
using UnityEngine.UI;

namespace MiniVanGame
{
    /// <summary>
    /// Builds the Face Paint HUD hierarchy (same layout as the old runtime EnsureUi).
    /// Used by the editor menu to regenerate the editable prefab.
    /// </summary>
    public static class MiniVanFacePaintUiBuilder
    {
        public static readonly Color[] DefaultPalette =
        {
            Color.white,
            Color.black,
            new Color(0.75f, 0.75f, 0.78f),
            new Color(0.28f, 0.28f, 0.3f),
            new Color(0.85f, 0.12f, 0.12f),
            new Color(0.55f, 0.05f, 0.08f),
            new Color(0.95f, 0.45f, 0.08f),
            new Color(0.95f, 0.82f, 0.12f),
            new Color(0.55f, 0.9f, 0.2f),
            new Color(0.18f, 0.72f, 0.22f),
            new Color(0.12f, 0.78f, 0.82f),
            new Color(0.15f, 0.35f, 0.95f),
            new Color(0.35f, 0.15f, 0.85f),
            new Color(0.55f, 0.18f, 0.78f),
            new Color(0.95f, 0.35f, 0.7f),
            new Color(0.55f, 0.32f, 0.18f),
            new Color(0.96f, 0.72f, 0.58f),
            new Color(1.00f, 0.45f, 0.40f),
            new Color(0.05f, 0.55f, 0.50f),
            new Color(0.08f, 0.12f, 0.40f),
            new Color(0.85f, 0.65f, 0.15f),
            new Color(0.72f, 0.60f, 0.90f)
        };

        public static MiniVanFacePaintUi Build()
        {
            GameObject canvasGo = new GameObject("FacePaintHUD", typeof(RectTransform));
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 80;
            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasGo.AddComponent<GraphicRaycaster>();
            MiniVanFacePaintUi ui = canvasGo.AddComponent<MiniVanFacePaintUi>();
            ui.Canvas = canvas;

            RectTransform panel = CreatePanel(canvasGo.transform, "ToolPanel",
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(168f, 760f));
            Image panelBg = panel.gameObject.AddComponent<Image>();
            panelBg.color = new Color(0.08f, 0.09f, 0.1f, 0.82f);

            CreateToolButton(panel, "Brush", new Vector2(0f, 330f));
            CreateToolButton(panel, "Eraser", new Vector2(0f, 260f));
            CreateToolButton(panel, "Fill", new Vector2(0f, 190f));

            const int colorColumns = 3;
            float[] colorXs = { -48f, 0f, 48f };
            float y = 120f;
            for (int i = 0; i < DefaultPalette.Length; i++)
            {
                int col = i % colorColumns;
                if (col == 0 && i > 0)
                {
                    y -= 38f;
                }

                CreateColorSwatch(panel, DefaultPalette[i], new Vector2(colorXs[col], y), i);
            }

            CreateSizeSlider(panel, new Vector2(0f, -260f));
            CreateGlyphButton(panel, "Undo", "↶", new Vector2(-28f, -310f), new Vector2(44f, 44f),
                new Color(1f, 1f, 1f, 0.2f));
            CreateGlyphButton(panel, "Redo", "↷", new Vector2(28f, -310f), new Vector2(44f, 44f),
                new Color(1f, 1f, 1f, 0.2f));

            CreateRotateButton(canvasGo.transform, "RotateLeft", "◀", new Vector2(0f, 0.5f), new Vector2(210f, 0f));
            CreateRotateButton(canvasGo.transform, "RotateRight", "▶", new Vector2(1f, 0.5f), new Vector2(-90f, 0f));

            CreateGlyphButtonAnchored(canvasGo.transform, "Close", "✕",
                new Vector2(1f, 1f), new Vector2(-48f, -48f), new Vector2(56f, 56f));
            CreateGlyphButtonAnchored(canvasGo.transform, "Confirm", "✓",
                new Vector2(1f, 0f), new Vector2(-48f, 48f), new Vector2(56f, 56f));

            GameObject cursorGo = new GameObject("BrushCursor", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            cursorGo.transform.SetParent(canvasGo.transform, false);
            RectTransform cursor = cursorGo.GetComponent<RectTransform>();
            cursor.sizeDelta = new Vector2(40f, 40f);
            Image cursorImg = cursorGo.GetComponent<Image>();
            cursorImg.color = new Color(1f, 1f, 1f, 0.85f);
            cursorImg.raycastTarget = false;

            GameObject ring = new GameObject("Ring", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            ring.transform.SetParent(cursor, false);
            RectTransform ringRt = ring.GetComponent<RectTransform>();
            ringRt.anchorMin = Vector2.zero;
            ringRt.anchorMax = Vector2.one;
            ringRt.offsetMin = new Vector2(-6f, -6f);
            ringRt.offsetMax = new Vector2(6f, 6f);
            Image ringImg = ring.GetComponent<Image>();
            ringImg.color = new Color(1f, 1f, 1f, 0.35f);
            ringImg.raycastTarget = false;

            Text hint = CreateLabel(canvasGo.transform, "Hint",
                "Paint your face — ◀ ▶ rotate · confirm to keep",
                new Vector2(0.5f, 0f), new Vector2(0f, 28f), new Vector2(560f, 28f));
            hint.alignment = TextAnchor.MiddleCenter;

#if UNITY_EDITOR
            ui.EditorAutoWire();
#endif
            return ui;
        }

        private static RectTransform CreatePanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 anchoredPos, Vector2 size)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            return rt;
        }

        private static void CreateToolButton(Transform parent, string label, Vector2 pos)
        {
            GameObject go = new GameObject(label, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(64f, 64f);
            go.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.08f);

            GameObject iconGo = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            iconGo.transform.SetParent(go.transform, false);
            RectTransform iconRt = iconGo.GetComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0.5f, 0.5f);
            iconRt.anchorMax = new Vector2(0.5f, 0.5f);
            iconRt.sizeDelta = new Vector2(44f, 44f);
            Image icon = iconGo.GetComponent<Image>();
            icon.raycastTarget = false;
            icon.preserveAspect = true;
            Sprite sprite = LoadToolIconSprite(label);
            if (sprite != null)
            {
                icon.sprite = sprite;
                icon.color = Color.white;
            }
            else
            {
                string glyph = label == "Fill" ? "▣" : label.Substring(0, 1);
                Text text = CreateLabel(go.transform, "Fallback", glyph, Vector2.one * 0.5f, Vector2.zero, new Vector2(64f, 64f));
                text.fontSize = 28;
                text.alignment = TextAnchor.MiddleCenter;
                icon.enabled = false;
            }
        }

        private static Sprite LoadToolIconSprite(string label)
        {
            string key = label switch
            {
                "Brush" => "FacePaintUI/icon_brush",
                "Eraser" => "FacePaintUI/icon_eraser",
                "Fill" => "FacePaintUI/icon_fill",
                _ => null
            };
            return key == null ? null : Resources.Load<Sprite>(key);
        }

        private static void CreateColorSwatch(Transform parent, Color color, Vector2 pos, int index)
        {
            GameObject go = new GameObject("Swatch_" + index.ToString("00"),
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(Outline));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(34f, 34f);
            go.GetComponent<Image>().color = color;
            Outline outline = go.GetComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.5f);
            outline.effectDistance = new Vector2(1f, 1f);
        }

        private static void CreateSizeSlider(Transform parent, Vector2 pos)
        {
            GameObject go = new GameObject("Size", typeof(RectTransform), typeof(Slider));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(110f, 20f);

            GameObject bg = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            bg.transform.SetParent(go.transform, false);
            RectTransform bgRt = bg.GetComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;
            bg.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.25f);

            GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(go.transform, false);
            RectTransform fillAreaRt = fillArea.GetComponent<RectTransform>();
            fillAreaRt.anchorMin = Vector2.zero;
            fillAreaRt.anchorMax = Vector2.one;
            fillAreaRt.offsetMin = new Vector2(5f, 0f);
            fillAreaRt.offsetMax = new Vector2(-5f, 0f);

            GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            fill.transform.SetParent(fillArea.transform, false);
            RectTransform fillRt = fill.GetComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;
            fill.GetComponent<Image>().color = Color.white;

            GameObject handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleArea.transform.SetParent(go.transform, false);
            RectTransform handleAreaRt = handleArea.GetComponent<RectTransform>();
            handleAreaRt.anchorMin = Vector2.zero;
            handleAreaRt.anchorMax = Vector2.one;
            handleAreaRt.offsetMin = Vector2.zero;
            handleAreaRt.offsetMax = Vector2.zero;

            GameObject handle = new GameObject("Handle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            handle.transform.SetParent(handleArea.transform, false);
            RectTransform handleRt = handle.GetComponent<RectTransform>();
            handleRt.sizeDelta = new Vector2(18f, 18f);
            handle.GetComponent<Image>().color = Color.white;

            Slider slider = go.GetComponent<Slider>();
            slider.fillRect = fillRt;
            slider.handleRect = handleRt;
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0.02f;
            slider.maxValue = 1f;
            slider.value = 0.22f;
        }

        private static void CreateRotateButton(Transform parent, string name, string glyph, Vector2 anchor, Vector2 anchoredPos)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(64f, 96f);
            go.GetComponent<Image>().color = new Color(0.08f, 0.09f, 0.1f, 0.82f);
            go.GetComponent<Button>().transition = Selectable.Transition.None;
            Text text = CreateLabel(go.transform, "Label", glyph, Vector2.one * 0.5f, Vector2.zero, new Vector2(64f, 96f));
            text.fontSize = 36;
            text.alignment = TextAnchor.MiddleCenter;
        }

        private static void CreateGlyphButton(Transform parent, string name, string glyph, Vector2 pos, Vector2 size, Color bg)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            go.GetComponent<Image>().color = bg;
            Text text = CreateLabel(go.transform, "Label", glyph, Vector2.one * 0.5f, Vector2.zero, size);
            text.fontSize = 22;
            text.alignment = TextAnchor.MiddleCenter;
        }

        private static void CreateGlyphButtonAnchored(Transform parent, string name, string glyph, Vector2 anchor,
            Vector2 anchoredPos, Vector2 size)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            go.GetComponent<Image>().color = new Color(0.08f, 0.09f, 0.1f, 0.82f);
            Text text = CreateLabel(go.transform, "Label", glyph, Vector2.one * 0.5f, Vector2.zero, size);
            text.fontSize = 28;
            text.alignment = TextAnchor.MiddleCenter;
        }

        private static Text CreateLabel(Transform parent, string name, string value, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            Text text = go.GetComponent<Text>();
            text.text = value;
            text.color = Color.white;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (text.font == null)
            {
                text.font = Font.CreateDynamicFontFromOSFont("Arial", 16);
            }

            text.raycastTarget = false;
            return text;
        }
    }
}
