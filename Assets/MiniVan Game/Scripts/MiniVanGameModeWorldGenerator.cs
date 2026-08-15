using System.Collections.Generic;
using UnityEngine;

namespace MiniVanGame
{
    [ExecuteAlways]
    public sealed class MiniVanGameModeWorldGenerator : MonoBehaviour
    {
        public MiniVanGameModeMapGenerator MapGenerator;
        public GameObject ZombiePrefab;
        public GameObject BatPickupPrefab;

        [Header("Panelka materials")]
        public Material ExteriorMaterial;
        public Material InteriorMaterial;
        public Material FloorMaterial;
        public Material DoorMaterial;
        [Tooltip("Apartment entrance leaves pick one of these per apartment.")]
        public Material[] ApartmentDoorMaterials;
        public Material OpaqueWindowMaterial;
        public Material CrackedWindowMaterial;
        public Material MetalMaterial;
        public Material StairwellFloorMaterial;
        public Material StairwellWallMaterial;
        public Material StairwellLowerWallMaterial;
        public Material StairwellUpperWallMaterial;
        public Material StairwellCeilingMaterial;
        public Material StairwellDoorMaterial;
        public Material WoodMaterial;
        public Material FabricMaterial;
        public Material PaperMaterial;
        public Material DarkMaterial;

        [Header("Game mode materials")]
        public Material HouseMaterial;
        public Material RoofMaterial;
        public Material CrateMaterial;
        public Material CoinMaterial;
        public Material ShopMaterial;

        [Header("Game mode prefabs")]
        public GameObject StartCompoundPrefab;
        public GameObject SaveZonePrefab;
        public bool GenerateOnStart;

        private const string GeneratedRootName = "Generated_GameMode_Content";
        private const float PanelkaModuleWidth = 26f;
        private const float PanelkaModuleDepth = 18f;
        private const float PanelkaModuleStep = PanelkaModuleWidth;
        private const float PanelkaTurnOffset =
            (PanelkaModuleWidth + PanelkaModuleDepth) * 0.5f;
        private Terrain terrain;
        private Transform root;
        private readonly List<TerrainFootprint> panelkaFootprints = new List<TerrainFootprint>();
        private readonly List<Vector3> placedPanelkaCenters = new List<Vector3>();

        private struct EntrancePose
        {
            public Vector3 Position;
            public float Yaw;

            public EntrancePose(Vector3 position, float yaw)
            {
                Position = position;
                Yaw = yaw;
            }
        }

        private struct TerrainFootprint
        {
            public Vector3 Center;
            public Quaternion Rotation;
            public Vector2 Size;
            public float Height;
        }

        private void Start()
        {
            if (!Application.isPlaying)
                return;

            if (MapGenerator == null)
                MapGenerator = FindFirstObjectByType<MiniVanGameModeMapGenerator>();
            if (MapGenerator != null)
                MapGenerator.EnsureRuntimeReady();

            if (GenerateOnStart || transform.Find(GeneratedRootName) == null)
            {
                Rebuild();
            }
        }

        [ContextMenu("Rebuild Game Mode Content")]
        public void Rebuild()
        {
            ClearGenerated();
            if (MapGenerator == null)
            {
                MapGenerator = FindFirstObjectByType<MiniVanGameModeMapGenerator>();
            }
            terrain = FindFirstObjectByType<Terrain>();
            if (MapGenerator == null || terrain == null || MapGenerator.RoadSamples.Count < 2)
            {
                return;
            }

            root = new GameObject(GeneratedRootName).transform;
            root.SetParent(transform, false);
            panelkaFootprints.Clear();
            placedPanelkaCenters.Clear();

            BuildPanelkaSite(0, MiniVanGameModePlacementKind.PanelkaSmall, 3, 2, 2, 0.25f, 1f, 46f);
            BuildPanelkaSite(1, MiniVanGameModePlacementKind.PanelkaMedium, 5, 6, 2, 0.50f, -1f, 55f);
            BuildPanelkaSite(2, MiniVanGameModePlacementKind.PanelkaLarge, 9, 12, 2, 0.75f, 1f, 60f);
            ApplyPanelkaTerrainFootprints();

            float[] houseRouteT = { 0.10f, 0.31f, 0.60f, 0.90f };
            for (int i = 0; i < houseRouteT.Length; i++)
            {
                BuildSmallHouse(i, houseRouteT[i], i % 2 == 0 ? -1f : 1f, 27f + i * 2f);
            }

            BuildStartCompound();
            BuildSaveZone();
            BuildRoadsideSpawnPoints();
            BuildSystems();
            EnsureGeneratedTextDepth();
        }

        [ContextMenu("Clear Game Mode Content")]
        public void ClearGenerated()
        {
            Transform old = transform.Find(GeneratedRootName);
            if (old == null) return;
            if (Application.isPlaying) Destroy(old.gameObject);
            else DestroyImmediate(old.gameObject);
        }

        private void BuildPanelkaSite(int siteIndex, MiniVanGameModePlacementKind kind, int floors,
            int entrances, int accessibleEntrances, float routeT, float side, float roadOffset)
        {
            List<EntrancePose> layout = BuildEntranceLayout(entrances);
            FindSafePanelkaPose(layout, routeT, side, roadOffset, out Vector3 position, out Quaternion rotation);
            Transform site = Group("Panelka_Site_" + (siteIndex + 1) + "_" + kind, root);
            site.position = position;
            site.rotation = rotation;

            MiniVanGameModePlacementMarker marker = site.gameObject.AddComponent<MiniVanGameModePlacementMarker>();
            marker.Kind = kind;
            marker.SiteIndex = siteIndex;
            marker.Floors = floors;
            marker.Entrances = entrances;
            marker.AccessibleEntrances = accessibleEntrances;

            const float entranceWidth = PanelkaModuleWidth;
            Bounds localBounds = GetLayoutBounds(layout);
            ClearTrees(site, new Vector2(localBounds.size.x + 28f, localBounds.size.z + 28f));

            HashSet<int> accessible = new HashSet<int>();
            List<MiniVanPanelkaStage1Generator> accessibleGenerators =
                new List<MiniVanPanelkaStage1Generator>();
            if (entrances <= 2)
            {
                for (int i = 0; i < entrances; i++) accessible.Add(i);
            }
            else
            {
                accessible.Add(Mathf.Clamp(entrances / 3, 0, entrances - 1));
                accessible.Add(Mathf.Clamp(entrances - 1 - entrances / 3, 0, entrances - 1));
            }

            for (int entrance = 0; entrance < entrances; entrance++)
            {
                EntrancePose pose = layout[entrance];
                Transform foundation = Group("Foundation_" + (entrance + 1), site);
                foundation.localPosition = pose.Position + Vector3.down * 0.2f;
                foundation.localRotation = Quaternion.Euler(0f, pose.Yaw, 0f);
                CreateBox("Concrete Foundation", foundation, Vector3.zero,
                    new Vector3(entranceWidth + 2f, 0.4f, 20f), FloorMaterial, true);

                Vector3 moduleWorld = site.TransformPoint(pose.Position);
                Quaternion moduleRotation = site.rotation * Quaternion.Euler(0f, pose.Yaw, 0f);
                placedPanelkaCenters.Add(moduleWorld);
                panelkaFootprints.Add(new TerrainFootprint
                {
                    Center = moduleWorld,
                    Rotation = moduleRotation,
                    Size = new Vector2(entranceWidth + 8f, 26f),
                    Height = position.y - 0.35f
                });

                if (accessible.Contains(entrance))
                {
                    accessibleGenerators.Add(BuildAccessibleEntrance(
                        site,
                        siteIndex,
                        entrance,
                        floors,
                        pose,
                        entrances > 2,
                        BuildFacadeOcclusionBounds(layout, entrance)));
                }
                else
                {
                    BuildClosedEntrance(
                        site,
                        siteIndex,
                        entrance,
                        floors,
                        pose,
                        BuildFacadeOcclusionBounds(layout, entrance));
                }
            }

            if (floors >= 5 && accessibleGenerators.Count >= 2)
            {
                MiniVanPanelkaStage1Generator source =
                    accessibleGenerators[0];
                MiniVanPanelkaStage1Generator target =
                    accessibleGenerators[1];
                if (!source.TryRedirectPipeRouteToEntrance(
                        target,
                        siteIndex,
                        out int sourceFloor,
                        out int targetFloor))
                {
                    Debug.LogWarning(
                        "[Panelka] Could not build a cross-entrance route for site " +
                        (siteIndex + 1) + ".",
                        site);
                }
                else
                {
                    Debug.Log(
                        "[Panelka] Cross-entrance route: " +
                        source.name + " floor " + sourceFloor +
                        " -> " + target.name + " floor " + targetFloor + ".",
                        site);
                }
            }

            CreateExteriorSpawnRing(site, siteIndex, localBounds.size.x, localBounds.size.z);
        }

