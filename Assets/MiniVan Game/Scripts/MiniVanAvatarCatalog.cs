using UnityEngine;

namespace MiniVanGame
{
    public enum MiniVanAvatarLifeIcon
    {
        Alive,
        Shock,
        Dead
    }

    /// <summary>
    /// Loads the 4 unique avatar icon sets (alive / shock / dead) from Resources
    /// and soft body tint colors matched to each avatar.
    /// 0 green, 1 coral/red, 2 purple, 3 yellow — light, low saturation.
    /// </summary>
    public static class MiniVanAvatarCatalog
    {
        public const int AvatarCount = 4;
        public const string ResourcesFolder = "MiniVanHUD/Avatars";

        private static readonly Color[] SoftBodyColors =
        {
            new Color(0.70f, 0.86f, 0.68f, 1f), // green
            new Color(0.90f, 0.70f, 0.66f, 1f), // coral / red
            new Color(0.76f, 0.70f, 0.88f, 1f), // purple
            new Color(0.93f, 0.88f, 0.66f, 1f), // yellow
        };

        private static Sprite[,] icons;
        private static bool loaded;

        public static Sprite GetIcon(int avatarIndex, MiniVanAvatarLifeIcon life)
        {
            EnsureLoaded();
            if (avatarIndex < 0 || avatarIndex >= AvatarCount || icons == null)
            {
                return null;
            }

            return icons[avatarIndex, (int)life];
        }

        public static Color GetBodyColor(int avatarIndex)
        {
            if (avatarIndex < 0 || avatarIndex >= SoftBodyColors.Length)
            {
                return new Color(0.86f, 0.86f, 0.86f, 1f);
            }

            return SoftBodyColors[avatarIndex];
        }

        public static Vector3 GetBodyColorVector(int avatarIndex)
        {
            Color color = GetBodyColor(avatarIndex);
            return new Vector3(color.r, color.g, color.b);
        }

        public static MiniVanAvatarLifeIcon ResolveLifeIcon(MiniVanPlayer player)
        {
            if (player == null)
            {
                return MiniVanAvatarLifeIcon.Alive;
            }

            if (player.IsPermanentlyDead)
            {
                return MiniVanAvatarLifeIcon.Dead;
            }

            if (player.IsUnconscious)
            {
                return MiniVanAvatarLifeIcon.Shock;
            }

            return MiniVanAvatarLifeIcon.Alive;
        }

        private static void EnsureLoaded()
        {
            if (loaded)
            {
                return;
            }

            loaded = true;
            icons = new Sprite[AvatarCount, 3];
            string[] states = { "alive", "shock", "dead" };
            for (int avatar = 0; avatar < AvatarCount; avatar++)
            {
                for (int state = 0; state < states.Length; state++)
                {
                    string path = ResourcesFolder + "/avatar_" + avatar + "_" + states[state];
                    icons[avatar, state] = Resources.Load<Sprite>(path);
                }
            }
        }
    }
}
