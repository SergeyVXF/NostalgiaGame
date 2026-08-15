using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace MiniVanGame
{
    public class MiniVanPanelkaBreakableWindowBase : MonoBehaviour
    {
        public string WindowId;
        public bool IsBroken { get; private set; }

        private Renderer[] cachedRenderers;
        private Collider[] cachedColliders;
        public Transform[] PassageParts = Array.Empty<Transform>();


        private void Awake()
        {
            CacheParts();
        }

public void Configure(string windowId, params Transform[] extraPassageParts)
        {
            WindowId = windowId;
            PassageParts = extraPassageParts ?? Array.Empty<Transform>();
            CacheParts();
        }

public void BreakLocal()
        {
            if (IsBroken)
            {
                return;
            }

            IsBroken = true;
            CacheParts();
            SetPartsEnabled(cachedRenderers, false);
            SetPartsEnabled(cachedColliders, false);

            for (int i = 0; i < PassageParts.Length; i++)
            {
                Transform part = PassageParts[i];
                if (part == null)
                {
                    continue;
                }

                SetPartsEnabled(part.GetComponentsInChildren<Renderer>(true), false);
                SetPartsEnabled(part.GetComponentsInChildren<Collider>(true), false);
            }

            SpawnGlassShards();
            RetireBrokenParts();
        }

        private void RetireBrokenParts()
        {
            // Distance culling restores the renderer flags it cached before the break,
            // so the pane has to leave the hierarchy pass entirely, not just switch
            // its renderers off.
            for (int i = 0; i < PassageParts.Length; i++)
            {
                Transform part = PassageParts[i];
                if (part != null && part.gameObject != gameObject)
                {
                    part.gameObject.SetActive(false);
                }
            }

            gameObject.SetActive(false);
        }

        private void CacheParts()
        {
            cachedRenderers = GetComponentsInChildren<Renderer>(true);
            cachedColliders = GetComponentsInChildren<Collider>(true);
        }

private static void SetPartsEnabled(Renderer[] renderers, bool enabled)
        {
            if (renderers == null)
            {
                return;
            }

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    renderers[i].enabled = enabled;
                }
            }
        }

        private static void SetPartsEnabled(Collider[] colliders, bool enabled)
        {
            if (colliders == null)
            {
                return;
            }

            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                {
                    colliders[i].enabled = enabled;
                }
            }
        }


        private void SpawnGlassShards()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            Material material = cachedRenderers != null && cachedRenderers.Length > 0 && cachedRenderers[0] != null
                ? cachedRenderers[0].sharedMaterial
                : null;
            System.Random random = new System.Random((WindowId ?? name).GetHashCode());

            for (int i = 0; i < 9; i++)
            {
                GameObject shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
                shard.name = "Broken_Glass_Shard_" + i.ToString("00");
                shard.transform.position = transform.position + transform.right * ((float)random.NextDouble() - 0.5f) * 0.8f;
                shard.transform.rotation = transform.rotation * Quaternion.Euler(
                    random.Next(-25, 26),
                    random.Next(-25, 26),
                    random.Next(-45, 46));
                shard.transform.localScale = new Vector3(
                    0.05f + (float)random.NextDouble() * 0.09f,
                    0.10f + (float)random.NextDouble() * 0.18f,
                    0.018f);
                Renderer renderer = shard.GetComponent<Renderer>();
                if (renderer != null && material != null)
                {
                    renderer.sharedMaterial = material;
                }

                Collider collider = shard.GetComponent<Collider>();
                if (collider != null)
                {
                    Destroy(collider);
                }

                Rigidbody body = shard.AddComponent<Rigidbody>();
                body.mass = 0.04f;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                body.AddForce(
                    transform.forward * (0.4f + (float)random.NextDouble() * 1.1f) +
                    Vector3.down * (0.2f + (float)random.NextDouble() * 0.6f),
                    ForceMode.VelocityChange);
                Destroy(shard, 2.5f);
            }
        }
    }

    public class MiniVanPanelkaCabinetBase : MonoBehaviour
    {
        public string CabinetId;
        public Transform LeftDoorPivot;
        public Transform RightDoorPivot;
        public Transform[] DrawerSlides = Array.Empty<Transform>();
        public float OpenAngle = 100f;
        public float DrawerTravel = 0.34f;
        public float AnimationSpeed = 5.5f;
        public GameObject LootRoot;
        public bool IsOpen { get; private set; }

        private Quaternion leftClosed = Quaternion.identity;
        private Quaternion rightClosed = Quaternion.identity;
        private Vector3[] drawerClosed = Array.Empty<Vector3>();
        private float openProgress;
        private bool cachedPose;

        private void Awake()
        {
            CacheClosedPose();
            openProgress = IsOpen ? 1f : 0f;
            ApplyPose(openProgress);
        }

        private void Update()
        {
            float target = IsOpen ? 1f : 0f;
            if (!Mathf.Approximately(openProgress, target))
            {
                openProgress = Mathf.MoveTowards(openProgress, target, AnimationSpeed * Time.deltaTime);
                ApplyPose(openProgress);
            }

            if (LootRoot != null)
            {
                LootRoot.SetActive(IsOpen && openProgress > 0.72f);
            }
        }

        public void Configure(
            string cabinetId,
            Transform leftDoorPivot,
            Transform rightDoorPivot,
            GameObject lootRoot,
            Transform[] drawerSlides = null,
            float drawerTravel = 0.34f)
        {
            CabinetId = cabinetId;
            LeftDoorPivot = leftDoorPivot;
            RightDoorPivot = rightDoorPivot;
            DrawerSlides = drawerSlides ?? Array.Empty<Transform>();
            DrawerTravel = Mathf.Max(0f, drawerTravel);
            LootRoot = lootRoot;
            cachedPose = false;
            openProgress = 0f;
            CacheClosedPose();
            ApplyPose(0f);
            if (LootRoot != null)
            {
                LootRoot.SetActive(false);
            }
        }

        public void ToggleLocal()
        {
            IsOpen = !IsOpen;
            if (LootRoot != null && IsOpen)
            {
                LootRoot.SetActive(false);
            }
        }

        private void CacheClosedPose()
        {
            if (cachedPose)
            {
                return;
            }

            leftClosed = LeftDoorPivot != null ? LeftDoorPivot.localRotation : Quaternion.identity;
            rightClosed = RightDoorPivot != null ? RightDoorPivot.localRotation : Quaternion.identity;
            drawerClosed = new Vector3[DrawerSlides != null ? DrawerSlides.Length : 0];
            for (int i = 0; i < drawerClosed.Length; i++)
            {
                drawerClosed[i] = DrawerSlides[i] != null ? DrawerSlides[i].localPosition : Vector3.zero;
            }
            cachedPose = true;
        }

        private void ApplyPose(float progress)
        {
            CacheClosedPose();
            float eased = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress));

            if (LeftDoorPivot != null)
            {
                LeftDoorPivot.localRotation =
                    leftClosed * Quaternion.Euler(0f, OpenAngle * eased, 0f);
            }

            if (RightDoorPivot != null)
            {
                RightDoorPivot.localRotation =
                    rightClosed * Quaternion.Euler(0f, -OpenAngle * eased, 0f);
            }

            if (DrawerSlides != null)
            {
                for (int i = 0; i < DrawerSlides.Length && i < drawerClosed.Length; i++)
                {
                    if (DrawerSlides[i] != null)
                    {
                        DrawerSlides[i].localPosition =
                            drawerClosed[i] + Vector3.back * (DrawerTravel * eased);
                    }
                }
            }
        }
    }

    public class MiniVanPanelkaCabinetLootBase : MonoBehaviour
    {
        public string LootId;
        public MiniVanInventoryItem Item;

        public void Configure(string lootId, MiniVanInventoryItem item)
        {
            LootId = lootId;
            Item = item;
        }

        public void ConsumeLocal()
        {
            gameObject.SetActive(false);
        }
    }

    public static class MiniVanPanelkaCabinetLootBuilder
    {
        private static readonly MiniVanInventoryItem[] TallCabinetLootPool =
        {
            MiniVanInventoryItem.Bat,
            MiniVanInventoryItem.HotPotatoBomb,
            MiniVanInventoryItem.HoverboardM
        };

        private static readonly MiniVanInventoryItem[] FridgeIngredientPool =
        {
            MiniVanInventoryItem.Flour,
            MiniVanInventoryItem.Water,
            MiniVanInventoryItem.TomatoPaste,
            MiniVanInventoryItem.Cheese,
            MiniVanInventoryItem.Sausage
        };

        public static void ConfigureApartment(
            Transform apartment,
            Transform furnishing,
            int deterministicSeed,
            Material wood,
            Material metal,
            Material ceramic,
            Material darkPlastic)
        {
            if (apartment == null || furnishing == null)
            {
                return;
            }

            MiniVanPanelkaApartmentRouteMarker marker =
                apartment.GetComponent<MiniVanPanelkaApartmentRouteMarker>();
            int apartmentNumber = marker != null
                ? marker.ApartmentNumber
                : Mathf.Abs(apartment.name.GetHashCode() % 1000);
            MiniVanPanelkaStage1Generator generator =
                apartment.GetComponentInParent<MiniVanPanelkaStage1Generator>();
            int panelkaId = generator != null ? generator.GenerationSeed : deterministicSeed;
            string apartmentId = "PANELKA_" + panelkaId + "_APT_" +
                                 apartmentNumber.ToString("00");
            List<MiniVanPanelkaCabinet> tallCabinets = new List<MiniVanPanelkaCabinet>();
            List<MiniVanPanelkaCabinet> fridges = new List<MiniVanPanelkaCabinet>();
            Transform[] all = furnishing.GetComponentsInChildren<Transform>(true);

            for (int i = 0; i < all.Length; i++)
            {
                Transform item = all[i];
                if (item == null)
                {
                    continue;
                }

                bool isTallCabinet =
                    item.name.StartsWith("Wardrobe", StringComparison.Ordinal) ||
                    item.name.StartsWith("Soviet_Wall_Unit", StringComparison.Ordinal) ||
                    item.name.StartsWith("FullHeight_Double_Wardrobe", StringComparison.Ordinal) ||
                    item.name.StartsWith("Hall_FullHeight_Wardrobe", StringComparison.Ordinal);
                if (isTallCabinet)
                {
                    MiniVanPanelkaCabinet cabinet = ConfigureTallCabinet(
                        item,
                        apartmentId + "_" + item.name + "_" + i,
                        wood,
                        metal);
                    if (cabinet != null)
                    {
                        tallCabinets.Add(cabinet);
                    }
                }
                else if (item.name.StartsWith("Kitchen_Cabinet_Run", StringComparison.Ordinal))
                {
                    ConfigureKitchenCabinets(
                        item,
                        apartmentId + "_KITCHEN_" + i,
                        wood,
                        metal,
                        new List<MiniVanPanelkaCabinet>());
                }
                else if (item.name.StartsWith("Rounded_Soviet_Fridge", StringComparison.Ordinal))
                {
                    MiniVanPanelkaCabinet fridge = ConfigureRefrigerator(
                        item,
                        apartmentId + "_FRIDGE_" + i,
                        ceramic,
                        metal);
                    if (fridge != null)
                    {
                        fridges.Add(fridge);
                    }
                }
            }

            System.Random random = new System.Random(deterministicSeed ^ 0x36C4A9);
            if (tallCabinets.Count > 0)
            {
                MiniVanPanelkaCabinet target = tallCabinets[random.Next(tallCabinets.Count)];
                MiniVanInventoryItem item = TallCabinetLootPool[random.Next(TallCabinetLootPool.Length)];
                CreateLoot(target, item, apartmentId, 0, metal, ceramic, darkPlastic);
            }

            for (int fridgeIndex = 0; fridgeIndex < fridges.Count; fridgeIndex++)
            {
                int firstIngredient = random.Next(FridgeIngredientPool.Length);
                int secondIngredient = (firstIngredient + 1 + random.Next(FridgeIngredientPool.Length - 1)) % FridgeIngredientPool.Length;
                CreateLoot(fridges[fridgeIndex], FridgeIngredientPool[firstIngredient], apartmentId, 10 + fridgeIndex * 2, metal, ceramic, darkPlastic);
                CreateLoot(fridges[fridgeIndex], FridgeIngredientPool[secondIngredient], apartmentId, 11 + fridgeIndex * 2, metal, ceramic, darkPlastic);
            }

            EnsureEveryCabinetIsInteractable(furnishing);
        }

        private static void EnsureEveryCabinetIsInteractable(Transform furnishing)
        {
            MiniVanPanelkaCabinet[] cabinets =
                furnishing.GetComponentsInChildren<MiniVanPanelkaCabinet>(true);
            for (int i = 0; i < cabinets.Length; i++)
            {
                MiniVanPanelkaCabinet cabinet = cabinets[i];
                if (cabinet == null)
                {
                    continue;
                }

                Transform root = cabinet.transform;
                Vector3 placedPosition = root.localPosition;
                Quaternion placedRotation = root.localRotation;
                MiniVanPanelkaInteractable interactable =
                    root.GetComponent<MiniVanPanelkaInteractable>();
                if (interactable == null)
                {
                    interactable = root.gameObject.AddComponent<MiniVanPanelkaInteractable>();
                }
                interactable.Type = MiniVanPanelkaInteractableType.Cabinet;
                root.localPosition = placedPosition;
                root.localRotation = placedRotation;
            }
        }

        private static MiniVanPanelkaCabinet ConfigureRefrigerator(
            Transform root,
            string id,
            Material ceramic,
            Material metal)
        {
            Transform body = FindDirect(root, "Fridge_Body");
            Transform upperDoor = FindDirect(root, "Upper_Door");
            Transform lowerDoor = FindDirect(root, "Lower_Door");
            Transform handle = FindDirect(root, "Handle");
            Bounds bodyBounds;
            Bounds upperBounds;
            Bounds lowerBounds;
            if (body == null || upperDoor == null || lowerDoor == null ||
                !TryGetLocalRendererBounds(root, body, out bodyBounds) ||
                !TryGetLocalRendererBounds(root, upperDoor, out upperBounds) ||
                !TryGetLocalRendererBounds(root, lowerDoor, out lowerBounds))
            {
                return null;
            }

            Material shellMaterial = GetMaterial(body, ceramic);
            DisableSolid(body);
            Vector3 size = bodyBounds.size;
            Vector3 center = bodyBounds.center;
            CreateBox("Fridge_Back", root, center + Vector3.forward * size.z * 0.47f, new Vector3(size.x, size.y, 0.035f), shellMaterial);
            CreateBox("Fridge_Side_L", root, center + Vector3.left * size.x * 0.47f, new Vector3(0.04f, size.y, size.z), shellMaterial);
            CreateBox("Fridge_Side_R", root, center + Vector3.right * size.x * 0.47f, new Vector3(0.04f, size.y, size.z), shellMaterial);
            CreateBox("Fridge_Top", root, center + Vector3.up * size.y * 0.47f, new Vector3(size.x, 0.04f, size.z), shellMaterial);
            CreateBox("Fridge_Bottom", root, center + Vector3.down * size.y * 0.47f, new Vector3(size.x, 0.04f, size.z), shellMaterial);
            CreateBox("Fridge_Shelf_Upper", root, center + Vector3.up * size.y * 0.12f, new Vector3(size.x * 0.90f, 0.035f, size.z * 0.86f), metal);
            CreateBox("Fridge_Shelf_Lower", root, center + Vector3.down * size.y * 0.18f, new Vector3(size.x * 0.90f, 0.035f, size.z * 0.86f), metal);

            Bounds doorsBounds = upperBounds;
            doorsBounds.Encapsulate(lowerBounds);
            Transform pivot = new GameObject("Fridge_Door_Assembly_Pivot").transform;
            pivot.SetParent(root, false);
            pivot.localPosition = new Vector3(doorsBounds.max.x, doorsBounds.center.y, doorsBounds.center.z);
            CreateBox(
                "Upper_Door_Openable",
                pivot,
                upperBounds.center - pivot.localPosition,
                upperBounds.size,
                GetMaterial(upperDoor, shellMaterial));
            CreateBox(
                "Lower_Door_Openable",
                pivot,
                lowerBounds.center - pivot.localPosition,
                lowerBounds.size,
                GetMaterial(lowerDoor, shellMaterial));
            DisableSolid(upperDoor);
            DisableSolid(lowerDoor);
            if (handle != null)
            {
                Bounds handleBounds;
                if (TryGetLocalRendererBounds(root, handle, out handleBounds))
                {
                    CreateBox(
                        "Fridge_Handle_Openable",
                        pivot,
                        handleBounds.center - pivot.localPosition,
                        handleBounds.size,
                        GetMaterial(handle, metal));
                }
                DisableSolid(handle);
            }

            GameObject lootRoot = new GameObject("Fridge_Ingredient_Root");
            lootRoot.transform.SetParent(root, false);
            lootRoot.transform.localPosition = center + new Vector3(0f, -size.y * 0.03f, -size.z * 0.20f);

            MiniVanPanelkaCabinet fridge = root.gameObject.GetComponent<MiniVanPanelkaCabinet>();
            if (fridge == null)
            {
                fridge = root.gameObject.AddComponent<MiniVanPanelkaCabinet>();
            }
            fridge.OpenAngle = 105f;
            fridge.Configure(id, null, pivot, lootRoot);

            MiniVanPanelkaInteractable interactable = root.gameObject.GetComponent<MiniVanPanelkaInteractable>();
            if (interactable == null)
            {
                Quaternion placedRotation = root.localRotation;
                interactable = root.gameObject.AddComponent<MiniVanPanelkaInteractable>();
                root.localRotation = placedRotation;
            }
            interactable.Type = MiniVanPanelkaInteractableType.Cabinet;
            return fridge;
        }

private static MiniVanPanelkaCabinet ConfigureTallCabinet(
            Transform root,
            string id,
            Material wood,
            Material metal)
        {
            Transform body = FindDirect(root, "Shell") ?? FindDirect(root, "Cabinet");
            Transform leftDoor = FindDirect(root, "Door_L");
            Transform rightDoor = FindDirect(root, "Door_R");
            if (leftDoor == null || rightDoor == null)
            {
                return null;
            }

            Bounds cabinetBounds;
            if (!TryGetLocalRendererBounds(root, root, out cabinetBounds))
            {
                return null;
            }

            Transform materialSource = body ?? FindDirect(root, "Cabinet_Back") ?? leftDoor;
            Material shellMaterial = GetMaterial(materialSource, wood);
            bool hasPrefabCarcass = FindDirect(root, "Cabinet_Back") != null;

            if (body != null && !hasPrefabCarcass)
            {
                Bounds bodyBounds;
                if (TryGetLocalRendererBounds(root, body, out bodyBounds))
                {
                    Vector3 size = bodyBounds.size;
                    Vector3 center = bodyBounds.center;
                    DisableSolid(body);

                    CreateBox("Cabinet_Back", root, center + new Vector3(0f, 0f, size.z * 0.47f), new Vector3(size.x, size.y, 0.035f), shellMaterial);
                    CreateBox("Cabinet_Side_L", root, center + new Vector3(-size.x * 0.48f, 0f, 0f), new Vector3(0.05f, size.y, size.z), shellMaterial);
                    CreateBox("Cabinet_Side_R", root, center + new Vector3(size.x * 0.48f, 0f, 0f), new Vector3(0.05f, size.y, size.z), shellMaterial);
                    CreateBox("Cabinet_Top", root, center + new Vector3(0f, size.y * 0.48f, 0f), new Vector3(size.x, 0.05f, size.z), shellMaterial);
                    CreateBox("Cabinet_Bottom", root, center + new Vector3(0f, -size.y * 0.48f, 0f), new Vector3(size.x, 0.05f, size.z), shellMaterial);
                    CreateBox("Cabinet_Shelf", root, center + new Vector3(0f, -size.y * 0.12f, 0f), new Vector3(size.x * 0.92f, 0.04f, size.z * 0.88f), shellMaterial);
                }
            }

            Transform oldLeftHandle = FindDirect(root, "Handle_L");
            Transform oldRightHandle = FindDirect(root, "Handle_R");
            if (oldLeftHandle != null)
            {
                DisableSolid(oldLeftHandle);
            }
            if (oldRightHandle != null)
            {
                DisableSolid(oldRightHandle);
            }

            Bounds leftBounds;
            Bounds rightBounds;
            if (!TryGetLocalRendererBounds(root, leftDoor, out leftBounds) ||
                !TryGetLocalRendererBounds(root, rightDoor, out rightBounds))
            {
                return null;
            }

            float leftDoorWidth = leftBounds.size.x;
            float rightDoorWidth = rightBounds.size.x;
            Transform leftPivot = CloneDoorOnPivot(root, leftDoor, true, shellMaterial);
            Transform rightPivot = CloneDoorOnPivot(root, rightDoor, false, shellMaterial);
            DisableSolid(leftDoor);
            DisableSolid(rightDoor);

            CreateBox(
                "Handle_L_Openable",
                leftPivot,
                new Vector3(leftDoorWidth * 0.84f, 0f, -0.035f),
                new Vector3(0.025f, 0.22f, 0.025f),
                metal);
            CreateBox(
                "Handle_R_Openable",
                rightPivot,
                new Vector3(-rightDoorWidth * 0.84f, 0f, -0.035f),
                new Vector3(0.025f, 0.22f, 0.025f),
                metal);

            GameObject lootRoot = new GameObject("Cabinet_Loot_Root");
            lootRoot.transform.SetParent(root, false);
            lootRoot.transform.localPosition =
                cabinetBounds.center +
                new Vector3(0f, -cabinetBounds.size.y * 0.04f, -cabinetBounds.size.z * 0.18f);

            MiniVanPanelkaCabinet cabinet = root.gameObject.GetComponent<MiniVanPanelkaCabinet>();
            if (cabinet == null)
            {
                cabinet = root.gameObject.AddComponent<MiniVanPanelkaCabinet>();
            }
            cabinet.Configure(id, leftPivot, rightPivot, lootRoot);

            MiniVanPanelkaInteractable interactable = root.gameObject.GetComponent<MiniVanPanelkaInteractable>();
            if (interactable == null)
            {
                Quaternion placedRotation = root.localRotation;
                interactable = root.gameObject.AddComponent<MiniVanPanelkaInteractable>();
                // Awake sees the enum's default Door value before Cabinet is assigned.
                root.localRotation = placedRotation;
            }
            interactable.Type = MiniVanPanelkaInteractableType.Cabinet;
            return cabinet;
        }

private static void ConfigureKitchenCabinets(
            Transform root,
            string idPrefix,
            Material wood,
            Material metal,
            List<MiniVanPanelkaCabinet> output)
        {
            Transform lowerSolid = FindDirect(root, "Lower_Cabinets");
            Bounds lowerBounds;
            if (lowerSolid != null &&
                TryGetLocalRendererBounds(root, lowerSolid, out lowerBounds))
            {
                Vector3 size = lowerBounds.size;
                Vector3 center = lowerBounds.center;
                Material shellMaterial = GetMaterial(lowerSolid, wood);

                Transform lowerRoot = new GameObject("Kitchen_Lower_Cabinet_TopDrawer").transform;
                lowerRoot.SetParent(root, false);

                Transform slide = new GameObject("Top_Drawer_Slide").transform;
                slide.SetParent(lowerRoot, false);
                slide.localPosition = center + new Vector3(0f, size.y * 0.29f, 0f);

                float drawerHeight = Mathf.Min(0.20f, size.y * 0.24f);
                CreateBox("Top_Drawer_Tray", slide, new Vector3(0f, 0f, 0.01f), new Vector3(size.x * 0.88f, drawerHeight * 0.72f, size.z * 0.72f), shellMaterial);
                CreateBox("Top_Drawer_Front", slide, new Vector3(0f, 0f, -size.z * 0.53f), new Vector3(size.x * 0.92f, drawerHeight, 0.04f), shellMaterial);
                CreateBox("Top_Drawer_Handle", slide, new Vector3(0f, 0f, -size.z * 0.62f), new Vector3(size.x * 0.30f, 0.035f, 0.025f), metal);

                GameObject lowerLootRoot = new GameObject("Cabinet_Loot_Root");
                lowerLootRoot.transform.SetParent(slide, false);
                lowerLootRoot.transform.localPosition = new Vector3(0f, 0.04f, -size.z * 0.06f);

                MiniVanPanelkaCabinet lowerCabinet =
                    lowerRoot.gameObject.AddComponent<MiniVanPanelkaCabinet>();
                lowerCabinet.Configure(
                    idPrefix + "_LOWER_TOP_DRAWER",
                    null,
                    null,
                    lowerLootRoot,
                    new[] { slide },
                    Mathf.Clamp(size.z * 0.72f, 0.22f, 0.34f));
                MiniVanPanelkaInteractable lowerInteractable =
                    lowerRoot.gameObject.AddComponent<MiniVanPanelkaInteractable>();
                lowerInteractable.Type = MiniVanPanelkaInteractableType.Cabinet;
                output.Add(lowerCabinet);
            }

            Transform[] upper =
            {
                FindDirect(root, "Upper_Left"),
                FindDirect(root, "Upper_Right")
            };

            for (int i = 0; i < upper.Length; i++)
            {
                Transform solid = upper[i];
                Bounds solidBounds;
                if (solid == null ||
                    !TryGetLocalRendererBounds(root, solid, out solidBounds))
                {
                    continue;
                }

                Vector3 size = solidBounds.size;
                Vector3 center = solidBounds.center;
                Material shellMaterial = GetMaterial(solid, wood);
                DisableSolid(solid);

                Transform cabinetRoot =
                    new GameObject("Kitchen_Upper_Cabinet_" + i).transform;
                cabinetRoot.SetParent(root, false);
                CreateBox("Back", cabinetRoot, center + new Vector3(0f, 0f, size.z * 0.47f), new Vector3(size.x, size.y, 0.035f), shellMaterial);
                CreateBox("Side_L", cabinetRoot, center + new Vector3(-size.x * 0.47f, 0f, 0f), new Vector3(0.04f, size.y, size.z), shellMaterial);
                CreateBox("Side_R", cabinetRoot, center + new Vector3(size.x * 0.47f, 0f, 0f), new Vector3(0.04f, size.y, size.z), shellMaterial);
                CreateBox("Top", cabinetRoot, center + new Vector3(0f, size.y * 0.47f, 0f), new Vector3(size.x, 0.04f, size.z), shellMaterial);
                CreateBox("Bottom", cabinetRoot, center + new Vector3(0f, -size.y * 0.47f, 0f), new Vector3(size.x, 0.04f, size.z), shellMaterial);

                GameObject door = CreateBox(
                    "Door",
                    cabinetRoot,
                    center + new Vector3(0f, 0f, -size.z * 0.54f),
                    new Vector3(size.x * 0.92f, size.y * 0.90f, 0.03f),
                    shellMaterial);
                bool hingeLeft = i == 0;
                Transform pivot = MakeDoorPivot(cabinetRoot, door.transform, hingeLeft);
                CreateBox(
                    "Handle_Openable",
                    pivot,
                    new Vector3((hingeLeft ? 1f : -1f) * size.x * 0.34f, 0f, -0.035f),
                    new Vector3(0.025f, 0.18f, 0.025f),
                    metal);

                GameObject lootRoot = new GameObject("Cabinet_Loot_Root");
                lootRoot.transform.SetParent(cabinetRoot, false);
                lootRoot.transform.localPosition = center + new Vector3(0f, -0.04f, -size.z * 0.16f);

                MiniVanPanelkaCabinet cabinet =
                    cabinetRoot.gameObject.AddComponent<MiniVanPanelkaCabinet>();
                cabinet.Configure(
                    idPrefix + "_UPPER_" + i,
                    hingeLeft ? pivot : null,
                    hingeLeft ? null : pivot,
                    lootRoot);
                MiniVanPanelkaInteractable interactable =
                    cabinetRoot.gameObject.AddComponent<MiniVanPanelkaInteractable>();
                interactable.Type = MiniVanPanelkaInteractableType.Cabinet;
                output.Add(cabinet);
            }
        }

        private static void CreateLoot(
            MiniVanPanelkaCabinet cabinet,
            MiniVanInventoryItem item,
            string apartmentId,
            int index,
            Material metal,
            Material ceramic,
            Material darkPlastic)
        {
            if (cabinet == null || cabinet.LootRoot == null)
            {
                return;
            }

            string lootId = apartmentId + "_LOOT_" + index.ToString("00");
            GameObject root = new GameObject("Cabinet_Loot_" + item);
            root.transform.SetParent(cabinet.LootRoot.transform, false);
            root.transform.localPosition = new Vector3(index % 2 == 0 ? -0.11f : 0.11f, 0f, 0f);

            Color color = GetLootColor(item);
            Material material = CreateRuntimeMaterial("PanelkaLoot_" + item, color);
            BuildLootVisual(root.transform, item, material, metal, ceramic, darkPlastic);

            BoxCollider collider = root.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 0.08f, 0f);
            collider.size = new Vector3(0.42f, 0.34f, 0.26f);

            MiniVanPanelkaCabinetLoot loot = root.AddComponent<MiniVanPanelkaCabinetLoot>();
            loot.Configure(lootId, item);
            MiniVanPanelkaInteractable interactable = root.AddComponent<MiniVanPanelkaInteractable>();
            interactable.Type = MiniVanPanelkaInteractableType.CabinetLoot;
            cabinet.LootRoot.SetActive(false);
        }

        private static void BuildLootVisual(
            Transform root,
            MiniVanInventoryItem item,
            Material material,
            Material metal,
            Material ceramic,
            Material darkPlastic)
        {
            if ((int)item >= (int)MiniVanInventoryItem.Flour &&
                (int)item <= (int)MiniVanInventoryItem.BurnedPizza)
            {
                GameObject prefab = Resources.Load<GameObject>("PizzaLoop/PizzaItem_" + item);
                if (prefab != null)
                {
                    GameObject visual = UnityEngine.Object.Instantiate(prefab, root, false);
                    visual.name = "LowPoly_" + item;
                    foreach (MiniVanPizzaItem pizzaItem in visual.GetComponentsInChildren<MiniVanPizzaItem>(true))
                    {
                        pizzaItem.enabled = false;
                    }
                    foreach (Collider childCollider in visual.GetComponentsInChildren<Collider>(true))
                    {
                        childCollider.enabled = false;
                    }
                    return;
                }
            }

            if (item == MiniVanInventoryItem.Bat)
            {
                GameObject bat = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                bat.name = "Compact_Bat";
                bat.transform.SetParent(root, false);
                bat.transform.localPosition = new Vector3(0f, 0.08f, 0f);
                bat.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                bat.transform.localScale = new Vector3(0.045f, 0.19f, 0.045f);
                bat.GetComponent<Renderer>().sharedMaterial = material;
                DestroyCollider(bat);
                return;
            }

            if (item == MiniVanInventoryItem.Coffee)
            {
                GameObject mug = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                mug.name = "Compact_Coffee";
                mug.transform.SetParent(root, false);
                mug.transform.localPosition = new Vector3(0f, 0.09f, 0f);
                mug.transform.localScale = new Vector3(0.10f, 0.11f, 0.10f);
                mug.GetComponent<Renderer>().sharedMaterial = ceramic != null ? ceramic : material;
                DestroyCollider(mug);
                CreateBox("Handle", root, new Vector3(0.12f, 0.09f, 0f), new Vector3(0.07f, 0.10f, 0.04f), ceramic != null ? ceramic : material);
                return;
            }

            if (item == MiniVanInventoryItem.HotPotatoBomb)
            {
                GameObject bomb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                bomb.name = "Compact_Bomb";
                bomb.transform.SetParent(root, false);
                bomb.transform.localPosition = new Vector3(0f, 0.10f, 0f);
                bomb.transform.localScale = Vector3.one * 0.18f;
                bomb.GetComponent<Renderer>().sharedMaterial = darkPlastic != null ? darkPlastic : material;
                DestroyCollider(bomb);
                return;
            }

            if (item == MiniVanInventoryItem.HoverboardM)
            {
                CreateBox("Compact_Hoverboard", root, new Vector3(0f, 0.07f, 0f), new Vector3(0.34f, 0.07f, 0.14f), material);
                return;
            }

            CreateBox("Compact_Ingredient", root, new Vector3(0f, 0.11f, 0f), new Vector3(0.22f, 0.22f, 0.16f), material);
        }

