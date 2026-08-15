using Unity.Netcode;
using UnityEngine;

namespace MiniVanGame
{
    public partial class MiniVanPlayer
    {
        private MiniVanPizzaItem lookedAtPizzaItem;
        private MiniVanPizzaItem highlightedPizzaItem;
        private MiniVanPizzaChest lookedAtPizzaChest;
        private MiniVanPizzaStation lookedAtPizzaStation;
        private MiniVanPizzaOven lookedAtPizzaOven;
        private MiniVanPizzaBoxStation lookedAtPizzaBoxStation;
        private MiniVanPizzaToolShelf lookedAtPizzaToolShelf;
        private MiniVanPizzaChest openPizzaChest;
        private MiniVanInventoryItem pizzaDraggedItem = MiniVanInventoryItem.None;
        private int pizzaDraggedSource = 0;
        private int pizzaDraggedIndex = -1;
        private string pizzaStatusText;
        private float pizzaStatusUntil;
        private GameObject heldPizzaVisual;
        private MiniVanInventoryItem heldPizzaVisualItem = MiniVanInventoryItem.None;

        private const int PizzaDragSourceInventory = 1;
        private const int PizzaDragSourceChest = 2;

        private void UpdatePizzaLookTargets()
        {
            MiniVanPizzaItem previousHighlight = highlightedPizzaItem;

            lookedAtPizzaItem = null;
            lookedAtPizzaChest = null;
            lookedAtPizzaStation = null;
            lookedAtPizzaOven = null;
            lookedAtPizzaBoxStation = null;
            lookedAtPizzaToolShelf = null;

            if (currentSeat == null && !IsPizzaChestOpen())
            {
                lookedAtPizzaChest = FindLookedAtPizzaChest();
                lookedAtPizzaStation = FindLookedAtPizzaStation();
                lookedAtPizzaOven = FindLookedAtPizzaOven();
                lookedAtPizzaBoxStation = FindLookedAtPizzaBoxStation();
                lookedAtPizzaToolShelf = FindLookedAtPizzaToolShelf();
                lookedAtPizzaItem = FindLookedAtPizzaItem();
            }

            highlightedPizzaItem = lookedAtPizzaItem;
            if (previousHighlight != null && previousHighlight != highlightedPizzaItem)
            {
                previousHighlight.SetOutlined(false);
            }

            if (highlightedPizzaItem != null)
            {
                highlightedPizzaItem.SetOutlined(true);
            }
        }

        private bool HandlePizzaInteractionInput()
        {
            if (IsPizzaChestOpen())
            {
                if (MiniVanKeyBindings.GetKeyDown(MiniVanKeyAction.Interact) || Input.GetKeyDown(KeyCode.Escape))
                {
                    ClosePizzaChest();
                }

                return true;
            }

            if (MiniVanKeyBindings.GetKeyDown(MiniVanKeyAction.Drop) && TryDropSelectedPizzaItem())
            {
                return true;
            }

            if (Input.GetMouseButtonDown(0))
            {
                if (lookedAtPizzaItem != null && TryBoxPizzaWithLookedAtBox(lookedAtPizzaItem))
                {
                    return true;
                }

                if (lookedAtPizzaBoxStation != null)
                {
                    TryUsePizzaBoxStation(lookedAtPizzaBoxStation);
                    return true;
                }

                if (lookedAtPizzaStation != null)
                {
                    TryUsePizzaStation(lookedAtPizzaStation);
                    return true;
                }
            }

            if (!MiniVanKeyBindings.GetKeyDown(MiniVanKeyAction.Interact))
            {
                return false;
            }

            if (lookedAtPizzaChest != null)
            {
                OpenPizzaChest(lookedAtPizzaChest);
                return true;
            }

            if (lookedAtPizzaItem != null)
            {
                TryPickupPizzaItem(lookedAtPizzaItem);
                return true;
            }

            if (lookedAtPizzaToolShelf != null)
            {
                TryUsePizzaToolShelf(lookedAtPizzaToolShelf);
                return true;
            }

            if (lookedAtPizzaOven != null)
            {
                TryUsePizzaOven(lookedAtPizzaOven);
                return true;
            }

            if (lookedAtPizzaBoxStation != null)
            {
                SetPizzaStatus("Use LMB with pizza");
                return true;
            }

            if (lookedAtPizzaStation != null)
            {
                TryPlaceOrTakePizzaStation(lookedAtPizzaStation);
                return true;
            }

            return false;
        }

        private void UpdatePizzaHeldVisual()
        {
            MiniVanInventoryItem selectedItem = GetInventorySlot(IsOwner ? localSelectedSlot : networkSelectedSlot.Value);
            bool shouldShow = (IsPizzaLoopItem(selectedItem) ||
                               selectedItem == MiniVanInventoryItem.PanelkaKey ||
                               selectedItem == MiniVanInventoryItem.Winch) && currentSeat == null;
            if (!shouldShow)
            {
                DestroyPizzaHeldVisual();
                return;
            }

            if (heldPizzaVisual != null && heldPizzaVisualItem == selectedItem)
            {
                return;
            }

            DestroyPizzaHeldVisual();
            heldPizzaVisualItem = selectedItem;

            Transform parent = IsOwner && CameraRoot != null ? CameraRoot : transform;
            heldPizzaVisual = CreatePizzaHeldVisual(selectedItem, parent);
            heldPizzaVisual.name = IsOwner ? "Held Pizza Item" : "Remote Held Pizza Item";
            if (IsOwner)
            {
                heldPizzaVisual.transform.localPosition = new Vector3(0.34f, -0.28f, 0.72f);
                heldPizzaVisual.transform.localRotation = Quaternion.Euler(8f, -18f, 0f);
                heldPizzaVisual.transform.localScale = Vector3.one;
            }
            else
            {
                heldPizzaVisual.transform.localPosition = new Vector3(0.36f, 0.35f, 0.42f);
                heldPizzaVisual.transform.localRotation = Quaternion.Euler(-8f, -18f, 0f);
                heldPizzaVisual.transform.localScale = Vector3.one * 0.7f;
            }
        }

