using UnityEngine;
using UnityEngine.UI;

namespace MiniVanGame
{
    /// <summary>
    /// Builds the editable MiniVanHUD prefab hierarchy (multiple canvases + roster slots).
    /// </summary>
    public static class MiniVanHudUiBuilder
    {
        public static MiniVanHudUi Build()
        {
            // Root Canvas is required so Prefab Mode / Scene view can preview UI.
            GameObject root = new GameObject("MiniVanHUD", typeof(RectTransform));
            Canvas rootCanvas = root.AddComponent<Canvas>();
            rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            rootCanvas.sortingOrder = 20;
            CanvasScaler scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<GraphicRaycaster>();

            MiniVanHudUi ui = root.AddComponent<MiniVanHudUi>();

            // Child canvases keep independent sort order; they stretch to the root.
            ui.RosterCanvas = CreateChildCanvas(root.transform, "RosterCanvas", 21);
            ui.PlayerStatusCanvas = CreateChildCanvas(root.transform, "PlayerStatusCanvas", 22);
            ui.VehicleCanvas = CreateChildCanvas(root.transform, "VehicleCanvas", 23);
            ui.HotbarCanvas = CreateChildCanvas(root.transform, "HotbarCanvas", 24);
            ui.PingCanvas = CreateChildCanvas(root.transform, "PingCanvas", 25);
            ui.EnemyCombatCanvas = CreateChildCanvas(root.transform, "EnemyCombatCanvas", 26);

            BuildRoster(ui);
            BuildPlayerStatus(ui);
            BuildVehicle(ui);
            BuildEnemyCombat(ui);
            BuildHotbar(ui);
            BuildPing(ui);

            return ui;
        }

        private static Canvas CreateChildCanvas(Transform parent, string name, int sortingOrder)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            StretchFull(rt);

            Canvas canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = sortingOrder;
            // No extra CanvasScaler on children — root scaler drives reference resolution.
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        private static void BuildRoster(MiniVanHudUi ui)
        {
            // Right side of screen; rows grow downward from mid-right.
            // Wide enough for long display names on a single line.
            RectTransform panel = CreatePanel(ui.RosterCanvas.transform, "RosterPanel",
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-24f, 40f), new Vector2(520f, 420f));

            VerticalLayoutGroup layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperRight;
            layout.spacing = 28f;
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            ui.RosterSlots = new MiniVanHudRosterSlot[4];
            for (int i = 0; i < 4; i++)
            {
                ui.RosterSlots[i] = CreateRosterSlot(panel, i);
            }
        }