        private MiniVanPanelkaStage1Generator BuildAccessibleEntrance(
            Transform site,
            int siteIndex,
            int entranceIndex,
            int floors, EntrancePose pose, bool roofOnly, Bounds[] facadeOcclusions)
        {
            Transform entrance = Group("Entrance_" + (entranceIndex + 1) + "_ACCESSIBLE", site);
            entrance.localPosition = pose.Position;
            entrance.localRotation = Quaternion.Euler(0f, pose.Yaw, 0f);
            entrance.localScale = Vector3.one;

            MiniVanPanelkaStage1Generator generator = entrance.gameObject.AddComponent<MiniVanPanelkaStage1Generator>();
            ConfigurePanelkaGenerator(generator, siteIndex, entranceIndex, floors);
            generator.ConfigureFacadeOcclusion(facadeOcclusions);
            generator.Rebuild();
            if (siteIndex == 0 && entranceIndex == 0)
            {
                BuildDoctorOffice(entrance);
            }

            GameObject zoneObject = new GameObject("Interior Zone");
            zoneObject.transform.SetParent(entrance, false);
            zoneObject.transform.localPosition = new Vector3(0f, floors * 1.6f, 0f);
            BoxCollider zoneCollider = zoneObject.AddComponent<BoxCollider>();
            zoneCollider.isTrigger = true;
            zoneCollider.size = new Vector3(25.5f, floors * 3.2f, 17.5f);
            MiniVanGameModeInteriorZone zone = zoneObject.AddComponent<MiniVanGameModeInteriorZone>();
            zone.SiteIndex = siteIndex;

            if (roofOnly)
            {
                MiniVanPanelkaRoomDoor[] doors =
                    entrance.GetComponentsInChildren<MiniVanPanelkaRoomDoor>(true);
                for (int i = 0; i < doors.Length; i++)
                {
                    if (doors[i].name != "Street_Entrance_Door")
                    {
                        continue;
                    }

                    doors[i].RequiresKey = true;
                    doors[i].KeyId = "panelka-ground-exit-inside-only";
                    doors[i].LockedApproachDirection = Vector3.back;
                    doors[i].Message = "Opens only from inside";
                    break;
                }
            }

            return generator;
        }

        private void BuildClosedEntrance(Transform site, int siteIndex, int entranceIndex, int floors,
            EntrancePose pose, Bounds[] facadeOcclusions)
        {
            Transform closed = Group("Entrance_" + (entranceIndex + 1) + "_CLOSED_NO_INTERIOR", site);
            closed.localPosition = pose.Position;
            closed.localRotation = Quaternion.Euler(0f, pose.Yaw, 0f);
            closed.localScale = Vector3.one;

            MiniVanPanelkaStage1Generator generator =
                closed.gameObject.AddComponent<MiniVanPanelkaStage1Generator>();
            ConfigurePanelkaGenerator(generator, siteIndex, entranceIndex, floors);
            generator.ConfigureFacadeOcclusion(facadeOcclusions);
            generator.ExteriorOnlyLocked = true;
            generator.FurnishTopFloor = false;
            generator.FurnishGeneratedRoute = false;
            generator.FurnishAllLandings = false;
            generator.Rebuild();

            for (int floor = 0; floor < floors; floor++)
            {
                float sideX =
                    ((floor + entranceIndex) & 1) == 0 ? -7.1f : 7.1f;
                if (!generator.IsFacadeDecorationOccluded(
                        new Bounds(
                            new Vector3(sideX, 0f, -9.72f),
                            new Vector3(3.4f, 0f, 1.35f))))
                {
                    BuildClosedFacadeBalcony(closed, floor, sideX, -1f);
                }
                if (!generator.IsFacadeDecorationOccluded(
                        new Bounds(
                            new Vector3(-sideX, 0f, 9.72f),
                            new Vector3(3.4f, 0f, 1.35f))))
                {
                    BuildClosedFacadeBalcony(closed, floor, -sideX, 1f);
                }
            }

            CreateBox("Hidden Ground Entry Blocker", closed, new Vector3(0f, 1.35f, -8.7f),
                new Vector3(3.4f, 2.7f, 0.32f), MetalMaterial, true).GetComponent<Renderer>().enabled = false;
        }

        private void ConfigurePanelkaGenerator(MiniVanPanelkaStage1Generator generator,
            int siteIndex, int entranceIndex, int floors)
        {
            generator.FloorCount = floors;
            generator.GenerationSeed = MapGenerator.Seed * 100 + siteIndex * 20 + entranceIndex;
            generator.GenerateOnStart = false;
            generator.RandomizeGenerationOnPlay = false;
            generator.SpawnPanelkaZombies = false;
            generator.FurnishGeneratedRoute = true;
            generator.FurnishAllLandings = floors <= 5;
            generator.ExteriorMaterial = ExteriorMaterial;
            generator.InteriorMaterial = InteriorMaterial;
            generator.FloorMaterial = FloorMaterial;
            generator.DoorMaterial = DoorMaterial;
            generator.ApartmentDoorMaterials = ApartmentDoorMaterials;
            generator.GlassMaterial = OpaqueWindowMaterial;
            generator.CrackedGlassMaterial = CrackedWindowMaterial;
            generator.MetalMaterial = MetalMaterial;
            generator.StairwellFloorMaterial = StairwellFloorMaterial;
            generator.StairwellWallMaterial = StairwellWallMaterial;
            generator.StairwellLowerWallMaterial = StairwellLowerWallMaterial;
            generator.StairwellUpperWallMaterial = StairwellUpperWallMaterial;
            generator.StairwellCeilingMaterial = StairwellCeilingMaterial;
            generator.StairwellDoorMaterial = StairwellDoorMaterial;
            generator.FurnitureWoodMaterial = WoodMaterial;
            generator.FurnitureFabricMaterial = FabricMaterial;
            generator.FurnitureCarpetMaterial = FabricMaterial;
            generator.FurnitureMetalMaterial = MetalMaterial;
            generator.FurnitureCeramicMaterial = InteriorMaterial;
            generator.FurniturePaperMaterial = PaperMaterial;
            generator.FurnitureDarkPlasticMaterial = DarkMaterial;
            generator.KeyMaterial = CoinMaterial;
            generator.ZombiePrefab = ZombiePrefab;
        }

        private void BuildClosedFacadeBalcony(Transform parent, int floor, float x, float facadeSign)
        {
            float y = floor * 3.2f + 0.18f;
            float z = facadeSign * 9.72f;
            Transform balcony = Group("Apartment_Balcony_F" + (floor + 1) + "_" +
                (facadeSign < 0f ? "Front" : "Back") + "_" + x, parent);
            balcony.localPosition = new Vector3(x, y, z);
            CreateBox("Platform", balcony, Vector3.zero,
                new Vector3(3.4f, 0.22f, 1.35f), FloorMaterial, false);
            CreateBox("Outer_Rail", balcony, new Vector3(0f, 0.68f, facadeSign * 0.58f),
                new Vector3(3.4f, 1.25f, 0.12f), MetalMaterial, false);
            CreateBox("Side_Rail_A", balcony, new Vector3(-1.64f, 0.68f, 0f),
                new Vector3(0.12f, 1.25f, 1.25f), MetalMaterial, false);
            CreateBox("Side_Rail_B", balcony, new Vector3(1.64f, 0.68f, 0f),
                new Vector3(0.12f, 1.25f, 1.25f), MetalMaterial, false);
        }

        private void BuildClosedRoofHatch(Transform parent, float roofHeight)
        {
            Transform hatch = Group("Roof_Hatch_CLOSED", parent);
            hatch.localPosition = new Vector3(0f, roofHeight + 0.36f, 6f);
            CreateBox("Hatch_Frame_Left", hatch, new Vector3(-0.9f, 0f, 0f),
                new Vector3(0.18f, 0.22f, 2f), MetalMaterial, true);
            CreateBox("Hatch_Frame_Right", hatch, new Vector3(0.9f, 0f, 0f),
                new Vector3(0.18f, 0.22f, 2f), MetalMaterial, true);
            CreateBox("Hatch_Frame_Front", hatch, new Vector3(0f, 0f, -0.9f),
                new Vector3(1.65f, 0.22f, 0.18f), MetalMaterial, true);
            CreateBox("Hatch_Frame_Back", hatch, new Vector3(0f, 0f, 0.9f),
                new Vector3(1.65f, 0.22f, 0.18f), MetalMaterial, true);
            CreateBox("Hatch_Panel_Locked", hatch, new Vector3(0f, 0.08f, 0f),
                new Vector3(1.7f, 0.16f, 1.7f), DoorMaterial, true);
        }

        private static List<EntrancePose> BuildEntranceLayout(int entrances)
        {
            const float step = PanelkaModuleStep;
            List<EntrancePose> layout = new List<EntrancePose>(entrances);
            if (entrances <= 2)
            {
                for (int i = 0; i < entrances; i++)
                {
                    layout.Add(new EntrancePose(new Vector3((i - (entrances - 1) * 0.5f) * step, 0f, 0f), 0f));
                }
                return layout;
            }

            if (entrances <= 6)
            {
                layout.Add(new EntrancePose(new Vector3(-step, 0f, 0f), 0f));
                layout.Add(new EntrancePose(Vector3.zero, 0f));
                layout.Add(new EntrancePose(new Vector3(step, 0f, 0f), 0f));
                layout.Add(new EntrancePose(
                    new Vector3(step, 0f, PanelkaTurnOffset),
                    90f));
                layout.Add(new EntrancePose(
                    new Vector3(step, 0f, PanelkaTurnOffset + step),
                    90f));
                layout.Add(new EntrancePose(
                    new Vector3(step, 0f, PanelkaTurnOffset + step * 2f),
                    90f));
                return layout;
            }

            layout.Add(new EntrancePose(new Vector3(-step * 1.5f, 0f, 0f), 0f));
            layout.Add(new EntrancePose(new Vector3(-step * 0.5f, 0f, 0f), 0f));
            layout.Add(new EntrancePose(new Vector3(step * 0.5f, 0f, 0f), 0f));
            layout.Add(new EntrancePose(new Vector3(step * 1.5f, 0f, 0f), 0f));
            for (int i = 1; i <= 4; i++)
            {
                float wingZ = PanelkaTurnOffset + step * (i - 1);
                layout.Add(new EntrancePose(
                    new Vector3(-step * 1.5f, 0f, wingZ),
                    90f));
                layout.Add(new EntrancePose(
                    new Vector3(step * 1.5f, 0f, wingZ),
                    -90f));
            }
            return layout;
        }

