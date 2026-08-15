using System;
using UnityEngine;

namespace MiniVanGame
{
    public static class MiniVanPanelkaFurnitureInteractionUtility
    {
        public static void Ensure(Transform root, string canonicalName)
        {
            if (root == null || string.IsNullOrEmpty(canonicalName))
            {
                return;
            }

            if (canonicalName == "Soviet_Stove")
            {
                EnsureOvenDoor(root);
                return;
            }

            if (!IsCabinetFamily(canonicalName))
            {
                return;
            }

            if (canonicalName == "Kitchen_Cabinet_Run")
            {
                EnsureKitchenFacades(root);
            }

            Transform leftPivot = null;
            Transform rightPivot = null;
            if (canonicalName == "Rounded_Soviet_Fridge")
            {
                Transform upperDoor = Find(root, "Upper_Door");
                leftPivot = EnsureVerticalHinge(
                    root, upperDoor, true, "Door_Runtime_Pivot_Fridge");
                Attach(leftPivot, Find(root, "Lower_Door"));
                Attach(leftPivot, Find(root, "Handle"));
            }
            else
            {
                leftPivot = EnsureVerticalHinge(
                    root, Find(root, "Door_L"), true, "Door_Runtime_Pivot_L");
                rightPivot = EnsureVerticalHinge(
                    root, Find(root, "Door_R"), false, "Door_Runtime_Pivot_R");
                Attach(leftPivot, Find(root, "Handle_L"));
                Attach(rightPivot, Find(root, "Handle_R"));
            }

            Transform[] drawers = FindDrawers(root);
            if (leftPivot == null && rightPivot == null && drawers.Length == 0)
            {
                return;
            }

            MiniVanPanelkaCabinet cabinet = root.GetComponent<MiniVanPanelkaCabinet>();
            if (cabinet == null)
            {
                cabinet = root.gameObject.AddComponent<MiniVanPanelkaCabinet>();
            }

            cabinet.Configure(canonicalName, leftPivot, rightPivot, null, drawers, 0.28f);

            MiniVanPanelkaFurnitureInteractable interactable =
                root.GetComponent<MiniVanPanelkaFurnitureInteractable>();
            if (interactable == null)
            {
                interactable =
                    root.gameObject.AddComponent<MiniVanPanelkaFurnitureInteractable>();
            }

            interactable.Type = MiniVanPanelkaInteractableType.Cabinet;
            interactable.Message = "E - open / close";
        }

        private static bool IsCabinetFamily(string name)
        {
            return name == "FullHeight_Double_Wardrobe" ||
                   name == "Hall_FullHeight_Wardrobe" ||
                   name == "Wardrobe" ||
                   name == "Soviet_Wall_Unit" ||
                   name == "Rounded_Soviet_Fridge" ||
                   name == "Kitchen_Cabinet_Run";
        }

        private static Transform EnsureVerticalHinge(
            Transform root, Transform door, bool hingeOnLeft, string pivotName)
        {
            Transform existing = Find(root, pivotName);
            if (existing != null)
            {
                return existing;
            }

            if (door == null || door.parent == null)
            {
                return null;
            }

            Transform parent = door.parent;
            Bounds bounds;
            if (!TryGetBoundsIn(door, parent, out bounds))
            {
                return null;
            }

            GameObject pivotObject = new GameObject(pivotName);
            Transform pivot = pivotObject.transform;
            pivot.SetParent(parent, false);
            pivot.localPosition = new Vector3(
                hingeOnLeft ? bounds.min.x : bounds.max.x,
                bounds.center.y,
                bounds.center.z);
            pivot.localRotation = Quaternion.identity;
            Attach(pivot, door);
            return pivot;
        }

        private static void EnsureOvenDoor(Transform root)
        {
            Transform door = Find(root, "Oven_Door");
            if (door == null || door.parent == null)
            {
                return;
            }

            Transform pivot = Find(root, "Oven_Door_Runtime_Pivot");
            if (pivot == null)
            {
                Bounds bounds;
                if (!TryGetBoundsIn(door, door.parent, out bounds))
                {
                    return;
                }

                GameObject pivotObject = new GameObject("Oven_Door_Runtime_Pivot");
                pivot = pivotObject.transform;
                pivot.SetParent(door.parent, false);
                pivot.localPosition = new Vector3(
                    bounds.center.x, bounds.min.y, bounds.center.z);
                Attach(pivot, door);
            }

            if (door.GetComponent<Collider>() == null)
            {
                door.gameObject.AddComponent<BoxCollider>();
            }

            MiniVanPanelkaFurnitureInteractable interactable =
                pivot.GetComponent<MiniVanPanelkaFurnitureInteractable>();
            if (interactable == null)
            {
                interactable =
                    pivot.gameObject.AddComponent<MiniVanPanelkaFurnitureInteractable>();
            }

            interactable.Type = MiniVanPanelkaInteractableType.Door;
            interactable.Pivot = pivot;
            interactable.ClosedEuler = Vector3.zero;
            interactable.OpenEuler = new Vector3(-78f, 0f, 0f);
            interactable.Message = "E - open / close";
        }

