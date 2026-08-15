using UnityEngine;
using UnityEngine.UI;

namespace MiniVanGame
{
    public static class MiniVanSettingsUiBuilder
    {
        private static readonly Color BgDark = new Color(0.08f, 0.09f, 0.10f, 0.72f);
        private static readonly Color PanelBg = new Color(0.12f, 0.13f, 0.14f, 0.98f);
        private static readonly Color BorderGray = new Color(0.55f, 0.60f, 0.66f, 0.65f);
        private static readonly Color TextWhite = new Color(0.95f, 0.96f, 0.97f, 1f);
        private static readonly Color TextMuted = new Color(0.72f, 0.76f, 0.80f, 1f);
        private static readonly Color AccentOrange = new Color(0.90f, 0.49f, 0.13f, 1f);

        public static MiniVanSettingsUi Build()
        {
            Font font = GetFont();
            GameObject root = new GameObject("MiniVanSettings", typeof(RectTransform));
            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 80;
            CanvasScaler scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<GraphicRaycaster>();

            MiniVanSettingsUi ui = root.AddComponent<MiniVanSettingsUi>();
            ui.Root = root;

            Image dim = CreateImage(root.transform, "Dim", BgDark, true);
            StretchFull(dim.rectTransform);

            RectTransform card = CreateEmpty(root.transform, "SettingsCard");
            card.anchorMin = card.anchorMax = card.pivot = new Vector2(0.5f, 0.5f);
            card.sizeDelta = new Vector2(1440f, 930f);
            Image cardBorder = card.gameObject.AddComponent<Image>();
            Sprite round = LoadIcon("round_rect");
            if (round != null)
            {
                cardBorder.sprite = round;
                cardBorder.type = Image.Type.Sliced;
            }

            cardBorder.color = BorderGray;
            RectTransform inner = CreateEmpty(card, "Inner");
            StretchFull(inner, 2f);
            Image fill = inner.gameObject.AddComponent<Image>();
            if (round != null)
            {
                fill.sprite = round;
                fill.type = Image.Type.Sliced;
            }

            fill.color = PanelBg;
            fill.transform.SetAsFirstSibling();

            // Header
            Button back = CreateIconButton(card, "BackBtn", LoadIcon("icon_back"), TextWhite, 52f);
            SetAnchored(back.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(40f, -30f), new Vector2(52f, 52f));
            ui.BackButton = back;

            Text title = CreateText(card, "Title", "Settings", 44, TextWhite, TextAnchor.MiddleLeft, font);
            SetAnchored(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(112f, -28f), new Vector2(400f, 56f));
            ui.TitleLabel = title;

            Image hline = CreateImage(card, "HeaderLine", BorderGray, false);
            SetAnchored(hline.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -100f), new Vector2(-40f, 2f));

            // Sidebar
            RectTransform sidebar = CreateEmpty(card, "Sidebar");
            sidebar.anchorMin = new Vector2(0f, 0f);
            sidebar.anchorMax = new Vector2(0f, 1f);
            sidebar.pivot = new Vector2(0f, 1f);
            sidebar.anchoredPosition = new Vector2(24f, -120f);
            sidebar.sizeDelta = new Vector2(360f, -150f);

            Image vline = CreateImage(card, "VDivider", BorderGray, false);
            SetAnchored(vline.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0.5f, 0.5f),
                new Vector2(408f, -30f), new Vector2(2f, -140f));

            string[] cats = { "Keyboard", "Sound", "Video", "Game" };
            string[] icons = { "icon_keyboard", "icon_sound", "icon_video", "icon_game" };
            ui.CategoryButtons = new Button[4];
            ui.CategoryIcons = new Image[4];
            ui.CategoryLabels = new Text[4];
            ui.CategoryActiveBars = new Image[4];

