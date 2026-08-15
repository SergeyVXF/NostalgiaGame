using System;
using System.Linq;
using Unity.AI.Navigation;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MiniVanGame
{
    public sealed partial class MiniVanPanelkaStage1Generator
    {
        private const string ApartmentTemplateCatalogResourcePath =
            "Panelka/ApartmentTemplateCatalog";
        private MiniVanPanelkaApartmentTemplateCatalog apartmentTemplateCatalog;

        private void BuildApartmentFromFullTemplate(
            Transform parent,
            int floorIndex,
            bool right,
            bool north,
            int layoutVariant,
            float yBase,
            string cornerName)
        {
            int apartmentSlot = GetApartmentSlot(cornerName);
            int apartmentNumber = floorIndex * 4 + apartmentSlot + 1;
            MiniVanPanelkaApartmentRouteRole routeRole =
                GetApartmentRouteRole(floorIndex, apartmentSlot);
            bool fullInterior =
                FurnishGeneratedRoute &&
                routeRole != MiniVanPanelkaApartmentRouteRole.Inaccessible;
            MiniVanPanelkaApartmentCornerVariant cornerVariant =
                ResolveApartmentCornerVariant(
                    right,
                    north,
                    floorIndex * StoreyHeight);

            MiniVanPanelkaApartmentTemplateCatalog catalog = GetApartmentTemplateCatalog();
            GameObject prefab = catalog != null
                ? (fullInterior
                    ? catalog.GetPrefab(layoutVariant, cornerVariant)
                    : catalog.ExteriorOnlyPrefab)
                : null;
            if (prefab == null)
            {
                throw new InvalidOperationException(
                    fullInterior
                        ? "Missing full apartment prefab for layout " + layoutVariant + "."
                        : "Missing ApartmentExteriorOnly prefab.");
            }

            float outerX = right ? BuildingHalfWidth : -BuildingHalfWidth;
            float innerX = right ? CoreHalfWidth : -CoreHalfWidth;
            float outerZ = north ? BuildingHalfDepth : -BuildingHalfDepth;
            float innerZ = 0f;
            float xCenter = (outerX + innerX) * 0.5f;
            float zCenter = (outerZ + innerZ) * 0.5f;

            Transform apartment = Group(
                "Apartment_" + apartmentNumber.ToString("00") + "_" + cornerName +
                "_Template_" + layoutVariant + "_" + routeRole,
                parent);
            MiniVanPanelkaApartmentRouteMarker routeMarker =
                apartment.gameObject.AddComponent<MiniVanPanelkaApartmentRouteMarker>();
            routeMarker.Configure(
                floorIndex + 1,
                apartmentNumber,
                apartmentSlot,
                routeRole);

            GameObject instance = InstantiateApartmentPrefab(prefab, apartment);
            instance.name = fullInterior
                ? "FULL_APARTMENT_PREFAB__EDIT_SOURCE_IN_PROJECT"
                : "APARTMENT_EXTERIOR_ONLY_PREFAB";
            instance.transform.localPosition = new Vector3(xCenter, yBase, zCenter);
            bool mirrorDepth = right != north;
            instance.transform.localRotation = right
                ? Quaternion.identity
                : Quaternion.Euler(0f, 180f, 0f);
            instance.transform.localScale = new Vector3(1f, 1f, mirrorDepth ? -1f : 1f);
            MiniVanPanelkaApartmentTemplate template =
                instance.GetComponent<MiniVanPanelkaApartmentTemplate>();
            if (template == null)
                throw new InvalidOperationException(prefab.name + " has no apartment template metadata.");
            if (template.EntrySocket == null)
                throw new InvalidOperationException(prefab.name + " has no EntrySocket.");
            if (!fullInterior)
            {
                AdaptExteriorOnlyEntry(instance.transform, template, layoutVariant);
            }

            Vector3 targetEntry = new Vector3(
                right ? CoreHalfWidth : -CoreHalfWidth,
                yBase,
                GetApartmentEntryZ(north, layoutVariant));
            Vector3 currentEntry = apartment.InverseTransformPoint(template.EntrySocket.position);
            instance.transform.localPosition += targetEntry - currentEntry;
            ConfigureApartmentWindowSockets(instance.transform);
            ApplyApartmentDoorMaterial(instance.transform, apartmentNumber);

            ValidateTemplatePlacement(
                apartment,
                template,
                targetEntry,
                outerX,
                outerZ,
                cornerName);

            MiniVanApartmentDoor entranceDoor = FindTemplateEntranceDoor(template);
            if (fullInterior && entranceDoor == null)
                throw new InvalidOperationException(prefab.name + " has no Apartment_Entrance_Door.");

            if (fullInterior)
            {
                bool hasHole = BuildTemplateRouteHole(
                    apartment,
                    template,
                    floorIndex,
                    routeRole,
                    cornerName,
                    out Vector3 routeHoleCenter);
                bool hasBalcony = BuildBalconyRoute(
                    apartment, template, floorIndex, routeRole, outerX, innerX,
                    outerZ, yBase, out Vector3 balconyCenter);
                bool hasPipe = BuildPipeRoute(
                    apartment, template, floorIndex, apartmentSlot, routeRole,
                    outerX, innerX, outerZ, yBase, out _);
                if (routeRole ==
                        MiniVanPanelkaApartmentRouteRole.MainRoute &&
                    floorIndex > 0 &&
                    !hasHole &&
                    !hasBalcony &&
                    !hasPipe &&
                    !IsStairTransitionFromFloor(floorIndex + 1))
                {
                    SetRouteTransitionToHole(floorIndex + 1);
                    hasHole = BuildTemplateRouteHole(
                        apartment,
                        template,
                        floorIndex,
                        routeRole,
                        cornerName,
                        out routeHoleCenter);
                }
                DisableTemplateRouteFeatureFurniture(
                    template,
                    apartment,
                    hasHole,
                    routeHoleCenter,
                    hasBalcony,
                    balconyCenter,
                    yBase);
                DisableTemplateEntranceFurniture(
                    template,
                    entranceDoor);
            }

            if (entranceDoor != null && fullInterior)
            {
                string requiredRouteKeyId = GetRouteDoorRequiredKeyId(floorIndex);
                MiniVanApartmentDoorLock doorLock =
                    entranceDoor.GetComponent<MiniVanApartmentDoorLock>();
                if (doorLock == null)
                    doorLock = entranceDoor.gameObject.AddComponent<MiniVanApartmentDoorLock>();
                bool shouldLock =
                    routeRole == MiniVanPanelkaApartmentRouteRole.MainRoute &&
                    !string.IsNullOrEmpty(requiredRouteKeyId);
                doorLock.Configure(requiredRouteKeyId, shouldLock);
            }

            if (routeRole == MiniVanPanelkaApartmentRouteRole.KeySource &&
                FloorUsesRouteKey(floorIndex))
            {
                BuildRouteKeyPickup(apartment, floorIndex, yBase);
            }
        }

        private MiniVanPanelkaApartmentCornerVariant
            ResolveApartmentCornerVariant(
                bool right,
                bool north,
                float yBase)
        {
            float outerX = right ? BuildingHalfWidth : -BuildingHalfWidth;
            float outerZ = north ? BuildingHalfDepth : -BuildingHalfDepth;
            float xCenter =
                (outerX + (right ? CoreHalfWidth : -CoreHalfWidth)) * 0.5f;
            float zCenter = outerZ * 0.5f;
            bool xBlocked = IsFacadeDecorationOccluded(
                new Bounds(
                    new Vector3(outerX, yBase + 1.5f, zCenter),
                    new Vector3(0.12f, 2.6f, 0.12f)));
            bool zBlocked = IsFacadeDecorationOccluded(
                new Bounds(
                    new Vector3(xCenter, yBase + 1.5f, outerZ),
                    new Vector3(0.12f, 2.6f, 0.12f)));

            if (xBlocked && !zBlocked)
                return MiniVanPanelkaApartmentCornerVariant.CornerLeft;
            if (zBlocked && !xBlocked)
                return MiniVanPanelkaApartmentCornerVariant.CornerRight;
            return MiniVanPanelkaApartmentCornerVariant.Standard;
        }

        private void ConfigureApartmentWindowSockets(Transform instance)
        {
            MiniVanPanelkaWindowSocket[] sockets =
                instance.GetComponentsInChildren<MiniVanPanelkaWindowSocket>(true);
            if (sockets.Length == 0)
                return;

            var roomGroups = sockets.GroupBy(socket =>
                string.IsNullOrEmpty(socket.RoomId)
                    ? socket.name
                    : socket.RoomId);
            foreach (var roomGroup in roomGroups)
            {
                MiniVanPanelkaApartmentFacadeSide? selectedSide = null;
                foreach (var sideGroup in roomGroup.GroupBy(socket => socket.Side))
                {
                    bool exposed = sideGroup.Any(socket =>
                    {
                        if (socket.WindowModule == null ||
                            !TryGetLocalRenderBounds(
                                socket.WindowModule.transform,
                                out Bounds localBounds))
                        {
                            return false;
                        }
                        return !IsFacadeDecorationOccluded(localBounds);
                    });
                    if (exposed)
                    {
                        selectedSide = sideGroup.Key;
                        break;
                    }
                }

                foreach (MiniVanPanelkaWindowSocket socket in roomGroup)
                {
                    bool active = selectedSide.HasValue &&
                                  socket.Side == selectedSide.Value &&
                                  socket.WindowModule != null &&
                                  TryGetLocalRenderBounds(
                                      socket.WindowModule.transform,
                                      out Bounds localBounds) &&
                                  !IsFacadeDecorationOccluded(localBounds);
                    socket.SetWindowActive(active);
                }
            }
        }

        /// <summary>
        /// Every layout prefab ships its own leaf colour and every sealed flat reuses the
        /// same exterior-only prefab, so the palette used to give away which apartments
        /// are playable. Repaint each leaf from the shared palette instead.
        /// </summary>
        private void ApplyApartmentDoorMaterial(Transform instance, int apartmentNumber)
        {
            if (ApartmentDoorMaterials == null || ApartmentDoorMaterials.Length == 0)
                return;

            Transform entrance = FindDescendant(instance, "Apartment_Entrance_Door");
            if (entrance == null)
                return;

            Renderer[] renderers = entrance.GetComponentsInChildren<Renderer>(true);
            Renderer leaf = null;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null && renderers[i].name == "Door_Panel")
                {
                    leaf = renderers[i];
                    break;
                }
            }

            if (leaf == null)
                return;

            Material material = ApartmentDoorMaterials[
                ScrambleDoorColorIndex(GenerationSeed, apartmentNumber) %
                ApartmentDoorMaterials.Length];
            if (material != null)
                leaf.sharedMaterial = material;
        }

        /// <summary>
        /// Route apartments sit on regular slot patterns, so a weakly mixed hash would
        /// still line the colours up with them. Avalanche the bits before picking.
        /// </summary>
        private static int ScrambleDoorColorIndex(int seed, int apartmentNumber)
        {
            unchecked
            {
                uint value = (uint)(seed * 73856093) ^ (uint)(apartmentNumber * 19349663);
                value ^= value >> 16;
                value *= 2246822519u;
                value ^= value >> 13;
                value *= 3266489917u;
                value ^= value >> 16;
                return (int)(value & 0x7fffffff);
            }
        }

        private void AdaptExteriorOnlyEntry(
            Transform instance,
            MiniVanPanelkaApartmentTemplate exteriorTemplate,
            int layoutVariant)
        {
            MiniVanPanelkaApartmentTemplateCatalog catalog =
                GetApartmentTemplateCatalog();
            GameObject layoutPrefab =
                catalog != null ? catalog.GetPrefab(layoutVariant) : null;
            MiniVanPanelkaApartmentTemplate layoutTemplate =
                layoutPrefab != null
                    ? layoutPrefab.GetComponent<MiniVanPanelkaApartmentTemplate>()
                    : null;
            if (layoutTemplate == null ||
                layoutTemplate.EntrySocket == null ||
                exteriorTemplate == null ||
                exteriorTemplate.EntrySocket == null)
            {
                return;
            }

            float desiredEntryZ = layoutTemplate.transform
                .InverseTransformPoint(layoutTemplate.EntrySocket.position).z;
            float currentEntryZ = exteriorTemplate.transform
                .InverseTransformPoint(exteriorTemplate.EntrySocket.position).z;
            float offset = desiredEntryZ - currentEntryZ;
            if (Mathf.Abs(offset) <= 0.001f)
                return;

            Transform wallBefore = FindDescendant(
                instance, "FacadeWall_Envelope_West_0");
            Transform wallHeader = FindDescendant(
                instance, "FacadeWall_Envelope_West_Header_0");
            Transform wallAfter = FindDescendant(
                instance, "FacadeWall_Envelope_West_1");
            Transform frame = FindDescendant(instance, "Door_Frame_Entrance");
            Transform entrance = FindDescendant(
                instance, "Apartment_Entrance_Door");
            if (wallBefore == null ||
                wallHeader == null ||
                wallAfter == null ||
                frame == null ||
                entrance == null)
            {
                throw new InvalidOperationException(
                    exteriorTemplate.TemplateId +
                    " cannot adapt its sealed entrance to layout " +
                    layoutVariant + ".");
            }

            float outerMin =
                wallBefore.localPosition.z - wallBefore.localScale.z * 0.5f;
            float outerMax =
                wallAfter.localPosition.z + wallAfter.localScale.z * 0.5f;
            float openingHalf = wallHeader.localScale.z * 0.5f;
            SetLocalZSegment(
                wallBefore,
                outerMin,
                desiredEntryZ - openingHalf);
            SetLocalZSegment(
                wallAfter,
                desiredEntryZ + openingHalf,
                outerMax);

            Vector3 headerPosition = wallHeader.localPosition;
            headerPosition.z = desiredEntryZ;
            wallHeader.localPosition = headerPosition;

            Vector3 framePosition = frame.localPosition;
            framePosition.z += offset;
            frame.localPosition = framePosition;

            Vector3 entrancePosition = entrance.localPosition;
            entrancePosition.z += offset;
            entrance.localPosition = entrancePosition;

            Vector3 socketPosition =
                exteriorTemplate.EntrySocket.localPosition;
            socketPosition.z = desiredEntryZ;
            exteriorTemplate.EntrySocket.localPosition = socketPosition;
        }

        private static void SetLocalZSegment(
            Transform segment,
            float minimum,
            float maximum)
        {
            Vector3 position = segment.localPosition;
            position.z = (minimum + maximum) * 0.5f;
            segment.localPosition = position;

            Vector3 scale = segment.localScale;
            scale.z = Mathf.Max(0.01f, maximum - minimum);
            segment.localScale = scale;
        }

        private void BuildTemplatePhysicsProxies(
            Transform apartment,
            MiniVanPanelkaApartmentTemplate template)
        {
            if (apartment == null || template == null || template.ContentRoot == null)
            {
                return;
            }

            Transform proxyRoot = Group("Template_Positive_Physics_Proxies", apartment);
            Renderer[] renderers = template.ContentRoot.GetComponentsInChildren<Renderer>(false);
            int floorProxyIndex = 0;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null ||
                    !renderer.gameObject.activeInHierarchy ||
                    renderer.name != "RoomFloorFinish")
                {
                    continue;
                }

                CreateBoundsColliderProxy(
                    proxyRoot,
                    "Floor_Collider_Proxy_" + floorProxyIndex.ToString("00"),
                    TransformBoundsToLocal(apartment, renderer.bounds),
                    0.35f);
                floorProxyIndex++;
            }

            MiniVanPanelkaRoomDoor[] doors =
                template.GetComponentsInChildren<MiniVanPanelkaRoomDoor>(false);
            EnsureTemplateRuntimeDoorProxyCoverage(doors);
        }

        private static void EnsureTemplateRuntimeDoorProxyCoverage(
            MiniVanPanelkaRoomDoor[] doors)
        {
            if (doors == null)
            {
                return;
            }

            for (int i = 0; i < doors.Length; i++)
            {
                MiniVanPanelkaRoomDoor door = doors[i];
                if (door == null || door.Pivot == null)
                {
                    continue;
                }

                Renderer runtimePanel = door.Pivot
                    .GetComponentsInChildren<Renderer>(true)
                    .FirstOrDefault(renderer => renderer != null &&
                                                renderer.name == "Door_Panel");
                if (runtimePanel == null)
                {
                    continue;
                }

                runtimePanel.enabled = true;
                Collider collider = runtimePanel.GetComponent<Collider>();
                if (collider != null)
                {
                    collider.enabled = true;
                    collider.isTrigger = false;
                }

                MiniVanPanelkaDoorCollisionProxy proxy =
                    runtimePanel.GetComponent<MiniVanPanelkaDoorCollisionProxy>();
                if (proxy == null)
                {
                    proxy = runtimePanel.gameObject
                        .AddComponent<MiniVanPanelkaDoorCollisionProxy>();
                }

                proxy.Configure(door, runtimePanel);
            }
        }

        private void BuildSealedTemplateDoorVisuals(
            Transform apartment,
            MiniVanPanelkaApartmentTemplate template)
        {
            if (apartment == null || template == null || template.ContentRoot == null)
            {
                return;
            }

            Renderer[] renderers = template.ContentRoot.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer panel = renderers[i];
                if (panel == null ||
                    panel.name != "Door_Panel" ||
                    !HasNamedAncestor(panel.transform, "Apartment_Entrance_Door"))
                {
                    continue;
                }

                panel.enabled = true;
                Collider[] colliders = panel.GetComponentsInChildren<Collider>(true);
                for (int colliderIndex = 0;
                     colliderIndex < colliders.Length;
                     colliderIndex++)
                {
                    colliders[colliderIndex].enabled = true;
                    colliders[colliderIndex].isTrigger = false;
                }
            }
        }

        private static void CreateBoundsColliderProxy(
            Transform parent,
            string name,
            Bounds localBounds,
            float minimumHeight)
        {
            GameObject proxy = new GameObject(name);
            proxy.transform.SetParent(parent, false);
            proxy.transform.localPosition = localBounds.center;
            proxy.transform.localRotation = Quaternion.identity;
            proxy.transform.localScale = Vector3.one;

            BoxCollider collider = proxy.AddComponent<BoxCollider>();
            Vector3 size = new Vector3(
                Mathf.Max(0.04f, localBounds.size.x),
                Mathf.Max(minimumHeight, localBounds.size.y),
                Mathf.Max(0.04f, localBounds.size.z));
            if (size.y > localBounds.size.y)
            {
                proxy.transform.localPosition = new Vector3(
                    localBounds.center.x,
                    localBounds.max.y - size.y * 0.5f,
                    localBounds.center.z);
            }

            collider.center = Vector3.zero;
            collider.size = size;
            collider.isTrigger = false;
            collider.enabled = true;
        }

        private MiniVanPanelkaApartmentTemplateCatalog GetApartmentTemplateCatalog()
        {
            if (apartmentTemplateCatalog == null)
            {
                apartmentTemplateCatalog =
                    Resources.Load<MiniVanPanelkaApartmentTemplateCatalog>(
                        ApartmentTemplateCatalogResourcePath);
            }
            return apartmentTemplateCatalog;
        }

        private GameObject InstantiateApartmentPrefab(GameObject prefab, Transform parent)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying && !ExteriorOnlyLocked)
            {
                GameObject instance = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
                if (instance != null)
                    return instance;
            }