        private static Bounds GetLayoutBounds(List<EntrancePose> layout)
        {
            Vector3 firstSize = IsQuarterTurn(layout[0].Yaw)
                ? new Vector3(20f, 1f, 28f)
                : new Vector3(28f, 1f, 20f);
            Bounds bounds = new Bounds(layout[0].Position, firstSize);
            for (int i = 1; i < layout.Count; i++)
            {
                Vector3 size = IsQuarterTurn(layout[i].Yaw)
                    ? new Vector3(20f, 1f, 28f)
                    : new Vector3(28f, 1f, 20f);
                bounds.Encapsulate(new Bounds(layout[i].Position, size));
            }
            return bounds;
        }

        private static bool IsQuarterTurn(float yaw)
        {
            return Mathf.Abs(Mathf.DeltaAngle(yaw, 90f)) < 1f ||
                   Mathf.Abs(Mathf.DeltaAngle(yaw, -90f)) < 1f;
        }

        private static Bounds[] BuildFacadeOcclusionBounds(
            List<EntrancePose> layout,
            int moduleIndex)
        {
            EntrancePose module = layout[moduleIndex];
            Quaternion inverseRotation =
                Quaternion.Inverse(Quaternion.Euler(0f, module.Yaw, 0f));
            List<Bounds> bounds = new List<Bounds>(layout.Count - 1);

            for (int otherIndex = 0;
                 otherIndex < layout.Count;
                 otherIndex++)
            {
                if (otherIndex == moduleIndex)
                    continue;

                EntrancePose other = layout[otherIndex];
                Quaternion otherRotation =
                    Quaternion.Euler(0f, other.Yaw, 0f);
                Bounds localBounds = new Bounds();
                bool initialized = false;
                for (int corner = 0; corner < 4; corner++)
                {
                    Vector3 otherLocal = new Vector3(
                        (corner & 1) == 0
                            ? -PanelkaModuleWidth * 0.5f
                            : PanelkaModuleWidth * 0.5f,
                        0f,
                        (corner & 2) == 0
                            ? -PanelkaModuleDepth * 0.5f
                            : PanelkaModuleDepth * 0.5f);
                    Vector3 sitePoint =
                        other.Position + otherRotation * otherLocal;
                    Vector3 moduleLocal =
                        inverseRotation * (sitePoint - module.Position);
                    if (!initialized)
                    {
                        localBounds = new Bounds(
                            moduleLocal,
                            Vector3.zero);
                        initialized = true;
                    }
                    else
                    {
                        localBounds.Encapsulate(moduleLocal);
                    }
                }

                localBounds.Expand(new Vector3(0.20f, 0f, 0.20f));
                bounds.Add(localBounds);
            }

            return bounds.ToArray();
        }

        private void BuildSmallHouse(int houseIndex, float routeT, float side, float roadOffset)
        {
            GetRoadPose(routeT, side, roadOffset, out Vector3 position, out Quaternion rotation);
            Transform house = Group("Small_House_" + (houseIndex + 1), root);
            house.position = position;
            house.rotation = rotation;
            MiniVanGameModePlacementMarker marker = house.gameObject.AddComponent<MiniVanGameModePlacementMarker>();
            marker.Kind = MiniVanGameModePlacementKind.SmallHouse;
            marker.SiteIndex = 100 + houseIndex;
            ClearTrees(house, new Vector2(15f, 15f));

            CreateBox("Floor", house, new Vector3(0f, 0f, 0f), new Vector3(8f, 0.25f, 7f), FloorMaterial, true);
            CreateBox("Back Wall", house, new Vector3(0f, 1.6f, 3.4f), new Vector3(8f, 3.2f, 0.3f), HouseMaterial, true);
            CreateBox("Left Wall", house, new Vector3(-3.85f, 1.6f, 0f), new Vector3(0.3f, 3.2f, 7f), HouseMaterial, true);
            CreateBox("Right Wall", house, new Vector3(3.85f, 1.6f, 0f), new Vector3(0.3f, 3.2f, 7f), HouseMaterial, true);
            CreateBox("Front Left", house, new Vector3(-2.55f, 1.6f, -3.4f), new Vector3(2.9f, 3.2f, 0.3f), HouseMaterial, true);
            CreateBox("Front Right", house, new Vector3(2.55f, 1.6f, -3.4f), new Vector3(2.9f, 3.2f, 0.3f), HouseMaterial, true);
            CreateBox("Door Lintel", house, new Vector3(0f, 2.85f, -3.4f), new Vector3(2.2f, 0.7f, 0.3f), HouseMaterial, true);
            CreateBox("Roof", house, new Vector3(0f, 3.35f, 0f), new Vector3(8.5f, 0.35f, 7.5f), RoofMaterial, true);
            BuildCrate(house, new Vector3(0f, 0.75f, 0.8f), houseIndex);
            CreateSpawnPoint(house, "House Exterior Spawn", new Vector3(0f, 0f, 12f), 100 + houseIndex);
        }

        private void BuildCrate(Transform parent, Vector3 localPosition, int index)
        {
            GameObject crate = new GameObject("Destructible Crate " + (index + 1));
            crate.transform.SetParent(parent, false);
            crate.transform.localPosition = localPosition;
            BoxCollider solid = crate.AddComponent<BoxCollider>();
            solid.size = new Vector3(1.35f, 1.35f, 1.35f);

            GameObject visual = CreateBox("Crate Visual", crate.transform, Vector3.zero,
                new Vector3(1.35f, 1.35f, 1.35f), CrateMaterial, false);
            GameObject coin = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            coin.name = "Coin $5-10";
            coin.transform.SetParent(crate.transform, false);
            coin.transform.localPosition = new Vector3(0f, -0.35f, 0f);
            coin.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            coin.transform.localScale = new Vector3(0.42f, 0.08f, 0.42f);
            coin.GetComponent<Renderer>().sharedMaterial = CoinMaterial;
            Collider coinCollider = coin.GetComponent<Collider>();
            coinCollider.isTrigger = true;
            Rigidbody coinBody = coin.AddComponent<Rigidbody>();
            coinBody.isKinematic = true;
            coinBody.useGravity = false;
            coin.SetActive(false);

            MiniVanDestructibleCrate component = crate.AddComponent<MiniVanDestructibleCrate>();
            component.CrateId = index;
            component.CoinValue = Random.Range(5, 11);
            component.CrateVisual = visual;
            component.SolidCollider = solid;
            component.CoinVisual = coin;
            MiniVanCoinPickup coinPickup = coin.AddComponent<MiniVanCoinPickup>();
            coinPickup.Owner = component;
        }

        private void BuildStartCompound()
        {
            if (StartCompoundPrefab != null)
            {
                Transform placed = PlaceCompoundPrefab(StartCompoundPrefab, "START_COMPOUND_FUNCTIONAL",
                    MapGenerator.StartPosition, false);
                EnsureStartWeapons(placed);
                MiniVanGameModeInteriorZone.EnsureZombieSafeZone(placed, 100);
                return;
            }

            Transform compound = Group("START_COMPOUND_FUNCTIONAL", root);
            compound.position = new Vector3(MapGenerator.StartPosition.x,
                SampleHeight(MapGenerator.StartPosition) + 0.38f, MapGenerator.StartPosition.z);
            compound.rotation = GetZoneRotation(false);
            ClearTrees(compound, new Vector2(54f, 46f));

            BuildCompoundPerimeter(compound, 17f, "Start");
            MiniVanGameModeGateController gate = BuildSlidingGate(compound,
                "Start Gate - Lever Controlled", 0, 17f, MiniVanGameModeGateKind.StartLever, 0f);
            BuildGateLever(compound, gate, new Vector3(7.1f, 0f, 14.6f));

            BuildGuardBooth(compound, new Vector3(-15.2f, 0f, -8.8f));
            BuildParkingMark(compound, new Vector3(-7f, 0.19f, 1.5f), "P1");
            BuildParkingMark(compound, new Vector3(7f, 0.19f, 1.5f), "P2");
            BuildParkingMark(compound, new Vector3(-7f, 0.19f, 9f), "P3");
            BuildParkingMark(compound, new Vector3(7f, 0.19f, 9f), "P4");

            BuildSupplyStack(compound, new Vector3(16.5f, 0f, -9.5f), 3);
            BuildSupplyStack(compound, new Vector3(-16.2f, 0f, 8.5f), 2);
            BuildBarrel(compound, new Vector3(18.2f, 0f, 9.6f), MetalMaterial);
            BuildWorldText(compound, "START", new Vector3(0f, 4.2f, -16.65f),
                Quaternion.Euler(0f, 0f, 0f), 0.22f, Color.white);
            BuildStartBat(compound);
            BuildStartTestBaton(compound);
            MiniVanGameModeInteriorZone.EnsureZombieSafeZone(compound, 100);
        }

