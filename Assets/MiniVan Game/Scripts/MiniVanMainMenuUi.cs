using UnityEngine;
using UnityEngine.UI;

namespace MiniVanGame
{
    /// <summary>
    /// Editable Main Menu UI root. Prefab: Resources/MiniVanMainMenu/MiniVanMainMenu
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MiniVanMainMenuUi : MonoBehaviour
    {
        public const string ResourcesPath = "MiniVanMainMenu/MiniVanMainMenu";

        public enum Panel
        {
            Main,
            CreateRoom,
            JoinRoom
        }

        [Header("Panels")]
        public GameObject MainPanel;
        public GameObject CreatePanel;
        public GameObject JoinPanel;

        [Header("Main")]
        public InputField MainNameField;
        public Button MainCreateButton;
        public Button MainJoinButton;
        public Button MainQuitButton;
        public Button MainSettingsButton;
        public Text MainStatusLabel;

        [Header("Create Room")]
        public Button CreateBackButton;
        public InputField CreateRoomNameField;
        public Button[] MapButtons = new Button[2];
        public Image[] MapBorders = new Image[2];
        public Image[] MapCheckIcons = new Image[2];
        public Button CreateConfirmButton;
        public Text CreateStatusLabel;

        [Header("Join Room")]
        public Button JoinBackButton;
        public Button JoinRefreshButton;
        public Text JoinRefreshLabel;
        public Transform RoomListContent;
        public GameObject RoomEmptyState;
        public ScrollRect RoomScroll;
        public Text JoinStatusLabel;

        [Header("Room row prefab (inactive template under Join panel)")]
        public GameObject RoomRowTemplate;

        public void ShowPanel(Panel panel)
        {
            if (MainPanel != null)
            {
                MainPanel.SetActive(panel == Panel.Main);
            }

            if (CreatePanel != null)
            {
                CreatePanel.SetActive(panel == Panel.CreateRoom);
            }

            if (JoinPanel != null)
            {
                JoinPanel.SetActive(panel == Panel.JoinRoom);
            }
        }

        public void SetStatus(string message)
        {
            string text = message ?? string.Empty;
            if (MainStatusLabel != null && MainPanel != null && MainPanel.activeSelf)
            {
                MainStatusLabel.text = text;
            }

            if (CreateStatusLabel != null && CreatePanel != null && CreatePanel.activeSelf)
            {
                CreateStatusLabel.text = text;
            }

            if (JoinStatusLabel != null && JoinPanel != null && JoinPanel.activeSelf)
            {
                JoinStatusLabel.text = text;
            }
        }

        public void SetMapSelected(int index)
        {
            Color selected = new Color(0.90f, 0.49f, 0.13f, 1f);
            Color idle = new Color(0.55f, 0.62f, 0.70f, 0.55f);
            for (int i = 0; i < MapBorders.Length; i++)
            {
                if (MapBorders[i] != null)
                {
                    Color color = i == index ? selected : idle;
                    MapBorders[i].color = color;

                    // Keep the hover effect in sync so it lerps from the selection color.
                    MiniVanMenuButtonHoverFx fx = i < MapButtons.Length && MapButtons[i] != null
                        ? MapButtons[i].GetComponent<MiniVanMenuButtonHoverFx>()
                        : null;
                    if (fx != null)
                    {
                        fx.SetBorderNormal(color);
                    }
                }

                if (i < MapCheckIcons.Length && MapCheckIcons[i] != null)
                {
                    MapCheckIcons[i].enabled = i == index;
                }
            }
        }
    }
}
