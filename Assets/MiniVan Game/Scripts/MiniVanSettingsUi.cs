using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MiniVanGame
{
    /// <summary>
    /// Shared Settings overlay (main menu + in-game pause). Prefab: Resources/MiniVanSettings/MiniVanSettings
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MiniVanSettingsUi : MonoBehaviour
    {
        public const string ResourcesPath = "MiniVanSettings/MiniVanSettings";

        public enum Category
        {
            Keyboard = 0,
            Sound = 1,
            Video = 2,
            Game = 3
        }

        public static MiniVanSettingsUi Instance { get; private set; }
        public static bool IsOpen => Instance != null && Instance.gameObject.activeSelf;

        [Header("Chrome")]
        public GameObject Root;
        public Button BackButton;
        public Text TitleLabel;

        [Header("Sidebar")]
        public Button[] CategoryButtons = new Button[4];
        public Image[] CategoryIcons = new Image[4];
        public Text[] CategoryLabels = new Text[4];
        public Image[] CategoryActiveBars = new Image[4];

        [Header("Pages")]
        public GameObject KeyboardPage;
        public GameObject SoundPage;
        public GameObject VideoPage;
        public GameObject GamePage;

        [Header("Keyboard")]
        public Transform KeybindListContent;
        public GameObject KeybindRowTemplate;
        public Button ResetDefaultsButton;
        public Button ApplyButton;
        public Text KeyboardStatusLabel;

        private Category current = Category.Keyboard;
        private int rebindIndex = -1;
        private Action onClosed;
        private Text[] keybindValueLabels;
        private Button[] keybindButtons;
        private bool wired;

        private static readonly Color AccentOrange = new Color(0.90f, 0.49f, 0.13f, 1f);
        private static readonly Color TextMuted = new Color(0.72f, 0.76f, 0.80f, 1f);
        private static readonly Color TextWhite = new Color(0.95f, 0.96f, 0.97f, 1f);

        public static MiniVanSettingsUi Show(Action onClosed = null)
        {
            MiniVanSettingsUi ui = EnsureInstance();
            ui.onClosed = onClosed;
            ui.OpenInternal();
            return ui;
        }

        public static void HideIfOpen()
        {
            if (Instance != null && Instance.gameObject.activeSelf)
            {
                Instance.CloseInternal(apply: false);
            }
        }

        public static bool TryHandleEscape()
        {
            if (!IsOpen)
            {
                return false;
            }

            if (Instance.rebindIndex >= 0)
            {
                Instance.rebindIndex = -1;
                Instance.RefreshKeybindLabels();
                Instance.SetKeyboardStatus(string.Empty);
                return true;
            }

            Instance.CloseInternal(apply: false);
            return true;
        }

        private static MiniVanSettingsUi EnsureInstance()
        {
            if (Instance != null)
            {
                return Instance;
            }

            MiniVanSettingsUi existing = FindFirstObjectByType<MiniVanSettingsUi>(FindObjectsInactive.Include);
            if (existing != null)
            {
                Instance = existing;
                return existing;
            }

            GameObject prefab = Resources.Load<GameObject>(ResourcesPath);
            if (prefab != null)
            {
                GameObject go = Instantiate(prefab);
                go.name = "MiniVanSettings";
                Instance = go.GetComponent<MiniVanSettingsUi>();
                return Instance;
            }

            Instance = MiniVanSettingsUiBuilder.Build();
            return Instance;
        }

        private void Awake()
        {
            Instance = this;
            if (Root == null)
            {
                Root = gameObject;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            if (!gameObject.activeSelf || current != Category.Keyboard || rebindIndex < 0)
            {
                return;
            }

            // Capture any key except Escape (handled via TryHandleEscape / pause menu).
            foreach (KeyCode key in Enum.GetValues(typeof(KeyCode)))
            {
                if (key == KeyCode.None || key == KeyCode.Escape || key == KeyCode.Mouse0 ||
                    key == KeyCode.Mouse1 || key == KeyCode.Mouse2)
                {
                    continue;
                }

                if (!Input.GetKeyDown(key))
                {
                    continue;
                }

                MiniVanKeyAction[] actions = MiniVanKeyBindings.Actions;
                if (rebindIndex < 0 || rebindIndex >= actions.Length)
                {
                    return;
                }

                MiniVanKeyBindings.SetEdit(actions[rebindIndex], key);
                rebindIndex = -1;
                RefreshKeybindLabels();
                SetKeyboardStatus(string.Empty);
                return;
            }
        }

        private void Wire()
        {
            if (BackButton != null)
            {
                BackButton.onClick.AddListener(() => CloseInternal(apply: false));
            }

            if (CategoryButtons != null)
            {
                for (int i = 0; i < CategoryButtons.Length; i++)
                {
                    int index = i;
                    if (CategoryButtons[i] != null)
                    {
                        CategoryButtons[i].onClick.AddListener(() => SelectCategory((Category)index));
                    }
                }
            }

            if (ResetDefaultsButton != null)
            {
                ResetDefaultsButton.onClick.AddListener(() =>
                {
                    MiniVanKeyBindings.ResetEditToDefaults();
                    rebindIndex = -1;
                    RefreshKeybindLabels();
                    SetKeyboardStatus("Defaults loaded — press Apply to save.");
                });
            }

            if (ApplyButton != null)
            {
                ApplyButton.onClick.AddListener(() =>
                {
                    MiniVanKeyBindings.ApplyEdit();
                    MiniVanKeyBindings.BeginEdit();
                    rebindIndex = -1;
                    RefreshKeybindLabels();
                    SetKeyboardStatus("Applied.");
                });
            }

            BuildKeybindRows();
        }

        private void OpenInternal()
        {
            EnsureEventSystem();
            EnsureWired();
            gameObject.SetActive(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            MiniVanKeyBindings.BeginEdit();
            rebindIndex = -1;
            SelectCategory(Category.Keyboard);
            RefreshKeybindLabels();
            SetKeyboardStatus(string.Empty);
        }

        private void EnsureWired()
        {
            if (wired)
            {
                return;
            }

            wired = true;
            Wire();
        }

        private static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null)
            {
                return;
            }

            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        private void CloseInternal(bool apply)
        {
            rebindIndex = -1;
            if (apply)
            {
                MiniVanKeyBindings.ApplyEdit();
            }
            else
            {
                MiniVanKeyBindings.DiscardEdit();
            }

            gameObject.SetActive(false);
            Action callback = onClosed;
            onClosed = null;
            callback?.Invoke();
        }

        private void SelectCategory(Category category)
        {
            if (category != Category.Keyboard && rebindIndex >= 0)
            {
                rebindIndex = -1;
            }

            current = category;
            if (KeyboardPage != null) KeyboardPage.SetActive(category == Category.Keyboard);
            if (SoundPage != null) SoundPage.SetActive(category == Category.Sound);
            if (VideoPage != null) VideoPage.SetActive(category == Category.Video);
            if (GamePage != null) GamePage.SetActive(category == Category.Game);

            for (int i = 0; i < 4; i++)
            {
                bool active = i == (int)category;
                if (CategoryActiveBars != null && i < CategoryActiveBars.Length && CategoryActiveBars[i] != null)
                {
                    CategoryActiveBars[i].enabled = active;
                }

                if (CategoryIcons != null && i < CategoryIcons.Length && CategoryIcons[i] != null)
                {
                    CategoryIcons[i].color = active ? AccentOrange : TextMuted;
                }

                if (CategoryLabels != null && i < CategoryLabels.Length && CategoryLabels[i] != null)
                {
                    CategoryLabels[i].color = active ? AccentOrange : TextMuted;
                }
            }
        }

        private void BuildKeybindRows()
        {
            if (KeybindListContent == null || KeybindRowTemplate == null)
            {
                return;
            }

            MiniVanKeyAction[] actions = MiniVanKeyBindings.Actions;
            keybindValueLabels = new Text[actions.Length];
            keybindButtons = new Button[actions.Length];

            for (int i = KeybindListContent.childCount - 1; i >= 0; i--)
            {
                Destroy(KeybindListContent.GetChild(i).gameObject);
            }

            for (int i = 0; i < actions.Length; i++)
            {
                GameObject row = Instantiate(KeybindRowTemplate, KeybindListContent);
                row.SetActive(true);
                row.name = "Keybind_" + actions[i];

                Text actionLabel = row.transform.Find("ActionLabel")?.GetComponent<Text>();
                if (actionLabel != null)
                {
                    actionLabel.text = MiniVanKeyBindings.GetLabel(actions[i]);
                }

                Button keyButton = row.transform.Find("KeyButton")?.GetComponent<Button>();
                Text valueLabel = row.transform.Find("KeyButton/Label")?.GetComponent<Text>();
                keybindButtons[i] = keyButton;
                keybindValueLabels[i] = valueLabel;

                int index = i;
                if (keyButton != null)
                {
                    keyButton.onClick.AddListener(() =>
                    {
                        rebindIndex = rebindIndex == index ? -1 : index;
                        RefreshKeybindLabels();
                        SetKeyboardStatus(rebindIndex >= 0 ? "Press a key... (Esc - cancel)" : string.Empty);
                    });
                }
            }

            KeybindRowTemplate.SetActive(false);
        }

        private void RefreshKeybindLabels()
        {
            MiniVanKeyAction[] actions = MiniVanKeyBindings.Actions;
            if (keybindValueLabels == null)
            {
                return;
            }

            for (int i = 0; i < keybindValueLabels.Length && i < actions.Length; i++)
            {
                if (keybindValueLabels[i] == null)
                {
                    continue;
                }

                keybindValueLabels[i].text = rebindIndex == i
                    ? "< press key >"
                    : MiniVanKeyBindings.KeyName(MiniVanKeyBindings.GetEdit(actions[i]));
            }
        }

        private void SetKeyboardStatus(string message)
        {
            if (KeyboardStatusLabel != null)
            {
                KeyboardStatusLabel.text = message ?? string.Empty;
            }
        }
    }
}
