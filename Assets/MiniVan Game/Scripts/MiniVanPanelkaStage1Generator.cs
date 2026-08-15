// Procedural panelka generation and spatial bake.
using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;

using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using Unity.AI.Navigation;

namespace MiniVanGame
{
    [ExecuteAlways]
    public sealed partial class MiniVanPanelkaStage1Generator : MonoBehaviour
    {
        private enum RouteTransitionType
        {
            None,
            Hole,
            Balcony,
            Pipe
        }

        [Header("Building")]
        [Min(1)] public int FloorCount = 9;
        public int GenerationSeed = 9137;
        [Tooltip("Generate the normal facade, then remove all playable interior content and lock its entrances.")]
        public bool ExteriorOnlyLocked;

        [Header("Player clearance")]
        [Min(0.2f)] public float PlayerRadius = 0.32f;
        [Min(1f)] public float PlayerHeight = 1.8f;
        [Min(0.9f)] public float DoorWidth = 1.2f;
        [Min(2f)] public float DoorHeight = 2.35f;
        [Min(1f)] public float CorridorWidthMultiplier = 1.15f;
        [Tooltip("0 = PlayerRadius*2+0.20. Affects route window glass and facade openings.")]
        [Min(0f)] public float MinRouteWindowClearWidth;

        [Header("Manual overrides")]
        [Tooltip("If length > 0, floor i uses ForcedLayoutSequence[i] (1..5) instead of seed shuffle.")]
        public int[] ForcedLayoutSequence;
        [Tooltip("Random keeps seed cycle. Other values force every floor transition.")]
        public MiniVanPanelkaForcedRouteTransition ForcedRouteTransition =
            MiniVanPanelkaForcedRouteTransition.Random;
        [Tooltip("How many apartments the route may stack in one vertical line before it has " +
                 "to leave to the landing. 2 = enter an apartment, drop one floor, then switch.")]
        [Min(2)] public int RouteVerticalApartmentsInSingleLine = 2;

        [Header("Route transition budgets (Random mode only)")]
        [Tooltip("Hole in the floor of the apartment above.")]
        public MiniVanPanelkaRouteTransitionBudget RouteHoleBudget =
            MiniVanPanelkaRouteTransitionBudget.Unlimited;
        [Tooltip("Drop from the balcony into the apartment below.")]
        public MiniVanPanelkaRouteTransitionBudget RouteBalconyBudget =
            MiniVanPanelkaRouteTransitionBudget.Unlimited;
        [Tooltip("Climb down the outer pipe into the neighbouring apartment.")]
        public MiniVanPanelkaRouteTransitionBudget RoutePipeBudget =
            MiniVanPanelkaRouteTransitionBudget.Unlimited;
        [Tooltip("Plain descent through the landing staircase.")]
        public MiniVanPanelkaRouteTransitionBudget RouteStairsBudget =
            MiniVanPanelkaRouteTransitionBudget.Unlimited;

        public bool FurnishLandingNotices = true;
        public bool FurnishLandingLamps = true;
        public bool ZombiesOnMainRouteOnly;
        public bool AllowOfflineZombieSpawn;

        [Header("Materials")]
        public Material ExteriorMaterial;
        public Material InteriorMaterial;
        public Material FloorMaterial;
        public Material DoorMaterial;
        [Tooltip("Apartment entrance leaves pick one of these per apartment, so the colour " +
                 "never hints at which flats can be entered.")]
        public Material[] ApartmentDoorMaterials;
        public Material GlassMaterial;
        public Material CrackedGlassMaterial;
        public Material MetalMaterial;
        public Material StairwellFloorMaterial;
        public Material StairwellWallMaterial;
        public Material StairwellLowerWallMaterial;
        public Material StairwellUpperWallMaterial;
        public Material StairwellCeilingMaterial;
        public Material StairwellDoorMaterial;
        [Header("Stage 2 test furnishing")]
        public bool FurnishTopFloor = true;
        [Tooltip("Furnish only apartments that belong to the generated playable route.")]
        public bool FurnishGeneratedRoute = true;
        [Tooltip("Decorate every stair landing, including floors without a furnished apartment.")]
        public bool FurnishAllLandings = true;
        [Tooltip("When GenerateOnStart is enabled, use a new shared session seed on every game launch.")]
        public bool RandomizeGenerationOnPlay = true;

        [Header("Panelka zombies")]
        public bool SpawnPanelkaZombies = true;
        [Min(0)] public int PanelkaZombieCount = 4;
        public GameObject ZombiePrefab;
        [Min(0.04f)] public float ZombieNavMeshVoxelSize = 0.12f;

        public Material FurnitureWoodMaterial;
        public Material FurnitureFabricMaterial;
        public Material FurnitureCarpetMaterial;
        public Material FurnitureMetalMaterial;
        public Material FurnitureCeramicMaterial;
        public Material FurniturePaperMaterial;
        public Material FurnitureDarkPlasticMaterial;
        public Material KeyMaterial;
        private Material runtimeKeyMaterial;


        public bool GenerateOnStart;

        private const string GeneratedRootName = "Generated_9_Floor_Building";
        private const string LegacyGeneratedRootName = "Generated_Stage1_OneFloor";
        private const float FloorTop = 0.2f;
        private const float FloorSlabThickness = 0.30f;
        private const float FloorSurfaceOffset = 0.10f;

        private const float StoreyHeight = 3.2f;
        private const float FacadeVerticalOverlap = 0.04f;
        private const float StairwellFacadeSeamOverlap = 0.28f;
        private const float WallThickness = 0.2f;
        private const float BuildingHalfWidth = 13f;
        private const float BuildingHalfDepth = 9f;
        private const float CoreHalfWidth = 4.2f;
        private const float HatchHalf = 0.8f;
        private const float HatchCenterZ = 6f;
        private const float BaseCorridorWidth = 1.36f;
        private int[] routeMainSlotByFloor;
        private int[] routeArrivalSlotByFloor;
        private int[] routeKeySlotByFloor;
        private RouteTransitionType[] routeTransitionByUpperFloor;
        private int[] activeLayoutSequence;
        private int routeHoleUpperFloorNumber = -1;
        [SerializeField, HideInInspector]
        private Bounds[] facadeOcclusionBounds = Array.Empty<Bounds>();


private IEnumerator Start()
        {
            if (!Application.isPlaying || !GenerateOnStart)
            {
                yield break;
            }

            if (RandomizeGenerationOnPlay)
            {
                bool networkLaunchExpected =
                    MiniVanLaunchState.PendingMode != MiniVanLaunchMode.None ||
                    MiniVanLaunchState.ActiveMode != MiniVanLaunchMode.None ||
                    !string.IsNullOrWhiteSpace(MiniVanLaunchState.LobbyId) ||
                    !string.IsNullOrWhiteSpace(MiniVanLaunchState.RoomName) ||
                    !string.IsNullOrWhiteSpace(MiniVanLaunchState.JoinCode) ||
                    !string.IsNullOrWhiteSpace(MiniVanLaunchState.LastJoinCode);

                if (networkLaunchExpected)
                {
                    float deadline = Time.realtimeSinceStartup + 10f;
                    while (MiniVanLaunchState.ActiveMode == MiniVanLaunchMode.None &&
                           Time.realtimeSinceStartup < deadline)
                    {
                        yield return null;
                    }
                }
                else
                {
                    yield return null;
                }

                GenerationSeed = ResolveSessionSeed();
            }

            Rebuild();
        }

        [ContextMenu("Rebuild 9 Floor Building")]
[ContextMenu("Rebuild 9 Floor Building")]
        public void Rebuild()
        {
            ClearGenerated();

            Transform root = Group(GeneratedRootName, transform);
            int floorCount = Mathf.Max(1, FloorCount);
            int[] layoutSequence = ResolveLayoutSequence(floorCount, GenerationSeed);
            activeLayoutSequence = layoutSequence;
            BuildRouteAccessPlan(floorCount, GenerationSeed);

            for (int floorIndex = 0; floorIndex < floorCount; floorIndex++)
            {
                BuildFloor(root, floorIndex, layoutSequence[floorIndex], floorCount);
            }

            BuildRoof(root, floorCount);
            BuildExteriorLadder(root, floorCount);
            ApplyFacadeOcclusion(root);
            if (ExteriorOnlyLocked)
            {
                ConvertToLockedExterior(root);
                return;
            }
            BuildTopHatchLadder(root, floorCount);
            ConfigurePanelkaNavigation(root);
            ConfigurePanelkaZombies(root);

            if (GetComponent<MiniVanPanelkaInteractionController>() == null)
            {
                gameObject.AddComponent<MiniVanPanelkaInteractionController>();
            }
            DisablePanelkaShadows(root);
        }

        public void ConfigureFacadeOcclusion(Bounds[] occlusionBounds)
        {
            facadeOcclusionBounds = occlusionBounds != null
                ? (Bounds[])occlusionBounds.Clone()
                : Array.Empty<Bounds>();
        }

        public bool IsFacadeDecorationOccluded(Vector3 localPoint)
        {
            return IsFacadeDecorationOccluded(
                new Bounds(localPoint, Vector3.zero));
        }

        public bool IsFacadeDecorationOccluded(Bounds localBounds)
        {
            for (int i = 0; i < facadeOcclusionBounds.Length; i++)
            {
                Bounds bounds = facadeOcclusionBounds[i];
                bool overlapsX =
                    localBounds.max.x >= bounds.min.x &&
                    localBounds.min.x <= bounds.max.x;
                bool overlapsZ =
                    localBounds.max.z >= bounds.min.z &&
                    localBounds.min.z <= bounds.max.z;
                if (overlapsX && overlapsZ)
                {
                    return true;
                }
            }

            return false;
        }

        [ContextMenu("Clear Generated Building")]
        public void ClearGenerated()
        {
            DestroyGeneratedChild(GeneratedRootName);
            DestroyGeneratedChild(LegacyGeneratedRootName);
        }

        private void DestroyGeneratedChild(string childName)
        {
            Transform old = transform.Find(childName);
            if (old == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(old.gameObject);
            }
            else
            {
                DestroyImmediate(old.gameObject);
            }
        }

        public int[] GetActiveLayoutSequence()
        {
            return activeLayoutSequence != null
                ? (int[])activeLayoutSequence.Clone()
                : Array.Empty<int>();
        }

        public MiniVanPanelkaForcedRouteTransition GetRouteTransitionMode(int upperFloorNumber)
        {
            RouteTransitionType type = GetRouteTransitionType(upperFloorNumber);
            switch (type)
            {
                case RouteTransitionType.Hole:
                    return MiniVanPanelkaForcedRouteTransition.Hole;
                case RouteTransitionType.Balcony:
                    return MiniVanPanelkaForcedRouteTransition.Balcony;
                case RouteTransitionType.Pipe:
                    return MiniVanPanelkaForcedRouteTransition.Pipe;
                default:
                    return MiniVanPanelkaForcedRouteTransition.Stairs;
            }
        }

        private int[] ResolveLayoutSequence(int floorCount, int seed)
        {
            if (ForcedLayoutSequence != null && ForcedLayoutSequence.Length > 0)
            {
                int[] forced = new int[floorCount];
                for (int i = 0; i < floorCount; i++)
                {
                    int raw = i < ForcedLayoutSequence.Length
                        ? ForcedLayoutSequence[i]
                        : ForcedLayoutSequence[ForcedLayoutSequence.Length - 1];
                    forced[i] = Mathf.Clamp(raw, 1, 5);
                }

                return forced;
            }

            return BuildLayoutSequence(floorCount, seed);
        }

        private static int[] BuildLayoutSequence(int floorCount, int seed)
        {
            int[] result = new int[floorCount];
            System.Random random = new System.Random(seed);
            List<int> requiredVariants = new List<int> { 1, 2, 3, 4, 5 };

            for (int i = requiredVariants.Count - 1; i > 0; i--)
            {
                int swapIndex = random.Next(i + 1);
                int value = requiredVariants[i];
                requiredVariants[i] = requiredVariants[swapIndex];
                requiredVariants[swapIndex] = value;
            }

            for (int floorIndex = 0; floorIndex < floorCount; floorIndex++)
            {
                result[floorIndex] = floorIndex < requiredVariants.Count
                    ? requiredVariants[floorIndex]
                    : random.Next(1, 6);
            }

            return result;
        }

private void BuildFloor(Transform root, int floorIndex, int layoutVariant, int floorCount)
        {
            float yBase = FloorTop + floorIndex * StoreyHeight;
            Transform floor = Group(
                "Floor_" + (floorIndex + 1).ToString("00") + "_Layout_" + layoutVariant,
                root);
            Transform structure = Group("Structure", floor);

            BuildCoreFloor(structure, floorIndex, yBase);
            BuildCoreWalls(structure, floorIndex, yBase, layoutVariant);
            BuildCoreCeiling(structure, floorIndex, floorCount, yBase);
            BuildLiftSegment(structure, floorIndex, yBase);

            if (floorIndex < floorCount - 1)
            {
                BuildStairToNextFloor(structure, floorIndex, yBase);
                if (ShouldBlockStairToNextFloor(floorIndex))
                {
                    BuildStairBlockage(structure, floorIndex, yBase);
                }
            }
            else
            {
                BuildTopFloorStairGuard(structure, yBase);
            }

            BuildApartment(structure, floorIndex, false, false, layoutVariant, yBase, "SW");
            BuildApartment(structure, floorIndex, false, true, layoutVariant, yBase, "NW");
            BuildApartment(structure, floorIndex, true, true, layoutVariant, yBase, "NE");
            BuildApartment(structure, floorIndex, true, false, layoutVariant, yBase, "SE");

            bool decorateLanding =
                FurnishAllLandings || (FurnishTopFloor && floorIndex == floorCount - 1);
            MiniVanPanelkaLandingFurnishing.Build(
                structure,
                floorIndex,
                yBase,
                PlayerRadius,
                FurnitureWoodMaterial != null ? FurnitureWoodMaterial : DoorMaterial,
                FurnitureMetalMaterial != null ? FurnitureMetalMaterial : MetalMaterial,
                FurniturePaperMaterial != null ? FurniturePaperMaterial : InteriorMaterial,
                FurnitureDarkPlasticMaterial != null ? FurnitureDarkPlasticMaterial : MetalMaterial,
                decorateLanding,
                FurnishLandingNotices,
                FurnishLandingLamps);
        }


private void BuildCoreFloor(Transform parent, int floorIndex, float yBase)
        {
            Material stairFloor = StairwellFloorMaterial != null
                ? StairwellFloorMaterial
                : FloorMaterial;
            float slabY = yBase + FloorSurfaceOffset - FloorSlabThickness * 0.5f;
            if (floorIndex == 0)
            {
                Box(
                    "Landing_Floor",
                    parent,
                    new Vector3(0f, slabY, 0f),
                    new Vector3(CoreHalfWidth * 2f, FloorSlabThickness, BuildingHalfDepth * 2f),
                    stairFloor);
                return;
            }

            const float openingMinX = -1.70f;
            const float openingMaxX = 1.70f;
            const float openingMinZ = 0.15f;
            const float openingMaxZ = 5.35f;

            Box(
                "Landing_Floor_West",
                parent,
                new Vector3((-CoreHalfWidth + openingMinX) * 0.5f, slabY, 0f),
                new Vector3(openingMinX + CoreHalfWidth, FloorSlabThickness, BuildingHalfDepth * 2f),
                stairFloor);
            Box(
                "Landing_Floor_East",
                parent,
                new Vector3((openingMaxX + CoreHalfWidth) * 0.5f, slabY, 0f),
                new Vector3(CoreHalfWidth - openingMaxX, FloorSlabThickness, BuildingHalfDepth * 2f),
                stairFloor);
            Box(
                "Landing_Floor_South",
                parent,
                new Vector3((openingMinX + openingMaxX) * 0.5f, slabY, (-BuildingHalfDepth + openingMinZ) * 0.5f),
                new Vector3(openingMaxX - openingMinX, FloorSlabThickness, openingMinZ + BuildingHalfDepth),
                stairFloor);
            Box(
                "Landing_Floor_North",
                parent,
                new Vector3((openingMinX + openingMaxX) * 0.5f, slabY, (openingMaxZ + BuildingHalfDepth) * 0.5f),
                new Vector3(openingMaxX - openingMinX, FloorSlabThickness, BuildingHalfDepth - openingMaxZ),
                stairFloor);
        }

private void BuildCoreWalls(
            Transform parent,
            int floorIndex,
            float yBase,
            int layoutVariant)
        {
            Material stairWall = StairwellWallMaterial != null
                ? StairwellWallMaterial
                : InteriorMaterial;
            Material stairLower = StairwellLowerWallMaterial != null
                ? StairwellLowerWallMaterial
                : stairWall;
            Material stairUpper = StairwellUpperWallMaterial != null
                ? StairwellUpperWallMaterial
                : stairWall;
            float[] apartmentDoorCenters =
            {
                GetApartmentEntryZ(false, layoutVariant),
                GetApartmentEntryZ(true, layoutVariant)
            };
            StairwellWallXWithOpenings(
                "Landing_WestWall",
                parent,
                -CoreHalfWidth,
                0f,
                BuildingHalfDepth * 2f,
                yBase,
                apartmentDoorCenters,
                stairLower,
                stairUpper);
            StairwellWallXWithOpenings(
                "Landing_EastWall",
                parent,
                CoreHalfWidth,
                0f,
                BuildingHalfDepth * 2f,
                yBase,
                apartmentDoorCenters,
                stairLower,
                stairUpper);

            if (floorIndex == 0)
            {
                StairwellWallZWithOpenings(
                    "Landing_SouthWall",
                    parent,
                    -BuildingHalfDepth,
                    0f,
                    CoreHalfWidth * 2f,
                    yBase,
                    new[] { 0f },
                    stairLower,
                    stairUpper);
                MiniVanPanelkaRoomDoor streetDoor = PlaceDoor(
                    "Street_Entrance_Door",
                    parent,
                    new Vector3(-DoorWidth * 0.5f, yBase, -BuildingHalfDepth + 0.03f),
                    false,
                    -80f);
                ApplyMaterialToHierarchy(
                    streetDoor.transform,
                    StairwellDoorMaterial != null ? StairwellDoorMaterial : DoorMaterial);
                FacadeCladdingZWithOpenings(
                    "Landing_SouthFacade_Cladding",
                parent,
                -BuildingHalfDepth,
                0f,
                CoreHalfWidth * 2f + StairwellFacadeSeamOverlap * 2f,
                yBase,
                new[] { 0f },
                ExteriorMaterial);
            }
            else
            {
                StairwellSolidWallZ(
                    "Landing_SouthWall",
                    parent,
                    -BuildingHalfDepth,
                    0f,
                    CoreHalfWidth * 2f,
                    yBase,
                    stairLower,
                    stairUpper);
                FacadeCladdingZWithOpenings(
                    "Landing_SouthFacade_Cladding",
                    parent,
                    -BuildingHalfDepth,
                    0f,
                    CoreHalfWidth * 2f + StairwellFacadeSeamOverlap * 2f,
                    yBase,
                    new float[0],
                    ExteriorMaterial);
            }

            StairwellSolidWallZ(
                "Landing_NorthWall",
                parent,
                BuildingHalfDepth,
                0f,
                CoreHalfWidth * 2f,
                yBase,
                stairLower,
                stairUpper);
            FacadeCladdingZWithOpenings(
                "Landing_NorthFacade_Cladding",
                parent,
                BuildingHalfDepth,
                0f,
                CoreHalfWidth * 2f + StairwellFacadeSeamOverlap * 2f,
                yBase,
                new float[0],
                ExteriorMaterial);
        }

