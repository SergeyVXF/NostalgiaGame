using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace MiniVanGame
{
    public enum MiniVanPanelkaManualLayoutShape
    {
        Row = 0,
        LShape = 1,
        UShape = 2,
        TwoRows = 3
    }

    public enum MiniVanPanelkaAccessibleMode
    {
        FirstFromLeft = 0,
        IndexList = 1
    }

    /// <summary>
    /// Manual panelka placement: set floors/entrances/seed in the inspector, then press Rebuild.
    /// Independent from <see cref="MiniVanGameModeWorldGenerator"/>.
    /// Each entrance is one full tower module.
    /// </summary>
    public sealed class MiniVanPanelkaManualBuilder : MonoBehaviour
    {
        private const string GeneratedRootName = "Generated_Manual_Panelka";
        private const float ModuleWidth = 26f;
        private const float ModuleDepth = 18f;

        [Header("Постройка")]
        [Tooltip("Сколько этажей у каждой башни.")]
        [Min(1)] public int FloorCount = 3;
        [Tooltip("Сколько подъездов-башен поставить (1–100).")]
        [Range(1, 100)] public int EntranceCount = 1;
        [Tooltip("Одно и то же число = одна и та же планировка (если шаблоны не зафиксированы).")]
        public int GenerationSeed = 9137;
        [Tooltip("Пересобирать при смене ключевых полей в инспекторе.")]
        public bool AutoRebuild;

        [Header("Доступность подъездов")]
        public MiniVanPanelkaAccessibleMode AccessibleMode =
            MiniVanPanelkaAccessibleMode.FirstFromLeft;
        [Tooltip("Сколько подъездов можно зайти (режим First From Left).")]
        [Min(0)] public int AccessibleEntrances = 1;
        [Tooltip("Номера подъездов с 1 (режим Index List). Пример: 1, 3")]
        public int[] AccessibleEntranceNumbers = { 1 };
        [Tooltip("Не ставить закрытые (пустые exterior-only) подъезды. " +
                 "Удобно для одного небольшого здания с одним подъездом.")]
        public bool SkipEmptyEntrances;

        [Header("Расстановка")]
        public MiniVanPanelkaManualLayoutShape LayoutShape =
            MiniVanPanelkaManualLayoutShape.Row;
        [Tooltip("Шаг между соседними подъездами.")]
        [Min(1f)] public float EntranceStep = 26f;
        [Tooltip("Общий поворот всего ряда (градусы).")]
        public float RowYaw;
        [Tooltip("Доп. yaw каждого подъезда. Длина < EntranceCount → 0 для остальных.")]
        public float[] EntranceYawOffsets;
        [Tooltip("Смещение второго ряда по локальному Z (TwoRows / часть L и U).")]
        public float SecondRowZOffset = 26f;

        [Header("Шаблоны квартир")]
        [Tooltip("Если включено — этажи берут индексы из FloorTemplateIndices (1..5).")]
        public bool FixApartmentTemplates;
        [Tooltip("Индекс шаблона каталога на этаж (1..5). Короткий массив добивается последним.")]
        public int[] FloorTemplateIndices = { 1, 2, 3, 4, 5 };

        [Header("Маршрут")]
        [Tooltip("Random = как выпало от seed. Иначе все переходы одного типа.")]
        public MiniVanPanelkaForcedRouteTransition RouteTransitionMode =
            MiniVanPanelkaForcedRouteTransition.Random;
        [Tooltip("Сколько квартир маршрут может поставить в одну вертикальную линию. " +
                 "2 = зашли в квартиру, спустились на этаж, дальше выход на площадку.")]
        [Min(2)] public int RouteVerticalApartmentsInSingleLine = 2;
        [Tooltip("Труба между двумя открытыми подъездами (нужно ≥2 открытых и ≥5 этажей).")]
        public bool EnableCrossEntrancePipe = true;

        [Header("Переходы маршрута (только режим Random)")]
        [Tooltip("Дыра в полу верхней квартиры.")]
        public MiniVanPanelkaRouteTransitionBudget RouteHoleBudget =
            MiniVanPanelkaRouteTransitionBudget.Unlimited;
        [Tooltip("Спуск с балкона в квартиру ниже.")]
        public MiniVanPanelkaRouteTransitionBudget RouteBalconyBudget =
            MiniVanPanelkaRouteTransitionBudget.Unlimited;
        [Tooltip("Спуск по трубе в соседнюю квартиру этажом ниже.")]
        public MiniVanPanelkaRouteTransitionBudget RoutePipeBudget =
            MiniVanPanelkaRouteTransitionBudget.Unlimited;
        [Tooltip("Обычный спуск по лестнице подъезда.")]
        public MiniVanPanelkaRouteTransitionBudget RouteStairsBudget =
            MiniVanPanelkaRouteTransitionBudget.Unlimited;

        [Header("Интерьер")]
        [Tooltip("Мебель на верхнем этаже.")]
        public bool FurnishTopFloor = true;
        [Tooltip("Мебель в квартирах по игровому маршруту.")]
        public bool FurnishGeneratedRoute = true;
        [Tooltip("Какие этажи получают декор площадки (вместе с TopFloor).")]
        public bool FurnishAllLandings = true;
        [Tooltip("Объявления / таблички / щитки на площадке.")]
        public bool FurnishLandingNotices = true;
        [Tooltip("Лампы на площадке.")]
        public bool FurnishLandingLamps = true;

        [Header("Фасад")]
        [Tooltip("Декоративные балконы на закрытых (exterior-only) подъездах.")]
        public bool BuildClosedFacadeBalconies = true;

        [Header("Зомби")]
        [Tooltip("Сколько зомби в открытых подъездах. 0 = без зомби.")]
        [Min(0)] public int PanelkaZombieCount;
        [Tooltip("Спавн только в MainRoute квартирах.")]
        public bool ZombiesOnMainRouteOnly;
        [Tooltip("Спавн без Netcode-сервера (удобно для ручных тестов).")]
        public bool AllowOfflineZombieSpawn = true;

        [Header("Player clearance / окна")]
        [Min(0.2f)] public float PlayerRadius = 0.32f;
        [Tooltip("Мин. ширина стекла и проёма для crawl-окон. 0 = PlayerRadius*2+0.20.")]
        [Min(0f)] public float MinWindowClearWidth;

        [HideInInspector] public string LastBuildPreview;

        [System.NonSerialized] private bool isRebuilding;

        [HideInInspector] [SerializeField] private Material exteriorMaterial;
        [HideInInspector] [SerializeField] private Material interiorMaterial;
        [HideInInspector] [SerializeField] private Material floorMaterial;
        [HideInInspector] [SerializeField] private Material doorMaterial;
        [HideInInspector] [SerializeField] private Material[] apartmentDoorMaterials;
        [HideInInspector] [SerializeField] private Material glassMaterial;
        [HideInInspector] [SerializeField] private Material crackedGlassMaterial;
        [HideInInspector] [SerializeField] private Material metalMaterial;
        [HideInInspector] [SerializeField] private Material stairwellFloorMaterial;
        [HideInInspector] [SerializeField] private Material stairwellWallMaterial;
        [HideInInspector] [SerializeField] private Material stairwellLowerWallMaterial;
        [HideInInspector] [SerializeField] private Material stairwellUpperWallMaterial;
        [HideInInspector] [SerializeField] private Material stairwellCeilingMaterial;
        [HideInInspector] [SerializeField] private Material stairwellDoorMaterial;
        [HideInInspector] [SerializeField] private Material furnitureWoodMaterial;
        [HideInInspector] [SerializeField] private Material furnitureFabricMaterial;
        [HideInInspector] [SerializeField] private Material furnitureCarpetMaterial;
        [HideInInspector] [SerializeField] private Material furnitureMetalMaterial;
        [HideInInspector] [SerializeField] private Material furnitureCeramicMaterial;
        [HideInInspector] [SerializeField] private Material furniturePaperMaterial;
        [HideInInspector] [SerializeField] private Material furnitureDarkPlasticMaterial;
        [HideInInspector] [SerializeField] private Material keyMaterial;
        [HideInInspector] [SerializeField] private GameObject zombiePrefab;

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

        [ContextMenu("Rebuild Manual Panelka")]
        public void Rebuild()
        {
            if (isRebuilding)
            {
                return;
            }

            isRebuilding = true;
            try
            {
                RebuildInternal();
            }
            finally
            {
                isRebuilding = false;
            }
        }

        private void RebuildInternal()
        {
            ClearGenerated();
            EnsureMaterials();
            NormalizeSettings();

            int floors = Mathf.Max(1, FloorCount);
            int entrances = Mathf.Clamp(EntranceCount, 1, 100);

            Transform root = Group(GeneratedRootName, transform);
            List<EntrancePose> layout = BuildLayout(entrances);
            HashSet<int> accessible = PickAccessibleIndices(entrances);
            List<MiniVanPanelkaStage1Generator> accessibleGenerators =
                new List<MiniVanPanelkaStage1Generator>(accessible.Count);
            StringBuilder preview = new StringBuilder(256);

            for (int entrance = 0; entrance < entrances; entrance++)
            {
                bool isAccessible = accessible.Contains(entrance);
                if (SkipEmptyEntrances && !isAccessible)
                {
                    continue;
                }

                EntrancePose pose = layout[entrance];
                Transform foundation = Group("Foundation_" + (entrance + 1), root);
                foundation.localPosition = pose.Position + Vector3.down * 0.2f;
                foundation.localRotation = Quaternion.Euler(0f, pose.Yaw, 0f);
                CreateBox(
                    "Concrete Foundation",
                    foundation,
                    Vector3.zero,
                    new Vector3(ModuleWidth + 2f, 0.4f, 20f),
                    floorMaterial,
                    true);

                Bounds[] occlusions = BuildFacadeOcclusionBounds(
                    layout,
                    entrance,
                    SkipEmptyEntrances ? accessible : null);
                if (isAccessible)
                {
                    MiniVanPanelkaStage1Generator generator = BuildAccessibleEntrance(
                        root,
                        entrance,
                        floors,
                        pose,
                        accessible.Count > 2,
                        occlusions);
                    accessibleGenerators.Add(generator);
                    AppendEntrancePreview(preview, entrance, true, generator);
                }
                else
                {
                    MiniVanPanelkaStage1Generator generator = BuildClosedEntrance(
                        root, entrance, floors, pose, occlusions);
                    AppendEntrancePreview(preview, entrance, false, generator);
                }
            }

            if (SkipEmptyEntrances && accessibleGenerators.Count == 0)
            {
                preview.AppendLine("SkipEmptyEntrances: no accessible entrances selected");
            }

            if (EnableCrossEntrancePipe &&
                floors >= 5 &&
                accessibleGenerators.Count >= 2)
            {
                MiniVanPanelkaStage1Generator source = accessibleGenerators[0];
                MiniVanPanelkaStage1Generator target = accessibleGenerators[1];
                if (!source.TryRedirectPipeRouteToEntrance(
                        target,
                        0,
                        out int sourceFloor,
                        out int targetFloor))
                {
                    preview.AppendLine("Cross-pipe: FAILED");
                    Debug.LogWarning(
                        "[Manual Panelka] Could not build a cross-entrance route on " +
                        name + ".",
                        this);
                }
                else
                {
                    preview.AppendLine(
                        "Cross-pipe: " + source.name + " F" + sourceFloor +
                        " -> " + target.name + " F" + targetFloor);
                    Debug.Log(
                        "[Manual Panelka] Cross-entrance route: " +
                        source.name + " floor " + sourceFloor +
                        " -> " + target.name + " floor " + targetFloor + ".",
                        this);
                }
            }
            else if (!EnableCrossEntrancePipe)
            {
                preview.AppendLine("Cross-pipe: disabled");
            }

            SyncLadderClimbTriggers(root);
            EnsureOutsideGeneratedWorldContent();
            MiniVanGameModeRenderOptimizer optimizer =
                MiniVanGameModeRenderOptimizer.EnsureOnHost();
            if (optimizer == null)
            {
                optimizer = gameObject.GetComponent<MiniVanGameModeRenderOptimizer>();
                if (optimizer == null)
                {
                    optimizer = gameObject.AddComponent<MiniVanGameModeRenderOptimizer>();
                }
            }

            optimizer.RefreshCullTargets();
            LastBuildPreview = preview.ToString();

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.EditorUtility.SetDirty(gameObject);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
            }
#endif
        } // RebuildInternal

        [ContextMenu("Clear Manual Panelka")]
        public void ClearGenerated()
        {
            Transform old = transform.Find(GeneratedRootName);
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

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.EditorUtility.SetDirty(gameObject);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
            }
#endif
        }

        public void RandomizeSeed()
        {
            GenerationSeed = Random.Range(1, 999999);
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
            if (AutoRebuild)
            {
                Rebuild();
            }
        }

        private void OnValidate()
        {
            NormalizeSettings();
#if UNITY_EDITOR
            if (!AutoRebuild || Application.isPlaying)
            {
                return;
            }

            UnityEditor.EditorApplication.delayCall -= DelayedAutoRebuild;
            UnityEditor.EditorApplication.delayCall += DelayedAutoRebuild;
#endif
        }