        private void DestroyPizzaHeldVisual()
        {
            if (heldPizzaVisual != null)
            {
                Destroy(heldPizzaVisual);
            }

            heldPizzaVisual = null;
            heldPizzaVisualItem = MiniVanInventoryItem.None;
        }

        private GameObject CreatePizzaHeldVisual(MiniVanInventoryItem item, Transform parent)
        {
            GameObject visual;
            if (item == MiniVanInventoryItem.PanelkaKey)
            {
                visual = CreatePanelkaKeyHeldVisual(parent);
            }
            else if (item == MiniVanInventoryItem.Winch)
            {
                visual = CreateWinchHeldVisual(parent);
            }
            else
            {
                GameObject prefab = Resources.Load<GameObject>(GetPizzaItemResourcePath(item));
                visual = prefab != null
                    ? Instantiate(prefab, parent, false)
                    : CreatePizzaFallbackVisual(item, parent);
            }
            visual.name = "Held " + GetInventoryLabel(item);

            foreach (MiniVanPizzaItem pizzaItem in visual.GetComponentsInChildren<MiniVanPizzaItem>(true))
            {
                pizzaItem.enabled = false;
            }

            foreach (Rigidbody body in visual.GetComponentsInChildren<Rigidbody>(true))
            {
                body.isKinematic = true;
                body.detectCollisions = false;
            }

            foreach (Collider collider in visual.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
            }

            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;
            return visual;
        }

        private GameObject CreateWinchHeldVisual(Transform parent)
        {
            GameObject root = new GameObject("Held Winch Visual");
            root.transform.SetParent(parent, false);

            Material metal = CreateHeldVisualMaterial(new Color(0.14f, 0.15f, 0.16f, 1f));
            Material rope = CreateHeldVisualMaterial(new Color(0.045f, 0.043f, 0.039f, 1f));
            Material handleMaterial = CreateHeldVisualMaterial(new Color(0.75f, 0.13f, 0.07f, 1f));

            GameObject spool = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            spool.name = "Held Winch Spool";
            spool.transform.SetParent(root.transform, false);
            spool.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            spool.transform.localScale = new Vector3(0.11f, 0.22f, 0.11f);
            SetHeldVisualMaterial(spool, rope);

            GameObject frameA = GameObject.CreatePrimitive(PrimitiveType.Cube);
            frameA.name = "Held Winch Frame A";
            frameA.transform.SetParent(root.transform, false);
            frameA.transform.localPosition = new Vector3(-0.26f, 0f, 0f);
            frameA.transform.localScale = new Vector3(0.045f, 0.32f, 0.32f);
            SetHeldVisualMaterial(frameA, metal);

            GameObject frameB = GameObject.CreatePrimitive(PrimitiveType.Cube);
            frameB.name = "Held Winch Frame B";
            frameB.transform.SetParent(root.transform, false);
            frameB.transform.localPosition = new Vector3(0.26f, 0f, 0f);
            frameB.transform.localScale = new Vector3(0.045f, 0.32f, 0.32f);
            SetHeldVisualMaterial(frameB, metal);

            GameObject handle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            handle.name = "Held Winch Handle";
            handle.transform.SetParent(root.transform, false);
            handle.transform.localPosition = new Vector3(0f, 0.25f, 0f);
            handle.transform.localScale = new Vector3(0.58f, 0.055f, 0.09f);
            SetHeldVisualMaterial(handle, handleMaterial);

            GameObject hook = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            hook.name = "Held Winch Hook";
            hook.transform.SetParent(root.transform, false);
            hook.transform.localPosition = new Vector3(0.40f, -0.13f, 0f);
            hook.transform.localScale = Vector3.one * 0.105f;
            SetHeldVisualMaterial(hook, metal);

            return root;
        }

        private GameObject CreatePizzaFallbackVisual(MiniVanInventoryItem item, Transform parent)
        {
            if (MiniVanFuelRules.IsZombiePart(item))
            {
                return MiniVanFuelPartFactory.CreateVisual(item, parent, false);
            }

            if (item == MiniVanInventoryItem.AcidClot)
            {
                return MiniVanAcidClotVisual.Create(parent, false);
            }

            if (item == MiniVanInventoryItem.TraceHead)
            {
                return MiniVanTraceHeadVisual.Create(parent, false);
            }

            GameObject visual = GameObject.CreatePrimitive(item == MiniVanInventoryItem.RollingPin ? PrimitiveType.Cylinder : PrimitiveType.Cube);
            visual.transform.SetParent(parent, false);
            Collider collider = visual.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            Renderer renderer = visual.GetComponent<Renderer>();
            if (renderer != null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null)
                {
                    shader = Shader.Find("Standard");
                }

                renderer.material = new Material(shader);
                ApplyPizzaMaterialColor(renderer.material, GetPizzaItemColor(item));
            }

            visual.transform.localScale = new Vector3(0.28f, 0.22f, 0.28f);
            return visual;
        }

        private void DrawPizzaGameplayUi()
        {
            if (IsPizzaChestOpen())
            {
                DrawPizzaChestUi();
            }

            if (!string.IsNullOrEmpty(pizzaStatusText) && Time.time < pizzaStatusUntil)
            {
                GUIStyle style = new GUIStyle(GUI.skin.box)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 16,
                    fontStyle = FontStyle.Bold
                };
                style.normal.textColor = Color.white;
                GUI.Box(new Rect(Screen.width * 0.5f - 150f, Screen.height * 0.5f + 74f, 300f, 34f), pizzaStatusText, style);
            }