        private static void EnsureKitchenFacades(Transform root)
        {
            if (Find(root, "Door_L") != null)
            {
                return;
            }

            Transform lower = Find(root, "Lower_Cabinets");
            Bounds bounds;
            if (lower == null || !TryGetBoundsIn(lower, root, out bounds))
            {
                return;
            }

            Renderer sourceRenderer = lower.GetComponentInChildren<Renderer>(true);
            Material material = sourceRenderer != null ? sourceRenderer.sharedMaterial : null;
            float doorWidth = Mathf.Max(0.18f, (bounds.size.x - 0.06f) * 0.5f);
            float frontZ = bounds.min.z - 0.022f;
            CreatePanel(root, "Door_L",
                new Vector3(bounds.center.x - doorWidth * 0.5f - 0.008f,
                    bounds.min.y + bounds.size.y * 0.43f, frontZ),
                new Vector3(doorWidth, bounds.size.y * 0.62f, 0.035f), material, false);
            CreatePanel(root, "Door_R",
                new Vector3(bounds.center.x + doorWidth * 0.5f + 0.008f,
                    bounds.min.y + bounds.size.y * 0.43f, frontZ),
                new Vector3(doorWidth, bounds.size.y * 0.62f, 0.035f), material, false);
            CreatePanel(root, "Drawer_01",
                new Vector3(bounds.center.x - bounds.size.x * 0.25f,
                    bounds.max.y - 0.10f, frontZ - 0.006f),
                new Vector3(bounds.size.x * 0.46f, 0.15f, 0.04f), material, false);
            CreatePanel(root, "Drawer_02",
                new Vector3(bounds.center.x + bounds.size.x * 0.25f,
                    bounds.max.y - 0.10f, frontZ - 0.006f),
                new Vector3(bounds.size.x * 0.46f, 0.15f, 0.04f), material, false);
        }

        private static Transform CreatePanel(
            Transform parent,
            string name,
            Vector3 position,
            Vector3 size,
            Material material,
            bool addCollider)
        {
            GameObject panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panel.name = name;
            panel.transform.SetParent(parent, false);
            panel.transform.localPosition = position;
            panel.transform.localScale = size;
            Renderer renderer = panel.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
            }

            Collider collider = panel.GetComponent<Collider>();
            if (!addCollider && collider != null)
            {
                UnityEngine.Object.DestroyImmediate(collider);
            }

            return panel.transform;
        }

        private static Transform[] FindDrawers(Transform root)
        {
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            int count = 0;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].name.StartsWith("Drawer_", StringComparison.Ordinal))
                {
                    count++;
                }
            }

            Transform[] drawers = new Transform[count];
            int index = 0;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].name.StartsWith("Drawer_", StringComparison.Ordinal))
                {
                    drawers[index++] = all[i];
                }
            }

            return drawers;
        }

        private static Transform Find(Transform root, string name)
        {
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].name == name)
                {
                    return all[i];
                }
            }

            return null;
        }

        private static void Attach(Transform parent, Transform child)
        {
            if (parent != null && child != null && child.parent != parent)
            {
                child.SetParent(parent, true);
            }
        }

        private static bool TryGetBoundsIn(
            Transform source, Transform targetSpace, out Bounds bounds)
        {
            Renderer[] renderers = source.GetComponentsInChildren<Renderer>(true);
            bounds = new Bounds(Vector3.zero, Vector3.zero);
            bool found = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                Bounds local = renderers[i].localBounds;
                Vector3 min = local.min;
                Vector3 max = local.max;
                for (int x = 0; x < 2; x++)
                {
                    for (int y = 0; y < 2; y++)
                    {
                        for (int z = 0; z < 2; z++)
                        {
                            Vector3 point = new Vector3(
                                x == 0 ? min.x : max.x,
                                y == 0 ? min.y : max.y,
                                z == 0 ? min.z : max.z);
                            point = targetSpace.InverseTransformPoint(
                                renderers[i].transform.TransformPoint(point));
                            if (!found)
                            {
                                bounds = new Bounds(point, Vector3.zero);
                                found = true;
                            }
                            else
                            {
                                bounds.Encapsulate(point);
                            }
                        }
                    }
                }
            }

            return found;
        }
    }
}