        private void BuildStartBat(Transform compound)
        {
            if (BatPickupPrefab == null || FindNamedChild(compound, "START BAT PICKUP") != null)
            {
                return;
            }

            GameObject bat = Instantiate(BatPickupPrefab, compound, false);
            bat.name = "START BAT PICKUP";
            bat.transform.localPosition = new Vector3(-12.2f, 1.05f, -5.4f);
            bat.transform.localRotation = Quaternion.Euler(0f, 28f, 90f);
            bat.transform.localScale = Vector3.one;
        }

        private void BuildStartTestBaton(Transform compound)
        {
            if (BatPickupPrefab == null || FindNamedChild(compound, "START RED TEST BATON") != null)
            {
                return;
            }

            GameObject baton = Instantiate(BatPickupPrefab, compound, false);
            baton.name = "START RED TEST BATON";
            baton.transform.localPosition = new Vector3(-10.8f, 1.05f, -5.4f);
            baton.transform.localRotation = Quaternion.Euler(0f, -24f, 90f);
            baton.transform.localScale = Vector3.one;
            MiniVanBatPickup pickup = baton.GetComponent<MiniVanBatPickup>();
            if (pickup != null)
            {
                pickup.IsTestBaton = true;
                pickup.RefreshBatonAppearance();
            }
        }

        private void EnsureStartWeapons(Transform compound)
        {
            if (compound == null) return;
            BuildStartBat(compound);
            BuildStartTestBaton(compound);
        }

        private void EnsureSellerReviveStation(Transform saveZone)
        {
            if (saveZone == null) return;
            MiniVanShopCounter[] counters = saveZone.GetComponentsInChildren<MiniVanShopCounter>(true);
            for (int i = 0; i < counters.Length; i++)
            {
                if (counters[i] == null || counters[i].GetComponent<MiniVanReviveStation>() != null) continue;
                MiniVanReviveStation station = counters[i].gameObject.AddComponent<MiniVanReviveStation>();
                station.Kind = MiniVanReviveStationKind.CitySeller;
                station.Price = 100;
            }
        }

        private void BuildDoctorOffice(Transform entrance)
        {
            if (entrance == null || FindNamedChild(entrance, "DOCTOR OFFICE - BLUE") != null) return;
            MiniVanPanelkaRoomIdentity identity = FindDoctorRoomIdentity(entrance);
            if (identity == null) return;
            Transform furnishingRoom = identity.transform;
            MiniVanPanelkaApartmentRouteMarker apartmentMarker =
                furnishingRoom.GetComponentInParent<MiniVanPanelkaApartmentRouteMarker>();
            if (identity == null || apartmentMarker == null ||
                identity.RoomSizeLocal.x < 1.2f || identity.RoomSizeLocal.z < 1.2f)
            {
                Debug.LogWarning("[Panelka] Doctor room has no valid layout metadata; office skipped.", entrance);
                return;
            }

            Transform apartment = apartmentMarker.transform;
            float roomWidth = Mathf.Max(1.2f, identity.RoomSizeLocal.x);
            float roomDepth = Mathf.Max(1.2f, identity.RoomSizeLocal.z);
            furnishingRoom.gameObject.SetActive(false);

            Transform office = Group("DOCTOR OFFICE - BLUE", apartment);
            office.localPosition = identity.RoomCenterLocal;
            office.localRotation = Quaternion.identity;
            Material blue = CreateColoredMaterial(new Color(0.22f, 0.62f, 0.88f, 1f));
            Material paleBlue = CreateColoredMaterial(new Color(0.56f, 0.82f, 0.96f, 1f));
            Material green = CreateColoredMaterial(new Color(0.08f, 0.86f, 0.22f, 1f));

            CreateBox("Doctor Blue Floor", office, new Vector3(0f, 0.02f, 0f),
                new Vector3(roomWidth - 0.08f, 0.08f, roomDepth - 0.08f), blue, false);

            string tableEdge = ChooseDoctorTableEdge(identity, roomWidth, roomDepth);
            string[] wallEdges = { "bottom", "top", "left", "right" };
            int crossCount = 0;
            for (int i = 0; i < wallEdges.Length; i++)
            {
                string edge = wallEdges[i];
                if (identity.HasDoorOnEdge(edge)) continue;
                BuildDoctorWallPanel(office, edge, roomWidth, roomDepth, paleBlue);
                if (crossCount < 2)
                {
                    BuildDoctorWallCross(office, edge, roomWidth, roomDepth, green);
                    crossCount++;
                }
            }

            Transform table = Group("DOCTOR REVIVE TABLE", office);
            PositionDoctorTable(table, tableEdge, roomWidth, roomDepth);
            float tableSpan = tableEdge == "left" || tableEdge == "right" ? roomDepth : roomWidth;
            float tableLength = Mathf.Min(2.45f, Mathf.Max(1.15f, tableSpan - 0.65f));
            float tableLegOffset = Mathf.Max(0.34f, tableLength * 0.34f);
            GameObject tableTop = CreateBox("Medical Table Top", table, new Vector3(0f, 0.82f, 0f), new Vector3(tableLength, 0.18f, 0.92f), PaperMaterial, true);
            CreateBox("Medical Table Leg A", table, new Vector3(-tableLegOffset, 0.4f, 0f), new Vector3(0.16f, 0.8f, 0.65f), MetalMaterial, true);
            CreateBox("Medical Table Leg B", table, new Vector3(tableLegOffset, 0.4f, 0f), new Vector3(0.16f, 0.8f, 0.65f), MetalMaterial, true);
            Transform bodyPoint = Group("Body Point", table);
            bodyPoint.localPosition = new Vector3(0f, 1.02f, 0f);
            bodyPoint.localRotation = Quaternion.Euler(0f, 0f, 90f);
            MiniVanReviveStation revive = tableTop.AddComponent<MiniVanReviveStation>();
            revive.Kind = MiniVanReviveStationKind.DoctorTable;
            revive.Price = 0;
            revive.BodyPoint = bodyPoint;

        }

        private static MiniVanPanelkaRoomIdentity FindDoctorRoomIdentity(Transform entrance)
        {
            MiniVanPanelkaRoomIdentity[] rooms =
                entrance.GetComponentsInChildren<MiniVanPanelkaRoomIdentity>(true);
            MiniVanPanelkaRoomIdentity bathroom = null;
            for (int i = 0; i < rooms.Length; i++)
            {
                MiniVanPanelkaRoomIdentity room = rooms[i];
                if (room == null || room.RoomSizeLocal.x < 1.2f ||
                    room.RoomSizeLocal.z < 1.2f)
                {
                    continue;
                }

                if (string.Equals(room.RoomId, "STORAGE", System.StringComparison.OrdinalIgnoreCase))
                    return room;
                if (string.Equals(room.RoomId, "BATH", System.StringComparison.OrdinalIgnoreCase))
                    bathroom = room;
            }

            return bathroom;
        }

        private static string ChooseDoctorTableEdge(MiniVanPanelkaRoomIdentity identity,
            float roomWidth, float roomDepth)
        {
            string[] preferred = { "top", "bottom", "right", "left" };
            if (identity.DoorEdges != null && identity.DoorEdges.Length > 0)
            {
                switch (identity.DoorEdges[0])
                {
                    case "bottom": preferred[0] = "top"; break;
                    case "top": preferred[0] = "bottom"; break;
                    case "left": preferred[0] = "right"; break;
                    case "right": preferred[0] = "left"; break;
                }
            }

            string best = "top";
            float bestSpan = -1f;
            for (int i = 0; i < preferred.Length; i++)
            {
                string edge = preferred[i];
                if (identity.HasDoorOnEdge(edge)) continue;
                float span = edge == "left" || edge == "right" ? roomDepth : roomWidth;
                if (i == 0 || span > bestSpan)
                {
                    best = edge;
                    bestSpan = span;
                    if (i == 0) break;
                }
            }
            return best;
        }

        private static void PositionDoctorTable(Transform table, string edge,
            float roomWidth, float roomDepth)
        {
            const float wallGap = 0.14f;
            const float halfTableDepth = 0.46f;
            switch (edge)
            {
                case "bottom":
                    table.localPosition = new Vector3(0f, 0f, -roomDepth * 0.5f + halfTableDepth + wallGap);
                    table.localRotation = Quaternion.Euler(0f, 180f, 0f);
                    break;
                case "left":
                    table.localPosition = new Vector3(-roomWidth * 0.5f + halfTableDepth + wallGap, 0f, 0f);
                    table.localRotation = Quaternion.Euler(0f, -90f, 0f);
                    break;
                case "right":
                    table.localPosition = new Vector3(roomWidth * 0.5f - halfTableDepth - wallGap, 0f, 0f);
                    table.localRotation = Quaternion.Euler(0f, 90f, 0f);
                    break;
                default:
                    table.localPosition = new Vector3(0f, 0f, roomDepth * 0.5f - halfTableDepth - wallGap);
                    table.localRotation = Quaternion.identity;
                    break;
            }
        }