        private void BuildCoreCeiling(
            Transform parent,
            int floorIndex,
            int floorCount,
            float yBase)
        {
            Material ceiling = StairwellCeilingMaterial != null
                ? StairwellCeilingMaterial
                : InteriorMaterial;
            float y = yBase + StoreyHeight - 0.025f;
            const float thickness = 0.05f;
            if (floorIndex >= floorCount - 1)
            {
                float hatchMinX = -HatchHalf;
                float hatchMaxX = HatchHalf;
                float hatchMinZ = HatchCenterZ - HatchHalf;
                float hatchMaxZ = HatchCenterZ + HatchHalf;
                Box("Landing_Ceiling_West_Of_Hatch", parent,
                    new Vector3((-CoreHalfWidth + hatchMinX) * 0.5f, y, 0f),
                    new Vector3(hatchMinX + CoreHalfWidth, thickness,
                        BuildingHalfDepth * 2f), ceiling);
                Box("Landing_Ceiling_East_Of_Hatch", parent,
                    new Vector3((hatchMaxX + CoreHalfWidth) * 0.5f, y, 0f),
                    new Vector3(CoreHalfWidth - hatchMaxX, thickness,
                        BuildingHalfDepth * 2f), ceiling);
                Box("Landing_Ceiling_South_Of_Hatch", parent,
                    new Vector3(0f, y, (-BuildingHalfDepth + hatchMinZ) * 0.5f),
                    new Vector3(hatchMaxX - hatchMinX, thickness,
                        hatchMinZ + BuildingHalfDepth), ceiling);
                Box("Landing_Ceiling_North_Of_Hatch", parent,
                    new Vector3(0f, y, (hatchMaxZ + BuildingHalfDepth) * 0.5f),
                    new Vector3(hatchMaxX - hatchMinX, thickness,
                        BuildingHalfDepth - hatchMaxZ), ceiling);
                return;
            }

            const float openingMinX = -1.70f;
            const float openingMaxX = 1.70f;
            const float openingMinZ = 0.15f;
            const float openingMaxZ = 5.35f;
            Box("Landing_Ceiling_West", parent,
                new Vector3((-CoreHalfWidth + openingMinX) * 0.5f, y, 0f),
                new Vector3(openingMinX + CoreHalfWidth, thickness, BuildingHalfDepth * 2f),
                ceiling);
            Box("Landing_Ceiling_East", parent,
                new Vector3((openingMaxX + CoreHalfWidth) * 0.5f, y, 0f),
                new Vector3(CoreHalfWidth - openingMaxX, thickness, BuildingHalfDepth * 2f),
                ceiling);
            Box("Landing_Ceiling_South", parent,
                new Vector3(0f, y, (-BuildingHalfDepth + openingMinZ) * 0.5f),
                new Vector3(openingMaxX - openingMinX, thickness,
                    openingMinZ + BuildingHalfDepth), ceiling);
            Box("Landing_Ceiling_North", parent,
                new Vector3(0f, y, (openingMaxZ + BuildingHalfDepth) * 0.5f),
                new Vector3(openingMaxX - openingMinX, thickness,
                    BuildingHalfDepth - openingMaxZ), ceiling);
        }

private void BuildLiftSegment(Transform parent, int floorIndex, float yBase)
        {
            Transform lift = Group("Lift_NonFunctional_" + (floorIndex + 1).ToString("00"), parent);
            const float liftX = 1.75f;
            const float liftZ = -4.35f;
            lift.localPosition = new Vector3(liftX, 0f, liftZ);
            lift.localRotation = Quaternion.Euler(0f, 180f, 0f);

            Box(
                "Lift_Back",
                lift,
                new Vector3(0f, yBase + StoreyHeight * 0.5f, 1.05f),
                new Vector3(1.8f, StoreyHeight, 0.18f),
                MetalMaterial);
            Box(
                "Lift_Left",
                lift,
                new Vector3(-0.9f, yBase + StoreyHeight * 0.5f, 0f),
                new Vector3(0.18f, StoreyHeight, 2.1f),
                MetalMaterial);
            Box(
                "Lift_Right",
                lift,
                new Vector3(0.9f, yBase + StoreyHeight * 0.5f, 0f),
                new Vector3(0.18f, StoreyHeight, 2.1f),
                MetalMaterial);
            Box(
                "Lift_Door",
                lift,
                new Vector3(0f, yBase + DoorHeight * 0.5f, -1.05f),
                new Vector3(1.65f, DoorHeight, 0.12f),
                StairwellDoorMaterial != null ? StairwellDoorMaterial : DoorMaterial);
            Box(
                "Lift_Lintel",
                lift,
                new Vector3(0f, yBase + DoorHeight + (StoreyHeight - DoorHeight) * 0.5f, -1.05f),
                new Vector3(1.8f, StoreyHeight - DoorHeight, 0.18f),
                MetalMaterial);
        }

private void BuildStairToNextFloor(Transform parent, int floorIndex, float yBase)
        {
            Material stairFloor = StairwellFloorMaterial != null
                ? StairwellFloorMaterial
                : FloorMaterial;
            Transform stairs = Group("Stair_To_Floor_" + (floorIndex + 2).ToString("00"), parent);
            if (!ShouldBlockStairToNextFloor(floorIndex))
            {
                Group(
                    "Route_Stair_Descent_From_Floor_" +
                    (floorIndex + 2).ToString("00") +
                    "_To_" +
                    (floorIndex + 1).ToString("00"),
                    stairs);
            }
            const int halfStepCount = 8;
            const float flightWidth = 1.42f;
            const float stepDepth = 0.46f;
            const float treadThickness = 0.20f;
            const float southZ = 0.45f;
            const float westX = -0.85f;
            const float eastX = 0.85f;
            const float midLandingZ = 4.52f;
            const float landingDepth = 1.30f;
            float stairBaseY = yBase + FloorSurfaceOffset;
            float stepRise = StoreyHeight / (halfStepCount * 2f);
            float northStepZ = southZ + (halfStepCount - 1) * stepDepth;

            for (int i = 0; i < halfStepCount; i++)
            {
                float lowerTop = (i + 1) * stepRise;
                float lowerZ = southZ + i * stepDepth;
                Box(
                    "Lower_Step_" + (i + 1).ToString("00"),
                    stairs,
                    new Vector3(westX, stairBaseY + lowerTop - treadThickness * 0.5f, lowerZ),
                    new Vector3(flightWidth, treadThickness, stepDepth + 0.02f),
                    stairFloor);

                float upperTop = StoreyHeight * 0.5f + (i + 1) * stepRise;
                float upperZ = northStepZ - i * stepDepth;
                Box(
                    "Upper_Step_" + (i + 1).ToString("00"),
                    stairs,
                    new Vector3(eastX, stairBaseY + upperTop - treadThickness * 0.5f, upperZ),
                    new Vector3(flightWidth, treadThickness, stepDepth + 0.02f),
                    stairFloor);

                if (i % 2 == 0 || i == halfStepCount - 1)
                {
                    Box(
                        "Lower_RailPost_" + i,
                        stairs,
                        new Vector3(westX - flightWidth * 0.5f, stairBaseY + lowerTop + 0.46f, lowerZ),
                        new Vector3(0.08f, 0.92f, 0.12f),
                        MetalMaterial);
                    Box(
                        "Upper_RailPost_" + i,
                        stairs,
                        new Vector3(eastX + flightWidth * 0.5f, stairBaseY + upperTop + 0.46f, upperZ),
                        new Vector3(0.08f, 0.92f, 0.12f),
                        MetalMaterial);
                }
            }

            Box(
                "Mid_Landing",
                stairs,
                new Vector3(0f, stairBaseY + StoreyHeight * 0.5f - treadThickness * 0.5f, midLandingZ),
                new Vector3(3.16f, treadThickness, landingDepth),
                stairFloor);
            Box(
                "Mid_Landing_North_Rail",
                stairs,
                new Vector3(0f, stairBaseY + StoreyHeight * 0.5f + 0.55f, midLandingZ + landingDepth * 0.5f),
                new Vector3(3.16f, 1.1f, 0.08f),
                MetalMaterial);
            Box(
                "Top_Landing",
                stairs,
                new Vector3(eastX, stairBaseY + StoreyHeight - treadThickness * 0.5f, -0.12f),
                new Vector3(flightWidth + 0.20f, treadThickness, 1.10f),
                stairFloor);

            if (!ShouldBlockStairToNextFloor(floorIndex))
            {
                AddStairNavLink(
                    stairs,
                    "NavLink_Lower_To_Mid",
                    new Vector3(westX, stairBaseY + 0.10f, southZ - 0.25f),
                    new Vector3(0f, stairBaseY + StoreyHeight * 0.5f + 0.10f, midLandingZ - 0.38f),
                    1.05f);
                AddStairNavLink(
                    stairs,
                    "NavLink_Mid_To_Upper",
                    new Vector3(0f, stairBaseY + StoreyHeight * 0.5f + 0.10f, midLandingZ - 0.38f),
                    new Vector3(eastX, stairBaseY + StoreyHeight + 0.10f, -0.12f),
                    1.05f);
            }
        }

        private void AddStairNavLink(
            Transform parent,
            string name,
            Vector3 startPoint,
            Vector3 endPoint,
            float width)
        {
            GameObject linkObject = new GameObject(name);
            linkObject.transform.SetParent(parent, false);

            NavMeshLink link = linkObject.AddComponent<NavMeshLink>();
            link.startPoint = startPoint;
            link.endPoint = endPoint;
            link.width = width;
            link.bidirectional = true;
        }

private void BuildStairBlockage(Transform parent, int floorIndex, float yBase)
        {
            Transform blockage = Group(
                "Route_Stair_Blockage_Between_Floor_" +
                (floorIndex + 1).ToString("00") + "_And_" +
                (floorIndex + 2).ToString("00"),
                parent);

            GameObject blocker = Box(
                "Rubble_Collision_Blocker",
                blockage,
                new Vector3(-1.0600f, yBase + 2.5480f, 1.5340f),
                new Vector3(1.4964f, 2.2725f, 2.0125f),
                ExteriorMaterial);
            blocker.transform.localRotation = Quaternion.Euler(359.527f, 28.334f, 344.737f);

            Vector3[] offsets =
            {
                new Vector3(-0.4415f, 1.5121f, 2.5601f),
                new Vector3(-1.1869f, 1.5174f, 2.1113f),
                new Vector3(-1.1460f, 1.9077f, 2.4876f),
                new Vector3(-0.7249f, 2.1217f, 2.2288f),
                new Vector3(-0.5751f, 2.0826f, 2.7842f),
                new Vector3(-0.3141f, 1.5174f, 2.6515f),
                new Vector3(-1.1147f, 2.1580f, 1.7685f),
                new Vector3(-0.4919f, 2.3075f, 1.9769f),
                new Vector3(-1.1624f, 1.7347f, 2.1054f),
                new Vector3(-1.4139f, 2.2197f, 2.6390f),
                new Vector3(-0.8406f, 2.3900f, 2.4041f),
                new Vector3(-0.3989f, 2.2059f, 2.5380f),
                new Vector3(-0.4634f, 1.5569f, 2.1040f),
                new Vector3(-1.2778f, 1.7224f, 1.9375f)
            };
            Vector3[] rotations =
            {
                new Vector3(14.347f, 125.397f, 349.105f),
                new Vector3(2.057f, 14.760f, 349.873f),
                new Vector3(355.146f, 114.194f, 14.792f),
                new Vector3(5.782f, 80.975f, 0.531f),
                new Vector3(5.979f, 118.091f, 343.170f),
                new Vector3(8.084f, 50.883f, 345.746f),
                new Vector3(359.396f, 55.171f, 17.975f),
                new Vector3(343.255f, 153.900f, 356.972f),
                new Vector3(15.055f, 84.809f, 357.138f),
                new Vector3(345.310f, 63.949f, 8.044f),
                new Vector3(4.125f, 162.503f, 11.162f),
                new Vector3(4.782f, 81.766f, 14.217f),
                new Vector3(15.617f, 71.608f, 1.107f),
                new Vector3(347.813f, 95.032f, 15.947f)
            };
            Vector3[] sizes =
            {
                new Vector3(0.4279f, 0.5166f, 0.4298f),
                new Vector3(0.3324f, 0.3204f, 0.4888f),
                new Vector3(0.3052f, 0.2805f, 0.6413f),
                new Vector3(0.4181f, 0.2239f, 0.3545f),
                new Vector3(0.5841f, 0.2819f, 0.5133f),
                new Vector3(0.5575f, 0.4850f, 0.2985f),
                new Vector3(0.5486f, 0.2409f, 0.4146f),
                new Vector3(0.3240f, 0.3681f, 0.6163f),
                new Vector3(0.3004f, 0.3424f, 0.4874f),
                new Vector3(0.4110f, 0.4245f, 0.5576f),
                new Vector3(0.4820f, 0.3434f, 0.6219f),
                new Vector3(0.5998f, 0.4544f, 0.2908f),
                new Vector3(0.4371f, 0.4816f, 0.5075f),
                new Vector3(0.4399f, 0.2312f, 0.3326f)
            };

            for (int i = 0; i < offsets.Length; i++)
            {
                Vector3 position = offsets[i];
                position.y += yBase;
                GameObject rock = Box(
                    "Concrete_Debris_" + (i + 1).ToString("00"),
                    blockage,
                    position,
                    sizes[i],
                    i % 3 == 0 ? FloorMaterial : ExteriorMaterial);
                rock.transform.localRotation = Quaternion.Euler(rotations[i]);
            }
        }

        
private void BuildTopFloorStairGuard(Transform parent, float yBase)
        {
            Transform guard = Group("Top_Floor_Stairwell_Guard", parent);
            const float openingMinX = -1.70f;
            const float openingMaxX = 1.70f;
            const float openingMinZ = 0.15f;
            const float openingMaxZ = 5.35f;
            float railY = yBase + FloorSurfaceOffset + 0.55f;
            float railCenterZ = (openingMinZ + openingMaxZ) * 0.5f;
            float railLengthZ = openingMaxZ - openingMinZ;

            Box(
                "Guard_West",
                guard,
                new Vector3(openingMinX, railY, railCenterZ),
                new Vector3(0.1f, 1.1f, railLengthZ),
                MetalMaterial);
            Box(
                "Guard_East",
                guard,
                new Vector3(openingMaxX, railY, railCenterZ),
                new Vector3(0.1f, 1.1f, railLengthZ),
                MetalMaterial);
            Box(
                "Guard_North",
                guard,
                new Vector3(0f, railY, openingMaxZ),
                new Vector3(openingMaxX - openingMinX, 1.1f, 0.1f),
                MetalMaterial);
        }
        private void BuildApartment(
            Transform parent,
            int floorIndex,
            bool right,
            bool north,
            int layoutVariant,
            float yBase,
            string cornerName)
        {
            BuildApartmentFromFullTemplate(
                parent,
                floorIndex,
                right,
                north,
                layoutVariant,
                yBase,
                cornerName);
        }

        private void AddFloorSlab(
            string name,
            Transform parent,
            float minX,
            float maxX,
            float minZ,
            float maxZ,
            float slabY)
        {
            if (maxX - minX < 0.04f || maxZ - minZ < 0.04f)
            {
                return;
            }

            Box(
                name,
                parent,
                new Vector3((minX + maxX) * 0.5f, slabY, (minZ + maxZ) * 0.5f),
                new Vector3(maxX - minX, FloorSlabThickness, maxZ - minZ),
                FloorMaterial);
        }

private void AddBrokenHoleEdge(
            Transform parent,
            Vector2 a,
            Vector2 b,
            float y,
            int index)
        {
            Vector2 delta = b - a;
            float length = delta.magnitude;
            if (length < 0.02f)
            {
                return;
            }

            Vector2 midpoint = (a + b) * 0.5f;
            GameObject edge = Box(
                "Broken_Polygon_Edge_" + index.ToString("00"),
                parent,
                new Vector3(midpoint.x, y, midpoint.y),
                new Vector3(length, 0.07f, 0.09f),
                ExteriorMaterial);
            edge.transform.localRotation = Quaternion.Euler(
                0f,
                -Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg,
                0f);
        }


        private bool BuildBalconyRoute(
            Transform apartment,
            MiniVanPanelkaApartmentTemplate template,
            int floorIndex,
            MiniVanPanelkaApartmentRouteRole routeRole,
            float outerX,
            float innerX,
            float outerZ,
            float yBase,
            out Vector3 openingCenter)
        {
            openingCenter = Vector3.zero;
            int floorNumber = floorIndex + 1;
            if (routeRole == MiniVanPanelkaApartmentRouteRole.MainRoute &&
                IsPipeRouteFloor(floorNumber))
            {
                return false;
            }
            bool isRouteBalcony =
                routeRole == MiniVanPanelkaApartmentRouteRole.MainRoute &&
                IsBalconyRouteFloor(floorNumber);
            bool isBalconyArrivalFloor =
                isRouteBalcony &&
                IsBalconyTransitionFromFloor(floorNumber + 1);
            if (isBalconyArrivalFloor)
            {
                // The upper route balcony owns the aligned landing and transfer pipe.
                return false;
            }

            Vector3 preferredWindow = template != null &&
                                      template.BalconySocket != null
                ? template.BalconySocket.position
                : apartment.TransformPoint(new Vector3(
                    (outerX + innerX) * 0.5f,
                    yBase + 1.5f,
                    outerZ));
            Transform selectedGlass =
                FindNearestFacadeGlass(
                    apartment,
                    preferredWindow,
                    true);
            if (selectedGlass == null)
            {
                return false;
            }

            openingCenter = apartment.InverseTransformPoint(selectedGlass.position);
            if (isRouteBalcony)
            {
                ConfigureCrackedRouteWindow(
                    selectedGlass,
                    "BALCONY_WINDOW_G" +
                    GenerationSeed.ToString() +
                    "_F" +
                    floorNumber.ToString("00") +
                    "_APT" +
                    (floorIndex * 4 +
                     GetApartmentSlotFromMarker(apartment) +
                     1).ToString("00"));
            }

            const float platformDepth = 1.60f;
            const float platformWidth = 2.85f;
            float platformY = yBase + FloorSurfaceOffset - 0.09f;
            float railY = yBase + FloorSurfaceOffset + 0.57f;
            bool hasHatch =
                isRouteBalcony &&
                IsBalconyTransitionFromFloor(floorNumber);

            Transform balcony = Group(
                (isRouteBalcony ? "Route_Balcony_" : "Apartment_Balcony_") +
                floorNumber.ToString("00"),
                apartment);
            bool onXFace =
                Mathf.Abs(Mathf.Abs(openingCenter.x) - BuildingHalfWidth) <=
                Mathf.Abs(Mathf.Abs(openingCenter.z) - BuildingHalfDepth);
            if (onXFace)
            {
                float sign = Mathf.Sign(openingCenter.x);
                balcony.localPosition = new Vector3(
                    sign * BuildingHalfWidth,
                    0f,
                    openingCenter.z);
                balcony.localRotation =
                    Quaternion.Euler(0f, sign > 0f ? 90f : -90f, 0f);
            }
            else
            {
                float sign = Mathf.Sign(openingCenter.z);
                balcony.localPosition = new Vector3(
                    openingCenter.x,
                    0f,
                    sign * BuildingHalfDepth);
                balcony.localRotation =
                    Quaternion.Euler(0f, sign > 0f ? 0f : 180f, 0f);
            }
            balcony.localScale = Vector3.one;

            float platformCenterZ = platformDepth * 0.5f;

            if (hasHatch)
            {
                float hatchX = platformWidth * 0.18f;
                float hatchZ = platformDepth * 0.55f;
                const float hatchWidth = 1.38f;
                const float hatchDepth = 1.32f;
                float xMin = -platformWidth * 0.5f;
                float xMax = platformWidth * 0.5f;
                float zMin = 0f;
                float zMax = platformDepth;
                float holeXMin = hatchX - hatchWidth * 0.5f;
                float holeXMax = hatchX + hatchWidth * 0.5f;
                float holeZMin = hatchZ - hatchDepth * 0.5f;
                float holeZMax = hatchZ + hatchDepth * 0.5f;

                Box(
                    "Platform_Left",
                    balcony,
                    new Vector3((xMin + holeXMin) * 0.5f, platformY, platformCenterZ),
                    new Vector3(holeXMin - xMin, 0.18f, platformDepth),
                    FloorMaterial);
                Box(
                    "Platform_Right",
                    balcony,
                    new Vector3((holeXMax + xMax) * 0.5f, platformY, platformCenterZ),
                    new Vector3(xMax - holeXMax, 0.18f, platformDepth),
                    FloorMaterial);
                Box(
                    "Platform_Hatch_Inner",
                    balcony,
                    new Vector3(hatchX, platformY, (zMin + holeZMin) * 0.5f),
                    new Vector3(hatchWidth, 0.18f, holeZMin - zMin),
                    FloorMaterial);
                Box(
                    "Platform_Hatch_Outer",
                    balcony,
                    new Vector3(hatchX, platformY, (holeZMax + zMax) * 0.5f),
                    new Vector3(hatchWidth, 0.18f, zMax - holeZMax),
                    FloorMaterial);

                Material frameMaterial = MetalMaterial != null ? MetalMaterial : DoorMaterial;
                Box(
                    "Balcony_Hatch_Frame_Left",
                    balcony,
                    new Vector3(holeXMin, platformY + 0.12f, hatchZ),
                    new Vector3(0.07f, 0.12f, hatchDepth + 0.10f),
                    frameMaterial);
                Box(
                    "Balcony_Hatch_Frame_Right",
                    balcony,
                    new Vector3(holeXMax, platformY + 0.12f, hatchZ),
                    new Vector3(0.07f, 0.12f, hatchDepth + 0.10f),
                    frameMaterial);
                Box(
                    "Balcony_Hatch_Frame_A",
                    balcony,
                    new Vector3(hatchX, platformY + 0.12f, holeZMin),
                    new Vector3(hatchWidth, 0.12f, 0.07f),
                    frameMaterial);
                Box(
                    "Balcony_Hatch_Frame_B",
                    balcony,
                    new Vector3(hatchX, platformY + 0.12f, holeZMax),
                    new Vector3(hatchWidth, 0.12f, 0.07f),
                    frameMaterial);

                GameObject lid = Box(
                    "Balcony_Hatch_Open_Lid",
                    balcony,
                    new Vector3(
                        hatchX,
                        platformY + hatchDepth * 0.46f,
                        holeZMax + 0.05f),
                    new Vector3(hatchWidth, 0.07f, hatchDepth),
                    frameMaterial);
                lid.transform.localRotation =
                    Quaternion.Euler(-82f, 0f, 0f);

                BuildBalconyReturnRope(
                    balcony,
                    yBase,
                    floorIndex,
                    platformDepth,
                    platformWidth);
            }
            else
            {
                Box(
                    "Platform",
                    balcony,
                    new Vector3(0f, platformY, platformCenterZ),
                    new Vector3(platformWidth, 0.18f, platformDepth),
                    FloorMaterial);
            }

            Box(
                "Outer_Rail",
                balcony,
                new Vector3(0f, railY, platformDepth),
                new Vector3(platformWidth, 1.05f, 0.09f),
                MetalMaterial);
            Box(
                "Side_Rail_A",
                balcony,
                new Vector3(
                    -platformWidth * 0.5f,
                    railY,
                    platformCenterZ),
                new Vector3(0.09f, 1.05f, platformDepth),
                MetalMaterial);
            Box(
                "Side_Rail_B",
                balcony,
                new Vector3(
                    platformWidth * 0.5f,
                    railY,
                    platformCenterZ),
                new Vector3(0.09f, 1.05f, platformDepth),
                MetalMaterial);

            if (hasHatch)
            {
                BuildBalconyArrivalRoute(
                    apartment,
                    balcony,
                    floorIndex,
                    yBase,
                    platformDepth,
                    platformWidth);
            }

            return isRouteBalcony;
        }

        private void BuildBalconyArrivalRoute(
            Transform upperApartment,
            Transform upperBalcony,
            int upperFloorIndex,
            float upperFloorBase,
            float platformDepth,
            float platformWidth)
        {
            MiniVanPanelkaApartmentRouteMarker upperMarker =
                upperApartment.GetComponent<MiniVanPanelkaApartmentRouteMarker>();
            if (upperMarker == null)
            {
                return;
            }

            MiniVanPanelkaApartmentRouteMarker lowerMarker = null;
            MiniVanPanelkaApartmentRouteMarker[] markers =
                GetComponentsInChildren<MiniVanPanelkaApartmentRouteMarker>(true);
            for (int markerIndex = 0;
                 markerIndex < markers.Length;
                 markerIndex++)
            {
                if (markers[markerIndex].FloorNumber ==
                        upperMarker.FloorNumber - 1 &&
                    markers[markerIndex].ApartmentSlot ==
                        upperMarker.ApartmentSlot &&
                    markers[markerIndex].Role !=
                        MiniVanPanelkaApartmentRouteRole.Inaccessible)
                {
                    lowerMarker = markers[markerIndex];
                    break;
                }
            }
            if (lowerMarker == null)
            {
                return;
            }

            int lowerFloorNumber = upperFloorIndex;
            string landingName =
                "Route_Balcony_Arrival_" +
                lowerFloorNumber.ToString("00");
            Transform[] generated =
                GetComponentsInChildren<Transform>(true);
            for (int itemIndex = generated.Length - 1;
                 itemIndex >= 0;
                 itemIndex--)
            {
                if (generated[itemIndex] != null &&
                    generated[itemIndex].name == landingName)
                {
                    DestroyGeneratedObject(generated[itemIndex].gameObject);
                }
            }

            Transform landing = Group(landingName, upperApartment);
            landing.localPosition = upperBalcony.localPosition;
            landing.localRotation = upperBalcony.localRotation;
            landing.localScale = Vector3.one;

            float platformY =
                upperFloorBase - StoreyHeight +
                FloorSurfaceOffset - 0.09f;
            float railY =
                upperFloorBase - StoreyHeight +
                FloorSurfaceOffset + 0.57f;
            float centerZ = platformDepth * 0.5f;
            Box(
                "Arrival_Platform",
                landing,
                new Vector3(0f, platformY, centerZ),
                new Vector3(platformWidth, 0.18f, platformDepth),
                FloorMaterial);

            const float transferOpeningWidth = 1.10f;
            float railSegmentWidth =
                (platformWidth - transferOpeningWidth) * 0.5f;
            float railOffset =
                transferOpeningWidth * 0.5f +
                railSegmentWidth * 0.5f;
            Box(
                "Arrival_Outer_Rail_Left",
                landing,
                new Vector3(-railOffset, railY, platformDepth),
                new Vector3(railSegmentWidth, 1.05f, 0.09f),
                MetalMaterial);
            Box(
                "Arrival_Outer_Rail_Right",
                landing,
                new Vector3(railOffset, railY, platformDepth),
                new Vector3(railSegmentWidth, 1.05f, 0.09f),
                MetalMaterial);
            Box(
                "Arrival_Side_Rail_A",
                landing,
                new Vector3(
                    -platformWidth * 0.5f,
                    railY,
                    centerZ),
                new Vector3(0.09f, 1.05f, platformDepth),
                MetalMaterial);
            Box(
                "Arrival_Side_Rail_B",
                landing,
                new Vector3(
                    platformWidth * 0.5f,
                    railY,
                    centerZ),
                new Vector3(0.09f, 1.05f, platformDepth),
                MetalMaterial);

            Vector3 preferredLowerWindow = landing.TransformPoint(
                new Vector3(
                    0f,
                    platformY + 1.45f,
                    0f));
            Transform lowerGlass = FindNearestFacadeGlass(
                lowerMarker.transform,
                preferredLowerWindow);
            if (lowerGlass == null)
            {
                return;
            }

            ConfigureCrackedRouteWindow(
                lowerGlass,
                "BALCONY_WINDOW_G" +
                GenerationSeed.ToString() +
                "_F" +
                lowerFloorNumber.ToString("00") +
                "_APT" +
                lowerMarker.ApartmentNumber.ToString("00"));

            Vector3 landingExitReference = landing.TransformPoint(
                new Vector3(
                    0f,
                    platformY + 0.88f,
                    platformDepth + 0.08f));
            BuildPipeTraversal(
                upperApartment,
                landingExitReference,
                lowerGlass.position,
                upperFloorIndex + 1,
                "Route_Balcony_Transfer_Pipe_From_" +
                (upperFloorIndex + 1).ToString("00") +
                "_To_" +
                lowerFloorNumber.ToString("00"));
        }

