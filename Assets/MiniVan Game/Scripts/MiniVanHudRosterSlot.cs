using UnityEngine;
using UnityEngine.UI;

namespace MiniVanGame
{
    /// <summary>
    /// One editable roster row: avatar icon + player name + amber score under the name.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MiniVanHudRosterSlot : MonoBehaviour
    {
        public GameObject Root;
        public Image AvatarImage;
        public Text NameLabel;
        public Text AmberCountLabel;

        public void SetVisible(bool visible)
        {
            GameObject target = Root != null ? Root : gameObject;
            if (target.activeSelf != visible)
            {
                target.SetActive(visible);
            }
        }

        public void Apply(string displayName, Sprite icon, int amberButtonCount = 0)
        {
            if (NameLabel != null)
            {
                NameLabel.text = displayName ?? string.Empty;
            }

            if (AmberCountLabel != null)
            {
                AmberCountLabel.text = "×" + Mathf.Max(0, amberButtonCount);
                if (!AmberCountLabel.gameObject.activeSelf)
                {
                    AmberCountLabel.gameObject.SetActive(true);
                }
            }
            else if (NameLabel != null)
            {
                // Fallback if prefab was not rebuilt yet.
                string name = displayName ?? string.Empty;
                NameLabel.text = string.IsNullOrEmpty(name)
                    ? "×" + Mathf.Max(0, amberButtonCount)
                    : name + "\n×" + Mathf.Max(0, amberButtonCount);
            }

            if (AvatarImage != null)
            {
                AvatarImage.sprite = icon;
                AvatarImage.enabled = icon != null;
            }
        }
    }
}