#endif
            return UnityEngine.Object.Instantiate(prefab, parent);
        }

        private static MiniVanApartmentDoor FindTemplateEntranceDoor(
            MiniVanPanelkaApartmentTemplate template)
        {
            MiniVanApartmentDoor[] doors =
                template.GetComponentsInChildren<MiniVanApartmentDoor>(true);
            for (int i = 0; i < doors.Length; i++)
            {
                if (doors[i].name == "Apartment_Entrance_Door")
                    return doors[i];
            }
            return null;
        }

        private static void SetTemplateExteriorOnly(MiniVanPanelkaApartmentTemplate template)
        {
            Transform content = template.ContentRoot;
            Transform apartmentRoot = content != null && content.childCount > 0
                ? content.GetChild(0)
                : null;
            if (apartmentRoot == null)
                return;

            for (int i = apartmentRoot.childCount - 1; i >= 0; i--)
            {
                Transform child = apartmentRoot.GetChild(i);
                if (child.name == "Apartment_Entrance_Door")
                {
                    SealTemplateEntranceDoor(child);
                    continue;
                }
                if (child.name == "APARTMENT_LAYOUT_SHELL")
                {
                    StripTemplateShellToExterior(child);
                    continue;
                }
                if (child.name.StartsWith("Door_Frame_", StringComparison.Ordinal))
                {
                    child.gameObject.SetActive(true);
                    continue;
                }

                DestroyTemplateObject(child.gameObject);
            }
        }

        private static void StripTemplateShellToExterior(Transform shell)
        {
            for (int i = shell.childCount - 1; i >= 0; i--)
            {
                Transform child = shell.GetChild(i);
                bool keep =
                    child.name.StartsWith("FacadeWall_", StringComparison.Ordinal) ||
                    child.GetComponentInChildren<MiniVanPanelkaApartmentFacadeMarker>(true) != null;
                if (keep)
                {
                    child.gameObject.SetActive(true);
                    continue;
                }

                DestroyTemplateObject(child.gameObject);
            }
        }

        private static void SealTemplateEntranceDoor(Transform entrance)
        {
            if (entrance == null)
            {
                return;
            }

            entrance.gameObject.SetActive(true);
            MiniVanPanelkaRoomDoor[] roomDoors =
                entrance.GetComponentsInChildren<MiniVanPanelkaRoomDoor>(true);
            for (int i = 0; i < roomDoors.Length; i++)
            {
                DestroyTemplateObject(roomDoors[i]);
            }

            MiniVanPanelkaInteractable[] interactables =
                entrance.GetComponentsInChildren<MiniVanPanelkaInteractable>(true);
            for (int i = 0; i < interactables.Length; i++)
            {
                DestroyTemplateObject(interactables[i]);
            }

            Transform pivot = entrance.Find("Door_Runtime_Pivot");
            if (pivot != null)
            {
                pivot.localRotation = Quaternion.identity;
            }

            Collider[] colliders = entrance.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                colliders[i].enabled = true;
                colliders[i].isTrigger = false;
            }
        }

        private static void DestroyTemplateObject(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(target);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        private static void RemoveDuplicateSharedBoundary(
            MiniVanPanelkaApartmentTemplate template,
            bool north)
        {
            if (!north || template.ContentRoot == null)
                return;

            Transform[] transforms = template.ContentRoot.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i].name.StartsWith(
                        "FacadeWall_Envelope_South",
                        StringComparison.Ordinal))
                {
                    transforms[i].gameObject.SetActive(false);
                }
            }
        }

        private static void ValidateTemplatePlacement(
            Transform apartment,
            MiniVanPanelkaApartmentTemplate template,
            Vector3 targetEntry,
            float outerX,
            float outerZ,
            string cornerName)
        {
            Vector3 actualEntry = apartment.InverseTransformPoint(template.EntrySocket.position);
            if (Vector3.Distance(actualEntry, targetEntry) > 0.035f)
            {
                throw new InvalidOperationException(
                    template.TemplateId + " entry socket does not match the " +
                    cornerName + " stairwell opening.");
            }

            MiniVanPanelkaApartmentFacadeMarker[] windows =
                template.GetComponentsInChildren<MiniVanPanelkaApartmentFacadeMarker>(true);
            for (int i = 0; i < windows.Length; i++)
            {
                Renderer renderer = windows[i].GetComponentsInChildren<Renderer>(true)
                    .FirstOrDefault(item => item.name == "Breakable_Glass");
                if (renderer == null)
                    continue;

                Vector3 worldCenter = renderer.transform.TransformPoint(
                    renderer.localBounds.center);
                Vector3 center = apartment.InverseTransformPoint(worldCenter);
                bool onOuterX = Mathf.Abs(center.x - outerX) <= 0.28f;
                bool onOuterZ = Mathf.Abs(center.z - outerZ) <= 0.28f;
                if (!onOuterX && !onOuterZ)
                {
                    throw new InvalidOperationException(
                        template.TemplateId + " places " + windows[i].name +
                        " inside the building at corner " + cornerName +
                        ". Window center=" + center.ToString("F3") +
                        ", expected outerX=" + outerX.ToString("F3") +
                        " or outerZ=" + outerZ.ToString("F3") + ".");
                }
            }
        }

        private static void ValidateTemplateDoorClearances(
            MiniVanPanelkaApartmentTemplate template)
        {
            MiniVanPanelkaRoomDoor[] doors =
                template.GetComponentsInChildren<MiniVanPanelkaRoomDoor>(true);
            MiniVanPanelkaFurnitureAnchor[] furniture =
                template.GetComponentsInChildren<MiniVanPanelkaFurnitureAnchor>(true);
            for (int doorIndex = 0; doorIndex < doors.Length; doorIndex++)
            {
                if (!doors[doorIndex].name.StartsWith(
                        "Interior_Door_",
                        StringComparison.Ordinal))
                {
                    continue;
                }

                Renderer panel = doors[doorIndex].GetComponentsInChildren<Renderer>(true)
                    .FirstOrDefault(item => item.name == "Door_Panel");
                if (panel == null)
                    continue;

                Bounds clearance = TransformBoundsTo(
                    panel.localBounds,
                    template.transform.worldToLocalMatrix *
                    panel.transform.localToWorldMatrix);
                clearance.Expand(new Vector3(0.55f, 0.15f, 0.55f));

                for (int furnitureIndex = 0; furnitureIndex < furniture.Length; furnitureIndex++)
                {
                    Renderer[] renderers =
                        furniture[furnitureIndex].GetComponentsInChildren<Renderer>(true);
                    for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
                    {
                        Bounds furnitureBounds = TransformBoundsTo(
                            renderers[rendererIndex].localBounds,
                            template.transform.worldToLocalMatrix *
                            renderers[rendererIndex].transform.localToWorldMatrix);
                        if (!clearance.Intersects(furnitureBounds))
                            continue;

                        throw new InvalidOperationException(
                            template.TemplateId + " blocks " + doors[doorIndex].name +
                            " with " + furniture[furnitureIndex].name + ".");
                    }
                }
            }
        }

        private static Bounds TransformBoundsTo(Bounds bounds, Matrix4x4 matrix)
        {
            Vector3 minimum = new Vector3(
                float.PositiveInfinity,
                float.PositiveInfinity,
                float.PositiveInfinity);
            Vector3 maximum = new Vector3(
                float.NegativeInfinity,
                float.NegativeInfinity,
                float.NegativeInfinity);
            for (int cornerIndex = 0; cornerIndex < 8; cornerIndex++)
            {
                Vector3 sign = new Vector3(
                    (cornerIndex & 1) == 0 ? -1f : 1f,
                    (cornerIndex & 2) == 0 ? -1f : 1f,
                    (cornerIndex & 4) == 0 ? -1f : 1f);
                Vector3 corner = bounds.center + Vector3.Scale(bounds.extents, sign);
                Vector3 transformed = matrix.MultiplyPoint3x4(corner);
                minimum = Vector3.Min(minimum, transformed);
                maximum = Vector3.Max(maximum, transformed);
            }

            Bounds result = new Bounds();
            result.SetMinMax(minimum, maximum);
            return result;
        }

        private bool BuildTemplateRouteHole(
            Transform apartment,
            MiniVanPanelkaApartmentTemplate template,
            int floorIndex,
            MiniVanPanelkaApartmentRouteRole routeRole,
            string targetCorner,
            out Vector3 routeHoleCenter)
        {
            routeHoleCenter = Vector3.zero;
            if (routeRole != MiniVanPanelkaApartmentRouteRole.MainRoute ||
                !IsHoleTransitionFromFloor(floorIndex + 1) ||
                template.RouteHoleSocket == null)
            {
                return false;
            }

            const float holeWidth = 1.68f;
            const float holeDepth = 1.58f;
            const float wallClearance = 0.35f;
            if (!TryResolveSharedRouteHoleCenter(
                    apartment,
                    template,
                    floorIndex,
                    holeWidth,
                    holeDepth,
                    wallClearance,
                    out Vector3 worldHoleCenter,
                    out Transform lowerApartment,
                    out MiniVanPanelkaApartmentTemplate lowerTemplate))
            {
                throw new InvalidOperationException(
                    name + " cannot place a floor hole inside rooms on floors " +
                    (floorIndex + 1) + " and " + floorIndex + ".");
            }

            routeHoleCenter = apartment.InverseTransformPoint(worldHoleCenter);
            routeHoleCenter.y = floorIndex * StoreyHeight + FloorSurfaceOffset;
            worldHoleCenter = apartment.TransformPoint(routeHoleCenter);
            DisableTemplateFloorAtHole(template, worldHoleCenter);
            if (!CutLowerApartmentCeilingAtHole(
                    lowerApartment,
                    lowerTemplate,
                    worldHoleCenter,
                    holeWidth,
                    holeDepth))
            {
                throw new InvalidOperationException(
                    name + " cannot cut the lower apartment ceiling below " +
                    (floorIndex + 1) + " floor route hole.");
            }
            DisableTemplateRouteFeatureFurniture(
                lowerTemplate,
                lowerApartment,
                true,
                lowerApartment.InverseTransformPoint(worldHoleCenter),
                false,
                Vector3.zero,
                FloorTop + (floorIndex - 1) * StoreyHeight);

            Vector2 orientedFootprint = template.Footprint;
            float minX = template.transform.localPosition.x - orientedFootprint.x * 0.5f;
            float maxX = template.transform.localPosition.x + orientedFootprint.x * 0.5f;
            float minZ = template.transform.localPosition.z - orientedFootprint.y * 0.5f;
            float maxZ = template.transform.localPosition.z + orientedFootprint.y * 0.5f;
            float holeMinX = routeHoleCenter.x - holeWidth * 0.5f;
            float holeMaxX = routeHoleCenter.x + holeWidth * 0.5f;
            float holeMinZ = routeHoleCenter.z - holeDepth * 0.5f;
            float holeMaxZ = routeHoleCenter.z + holeDepth * 0.5f;
            float slabY = floorIndex * StoreyHeight + FloorSurfaceOffset - FloorSlabThickness * 0.5f;

            AddFloorSlab("TemplateFloor_SouthOfHole", apartment, minX, maxX, minZ, holeMinZ, slabY);
            AddFloorSlab("TemplateFloor_NorthOfHole", apartment, minX, maxX, holeMaxZ, maxZ, slabY);
            AddFloorSlab("TemplateFloor_WestOfHole", apartment, minX, holeMinX, holeMinZ, holeMaxZ, slabY);
            AddFloorSlab("TemplateFloor_EastOfHole", apartment, holeMaxX, maxX, holeMinZ, holeMaxZ, slabY);

            Transform marker = Group(
                "Route_Template_Floor_Hole_From_" + (floorIndex + 1).ToString("00") +
                "_To_" + floorIndex.ToString("00"),
                apartment);
            Transform clearance = Group(
                "Route_Hole_Clearance_Volume",
                marker);
            clearance.localPosition = routeHoleCenter;
            clearance.localScale = new Vector3(
                holeWidth,
                StoreyHeight,
                holeDepth);
            Vector2[] outline =
            {
                new Vector2(holeMinX, holeMinZ),
                new Vector2(holeMaxX, holeMinZ),
                new Vector2(holeMaxX, holeMaxZ),
                new Vector2(holeMinX, holeMaxZ)
            };
            for (int i = 0; i < outline.Length; i++)
            {
                AddBrokenHoleEdge(
                    marker,
                    outline[i],
                    outline[(i + 1) % outline.Length],
                    routeHoleCenter.y + 0.03f,
                    i);
            }
            BuildRouteHoleRope(
                marker,
                routeHoleCenter.x,
                routeHoleCenter.z,
                floorIndex * StoreyHeight,
                floorIndex);
            return true;
        }

        private bool TryResolveSharedRouteHoleCenter(
            Transform upperApartment,
            MiniVanPanelkaApartmentTemplate upperTemplate,
            int upperFloorIndex,
            float holeWidth,
            float holeDepth,
            float wallClearance,
            out Vector3 worldHoleCenter,
            out Transform lowerApartment,
            out MiniVanPanelkaApartmentTemplate lowerTemplate)
        {
            worldHoleCenter = Vector3.zero;
            lowerApartment = null;
            lowerTemplate = null;

            MiniVanPanelkaApartmentRouteMarker upperMarker =
                upperApartment.GetComponent<MiniVanPanelkaApartmentRouteMarker>();
            if (upperMarker == null || upperFloorIndex <= 0)
            {
                return false;
            }

            MiniVanPanelkaApartmentRouteMarker lowerMarker =
                GetComponentsInChildren<MiniVanPanelkaApartmentRouteMarker>(true)
                    .FirstOrDefault(candidate =>
                        candidate.FloorNumber == upperMarker.FloorNumber - 1 &&
                        candidate.ApartmentSlot == upperMarker.ApartmentSlot &&
                        candidate.Role !=
                        MiniVanPanelkaApartmentRouteRole.Inaccessible);
            if (lowerMarker == null)
            {
                return false;
            }

            lowerApartment = lowerMarker.transform;
            lowerTemplate =
                lowerApartment.GetComponentInChildren<
                    MiniVanPanelkaApartmentTemplate>(true);
            if (lowerTemplate == null)
            {
                return false;
            }

            MiniVanPanelkaRoomIdentity[] upperRooms =
                upperTemplate.GetComponentsInChildren<
                    MiniVanPanelkaRoomIdentity>(true);
            MiniVanPanelkaRoomIdentity[] lowerRooms =
                lowerTemplate.GetComponentsInChildren<
                    MiniVanPanelkaRoomIdentity>(true);
            Vector3 preferred = transform.InverseTransformPoint(
                upperTemplate.RouteHoleSocket.position);
            float bestScore = float.PositiveInfinity;
            Vector3 bestCenter = Vector3.zero;
            bool found = false;

            for (int upperIndex = 0;
                 upperIndex < upperRooms.Length;
                 upperIndex++)
            {
                Bounds upperBounds = GetRoomBoundsInGenerator(
                    upperTemplate,
                    upperRooms[upperIndex]);
                for (int lowerIndex = 0;
                     lowerIndex < lowerRooms.Length;
                     lowerIndex++)
                {
                    Bounds lowerBounds = GetRoomBoundsInGenerator(
                        lowerTemplate,
                        lowerRooms[lowerIndex]);
                    float centerMinX =
                        Mathf.Max(upperBounds.min.x, lowerBounds.min.x) +
                        wallClearance + holeWidth * 0.5f;
                    float centerMaxX =
                        Mathf.Min(upperBounds.max.x, lowerBounds.max.x) -
                        wallClearance - holeWidth * 0.5f;
                    float centerMinZ =
                        Mathf.Max(upperBounds.min.z, lowerBounds.min.z) +
                        wallClearance + holeDepth * 0.5f;
                    float centerMaxZ =
                        Mathf.Min(upperBounds.max.z, lowerBounds.max.z) -
                        wallClearance - holeDepth * 0.5f;
                    if (centerMinX > centerMaxX ||
                        centerMinZ > centerMaxZ)
                    {
                        continue;
                    }

                    Vector3 candidate = new Vector3(
                        Mathf.Clamp(preferred.x, centerMinX, centerMaxX),
                        preferred.y,
                        Mathf.Clamp(preferred.z, centerMinZ, centerMaxZ));
                    float score =
                        (candidate - preferred).sqrMagnitude;
                    if (score >= bestScore)
                    {
                        continue;
                    }

                    bestScore = score;
                    bestCenter = candidate;
                    found = true;
                }
            }

            if (!found)
            {
                return false;
            }

            bestCenter.y = transform.InverseTransformPoint(
                upperTemplate.RouteHoleSocket.position).y;
            worldHoleCenter = transform.TransformPoint(bestCenter);
            return true;
        }

        private Bounds GetRoomBoundsInGenerator(
            MiniVanPanelkaApartmentTemplate template,
            MiniVanPanelkaRoomIdentity room)
        {
            return TransformBoundsTo(
                new Bounds(
                    room.RoomCenterLocal,
                    room.RoomSizeLocal),
                transform.worldToLocalMatrix *
                template.transform.localToWorldMatrix);
        }

        private bool CutLowerApartmentCeilingAtHole(
            Transform lowerApartment,
            MiniVanPanelkaApartmentTemplate lowerTemplate,
            Vector3 worldHoleCenter,
            float holeWidth,
            float holeDepth)
        {
            Transform[] ceilingRoots =
                lowerTemplate.GetComponentsInChildren<Transform>(true)
                    .Where(candidate =>
                        candidate.name == "EXPLICIT_CEILING")
                    .ToArray();
            Vector3 localHoleCenter =
                lowerApartment.InverseTransformPoint(worldHoleCenter);
            float holeMinX = localHoleCenter.x - holeWidth * 0.5f;
            float holeMaxX = localHoleCenter.x + holeWidth * 0.5f;
            float holeMinZ = localHoleCenter.z - holeDepth * 0.5f;
            float holeMaxZ = localHoleCenter.z + holeDepth * 0.5f;
            int cutSectionCount = 0;

            for (int rootIndex = 0;
                 rootIndex < ceilingRoots.Length;
                 rootIndex++)
            {
                Renderer[] renderers =
                    ceilingRoots[rootIndex].GetComponentsInChildren<
                        Renderer>(true);
                for (int rendererIndex = 0;
                     rendererIndex < renderers.Length;
                     rendererIndex++)
                {
                    Renderer renderer = renderers[rendererIndex];
                    Bounds bounds = TransformBoundsTo(
                        renderer.localBounds,
                        lowerApartment.worldToLocalMatrix *
                        renderer.transform.localToWorldMatrix);
                    float cutMinX = Mathf.Max(bounds.min.x, holeMinX);
                    float cutMaxX = Mathf.Min(bounds.max.x, holeMaxX);
                    float cutMinZ = Mathf.Max(bounds.min.z, holeMinZ);
                    float cutMaxZ = Mathf.Min(bounds.max.z, holeMaxZ);
                    if (cutMaxX - cutMinX < 0.01f ||
                        cutMaxZ - cutMinZ < 0.01f)
                    {
                        continue;
                    }

                    Material material = renderer.sharedMaterial != null
                        ? renderer.sharedMaterial
                        : InteriorMaterial;
                    float ceilingY = bounds.center.y;
                    float thickness = Mathf.Max(0.06f, bounds.size.y);
                    string sectionSuffix = "_" +
                        cutSectionCount.ToString("00");

                    AddRouteCeilingSlab(
                        "RouteCeiling_SouthOfHole" + sectionSuffix,
                        lowerApartment,
                        bounds.min.x,
                        bounds.max.x,
                        bounds.min.z,
                        cutMinZ,
                        ceilingY,
                        thickness,
                        material);
                    AddRouteCeilingSlab(
                        "RouteCeiling_NorthOfHole" + sectionSuffix,
                        lowerApartment,
                        bounds.min.x,
                        bounds.max.x,
                        cutMaxZ,
                        bounds.max.z,
                        ceilingY,
                        thickness,
                        material);
                    AddRouteCeilingSlab(
                        "RouteCeiling_WestOfHole" + sectionSuffix,
                        lowerApartment,
                        bounds.min.x,
                        cutMinX,
                        cutMinZ,
                        cutMaxZ,
                        ceilingY,
                        thickness,
                        material);
                    AddRouteCeilingSlab(
                        "RouteCeiling_EastOfHole" + sectionSuffix,
                        lowerApartment,
                        cutMaxX,
                        bounds.max.x,
                        cutMinZ,
                        cutMaxZ,
                        ceilingY,
                        thickness,
                        material);
                    ceilingRoots[rootIndex].gameObject.SetActive(false);
                    cutSectionCount++;
                }
            }

            return cutSectionCount > 0;
        }

        private void AddRouteCeilingSlab(
            string name,
            Transform parent,
            float minX,
            float maxX,
            float minZ,
            float maxZ,
            float y,
            float thickness,
            Material material)
        {
            if (maxX - minX < 0.04f ||
                maxZ - minZ < 0.04f)
            {
                return;
            }

            Box(
                name,
                parent,
                new Vector3(
                    (minX + maxX) * 0.5f,
                    y,
                    (minZ + maxZ) * 0.5f),
                new Vector3(
                    maxX - minX,
                    thickness,
                    maxZ - minZ),
                material);
        }

        private static void DisableTemplateFloorAtHole(
            MiniVanPanelkaApartmentTemplate template,
            Vector3 worldHoleCenter)
        {
            Transform[] transforms = template.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform candidate = transforms[i];
                if (candidate.name == "Floor")
                {
                    candidate.gameObject.SetActive(false);
                    continue;
                }
                if (candidate.name != "RoomFloorFinish" && candidate.name != "Floor_Finish")
                    continue;
                Renderer renderer = candidate.GetComponent<Renderer>();
                if (renderer == null)
                    continue;
                Bounds bounds = renderer.bounds;
                if (worldHoleCenter.x >= bounds.min.x && worldHoleCenter.x <= bounds.max.x &&
                    worldHoleCenter.z >= bounds.min.z && worldHoleCenter.z <= bounds.max.z)
                {
                    candidate.gameObject.SetActive(false);
                }
            }
        }

        private static void DisableTemplateRouteFeatureFurniture(
            MiniVanPanelkaApartmentTemplate template,
            Transform apartment,
            bool hasHole,
            Vector3 holeCenter,
            bool hasBalcony,
            Vector3 balconyCenter,
            float yBase)
        {
            if ((!hasHole && !hasBalcony) || template.ContentRoot == null)
                return;

            Vector3 holeWorldCenter = apartment.TransformPoint(holeCenter);
            Vector3 balconyWorldCenter = apartment.TransformPoint(balconyCenter);
            Renderer[] renderers = template.ContentRoot.GetComponentsInChildren<Renderer>(true);
            System.Collections.Generic.HashSet<Transform> disabled =
                new System.Collections.Generic.HashSet<Transform>();
            for (int i = 0; i < renderers.Length; i++)
            {
                Bounds bounds = renderers[i].bounds;
                if (bounds.min.y > yBase + 1.45f)
                    continue;

                bool overlapsHole = hasHole &&
                    Mathf.Abs(bounds.center.x - holeWorldCenter.x) < bounds.extents.x + 0.95f &&
                    Mathf.Abs(bounds.center.z - holeWorldCenter.z) < bounds.extents.z + 0.95f;
                bool overlapsBalcony = hasBalcony &&
                    Mathf.Abs(bounds.center.x - balconyWorldCenter.x) < bounds.extents.x + 1.05f &&
                    Mathf.Abs(bounds.center.z - balconyWorldCenter.z) < bounds.extents.z + 1.05f;
                if (!overlapsHole && !overlapsBalcony)
                    continue;

                Transform unit = FindFurnishingUnit(renderers[i].transform);
                if (unit != null)
                    disabled.Add(unit);
            }

            foreach (Transform unit in disabled)
                unit.gameObject.SetActive(false);
        }

        private static void DisableTemplateEntranceFurniture(
            MiniVanPanelkaApartmentTemplate template,
            MiniVanApartmentDoor entranceDoor)
        {
            if (template == null ||
                template.ContentRoot == null ||
                entranceDoor == null ||
                entranceDoor.Pivot == null)
            {
                return;
            }

            Renderer panel = entranceDoor.Pivot
                .GetComponentsInChildren<Renderer>(true)
                .FirstOrDefault(renderer => renderer.name == "Door_Panel");
            if (panel == null)
                return;

            Vector3 panelCenterLocal =
                template.transform.InverseTransformPoint(panel.bounds.center);
            Bounds entranceClearance = new Bounds(
                panelCenterLocal + Vector3.right * 0.90f,
                new Vector3(2.05f, 2.25f, 1.90f));
            MiniVanPanelkaFurnitureAnchor[] furniture =
                template.ContentRoot.GetComponentsInChildren<
                    MiniVanPanelkaFurnitureAnchor>(true);
            for (int furnitureIndex = 0;
                 furnitureIndex < furniture.Length;
                 furnitureIndex++)
            {
                MiniVanPanelkaFurnitureAnchor item = furniture[furnitureIndex];
                if (item == null || !item.gameObject.activeInHierarchy)
                    continue;

                Renderer[] renderers =
                    item.GetComponentsInChildren<Renderer>(true);
                bool blocksEntrance = false;
                for (int rendererIndex = 0;
                     rendererIndex < renderers.Length;
                     rendererIndex++)
                {
                    Bounds furnitureBounds = TransformBoundsTo(
                        renderers[rendererIndex].localBounds,
                        template.transform.worldToLocalMatrix *
                        renderers[rendererIndex].transform.localToWorldMatrix);
                    if (furnitureBounds.max.y <= 0.35f ||
                        !entranceClearance.Intersects(furnitureBounds))
                    {
                        continue;
                    }

                    blocksEntrance = true;
                    break;
                }

                if (blocksEntrance)
                    item.gameObject.SetActive(false);
            }
        }
    }
}