        private MiniVanPanelkaBreakableWindow ConfigureCrackedRouteWindow(
            Transform glass,
            string windowId)
        {
            if (glass == null)
            {
                return null;
            }

            MiniVanPanelkaBreakableWindowBase[] existing =
                glass.GetComponents<MiniVanPanelkaBreakableWindowBase>();
            MiniVanPanelkaBreakableWindow breakable = null;
            for (int componentIndex = 0;
                 componentIndex < existing.Length;
                 componentIndex++)
            {
                MiniVanPanelkaBreakableWindow routeWindow =
                    existing[componentIndex] as
                        MiniVanPanelkaBreakableWindow;
                if (routeWindow != null)
                {
                    breakable = routeWindow;
                }
                else
                {
                    DestroyGeneratedObject(existing[componentIndex]);
                }
            }

            if (breakable == null)
            {
                breakable =
                    glass.gameObject.AddComponent<
                        MiniVanPanelkaBreakableWindow>();
            }

            EnsureRouteWindowPassageWidth(glass);
            breakable.Configure(
                windowId,
                CollectRouteWindowPassageParts(glass));
            ConfigureBreakableWindowHitProxy(glass);
            AddCrackPattern(glass);
            return breakable;
        }

        private float GetMinRouteWindowClearWidth()
        {
            if (MinRouteWindowClearWidth > 0.01f)
            {
                return MinRouteWindowClearWidth;
            }

            // Capsule diameter + skin + small squeeze margin.
            return PlayerRadius * 2f + 0.20f;
        }

        private static float GetGlassClearWidth(Transform glass)
        {
            if (glass == null)
            {
                return 0f;
            }

            Vector3 scale = glass.localScale;
            if (Mathf.Abs(scale.x) < 0.12f)
            {
                return Mathf.Abs(scale.z);
            }

            if (Mathf.Abs(scale.z) < 0.12f)
            {
                return Mathf.Abs(scale.x);
            }

            return Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
        }

        private static bool IsGlassWidthOnX(Transform glass)
        {
            Vector3 scale = glass.localScale;
            if (Mathf.Abs(scale.x) < 0.12f)
            {
                return false;
            }

            if (Mathf.Abs(scale.z) < 0.12f)
            {
                return true;
            }

            return Mathf.Abs(scale.x) >= Mathf.Abs(scale.z);
        }

        private void EnsureRouteWindowPassageWidth(Transform glass)
        {
            if (glass == null)
            {
                return;
            }

            float minClear = GetMinRouteWindowClearWidth();
            float current = GetGlassClearWidth(glass);
            if (current >= minClear - 0.001f)
            {
                return;
            }

            bool widthOnX = IsGlassWidthOnX(glass);
            float grow = minClear - current;
            SetAxisScale(glass, widthOnX, minClear);

            Transform module = glass.parent;
            if (module != null)
            {
                for (int i = 0; i < module.childCount; i++)
                {
                    Transform child = module.GetChild(i);
                    if (child == null || child == glass)
                    {
                        continue;
                    }

                    string name = child.name;
                    if (name == "Frame_Bottom" ||
                        name == "Frame_Top" ||
                        name.StartsWith("Sill_", StringComparison.Ordinal) ||
                        name.StartsWith("Lintel_", StringComparison.Ordinal))
                    {
                        GrowAxisScale(child, widthOnX, grow);
                    }
                    else if (name == "Frame_Left" ||
                             name == "Frame_Right" ||
                             name.StartsWith("Jamb_", StringComparison.Ordinal))
                    {
                        NudgeOutwardAlongWidth(child, widthOnX, grow * 0.5f, name);
                    }
                }
            }

            Transform facadeRoot = module != null ? module.parent : null;
            Transform solidModule = facadeRoot != null
                ? facadeRoot.Find("Solid_Wall_Module")
                : null;
            if (solidModule != null)
            {
                for (int i = 0; i < solidModule.childCount; i++)
                {
                    GrowAxisScale(solidModule.GetChild(i), widthOnX, grow);
                }
            }

            CarveRouteWindowWallClearance(glass, minClear);
        }

        private void CarveRouteWindowWallClearance(
            Transform glass,
            float minClearWidth)
        {
            Renderer glassRenderer = glass.GetComponent<Renderer>();
            if (glassRenderer == null)
            {
                return;
            }

            Transform apartment = glass;
            while (apartment.parent != null &&
                   apartment.GetComponent<MiniVanPanelkaApartmentTemplate>() == null &&
                   !apartment.name.StartsWith("Apartment_", StringComparison.Ordinal))
            {
                apartment = apartment.parent;
            }

            bool widthOnX = IsGlassWidthOnX(glass);
            Bounds passage = glassRenderer.bounds;
            Vector3 size = passage.size;
            if (widthOnX)
            {
                size.x = Mathf.Max(size.x, minClearWidth + 0.10f);
            }
            else
            {
                size.z = Mathf.Max(size.z, minClearWidth + 0.10f);
            }

            size.y = Mathf.Max(size.y, 1.05f);
            if (widthOnX)
            {
                size.z = Mathf.Max(0.55f, size.z);
            }
            else
            {
                size.x = Mathf.Max(0.55f, size.x);
            }

            Bounds carveBounds = new Bounds(passage.center, size);
            Transform facadeRoot =
                glass.parent != null ? glass.parent.parent : null;
            Collider[] colliders = apartment.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider == null || !collider.enabled || collider.isTrigger)
                {
                    continue;
                }

                Transform part = collider.transform;
                if (part == glass || part.IsChildOf(glass))
                {
                    continue;
                }

                if (facadeRoot != null && part.IsChildOf(facadeRoot))
                {
                    continue;
                }

                string name = part.name;
                bool isWall =
                    name.StartsWith("FacadeWall_", StringComparison.Ordinal) ||
                    name.StartsWith("Wall_", StringComparison.Ordinal) ||
                    name.StartsWith("InteriorWall_", StringComparison.Ordinal) ||
                    name.IndexOf("Wall_Segment", StringComparison.Ordinal) >= 0;
                if (!isWall || !collider.bounds.Intersects(carveBounds))
                {
                    continue;
                }

                Bounds bounds = collider.bounds;
                float along = widthOnX ? bounds.size.x : bounds.size.z;
                float thickness = widthOnX ? bounds.size.z : bounds.size.x;
                // Disable only thin shell slabs that pinch the crawl opening.
                if (along < minClearWidth + 0.35f && thickness < 0.45f)
                {
                    collider.enabled = false;
                }
            }
        }

        private static Transform[] CollectRouteWindowPassageParts(Transform glass)
        {
            List<Transform> parts = new List<Transform>(8);
            AddPassagePart(parts, FindWindowPassagePart(glass, "Sill_"));
            AddPassagePart(parts, FindWindowPassagePart(glass, "Lintel_"));

            Transform module = glass != null ? glass.parent : null;
            if (module != null)
            {
                for (int i = 0; i < module.childCount; i++)
                {
                    Transform child = module.GetChild(i);
                    if (child == null || child == glass)
                    {
                        continue;
                    }

                    string name = child.name;
                    if (name.StartsWith("Frame_", StringComparison.Ordinal) ||
                        name.StartsWith("Sill_", StringComparison.Ordinal) ||
                        name.StartsWith("Lintel_", StringComparison.Ordinal) ||
                        name.StartsWith("Jamb_", StringComparison.Ordinal))
                    {
                        AddPassagePart(parts, child);
                    }
                }

                Transform facadeRoot = module.parent;
                Transform solidModule = facadeRoot != null
                    ? facadeRoot.Find("Solid_Wall_Module")
                    : null;
                AddPassagePart(parts, solidModule);
            }

            return parts.ToArray();
        }

        private static void AddPassagePart(List<Transform> parts, Transform part)
        {
            if (part == null || parts.Contains(part))
            {
                return;
            }

            parts.Add(part);
        }

        private static void SetAxisScale(Transform target, bool axisX, float absolute)
        {
            if (target == null)
            {
                return;
            }

            Vector3 scale = target.localScale;
            if (axisX)
            {
                scale.x = Mathf.Sign(scale.x == 0f ? 1f : scale.x) * absolute;
            }
            else
            {
                scale.z = Mathf.Sign(scale.z == 0f ? 1f : scale.z) * absolute;
            }

            target.localScale = scale;
        }

        private static void GrowAxisScale(Transform target, bool axisX, float grow)
        {
            if (target == null || grow <= 0f)
            {
                return;
            }

            Vector3 scale = target.localScale;
            if (axisX)
            {
                scale.x = Mathf.Sign(scale.x == 0f ? 1f : scale.x) *
                          (Mathf.Abs(scale.x) + grow);
            }
            else
            {
                scale.z = Mathf.Sign(scale.z == 0f ? 1f : scale.z) *
                          (Mathf.Abs(scale.z) + grow);
            }

            target.localScale = scale;
        }

        private static void NudgeOutwardAlongWidth(
            Transform target,
            bool axisX,
            float distance,
            string name)
        {
            if (target == null || distance <= 0f)
            {
                return;
            }

            Vector3 position = target.localPosition;
            float sign = axisX ? Mathf.Sign(position.x) : Mathf.Sign(position.z);
            if (Mathf.Abs(sign) < 0.01f)
            {
                sign = name.IndexOf("Left", StringComparison.OrdinalIgnoreCase) >= 0 ||
                       name.IndexOf("_A", StringComparison.Ordinal) >= 0
                    ? -1f
                    : 1f;
            }

            if (axisX)
            {
                position.x += sign * distance;
            }
            else
            {
                position.z += sign * distance;
            }

            target.localPosition = position;
        }

        private static void ConfigureBreakableWindowHitProxy(Transform glass)
        {
            const string proxyName = "Breakable_Window_Hit_Proxy";
            Transform proxy = glass.Find(proxyName);
            if (proxy == null)
            {
                GameObject proxyObject = new GameObject(proxyName);
                proxy = proxyObject.transform;
                proxy.SetParent(glass, false);
            }

            proxy.localPosition = Vector3.zero;
            proxy.localRotation = Quaternion.identity;
            Vector3 glassScale = glass.localScale;
            proxy.localScale = new Vector3(
                1f / Mathf.Max(0.001f, Mathf.Abs(glassScale.x)),
                1f / Mathf.Max(0.001f, Mathf.Abs(glassScale.y)),
                1f / Mathf.Max(0.001f, Mathf.Abs(glassScale.z)));

            BoxCollider source = glass.GetComponent<BoxCollider>();
            BoxCollider hitbox = proxy.GetComponent<BoxCollider>();
            if (hitbox == null)
            {
                hitbox = proxy.gameObject.AddComponent<BoxCollider>();
            }

            Vector3 size = new Vector3(
                Mathf.Max(0.35f, Mathf.Abs(glassScale.x)),
                Mathf.Max(0.70f, Mathf.Abs(glassScale.y)),
                Mathf.Max(0.35f, Mathf.Abs(glassScale.z)));
            hitbox.center = Vector3.zero;
            hitbox.size = size;
            hitbox.isTrigger = false;
            hitbox.enabled = true;

            ConfigureBreakableWindowSwingTrigger(glass, proxy.localScale);
        }

        private static void ConfigureBreakableWindowSwingTrigger(
            Transform glass,
            Vector3 inverseGlassScale)
        {
            const string triggerName = "Breakable_Window_Swing_Trigger";
            Transform trigger = glass.Find(triggerName);
            if (trigger == null)
            {
                GameObject triggerObject = new GameObject(triggerName);
                trigger = triggerObject.transform;
                trigger.SetParent(glass, false);
            }

            trigger.localPosition = Vector3.zero;
            trigger.localRotation = Quaternion.identity;
            trigger.localScale = inverseGlassScale;

            BoxCollider swingBox = trigger.GetComponent<BoxCollider>();
            if (swingBox == null)
            {
                swingBox = trigger.gameObject.AddComponent<BoxCollider>();
            }

            Vector3 glassScale = glass.localScale;
            swingBox.center = Vector3.zero;
            swingBox.size = new Vector3(
                GetBreakableWindowSwingExtent(glassScale.x),
                GetBreakableWindowSwingExtent(glassScale.y),
                GetBreakableWindowSwingExtent(glassScale.z));
            swingBox.isTrigger = true;
            swingBox.enabled = true;
        }

        private static float GetBreakableWindowSwingExtent(float glassScaleAxis)
        {
            float size = Mathf.Abs(glassScaleAxis);
            // The pane is a thin slab, so its depth axis has to reach past the wall on
            // both sides, otherwise a swing from the street never touches the glass.
            return size < 0.35f ? 1.2f : size + 0.40f;
        }

        private static int GetApartmentSlotFromMarker(
            Transform apartment)
        {
            MiniVanPanelkaApartmentRouteMarker marker =
                apartment != null
                    ? apartment.GetComponent<
                        MiniVanPanelkaApartmentRouteMarker>()
                    : null;
            return marker != null ? marker.ApartmentSlot : 0;
        }


        
        private float GetApartmentEntryZ(bool north, int layoutVariant)
        {
            MiniVanPanelkaApartmentTemplateCatalog catalog = GetApartmentTemplateCatalog();
            GameObject prefab = catalog != null ? catalog.GetPrefab(layoutVariant) : null;
            MiniVanPanelkaApartmentTemplate template = prefab != null
                ? prefab.GetComponent<MiniVanPanelkaApartmentTemplate>()
                : null;
            float sourceEntryZ = template != null && template.EntrySocket != null
                ? template.transform.InverseTransformPoint(template.EntrySocket.position).z
                : -2.8f;
            float apartmentCenterZ = north ? BuildingHalfDepth * 0.5f : -BuildingHalfDepth * 0.5f;
            return apartmentCenterZ + (north ? sourceEntryZ : -sourceEntryZ);
        }


private static float[] EvenCenters(
            float min,
            float max,
            int count,
            float itemWidth = 0f)
        {
            count = Mathf.Max(1, count);
            float[] result = new float[count];
            float span = max - min;

            if (count == 1)
            {
                result[0] = (min + max) * 0.5f;
                return result;
            }

            if (itemWidth <= 0f)
            {
                for (int i = 0; i < count; i++)
                {
                    result[i] = min + span * ((i + 0.5f) / count);
                }

                return result;
            }

            const float edgeMargin = 0.2f;
            float first = min + edgeMargin + itemWidth * 0.5f;
            float last = max - edgeMargin - itemWidth * 0.5f;
            if (last < first)
            {
                first = (min + max) * 0.5f;
                last = first;
            }

            for (int i = 0; i < count; i++)
            {
                result[i] = Mathf.Lerp(first, last, i / (float)(count - 1));
            }

            return result;
        }

