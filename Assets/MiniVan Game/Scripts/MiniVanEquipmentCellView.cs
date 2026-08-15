using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MiniVanGame
{
    /// <summary>
    /// One draggable cell of the equipment window: either an inventory cell or an equipment slot.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MiniVanEquipmentCellView : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public enum CellKind
        {
            Inventory = 0,
            Equipment = 1
        }

        public CellKind Kind = CellKind.Inventory;
        public int Index;
        public Image Background;
        public Image Silhouette;
        public Text ItemLabel;
        public Text SlotNameLabel;

        private static readonly Color IdleColor = new Color(0.12f, 0.13f, 0.16f, 0.92f);
        private static readonly Color AcceptColor = new Color(0.18f, 0.62f, 0.28f, 0.95f);
        private static readonly Color RejectColor = new Color(0.68f, 0.18f, 0.16f, 0.95f);

        private MiniVanEquipmentUi ui;

        public void Bind(MiniVanEquipmentUi owner)
        {
            ui = owner;
            SetHighlight(false, false);
        }

        public void SetContent(MiniVanInventoryItem item)
        {
            if (ItemLabel != null)
            {
                ItemLabel.text = item == MiniVanInventoryItem.None ? string.Empty : MiniVanPlayer.GetItemShortLabel(item);
            }

            if (Silhouette != null)
            {
                // The silhouette is only a hint for an empty equipment slot.
                Silhouette.enabled = Kind == CellKind.Equipment && item == MiniVanInventoryItem.None;
            }
        }

        public void SetHighlight(bool active, bool accepted)
        {
            if (Background == null)
            {
                return;
            }

            Background.color = !active ? IdleColor : (accepted ? AcceptColor : RejectColor);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (ui != null)
            {
                ui.BeginDrag(this, eventData);
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (ui != null)
            {
                ui.UpdateDrag(eventData);
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (ui != null)
            {
                ui.EndDrag(eventData);
            }
        }

        public void OnDrop(PointerEventData eventData)
        {
            if (ui != null)
            {
                ui.DropOn(this);
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (ui != null)
            {
                ui.HoverEnter(this);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (ui != null)
            {
                ui.HoverExit(this);
            }
        }
    }
}
