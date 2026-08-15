using UnityEngine;
using UnityEngine.UI;

namespace MiniVanGame
{
    /// <summary>
    /// Builds the editable MiniVan Main Menu prefab (Main / Create Room / Join Room).
    /// </summary>
    public static class MiniVanMainMenuUiBuilder
    {
        private static readonly Color BgDark = new Color(0.10f, 0.11f, 0.12f, 1f);
        private static readonly Color PanelBg = new Color(0.14f, 0.15f, 0.16f, 0.96f);
        private static readonly Color BorderGray = new Color(0.55f, 0.60f, 0.66f, 0.65f);
        private static readonly Color TextWhite = new Color(0.95f, 0.96f, 0.97f, 1f);
        private static readonly Color TextMuted = new Color(0.72f, 0.76f, 0.80f, 1f);
        private static readonly Color AccentOrange = new Color(0.90f, 0.49f, 0.13f, 1f);
        private static readonly Color AccentBlue = new Color(0.30f, 0.64f, 1f, 1f);
        private static readonly Color HeaderBar = new Color(0.18f, 0.22f, 0.26f, 1f);

        public static MiniVanMainMenuUi Build()
        {
            Font font = GetFont();

            GameObject root = new GameObject("MiniVanMainMenu", typeof(RectTransform));
            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;
            CanvasScaler scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<GraphicRaycaster>();

            MiniVanMainMenuUi ui = root.AddComponent<MiniVanMainMenuUi>();

            // Fullscreen dim background
            Image bg = CreateImage(root.transform, "Background", BgDark, true);
            StretchFull(bg.rectTransform);

            ui.MainPanel = BuildMainPanel(root.transform, ui, font);
            ui.CreatePanel = BuildCreatePanel(root.transform, ui, font);
            ui.JoinPanel = BuildJoinPanel(root.transform, ui, font);

            ui.ShowPanel(MiniVanMainMenuUi.Panel.Main);
            return ui;
        }

        private static GameObject BuildMainPanel(Transform parent, MiniVanMainMenuUi ui, Font font)
        {
            // Concept: no card — centered stack on dark background.
            RectTransform stack = CreateEmpty(parent, "MainPanel");
            StretchFull(stack);

            // Concept 1024x576 → reference 1920x1080 (×1.875).
            const float fieldWidth = 1140f;
            const float btnHeight = 160f;
            const float quitHeight = 84f;

            Text title = CreateText(stack, "Title", "MiniVan Game", 72, TextWhite, TextAnchor.MiddleCenter, font);
            SetAnchored(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 287f), new Vector2(1000f, 100f));

            Text nameLabel = CreateText(stack, "NameLabel", "Your name", 28, TextWhite, TextAnchor.MiddleLeft, font);
            SetAnchored(nameLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 176f), new Vector2(fieldWidth, 40f));