private float FitWindowWidth(float span, int count, float maximumWidth)
        {
            count = Mathf.Max(1, count);
            const float edgeMargin = 0.2f;
            const float minimumGap = 0.18f;
            float minimumWidth = GetMinRouteWindowClearWidth();
            float available = span - edgeMargin * 2f - minimumGap * (count - 1);
            float fittedWidth = available / count;
            return Mathf.Min(maximumWidth, Mathf.Max(minimumWidth, fittedWidth));
        }

        private void ApplyFacadeOcclusion(Transform generatedRoot)
        {
            if (generatedRoot == null ||
                facadeOcclusionBounds == null ||
                facadeOcclusionBounds.Length == 0)
            {
                return;
            }

            Transform patches =
                Group("Shared_Facade_Occlusion_Patches", generatedRoot);
            MiniVanPanelkaApartmentFacadeMarker[] windows =
                generatedRoot.GetComponentsInChildren<
                    MiniVanPanelkaApartmentFacadeMarker>(true);
            int patchIndex = 0;
            for (int i = 0; i < windows.Length; i++)
            {
                MiniVanPanelkaApartmentFacadeMarker window = windows[i];
                if (window == null ||
                    window.GetComponent<MiniVanPanelkaWindowSocket>() != null ||
                    !TryGetLocalRenderBounds(
                        window.transform,
                        out Bounds localBounds) ||
                    !IsFacadeDecorationOccluded(localBounds))
                {
                    continue;
                }

                Vector3 center = localBounds.center;
                bool onXFace =
                    Mathf.Abs(Mathf.Abs(center.x) - BuildingHalfWidth) <=
                    Mathf.Abs(Mathf.Abs(center.z) - BuildingHalfDepth);
                Vector3 scale;
                if (onXFace)
                {
                    center.x = Mathf.Sign(center.x) * BuildingHalfWidth;
                    scale = new Vector3(
                        WallThickness + 0.08f,
                        Mathf.Max(0.8f, localBounds.size.y + 0.10f),
                        Mathf.Max(0.8f, localBounds.size.z + 0.10f));
                }
                else
                {
                    center.z = Mathf.Sign(center.z) * BuildingHalfDepth;
                    scale = new Vector3(
                        Mathf.Max(0.8f, localBounds.size.x + 0.10f),
                        Mathf.Max(0.8f, localBounds.size.y + 0.10f),
                        WallThickness + 0.08f);
                }

                Box(
                    "Shared_Facade_Window_Patch_" +
                    patchIndex.ToString("00"),
                    patches,
                    center,
                    scale,
                    ExteriorMaterial);
                patchIndex++;
                DestroyGeneratedObject(window.gameObject);
            }

            Transform[] all =
                generatedRoot.GetComponentsInChildren<Transform>(true);
            for (int i = all.Length - 1; i >= 0; i--)
            {
                Transform candidate = all[i];
                if (candidate == null ||
                    (!candidate.name.StartsWith(
                         "Apartment_Balcony_",
                         StringComparison.Ordinal) &&
                     (!candidate.name.StartsWith(
                          "Route_Balcony_",
                          StringComparison.Ordinal) ||
                      candidate.name.StartsWith(
                          "Route_Balcony_Hatch_",
                          StringComparison.Ordinal))) ||
                    !TryGetLocalRenderBounds(
                        candidate,
                        out Bounds balconyBounds) ||
                    !IsFacadeDecorationOccluded(balconyBounds))
                {
                    continue;
                }

                DestroyGeneratedObject(candidate.gameObject);
            }

            Transform exteriorLadder =
                generatedRoot.Find("Exterior_Roof_Ladder");
            if (exteriorLadder != null &&
                TryGetLocalRenderBounds(
                    exteriorLadder,
                    out Bounds ladderBounds))
            {
                Bounds ladderClearance = ladderBounds;
                if (ExteriorOnlyLocked)
                {
                    ladderClearance.Expand(
                        new Vector3(2f, 0f, 2f));
                }
                if (IsFacadeDecorationOccluded(ladderClearance))
                {
                    DestroyGeneratedObject(exteriorLadder.gameObject);
                }
            }
        }

        private bool TryGetLocalRenderBounds(
            Transform root,
            out Bounds bounds)
        {
            bounds = default;
            Renderer[] renderers =
                root.GetComponentsInChildren<Renderer>(true);
            bool initialized = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                    continue;

                Bounds local = TransformBoundsTo(
                    renderer.localBounds,
                    transform.worldToLocalMatrix *
                    renderer.transform.localToWorldMatrix);
                if (!initialized)
                {
                    bounds = local;
                    initialized = true;
                }
                else
                {
                    bounds.Encapsulate(local);
                }
            }

            return initialized;
        }



        private void ConvertToLockedExterior(Transform generatedRoot)
        {
            Transform exterior = Group("ExteriorOnly_Locked", generatedRoot);
            Transform roof = generatedRoot.Find("Roof");
            if (roof != null)
            {
                roof.SetParent(exterior, true);
            }
            Transform ladder = generatedRoot.Find("Exterior_Roof_Ladder");
            if (ladder != null)
            {
                ladder.SetParent(exterior, true);
            }

            Renderer[] renderers = generatedRoot.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                bool interiorWallpaper =
                    renderer != null &&
                    renderer.sharedMaterial != null &&
                    renderer.sharedMaterial.name.StartsWith(
                        "Wallpaper_",
                        StringComparison.Ordinal);
                if (renderer == null ||
                    !renderer.gameObject.activeInHierarchy ||
                    interiorWallpaper ||
                    renderer.name == "Solid_Interior_Skin" ||
                    renderer.transform.IsChildOf(exterior))
                {
                    continue;
                }

                Vector3 local = generatedRoot.InverseTransformPoint(renderer.bounds.center);
                bool nearPerimeter =
                    Mathf.Abs(Mathf.Abs(local.x) - BuildingHalfWidth) <= 1.05f ||
                    Mathf.Abs(Mathf.Abs(local.z) - BuildingHalfDepth) <= 1.05f ||
                    Mathf.Abs(local.x) >= BuildingHalfWidth - 0.55f ||
                    Mathf.Abs(local.z) >= BuildingHalfDepth - 0.55f;
                if (!nearPerimeter)
                {
                    continue;
                }

                bool facadeMaterial =
                    renderer.sharedMaterial == ExteriorMaterial ||
                    renderer.sharedMaterial == GlassMaterial;
                bool facadeWindow =
                    renderer.name == "Breakable_Glass" ||
                    HasNamedAncestor(
                        renderer.transform,
                        "FacadeWall_Window_");
                bool balconyPart = HasNamedAncestor(renderer.transform, "Balcony");
                bool pipePart =
                    renderer.name.StartsWith("Pipe_", StringComparison.Ordinal) ||
                    HasNamedAncestor(renderer.transform, "Route_Pipe");
                bool crackPart = HasNamedAncestor(renderer.transform, "Crack");
                bool streetDoor =
                    renderer.name == "Door_Panel" &&
                    Mathf.Abs(local.z) >= BuildingHalfDepth - 0.8f;
                if (facadeMaterial || facadeWindow || balconyPart ||
                    pipePart || crackPart || streetDoor)
                {
                    renderer.transform.SetParent(exterior, true);
                }
            }

            for (int i = generatedRoot.childCount - 1; i >= 0; i--)
            {
                Transform child = generatedRoot.GetChild(i);
                if (child == exterior)
                {
                    continue;
                }
                DestroyGeneratedObject(child.gameObject);
            }

            RemoveComponents<MiniVanPanelkaBreakableWindow>(exterior);
            RemoveComponents<MiniVanPanelkaRoomDoor>(exterior);
            RemoveComponents<MiniVanPanelkaInteractable>(exterior);
            BakeLockedExteriorRenderers(exterior);

            Transform hatch = FindDescendant(exterior, "Roof_Hatch");
            if (hatch != null)
            {
                hatch.name = "Roof_Hatch_CLOSED";
            }

            MiniVanPanelkaInteractionController controller =
                GetComponent<MiniVanPanelkaInteractionController>();
            if (controller != null)
            {
                DestroyGeneratedObject(controller);
            }
        }

        private static Transform FindFurnishingUnit(Transform item)
        {
            Transform current = item;
            while (current != null && current.parent != null)
            {
                if (current.parent.name == "FURNITURE__PREFAB_INSTANCES")
                {
                    return current;
                }

                current = current.parent;
            }

            return null;
        }

        private void BakeLockedExteriorRenderers(Transform exterior)
        {
            MeshRenderer[] renderers =
                exterior.GetComponentsInChildren<MeshRenderer>(false);
            Dictionary<Material, List<MeshFilter>> groups = new Dictionary<Material, List<MeshFilter>>();
            for (int i = 0; i < renderers.Length; i++)
            {
                MeshRenderer renderer = renderers[i];
                MeshFilter filter = renderer != null ? renderer.GetComponent<MeshFilter>() : null;
                if (renderer == null || filter == null || filter.sharedMesh == null ||
                    renderer.name == "Solid_Interior_Skin" ||
                    renderer.sharedMaterial != null &&
                    renderer.sharedMaterial.name.StartsWith(
                        "Wallpaper_",
                        StringComparison.Ordinal) ||
                    renderer.sharedMaterials.Length != 1 || renderer.sharedMaterial == null)
                {
                    continue;
                }

                Material material = renderer.sharedMaterial;
                if (!groups.TryGetValue(material, out List<MeshFilter> filters))
                {
                    filters = new List<MeshFilter>();
                    groups.Add(material, filters);
                }
                filters.Add(filter);
            }

            foreach (KeyValuePair<Material, List<MeshFilter>> group in groups)
            {
                List<MeshFilter> filters = group.Value;
                CombineInstance[] instances = new CombineInstance[filters.Count];
                for (int i = 0; i < filters.Count; i++)
                {
                    instances[i] = new CombineInstance
                    {
                        mesh = filters[i].sharedMesh,
                        transform = exterior.worldToLocalMatrix * filters[i].transform.localToWorldMatrix
                    };
                }

                Mesh combinedMesh = new Mesh
                {
                    name = "Baked Locked Exterior " + group.Key.name,
                    indexFormat = IndexFormat.UInt32
                };
                combinedMesh.CombineMeshes(instances, true, true, false);
                combinedMesh.RecalculateBounds();

                GameObject baked = new GameObject("Baked_" + group.Key.name);
                baked.transform.SetParent(exterior, false);
                baked.isStatic = true;
                MeshFilter bakedFilter = baked.AddComponent<MeshFilter>();
                bakedFilter.sharedMesh = combinedMesh;
                MeshRenderer bakedRenderer = baked.AddComponent<MeshRenderer>();
                bakedRenderer.sharedMaterial = group.Key;
                bakedRenderer.shadowCastingMode = ShadowCastingMode.Off;
                bakedRenderer.receiveShadows = false;
                group.Key.enableInstancing = true;

                for (int i = 0; i < filters.Count; i++)
                {
                    RemoveBakedDecorativeCollider(filters[i]);
                    RemoveBakedSourceMesh(filters[i]);
                }
            }
        }

        private void BakeStaticAccessibleRenderers(Transform generatedRoot)
        {
            MeshRenderer[] renderers = generatedRoot.GetComponentsInChildren<MeshRenderer>(true);
            Dictionary<string, List<MeshFilter>> groups = new Dictionary<string, List<MeshFilter>>();
            Dictionary<string, Material> groupMaterials = new Dictionary<string, Material>();
            for (int i = 0; i < renderers.Length; i++)
            {
                MeshRenderer renderer = renderers[i];
                MeshFilter filter = renderer != null ? renderer.GetComponent<MeshFilter>() : null;
                if (renderer == null || filter == null || filter.sharedMesh == null ||
                    renderer.sharedMaterials.Length != 1 || renderer.sharedMaterial == null ||
                    renderer.GetComponentInParent<MiniVanPanelkaInteractable>() != null ||
                    renderer.GetComponentInParent<MiniVanPanelkaDoorCollisionProxy>() != null ||
                    renderer.name == "Door_Panel" ||
                    renderer.GetComponentInParent<MiniVanPanelkaBreakableWindow>() != null ||
                    renderer.GetComponentInParent<Rigidbody>() != null)
                {
                    continue;
                }

                Material material = renderer.sharedMaterial;
                Vector3 localCenter = generatedRoot.InverseTransformPoint(renderer.bounds.center);
                int floorCell = Mathf.FloorToInt(localCenter.y / StoreyHeight);
                int xCell = Mathf.FloorToInt(localCenter.x / 10f);
                int zCell = Mathf.FloorToInt(localCenter.z / 10f);
                string groupKey = material.GetInstanceID() + "_" + floorCell + "_" + xCell + "_" + zCell;
                if (!groups.TryGetValue(groupKey, out List<MeshFilter> filters))
                {
                    filters = new List<MeshFilter>();
                    groups.Add(groupKey, filters);
                    groupMaterials.Add(groupKey, material);
                }
                filters.Add(filter);
            }

            Transform bakedRoot = Group("Baked_Static_Geometry", generatedRoot);
            foreach (KeyValuePair<string, List<MeshFilter>> group in groups)
            {
                List<MeshFilter> filters = group.Value;
                if (filters.Count == 0)
                {
                    continue;
                }

                CombineInstance[] instances = new CombineInstance[filters.Count];
                for (int i = 0; i < filters.Count; i++)
                {
                    instances[i] = new CombineInstance
                    {
                        mesh = filters[i].sharedMesh,
                        transform = bakedRoot.worldToLocalMatrix * filters[i].transform.localToWorldMatrix
                    };
                }

                Mesh combinedMesh = new Mesh
                {
                    name = "Baked Static " + group.Key,
                    indexFormat = IndexFormat.UInt32
                };
                combinedMesh.CombineMeshes(instances, true, true, false);
                combinedMesh.RecalculateBounds();

                Material groupMaterial = groupMaterials[group.Key];
                GameObject baked = new GameObject("Baked_" + groupMaterial.name + "_" + group.Key);
                baked.transform.SetParent(bakedRoot, false);
                baked.isStatic = true;
                baked.AddComponent<MeshFilter>().sharedMesh = combinedMesh;
                MeshRenderer bakedRenderer = baked.AddComponent<MeshRenderer>();
                bakedRenderer.sharedMaterial = groupMaterial;
                bakedRenderer.shadowCastingMode = ShadowCastingMode.Off;
                bakedRenderer.receiveShadows = false;
                groupMaterial.enableInstancing = true;

                for (int i = 0; i < filters.Count; i++)
                {
                    RemoveBakedDecorativeCollider(filters[i]);
                    RemoveBakedSourceMesh(filters[i]);
                }
            }
        }

        private static void EnsureRuntimeDoorProxyCoverage(Transform generatedRoot)
        {
            if (generatedRoot == null)
            {
                return;
            }

            MiniVanPanelkaRoomDoor[] doors =
                generatedRoot.GetComponentsInChildren<MiniVanPanelkaRoomDoor>(true);
            for (int i = 0; i < doors.Length; i++)
            {
                MiniVanPanelkaRoomDoor door = doors[i];
                if (door == null ||
                    !door.enabled ||
                    !door.gameObject.activeInHierarchy ||
                    door.Pivot == null)
                {
                    continue;
                }

                Renderer[] renderers = door.Pivot.GetComponentsInChildren<Renderer>(true);
                Renderer runtimePanel = null;
                for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
                {
                    if (renderers[rendererIndex] != null &&
                        renderers[rendererIndex].name == "Door_Panel")
                    {
                        runtimePanel = renderers[rendererIndex];
                        break;
                    }
                }

                if (runtimePanel == null)
                {
                    continue;
                }

                MiniVanPanelkaDoorCollisionProxy pivotProxy =
                    door.Pivot.gameObject.GetComponent<MiniVanPanelkaDoorCollisionProxy>();
                if (pivotProxy == null)
                {
                    pivotProxy = door.Pivot.gameObject
                        .AddComponent<MiniVanPanelkaDoorCollisionProxy>();
                }

                pivotProxy.ConfigureForwardOnly(door);
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

        private void RemoveBakedSourceMesh(MeshFilter filter)
        {
            if (filter == null)
            {
                return;
            }

            MeshRenderer renderer = filter.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.enabled = false;
            }
        }

        private void RemoveBakedDecorativeCollider(MeshFilter filter)
        {
            if (filter == null)
            {
                return;
            }

            string objectName = filter.name;
            bool decorative =
                objectName.StartsWith("Glass_", StringComparison.Ordinal) ||
                objectName.StartsWith("Sill_", StringComparison.Ordinal) ||
                objectName.StartsWith("Lintel_", StringComparison.Ordinal) ||
                objectName.StartsWith("Crack_", StringComparison.Ordinal) ||
                objectName.StartsWith("Handle_", StringComparison.Ordinal);
            if (!decorative || filter.GetComponentInParent<MiniVanPanelkaBreakableWindow>() != null)
            {
                return;
            }

            Collider collider = filter.GetComponent<Collider>();
            if (collider != null)
            {
                DestroyGeneratedObject(collider);
            }
        }

        private static void DisablePanelkaShadows(Transform generatedRoot)
        {
            MeshRenderer[] renderers = generatedRoot.GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null)
                {
                    continue;
                }
                renderers[i].shadowCastingMode = ShadowCastingMode.Off;
                renderers[i].receiveShadows = false;
            }
        }

        private static bool HasNamedAncestor(Transform transform, string token)
        {
            Transform current = transform;
            while (current != null)
            {
                if (current.name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
                current = current.parent;
            }
            return false;
        }

        private static Transform FindDescendant(Transform parent, string name)
        {
            Transform[] children = parent.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i].name == name)
                {
                    return children[i];
                }
            }
            return null;
        }

        private void RemoveComponents<T>(Transform parent) where T : Component
        {
            T[] components = parent.GetComponentsInChildren<T>(true);
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] != null)
                {
                    DestroyGeneratedObject(components[i]);
                }
            }
        }

        private void DestroyGeneratedObject(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }
            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        private void BuildRoof(Transform root, int floorCount)
        {
            Transform roof = Group("Roof", root);
            float roofY = FloorTop + floorCount * StoreyHeight + 0.1f;
            float hatchMinX = -HatchHalf;
            float hatchMaxX = HatchHalf;
            float hatchMinZ = HatchCenterZ - HatchHalf;
            float hatchMaxZ = HatchCenterZ + HatchHalf;

            Box(
                "Roof_West",
                roof,
                new Vector3((-BuildingHalfWidth + hatchMinX) * 0.5f, roofY, 0f),
                new Vector3(hatchMinX + BuildingHalfWidth, 0.2f, BuildingHalfDepth * 2f),
                ExteriorMaterial);
            Box(
                "Roof_East",
                roof,
                new Vector3((hatchMaxX + BuildingHalfWidth) * 0.5f, roofY, 0f),
                new Vector3(BuildingHalfWidth - hatchMaxX, 0.2f, BuildingHalfDepth * 2f),
                ExteriorMaterial);
            Box(
                "Roof_South_Of_Hatch",
                roof,
                new Vector3(0f, roofY, (-BuildingHalfDepth + hatchMinZ) * 0.5f),
                new Vector3(HatchHalf * 2f, 0.2f, hatchMinZ + BuildingHalfDepth),
                ExteriorMaterial);
            Box(
                "Roof_North_Of_Hatch",
                roof,
                new Vector3(0f, roofY, (hatchMaxZ + BuildingHalfDepth) * 0.5f),
                new Vector3(HatchHalf * 2f, 0.2f, BuildingHalfDepth - hatchMaxZ),
                ExteriorMaterial);

            float roofTop = roofY + 0.1f;
            PlaceHatch(roof, new Vector3(-HatchHalf, roofTop, hatchMinZ));

            float parapetY = roofTop + 0.3f;
            Box("Parapet_West", roof, new Vector3(-BuildingHalfWidth, parapetY, 0f), new Vector3(0.2f, 0.6f, BuildingHalfDepth * 2f), ExteriorMaterial);
            Box("Parapet_East", roof, new Vector3(BuildingHalfWidth, parapetY, 0f), new Vector3(0.2f, 0.6f, BuildingHalfDepth * 2f), ExteriorMaterial);
            Box("Parapet_South", roof, new Vector3(0f, parapetY, -BuildingHalfDepth), new Vector3(BuildingHalfWidth * 2f, 0.6f, 0.2f), ExteriorMaterial);
            Box("Parapet_North_Left", roof, new Vector3(-6.9f, parapetY, BuildingHalfDepth), new Vector3(12.2f, 0.6f, 0.2f), ExteriorMaterial);
            Box("Parapet_North_Right", roof, new Vector3(6.9f, parapetY, BuildingHalfDepth), new Vector3(12.2f, 0.6f, 0.2f), ExteriorMaterial);
        }

        private void PlaceHatch(Transform parent, Vector3 hingePosition)
        {
            Transform hinge = Group("Roof_Hatch", parent);
            hinge.localPosition = hingePosition;
            Transform pivot = Group("Hatch_Runtime_Pivot", hinge);

            MiniVanPanelkaRoomDoor interactable = hinge.gameObject.AddComponent<MiniVanPanelkaRoomDoor>();
            interactable.Type = MiniVanPanelkaInteractableType.RoofHatch;
            interactable.Pivot = pivot;
            interactable.OpenEuler = new Vector3(-100f, 0f, 0f);
            interactable.Message = "Roof hatch";

            Box(
                "Hatch_Panel",
                pivot,
                new Vector3(HatchHalf, 0.06f, HatchHalf),
                new Vector3(HatchHalf * 2f, 0.12f, HatchHalf * 2f),
                DoorMaterial);
        }

        private void BuildExteriorLadder(Transform root, int floorCount)
        {
            float ladderBaseY = FloorTop - 0.3f;
            float roofBaseY = FloorTop + floorCount * StoreyHeight;
            float height = roofBaseY - ladderBaseY + 1.35f;
            float roofEntryHeight = roofBaseY - ladderBaseY + 0.28f;
            ChooseExteriorLadderPose(root, ladderBaseY, out Vector3 position, out Quaternion rotation);
            Transform ladder = BuildLadder(
                "Exterior_Roof_Ladder",
                root,
                position,
                height,
                roofEntryHeight);
            ladder.localRotation = rotation;
        }

        private void ChooseExteriorLadderPose(Transform root, float ladderBaseY,
            out Vector3 position, out Quaternion rotation)
        {
            List<Vector3> positions = new List<Vector3>();
            List<Quaternion> rotations = new List<Quaternion>();
            List<int> facadePriorities = new List<int>();
            AddLadderFacadeCandidates(positions, rotations, facadePriorities,
                ladderBaseY, true, BuildingHalfDepth + 0.36f, -11.7f, 11.7f, 0f, 0);
            AddLadderFacadeCandidates(positions, rotations, facadePriorities,
                ladderBaseY, true, -BuildingHalfDepth - 0.36f, -11.7f, 11.7f, 180f, 0);
            AddLadderFacadeCandidates(positions, rotations, facadePriorities,
                ladderBaseY, false, BuildingHalfWidth + 0.36f, -7.7f, 7.7f, 90f, 1);
            AddLadderFacadeCandidates(positions, rotations, facadePriorities,
                ladderBaseY, false, -BuildingHalfWidth - 0.36f, -7.7f, 7.7f, -90f, 1);

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            int bestConflicts = int.MaxValue;
            int bestFacadePriority = int.MaxValue;
            float bestClearance = float.MinValue;
            int bestIndex = 0;
            for (int candidate = 0; candidate < positions.Count; candidate++)
            {
                int conflicts = 0;
                float nearest = float.MaxValue;
                for (int i = 0; i < renderers.Length; i++)
                {
                    Renderer renderer = renderers[i];
                    if (renderer == null || !IsExteriorLadderObstacle(renderer))
                    {
                        continue;
                    }

                    Bounds bounds = TransformBoundsToLocal(root, renderer.bounds);
                    float dx = Mathf.Max(0f,
                        Mathf.Abs(positions[candidate].x - bounds.center.x) - bounds.extents.x);
                    float dz = Mathf.Max(0f,
                        Mathf.Abs(positions[candidate].z - bounds.center.z) - bounds.extents.z);
                    float distance = Mathf.Sqrt(dx * dx + dz * dz);
                    nearest = Mathf.Min(nearest, distance);
                    float requiredClearance = renderer.name.StartsWith("Pipe_Perimeter_", StringComparison.Ordinal)
                        ? 1.65f
                        : 1.05f;
                    if (HasNamedAncestor(renderer.transform, "Balcony"))
                    {
                        requiredClearance = 1.25f;
                    }
                    if (distance < requiredClearance)
                    {
                        conflicts++;
                    }
                }

                if (conflicts < bestConflicts ||
                    (conflicts == bestConflicts && facadePriorities[candidate] < bestFacadePriority) ||
                    (conflicts == bestConflicts && facadePriorities[candidate] == bestFacadePriority &&
                     nearest > bestClearance))
                {
                    bestConflicts = conflicts;
                    bestFacadePriority = facadePriorities[candidate];
                    bestClearance = nearest;
                    bestIndex = candidate;
                }
            }

            position = positions[bestIndex];
            rotation = rotations[bestIndex];
        }

        private static void AddLadderFacadeCandidates(List<Vector3> positions,
            List<Quaternion> rotations, List<int> priorities, float y, bool alongX,
            float fixedCoordinate, float minimum, float maximum, float yaw, int priority)
        {
            const float step = 0.55f;
            for (float value = minimum; value <= maximum + 0.01f; value += step)
            {
                positions.Add(alongX
                    ? new Vector3(value, y, fixedCoordinate)
                    : new Vector3(fixedCoordinate, y, value));
                rotations.Add(Quaternion.Euler(0f, yaw, 0f));
                priorities.Add(priority);
            }
        }

        private static bool IsExteriorLadderObstacle(Renderer renderer)
        {
            string objectName = renderer.name;
            return objectName.StartsWith("Glass_", StringComparison.Ordinal) ||
                   objectName.StartsWith("Sill_", StringComparison.Ordinal) ||
                   objectName.StartsWith("Lintel_", StringComparison.Ordinal) ||
                   objectName.StartsWith("Pipe_Perimeter_", StringComparison.Ordinal) ||
                   objectName == "Door_Panel" ||
                   HasNamedAncestor(renderer.transform, "Balcony") ||
                   HasNamedAncestor(renderer.transform, "Street_Door");
        }

        private static Bounds TransformBoundsToLocal(Transform root, Bounds worldBounds)
        {
            Vector3 minimum = worldBounds.min;
            Vector3 maximum = worldBounds.max;
            Bounds localBounds = new Bounds(root.InverseTransformPoint(minimum), Vector3.zero);
            for (int x = 0; x < 2; x++)
            {
                for (int y = 0; y < 2; y++)
                {
                    for (int z = 0; z < 2; z++)
                    {
                        Vector3 corner = new Vector3(
                            x == 0 ? minimum.x : maximum.x,
                            y == 0 ? minimum.y : maximum.y,
                            z == 0 ? minimum.z : maximum.z);
                        localBounds.Encapsulate(root.InverseTransformPoint(corner));
                    }
                }
            }
            return localBounds;
        }

        private void BuildTopHatchLadder(Transform root, int floorCount)
        {
            float yBase = FloorTop + (floorCount - 1) * StoreyHeight;
            Transform ladderArea = Group("Top_Floor_Hatch_Access", root);
            Box(
                "Ladder_Backing_Wall",
                ladderArea,
                new Vector3(0f, yBase + StoreyHeight * 0.5f, HatchCenterZ + 0.95f),
                new Vector3(2.2f, StoreyHeight, 0.18f),
                InteriorMaterial);

            Transform interiorLadder = BuildLadder(
                "Interior_Hatch_Ladder",
                ladderArea,
                new Vector3(-0.806f, yBase, 6.071f),
                StoreyHeight + 0.35f,
                StoreyHeight - 0.05f);
            interiorLadder.localRotation = Quaternion.Euler(0f, 90f, 0f);
        }

private Transform BuildLadder(
            string name,
            Transform parent,
            Vector3 position,
            float height,
            float roofEntryHeight)
        {
            Transform ladder = Group(name, parent);
            ladder.localPosition = position;

            // Size the root volume before MiniVanLadder.OnValidate creates ClimbTrigger.
            // Root collider is disabled by MiniVanLadder; ClimbTrigger + thin physical collider remain.
            BoxCollider rootVolume = ladder.gameObject.AddComponent<BoxCollider>();
            rootVolume.center = new Vector3(0f, height * 0.5f + 0.85f, 0f);
            rootVolume.size = new Vector3(1.75f, height + 3.2f, 1.45f);
            rootVolume.isTrigger = false;
            rootVolume.enabled = false;

            MiniVanLadder ladderBehaviour = ladder.gameObject.AddComponent<MiniVanLadder>();
            ladderBehaviour.ClimbSpeed = 3.1f;
            ladderBehaviour.RoofEntryHeight = roofEntryHeight;
            ladderBehaviour.RoofEntryPushSpeed = 3.6f;
            ladderBehaviour.RoofEntryLocalDirection = Vector3.back;
            ladderBehaviour.StickToLadderStrength = 4.2f;
            // Wider top/side engage so the player can re-grab from the roof to descend
            // without walking off the edge past the climb volume.
            ladderBehaviour.EngageHalfWidth = 1.05f;
            ladderBehaviour.EngageDepth = 1.05f;
            ladderBehaviour.SyncClimbVolume();

            GameObject physicalCollider = new GameObject("Ladder_Physical_Collider");
            physicalCollider.transform.SetParent(ladder, false);
            BoxCollider blocker = physicalCollider.AddComponent<BoxCollider>();
            blocker.center = new Vector3(0f, height * 0.5f, 0f);
            // Just the rung plane: thick enough that the capsule cannot squeeze between the
            // rails, thin enough to leave the climbing volume free.
            blocker.size = new Vector3(1.12f, height, 0.26f);
            blocker.isTrigger = false;

            Box("Rail_Left", ladder, new Vector3(-0.48f, height * 0.5f, 0f), new Vector3(0.12f, height, 0.12f), MetalMaterial);
            Box("Rail_Right", ladder, new Vector3(0.48f, height * 0.5f, 0f), new Vector3(0.12f, height, 0.12f), MetalMaterial);

            int rungCount = Mathf.Max(2, Mathf.FloorToInt(height / 0.31f));
            for (int i = 0; i < rungCount; i++)
            {
                float y = 0.22f + i * 0.31f;
                GameObject rung = Box(
                    "Rung_" + i,
                    ladder,
                    new Vector3(0f, y, 0f),
                    new Vector3(1.08f, 0.1f, 0.12f),
                    MetalMaterial);
                Collider rungCollider = rung.GetComponent<Collider>();
                if (rungCollider != null)
                {
                    rungCollider.enabled = false;
                }
            }

            return ladder;
        }

