using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MiniVanGame
{
    /// <summary>
    /// Editable equipment window. Assign references in the prefab; the local player binds it at runtime.
    /// Prefab path: Resources/EquipmentUI/EquipmentHUD
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MiniVanEquipmentUi : MonoBehaviour
    {
        [Header("Root")]
        public Canvas Canvas;
        public RectTransform Window;
        public Text TitleText;
        public Text HintText;

        [Header("Preview")]
        public RawImage PreviewImage;

        /// <summary>Idle spin. Zero keeps the character facing the player (face paint stays readable).</summary>
        public float PreviewSpinDegreesPerSecond;

        public float PreviewDragSensitivity = 0.5f;

        [Header("Cells")]
        public MiniVanEquipmentCellView[] EquipmentSlots;
        public MiniVanEquipmentCellView[] InventoryCells;

        [Header("Drag")]
        public RectTransform DragGhost;
        public Text DragGhostLabel;

        private MiniVanPlayer player;
        private MiniVanEquipmentPreview preview;
        private MiniVanEquipmentCellView dragSource;
        private MiniVanEquipmentCellView hoverCell;
        private MiniVanInventoryItem draggedItem = MiniVanInventoryItem.None;
        private float previewYaw;

        public void Bind(MiniVanPlayer owner)
        {
            player = owner;
            BindCells(EquipmentSlots);
            BindCells(InventoryCells);
            EnsurePreview();
            SetDragGhostVisible(false);

            if (TitleText != null)
            {
                TitleText.text = "ЭКИПИРОВКА";
            }

            if (HintText != null)
            {
                HintText.text = string.Empty;
                HintText.gameObject.SetActive(false);
            }

            if (EquipmentSlots != null)
            {
                for (int i = 0; i < EquipmentSlots.Length; i++)
                {
                    MiniVanEquipmentCellView cell = EquipmentSlots[i];
                    if (cell != null && cell.SlotNameLabel != null)
                    {
                        cell.SlotNameLabel.text = MiniVanCosmeticCatalog.GetSlotName((MiniVanEquipmentSlot)cell.Index);
                    }
                }
            }
        }

        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
            if (preview != null)
            {
                preview.SetActive(visible);
            }

            if (visible)
            {
                RefreshContents();
            }
            else
            {
                CancelDrag();
            }
        }

        public void RefreshContents()
        {
            if (player == null)
            {
                return;
            }

            if (InventoryCells != null)
            {
                for (int i = 0; i < InventoryCells.Length; i++)
                {
                    MiniVanEquipmentCellView cell = InventoryCells[i];
                    if (cell != null)
                    {
                        cell.SetContent(player.GetInventoryItem(cell.Index));
                    }
                }
            }

            if (EquipmentSlots != null)
            {
                for (int i = 0; i < EquipmentSlots.Length; i++)
                {
                    MiniVanEquipmentCellView cell = EquipmentSlots[i];
                    if (cell != null)
                    {
                        cell.SetContent(player.GetEquippedItem((MiniVanEquipmentSlot)cell.Index));
                    }
                }
            }

            EnsurePreview();
            if (preview != null)
            {
                preview.Refresh(player);
            }
        }

        private void Update()
        {
            if (player == null)
            {
                return;
            }

            RefreshContents();

            if (dragSource != null && Input.GetMouseButtonUp(0) &&
                !IsPointerOverInventoryOrSlots(Input.mousePosition))
            {
                TryDropDraggedInventoryItem(Input.mousePosition);
                CancelDrag();
            }

            if (preview != null)
            {
                previewYaw += PreviewSpinDegreesPerSecond * Time.unscaledDeltaTime;
                previewYaw += GetPreviewDragDelta();
                preview.SetYaw(previewYaw);
            }
        }

        /// <summary>Holding the mouse over the portrait turns the character around.</summary>
        private float GetPreviewDragDelta()
        {
            if (PreviewImage == null || dragSource != null || !Input.GetMouseButton(0))
            {
                return 0f;
            }

            RectTransform rect = PreviewImage.rectTransform;
            Camera uiCamera = Canvas != null && Canvas.renderMode != RenderMode.ScreenSpaceOverlay ? Canvas.worldCamera : null;
            if (!RectTransformUtility.RectangleContainsScreenPoint(rect, Input.mousePosition, uiCamera))
            {
                return 0f;
            }

            return -Input.GetAxisRaw("Mouse X") * PreviewDragSensitivity * 12f;
        }

        public void BeginDrag(MiniVanEquipmentCellView source, PointerEventData eventData)
        {
            if (player == null || source == null)
            {
                return;
            }

            draggedItem = GetCellItem(source);
            if (draggedItem == MiniVanInventoryItem.None)
            {
                dragSource = null;
                return;
            }

            dragSource = source;
            if (DragGhostLabel != null)
            {
                DragGhostLabel.text = MiniVanPlayer.GetItemShortLabel(draggedItem);
            }

            SetDragGhostVisible(true);
            UpdateDrag(eventData);
        }

        public void UpdateDrag(PointerEventData eventData)
        {
            if (dragSource == null || DragGhost == null || Canvas == null)
            {
                return;
            }

            RectTransform canvasRect = Canvas.transform as RectTransform;
            if (canvasRect == null)
            {
                return;
            }

            Camera uiCamera = Canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : Canvas.worldCamera;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect, eventData.position, uiCamera, out Vector2 localPoint))
            {
                DragGhost.localPosition = localPoint;
            }
        }

        public void EndDrag(PointerEventData eventData)
        {
            Vector2 screenPosition = eventData != null ? eventData.position : (Vector2)Input.mousePosition;
            TryDropDraggedInventoryItem(screenPosition);
            CancelDrag();
        }

        public void DropOn(MiniVanEquipmentCellView target)
        {
            if (player == null || dragSource == null || target == null || target == dragSource)
            {
                return;
            }

            if (!IsDropAllowed(target))
            {
                return;
            }

            if (target.Kind == MiniVanEquipmentCellView.CellKind.Equipment)
            {
                player.RequestEquipFromInventory(dragSource.Index, (MiniVanEquipmentSlot)target.Index);
            }
            else
            {
                player.RequestUnequip((MiniVanEquipmentSlot)dragSource.Index, target.Index);
            }

            CancelDrag();
        }

        public void HoverEnter(MiniVanEquipmentCellView cell)
        {
            hoverCell = cell;
            if (dragSource == null || cell == null || cell == dragSource)
            {
                return;
            }

            cell.SetHighlight(true, IsDropAllowed(cell));
        }

        public void HoverExit(MiniVanEquipmentCellView cell)
        {
            if (cell == null)
            {
                return;
            }

            if (hoverCell == cell)
            {
                hoverCell = null;
            }

            cell.SetHighlight(false, false);
        }

        private void TryDropDraggedInventoryItem(Vector2 screenPosition)
        {
            if (player == null ||
                dragSource == null ||
                dragSource.Kind != MiniVanEquipmentCellView.CellKind.Inventory)
            {
                return;
            }

            if (IsPointerOverInventoryOrSlots(screenPosition))
            {
                return;
            }

            player.TryDropInventorySlot(dragSource.Index);
        }

        private bool IsPointerOverInventoryOrSlots(Vector2 screenPosition)
        {
            Camera uiCamera = Canvas != null && Canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? Canvas.worldCamera
                : null;
            return IsPointerOverCells(InventoryCells, screenPosition, uiCamera) ||
                   IsPointerOverCells(EquipmentSlots, screenPosition, uiCamera);
        }

        private static bool IsPointerOverCells(
            MiniVanEquipmentCellView[] cells,
            Vector2 screenPosition,
            Camera uiCamera)
        {
            if (cells == null)
            {
                return false;
            }

            for (int i = 0; i < cells.Length; i++)
            {
                MiniVanEquipmentCellView cell = cells[i];
                if (cell == null)
                {
                    continue;
                }

                RectTransform rect = cell.transform as RectTransform;
                if (rect != null &&
                    RectTransformUtility.RectangleContainsScreenPoint(rect, screenPosition, uiCamera))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsDropAllowed(MiniVanEquipmentCellView target)
        {
            if (player == null || dragSource == null || target == null || target == dragSource)
            {
                return false;
            }

            if (dragSource.Kind == MiniVanEquipmentCellView.CellKind.Inventory &&
                target.Kind == MiniVanEquipmentCellView.CellKind.Equipment)
            {
                return MiniVanCosmeticCatalog.CanEquip(draggedItem, (MiniVanEquipmentSlot)target.Index);
            }

            if (dragSource.Kind == MiniVanEquipmentCellView.CellKind.Equipment &&
                target.Kind == MiniVanEquipmentCellView.CellKind.Inventory)
            {
                return player.GetInventoryItem(target.Index) == MiniVanInventoryItem.None;
            }

            return false;
        }

        private MiniVanInventoryItem GetCellItem(MiniVanEquipmentCellView cell)
        {
            if (player == null || cell == null)
            {
                return MiniVanInventoryItem.None;
            }

            return cell.Kind == MiniVanEquipmentCellView.CellKind.Inventory
                ? player.GetInventoryItem(cell.Index)
                : player.GetEquippedItem((MiniVanEquipmentSlot)cell.Index);
        }

        private void CancelDrag()
        {
            dragSource = null;
            draggedItem = MiniVanInventoryItem.None;
            SetDragGhostVisible(false);
            ClearHighlights(EquipmentSlots);
            ClearHighlights(InventoryCells);
        }

        private static void ClearHighlights(MiniVanEquipmentCellView[] cells)
        {
            if (cells == null)
            {
                return;
            }

            for (int i = 0; i < cells.Length; i++)
            {
                if (cells[i] != null)
                {
                    cells[i].SetHighlight(false, false);
                }
            }
        }

        private void SetDragGhostVisible(bool visible)
        {
            if (DragGhost != null)
            {
                DragGhost.gameObject.SetActive(visible);
            }
        }

        private void BindCells(MiniVanEquipmentCellView[] cells)
        {
            if (cells == null)
            {
                return;
            }

            for (int i = 0; i < cells.Length; i++)
            {
                if (cells[i] != null)
                {
                    cells[i].Bind(this);
                }
            }
        }

        private void EnsurePreview()
        {
            if (preview != null || player == null)
            {
                return;
            }

            preview = MiniVanEquipmentPreview.Create(player);
            preview.SetActive(gameObject.activeInHierarchy);

            if (PreviewImage != null)
            {
                PreviewImage.texture = preview.Texture;
                PreviewImage.color = Color.white;
            }
        }

        private void OnDestroy()
        {
            if (preview != null)
            {
                Destroy(preview.gameObject);
                preview = null;
            }
        }
    }
}