private static Transform MakeDoorPivot(Transform root, Transform door, bool hingeLeft)
        {
            Bounds doorBounds;
            if (!TryGetLocalRendererBounds(root, door, out doorBounds))
            {
                return null;
            }

            Vector3 hingePosition = doorBounds.center;
            hingePosition.x = hingeLeft ? doorBounds.min.x : doorBounds.max.x;

            GameObject pivotObject = new GameObject(door.name + "_Pivot");
            Transform pivot = pivotObject.transform;
            pivot.SetParent(root, false);
            pivot.localPosition = hingePosition;
            pivot.localRotation = Quaternion.identity;
            pivot.localScale = Vector3.one;

            door.SetParent(pivot, true);
            return pivot;
        }

        private static Transform FindDirect(Transform parent, string childName)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name == childName)
                {
                    return child;
                }
            }
            return null;
        }

        private static void DisableSolid(Transform solid)
        {
            Renderer renderer = solid.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.enabled = false;
            }

            Collider collider = solid.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
            }
        }

        private static Material GetMaterial(Transform source, Material fallback)
        {
            Renderer renderer = source.GetComponent<Renderer>();
            return renderer != null && renderer.sharedMaterial != null ? renderer.sharedMaterial : fallback;
        }

        private static GameObject CreateBox(
            string name,
            Transform parent,
            Vector3 localPosition,
            Vector3 localScale,
            Material material)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = localPosition;
            box.transform.localScale = localScale;
            Renderer renderer = box.GetComponent<Renderer>();
            if (renderer != null && material != null)
            {
                renderer.sharedMaterial = material;
            }
            return box;
        }

        private static void DestroyCollider(GameObject target)
        {
            Collider collider = target.GetComponent<Collider>();
            if (collider != null)
            {
                UnityEngine.Object.DestroyImmediate(collider);
            }
        }

        private static Material CreateRuntimeMaterial(string name, Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material material = new Material(shader);
            material.name = name;
            material.color = color;
            return material;
        }

        private static Color GetLootColor(MiniVanInventoryItem item)
        {
            switch (item)
            {
                case MiniVanInventoryItem.Bat: return new Color(0.42f, 0.18f, 0.06f);
                case MiniVanInventoryItem.Coffee: return new Color(0.82f, 0.82f, 0.76f);
                case MiniVanInventoryItem.HotPotatoBomb: return new Color(0.08f, 0.08f, 0.08f);
                case MiniVanInventoryItem.HoverboardM: return new Color(0.18f, 0.72f, 0.78f);
                case MiniVanInventoryItem.Flour: return new Color(0.92f, 0.88f, 0.72f);
                case MiniVanInventoryItem.Water: return new Color(0.25f, 0.62f, 0.95f);
                case MiniVanInventoryItem.TomatoPaste: return new Color(0.72f, 0.08f, 0.04f);
                case MiniVanInventoryItem.Cheese: return new Color(0.95f, 0.72f, 0.08f);
                default: return new Color(0.72f, 0.22f, 0.18f);
            }
        }
    