private MiniVanPanelkaRoomDoor PlaceDoor(
            string name,
            Transform parent,
            Vector3 hingePosition,
            bool wallPlaneX,
            float openAngle)
        {
            Transform hinge = Group(name, parent);
            hinge.localPosition = hingePosition;
            Transform pivot = Group("Door_Runtime_Pivot", hinge);

            MiniVanPanelkaRoomDoor interactable = hinge.gameObject.AddComponent<MiniVanPanelkaRoomDoor>();
            interactable.Type = MiniVanPanelkaInteractableType.Door;
            interactable.Pivot = pivot;
            interactable.OpenEuler = new Vector3(0f, Mathf.Clamp(openAngle, -117f, 117f), 0f);
            interactable.Message = "Door";

            Vector3 panelSize = wallPlaneX
                ? new Vector3(0.12f, DoorHeight, DoorWidth)
                : new Vector3(DoorWidth, DoorHeight, 0.12f);
            Vector3 panelOffset = wallPlaneX
                ? new Vector3(0f, DoorHeight * 0.5f, DoorWidth * 0.5f)
                : new Vector3(DoorWidth * 0.5f, DoorHeight * 0.5f, 0f);

            GameObject doorPanel = Box("Door_Panel", pivot, panelOffset, panelSize, DoorMaterial);
            if (doorPanel != null)
            {
                NavMeshModifier modifier = doorPanel.GetComponent<NavMeshModifier>();
                if (modifier == null)
                {
                    modifier = doorPanel.AddComponent<NavMeshModifier>();
                }

                modifier.ignoreFromBuild = true;
            }

            return interactable;
        }

        private void StairwellWallXWithOpenings(
            string name,
            Transform parent,
            float x,
            float zCenter,
            float zSpan,
            float yBase,
            IReadOnlyList<float> openingCenters,
            Material lowerMaterial,
            Material upperMaterial)
        {
            Transform wall = Group(name, parent);
            List<float> centers = new List<float>(openingCenters);
            centers.Sort();
            float cursor = zCenter - zSpan * 0.5f;
            float end = zCenter + zSpan * 0.5f;
            for (int i = 0; i < centers.Count; i++)
            {
                float openingMin = centers[i] - DoorWidth * 0.5f;
                float openingMax = centers[i] + DoorWidth * 0.5f;
                if (openingMin > cursor)
                {
                    StairwellSolidWallX(
                        "Solid_" + i,
                        wall,
                        x,
                        (cursor + openingMin) * 0.5f,
                        openingMin - cursor,
                        yBase,
                        lowerMaterial,
                        upperMaterial);
                }
                Box(
                    "Lintel_" + i,
                    wall,
                    new Vector3(
                        x,
                        yBase + DoorHeight + (StoreyHeight - DoorHeight) * 0.5f,
                        centers[i]),
                    new Vector3(
                        WallThickness,
                        StoreyHeight - DoorHeight,
                        DoorWidth),
                    upperMaterial);
                cursor = Mathf.Max(cursor, openingMax);
            }
            if (cursor < end)
            {
                StairwellSolidWallX(
                    "Solid_End",
                    wall,
                    x,
                    (cursor + end) * 0.5f,
                    end - cursor,
                    yBase,
                    lowerMaterial,
                    upperMaterial);
            }
        }

        private void StairwellWallZWithOpenings(
            string name,
            Transform parent,
            float z,
            float xCenter,
            float xSpan,
            float yBase,
            IReadOnlyList<float> openingCenters,
            Material lowerMaterial,
            Material upperMaterial)
        {
            Transform wall = Group(name, parent);
            List<float> centers = new List<float>(openingCenters);
            centers.Sort();
            float cursor = xCenter - xSpan * 0.5f;
            float end = xCenter + xSpan * 0.5f;
            for (int i = 0; i < centers.Count; i++)
            {
                float openingMin = centers[i] - DoorWidth * 0.5f;
                float openingMax = centers[i] + DoorWidth * 0.5f;
                if (openingMin > cursor)
                {
                    StairwellSolidWallZ(
                        "Solid_" + i,
                        wall,
                        z,
                        (cursor + openingMin) * 0.5f,
                        openingMin - cursor,
                        yBase,
                        lowerMaterial,
                        upperMaterial);
                }
                Box(
                    "Lintel_" + i,
                    wall,
                    new Vector3(
                        centers[i],
                        yBase + DoorHeight + (StoreyHeight - DoorHeight) * 0.5f,
                        z),
                    new Vector3(
                        DoorWidth,
                        StoreyHeight - DoorHeight,
                        WallThickness),
                    upperMaterial);
                cursor = Mathf.Max(cursor, openingMax);
            }
            if (cursor < end)
            {
                StairwellSolidWallZ(
                    "Solid_End",
                    wall,
                    z,
                    (cursor + end) * 0.5f,
                    end - cursor,
                    yBase,
                    lowerMaterial,
                    upperMaterial);
            }
        }

        private void StairwellSolidWallX(
            string name,
            Transform parent,
            float x,
            float zCenter,
            float zSpan,
            float yBase,
            Material lowerMaterial,
            Material upperMaterial)
        {
            const float greenHeight = 1.25f;
            Transform wall = Group(name, parent);
            Box("Green_Lower", wall,
                new Vector3(x, yBase + greenHeight * 0.5f, zCenter),
                new Vector3(WallThickness, greenHeight, zSpan), lowerMaterial);
            Box("White_Upper", wall,
                new Vector3(
                    x,
                    yBase + greenHeight + (StoreyHeight - greenHeight) * 0.5f,
                    zCenter),
                new Vector3(
                    WallThickness, StoreyHeight - greenHeight, zSpan), upperMaterial);
        }

        private void StairwellSolidWallZ(
            string name,
            Transform parent,
            float z,
            float xCenter,
            float xSpan,
            float yBase,
            Material lowerMaterial,
            Material upperMaterial)
        {
            const float greenHeight = 1.25f;
            Transform wall = Group(name, parent);
            Box("Green_Lower", wall,
                new Vector3(xCenter, yBase + greenHeight * 0.5f, z),
                new Vector3(xSpan, greenHeight, WallThickness), lowerMaterial);
            Box("White_Upper", wall,
                new Vector3(
                    xCenter,
                    yBase + greenHeight + (StoreyHeight - greenHeight) * 0.5f,
                    z),
                new Vector3(
                    xSpan, StoreyHeight - greenHeight, WallThickness), upperMaterial);
        }

        private void FacadeCladdingZWithOpenings(
            string name,
            Transform parent,
            float z,
            float xCenter,
            float xSpan,
            float yBase,
            IReadOnlyList<float> openingCenters,
            Material material)
        {
            if (material == null)
                return;

            const float claddingThickness = 0.06f;
            float claddingOffset = WallThickness * 0.5f + claddingThickness * 0.5f + 0.01f;
            float claddingZ = z + Mathf.Sign(z) * claddingOffset;
            Transform wall = Group(name, parent);
            List<float> centers = new List<float>(openingCenters);
            centers.Sort();

            float cursor = xCenter - xSpan * 0.5f;
            float end = xCenter + xSpan * 0.5f;
            for (int i = 0; i < centers.Count; i++)
            {
                float openingMin = centers[i] - DoorWidth * 0.5f;
                float openingMax = centers[i] + DoorWidth * 0.5f;
                if (openingMin > cursor)
                {
                    Box(
                        "Facade_Solid_" + i,
                        wall,
                        new Vector3(
                            (cursor + openingMin) * 0.5f,
                            yBase + StoreyHeight * 0.5f,
                            claddingZ),
                        new Vector3(
                            openingMin - cursor,
                            StoreyHeight + FacadeVerticalOverlap * 2f,
                            claddingThickness),
                        material);
                }

                Box(
                    "Facade_Lintel_" + i,
                    wall,
                    new Vector3(
                        centers[i],
                        yBase + DoorHeight +
                        (StoreyHeight - DoorHeight + FacadeVerticalOverlap) * 0.5f,
                        claddingZ),
                    new Vector3(
                        DoorWidth,
                        StoreyHeight - DoorHeight + FacadeVerticalOverlap,
                        claddingThickness),
                    material);
                cursor = Mathf.Max(cursor, openingMax);
            }

            if (cursor < end)
            {
                Box(
                    "Facade_Solid_End",
                    wall,
                    new Vector3(
                        (cursor + end) * 0.5f,
                        yBase + StoreyHeight * 0.5f,
                        claddingZ),
                    new Vector3(
                        end - cursor,
                        StoreyHeight + FacadeVerticalOverlap * 2f,
                        claddingThickness),
                    material);
            }
        }

        private void WallXWithOpenings(
            string name,
            Transform parent,
            float x,
            float zCenter,
            float zSpan,
            float yBase,
            IReadOnlyList<float> openingCenters,
            Material material)
        {
            Transform wall = Group(name, parent);
            List<float> centers = new List<float>(openingCenters);
            centers.Sort();

            float cursor = zCenter - zSpan * 0.5f;
            float end = zCenter + zSpan * 0.5f;
            for (int i = 0; i < centers.Count; i++)
            {
                float openingMin = centers[i] - DoorWidth * 0.5f;
                float openingMax = centers[i] + DoorWidth * 0.5f;
                if (openingMin > cursor)
                {
                    Box(
                        "Solid_" + i,
                        wall,
                        new Vector3(x, yBase + StoreyHeight * 0.5f, (cursor + openingMin) * 0.5f),
                        new Vector3(WallThickness, StoreyHeight, openingMin - cursor),
                        material);
                }

                Box(
                    "Lintel_" + i,
                    wall,
                    new Vector3(x, yBase + DoorHeight + (StoreyHeight - DoorHeight) * 0.5f, centers[i]),
                    new Vector3(
                        WallThickness, StoreyHeight - DoorHeight, DoorWidth),
                    material);
                cursor = openingMax;
            }

            if (cursor < end)
            {
                Box(
                    "Solid_End",
                    wall,
                    new Vector3(x, yBase + StoreyHeight * 0.5f, (cursor + end) * 0.5f),
                    new Vector3(WallThickness, StoreyHeight, end - cursor),
                    material);
            }
        }

        private void WallZWithOpenings(
            string name,
            Transform parent,
            float z,
            float xCenter,
            float xSpan,
            float yBase,
            IReadOnlyList<float> openingCenters,
            Material material)
        {
            Transform wall = Group(name, parent);
            List<float> centers = new List<float>(openingCenters);
            centers.Sort();

            float cursor = xCenter - xSpan * 0.5f;
            float end = xCenter + xSpan * 0.5f;
            for (int i = 0; i < centers.Count; i++)
            {
                float openingMin = centers[i] - DoorWidth * 0.5f;
                float openingMax = centers[i] + DoorWidth * 0.5f;
                if (openingMin > cursor)
                {
                    Box(
                        "Solid_" + i,
                        wall,
                        new Vector3((cursor + openingMin) * 0.5f, yBase + StoreyHeight * 0.5f, z),
                        new Vector3(openingMin - cursor, StoreyHeight, WallThickness),
                        material);
                }

                Box(
                    "Lintel_" + i,
                    wall,
                    new Vector3(centers[i], yBase + DoorHeight + (StoreyHeight - DoorHeight) * 0.5f, z),
                    new Vector3(
                        DoorWidth, StoreyHeight - DoorHeight, WallThickness),
                    material);
                cursor = openingMax;
            }

            if (cursor < end)
            {
                Box(
                    "Solid_End",
                    wall,
                    new Vector3((cursor + end) * 0.5f, yBase + StoreyHeight * 0.5f, z),
                    new Vector3(end - cursor, StoreyHeight, WallThickness),
                    material);
            }
        }

private void WindowWallX(
            string name,
            Transform parent,
            float x,
            float zCenter,
            float zSpan,
            float yBase,
            IReadOnlyList<float> windowCenters)
        {
            const float windowBottom = 0.95f;
            const float windowTop = 2.45f;
            float windowWidth = FitWindowWidth(zSpan, windowCenters.Count, 1.6f);
            Transform wall = Group(name, parent);
            List<float> centers = new List<float>(EvenCenters(
                zCenter - zSpan * 0.5f,
                zCenter + zSpan * 0.5f,
                windowCenters.Count,
                windowWidth));

            float cursor = zCenter - zSpan * 0.5f;
            float end = zCenter + zSpan * 0.5f;
            for (int i = 0; i < centers.Count; i++)
            {
                float min = centers[i] - windowWidth * 0.5f;
                float max = centers[i] + windowWidth * 0.5f;
                if (min > cursor)
                {
                    Box(
                        "Solid_" + i,
                        wall,
                        new Vector3(x, yBase + StoreyHeight * 0.5f, (cursor + min) * 0.5f),
                        new Vector3(WallThickness, StoreyHeight, min - cursor),
                        ExteriorMaterial);
                }

                Box("Sill_" + i, wall, new Vector3(x, yBase + windowBottom * 0.5f, centers[i]), new Vector3(WallThickness, windowBottom, windowWidth), ExteriorMaterial);
                Box("Lintel_" + i, wall, new Vector3(x, yBase + windowTop + (StoreyHeight - windowTop) * 0.5f, centers[i]), new Vector3(WallThickness, StoreyHeight - windowTop, windowWidth), ExteriorMaterial);
                Box("Glass_" + i, wall, new Vector3(x, yBase + (windowBottom + windowTop) * 0.5f, centers[i]), new Vector3(0.06f, windowTop - windowBottom, windowWidth), GlassMaterial);
                cursor = max;
            }

            if (cursor < end)
            {
                Box(
                    "Solid_End",
                    wall,
                    new Vector3(x, yBase + StoreyHeight * 0.5f, (cursor + end) * 0.5f),
                    new Vector3(WallThickness, StoreyHeight, end - cursor),
                    ExteriorMaterial);
            }
        }

private void WindowWallZMultiple(
            string name,
            Transform parent,
            float z,
            float xCenter,
            float xSpan,
            float yBase,
            IReadOnlyList<float> windowCenters)
        {
            const float windowBottom = 0.95f;
            const float windowTop = 2.45f;
            float windowWidth = FitWindowWidth(xSpan, windowCenters.Count, 1.35f);
            Transform wall = Group(name, parent);
            List<float> centers = new List<float>(EvenCenters(
                xCenter - xSpan * 0.5f,
                xCenter + xSpan * 0.5f,
                windowCenters.Count,
                windowWidth));

            float cursor = xCenter - xSpan * 0.5f;
            float end = xCenter + xSpan * 0.5f;
            for (int i = 0; i < centers.Count; i++)
            {
                float min = centers[i] - windowWidth * 0.5f;
                float max = centers[i] + windowWidth * 0.5f;
                if (min > cursor)
                {
                    Box(
                        "Solid_" + i,
                        wall,
                        new Vector3((cursor + min) * 0.5f, yBase + StoreyHeight * 0.5f, z),
                        new Vector3(min - cursor, StoreyHeight, WallThickness),
                        ExteriorMaterial);
                }

                Box("Sill_" + i, wall, new Vector3(centers[i], yBase + windowBottom * 0.5f, z), new Vector3(windowWidth, windowBottom, WallThickness), ExteriorMaterial);
                Box("Lintel_" + i, wall, new Vector3(centers[i], yBase + windowTop + (StoreyHeight - windowTop) * 0.5f, z), new Vector3(windowWidth, StoreyHeight - windowTop, WallThickness), ExteriorMaterial);
                Box("Glass_" + i, wall, new Vector3(centers[i], yBase + (windowBottom + windowTop) * 0.5f, z), new Vector3(windowWidth, windowTop - windowBottom, 0.06f), GlassMaterial);
                cursor = max;
            }

            if (cursor < end)
            {
                Box(
                    "Solid_End",
                    wall,
                    new Vector3((cursor + end) * 0.5f, yBase + StoreyHeight * 0.5f, z),
                    new Vector3(end - cursor, StoreyHeight, WallThickness),
                    ExteriorMaterial);
            }
        }

        private static void ApplyMaterialToHierarchy(Transform root, Material material)
        {
            if (root == null || material == null)
                return;
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
                renderers[i].sharedMaterial = material;
            material.enableInstancing = true;
        }

        private GameObject Box(
            string name,
            Transform parent,
            Vector3 localPosition,
            Vector3 size,
            Material material)
        {
            GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = name;
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = localPosition;
            obj.transform.localRotation = Quaternion.identity;
            obj.transform.localScale = size;

            MeshRenderer renderer = obj.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = material == GlassMaterial
                    ? ShadowCastingMode.Off
                    : ShadowCastingMode.On;
                renderer.receiveShadows = material != GlassMaterial;
            }

            if (material != null)
            {
                material.enableInstancing = true;
            }
            return obj;
        }

        private static Transform Group(string name, Transform parent)
        {
            GameObject group = new GameObject(name);
            group.transform.SetParent(parent, false);
            return group.transform;
        }

        private void ConfigurePanelkaNavigation(Transform root)
        {
            if (root == null)
            {
                return;
            }

            NavMeshSurface surface = root.GetComponent<NavMeshSurface>();
            if (surface == null)
            {
                surface = root.gameObject.AddComponent<NavMeshSurface>();
            }

            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.overrideVoxelSize = true;
            surface.voxelSize = Mathf.Max(0.04f, ZombieNavMeshVoxelSize);
            surface.overrideTileSize = true;
            surface.tileSize = 128;
            surface.BuildNavMesh();
        }

        private void ConfigurePanelkaZombies(Transform root)
        {
            if (root == null || !SpawnPanelkaZombies || PanelkaZombieCount <= 0)
            {
                return;
            }

            MiniVanPanelkaZombieSpawnController controller = root.GetComponent<MiniVanPanelkaZombieSpawnController>();
            if (controller == null)
            {
                controller = root.gameObject.AddComponent<MiniVanPanelkaZombieSpawnController>();
            }

            controller.ZombiePrefab = ResolvePanelkaZombiePrefab();
            controller.ZombieCount = PanelkaZombieCount;
            controller.GenerationSeed = GenerationSeed;
            controller.SpawnOnServerStart = true;
            controller.AllowOfflineSpawn = AllowOfflineZombieSpawn;
            controller.MainRouteOnly = ZombiesOnMainRouteOnly;
        }

        private GameObject ResolvePanelkaZombiePrefab()
        {
            if (ZombiePrefab != null)
            {
                return ZombiePrefab;
            }

#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/MiniVan Game/Prefabs/Characters/Zombies/Zombie.prefab");
#else
            return null;
#endif
        }
    

private int ResolveSessionSeed()
        {
            string sharedToken = !string.IsNullOrWhiteSpace(MiniVanLaunchState.LobbyId)
                ? MiniVanLaunchState.LobbyId
                : !string.IsNullOrWhiteSpace(MiniVanLaunchState.LastJoinCode)
                ? MiniVanLaunchState.LastJoinCode
                : MiniVanLaunchState.JoinCode;

            if (!string.IsNullOrWhiteSpace(sharedToken))
            {
                return StableHash(sharedToken);
            }

            if (MiniVanLaunchState.ActiveMode == MiniVanLaunchMode.Host ||
                MiniVanLaunchState.ActiveMode == MiniVanLaunchMode.Client ||
                MiniVanLaunchState.ActiveMode == MiniVanLaunchMode.Server)
            {
                return GenerationSeed;
            }

            return unchecked(
                System.Environment.TickCount ^
                (int)System.DateTime.UtcNow.Ticks ^
                GetInstanceID());
        }

        private static int StableHash(string value)
        {
            unchecked
            {
                uint hash = 2166136261;
                for (int i = 0; i < value.Length; i++)
                {
                    hash ^= char.ToUpperInvariant(value[i]);
                    hash *= 16777619;
                }

                return (int)hash;
            }
        }

        private void BuildRouteAccessPlan(int floorCount, int seed)
        {
            routeMainSlotByFloor = new int[floorCount];
            routeArrivalSlotByFloor = new int[floorCount];
            routeKeySlotByFloor = new int[floorCount];
            routeTransitionByUpperFloor =
                new RouteTransitionType[floorCount + 1];
            routeHoleUpperFloorNumber = floorCount > 1 ? floorCount : -1;
            System.Random random = new System.Random(seed ^ 0x51A7C3);

            for (int i = 0; i < floorCount; i++)
            {
                routeArrivalSlotByFloor[i] = -1;
                routeKeySlotByFloor[i] = -1;
            }

            int topIndex = floorCount - 1;
            routeMainSlotByFloor[topIndex] = random.Next(0, 4);
            RouteTransitionType[] balancedTransitions =
                BuildBalancedRouteTransitions(
                    Mathf.Max(0, floorCount - 1),
                    random,
                    ForcedRouteTransition);
            int maxApartmentsInLine = Mathf.Max(2, RouteVerticalApartmentsInSingleLine);
            int apartmentsInCurrentLine = 1;
            for (int upperFloorNumber = floorCount;
                 upperFloorNumber >= 2;
                 upperFloorNumber--)
            {
                int upperIndex = upperFloorNumber - 1;
                int lowerIndex = upperIndex - 1;
                RouteTransitionType transition =
                    balancedTransitions[floorCount - upperFloorNumber];
                routeTransitionByUpperFloor[upperFloorNumber] = transition;

                int arrivalSlot = routeMainSlotByFloor[upperIndex];
                if (transition == RouteTransitionType.Pipe)
                {
                    // The pipe runs down the shared outer wall into the apartment next
                    // door. Dropping straight into the one below belongs to balconies
                    // and floor holes.
                    arrivalSlot = GetApartmentSharingOuterWall(arrivalSlot);
                }

                routeArrivalSlotByFloor[lowerIndex] = arrivalSlot;

                bool switchApartment;
                if (transition == RouteTransitionType.None)
                {
                    // Stairs already send the player through the landing, so the vertical
                    // line ends here and the apartment below is entered through its door.
                    switchApartment = false;
                    apartmentsInCurrentLine = 1;
                }
                else
                {
                    apartmentsInCurrentLine++;
                    switchApartment = apartmentsInCurrentLine >= maxApartmentsInLine;
                    if (switchApartment)
                        apartmentsInCurrentLine = 1;
                }

                routeMainSlotByFloor[lowerIndex] = switchApartment
                    ? GetOppositeApartmentOnSameFacade(arrivalSlot)
                    : arrivalSlot;
            }

            if (floorCount >= 5)
            {
                AssignRouteKeyFloor(random, floorCount);
            }
        }

        private void AssignRouteKeyFloor(System.Random random, int floorCount)
        {
            // A key only makes sense when the door it opens is the way in. If the locked
            // apartment is the one the route drops into from above, the player would enter
            // it from the inside and the key would be dead weight, so walk down until a
            // floor whose main apartment is reached from the landing.
            for (int keyFloorIndex = floorCount - 1; keyFloorIndex >= 1; keyFloorIndex--)
            {
                if (!IsDoorEntryRouteApartment(keyFloorIndex - 1))
                    continue;

                routeKeySlotByFloor[keyFloorIndex] =
                    SelectAvailableRouteSlot(
                        random,
                        routeMainSlotByFloor[keyFloorIndex],
                        routeArrivalSlotByFloor[keyFloorIndex]);
                return;
            }
        }

        private bool IsDoorEntryRouteApartment(int floorIndex)
        {
            if (routeMainSlotByFloor == null ||
                routeArrivalSlotByFloor == null ||
                floorIndex < 0 ||
                floorIndex >= routeMainSlotByFloor.Length ||
                floorIndex >= routeArrivalSlotByFloor.Length)
            {
                return false;
            }

            // Stairs keep the recorded arrival slot even though nobody drops in through it,
            // so the apartment below is still reached from the landing.
            if (IsStairTransitionFromFloor(floorIndex + 2))
                return true;

            return routeArrivalSlotByFloor[floorIndex] < 0 ||
                   routeArrivalSlotByFloor[floorIndex] !=
                   routeMainSlotByFloor[floorIndex];
        }

        private RouteTransitionType[] BuildBalancedRouteTransitions(
            int transitionCount,
            System.Random random,
            MiniVanPanelkaForcedRouteTransition forced)
        {
            RouteTransitionType[] result =
                new RouteTransitionType[transitionCount];
            if (transitionCount <= 0)
            {
                return result;
            }

            if (forced != MiniVanPanelkaForcedRouteTransition.Random)
            {
                RouteTransitionType forcedType = ToRouteTransitionType(forced);
                for (int i = 0; i < transitionCount; i++)
                {
                    result[i] = forcedType;
                }

                return result;
            }

            RouteTransitionType[] cycle =
            {
                RouteTransitionType.Hole,
                RouteTransitionType.Balcony,
                RouteTransitionType.None,
                RouteTransitionType.Pipe
            };

            int[] counts = BuildRouteTransitionCounts(
                transitionCount,
                cycle,
                random);

            int offset = random.Next(0, cycle.Length);
            int direction = random.Next(0, 2) == 0 ? 1 : -1;
            int cursor = 0;
            int cycleIndex = offset;
            int guard = transitionCount * cycle.Length + cycle.Length;
            while (cursor < transitionCount && guard-- > 0)
            {
                int slot = cycleIndex % cycle.Length;
                if (slot < 0)
                    slot += cycle.Length;
                if (counts[slot] > 0)
                {
                    counts[slot]--;
                    result[cursor] = cycle[slot];
                    cursor++;
                }

                cycleIndex += direction;
            }

            // Budgets can leave floors without an allowed special transition; the
            // staircase is the only descent that always exists, so it fills the rest.
            while (cursor < transitionCount)
            {
                result[cursor] = RouteTransitionType.None;
                cursor++;
            }

            return result;
        }

        private int[] BuildRouteTransitionCounts(
            int transitionCount,
            RouteTransitionType[] cycle,
            System.Random random)
        {
            int[] counts = new int[cycle.Length];
            int[] limits = new int[cycle.Length];
            bool anyAllowed = false;
            for (int i = 0; i < cycle.Length; i++)
            {
                MiniVanPanelkaRouteTransitionBudget budget =
                    GetRouteTransitionBudget(cycle[i]);
                limits[i] = Mathf.Min(budget.AllowedMax, transitionCount);
                anyAllowed |= limits[i] > 0;
            }

            if (!anyAllowed)
            {
                // Every transition switched off would leave nothing to generate, so
                // the plan falls back to the unrestricted mix.
                for (int i = 0; i < cycle.Length; i++)
                {
                    limits[i] = transitionCount;
                }
            }

            int placed = 0;
            for (int i = 0; i < cycle.Length && placed < transitionCount; i++)
            {
                int minimum = Mathf.Min(
                    GetRouteTransitionBudget(cycle[i]).RequiredMin,
                    Mathf.Min(limits[i], transitionCount - placed));
                counts[i] = minimum;
                placed += minimum;
            }

            int offset = random.Next(0, cycle.Length);
            int direction = random.Next(0, 2) == 0 ? 1 : -1;
            int cycleIndex = offset;
            int guard = transitionCount * cycle.Length + cycle.Length;
            while (placed < transitionCount && guard-- > 0)
            {
                int slot = cycleIndex % cycle.Length;
                if (slot < 0)
                    slot += cycle.Length;
                if (counts[slot] < limits[slot])
                {
                    counts[slot]++;
                    placed++;
                }

                cycleIndex += direction;
            }

            return counts;
        }

        private MiniVanPanelkaRouteTransitionBudget GetRouteTransitionBudget(
            RouteTransitionType type)
        {
            switch (type)
            {
                case RouteTransitionType.Hole:
                    return RouteHoleBudget;
                case RouteTransitionType.Balcony:
                    return RouteBalconyBudget;
                case RouteTransitionType.Pipe:
                    // Every facade walled in by a neighbouring entrance leaves the pipe
                    // nowhere to run, so the floor takes another transition instead.
                    return HasFreePipeFacade()
                        ? RoutePipeBudget
                        : new MiniVanPanelkaRouteTransitionBudget(false, 0, 0);
                default:
                    return RouteStairsBudget;
            }
        }

        private static RouteTransitionType ToRouteTransitionType(
            MiniVanPanelkaForcedRouteTransition forced)
        {
            switch (forced)
            {
                case MiniVanPanelkaForcedRouteTransition.Hole:
                    return RouteTransitionType.Hole;
                case MiniVanPanelkaForcedRouteTransition.Balcony:
                    return RouteTransitionType.Balcony;
                case MiniVanPanelkaForcedRouteTransition.Pipe:
                    return RouteTransitionType.Pipe;
                case MiniVanPanelkaForcedRouteTransition.Stairs:
                    return RouteTransitionType.None;
                default:
                    return RouteTransitionType.None;
            }
        }

        private static int SelectAvailableRouteSlot(
            System.Random random,
            int mainSlot,
            int arrivalSlot)
        {
            List<int> available = new List<int>(4);
            for (int slot = 0; slot < 4; slot++)
            {
                if (slot != mainSlot && slot != arrivalSlot)
                    available.Add(slot);
            }
            return available.Count > 0
                ? available[random.Next(0, available.Count)]
                : GetOppositeApartmentOnSameFacade(mainSlot);
        }

        private static int NextDifferentSlot(System.Random random, int excluded)
        {
            int value = random.Next(0, 3);
            return value >= excluded ? value + 1 : value;
        }

        private bool IsHoleTransitionFromFloor(int upperFloorNumber)
        {
            return GetRouteTransitionType(upperFloorNumber) ==
                   RouteTransitionType.Hole;
        }

        private bool IsBalconyTransitionFromFloor(int upperFloorNumber)
        {
            return GetRouteTransitionType(upperFloorNumber) ==
                   RouteTransitionType.Balcony;
        }

        private bool IsStairTransitionFromFloor(int upperFloorNumber)
        {
            return GetRouteTransitionType(upperFloorNumber) ==
                   RouteTransitionType.None &&
                   upperFloorNumber >= 2 &&
                   routeMainSlotByFloor != null &&
                   upperFloorNumber <= routeMainSlotByFloor.Length;
        }

        private RouteTransitionType GetRouteTransitionType(
            int upperFloorNumber)
        {
            if (routeTransitionByUpperFloor == null ||
                upperFloorNumber < 0 ||
                upperFloorNumber >= routeTransitionByUpperFloor.Length)
            {
                return RouteTransitionType.None;
            }
            return routeTransitionByUpperFloor[upperFloorNumber];
        }

        private void SetRouteTransitionToHole(int upperFloorNumber)
        {
            if (routeTransitionByUpperFloor == null ||
                upperFloorNumber < 2 ||
                upperFloorNumber >= routeTransitionByUpperFloor.Length)
            {
                return;
            }
            routeTransitionByUpperFloor[upperFloorNumber] =
                RouteTransitionType.Hole;
        }

        private bool ShouldBlockStairToNextFloor(int lowerFloorIndex)
        {
            int upperFloorNumber = lowerFloorIndex + 2;
            return upperFloorNumber >= 2 &&
                   upperFloorNumber <= 9 &&
                   !IsStairTransitionFromFloor(upperFloorNumber);
        }

        private bool IsBalconyRouteFloor(int floorNumber)
        {
            return IsBalconyTransitionFromFloor(floorNumber) ||
                   IsBalconyTransitionFromFloor(floorNumber + 1);
        }