            ui.MainNameField = CreateInputField(stack, "MainNameField", font, fieldWidth, 84f);
            SetAnchored(ui.MainNameField.GetComponent<RectTransform>(),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 86f), new Vector2(fieldWidth, 84f));

            RectTransform row = CreateEmpty(stack, "ActionRow");
            SetAnchored(row, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -105f), new Vector2(fieldWidth, btnHeight));
            HorizontalLayoutGroup h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 40f;
            h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = true;
            h.childForceExpandHeight = true;

            ui.MainCreateButton = CreateOutlineButton(row, "CreateRoomBtn", "Create Room",
                AccentOrange, LoadIcon("icon_create_room"), font, btnHeight, 42, 56f);
            ui.MainJoinButton = CreateOutlineButton(row, "JoinRoomBtn", "Join Room",
                AccentBlue, LoadIcon("icon_join_room"), font, btnHeight, 42, 56f);

            ui.MainQuitButton = CreateOutlineButton(stack, "QuitBtn", "Quit Game",
                BorderGray, LoadIcon("icon_quit"), font, quitHeight, 34, 40f);
            SetAnchored(ui.MainQuitButton.GetComponent<RectTransform>(),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -300f), new Vector2(430f, quitHeight));

            ui.MainStatusLabel = CreateText(stack, "Status", "", 22, TextMuted, TextAnchor.MiddleCenter, font);
            SetAnchored(ui.MainStatusLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -400f), new Vector2(900f, 36f));

            // Settings gear — screen bottom-right (concept).
            ui.MainSettingsButton = CreateOutlineIconButton(stack, "SettingsBtn", LoadIcon("icon_settings"), AccentOrange, 100f);
            SetAnchored(ui.MainSettingsButton.GetComponent<RectTransform>(),
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-48f, 48f), new Vector2(100f, 100f));

            return stack.gameObject;
        }

        private static GameObject BuildCreatePanel(Transform parent, MiniVanMainMenuUi ui, Font font)
        {
            RectTransform card = CreateCard(parent, "CreatePanel", new Vector2(1530f, 710f));

            // Header
            Button back = CreateIconButton(card, "BackBtn", LoadIcon("icon_back"), TextWhite, 52f);
            SetAnchored(back.GetComponent<RectTransform>(),
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(44f, -30f), new Vector2(52f, 52f));
            ui.CreateBackButton = back;

            Text title = CreateText(card, "Title", "Create Room", 44, TextWhite, TextAnchor.MiddleLeft, font);
            SetAnchored(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(120f, -28f), new Vector2(500f, 56f));

            Image divider = CreateImage(card, "HeaderLine", BorderGray, false);
            SetAnchored(divider.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -100f), new Vector2(-48f, 2f));

            // Left: room name
            RectTransform left = CreateEmpty(card, "LeftColumn");
            left.anchorMin = new Vector2(0f, 0f);
            left.anchorMax = new Vector2(0f, 1f);
            left.pivot = new Vector2(0f, 1f);
            left.anchoredPosition = new Vector2(48f, -160f);
            left.sizeDelta = new Vector2(560f, -210f);

            Text roomLabel = CreateText(left, "RoomNameLabel", "Room name", 30, TextWhite, TextAnchor.UpperLeft, font);
            SetAnchored(roomLabel.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 0f), new Vector2(0f, 42f));

            ui.CreateRoomNameField = CreateInputField(left, "CreateRoomNameField", font, 560f, 80f);
            SetAnchored(ui.CreateRoomNameField.GetComponent<RectTransform>(),
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -56f), new Vector2(0f, 80f));

            // Right: horizontal square map tiles (concept)
            RectTransform right = CreateEmpty(card, "RightColumn");
            right.anchorMin = new Vector2(1f, 0f);
            right.anchorMax = new Vector2(1f, 1f);
            right.pivot = new Vector2(1f, 1f);
            right.anchoredPosition = new Vector2(-48f, -160f);
            right.sizeDelta = new Vector2(800f, -210f);

            Text mapLabel = CreateText(right, "MapLabel", "Choose map", 30, TextWhite, TextAnchor.UpperLeft, font);
            SetAnchored(mapLabel.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 0f), new Vector2(0f, 42f));

            RectTransform mapsRow = CreateEmpty(right, "MapsRow");
            SetAnchored(mapsRow, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -60f), new Vector2(0f, 265f));
            HorizontalLayoutGroup mapsLayout = mapsRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            mapsLayout.spacing = 24f;
            mapsLayout.childAlignment = TextAnchor.MiddleLeft;
            mapsLayout.childControlWidth = false;
            mapsLayout.childControlHeight = false;
            mapsLayout.childForceExpandWidth = false;
            mapsLayout.childForceExpandHeight = false;

            string[] maps = { "New Game", "Test" };
            ui.MapButtons = new Button[maps.Length];
            ui.MapBorders = new Image[maps.Length];
            ui.MapCheckIcons = new Image[maps.Length];
            for (int i = 0; i < maps.Length; i++)
            {
                GameObject mapCardGo = new GameObject("Map_" + maps[i], typeof(RectTransform), typeof(LayoutElement));
                mapCardGo.transform.SetParent(mapsRow, false);
                LayoutElement mapLe = mapCardGo.GetComponent<LayoutElement>();
                mapLe.minWidth = mapLe.preferredWidth = 245f;
                mapLe.minHeight = mapLe.preferredHeight = 265f;
                RectTransform mapCard = mapCardGo.GetComponent<RectTransform>();
                mapCard.sizeDelta = new Vector2(245f, 265f);

                Image border = CreateImage(mapCard, "Border", BorderGray, true);
                StretchFull(border.rectTransform);
                border.color = BorderGray;
                border.raycastTarget = false;

                Image fill = CreateImage(mapCard, "Fill", new Color(0.12f, 0.13f, 0.14f, 1f), true);
                StretchFull(fill.rectTransform, 3f);

                Text label = CreateText(mapCard, "Label", maps[i], 30, TextWhite, TextAnchor.MiddleCenter, font);
                StretchFull(label.rectTransform, 10f);

                Image check = CreateImage(mapCard, "Check", AccentOrange, false);
                check.sprite = LoadIcon("icon_check");
                check.preserveAspect = true;
                check.enabled = i == 0;
                SetAnchored(check.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                    new Vector2(-16f, -16f), new Vector2(42f, 42f));

                Button btn = mapCardGo.AddComponent<Button>();
                btn.targetGraphic = fill;
                MiniVanMenuButtonHoverFx.Attach(btn);

                ui.MapButtons[i] = btn;
                ui.MapBorders[i] = border;
                ui.MapCheckIcons[i] = check;
            }

            ui.SetMapSelected(0);

            ui.CreateConfirmButton = CreateFilledButton(right, "CreateConfirm", "Create Room",
                AccentOrange, LoadIcon("icon_people"), font, 100f, 40, 48f);
            SetAnchored(ui.CreateConfirmButton.GetComponent<RectTransform>(),
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -390f), new Vector2(0f, 100f));

            ui.CreateStatusLabel = CreateText(card, "Status", "", 22, TextMuted, TextAnchor.LowerCenter, font);
            SetAnchored(ui.CreateStatusLabel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 16f), new Vector2(1400f, 32f));

            return card.gameObject;
        }

        private static GameObject BuildJoinPanel(Transform parent, MiniVanMainMenuUi ui, Font font)
        {
            RectTransform card = CreateCard(parent, "JoinPanel", new Vector2(1440f, 915f));

            Button back = CreateIconButton(card, "BackBtn", LoadIcon("icon_back"), AccentOrange, 52f);
            SetAnchored(back.GetComponent<RectTransform>(),
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(44f, -30f), new Vector2(52f, 52f));
            ui.JoinBackButton = back;

            Text title = CreateText(card, "Title", "Join Room", 44, TextWhite, TextAnchor.MiddleLeft, font);
            SetAnchored(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(120f, -28f), new Vector2(400f, 56f));

            ui.JoinRefreshButton = CreateOutlineButton(card, "RefreshBtn", "Refresh",
                AccentOrange, LoadIcon("icon_refresh"), font, 72f, 30, 36f);
            SetAnchored(ui.JoinRefreshButton.GetComponent<RectTransform>(),
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-44f, -28f), new Vector2(250f, 72f));
            ui.JoinRefreshLabel = ui.JoinRefreshButton.GetComponentInChildren<Text>();

            Image divider = CreateImage(card, "HeaderLine", BorderGray, false);
            SetAnchored(divider.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -128f), new Vector2(-48f, 2f));

            // Available rooms header bar
            RectTransform header = CreateEmpty(card, "RoomsHeader");
            SetAnchored(header, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -160f), new Vector2(-96f, 60f));
            Image headerBg = header.gameObject.AddComponent<Image>();
            headerBg.color = HeaderBar;
            Text headerText = CreateText(header, "Label", "Available rooms", 28, new Color(0.65f, 0.78f, 0.88f, 1f),
                TextAnchor.MiddleLeft, font);
            SetAnchored(headerText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0.5f),
                new Vector2(20f, 0f), new Vector2(-20f, 0f));

            // List area
            RectTransform listFrame = CreateEmpty(card, "RoomListFrame");
            SetAnchored(listFrame, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -85f), new Vector2(-96f, -270f));
            Image listBorder = listFrame.gameObject.AddComponent<Image>();
            listBorder.color = BorderGray;

            RectTransform listInner = CreateEmpty(listFrame, "Inner");
            StretchFull(listInner, 2f);
            Image listBg = listInner.gameObject.AddComponent<Image>();
            listBg.color = new Color(0.11f, 0.12f, 0.13f, 1f);

            GameObject scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect), typeof(Image));
            scrollGo.transform.SetParent(listInner, false);
            RectTransform scrollRt = scrollGo.GetComponent<RectTransform>();
            StretchFull(scrollRt);
            Image scrollImg = scrollGo.GetComponent<Image>();
            scrollImg.color = new Color(0f, 0f, 0f, 0.01f);
            scrollImg.raycastTarget = true;
            ScrollRect scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            ui.RoomScroll = scroll;

            GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewport.transform.SetParent(scrollGo.transform, false);
            RectTransform vpRt = viewport.GetComponent<RectTransform>();
            StretchFull(vpRt);
            viewport.GetComponent<Image>().color = Color.white;
            viewport.GetComponent<Mask>().showMaskGraphic = false;
            scroll.viewport = vpRt;

            GameObject content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            RectTransform contentRt = content.GetComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.anchoredPosition = Vector2.zero;
            contentRt.sizeDelta = new Vector2(0f, 0f);
            VerticalLayoutGroup vlg = content.GetComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(10, 10, 10, 10);
            vlg.spacing = 8f;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = contentRt;
            ui.RoomListContent = contentRt;

            // Empty state
            GameObject empty = new GameObject("EmptyState", typeof(RectTransform));
            empty.transform.SetParent(listInner, false);
            RectTransform emptyRt = empty.GetComponent<RectTransform>();
            StretchFull(emptyRt);
            Image emptyIcon = CreateImage(empty.transform, "DoorIcon", new Color(0.55f, 0.70f, 0.82f, 1f), false);
            emptyIcon.sprite = LoadIcon("icon_empty_door");
            emptyIcon.preserveAspect = true;
            SetAnchored(emptyIcon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 55f), new Vector2(130f, 130f));
            Text emptyText = CreateText(empty.transform, "EmptyLabel", "No open rooms found.", 32, TextWhite,
                TextAnchor.MiddleCenter, font);
            SetAnchored(emptyText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -75f), new Vector2(700f, 50f));
            ui.RoomEmptyState = empty;

            // Room row template (inactive)
            GameObject rowTemplate = BuildRoomRowTemplate(card, font);
            rowTemplate.SetActive(false);
            ui.RoomRowTemplate = rowTemplate;

            ui.JoinStatusLabel = CreateText(card, "Status", "", 22, TextMuted, TextAnchor.LowerCenter, font);
            SetAnchored(ui.JoinStatusLabel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 14f), new Vector2(1300f, 32f));

            return card.gameObject;
        }

        private static GameObject BuildRoomRowTemplate(Transform parent, Font font)
        {
            GameObject row = new GameObject("RoomRowTemplate", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            row.transform.SetParent(parent, false);
            RectTransform rt = row.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(1300f, 80f);
            LayoutElement le = row.GetComponent<LayoutElement>();
            le.minHeight = 80f;
            le.preferredHeight = 80f;
            Image bg = row.GetComponent<Image>();
            bg.color = new Color(0.16f, 0.42f, 0.22f, 1f);
            Button btn = row.GetComponent<Button>();
            btn.targetGraphic = bg;

            Text label = CreateText(row.transform, "Label", "Room    0/4", 30, TextWhite, TextAnchor.MiddleLeft, font);
            StretchFull(label.rectTransform, 20f);
            label.alignment = TextAnchor.MiddleLeft;

            MiniVanMenuButtonHoverFx.Attach(btn);
            return row;
        }

        // ---- helpers ----

        private static RectTransform CreateCard(Transform parent, string name, Vector2 size)
        {
            RectTransform card = CreateEmpty(parent, name);
            card.anchorMin = card.anchorMax = card.pivot = new Vector2(0.5f, 0.5f);
            card.sizeDelta = size;
            Image border = card.gameObject.AddComponent<Image>();
            Sprite round = LoadIcon("round_rect");
            if (round != null)
            {
                border.sprite = round;
                border.type = Image.Type.Sliced;
            }

            border.color = BorderGray;
            RectTransform inner = CreateEmpty(card, "Inner");
            StretchFull(inner, 2f);
            Image fill = inner.gameObject.AddComponent<Image>();
            if (round != null)
            {
                fill.sprite = round;
                fill.type = Image.Type.Sliced;
            }

            fill.color = PanelBg;
            // Reparent subsequent children onto card (border), but content goes on Inner visually.
            // Simpler: put fill as sibling behind by sibling order — actually children of card after border image component
            // We'll attach content directly to card; draw fill as first child.
            fill.transform.SetAsFirstSibling();
            // Make fill not block — content on card. Move fill under card as background only.
            return card;
        }

        private static Button CreateOutlineButton(Transform parent, string name, string label, Color accent, Sprite icon, Font font, float height,
            int fontSize = 20, float iconSize = 28f)
        {
            float iconLeft = Mathf.Round(height * 0.25f);
            float labelLeft = iconLeft + iconSize + 16f;

            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            LayoutElement le = go.GetComponent<LayoutElement>();
            le.minHeight = height;
            le.preferredHeight = height;

            Image border = go.GetComponent<Image>();
            Sprite round = LoadIcon("round_rect");
            if (round != null)
            {
                border.sprite = round;
                border.type = Image.Type.Sliced;
            }

            border.color = accent;

            // Dark fill inset — ignore layout so it never participates in sizing.
            RectTransform fillRt = CreateEmpty(go.transform, "Fill");
            StretchFull(fillRt, 2f);
            Image fill = fillRt.gameObject.AddComponent<Image>();
            if (round != null)
            {
                fill.sprite = round;
                fill.type = Image.Type.Sliced;
            }

            fill.color = new Color(0.10f, 0.11f, 0.12f, 1f);
            fill.raycastTarget = false;
            LayoutElement fillLe = fillRt.gameObject.AddComponent<LayoutElement>();
            fillLe.ignoreLayout = true;

            Button btn = go.GetComponent<Button>();
            btn.targetGraphic = fill;

            // No HorizontalLayoutGroup — icons were getting crushed by layout.
            // Icon and label are manually placed; Pos X/Y stay editable.
            if (icon != null)
            {
                Image iconImg = CreateImage(go.transform, "Icon", accent, false);
                iconImg.sprite = icon;
                iconImg.preserveAspect = true;
                iconImg.type = Image.Type.Simple;
                RectTransform iconRt = iconImg.rectTransform;
                SetAnchored(iconRt,
                    new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(iconLeft + iconSize * 0.5f, 0f),
                    new Vector2(iconSize, iconSize));
                iconRt.localScale = Vector3.one;
                LayoutElement iconLe = iconImg.gameObject.AddComponent<LayoutElement>();
                iconLe.ignoreLayout = true;
            }

            Text text = CreateText(go.transform, "Label", label, fontSize, accent, TextAnchor.MiddleLeft, font);
            RectTransform textRt = text.rectTransform;
            textRt.anchorMin = new Vector2(0f, 0f);
            textRt.anchorMax = new Vector2(1f, 1f);
            textRt.pivot = new Vector2(0.5f, 0.5f);
            textRt.offsetMin = new Vector2(labelLeft, 4f);
            textRt.offsetMax = new Vector2(-16f, -4f);
            textRt.localScale = Vector3.one;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            LayoutElement textLe = text.gameObject.AddComponent<LayoutElement>();
            textLe.ignoreLayout = true;

            fillRt.SetAsFirstSibling();
            MiniVanMenuButtonHoverFx.Attach(btn);
            return btn;
        }

        private static Button CreateFilledButton(Transform parent, string name, string label, Color fillColor, Sprite icon, Font font, float height,
            int fontSize = 22, float iconSize = 28f)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            LayoutElement le = go.GetComponent<LayoutElement>();
            le.minHeight = height;
            le.preferredHeight = height;
            Image bg = go.GetComponent<Image>();
            Sprite round = LoadIcon("round_rect");
            if (round != null)
            {
                bg.sprite = round;
                bg.type = Image.Type.Sliced;
            }

            bg.color = fillColor;
            Button btn = go.GetComponent<Button>();
            btn.targetGraphic = bg;

            // Manual placement (no layout group) — icon sits left of the centered label.
            if (icon != null)
            {
                Image iconImg = CreateImage(go.transform, "Icon", TextWhite, false);
                iconImg.sprite = icon;
                iconImg.preserveAspect = true;
                SetAnchored(iconImg.rectTransform,
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(-140f, 0f), new Vector2(iconSize, iconSize));
            }

            Text text = CreateText(go.transform, "Label", label, fontSize, TextWhite, TextAnchor.MiddleCenter, font);
            text.fontStyle = FontStyle.Bold;
            RectTransform textRt = text.rectTransform;
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(40f, 4f);
            textRt.offsetMax = new Vector2(-16f, -4f);
            text.verticalOverflow = VerticalWrapMode.Overflow;
            MiniVanMenuButtonHoverFx.Attach(btn);
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
            img.raycastTarget = true;
            Button btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(size, size);
            MiniVanMenuButtonHoverFx.Attach(btn);
            return btn;
        }

        private static Button CreateOutlineIconButton(Transform parent, string name, Sprite icon, Color accent, float size)
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

            Image iconImg = CreateImage(go.transform, "Icon", accent, false);
            iconImg.sprite = icon;
            iconImg.preserveAspect = true;
            SetAnchored(iconImg.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(size * 0.55f, size * 0.55f));
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(size, size);
            MiniVanMenuButtonHoverFx.Attach(btn);
            return btn;
        }

        private static InputField CreateInputField(Transform parent, string name, Font font, float width, float height)
        {
            // Standard uGUI layout: dark Image on the InputField itself (no Fill child).
            // A Fill child sits above the caret mesh and hides typed text while focused.
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(InputField));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(width, height);
            Image bg = go.GetComponent<Image>();
            // Rounded corners via 9-sliced sprite (Image without sprite = sharp quad).
            Sprite round = LoadIcon("round_rect");
            if (round != null)
            {
                bg.sprite = round;
                bg.type = Image.Type.Sliced;
                bg.pixelsPerUnitMultiplier = 1f;
            }

            bg.color = new Color(0.09f, 0.10f, 0.11f, 1f);

            Outline outline = go.AddComponent<Outline>();
            outline.effectColor = BorderGray;
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            outline.useGraphicAlpha = true;

            MiniVanInputFieldChrome chrome = go.AddComponent<MiniVanInputFieldChrome>();
            chrome.Outline = outline;
            chrome.Normal = BorderGray;
            chrome.Hover = new Color(0.78f, 0.82f, 0.88f, 1f);
            chrome.Selected = AccentOrange;

            // Vertical padding must leave enough height for fontSize (12px inset → 20px box
            // at height 44 culls every glyph with Truncate).
            GameObject textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(go.transform, false);
            RectTransform textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(20f, 10f);
            textRt.offsetMax = new Vector2(-20f, -10f);
            Text text = textGo.GetComponent<Text>();
            text.font = font;
            text.fontSize = 32;
            text.color = TextWhite;
            text.alignment = TextAnchor.MiddleLeft;
            text.supportRichText = false;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            GameObject placeholderGo = new GameObject("Placeholder", typeof(RectTransform), typeof(Text));
            placeholderGo.transform.SetParent(go.transform, false);
            RectTransform placeholderRt = placeholderGo.GetComponent<RectTransform>();
            placeholderRt.anchorMin = Vector2.zero;
            placeholderRt.anchorMax = Vector2.one;
            placeholderRt.offsetMin = new Vector2(20f, 10f);
            placeholderRt.offsetMax = new Vector2(-20f, -10f);
            Text placeholder = placeholderGo.GetComponent<Text>();
            placeholder.font = font;
            placeholder.fontSize = 32;
            placeholder.fontStyle = FontStyle.Italic;
            placeholder.color = new Color(TextMuted.r, TextMuted.g, TextMuted.b, 0.55f);
            placeholder.alignment = TextAnchor.MiddleLeft;
            placeholder.raycastTarget = false;
            placeholder.horizontalOverflow = HorizontalWrapMode.Overflow;
            placeholder.verticalOverflow = VerticalWrapMode.Overflow;
            placeholder.text = "";

            InputField field = go.GetComponent<InputField>();
            field.textComponent = text;
            field.placeholder = placeholder;
            field.lineType = InputField.LineType.SingleLine;
            field.characterLimit = 48;
            field.targetGraphic = bg;
            field.transition = Selectable.Transition.None;
            field.caretBlinkRate = 0.85f;
            field.caretWidth = 2;
            field.customCaretColor = true;
            field.caretColor = Color.white;
            field.selectionColor = new Color(AccentOrange.r, AccentOrange.g, AccentOrange.b, 0.45f);
            return field;
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

        private static Sprite LoadIcon(string name)
        {
            return Resources.Load<Sprite>("UI/MainMenu/" + name);
        }
    }
}