#if UNITY_EDITOR
        private void DelayedAutoRebuild()
        {
            if (this == null || !AutoRebuild || Application.isPlaying || isRebuilding)
            {
                return;
            }

            Rebuild();
        }
#endif

        private void NormalizeSettings()
        {
            FloorCount = Mathf.Max(1, FloorCount);
            EntranceCount = Mathf.Clamp(EntranceCount, 1, 100);
            AccessibleEntrances = Mathf.Clamp(AccessibleEntrances, 0, EntranceCount);
            PanelkaZombieCount = Mathf.Max(0, PanelkaZombieCount);
            EntranceStep = Mathf.Max(1f, EntranceStep);
            PlayerRadius = Mathf.Max(0.2f, PlayerRadius);
            MinWindowClearWidth = Mathf.Max(0f, MinWindowClearWidth);

            if (FloorTemplateIndices == null || FloorTemplateIndices.Length == 0)
            {
                FloorTemplateIndices = new[] { 1, 2, 3, 4, 5 };
            }

            for (int i = 0; i < FloorTemplateIndices.Length; i++)
            {
                FloorTemplateIndices[i] = Mathf.Clamp(FloorTemplateIndices[i], 1, 5);
            }

            if (AccessibleEntranceNumbers == null)
            {
                AccessibleEntranceNumbers = new[] { 1 };
            }
        }

        private void OnDrawGizmos()
        {
            int entrances = Mathf.Clamp(EntranceCount, 1, 100);
            List<EntrancePose> layout = BuildLayout(entrances);
            HashSet<int> accessible = PickAccessibleIndices(entrances);
            for (int i = 0; i < layout.Count; i++)
            {
                bool isAccessible = accessible.Contains(i);
                if (SkipEmptyEntrances && !isAccessible)
                {
                    continue;
                }

                EntrancePose pose = layout[i];
                Gizmos.color = isAccessible
                    ? new Color(0.35f, 0.85f, 0.45f, 0.9f)
                    : new Color(0.35f, 0.75f, 1f, 0.7f);
                Matrix4x4 matrix = Matrix4x4.TRS(
                    transform.TransformPoint(pose.Position + Vector3.up * 1.6f),
                    transform.rotation * Quaternion.Euler(0f, pose.Yaw, 0f),
                    Vector3.one);
                Gizmos.matrix = matrix;
                Gizmos.DrawWireCube(Vector3.zero, new Vector3(ModuleWidth, 3.2f, ModuleDepth));
            }

            Gizmos.matrix = Matrix4x4.identity;
        }

        private void AppendEntrancePreview(
            StringBuilder preview,
            int entranceIndex,
            bool accessible,
            MiniVanPanelkaStage1Generator generator)
        {
            preview.Append("E").Append(entranceIndex + 1);
            preview.Append(accessible ? " OPEN" : " CLOSED");
            if (generator == null)
            {
                preview.AppendLine();
                return;
            }

            int[] layouts = generator.GetActiveLayoutSequence();
            preview.Append(" templates=");
            for (int i = 0; i < layouts.Length; i++)
            {
                if (i > 0)
                {
                    preview.Append(',');
                }

                preview.Append(layouts[i]);
            }

            preview.Append(" route=");
            for (int upper = generator.FloorCount; upper >= 2; upper--)
            {
                if (upper < generator.FloorCount)
                {
                    preview.Append(',');
                }

                preview.Append('F').Append(upper).Append('→')
                    .Append(ShortRouteLabel(generator.GetRouteTransitionMode(upper)));
            }

            preview.AppendLine();
        }

        private static string ShortRouteLabel(MiniVanPanelkaForcedRouteTransition mode)
        {
            switch (mode)
            {
                case MiniVanPanelkaForcedRouteTransition.Hole:
                    return "Hole";
                case MiniVanPanelkaForcedRouteTransition.Balcony:
                    return "Balcony";
                case MiniVanPanelkaForcedRouteTransition.Pipe:
                    return "Pipe";
                case MiniVanPanelkaForcedRouteTransition.Stairs:
                    return "Stairs";
                default:
                    return "?";
            }
        }

        private MiniVanPanelkaStage1Generator BuildAccessibleEntrance(
            Transform root,
            int entranceIndex,
            int floors,
            EntrancePose pose,
            bool roofOnly,
            Bounds[] facadeOcclusions)
        {
            Transform entrance = Group("Entrance_" + (entranceIndex + 1) + "_ACCESSIBLE", root);
            entrance.localPosition = pose.Position;
            entrance.localRotation = Quaternion.Euler(0f, pose.Yaw, 0f);

            MiniVanPanelkaStage1Generator generator =
                entrance.gameObject.AddComponent<MiniVanPanelkaStage1Generator>();
            ConfigureGenerator(generator, entranceIndex, floors, false);
            generator.ConfigureFacadeOcclusion(facadeOcclusions);
            generator.Rebuild();

            GameObject zoneObject = new GameObject("Interior Zone");
            zoneObject.transform.SetParent(entrance, false);
            zoneObject.transform.localPosition = new Vector3(0f, floors * 1.6f, 0f);
            BoxCollider zoneCollider = zoneObject.AddComponent<BoxCollider>();
            zoneCollider.isTrigger = true;
            zoneCollider.size = new Vector3(25.5f, floors * 3.2f, 17.5f);
            MiniVanGameModeInteriorZone zone = zoneObject.AddComponent<MiniVanGameModeInteriorZone>();
            zone.SiteIndex = 0;

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

        private MiniVanPanelkaStage1Generator BuildClosedEntrance(
            Transform root,
            int entranceIndex,
            int floors,
            EntrancePose pose,
            Bounds[] facadeOcclusions)
        {
            Transform closed = Group("Entrance_" + (entranceIndex + 1) + "_CLOSED_NO_INTERIOR", root);
            closed.localPosition = pose.Position;
            closed.localRotation = Quaternion.Euler(0f, pose.Yaw, 0f);

            MiniVanPanelkaStage1Generator generator =
                closed.gameObject.AddComponent<MiniVanPanelkaStage1Generator>();
            ConfigureGenerator(generator, entranceIndex, floors, true);
            generator.ConfigureFacadeOcclusion(facadeOcclusions);
            generator.Rebuild();

            if (BuildClosedFacadeBalconies)
            {
                for (int floor = 0; floor < floors; floor++)
                {
                    float sideX = ((floor + entranceIndex) & 1) == 0 ? -7.1f : 7.1f;
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
            }

            CreateBox(
                    "Hidden Ground Entry Blocker",
                    closed,
                    new Vector3(0f, 1.35f, -8.7f),
                    new Vector3(3.4f, 2.7f, 0.32f),
                    metalMaterial,
                    true)
                .GetComponent<Renderer>().enabled = false;
            return generator;
        }

        private void ConfigureGenerator(
            MiniVanPanelkaStage1Generator generator,
            int entranceIndex,
            int floors,
            bool exteriorOnly)
        {
            generator.FloorCount = floors;
            generator.GenerationSeed = GenerationSeed * 100 + entranceIndex;
            generator.GenerateOnStart = false;
            generator.RandomizeGenerationOnPlay = false;
            generator.ExteriorOnlyLocked = exteriorOnly;
            generator.FurnishTopFloor = !exteriorOnly && FurnishTopFloor;
            generator.FurnishGeneratedRoute = !exteriorOnly && FurnishGeneratedRoute;
            generator.FurnishAllLandings = !exteriorOnly && FurnishAllLandings;
            generator.FurnishLandingNotices = FurnishLandingNotices;
            generator.FurnishLandingLamps = FurnishLandingLamps;
            generator.ForcedRouteTransition = RouteTransitionMode;
            generator.RouteVerticalApartmentsInSingleLine =
                RouteVerticalApartmentsInSingleLine;
            generator.RouteHoleBudget = RouteHoleBudget;
            generator.RouteBalconyBudget = RouteBalconyBudget;
            generator.RoutePipeBudget = RoutePipeBudget;
            generator.RouteStairsBudget = RouteStairsBudget;
            generator.PlayerRadius = PlayerRadius;
            generator.MinRouteWindowClearWidth = MinWindowClearWidth;
            generator.SpawnPanelkaZombies = !exteriorOnly && PanelkaZombieCount > 0;
            generator.PanelkaZombieCount = exteriorOnly ? 0 : PanelkaZombieCount;
            generator.ZombiesOnMainRouteOnly = ZombiesOnMainRouteOnly;
            generator.AllowOfflineZombieSpawn = AllowOfflineZombieSpawn;
            generator.ZombiePrefab = zombiePrefab;
            generator.ForcedLayoutSequence = FixApartmentTemplates
                ? BuildForcedLayoutSequence(floors)
                : null;
            generator.ExteriorMaterial = exteriorMaterial;
            generator.InteriorMaterial = interiorMaterial;
            generator.FloorMaterial = floorMaterial;
            generator.DoorMaterial = doorMaterial;
            generator.ApartmentDoorMaterials = apartmentDoorMaterials;
            generator.GlassMaterial = glassMaterial;
            generator.CrackedGlassMaterial = crackedGlassMaterial;
            generator.MetalMaterial = metalMaterial;
            generator.StairwellFloorMaterial = stairwellFloorMaterial;
            generator.StairwellWallMaterial = stairwellWallMaterial;
            generator.StairwellLowerWallMaterial = stairwellLowerWallMaterial;
            generator.StairwellUpperWallMaterial = stairwellUpperWallMaterial;
            generator.StairwellCeilingMaterial = stairwellCeilingMaterial;
            generator.StairwellDoorMaterial = stairwellDoorMaterial;
            generator.FurnitureWoodMaterial = furnitureWoodMaterial;
            generator.FurnitureFabricMaterial = furnitureFabricMaterial;
            generator.FurnitureCarpetMaterial = furnitureCarpetMaterial;
            generator.FurnitureMetalMaterial = furnitureMetalMaterial;
            generator.FurnitureCeramicMaterial = furnitureCeramicMaterial;
            generator.FurniturePaperMaterial = furniturePaperMaterial;
            generator.FurnitureDarkPlasticMaterial = furnitureDarkPlasticMaterial;
            generator.KeyMaterial = keyMaterial;
        }

        private int[] BuildForcedLayoutSequence(int floors)
        {
            int[] result = new int[floors];
            int[] source = FloorTemplateIndices != null && FloorTemplateIndices.Length > 0
                ? FloorTemplateIndices
                : new[] { 1 };
            for (int i = 0; i < floors; i++)
            {
                int raw = i < source.Length ? source[i] : source[source.Length - 1];
                result[i] = Mathf.Clamp(raw, 1, 5);
            }

            return result;
        }

        private void BuildClosedFacadeBalcony(Transform parent, int floor, float x, float facadeSign)
        {
            float y = floor * 3.2f + 0.18f;
            float z = facadeSign * 9.72f;
            Transform balcony = Group(
                "Apartment_Balcony_F" + (floor + 1) + "_" +
                (facadeSign < 0f ? "Front" : "Back") + "_" + x,
                parent);
            balcony.localPosition = new Vector3(x, y, z);
            CreateBox("Platform", balcony, Vector3.zero,
                new Vector3(3.4f, 0.22f, 1.35f), floorMaterial, false);
            CreateBox("Outer_Rail", balcony, new Vector3(0f, 0.68f, facadeSign * 0.58f),
                new Vector3(3.4f, 1.25f, 0.12f), metalMaterial, false);
            CreateBox("Side_Rail_A", balcony, new Vector3(-1.64f, 0.68f, 0f),
                new Vector3(0.12f, 1.25f, 1.25f), metalMaterial, false);
            CreateBox("Side_Rail_B", balcony, new Vector3(1.64f, 0.68f, 0f),
                new Vector3(0.12f, 1.25f, 1.25f), metalMaterial, false);
        }

        private List<EntrancePose> BuildLayout(int entrances)
        {
            float step = Mathf.Max(1f, EntranceStep);
            float zOffset = SecondRowZOffset;
            List<EntrancePose> layout = new List<EntrancePose>(entrances);

            switch (LayoutShape)
            {
                case MiniVanPanelkaManualLayoutShape.LShape:
                {
                    int alongX = Mathf.Max(1, (entrances + 1) / 2);
                    for (int i = 0; i < entrances; i++)
                    {
                        Vector3 pos;
                        if (i < alongX)
                        {
                            pos = new Vector3(i * step, 0f, 0f);
                        }
                        else
                        {
                            int k = i - alongX + 1;
                            pos = new Vector3((alongX - 1) * step, 0f, k * zOffset);
                        }

                        layout.Add(new EntrancePose(pos, ResolveEntranceYaw(i)));
                    }

                    break;
                }
                case MiniVanPanelkaManualLayoutShape.UShape:
                {
                    if (entrances == 1)
                    {
                        layout.Add(new EntrancePose(Vector3.zero, ResolveEntranceYaw(0)));
                        break;
                    }

                    int side = Mathf.Max(1, (entrances + 2) / 3);
                    int index = 0;
                    for (int i = 0; i < side && index < entrances; i++, index++)
                    {
                        layout.Add(new EntrancePose(
                            new Vector3(i * step, 0f, 0f),
                            ResolveEntranceYaw(index)));
                    }

                    for (int i = 1; i <= side && index < entrances; i++, index++)
                    {
                        layout.Add(new EntrancePose(
                            new Vector3((side - 1) * step, 0f, i * zOffset),
                            ResolveEntranceYaw(index)));
                    }

                    for (int i = side - 2; i >= 0 && index < entrances; i--, index++)
                    {
                        layout.Add(new EntrancePose(
                            new Vector3(i * step, 0f, side * zOffset),
                            ResolveEntranceYaw(index)));
                    }

                    while (index < entrances)
                    {
                        layout.Add(new EntrancePose(
                            new Vector3(index * step, 0f, 0f),
                            ResolveEntranceYaw(index)));
                        index++;
                    }

                    break;
                }
                case MiniVanPanelkaManualLayoutShape.TwoRows:
                {
                    int firstRow = (entrances + 1) / 2;
                    for (int i = 0; i < entrances; i++)
                    {
                        bool second = i >= firstRow;
                        int col = second ? i - firstRow : i;
                        float origin = -((second ? entrances - firstRow : firstRow) - 1) * 0.5f * step;
                        layout.Add(new EntrancePose(
                            new Vector3(origin + col * step, 0f, second ? zOffset : 0f),
                            ResolveEntranceYaw(i)));
                    }

                    break;
                }
                default:
                {
                    float origin = -(entrances - 1) * 0.5f * step;
                    for (int i = 0; i < entrances; i++)
                    {
                        layout.Add(new EntrancePose(
                            new Vector3(origin + i * step, 0f, 0f),
                            ResolveEntranceYaw(i)));
                    }

                    break;
                }
            }

            return layout;
        }

        private float ResolveEntranceYaw(int entranceIndex)
        {
            float yaw = RowYaw;
            if (EntranceYawOffsets != null &&
                entranceIndex >= 0 &&
                entranceIndex < EntranceYawOffsets.Length)
            {
                yaw += EntranceYawOffsets[entranceIndex];
            }

            return yaw;
        }

        private HashSet<int> PickAccessibleIndices(int entrances)
        {
            HashSet<int> result = new HashSet<int>();
            if (AccessibleMode == MiniVanPanelkaAccessibleMode.IndexList)
            {
                if (AccessibleEntranceNumbers == null)
                {
                    return result;
                }

                for (int i = 0; i < AccessibleEntranceNumbers.Length; i++)
                {
                    int number = AccessibleEntranceNumbers[i];
                    int index = number - 1;
                    if (index >= 0 && index < entrances)
                    {
                        result.Add(index);
                    }
                }

                return result;
            }

            int count = Mathf.Clamp(AccessibleEntrances, 0, Mathf.Max(0, entrances));
            for (int i = 0; i < count; i++)
            {
                result.Add(i);
            }

            return result;
        }

        private static void SyncLadderClimbTriggers(Transform root)
        {
            if (root == null)
            {
                return;
            }

            MiniVanLadder[] ladders = root.GetComponentsInChildren<MiniVanLadder>(true);
            for (int i = 0; i < ladders.Length; i++)
            {
                MiniVanLadder ladder = ladders[i];
                if (ladder == null)
                {
                    continue;
                }

                // Existing baked ladders keep the older narrow engage volume; widen so roof
                // re-entry for descend is reliable without a full geometry rebuild.
                if (ladder.EngageHalfWidth < 1.05f)
                {
                    ladder.EngageHalfWidth = 1.05f;
                }

                if (ladder.EngageDepth < 1.05f)
                {
                    ladder.EngageDepth = 1.05f;
                }

                EnsureLadderPhysicalCollider(ladder);
                ladder.SyncClimbVolume();
            }
        }

        /// <summary>
        /// Ladders baked before the solid rung plane existed let the player walk between the
        /// rails; rebuild the thin blocker from the rail geometry.
        /// </summary>
        private static void EnsureLadderPhysicalCollider(MiniVanLadder ladder)
        {
            Transform railLeft = ladder.transform.Find("Rail_Left");
            Transform railRight = ladder.transform.Find("Rail_Right");
            if (railLeft == null || railRight == null)
            {
                return;
            }

            float height = Mathf.Max(railLeft.localScale.y, railRight.localScale.y);
            if (height < 0.5f)
            {
                return;
            }

            Transform existing = ladder.transform.Find("Ladder_Physical_Collider");
            BoxCollider blocker = existing != null ? existing.GetComponent<BoxCollider>() : null;
            if (blocker == null)
            {
                GameObject holder = existing != null
                    ? existing.gameObject
                    : new GameObject("Ladder_Physical_Collider");
                holder.transform.SetParent(ladder.transform, false);
                blocker = holder.GetComponent<BoxCollider>();
                if (blocker == null)
                {
                    blocker = holder.AddComponent<BoxCollider>();
                }
            }

            blocker.enabled = true;
            blocker.isTrigger = false;
            blocker.center = new Vector3(0f, height * 0.5f, 0f);
            blocker.size = new Vector3(1.12f, height, 0.26f);
        }

        private void EnsureOutsideGeneratedWorldContent()
        {
            Transform parent = transform.parent;
            if (parent == null || parent.name != "Generated_GameMode_Content")
            {
                return;
            }

            Transform worldRoot = parent.parent;
            transform.SetParent(worldRoot != null ? worldRoot : null, true);
        }

        private static Bounds[] BuildFacadeOcclusionBounds(
            List<EntrancePose> layout,
            int moduleIndex,
            HashSet<int> includedIndices = null)
        {
            EntrancePose module = layout[moduleIndex];
            Quaternion inverseRotation =
                Quaternion.Inverse(Quaternion.Euler(0f, module.Yaw, 0f));
            List<Bounds> bounds = new List<Bounds>(layout.Count - 1);

            for (int otherIndex = 0; otherIndex < layout.Count; otherIndex++)
            {
                if (otherIndex == moduleIndex)
                {
                    continue;
                }

                if (includedIndices != null && !includedIndices.Contains(otherIndex))
                {
                    continue;
                }

                EntrancePose other = layout[otherIndex];
                Quaternion otherRotation = Quaternion.Euler(0f, other.Yaw, 0f);
                Bounds localBounds = new Bounds();
                bool initialized = false;
                for (int corner = 0; corner < 4; corner++)
                {
                    Vector3 otherLocal = new Vector3(
                        (corner & 1) == 0 ? -ModuleWidth * 0.5f : ModuleWidth * 0.5f,
                        0f,
                        (corner & 2) == 0 ? -ModuleDepth * 0.5f : ModuleDepth * 0.5f);
                    Vector3 sitePoint = other.Position + otherRotation * otherLocal;
                    Vector3 moduleLocal = inverseRotation * (sitePoint - module.Position);
                    if (!initialized)
                    {
                        localBounds = new Bounds(moduleLocal, Vector3.zero);
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

        private void EnsureMaterials()
        {
#if UNITY_EDITOR
            const string stage1 = "Assets/MiniVan Game/Materials/Panelka/Stage1/";
            const string generated = "Assets/MiniVan Game/Materials/Panelka/Generated/";
            const string stairwell = "Assets/MiniVan Game/Materials/Panelka/Interior/LowPolyPack/";

            exteriorMaterial = LoadMaterial(exteriorMaterial, stage1 + "PanelkaStage1_Exterior.mat");
            interiorMaterial = LoadMaterial(interiorMaterial, stage1 + "PanelkaStage1_Interior.mat");
            floorMaterial = LoadMaterial(floorMaterial, stage1 + "PanelkaStage1_Floor.mat");
            doorMaterial = LoadMaterial(doorMaterial, stage1 + "PanelkaStage1_Door.mat");
            apartmentDoorMaterials = LoadApartmentDoorPalette(
                apartmentDoorMaterials,
                stairwell);
            glassMaterial = LoadMaterial(
                glassMaterial,
                stage1 + "PanelkaStage1_WindowGlassGenerated.mat");
            crackedGlassMaterial = LoadMaterial(
                crackedGlassMaterial,
                stage1 + "PanelkaStage1_WindowGlassCrackedGenerated.mat");
            metalMaterial = LoadMaterial(metalMaterial, stage1 + "PanelkaStage1_Metal.mat");
            stairwellFloorMaterial = LoadMaterial(
                stairwellFloorMaterial,
                stairwell + "Stairwell_Floor_GrayTerrazzo_01.mat");
            stairwellWallMaterial = LoadMaterial(
                stairwellWallMaterial,
                stairwell + "Stairwell_Wall_GreenWhite_01.mat");
            stairwellLowerWallMaterial = LoadMaterial(
                stairwellLowerWallMaterial,
                stairwell + "Stairwell_Wall_GreenLower_01.mat");
            stairwellUpperWallMaterial = LoadMaterial(
                stairwellUpperWallMaterial,
                stairwell + "Stairwell_Wall_WhiteUpper_01.mat");
            stairwellCeilingMaterial = stairwellCeilingMaterial != null
                ? stairwellCeilingMaterial
                : stairwellWallMaterial;
            stairwellDoorMaterial = LoadMaterial(
                stairwellDoorMaterial,
                stairwell + "Door_GrayMetal_04.mat");
            furnitureWoodMaterial = LoadMaterial(
                furnitureWoodMaterial,
                generated + "Panelka_Wood_Pixel.mat");
            furnitureFabricMaterial = LoadMaterial(
                furnitureFabricMaterial,
                generated + "Panelka_Fabric_Red_Pixel.mat");
            furnitureCarpetMaterial = LoadMaterial(
                furnitureCarpetMaterial,
                generated + "Panelka_Carpet_Pixel.mat");
            furnitureMetalMaterial = LoadMaterial(
                furnitureMetalMaterial,
                generated + "Panelka_Metal_Pixel.mat");
            furnitureCeramicMaterial = LoadMaterial(
                furnitureCeramicMaterial,
                generated + "Panelka_Ceramic_Pixel.mat");
            furniturePaperMaterial = LoadMaterial(
                furniturePaperMaterial,
                generated + "Panelka_Paper_Pixel.mat");
            furnitureDarkPlasticMaterial = LoadMaterial(
                furnitureDarkPlasticMaterial,
                generated + "Panelka_DarkPlastic_Pixel.mat");
            keyMaterial = LoadMaterial(keyMaterial, generated + "Panelka_Key_Yellow.mat");

            if (zombiePrefab == null)
            {
                zombiePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/MiniVan Game/Prefabs/Characters/Zombies/Zombie.prefab");
            }
#endif
        }

#if UNITY_EDITOR
        private static Material[] LoadApartmentDoorPalette(
            Material[] current,
            string folder)
        {
            if (current != null && current.Length > 0)
            {
                return current;
            }

            string[] names =
            {
                "Door_DarkBrownPanels_01.mat",
                "Door_FadedTeal_02.mat",
                "Door_MustardVinyl_03.mat",
                "Door_GrayMetal_04.mat",
                "Door_RedBrownVeneer_05.mat"
            };
            List<Material> palette = new List<Material>(names.Length);
            for (int i = 0; i < names.Length; i++)
            {
                Material material =
                    UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(folder + names[i]);
                if (material != null)
                {
                    palette.Add(material);
                }
            }

            return palette.ToArray();
        }

        private static Material LoadMaterial(Material current, string path)
        {
            if (current != null)
            {
                return current;
            }

            return UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(path);
        }
#endif

        private static Transform Group(string name, Transform parent)
        {
            GameObject group = new GameObject(name);
            group.transform.SetParent(parent, false);
            return group.transform;
        }

        private static GameObject CreateBox(
            string name,
            Transform parent,
            Vector3 localPosition,
            Vector3 localScale,
            Material material,
            bool collider)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = localPosition;
            box.transform.localScale = localScale;
            if (material != null)
            {
                box.GetComponent<Renderer>().sharedMaterial = material;
            }

            Collider boxCollider = box.GetComponent<Collider>();
            if (!collider && boxCollider != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(boxCollider);
                }
                else
                {
                    DestroyImmediate(boxCollider);
                }
            }

            return box;
        }
    }
}
