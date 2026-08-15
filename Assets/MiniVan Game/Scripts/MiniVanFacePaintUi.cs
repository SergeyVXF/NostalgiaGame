using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MiniVanGame
{
    /// <summary>
    /// Editable Face Paint HUD root. Assign references in the prefab; station binds logic at runtime.
    /// Prefab path: Resources/FacePaintUI/FacePaintHUD
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MiniVanFacePaintUi : MonoBehaviour
    {
        [Header("Root")]
        public Canvas Canvas;
        public RectTransform BrushCursor;
        public Text HintText;

        [Header("Tools")]
        public Button BrushButton;
        public Button EraserButton;
        public Button FillButton;
        public Image BrushIcon;
        public Image EraserIcon;
        public Image FillIcon;

        [Header("Colors / Size")]
        public Image[] ColorSwatches;
        public Slider SizeSlider;

        [Header("Actions")]
        public Button UndoButton;
        public Button RedoButton;
        public Button CloseButton;
        public Button ConfirmButton;
        public GameObject RotateLeftButton;
        public GameObject RotateRightButton;

        private MiniVanFacePaintStation station;

        public void Bind(MiniVanFacePaintStation owner)
        {
            station = owner;
            if (station == null)
            {
                return;
            }

            WireClick(BrushButton, station.UiSelectBrush);
            WireClick(EraserButton, station.UiSelectEraser);
            WireClick(FillButton, station.UiSelectFill);
            WireClick(UndoButton, station.UiUndo);
            WireClick(RedoButton, station.UiRedo);
            WireClick(CloseButton, station.UiClose);
            WireClick(ConfirmButton, station.UiConfirm);

            if (SizeSlider != null)
            {
                SizeSlider.onValueChanged.RemoveAllListeners();
                SizeSlider.onValueChanged.AddListener(station.UiSetBrushSize);
                station.UiSetBrushSize(SizeSlider.value);
            }

            if (ColorSwatches != null)
            {
                for (int i = 0; i < ColorSwatches.Length; i++)
                {
                    Image swatch = ColorSwatches[i];
                    if (swatch == null)
                    {
                        continue;
                    }

                    Button button = swatch.GetComponent<Button>();
                    if (button == null)
                    {
                        continue;
                    }

                    Color color = swatch.color;
                    button.onClick.RemoveAllListeners();
                    button.onClick.AddListener(() => station.UiSelectColor(color));
                }
            }

            WireRotateHold(RotateLeftButton, -1f);
            WireRotateHold(RotateRightButton, 1f);
        }

        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }

        public void RefreshToolHighlights(bool brush, bool eraser, bool fill, Color paintColor, bool eraserTool)
        {
            Color idle = new Color(1f, 1f, 1f, 0.85f);
            Color active = new Color(0.25f, 0.55f, 1f, 1f);
            if (BrushIcon != null)
            {
                BrushIcon.color = brush ? active : idle;
            }

            if (EraserIcon != null)
            {
                EraserIcon.color = eraser ? active : idle;
            }

            if (FillIcon != null)
            {
                FillIcon.color = fill ? active : idle;
            }

            if (ColorSwatches == null)
            {
                return;
            }

            for (int i = 0; i < ColorSwatches.Length; i++)
            {
                Image swatch = ColorSwatches[i];
                if (swatch == null)
                {
                    continue;
                }

                Outline outline = swatch.GetComponent<Outline>();
                if (outline == null)
                {
                    continue;
                }

                bool selected = !eraserTool && ColorsClose(swatch.color, paintColor);
                outline.effectColor = selected ? active : new Color(0f, 0f, 0f, 0.5f);
                outline.effectDistance = selected ? new Vector2(3f, 3f) : new Vector2(1f, 1f);
            }
        }

        private void WireRotateHold(GameObject buttonGo, float direction)
        {
            if (buttonGo == null || station == null)
            {
                return;
            }

            EventTrigger trigger = buttonGo.GetComponent<EventTrigger>();
            if (trigger == null)
            {
                trigger = buttonGo.AddComponent<EventTrigger>();
            }

            trigger.triggers.Clear();
            AddHold(trigger, EventTriggerType.PointerDown, () => station.UiSetRotateHold(direction));
            AddHold(trigger, EventTriggerType.PointerUp, () => station.UiClearRotateHold(direction));
            AddHold(trigger, EventTriggerType.PointerExit, () => station.UiClearRotateHold(direction));
        }

        private static void WireClick(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null || action == null)
            {
                return;
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }

        private static void AddHold(EventTrigger trigger, EventTriggerType type, UnityEngine.Events.UnityAction action)
        {
            EventTrigger.Entry entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(_ => action());
            trigger.triggers.Add(entry);
        }

        private static bool ColorsClose(Color a, Color b) =>
            Mathf.Abs(a.r - b.r) < 0.02f && Mathf.Abs(a.g - b.g) < 0.02f && Mathf.Abs(a.b - b.b) < 0.02f;

#if UNITY_EDITOR
        /// <summary>Auto-fill empty references from children by name (used by prefab builder).</summary>
        public void EditorAutoWire()
        {
            if (Canvas == null)
            {
                Canvas = GetComponent<Canvas>();
            }

            BrushCursor = FindRt("BrushCursor");
            HintText = FindComp<Text>("Hint");
            BrushButton = FindComp<Button>("Brush");
            EraserButton = FindComp<Button>("Eraser");
            FillButton = FindComp<Button>("Fill");
            BrushIcon = FindNamedImage("Brush", "Icon");
            EraserIcon = FindNamedImage("Eraser", "Icon");
            FillIcon = FindNamedImage("Fill", "Icon");
            SizeSlider = FindComp<Slider>("Size");
            UndoButton = FindComp<Button>("Undo");
            RedoButton = FindComp<Button>("Redo");
            CloseButton = FindComp<Button>("Close");
            ConfirmButton = FindComp<Button>("Confirm");
            RotateLeftButton = FindGo("RotateLeft");
            RotateRightButton = FindGo("RotateRight");

            List<Image> swatches = new List<Image>(24);
            Transform panel = transform.Find("ToolPanel");
            if (panel != null)
            {
                for (int i = 0; i < panel.childCount; i++)
                {
                    Transform child = panel.GetChild(i);
                    if (child.name.StartsWith("Swatch"))
                    {
                        Image img = child.GetComponent<Image>();
                        if (img != null)
                        {
                            swatches.Add(img);
                        }
                    }
                }
            }

            ColorSwatches = swatches.ToArray();
        }

        private RectTransform FindRt(string name)
        {
            Transform t = transform.Find(name);
            return t != null ? t.GetComponent<RectTransform>() : null;
        }

        private GameObject FindGo(string name)
        {
            Transform t = FindDeep(transform, name);
            return t != null ? t.gameObject : null;
        }

        private T FindComp<T>(string name) where T : Component
        {
            Transform t = FindDeep(transform, name);
            return t != null ? t.GetComponent<T>() : null;
        }

        private Image FindNamedImage(string parentName, string childName)
        {
            Transform parent = FindDeep(transform, parentName);
            if (parent == null)
            {
                return null;
            }

            Transform child = parent.Find(childName);
            return child != null ? child.GetComponent<Image>() : null;
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name)
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindDeep(root.GetChild(i), name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
#endif
    }
}
