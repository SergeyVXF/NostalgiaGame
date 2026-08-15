using UnityEngine;
using UnityEngine.UI;

namespace MiniVanGame
{
    /// <summary>
    /// Editable MiniVan HUD root. Layout and sprites are authored on the prefab;
    /// runtime only binds player data into the exposed references.
    /// Prefab: Resources/MiniVanHUD/MiniVanHUD
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MiniVanHudUi : MonoBehaviour
    {
        public const string ResourcesPath = "MiniVanHUD/MiniVanHUD";

        [Header("Root canvases (edit positions / sort order here)")]
        public Canvas RosterCanvas;
        public Canvas PlayerStatusCanvas;
        public Canvas VehicleCanvas;
        public Canvas HotbarCanvas;
        public Canvas PingCanvas;
        public Canvas EnemyCombatCanvas;

        [Header("Roster")]
        public MiniVanHudRosterSlot[] RosterSlots = new MiniVanHudRosterSlot[4];

        [Header("Player status")]
        public Image HealthFill;
        public Image OxygenFill;
        public GameObject OxygenRoot;

        [Header("Vehicle")]
        public GameObject VehicleRoot;
        public Image VehicleHealthFill;
        public Image VehicleBodyIcon;
        public Image[] VehicleWheelIcons = new Image[4];

        [Header("Enemy combat (top-center target bar)")]
        public GameObject EnemyCombatRoot;
        public Text EnemyCombatNameLabel;
        public Image EnemyCombatHealthFill;
        public Image EnemyCombatHealthBackground;

        [Header("Hotbar")]
        public Image[] HotbarSlotBackgrounds = new Image[4];
        public Text[] HotbarSlotLabels = new Text[4];
        public Image[] HotbarWinchBars = new Image[4];

        [Header("Ping")]
        public Text PingLabel;

        private MiniVanPlayer owner;
        private static Sprite sharedWhiteFillSprite;

        private static readonly Color HealthColorFull = new Color(0.2f, 0.85f, 0.3f, 1f);
        private static readonly Color HealthColorMid = new Color(1f, 0.82f, 0.12f, 1f);
        private static readonly Color HealthColorLow = new Color(0.9f, 0.12f, 0.08f, 1f);

        public void Bind(MiniVanPlayer player)
        {
            owner = player;
            EnsureFillSprites();
        }

        public void SetVisible(bool visible)
        {
            if (gameObject.activeSelf != visible)
            {
                gameObject.SetActive(visible);
            }
        }

        public void Refresh()
        {
            if (owner == null || !owner.IsOwner)
            {
                return;
            }

            RefreshRoster();
            RefreshPlayerStatus();
            RefreshVehicle();
            RefreshEnemyCombat();
            RefreshHotbar();
            RefreshPing();
        }

        public bool HasEnemyCombatWidgets =>
            EnemyCombatRoot != null && EnemyCombatNameLabel != null && EnemyCombatHealthFill != null;

        private void RefreshEnemyCombat()
        {
            if (!HasEnemyCombatWidgets)
            {
                return;
            }

            bool show = MiniVanEnemyCombatHud.IsVisible;
            if (EnemyCombatRoot.activeSelf != show)
            {
                EnemyCombatRoot.SetActive(show);
            }

            if (!show)
            {
                return;
            }

            EnemyCombatNameLabel.text = MiniVanEnemyCombatHud.DisplayName;
            ApplyHealthFill(EnemyCombatHealthFill, MiniVanEnemyCombatHud.Health01);
        }

        private void RefreshRoster()
        {
            if (RosterSlots == null)
            {
                return;
            }

            MiniVanPlayer[] players = FindObjectsByType<MiniVanPlayer>(FindObjectsSortMode.None);
            System.Array.Sort(players, CompareRosterOrder);

            int write = 0;
            for (int i = 0; i < players.Length && write < RosterSlots.Length; i++)
            {
                MiniVanPlayer player = players[i];
                if (player == null || !player.IsSpawned)
                {
                    continue;
                }

                MiniVanHudRosterSlot slot = RosterSlots[write];
                if (slot == null)
                {
                    write++;
                    continue;
                }

                slot.SetVisible(true);
                int avatar = player.AvatarIndex;
                Sprite icon = MiniVanAvatarCatalog.GetIcon(avatar, MiniVanAvatarCatalog.ResolveLifeIcon(player));
                slot.Apply(player.DisplayName, icon, player.AmberButtonCount);
                write++;
            }

            for (int i = write; i < RosterSlots.Length; i++)
            {
                if (RosterSlots[i] != null)
                {
                    RosterSlots[i].SetVisible(false);
                }
            }
        }

        private int CompareRosterOrder(MiniVanPlayer left, MiniVanPlayer right)
        {
            if (left == owner && right != owner)
            {
                return -1;
            }

            if (right == owner && left != owner)
            {
                return 1;
            }

            ulong leftId = left != null ? left.OwnerClientId : ulong.MaxValue;
            ulong rightId = right != null ? right.OwnerClientId : ulong.MaxValue;
            return leftId.CompareTo(rightId);
        }

        private void RefreshPlayerStatus()
        {
            if (HealthFill != null)
            {
                float value01 = Mathf.Clamp01(owner.NetworkHealth / (float)Mathf.Max(1, owner.PlayerMaxHealth));
                ApplyHealthFill(HealthFill, value01);
            }

            bool showOxygen = owner.IsDamWaterHeadSubmergedForHud();
            if (OxygenRoot != null && OxygenRoot.activeSelf != showOxygen)
            {
                OxygenRoot.SetActive(showOxygen);
            }

            if (showOxygen && OxygenFill != null)
            {
                EnsureFilledImage(OxygenFill);
                OxygenFill.fillAmount = owner.DamWaterOxygen01ForHud();
            }
        }

        private void RefreshVehicle()
        {
            MiniVanVehicle vehicle = owner.ResolveHudVehicleForHud();
            bool show = vehicle != null;
            if (VehicleRoot != null && VehicleRoot.activeSelf != show)
            {
                VehicleRoot.SetActive(show);
            }

            if (!show)
            {
                return;
            }

            if (VehicleHealthFill != null)
            {
                ApplyHealthFill(VehicleHealthFill, Mathf.Clamp01(vehicle.Health01));
            }

            if (VehicleBodyIcon != null)
            {
                VehicleBodyIcon.color = owner.MiniVanHudIconOkColor;
            }

            // Side-view icon: index 0 = rear axle, 1 = front axle.
            int detached = vehicle.DetachedWheelIndex.Value;
            bool rearLost = detached == 2 || detached == 3;
            bool frontLost = detached == 0 || detached == 1;
            ApplyWheelIcon(0, rearLost);
            ApplyWheelIcon(1, frontLost);
        }

        private void EnsureFillSprites()
        {
            EnsureFilledImage(HealthFill);
            EnsureFilledImage(OxygenFill);
            EnsureFilledImage(VehicleHealthFill);
            EnsureFilledImage(EnemyCombatHealthFill);
            if (HotbarWinchBars == null)
            {
                return;
            }

            for (int i = 0; i < HotbarWinchBars.Length; i++)
            {
                EnsureFilledImage(HotbarWinchBars[i]);
            }
        }

        private static void ApplyHealthFill(Image image, float value01)
        {
            EnsureFilledImage(image);
            value01 = Mathf.Clamp01(value01);
            image.fillAmount = value01;
            image.color = EvaluateHealthColor(value01);
        }

        private static Color EvaluateHealthColor(float value01)
        {
            if (value01 >= 0.5f)
            {
                return Color.Lerp(HealthColorMid, HealthColorFull, (value01 - 0.5f) * 2f);
            }

            return Color.Lerp(HealthColorLow, HealthColorMid, value01 * 2f);
        }

        private static void EnsureFilledImage(Image image)
        {
            if (image == null)
            {
                return;
            }

            if (image.sprite == null)
            {
                image.sprite = GetWhiteFillSprite();
            }

            if (image.type != Image.Type.Filled)
            {
                image.type = Image.Type.Filled;
            }

            image.fillMethod = Image.FillMethod.Horizontal;
            image.fillOrigin = (int)Image.OriginHorizontal.Left;
        }

        private static Sprite GetWhiteFillSprite()
        {
            if (sharedWhiteFillSprite != null)
            {
                return sharedWhiteFillSprite;
            }

            Texture2D tex = Texture2D.whiteTexture;
            sharedWhiteFillSprite = Sprite.Create(
                tex,
                new Rect(0f, 0f, tex.width, tex.height),
                new Vector2(0.5f, 0.5f),
                100f);
            sharedWhiteFillSprite.name = "MiniVanHud_WhiteFill";
            return sharedWhiteFillSprite;
        }

        private void ApplyWheelIcon(int index, bool lost)
        {
            if (VehicleWheelIcons == null || index < 0 || index >= VehicleWheelIcons.Length)
            {
                return;
            }

            Image wheel = VehicleWheelIcons[index];
            if (wheel == null)
            {
                return;
            }

            wheel.enabled = true;
            wheel.color = lost ? owner.MiniVanHudIconLostWheelColor : owner.MiniVanHudIconOkColor;
        }

        private void RefreshHotbar()
        {
            if (HotbarSlotLabels == null)
            {
                return;
            }

            for (int i = 0; i < HotbarSlotLabels.Length && i < 4; i++)
            {
                MiniVanInventoryItem item = owner.GetInventorySlotPublic(i);
                bool selected = i == owner.LocalSelectedSlotForHud;

                if (HotbarSlotBackgrounds != null && i < HotbarSlotBackgrounds.Length && HotbarSlotBackgrounds[i] != null)
                {
                    HotbarSlotBackgrounds[i].color = selected
                        ? new Color(0.95f, 0.82f, 0.22f, 0.78f)
                        : new Color(0f, 0f, 0f, 0.54f);
                }

                if (HotbarSlotLabels[i] != null)
                {
                    HotbarSlotLabels[i].text = MiniVanPlayer.GetInventoryLabelPublic(item);
                }

                if (HotbarWinchBars != null && i < HotbarWinchBars.Length && HotbarWinchBars[i] != null)
                {
                    bool winch = item == MiniVanInventoryItem.Winch;
                    HotbarWinchBars[i].gameObject.SetActive(winch);
                    if (winch)
                    {
                        EnsureFilledImage(HotbarWinchBars[i]);
                        HotbarWinchBars[i].fillAmount = owner.WinchDurability01ForHud;
                    }
                }
            }
        }

        private void RefreshPing()
        {
            if (PingLabel == null)
            {
                return;
            }

            PingLabel.text = owner.PingHudTextForHud();
        }
    }
}