private MiniVanPanelkaApartmentRouteRole GetApartmentRouteRole(
            int floorIndex,
            int apartmentSlot)
        {
            if (routeMainSlotByFloor == null ||
                floorIndex < 0 ||
                floorIndex >= routeMainSlotByFloor.Length)
            {
                return MiniVanPanelkaApartmentRouteRole.Inaccessible;
            }

            if (routeMainSlotByFloor[floorIndex] == apartmentSlot)
            {
                return MiniVanPanelkaApartmentRouteRole.MainRoute;
            }

            if (routeArrivalSlotByFloor != null &&
                floorIndex < routeArrivalSlotByFloor.Length &&
                routeArrivalSlotByFloor[floorIndex] == apartmentSlot)
            {
                return MiniVanPanelkaApartmentRouteRole.TransferArrival;
            }

            if (routeKeySlotByFloor != null &&
                floorIndex < routeKeySlotByFloor.Length &&
                routeKeySlotByFloor[floorIndex] == apartmentSlot)
            {
                return MiniVanPanelkaApartmentRouteRole.KeySource;
            }

            return MiniVanPanelkaApartmentRouteRole.Inaccessible;
        }

        private static int GetApartmentSlot(string cornerName)
        {
            switch (cornerName)
            {
                case "NW":
                    return 0;
                case "NE":
                    return 1;
                case "SW":
                    return 2;
                default:
                    return 3;
            }
        }


private bool FloorUsesRouteKey(int floorIndex)
        {
            return routeKeySlotByFloor != null &&
                floorIndex >= 0 &&
                floorIndex < routeKeySlotByFloor.Length &&
                routeKeySlotByFloor[floorIndex] >= 0;
        }

private string GetRouteKeyId(int floorIndex)
        {
            int targetFloorIndex = floorIndex - 1;
            if (routeMainSlotByFloor == null ||
                targetFloorIndex < 0 ||
                targetFloorIndex >= routeMainSlotByFloor.Length ||
                routeMainSlotByFloor[targetFloorIndex] < 0)
            {
                return string.Empty;
            }

            int targetApartmentNumber = targetFloorIndex * 4 + routeMainSlotByFloor[targetFloorIndex] + 1;
            return "panelka-" + GenerationSeed + "-apartment-key-" +
                   targetApartmentNumber.ToString("00");
        }

private string GetRouteDoorRequiredKeyId(int floorIndex)
        {
            int keySourceFloorIndex = floorIndex + 1;
            if (!FloorUsesRouteKey(keySourceFloorIndex))
            {
                return string.Empty;
            }

            return GetRouteKeyId(keySourceFloorIndex);
        }

        private void BuildRouteKeyPickup(
            Transform apartment,
            int floorIndex,
            float yBase)
        {
            string keyId = ResolveRouteTargetDoorKeyId(floorIndex);
            Transform table = FindDescendantByName(apartment, "Kitchen_Table_Set");
            Transform sofa = FindDescendantByName(apartment, "Soviet_Sofa");
            Transform support = floorIndex % 2 == 0
                ? (table != null ? table : sofa)
                : (sofa != null ? sofa : table);

            Vector3 localPosition = new Vector3(0f, yBase + FloorSurfaceOffset + 0.82f, 0f);
            if (support != null)
            {
                Transform surface = support.Find(
                    support.name == "Kitchen_Table_Set" ? "TableTop" : "Seat");
                if (surface == null)
                {
                    surface = support;
                }

                Bounds bounds;
                if (TryGetWorldRenderBounds(surface, out bounds))
                {
                    Vector3 worldPosition = new Vector3(
                        bounds.center.x,
                        bounds.max.y + 0.055f,
                        bounds.center.z);
                    localPosition = apartment.InverseTransformPoint(worldPosition);
                }
            }
            else
            {
                Transform kitchenMarker = FindDescendantContaining(apartment, "_KITCHEN_");
                if (kitchenMarker != null)
                {
                    localPosition = kitchenMarker.localPosition +
                        new Vector3(0f, FloorSurfaceOffset + 0.82f, 0f);
                }
            }

            Transform key = Group(
                "Route_Key_Floor_" + (floorIndex + 1).ToString("00"),
                apartment);
            key.localPosition = localPosition;
            key.localRotation = Quaternion.Euler(90f, floorIndex * 37f, 0f);

            Material keyMaterial = GetPanelkaKeyMaterial();
            Box("Shaft", key, new Vector3(0.07f, 0f, 0f),
                new Vector3(0.38f, 0.055f, 0.08f), keyMaterial);
            Box("Bow_Top", key, new Vector3(-0.19f, 0.10f, 0f),
                new Vector3(0.17f, 0.055f, 0.08f), keyMaterial);
            Box("Bow_Bottom", key, new Vector3(-0.19f, -0.10f, 0f),
                new Vector3(0.17f, 0.055f, 0.08f), keyMaterial);
            Box("Bow_Back", key, new Vector3(-0.27f, 0f, 0f),
                new Vector3(0.055f, 0.24f, 0.08f), keyMaterial);
            Box("Tooth_A", key, new Vector3(0.22f, -0.07f, 0f),
                new Vector3(0.07f, 0.14f, 0.08f), keyMaterial);
            Box("Tooth_B", key, new Vector3(0.31f, -0.05f, 0f),
                new Vector3(0.055f, 0.10f, 0.08f), keyMaterial);
            CreatePanelkaKeyNumberLabel(key, GetPanelkaKeyApartmentNumber(keyId));

            MiniVanApartmentKeyPickup pickup =
                key.gameObject.AddComponent<MiniVanApartmentKeyPickup>();
            pickup.Configure(keyId);
        }

private static string GetPanelkaKeyApartmentNumber(string keyId)
        {
            if (string.IsNullOrEmpty(keyId))
            {
                return string.Empty;
            }

            int separator = keyId.LastIndexOf('-');
            int apartmentNumber;
            if (separator < 0 ||
                separator >= keyId.Length - 1 ||
                !int.TryParse(keyId.Substring(separator + 1), out apartmentNumber))
            {
                return string.Empty;
            }

            return apartmentNumber.ToString();
        }

        private static void CreatePanelkaKeyNumberLabel(
            Transform parent,
            string apartmentNumber)
        {
            if (parent == null || string.IsNullOrEmpty(apartmentNumber))
            {
                return;
            }

            // A TextMesh reads correctly from the side its forward points away from, so each
            // face has to look back into the key, not out of it.
            CreatePanelkaKeyNumberFace(
                parent,
                "Apartment_Number_Front",
                apartmentNumber,
                0.046f,
                Quaternion.Euler(0f, 180f, 0f));
            CreatePanelkaKeyNumberFace(
                parent,
                "Apartment_Number_Back",
                apartmentNumber,
                -0.046f,
                Quaternion.identity);
        }

        private string ResolveRouteTargetDoorKeyId(int keySourceFloorIndex)
        {
            return GetRouteKeyId(keySourceFloorIndex);
        }

        private static void CreatePanelkaKeyNumberFace(
            Transform parent,
            string name,
            string apartmentNumber,
            float localZ,
            Quaternion localRotation)
        {
            GameObject label = new GameObject(name);
            label.transform.SetParent(parent, false);
            label.transform.localPosition = new Vector3(0.065f, 0.005f, localZ);
            label.transform.localRotation = localRotation;

            TextMesh text = label.AddComponent<TextMesh>();
            text.text = apartmentNumber;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = 64;
            text.characterSize = 0.012f;
            text.fontStyle = FontStyle.Bold;
            text.color = new Color(0.42f, 0.025f, 0.015f, 1f);

            MeshRenderer renderer = label.GetComponent<MeshRenderer>();
            Material depthMaterial = Resources.Load<Material>("Panelka_WorldTextDepth");
            if (renderer != null && depthMaterial != null)
            {
                renderer.sharedMaterial = depthMaterial;
            }
            label.AddComponent<MiniVanPanelkaWorldTextDepth>();
        }


        private Material GetPanelkaKeyMaterial()
        {
            if (KeyMaterial != null)
            {
                return KeyMaterial;
            }

            if (runtimeKeyMaterial != null)
            {
                return runtimeKeyMaterial;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            runtimeKeyMaterial = new Material(shader);
            runtimeKeyMaterial.name = "Panelka Key Yellow (Generated)";
            Color yellow = new Color(1f, 0.72f, 0.03f);
            runtimeKeyMaterial.color = yellow;
            if (runtimeKeyMaterial.HasProperty("_BaseColor"))
            {
                runtimeKeyMaterial.SetColor("_BaseColor", yellow);
            }
            return runtimeKeyMaterial;
        }

        private static Transform FindDescendantByName(Transform root, string exactName)
        {
            Transform[] children = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i].name == exactName)
                {
                    return children[i];
                }
            }
            return null;
        }

        private static Transform FindDescendantContaining(Transform root, string fragment)
        {
            Transform[] children = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i].name.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return children[i];
                }
            }
            return null;
        }

        private static bool TryGetWorldRenderBounds(Transform root, out Bounds bounds)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                bounds = default;
                return false;
            }

            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }
            return true;
        }


        private void BuildBalconyReturnRope(
            Transform parent,
            float yBase,
            int floorIndex,
            float platformDepth,
            float platformWidth)
        {
            float lowerFloorSurface = yBase - StoreyHeight + FloorSurfaceOffset;
            float ropeBaseY = lowerFloorSurface - 0.64f;
            float ropeHeight = StoreyHeight + 0.92f;
            const float ropeX = 1.10f;
            const float ropeZ = 0.95f;

            Transform rope = Group(
                "Route_Balcony_Hatch_Rope_From_" + (floorIndex + 1).ToString("00") +
                "_To_" + floorIndex.ToString("00"),
                parent);
            rope.localPosition = new Vector3(ropeX, ropeBaseY, ropeZ);
            rope.localRotation = Quaternion.identity;
            rope.localScale = Vector3.one;

            MiniVanLadder climb = rope.gameObject.AddComponent<MiniVanLadder>();
            climb.ClimbSpeed = 2.85f;
            climb.RoofEntryHeight = StoreyHeight + 0.24f;
            climb.RoofEntryPushSpeed = 3.8f;
            climb.RoofEntryLocalDirection = Vector3.back;
            climb.StickToLadderStrength = 5.5f;
            climb.EngageHalfWidth = 1.05f;
            climb.EngageDepth = 0.98f;

            BoxCollider trigger = rope.gameObject.GetComponent<BoxCollider>();
            trigger.center = new Vector3(0f, ropeHeight * 0.5f + 0.20f, 0f);
            trigger.size = new Vector3(1.80f, ropeHeight + 1.40f, 1.80f);
            trigger.isTrigger = true;
            BuildThinRopeCollider(rope, ropeHeight);

            Material ropeMaterial = FurnitureWoodMaterial != null
                ? FurnitureWoodMaterial
                : DoorMaterial;
            GameObject ropeVisual = Box(
                "Balcony_Hatch_Rope_Visual",
                rope,
                new Vector3(0f, ropeHeight * 0.5f, 0f),
                new Vector3(0.085f, ropeHeight, 0.085f),
                ropeMaterial);
            DisableGeneratedCollider(ropeVisual);

            for (int knotIndex = 0; knotIndex < 6; knotIndex++)
            {
                float knotY = 0.36f + knotIndex * 0.58f;
                GameObject knot = Box(
                    "Balcony_Hatch_Rope_Knot_" + knotIndex.ToString("00"),
                    rope,
                    new Vector3(0f, knotY, 0f),
                    new Vector3(0.15f, 0.09f, 0.15f),
                    ropeMaterial);
                knot.transform.localRotation = Quaternion.Euler(0f, knotIndex * 31f, 0f);
                DisableGeneratedCollider(knot);
            }

            GameObject anchor = Box(
                "Balcony_Hatch_Rope_Anchor",
                rope,
                new Vector3(0f, ropeHeight - 0.08f, 0f),
                new Vector3(0.32f, 0.14f, 0.32f),
                MetalMaterial != null ? MetalMaterial : ropeMaterial);
            DisableGeneratedCollider(anchor);
        }

private void BuildRouteHoleRope(
            Transform parent,
            float holeX,
            float holeZ,
            float yBase,
            int floorIndex)
        {
            float lowerFloorSurface = yBase - StoreyHeight + FloorSurfaceOffset;
            float ropeBaseY = lowerFloorSurface + 0.05f;
            float ropeHeight = StoreyHeight + 0.78f;
            float ropeX = holeX - 0.58f;

            Transform rope = Group(
                "Route_Return_Rope_From_" + (floorIndex + 1).ToString("00") +
                "_To_" + floorIndex.ToString("00"),
                parent);
            rope.localPosition = new Vector3(ropeX, ropeBaseY, holeZ);

            MiniVanLadder climb = rope.gameObject.AddComponent<MiniVanLadder>();
            climb.ClimbSpeed = 2.85f;
            climb.RoofEntryHeight = StoreyHeight + 0.17f;
            climb.RoofEntryPushSpeed = 3.8f;
            climb.RoofEntryLocalDirection = Vector3.left;
            climb.StickToLadderStrength = 5.5f;
            climb.EngageHalfWidth = 1.05f;
            climb.EngageDepth = 0.98f;

            BoxCollider trigger = rope.gameObject.GetComponent<BoxCollider>();
            trigger.center = new Vector3(0f, ropeHeight * 0.5f + 0.20f, 0f);
            trigger.size = new Vector3(1.80f, ropeHeight + 1.40f, 1.80f);
            trigger.isTrigger = true;
            BuildThinRopeCollider(rope, ropeHeight);

            Material ropeMaterial = FurnitureWoodMaterial != null
                ? FurnitureWoodMaterial
                : DoorMaterial;
            GameObject ropeVisual = Box(
                "Rope_Visual",
                rope,
                new Vector3(0f, ropeHeight * 0.5f, 0f),
                new Vector3(0.085f, ropeHeight, 0.085f),
                ropeMaterial);
            DisableGeneratedCollider(ropeVisual);

            for (int knotIndex = 0; knotIndex < 5; knotIndex++)
            {
                float knotY = 0.38f + knotIndex * 0.72f;
                GameObject knot = Box(
                    "Rope_Knot_" + knotIndex.ToString("00"),
                    rope,
                    new Vector3(0f, knotY, 0f),
                    new Vector3(0.14f, 0.10f, 0.14f),
                    ropeMaterial);
                knot.transform.localRotation = Quaternion.Euler(0f, knotIndex * 27f, 0f);
                DisableGeneratedCollider(knot);
            }

            GameObject anchor = Box(
                "Rope_Top_Anchor",
                rope,
                new Vector3(-0.20f, ropeHeight - 0.04f, 0f),
                new Vector3(0.48f, 0.11f, 0.14f),
                MetalMaterial);
            DisableGeneratedCollider(anchor);
        }

        private static void BuildThinRopeCollider(
            Transform rope,
            float ropeHeight)
        {
            GameObject colliderObject = new GameObject("Rope_Physical_Collider");
            colliderObject.transform.SetParent(rope, false);
            BoxCollider physical = colliderObject.AddComponent<BoxCollider>();
            physical.center = new Vector3(0f, ropeHeight * 0.5f, 0f);
            physical.size = new Vector3(0.16f, ropeHeight + 0.24f, 0.16f);
            physical.isTrigger = false;
        }

        private static void DisableGeneratedCollider(GameObject obj)
        {
            Collider collider = obj != null ? obj.GetComponent<Collider>() : null;
            if (collider != null)
            {
                collider.enabled = false;
            }
        }


        private bool IsPipeTransitionFromFloor(int upperFloorNumber)
        {
            return GetRouteTransitionType(upperFloorNumber) ==
                   RouteTransitionType.Pipe;
        }

        private bool IsPipeRouteFloor(int floorNumber)
        {
            return IsPipeTransitionFromFloor(floorNumber) ||
                   IsPipeTransitionFromFloor(floorNumber + 1);
        }

        private static int GetOppositeApartmentOnSameFacade(int apartmentSlot)
        {
            return (apartmentSlot & 1) == 0 ? apartmentSlot + 1 : apartmentSlot - 1;
        }

        /// <summary>
        /// Slots are NW=0, NE=1, SW=2, SE=3. Flipping the north/south bit keeps the
        /// apartment on the same outer X wall, which is where the pipe socket sits.
        /// When a neighbouring entrance is glued to that wall the pair flips to the
        /// north/south wall instead, so the run has a free facade to follow.
        /// </summary>
        private int GetApartmentSharingOuterWall(int apartmentSlot)
        {
            int wallFacade = (apartmentSlot & 1) == 1 ? 1 : 3;
            int depthFacade = (apartmentSlot & 2) == 0 ? 2 : 0;
            if (IsPipeFacadeBlockedByNeighbour(wallFacade) &&
                !IsPipeFacadeBlockedByNeighbour(depthFacade))
            {
                return apartmentSlot ^ 1;
            }

            return apartmentSlot ^ 2;
        }


