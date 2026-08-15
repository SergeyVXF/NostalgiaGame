using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MiniVanGame
{
    internal sealed class MiniVanPanelkaPlayModeSmokeProbe : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Scene scene = SceneManager.GetSceneByName("Game_v01");
            if (!scene.IsValid() || !scene.isLoaded)
                return;

            GameObject probe = new GameObject("Panelka PlayMode Smoke Probe");
            DontDestroyOnLoad(probe);
            probe.AddComponent<MiniVanPanelkaPlayModeSmokeProbe>();
#endif
        }

        private IEnumerator Start()
        {
            yield return null;
            yield return new WaitForSecondsRealtime(2f);

            List<string> failures = new List<string>();
            Terrain terrain = FindFirstObjectByType<Terrain>();
            TerrainCollider terrainCollider = terrain != null
                ? terrain.GetComponent<TerrainCollider>()
                : null;
            MiniVanGameModeMapGenerator mapGenerator =
                FindFirstObjectByType<MiniVanGameModeMapGenerator>();
            bool terrainReady = terrain != null &&
                                terrain.gameObject.activeInHierarchy &&
                                terrain.enabled &&
                                terrain.drawHeightmap &&
                                terrain.terrainData != null &&
                                terrainCollider != null &&
                                terrainCollider.terrainData == terrain.terrainData;
            if (!terrainReady)
                failures.Add("runtime terrain is missing, hidden, or has no matching collider data");
            if (mapGenerator == null || mapGenerator.RoadSamples.Count < 2)
                failures.Add("runtime road cache was not restored");

            MiniVanPanelkaApartmentRouteMarker[] apartments =
                FindObjectsByType<MiniVanPanelkaApartmentRouteMarker>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None);
            HashSet<string> templateIds = new HashSet<string>(StringComparer.Ordinal);
            HashSet<int> templateIndices = new HashSet<int>();
            HashSet<string> pickupKeys = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> routeDoorKeys = new HashSet<string>(StringComparer.Ordinal);
            int routeApartments = 0;
            int activeRooms = 0;
            int activeCabinets = 0;

            for (int i = 0; i < apartments.Length; i++)
            {
                MiniVanPanelkaApartmentRouteMarker apartment = apartments[i];
                MiniVanPanelkaApartmentTemplate template =
                    apartment.GetComponentInChildren<MiniVanPanelkaApartmentTemplate>(true);
                if (template == null)
                {
                    failures.Add(apartment.name + " has no full apartment prefab");
                    continue;
                }

                if (apartment.RequiresVisit)
                {
                    routeApartments++;
                    templateIndices.Add(template.TemplateIndex);
                    AuditRouteApartment(
                        apartment,
                        template,
                        failures,
                        templateIds,
                        ref activeRooms,
                        ref activeCabinets);
                }
                else
                {
                    AuditExteriorOnlyApartment(apartment, template, failures);
                }

                MiniVanApartmentKeyPickup[] pickups =
                    apartment.GetComponentsInChildren<MiniVanApartmentKeyPickup>(true);
                for (int itemIndex = 0; itemIndex < pickups.Length; itemIndex++)
                {
                    if (!string.IsNullOrEmpty(pickups[itemIndex].KeyId))
                        pickupKeys.Add(pickups[itemIndex].KeyId);
                }

                MiniVanApartmentDoorLock[] locks =
                    apartment.GetComponentsInChildren<MiniVanApartmentDoorLock>(true);
                for (int doorIndex = 0; doorIndex < locks.Length; doorIndex++)
                {
                    if (apartment.Role == MiniVanPanelkaApartmentRouteRole.MainRoute &&
                        locks[doorIndex].name == "Apartment_Entrance_Door" &&
                        locks[doorIndex].IsLocked &&
                        !string.IsNullOrEmpty(locks[doorIndex].RequiredKeyId))
                    {
                        routeDoorKeys.Add(locks[doorIndex].RequiredKeyId);
                    }
                }
            }

            foreach (string keyId in pickupKeys)
                if (!routeDoorKeys.Contains(keyId))
                    failures.Add("key has no matching route door: " + keyId);
            foreach (string keyId in routeDoorKeys)
                if (!pickupKeys.Contains(keyId))
                    failures.Add("route door has no matching key: " + keyId);

            if (routeApartments == 0)
                failures.Add("no route apartments were generated");
            if (templateIndices.Count != 5)
                failures.Add(
                    "expected all 5 apartment layouts, generated " +
                    templateIndices.Count);
            if (activeRooms == 0 || activeCabinets == 0)
                failures.Add("full apartment prefabs have no active room content");

            bool doorAnimationReady = false;
            MiniVanApartmentDoor animationDoor = apartments
                .Where(apartment => apartment.RequiresVisit)
                .SelectMany(apartment =>
                    apartment.GetComponentsInChildren<MiniVanApartmentDoor>(true))
                .FirstOrDefault(door =>
                    door.gameObject.activeInHierarchy &&
                    door.name.StartsWith(
                        "Interior_Door_",
                        StringComparison.Ordinal) &&
                    door.Pivot != null);
            Renderer animationPanel = animationDoor != null
                ? animationDoor.Pivot.GetComponentsInChildren<Renderer>(true)
                    .FirstOrDefault(renderer => renderer.name == "Door_Panel")
                : null;
            if (animationDoor == null || animationPanel == null)
            {
                failures.Add("no active prefab interior door is available for animation");
            }
            else
            {
                Quaternion closedRotation = animationDoor.Pivot.localRotation;
                Vector3 closedPanelCenter = animationPanel.bounds.center;
                animationDoor.SetOpen(true);
                yield return new WaitForSecondsRealtime(0.65f);

                float openedAngle = Quaternion.Angle(
                    closedRotation,
                    animationDoor.Pivot.localRotation);
                float panelTravel = Vector3.Distance(
                    closedPanelCenter,
                    animationPanel.bounds.center);
                if (openedAngle < 35f || panelTravel < 0.2f)
                {
                    failures.Add(
                        animationDoor.name +
                        " did not visibly rotate its prefab door panel");
                }
                else
                {
                    animationDoor.SetOpen(false);
                    yield return new WaitForSecondsRealtime(0.65f);
                    if (Quaternion.Angle(
                            closedRotation,
                            animationDoor.Pivot.localRotation) > 2f)
                    {
                        failures.Add(
                            animationDoor.name +
                            " did not return to its closed prefab pose");
                    }
                    else
                    {
                        doorAnimationReady = true;
                    }
                }
            }

            int floorHolePassages = AuditFloorHolePassages(failures);
            int balconyPassages = AuditBalconyPassages(failures);
            int stairwellLowerWalls;
            int stairwellUpperWalls;
            AuditStairwellWallBands(
                failures, out stairwellLowerWalls, out stairwellUpperWalls);
            CaptureRepresentativeApartments(apartments, failures);

            string summary = "terrain=" + (terrainReady ? "ready" : "invalid") +
                             ", route samples=" +
                             (mapGenerator != null ? mapGenerator.RoadSamples.Count : 0) +
                             ", route apartments=" + routeApartments +
                             ", apartment prefabs=" + templateIds.Count +
                             ", active rooms=" + activeRooms +
                             ", active cabinets=" + activeCabinets +
                             ", route keys=" + pickupKeys.Count +
                             ", locked route doors=" + routeDoorKeys.Count +
                             ", door animation=" +
                             (doorAnimationReady ? "ready" : "invalid") +
                             ", floor-hole passages=" + floorHolePassages +
                             ", balcony passages=" + balconyPassages +
                             ", stairwell bands=" + stairwellLowerWalls + "/" +
                             stairwellUpperWalls;
            WriteResult(failures, summary);
            Destroy(gameObject);
        }

        private static void AuditRouteApartment(
            MiniVanPanelkaApartmentRouteMarker apartment,
            MiniVanPanelkaApartmentTemplate template,
            List<string> failures,
            HashSet<string> templateIds,
            ref int activeRooms,
            ref int activeCabinets)
        {
            if (string.IsNullOrEmpty(template.TemplateId))
                failures.Add(apartment.name + " has no apartment prefab id");
            else
                templateIds.Add(template.TemplateId);

            if (template.ContentRoot == null || !template.ContentRoot.gameObject.activeInHierarchy)
                failures.Add(apartment.name + " has no active editable apartment content");
            if (template.EntrySocket == null || template.RouteHoleSocket == null ||
                template.BalconySocket == null || template.PipeSocket == null ||
                template.KeySocket == null)
                failures.Add(apartment.name + " has incomplete route sockets");

            MiniVanPanelkaRoomIdentity[] rooms =
                template.GetComponentsInChildren<MiniVanPanelkaRoomIdentity>(true);
            int apartmentRooms = rooms.Count(room => room.gameObject.activeInHierarchy);
            if (apartmentRooms < 4)
                failures.Add(apartment.name + " has an incomplete apartment interior");
            activeRooms += apartmentRooms;

            MiniVanPanelkaCabinet[] cabinets =
                template.GetComponentsInChildren<MiniVanPanelkaCabinet>(true);
            int apartmentCabinets = cabinets.Count(cabinet => cabinet.gameObject.activeInHierarchy);
            if (apartmentCabinets == 0)
                failures.Add(apartment.name + " has no active cabinets");
            activeCabinets += apartmentCabinets;

            MiniVanApartmentDoor entrance = template
                .GetComponentsInChildren<MiniVanApartmentDoor>(true)
                .FirstOrDefault(door => door.name == "Apartment_Entrance_Door");
            if (entrance == null || !entrance.gameObject.activeInHierarchy)
                failures.Add(apartment.name + " has no active entrance door");
            MiniVanApartmentDoor[] prefabDoors =
                template.GetComponentsInChildren<MiniVanApartmentDoor>(true);
            for (int doorIndex = 0;
                 doorIndex < prefabDoors.Length;
                 doorIndex++)
            {
                MiniVanApartmentDoor door = prefabDoors[doorIndex];
                int panelCount = door.Pivot != null
                    ? door.Pivot.GetComponentsInChildren<Renderer>(true)
                        .Count(renderer => renderer.name == "Door_Panel")
                    : 0;
                int blockingPanelCount = door.Pivot != null
                    ? door.Pivot.GetComponentsInChildren<Collider>(true)
                        .Count(collider =>
                            collider.name == "Door_Panel" &&
                            collider.enabled &&
                            !collider.isTrigger)
                    : 0;
                if (panelCount != 1 || blockingPanelCount != 1)
                {
                    failures.Add(
                        apartment.name + "/" + door.name +
                        " does not contain one visible blocking prefab panel");
                }
            }
            if (template.GetComponentsInChildren<MiniVanPanelkaRoomDoor>(true).Length > 0)
                failures.Add(apartment.name + " still contains legacy room doors");
            if (template.GetComponentsInChildren<MiniVanPanelkaDoorCollisionProxy>(true).Length > 0)
                failures.Add(apartment.name + " still contains door collision proxies");

            Transform[] ceilings = template.GetComponentsInChildren<Transform>(true)
                .Where(item => item.name == "EXPLICIT_CEILING" &&
                               item.gameObject.activeInHierarchy)
                .ToArray();
            bool hasExplicitCeiling =
                ceilings.Any(item =>
                    item.GetComponentsInChildren<Renderer>(true).Length > 0);
            int routeCeilingSlabs = apartment
                .GetComponentsInChildren<Transform>(true)
                .Count(item =>
                    item.name.StartsWith(
                        "RouteCeiling_",
                        StringComparison.Ordinal) &&
                    item.GetComponent<Renderer>() != null &&
                    item.gameObject.activeInHierarchy);
            if (!hasExplicitCeiling && routeCeilingSlabs < 4)
                failures.Add(apartment.name + " has incomplete explicit ceilings");

            Transform[] all = template.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                string name = all[i].name;
                if (name == "TEMPLATE_RUNTIME_SHELL" ||
                    name == "TEMPLATE_HALL_SHELL" ||
                    name == "FINISH__WALLS_FLOOR_DOORS" ||
                    name == "ANCHORS__DO_NOT_MOVE" ||
                    name.StartsWith("GUIDE_", StringComparison.Ordinal) ||
                    name.StartsWith("DOOR_SOCKET__", StringComparison.Ordinal) ||
                    name.StartsWith("NO_FURNITURE__", StringComparison.Ordinal))
                {
                    failures.Add(apartment.name + " still contains legacy room generator object " + name);
                    break;
                }
            }
        }

        private static void AuditExteriorOnlyApartment(
            MiniVanPanelkaApartmentRouteMarker apartment,
            MiniVanPanelkaApartmentTemplate template,
            List<string> failures)
        {
            if (template.GetComponentsInChildren<MiniVanPanelkaRoomIdentity>(true)
                .Any(room => room.gameObject.activeInHierarchy))
                failures.Add(apartment.name + " is inaccessible but has active rooms");
            if (template.GetComponentsInChildren<MiniVanPanelkaCabinet>(true)
                .Any(cabinet => cabinet.gameObject.activeInHierarchy))
                failures.Add(apartment.name + " is inaccessible but has active cabinets");
            if (template.GetComponentsInChildren<MiniVanPanelkaInteractable>(true)
                .Any(item => item.gameObject.activeInHierarchy))
                failures.Add(apartment.name + " is inaccessible but has active interactables");

            Transform entrance = template.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(item => item.name == "Apartment_Entrance_Door");
            if (entrance == null || !entrance.gameObject.activeInHierarchy)
            {
                failures.Add(apartment.name + " has no sealed entrance door");
                return;
            }

            if (entrance.GetComponentsInChildren<MiniVanApartmentDoor>(true)
                .Any(door => door.gameObject.activeInHierarchy))
                failures.Add(apartment.name + " is inaccessible but has an openable entrance door");

            Transform panel = entrance.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(item => item.name == "Door_Panel" &&
                                        item.gameObject.activeInHierarchy);
            Collider panelCollider = panel != null ? panel.GetComponent<Collider>() : null;
            if (panel == null || panelCollider == null ||
                !panelCollider.enabled || panelCollider.isTrigger)
                failures.Add(apartment.name + " sealed entrance door has no blocking collider");
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

        private static int AuditBalconyPassages(
            List<string> failures)
        {
            int validPassages = 0;
            MiniVanPanelkaStage1Generator[] generators =
                FindObjectsByType<MiniVanPanelkaStage1Generator>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            for (int generatorIndex = 0;
                 generatorIndex < generators.Length;
                 generatorIndex++)
            {
                MiniVanPanelkaStage1Generator generator =
                    generators[generatorIndex];
                if (generator.ExteriorOnlyLocked)
                {
                    continue;
                }

                Transform[] all =
                    generator.GetComponentsInChildren<Transform>(true);
                Transform[] balconies = all.Where(item =>
                        item.name.StartsWith(
                            "Route_Balcony_",
                            StringComparison.Ordinal) &&
                        !item.name.StartsWith(
                            "Route_Balcony_Hatch_",
                            StringComparison.Ordinal) &&
                        !item.name.StartsWith(
                            "Route_Balcony_Transfer_",
                            StringComparison.Ordinal))
                    .ToArray();
                Transform[] ropes = all.Where(item =>
                        item.name.StartsWith(
                            "Route_Balcony_Hatch_Rope_From_",
                            StringComparison.Ordinal))
                    .ToArray();
                Transform[] transferPipes = all.Where(item =>
                        item.name.StartsWith(
                            "Route_Balcony_Transfer_Pipe_From_",
                            StringComparison.Ordinal))
                    .ToArray();
                MiniVanPanelkaBreakableWindow[] windows = generator
                    .GetComponentsInChildren<
                        MiniVanPanelkaBreakableWindow>(true)
                    .Where(window =>
                        window.WindowId != null &&
                        window.WindowId.StartsWith(
                            "BALCONY_WINDOW_",
                            StringComparison.Ordinal))
                    .ToArray();
                Renderer[] landings = all
                    .Where(item => item.name == "Arrival_Platform")
                    .Select(item => item.GetComponent<Renderer>())
                    .Where(renderer => renderer != null)
                    .ToArray();
                if (ropes.Length == 0)
                    continue;
                bool ropeOverLanding =
                    ropes.All(rope => landings.Any(landing =>
                        rope.position.x >= landing.bounds.min.x - 0.08f &&
                        rope.position.x <= landing.bounds.max.x + 0.08f &&
                        rope.position.z >= landing.bounds.min.z - 0.08f &&
                        rope.position.z <= landing.bounds.max.z + 0.08f));
                bool approvedRopePose =
                    ropes.All(rope =>
                        Mathf.Abs(rope.localPosition.x - 1.10f) <= 0.04f &&
                        Mathf.Abs(rope.localPosition.z - 0.95f) <= 0.04f);
                bool climbColliders =
                    ropes.All(rope =>
                    {
                        BoxCollider trigger =
                            rope.GetComponent<BoxCollider>();
                        BoxCollider physical = rope
                            .GetComponentsInChildren<BoxCollider>(true)
                            .FirstOrDefault(collider =>
                                collider.name == "Rope_Physical_Collider");
                        return trigger != null &&
                               trigger.isTrigger &&
                               trigger.size.x >= 1.79f &&
                               trigger.size.y >= 5.4f &&
                               physical != null &&
                               physical.enabled &&
                               !physical.isTrigger &&
                               physical.size.x <= 0.17f;
                    });
                Transform[] hatchLids = all.Where(item =>
                        item.name == "Balcony_Hatch_Open_Lid")
                    .ToArray();
                bool enlargedHatch =
                    hatchLids.Length == ropes.Length &&
                    hatchLids.All(hatchLid =>
                        Mathf.Abs(hatchLid.localScale.x - 1.38f) <= 0.02f &&
                        Mathf.Abs(hatchLid.localScale.z - 1.32f) <= 0.02f);
                bool walkablePipe =
                    transferPipes.Length == ropes.Length &&
                    transferPipes.All(pipe => pipe
                        .GetComponentsInChildren<BoxCollider>(true)
                        .Any(collider =>
                            collider.enabled &&
                            collider.name.EndsWith(
                                "_Walkable_Top",
                                StringComparison.Ordinal)));
                bool crackedBothSides =
                    generator.CrackedGlassMaterial != null &&
                    windows.Length == ropes.Length * 2 &&
                    windows.All(window =>
                    {
                        Renderer renderer =
                            window.GetComponent<Renderer>();
                        return renderer != null &&
                               renderer.gameObject.activeInHierarchy &&
                               renderer.sharedMaterial ==
                                   generator.CrackedGlassMaterial;
                    }) &&
                    (!generator.CrackedGlassMaterial.HasProperty("_Cull") ||
                     generator.CrackedGlassMaterial.GetFloat("_Cull") <=
                          0.01f);
                MiniVanPanelkaBreakableWindow[] routeWindows = generator
                    .GetComponentsInChildren<
                        MiniVanPanelkaBreakableWindow>(true)
                    .Where(window =>
                        window.WindowId != null &&
                        (window.WindowId.StartsWith(
                             "BALCONY_WINDOW_",
                             StringComparison.Ordinal) ||
                         window.WindowId.StartsWith(
                             "PIPE_WINDOW_",
                             StringComparison.Ordinal)))
                    .ToArray();
                bool exteriorHitProxies = routeWindows.All(window =>
                {
                    Transform proxy =
                        window.transform.Find("Breakable_Window_Hit_Proxy");
                    BoxCollider collider =
                        proxy != null ? proxy.GetComponent<BoxCollider>() : null;
                    return collider != null &&
                           collider.enabled &&
                           !collider.isTrigger &&
                           collider.bounds.size.y >= 0.69f &&
                           Mathf.Min(
                               collider.bounds.size.x,
                               collider.bounds.size.z) >= 0.34f;
                });

                bool valid =
                    balconies.Length == ropes.Length * 2 &&
                    ropeOverLanding &&
                    approvedRopePose &&
                    climbColliders &&
                    enlargedHatch &&
                    walkablePipe &&
                    crackedBothSides &&
                    exteriorHitProxies;
                if (valid)
                {
                    validPassages += ropes.Length;
                }
                else
                {
                    failures.Add(
                        generator.name +
                        " has an incomplete balcony passage: balconies=" +
                        balconies.Length +
                        ", ropes=" + ropes.Length +
                        ", transfer pipes=" + transferPipes.Length +
                        ", cracked windows=" + windows.Length +
                        ", ropeOverLanding=" + ropeOverLanding +
                        ", approvedRopePose=" + approvedRopePose +
                        ", climbColliders=" + climbColliders +
                        ", enlargedHatch=" + enlargedHatch +
                        ", walkablePipe=" + walkablePipe +
                        ", crackedBothSides=" + crackedBothSides +
                        ", exteriorHitProxies=" + exteriorHitProxies);
                }
            }

            return validPassages;
        }

        private static int AuditFloorHolePassages(
            List<string> failures)
        {
            int validPassages = 0;
            MiniVanPanelkaStage1Generator[] generators =
                FindObjectsByType<MiniVanPanelkaStage1Generator>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            for (int generatorIndex = 0;
                 generatorIndex < generators.Length;
                 generatorIndex++)
            {
                MiniVanPanelkaStage1Generator generator =
                    generators[generatorIndex];
                if (generator.ExteriorOnlyLocked ||
                    generator.FloorCount <= 1)
                {
                    continue;
                }

                Transform[] all =
                    generator.GetComponentsInChildren<Transform>(true);
                Transform[] holes = all.Where(item =>
                        item.name.StartsWith(
                            "Route_Template_Floor_Hole_",
                            StringComparison.Ordinal))
                    .ToArray();
                int ropes = all.Count(item =>
                    item.name.StartsWith(
                        "Route_Return_Rope_",
                        StringComparison.Ordinal));
                int balconyTransitions = all.Count(item =>
                    item.name.StartsWith(
                        "Route_Balcony_Hatch_Rope_From_",
                        StringComparison.Ordinal));
                int pipeTransitions = all.Count(item =>
                    item.name.StartsWith(
                        "Route_Pipe_From_Floor_",
                        StringComparison.Ordinal));
                int stairTransitions = all.Count(item =>
                    item.name.StartsWith(
                        "Route_Stair_Descent_From_Floor_",
                        StringComparison.Ordinal));
                int stairBlockages = all.Count(item =>
                    item.name.StartsWith(
                        "Route_Stair_Blockage_Between_Floor_",
                        StringComparison.Ordinal));
                if (holes.Length != ropes ||
                    holes.Length +
                    balconyTransitions +
                    pipeTransitions +
                    stairTransitions != generator.FloorCount - 1 ||
                    stairBlockages !=
                    generator.FloorCount - 1 - stairTransitions)
                {
                    failures.Add(generator.name +
                                 " does not provide a forced route transition " +
                                 "at every floor boundary");
                    continue;
                }

                for (int holeIndex = 0;
                     holeIndex < holes.Length;
                     holeIndex++)
                {
                    Transform hole = holes[holeIndex];
                    MiniVanPanelkaApartmentRouteMarker upper =
                        hole.GetComponentInParent<
                            MiniVanPanelkaApartmentRouteMarker>(true);
                    MiniVanPanelkaApartmentRouteMarker lower =
                        upper != null
                            ? generator
                                .GetComponentsInChildren<
                                    MiniVanPanelkaApartmentRouteMarker>(true)
                                .FirstOrDefault(apartment =>
                                    apartment.FloorNumber ==
                                    upper.FloorNumber - 1 &&
                                    apartment.ApartmentSlot ==
                                    upper.ApartmentSlot)
                            : null;
                    MiniVanPanelkaApartmentTemplate lowerTemplate =
                        lower != null
                            ? lower.GetComponentInChildren<
                                MiniVanPanelkaApartmentTemplate>(true)
                            : null;
                    if (lower == null ||
                        lower.Role ==
                        MiniVanPanelkaApartmentRouteRole.Inaccessible ||
                        lowerTemplate == null ||
                        lowerTemplate.GetComponentsInChildren<
                            MiniVanPanelkaRoomIdentity>(true).Length < 4)
                    {
                        failures.Add(generator.name +
                                     " floor hole leads to an exterior-only apartment");
                        continue;
                    }

                    Transform clearance =
                        hole.Find("Route_Hole_Clearance_Volume");
                    MiniVanPanelkaApartmentTemplate upperTemplate =
                        upper != null
                            ? upper.GetComponentInChildren<
                                MiniVanPanelkaApartmentTemplate>(true)
                            : null;
                    if (clearance == null ||
                        upperTemplate == null)
                    {
                        failures.Add(generator.name +
                                     " floor hole has no clearance marker");
                        continue;
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
                                     " floor hole crosses a room wall");
                        continue;
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
                                     " floor hole has no matching ceiling opening");
                        continue;
                    }

                    validPassages++;
                }
            }

            return validPassages;
        }

        private static bool IsHoleInsideRoomWithClearance(
            MiniVanPanelkaStage1Generator generator,
            MiniVanPanelkaApartmentTemplate template,
            Bounds holeBounds)
        {
            const float wallClearance = 0.30f;
            MiniVanPanelkaRoomIdentity[] rooms =
                template.GetComponentsInChildren<
                    MiniVanPanelkaRoomIdentity>(true);
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

        private static Bounds TransformBounds(
            Bounds source,
            Matrix4x4 matrix)
        {
            Vector3 center =
                matrix.MultiplyPoint3x4(source.center);
            Vector3 extents = source.extents;
            Vector3 axisX = matrix.MultiplyVector(
                new Vector3(extents.x, 0f, 0f));
            Vector3 axisY = matrix.MultiplyVector(
                new Vector3(0f, extents.y, 0f));
            Vector3 axisZ = matrix.MultiplyVector(
                new Vector3(0f, 0f, extents.z));
            Vector3 transformedExtents = new Vector3(
                Mathf.Abs(axisX.x) +
                Mathf.Abs(axisY.x) +
                Mathf.Abs(axisZ.x),
                Mathf.Abs(axisX.y) +
                Mathf.Abs(axisY.y) +
                Mathf.Abs(axisZ.y),
                Mathf.Abs(axisX.z) +
                Mathf.Abs(axisY.z) +
                Mathf.Abs(axisZ.z));
            return new Bounds(
                center,
                transformedExtents * 2f);
        }

        private static void AuditStairwellWallBands(
            List<string> failures,
            out int lowerCount,
            out int upperCount)
        {
            lowerCount = 0;
            upperCount = 0;
            MiniVanPanelkaStage1Generator[] generators =
                FindObjectsByType<MiniVanPanelkaStage1Generator>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int generatorIndex = 0; generatorIndex < generators.Length; generatorIndex++)
            {
                MiniVanPanelkaStage1Generator generator = generators[generatorIndex];
                Renderer[] renderers = generator.GetComponentsInChildren<Renderer>(true);
                for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
                {
                    Renderer renderer = renderers[rendererIndex];
                    if (renderer.name.EndsWith("Green_Lower", StringComparison.Ordinal))
                    {
                        lowerCount++;
                        if (generator.StairwellLowerWallMaterial != null &&
                            renderer.sharedMaterial != generator.StairwellLowerWallMaterial)
                            failures.Add(generator.name + " has a wrong lower stairwell material");
                    }
                    else if (renderer.name.EndsWith("White_Upper", StringComparison.Ordinal))
                    {
                        upperCount++;
                        if (generator.StairwellUpperWallMaterial != null &&
                            renderer.sharedMaterial != generator.StairwellUpperWallMaterial)
                            failures.Add(generator.name + " has a wrong upper stairwell material");
                    }
                }
            }
            if (lowerCount == 0 || upperCount == 0)
                failures.Add("no two-band stairwell wall finish was generated");
        }

        private static void CaptureRepresentativeApartments(
            MiniVanPanelkaApartmentRouteMarker[] apartments,
            List<string> failures)
        {
            MiniVanPanelkaApartmentTemplateCatalog catalog =
                Resources.Load<MiniVanPanelkaApartmentTemplateCatalog>(
                    "Panelka/ApartmentTemplateCatalog");
            if (catalog == null)
            {
                failures.Add("apartment prefab catalog is missing during visual capture");
                return;
            }

            string folder = Path.GetFullPath(Path.Combine(
                Application.dataPath, "..", "Library", "CodexTools", "PanelkaVisuals"));
            Directory.CreateDirectory(folder);
            foreach (string oldFile in Directory.GetFiles(folder, "ApartmentPrefab_*.png"))
                File.Delete(oldFile);

            GameObject cameraObject = new GameObject("Panelka Apartment Prefab Audit Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.22f, 0.24f, 0.27f, 1f);
            camera.nearClipPlane = 0.02f;
            camera.farClipPlane = 80f;
            camera.allowHDR = false;
            camera.allowMSAA = true;
            camera.cullingMask = ~0;

            RenderTexture texture = new RenderTexture(1280, 800, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = texture;
            int captureIndex = 0;
            for (int templateIndex = 1; templateIndex <= 5; templateIndex++)
            {
                GameObject prefab = catalog.GetPrefab(templateIndex);
                if (prefab == null)
                {
                    failures.Add("missing apartment prefab " + templateIndex + " for capture");
                    continue;
                }

                GameObject clone = Instantiate(prefab);
                clone.name = "Apartment_Prefab_Visual_Audit_" + templateIndex.ToString("00");
                clone.transform.SetPositionAndRotation(
                    new Vector3(100000f + templateIndex * 30f, 0f, 100000f),
                    Quaternion.identity);
                clone.transform.localScale = Vector3.one;
                MiniVanPanelkaApartmentTemplate template =
                    clone.GetComponent<MiniVanPanelkaApartmentTemplate>();
                Renderer[] renderers = clone.GetComponentsInChildren<Renderer>(false);
                if (renderers.Length == 0)
                {
                    failures.Add(prefab.name + " has no visible renderers for capture");
                    Destroy(clone);
                    continue;
                }

                bool hasVisibleFloor = renderers.Any(renderer =>
                    renderer.enabled &&
                    (renderer.name == "Floor" ||
                     renderer.name == "Floor_Finish" ||
                     renderer.name == "RoomFloorFinish"));
                bool hasVisibleWall = renderers.Any(renderer =>
                    renderer.enabled &&
                    (renderer.name.StartsWith("LayoutWall_", StringComparison.Ordinal) ||
                     renderer.name.StartsWith("FacadeWall_", StringComparison.Ordinal)));
                if (!hasVisibleFloor)
                    failures.Add(prefab.name + " has no enabled floor renderer");
                if (!hasVisibleWall)
                    failures.Add(prefab.name + " has no enabled wall renderer");

                Bounds bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                    bounds.Encapsulate(renderers[i].bounds);
                camera.transform.position = bounds.center + Vector3.up * (bounds.extents.y + 0.3f);
                camera.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
                camera.orthographicSize = Mathf.Max(bounds.extents.x, bounds.extents.z) * 1.18f;
                camera.Render();

                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = texture;
                Texture2D image = new Texture2D(1280, 800, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0f, 0f, 1280f, 800f), 0, 0);
                image.Apply();
                string templateId = template != null && !string.IsNullOrEmpty(template.TemplateId)
                    ? template.TemplateId
                    : prefab.name;
                File.WriteAllBytes(
                    Path.Combine(folder, "ApartmentPrefab_" + (++captureIndex).ToString("00") +
                                 "_" + templateId + ".png"),
                    image.EncodeToPNG());
                Destroy(image);
                RenderTexture.active = previous;
                Destroy(clone);
            }

            camera.targetTexture = null;
            texture.Release();
            Destroy(texture);
            Destroy(cameraObject);
        }

        private static void WriteResult(List<string> failures, string summary)
        {
            string resultPath = Path.GetFullPath(Path.Combine(
                Application.dataPath, "..", "Library", "CodexTools",
                "PanelkaRuntimeSmoke.result"));
            if (failures.Count == 0)
            {
                File.WriteAllText(resultPath, "PASS: " + summary + Environment.NewLine);
                Debug.Log("[Panelka PlayMode Smoke] PASS: " + summary);
                return;
            }

            File.WriteAllText(
                resultPath,
                "FAIL: " + summary + Environment.NewLine +
                string.Join(Environment.NewLine, failures) + Environment.NewLine);
            Debug.LogError("[Panelka PlayMode Smoke] FAIL: " + summary + "\n" +
                           string.Join("\n", failures));
        }
    }
}
