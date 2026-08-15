using UnityEngine;

namespace MiniVanGame
{
    /// <summary>
    /// Single combat-target health bar state. MiniVanHUD binds to this; no stacking bars.
    /// </summary>
    public static class MiniVanEnemyCombatHud
    {
        public const float VisibleSeconds = 3f;

        private static MiniVanZombie currentTarget;
        private static float hideAtUnscaled = -1f;
        private static string cachedName = "Enemy";
        private static float cachedHealth;
        private static int cachedMaxHealth = 1;

        private static GUIStyle nameStyle;
        private static Texture2D whiteTexture;

        public static bool IsVisible =>
            hideAtUnscaled >= 0f && Time.unscaledTime < hideAtUnscaled;

        public static string DisplayName
        {
            get
            {
                RefreshCacheIfNeeded();
                return cachedName;
            }
        }

        public static int CurrentHealth
        {
            get
            {
                RefreshCacheIfNeeded();
                return Mathf.CeilToInt(cachedHealth);
            }
        }

        public static int MaxHealth
        {
            get
            {
                RefreshCacheIfNeeded();
                return cachedMaxHealth;
            }
        }

        public static float Health01
        {
            get
            {
                RefreshCacheIfNeeded();
                int max = cachedMaxHealth;
                return max > 0 ? Mathf.Clamp01(cachedHealth / max) : 0f;
            }
        }

        public static void Show(MiniVanZombie enemy)
        {
            if (enemy == null)
            {
                return;
            }

            ShowSnapshot(
                enemy.EnemyDisplayName,
                enemy.CurrentHealthPrecise,
                Mathf.Max(1, enemy.MaxHealth),
                enemy);
        }

        public static void ShowSnapshot(
            string enemyName,
            float health,
            int maxHealth,
            MiniVanZombie enemy = null)
        {
            currentTarget = enemy;
            cachedName = string.IsNullOrWhiteSpace(enemyName) ? "Enemy" : enemyName;
            cachedMaxHealth = Mathf.Max(1, maxHealth);
            cachedHealth = Mathf.Clamp(health, 0f, cachedMaxHealth);
            hideAtUnscaled = Time.unscaledTime + VisibleSeconds;
        }

        /// <summary>
        /// Legacy OnGUI fallback when MiniVanHUD enemy widgets are not wired.
        /// </summary>
        public static void DrawLegacyOnGui()
        {
            if (!IsVisible)
            {
                currentTarget = null;
                hideAtUnscaled = -1f;
                return;
            }

            RefreshCacheIfNeeded();
            EnsureStyles();

            float width = Mathf.Clamp(Screen.width * 0.22f, 220f, 340f);
            float height = 20f;
            float x = (Screen.width - width) * 0.5f;
            float y = 54f;

            Rect nameRect = new Rect(x, y - 22f, width, 20f);
            Rect barRect = new Rect(x, y, width, height);

            Color previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(
                new Rect(nameRect.x - 6f, nameRect.y - 2f, nameRect.width + 12f, nameRect.height + height + 10f),
                whiteTexture);
            GUI.color = Color.white;
            GUI.Label(nameRect, cachedName, nameStyle);
            DrawBar(barRect, Health01);
            GUI.color = previous;
        }

        private static void RefreshCacheIfNeeded()
        {
            if (!IsVisible)
            {
                return;
            }

            if (currentTarget)
            {
                CacheFromTarget();
            }
        }

        private static void CacheFromTarget()
        {
            if (!currentTarget)
            {
                currentTarget = null;
                return;
            }

            cachedName = currentTarget.EnemyDisplayName;
            cachedMaxHealth = Mathf.Max(1, currentTarget.MaxHealth);
            float live = Mathf.Clamp(currentTarget.CurrentHealthPrecise, 0f, cachedMaxHealth);
            // Clients don't simulate shield chip residue — never raise the bar above the
            // latest hit snapshot, only allow live values to pull it further down.
            cachedHealth = Mathf.Clamp(Mathf.Min(cachedHealth, live), 0f, cachedMaxHealth);
        }

        private static void EnsureStyles()
        {
            if (whiteTexture == null)
            {
                whiteTexture = Texture2D.whiteTexture;
            }

            if (nameStyle != null)
            {
                return;
            }

            nameStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                fontSize = 15
            };
            nameStyle.normal.textColor = Color.white;
        }

        private static void DrawBar(Rect rect, float value01)
        {
            Color empty = new Color(0.18f, 0.05f, 0.05f, 0.92f);
            Color fill = Color.Lerp(new Color(0.85f, 0.12f, 0.1f), new Color(0.2f, 0.82f, 0.18f), value01);
            Color border = new Color(0f, 0f, 0f, 0.85f);

            GUI.color = border;
            GUI.DrawTexture(rect, whiteTexture);
            Rect inner = new Rect(rect.x + 2f, rect.y + 2f, rect.width - 4f, rect.height - 4f);
            GUI.color = empty;
            GUI.DrawTexture(inner, whiteTexture);
            if (value01 > 0.001f)
            {
                GUI.color = fill;
                GUI.DrawTexture(new Rect(inner.x, inner.y, inner.width * value01, inner.height), whiteTexture);
            }
        }
    }
}
