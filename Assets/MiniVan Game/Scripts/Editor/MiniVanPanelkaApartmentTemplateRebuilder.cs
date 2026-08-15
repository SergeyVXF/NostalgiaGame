using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MiniVanGame;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class MiniVanPanelkaApartmentTemplateRebuilder
{
    private enum TemplateVariant
    {
        Standard,
        CornerLeft,
        CornerRight
    }

    private const string TriggerPath = "Library/CodexTools/RebuildApartmentTemplates.flag";
    private const string ResultPath = "Library/CodexTools/RebuildApartmentTemplates.result";
    private const string TemplateFolder =
        "Assets/MiniVan Game/Prefabs/Panelka/Interiors/ApartmentTemplates";
    private const string CatalogPath =
        "Assets/MiniVan Game/Resources/Panelka/ApartmentTemplateCatalog.asset";
    private const string MaterialFolder =
        "Assets/MiniVan Game/Materials/Panelka/Interior/LowPolyPack/";
    private const string ExteriorMaterialPath =
        "Assets/MiniVan Game/Materials/Panelka/Stage1/PanelkaStage1_Exterior.mat";
    private const string WindowGlassMaterialPath =
        "Assets/MiniVan Game/Materials/Panelka/Stage1/PanelkaStage1_WindowGlassGenerated.mat";
    private const string FurnitureFolder =
        "Assets/MiniVan Game/Prefabs/Panelka/Interiors/Furniture/";

    private const float MinX = -4.4f;
    private const float MaxX = 4.4f;
    private const float MinZ = -4.5f;
    private const float MaxZ = 4.5f;
    private const float WallHeight = 3f;
    private const float FacadeStoreyHeight = 3.2f;
    private const float FacadeVerticalOverlap = 0.04f;
    private const float WallThickness = 0.16f;
    private const float FloorTop = 0.08f;
    private const float DoorWidth = 1.06f;
    // Walls are centered on room edges; furniture must hug the INNER face.
    private const float HalfWallThickness = WallThickness * 0.5f;
    // Extra inset from the inner face into the room before mesh flush.
    private const float WallFurnitureInset = 0f;
    // Floors stop at room wall centerlines: under own wall half-thickness, never into neighbor rooms.
    private const float FloorWallOverlap = 0f;
    private const float RoomBoundsMargin = 0.02f;
    // Tiny plaster bite past the inner face (not past the wall centerline).
    private const float WallMeshBite = 0.008f;
    // Rule 1.A: only the door leaf/opening is forbidden; frame may be touched.
    private const float DoorOpeningClearanceExpand = 0.02f;

    private sealed class RoomSpec
    {
        public string Id;
        public Rect Bounds;
        public string Surface;
        public string[] DoorEdges;
        public readonly List<FurnitureSpec> Furniture = new List<FurnitureSpec>();
    }

    private sealed class FurnitureSpec
    {
        public string Path;
        public Vector2 Back;
        public Vector2 Front;
        public float Scale = 1f;
    }

    private sealed class OpeningSpec
    {
        public float Center;
        public float Width;
        public bool IsWindow;
        public bool IsEntrance;
        public string Id;
        public string RoomId;
    }

    private sealed class WallSpec
    {
        public string Id;
        public bool AlongX;
        public float Fixed;
        public float Min;
        public float Max;
        public bool Envelope;
        public string Surface;
        public readonly List<OpeningSpec> Openings = new List<OpeningSpec>();
    }

    private sealed class LayoutSpec
    {
        public int Index;
        public string Name;
        public TemplateVariant Variant;
        public string Wallpaper;
        public string KitchenWallpaper;
        public string Floor;
        public string WetFloor;
        public string WetTile;
        public string Door;
        public Vector2 RouteHole;
        public Vector2 Balcony;
        public Vector2 Pipe;
        public readonly List<RoomSpec> Rooms = new List<RoomSpec>();
        public readonly List<WallSpec> Walls = new List<WallSpec>();
    }

    static MiniVanPanelkaApartmentTemplateRebuilder()
    {
        EditorApplication.update += RunIfTriggered;
    }

    [MenuItem("MiniVan Game/Panelka/Sync Apartment Template Catalog (Preserve Prefabs)")]
    public static void Rebuild()
    {
        Rebuild(false);
    }

    [MenuItem("MiniVan Game/Panelka/Force Regenerate Apartment Templates...")]
    public static void ForceRegenerate()
    {
        if (!EditorUtility.DisplayDialog(
                "Force regenerate apartment templates?",
                "This replaces all five apartment prefab assets and discards manual prefab edits.",
                "Regenerate",
                "Cancel"))
        {
            return;
        }

        Rebuild(true);
    }

    /// <summary>
    /// Silent force regenerate for tooling / MCP. Overwrites all apartment template prefabs.
    /// </summary>
    public static void ForceRegenerateSilent()
    {
        // door-aware furniture placement rebuild entry
        Rebuild(true);
    }

    private static void Rebuild(bool forceRegenerate)
    {
        try
        {
            Directory.CreateDirectory(TemplateFolder);
            LayoutSpec[] layouts = CreateLayouts();
            GameObject[] prefabs = new GameObject[layouts.Length];
            GameObject[] cornerLeftPrefabs = new GameObject[layouts.Length];
            GameObject[] cornerRightPrefabs = new GameObject[layouts.Length];
            int preservedCount = 0;
            int generatedCount = 0;
            for (int i = 0; i < layouts.Length; i++)
            {
                string prefabPath = GetPrefabPath(layouts[i]);
                GameObject existing =
                    AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (!forceRegenerate && existing != null)
                {
                    EnsureWindowSocketMetadata(prefabPath);
                    prefabs[i] = existing;
                    preservedCount++;
                }
                else
                {
                    prefabs[i] = BuildPrefab(layouts[i]);
                    generatedCount++;
                }

                LayoutSpec cornerLeft =
                    CreateCornerLayout(layouts[i], TemplateVariant.CornerLeft);
                LayoutSpec cornerRight =
                    CreateCornerLayout(layouts[i], TemplateVariant.CornerRight);
                cornerLeftPrefabs[i] = LoadOrBuildPrefab(
                    cornerLeft,
                    forceRegenerate,
                    ref preservedCount,
                    ref generatedCount);
                cornerRightPrefabs[i] = LoadOrBuildPrefab(
                    cornerRight,
                    forceRegenerate,
                    ref preservedCount,
                    ref generatedCount);
            }

            MiniVanPanelkaApartmentTemplateCatalog catalog =
                AssetDatabase.LoadAssetAtPath<MiniVanPanelkaApartmentTemplateCatalog>(CatalogPath);
            if (catalog == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(CatalogPath) ?? string.Empty);
                catalog = ScriptableObject.CreateInstance<MiniVanPanelkaApartmentTemplateCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            catalog.Configure(
                prefabs,
                cornerLeftPrefabs,
                cornerRightPrefabs);
            if (catalog.ExteriorOnlyPrefab != null)
            {
                string exteriorPath =
                    AssetDatabase.GetAssetPath(catalog.ExteriorOnlyPrefab);
                EnsureWindowSocketMetadata(exteriorPath);
            }
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            string summary = "PASS: apartment template catalog synchronized; " +
                             "preserved edited prefabs=" + preservedCount +
                             ", generated prefabs=" + generatedCount +
                             ", catalog entries=" + catalog.Count +
                             ", corner variants=" +
                             (cornerLeftPrefabs.Length +
                              cornerRightPrefabs.Length);
            Directory.CreateDirectory(Path.GetDirectoryName(ResultPath) ?? string.Empty);
            File.WriteAllText(ResultPath, summary + Environment.NewLine);
            Debug.Log("[Apartment Template Rebuilder] " + summary);
        }
        catch (Exception exception)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ResultPath) ?? string.Empty);
            File.WriteAllText(ResultPath, "FAIL: " + exception + Environment.NewLine);
            Debug.LogException(exception);
        }
    }

    private static void RunIfTriggered()
    {
        if (!File.Exists(TriggerPath))
            return;
        string payload = File.ReadAllText(TriggerPath);
        File.Delete(TriggerPath);
        if (payload != null &&
            payload.IndexOf("force", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            Rebuild(true);
            return;
        }

        Rebuild(false);
    }

    private static GameObject LoadOrBuildPrefab(
        LayoutSpec layout,
        bool forceRegenerate,
        ref int preservedCount,
        ref int generatedCount)
    {
        string path = GetPrefabPath(layout);
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (!forceRegenerate && existing != null)
        {
            EnsureWindowSocketMetadata(path);
            preservedCount++;
            return existing;
        }

        generatedCount++;
        return BuildPrefab(layout);
    }

    private static void EnsureWindowSocketMetadata(string prefabPath)
    {
        if (string.IsNullOrEmpty(prefabPath) ||
            AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
        {
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        bool changed = false;
        try
        {
            MiniVanPanelkaApartmentFacadeMarker[] markers =
                root.GetComponentsInChildren<MiniVanPanelkaApartmentFacadeMarker>(true);
            for (int markerIndex = 0; markerIndex < markers.Length; markerIndex++)
            {
                MiniVanPanelkaApartmentFacadeMarker marker = markers[markerIndex];
                MiniVanPanelkaWindowSocket socket =
                    marker.GetComponent<MiniVanPanelkaWindowSocket>();
                Transform windowModule;
                Transform solidModule;
                if (socket == null)
                {
                    Renderer[] legacyRenderers =
                        marker.GetComponentsInChildren<Renderer>(true);
                    if (legacyRenderers.Length == 0)
                        continue;

                    windowModule = NewChild("Window_Module", marker.transform);
                    List<Transform> existingChildren = new List<Transform>();
                    for (int childIndex = 0;
                         childIndex < marker.transform.childCount - 1;
                         childIndex++)
                    {
                        existingChildren.Add(marker.transform.GetChild(childIndex));
                    }
                    for (int childIndex = 0;
                         childIndex < existingChildren.Count;
                         childIndex++)
                    {
                        existingChildren[childIndex].SetParent(windowModule, false);
                    }

                    solidModule = NewChild("Solid_Wall_Module", marker.transform);
                    socket = marker.gameObject.AddComponent<MiniVanPanelkaWindowSocket>();
                }
                else
                {
                    windowModule = socket.WindowModule != null
                        ? socket.WindowModule.transform
                        : marker.transform.Find("Window_Module");
                    solidModule = socket.SolidWallModule != null
                        ? socket.SolidWallModule.transform
                        : marker.transform.Find("Solid_Wall_Module");
                    if (windowModule == null)
                        continue;
                    if (solidModule == null)
                        solidModule = NewChild("Solid_Wall_Module", marker.transform);
                }

                Renderer[] windowRenderers =
                    windowModule.GetComponentsInChildren<Renderer>(true);
                if (windowRenderers.Length == 0)
                    continue;

                Bounds bounds =
                    GetLocalRenderBounds(marker.transform, windowRenderers);
                Material interior = FindNearestInteriorWallMaterial(
                    root.transform,
                    marker.transform,
                    bounds.center);
                Material exterior =
                    AssetDatabase.LoadAssetAtPath<Material>(ExteriorMaterialPath);
                RebuildSolidWallModule(
                    marker,
                    solidModule,
                    bounds,
                    interior,
                    exterior);
                solidModule.gameObject.SetActive(false);

                socket.Configure(
                    ParseWindowRoomId(marker),
                    marker.Side,
                    windowModule.gameObject,
                    solidModule.gameObject);
                changed = true;
            }

            MiniVanPanelkaRoomDoor[] legacyDoors =
                root.GetComponentsInChildren<MiniVanPanelkaRoomDoor>(true);
            for (int doorIndex = 0;
                 doorIndex < legacyDoors.Length;
                 doorIndex++)
            {
                MiniVanPanelkaRoomDoor legacyDoor = legacyDoors[doorIndex];
                if (!legacyDoor.name.StartsWith(
                        "Apartment_Entrance_Door",
                        StringComparison.Ordinal) &&
                    !legacyDoor.name.StartsWith(
                        "Interior_Door_",
                        StringComparison.Ordinal))
                {
                    continue;
                }

                MiniVanApartmentDoor door =
                    legacyDoor.GetComponent<MiniVanApartmentDoor>();
                if (door == null)
                {
                    door = legacyDoor.gameObject.AddComponent<
                        MiniVanApartmentDoor>();
                    door.Configure(
                        legacyDoor.Pivot,
                        legacyDoor.ClosedEuler,
                        legacyDoor.OpenEuler);
                }
                UnityEngine.Object.DestroyImmediate(legacyDoor);
                changed = true;
            }

            MiniVanApartmentDoor[] apartmentDoors =
                root.GetComponentsInChildren<MiniVanApartmentDoor>(true);
            Material fallbackDoorMaterial = apartmentDoors
                .SelectMany(door => door.GetComponentsInChildren<Renderer>(true))
                .Where(renderer => renderer.name == "Door_Panel")
                .Select(renderer => renderer.sharedMaterial)
                .FirstOrDefault(material => material != null);
            for (int doorIndex = 0;
                 doorIndex < apartmentDoors.Length;
                 doorIndex++)
            {
                changed |= EnsureApartmentDoorVisuals(
                    apartmentDoors[doorIndex],
                    fallbackDoorMaterial);
                changed |= ClearStaticFlagsRecursively(
                    apartmentDoors[doorIndex].transform);
            }

            MiniVanPanelkaApartmentTemplate template =
                root.GetComponent<MiniVanPanelkaApartmentTemplate>();
            if (template != null && template.TemplateIndex == 3)
            {
                changed |= EnsureTHallEntranceClearance(template);
            }
            if (template != null &&
                template.ContentRoot != null &&
                template.GetComponentsInChildren<MiniVanPanelkaRoomIdentity>(true)
                    .Length > 0 &&
                template.ContentRoot.Find(
                    "APARTMENT_LAYOUT/EXPLICIT_CEILING") == null)
            {
                Transform apartment =
                    template.ContentRoot.Find("APARTMENT_LAYOUT");
                if (apartment != null)
                {
                    BuildExplicitCeiling(apartment, LoadCeilingMaterial());
                    changed = true;
                }
            }

            if (changed)
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static bool EnsureTHallEntranceClearance(
        MiniVanPanelkaApartmentTemplate template)
    {
        if (template == null || template.EntrySocket == null)
            return false;

        const float desiredEntryZ = -0.55f;
        float currentEntryZ =
            template.transform.InverseTransformPoint(
                template.EntrySocket.position).z;
        float offset = desiredEntryZ - currentEntryZ;
        if (Mathf.Abs(offset) <= 0.001f)
            return false;

        Transform wallBefore = FindDescendant(
            template.transform,
            "FacadeWall_Envelope_West_0");
        Transform wallHeader = FindDescendant(
            template.transform,
            "FacadeWall_Envelope_West_Header_0");
        Transform wallAfter = FindDescendant(
            template.transform,
            "FacadeWall_Envelope_West_1");
        Transform frame = FindDescendant(
            template.transform,
            "Door_Frame_Entrance");
        Transform entrance = FindDescendant(
            template.transform,
            "Apartment_Entrance_Door");
        if (wallBefore == null ||
            wallHeader == null ||
            wallAfter == null ||
            frame == null ||
            entrance == null)
        {
            throw new InvalidOperationException(
                template.TemplateId +
                " cannot move its entrance away from an interior wall.");
        }

        float outerMin =
            wallBefore.localPosition.z -
            wallBefore.localScale.z * 0.5f;
        float outerMax =
            wallAfter.localPosition.z +
            wallAfter.localScale.z * 0.5f;
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

        Vector3 socketPosition = template.EntrySocket.localPosition;
        socketPosition.z = desiredEntryZ;
        template.EntrySocket.localPosition = socketPosition;
        return true;
    }

    private static Transform FindDescendant(
        Transform root,
        string exactName)
    {
        if (root == null || string.IsNullOrEmpty(exactName))
            return null;

        Transform[] descendants =
            root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < descendants.Length; i++)
        {
            if (descendants[i].name == exactName)
                return descendants[i];
        }

        return null;
    }

    private static void SetLocalZSegment(
        Transform segment,
        float minimum,
        float maximum)
    {
        Vector3 position = segment.localPosition;
        Vector3 scale = segment.localScale;
        position.z = (minimum + maximum) * 0.5f;
        scale.z = Mathf.Max(0.01f, maximum - minimum);
        segment.localPosition = position;
        segment.localScale = scale;
    }

    private static bool EnsureApartmentDoorVisuals(
        MiniVanApartmentDoor door,
        Material fallbackDoorMaterial)
    {
        if (door == null || door.Pivot == null)
            return false;

        bool changed = false;
        bool alongX =
            Mathf.DeltaAngle(door.ClosedEuler.y, door.OpenEuler.y) < 0f;
        const float doorHeight = 2.18f;
        const float panelThickness = 0.055f;

        Renderer panel = door.Pivot
            .GetComponentsInChildren<Renderer>(true)
            .FirstOrDefault(renderer => renderer.name == "Door_Panel");
        if (panel == null)
        {
            Vector3 panelCenter = alongX
                ? new Vector3(DoorWidth * 0.5f, doorHeight * 0.5f, 0f)
                : new Vector3(0f, doorHeight * 0.5f, DoorWidth * 0.5f);
            Vector3 panelScale = alongX
                ? new Vector3(DoorWidth, doorHeight, panelThickness)
                : new Vector3(panelThickness, doorHeight, DoorWidth);
            BuildBox(
                "Door_Panel",
                door.Pivot,
                panelCenter,
                panelScale,
                fallbackDoorMaterial);
            door.Configure(
                door.Pivot,
                door.ClosedEuler,
                door.OpenEuler);
            changed = true;
        }

        string frameName = door.name == "Apartment_Entrance_Door"
            ? "Door_Frame_Entrance"
            : door.name.StartsWith("Interior_Door_", StringComparison.Ordinal)
                ? "Door_Frame_" +
                  door.name.Substring("Interior_Door_".Length)
                : string.Empty;
        if (string.IsNullOrEmpty(frameName))
            return changed;

        Transform frame = door.transform.parent != null
            ? door.transform.parent.Find(frameName)
            : null;
        if (frame == null && door.transform.parent != null)
        {
            frame = NewChild(frameName, door.transform.parent);
            changed = true;
        }

        if (frame == null ||
            frame.GetComponentsInChildren<Renderer>(true).Length > 0)
        {
            return changed;
        }

        Material frameMaterial = LoadMaterial("Door_GrayMetal_04.mat");
        if (frameMaterial == null)
            frameMaterial = fallbackDoorMaterial;
        Vector3 hinge = door.transform.localPosition;
        const float trim = 0.14f;
        const float trimDepth = WallThickness * 1.62f;
        if (alongX)
        {
            float openingCenter = hinge.x + DoorWidth * 0.5f;
            BuildTrimBox(
                "Jamb_Left",
                frame,
                new Vector3(hinge.x, doorHeight * 0.5f, hinge.z),
                new Vector3(trim, doorHeight + trim, trimDepth),
                frameMaterial);
            BuildTrimBox(
                "Jamb_Right",
                frame,
                new Vector3(
                    hinge.x + DoorWidth,
                    doorHeight * 0.5f,
                    hinge.z),
                new Vector3(trim, doorHeight + trim, trimDepth),
                frameMaterial);
            BuildTrimBox(
                "Header",
                frame,
                new Vector3(
                    openingCenter,
                    doorHeight + trim * 0.5f,
                    hinge.z),
                new Vector3(
                    DoorWidth + trim * 2f,
                    trim,
                    trimDepth),
                frameMaterial);
        }
        else
        {
            float openingCenter = hinge.z + DoorWidth * 0.5f;
            BuildTrimBox(
                "Jamb_Left",
                frame,
                new Vector3(hinge.x, doorHeight * 0.5f, hinge.z),
                new Vector3(trimDepth, doorHeight + trim, trim),
                frameMaterial);
            BuildTrimBox(
                "Jamb_Right",
                frame,
                new Vector3(
                    hinge.x,
                    doorHeight * 0.5f,
                    hinge.z + DoorWidth),
                new Vector3(trimDepth, doorHeight + trim, trim),
                frameMaterial);
            BuildTrimBox(
                "Header",
                frame,
                new Vector3(
                    hinge.x,
                    doorHeight + trim * 0.5f,
                    openingCenter),
                new Vector3(
                    trimDepth,
                    trim,
                    DoorWidth + trim * 2f),
                frameMaterial);
        }

        return true;
    }

    private static void RebuildSolidWallModule(
        MiniVanPanelkaApartmentFacadeMarker marker,
        Transform solidModule,
        Bounds openingBounds,
        Material interiorMaterial,
        Material exteriorMaterial)
    {
        for (int childIndex = solidModule.childCount - 1;
             childIndex >= 0;
             childIndex--)
        {
            UnityEngine.Object.DestroyImmediate(
                solidModule.GetChild(childIndex).gameObject);
        }

        bool alongX =
            marker.Side == MiniVanPanelkaApartmentFacadeSide.PositiveZ;
        const float overlap = 0.08f;
        Vector3 coreScale = alongX
            ? new Vector3(
                openingBounds.size.x + overlap,
                openingBounds.size.y + overlap,
                WallThickness)
            : new Vector3(
                WallThickness,
                openingBounds.size.y + overlap,
                openingBounds.size.z + overlap);
        Material safeExterior =
            exteriorMaterial != null ? exteriorMaterial : interiorMaterial;
        BuildBox(
            "Solid_Exterior_Core",
            solidModule,
            openingBounds.center,
            coreScale,
            safeExterior);

        Vector3 inward =
            alongX ? Vector3.back : Vector3.left;
        Vector3 skinScale = alongX
            ? new Vector3(
                openingBounds.size.x + overlap,
                openingBounds.size.y + overlap,
                0.035f)
            : new Vector3(
                0.035f,
                openingBounds.size.y + overlap,
                openingBounds.size.z + overlap);
        GameObject interiorSkin = BuildBox(
            "Solid_Interior_Skin",
            solidModule,
            openingBounds.center + inward * 0.105f,
            skinScale,
            interiorMaterial != null ? interiorMaterial : safeExterior);
        Collider skinCollider = interiorSkin.GetComponent<Collider>();
        if (skinCollider != null)
            skinCollider.enabled = false;
    }

    private static bool ClearStaticFlagsRecursively(Transform root)
    {
        bool changed = false;
        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            GameObject item = transforms[i].gameObject;
            if (GameObjectUtility.GetStaticEditorFlags(item) ==
                (StaticEditorFlags)0)
            {
                continue;
            }

            GameObjectUtility.SetStaticEditorFlags(
                item,
                (StaticEditorFlags)0);
            changed = true;
        }

        return changed;
    }

    private static string ParseWindowRoomId(
        MiniVanPanelkaApartmentFacadeMarker marker)
    {
        string token = marker.Side ==
                       MiniVanPanelkaApartmentFacadeSide.PositiveX
            ? "_East_"
            : "_North_";
        string value = marker.name;
        int start = value.IndexOf(token, StringComparison.Ordinal);
        if (start >= 0)
            value = value.Substring(start + token.Length);
        if (value.EndsWith("_A", StringComparison.Ordinal) ||
            value.EndsWith("_B", StringComparison.Ordinal))
        {
            value = value.Substring(0, value.Length - 2);
        }
        return value;
    }

    private static Bounds GetLocalRenderBounds(
        Transform root,
        Renderer[] renderers)
    {
        Bounds result = new Bounds();
        bool initialized = false;
        Matrix4x4 toLocal = root.worldToLocalMatrix;
        for (int rendererIndex = 0;
             rendererIndex < renderers.Length;
             rendererIndex++)
        {
            Bounds world = renderers[rendererIndex].bounds;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 point = world.center + Vector3.Scale(
                    world.extents,
                    new Vector3(
                        (corner & 1) == 0 ? -1f : 1f,
                        (corner & 2) == 0 ? -1f : 1f,
                        (corner & 4) == 0 ? -1f : 1f));
                point = toLocal.MultiplyPoint3x4(point);
                if (!initialized)
                {
                    result = new Bounds(point, Vector3.zero);
                    initialized = true;
                }
                else
                {
                    result.Encapsulate(point);
                }
            }
        }
        return result;
    }

    private static Material FindNearestInteriorWallMaterial(
        Transform prefabRoot,
        Transform excludedRoot,
        Vector3 localCenter)
    {
        Renderer[] renderers =
            prefabRoot.GetComponentsInChildren<Renderer>(true);
        Material selected = null;
        float bestDistance = float.PositiveInfinity;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer.transform.IsChildOf(excludedRoot) ||
                renderer.name.StartsWith(
                    "FacadeWall_ExteriorCladding_",
                    StringComparison.Ordinal))
            {
                continue;
            }

            Vector3 center =
                prefabRoot.InverseTransformPoint(renderer.bounds.center);
            float distance = (center - localCenter).sqrMagnitude;
            if (distance >= bestDistance)
                continue;
            selected = renderer.sharedMaterial;
            bestDistance = distance;
        }

        return selected != null
            ? selected
            : LoadMaterial("Wallpaper_Room_SageLeaves_01.mat");
    }

    private static GameObject BuildPrefab(LayoutSpec layout)
    {
        GameObject root = new GameObject(
            "ApartmentTemplate_" + layout.Index.ToString("00") + "_" + layout.Name);
        try
        {
            Transform content = NewChild("CONTENT__EDIT_THIS_PREFAB", root.transform);
            Transform apartment = NewChild("APARTMENT_LAYOUT", content);
            Transform shell = NewChild("APARTMENT_LAYOUT_SHELL", apartment);
            Transform sockets = NewChild("ROUTE_SOCKETS__MOVE_IF_NEEDED", root.transform);

            Material defaultWall = LoadMaterial(layout.Wallpaper);
            Material kitchenWall = LoadMaterial(layout.KitchenWallpaper);
            Material wetWall = LoadMaterial(layout.WetTile);
            Material doorMaterial = LoadMaterial(layout.Door);
            Material exteriorMaterial =
                AssetDatabase.LoadAssetAtPath<Material>(ExteriorMaterialPath);
            if (exteriorMaterial == null)
                throw new InvalidOperationException(
                    "Missing apartment facade material: " + ExteriorMaterialPath);

            Dictionary<string, Transform> rooms = new Dictionary<string, Transform>(
                StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < layout.Rooms.Count; i++)
            {
                RoomSpec room = layout.Rooms[i];
                Transform roomRoot = NewChild("FURNISHED_" + room.Id, apartment);
                MiniVanPanelkaRoomIdentity identity =
                    roomRoot.gameObject.AddComponent<MiniVanPanelkaRoomIdentity>();
                Vector3 center = new Vector3(room.Bounds.center.x, 0f, room.Bounds.center.y);
                Vector3 size = new Vector3(room.Bounds.width, WallHeight, room.Bounds.height);
                identity.Configure(room.Id, center, size, room.DoorEdges);
                // Exact room rect (wall centerlines): under own wall, not into the next room.
                BuildBox(
                    "RoomFloorFinish",
                    roomRoot,
                    new Vector3(center.x, FloorTop * 0.5f, center.z),
                    new Vector3(
                        room.Bounds.width + FloorWallOverlap * 2f,
                        FloorTop,
                        room.Bounds.height + FloorWallOverlap * 2f),
                    LoadMaterial(room.Surface));
                rooms[room.Id] = roomRoot;
            }

            ApplyWindowRules(layout);

            for (int i = 0; i < layout.Walls.Count; i++)
            {
                WallSpec wall = layout.Walls[i];
                Material wallMaterial = defaultWall;
                if (wall.Surface == "KITCHEN")
                    wallMaterial = kitchenWall;
                else if (wall.Surface == "WET")
                    wallMaterial = wetWall;
                BuildWall(
                    wall,
                    shell,
                    apartment,
                    wallMaterial,
                    exteriorMaterial,
                    doorMaterial,
                    layout.Index);
            }

            BuildExplicitCeiling(apartment, LoadCeilingMaterial());

            for (int roomIndex = 0; roomIndex < layout.Rooms.Count; roomIndex++)
            {
                RoomSpec room = layout.Rooms[roomIndex];
                Transform parent = rooms[room.Id];
                for (int furnitureIndex = 0; furnitureIndex < room.Furniture.Count; furnitureIndex++)
                    PlaceFurniture(room.Furniture[furnitureIndex], parent);
            }

            ResolveFurnitureLayout(layout, apartment);
            FinalSnapWallFurniture(apartment);
            ResolveDoorConflictsAlongWallsOnly(layout, apartment);
            FinalSnapWallFurniture(apartment);
            RemoveFurnitureStillBlockingDoors(layout, apartment);
            ValidateFurnitureBounds(layout, apartment);
            ValidateDoorClearances(layout, apartment);

            Transform entry = NewChild("EntrySocket", sockets);
            entry.localPosition = new Vector3(MinX, 0f, -2.8f);
            Transform routeHole = NewChild("RouteHoleSocket", sockets);
            routeHole.localPosition = new Vector3(layout.RouteHole.x, FloorTop + 0.03f,
                layout.RouteHole.y);
            Transform balcony = NewChild("BalconySocket", sockets);
            balcony.localPosition = new Vector3(layout.Balcony.x, 0f, layout.Balcony.y);
            Transform pipe = NewChild("PipeSocket", sockets);
            pipe.localPosition = new Vector3(layout.Pipe.x, 1.35f, layout.Pipe.y);
            Transform key = NewChild("KeySocket", sockets);
            key.localPosition = new Vector3(-2.85f, 0.85f, -2.35f);

            MiniVanPanelkaApartmentTemplate metadata =
                root.AddComponent<MiniVanPanelkaApartmentTemplate>();
            string variantSuffix = layout.Variant == TemplateVariant.Standard
                ? string.Empty
                : "_" + layout.Variant.ToString().ToUpperInvariant();
            metadata.Configure(
                layout.Index,
                "APARTMENT_TEMPLATE_" + layout.Index.ToString("00") + "_" +
                layout.Name.ToUpperInvariant() + variantSuffix,
                "NE",
                new Vector2(MaxX - MinX, MaxZ - MinZ),
                content,
                entry,
                routeHole,
                balcony,
                pipe,
                key);

            string path = GetPrefabPath(layout);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            if (prefab == null)
                throw new InvalidOperationException("Could not save " + path);
            return prefab;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static string GetPrefabPath(LayoutSpec layout)
    {
        string variantSuffix = layout.Variant == TemplateVariant.Standard
            ? string.Empty
            : "_" + layout.Variant;
        return TemplateFolder + "/ApartmentTemplate_" +
               layout.Index.ToString("00") + "_" + layout.Name +
               variantSuffix + ".prefab";
    }

    private static void BuildWall(
        WallSpec wall,
        Transform shell,
        Transform apartment,
        Material wallMaterial,
        Material exteriorMaterial,
        Material doorMaterial,
        int layoutIndex)
    {
        List<OpeningSpec> openings = wall.Openings.OrderBy(item => item.Center).ToList();
        float cursor = wall.Min;
        int part = 0;
        string prefix = wall.Envelope ? "FacadeWall_" : "LayoutWall_";
        for (int i = 0; i < openings.Count; i++)
        {
            OpeningSpec opening = openings[i];
            float openingMin = Mathf.Max(wall.Min, opening.Center - opening.Width * 0.5f);
            float openingMax = Mathf.Min(wall.Max, opening.Center + opening.Width * 0.5f);
            if (openingMin > cursor + 0.01f)
                BuildWallAndCladdingPiece(
                    prefix + wall.Id + "_" + part++,
                    shell,
                    wall,
                    cursor,
                    openingMin,
                    WallHeight * 0.5f,
                    WallHeight,
                    wallMaterial,
                    exteriorMaterial);

            if (opening.IsWindow)
            {
                const float sill = 0.82f;
                const float windowHeight = 1.28f;
                BuildWallAndCladdingPiece(
                    prefix + wall.Id + "_Sill_" + i,
                    shell,
                    wall,
                    openingMin,
                    openingMax,
                    sill * 0.5f,
                    sill,
                    wallMaterial,
                    exteriorMaterial);
                float topHeight = WallHeight - sill - windowHeight;
                BuildWallAndCladdingPiece(
                    prefix + wall.Id + "_Header_" + i,
                    shell,
                    wall,
                    openingMin,
                    openingMax,
                    sill + windowHeight + topHeight * 0.5f,
                    topHeight,
                    wallMaterial,
                    exteriorMaterial);
                BuildWindow(
                    opening,
                    wall,
                    shell,
                    layoutIndex,
                    sill,
                    windowHeight,
                    wallMaterial,
                    exteriorMaterial);
            }
            else
            {
                const float doorHeight = 2.18f;
                BuildWallAndCladdingPiece(
                    prefix + wall.Id + "_Header_" + i,
                    shell,
                    wall,
                    openingMin,
                    openingMax,
                    doorHeight + (WallHeight - doorHeight) * 0.5f,
                    WallHeight - doorHeight,
                    wallMaterial,
                    exteriorMaterial);
                Material frameMaterial = LoadMaterial("Door_GrayMetal_04.mat");
                BuildDoorFrame(
                    opening,
                    wall,
                    opening.IsEntrance ? apartment : shell,
                    frameMaterial != null ? frameMaterial : doorMaterial);
                BuildDoor(opening, wall, opening.IsEntrance ? apartment : shell, doorMaterial);
            }
            cursor = openingMax;
        }

        if (cursor < wall.Max - 0.01f)
            BuildWallAndCladdingPiece(
                prefix + wall.Id + "_" + part,
                shell,
                wall,
                cursor,
                wall.Max,
                WallHeight * 0.5f,
                WallHeight,
                wallMaterial,
                exteriorMaterial);
    }

    private static void BuildWallAndCladdingPiece(
        string name,
        Transform parent,
        WallSpec wall,
        float min,
        float max,
        float y,
        float height,
        Material interiorMaterial,
        Material exteriorMaterial)
    {
        BuildWallPiece(name, parent, wall, min, max, y, height, interiorMaterial);
        bool exteriorFace =
            wall.Envelope &&
            (wall.Id == "Envelope_East" || wall.Id == "Envelope_North");
        if (!exteriorFace)
            return;

        float claddingBottom = y - height * 0.5f;
        float claddingTop = y + height * 0.5f;
        if (Mathf.Abs(claddingBottom) < 0.01f)
            claddingBottom -= FacadeVerticalOverlap;
        if (Mathf.Abs(claddingTop - WallHeight) < 0.01f)
            claddingTop = FacadeStoreyHeight + FacadeVerticalOverlap;
        float claddingHeight = claddingTop - claddingBottom;
        float claddingY = claddingBottom + claddingHeight * 0.5f;

        float outwardOffset = 0.11f;
        BuildWallPiece(
            "FacadeWall_ExteriorCladding_" + name,
            parent,
            wall,
            min,
            max,
            claddingY,
            claddingHeight,
            exteriorMaterial,
            outwardOffset);
    }

    private static void BuildWallPiece(
        string name,
        Transform parent,
        WallSpec wall,
        float min,
        float max,
        float y,
        float height,
        Material material,
        float fixedOffset = 0f)
    {
        float length = max - min;
        Vector3 center;
        Vector3 scale;
        if (wall.AlongX)
        {
            center = new Vector3((min + max) * 0.5f, y, wall.Fixed + fixedOffset);
            scale = new Vector3(
                length,
                height,
                fixedOffset == 0f ? WallThickness : 0.04f);
        }
        else
        {
            center = new Vector3(wall.Fixed + fixedOffset, y, (min + max) * 0.5f);
            scale = new Vector3(
                fixedOffset == 0f ? WallThickness : 0.04f,
                height,
                length);
        }
        BuildBox(name, parent, center, scale, material);
    }

    private static void BuildWindow(
        OpeningSpec opening,
        WallSpec wall,
        Transform shell,
        int layoutIndex,
        float sill,
        float height,
        Material interiorMaterial,
        Material exteriorMaterial)
    {
        MiniVanPanelkaApartmentFacadeSide side = wall.AlongX
            ? MiniVanPanelkaApartmentFacadeSide.PositiveZ
            : MiniVanPanelkaApartmentFacadeSide.PositiveX;
        Transform root = NewChild(
            "FacadeWall_Window_" + side + "_" + opening.Id,
            shell);
        root.gameObject.AddComponent<MiniVanPanelkaApartmentFacadeMarker>().Configure(side);
        Transform windowModule = NewChild("Window_Module", root);
        Transform solidModule = NewChild("Solid_Wall_Module", root);
        float frame = 0.08f;
        Material frameMaterial = LoadMaterial("Door_GrayMetal_04.mat");
        Material glassMaterial =
            AssetDatabase.LoadAssetAtPath<Material>(WindowGlassMaterialPath) ??
            LoadMaterial("Tile_Bathroom_PowderBlueSubway_03.mat");
        Vector3 center = wall.AlongX
            ? new Vector3(opening.Center, sill + height * 0.5f, wall.Fixed)
            : new Vector3(wall.Fixed, sill + height * 0.5f, opening.Center);
        Vector3 glassScale = wall.AlongX
            ? new Vector3(opening.Width - frame * 2f, height - frame * 2f, 0.045f)
            : new Vector3(0.045f, height - frame * 2f, opening.Width - frame * 2f);
        GameObject glass = BuildBox(
            "Breakable_Glass",
            windowModule,
            center,
            glassScale,
            glassMaterial);
        MiniVanPanelkaBreakableWindowBase breakable =
            glass.AddComponent<MiniVanPanelkaBreakableWindowBase>();
        breakable.Configure("apartment-" + layoutIndex + "-" + opening.Id);

        if (wall.AlongX)
        {
            BuildBox("Frame_Bottom", windowModule,
                center + Vector3.down * (height * 0.5f - frame * 0.5f),
                new Vector3(opening.Width, frame, WallThickness * 1.18f), frameMaterial);
            BuildBox("Frame_Top", windowModule,
                center + Vector3.up * (height * 0.5f - frame * 0.5f),
                new Vector3(opening.Width, frame, WallThickness * 1.18f), frameMaterial);
            BuildBox("Frame_Left", windowModule,
                center + Vector3.left * (opening.Width * 0.5f - frame * 0.5f),
                new Vector3(frame, height, WallThickness * 1.18f), frameMaterial);
            BuildBox("Frame_Right", windowModule,
                center + Vector3.right * (opening.Width * 0.5f - frame * 0.5f),
                new Vector3(frame, height, WallThickness * 1.18f), frameMaterial);
        }
        else
        {
            BuildBox("Frame_Bottom", windowModule,
                center + Vector3.down * (height * 0.5f - frame * 0.5f),
                new Vector3(WallThickness * 1.18f, frame, opening.Width), frameMaterial);
            BuildBox("Frame_Top", windowModule,
                center + Vector3.up * (height * 0.5f - frame * 0.5f),
                new Vector3(WallThickness * 1.18f, frame, opening.Width), frameMaterial);
            BuildBox("Frame_Left", windowModule,
                center + Vector3.back * (opening.Width * 0.5f - frame * 0.5f),
                new Vector3(WallThickness * 1.18f, height, frame), frameMaterial);
            BuildBox("Frame_Right", windowModule,
                center + Vector3.forward * (opening.Width * 0.5f - frame * 0.5f),
                new Vector3(WallThickness * 1.18f, height, frame), frameMaterial);
        }

        const float solidOverlap = 0.08f;
        Vector3 solidScale = wall.AlongX
            ? new Vector3(
                opening.Width + solidOverlap,
                height + solidOverlap,
                WallThickness)
            : new Vector3(
                WallThickness,
                height + solidOverlap,
                opening.Width + solidOverlap);
        BuildBox(
            "Solid_Exterior_Core",
            solidModule,
            center,
            solidScale,
            exteriorMaterial);
        GameObject interiorSkin = BuildBox(
            "Solid_Interior_Skin",
            solidModule,
            center + (wall.AlongX
                ? Vector3.back * 0.105f
                : Vector3.left * 0.105f),
            wall.AlongX
                ? new Vector3(
                    opening.Width + solidOverlap,
                    height + solidOverlap,
                    0.035f)
                : new Vector3(
                    0.035f,
                    height + solidOverlap,
                    opening.Width + solidOverlap),
            interiorMaterial);
        Collider interiorCollider = interiorSkin.GetComponent<Collider>();
        if (interiorCollider != null)
            interiorCollider.enabled = false;
        solidModule.gameObject.SetActive(false);
        root.gameObject.AddComponent<MiniVanPanelkaWindowSocket>().Configure(
            opening.RoomId,
            side,
            windowModule.gameObject,
            solidModule.gameObject);
    }

    private static void BuildDoor(
        OpeningSpec opening,
        WallSpec wall,
        Transform parent,
        Material material)
    {
        string name = opening.IsEntrance ? "Apartment_Entrance_Door" :
            "Interior_Door_" + opening.Id;
        Transform doorRoot = NewChild(name, parent);
        Vector3 hinge;
        if (wall.AlongX)
            hinge = new Vector3(opening.Center - opening.Width * 0.5f, 0f, wall.Fixed);
        else
            hinge = new Vector3(wall.Fixed, 0f, opening.Center - opening.Width * 0.5f);
        doorRoot.localPosition = hinge;
        Transform pivot = NewChild("Door_Runtime_Pivot", doorRoot);
        const float panelHeight = 2.18f;
        const float panelOverlap = 0f;
        const float panelThickness = 0.055f;
        Vector3 panelCenter = wall.AlongX
            ? new Vector3(opening.Width * 0.5f, panelHeight * 0.5f, 0f)
            : new Vector3(0f, panelHeight * 0.5f, opening.Width * 0.5f);
        Vector3 panelScale = wall.AlongX
            ? new Vector3(opening.Width + panelOverlap, panelHeight, panelThickness)
            : new Vector3(panelThickness, panelHeight, opening.Width + panelOverlap);
        BuildBox("Door_Panel", pivot, panelCenter, panelScale, material);
        MiniVanApartmentDoor interactable =
            doorRoot.gameObject.AddComponent<MiniVanApartmentDoor>();
        interactable.Configure(
            pivot,
            Vector3.zero,
            new Vector3(0f, wall.AlongX ? -117f : 117f, 0f));
        ClearStaticFlagsRecursively(doorRoot);
    }

    private static void BuildDoorFrame(
        OpeningSpec opening,
        WallSpec wall,
        Transform parent,
        Material material)
    {
        const float doorHeight = 2.18f;
        const float trim = 0.14f;
        const float trimDepth = WallThickness * 1.62f;
        Transform frame = NewChild("Door_Frame_" + opening.Id, parent);
        if (wall.AlongX)
        {
            float left = opening.Center - opening.Width * 0.5f;
            float right = opening.Center + opening.Width * 0.5f;
            BuildTrimBox("Jamb_Left", frame,
                new Vector3(left, doorHeight * 0.5f, wall.Fixed),
                new Vector3(trim, doorHeight + trim, trimDepth), material);
            BuildTrimBox("Jamb_Right", frame,
                new Vector3(right, doorHeight * 0.5f, wall.Fixed),
                new Vector3(trim, doorHeight + trim, trimDepth), material);
            BuildTrimBox("Header", frame,
                new Vector3(opening.Center, doorHeight + trim * 0.5f, wall.Fixed),
                new Vector3(opening.Width + trim * 2f, trim, trimDepth), material);
        }
        else
        {
            float left = opening.Center - opening.Width * 0.5f;
            float right = opening.Center + opening.Width * 0.5f;
            BuildTrimBox("Jamb_Left", frame,
                new Vector3(wall.Fixed, doorHeight * 0.5f, left),
                new Vector3(trimDepth, doorHeight + trim, trim), material);
            BuildTrimBox("Jamb_Right", frame,
                new Vector3(wall.Fixed, doorHeight * 0.5f, right),
                new Vector3(trimDepth, doorHeight + trim, trim), material);
            BuildTrimBox("Header", frame,
                new Vector3(wall.Fixed, doorHeight + trim * 0.5f, opening.Center),
                new Vector3(trimDepth, trim, opening.Width + trim * 2f), material);
        }
    }

    private static GameObject BuildTrimBox(
        string name,
        Transform parent,
        Vector3 center,
        Vector3 scale,
        Material material)
    {
        GameObject trim = BuildBox(name, parent, center, scale, material);
        Collider collider = trim.GetComponent<Collider>();
        if (collider != null)
            UnityEngine.Object.DestroyImmediate(collider);
        return trim;
    }

    private static void PlaceFurniture(FurnitureSpec spec, Transform parent)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FurnitureFolder + spec.Path);
        if (prefab == null)
            throw new InvalidOperationException("Missing furniture prefab: " + spec.Path);
        GameObject instance = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
        if (instance == null)
            throw new InvalidOperationException("Could not instantiate furniture: " + spec.Path);
        // Unpack so wall-snap transforms are baked into the apartment prefab reliably.
        PrefabUtility.UnpackPrefabInstance(
            instance,
            PrefabUnpackMode.Completely,
            InteractionMode.AutomatedAction);
        instance.name = Path.GetFileNameWithoutExtension(spec.Path);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one * spec.Scale;

        MiniVanPanelkaFurnitureAnchor anchor =
            instance.GetComponent<MiniVanPanelkaFurnitureAnchor>();
        Vector3 desiredDirection = new Vector3(spec.Front.x, 0f, spec.Front.y).normalized;
        if (anchor != null && anchor.BackWallAnchor != null && anchor.FrontRoomAnchor != null)
        {
            Vector3 currentDirection = anchor.FrontRoomAnchor.position - anchor.BackWallAnchor.position;
            currentDirection.y = 0f;
            if (currentDirection.sqrMagnitude > 0.001f)
            {
                float yaw = Vector3.SignedAngle(currentDirection, desiredDirection, Vector3.up);
                instance.transform.Rotate(Vector3.up, yaw, Space.World);
            }
            Vector3 target = parent.TransformPoint(new Vector3(spec.Back.x, 0f, spec.Back.y));
            instance.transform.position += target - anchor.BackWallAnchor.position;
        }
        else
        {
            instance.transform.localPosition = new Vector3(spec.Back.x, 0f, spec.Back.y);
            instance.transform.localRotation = Quaternion.LookRotation(desiredDirection, Vector3.up);
        }

        Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length > 0)
        {
            float minY = renderers.Min(renderer => renderer.bounds.min.y);
            instance.transform.position += Vector3.up * (FloorTop - minY);
        }

        // Anchor markers are often outside the mesh — snap the real back face to the wall.
        SnapFlushToWall(instance.transform, parent, spec.Back, desiredDirection);
    }

    private static void SnapFlushToWall(
        Transform furniture,
        Transform roomRoot,
        Vector2 back,
        Vector3 frontDir)
    {
        if (furniture == null || roomRoot == null || frontDir.sqrMagnitude < 0.0001f)
            return;

        Transform apartment = roomRoot.parent != null ? roomRoot.parent : roomRoot;
        if (!TryGetLocalBounds(furniture, apartment, out Bounds bounds))
            return;

        Vector3 front = new Vector3(frontDir.x, 0f, frontDir.z).normalized;
        if (front.sqrMagnitude < 0.0001f)
            front = new Vector3(frontDir.x, 0f, frontDir.y).normalized;
        Vector3 intoWall = -front;
        Vector3 wallPoint = new Vector3(back.x, 0f, back.y);

        float maxIntoWall = float.NegativeInfinity;
        Vector3[] corners =
        {
            new Vector3(bounds.min.x, bounds.min.y, bounds.min.z),
            new Vector3(bounds.min.x, bounds.min.y, bounds.max.z),
            new Vector3(bounds.min.x, bounds.max.y, bounds.min.z),
            new Vector3(bounds.min.x, bounds.max.y, bounds.max.z),
            new Vector3(bounds.max.x, bounds.min.y, bounds.min.z),
            new Vector3(bounds.max.x, bounds.min.y, bounds.max.z),
            new Vector3(bounds.max.x, bounds.max.y, bounds.min.z),
            new Vector3(bounds.max.x, bounds.max.y, bounds.max.z)
        };
        for (int i = 0; i < corners.Length; i++)
        {
            float depth = Vector3.Dot(corners[i] - wallPoint, intoWall);
            if (depth > maxIntoWall)
                maxIntoWall = depth;
        }

        // Slightly bite into the wall so no light leak / floating gap remains.
        float delta = WallMeshBite - maxIntoWall;
        if (Mathf.Abs(delta) > 0.0005f)
            furniture.position += apartment.TransformVector(intoWall * delta);
    }

    private static void ResolveFurnitureLayout(LayoutSpec layout, Transform apartment)
    {
        MiniVanPanelkaFurnitureAnchor[] furniture =
            apartment.GetComponentsInChildren<MiniVanPanelkaFurnitureAnchor>(true);
        Bounds[] doorClearances = GetDoorClearances(apartment);
        List<GameObject> remove = new List<GameObject>();

        for (int i = 0; i < furniture.Length; i++)
        {
            MiniVanPanelkaRoomIdentity room =
                furniture[i].GetComponentInParent<MiniVanPanelkaRoomIdentity>();
            if (room == null)
                throw new InvalidOperationException(
                    layout.Name + " furniture " + furniture[i].name +
                    " is not assigned to a room.");

            Bounds bounds;
            if (!TryGetLocalBounds(furniture[i].transform, apartment, out bounds))
                continue;

            bool wallItem = IsWallRuleFurnitureName(furniture[i].name);
            bool isEntrywaySet = furniture[i].name.Contains("Entryway_Set");
            Vector3 correction = ClampBoundsIntoRoom(bounds, room, wallItem);
            if (correction.sqrMagnitude > 0.000001f)
            {
                // For wall furniture never push away from the facing wall.
                if (wallItem)
                {
                    correction = ClampCorrectionToStayNearWall(
                        correction, bounds, room, furniture[i].transform);
                }

                furniture[i].transform.position += apartment.TransformVector(correction);
                TryGetLocalBounds(furniture[i].transform, apartment, out bounds);
            }

            // Entryway is placed on a blank wall only — never slide it toward openings.
            if (isEntrywaySet || !IntersectsAny(bounds, doorClearances))
                continue;

            Vector3 startPosition = furniture[i].transform.position;
            bool resolved = TryResolveDoorConflictAlongWall(
                furniture[i].transform,
                apartment,
                room,
                doorClearances,
                startPosition,
                wallItem);

            if (!resolved && !wallItem)
            {
                // Non-wall pieces may drift a little; wall pieces stay on the wall plane.
                const float step = 0.16f;
                for (int ring = 1; ring <= 10 && !resolved; ring++)
                {
                    for (int x = -ring; x <= ring && !resolved; x++)
                    {
                        for (int z = -ring; z <= ring && !resolved; z++)
                        {
                            if (Mathf.Max(Mathf.Abs(x), Mathf.Abs(z)) != ring)
                                continue;
                            Vector3 offset = new Vector3(x * step, 0f, z * step);
                            furniture[i].transform.position =
                                startPosition + apartment.TransformVector(offset);
                            if (!TryGetLocalBounds(furniture[i].transform, apartment, out bounds) ||
                                !IsInsideRoom(bounds, room) ||
                                IntersectsAny(bounds, doorClearances))
                            {
                                continue;
                            }

                            resolved = true;
                        }
                    }
                }
            }

            if (!resolved)
            {
                // Rule 4.A: drop the piece rather than blocking the opening.
                remove.Add(furniture[i].gameObject);
            }
        }

        for (int i = 0; i < remove.Count; i++)
        {
            if (remove[i] != null)
                UnityEngine.Object.DestroyImmediate(remove[i]);
        }
    }

    private static bool TryResolveDoorConflictAlongWall(
        Transform furniture,
        Transform apartment,
        MiniVanPanelkaRoomIdentity room,
        Bounds[] doorClearances,
        Vector3 startPosition,
        bool wallItem)
    {
        // Slide along the intended back wall (facing), not the geometrically nearest edge.
        RoomWallEdge edge = TryGetFacingWallEdge(furniture, out RoomWallEdge facing)
            ? facing
            : NearestWallEdge(apartment.InverseTransformPoint(startPosition), room);
        Vector3 along = edge == RoomWallEdge.West || edge == RoomWallEdge.East
            ? new Vector3(0f, 0f, 1f)
            : new Vector3(1f, 0f, 0f);

        const float step = 0.12f;
        for (int i = 1; i <= 14; i++)
        {
            for (int sign = -1; sign <= 1; sign += 2)
            {
                Vector3 offset = along * (step * i * sign);
                furniture.position = startPosition + apartment.TransformVector(offset);
                if (!TryGetLocalBounds(furniture, apartment, out Bounds bounds))
                    continue;
                if (!IsInsideRoom(bounds, room, wallItem))
                    continue;
                if (IntersectsAny(bounds, doorClearances))
                    continue;
                if (wallItem)
                {
                    Vector3 pushed = ClampBoundsIntoRoom(bounds, room, true);
                    if (pushed.sqrMagnitude > 0.000001f)
                    {
                        furniture.position += apartment.TransformVector(
                            ClampCorrectionToStayNearWall(pushed, bounds, room, furniture));
                        if (!TryGetLocalBounds(furniture, apartment, out bounds) ||
                            IntersectsAny(bounds, doorClearances) ||
                            !IsInsideRoom(bounds, room, true))
                        {
                            continue;
                        }
                    }
                }

                return true;
            }
        }

        furniture.position = startPosition;
        return false;
    }

    private static Vector3 ClampCorrectionToStayNearWall(
        Vector3 correction,
        Bounds bounds,
        MiniVanPanelkaRoomIdentity room,
        Transform furniture = null)
    {
        RoomWallEdge edge = furniture != null && TryGetFacingWallEdge(furniture, out RoomWallEdge facing)
            ? facing
            : NearestWallEdge(bounds.center, room);
        // Kill any component that pulls the piece off its wall.
        switch (edge)
        {
            case RoomWallEdge.West:
                if (correction.x > 0f) correction.x = 0f;
                break;
            case RoomWallEdge.East:
                if (correction.x < 0f) correction.x = 0f;
                break;
            case RoomWallEdge.South:
                if (correction.z > 0f) correction.z = 0f;
                break;
            default:
                if (correction.z < 0f) correction.z = 0f;
                break;
        }

        return correction;
    }

    private static bool TryGetFacingWallEdge(Transform furniture, out RoomWallEdge edge)
    {
        edge = RoomWallEdge.South;
        if (furniture == null)
            return false;

        Vector3 intoWall = Vector3.zero;
        MiniVanPanelkaFurnitureAnchor anchor =
            furniture.GetComponent<MiniVanPanelkaFurnitureAnchor>();
        if (anchor != null &&
            anchor.BackWallAnchor != null &&
            anchor.FrontRoomAnchor != null)
        {
            intoWall = anchor.BackWallAnchor.position - anchor.FrontRoomAnchor.position;
        }
        else
        {
            intoWall = -furniture.forward;
        }

        intoWall.y = 0f;
        if (intoWall.sqrMagnitude < 0.0001f)
            return false;

        intoWall.Normalize();
        if (Mathf.Abs(intoWall.x) >= Mathf.Abs(intoWall.z))
            edge = intoWall.x < 0f ? RoomWallEdge.West : RoomWallEdge.East;
        else
            edge = intoWall.z < 0f ? RoomWallEdge.South : RoomWallEdge.North;
        return true;
    }

    private static void GetRoomInnerPlanes(
        MiniVanPanelkaRoomIdentity room,
        out float minX,
        out float maxX,
        out float minZ,
        out float maxZ)
    {
        minX = room.RoomCenterLocal.x - room.RoomSizeLocal.x * 0.5f + HalfWallThickness;
        maxX = room.RoomCenterLocal.x + room.RoomSizeLocal.x * 0.5f - HalfWallThickness;
        minZ = room.RoomCenterLocal.z - room.RoomSizeLocal.z * 0.5f + HalfWallThickness;
        maxZ = room.RoomCenterLocal.z + room.RoomSizeLocal.z * 0.5f - HalfWallThickness;
    }

    private static RoomWallEdge NearestWallEdge(Vector3 localPoint, MiniVanPanelkaRoomIdentity room)
    {
        GetRoomInnerPlanes(room, out float minX, out float maxX, out float minZ, out float maxZ);
        float dW = Mathf.Abs(localPoint.x - minX);
        float dE = Mathf.Abs(localPoint.x - maxX);
        float dS = Mathf.Abs(localPoint.z - minZ);
        float dN = Mathf.Abs(localPoint.z - maxZ);
        float best = dW;
        RoomWallEdge edge = RoomWallEdge.West;
        if (dE < best) { best = dE; edge = RoomWallEdge.East; }
        if (dS < best) { best = dS; edge = RoomWallEdge.South; }
        if (dN < best) edge = RoomWallEdge.North;
        return edge;
    }

    private static bool IsWallRuleFurnitureName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return false;
        return name.Contains("Sofa") ||
               name.Contains("Wardrobe") ||
               name.Contains("Wall_Unit") ||
               name.Contains("Fridge") ||
               name.Contains("Stove") ||
               name.Contains("Kitchen_Cabinet") ||
               name.Contains("Toilet") ||
               name.Contains("Bathtub") ||
               name.Contains("Sink") ||
               name.Contains("Entryway_Set") ||
               name.Contains("Storage_Shelves");
    }

    private static void FinalSnapWallFurniture(Transform apartment)
    {
        MiniVanPanelkaFurnitureAnchor[] furniture =
            apartment.GetComponentsInChildren<MiniVanPanelkaFurnitureAnchor>(true);
        for (int i = 0; i < furniture.Length; i++)
        {
            if (!IsWallRuleFurnitureName(furniture[i].name))
                continue;

            MiniVanPanelkaRoomIdentity room =
                furniture[i].GetComponentInParent<MiniVanPanelkaRoomIdentity>();
            if (room == null ||
                !TryGetLocalBounds(furniture[i].transform, apartment, out Bounds bounds))
            {
                continue;
            }

            // Snap to the INNER face of the wall the piece faces.
            RoomWallEdge edge = TryGetFacingWallEdge(furniture[i].transform, out RoomWallEdge facing)
                ? facing
                : NearestWallEdge(bounds.center, room);
            GetRoomInnerPlanes(room, out float minX, out float maxX, out float minZ, out float maxZ);

            // Slight plaster bite past the inner face only (never toward wall centerline deep).
            Vector3 delta = Vector3.zero;
            switch (edge)
            {
                case RoomWallEdge.West:
                    delta.x = (minX - WallMeshBite) - bounds.min.x;
                    break;
                case RoomWallEdge.East:
                    delta.x = (maxX + WallMeshBite) - bounds.max.x;
                    break;
                case RoomWallEdge.South:
                    delta.z = (minZ - WallMeshBite) - bounds.min.z;
                    break;
                default:
                    delta.z = (maxZ + WallMeshBite) - bounds.max.z;
                    break;
            }

            if (delta.sqrMagnitude > 0.0000001f)
                furniture[i].transform.position += apartment.TransformVector(delta);
        }
    }

    private static void ResolveDoorConflictsAlongWallsOnly(LayoutSpec layout, Transform apartment)
    {
        MiniVanPanelkaFurnitureAnchor[] furniture =
            apartment.GetComponentsInChildren<MiniVanPanelkaFurnitureAnchor>(true);
        Bounds[] doorClearances = GetDoorClearances(apartment);
        List<GameObject> remove = new List<GameObject>();
        for (int i = 0; i < furniture.Length; i++)
        {
            MiniVanPanelkaRoomIdentity room =
                furniture[i].GetComponentInParent<MiniVanPanelkaRoomIdentity>();
            if (room == null ||
                furniture[i].name.Contains("Entryway_Set") ||
                !TryGetLocalBounds(furniture[i].transform, apartment, out Bounds bounds) ||
                !IntersectsAny(bounds, doorClearances))
            {
                continue;
            }

            bool wallItem = IsWallRuleFurnitureName(furniture[i].name);
            bool resolved = TryResolveDoorConflictAlongWall(
                furniture[i].transform,
                apartment,
                room,
                doorClearances,
                furniture[i].transform.position,
                wallItem);
            if (!resolved)
                remove.Add(furniture[i].gameObject);
        }

        for (int i = 0; i < remove.Count; i++)
        {
            if (remove[i] != null)
                UnityEngine.Object.DestroyImmediate(remove[i]);
        }
    }

    private static void RemoveFurnitureStillBlockingDoors(LayoutSpec layout, Transform apartment)
    {
        Bounds[] clearances = GetDoorClearances(apartment);
        MiniVanPanelkaFurnitureAnchor[] furniture =
            apartment.GetComponentsInChildren<MiniVanPanelkaFurnitureAnchor>(true);
        for (int i = 0; i < furniture.Length; i++)
        {
            // Keep entryway where it was placed (blank wall); do not delete/move it here.
            if (furniture[i].name.Contains("Entryway_Set"))
                continue;
            if (!TryGetLocalBounds(furniture[i].transform, apartment, out Bounds bounds))
                continue;
            if (!IntersectsAny(bounds, clearances))
                continue;
            UnityEngine.Object.DestroyImmediate(furniture[i].gameObject);
        }
    }

    private static void ValidateFurnitureBounds(LayoutSpec layout, Transform apartment)
    {
        MiniVanPanelkaFurnitureAnchor[] furniture =
            apartment.GetComponentsInChildren<MiniVanPanelkaFurnitureAnchor>(true);
        for (int i = 0; i < furniture.Length; i++)
        {
            MiniVanPanelkaRoomIdentity room =
                furniture[i].GetComponentInParent<MiniVanPanelkaRoomIdentity>();
            Bounds bounds;
            if (room == null ||
                !TryGetLocalBounds(furniture[i].transform, apartment, out bounds))
                continue;

            if (!IsInsideRoom(bounds, room, IsWallRuleFurnitureName(furniture[i].name)))
            {
                throw new InvalidOperationException(
                    layout.Name + " places " + furniture[i].name +
                    " outside room " + room.RoomId + " at " + bounds.center + ".");
            }
        }
    }

    private static void ValidateDoorClearances(LayoutSpec layout, Transform apartment)
    {
        Bounds[] clearances = GetDoorClearances(apartment);
        MiniVanPanelkaFurnitureAnchor[] furniture =
            apartment.GetComponentsInChildren<MiniVanPanelkaFurnitureAnchor>(true);
        for (int furnitureIndex = 0; furnitureIndex < furniture.Length; furnitureIndex++)
        {
            Bounds bounds;
            if (!TryGetLocalBounds(
                    furniture[furnitureIndex].transform,
                    apartment,
                    out bounds))
            {
                continue;
            }

            if (IntersectsAny(bounds, clearances))
            {
                throw new InvalidOperationException(
                    layout.Name + " furniture " + furniture[furnitureIndex].name +
                    " still blocks a door.");
            }
        }
    }

    private static Bounds[] GetDoorClearances(Transform apartment)
    {
        MiniVanPanelkaRoomDoor[] doors =
            apartment.GetComponentsInChildren<MiniVanPanelkaRoomDoor>(true);
        List<Bounds> result = new List<Bounds>(doors.Length);
        for (int i = 0; i < doors.Length; i++)
        {
            Renderer panel = doors[i].GetComponentsInChildren<Renderer>(true)
                .FirstOrDefault(item => item.name == "Door_Panel");
            if (panel == null)
                continue;

            Bounds clearance = TransformBounds(
                panel.localBounds,
                apartment.worldToLocalMatrix * panel.transform.localToWorldMatrix);
            // Rule 1.A: forbid the opening/leaf only; jamb/frame may be touched.
            clearance.Expand(new Vector3(
                DoorOpeningClearanceExpand,
                0.05f,
                DoorOpeningClearanceExpand));
            result.Add(clearance);
        }
        return result.ToArray();
    }

    private static Vector3 ClampBoundsIntoRoom(
        Bounds bounds,
        MiniVanPanelkaRoomIdentity room,
        bool wallItem = false)
    {
        // Contain against inner wall faces; wall pieces may bite plaster slightly.
        float margin = wallItem ? -WallMeshBite : RoomBoundsMargin;
        GetRoomInnerPlanes(room, out float minX, out float maxX, out float minZ, out float maxZ);
        minX += margin;
        maxX -= margin;
        minZ += margin;
        maxZ -= margin;
        if (bounds.size.x > maxX - minX || bounds.size.z > maxZ - minZ)
        {
            throw new InvalidOperationException(
                room.RoomId + " is too small for furniture bounds " + bounds.size + ".");
        }

        float dx = bounds.min.x < minX
            ? minX - bounds.min.x
            : bounds.max.x > maxX ? maxX - bounds.max.x : 0f;
        float dz = bounds.min.z < minZ
            ? minZ - bounds.min.z
            : bounds.max.z > maxZ ? maxZ - bounds.max.z : 0f;
        return new Vector3(dx, 0f, dz);
    }

    private static bool IsInsideRoom(
        Bounds bounds,
        MiniVanPanelkaRoomIdentity room,
        bool wallItem = false)
    {
        // Wall pieces are allowed to bite slightly into plaster past the inner face.
        float margin = wallItem ? -WallMeshBite : RoomBoundsMargin;
        float slack = wallItem ? 0.04f : 0.02f;
        GetRoomInnerPlanes(room, out float minX, out float maxX, out float minZ, out float maxZ);
        minX += margin;
        maxX -= margin;
        minZ += margin;
        maxZ -= margin;
        return bounds.min.x >= minX - slack &&
               bounds.max.x <= maxX + slack &&
               bounds.min.z >= minZ - slack &&
               bounds.max.z <= maxZ + slack;
    }

    private static bool IntersectsAny(Bounds bounds, Bounds[] others)
    {
        for (int i = 0; i < others.Length; i++)
        {
            if (bounds.Intersects(others[i]))
                return true;
        }
        return false;
    }

    private static bool TryGetLocalBounds(
        Transform subject,
        Transform root,
        out Bounds bounds)
    {
        Renderer[] renderers = subject.GetComponentsInChildren<Renderer>(true);
        bounds = default(Bounds);
        bool initialized = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            Bounds current = TransformBounds(
                renderers[i].localBounds,
                root.worldToLocalMatrix * renderers[i].transform.localToWorldMatrix);
            if (!initialized)
            {
                bounds = current;
                initialized = true;
            }
            else
            {
                bounds.Encapsulate(current);
            }
        }
        return initialized;
    }

    private static Bounds TransformBounds(Bounds localBounds, Matrix4x4 matrix)
    {
        Vector3 center = matrix.MultiplyPoint3x4(localBounds.center);
        Vector3 extents = localBounds.extents;
        Vector3 axisX = matrix.MultiplyVector(new Vector3(extents.x, 0f, 0f));
        Vector3 axisY = matrix.MultiplyVector(new Vector3(0f, extents.y, 0f));
        Vector3 axisZ = matrix.MultiplyVector(new Vector3(0f, 0f, extents.z));
        Vector3 worldExtents = new Vector3(
            Mathf.Abs(axisX.x) + Mathf.Abs(axisY.x) + Mathf.Abs(axisZ.x),
            Mathf.Abs(axisX.y) + Mathf.Abs(axisY.y) + Mathf.Abs(axisZ.y),
            Mathf.Abs(axisX.z) + Mathf.Abs(axisY.z) + Mathf.Abs(axisZ.z));
        return new Bounds(center, worldExtents * 2f);
    }

    private static Transform NewChild(string name, Transform parent)
    {
        GameObject child = new GameObject(name);
        child.transform.SetParent(parent, false);
        return child.transform;
    }

    private static GameObject BuildBox(
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
        box.transform.localRotation = Quaternion.identity;
        box.transform.localScale = localScale;
        Renderer renderer = box.GetComponent<Renderer>();
        if (renderer != null)
            renderer.sharedMaterial = material;
        GameObjectUtility.SetStaticEditorFlags(box,
            StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
        return box;
    }

    private static void BuildExplicitCeiling(
        Transform apartment,
        Material material)
    {
        Transform existing = apartment.Find("EXPLICIT_CEILING");
        if (existing != null)
            UnityEngine.Object.DestroyImmediate(existing.gameObject);

        Transform ceiling = NewChild("EXPLICIT_CEILING", apartment);
        BuildBox(
            "Ceiling_Slab",
            ceiling,
            new Vector3(
                (MinX + MaxX) * 0.5f,
                WallHeight + 0.06f,
                (MinZ + MaxZ) * 0.5f),
            new Vector3(
                MaxX - MinX,
                0.12f,
                MaxZ - MinZ),
            material != null ? material : LoadCeilingMaterial());
    }

    private static Material LoadCeilingMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(
            MaterialFolder + "Ceiling_Apartment_LightGray_01.mat");
        if (material == null)
            throw new InvalidOperationException(
                "Missing ceiling material: " + MaterialFolder +
                "Ceiling_Apartment_LightGray_01.mat");
        return material;
    }

    private static Material LoadMaterial(string fileName)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + fileName);
        if (material == null)
            throw new InvalidOperationException("Missing material: " + fileName);
        return material;
    }

    private static LayoutSpec[] CreateLayouts()
    {
        return new[]
        {
            CreateCompact(),
            CreateLHall(),
            CreateTHall(),
            CreateFamily(),
            CreateDoctor()
        };
    }

    private static LayoutSpec CreateCornerLayout(
        LayoutSpec source,
        TemplateVariant variant)
    {
        bool blindX = variant == TemplateVariant.CornerLeft;
        LayoutSpec layout = BaseLayout(
            source.Index,
            source.Name,
            source.Wallpaper,
            source.KitchenWallpaper,
            source.Floor,
            source.WetFloor,
            source.WetTile,
            source.Door,
            blindX
                ? new Vector2(-1.2f, -0.3f)
                : new Vector2(-0.8f, -0.35f),
            blindX
                ? new Vector2(0f, MaxZ)
                : new Vector2(MaxX, -0.1f),
            blindX
                ? new Vector2(2.6f, MaxZ)
                : new Vector2(MaxX, -2.4f));
        layout.Variant = variant;

        if (blindX)
        {
            AddRoom(layout, "HALL", -4.24f, -4.34f, 2.2f, 0.3f,
                layout.Floor, "W", "N", "E");
            AddRoom(layout, "BATH", 2.2f, -4.34f, 4.24f, -2.15f,
                layout.WetFloor, "W");
            AddRoom(layout, "TOILET", 2.2f, -2.15f, 4.24f, 0.3f,
                layout.WetFloor, "W");
            AddRoom(layout, "BEDROOM_1", -4.24f, 0.3f, -1.45f, 4.34f,
                layout.Floor, "S");
            AddRoom(layout, "LIVING", -1.45f, 0.3f, 1.45f, 4.34f,
                layout.Floor, "S");
            AddRoom(layout, "KITCHEN", 1.45f, 0.3f, 4.24f, 4.34f,
                layout.WetFloor, "S");
            AddInteriorWall(
                layout, "Corner_FreeFacade_South", true, 0.3f, -4.4f, 4.4f,
                DoorAt(-2.8f, "Bedroom"), DoorAt(0f, "Living"),
                DoorAt(2.8f, "Kitchen"));
            AddInteriorWall(
                layout, "Corner_Wet_West", false, 2.2f, -4.5f, 0.3f,
                DoorAt(-3.15f, "Bath"), DoorAt(-0.95f, "Toilet"));
            AddInteriorWall(layout, "Corner_Wet_Split", true, -2.15f, 2.2f, 4.4f);
            AddInteriorWall(layout, "Corner_North_Split_A", false, -1.45f, 0.3f, 4.5f);
            AddInteriorWall(layout, "Corner_North_Split_B", false, 1.45f, 0.3f, 4.5f);
        }
        else
        {
            AddRoom(layout, "HALL", -4.24f, -4.34f, 1.15f, 1.25f,
                layout.Floor, "W", "N", "E");
            AddRoom(layout, "BEDROOM_1", 1.15f, -4.34f, 4.24f, -1.45f,
                layout.Floor, "W");
            AddRoom(layout, "LIVING", 1.15f, -1.45f, 4.24f, 1.45f,
                layout.Floor, "W");
            AddRoom(layout, "KITCHEN", 1.15f, 1.45f, 4.24f, 4.34f,
                layout.WetFloor, "W");
            AddRoom(layout, "BATH", -4.24f, 1.25f, -1.6f, 4.34f,
                layout.WetFloor, "S");
            AddRoom(layout, "TOILET", -1.6f, 1.25f, 1.15f, 4.34f,
                layout.WetFloor, "S");
            AddInteriorWall(
                layout, "Corner_FreeFacade_West", false, 1.15f, -4.5f, 4.5f,
                DoorAt(-2.75f, "Bedroom"), DoorAt(0f, "Living"),
                DoorAt(2.75f, "Kitchen"));
            AddInteriorWall(
                layout, "Corner_Wet_South", true, 1.25f, -4.4f, 1.15f,
                DoorAt(-2.8f, "Bath"), DoorAt(-0.25f, "Toilet"));
            AddInteriorWall(layout, "Corner_Wet_Split", false, -1.6f, 1.25f, 4.5f);
            AddInteriorWall(layout, "Corner_East_Split_A", true, -1.45f, 1.15f, 4.4f);
            AddInteriorWall(layout, "Corner_East_Split_B", true, 1.45f, 1.15f, 4.4f);
        }

        AutoFurnishLayout(layout);
        return layout;
    }

    private static LayoutSpec BaseLayout(
        int index,
        string name,
        string wallpaper,
        string kitchen,
        string floor,
        string wetFloor,
        string wetTile,
        string door,
        Vector2 routeHole,
        Vector2 balcony,
        Vector2 pipe)
    {
        LayoutSpec layout = new LayoutSpec
        {
            Index = index,
            Name = name,
            Wallpaper = wallpaper,
            KitchenWallpaper = kitchen,
            Floor = floor,
            WetFloor = wetFloor,
            WetTile = wetTile,
            Door = door,
            RouteHole = routeHole,
            Balcony = balcony,
            Pipe = pipe
        };
        AddOuterEnvelope(layout);
        return layout;
    }

    private static void AddOuterEnvelope(LayoutSpec layout)
    {
        WallSpec west = Wall("Envelope_West", false, MinX, MinZ, MaxZ, true, "ROOM");
        west.Openings.Add(Door(-2.8f, DoorWidth, "Entrance", true));
        layout.Walls.Add(west);
        layout.Walls.Add(Wall("Envelope_South", true, MinZ, MinX, MaxX, true, "ROOM"));
        layout.Walls.Add(Wall("Envelope_East", false, MaxX, MinZ, MaxZ, true, "ROOM"));
        layout.Walls.Add(Wall("Envelope_North", true, MaxZ, MinX, MaxX, true, "ROOM"));
    }

    private static void ApplyWindowRules(LayoutSpec layout)
    {
        WallSpec east = layout.Walls.FirstOrDefault(wall => wall.Id == "Envelope_East");
        WallSpec north = layout.Walls.FirstOrDefault(wall => wall.Id == "Envelope_North");
        if (east == null || north == null)
            throw new InvalidOperationException(layout.Name + " is missing exterior window walls.");

        east.Openings.RemoveAll(opening => opening.IsWindow);
        north.Openings.RemoveAll(opening => opening.IsWindow);

        for (int i = 0; i < layout.Rooms.Count; i++)
        {
            RoomSpec room = layout.Rooms[i];
            if (DisallowsWindows(room.Id))
                continue;

            bool touchesEast = room.Bounds.xMax >= MaxX - 0.25f;
            bool touchesNorth = room.Bounds.yMax >= MaxZ - 0.25f;
            if (touchesEast)
                AddCenteredRoomWindows(
                    east,
                    room,
                    room.Bounds.yMin,
                    room.Bounds.yMax,
                    "East_" + room.Id);
            if (touchesNorth)
                AddCenteredRoomWindows(
                    north,
                    room,
                    room.Bounds.xMin,
                    room.Bounds.xMax,
                    "North_" + room.Id);
        }
    }

    private static bool DisallowsWindows(string roomId)
    {
        if (string.IsNullOrEmpty(roomId))
            return true;

        string normalized = roomId.ToUpperInvariant();
        return normalized.Contains("TOILET") ||
               normalized.Contains("BATH") ||
               normalized.Contains("HALL") ||
               normalized.Contains("CORRIDOR");
    }

    private static void AddCenteredRoomWindows(
        WallSpec wall,
        RoomSpec room,
        float roomMin,
        float roomMax,
        string idPrefix)
    {
        const float edgePadding = 0.42f;
        float span = roomMax - roomMin;
        if (span < 0.95f)
            return;

        float center = (roomMin + roomMax) * 0.5f;
        if (span >= 3.75f)
        {
            float windowWidth = Mathf.Min(1.02f, (span - edgePadding * 2f - 0.42f) * 0.5f);
            windowWidth = Mathf.Max(1.00f, windowWidth);
            float offset = Mathf.Min(0.68f, span * 0.18f);
            wall.Openings.Add(Window(
                Mathf.Clamp(center - offset, roomMin + edgePadding + windowWidth * 0.5f,
                    roomMax - edgePadding - windowWidth * 0.5f),
                windowWidth,
                idPrefix + "_A",
                room.Id));
            wall.Openings.Add(Window(
                Mathf.Clamp(center + offset, roomMin + edgePadding + windowWidth * 0.5f,
                    roomMax - edgePadding - windowWidth * 0.5f),
                windowWidth,
                idPrefix + "_B",
                room.Id));
            return;
        }

        // Opening must leave glass >= ~0.84 after 0.08 frames each side.
        float width = Mathf.Min(1.35f, span - edgePadding * 2f);
        if (width < 1.00f)
            return;
        wall.Openings.Add(Window(center, width, idPrefix, room.Id));
    }

    private static LayoutSpec CreateCompact()
    {
        LayoutSpec l = BaseLayout(1, "Compact",
            "Wallpaper_Room_SageLeaves_01.mat", "Wallpaper_Kitchen_MintGrid_02.mat",
            "Floor_Parquet_Herringbone_Honey_01.mat", "Floor_Tile_WarmGray_05.mat",
            "Tile_Bathroom_Seafoam_02.mat", "Door_DarkBrownPanels_01.mat",
            new Vector2(-1.35f, -0.2f), new Vector2(2.1f, MaxZ), new Vector2(MaxX, 2.05f));
        AddRoom(l, "HALL", -4.24f, -4.34f, 1.15f, 0.65f, l.Floor, "W", "N", "E");
        AddRoom(l, "TOILET", -4.24f, 0.65f, -2.45f, 2.05f, l.WetFloor, "S");
        AddRoom(l, "BATH", -4.24f, 2.05f, -2.45f, 4.34f, l.WetFloor, "S");
        AddRoom(l, "KITCHEN", 1.15f, -4.34f, 4.24f, 0.65f, l.WetFloor, "W");
        AddRoom(l, "BEDROOM", -2.45f, 0.65f, 0.35f, 4.34f, l.Floor, "S");
        AddRoom(l, "LIVING", 0.35f, 0.65f, 4.24f, 4.34f, l.Floor, "S");
        AddInteriorWall(l, "Wet_East", false, -2.45f, 0.65f, 4.5f,
            DoorAt(1.38f, "Toilet"), DoorAt(2.8f, "Bath"));
        AddInteriorWall(l, "Wet_Split", true, 2.05f, -4.4f, -2.45f);
        AddInteriorWall(l, "Kitchen_West", false, 1.15f, -4.5f, 0.65f,
            DoorAt(-0.3f, "Kitchen"));
        AddInteriorWall(l, "North_South", true, 0.65f, -2.45f, 4.4f,
            DoorAt(-1.1f, "Bedroom"), DoorAt(1.2f, "Living"));
        AddInteriorWall(l, "North_Split", false, 0.35f, 0.65f, 4.5f);
        AutoFurnishLayout(l);
        return l;
    }

    private static LayoutSpec CreateLHall()
    {
        LayoutSpec l = BaseLayout(2, "LHall",
            "Wallpaper_Room_DustyBlueDiamonds_02.mat", "Wallpaper_Kitchen_Cherries_03.mat",
            "Floor_Laminate_Oak_03.mat", "Floor_Linoleum_Retro_04.mat",
            "Tile_Bathroom_BlueChecker_01.mat", "Door_FadedTeal_02.mat",
            new Vector2(-1.2f, -0.3f), new Vector2(4.4f, 2.5f), new Vector2(4.4f, 1.7f));
        AddRoom(l, "HALL", -4.24f, -4.34f, 0.1f, 0.4f, l.Floor, "W", "N", "E");
        AddRoom(l, "BATH", 0.1f, -4.34f, 1.75f, -1.15f, l.WetFloor, "W");
        AddRoom(l, "TOILET", 1.75f, -4.34f, 3.1f, -1.15f, l.WetFloor, "W");
        AddRoom(l, "KITCHEN", 3.1f, -4.34f, 4.24f, 0.4f, l.WetFloor, "W");
        AddRoom(l, "BEDROOM_1", -4.24f, 0.4f, -1.45f, 4.34f, l.Floor, "S");
        AddRoom(l, "BEDROOM_2", -1.45f, 0.4f, 1.2f, 4.34f, l.Floor, "S");
        AddRoom(l, "LIVING", 1.2f, 0.4f, 4.24f, 4.34f, l.Floor, "S");
        AddInteriorWall(l, "North_South", true, 0.4f, -4.4f, 4.4f,
            DoorAt(-2.8f, "Bedroom2"), DoorAt(-0.25f, "Bedroom1"), DoorAt(2f, "Living"));
        AddInteriorWall(l, "North_LeftSplit", false, -1.45f, 0.4f, 4.5f);
        AddInteriorWall(l, "North_RightSplit", false, 1.2f, 0.4f, 4.5f);
        AddInteriorWall(l, "Wet_North", true, -1.15f, 0.1f, 4.4f,
            DoorAt(0.9f, "Bath"), DoorAt(2.35f, "Toilet"), DoorAt(3.65f, "Kitchen"));
        AddInteriorWall(l, "Wet_Split1", false, 1.75f, -4.5f, -1.15f);
        AddInteriorWall(l, "Wet_Split2", false, 3.1f, -4.5f, -1.15f);
        AutoFurnishLayout(l);
        return l;
    }

    private static LayoutSpec CreateTHall()
    {
        LayoutSpec l = BaseLayout(3, "THall",
            "Wallpaper_Room_MutedRoseGeometry_05.mat", "Wallpaper_Kitchen_YellowFlowers_01.mat",
            "Floor_Parquet_Herringbone_Dark_02.mat", "Floor_Tile_WarmGray_05.mat",
            "Tile_Bathroom_PowderBlueSubway_03.mat", "Door_MustardVinyl_03.mat",
            new Vector2(-1.2f, -0.2f), new Vector2(2.7f, 4.5f), new Vector2(4.4f, 2.2f));
        AddRoom(l, "HALL", -1.5f, -4.34f, 1.45f, 4.34f, l.Floor, "W", "N", "E");
        AddRoom(l, "BATH", -4.24f, -4.34f, -2.75f, -1.5f, l.WetFloor, "E");
        AddRoom(l, "TOILET", -2.75f, -4.34f, -1.5f, -1.5f, l.WetFloor, "E");
        AddRoom(l, "BEDROOM_1", -4.24f, -1.5f, -1.5f, 1.35f, l.Floor, "E");
        AddRoom(l, "KITCHEN", -4.24f, 1.35f, -1.5f, 4.34f, l.WetFloor, "E");
        AddRoom(l, "BEDROOM_2", 1.45f, -4.34f, 4.24f, 0.3f, l.Floor, "W");
        AddRoom(l, "LIVING", 1.45f, 0.3f, 4.24f, 4.34f, l.Floor, "W");
        AddInteriorWall(l, "Left_Hall", false, -1.5f, -4.5f, 4.5f,
            DoorAt(-2.9f, "Toilet"), DoorAt(-0.1f, "Bedroom1"), DoorAt(2.4f, "Kitchen"));
        AddInteriorWall(l, "Left_WetSplit", false, -2.75f, -4.5f, -1.5f);
        AddInteriorWall(l, "Left_Split1", true, -1.5f, -4.4f, -1.5f);
        AddInteriorWall(l, "Left_Split2", true, 1.35f, -4.4f, -1.5f);
        AddInteriorWall(l, "Right_Hall", false, 1.45f, -4.5f, 4.5f,
            DoorAt(-1.5f, "Bedroom2"), DoorAt(1.55f, "Living"));
        AddInteriorWall(l, "Right_Split", true, 0.3f, 1.45f, 4.4f);
        AutoFurnishLayout(l);
        return l;
    }

    private static LayoutSpec CreateFamily()
    {
        LayoutSpec l = BaseLayout(4, "Family",
            "Wallpaper_Room_WarmOchreFloral_03.mat", "Wallpaper_Kitchen_BeigeVines_05.mat",
            "Floor_Laminate_Oak_03.mat", "Floor_Linoleum_Retro_04.mat",
            "Tile_Bathroom_IvoryFloral_04.mat", "Door_RedBrownVeneer_05.mat",
            new Vector2(-1f, -0.15f), new Vector2(0.7f, 4.5f), new Vector2(4.4f, 1.8f));
        AddRoom(l, "HALL", -4.24f, -4.34f, 0.2f, -1.05f, l.Floor, "W", "N", "E");
        AddRoom(l, "BATH", 0.2f, -4.34f, 1.75f, -1.05f, l.WetFloor, "W");
        AddRoom(l, "TOILET", 1.75f, -4.34f, 2.95f, -1.05f, l.WetFloor, "W");
        AddRoom(l, "KITCHEN", 2.95f, -4.34f, 4.24f, 0.35f, l.WetFloor, "W");
        AddRoom(l, "CORRIDOR", -4.24f, -1.05f, 4.24f, 0.35f, l.Floor, "S", "N");
        AddRoom(l, "BEDROOM_1", -4.24f, 0.35f, -1.5f, 4.34f, l.Floor, "S");
        AddRoom(l, "LIVING", -1.5f, 0.35f, 1.4f, 4.34f, l.Floor, "S");
        AddRoom(l, "BEDROOM_2", 1.4f, 2f, 4.24f, 4.34f, l.Floor, "S");
        AddRoom(l, "BEDROOM_3", 1.4f, 0.35f, 4.24f, 2f, l.Floor, "N");
        AddInteriorWall(l, "Lower_North", true, -1.05f, -4.4f, 2.95f,
            DoorAt(-1.2f, "Hall"), DoorAt(0.95f, "Bath"), DoorAt(2.3f, "Toilet"));
        AddInteriorWall(l, "Wet_1", false, 0.2f, -4.5f, -1.05f);
        AddInteriorWall(l, "Wet_2", false, 1.75f, -4.5f, -1.05f);
        AddInteriorWall(l, "Kitchen_West", false, 2.95f, -4.5f, 0.35f,
            DoorAt(-0.25f, "Kitchen"));
        AddInteriorWall(l, "Corridor_North", true, 0.35f, -4.4f, 4.4f,
            DoorAt(-2.8f, "Bedroom1"), DoorAt(-0.1f, "Living"), DoorAt(2.8f, "Bedroom3"));
        AddInteriorWall(l, "North_1", false, -1.5f, 0.35f, 4.5f);
        AddInteriorWall(l, "North_2", false, 1.4f, 0.35f, 4.5f,
            DoorAt(1f, "Bedroom3"));
        AddInteriorWall(l, "East_Split", true, 2f, 1.4f, 4.4f,
            DoorAt(2.8f, "Bedroom2"));
        AutoFurnishLayout(l);
        return l;
    }

    private static LayoutSpec CreateDoctor()
    {
        LayoutSpec l = BaseLayout(5, "Doctor",
            "Wallpaper_Room_PaleGrayStripes_04.mat", "Wallpaper_Kitchen_BlueDiamonds_04.mat",
            "Floor_Parquet_Herringbone_Honey_01.mat", "Floor_Tile_WarmGray_05.mat",
            "Tile_Bathroom_TurquoiseGeometry_05.mat", "Door_GrayMetal_04.mat",
            new Vector2(-0.8f, -0.4f), new Vector2(0.2f, 4.5f), new Vector2(4.4f, 2.2f));
        AddRoom(l, "HALL", -4.24f, -4.34f, 0.4f, -0.75f, l.Floor, "W", "N", "E");
        AddRoom(l, "BATH", 0.4f, -4.34f, 1.9f, -0.75f, l.WetFloor, "W");
        AddRoom(l, "TOILET", 1.9f, -4.34f, 3f, -0.75f, l.WetFloor, "W");
        AddRoom(l, "KITCHEN", 3f, -4.34f, 4.24f, 0.7f, l.WetFloor, "W");
        AddRoom(l, "STORAGE", -4.24f, -0.75f, -1.15f, 4.34f, l.WetFloor, "S");
        AddRoom(l, "BEDROOM", -1.15f, -0.75f, 1.45f, 4.34f, l.Floor, "S");
        AddRoom(l, "LIVING", 1.45f, 0.7f, 4.24f, 4.34f, l.Floor, "S");
        AddRoom(l, "DINING", 1.45f, -0.75f, 4.24f, 0.7f, l.Floor, "N", "E");
        AddInteriorWall(l, "Lower_North", true, -0.75f, -4.4f, 3f,
            DoorAt(-2.6f, "Doctor"), DoorAt(0.95f, "Bath"), DoorAt(2.42f, "Toilet"));
        AddInteriorWall(l, "Wet_1", false, 0.4f, -4.5f, -0.75f);
        AddInteriorWall(l, "Wet_2", false, 1.9f, -4.5f, -0.75f);
        AddInteriorWall(l, "Kitchen_West", false, 3f, -4.5f, 0.7f,
            DoorAt(-0.2f, "Kitchen"));
        AddInteriorWall(l, "Doctor_East", false, -1.15f, -0.75f, 4.5f,
            DoorAt(0.15f, "Doctor"));
        AddInteriorWall(l, "Bedroom_East", false, 1.45f, -0.75f, 4.5f,
            DoorAt(0.05f, "Bedroom"), DoorAt(2f, "Living"));
        AddInteriorWall(l, "Living_South", true, 0.7f, 1.45f, 4.4f,
            DoorAt(2.5f, "Dining"));
        AutoFurnishLayout(l);
        return l;
    }

    private static RoomSpec AddRoom(
        LayoutSpec layout,
        string id,
        float xMin,
        float zMin,
        float xMax,
        float zMax,
        string surface,
        params string[] doors)
    {
        RoomSpec room = new RoomSpec
        {
            Id = id,
            Bounds = Rect.MinMaxRect(xMin, zMin, xMax, zMax),
            Surface = surface,
            DoorEdges = doors
        };
        layout.Rooms.Add(room);
        return room;
    }

    private static WallSpec Wall(
        string id,
        bool alongX,
        float fixedCoordinate,
        float min,
        float max,
        bool envelope,
        string surface)
    {
        return new WallSpec
        {
            Id = id,
            AlongX = alongX,
            Fixed = fixedCoordinate,
            Min = min,
            Max = max,
            Envelope = envelope,
            Surface = surface
        };
    }

    private static void AddInteriorWall(
        LayoutSpec layout,
        string id,
        bool alongX,
        float fixedCoordinate,
        float min,
        float max,
        params OpeningSpec[] doors)
    {
        WallSpec wall = Wall(id, alongX, fixedCoordinate, min, max, false, "ROOM");
        wall.Openings.AddRange(doors);
        layout.Walls.Add(wall);
    }

    private static OpeningSpec DoorAt(float center, string id)
    {
        return Door(center, DoorWidth, id, false);
    }

    private static OpeningSpec Door(float center, float width, string id, bool entrance)
    {
        return new OpeningSpec
        {
            Center = center,
            Width = width,
            IsWindow = false,
            IsEntrance = entrance,
            Id = id
        };
    }

    private static OpeningSpec Window(float center, float width, string id)
    {
        return Window(center, width, id, string.Empty);
    }

    private static OpeningSpec Window(
        float center,
        float width,
        string id,
        string roomId)
    {
        return new OpeningSpec
        {
            Center = center,
            Width = width,
            IsWindow = true,
            Id = id,
            RoomId = roomId
        };
    }

    private enum RoomWallEdge
    {
        West,
        East,
        South,
        North
    }

    private sealed class WallPlacement
    {
        public RoomWallEdge Edge;
        public Vector2 Back;
        public Vector2 Front;
        public float Score;
        public bool HasWindow;
        public bool HasDoor;
        public float Length;
    }

    private static void AddFurniture(
        LayoutSpec layout,
        string roomId,
        string path,
        float backX,
        float backZ,
        float frontX,
        float frontZ,
        float scale = 1f)
    {
        RoomSpec room = layout.Rooms.First(item => item.Id == roomId);
        room.Furniture.Add(new FurnitureSpec
        {
            Path = path,
            Back = new Vector2(backX, backZ),
            Front = new Vector2(frontX, frontZ),
            Scale = scale
        });
    }

    private static void AutoFurnishLayout(LayoutSpec layout)
    {
        for (int i = 0; i < layout.Rooms.Count; i++)
        {
            RoomSpec room = layout.Rooms[i];
            string id = room.Id.ToUpperInvariant();
            if (id.Contains("HALL") || id.Contains("CORRIDOR"))
                FurnishEntryRoom(layout, room);
            else if (id.Contains("KITCHEN"))
                FurnishKitchenRoom(layout, room);
            else if (id.Contains("BEDROOM"))
                FurnishBedroomRoom(layout, room);
            else if (id.Contains("LIVING"))
                FurnishLivingRoom(layout, room);
            else if (id.Contains("BATH"))
                FurnishBathSoviet(layout, room);
            else if (id.Contains("TOILET"))
                FurnishToiletSoviet(layout, room);
            else if (id.Contains("STORAGE"))
                FurnishStorageRoom(layout, room);
        }
    }

    private static void FurnishEntryRoom(LayoutSpec layout, RoomSpec room)
    {
        RoomWallEdge doorWall = FindDoorWall(room) ?? RoomWallEdge.West;

        // Entryway ONLY: longest wall with NO door, centered, flush.
        RoomWallEdge? entryEdge = null;
        if (TryFindLongestWallWithoutDoor(room, out RoomWallEdge blankWall))
        {
            entryEdge = blankWall;
            GetWallPose(
                room,
                blankWall,
                0.5f,
                WallFurnitureInset,
                out Vector2 entryBack,
                out Vector2 entryFront);
            AddFurniture(
                layout,
                room.Id,
                "Entryway/Soviet_Entryway_Set.prefab",
                entryBack.x,
                entryBack.y,
                entryFront.x,
                entryFront.y,
                0.88f);
        }

        WallPlacement wardrobeWall = ChooseBestWall(
            room,
            preferNoDoor: true,
            minLength: 0.9f,
            exclude: entryEdge,
            requireNoDoor: true);
        if (wardrobeWall != null)
        {
            PlaceWallFurnitureOnWall(
                layout,
                room,
                wardrobeWall,
                "Entryway/Hall_FullHeight_Wardrobe.prefab",
                0.82f,
                0.5f,
                0.45f);
        }

        GetWallPose(room, doorWall, 0.5f, WallFurnitureInset, out Vector2 doorBack, out Vector2 doorFront);
        AddFurniture(
            layout,
            room.Id,
            "Entryway/Soviet_Entry_Hall_Rug.prefab",
            doorBack.x + doorFront.x * 0.55f,
            doorBack.y + doorFront.y * 0.55f,
            doorFront.x,
            doorFront.y,
            0.8f);
    }

    private static bool TryFindLongestWallWithoutDoor(RoomSpec room, out RoomWallEdge edge)
    {
        edge = RoomWallEdge.South;
        float bestLength = -1f;
        bool found = false;
        RoomWallEdge[] edges =
        {
            RoomWallEdge.West,
            RoomWallEdge.East,
            RoomWallEdge.South,
            RoomWallEdge.North
        };
        for (int i = 0; i < edges.Length; i++)
        {
            if (HasDoorOnEdge(room, edges[i]))
                continue;
            float length = edges[i] == RoomWallEdge.West || edges[i] == RoomWallEdge.East
                ? room.Bounds.height
                : room.Bounds.width;
            if (length > bestLength)
            {
                bestLength = length;
                edge = edges[i];
                found = true;
            }
        }

        return found;
    }

    private static void FurnishKitchenRoom(LayoutSpec layout, RoomSpec room)
    {
        WallPlacement wall = ChooseBestWall(room, preferNoDoor: true, minLength: 1.4f);
        if (wall == null)
            return;
        PlaceWallFurnitureOnWall(
            layout, room, wall, "Kitchen/Kitchen_Cabinet_Run.prefab", 0.84f, 0.5f, 0.7f);
        PlaceWallFurnitureOnWall(
            layout, room, wall, "Kitchen/Rounded_Soviet_Fridge.prefab", 0.82f, 0.22f, 0.35f);
        PlaceWallFurnitureOnWall(
            layout, room, wall, "Kitchen/Soviet_Stove.prefab", 0.82f, 0.78f, 0.35f);

        Vector2 center = new Vector2(room.Bounds.center.x, room.Bounds.center.y);
        AddFurniture(
            layout,
            room.Id,
            "Kitchen/Kitchen_Table_Set.prefab",
            center.x + wall.Front.x * 0.85f,
            center.y + wall.Front.y * 0.85f,
            wall.Front.x,
            wall.Front.y,
            0.72f);
    }

    private static void FurnishBedroomRoom(LayoutSpec layout, RoomSpec room)
    {
        TryPlaceWallFurniture(
            layout,
            room,
            "Bedroom/FullHeight_Double_Wardrobe.prefab",
            room.Bounds.width < 2.6f || room.Bounds.height < 2.6f ? 0.68f : 0.78f,
            0.28f,
            0.5f,
            preferNoDoor: true,
            requireNoDoor: false,
            out WallPlacement wardrobeWall);

        // Bed is not wall-rule furniture: keep clear of door and slightly off wardrobe.
        WallPlacement bedWall = ChooseBestWall(
            room,
            preferNoDoor: true,
            minLength: 1.2f,
            exclude: wardrobeWall != null ? wardrobeWall.Edge : (RoomWallEdge?)null);
        if (bedWall == null)
            return;
        GetWallPose(room, bedWall.Edge, 0.55f, WallFurnitureInset, out Vector2 bedBack, out Vector2 bedFront);
        AddFurniture(
            layout,
            room.Id,
            "Bedroom/LowPoly_Bed.prefab",
            bedBack.x + bedFront.x * 0.2f,
            bedBack.y + bedFront.y * 0.2f,
            bedFront.x,
            bedFront.y,
            0.82f);
    }

    private static void FurnishLivingRoom(LayoutSpec layout, RoomSpec room)
    {
        TryPlaceWallFurniture(
            layout,
            room,
            "LivingRoom/Soviet_Wall_Unit.prefab",
            0.76f,
            0.3f,
            0.7f,
            preferNoDoor: true,
            requireNoDoor: false,
            out WallPlacement unitWall);

        RoomWallEdge? preferFacing = null;
        if (unitWall != null && !unitWall.HasWindow)
            preferFacing = unitWall.Edge;

        // Rule 6.A: sofa only on a wall without a door (window allowed).
        WallPlacement sofaWall = ChooseBestWall(
            room,
            preferNoDoor: true,
            minLength: 1.4f,
            exclude: unitWall != null ? unitWall.Edge : (RoomWallEdge?)null,
            preferOppositeOf: preferFacing,
            requireNoDoor: true);
        if (sofaWall != null && sofaWall.HasDoor)
            sofaWall = null;

        Vector2 center = new Vector2(room.Bounds.center.x, room.Bounds.center.y);
        Vector2 facing = sofaWall != null
            ? sofaWall.Front
            : unitWall != null
                ? unitWall.Front
                : new Vector2(0f, -1f);

        if (sofaWall != null)
        {
            PlaceWallFurnitureOnWall(
                layout, room, sofaWall, "LivingRoom/Soviet_Sofa.prefab", 0.82f, 0.5f, 0.95f);
        }

        AddFurniture(
            layout,
            room.Id,
            "LivingRoom/Pixel_Carpet.prefab",
            center.x,
            center.y,
            facing.x,
            facing.y,
            0.85f);

        if (unitWall != null)
        {
            GetWallPose(room, unitWall.Edge, 0.55f, WallFurnitureInset, out Vector2 tvBack, out Vector2 tvFront);
            AddFurniture(
                layout,
                room.Id,
                "LivingRoom/CRT_Television.prefab",
                tvBack.x + tvFront.x * 0.2f,
                tvBack.y + tvFront.y * 0.2f,
                tvFront.x,
                tvFront.y,
                0.82f);
        }
        else if (sofaWall != null)
        {
            AddFurniture(
                layout,
                room.Id,
                "LivingRoom/CRT_Television.prefab",
                center.x - facing.x * 0.8f,
                center.y - facing.y * 0.8f,
                facing.x,
                facing.y,
                0.82f);
        }
    }

    private static void FurnishBathSoviet(LayoutSpec layout, RoomSpec room)
    {
        // Classic soviet wet room: tub along longest side, sink on remaining wall.
        RoomWallEdge longWall = room.Bounds.width >= room.Bounds.height
            ? (room.Bounds.center.y > 0f ? RoomWallEdge.North : RoomWallEdge.South)
            : (room.Bounds.center.x > 0f ? RoomWallEdge.East : RoomWallEdge.West);
        if (HasDoorOnEdge(room, longWall))
        {
            longWall = Opposite(longWall);
        }

        GetWallPose(room, longWall, 0.5f, WallFurnitureInset, out Vector2 tubBack, out Vector2 tubFront);
        AddFurniture(
            layout, room.Id, "Bathroom/Bathtub.prefab",
            tubBack.x, tubBack.y, tubFront.x, tubFront.y, 0.74f);

        WallPlacement sinkWall = ChooseBestWall(
            room, preferNoDoor: true, minLength: 0.7f, exclude: longWall);
        if (sinkWall == null)
            return;
        GetWallPose(room, sinkWall.Edge, 0.7f, WallFurnitureInset, out Vector2 sinkBack, out Vector2 sinkFront);
        AddFurniture(
            layout, room.Id, "Bathroom/Pedestal_Sink.prefab",
            sinkBack.x, sinkBack.y, sinkFront.x, sinkFront.y, 0.72f);
    }

    private static void FurnishToiletSoviet(LayoutSpec layout, RoomSpec room)
    {
        // Classic: toilet against wall opposite the door, facing the door.
        RoomWallEdge doorWall = FindDoorWall(room) ?? RoomWallEdge.South;
        RoomWallEdge toiletWall = Opposite(doorWall);
        GetWallPose(room, toiletWall, 0.5f, WallFurnitureInset, out Vector2 back, out Vector2 front);
        AddFurniture(
            layout, room.Id, "Toilet/Toilet.prefab",
            back.x, back.y, front.x, front.y, 0.72f);
    }

    private static void FurnishStorageRoom(LayoutSpec layout, RoomSpec room)
    {
        WallPlacement a = ChooseBestWall(room, preferNoDoor: true, minLength: 1f, requireNoDoor: true);
        if (a != null)
            PlaceWallFurnitureOnWall(layout, room, a, "Storage/Storage_Shelves.prefab", 1f, 0.3f, 0.4f);
        WallPlacement b = ChooseBestWall(
            room,
            preferNoDoor: true,
            minLength: 1f,
            exclude: a != null ? a.Edge : (RoomWallEdge?)null,
            requireNoDoor: true);
        if (b != null)
            PlaceWallFurnitureOnWall(layout, room, b, "Storage/Storage_Shelves.prefab", 1f, 0.7f, 0.4f);
        WallPlacement c = ChooseBestWall(
            room,
            preferNoDoor: false,
            minLength: 0.9f,
            exclude: a != null ? a.Edge : (RoomWallEdge?)null,
            excludeB: b != null ? b.Edge : (RoomWallEdge?)null);
        if (c != null)
            PlaceWallFurnitureOnWall(layout, room, c, "Storage/Storage_Shelves.prefab", 1f, 0.5f, 0.4f);
    }

    private static bool TryPlaceWallFurniture(
        LayoutSpec layout,
        RoomSpec room,
        string path,
        float scale,
        float along,
        float halfWidthWorld,
        bool preferNoDoor,
        bool requireNoDoor,
        out WallPlacement wall)
    {
        wall = ChooseBestWall(
            room,
            preferNoDoor: preferNoDoor,
            minLength: 0.9f,
            requireNoDoor: requireNoDoor);
        if (wall == null)
            return false;
        return PlaceWallFurnitureOnWall(layout, room, wall, path, scale, along, halfWidthWorld);
    }

    private static bool PlaceWallFurnitureOnWall(
        LayoutSpec layout,
        RoomSpec room,
        WallPlacement wall,
        string path,
        float scale,
        float along,
        float halfWidthWorld = 0.4f)
    {
        if (wall == null)
            return false;

        float t = along;
        if (wall.HasDoor)
        {
            if (!TryPickAlongAwayFromDoors(
                    layout,
                    room,
                    wall.Edge,
                    along,
                    halfWidthWorld,
                    out t))
            {
                return false;
            }
        }

        GetWallPose(room, wall.Edge, t, WallFurnitureInset, out Vector2 back, out Vector2 front);
        AddFurniture(layout, room.Id, path, back.x, back.y, front.x, front.y, scale);
        return true;
    }

    private static WallPlacement ChooseBestWall(
        RoomSpec room,
        bool preferNoDoor,
        float minLength,
        RoomWallEdge? exclude = null,
        RoomWallEdge? excludeB = null,
        RoomWallEdge? preferOppositeOf = null,
        bool requireNoDoor = false)
    {
        WallPlacement[] walls = BuildWallPlacements(room);
        WallPlacement best = null;
        float bestScore = float.NegativeInfinity;
        for (int i = 0; i < walls.Length; i++)
        {
            WallPlacement wall = walls[i];
            if (exclude.HasValue && wall.Edge == exclude.Value)
                continue;
            if (excludeB.HasValue && wall.Edge == excludeB.Value)
                continue;
            if (requireNoDoor && wall.HasDoor)
                continue;
            if (wall.Length + 0.001f < minLength)
                continue;

            float score = wall.Score;
            if (preferNoDoor && wall.HasDoor)
                score -= 40f;
            if (preferOppositeOf.HasValue && wall.Edge == Opposite(preferOppositeOf.Value))
                score += 35f;
            // Prefer no-window first (already in Score), then longer walls.
            // Rule 5.A: window is allowed on "blank" (no-door) walls — do not over-penalize.
            if (wall.HasWindow && !wall.HasDoor)
                score += 200f;
            score += wall.Length * 2f;
            if (score > bestScore)
            {
                bestScore = score;
                best = wall;
            }
        }

        if (best != null)
            return best;

        if (requireNoDoor)
            return null;

        // Fallback: any remaining wall, even short / with door.
        for (int i = 0; i < walls.Length; i++)
        {
            WallPlacement wall = walls[i];
            if (exclude.HasValue && wall.Edge == exclude.Value)
                continue;
            if (excludeB.HasValue && wall.Edge == excludeB.Value)
                continue;
            if (best == null || wall.Score > best.Score)
                best = wall;
        }

        return best;
    }

    private static bool TryPickAlongAwayFromDoors(
        LayoutSpec layout,
        RoomSpec room,
        RoomWallEdge edge,
        float preferredAlong01,
        float halfWidthWorld,
        out float along01)
    {
        along01 = preferredAlong01;
        GetRoomEdgeSpan(room, edge, out float wallMin, out float wallMax);
        float length = wallMax - wallMin;
        if (length < 0.2f)
            return false;

        float halfNorm = Mathf.Clamp(halfWidthWorld / length, 0.05f, 0.45f);
        List<Vector2> blocked = GetDoorIntervalsNormalized(layout, room, edge);
        List<Vector2> free = BuildFreeSegments(blocked);
        if (free.Count == 0)
            return false;

        float prefer = Mathf.Clamp01(preferredAlong01);
        Vector2 bestSeg = default;
        float bestScore = float.NegativeInfinity;
        for (int i = 0; i < free.Count; i++)
        {
            Vector2 seg = free[i];
            float segLen = seg.y - seg.x;
            if (segLen < halfNorm * 2f + 0.02f)
                continue;

            float usableMin = seg.x + halfNorm;
            float usableMax = seg.y - halfNorm;
            float t = Mathf.Clamp(prefer, usableMin, usableMax);
            float score = segLen * 10f - Mathf.Abs(t - prefer);
            if (score > bestScore)
            {
                bestScore = score;
                bestSeg = new Vector2(usableMin, usableMax);
                along01 = t;
            }
        }

        if (bestScore > float.NegativeInfinity)
            return true;

        // No segment wide enough for the piece.
        return false;
    }

    private static void GetRoomEdgeSpan(
        RoomSpec room,
        RoomWallEdge edge,
        out float wallMin,
        out float wallMax)
    {
        if (edge == RoomWallEdge.West || edge == RoomWallEdge.East)
        {
            wallMin = room.Bounds.yMin;
            wallMax = room.Bounds.yMax;
        }
        else
        {
            wallMin = room.Bounds.xMin;
            wallMax = room.Bounds.xMax;
        }
    }

    private static List<Vector2> GetDoorIntervalsNormalized(
        LayoutSpec layout,
        RoomSpec room,
        RoomWallEdge edge)
    {
        List<Vector2> result = new List<Vector2>();
        GetRoomEdgeSpan(room, edge, out float wallMin, out float wallMax);
        float length = wallMax - wallMin;
        if (length < 0.01f)
            return result;

        float fixedCoord = edge switch
        {
            RoomWallEdge.West => room.Bounds.xMin,
            RoomWallEdge.East => room.Bounds.xMax,
            RoomWallEdge.South => room.Bounds.yMin,
            _ => room.Bounds.yMax
        };
        bool alongX = edge == RoomWallEdge.South || edge == RoomWallEdge.North;

        for (int i = 0; i < layout.Walls.Count; i++)
        {
            WallSpec wall = layout.Walls[i];
            if (wall.AlongX != alongX)
                continue;
            if (Mathf.Abs(wall.Fixed - fixedCoord) > 0.06f)
                continue;

            for (int o = 0; o < wall.Openings.Count; o++)
            {
                OpeningSpec opening = wall.Openings[o];
                if (opening.IsWindow)
                    continue;

                float min = (opening.Center - opening.Width * 0.5f - wallMin) / length;
                float max = (opening.Center + opening.Width * 0.5f - wallMin) / length;
                if (max < 0f || min > 1f)
                    continue;
                result.Add(new Vector2(Mathf.Clamp01(min), Mathf.Clamp01(max)));
            }
        }

        result.Sort((a, b) => a.x.CompareTo(b.x));
        return MergeIntervals(result);
    }

    private static List<Vector2> MergeIntervals(List<Vector2> intervals)
    {
        List<Vector2> merged = new List<Vector2>();
        for (int i = 0; i < intervals.Count; i++)
        {
            if (merged.Count == 0 || intervals[i].x > merged[merged.Count - 1].y)
                merged.Add(intervals[i]);
            else
            {
                Vector2 last = merged[merged.Count - 1];
                last.y = Mathf.Max(last.y, intervals[i].y);
                merged[merged.Count - 1] = last;
            }
        }

        return merged;
    }

    private static List<Vector2> BuildFreeSegments(List<Vector2> blocked)
    {
        List<Vector2> free = new List<Vector2>();
        float cursor = 0f;
        for (int i = 0; i < blocked.Count; i++)
        {
            if (blocked[i].x > cursor + 0.001f)
                free.Add(new Vector2(cursor, blocked[i].x));
            cursor = Mathf.Max(cursor, blocked[i].y);
        }

        if (cursor < 0.999f)
            free.Add(new Vector2(cursor, 1f));
        return free;
    }

    private static WallPlacement[] BuildWallPlacements(RoomSpec room)
    {
        bool windowsAllowed = !DisallowsWindows(room.Id);
        bool eastWindow = windowsAllowed && room.Bounds.xMax >= MaxX - 0.25f;
        bool northWindow = windowsAllowed && room.Bounds.yMax >= MaxZ - 0.25f;

        return new[]
        {
            MakeWallPlacement(room, RoomWallEdge.West, false, HasDoorOnEdge(room, RoomWallEdge.West)),
            MakeWallPlacement(room, RoomWallEdge.East, eastWindow, HasDoorOnEdge(room, RoomWallEdge.East)),
            MakeWallPlacement(room, RoomWallEdge.South, false, HasDoorOnEdge(room, RoomWallEdge.South)),
            MakeWallPlacement(room, RoomWallEdge.North, northWindow, HasDoorOnEdge(room, RoomWallEdge.North))
        };
    }

    private static WallPlacement MakeWallPlacement(
        RoomSpec room,
        RoomWallEdge edge,
        bool hasWindow,
        bool hasDoor)
    {
        GetWallPose(room, edge, 0.5f, WallFurnitureInset, out Vector2 back, out Vector2 front);
        float length = edge == RoomWallEdge.West || edge == RoomWallEdge.East
            ? room.Bounds.height
            : room.Bounds.width;
        float score = 100f;
        if (hasWindow)
            score -= 1000f;
        if (hasDoor)
            score -= 50f;
        return new WallPlacement
        {
            Edge = edge,
            Back = back,
            Front = front,
            Score = score,
            HasWindow = hasWindow,
            HasDoor = hasDoor,
            Length = length
        };
    }

    private static void GetWallPose(
        RoomSpec room,
        RoomWallEdge edge,
        float along01,
        float inset,
        out Vector2 back,
        out Vector2 frontDir)
    {
        // Room bounds edges are wall centerlines; place against the inner plaster face.
        float xMin = room.Bounds.xMin + HalfWallThickness;
        float xMax = room.Bounds.xMax - HalfWallThickness;
        float zMin = room.Bounds.yMin + HalfWallThickness;
        float zMax = room.Bounds.yMax - HalfWallThickness;
        float along = Mathf.Clamp01(along01);
        switch (edge)
        {
            case RoomWallEdge.West:
                back = new Vector2(xMin + inset, Mathf.Lerp(zMin, zMax, along));
                frontDir = new Vector2(1f, 0f);
                break;
            case RoomWallEdge.East:
                back = new Vector2(xMax - inset, Mathf.Lerp(zMin, zMax, along));
                frontDir = new Vector2(-1f, 0f);
                break;
            case RoomWallEdge.South:
                back = new Vector2(Mathf.Lerp(xMin, xMax, along), zMin + inset);
                frontDir = new Vector2(0f, 1f);
                break;
            default:
                back = new Vector2(Mathf.Lerp(xMin, xMax, along), zMax - inset);
                frontDir = new Vector2(0f, -1f);
                break;
        }
    }

    private static bool HasDoorOnEdge(RoomSpec room, RoomWallEdge edge)
    {
        if (room.DoorEdges == null)
            return false;
        string token = edge switch
        {
            RoomWallEdge.West => "W",
            RoomWallEdge.East => "E",
            RoomWallEdge.South => "S",
            _ => "N"
        };
        for (int i = 0; i < room.DoorEdges.Length; i++)
        {
            if (string.Equals(room.DoorEdges[i], token, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static RoomWallEdge? FindDoorWall(RoomSpec room)
    {
        if (room.DoorEdges == null || room.DoorEdges.Length == 0)
            return null;
        string token = room.DoorEdges[0].ToUpperInvariant();
        return token switch
        {
            "W" => RoomWallEdge.West,
            "E" => RoomWallEdge.East,
            "S" => RoomWallEdge.South,
            "N" => RoomWallEdge.North,
            _ => null
        };
    }

    private static RoomWallEdge Opposite(RoomWallEdge edge)
    {
        return edge switch
        {
            RoomWallEdge.West => RoomWallEdge.East,
            RoomWallEdge.East => RoomWallEdge.West,
            RoomWallEdge.South => RoomWallEdge.North,
            _ => RoomWallEdge.South
        };
    }
}