        private static void BuildDoctorWallPanel(Transform office, string edge,
            float roomWidth, float roomDepth, Material material)
        {
            if (edge == "bottom" || edge == "top")
            {
                float z = (edge == "top" ? 1f : -1f) * (roomDepth * 0.5f - 0.025f);
                CreateBox("Doctor Blue Wall " + edge, office, new Vector3(0f, 1.4f, z),
                    new Vector3(roomWidth, 2.8f, 0.05f), material, false);
            }
            else
            {
                float x = (edge == "right" ? 1f : -1f) * (roomWidth * 0.5f - 0.025f);
                CreateBox("Doctor Blue Wall " + edge, office, new Vector3(x, 1.4f, 0f),
                    new Vector3(0.05f, 2.8f, roomDepth), material, false);
            }
        }

        private static void BuildDoctorWallCross(Transform office, string edge,
            float roomWidth, float roomDepth, Material material)
        {
            Vector3 position;
            Quaternion rotation;
            switch (edge)
            {
                case "bottom":
                    position = new Vector3(0f, 1.65f, -roomDepth * 0.5f + 0.055f);
                    rotation = Quaternion.Euler(0f, 180f, 0f);
                    break;
                case "left":
                    position = new Vector3(-roomWidth * 0.5f + 0.055f, 1.65f, 0f);
                    rotation = Quaternion.Euler(0f, -90f, 0f);
                    break;
                case "right":
                    position = new Vector3(roomWidth * 0.5f - 0.055f, 1.65f, 0f);
                    rotation = Quaternion.Euler(0f, 90f, 0f);
                    break;
                default:
                    position = new Vector3(0f, 1.65f, roomDepth * 0.5f - 0.055f);
                    rotation = Quaternion.identity;
                    break;
            }
            BuildGreenCross(office, position, rotation, material);
        }

        private static void BuildGreenCross(Transform parent, Vector3 position, Quaternion rotation, Material material)
        {
            Transform cross = Group("GREEN MEDICAL CROSS", parent);
            cross.localPosition = position;
            cross.localRotation = rotation;
            CreateBox("Cross Vertical", cross, Vector3.zero, new Vector3(0.28f, 1.15f, 0.08f), material, false);
            CreateBox("Cross Horizontal", cross, Vector3.zero, new Vector3(1.15f, 0.28f, 0.08f), material, false);
        }