private bool BuildPipeRoute(
            Transform apartment,
            MiniVanPanelkaApartmentTemplate template,
            int floorIndex,
            int apartmentSlot,
            MiniVanPanelkaApartmentRouteRole routeRole,
            float outerX,
            float innerX,
            float outerZ,
            float yBase,
            out Vector3 openingCenter)
        {
            openingCenter = Vector3.zero;
            int floorNumber = floorIndex + 1;
            bool isDeparture =
                routeRole == MiniVanPanelkaApartmentRouteRole.MainRoute &&
                IsPipeTransitionFromFloor(floorNumber);
            bool isArrival =
                routeArrivalSlotByFloor != null &&
                floorIndex >= 0 &&
                floorIndex < routeArrivalSlotByFloor.Length &&
                routeArrivalSlotByFloor[floorIndex] == apartmentSlot &&
                IsPipeTransitionFromFloor(floorNumber + 1);
            if (!isDeparture && !isArrival)
            {
                return false;
            }

            Vector3 preferredWindow = template != null &&
                                      template.PipeSocket != null
                ? template.PipeSocket.position
                : apartment.TransformPoint(new Vector3(
                    (outerX + innerX) * 0.5f,
                    yBase + 1.5f,
                    outerZ));
            // Both apartments hang their pipe socket on the same outer wall, so each end
            // uses its own pipe window and the run stays on one facade.
            Transform selectedGlass = FindNearestFacadeGlass(
                apartment,
                preferredWindow);
            if (selectedGlass == null)
            {
                return false;
            }

            openingCenter = apartment.InverseTransformPoint(selectedGlass.position);
            string windowId =
                "PIPE_WINDOW_G" + GenerationSeed.ToString() +
                "_F" + floorNumber.ToString("00") +
                "_APT_" + (floorIndex * 4 + apartmentSlot + 1).ToString("00");
            ConfigureCrackedRouteWindow(selectedGlass, windowId);

            if (isDeparture)
            {
                Transform generatedRoot = apartment;
                while (generatedRoot.parent != null &&
                       generatedRoot.name != GeneratedRootName)
                {
                    generatedRoot = generatedRoot.parent;
                }

                MiniVanPanelkaBreakableWindow lowerWindow =
                    FindPipeArrivalWindow(generatedRoot, floorNumber - 1);
                if (lowerWindow != null)
                {
                    BuildPipeTraversal(
                        apartment,
                        selectedGlass.position,
                        lowerWindow.transform.position,
                        floorNumber);
                }
            }

            return true;
        }

        public bool TryRedirectPipeRouteToEntrance(
            MiniVanPanelkaStage1Generator target,
            int siteIndex,
            out int sourceFloorNumber,
            out int targetFloorNumber)
        {
            sourceFloorNumber = -1;
            targetFloorNumber = -1;
            if (target == null ||
                target == this ||
                ExteriorOnlyLocked ||
                target.ExteriorOnlyLocked)
            {
                return false;
            }

            for (int floorNumber = FloorCount;
                 floorNumber >= 2;
                 floorNumber--)
            {
                if (IsPipeTransitionFromFloor(floorNumber))
                {
                    sourceFloorNumber = floorNumber;
                    break;
                }
            }
            if (sourceFloorNumber < 2)
                return false;

            MiniVanPanelkaBreakableWindow sourceWindow =
                FindPipeRouteWindow(
                    sourceFloorNumber,
                    MiniVanPanelkaApartmentRouteRole.MainRoute);
            if (sourceWindow == null)
                return false;

            int resolvedTargetFloorNumber = Mathf.Clamp(
                sourceFloorNumber - 2,
                1,
                target.FloorCount);
            targetFloorNumber = resolvedTargetFloorNumber;
            MiniVanPanelkaApartmentRouteMarker targetApartment =
                target.GetComponentsInChildren<
                        MiniVanPanelkaApartmentRouteMarker>(true)
                    .FirstOrDefault(marker =>
                        marker.FloorNumber == resolvedTargetFloorNumber &&
                        marker.Role ==
                        MiniVanPanelkaApartmentRouteRole.MainRoute);
            if (targetApartment == null)
                return false;

            Transform targetGlass = target.FindNearestFacadeGlass(
                targetApartment.transform,
                sourceWindow.transform.position,
                false,
                true);
            if (targetGlass == null)
                return false;

            if (!TryBuildCrossEntranceWallPath(
                    sourceWindow.transform.position,
                    targetGlass.position,
                    target,
                    out List<Vector3> crossWallPath,
                    out Vector3 sourceOutward,
                    out Vector3 targetOutward))
            {
                return false;
            }

            string routeToken =
                "SITE_" + (siteIndex + 1).ToString("00") +
                "_FROM_" + name +
                "_F" + sourceFloorNumber.ToString("00") +
                "_TO_" + target.name +
                "_F" + targetFloorNumber.ToString("00");
            ConfigureCrackedRouteWindow(
                sourceWindow.transform,
                "CROSS_PIPE_DEPARTURE_" + routeToken);
            target.ConfigureCrackedRouteWindow(
                targetGlass,
                "CROSS_PIPE_ARRIVAL_" + routeToken);

            Transform generatedRoot = transform.Find(GeneratedRootName);
            MiniVanPanelkaBreakableWindow obsoleteArrival =
                FindPipeArrivalWindow(
                    generatedRoot,
                    sourceFloorNumber - 1);
            if (obsoleteArrival != null &&
                obsoleteArrival != sourceWindow)
            {
                RestoreRegularWindow(obsoleteArrival.transform);
            }

            Transform[] generated =
                GetComponentsInChildren<Transform>(true);
            string internalRoutePrefix =
                "Route_Pipe_From_Floor_" +
                sourceFloorNumber.ToString("00") +
                "_To_";
            for (int i = generated.Length - 1; i >= 0; i--)
            {
                if (generated[i] != null &&
                    generated[i].name.StartsWith(
                        internalRoutePrefix,
                        StringComparison.Ordinal) &&
                    generated[i].name.IndexOf(
                        "CrossEntrance",
                        StringComparison.Ordinal) < 0)
                {
                    DestroyGeneratedObject(generated[i].gameObject);
                }
            }

            BuildCrossEntrancePipeTraversal(
                sourceWindow.transform.position,
                targetGlass.position,
                crossWallPath,
                sourceOutward,
                targetOutward,
                sourceFloorNumber,
                targetFloorNumber,
                target.name);
            return true;
        }

        private void RestoreRegularWindow(Transform glass)
        {
            if (glass == null)
                return;

            MiniVanPanelkaBreakableWindowBase[] breakables =
                glass.GetComponents<MiniVanPanelkaBreakableWindowBase>();
            for (int i = 0; i < breakables.Length; i++)
                DestroyGeneratedObject(breakables[i]);

            Transform proxy =
                glass.Find("Breakable_Window_Hit_Proxy");
            if (proxy != null)
                DestroyGeneratedObject(proxy.gameObject);

            Renderer renderer = glass.GetComponent<Renderer>();
            if (renderer != null && GlassMaterial != null)
                renderer.sharedMaterial = GlassMaterial;
        }

        private MiniVanPanelkaBreakableWindow FindPipeRouteWindow(
            int floorNumber,
            MiniVanPanelkaApartmentRouteRole role)
        {
            string floorToken =
                "_F" + floorNumber.ToString("00") + "_";
            MiniVanPanelkaBreakableWindow[] windows =
                GetComponentsInChildren<MiniVanPanelkaBreakableWindow>(true);
            for (int i = 0; i < windows.Length; i++)
            {
                MiniVanPanelkaBreakableWindow window = windows[i];
                if (window == null ||
                    string.IsNullOrEmpty(window.WindowId) ||
                    !window.WindowId.StartsWith(
                        "PIPE_WINDOW_",
                        StringComparison.Ordinal) ||
                    window.WindowId.IndexOf(
                        floorToken,
                        StringComparison.Ordinal) < 0)
                {
                    continue;
                }

                MiniVanPanelkaApartmentRouteMarker marker =
                    window.GetComponentInParent<
                        MiniVanPanelkaApartmentRouteMarker>();
                if (marker != null && marker.Role == role)
                    return window;
            }
            return null;
        }

        private bool TryBuildCrossEntranceWallPath(
            Vector3 sourceWindowWorld,
            Vector3 targetWindowWorld,
            MiniVanPanelkaStage1Generator target,
            out List<Vector3> wallPath,
            out Vector3 sourceOutward,
            out Vector3 targetOutward)
        {
            wallPath = null;
            sourceOutward = GetWindowFacadeOutward(
                this,
                sourceWindowWorld);
            targetOutward = GetWindowFacadeOutward(
                target,
                targetWindowWorld);
            if (sourceOutward.sqrMagnitude < 0.5f ||
                targetOutward.sqrMagnitude < 0.5f)
            {
                return false;
            }

            MiniVanPanelkaStage1Generator[] siteGenerators =
                transform.parent != null
                    ? transform.parent.GetComponentsInChildren<
                        MiniVanPanelkaStage1Generator>(true)
                    : new[] { this, target };
            Vector3 sourceExterior =
                sourceWindowWorld +
                sourceOutward * 1.05f +
                Vector3.down * 0.88f;
            Vector3 targetExterior =
                targetWindowWorld +
                targetOutward * 1.05f +
                Vector3.down * 0.88f;
            return TryBuildSiteWallBandPath(
                sourceExterior,
                targetExterior,
                siteGenerators,
                out wallPath);
        }

        private static Vector3 GetWindowFacadeOutward(
            MiniVanPanelkaStage1Generator generator,
            Vector3 windowWorld)
        {
            if (generator == null)
                return Vector3.zero;

            Vector3 local =
                generator.transform.InverseTransformPoint(windowWorld);
            float distanceX =
                Mathf.Abs(Mathf.Abs(local.x) - BuildingHalfWidth);
            float distanceZ =
                Mathf.Abs(Mathf.Abs(local.z) - BuildingHalfDepth);
            Vector3 localOutward = distanceX <= distanceZ
                ? new Vector3(
                    local.x >= 0f ? 1f : -1f,
                    0f,
                    0f)
                : new Vector3(
                    0f,
                    0f,
                    local.z >= 0f ? 1f : -1f);
            return generator.transform
                .TransformDirection(localOutward).normalized;
        }

        private static bool TryBuildSiteWallBandPath(
            Vector3 startWorld,
            Vector3 endWorld,
            MiniVanPanelkaStage1Generator[] generators,
            out List<Vector3> path)
        {
            path = null;
            MiniVanPanelkaStage1Generator[] validGenerators =
                generators != null
                    ? generators.Where(generator => generator != null)
                        .Distinct()
                        .ToArray()
                    : Array.Empty<MiniVanPanelkaStage1Generator>();
            if (validGenerators.Length == 0)
                return false;

            Transform siteRoot = validGenerators[0].transform.parent != null
                ? validGenerators[0].transform.parent
                : validGenerators[0].transform;
            Vector3 startLocal = siteRoot.InverseTransformPoint(startWorld);
            Vector3 endLocal = siteRoot.InverseTransformPoint(endWorld);
            float minX = Mathf.Min(startLocal.x, endLocal.x);
            float maxX = Mathf.Max(startLocal.x, endLocal.x);
            float minZ = Mathf.Min(startLocal.z, endLocal.z);
            float maxZ = Mathf.Max(startLocal.z, endLocal.z);
            for (int generatorIndex = 0;
                 generatorIndex < validGenerators.Length;
                 generatorIndex++)
            {
                MiniVanPanelkaStage1Generator generator =
                    validGenerators[generatorIndex];
                for (int cornerIndex = 0;
                     cornerIndex < 4;
                     cornerIndex++)
                {
                    Vector3 cornerLocal = new Vector3(
                        (cornerIndex & 1) == 0
                            ? -BuildingHalfWidth
                            : BuildingHalfWidth,
                        0f,
                        (cornerIndex & 2) == 0
                            ? -BuildingHalfDepth
                            : BuildingHalfDepth);
                    Vector3 siteLocal = siteRoot.InverseTransformPoint(
                        generator.transform.TransformPoint(cornerLocal));
                    minX = Mathf.Min(minX, siteLocal.x);
                    maxX = Mathf.Max(maxX, siteLocal.x);
                    minZ = Mathf.Min(minZ, siteLocal.z);
                    maxZ = Mathf.Max(maxZ, siteLocal.z);
                }
            }

            const float gridMargin = 3.20f;
            const float gridStep = 0.85f;
            minX -= gridMargin;
            maxX += gridMargin;
            minZ -= gridMargin;
            maxZ += gridMargin;
            int width = Mathf.CeilToInt((maxX - minX) / gridStep) + 1;
            int height = Mathf.CeilToInt((maxZ - minZ) / gridStep) + 1;
            if (width < 2 ||
                height < 2 ||
                width * height > 180000)
            {
                return false;
            }

            int nodeCount = width * height;
            bool[] allowed = new bool[nodeCount];
            for (int z = 0; z < height; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    Vector3 localPoint = new Vector3(
                        minX + x * gridStep,
                        0f,
                        minZ + z * gridStep);
                    Vector3 worldPoint =
                        siteRoot.TransformPoint(localPoint);
                    allowed[z * width + x] =
                        IsSiteWallBandPointAllowed(
                            worldPoint,
                            validGenerators);
                }
            }

            int startNode = FindNearestAllowedPipeNode(
                startLocal,
                allowed,
                width,
                height,
                minX,
                minZ,
                gridStep);
            int endNode = FindNearestAllowedPipeNode(
                endLocal,
                allowed,
                width,
                height,
                minX,
                minZ,
                gridStep);
            if (startNode < 0 || endNode < 0)
                return false;

            float[] costs = new float[nodeCount];
            int[] parents = new int[nodeCount];
            bool[] closed = new bool[nodeCount];
            bool[] inOpen = new bool[nodeCount];
            for (int nodeIndex = 0;
                 nodeIndex < nodeCount;
                 nodeIndex++)
            {
                costs[nodeIndex] = float.PositiveInfinity;
                parents[nodeIndex] = -1;
            }

            List<int> open = new List<int> { startNode };
            costs[startNode] = 0f;
            inOpen[startNode] = true;
            int endX = endNode % width;
            int endZ = endNode / width;
            int[] neighborX = { -1, 1, 0, 0 };
            int[] neighborZ = { 0, 0, -1, 1 };
            while (open.Count > 0)
            {
                int bestOpenIndex = 0;
                float bestScore = float.PositiveInfinity;
                for (int openIndex = 0;
                     openIndex < open.Count;
                     openIndex++)
                {
                    int candidate = open[openIndex];
                    int candidateX = candidate % width;
                    int candidateZ = candidate / width;
                    float heuristic =
                        Mathf.Abs(candidateX - endX) +
                        Mathf.Abs(candidateZ - endZ);
                    float score = costs[candidate] + heuristic;
                    if (score < bestScore)
                    {
                        bestScore = score;
                        bestOpenIndex = openIndex;
                    }
                }

                int current = open[bestOpenIndex];
                open.RemoveAt(bestOpenIndex);
                inOpen[current] = false;
                if (current == endNode)
                    break;
                if (closed[current])
                    continue;
                closed[current] = true;

                int currentX = current % width;
                int currentZ = current / width;
                for (int neighborIndex = 0;
                     neighborIndex < 4;
                     neighborIndex++)
                {
                    int nextX = currentX + neighborX[neighborIndex];
                    int nextZ = currentZ + neighborZ[neighborIndex];
                    if (nextX < 0 ||
                        nextX >= width ||
                        nextZ < 0 ||
                        nextZ >= height)
                    {
                        continue;
                    }

                    int next = nextZ * width + nextX;
                    if (!allowed[next] || closed[next])
                        continue;
                    float nextCost = costs[current] + 1f;
                    if (nextCost >= costs[next])
                        continue;
                    costs[next] = nextCost;
                    parents[next] = current;
                    if (!inOpen[next])
                    {
                        open.Add(next);
                        inOpen[next] = true;
                    }
                }
            }

            if (endNode != startNode && parents[endNode] < 0)
                return false;

            List<int> reverseNodes = new List<int>();
            int cursor = endNode;
            reverseNodes.Add(cursor);
            while (cursor != startNode)
            {
                cursor = parents[cursor];
                if (cursor < 0)
                    return false;
                reverseNodes.Add(cursor);
            }
            reverseNodes.Reverse();

            List<Vector3> rawPath =
                new List<Vector3> { startWorld };
            for (int nodeIndex = 0;
                 nodeIndex < reverseNodes.Count;
                 nodeIndex++)
            {
                int node = reverseNodes[nodeIndex];
                Vector3 localPoint = new Vector3(
                    minX + (node % width) * gridStep,
                    0f,
                    minZ + (node / width) * gridStep);
                rawPath.Add(siteRoot.TransformPoint(localPoint));
            }
            rawPath.Add(endWorld);

            List<Vector3> simplified = new List<Vector3>();
            for (int pointIndex = 0;
                 pointIndex < rawPath.Count;
                 pointIndex++)
            {
                Vector3 point = rawPath[pointIndex];
                point.y = 0f;
                if (simplified.Count >= 2)
                {
                    Vector3 previousDirection =
                        (simplified[simplified.Count - 1] -
                         simplified[simplified.Count - 2]).normalized;
                    Vector3 nextDirection =
                        (point -
                         simplified[simplified.Count - 1]).normalized;
                    if (Vector3.Dot(
                            previousDirection,
                            nextDirection) > 0.999f)
                    {
                        simplified[simplified.Count - 1] = point;
                        continue;
                    }
                }
                simplified.Add(point);
            }

            if (simplified.Count < 2)
                return false;
            float totalLength = Mathf.Max(
                0.001f,
                PipePathLength(simplified));
            float travelled = 0f;
            for (int pointIndex = 0;
                 pointIndex < simplified.Count;
                 pointIndex++)
            {
                if (pointIndex > 0)
                {
                    travelled += PlanarDistance(
                        simplified[pointIndex - 1],
                        simplified[pointIndex]);
                }
                Vector3 point = simplified[pointIndex];
                point.y = Mathf.Lerp(
                    startWorld.y,
                    endWorld.y,
                    travelled / totalLength);
                simplified[pointIndex] = point;
            }

            path = simplified;
            return true;
        }

        private static bool IsSiteWallBandPointAllowed(
            Vector3 worldPoint,
            MiniVanPanelkaStage1Generator[] generators)
        {
            const float pipeBodyClearance = 0.58f;
            const float maximumWallDistance = 1.85f;
            float nearestWallDistance = float.PositiveInfinity;
            for (int generatorIndex = 0;
                 generatorIndex < generators.Length;
                 generatorIndex++)
            {
                Vector3 local = generators[generatorIndex]
                    .transform.InverseTransformPoint(worldPoint);
                float absoluteX = Mathf.Abs(local.x);
                float absoluteZ = Mathf.Abs(local.z);
                if (absoluteX <=
                        BuildingHalfWidth + pipeBodyClearance &&
                    absoluteZ <=
                        BuildingHalfDepth + pipeBodyClearance)
                {
                    return false;
                }

                float outsideX =
                    Mathf.Max(0f, absoluteX - BuildingHalfWidth);
                float outsideZ =
                    Mathf.Max(0f, absoluteZ - BuildingHalfDepth);
                float distance =
                    Mathf.Sqrt(
                        outsideX * outsideX +
                        outsideZ * outsideZ);
                nearestWallDistance =
                    Mathf.Min(nearestWallDistance, distance);
            }

            return nearestWallDistance <= maximumWallDistance;
        }

        private static int FindNearestAllowedPipeNode(
            Vector3 localPoint,
            bool[] allowed,
            int width,
            int height,
            float minX,
            float minZ,
            float gridStep)
        {
            int nearest = -1;
            float nearestDistance = float.PositiveInfinity;
            for (int z = 0; z < height; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    int node = z * width + x;
                    if (!allowed[node])
                        continue;
                    float dx =
                        minX + x * gridStep - localPoint.x;
                    float dz =
                        minZ + z * gridStep - localPoint.z;
                    float distance = dx * dx + dz * dz;
                    if (distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        nearest = node;
                    }
                }
            }
            return nearest;
        }

        private void BuildCrossEntrancePipeTraversal(
            Vector3 sourceWindowWorld,
            Vector3 targetWindowWorld,
            List<Vector3> wallPath,
            Vector3 sourceOutward,
            Vector3 targetOutward,
            int sourceFloorNumber,
            int targetFloorNumber,
            string targetEntranceName)
        {
            Transform route = Group(
                "Route_Pipe_From_Floor_" +
                sourceFloorNumber.ToString("00") +
                "_To_CrossEntrance_" +
                targetEntranceName +
                "_Floor_" +
                targetFloorNumber.ToString("00"),
                transform);

            for (int pointIndex = 1;
                 pointIndex < wallPath.Count;
                 pointIndex++)
            {
                BuildPipeSegment(
                    route,
                    wallPath[pointIndex - 1],
                    wallPath[pointIndex],
                    "CrossEntrance_Pipe_" +
                    pointIndex.ToString("00"));
            }

            Material pipeMaterial =
                MetalMaterial != null ? MetalMaterial : DoorMaterial;
            for (int pointIndex = 1;
                 pointIndex < wallPath.Count - 1;
                 pointIndex++)
            {
                Box(
                    pointIndex == 1
                        ? "CrossEntrance_Turn_Platform"
                        : "CrossEntrance_Turn_Platform_" +
                          pointIndex.ToString("00"),
                    route,
                    route.InverseTransformPoint(
                        wallPath[pointIndex] + Vector3.up * 0.02f),
                    new Vector3(1.05f, 0.12f, 1.05f),
                    pipeMaterial);
            }
            Box(
                "CrossEntrance_Source_Window_Step",
                route,
                route.InverseTransformPoint(
                    sourceWindowWorld +
                    sourceOutward * 0.52f +
                    Vector3.down * 0.88f),
                new Vector3(1.05f, 0.12f, 0.66f),
                pipeMaterial);
            Box(
                "CrossEntrance_Target_Window_Step",
                route,
                route.InverseTransformPoint(
                    targetWindowWorld +
                    targetOutward * 0.52f +
                    Vector3.down * 0.88f),
                new Vector3(1.05f, 0.12f, 0.66f),
                pipeMaterial);
        }

        private static MiniVanPanelkaBreakableWindow FindPipeArrivalWindow(
            Transform generatedRoot,
            int floorNumber)
        {
            if (generatedRoot == null)
            {
                return null;
            }

            MiniVanPanelkaBreakableWindow[] windows =
                generatedRoot.GetComponentsInChildren<MiniVanPanelkaBreakableWindow>(true);
            string floorToken =
                "_F" + floorNumber.ToString("00") + "_";
            for (int i = 0; i < windows.Length; i++)
            {
                if (windows[i] != null &&
                    !string.IsNullOrEmpty(windows[i].WindowId) &&
                    windows[i].WindowId.StartsWith(
                        "PIPE_WINDOW_",
                        StringComparison.Ordinal) &&
                    windows[i].WindowId.IndexOf(
                        floorToken,
                        StringComparison.Ordinal) >= 0)
                {
                    return windows[i];
                }
            }
            return null;
        }

        private void BuildPipeTraversal(
            Transform parent,
            Vector3 upperWindowWorld,
            Vector3 lowerWindowWorld,
            int upperFloorNumber,
            string routeName = null)
        {
            Transform route = Group(
                !string.IsNullOrEmpty(routeName)
                    ? routeName
                    : "Route_Pipe_From_Floor_" +
                      upperFloorNumber.ToString("00") +
                      "_To_" +
                      (upperFloorNumber - 1).ToString("00"),
                parent);

            Vector3 upperRawLocal =
                transform.InverseTransformPoint(upperWindowWorld) + Vector3.down * 0.82f;
            Vector3 lowerRawLocal =
                transform.InverseTransformPoint(lowerWindowWorld) + Vector3.down * 0.82f;
            bool detourShortSameFacade = !string.IsNullOrEmpty(routeName);
            PipePerimeterFrame frame = PipePerimeterFrame.Uniform(PipeFacadeClearance);
            List<Vector3> perimeterPath = BuildPipeRoutePath(
                upperRawLocal,
                lowerRawLocal,
                frame,
                detourShortSameFacade);
            List<Bounds> obstacles = CollectPipeRouteObstacles(
                Mathf.Min(upperRawLocal.y, lowerRawLocal.y) - 1.0f,
                Mathf.Max(upperRawLocal.y, lowerRawLocal.y) + 1.6f);
            if (TryClearPipeRouteObstacles(perimeterPath, obstacles, ref frame))
            {
                perimeterPath = BuildPipeRoutePath(
                    upperRawLocal,
                    lowerRawLocal,
                    frame,
                    detourShortSameFacade);
            }

            for (int i = 1; i < perimeterPath.Count; i++)
            {
                BuildPipeSegment(
                    route,
                    transform.TransformPoint(perimeterPath[i - 1]),
                    transform.TransformPoint(perimeterPath[i]),
                    "Pipe_Perimeter_" + i.ToString("00"));
            }

            Material pipeMaterial = MetalMaterial != null ? MetalMaterial : DoorMaterial;
            Vector3 upperExteriorWorld = transform.TransformPoint(perimeterPath[0]);
            Vector3 lowerExteriorWorld = transform.TransformPoint(perimeterPath[perimeterPath.Count - 1]);
            Vector3 upperStepOffset = Vector3.ProjectOnPlane(
                upperExteriorWorld - upperWindowWorld,
                Vector3.up);
            Vector3 lowerStepOffset = Vector3.ProjectOnPlane(
                lowerExteriorWorld - lowerWindowWorld,
                Vector3.up);
            GameObject upperStep = Box(
                "Pipe_Upper_Window_Step",
                route,
                parent.InverseTransformPoint(
                    upperWindowWorld + Vector3.down * 0.88f + upperStepOffset * 0.52f),
                new Vector3(1.05f, 0.12f, 0.62f),
                pipeMaterial);
            GameObject lowerStep = Box(
                "Pipe_Lower_Window_Step",
                route,
                parent.InverseTransformPoint(
                    lowerWindowWorld + Vector3.down * 0.88f + lowerStepOffset * 0.52f),
                new Vector3(1.05f, 0.12f, 0.62f),
                pipeMaterial);
            upperStep.transform.SetParent(route, true);
            lowerStep.transform.SetParent(route, true);
        }

        private static Vector3 ProjectPipePointToPerimeter(
            Vector3 point,
            PipePerimeterFrame frame,
            out int facade)
        {
            float distanceX = Mathf.Abs(Mathf.Abs(point.x) - BuildingHalfWidth);
            float distanceZ = Mathf.Abs(Mathf.Abs(point.z) - BuildingHalfDepth);

            if (distanceZ <= distanceX)
            {
                facade = point.z < 0f ? 0 : 2;
                point.x = Mathf.Clamp(point.x, frame.MinX, frame.MaxX);
                point.z = facade == 0 ? frame.MinZ : frame.MaxZ;
            }
            else
            {
                facade = point.x > 0f ? 1 : 3;
                point.x = facade == 1 ? frame.MaxX : frame.MinX;
                point.z = Mathf.Clamp(point.z, frame.MinZ, frame.MaxZ);
            }

            return point;
        }

        /// <summary>
        /// Facades are indexed 0 = -Z, 1 = +X, 2 = +Z, 3 = -X. Each one keeps its own
        /// offset so the run can clear a balcony or the roof ladder on that side while
        /// staying tight to the wall everywhere else.
        /// </summary>
        private struct PipePerimeterFrame
        {
            public float MinX;
            public float MaxX;
            public float MinZ;
            public float MaxZ;

            public static PipePerimeterFrame Uniform(float clearance)
            {
                return new PipePerimeterFrame
                {
                    MinX = -BuildingHalfWidth - clearance,
                    MaxX = BuildingHalfWidth + clearance,
                    MinZ = -BuildingHalfDepth - clearance,
                    MaxZ = BuildingHalfDepth + clearance
                };
            }

            public float GetClearance(int facade)
            {
                switch (facade)
                {
                    case 0: return -MinZ - BuildingHalfDepth;
                    case 1: return MaxX - BuildingHalfWidth;
                    case 2: return MaxZ - BuildingHalfDepth;
                    default: return -MinX - BuildingHalfWidth;
                }
            }

            public void SetClearance(int facade, float clearance)
            {
                switch (facade)
                {
                    case 0:
                        MinZ = -BuildingHalfDepth - clearance;
                        break;
                    case 1:
                        MaxX = BuildingHalfWidth + clearance;
                        break;
                    case 2:
                        MaxZ = BuildingHalfDepth + clearance;
                        break;
                    default:
                        MinX = -BuildingHalfWidth - clearance;
                        break;
                }
            }
        }

        private const float PipeFacadeClearance = 0.82f;
        private const float PipeObstacleClearance = 0.42f;
        private const float PipeMaxFacadeClearance = 1.60f;

        private List<Vector3> BuildPipeRoutePath(
            Vector3 upperRawLocal,
            Vector3 lowerRawLocal,
            PipePerimeterFrame frame,
            bool detourShortSameFacade)
        {
            int upperFacade;
            int lowerFacade;
            Vector3 upperLocal = ProjectPipePointToPerimeter(
                upperRawLocal,
                frame,
                out upperFacade);
            Vector3 lowerLocal = ProjectPipePointToPerimeter(
                lowerRawLocal,
                frame,
                out lowerFacade);
            return BuildShortestPipePerimeterPath(
                upperLocal,
                upperFacade,
                lowerLocal,
                lowerFacade,
                frame,
                detourShortSameFacade);
        }

        private List<Bounds> CollectPipeRouteObstacles(float minLocalY, float maxLocalY)
        {
            List<Bounds> obstacles = new List<Bounds>();
            Transform generatedRoot = transform.Find(GeneratedRootName);
            if (generatedRoot == null)
            {
                return obstacles;
            }

            Renderer[] renderers =
                generatedRoot.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !IsPipeRouteObstacle(renderer))
                {
                    continue;
                }

                Bounds local = TransformBoundsTo(
                    renderer.localBounds,
                    transform.worldToLocalMatrix *
                    renderer.transform.localToWorldMatrix);
                if (local.max.y < minLocalY || local.min.y > maxLocalY)
                {
                    continue;
                }

                obstacles.Add(local);
            }

            return obstacles;
        }

        /// <summary>
        /// Balconies and the roof ladder stick out of the wall the run follows. The pipe
        /// only steps around the ones it would actually hit, so it stays tight to the
        /// facade everywhere else.
        /// </summary>
        private static bool TryClearPipeRouteObstacles(
            List<Vector3> path,
            List<Bounds> obstacles,
            ref PipePerimeterFrame frame)
        {
            if (path == null || obstacles == null || obstacles.Count == 0)
            {
                return false;
            }

            bool expanded = false;
            for (int i = 1; i < path.Count; i++)
            {
                Vector3 from = path[i - 1];
                Vector3 to = path[i];
                int facade = ClassifyPipeSegmentFacade(from, to, frame);
                if (facade < 0)
                {
                    continue;
                }

                bool alongX = facade == 0 || facade == 2;
                float halfSpan = alongX ? BuildingHalfDepth : BuildingHalfWidth;
                float fromTangent = alongX ? from.x : from.z;
                float toTangent = alongX ? to.x : to.z;
                float minTangent = Mathf.Min(fromTangent, toTangent) - 0.30f;
                float maxTangent = Mathf.Max(fromTangent, toTangent) + 0.30f;

                for (int obstacleIndex = 0;
                     obstacleIndex < obstacles.Count;
                     obstacleIndex++)
                {
                    Bounds obstacle = obstacles[obstacleIndex];
                    float obstacleMinTangent = alongX ? obstacle.min.x : obstacle.min.z;
                    float obstacleMaxTangent = alongX ? obstacle.max.x : obstacle.max.z;
                    if (obstacleMaxTangent < minTangent || obstacleMinTangent > maxTangent)
                    {
                        continue;
                    }

                    // The run is sloped, so only the height it actually has next to this
                    // obstacle decides whether it hits it or passes above.
                    float overlapFrom = Mathf.Max(minTangent, obstacleMinTangent);
                    float overlapTo = Mathf.Min(maxTangent, obstacleMaxTangent);
                    float heightFrom = EvaluatePipeSegmentHeight(
                        from, to, fromTangent, toTangent, overlapFrom);
                    float heightTo = EvaluatePipeSegmentHeight(
                        from, to, fromTangent, toTangent, overlapTo);
                    // Tube radius below, walkway plate above.
                    float minY = Mathf.Min(heightFrom, heightTo) - 0.12f;
                    float maxY = Mathf.Max(heightFrom, heightTo) + 0.24f;
                    if (obstacle.max.y < minY || obstacle.min.y > maxY)
                    {
                        continue;
                    }

                    float outward;
                    float inward;
                    switch (facade)
                    {
                        case 0:
                            outward = -obstacle.min.z;
                            inward = -obstacle.max.z;
                            break;
                        case 1:
                            outward = obstacle.max.x;
                            inward = obstacle.min.x;
                            break;
                        case 2:
                            outward = obstacle.max.z;
                            inward = obstacle.min.z;
                            break;
                        default:
                            outward = -obstacle.min.x;
                            inward = -obstacle.max.x;
                            break;
                    }

                    float clearance = frame.GetClearance(facade);
                    float protrusion = outward - halfSpan;
                    bool stopsShortOfRun = protrusion < clearance - 0.22f;
                    bool startsBeyondRun = inward - halfSpan > clearance + 0.14f;
                    if (stopsShortOfRun || startsBeyondRun)
                    {
                        continue;
                    }

                    float required = Mathf.Min(
                        PipeMaxFacadeClearance,
                        protrusion + PipeObstacleClearance);
                    if (required > clearance + 0.01f)
                    {
                        frame.SetClearance(facade, required);
                        expanded = true;
                    }
                }
            }

            return expanded;
        }

        private static float EvaluatePipeSegmentHeight(
            Vector3 from,
            Vector3 to,
            float fromTangent,
            float toTangent,
            float tangent)
        {
            float span = toTangent - fromTangent;
            if (Mathf.Abs(span) < 0.001f)
            {
                return Mathf.Max(from.y, to.y);
            }

            return Mathf.Lerp(
                from.y,
                to.y,
                Mathf.Clamp01((tangent - fromTangent) / span));
        }

        private static int ClassifyPipeSegmentFacade(
            Vector3 from,
            Vector3 to,
            PipePerimeterFrame frame)
        {
            const float epsilon = 0.05f;
            if (Mathf.Abs(from.z - frame.MinZ) < epsilon &&
                Mathf.Abs(to.z - frame.MinZ) < epsilon)
            {
                return 0;
            }

            if (Mathf.Abs(from.x - frame.MaxX) < epsilon &&
                Mathf.Abs(to.x - frame.MaxX) < epsilon)
            {
                return 1;
            }

            if (Mathf.Abs(from.z - frame.MaxZ) < epsilon &&
                Mathf.Abs(to.z - frame.MaxZ) < epsilon)
            {
                return 2;
            }

            if (Mathf.Abs(from.x - frame.MinX) < epsilon &&
                Mathf.Abs(to.x - frame.MinX) < epsilon)
            {
                return 3;
            }

            return -1;
        }

        private static bool IsPipeRouteObstacle(Renderer renderer)
        {
            Transform cursor = renderer.transform;
            while (cursor != null)
            {
                string name = cursor.name;
                if (name.StartsWith("Route_Pipe", StringComparison.Ordinal) ||
                    name.StartsWith("Pipe_", StringComparison.Ordinal) ||
                    name.IndexOf("Transfer_Pipe", StringComparison.Ordinal) >= 0)
                {
                    return false;
                }

                if (name.IndexOf("Balcony", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("Street_Door", StringComparison.Ordinal) >= 0 ||
                    name.IndexOf("Exterior_Roof_Ladder", StringComparison.Ordinal) >= 0)
                {
                    return true;
                }

                cursor = cursor.parent;
            }

            return false;
        }

        /// <summary>
        /// True when a neighbouring entrance module is glued to that facade, which would
        /// put the run inside somebody else's wall.
        /// </summary>
        private bool IsPipeFacadeBlockedByNeighbour(int facade)
        {
            if (facadeOcclusionBounds == null || facadeOcclusionBounds.Length == 0)
            {
                return false;
            }

            float x = BuildingHalfWidth + PipeFacadeClearance;
            float z = BuildingHalfDepth + PipeFacadeClearance;
            const int samples = 8;
            for (int i = 0; i <= samples; i++)
            {
                float t = Mathf.Lerp(0.08f, 0.92f, i / (float)samples);
                Vector3 point;
                switch (facade)
                {
                    case 0:
                        point = new Vector3(Mathf.Lerp(-x, x, t), 0f, -z);
                        break;
                    case 1:
                        point = new Vector3(x, 0f, Mathf.Lerp(-z, z, t));
                        break;
                    case 2:
                        point = new Vector3(Mathf.Lerp(-x, x, t), 0f, z);
                        break;
                    default:
                        point = new Vector3(-x, 0f, Mathf.Lerp(-z, z, t));
                        break;
                }

                if (IsPipePointBlockedByNeighbour(point))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsPipePointBlockedByNeighbour(Vector3 localPoint)
        {
            if (facadeOcclusionBounds == null || facadeOcclusionBounds.Length == 0)
            {
                return false;
            }

            return IsFacadeDecorationOccluded(
                new Bounds(
                    new Vector3(localPoint.x, 0f, localPoint.z),
                    new Vector3(0.34f, 0f, 0.34f)));
        }

        private bool IsPipePathBlockedByNeighbour(List<Vector3> path)
        {
            if (path == null ||
                facadeOcclusionBounds == null ||
                facadeOcclusionBounds.Length == 0)
            {
                return false;
            }

            for (int i = 1; i < path.Count; i++)
            {
                Vector3 from = path[i - 1];
                Vector3 to = path[i];
                int steps = Mathf.Max(1, Mathf.CeilToInt(PlanarDistance(from, to)));
                for (int step = 0; step <= steps; step++)
                {
                    if (IsPipePointBlockedByNeighbour(
                            Vector3.Lerp(from, to, step / (float)steps)))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool HasFreePipeFacade()
        {
            for (int facade = 0; facade < 4; facade++)
            {
                if (!IsPipeFacadeBlockedByNeighbour(facade))
                {
                    return true;
                }
            }

            return false;
        }

        private List<Vector3> BuildShortestPipePerimeterPath(
            Vector3 start,
            int startFacade,
            Vector3 end,
            int endFacade,
            PipePerimeterFrame frame,
            bool detourShortSameFacade)
        {
            if (detourShortSameFacade &&
                startFacade == endFacade &&
                PlanarDistance(start, end) < 5.60f)
            {
                const float minimumWindowBayDetour = 6.00f;
                bool alongX = startFacade == 0 || startFacade == 2;
                Vector3 tangent = alongX ? Vector3.right : Vector3.forward;
                Vector3 average = (start + end) * 0.5f;
                float sideSign =
                    Vector3.Dot(
                        new Vector3(average.x, 0f, average.z),
                        tangent) > 0f
                        ? -1f
                        : 1f;
                Vector3 midpoint = ClampPipePointToFacade(
                    average + tangent * sideSign * minimumWindowBayDetour,
                    alongX,
                    frame);
                if (IsPipePointBlockedByNeighbour(midpoint))
                {
                    Vector3 mirrored = ClampPipePointToFacade(
                        average - tangent * sideSign * minimumWindowBayDetour,
                        alongX,
                        frame);
                    if (!IsPipePointBlockedByNeighbour(mirrored))
                    {
                        midpoint = mirrored;
                    }
                }

                return new List<Vector3>
                {
                    start,
                    midpoint,
                    end
                };
            }

            List<Vector3> clockwise = BuildPipePerimeterPath(
                start, startFacade, end, endFacade, 1, frame);
            List<Vector3> counterClockwise = BuildPipePerimeterPath(
                start, startFacade, end, endFacade, -1, frame);
            bool clockwiseBlocked = IsPipePathBlockedByNeighbour(clockwise);
            bool counterBlocked = IsPipePathBlockedByNeighbour(counterClockwise);
            List<Vector3> selected;
            if (clockwiseBlocked != counterBlocked)
            {
                // Walking the long way round the tower beats cutting through the
                // neighbouring entrance.
                selected = clockwiseBlocked ? counterClockwise : clockwise;
            }
            else
            {
                selected = PipePathLength(clockwise) <= PipePathLength(counterClockwise)
                    ? clockwise
                    : counterClockwise;
            }

            float totalLength = Mathf.Max(0.001f, PipePathLength(selected));
            float travelled = 0f;
            for (int i = 0; i < selected.Count; i++)
            {
                if (i > 0)
                {
                    travelled += PlanarDistance(selected[i - 1], selected[i]);
                }

                Vector3 point = selected[i];
                point.y = Mathf.Lerp(start.y, end.y, travelled / totalLength);
                selected[i] = point;
            }

            return selected;
        }

        private static Vector3 ClampPipePointToFacade(
            Vector3 point,
            bool alongX,
            PipePerimeterFrame frame)
        {
            if (alongX)
            {
                point.x = Mathf.Clamp(point.x, frame.MinX, frame.MaxX);
            }
            else
            {
                point.z = Mathf.Clamp(point.z, frame.MinZ, frame.MaxZ);
            }

            return point;
        }

        private static List<Vector3> BuildPipePerimeterPath(
            Vector3 start,
            int startFacade,
            Vector3 end,
            int endFacade,
            int direction,
            PipePerimeterFrame frame)
        {
            List<Vector3> path = new List<Vector3> { start };
            int facade = startFacade;
            while (facade != endFacade)
            {
                path.Add(GetPipePerimeterCorner(facade, direction, frame));
                facade = (facade + direction + 4) % 4;
            }

            path.Add(end);
            return path;
        }

        private static Vector3 GetPipePerimeterCorner(
            int facade,
            int direction,
            PipePerimeterFrame frame)
        {
            if (direction > 0)
            {
                switch (facade)
                {
                    case 0: return new Vector3(frame.MaxX, 0f, frame.MinZ);
                    case 1: return new Vector3(frame.MaxX, 0f, frame.MaxZ);
                    case 2: return new Vector3(frame.MinX, 0f, frame.MaxZ);
                    default: return new Vector3(frame.MinX, 0f, frame.MinZ);
                }
            }

            switch (facade)
            {
                case 0: return new Vector3(frame.MinX, 0f, frame.MinZ);
                case 3: return new Vector3(frame.MinX, 0f, frame.MaxZ);
                case 2: return new Vector3(frame.MaxX, 0f, frame.MaxZ);
                default: return new Vector3(frame.MaxX, 0f, frame.MinZ);
            }
        }

        private static float PipePathLength(List<Vector3> path)
        {
            float length = 0f;
            for (int i = 1; i < path.Count; i++)
            {
                length += PlanarDistance(path[i - 1], path[i]);
            }

            return length;
        }

        private static float PlanarDistance(Vector3 a, Vector3 b)
        {
            return Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
        }

        private void BuildPipeSegment(Transform parent, Vector3 startWorld, Vector3 endWorld, string name)
        {
            Vector3 delta = endWorld - startWorld;
            float length = delta.magnitude;
            if (length < 0.05f)
            {
                return;
            }

            GameObject pipe = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pipe.name = name;
            pipe.transform.SetParent(parent, true);
            pipe.transform.position = (startWorld + endWorld) * 0.5f;
            pipe.transform.rotation = Quaternion.FromToRotation(Vector3.up, delta.normalized);
            pipe.transform.localScale = new Vector3(0.24f, length * 0.5f, 0.24f);
            Renderer renderer = pipe.GetComponent<Renderer>();
            if (renderer != null && MetalMaterial != null)
            {
                renderer.sharedMaterial = MetalMaterial;
            }

            GameObject walkway = new GameObject(name + "_Walkable_Top");
            walkway.transform.SetParent(parent, true);
            walkway.transform.position = (startWorld + endWorld) * 0.5f + Vector3.up * 0.17f;
            walkway.transform.rotation = Quaternion.LookRotation(delta.normalized, Vector3.up);
            BoxCollider topCollider = walkway.AddComponent<BoxCollider>();
            topCollider.size = new Vector3(0.56f, 0.10f, length);
        }

        private void AddCrackPattern(Transform glass)
        {
            if (glass == null)
                return;

            Renderer renderer = glass.GetComponent<Renderer>();
            if (renderer != null && CrackedGlassMaterial != null)
            {
                if (CrackedGlassMaterial.HasProperty("_Cull"))
                {
                    CrackedGlassMaterial.SetFloat("_Cull", 0f);
                }
                renderer.sharedMaterial = CrackedGlassMaterial;
            }
        }

        private Transform FindNearestFacadeGlass(
            Transform apartment,
            Vector3 preferredWorldPosition,
            bool requireBalconyClearance = false,
            bool requireUnusedWindow = false)
        {
            Transform selectedGlass = null;
            float bestScore = float.MaxValue;
            float minClear = GetMinRouteWindowClearWidth();
            bool foundWideEnough = false;
            MiniVanPanelkaApartmentFacadeMarker[] windows =
                apartment.GetComponentsInChildren<MiniVanPanelkaApartmentFacadeMarker>(true);
            for (int i = 0; i < windows.Length; i++)
            {
                Renderer[] renderers =
                    windows[i].GetComponentsInChildren<Renderer>(true);
                for (int rendererIndex = 0;
                     rendererIndex < renderers.Length;
                     rendererIndex++)
                {
                    Renderer renderer = renderers[rendererIndex];
                    if (renderer == null ||
                        !renderer.gameObject.activeInHierarchy ||
                        (renderer.name != "Breakable_Glass" &&
                         !renderer.name.StartsWith(
                             "Glass_",
                             StringComparison.Ordinal)))
                    {
                        continue;
                    }
                    if (requireUnusedWindow &&
                        renderer.GetComponent<
                            MiniVanPanelkaBreakableWindow>() != null)
                    {
                        continue;
                    }

                    Vector3 center =
                        renderer.transform.TransformPoint(
                            renderer.localBounds.center);
                    if (requireBalconyClearance &&
                         IsBalconyPlacementOccluded(
                             transform.InverseTransformPoint(center)))
                    {
                        continue;
                    }

                    float clearWidth = GetGlassClearWidth(renderer.transform);
                    bool wideEnough = clearWidth >= minClear - 0.001f;
                    if (foundWideEnough && !wideEnough)
                    {
                        continue;
                    }

                    float score =
                        (center - preferredWorldPosition).sqrMagnitude;
                    // Prefer crawlable glass; among equals, nearer to the socket.
                    if (!wideEnough)
                    {
                        score += 1000f + (minClear - clearWidth) * 100f;
                    }

                    if (!foundWideEnough && wideEnough)
                    {
                        foundWideEnough = true;
                        bestScore = score;
                        selectedGlass = renderer.transform;
                        continue;
                    }

                    if (score < bestScore)
                    {
                        bestScore = score;
                        selectedGlass = renderer.transform;
                    }
                }
            }
            return selectedGlass;
        }

        private bool IsBalconyPlacementOccluded(
            Vector3 localOpeningCenter)
        {
            const float platformDepth = 1.60f;
            const float platformWidth = 2.85f;
            bool onXFace =
                Mathf.Abs(
                    Mathf.Abs(localOpeningCenter.x) -
                    BuildingHalfWidth) <=
                Mathf.Abs(
                    Mathf.Abs(localOpeningCenter.z) -
                    BuildingHalfDepth);
            Vector3 center = localOpeningCenter;
            Vector3 size;
            if (onXFace)
            {
                float sign = Mathf.Sign(localOpeningCenter.x);
                center.x =
                    sign * (BuildingHalfWidth + platformDepth * 0.5f);
                size = new Vector3(
                    platformDepth,
                    0f,
                    platformWidth);
            }
            else
            {
                float sign = Mathf.Sign(localOpeningCenter.z);
                center.z =
                    sign * (BuildingHalfDepth + platformDepth * 0.5f);
                size = new Vector3(
                    platformWidth,
                    0f,
                    platformDepth);
            }

            return IsFacadeDecorationOccluded(
                new Bounds(center, size));
        }

        private static void RemoveWindowGlassAndSill(Transform selectedGlass)
        {
            if (selectedGlass == null)
            {
                return;
            }

            string suffix = selectedGlass.name.Substring("Glass_".Length);
            Transform sill = selectedGlass.parent != null &&
                             selectedGlass.name.StartsWith(
                                 "Glass_",
                                 StringComparison.Ordinal)
                ? selectedGlass.parent.Find("Sill_" + suffix)
                : selectedGlass.parent != null
                    ? selectedGlass.parent.Find("Frame_Bottom")
                    : null;
            if (sill != null)
            {
                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(sill.gameObject);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(sill.gameObject);
                }
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(selectedGlass.gameObject);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(selectedGlass.gameObject);
            }
        }


private static Transform FindWindowPassagePart(
            Transform glass,
            string prefix)
        {
            if (glass == null || glass.parent == null)
            {
                return null;
            }

            string suffix =
                glass.name.StartsWith("Glass_", StringComparison.Ordinal)
                    ? glass.name.Substring("Glass_".Length)
                    : string.Empty;
            Transform exact =
                string.IsNullOrEmpty(suffix)
                    ? null
                    : glass.parent.Find(prefix + suffix);
            if (exact != null)
            {
                return exact;
            }

            foreach (Transform child in glass.parent)
            {
                if (child != null &&
                    child.name.StartsWith(prefix, StringComparison.Ordinal))
                {
                    return child;
                }
            }

            if (prefix == "Sill_")
                return glass.parent.Find("Frame_Bottom");
            if (prefix == "Lintel_")
                return glass.parent.Find("Frame_Top");

            return null;
        }
}
}