        private static MiniVanHudRosterSlot CreateRosterSlot(Transform parent, int index)
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
            {
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            GameObject row = new GameObject("RosterSlot_" + index, typeof(RectTransform));
            row.transform.SetParent(parent, false);
            LayoutElement le = row.AddComponent<LayoutElement>();
            le.minHeight = 88f;
            le.preferredHeight = 88f;

            HorizontalLayoutGroup rowLayout = row.AddComponent<HorizontalLayoutGroup>();
            // Text column left of icon; pack toward the right edge of the screen.
            rowLayout.childAlignment = TextAnchor.MiddleRight;
            rowLayout.spacing = 12f;
            rowLayout.padding = new RectOffset(0, 0, 0, 0);
            rowLayout.childControlWidth = false;
            rowLayout.childControlHeight = false;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = false;
            rowLayout.reverseArrangement = false;

            // [Name+Count column][Avatar]
            GameObject textCol = new GameObject("TextColumn", typeof(RectTransform));
            textCol.transform.SetParent(row.transform, false);
            LayoutElement textColLe = textCol.AddComponent<LayoutElement>();
            textColLe.minWidth = 320f;
            textColLe.preferredWidth = 420f;
            textColLe.flexibleWidth = 1f;
            textColLe.preferredHeight = 72f;
            VerticalLayoutGroup textLayout = textCol.AddComponent<VerticalLayoutGroup>();
            textLayout.childAlignment = TextAnchor.MiddleRight;
            textLayout.spacing = 2f;
            textLayout.padding = new RectOffset(0, 0, 0, 0);
            textLayout.childControlWidth = true;
            textLayout.childControlHeight = false;
            textLayout.childForceExpandWidth = true;
            textLayout.childForceExpandHeight = false;

            GameObject nameGo = new GameObject("Name", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            nameGo.transform.SetParent(textCol.transform, false);
            LayoutElement nameLe = nameGo.AddComponent<LayoutElement>();
            nameLe.minHeight = 28f;
            nameLe.preferredHeight = 32f;
            Text name = nameGo.GetComponent<Text>();
            name.font = font;
            name.fontSize = 22;
            name.fontStyle = FontStyle.Bold;
            name.alignment = TextAnchor.MiddleRight;
            name.color = Color.white;
            name.text = "Player " + (index + 1);
            name.horizontalOverflow = HorizontalWrapMode.Overflow;
            name.verticalOverflow = VerticalWrapMode.Truncate;
            name.raycastTarget = false;
            Outline nameOutline = nameGo.AddComponent<Outline>();
            nameOutline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            nameOutline.effectDistance = new Vector2(1.2f, -1.2f);

            GameObject countGo = new GameObject("AmberCount", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            countGo.transform.SetParent(textCol.transform, false);
            LayoutElement countLe = countGo.AddComponent<LayoutElement>();
            countLe.minHeight = 22f;
            countLe.preferredHeight = 26f;
            Text count = countGo.GetComponent<Text>();
            count.font = font;
            count.fontSize = 20;
            count.fontStyle = FontStyle.Bold;
            count.alignment = TextAnchor.MiddleRight;
            count.color = new Color(1f, 0.72f, 0.22f, 1f);
            count.text = "×0";
            count.raycastTarget = false;
            Outline countOutline = countGo.AddComponent<Outline>();
            countOutline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            countOutline.effectDistance = new Vector2(1.1f, -1.1f);

            GameObject iconGo = new GameObject("Avatar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            iconGo.transform.SetParent(row.transform, false);
            RectTransform iconRt = iconGo.GetComponent<RectTransform>();
            iconRt.sizeDelta = new Vector2(64f, 64f);
            LayoutElement iconLe = iconGo.AddComponent<LayoutElement>();
            iconLe.minWidth = 64f;
            iconLe.minHeight = 64f;
            iconLe.preferredWidth = 64f;
            iconLe.preferredHeight = 64f;
            Image icon = iconGo.GetComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            Sprite preview = MiniVanAvatarCatalog.GetIcon(index, MiniVanAvatarLifeIcon.Alive);
            if (preview != null)
            {
                icon.sprite = preview;
            }

            MiniVanHudRosterSlot slot = row.AddComponent<MiniVanHudRosterSlot>();
            slot.Root = row;
            slot.AvatarImage = icon;
            slot.NameLabel = name;
            slot.AmberCountLabel = count;
            // Keep all slots active in the prefab so layout is editable; runtime hides unused ones.
            row.SetActive(true);
            return slot;
        }

        private static void BuildPlayerStatus(MiniVanHudUi ui)
        {
            RectTransform panel = CreatePanel(ui.PlayerStatusCanvas.transform, "PlayerStatusPanel",
                new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(24f, 110f), new Vector2(260f, 56f));

            ui.HealthFill = CreateBar(panel, "HealthBar", new Vector2(0f, 14f), new Vector2(250f, 22f),
                new Color(0.12f, 0.8f, 0.22f, 1f));

            RectTransform oxygenRoot = CreatePanel(panel, "OxygenRoot",
                new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(0f, 40f), new Vector2(250f, 18f));
            ui.OxygenRoot = oxygenRoot.gameObject;
            ui.OxygenFill = CreateBar(oxygenRoot, "OxygenBar", Vector2.zero, new Vector2(250f, 16f),
                new Color(0.2f, 0.65f, 1f, 1f));
            ui.OxygenRoot.SetActive(false);
        }

        private static void BuildVehicle(MiniVanHudUi ui)
        {
            RectTransform panel = CreatePanel(ui.VehicleCanvas.transform, "VehiclePanel",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -28f), new Vector2(420f, 48f));
            ui.VehicleRoot = panel.gameObject;

            ui.VehicleHealthFill = CreateBar(panel, "VehicleHealth", Vector2.zero, new Vector2(360f, 22f),
                new Color(0.2f, 0.85f, 0.3f, 1f));

            Sprite body = Resources.Load<Sprite>("UI/minivan-hud-body");
            Sprite wheel = Resources.Load<Sprite>("UI/minivan-hud-wheel");
            GameObject bodyGo = new GameObject("BodyIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            bodyGo.transform.SetParent(panel, false);
            RectTransform bodyRt = bodyGo.GetComponent<RectTransform>();
            bodyRt.anchorMin = new Vector2(0f, 0.5f);
            bodyRt.anchorMax = new Vector2(0f, 0.5f);
            bodyRt.pivot = new Vector2(0.5f, 0.5f);
            bodyRt.anchoredPosition = new Vector2(-28f, 0f);
            bodyRt.sizeDelta = new Vector2(58f, 44f);
            ui.VehicleBodyIcon = bodyGo.GetComponent<Image>();
            ui.VehicleBodyIcon.preserveAspect = true;
            ui.VehicleBodyIcon.raycastTarget = false;
            if (body != null)
            {
                ui.VehicleBodyIcon.sprite = body;
            }

            // Side view: [0]=rear axle, [1]=front axle (matches legacy OnGUI icon).
            ui.VehicleWheelIcons = new Image[4];
            ui.VehicleWheelIcons[0] = CreateWheelIcon(bodyRt, "WheelRear", wheel, new Vector2(-10.5f, -7.5f));
            ui.VehicleWheelIcons[1] = CreateWheelIcon(bodyRt, "WheelFront", wheel, new Vector2(8.5f, -7.5f));
        }

        /// <summary>
        /// Enemy target bar under the vehicle strip. Public so the prefab menu can inject into an existing HUD.
        /// </summary>
        public static void BuildEnemyCombat(MiniVanHudUi ui)
        {
            if (ui == null || ui.EnemyCombatCanvas == null)
            {
                return;
            }

            RectTransform panel = CreatePanel(ui.EnemyCombatCanvas.transform, "EnemyCombatPanel",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -78f), new Vector2(320f, 52f));
            ui.EnemyCombatRoot = panel.gameObject;

            Image panelBg = panel.gameObject.AddComponent<Image>();
            panelBg.color = new Color(0f, 0f, 0f, 0.45f);
            panelBg.raycastTarget = false;

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
            {
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            GameObject nameGo = new GameObject("EnemyName", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            nameGo.transform.SetParent(panel, false);
            RectTransform nameRt = nameGo.GetComponent<RectTransform>();
            nameRt.anchorMin = new Vector2(0f, 1f);
            nameRt.anchorMax = new Vector2(1f, 1f);
            nameRt.pivot = new Vector2(0.5f, 1f);
            nameRt.anchoredPosition = new Vector2(0f, -4f);
            nameRt.sizeDelta = new Vector2(-16f, 22f);
            Text name = nameGo.GetComponent<Text>();
            name.font = font;
            name.fontSize = 16;
            name.fontStyle = FontStyle.Bold;
            name.alignment = TextAnchor.MiddleCenter;
            name.color = Color.white;
            name.text = "Zombie";
            name.raycastTarget = false;
            ui.EnemyCombatNameLabel = name;

            ui.EnemyCombatHealthFill = CreateBar(
                panel,
                "EnemyHealthBar",
                new Vector2(0f, 8f),
                new Vector2(288f, 18f),
                new Color(0.2f, 0.85f, 0.3f, 1f));
            if (ui.EnemyCombatHealthFill != null && ui.EnemyCombatHealthFill.transform.parent != null)
            {
                Transform barParent = ui.EnemyCombatHealthFill.transform.parent;
                ui.EnemyCombatHealthBackground = barParent.GetComponent<Image>();
                RectTransform barRt = barParent.GetComponent<RectTransform>();
                if (barRt != null)
                {
                    barRt.anchorMin = new Vector2(0.5f, 0f);
                    barRt.anchorMax = new Vector2(0.5f, 0f);
                    barRt.pivot = new Vector2(0.5f, 0f);
                    barRt.anchoredPosition = new Vector2(0f, 8f);
                    barRt.sizeDelta = new Vector2(288f, 18f);
                }
            }

            ui.EnemyCombatRoot.SetActive(false);
        }

        private static Image CreateWheelIcon(Transform body, string name, Sprite sprite, Vector2 anchoredPosition)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(body, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPosition;
            rt.sizeDelta = new Vector2(14f, 14f);
            Image image = go.GetComponent<Image>();
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.sprite = sprite;
            image.color = Color.white;
            return image;
        }

        private static void BuildHotbar(MiniVanHudUi ui)
        {
            RectTransform panel = CreatePanel(ui.HotbarCanvas.transform, "HotbarPanel",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 42f), new Vector2(280f, 70f));

            HorizontalLayoutGroup layout = panel.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 8f;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            ui.HotbarSlotBackgrounds = new Image[4];
            ui.HotbarSlotLabels = new Text[4];
            ui.HotbarWinchBars = new Image[4];

            for (int i = 0; i < 4; i++)
            {
                GameObject slot = new GameObject("Slot_" + (i + 1), typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                slot.transform.SetParent(panel, false);
                RectTransform slotRt = slot.GetComponent<RectTransform>();
                slotRt.sizeDelta = new Vector2(58f, 58f);
                LayoutElement le = slot.AddComponent<LayoutElement>();
                le.minWidth = 58f;
                le.minHeight = 58f;
                Image bg = slot.GetComponent<Image>();
                bg.color = new Color(0f, 0f, 0f, 0.54f);
                ui.HotbarSlotBackgrounds[i] = bg;

                GameObject inner = new GameObject("Inner", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                inner.transform.SetParent(slot.transform, false);
                RectTransform innerRt = inner.GetComponent<RectTransform>();
                StretchFull(innerRt, 3f);
                inner.GetComponent<Image>().color = new Color(0.12f, 0.13f, 0.14f, 0.86f);
                inner.GetComponent<Image>().raycastTarget = false;

                GameObject labelGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                labelGo.transform.SetParent(slot.transform, false);
                StretchFull(labelGo.GetComponent<RectTransform>());
                Text label = labelGo.GetComponent<Text>();
                label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (label.font == null)
                {
                    label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                }

                label.fontSize = 13;
                label.fontStyle = FontStyle.Bold;
                label.alignment = TextAnchor.MiddleCenter;
                label.color = Color.white;
                label.text = (i + 1).ToString();
                label.raycastTarget = false;
                ui.HotbarSlotLabels[i] = label;

                GameObject winch = new GameObject("WinchBar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                winch.transform.SetParent(slot.transform, false);
                RectTransform winchRt = winch.GetComponent<RectTransform>();
                winchRt.anchorMin = new Vector2(0f, 0f);
                winchRt.anchorMax = new Vector2(1f, 0f);
                winchRt.pivot = new Vector2(0.5f, 0f);
                winchRt.anchoredPosition = new Vector2(0f, 6f);
                winchRt.sizeDelta = new Vector2(-14f, 4f);
                Image winchImg = winch.GetComponent<Image>();
                winchImg.color = new Color(0.16f, 0.84f, 0.22f, 1f);
                winchImg.type = Image.Type.Filled;
                winchImg.fillMethod = Image.FillMethod.Horizontal;
                winchImg.fillAmount = 1f;
                winchImg.raycastTarget = false;
                winch.SetActive(false);
                ui.HotbarWinchBars[i] = winchImg;
            }
        }

        private static void BuildPing(MiniVanHudUi ui)
        {
            RectTransform panel = CreatePanel(ui.PingCanvas.transform, "PingPanel",
                new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-16f, -16f), new Vector2(170f, 34f));
            Image bg = panel.gameObject.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.55f);

            GameObject labelGo = new GameObject("PingLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            labelGo.transform.SetParent(panel, false);
            StretchFull(labelGo.GetComponent<RectTransform>(), 4f);
            Text label = labelGo.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (label.font == null)
            {
                label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            label.fontSize = 14;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.text = "Ping: 0 ms  OK";
            label.raycastTarget = false;
            ui.PingLabel = label;
        }

        private static Image CreateBar(Transform parent, string name, Vector2 anchoredPos, Vector2 size, Color fillColor)
        {
            GameObject backGo = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            backGo.transform.SetParent(parent, false);
            RectTransform backRt = backGo.GetComponent<RectTransform>();
            backRt.anchorMin = new Vector2(0f, 0f);
            backRt.anchorMax = new Vector2(0f, 0f);
            backRt.pivot = new Vector2(0f, 0f);
            backRt.anchoredPosition = anchoredPos;
            backRt.sizeDelta = size;
            Image back = backGo.GetComponent<Image>();
            back.color = new Color(0.08f, 0.08f, 0.08f, 0.75f);
            back.raycastTarget = false;

            GameObject fillGo = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            fillGo.transform.SetParent(backGo.transform, false);
            StretchFull(fillGo.GetComponent<RectTransform>(), 2f);
            Image fill = fillGo.GetComponent<Image>();
            fill.color = fillColor;
            // Filled images need a sprite — without one fillAmount is ignored (bar stays full).
            Texture2D white = Texture2D.whiteTexture;
            fill.sprite = Sprite.Create(
                white,
                new Rect(0f, 0f, white.width, white.height),
                new Vector2(0.5f, 0.5f),
                100f);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 1f;
            fill.raycastTarget = false;
            return fill;
        }

        private static RectTransform CreatePanel(
            Transform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 anchoredPosition,
            Vector2 size)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = anchorMin;
            if (Mathf.Approximately(anchorMin.x, 0.5f))
            {
                rt.pivot = new Vector2(0.5f, rt.pivot.y);
            }

            if (Mathf.Approximately(anchorMin.y, 1f))
            {
                rt.pivot = new Vector2(rt.pivot.x, 1f);
            }

            if (Mathf.Approximately(anchorMin.x, 1f))
            {
                rt.pivot = new Vector2(1f, rt.pivot.y);
            }

            rt.anchoredPosition = anchoredPosition;
            rt.sizeDelta = size;
            return rt;
        }

        private static void StretchFull(RectTransform rt, float inset = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
        }
    }
}