        private static Transform FindNamedChild(Transform rootTransform, string childName)
        {
            if (rootTransform == null) return null;
            Transform[] children = rootTransform.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i] != null && children[i].name == childName) return children[i];
            }
            return null;
        }

        private static Material CreateColoredMaterial(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            Material material = new Material(shader);
            material.color = color;
            return material;
        }

        private void BuildSaveZone()
        {
            if (SaveZonePrefab != null)
            {
                Transform placed = PlaceCompoundPrefab(SaveZonePrefab, "SAVE_ZONE_FUNCTIONAL",
                    MapGenerator.SavePosition, true);
                EnsureSellerReviveStation(placed);
                MiniVanGameModeInteriorZone.EnsureZombieSafeZone(placed, 200);
                return;
            }

            Transform zone = Group("SAVE_ZONE_FUNCTIONAL", root);
            zone.position = new Vector3(MapGenerator.SavePosition.x,
                SampleHeight(MapGenerator.SavePosition) + 0.38f, MapGenerator.SavePosition.z);
            zone.rotation = GetZoneRotation(true);
            ClearTrees(zone, new Vector2(58f, 48f));

            MiniVanGameModePlacementMarker marker =
                zone.gameObject.AddComponent<MiniVanGameModePlacementMarker>();
            marker.Kind = MiniVanGameModePlacementKind.SaveShop;
            marker.SiteIndex = 200;

            BuildCompoundPerimeter(zone, -17f, "Save");
            BuildSlidingGate(zone, "Save Gate - Horn 20 Seconds", 1, -17f,
                MiniVanGameModeGateKind.SaveHorn, 20f);
            BuildSaveShop(zone);
            BuildParkingMark(zone, new Vector3(11.5f, 0.19f, 6f), "P");
            BuildWatchTower(zone, new Vector3(-19.2f, 0f, -13.5f));
            BuildWatchTower(zone, new Vector3(19.2f, 0f, -13.5f));
            BuildSupplyStack(zone, new Vector3(17f, 0f, 11.5f), 3);
            BuildBarrel(zone, new Vector3(18.7f, 0f, 6f), MetalMaterial);
            BuildWorldText(zone, "SAFE ZONE", new Vector3(0f, 4.25f, 16.65f),
                Quaternion.Euler(0f, 180f, 0f), 0.2f, Color.green);
            BuildWorldText(zone, "HONK", new Vector3(-7.2f, 2.7f, -16.7f),
                Quaternion.identity, 0.14f, Color.green);

            MiniVanGameModeInteriorZone.EnsureZombieSafeZone(zone, 200);

            GameObject safeVolume = new GameObject("Safe Zone Spawn Exclusion");
            safeVolume.transform.SetParent(zone, false);
            safeVolume.transform.localPosition = new Vector3(0f, 2.2f, 0f);
            BoxCollider safeCollider = safeVolume.AddComponent<BoxCollider>();
            safeCollider.isTrigger = true;
            safeCollider.size = new Vector3(43f, 5f, 33f);
            MiniVanGameModeInteriorZone safeZone =
                safeVolume.AddComponent<MiniVanGameModeInteriorZone>();
            safeZone.SiteIndex = 200;
        }

        private Transform PlaceCompoundPrefab(GameObject prefab, string instanceName,
            Vector3 mapPosition, bool saveZone)
        {
            GameObject instance = Instantiate(prefab, root, false);
            instance.name = instanceName;
            instance.transform.position = new Vector3(mapPosition.x,
                SampleHeight(mapPosition) + 0.38f, mapPosition.z);
            instance.transform.rotation = GetZoneRotation(saveZone);
            instance.transform.localScale = Vector3.one;
            return instance.transform;
        }

        private void BuildCompoundPerimeter(Transform parent, float gateZ, string prefix)
        {
            const float halfWidth = 22f;
            const float halfDepth = 17f;
            const float wallHeight = 4f;
            const float gateHalfWidth = 5.2f;
            float gateSign = Mathf.Sign(gateZ);
            float oppositeZ = -gateSign * halfDepth;
            const float sideSegmentWidth = 16.72f;
            float sideCenter = gateHalfWidth + sideSegmentWidth * 0.5f;

            CreateBox(prefix + " Wall Left", parent, new Vector3(-halfWidth, wallHeight * 0.5f, 0f),
                new Vector3(0.65f, wallHeight, halfDepth * 2f), ExteriorMaterial, true);
            CreateBox(prefix + " Wall Right", parent, new Vector3(halfWidth, wallHeight * 0.5f, 0f),
                new Vector3(0.65f, wallHeight, halfDepth * 2f), ExteriorMaterial, true);
            CreateBox(prefix + " Wall Back", parent, new Vector3(0f, wallHeight * 0.5f, oppositeZ),
                new Vector3(halfWidth * 2f, wallHeight, 0.65f), ExteriorMaterial, true);
            CreateBox(prefix + " Gate Wall Left", parent,
                new Vector3(-sideCenter, wallHeight * 0.5f, gateZ),
                new Vector3(sideSegmentWidth, wallHeight, 0.65f), ExteriorMaterial, true);
            CreateBox(prefix + " Gate Wall Right", parent,
                new Vector3(sideCenter, wallHeight * 0.5f, gateZ),
                new Vector3(sideSegmentWidth, wallHeight, 0.65f), ExteriorMaterial, true);
            CreateBox(prefix + " Gate Pillar Left", parent,
                new Vector3(-gateHalfWidth, 2.5f, gateZ), new Vector3(0.9f, 5f, 1.1f), ExteriorMaterial, true);
            CreateBox(prefix + " Gate Pillar Right", parent,
                new Vector3(gateHalfWidth, 2.5f, gateZ), new Vector3(0.9f, 5f, 1.1f), ExteriorMaterial, true);

            for (int i = -1; i <= 1; i += 2)
            {
                CreateBox(prefix + " Corrugated Side " + i, parent,
                    new Vector3(i * (halfWidth + 0.02f), 2f, 7.5f),
                    new Vector3(0.7f, 3.2f, 4.2f), MetalMaterial, false);
                CreateBox(prefix + " Corrugated Back " + i, parent,
                    new Vector3(i * 13.5f, 2f, oppositeZ + gateSign * 0.02f),
                    new Vector3(4.2f, 3.2f, 0.7f), MetalMaterial, false);
            }
        }

        private MiniVanGameModeGateController BuildSlidingGate(Transform parent, string name,
            int gateId, float gateZ, MiniVanGameModeGateKind kind, float autoCloseSeconds)
        {
            Transform gateRoot = Group(name, parent);
            gateRoot.localPosition = new Vector3(0f, 0f, gateZ);
            Transform left = Group("Left Gate Leaf", gateRoot);
            Transform right = Group("Right Gate Leaf", gateRoot);
            left.localPosition = new Vector3(-2.55f, 2f, 0f);
            right.localPosition = new Vector3(2.55f, 2f, 0f);
            BuildGateLeaf(left, "Left");
            BuildGateLeaf(right, "Right");

            MiniVanGameModeGateController controller =
                gateRoot.gameObject.AddComponent<MiniVanGameModeGateController>();
            controller.GateId = gateId;
            controller.Kind = kind;
            controller.LeftLeaf = left;
            controller.RightLeaf = right;
            controller.LeftClosedPosition = left.localPosition;
            controller.RightClosedPosition = right.localPosition;
            controller.LeftOpenPosition = new Vector3(-7.8f, 2f, 0f);
            controller.RightOpenPosition = new Vector3(7.8f, 2f, 0f);
            controller.AutoCloseSeconds = autoCloseSeconds;
            controller.MoveSeconds = 1.7f;
            controller.HornRadius = 27f;

            BuildGateLamp(gateRoot, new Vector3(-5.25f, 4.25f, 0f), kind);
            BuildGateLamp(gateRoot, new Vector3(5.25f, 4.25f, 0f), kind);
            return controller;
        }

        private void BuildGateLeaf(Transform leaf, string side)
        {
            CreateBox(side + " Gate Panel", leaf, Vector3.zero,
                new Vector3(4.95f, 3.7f, 0.36f), MetalMaterial, true);
            CreateBox(side + " Gate Top Brace", leaf, new Vector3(0f, 1.55f, -0.22f),
                new Vector3(4.75f, 0.18f, 0.18f), DarkMaterial, false);
            CreateBox(side + " Gate Bottom Brace", leaf, new Vector3(0f, -1.55f, -0.22f),
                new Vector3(4.75f, 0.18f, 0.18f), DarkMaterial, false);
            CreateBox(side + " Gate Diagonal", leaf, new Vector3(0f, 0f, -0.23f),
                new Vector3(0.18f, 4.5f, 0.18f), DarkMaterial, false)
                .transform.localRotation = Quaternion.Euler(0f, 0f, side == "Left" ? -48f : 48f);
        }

        private void BuildGateLamp(Transform parent, Vector3 localPosition,
            MiniVanGameModeGateKind kind)
        {
            GameObject lamp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            lamp.name = kind == MiniVanGameModeGateKind.SaveHorn ? "Green Gate Lamp" : "Amber Gate Lamp";
            lamp.transform.SetParent(parent, false);
            lamp.transform.localPosition = localPosition;
            lamp.transform.localScale = Vector3.one * 0.34f;
            lamp.GetComponent<Renderer>().sharedMaterial = kind == MiniVanGameModeGateKind.SaveHorn
                ? MapGenerator.SaveMaterial
                : CoinMaterial;
            Collider collider = lamp.GetComponent<Collider>();
            if (Application.isPlaying) Destroy(collider); else DestroyImmediate(collider);
        }

        private void BuildGateLever(Transform parent, MiniVanGameModeGateController gate,
            Vector3 localPosition)
        {
            Transform leverRoot = Group("START GATE LEVER - INSIDE", parent);
            leverRoot.localPosition = localPosition;
            BoxCollider interactionVolume = leverRoot.gameObject.AddComponent<BoxCollider>();
            interactionVolume.center = new Vector3(0f, 1.35f, 0f);
            interactionVolume.size = new Vector3(2.25f, 2.7f, 2.1f);
            interactionVolume.isTrigger = true;
            CreateBox("Lever Pedestal", leverRoot, new Vector3(0f, 0.65f, 0f),
                new Vector3(1.1f, 1.3f, 0.9f), MetalMaterial, true);
            CreateBox("Hazard Stripe A", leverRoot, new Vector3(-0.28f, 0.72f, -0.46f),
                new Vector3(0.18f, 1.05f, 0.06f), CoinMaterial, false)
                .transform.localRotation = Quaternion.Euler(0f, 0f, 24f);
            CreateBox("Hazard Stripe B", leverRoot, new Vector3(0.28f, 0.72f, -0.46f),
                new Vector3(0.18f, 1.05f, 0.06f), CoinMaterial, false)
                .transform.localRotation = Quaternion.Euler(0f, 0f, 24f);

            Transform pivot = Group("Lever Handle Pivot", leverRoot);
            pivot.localPosition = new Vector3(0f, 1.25f, 0f);
            GameObject handle = CreateCylinder("Lever Handle", pivot, new Vector3(0f, 0.55f, 0f),
                new Vector3(0.11f, 0.55f, 0.11f), DarkMaterial, false);
            GameObject knob = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            knob.name = "Red Lever Knob";
            knob.transform.SetParent(pivot, false);
            knob.transform.localPosition = new Vector3(0f, 1.15f, 0f);
            knob.transform.localScale = Vector3.one * 0.34f;
            knob.GetComponent<Renderer>().sharedMaterial = FabricMaterial;
            Collider knobCollider = knob.GetComponent<Collider>();
            if (Application.isPlaying) Destroy(knobCollider); else DestroyImmediate(knobCollider);

            MiniVanGameModeGateLever lever =
                leverRoot.gameObject.AddComponent<MiniVanGameModeGateLever>();
            lever.Gate = gate;
            lever.Handle = pivot;
            gate.LeverPoint = leverRoot;
        }

        private void BuildGuardBooth(Transform parent, Vector3 localPosition)
        {
            Transform booth = Group("Start Guard Booth", parent);
            booth.localPosition = localPosition;
            CreateBox("Booth Body", booth, new Vector3(0f, 1.45f, 0f),
                new Vector3(5.2f, 2.9f, 4.2f), ShopMaterial, true);
            CreateBox("Booth Roof", booth, new Vector3(0f, 3.15f, 0f),
                new Vector3(5.7f, 0.35f, 4.7f), RoofMaterial, true);
            CreateBox("Booth Window", booth, new Vector3(0f, 1.85f, -2.13f),
                new Vector3(2.3f, 1.15f, 0.08f), OpaqueWindowMaterial, false);
        }

        private void BuildSaveShop(Transform parent)
        {
            Transform shop = Group("Save Zone Seller Shop", parent);
            shop.localPosition = new Vector3(-10.5f, 0f, 8.8f);
            CreateBox("Shop Back", shop, new Vector3(0f, 1.8f, 4f),
                new Vector3(14f, 3.6f, 0.4f), ShopMaterial, true);
            CreateBox("Shop Roof", shop, new Vector3(0f, 3.85f, 1.5f),
                new Vector3(14.5f, 0.35f, 5.5f), ShopMaterial, true);
            CreateBox("Shop Left", shop, new Vector3(-6.8f, 1.8f, 1.5f),
                new Vector3(0.4f, 3.6f, 5f), WoodMaterial, true);
            CreateBox("Shop Right", shop, new Vector3(6.8f, 1.8f, 1.5f),
                new Vector3(0.4f, 3.6f, 5f), WoodMaterial, true);

            GameObject counter = CreateBox("SELL COUNTER", shop, new Vector3(0f, 0.8f, -1f),
                new Vector3(10f, 1.6f, 1.2f), WoodMaterial, true);
            counter.AddComponent<MiniVanShopCounter>();
            MiniVanReviveStation revive = counter.AddComponent<MiniVanReviveStation>();
            revive.Kind = MiniVanReviveStationKind.CitySeller;
            revive.Price = 100;
            BuildSeller(shop, new Vector3(0f, 0f, 0.5f));
            BuildShelf(shop, new Vector3(-4.5f, 0f, 3.4f));
            BuildShelf(shop, new Vector3(4.5f, 0f, 3.4f));
            BuildWorldText(shop, "SELL", new Vector3(0f, 3.35f, -1.35f),
                Quaternion.identity, 0.16f, Color.green);
        }

        private void BuildSeller(Transform parent, Vector3 localPosition)
        {
            Transform seller = Group("Seller NPC", parent);
            seller.localPosition = localPosition;
            CreateCylinder("Seller Body", seller, new Vector3(0f, 1.25f, 0f),
                new Vector3(0.55f, 1.05f, 0.55f), FabricMaterial, false);
            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Seller Head";
            head.transform.SetParent(seller, false);
            head.transform.localPosition = new Vector3(0f, 2.65f, 0f);
            head.transform.localScale = new Vector3(0.85f, 0.95f, 0.85f);
            head.GetComponent<Renderer>().sharedMaterial = PaperMaterial;
            Collider collider = head.GetComponent<Collider>();
            if (Application.isPlaying) Destroy(collider); else DestroyImmediate(collider);
            CreateBox("Seller Cap", seller, new Vector3(0f, 3.08f, 0f),
                new Vector3(1.15f, 0.18f, 1.15f), ShopMaterial, false);
        }

        private void BuildShelf(Transform parent, Vector3 localPosition)
        {
            Transform shelf = Group("Shop Shelf", parent);
            shelf.localPosition = localPosition;
            CreateBox("Shelf Back", shelf, new Vector3(0f, 1.3f, 0.2f),
                new Vector3(2.8f, 2.6f, 0.22f), DarkMaterial, false);
            for (int level = 0; level < 3; level++)
            {
                CreateBox("Shelf Level " + level, shelf, new Vector3(0f, 0.35f + level * 0.85f, -0.25f),
                    new Vector3(2.8f, 0.16f, 0.9f), WoodMaterial, false);
            }
        }

        private void BuildParkingMark(Transform parent, Vector3 localPosition, string label)
        {
            Transform mark = Group("Parking " + label, parent);
            mark.localPosition = localPosition;
            CreateBox("Parking Left Line", mark, new Vector3(-2.2f, 0f, 0f),
                new Vector3(0.16f, 0.04f, 5f), PaperMaterial, false);
            CreateBox("Parking Right Line", mark, new Vector3(2.2f, 0f, 0f),
                new Vector3(0.16f, 0.04f, 5f), PaperMaterial, false);
            CreateBox("Parking End Line", mark, new Vector3(0f, 0f, 2.45f),
                new Vector3(4.5f, 0.04f, 0.16f), PaperMaterial, false);
            BuildWorldText(mark, label, new Vector3(0f, 0.035f, 0.3f),
                Quaternion.Euler(90f, 0f, 0f), 0.12f, Color.white);
        }

        private void BuildSupplyStack(Transform parent, Vector3 localPosition, int count)
        {
            Transform stack = Group("Supply Stack", parent);
            stack.localPosition = localPosition;
            for (int i = 0; i < count; i++)
            {
                CreateBox("Supply Crate " + i, stack,
                    new Vector3((i % 2) * 1.15f, 0.55f + (i / 2) * 1.05f, 0f),
                    new Vector3(1.05f, 1.05f, 1.05f), CrateMaterial, true);
            }
        }

        private void BuildBarrel(Transform parent, Vector3 localPosition, Material material)
        {
            CreateCylinder("Supply Barrel", parent, localPosition + Vector3.up * 0.75f,
                new Vector3(0.6f, 0.75f, 0.6f), material, true);
        }

        private void BuildWatchTower(Transform parent, Vector3 localPosition)
        {
            Transform tower = Group("Watch Tower", parent);
            tower.localPosition = localPosition;
            for (int x = -1; x <= 1; x += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    CreateBox("Tower Post", tower, new Vector3(x * 1.25f, 2.5f, z * 1.25f),
                        new Vector3(0.28f, 5f, 0.28f), WoodMaterial, true);
                }
            }
            CreateBox("Tower Deck", tower, new Vector3(0f, 4.6f, 0f),
                new Vector3(3.4f, 0.28f, 3.4f), WoodMaterial, true);
            CreateBox("Tower Roof", tower, new Vector3(0f, 6.25f, 0f),
                new Vector3(3.8f, 0.3f, 3.8f), RoofMaterial, true);
        }

        private Quaternion GetZoneRotation(bool saveZone)
        {
            IReadOnlyList<Vector3> road = MapGenerator.RoadSamples;
            Vector3 tangent = saveZone
                ? road[road.Count - 1] - road[Mathf.Max(0, road.Count - 3)]
                : road[Mathf.Min(2, road.Count - 1)] - road[0];
            tangent.y = 0f;
            return tangent.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(tangent.normalized, Vector3.up)
                : Quaternion.identity;
        }

        private static void BuildWorldText(Transform parent, string text, Vector3 localPosition,
            Quaternion localRotation, float characterSize, Color color)
        {
            GameObject textObject = new GameObject(text + " Sign");
            textObject.transform.SetParent(parent, false);
            textObject.transform.localPosition = localPosition;
            textObject.transform.localRotation = localRotation;
            TextMesh mesh = textObject.AddComponent<TextMesh>();
            mesh.text = text;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.fontSize = 64;
            mesh.characterSize = characterSize;
            mesh.color = color;
            MiniVanPanelkaWorldTextDepth depth =
                textObject.AddComponent<MiniVanPanelkaWorldTextDepth>();
            depth.ApplyNow();
        }

        private void EnsureGeneratedTextDepth()
        {
            if (root == null)
            {
                return;
            }

            Material depthMaterial = Resources.Load<Material>("Panelka_WorldTextDepth");
            TextMesh[] texts = root.GetComponentsInChildren<TextMesh>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                TextMesh text = texts[i];
                MeshRenderer renderer = text.GetComponent<MeshRenderer>();
                if (renderer != null && depthMaterial != null)
                {
                    renderer.sharedMaterial = depthMaterial;
                }

                MiniVanPanelkaWorldTextDepth depth =
                    text.GetComponent<MiniVanPanelkaWorldTextDepth>();
                if (depth == null)
                {
                    depth = text.gameObject.AddComponent<MiniVanPanelkaWorldTextDepth>();
                }
                depth.ApplyNow();
            }

            MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                MeshRenderer renderer = renderers[i];
                if (renderer == null || renderer.GetComponent<TextMesh>() != null ||
                    !HasTextMeshProComponent(renderer))
                {
                    continue;
                }

                MiniVanPanelkaWorldTextDepth depth =
                    renderer.GetComponent<MiniVanPanelkaWorldTextDepth>();
                if (depth == null)
                {
                    depth = renderer.gameObject.AddComponent<MiniVanPanelkaWorldTextDepth>();
                }
                depth.ApplyNow();
            }
        }

        private static bool HasTextMeshProComponent(Component component)
        {
            Component[] components = component.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                Component item = components[i];
                if (item == null)
                {
                    continue;
                }

                System.Type type = item.GetType();
                if (type.FullName != null &&
                    type.FullName.StartsWith("TMPro.", System.StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private void BuildRoadsideSpawnPoints()
        {
            float[] values = { 0.12f, 0.25f, 0.38f, 0.56f, 0.68f, 0.83f, 0.94f };
            for (int i = 0; i < values.Length; i++)
            {
                GetRoadPose(values[i], i % 2 == 0 ? 1f : -1f, 42f, out Vector3 position, out Quaternion rotation);
                Transform point = Group("Roadside_Exterior_Spawn_" + (i + 1), root);
                point.position = position;
                point.rotation = rotation;
                MiniVanGameModeSpawnPoint marker = point.gameObject.AddComponent<MiniVanGameModeSpawnPoint>();
                marker.SiteIndex = -1;
                marker.ExteriorOnly = true;
            }
        }

        private void BuildSystems()
        {
            GameObject interaction = new GameObject("Game Mode Interaction System");
            interaction.transform.SetParent(root, false);
            MiniVanGameModeInteractionSystem interactionSystem =
                interaction.AddComponent<MiniVanGameModeInteractionSystem>();
            interactionSystem.InteractionDistance = 5.5f;
            interactionSystem.DoorInteractionDistance = 1.1f;

            MiniVanGameModeRenderOptimizer optimizer =
                GetComponent<MiniVanGameModeRenderOptimizer>();
            if (optimizer == null)
            {
                optimizer = gameObject.AddComponent<MiniVanGameModeRenderOptimizer>();
            }

            optimizer.RefreshCullTargets();

            GameObject fpsCounter = new GameObject("FPS Counter");
            fpsCounter.transform.SetParent(root, false);
            fpsCounter.AddComponent<MiniVanFpsCounter>();

            GameObject directorObject = new GameObject("Threat Director - Normal Zombies");
            directorObject.transform.SetParent(root, false);
            MiniVanThreatDirector director = directorObject.AddComponent<MiniVanThreatDirector>();
            director.ZombiePrefab = ZombiePrefab;
            director.StartSafeCenter = MapGenerator.StartPosition;
            director.AllowOfflineSimulation = false;
        }

        private void CreateExteriorSpawnRing(Transform site, int siteIndex, float width, float depth)
        {
            float halfWidth = width * 0.5f + 18f;
            float halfDepth = depth * 0.5f + 12f;
            Vector3[] positions =
            {
                new Vector3(-halfWidth, 0f, -halfDepth), new Vector3(0f, 0f, -halfDepth),
                new Vector3(halfWidth, 0f, -halfDepth), new Vector3(-halfWidth, 0f, halfDepth),
                new Vector3(0f, 0f, halfDepth), new Vector3(halfWidth, 0f, halfDepth),
                new Vector3(-halfWidth, 0f, 0f), new Vector3(halfWidth, 0f, 0f)
            };
            for (int i = 0; i < positions.Length; i++)
            {
                CreateSpawnPoint(site, "Exterior Spawn " + (i + 1), positions[i], siteIndex);
            }
        }

        private void CreateSpawnPoint(Transform parent, string name, Vector3 localPosition, int siteIndex)
        {
            Transform point = Group(name, parent);
            point.localPosition = localPosition;
            Vector3 world = point.position;
            world.y = SampleHeight(world) + 0.1f;
            point.position = world;
            MiniVanGameModeSpawnPoint spawn = point.gameObject.AddComponent<MiniVanGameModeSpawnPoint>();
            spawn.SiteIndex = siteIndex;
            spawn.ExteriorOnly = true;
        }

        private void FindSafePanelkaPose(List<EntrancePose> layout, float routeT, float preferredSide,
            float preferredOffset, out Vector3 bestPosition, out Quaternion bestRotation)
        {
            bestPosition = Vector3.zero;
            bestRotation = Quaternion.identity;
            float bestScore = float.MaxValue;
            float[] routeOffsets = { 0f };

            for (int sidePass = 0; sidePass < 2; sidePass++)
            {
                float side = sidePass == 0 ? preferredSide : -preferredSide;
                for (int offsetStep = 0; offsetStep < 6; offsetStep++)
                {
                    float offset = Mathf.Max(preferredOffset, MapGenerator.RoadClearRadius + 30f) + offsetStep * 11f;
                    for (int routePass = 0; routePass < routeOffsets.Length; routePass++)
                    {
                        float candidateT = Mathf.Clamp(routeT + routeOffsets[routePass], 0.08f, 0.92f);
                        GetRoadPose(candidateT, side, offset, out Vector3 candidate, out Quaternion rotation);
                        float minRoadDistance = float.MaxValue;
                        float minHeight = float.MaxValue;
                        float maxHeight = float.MinValue;
                        float heightSum = 0f;
                        bool valid = true;

                        for (int i = 0; i < layout.Count; i++)
                        {
                            Vector3 world = candidate + rotation * layout[i].Position;
                            if (world.x < 24f || world.z < 24f ||
                                world.x > MapGenerator.MapSize - 24f || world.z > MapGenerator.MapLength - 24f)
                            {
                                valid = false;
                                break;
                            }

                            float roadDistance = DistanceToRoad(new Vector2(world.x, world.z));
                            minRoadDistance = Mathf.Min(minRoadDistance, roadDistance);
                            float height = SampleHeight(world);
                            minHeight = Mathf.Min(minHeight, height);
                            maxHeight = Mathf.Max(maxHeight, height);
                            heightSum += height;

                            for (int placed = 0; placed < placedPanelkaCenters.Count; placed++)
                            {
                                if (Vector2.Distance(new Vector2(world.x, world.z),
                                        new Vector2(placedPanelkaCenters[placed].x, placedPanelkaCenters[placed].z)) < 34f)
                                {
                                    valid = false;
                                    break;
                                }
                            }
                            if (!valid) break;
                        }

                        float requiredRoadDistance = MapGenerator.RoadWidth * 0.5f + 15f;
                        if (!valid || minRoadDistance < requiredRoadDistance)
                        {
                            continue;
                        }

                        float heightRange = maxHeight - minHeight;
                        float score = heightRange * 8f + offset * 0.18f +
                                      Mathf.Abs(routeOffsets[routePass]) * 80f + sidePass * 3f;
                        if (score >= bestScore)
                        {
                            continue;
                        }

                        bestScore = score;
                        candidate.y = heightSum / layout.Count + 0.25f;
                        bestPosition = candidate;
                        bestRotation = rotation;
                    }
                }
            }

            if (bestScore == float.MaxValue)
            {
                GetRoadPose(routeT, preferredSide, preferredOffset + 35f, out bestPosition, out bestRotation);
            }
        }

        private float DistanceToRoad(Vector2 point)
        {
            float nearest = float.MaxValue;
            IReadOnlyList<Vector3> road = MapGenerator.RoadSamples;
            for (int i = 0; i < road.Count - 1; i++)
            {
                Vector2 a = new Vector2(road[i].x, road[i].z);
                Vector2 b = new Vector2(road[i + 1].x, road[i + 1].z);
                Vector2 segment = b - a;
                float lengthSquared = segment.sqrMagnitude;
                float t = lengthSquared > 0.0001f
                    ? Mathf.Clamp01(Vector2.Dot(point - a, segment) / lengthSquared)
                    : 0f;
                nearest = Mathf.Min(nearest, Vector2.Distance(point, a + segment * t));
            }
            return nearest;
        }

        private void ApplyPanelkaTerrainFootprints()
        {
            if (terrain == null || panelkaFootprints.Count == 0)
            {
                return;
            }

            TerrainData data = terrain.terrainData;
            int resolution = data.heightmapResolution;
            float[,] heights = data.GetHeights(0, 0, resolution, resolution);
            Vector3 terrainPosition = terrain.transform.position;
            Vector3 terrainSize = data.size;

            for (int zIndex = 0; zIndex < resolution; zIndex++)
            {
                float worldZ = terrainPosition.z + zIndex / (float)(resolution - 1) * terrainSize.z;
                for (int xIndex = 0; xIndex < resolution; xIndex++)
                {
                    float worldX = terrainPosition.x + xIndex / (float)(resolution - 1) * terrainSize.x;
                    Vector3 world = new Vector3(worldX, 0f, worldZ);
                    float blend = 0f;
                    float targetHeight = heights[zIndex, xIndex];

                    for (int i = 0; i < panelkaFootprints.Count; i++)
                    {
                        TerrainFootprint footprint = panelkaFootprints[i];
                        Vector3 local = Quaternion.Inverse(footprint.Rotation) * (world - footprint.Center);
                        float halfX = footprint.Size.x * 0.5f;
                        float halfZ = footprint.Size.y * 0.5f;
                        float outside = Mathf.Max(Mathf.Abs(local.x) - halfX, Mathf.Abs(local.z) - halfZ);
                        if (outside > 4f)
                        {
                            continue;
                        }

                        float footprintBlend = outside <= 0f
                            ? 1f
                            : 1f - Mathf.SmoothStep(0f, 1f, outside / 4f);
                        if (footprintBlend > blend)
                        {
                            blend = footprintBlend;
                            targetHeight = Mathf.Clamp01((footprint.Height - terrainPosition.y) / terrainSize.y);
                        }
                    }

                    if (blend > 0f)
                    {
                        heights[zIndex, xIndex] = Mathf.Lerp(heights[zIndex, xIndex], targetHeight, blend);
                    }
                }
            }

            data.SetHeights(0, 0, heights);
            terrain.Flush();
        }

        private void GetRoadPose(float routeT, float side, float roadOffset,
            out Vector3 position, out Quaternion rotation)
        {
            IReadOnlyList<Vector3> road = MapGenerator.RoadSamples;
            float totalLength = 0f;
            for (int i = 1; i < road.Count; i++)
            {
                totalLength += Vector3.Distance(road[i - 1], road[i]);
            }

            float targetDistance = Mathf.Clamp01(routeT) * totalLength;
            float traveled = 0f;
            int segment = 1;
            float segmentT = 0f;
            for (int i = 1; i < road.Count; i++)
            {
                float segmentLength = Vector3.Distance(road[i - 1], road[i]);
                if (traveled + segmentLength >= targetDistance || i == road.Count - 1)
                {
                    segment = i;
                    segmentT = segmentLength > 0.001f
                        ? Mathf.Clamp01((targetDistance - traveled) / segmentLength)
                        : 0f;
                    break;
                }
                traveled += segmentLength;
            }

            Vector3 routePosition = Vector3.Lerp(road[segment - 1], road[segment], segmentT);
            int tangentFrom = Mathf.Max(0, segment - 2);
            int tangentTo = Mathf.Min(road.Count - 1, segment + 1);
            Vector3 tangent = Vector3.ProjectOnPlane(
                road[tangentTo] - road[tangentFrom], Vector3.up).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, tangent).normalized;
            position = routePosition + right * side * roadOffset;
            position.y = SampleHeight(position) + 0.25f;
            rotation = Quaternion.LookRotation(right * side, Vector3.up);
        }

        private float SampleHeight(Vector3 position)
        {
            return terrain != null ? terrain.SampleHeight(position) + terrain.transform.position.y : position.y;
        }

        private void ClearTrees(Transform center, Vector2 size)
        {
            BoxCollider[] blockers = FindObjectsByType<BoxCollider>(FindObjectsSortMode.None);
            for (int i = blockers.Length - 1; i >= 0; i--)
            {
                BoxCollider blocker = blockers[i];
                if (blocker == null || !blocker.gameObject.name.StartsWith("Tree Blocker")) continue;
                Vector3 local = center.InverseTransformPoint(blocker.transform.position);
                if (Mathf.Abs(local.x) <= size.x * 0.5f && Mathf.Abs(local.z) <= size.y * 0.5f)
                {
                    if (Application.isPlaying) Destroy(blocker.gameObject);
                    else DestroyImmediate(blocker.gameObject);
                }
            }
        }

        private static Transform Group(string name, Transform parent)
        {
            GameObject group = new GameObject(name);
            group.transform.SetParent(parent, false);
            return group.transform;
        }

        private static GameObject CreateBox(string name, Transform parent, Vector3 localPosition,
            Vector3 localScale, Material material, bool collider)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = localPosition;
            box.transform.localScale = localScale;
            box.GetComponent<Renderer>().sharedMaterial = material;
            Collider boxCollider = box.GetComponent<Collider>();
            if (!collider && boxCollider != null)
            {
                if (Application.isPlaying) Destroy(boxCollider);
                else DestroyImmediate(boxCollider);
            }
            return box;
        }

        private static GameObject CreateCylinder(string name, Transform parent, Vector3 localPosition,
            Vector3 localScale, Material material, bool collider)
        {
            GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cylinder.name = name;
            cylinder.transform.SetParent(parent, false);
            cylinder.transform.localPosition = localPosition;
            cylinder.transform.localScale = localScale;
            cylinder.GetComponent<Renderer>().sharedMaterial = material;
            Collider cylinderCollider = cylinder.GetComponent<Collider>();
            if (!collider && cylinderCollider != null)
            {
                if (Application.isPlaying) Destroy(cylinderCollider);
                else DestroyImmediate(cylinderCollider);
            }
            return cylinder;
        }
    }
}