            if (lookedAtPizzaStation != null)
            {
                DrawPizzaSmallPrompt(GetStationPrompt(lookedAtPizzaStation));
            }
            else if (lookedAtPizzaOven != null)
            {
                DrawPizzaSmallPrompt(lookedAtPizzaOven.HasPizza ? "E - take pizza" : "E - put raw pizza in oven");
            }
            else if (lookedAtPizzaBoxStation != null)
            {
                DrawPizzaSmallPrompt("LMB - box pizza");
            }
            else if (lookedAtPizzaToolShelf != null)
            {
                DrawPizzaSmallPrompt(lookedAtPizzaToolShelf.StoredItem == MiniVanInventoryItem.None ? "E - place tool" : "E - take tool");
            }
            else if (lookedAtPizzaChest != null && !IsPizzaChestOpen())
            {
                DrawPizzaSmallPrompt("E - open chest");
            }
            else if (lookedAtPizzaItem != null)
            {
                DrawPizzaSmallPrompt("E - pick up " + GetInventoryLabel(lookedAtPizzaItem.Item));
            }
        }

        private void DrawPizzaSmallPrompt(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            GUIStyle style = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 16,
                fontStyle = FontStyle.Bold
            };
            style.normal.textColor = Color.white;
            GUI.Box(new Rect(Screen.width * 0.5f - 150f, Screen.height * 0.64f, 300f, 32f), text, style);
        }

        private void DrawPizzaChestUi()
        {
            if (openPizzaChest == null)
            {
                ClosePizzaChest();
                return;
            }

            openPizzaChest.EnsureSlots();
            const float slot = 52f;
            const float gap = 8f;
            Rect panel = new Rect(Screen.width * 0.5f - 365f, Screen.height * 0.5f - 165f, 730f, 330f);
            DrawSolidRect(panel, new Color(0f, 0f, 0f, 0.72f));
            GUI.Box(panel, "Pizza Chest");

            GUI.Label(new Rect(panel.x + 24f, panel.y + 34f, 260f, 24f), "Inventory");
            for (int i = 0; i < 4; i++)
            {
                Rect r = new Rect(panel.x + 24f + i * (slot + gap), panel.y + 68f, slot, slot);
                DrawPizzaInventorySlot(r, PizzaDragSourceInventory, i, GetInventorySlot(i));
            }

            GUI.Label(new Rect(panel.x + 24f, panel.y + 150f, 260f, 24f), "Chest");
            for (int i = 0; i < openPizzaChest.Slots.Length; i++)
            {
                int col = i % 6;
                int row = i / 6;
                Rect r = new Rect(panel.x + 24f + col * (slot + gap), panel.y + 184f + row * (slot + gap), slot, slot);
                DrawPizzaInventorySlot(r, PizzaDragSourceChest, i, openPizzaChest.GetSlot(i));
            }

            if (pizzaDraggedItem != MiniVanInventoryItem.None)
            {
                Rect dragRect = new Rect(Event.current.mousePosition.x + 12f, Event.current.mousePosition.y + 12f, 72f, 26f);
                GUI.Box(dragRect, GetInventoryLabel(pizzaDraggedItem));
            }

            HandlePizzaChestDragReleaseOutsideSlots();
        }

        private void DrawPizzaInventorySlot(Rect rect, int source, int index, MiniVanInventoryItem item)
        {
            DrawSolidRect(rect, new Color(0.12f, 0.13f, 0.14f, 0.96f));
            GUI.Box(rect, GetInventoryLabel(item));

            Event e = Event.current;
            if (!rect.Contains(e.mousePosition))
            {
                return;
            }

            if (e.type == EventType.MouseDown && e.button == 0 && pizzaDraggedItem == MiniVanInventoryItem.None && item != MiniVanInventoryItem.None)
            {
                pizzaDraggedItem = item;
                pizzaDraggedSource = source;
                pizzaDraggedIndex = index;
                SetPizzaSlot(source, index, MiniVanInventoryItem.None);
                e.Use();
            }
            else if (e.type == EventType.MouseUp && e.button == 0 && pizzaDraggedItem != MiniVanInventoryItem.None)
            {
                MiniVanInventoryItem target = item;
                SetPizzaSlot(source, index, pizzaDraggedItem);
                if (target != MiniVanInventoryItem.None)
                {
                    SetPizzaSlot(pizzaDraggedSource, pizzaDraggedIndex, target);
                }

                pizzaDraggedItem = MiniVanInventoryItem.None;
                pizzaDraggedSource = 0;
                pizzaDraggedIndex = -1;
                e.Use();
            }
        }

        private void SetPizzaSlot(int source, int index, MiniVanInventoryItem item)
        {
            if (source == PizzaDragSourceInventory)
            {
                SetInventorySlotNetworked(index, item);
            }
            else if (source == PizzaDragSourceChest && openPizzaChest != null)
            {
                SetPizzaChestSlotNetworked(openPizzaChest.name, index, item);
            }
        }

        private void SetPizzaChestSlotNetworked(string chestName, int index, MiniVanInventoryItem item)
        {
            SetPizzaChestSlotLocal(chestName, index, item);
            if (IsServer)
            {
                SetPizzaChestSlotClientRpc(chestName, index, (int)item);
            }
            else
            {
                SetPizzaChestSlotServerRpc(chestName, index, (int)item);
            }
        }

        [ServerRpc]
        private void SetPizzaChestSlotServerRpc(string chestName, int index, int itemValue, ServerRpcParams rpcParams = default)
        {
            SetPizzaChestSlotClientRpc(chestName, index, itemValue);
        }

        [ClientRpc]
        private void SetPizzaChestSlotClientRpc(string chestName, int index, int itemValue)
        {
            SetPizzaChestSlotLocal(chestName, index, (MiniVanInventoryItem)itemValue);
        }

        private void SetPizzaChestSlotLocal(string chestName, int index, MiniVanInventoryItem item)
        {
            MiniVanPizzaChest[] chests = FindObjectsByType<MiniVanPizzaChest>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (MiniVanPizzaChest chest in chests)
            {
                if (chest != null && chest.name == chestName)
                {
                    chest.SetSlot(index, item);
                }
            }
        }

        private void HandlePizzaChestDragReleaseOutsideSlots()
        {
            Event e = Event.current;
            if (pizzaDraggedItem == MiniVanInventoryItem.None ||
                e.type != EventType.MouseUp ||
                e.button != 0)
            {
                return;
            }

            MiniVanInventoryItem dragged = pizzaDraggedItem;
            int source = pizzaDraggedSource;
            int index = pizzaDraggedIndex;
            pizzaDraggedItem = MiniVanInventoryItem.None;
            pizzaDraggedSource = 0;
            pizzaDraggedIndex = -1;

            if (source == PizzaDragSourceInventory)
            {
                SetPizzaSlot(source, index, dragged);
                if (!TryDropInventorySlot(index))
                {
                    SetPizzaSlot(source, index, dragged);
                }
            }
            else
            {
                SetPizzaSlot(source, index, dragged);
            }

            e.Use();
        }

        private void OpenPizzaChest(MiniVanPizzaChest chest)
        {
            openPizzaChest = chest;
            pizzaDraggedItem = MiniVanInventoryItem.None;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        private void ClosePizzaChest()
        {
            if (pizzaDraggedItem != MiniVanInventoryItem.None)
            {
                SetPizzaSlot(pizzaDraggedSource, pizzaDraggedIndex, pizzaDraggedItem);
            }

            pizzaDraggedItem = MiniVanInventoryItem.None;
            pizzaDraggedSource = 0;
            pizzaDraggedIndex = -1;
            openPizzaChest = null;
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }

        private bool IsPizzaChestOpen()
        {
            return openPizzaChest != null;
        }

        private void TryPickupPizzaItem(MiniVanPizzaItem item)
        {
            if (item == null || !item.CanPickup(transform.position))
            {
                return;
            }

            int slot = FindFirstEmptyInventorySlot();
            if (slot < 0)
            {
                SetPizzaStatus("Inventory full");
                return;
            }

            SetInventorySlotNetworked(slot, item.Item);
            HidePizzaPickupNetworked(item.name);
            SetPizzaStatus(GetInventoryLabel(item.Item) + " picked");
        }

        private void HidePizzaPickupNetworked(string itemName)
        {
            if (IsServer)
            {
                HidePizzaPickupClientRpc(itemName);
            }
            else
            {
                HidePizzaPickupServerRpc(itemName);
                HidePizzaPickupByName(itemName);
            }
        }

        [ServerRpc]
        private void HidePizzaPickupServerRpc(string itemName, ServerRpcParams rpcParams = default)
        {
            HidePizzaPickupClientRpc(itemName);
        }

        [ClientRpc]
        private void HidePizzaPickupClientRpc(string itemName)
        {
            HidePizzaPickupByName(itemName);
        }

        private void HidePizzaPickupByName(string itemName)
        {
            MiniVanPizzaItem[] items = FindObjectsByType<MiniVanPizzaItem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (MiniVanPizzaItem pizzaItem in items)
            {
                if (pizzaItem != null && pizzaItem.name == itemName)
                {
                    pizzaItem.SetOutlined(false);
                    pizzaItem.gameObject.SetActive(false);
                }
            }
        }

        private void TryPlaceOrTakePizzaStation(MiniVanPizzaStation station)
        {
            MiniVanInventoryItem selected = GetInventorySlot(localSelectedSlot);
            if (station.ActiveItem != MiniVanInventoryItem.None)
            {
                if (selected != MiniVanInventoryItem.None)
                {
                    SetPizzaStatus("Use LMB to add item");
                    return;
                }

                if (!station.TryTakeItem(out MiniVanInventoryItem taken, out string takeStatus))
                {
                    SetPizzaStatus(takeStatus);
                    return;
                }

                SetInventorySlotNetworked(localSelectedSlot, taken);
                SetPizzaStatus(takeStatus);
                return;
            }

            if (selected == MiniVanInventoryItem.None)
            {
                SetPizzaStatus("Select ingredient");
                return;
            }

            if (!station.TryPlaceItem(selected, out bool consumed, out string status))
            {
                SetPizzaStatus(status);
                return;
            }

            if (consumed)
            {
                SetInventorySlotNetworked(localSelectedSlot, MiniVanInventoryItem.None);
            }

            SetPizzaStatus(status);
        }

        private void TryUsePizzaStation(MiniVanPizzaStation station)
        {
            MiniVanInventoryItem selected = GetInventorySlot(localSelectedSlot);
            if (!station.TryUseItem(selected, out bool consumed, out string status))
            {
                SetPizzaStatus(status ?? "Wrong item");
                return;
            }

            if (consumed)
            {
                SetInventorySlotNetworked(localSelectedSlot, MiniVanInventoryItem.None);
            }

            SetPizzaStatus(status);
        }

        private void TryUsePizzaOven(MiniVanPizzaOven oven)
        {
            MiniVanInventoryItem selected = GetInventorySlot(localSelectedSlot);
            if (!oven.HasPizza)
            {
                if (selected == MiniVanInventoryItem.RawPizza && oven.TryInsert(selected))
                {
                    SetInventorySlotNetworked(localSelectedSlot, MiniVanInventoryItem.None);
                    SetPizzaStatus("Pizza cooking");
                }
                else
                {
                    SetPizzaStatus("Need raw pizza");
                }

                return;
            }

            MiniVanInventoryItem result = oven.TryTake();
            if (result == MiniVanInventoryItem.None)
            {
                return;
            }

            int slot = selected == MiniVanInventoryItem.None ? localSelectedSlot : FindFirstEmptyInventorySlot();
            if (slot < 0)
            {
                SetPizzaStatus("Inventory full");
                return;
            }

            SetInventorySlotNetworked(slot, result);
            SetPizzaStatus(result == MiniVanInventoryItem.BurnedPizza ? "Pizza burned" : result == MiniVanInventoryItem.CookedPizza ? "Pizza ready" : "Still raw");
        }

        private void TryUsePizzaBoxStation(MiniVanPizzaBoxStation station)
        {
            MiniVanInventoryItem selected = GetInventorySlot(localSelectedSlot);
            bool selectedPizza = selected == MiniVanInventoryItem.CookedPizza || selected == MiniVanInventoryItem.BurnedPizza;
            if (!selectedPizza)
            {
                SetPizzaStatus("Hold cooked pizza");
                return;
            }

            if (!station.TryUseBox())
            {
                SetPizzaStatus("No box here");
                return;
            }

            SetInventorySlotNetworked(localSelectedSlot, MiniVanInventoryItem.BoxedPizza);
            SetPizzaStatus("Pizza boxed");
        }

        private void TryUsePizzaToolShelf(MiniVanPizzaToolShelf shelf)
        {
            if (shelf == null || !shelf.IsInRange(transform.position))
            {
                return;
            }

            MiniVanInventoryItem selected = GetInventorySlot(localSelectedSlot);
            if (shelf.StoredItem == MiniVanInventoryItem.None)
            {
                if (!shelf.CanStore(selected))
                {
                    SetPizzaStatus("Hold pin or grater");
                    return;
                }

                SetPizzaShelfItemNetworked(shelf.name, shelf.transform.position, selected);
                SetInventorySlotNetworked(localSelectedSlot, MiniVanInventoryItem.None);
                SetPizzaStatus(GetInventoryLabel(selected) + " shelved");
                return;
            }

            if (selected != MiniVanInventoryItem.None)
            {
                SetPizzaStatus("Select empty slot");
                return;
            }

            MiniVanInventoryItem taken = shelf.StoredItem;
            SetPizzaShelfItemNetworked(shelf.name, shelf.transform.position, MiniVanInventoryItem.None);
            SetInventorySlotNetworked(localSelectedSlot, taken);
            SetPizzaStatus(GetInventoryLabel(taken) + " taken");
        }

        private bool TryBoxPizzaWithLookedAtBox(MiniVanPizzaItem boxItem)
        {
            if (boxItem == null || boxItem.Item != MiniVanInventoryItem.PizzaBox || !boxItem.CanPickup(transform.position))
            {
                return false;
            }

            MiniVanInventoryItem selected = GetInventorySlot(localSelectedSlot);
            if (selected != MiniVanInventoryItem.CookedPizza && selected != MiniVanInventoryItem.BurnedPizza)
            {
                return false;
            }

            SetInventorySlotNetworked(localSelectedSlot, MiniVanInventoryItem.BoxedPizza);
            HidePizzaPickupNetworked(boxItem.name);
            SetPizzaStatus("Pizza boxed");
            return true;
        }

        private void SetInventorySlotNetworked(int slotIndex, MiniVanInventoryItem item)
        {
            PredictInventorySlot(slotIndex, item);
            if (IsServer)
            {
                SetInventorySlot(slotIndex, item);
            }
            else
            {
                RequestSetPizzaInventorySlotServerRpc(slotIndex, (int)item);
            }
        }

        [ServerRpc]
        private void RequestSetPizzaInventorySlotServerRpc(int slotIndex, int itemValue, ServerRpcParams rpcParams = default)
        {
            SetInventorySlot(slotIndex, (MiniVanInventoryItem)itemValue);
        }

        private MiniVanPizzaItem FindLookedAtPizzaItem()
        {
            return FindLookedAtPizzaComponent<MiniVanPizzaItem>(2.6f);
        }

        private MiniVanPizzaChest FindLookedAtPizzaChest()
        {
            return FindLookedAtPizzaComponent<MiniVanPizzaChest>(2.8f);
        }

        private MiniVanPizzaStation FindLookedAtPizzaStation()
        {
            return FindLookedAtPizzaComponent<MiniVanPizzaStation>(2.8f);
        }

        private MiniVanPizzaOven FindLookedAtPizzaOven()
        {
            return FindLookedAtPizzaComponent<MiniVanPizzaOven>(2.8f);
        }

        private MiniVanPizzaBoxStation FindLookedAtPizzaBoxStation()
        {
            return FindLookedAtPizzaComponent<MiniVanPizzaBoxStation>(2.8f);
        }

        private MiniVanPizzaToolShelf FindLookedAtPizzaToolShelf()
        {
            return FindLookedAtPizzaComponent<MiniVanPizzaToolShelf>(2.8f);
        }

        private T FindLookedAtPizzaComponent<T>(float radius) where T : Component
        {
            if (PlayerCamera == null)
            {
                return null;
            }

            Ray ray = new Ray(PlayerCamera.transform.position, PlayerCamera.transform.forward);
            // Thick aim while riding: third-person cam can't precisely hit small items.
            RaycastHit[] hits = IsRidingBoard
                ? Physics.SphereCastAll(ray, 0.6f, InteractDistance, ~0, QueryTriggerInteraction.Collide)
                : Physics.RaycastAll(ray, InteractDistance, ~0, QueryTriggerInteraction.Collide);
            float bestDistance = float.MaxValue;
            T best = null;

            for (int i = 0; i < hits.Length; i++)
            {
                if (ShouldIgnoreAimCollider(hits[i].collider))
                {
                    continue;
                }

                T component = hits[i].collider.GetComponentInParent<T>();
                if (component == null)
                {
                    continue;
                }

                float distance = Vector3.Distance(transform.position, component.transform.position);
                if (distance <= radius && hits[i].distance < bestDistance)
                {
                    bestDistance = hits[i].distance;
                    best = component;
                }
            }

            return best;
        }

        private string GetStationPrompt(MiniVanPizzaStation station)
        {
            if (station == null)
            {
                return null;
            }

            switch (station.Kind)
            {
                case MiniVanPizzaStationKind.CuttingBoard:
                    return "E - place/take, LMB - cut";
                case MiniVanPizzaStationKind.Grater:
                    return "E - place/take, LMB - grate";
                default:
                    return "E - place/take, LMB - add";
            }
        }

        private void SetPizzaShelfItemNetworked(string shelfName, Vector3 shelfPosition, MiniVanInventoryItem item)
        {
            if (IsServer)
            {
                SetPizzaShelfItemClientRpc(shelfName, shelfPosition, (int)item);
            }
            else
            {
                SetPizzaShelfItemServerRpc(shelfName, shelfPosition, (int)item);
            }
        }

        [ServerRpc]
        private void SetPizzaShelfItemServerRpc(string shelfName, Vector3 shelfPosition, int itemValue, ServerRpcParams rpcParams = default)
        {
            SetPizzaShelfItemClientRpc(shelfName, shelfPosition, itemValue);
        }

        [ClientRpc]
        private void SetPizzaShelfItemClientRpc(string shelfName, Vector3 shelfPosition, int itemValue)
        {
            SetPizzaShelfItemLocal(shelfName, shelfPosition, (MiniVanInventoryItem)itemValue);
        }

        private void SetPizzaShelfItemLocal(string shelfName, Vector3 shelfPosition, MiniVanInventoryItem item)
        {
            MiniVanPizzaToolShelf[] shelves = FindObjectsByType<MiniVanPizzaToolShelf>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            MiniVanPizzaToolShelf best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < shelves.Length; i++)
            {
                if (shelves[i] != null && shelves[i].name == shelfName)
                {
                    float distance = (shelves[i].transform.position - shelfPosition).sqrMagnitude;
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        best = shelves[i];
                    }
                }
            }

            if (best != null && bestDistance <= 4f)
            {
                best.SetStoredItem(item);
            }
        }

        private bool TryDropSelectedPizzaItem()
        {
            return TryDropPizzaItemFromSlot(localSelectedSlot);
        }

        private bool TryDropPizzaItemFromSlot(int slot)
        {
            if (slot < 0 || slot > 3)
            {
                return false;
            }

            MiniVanInventoryItem item = GetInventorySlot(slot);
            if (!IsPizzaLoopItem(item))
            {
                return false;
            }

            Vector3 forward = CameraRoot != null ? CameraRoot.forward : transform.forward;
            Vector3 dropPosition = transform.position + forward.normalized * 1.05f + Vector3.up * 0.65f;
            Quaternion dropRotation = Quaternion.LookRotation(new Vector3(forward.x, 0f, forward.z).sqrMagnitude > 0.001f ? new Vector3(forward.x, 0f, forward.z).normalized : transform.forward, Vector3.up);
            string dropName = "PizzaDrop_" + item + "_" + NetworkObjectId + "_" + Time.frameCount;
            MiniVanVehicle interiorVehicle = FindInteriorVehicle();
            Rigidbody vehicleBody = interiorVehicle != null ? interiorVehicle.GetComponent<Rigidbody>() : null;
            bool cabinCargo = MiniVanFuelRules.IsZombiePart(item) && vehicleBody != null;
            Vector3 inheritedVelocity = cabinCargo ? vehicleBody.GetPointVelocity(dropPosition) : Vector3.zero;
            Vector3 inheritedAngularVelocity = cabinCargo ? vehicleBody.angularVelocity : Vector3.zero;

            SetInventorySlotNetworked(slot, MiniVanInventoryItem.None);
            if (!IsServer)
            {
                SpawnPizzaPickupLocal(item, dropPosition, dropRotation, dropName, cabinCargo,
                    inheritedVelocity, inheritedAngularVelocity);
            }

            DropPizzaItemNetworked((int)item, dropPosition, dropRotation, dropName, cabinCargo,
                inheritedVelocity, inheritedAngularVelocity);
            SetPizzaStatus(GetInventoryLabel(item) + " dropped");
            return true;
        }

        private void DropPizzaItemNetworked(int itemValue, Vector3 position, Quaternion rotation, string dropName,
            bool cabinCargo, Vector3 inheritedVelocity, Vector3 inheritedAngularVelocity)
        {
            if (IsServer)
            {
                SpawnPizzaPickupClientRpc(itemValue, position, rotation, dropName, cabinCargo,
                    inheritedVelocity, inheritedAngularVelocity);
            }
            else
            {
                DropPizzaItemServerRpc(itemValue, position, rotation, dropName, cabinCargo,
                    inheritedVelocity, inheritedAngularVelocity);
            }
        }

        [ServerRpc]
        private void DropPizzaItemServerRpc(int itemValue, Vector3 position, Quaternion rotation, string dropName,
            bool cabinCargo, Vector3 inheritedVelocity, Vector3 inheritedAngularVelocity,
            ServerRpcParams rpcParams = default)
        {
            SpawnPizzaPickupClientRpc(itemValue, position, rotation, dropName, cabinCargo,
                inheritedVelocity, inheritedAngularVelocity);
        }

        [ClientRpc]
        private void SpawnPizzaPickupClientRpc(int itemValue, Vector3 position, Quaternion rotation, string dropName,
            bool cabinCargo, Vector3 inheritedVelocity, Vector3 inheritedAngularVelocity)
        {
            SpawnPizzaPickupLocal((MiniVanInventoryItem)itemValue, position, rotation, dropName, cabinCargo,
                inheritedVelocity, inheritedAngularVelocity);
        }

        private void SpawnPizzaPickupLocal(MiniVanInventoryItem item, Vector3 position, Quaternion rotation,
            string dropName, bool cabinCargo, Vector3 inheritedVelocity, Vector3 inheritedAngularVelocity)
        {
            if (!string.IsNullOrEmpty(dropName))
            {
                GameObject existing = GameObject.Find(dropName);
                if (existing != null)
                {
                    return;
                }
            }

            GameObject prefab = Resources.Load<GameObject>(GetPizzaItemResourcePath(item));
            GameObject pickup = prefab != null ? Instantiate(prefab, position, rotation) : CreatePizzaFallbackPickup(item, position, rotation);
            pickup.name = dropName;
            MiniVanPizzaItem pizzaItem = pickup.GetComponent<MiniVanPizzaItem>();
            if (pizzaItem == null)
            {
                pizzaItem = pickup.AddComponent<MiniVanPizzaItem>();
            }

            pizzaItem.enabled = true;
            pizzaItem.Item = item;
            pizzaItem.Type = GetPizzaItemType(item);
            pizzaItem.PickupRadius = 2.0f;
            pizzaItem.CanHoldInHands = true;
            pizzaItem.CanPutInInventory = true;

            foreach (Collider collider in pickup.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = true;
                collider.isTrigger = false;
            }

            MakePizzaPickupPhysical(pickup, item);
            if (MiniVanFuelRules.IsZombiePart(item))
            {
                MiniVanZombiePartPhysics partPhysics = pickup.GetComponent<MiniVanZombiePartPhysics>();
                if (partPhysics == null)
                {
                    partPhysics = pickup.AddComponent<MiniVanZombiePartPhysics>();
                }

                if (cabinCargo)
                {
                    partPhysics.ConfigureCabinCargo(inheritedVelocity, inheritedAngularVelocity);
                }
                else
                {
                    partPhysics.ConfigureRoadDebris();
                }
            }
        }

        private static void MakePizzaPickupPhysical(GameObject pickup, MiniVanInventoryItem item)
        {
            Rigidbody body = pickup.GetComponent<Rigidbody>();
            if (body == null)
            {
                body = pickup.AddComponent<Rigidbody>();
            }

            body.isKinematic = false;
            body.useGravity = true;
            body.detectCollisions = true;
            body.mass = GetPizzaDropMass(item);
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.linearDamping = 0.12f;
            body.angularDamping = 0.45f;
            body.maxLinearVelocity = MiniVanFuelRules.IsZombiePart(item) ? 90f : 22f;
        }

        private GameObject CreatePizzaFallbackPickup(MiniVanInventoryItem item, Vector3 position, Quaternion rotation)
        {
            if (MiniVanFuelRules.IsZombiePart(item))
            {
                GameObject part = MiniVanFuelPartFactory.CreateVisual(item, null, true);
                part.transform.SetPositionAndRotation(position, rotation);
                return part;
            }

            if (item == MiniVanInventoryItem.AcidClot)
            {
                GameObject clot = MiniVanAcidClotVisual.Create(null, true);
                clot.transform.SetPositionAndRotation(position, rotation);
                return clot;
            }

            if (item == MiniVanInventoryItem.TraceHead)
            {
                GameObject head = MiniVanTraceHeadVisual.Create(null, true);
                head.transform.SetPositionAndRotation(position, rotation);
                return head;
            }

            GameObject pickup = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pickup.transform.SetPositionAndRotation(position, rotation);
            Renderer renderer = pickup.GetComponent<Renderer>();
            if (renderer != null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null)
                {
                    shader = Shader.Find("Standard");
                }

                renderer.material = new Material(shader);
                ApplyPizzaMaterialColor(renderer.material, GetPizzaItemColor(item));
            }

            return pickup;
        }

        private static string GetPizzaItemResourcePath(MiniVanInventoryItem item)
        {
            return "PizzaLoop/PizzaItem_" + item;
        }

        private static MiniVanPizzaItemType GetPizzaItemType(MiniVanInventoryItem item)
        {
            switch (item)
            {
                case MiniVanInventoryItem.Flour:
                case MiniVanInventoryItem.Water:
                case MiniVanInventoryItem.TomatoPaste:
                case MiniVanInventoryItem.Cheese:
                case MiniVanInventoryItem.Sausage:
                    return MiniVanPizzaItemType.Ingredient;
                case MiniVanInventoryItem.RollingPin:
                case MiniVanInventoryItem.Grater:
                    return MiniVanPizzaItemType.Tool;
                case MiniVanInventoryItem.CookedPizza:
                case MiniVanInventoryItem.BurnedPizza:
                case MiniVanInventoryItem.BoxedPizza:
                    return MiniVanPizzaItemType.Pizza;
                default:
                    return MiniVanPizzaItemType.PizzaPart;
            }
        }

        private static float GetPizzaDropMass(MiniVanInventoryItem item)
        {
            switch (item)
            {
                case MiniVanInventoryItem.PizzaBox:
                case MiniVanInventoryItem.BoxedPizza:
                    return 0.45f;
                case MiniVanInventoryItem.RollingPin:
                case MiniVanInventoryItem.Grater:
                    return 0.35f;
                case MiniVanInventoryItem.RawPizza:
                case MiniVanInventoryItem.CookedPizza:
                case MiniVanInventoryItem.BurnedPizza:
                    return 0.28f;
                case MiniVanInventoryItem.ZombieTorso:
                case MiniVanInventoryItem.ZombieArm:
                case MiniVanInventoryItem.ZombieLeg:
                case MiniVanInventoryItem.ZombieHead:
                    return 1.2f;
                case MiniVanInventoryItem.AcidClot:
                    return 0.85f;
                case MiniVanInventoryItem.TraceHead:
                    return 0.9f;
                default:
                    return 0.22f;
            }
        }

        private static void ApplyPizzaMaterialColor(Material material, Color color)
        {
            if (material == null)
            {
                return;
            }

            material.color = color;
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }
        }

        private void SetPizzaStatus(string text)
        {
            pizzaStatusText = text;
            pizzaStatusUntil = Time.time + 2.2f;
        }

        private static bool IsPizzaLoopItem(MiniVanInventoryItem item)
        {
            return ((int)item >= 20 && (int)item <= 35) ||
                   MiniVanFuelRules.IsZombiePart(item) ||
                   item == MiniVanInventoryItem.AcidClot ||
                   item == MiniVanInventoryItem.TraceHead;
        }

        private static string GetInventoryLabel(MiniVanInventoryItem item)
        {
            switch (item)
            {
                case MiniVanInventoryItem.Bat: return "BAT";
                case MiniVanInventoryItem.Coffee: return "COF";
                case MiniVanInventoryItem.Skateboard: return "SK8";
                case MiniVanInventoryItem.HotPotatoBomb: return "BOMB";
                case MiniVanInventoryItem.HoverboardM: return "HOV";
                case MiniVanInventoryItem.PanelkaKey: return "KEY";
                case MiniVanInventoryItem.Winch: return "WINCH";
                case MiniVanInventoryItem.Flamethrower: return "FLAME";
                case MiniVanInventoryItem.FireExtinguisher: return "EXT";
                case MiniVanInventoryItem.Defibrillator: return "DEFIB";
                case MiniVanInventoryItem.HolyCross: return "CROSS";
                case MiniVanInventoryItem.AspenStake: return "STAKE";
                case MiniVanInventoryItem.SnesConsole: return "SNES";
                case MiniVanInventoryItem.SnesCartridge: return "CART";
                case MiniVanInventoryItem.Stretcher: return "STRET";
                case MiniVanInventoryItem.AntonLocator: return "LOC";
                case MiniVanInventoryItem.Flour: return "FLOUR";
                case MiniVanInventoryItem.Water: return "WATER";
                case MiniVanInventoryItem.TomatoPaste: return "TOM";
                case MiniVanInventoryItem.Cheese: return "CHEESE";
                case MiniVanInventoryItem.Sausage: return "SAUS";
                case MiniVanInventoryItem.RollingPin: return "PIN";
                case MiniVanInventoryItem.Grater: return "GRATER";
                case MiniVanInventoryItem.Dough: return "DOUGH";
                case MiniVanInventoryItem.RoundDough: return "ROUND";
                case MiniVanInventoryItem.GratedCheese: return "G-CHE";
                case MiniVanInventoryItem.SlicedSausage: return "S-SAUS";
                case MiniVanInventoryItem.RawPizza: return "RAW";
                case MiniVanInventoryItem.CookedPizza: return "PIZZA";
                case MiniVanInventoryItem.BurnedPizza: return "BURN";
                case MiniVanInventoryItem.PizzaBox: return "BOX";
                case MiniVanInventoryItem.BoxedPizza: return "BOXED";
                case MiniVanInventoryItem.ZombieTorso: return "TORSO";
                case MiniVanInventoryItem.ZombieArm: return "ARM";
                case MiniVanInventoryItem.ZombieLeg: return "LEG";
                case MiniVanInventoryItem.ZombieHead: return "HEAD";
                case MiniVanInventoryItem.AcidClot: return "ACID";
                case MiniVanInventoryItem.TraceHead: return "SKULL";
                case MiniVanInventoryItem.TestHat: return "HAT";
                case MiniVanInventoryItem.ZoroBandana: return "ZORO";
                case MiniVanInventoryItem.StrawHat: return "STRAW";
                case MiniVanInventoryItem.ChopperHat: return "CHOP";
                case MiniVanInventoryItem.AshCap: return "ASH";
                case MiniVanInventoryItem.NarutoHeadband: return "LEAF";
                case MiniVanInventoryItem.LawHat: return "LAW";
                case MiniVanInventoryItem.GokuHair: return "GOKU";
                case MiniVanInventoryItem.SuperSaiyanHair: return "SSJ";
                case MiniVanInventoryItem.MarioCap: return "MARIO";
                case MiniVanInventoryItem.VikingHelmet: return "VIKING";
                case MiniVanInventoryItem.PirateTricorn: return "PIRATE";
                default: return "";
            }
        }

        private static Material CreateHeldVisualMaterial(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            Material material = new Material(shader);
            material.color = color;
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            return material;
        }

        private static void SetHeldVisualMaterial(GameObject target, Material material)
        {
            Renderer renderer = target != null ? target.GetComponent<Renderer>() : null;
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
            }

            Collider collider = target != null ? target.GetComponent<Collider>() : null;
            if (collider != null)
            {
                Object.Destroy(collider);
            }
        }

        private static Color GetPizzaItemColor(MiniVanInventoryItem item)
        {
            switch (item)
            {
                case MiniVanInventoryItem.Flour: return new Color(0.92f, 0.88f, 0.76f);
                case MiniVanInventoryItem.Water: return new Color(0.25f, 0.58f, 1f);
                case MiniVanInventoryItem.TomatoPaste: return new Color(0.75f, 0.06f, 0.04f);
                case MiniVanInventoryItem.Cheese:
                case MiniVanInventoryItem.GratedCheese: return new Color(1f, 0.82f, 0.18f);
                case MiniVanInventoryItem.Sausage:
                case MiniVanInventoryItem.SlicedSausage: return new Color(0.62f, 0.18f, 0.13f);
                case MiniVanInventoryItem.RollingPin: return new Color(0.55f, 0.32f, 0.16f);
                case MiniVanInventoryItem.Grater: return new Color(0.72f, 0.74f, 0.76f);
                case MiniVanInventoryItem.Dough:
                case MiniVanInventoryItem.RoundDough: return new Color(0.9f, 0.72f, 0.46f);
                case MiniVanInventoryItem.RawPizza: return new Color(0.92f, 0.54f, 0.26f);
                case MiniVanInventoryItem.ZombieTorso:
                case MiniVanInventoryItem.ZombieArm:
                case MiniVanInventoryItem.ZombieLeg:
                case MiniVanInventoryItem.ZombieHead:
                    return new Color(0.34f, 0.46f, 0.24f);
                case MiniVanInventoryItem.AcidClot:
                    return new Color(0.55f, 0.82f, 0.08f);
                case MiniVanInventoryItem.TraceHead:
                    return new Color(0.76f, 0.66f, 0.46f);
                case MiniVanInventoryItem.CookedPizza:
                case MiniVanInventoryItem.BoxedPizza: return new Color(0.96f, 0.58f, 0.2f);
                case MiniVanInventoryItem.BurnedPizza: return new Color(0.12f, 0.08f, 0.05f);
                case MiniVanInventoryItem.PizzaBox: return new Color(0.78f, 0.78f, 0.72f);
                default: return Color.white;
            }
        }
    }
}