            for (int i = 0; i < 4; i++)
            {
                float y = -i * 96f;
                RectTransform row = CreateEmpty(sidebar, "Cat_" + cats[i]);
                SetAnchored(row, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(0f, y), new Vector2(0f, 86f));

                Image bar = CreateImage(row, "ActiveBar", AccentOrange, false);
                SetAnchored(bar.rectTransform, new Vector2(0f, 0.15f), new Vector2(0f, 0.85f), new Vector2(0f, 0.5f),
                    new Vector2(0f, 0f), new Vector2(6f, 0f));
                bar.enabled = i == 0;
                ui.CategoryActiveBars[i] = bar;

                Button btn = row.gameObject.AddComponent<Button>();
                Image hit = row.gameObject.AddComponent<Image>();
                hit.color = new Color(1f, 1f, 1f, 0.001f);
                btn.targetGraphic = hit;
                ApplyHover(btn);

                Image icon = CreateImage(row, "Icon", i == 0 ? AccentOrange : TextMuted, false);
                icon.sprite = LoadIcon(icons[i]);
                icon.preserveAspect = true;
                SetAnchored(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                    new Vector2(28f, 0f), new Vector2(42f, 42f));
                ui.CategoryIcons[i] = icon;

                Text label = CreateText(row, "Label", cats[i], 32, i == 0 ? AccentOrange : TextMuted, TextAnchor.MiddleLeft, font);
                SetAnchored(label.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0.5f),
                    new Vector2(90f, 0f), new Vector2(-96f, 0f));
                ui.CategoryLabels[i] = label;
                ui.CategoryButtons[i] = btn;
            }

            // Content area
            RectTransform content = CreateEmpty(card, "Content");
            content.anchorMin = new Vector2(0f, 0f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0f, 1f);
            content.anchoredPosition = new Vector2(436f, -120f);
            content.sizeDelta = new Vector2(-484f, -150f);

            ui.KeyboardPage = BuildKeyboardPage(content, ui, font);
            ui.SoundPage = BuildPlaceholderPage(content, "SoundPage", "Sound", "Coming soon.", font);
            ui.VideoPage = BuildPlaceholderPage(content, "VideoPage", "Video", "Coming soon.", font);
            ui.GamePage = BuildPlaceholderPage(content, "GamePage", "Game", "Coming soon.", font);

            ui.SoundPage.SetActive(false);
            ui.VideoPage.SetActive(false);
            ui.GamePage.SetActive(false);
            return ui;
        }

        private static GameObject BuildKeyboardPage(Transform parent, MiniVanSettingsUi ui, Font font)
        {
            RectTransform page = CreateEmpty(parent, "KeyboardPage");
            StretchFull(page);

            GameObject scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect), typeof(Image));
            scrollGo.transform.SetParent(page, false);
            RectTransform scrollRt = scrollGo.GetComponent<RectTransform>();
            SetAnchored(scrollRt, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 52f), new Vector2(0f, -110f));
            scrollGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.01f);
            ScrollRect scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;

            GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewport.transform.SetParent(scrollGo.transform, false);
            StretchFull(viewport.GetComponent<RectTransform>());
            viewport.GetComponent<Image>().color = Color.white;
            viewport.GetComponent<Mask>().showMaskGraphic = false;
            scroll.viewport = viewport.GetComponent<RectTransform>();

            GameObject content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            RectTransform contentRt = content.GetComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.sizeDelta = Vector2.zero;
            VerticalLayoutGroup vlg = content.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 0f;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = contentRt;
            ui.KeybindListContent = contentRt;

            ui.KeybindRowTemplate = BuildKeybindRowTemplate(page, font);

            ui.KeyboardStatusLabel = CreateText(page, "Status", "", 22, TextMuted, TextAnchor.LowerLeft, font);
            SetAnchored(ui.KeyboardStatusLabel.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f),
                new Vector2(0f, 92f), new Vector2(0f, 30f));

            ui.ResetDefaultsButton = CreateOutlineButton(page, "ResetDefaults", "Reset Defaults", BorderGray, null, font, 70f, 28);
            SetAnchored(ui.ResetDefaultsButton.GetComponent<RectTransform>(),
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(0f, 8f), new Vector2(300f, 70f));

            ui.ApplyButton = CreateOutlineButton(page, "Apply", "Apply", AccentOrange, null, font, 70f, 28);
            SetAnchored(ui.ApplyButton.GetComponent<RectTransform>(),
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(0f, 8f), new Vector2(240f, 70f));

            return page.gameObject;
        }

        private static GameObject BuildKeybindRowTemplate(Transform parent, Font font)
        {
            GameObject row = new GameObject("KeybindRowTemplate", typeof(RectTransform), typeof(LayoutElement), typeof(Image));
            row.transform.SetParent(parent, false);
            LayoutElement le = row.GetComponent<LayoutElement>();
            le.minHeight = 78f;
            le.preferredHeight = 78f;
            // Barely-visible row tint; concept keeps rows on the dark panel with hairline separators.
            Image line = row.GetComponent<Image>();
            line.color = new Color(BorderGray.r, BorderGray.g, BorderGray.b, 0.05f);

            Text action = CreateText(row.transform, "ActionLabel", "Action", 30, TextMuted, TextAnchor.MiddleLeft, font);
            SetAnchored(action.rectTransform, new Vector2(0f, 0f), new Vector2(0.55f, 1f), new Vector2(0f, 0.5f),
                new Vector2(12f, 0f), new Vector2(-12f, 0f));

            GameObject keyBtnGo = new GameObject("KeyButton", typeof(RectTransform), typeof(Image), typeof(Button));
            keyBtnGo.transform.SetParent(row.transform, false);
            RectTransform keyRt = keyBtnGo.GetComponent<RectTransform>();
            SetAnchored(keyRt, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-12f, 0f), new Vector2(250f, 56f));
            Image keyBorder = keyBtnGo.GetComponent<Image>();
            Sprite roundKey = LoadIcon("round_rect");
            if (roundKey != null)
            {
                keyBorder.sprite = roundKey;
                keyBorder.type = Image.Type.Sliced;
            }

            keyBorder.color = BorderGray;
            Button keyBtn = keyBtnGo.GetComponent<Button>();
            keyBtn.targetGraphic = keyBorder;
            ApplyHover(keyBtn);

            RectTransform keyFill = CreateEmpty(keyBtnGo.transform, "Fill");
            StretchFull(keyFill, 2f);
            Image fillImg = keyFill.gameObject.AddComponent<Image>();
            fillImg.color = new Color(0.10f, 0.11f, 0.12f, 1f);
            fillImg.raycastTarget = false;

            Text keyLabel = CreateText(keyBtnGo.transform, "Label", "W", 26, TextWhite, TextAnchor.MiddleCenter, font);
            keyLabel.verticalOverflow = VerticalWrapMode.Overflow;
            StretchFull(keyLabel.rectTransform, 4f);

            row.SetActive(false);
            return row;
        }

        private static GameObject BuildPlaceholderPage(Transform parent, string name, string title, string body, Font font)
        {
            RectTransform page = CreateEmpty(parent, name);
            StretchFull(page);
            Image icon = CreateImage(page, "Icon", TextMuted, false);
            string iconName = name == "SoundPage" ? "icon_sound" : name == "VideoPage" ? "icon_video" : "icon_game";
            icon.sprite = LoadIcon(iconName);
            icon.preserveAspect = true;
            SetAnchored(icon.rectTransform, new Vector2(0.5f, 0.55f), new Vector2(0.5f, 0.55f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(130f, 130f));
            Text t = CreateText(page, "Title", title, 42, TextWhite, TextAnchor.MiddleCenter, font);
            SetAnchored(t.rectTransform, new Vector2(0.5f, 0.55f), new Vector2(0.5f, 0.55f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -125f), new Vector2(500f, 56f));
            Text b = CreateText(page, "Body", body, 28, TextMuted, TextAnchor.MiddleCenter, font);
            SetAnchored(b.rectTransform, new Vector2(0.5f, 0.55f), new Vector2(0.5f, 0.55f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -185f), new Vector2(600f, 40f));
            return page.gameObject;
        }

        private static Button CreateOutlineButton(Transform parent, string name, string label, Color accent, Sprite icon, Font font, float height,
            int fontSize = 17)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            Image border = go.GetComponent<Image>();
            Sprite round = LoadIcon("round_rect");
            if (round != null)
            {
                border.sprite = round;
                border.type = Image.Type.Sliced;
            }

            border.color = accent;
            RectTransform fill = CreateEmpty(go.transform, "Fill");
            StretchFull(fill, 2f);
            Image fillImg = fill.gameObject.AddComponent<Image>();
            if (round != null)
            {
                fillImg.sprite = round;
                fillImg.type = Image.Type.Sliced;
            }

            fillImg.color = new Color(0.10f, 0.11f, 0.12f, 1f);
            fillImg.raycastTarget = false;
            Button btn = go.GetComponent<Button>();
            btn.targetGraphic = fillImg;
            ApplyHover(btn);
            Text text = CreateText(go.transform, "Label", label, fontSize, accent, TextAnchor.MiddleCenter, font);
            text.verticalOverflow = VerticalWrapMode.Overflow;
            StretchFull(text.rectTransform, 6f);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(160f, height);
            return btn;
        }

        private static Button CreateIconButton(Transform parent, string name, Sprite icon, Color color, float size)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            Image img = go.GetComponent<Image>();
            img.sprite = icon;
            img.color = color;
            img.preserveAspect = true;
            Button btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            ApplyHover(btn);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(size, size);
            return btn;
        }

        private static void ApplyHover(Button button)
        {
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.4f, 1.4f, 1.4f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.selectedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
        }

        private static Text CreateText(Transform parent, string name, string value, int size, Color color, TextAnchor align, Font font)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            Text text = go.GetComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.color = color;
            text.alignment = align;
            text.text = value;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private static Image CreateImage(Transform parent, string name, Color color, bool raycast)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            Image img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = raycast;
            return img;
        }

        private static RectTransform CreateEmpty(Transform parent, string name)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        private static void SetAnchored(RectTransform rt, Vector2 min, Vector2 max, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        private static void StretchFull(RectTransform rt, float inset = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
        }

        private static Font GetFont()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return font != null ? font : Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        private static Sprite LoadIcon(string name) => Resources.Load<Sprite>("UI/MainMenu/" + name);
    }
}
