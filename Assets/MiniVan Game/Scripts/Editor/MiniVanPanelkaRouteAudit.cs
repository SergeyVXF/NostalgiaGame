using System;
using System.Collections.Generic;
using System.Linq;
using MiniVanGame;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class MiniVanPanelkaRouteAudit
{
    private const string RebuildTriggerPath =
        "Library/CodexTools/RebuildAndAuditGameScene.flag";
    private const string ScenePath = "Assets/MiniVan Game/Scenes/Game_v01.unity";
    private const string OutlineFolder = "Assets/MiniVan Game/Resources/Panelka";
    private const string OutlineMaterialPath = OutlineFolder + "/ThinWhiteOutline.mat";

    static MiniVanPanelkaRouteAudit()
    {
        EditorApplication.update += RunTriggeredRebuild;
    }

    private static void RunTriggeredRebuild()
    {
        if (!System.IO.File.Exists(RebuildTriggerPath))
            return;

        System.IO.File.Delete(RebuildTriggerPath);
        RebuildAndAuditGameScene();
    }

    [MenuItem("MiniVan Game/Panelka/Rebuild And Audit Game_v01 Route")]
    public static void RebuildAndAuditGameScene()
    {
        EnsureOutlineMaterial();

        Scene previousActive = SceneManager.GetActiveScene();
        Scene gameScene = SceneManager.GetSceneByPath(ScenePath);
        bool openedForAudit = !gameScene.IsValid() || !gameScene.isLoaded;
        if (openedForAudit)
            gameScene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

        SceneManager.SetActiveScene(gameScene);
        MiniVanGameModeWorldGenerator world = FindInScene<MiniVanGameModeWorldGenerator>(gameScene);
        if (world == null)
            throw new InvalidOperationException("Game_v01 has no MiniVanGameModeWorldGenerator.");

        AssignStairwellMaterials(world);

        world.GenerateOnStart = false;
        if (world.MapGenerator != null)
        {
            world.MapGenerator.GenerateOnStart = false;
            world.MapGenerator.Rebuild();
        }
        world.Rebuild();
        EditorSceneManager.MarkSceneDirty(gameScene);
        EditorSceneManager.SaveScene(gameScene);
        AuditScene(gameScene, true);

        if (previousActive.IsValid() && previousActive.isLoaded)
            SceneManager.SetActiveScene(previousActive);
        if (openedForAudit)
            EditorSceneManager.CloseScene(gameScene, true);

        AssetDatabase.SaveAssets();
        Debug.Log("[Panelka Route Audit] Game_v01 rebuilt, audited and saved.");
    }

    private static void AssignStairwellMaterials(MiniVanGameModeWorldGenerator world)
    {
        const string root =
            "Assets/MiniVan Game/Materials/Panelka/Interior/LowPolyPack/";
        world.StairwellFloorMaterial = AssetDatabase.LoadAssetAtPath<Material>(
            root + "Stairwell_Floor_GrayTerrazzo_01.mat");
        Material twoToneWall = AssetDatabase.LoadAssetAtPath<Material>(
            root + "Stairwell_Wall_GreenWhite_01.mat");
        world.StairwellWallMaterial = twoToneWall;
        world.StairwellLowerWallMaterial = EnsureStairwellWallSlice(
            twoToneWall,
            root + "Stairwell_Wall_GreenLower_01.mat",
            false);
        world.StairwellUpperWallMaterial = EnsureStairwellWallSlice(
            twoToneWall,
            root + "Stairwell_Wall_WhiteUpper_01.mat",
            true);
        world.StairwellCeilingMaterial = world.StairwellWallMaterial;
        world.StairwellDoorMaterial = AssetDatabase.LoadAssetAtPath<Material>(
            root + "Door_GrayMetal_04.mat");
        world.CrackedWindowMaterial = AssetDatabase.LoadAssetAtPath<Material>(
            "Assets/MiniVan Game/Materials/Panelka/Stage1/" +
            "PanelkaStage1_WindowGlassCrackedGenerated.mat");
        EditorUtility.SetDirty(world);
    }

    private static Material EnsureStairwellWallSlice(
        Material source,
        string path,
        bool upper)
    {
        if (source == null)
            return null;
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(source);
            AssetDatabase.CreateAsset(material, path);
        }
        else
        {
            material.CopyPropertiesFromMaterial(source);
        }
        material.name = System.IO.Path.GetFileNameWithoutExtension(path);
        Vector2 scale = new Vector2(1f, 0.5f);
        Vector2 offset = new Vector2(0f, upper ? 0.5f : 0f);
        if (material.HasProperty("_BaseMap"))
        {
            material.SetTextureScale("_BaseMap", scale);
            material.SetTextureOffset("_BaseMap", offset);
        }
        if (material.HasProperty("_MainTex"))
        {
            material.SetTextureScale("_MainTex", scale);
            material.SetTextureOffset("_MainTex", offset);
        }
        EditorUtility.SetDirty(material);
        return material;
    }

    [MenuItem("MiniVan Game/Panelka/Audit Loaded Game_v01 Route")]
    public static void AuditLoadedGameScene()
    {
        AuditScene(SceneManager.GetActiveScene(), true);
    }

    [MenuItem("MiniVan Game/Panelka/Audit Saved Game_v01 Route")]
    public static void AuditSavedGameScene()
    {
        Scene gameScene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        SceneManager.SetActiveScene(gameScene);
        AuditScene(gameScene, true);
    }

    internal static void AuditScene(Scene scene, bool throwOnFailure)
    {
        List<string> failures = new List<string>();
        MiniVanPanelkaApartmentRouteMarker[] apartments =
            FindAllInScene<MiniVanPanelkaApartmentRouteMarker>(scene);
        HashSet<string> routeDoorKeys = new HashSet<string>(StringComparer.Ordinal);
        HashSet<string> pickupKeys = new HashSet<string>(StringComparer.Ordinal);
        int routeApartments = 0;
        int furnishedApartments = 0;
        HashSet<string> layoutTemplateIds = new HashSet<string>(StringComparer.Ordinal);

        for (int i = 0; i < apartments.Length; i++)
        {
            MiniVanPanelkaApartmentRouteMarker apartment = apartments[i];
            MiniVanPanelkaApartmentTemplate fullTemplate =
                apartment.GetComponentInChildren<MiniVanPanelkaApartmentTemplate>(true);
            if (fullTemplate != null)
            {
                if (apartment.RequiresVisit)
                {
                    routeApartments++;
                    furnishedApartments++;
                    AuditFullApartmentTemplate(apartment, fullTemplate, failures);
                    if (string.IsNullOrEmpty(fullTemplate.TemplateId))
                        failures.Add(apartment.name + " has no full apartment prefab id.");
                    else
                        layoutTemplateIds.Add(fullTemplate.TemplateId);
                }
                else
                {
                    AuditExteriorOnlyApartmentTemplate(apartment, fullTemplate, failures);
                }
            }
            else
            {
                if (apartment.RequiresVisit)
                    routeApartments++;
                failures.Add(apartment.name + " was not generated from a full apartment prefab.");
            }

            MiniVanApartmentKeyPickup[] pickups =
                apartment.GetComponentsInChildren<MiniVanApartmentKeyPickup>(true);
            for (int j = 0; j < pickups.Length; j++)
            {
                if (!string.IsNullOrEmpty(pickups[j].KeyId))
                    pickupKeys.Add(pickups[j].KeyId);
            }

            MiniVanApartmentDoorLock[] locks =
                apartment.GetComponentsInChildren<MiniVanApartmentDoorLock>(true);
            for (int j = 0; j < locks.Length; j++)
            {
                if (apartment.Role == MiniVanPanelkaApartmentRouteRole.MainRoute &&
                    locks[j].IsLocked &&
                    !string.IsNullOrEmpty(locks[j].RequiredKeyId) &&
                    locks[j].name.IndexOf(
                        "Apartment_Entrance_Door",
                        StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    routeDoorKeys.Add(locks[j].RequiredKeyId);
                }
            }
        }

        foreach (string keyId in pickupKeys)
            if (!routeDoorKeys.Contains(keyId))
                failures.Add("Key has no matching route door: " + keyId);
        foreach (string keyId in routeDoorKeys)
            if (!pickupKeys.Contains(keyId))
                failures.Add("Locked route door has no matching key: " + keyId);
        if (layoutTemplateIds.Count < 5)
            failures.Add("Only " + layoutTemplateIds.Count + " of " +
                         5 + " full apartment prefabs were generated.");

        AuditFullApartmentCatalog(failures);
        AuditStairwellFinishes(scene, failures);
        AuditPrefabOnlyAssembly(scene, failures);
        AuditCrossEntranceRoutes(scene, failures);

        GameObject[] roots = scene.GetRootGameObjects();
        int missingScripts = 0;
        for (int i = 0; i < roots.Length; i++)
        {
            Transform[] transforms = roots[i].GetComponentsInChildren<Transform>(true);
            for (int j = 0; j < transforms.Length; j++)
                missingScripts += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(
                    transforms[j].gameObject);
        }
        if (missingScripts > 0)
            failures.Add("Missing scripts in generated scene: " + missingScripts);

        string summary = "route apartments=" + routeApartments +
                         ", furnished=" + furnishedApartments +
                         ", route keys=" + pickupKeys.Count +
                         ", locked route doors=" + routeDoorKeys.Count;
        if (failures.Count == 0)
        {
            Debug.Log("[Panelka Route Audit] PASS: " + summary);
            return;
        }

        string message = "[Panelka Route Audit] FAIL: " + summary + "\n" +
                         string.Join("\n", failures);
        Debug.LogError(message);
        if (throwOnFailure)
            throw new InvalidOperationException(message);
    }

    private static void AuditFullApartmentTemplate(
        MiniVanPanelkaApartmentRouteMarker apartment,
        MiniVanPanelkaApartmentTemplate template,
        List<string> failures)
    {
        if (template.TemplateIndex < 1 || template.TemplateIndex > 5)
            failures.Add(apartment.name + " has an invalid full apartment template index.");
        if (template.ContentRoot == null || !template.ContentRoot.gameObject.activeInHierarchy)
            failures.Add(apartment.name + " has no active editable apartment content root.");
        if (template.EntrySocket == null || template.RouteHoleSocket == null ||
            template.BalconySocket == null || template.PipeSocket == null ||
            template.KeySocket == null)
        {
            failures.Add(apartment.name + " has incomplete route sockets.");
        }

        MiniVanApartmentDoor entrance = template
            .GetComponentsInChildren<MiniVanApartmentDoor>(true)
            .FirstOrDefault(door => door.name == "Apartment_Entrance_Door");
        if (entrance == null)
            failures.Add(apartment.name + " has no prefab apartment entrance door.");
        MiniVanApartmentDoor[] prefabDoors =
            template.GetComponentsInChildren<MiniVanApartmentDoor>(true);
        for (int doorIndex = 0; doorIndex < prefabDoors.Length; doorIndex++)
        {
            MiniVanApartmentDoor door = prefabDoors[doorIndex];
            Renderer[] panels = door.Pivot != null
                ? door.Pivot.GetComponentsInChildren<Renderer>(true)
                    .Where(renderer => renderer.name == "Door_Panel")
                    .ToArray()
                : Array.Empty<Renderer>();
            if (panels.Length != 1)
            {
                failures.Add(
                    apartment.name + "/" + door.name +
                    " must contain exactly one prefab door panel.");
            }

            string frameName = door.name == "Apartment_Entrance_Door"
                ? "Door_Frame_Entrance"
                : door.name.StartsWith(
                    "Interior_Door_",
                    StringComparison.Ordinal)
                    ? "Door_Frame_" +
                      door.name.Substring("Interior_Door_".Length)
                    : string.Empty;
            Transform frame = !string.IsNullOrEmpty(frameName) &&
                              door.transform.parent != null
                ? door.transform.parent.Find(frameName)
                : null;
            if (frame == null ||
                frame.GetComponentsInChildren<Renderer>(true).Length == 0)
            {
                failures.Add(
                    apartment.name + "/" + door.name +
                    " has no visible prefab door frame.");
            }
        }
        if (template.GetComponentsInChildren<MiniVanPanelkaRoomDoor>(true).Length > 0)
            failures.Add(apartment.name + " still contains legacy room doors.");
        if (template.GetComponentsInChildren<MiniVanPanelkaDoorCollisionProxy>(true).Length > 0)
            failures.Add(apartment.name + " still contains door collision proxies.");
        MiniVanPanelkaRoomIdentity[] rooms =
            template.GetComponentsInChildren<MiniVanPanelkaRoomIdentity>(true);
        if (rooms.Length < 4)
            failures.Add(apartment.name + " does not contain a complete apartment interior.");
        Transform[] explicitCeilings = template.GetComponentsInChildren<Transform>(true)
            .Where(item => item.name == "EXPLICIT_CEILING")
            .ToArray();
        if (explicitCeilings.Length == 0 ||
            !explicitCeilings.Any(item =>
                item.GetComponentsInChildren<Renderer>(true).Length > 0))
            failures.Add(apartment.name + " has incomplete explicit ceilings.");
        if (template.GetComponentsInChildren<MiniVanPanelkaCabinet>(true).Length == 0)
            failures.Add(apartment.name + " has no interactive cabinets in its full prefab.");
        if (template.GetComponentsInChildren<MiniVanPanelkaInteractable>(true).Length == 0)
            failures.Add(apartment.name + " has no interactable furniture in its full prefab.");

        string[] legacyComponentNames =
        {
            "MiniVanPanelkaApartmentLayoutShell",
            "MiniVanPanelkaRoomTemplate",
            "MiniVanPanelkaRoomTemplateMarker",
            "MiniVanPanelkaRoomTemplateInstance"
        };
        if (template.GetComponentsInChildren<MonoBehaviour>(true).Any(component =>
                component != null && legacyComponentNames.Contains(
                    component.GetType().Name, StringComparer.Ordinal)))
        {
            failures.Add(apartment.name + " still contains legacy procedural-room components.");
        }

        if (PrefabUtility.GetCorrespondingObjectFromSource(template.gameObject) == null)
            failures.Add(apartment.name + " is not a connected full apartment prefab instance.");
    }

    private static void AuditExteriorOnlyApartmentTemplate(
        MiniVanPanelkaApartmentRouteMarker apartment,
        MiniVanPanelkaApartmentTemplate template,
        List<string> failures)
    {
        Transform entrance = template.GetComponentsInChildren<Transform>(true)
            .FirstOrDefault(item => item.name == "Apartment_Entrance_Door");
        if (entrance == null || !entrance.gameObject.activeInHierarchy)
        {
            failures.Add(apartment.name + " has no active sealed entrance door.");
        }
        else
        {
            bool hasOpenableDoor = entrance.GetComponentsInChildren<MiniVanApartmentDoor>(true)
                .Any(door => door.gameObject.activeInHierarchy);
            if (hasOpenableDoor)
                failures.Add(apartment.name + " is inaccessible but has an openable entrance door.");

            Transform panel = entrance.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(item => item.name == "Door_Panel" &&
                                        item.gameObject.activeInHierarchy);
            Collider panelCollider = panel != null ? panel.GetComponent<Collider>() : null;
            if (panel == null || panelCollider == null ||
                !panelCollider.enabled || panelCollider.isTrigger)
            {
                failures.Add(apartment.name + " sealed entrance door has no blocking collider.");
            }
        }

        if (template.GetComponentsInChildren<MiniVanPanelkaCabinet>(true).Length > 0)
            failures.Add(apartment.name + " is inaccessible but still has cabinet content.");
        if (template.GetComponentsInChildren<MiniVanPanelkaRoomIdentity>(true).Length > 0)
            failures.Add(apartment.name + " is inaccessible but still has room content.");
        if (template.GetComponentsInChildren<MiniVanPanelkaInteractable>(true).Length > 0)
            failures.Add(apartment.name + " is inaccessible but still has interactable content.");
        if (template.GetComponentsInChildren<MiniVanPanelkaRoomDoor>(true).Length > 0)
            failures.Add(apartment.name + " exterior prefab contains legacy room doors.");
        if (template.GetComponentsInChildren<MiniVanPanelkaDoorCollisionProxy>(true).Length > 0)
            failures.Add(apartment.name + " exterior prefab contains door collision proxies.");

        bool hasFacade = template.GetComponentsInChildren<Renderer>(true).Any(renderer =>
            renderer.gameObject.activeInHierarchy &&
            renderer.name.StartsWith("FacadeWall_", StringComparison.Ordinal));
        if (!hasFacade)
            failures.Add(apartment.name + " lost its exterior facade.");
    }

    private static bool HasNamedAncestor(Transform transform, string token)
    {
        while (transform != null)
        {
            if (transform.name.IndexOf(token, StringComparison.Ordinal) >= 0)
                return true;
            transform = transform.parent;
        }

        return false;
    }

    private static void AuditFullApartmentCatalog(List<string> failures)
    {
        MiniVanPanelkaApartmentTemplateCatalog catalog =
            Resources.Load<MiniVanPanelkaApartmentTemplateCatalog>(
                "Panelka/ApartmentTemplateCatalog");
        if (catalog == null)
        {
            failures.Add("Full apartment prefab catalog is missing.");
            return;
        }
        if (catalog.Count != 5)
            failures.Add("Full apartment prefab catalog must contain exactly five entries.");
        if (catalog.ExteriorOnlyPrefab == null)
        {
            failures.Add("ApartmentExteriorOnly prefab is missing from the catalog.");
        }
        else
        {
            GameObject exterior = catalog.ExteriorOnlyPrefab;
            if (exterior.GetComponentsInChildren<MiniVanPanelkaRoomIdentity>(true).Length != 0)
                failures.Add("ApartmentExteriorOnly contains room content.");
            if (exterior.GetComponentsInChildren<MiniVanApartmentDoor>(true).Length != 0)
                failures.Add("ApartmentExteriorOnly contains an openable door.");
        }

        HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
        for (int templateIndex = 1; templateIndex <= 5; templateIndex++)
        {
            GameObject prefab = catalog.GetPrefab(templateIndex);
            if (prefab == null)
            {
                failures.Add("Missing full apartment prefab " + templateIndex + ".");
                continue;
            }

            string path = AssetDatabase.GetAssetPath(prefab);
            if (string.IsNullOrEmpty(path) ||
                path.IndexOf("/ApartmentTemplates/", StringComparison.Ordinal) < 0)
                failures.Add(prefab.name + " is outside the apartment template prefab folder.");

            MiniVanPanelkaApartmentTemplate template =
                prefab.GetComponent<MiniVanPanelkaApartmentTemplate>();
            if (template == null)
            {
                failures.Add(prefab.name + " has no apartment template metadata.");
                continue;
            }
            if (template.TemplateIndex != templateIndex)
                failures.Add(prefab.name + " has the wrong template index.");
            if (string.IsNullOrEmpty(template.TemplateId) || !ids.Add(template.TemplateId))
                failures.Add(prefab.name + " has a missing or duplicate template id.");

            MiniVanPanelkaApartmentFacadeMarker[] windows =
                prefab.GetComponentsInChildren<MiniVanPanelkaApartmentFacadeMarker>(true);
            if (windows.Length < 2)
                failures.Add(prefab.name + " has no complete exterior window set.");
            for (int windowIndex = 0; windowIndex < windows.Length; windowIndex++)
            {
                MiniVanPanelkaApartmentFacadeMarker window = windows[windowIndex];
                Renderer windowRenderer =
                    window.GetComponentsInChildren<Renderer>(true).FirstOrDefault();
                Vector3 local = windowRenderer != null
                    ? windowRenderer.transform.localPosition
                    : window.transform.localPosition;
                bool valid = window.Side == MiniVanPanelkaApartmentFacadeSide.PositiveX
                    ? local.x > 4f
                    : local.z > 4f;
                if (!valid)
                    failures.Add(prefab.name + " has a window on a non-exterior side: " +
                                 window.name + ".");
            }
        }
    }


    private static void AuditStairwellFinishes(Scene scene, List<string> failures)
    {
        MiniVanPanelkaStage1Generator[] generators =
            FindAllInScene<MiniVanPanelkaStage1Generator>(scene);
        for (int i = 0; i < generators.Length; i++)
        {
            MiniVanPanelkaStage1Generator generator = generators[i];
            if (generator.ExteriorOnlyLocked)
                continue;
            if (generator.StairwellFloorMaterial == null ||
                generator.StairwellWallMaterial == null ||
                generator.StairwellLowerWallMaterial == null ||
                generator.StairwellUpperWallMaterial == null ||
                generator.StairwellCeilingMaterial == null ||
                generator.StairwellDoorMaterial == null)
                failures.Add(generator.name + " has incomplete stairwell materials.");
            Renderer[] stairwellRenderers =
                generator.GetComponentsInChildren<Renderer>(true);
            Renderer[] lowerBands = stairwellRenderers.Where(renderer =>
                renderer.name.EndsWith("Green_Lower", StringComparison.Ordinal)).ToArray();
            Renderer[] upperBands = stairwellRenderers.Where(renderer =>
                renderer.name.EndsWith("White_Upper", StringComparison.Ordinal)).ToArray();
            if (lowerBands.Length == 0 || upperBands.Length == 0)
            {
                failures.Add(generator.name +
                             " has no physically split green/white stairwell wall bands.");
            }
            else
            {
                if (lowerBands.Any(renderer =>
                        renderer.sharedMaterial != generator.StairwellLowerWallMaterial))
                    failures.Add(generator.name +
                                 " has a lower stairwell band with the wrong material.");
                if (upperBands.Any(renderer =>
                        renderer.sharedMaterial != generator.StairwellUpperWallMaterial))
                    failures.Add(generator.name +
                                 " has an upper stairwell band with the wrong material.");
            }
            if (generator.transform.Find(
                    "Generated_9_Floor_Building/Floor_01_Layout_1/Structure/Landing_Ceiling") == null &&
                stairwellRenderers.All(renderer =>
                    !renderer.name.StartsWith("Landing_Ceiling", StringComparison.Ordinal)))
                failures.Add(generator.name + " has no generated stairwell ceiling finish.");
        }
    }

    private static void AuditPrefabOnlyAssembly(Scene scene, List<string> failures)
    {
        MiniVanPanelkaStage1Generator[] generators =
            FindAllInScene<MiniVanPanelkaStage1Generator>(scene);
        for (int generatorIndex = 0; generatorIndex < generators.Length; generatorIndex++)
        {
            MiniVanPanelkaStage1Generator generator = generators[generatorIndex];
            AuditFacadeOcclusion(generator, failures);
            if (generator.ExteriorOnlyLocked)
            {
                AuditLockedExterior(generator, failures);
                continue;
            }

            Transform[] all = generator.GetComponentsInChildren<Transform>(true);
            if (all.Any(item =>
                    item.name == "Apartment_West_SharedWall" ||
                    item.name == "Apartment_East_SharedWall"))
            {
                failures.Add(generator.name +
                             " still contains walls from the old apartment assembler.");
            }

            Transform[] routeHoles = all.Where(item =>
                    item.name.StartsWith(
                        "Route_Template_Floor_Hole_",
                        StringComparison.Ordinal))
                .ToArray();
            int holeRopes = all.Count(item =>
                item.name.StartsWith(
                    "Route_Return_Rope_",
                    StringComparison.Ordinal));
            int balconyRopeCount = all.Count(item =>
                item.name.StartsWith(
                    "Route_Balcony_Hatch_Rope_From_",
                    StringComparison.Ordinal));
            int routePipeCount = all.Count(item =>
                item.name.StartsWith(
                    "Route_Pipe_From_Floor_",
                    StringComparison.Ordinal));
            int stairTransitionCount = all.Count(item =>
                item.name.StartsWith(
                    "Route_Stair_Descent_From_Floor_",
                    StringComparison.Ordinal));
            if (generator.FloorCount > 1)
            {
                int stairBlockages = all.Count(item =>
                    item.name.StartsWith(
                        "Route_Stair_Blockage_Between_Floor_",
                        StringComparison.Ordinal));
                if (routeHoles.Length != holeRopes)
                {
                    failures.Add(generator.name +
                                 " has a floor-hole transition without its return rope.");
                }
                if (routeHoles.Length +
                    balconyRopeCount +
                    routePipeCount +
                    stairTransitionCount != generator.FloorCount - 1)
                {
                    failures.Add(generator.name +
                                 " does not cover every floor boundary with exactly " +
                                 "one route transition.");
                }
                if (stairBlockages !=
                    generator.FloorCount - 1 - stairTransitionCount)
                {
                    failures.Add(generator.name +
                                 " has an incorrect number of blocked stair boundaries.");
                }
                for (int holeIndex = 0;
                     holeIndex < routeHoles.Length;
                     holeIndex++)
                {
                    AuditRouteHoleTarget(
                        generator,
                        routeHoles[holeIndex],
                        failures);
                }
                AuditRouteSequence(generator, failures);
            }

            if (balconyRopeCount > 0)
            {
                int routeBalconies = all.Count(item =>
                    item.name.StartsWith(
                        "Route_Balcony_",
                        StringComparison.Ordinal) &&
                    !item.name.StartsWith(
                        "Route_Balcony_Hatch_",
                        StringComparison.Ordinal) &&
                    !item.name.StartsWith(
                        "Route_Balcony_Transfer_",
                        StringComparison.Ordinal));
                int balconyTransferPipes = all.Count(item =>
                    item.name.StartsWith(
                        "Route_Balcony_Transfer_Pipe_From_",
                        StringComparison.Ordinal));
                MiniVanPanelkaBreakableWindow[] balconyWindows =
                    generator.GetComponentsInChildren<MiniVanPanelkaBreakableWindow>(
                            true)
                        .Where(window =>
                            window.WindowId != null &&
                            window.WindowId.StartsWith(
                                "BALCONY_WINDOW_",
                                StringComparison.Ordinal))
                        .ToArray();
                Transform[] balconyRopes = all.Where(item =>
                        item.name.StartsWith(
                            "Route_Balcony_Hatch_Rope_From_",
                            StringComparison.Ordinal))
                    .ToArray();

                if (routeBalconies != balconyRopeCount * 2)
                    failures.Add(generator.name +
                                 " must contain a departure and arrival balcony " +
                                 "for every balcony transition.");
                if (balconyTransferPipes != balconyRopeCount ||
                    balconyWindows.Length != balconyRopeCount * 2)
                {
                    failures.Add(generator.name +
                                 " must contain one transfer pipe and two cracked " +
                                 "windows per balcony transition.");
                }
                if (balconyRopes.Any(rope =>
                        Mathf.Abs(rope.localPosition.x - 1.10f) > 0.04f ||
                        Mathf.Abs(rope.localPosition.z - 0.95f) > 0.04f ||
                        Quaternion.Angle(
                            rope.localRotation,
                            Quaternion.identity) > 0.5f))
                {
                    failures.Add(generator.name +
                                 " does not preserve the approved balcony-rope pose.");
                }
                if (balconyRopes.Any(rope =>
                {
                    BoxCollider trigger = rope.GetComponent<BoxCollider>();
                    BoxCollider physical = rope
                        .GetComponentsInChildren<BoxCollider>(true)
                        .FirstOrDefault(collider =>
                            collider.name == "Rope_Physical_Collider");
                    return trigger == null ||
                           !trigger.isTrigger ||
                           trigger.size.x < 1.79f ||
                           trigger.size.y < 5.4f ||
                           physical == null ||
                           physical.isTrigger ||
                           physical.size.x > 0.17f;
                }))
                {
                    failures.Add(generator.name +
                                 " has incomplete balcony-rope climb colliders.");
                }
                Transform[] hatchLids = all.Where(item =>
                        item.name == "Balcony_Hatch_Open_Lid")
                    .ToArray();
                if (hatchLids.Length != balconyRopeCount ||
                    hatchLids.Any(hatchLid =>
                        Mathf.Abs(hatchLid.localScale.x - 1.38f) > 0.02f ||
                        Mathf.Abs(hatchLid.localScale.z - 1.32f) > 0.02f))
                {
                    failures.Add(generator.name +
                                 " does not contain the enlarged balcony hatch.");
                }
                if (generator.CrackedGlassMaterial == null ||
                    balconyWindows.Any(window =>
                    {
                        Renderer glass = window.GetComponent<Renderer>();
                        return glass == null ||
                               glass.sharedMaterial != generator.CrackedGlassMaterial;
                    }) ||
                    (generator.CrackedGlassMaterial.HasProperty("_Cull") &&
                     generator.CrackedGlassMaterial.GetFloat("_Cull") > 0.01f))
                {
                    failures.Add(generator.name +
                                 " has a balcony window without two-sided cracked glass.");
                }
            }

            MiniVanPanelkaBreakableWindow[] routeWindows =
                generator.GetComponentsInChildren<
                        MiniVanPanelkaBreakableWindow>(true)
                    .Where(window =>
                        window.WindowId != null &&
                        (window.WindowId.StartsWith(
                             "BALCONY_WINDOW_",
                             StringComparison.Ordinal) ||
                         window.WindowId.StartsWith(
                             "PIPE_WINDOW_",
                             StringComparison.Ordinal) ||
                         window.WindowId.StartsWith(
                             "CROSS_PIPE_",
                             StringComparison.Ordinal)))
                    .ToArray();
            if (routeWindows.Any(window =>
            {
                Transform proxy =
                    window.transform.Find("Breakable_Window_Hit_Proxy");
                BoxCollider collider =
                    proxy != null ? proxy.GetComponent<BoxCollider>() : null;
                return collider == null ||
                       !collider.enabled ||
                       collider.isTrigger ||
                       collider.bounds.size.y < 0.69f ||
                       Mathf.Min(
                           collider.bounds.size.x,
                           collider.bounds.size.z) < 0.34f;
            }))
            {
                failures.Add(generator.name +
                             " has a route window without an exterior hit proxy.");
            }

            if (routePipeCount > 0)
            {
                Transform[] routePipes = all.Where(item =>
                        item.name.StartsWith(
                            "Route_Pipe_From_Floor_",
                            StringComparison.Ordinal))
                    .ToArray();
                int crossDepartureRoutes = routePipes.Count(item =>
                    item.name.IndexOf(
                        "CrossEntrance",
                        StringComparison.Ordinal) >= 0);
                int internalPipeRoutes =
                    routePipes.Length - crossDepartureRoutes;
                MiniVanPanelkaBreakableWindow[] pipeWindows =
                    generator.GetComponentsInChildren<MiniVanPanelkaBreakableWindow>(
                            true)
                        .Where(window =>
                            window.WindowId != null &&
                            window.WindowId.StartsWith(
                                "PIPE_WINDOW_",
                                StringComparison.Ordinal))
                        .ToArray();
                int crossDepartureWindows = generator
                    .GetComponentsInChildren<
                        MiniVanPanelkaBreakableWindow>(true)
                    .Count(window =>
                        window.WindowId != null &&
                        window.WindowId.StartsWith(
                            "CROSS_PIPE_DEPARTURE_",
                            StringComparison.Ordinal));
                if (pipeWindows.Length != internalPipeRoutes * 2 ||
                    crossDepartureWindows != crossDepartureRoutes)
                {
                    failures.Add(generator.name +
                                 " has incomplete internal or cross-entrance pipe windows.");
                }
            }

            MiniVanPanelkaApartmentFacadeMarker[] windows =
                generator.GetComponentsInChildren<MiniVanPanelkaApartmentFacadeMarker>(true);
            HashSet<GameObject> markedWindowRoots = new HashSet<GameObject>();
            for (int windowIndex = 0; windowIndex < windows.Length; windowIndex++)
            {
                MiniVanPanelkaApartmentFacadeMarker marker = windows[windowIndex];
                if (!markedWindowRoots.Add(marker.gameObject))
                {
                    failures.Add(generator.name + " has duplicate facade markers on " +
                                 marker.name + ".");
                    continue;
                }

                Renderer glass = marker.GetComponentsInChildren<Renderer>(true)
                    .FirstOrDefault(renderer => renderer.name == "Breakable_Glass");
                if (glass == null || !glass.gameObject.activeInHierarchy)
                    continue;
                Vector3 local = generator.transform.InverseTransformPoint(glass.bounds.center);
                bool onExteriorX = Mathf.Abs(Mathf.Abs(local.x) - 13f) <= 0.32f;
                bool onExteriorZ = Mathf.Abs(Mathf.Abs(local.z) - 9f) <= 0.32f;
                if (!onExteriorX && !onExteriorZ)
                {
                    failures.Add(generator.name + " has an inward-facing window at " +
                                 local + " (" + marker.name + ").");
                }
            }

            Renderer[] renderers = generator.GetComponentsInChildren<Renderer>(true);
            Renderer[] stairwellWalls = renderers.Where(renderer =>
                    renderer.gameObject.activeInHierarchy &&
                    (renderer.name.StartsWith("Landing_WestWall", StringComparison.Ordinal) ||
                     renderer.name.StartsWith("Landing_EastWall", StringComparison.Ordinal)))
                .ToArray();
            MiniVanApartmentDoor[] entrances =
                generator.GetComponentsInChildren<MiniVanApartmentDoor>(true)
                    .Where(door => door.name == "Apartment_Entrance_Door" &&
                                   door.gameObject.activeInHierarchy)
                    .ToArray();
            for (int entranceIndex = 0; entranceIndex < entrances.Length; entranceIndex++)
            {
                Renderer panel = entrances[entranceIndex]
                    .GetComponentsInChildren<Renderer>(true)
                    .FirstOrDefault(renderer => renderer.name == "Door_Panel");
                if (panel == null)
                    continue;
                if (stairwellWalls.Any(wall =>
                        AreCoplanarWallRenderers(wall, panel) &&
                        RendererBoundsIntersect(panel, wall, 0.12f)))
                {
                    failures.Add(generator.name + " has a stairwell wall crossing " +
                                 entrances[entranceIndex].transform.parent.name + ".");
                }
            }

            if (all.Any(item => item.name == "Baked_Static_Geometry"))
                failures.Add(generator.name + " still contains baked apartment geometry.");

            Renderer[] topCeilings = renderers.Where(renderer =>
                    renderer.gameObject.activeInHierarchy &&
                    renderer.name.StartsWith("Landing_Ceiling", StringComparison.Ordinal))
                .ToArray();
            Transform roofHatch = all.FirstOrDefault(item => item.name == "Roof_Hatch");
            float roofHatchY = roofHatch != null
                ? generator.transform.InverseTransformPoint(roofHatch.position).y
                : float.PositiveInfinity;
            for (int ceilingIndex = 0; ceilingIndex < topCeilings.Length; ceilingIndex++)
            {
                Bounds local = TransformBounds(
                    topCeilings[ceilingIndex].localBounds,
                    generator.transform.worldToLocalMatrix *
                    topCeilings[ceilingIndex].transform.localToWorldMatrix);
                bool atTop = Mathf.Abs(local.center.y - roofHatchY) <= 0.45f;
                bool crossesHatch =
                    local.min.x < 0.72f && local.max.x > -0.72f &&
                    local.min.z < 6.72f && local.max.z > 5.28f;
                if (atTop && crossesHatch)
                {
                    failures.Add(generator.name + " has ceiling " +
                                 topCeilings[ceilingIndex].name +
                                 " crossing the roof hatch.");
                }
            }
        }
    }

    private static void AuditRouteHoleTarget(
        MiniVanPanelkaStage1Generator generator,
        Transform hole,
        List<string> failures)
    {
        MiniVanPanelkaApartmentRouteMarker upper =
            hole.GetComponentInParent<MiniVanPanelkaApartmentRouteMarker>(true);
        if (upper == null)
        {
            failures.Add(generator.name +
                         " has a floor hole outside a route apartment.");
            return;
        }

        MiniVanPanelkaApartmentRouteMarker lower = generator
            .GetComponentsInChildren<MiniVanPanelkaApartmentRouteMarker>(true)
            .FirstOrDefault(apartment =>
                apartment.FloorNumber == upper.FloorNumber - 1 &&
                apartment.ApartmentSlot == upper.ApartmentSlot);
        MiniVanPanelkaApartmentTemplate lowerTemplate = lower != null
            ? lower.GetComponentInChildren<MiniVanPanelkaApartmentTemplate>(true)
            : null;
        MiniVanPanelkaApartmentTemplate upperTemplate =
            upper.GetComponentInChildren<MiniVanPanelkaApartmentTemplate>(true);
        bool hasFullInterior =
            lower != null &&
            lower.Role != MiniVanPanelkaApartmentRouteRole.Inaccessible &&
            lowerTemplate != null &&
            lowerTemplate.ContentRoot != null &&
            lowerTemplate.ContentRoot.gameObject.activeInHierarchy &&
            lowerTemplate.GetComponentsInChildren<MiniVanPanelkaRoomIdentity>(true)
                .Length >= 4;
        if (!hasFullInterior)
        {
            failures.Add(generator.name + " has floor hole in " +
                         upper.name +
                         " leading to an exterior-only apartment.");
            return;
        }

        Transform clearance =
            hole.Find("Route_Hole_Clearance_Volume");
        if (clearance == null ||
            upperTemplate == null)
        {
            failures.Add(generator.name +
                         " has no floor-hole clearance marker.");
            return;
        }

        Bounds holeBounds = TransformBounds(
            new Bounds(Vector3.zero, Vector3.one),
            generator.transform.worldToLocalMatrix *
            clearance.localToWorldMatrix);
        if (!IsHoleInsideRoomWithClearance(
                generator,
                upperTemplate,
                holeBounds) ||
            !IsHoleInsideRoomWithClearance(
                generator,
                lowerTemplate,
                holeBounds))
        {
            failures.Add(generator.name +
                         " has a floor hole crossing a room wall.");
        }

        int ceilingSlabs = lower
            .GetComponentsInChildren<Transform>(true)
            .Count(item => item.name.StartsWith(
                "RouteCeiling_",
                StringComparison.Ordinal));
        bool ceilingCoversOpening = lowerTemplate
            .GetComponentsInChildren<Transform>(true)
            .Where(item =>
                item.name == "EXPLICIT_CEILING" &&
                item.gameObject.activeInHierarchy)
            .SelectMany(item =>
                item.GetComponentsInChildren<Renderer>(true))
            .Any(renderer =>
            {
                Bounds bounds = TransformBounds(
                    renderer.localBounds,
                    generator.transform.worldToLocalMatrix *
                    renderer.transform.localToWorldMatrix);
                return holeBounds.center.x >= bounds.min.x &&
                       holeBounds.center.x <= bounds.max.x &&
                       holeBounds.center.z >= bounds.min.z &&
                       holeBounds.center.z <= bounds.max.z;
            });
        if (ceilingSlabs < 4 || ceilingCoversOpening)
        {
            failures.Add(generator.name +
                         " has no matching ceiling opening below its floor hole.");
        }
    }

    private static void AuditRouteSequence(
        MiniVanPanelkaStage1Generator generator,
        List<string> failures)
    {
        Transform[] all =
            generator.GetComponentsInChildren<Transform>(true);
        Dictionary<int, string> transitions =
            new Dictionary<int, string>();
        for (int i = 0; i < all.Length; i++)
        {
            string type = null;
            string token = null;
            if (all[i].name.StartsWith(
                    "Route_Template_Floor_Hole_From_",
                    StringComparison.Ordinal))
            {
                type = "Hole";
                token = "From_";
            }
            else if (all[i].name.StartsWith(
                         "Route_Balcony_Hatch_Rope_From_",
                         StringComparison.Ordinal))
            {
                type = "Balcony";
                token = "From_";
            }
            else if (all[i].name.StartsWith(
                         "Route_Pipe_From_Floor_",
                         StringComparison.Ordinal))
            {
                type = "Pipe";
                token = "From_Floor_";
            }
            else if (all[i].name.StartsWith(
                         "Route_Stair_Descent_From_Floor_",
                         StringComparison.Ordinal))
            {
                type = "Stair";
                token = "From_Floor_";
            }

            if (type == null ||
                !TryReadFloorNumber(all[i].name, token, out int upperFloor))
            {
                continue;
            }

            if (transitions.ContainsKey(upperFloor))
            {
                failures.Add(generator.name +
                             " has multiple route transitions from floor " +
                             upperFloor + ".");
                return;
            }
            transitions.Add(upperFloor, type);
        }

        string previous = null;
        int repeated = 0;
        for (int upperFloor = generator.FloorCount;
             upperFloor >= 2;
             upperFloor--)
        {
            if (!transitions.TryGetValue(upperFloor, out string current))
            {
                failures.Add(generator.name +
                             " has no route transition from floor " +
                             upperFloor + ".");
                continue;
            }
            repeated = current == previous ? repeated + 1 : 1;
            if (repeated >= 3)
            {
                failures.Add(generator.name +
                             " repeats the " + current +
                             " transition three times in a row.");
                break;
            }
            previous = current;
        }

        if (generator.FloorCount >= 5)
        {
            string[] requiredTypes =
                { "Hole", "Balcony", "Pipe", "Stair" };
            for (int i = 0; i < requiredTypes.Length; i++)
            {
                if (!transitions.Values.Contains(requiredTypes[i]))
                {
                    failures.Add(generator.name +
                                 " route does not contain the required " +
                                 requiredTypes[i] + " transition.");
                }
            }

            int minimumCount = requiredTypes.Min(type =>
                transitions.Values.Count(value => value == type));
            int maximumCount = requiredTypes.Max(type =>
                transitions.Values.Count(value => value == type));
            if (maximumCount - minimumCount > 1)
            {
                failures.Add(generator.name +
                             " route transition methods are not evenly distributed.");
            }

            MiniVanApartmentKeyPickup[] keys =
                generator.GetComponentsInChildren<
                    MiniVanApartmentKeyPickup>(true);
            MiniVanApartmentDoorLock[] lockedDoors =
                generator.GetComponentsInChildren<
                        MiniVanApartmentDoorLock>(true)
                    .Where(door => door.IsLocked)
                    .ToArray();
            if (keys.Length == 0 || lockedDoors.Length == 0)
            {
                failures.Add(generator.name +
                             " route must contain at least one key gate.");
            }
        }

        MiniVanPanelkaApartmentRouteMarker[] markers =
            generator.GetComponentsInChildren<
                MiniVanPanelkaApartmentRouteMarker>(true);
        for (int upperFloor = generator.FloorCount;
             upperFloor >= 2;
             upperFloor--)
        {
            MiniVanPanelkaApartmentRouteMarker upperMain =
                markers.FirstOrDefault(marker =>
                    marker.FloorNumber == upperFloor &&
                    marker.Role ==
                    MiniVanPanelkaApartmentRouteRole.MainRoute);
            MiniVanPanelkaApartmentRouteMarker lowerMain =
                markers.FirstOrDefault(marker =>
                    marker.FloorNumber == upperFloor - 1 &&
                    marker.Role ==
                    MiniVanPanelkaApartmentRouteRole.MainRoute);
            if (upperMain == null || lowerMain == null)
            {
                failures.Add(generator.name +
                             " has an incomplete main apartment route near floor " +
                             upperFloor + ".");
                continue;
            }

            int descentOrdinal =
                generator.FloorCount - upperFloor + 1;
            bool mustSwitch = descentOrdinal % 2 == 0;
            int expectedSlot = mustSwitch
                ? (upperMain.ApartmentSlot ^ 1)
                : upperMain.ApartmentSlot;
            if (lowerMain.ApartmentSlot != expectedSlot)
            {
                failures.Add(generator.name +
                             " does not switch to the opposite apartment after " +
                             "two descents near floor " + upperFloor + ".");
            }

            MiniVanPanelkaApartmentRouteMarker arrival =
                markers.FirstOrDefault(marker =>
                    marker.FloorNumber == upperFloor - 1 &&
                    marker.ApartmentSlot == upperMain.ApartmentSlot &&
                    marker.Role !=
                    MiniVanPanelkaApartmentRouteRole.Inaccessible);
            if (arrival == null)
            {
                failures.Add(generator.name +
                             " route transition from floor " + upperFloor +
                             " arrives in an exterior-only apartment.");
            }
            else if (mustSwitch &&
                     arrival.Role !=
                     MiniVanPanelkaApartmentRouteRole.TransferArrival)
            {
                failures.Add(generator.name +
                             " has no explicit transfer-arrival apartment on floor " +
                             (upperFloor - 1) + ".");
            }
        }
    }

    private static void AuditCrossEntranceRoutes(
        Scene scene,
        List<string> failures)
    {
        MiniVanGameModePlacementMarker[] sites =
            FindAllInScene<MiniVanGameModePlacementMarker>(scene);
        for (int siteIndex = 0;
             siteIndex < sites.Length;
             siteIndex++)
        {
            MiniVanGameModePlacementMarker site = sites[siteIndex];
            if (site.Floors < 5 || site.AccessibleEntrances < 2)
                continue;

            Transform[] all =
                site.GetComponentsInChildren<Transform>(true);
            Transform[] crossRoutes = all.Where(item =>
                    item.name.StartsWith(
                        "Route_Pipe_From_Floor_",
                        StringComparison.Ordinal) &&
                    item.name.IndexOf(
                        "CrossEntrance",
                        StringComparison.Ordinal) >= 0)
                .ToArray();
            MiniVanPanelkaBreakableWindow[] windows =
                site.GetComponentsInChildren<
                    MiniVanPanelkaBreakableWindow>(true);
            MiniVanPanelkaBreakableWindow[] departures =
                windows.Where(window =>
                        window.WindowId != null &&
                        window.WindowId.StartsWith(
                            "CROSS_PIPE_DEPARTURE_",
                            StringComparison.Ordinal))
                    .ToArray();
            MiniVanPanelkaBreakableWindow[] arrivals =
                windows.Where(window =>
                        window.WindowId != null &&
                        window.WindowId.StartsWith(
                            "CROSS_PIPE_ARRIVAL_",
                            StringComparison.Ordinal))
                    .ToArray();

            if (crossRoutes.Length == 0 ||
                departures.Length != crossRoutes.Length ||
                arrivals.Length != crossRoutes.Length)
            {
                failures.Add(site.name +
                             " must contain one complete cross-entrance pipe route.");
                continue;
            }

            for (int routeIndex = 0;
                 routeIndex < crossRoutes.Length;
                 routeIndex++)
            {
                Transform route = crossRoutes[routeIndex];
                int walkableSegments = route
                    .GetComponentsInChildren<BoxCollider>(true)
                    .Count(collider =>
                        collider.name.EndsWith(
                            "_Walkable_Top",
                            StringComparison.Ordinal));
                if (walkableSegments < 2 ||
                    route.Find("CrossEntrance_Turn_Platform") == null)
                {
                    failures.Add(site.name +
                                 " has an incomplete walkable cross-entrance pipe.");
                }
            }

            for (int departureIndex = 0;
                 departureIndex < departures.Length;
                 departureIndex++)
            {
                string token = departures[departureIndex].WindowId.Substring(
                    "CROSS_PIPE_DEPARTURE_".Length);
                MiniVanPanelkaBreakableWindow arrival =
                    arrivals.FirstOrDefault(window =>
                        window.WindowId ==
                        "CROSS_PIPE_ARRIVAL_" + token);
                MiniVanPanelkaStage1Generator sourceGenerator =
                    departures[departureIndex].GetComponentInParent<
                        MiniVanPanelkaStage1Generator>();
                MiniVanPanelkaStage1Generator targetGenerator =
                    arrival != null
                        ? arrival.GetComponentInParent<
                            MiniVanPanelkaStage1Generator>()
                        : null;
                if (arrival == null ||
                    sourceGenerator == null ||
                    targetGenerator == null ||
                    sourceGenerator == targetGenerator)
                {
                    failures.Add(site.name +
                                 " cross-entrance pipe does not connect two entrances.");
                }
            }
        }
    }

    private static bool TryReadFloorNumber(
        string value,
        string token,
        out int floorNumber)
    {
        floorNumber = 0;
        int start = value.IndexOf(token, StringComparison.Ordinal);
        if (start < 0)
            return false;
        start += token.Length;
        int length = 0;
        while (start + length < value.Length &&
               char.IsDigit(value[start + length]))
        {
            length++;
        }
        return length > 0 &&
               int.TryParse(
                   value.Substring(start, length),
                   out floorNumber);
    }

    private static bool IsHoleInsideRoomWithClearance(
        MiniVanPanelkaStage1Generator generator,
        MiniVanPanelkaApartmentTemplate template,
        Bounds holeBounds)
    {
        const float wallClearance = 0.30f;
        MiniVanPanelkaRoomIdentity[] rooms =
            template.GetComponentsInChildren<MiniVanPanelkaRoomIdentity>(true);
        for (int roomIndex = 0;
             roomIndex < rooms.Length;
             roomIndex++)
        {
            Bounds roomBounds = TransformBounds(
                new Bounds(
                    rooms[roomIndex].RoomCenterLocal,
                    rooms[roomIndex].RoomSizeLocal),
                generator.transform.worldToLocalMatrix *
                template.transform.localToWorldMatrix);
            if (holeBounds.min.x >=
                    roomBounds.min.x + wallClearance &&
                holeBounds.max.x <=
                    roomBounds.max.x - wallClearance &&
                holeBounds.min.z >=
                    roomBounds.min.z + wallClearance &&
                holeBounds.max.z <=
                    roomBounds.max.z - wallClearance)
            {
                return true;
            }
        }

        return false;
    }

    private static void AuditLockedExterior(
        MiniVanPanelkaStage1Generator generator,
        List<string> failures)
    {
        Renderer[] renderers =
            generator.GetComponentsInChildren<Renderer>(true);
        bool hasDecorativeGlass = renderers.Any(renderer =>
            renderer != null &&
            renderer.enabled &&
            renderer.gameObject.activeInHierarchy &&
            renderer.name.StartsWith("Baked_", StringComparison.Ordinal) &&
            renderer.sharedMaterial != null &&
            renderer.sharedMaterial.name.IndexOf(
                "WindowGlass",
                StringComparison.OrdinalIgnoreCase) >= 0);
        if (!hasDecorativeGlass)
            failures.Add(generator.name +
                         " has no baked decorative window glass.");

        bool hasDecorativeEntrance = renderers.Any(renderer =>
            renderer != null &&
            renderer.enabled &&
            renderer.gameObject.activeInHierarchy &&
            renderer.name.StartsWith("Baked_", StringComparison.Ordinal) &&
            renderer.sharedMaterial != null &&
            renderer.sharedMaterial.name.IndexOf(
                "Door",
                StringComparison.OrdinalIgnoreCase) >= 0);
        if (!hasDecorativeEntrance)
            failures.Add(generator.name +
                         " has no baked decorative entrance door.");

        Transform[] all =
            generator.GetComponentsInChildren<Transform>(true);
        Transform[] balconies = all.Where(item =>
                item.name.StartsWith(
                    "Apartment_Balcony_F",
                    StringComparison.Ordinal))
            .ToArray();
        int minimumBalconies = generator.FloorCount;
        int maximumBalconies = generator.FloorCount * 2;
        if (balconies.Length < minimumBalconies ||
            balconies.Length > maximumBalconies)
        {
            failures.Add(generator.name +
                         " has an invalid decorative balcony count: " +
                         balconies.Length + " (expected " +
                         minimumBalconies + "-" +
                         maximumBalconies + ").");
        }
        if (balconies.Any(balcony =>
                balcony.GetComponentsInChildren<Collider>(true).Length > 0))
        {
            failures.Add(generator.name +
                         " has colliders on decorative balconies.");
        }
    }

    private static void AuditFacadeOcclusion(
        MiniVanPanelkaStage1Generator generator,
        List<string> failures)
    {
        MiniVanPanelkaApartmentFacadeMarker[] windows =
            generator.GetComponentsInChildren<
                MiniVanPanelkaApartmentFacadeMarker>(true);
        for (int i = 0; i < windows.Length; i++)
        {
            MiniVanPanelkaWindowSocket socket =
                windows[i].GetComponent<MiniVanPanelkaWindowSocket>();
            if (socket != null && !socket.IsWindowActive)
                continue;
            Transform windowRoot =
                socket != null && socket.WindowModule != null
                    ? socket.WindowModule.transform
                    : windows[i].transform;
            Renderer activeGlass = windowRoot
                .GetComponentsInChildren<Renderer>(true)
                .FirstOrDefault(renderer =>
                    renderer.gameObject.activeInHierarchy &&
                    renderer.name == "Breakable_Glass");
            if (activeGlass != null &&
                TryGetBoundsRelativeTo(
                    windowRoot,
                    generator.transform,
                    out Bounds bounds) &&
                generator.IsFacadeDecorationOccluded(bounds))
            {
                failures.Add(generator.name +
                             " has a window inside an adjacent module.");
                break;
            }
        }

        Transform[] balconies = generator
            .GetComponentsInChildren<Transform>(true)
            .Where(item =>
                item.name.StartsWith(
                    "Apartment_Balcony_",
                    StringComparison.Ordinal) ||
                (item.name.StartsWith(
                     "Route_Balcony_",
                     StringComparison.Ordinal) &&
                 !item.name.StartsWith(
                     "Route_Balcony_Hatch_",
                     StringComparison.Ordinal)))
            .ToArray();
        for (int i = 0; i < balconies.Length; i++)
        {
            if (TryGetBoundsRelativeTo(
                    balconies[i],
                    generator.transform,
                    out Bounds bounds) &&
                generator.IsFacadeDecorationOccluded(bounds))
            {
                failures.Add(generator.name +
                             " has a balcony inside an adjacent module.");
                break;
            }
        }
    }

    private static bool TryGetBoundsRelativeTo(
        Transform root,
        Transform relativeTo,
        out Bounds bounds)
    {
        Renderer[] renderers =
            root.GetComponentsInChildren<Renderer>(true);
        bounds = default;
        bool initialized = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.gameObject.activeInHierarchy)
                continue;
            Bounds relativeBounds = TransformBounds(
                renderer.localBounds,
                relativeTo.worldToLocalMatrix *
                renderer.transform.localToWorldMatrix);
            if (!initialized)
            {
                bounds = relativeBounds;
                initialized = true;
            }
            else
            {
                bounds.Encapsulate(relativeBounds);
            }
        }

        return initialized;
    }

    private static void EnsureOutlineMaterial()
    {
        EnsureFolder("Assets/MiniVan Game/Resources", "Panelka");
        Shader shader = Shader.Find("MiniVanGame/ThinWhiteOutline");
        if (shader == null)
            throw new InvalidOperationException("Thin white outline shader was not imported.");

        Material material = AssetDatabase.LoadAssetAtPath<Material>(OutlineMaterialPath);
        if (material == null)
        {
            material = new Material(shader) { name = "Panelka Thin White Outline" };
            AssetDatabase.CreateAsset(material, OutlineMaterialPath);
        }
        else
        {
            material.shader = shader;
        }
        material.SetColor("_OutlineColor", Color.white);
        material.SetFloat("_OutlineWidth", 0f);
        material.SetFloat("_OutlineWidthIndependent", 0f);
        material.SetFloat("_OutlineZPos", -0.1f);
        EditorUtility.SetDirty(material);
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, child);
    }

    private static T FindInScene<T>(Scene scene) where T : Component
    {
        T[] all = FindAllInScene<T>(scene);
        return all.Length > 0 ? all[0] : null;
    }

    private static T[] FindAllInScene<T>(Scene scene) where T : Component
    {
        List<T> result = new List<T>();
        foreach (GameObject root in scene.GetRootGameObjects())
            result.AddRange(root.GetComponentsInChildren<T>(true));
        return result.ToArray();
    }


    private static bool AreCoplanarWallRenderers(Renderer wall, Renderer door)
    {
        Vector3 wallNormal = GetThinHorizontalAxis(wall);
        Vector3 doorNormal = GetThinHorizontalAxis(door);
        return Mathf.Abs(Vector3.Dot(wallNormal, doorNormal)) >= 0.96f;
    }

    private static Vector3 GetThinHorizontalAxis(Renderer renderer)
    {
        Bounds local = renderer.localBounds;
        Vector3 x = renderer.transform.TransformVector(Vector3.right * local.size.x);
        Vector3 z = renderer.transform.TransformVector(Vector3.forward * local.size.z);
        return (x.sqrMagnitude <= z.sqrMagnitude
                ? renderer.transform.TransformDirection(Vector3.right)
                : renderer.transform.TransformDirection(Vector3.forward)).normalized;
    }

    private static bool RendererBoundsIntersect(
        Renderer reference,
        Renderer candidate,
        float shrink)
    {
        Bounds candidateLocal = TransformBounds(
            candidate.localBounds,
            reference.transform.worldToLocalMatrix * candidate.transform.localToWorldMatrix);
        Vector3 shrinkVector = Vector3.one * Mathf.Max(0f, shrink);
        candidateLocal.Expand(-shrinkVector);
        return reference.localBounds.Intersects(candidateLocal);
    }

    private static Bounds TransformBounds(Bounds bounds, Matrix4x4 matrix)
    {
        Vector3 minimum = new Vector3(
            float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        Vector3 maximum = new Vector3(
            float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
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


    private static bool HasAncestorStartingWith(
        Transform item,
        Transform root,
        string prefix)
    {
        Transform current = item != null ? item.parent : null;
        while (current != null && current != root)
        {
            if (current.name.StartsWith(prefix, StringComparison.Ordinal))
                return true;
            current = current.parent;
        }
        return false;
    }
}