private static Transform CloneDoorOnPivot(
            Transform root,
            Transform sourceDoor,
            bool hingeLeft,
            Material fallback)
        {
            Bounds doorBounds;
            if (!TryGetLocalRendererBounds(root, sourceDoor, out doorBounds))
            {
                return null;
            }

            Vector3 hingePosition = doorBounds.center;
            hingePosition.x = hingeLeft ? doorBounds.min.x : doorBounds.max.x;

            GameObject pivotObject =
                new GameObject(sourceDoor.name + "_Runtime_Pivot");
            Transform pivot = pivotObject.transform;
            pivot.SetParent(root, false);
            pivot.localPosition = hingePosition;
            pivot.localRotation = Quaternion.identity;
            pivot.localScale = Vector3.one;

            Vector3 centerFromPivot = doorBounds.center - hingePosition;
            CreateBox(
                sourceDoor.name + "_Openable",
                pivot,
                centerFromPivot,
                doorBounds.size,
                GetMaterial(sourceDoor, fallback));
            return pivot;
        }


private static bool TryGetLocalRendererBounds(
            Transform relativeTo,
            Transform source,
            out Bounds bounds)
        {
            bounds = new Bounds();
            if (relativeTo == null || source == null)
            {
                return false;
            }

            Renderer[] renderers = source.GetComponentsInChildren<Renderer>(true);
            bool hasBounds = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                Bounds rendererBounds = renderer.localBounds;
                Vector3 min = rendererBounds.min;
                Vector3 max = rendererBounds.max;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 rendererLocalPoint = new Vector3(
                        (corner & 1) == 0 ? min.x : max.x,
                        (corner & 2) == 0 ? min.y : max.y,
                        (corner & 4) == 0 ? min.z : max.z);
                    Vector3 worldPoint = renderer.transform.TransformPoint(rendererLocalPoint);
                    Vector3 localPoint = relativeTo.InverseTransformPoint(worldPoint);
                    if (!hasBounds)
                    {
                        bounds = new Bounds(localPoint, Vector3.zero);
                        hasBounds = true;
                    }
                    else
                    {
                        bounds.Encapsulate(localPoint);
                    }
                }
            }

            return hasBounds;
        }
}

    public partial class MiniVanPlayer
    {
        public void TryTogglePanelkaCabinet(MiniVanPanelkaCabinet cabinet)
        {
            if (!IsOwner || cabinet == null || string.IsNullOrEmpty(cabinet.CabinetId))
            {
                return;
            }
            RequestTogglePanelkaCabinetServerRpc(cabinet.CabinetId);
        }

        public void TryPickupPanelkaCabinetLoot(MiniVanPanelkaCabinetLoot loot)
        {
            if (!IsOwner || loot == null || string.IsNullOrEmpty(loot.LootId))
            {
                return;
            }
            RequestPanelkaCabinetLootServerRpc(loot.LootId, (int)loot.Item);
        }

        private bool TryBreakPanelkaWindow(Collider hitCollider)
        {
            MiniVanPanelkaBreakableWindow window =
                hitCollider != null ? hitCollider.GetComponentInParent<MiniVanPanelkaBreakableWindow>() : null;
            if (window == null || window.IsBroken || string.IsNullOrEmpty(window.WindowId))
            {
                return false;
            }

            window.BreakLocal();
            BreakPanelkaWindowClientRpc(window.WindowId);
            return true;
        }

        [ServerRpc]
        private void RequestTogglePanelkaCabinetServerRpc(string cabinetId, ServerRpcParams rpcParams = default)
        {
            MiniVanPanelkaCabinet cabinet = FindCabinet(cabinetId);
            if (cabinet == null || DistanceToCabinet(cabinet, transform.position) > 4.5f)
            {
                return;
            }
            TogglePanelkaCabinetClientRpc(cabinetId);
        }

        [ClientRpc]
        private void TogglePanelkaCabinetClientRpc(string cabinetId)
        {
            MiniVanPanelkaCabinet cabinet = FindCabinet(cabinetId);
            if (cabinet != null)
            {
                cabinet.ToggleLocal();
            }
        }

        [ServerRpc]
        private void RequestPanelkaCabinetLootServerRpc(
            string lootId,
            int itemValue,
            ServerRpcParams rpcParams = default)
        {
            MiniVanPanelkaCabinetLoot loot = FindCabinetLoot(lootId);
            if (loot == null ||
                !loot.gameObject.activeInHierarchy ||
                Vector3.Distance(transform.position, loot.transform.position) > 4.5f ||
                (int)loot.Item != itemValue)
            {
                return;
            }

            int slot = FindFirstEmptyInventorySlot();
            if (slot < 0)
            {
                return;
            }

            if (loot.Item == MiniVanInventoryItem.HotPotatoBomb)
            {
                MiniVanHotPotatoBomb bomb = FindAvailableCabinetBomb();
                if (bomb == null || !bomb.ServerPickupByPlayer(this))
                {
                    return;
                }

                SetInventorySlot(slot, MiniVanInventoryItem.HotPotatoBomb);
                networkSelectedSlot.Value = slot;
                heldHotPotatoBomb = bomb;
                heldHotPotatoBombSlot = slot;
                hotPotatoDropBlockedUntilFrame = Time.frameCount + 1;
                SetHotPotatoHeldClientRpc(new NetworkObjectReference(bomb.NetworkObject), true, slot);
                ConsumePanelkaCabinetLootClientRpc(lootId);
                return;
            }

            if (loot.Item == MiniVanInventoryItem.HoverboardM)
            {
                MiniVanHoverboardM hoverboard = FindAvailableCabinetHoverboard();
                if (hoverboard == null)
                {
                    return;
                }

                hoverboard.transform.position = loot.transform.position;
                if (!hoverboard.TryPickup(rpcParams.Receive.SenderClientId, this))
                {
                    return;
                }

                SetInventorySlot(slot, MiniVanInventoryItem.HoverboardM);
                networkSelectedSlot.Value = slot;
                SetHeldHoverboardMClientRpc(new NetworkObjectReference(hoverboard.NetworkObject), true, slot, BuildOwnerTarget());
                ConsumePanelkaCabinetLootClientRpc(lootId);
                return;
            }

            SetInventorySlot(slot, loot.Item);
            ConsumePanelkaCabinetLootClientRpc(lootId);
        }

        private static MiniVanHotPotatoBomb FindAvailableCabinetBomb()
        {
            MiniVanHotPotatoBomb[] bombs = FindObjectsByType<MiniVanHotPotatoBomb>(FindObjectsSortMode.None);
            for (int i = 0; i < bombs.Length; i++)
            {
                if (bombs[i] != null && bombs[i].IsAvailable)
                {
                    return bombs[i];
                }
            }
            return null;
        }

        private static MiniVanHoverboardM FindAvailableCabinetHoverboard()
        {
            MiniVanHoverboardM[] hoverboards = FindObjectsByType<MiniVanHoverboardM>(FindObjectsSortMode.None);
            for (int i = 0; i < hoverboards.Length; i++)
            {
                if (hoverboards[i] != null && hoverboards[i].IsAvailable)
                {
                    return hoverboards[i];
                }
            }
            return null;
        }

        [ClientRpc]
        private void ConsumePanelkaCabinetLootClientRpc(string lootId)
        {
            MiniVanPanelkaCabinetLoot loot = FindCabinetLoot(lootId);
            if (loot != null)
            {
                loot.ConsumeLocal();
            }
        }

        [ClientRpc]
        private void BreakPanelkaWindowClientRpc(string windowId)
        {
            MiniVanPanelkaBreakableWindow window = FindBreakableWindow(windowId);
            if (window != null)
            {
                window.BreakLocal();
            }
        }

        private static MiniVanPanelkaCabinet FindCabinet(string id)
        {
            MiniVanPanelkaCabinet[] cabinets =
                FindObjectsByType<MiniVanPanelkaCabinet>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < cabinets.Length; i++)
            {
                if (cabinets[i] != null && cabinets[i].CabinetId == id)
                {
                    return cabinets[i];
                }
            }
            return null;
        }

        private static float DistanceToCabinet(MiniVanPanelkaCabinet cabinet, Vector3 point)
        {
            if (cabinet == null)
            {
                return float.MaxValue;
            }

            Collider[] colliders = cabinet.GetComponentsInChildren<Collider>(true);
            float best = Vector3.Distance(point, cabinet.transform.position);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider != null && collider.enabled && !collider.isTrigger)
                {
                    Vector3 closestPoint = collider.bounds.ClosestPoint(point);
                    best = Mathf.Min(best, Vector3.Distance(point, closestPoint));
                }
            }
            return best;
        }

        private static MiniVanPanelkaCabinetLoot FindCabinetLoot(string id)
        {
            MiniVanPanelkaCabinetLoot[] loot =
                FindObjectsByType<MiniVanPanelkaCabinetLoot>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < loot.Length; i++)
            {
                if (loot[i] != null && loot[i].LootId == id)
                {
                    return loot[i];
                }
            }
            return null;
        }

        private static MiniVanPanelkaBreakableWindow FindBreakableWindow(string id)
        {
            MiniVanPanelkaBreakableWindow[] windows =
                FindObjectsByType<MiniVanPanelkaBreakableWindow>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < windows.Length; i++)
            {
                if (windows[i] != null && windows[i].WindowId == id)
                {
                    return windows[i];
                }
            }
            return null;
        }
    }
}
