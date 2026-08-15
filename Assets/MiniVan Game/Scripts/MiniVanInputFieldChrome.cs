using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MiniVanGame
{
    /// <summary>
    /// Drives Outline border colors for menu InputFields (idle / hover / focused).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MiniVanInputFieldChrome : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        public Outline Outline;
        public Color Normal = new Color(0.55f, 0.60f, 0.66f, 0.85f);
        public Color Hover = new Color(0.78f, 0.82f, 0.88f, 1f);
        public Color Selected = new Color(0.90f, 0.49f, 0.13f, 1f);

        private bool hovered;
        private bool selected;

        private void OnEnable()
        {
            Refresh();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            hovered = true;
            Refresh();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hovered = false;
            Refresh();
        }

        public void OnSelect(BaseEventData eventData)
        {
            selected = true;
            Refresh();
        }

        public void OnDeselect(BaseEventData eventData)
        {
            selected = false;
            Refresh();
        }

        private void Refresh()
        {
            if (Outline == null)
            {
                return;
            }

            Outline.effectColor = selected ? Selected : (hovered ? Hover : Normal);
        }
    }
}
