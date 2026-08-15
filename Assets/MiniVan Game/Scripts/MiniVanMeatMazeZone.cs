using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Serialization;

namespace MiniVanGame
{
    /// <summary>
    /// Meat maze: infection is a cheap procedural 2D field over the whole zone, while the deformable
    /// surface-nets mesh and MeshColliders only exist in a streaming radius around players.
    /// That keeps 2000x2000 maps viable without allocating a continent of voxels.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MiniVanMeatMazeZone : NetworkBehaviour
    {
        public static MiniVanMeatMazeZone Instance { get; private set; }

        [Header("Zone")]
        [Tooltip("Width of the zone along local X, in meters.")]
        [FormerlySerializedAs("ZoneSize")]
        public float ZoneSizeX = 60f;
        [Tooltip("Depth of the zone along local Z, in meters (second map axis).")]
        public float ZoneSizeY = 60f;
        public float CellSize = 0.55f;
        public float WallHeight = 3.5f;
        public int EditorSeed = 4242;

        [Header("Shape")]
        [Tooltip("Noise frequency along local X, in cycles per meter.")]
        [FormerlySerializedAs("NoiseScale")]
        public float NoiseScaleX = 0.075f;
        [Tooltip("Noise frequency along local Z, in cycles per meter.")]
        public float NoiseScaleY = 0.075f;
        [Tooltip("0 = organic Perlin contours, 1 = square Chebyshev lattice walls.")]
        [Range(0f, 1f)]
        public float NoiseAngular = 0f;
        [Tooltip("Noise levels the walls follow. Two contours give branching junctions.")]
        public float ContourA = 0.42f;
        public float ContourB = 0.68f;
        [Tooltip("Wall thickness in meters.")]
        public float WallThickness = 1.7f;
        [Tooltip("Meters of extra thickness each infection pulse adds.")]
        public float ThicknessPerPulse = 0.3f;
        public float MaxWallThickness = 3.6f;
        [Tooltip("Meters of 3D noise displacement applied to the surface.")]
        public float BlobAmplitude = 0.6f;
        public float BlobScale = 0.55f;
        public float EntranceDepth = 4f;
        public float EntranceWidth = 2.4f;

        [Header("Growth & Pulse")]
        public float GrowthSpeed = 0.4f;
        public float ShrinkSpeed = 1.2f;
        public float InitialGrowSeconds = 6f;
        [Tooltip("Seconds between full maze pattern rebuilds.")]
        public float PulseIntervalSeconds = 150f;
        [Tooltip("Seconds over which the new pattern morphs in after a pulse.")]
        public float PulseMorphSeconds = 30f;

        [Header("Burn")]
        public float BurnRadius = 1.7f;
        public float BurnStrengthPerStroke = 0.35f;
        public float BurnHoldSeconds = 5f;
        public float BurnRegenSeconds = 18f;
        [Tooltip("How deeply a burn stroke carves through wall height (1 = full tunnel through typical walls).")]
        [Range(0.5f, 2.5f)]
        public float BurnCarveDepth = 1.35f;

        [Header("Streaming")]
        [Tooltip("Meters around each player where full meat mesh + colliders stay loaded.")]
        public float StreamRadius = 120f;
        [Tooltip("Meters around each player where cheap distant LOD meshes stay visible.")]
        public float LodRadius = 280f;
        [Tooltip("Extra meters before an idle chunk is unloaded.")]
        public float StreamUnloadExtra = 40f;
        [Tooltip("Seconds for a newly loaded chunk to fade/grow in.")]
        public float ChunkFadeInSeconds = 0.55f;
        [Tooltip("Max new chunks created per frame.")]
        public int MaxChunkLoadsPerFrame = 2;
        [Tooltip("Max idle chunks destroyed per frame.")]
        public int MaxChunkUnloadsPerFrame = 2;
        [Tooltip("Hard cap on simultaneously loaded chunks.")]
        public int MaxLoadedChunks = 280;
        [Tooltip("If the whole zone fits in this many chunks, keep everything resident (small test zones).")]
        public int LoadAllBelowChunkCount = 64;

        [Header("Performance")]
        [Tooltip("Cells per chunk edge. Smaller chunks rebuild faster but add draw calls.")]
        public int ChunkCells = 20;
        [Tooltip("Maximum chunk meshes rebuilt per frame.")]
        public int ChunkRebuildsPerFrame = 2;
        public bool ShowGroundPatch = true;

        [Header("Visual")]
        [Tooltip("Assign a Material using MiniVanGame/MeatMazeOrganic. Its colors/veins are kept; Motion* still applies at runtime.")]
        public Material MeatMaterial;
        [Tooltip("How strongly the meat writhes. Visual only — colliders stay still.")]
        [Range(0f, 1f)]
        public float MotionDistort = 0.25f;
        [Tooltip("Speed of the writhe / breathing motion.")]
        public float MotionSpeed = 1.1f;

        private readonly NetworkVariable<int> networkSeed = new NetworkVariable<int>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> networkPulse = new NetworkVariable<int>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private sealed class StreamChunk
        {
            public int Cx;
            public int Cz;
            public int CellMinX;
            public int CellMaxX;
            public int CellMinZ;
            public int CellMaxZ;
            public int NodeMinX;
            public int NodeMaxX;
            public int NodeMinZ;
            public int NodeMaxZ;
            public int LnX;
            public int LnZ;
            public float[] Field;
            public float[] Bump;
            public float[] BaseDensity;
            public float[] DisplayGrowth;
            public float[] MorphFrom;
            public float[] MorphTo;
            public bool[] ColumnDirty;
            public int[] CellVertex;
            public GameObject Go;
            public MeshFilter Filter;
            public MeshCollider Collider;
            public Mesh Mesh;
            public bool MeshDirty;
            public bool AnyColumnDirty;
            public bool HasCollisionMesh;
            public bool IsLod;
            public float Fade = 1f;
            public int MeshStride = 1;
        }

        private struct BurnCell
        {
            public float Amount;
            public float Expire;
        }

        // Logical lattice over the whole zone (counts only — no full arrays).
        private int gx, gy, gz;
        private int nx, ny, nz;
        private float yMin;
        private int chunksX;
        private int chunksZ;
        private int chunkStride;

        private readonly Dictionary<long, StreamChunk> loaded = new Dictionary<long, StreamChunk>(256);
        private readonly Dictionary<long, BurnCell> burns = new Dictionary<long, BurnCell>(1024);
        private readonly List<long> burnKeyBuffer = new List<long>(256);
        private readonly List<Vector3> focusPoints = new List<Vector3>(16);
        private readonly HashSet<long> desiredFull = new HashSet<long>();
        private readonly HashSet<long> desiredLod = new HashSet<long>();
        private readonly List<long> unloadBuffer = new List<long>(64);
        private readonly List<long> loadBuffer = new List<long>(64);
        private readonly List<long> rebuildCursorKeys = new List<long>(256);

        private readonly List<Vector3> verts = new List<Vector3>(4096);
        private readonly List<Vector3> normals = new List<Vector3>(4096);
        private readonly List<Color> colors = new List<Color>(4096);
        private readonly List<int> tris = new List<int>(8192);
        private readonly float[] corner = new float[8];

        private Material meatMaterial;
        private Transform chunkRoot;

        private float nextPulseTime;
        private int cachedSeed = int.MinValue;
        private int cachedPulse = int.MinValue;
        private int localPulse;
        private float spawnTime;
        private bool isMorphing;
        private float morphElapsed;
        private int morphFromPulse;
        private int morphToPulse;
        private int rebuildCursor;
        private bool gridsReady;
        private bool forceLoadAll;

        public int CurrentSeed => IsSpawned ? networkSeed.Value : EditorSeed;
        public int CurrentPulse => IsSpawned ? networkPulse.Value : localPulse;
        public int LoadedChunkCount => loaded.Count;

        public float HalfX => ZoneSizeX * 0.5f;
        public float HalfZ => ZoneSizeY * 0.5f;

        public int GridCellsX => Mathf.Max(4, Mathf.RoundToInt(ZoneSizeX / Mathf.Max(0.2f, CellSize)));
        public int GridCellsZ => Mathf.Max(4, Mathf.RoundToInt(ZoneSizeY / Mathf.Max(0.2f, CellSize)));
        public int GridCellsVertical => Mathf.Max(3, Mathf.CeilToInt(
            (WallHeight + BlobAmplitude + CellSize * 2f) / Mathf.Max(0.2f, CellSize)));

        public int ChunkGridSizeX => Mathf.CeilToInt(GridCellsX / (float)Mathf.Max(2, ChunkCells));
        public int ChunkGridSizeZ => Mathf.CeilToInt(GridCellsZ / (float)Mathf.Max(2, ChunkCells));

        public float ThicknessAtPulse(int pulse) =>
            Mathf.Min(MaxWallThickness, WallThickness + pulse * ThicknessPerPulse);

        private static readonly int[] CornerX = { 0, 1, 1, 0, 0, 1, 1, 0 };
        private static readonly int[] CornerY = { 0, 0, 0, 0, 1, 1, 1, 1 };
        private static readonly int[] CornerZ = { 0, 0, 1, 1, 0, 0, 1, 1 };
        private static readonly int[] EdgeA = { 0, 1, 2, 3, 4, 5, 6, 7, 0, 1, 2, 3 };
        private static readonly int[] EdgeB = { 1, 2, 3, 0, 5, 6, 7, 4, 4, 5, 6, 7 };

        private void Awake()
        {
            spawnTime = Time.time;
            EnsureRuntimeObjects();
            EnsureLattice();
        }

        private void OnEnable()
        {
            Instance = this;
        }

        private void OnDisable()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public override void OnNetworkSpawn()
        {
            networkSeed.OnValueChanged += HandleSeedOrPulseChanged;
            networkPulse.OnValueChanged += HandleSeedOrPulseChanged;
            spawnTime = Time.time;

            if (IsServer)
            {
                if (networkSeed.Value == 0)
                {
                    networkSeed.Value = EditorSeed != 0 ? EditorSeed : UnityEngine.Random.Range(1, int.MaxValue);
                }

                ScheduleNextPulse();
                NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
            }

            ResetGrowthAndRebuild();
        }

        public override void OnNetworkDespawn()
        {
            networkSeed.OnValueChanged -= HandleSeedOrPulseChanged;
            networkPulse.OnValueChanged -= HandleSeedOrPulseChanged;
            if (IsServer && NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
            }
        }

        private void Start()
        {
            if (!IsSpawned)
            {
                ScheduleNextPulse();
                ResetGrowthAndRebuild();
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            EnsureLattice();

            if (IsServerAuthority() && Time.time >= nextPulseTime)
            {
                ApplyPulse();
                ScheduleNextPulse();
            }

            TickStreaming();
            TickChunkFades(dt);
            TickMorph(dt);
            TickBurnRegen(dt);
            if (!isMorphing)
            {
                TickGrowth(dt);
            }

            FlushColumnUpdates();
            int budget = isMorphing
                ? Mathf.Max(ChunkRebuildsPerFrame, Mathf.CeilToInt(loaded.Count / 12f))
                : ChunkRebuildsPerFrame;
            RebuildDirtyChunks(budget);
            ApplyVisualMaterialParams();
        }

        // ---------------------------------------------------------------- public API

        public bool ContainsPoint(Vector3 worldPoint)
        {
            Vector3 local = transform.InverseTransformPoint(worldPoint);
            return local.x >= -HalfX - 0.5f && local.x <= HalfX + 0.5f &&
                   local.z >= -HalfZ - 0.5f && local.z <= HalfZ + 0.5f &&
                   local.y >= -1.5f && local.y <= WallHeight + 2.5f;
        }

        /// <summary>Marches the flame through the field and melts the first meat it touches.</summary>
        public bool TryBurnBeam(Vector3 worldOrigin, Vector3 worldDirection, float range)
        {
            if (!IsServerAuthority())
            {
                return false;
            }

            EnsureLattice();
            Vector3 origin = transform.InverseTransformPoint(worldOrigin);
            Vector3 dir = transform.InverseTransformDirection(worldDirection);
            if (dir.sqrMagnitude < 0.0001f)
            {
                return false;
            }

            dir.Normalize();
            float step = Mathf.Max(0.15f, CellSize * 0.5f);
            float intro = IntroFactor();
            for (float t = 0.4f; t <= range; t += step)
            {
                Vector3 p = origin + dir * t;
                if (!InsideLocalBounds(p))
                {
                    continue;
                }

                if (SampleOccupancyLocal(p, intro) > 0f)
                {
                    return BurnAtLocal(p, BurnRadius, BurnStrengthPerStroke);
                }
            }

            return false;
        }

        public bool TryBurnSphere(Vector3 worldCenter, float radius)
        {
            if (!IsServerAuthority())
            {
                return false;
            }

            EnsureLattice();
            Vector3 local = transform.InverseTransformPoint(worldCenter);
            return BurnAtLocal(local, Mathf.Max(radius, BurnRadius), BurnStrengthPerStroke);
        }

        // ---------------------------------------------------------------- burn

        private bool BurnAtLocal(Vector3 local, float radius, float strength)
        {
            bool any = PaintBurn(local, radius, strength);
            if (any && IsSpawned && IsServer)
            {
                BurnStrokeClientRpc(local, radius, strength);
            }

            return any;
        }

        private bool PaintBurn(Vector3 local, float radius, float strength)
        {
            EnsureLattice();
            float halfX = HalfX;
            float halfZ = HalfZ;
            int minI = Mathf.Clamp(Mathf.FloorToInt((local.x + halfX - radius) / CellSize), 0, nx - 1);
            int maxI = Mathf.Clamp(Mathf.CeilToInt((local.x + halfX + radius) / CellSize), 0, nx - 1);
            int minK = Mathf.Clamp(Mathf.FloorToInt((local.z + halfZ - radius) / CellSize), 0, nz - 1);
            int maxK = Mathf.Clamp(Mathf.CeilToInt((local.z + halfZ + radius) / CellSize), 0, nz - 1);
            int minJ = Mathf.Clamp(Mathf.FloorToInt((local.y - radius - yMin) / CellSize), 1, ny - 2);
            int maxJ = Mathf.Clamp(Mathf.CeilToInt((local.y + radius - yMin) / CellSize), 1, ny - 2);

            bool any = false;
            float radiusSqr = radius * radius;
            float now = Time.time;
            float intro = IntroFactor();
            int pulse = CurrentPulse;

            for (int k = minK; k <= maxK; k++)
            {
                float pz = -halfZ + k * CellSize;
                for (int i = minI; i <= maxI; i++)
                {
                    float px = -halfX + i * CellSize;
                    float dx = px - local.x;
                    float dz = pz - local.z;
                    float distXZSqr = dx * dx + dz * dz;
                    if (distXZSqr > radiusSqr)
                    {
                        continue;
                    }

                    float density = SampleDensityWorld(px, pz, pulse);
                    if (density * intro <= 0.01f)
                    {
                        continue;
                    }

                    bool columnHit = false;
                    for (int j = minJ; j <= maxJ; j++)
                    {
                        float py = yMin + j * CellSize;
                        float dy = py - local.y;
                        float distSqr = distXZSqr + dy * dy;
                        if (distSqr > radiusSqr)
                        {
                            continue;
                        }

                        // Only carve where meat currently exists (height shell), so we dig tunnels
                        // instead of deleting whole columns into gates.
                        float top = density * intro * (WallHeight + CellSize) - CellSize;
                        if (py > top + CellSize * 0.75f || py < -CellSize * 0.25f)
                        {
                            continue;
                        }

                        long key = PackVoxel(i, j, k);
                        burns.TryGetValue(key, out BurnCell cell);
                        float before = cell.Amount;
                        float falloff = 1f - Mathf.Sqrt(distSqr) / Mathf.Max(0.01f, radius);
                        cell.Amount = Mathf.Min(2f, before + strength * SmoothStep01(0f, 1f, falloff + 0.2f));
                        cell.Expire = now + BurnHoldSeconds;
                        if (cell.Amount <= 0.001f)
                        {
                            burns.Remove(key);
                        }
                        else
                        {
                            burns[key] = cell;
                        }

                        if (cell.Amount > before + 0.0005f)
                        {
                            columnHit = true;
                            any = true;
                        }
                    }

                    if (columnHit)
                    {
                        MarkWorldColumnDirty(i, k);
                    }
                }
            }

            return any;
        }

        private void TickBurnRegen(float dt)
        {
            if (burns.Count == 0)
            {
                return;
            }

            float now = Time.time;
            float regen = dt / Mathf.Max(0.5f, BurnRegenSeconds);
            burnKeyBuffer.Clear();
            foreach (KeyValuePair<long, BurnCell> pair in burns)
            {
                burnKeyBuffer.Add(pair.Key);
            }

            for (int n = 0; n < burnKeyBuffer.Count; n++)
            {
                long key = burnKeyBuffer[n];
                BurnCell cell = burns[key];
                if (now < cell.Expire)
                {
                    continue;
                }

                cell.Amount = Mathf.Max(0f, cell.Amount - regen);
                if (cell.Amount <= 0.001f)
                {
                    burns.Remove(key);
                }
                else
                {
                    burns[key] = cell;
                }

                UnpackVoxel(key, out int wi, out int wj, out int wk);
                MarkWorldColumnDirty(wi, wk);
            }
        }

        private float GetBurn3D(int i, int j, int k)
        {
            return burns.TryGetValue(PackVoxel(i, j, k), out BurnCell cell) ? cell.Amount : 0f;
        }

        private float SampleBurnWorld(float localX, float localY, float localZ)
        {
            float fx = (localX + HalfX) / CellSize;
            float fy = (localY - yMin) / CellSize;
            float fz = (localZ + HalfZ) / CellSize;
            int i = Mathf.Clamp(Mathf.RoundToInt(fx), 0, nx - 1);
            int j = Mathf.Clamp(Mathf.RoundToInt(fy), 0, ny - 1);
            int k = Mathf.Clamp(Mathf.RoundToInt(fz), 0, nz - 1);
            return GetBurn3D(i, j, k);
        }

        // ---------------------------------------------------------------- growth / morph

        private void TickGrowth(float dt)
        {
            float intro = IntroFactor();
            foreach (StreamChunk chunk in loaded.Values)
            {
                for (int lk = 0; lk < chunk.LnZ; lk++)
                {
                    for (int li = 0; li < chunk.LnX; li++)
                    {
                        int c = LCol(chunk, li, lk);
                        // Burn is carved in 3D inside BuildFieldColumn — do not flatten whole columns.
                        float target = chunk.BaseDensity[c] * intro;
                        float current = chunk.DisplayGrowth[c];
                        float delta = target - current;
                        if (delta > -0.0005f && delta < 0.0005f)
                        {
                            continue;
                        }

                        float speed = delta < 0f ? ShrinkSpeed : GrowthSpeed;
                        chunk.DisplayGrowth[c] = Mathf.MoveTowards(current, target, speed * dt);
                        chunk.ColumnDirty[c] = true;
                        chunk.AnyColumnDirty = true;
                    }
                }
            }
        }

        private void BeginPatternMorph(int seed, int pulse)
        {
            EnsureLattice();
            morphFromPulse = cachedPulse == int.MinValue ? Mathf.Max(0, pulse - 1) : cachedPulse;
            morphToPulse = pulse;
            morphElapsed = 0f;
            isMorphing = loaded.Count > 0;
            cachedSeed = seed;
            cachedPulse = pulse;

            foreach (StreamChunk chunk in loaded.Values)
            {
                Array.Copy(chunk.BaseDensity, chunk.MorphFrom, chunk.BaseDensity.Length);
                FillChunkDensity(chunk, chunk.MorphTo, morphToPulse);
            }

            if (!isMorphing)
            {
                // Nothing loaded yet: next chunk load will evaluate the current pulse directly.
            }
        }

        private void SnapPattern(int seed, int pulse)
        {
            isMorphing = false;
            morphElapsed = 0f;
            morphFromPulse = pulse;
            morphToPulse = pulse;
            cachedSeed = seed;
            cachedPulse = pulse;

            foreach (StreamChunk chunk in loaded.Values)
            {
                FillChunkDensity(chunk, chunk.BaseDensity, pulse);
                float intro = IntroFactor();
                for (int c = 0; c < chunk.DisplayGrowth.Length; c++)
                {
                    chunk.DisplayGrowth[c] = chunk.BaseDensity[c] * intro;
                    chunk.ColumnDirty[c] = true;
                }

                chunk.AnyColumnDirty = true;
                EnsureChunkBump(chunk, seed);
            }
        }

        private void TickMorph(float dt)
        {
            if (!isMorphing)
            {
                return;
            }

            morphElapsed += dt;
            float t = SmoothStep01(0f, 1f, morphElapsed / Mathf.Max(0.1f, PulseMorphSeconds));
            float intro = IntroFactor();

            foreach (StreamChunk chunk in loaded.Values)
            {
                for (int c = 0; c < chunk.BaseDensity.Length; c++)
                {
                    float density = Mathf.Lerp(chunk.MorphFrom[c], chunk.MorphTo[c], t);
                    float targetGrowth = density * intro;
                    if (Mathf.Abs(density - chunk.BaseDensity[c]) > 0.0004f ||
                        Mathf.Abs(targetGrowth - chunk.DisplayGrowth[c]) > 0.0004f)
                    {
                        chunk.ColumnDirty[c] = true;
                        chunk.AnyColumnDirty = true;
                    }

                    chunk.BaseDensity[c] = density;
                    chunk.DisplayGrowth[c] = targetGrowth;
                }
            }

            if (t >= 1f)
            {
                foreach (StreamChunk chunk in loaded.Values)
                {
                    Array.Copy(chunk.MorphTo, chunk.BaseDensity, chunk.MorphTo.Length);
                }

                isMorphing = false;
            }
        }

        // ---------------------------------------------------------------- density API (global / procedural)

        public float EvaluateWallDensity(float px, float pz, int seed, int pulse)
        {
            float halfWidth = Mathf.Min(MaxWallThickness, WallThickness + pulse * ThicknessPerPulse) * 0.5f;
            float h = Mathf.Max(0.1f, CellSize);

            float n = SampleNoise(px, pz, seed, pulse);
            float dnx = (SampleNoise(px + h, pz, seed, pulse) - n) / h;
            float dnz = (SampleNoise(px, pz + h, seed, pulse) - n) / h;
            float gradient = Mathf.Max(0.004f, Mathf.Sqrt(dnx * dnx + dnz * dnz));

            float dist = Mathf.Min(Mathf.Abs(n - ContourA), Mathf.Abs(n - ContourB)) / gradient;
            float d = 1f - SmoothStep01(halfWidth * 0.72f, halfWidth, dist);

            float crest = Mathf.PerlinNoise(px * 0.045f + seed * 0.011f, pz * 0.045f - seed * 0.007f);
            d *= Mathf.Lerp(0.86f, 1.06f, crest);

            d *= OpeningMask(px, pz);
            d *= BorderMask(px, pz);
            return Mathf.Clamp01(d);
        }

        public float EvaluateRawNoise(float px, float pz, int seed, int pulse)
        {
            return SampleNoise(px, pz, seed, pulse);
        }

        private float SampleDensityWorld(float px, float pz, int pulse)
        {
            if (isMorphing)
            {
                float t = SmoothStep01(0f, 1f, morphElapsed / Mathf.Max(0.1f, PulseMorphSeconds));
                float a = EvaluateWallDensity(px, pz, CurrentSeed, morphFromPulse);
                float b = EvaluateWallDensity(px, pz, CurrentSeed, morphToPulse);
                return Mathf.Lerp(a, b, t);
            }

            return EvaluateWallDensity(px, pz, CurrentSeed, pulse);
        }

        private float OpeningMask(float px, float pz)
        {
            float halfX = HalfX;
            float halfZ = HalfZ;
            float w = Mathf.Max(0.4f, EntranceWidth * 0.5f);
            float open = 1f;

            float alongZ = halfZ - Mathf.Abs(pz);
            if (alongZ <= EntranceDepth && Mathf.Abs(px) <= w + 1f)
            {
                float lateral = Mathf.Clamp01(Mathf.Abs(px) - w);
                float depth = Mathf.Clamp01(alongZ / EntranceDepth);
                open = Mathf.Min(open, Mathf.Max(lateral, depth * depth));
            }

            float alongX = halfX - Mathf.Abs(px);
            if (alongX <= EntranceDepth && Mathf.Abs(pz) <= w + 1f)
            {
                float lateral = Mathf.Clamp01(Mathf.Abs(pz) - w);
                float depth = Mathf.Clamp01(alongX / EntranceDepth);
                open = Mathf.Min(open, Mathf.Max(lateral, depth * depth));
            }

            return open;
        }

        private float BorderMask(float px, float pz)
        {
            float edge = Mathf.Min(HalfX - Mathf.Abs(px), HalfZ - Mathf.Abs(pz));
            return Mathf.Clamp01(edge / Mathf.Max(0.01f, CellSize * 1.5f));
        }

        // ---------------------------------------------------------------- streaming

        private void TickStreaming()
        {
            CollectFocusPoints();
            desiredFull.Clear();
            desiredLod.Clear();

            if (forceLoadAll || chunksX * chunksZ <= Mathf.Max(1, LoadAllBelowChunkCount))
            {
                for (int cz = 0; cz < chunksZ; cz++)
                {
                    for (int cx = 0; cx < chunksX; cx++)
                    {
                        desiredFull.Add(PackChunk(cx, cz));
                    }
                }
            }
            else
            {
                float fullR = Mathf.Max(CellSize * chunkStride, StreamRadius);
                float lodR = Mathf.Max(fullR + CellSize * chunkStride, LodRadius);
                float fullR2 = fullR * fullR;
                float lodR2 = lodR * lodR;

                for (int f = 0; f < focusPoints.Count; f++)
                {
                    Vector3 local = focusPoints[f];
                    int centerCx = Mathf.Clamp(Mathf.FloorToInt((local.x + HalfX) / (CellSize * chunkStride)), 0, chunksX - 1);
                    int centerCz = Mathf.Clamp(Mathf.FloorToInt((local.z + HalfZ) / (CellSize * chunkStride)), 0, chunksZ - 1);
                    int span = Mathf.CeilToInt(lodR / (CellSize * chunkStride)) + 1;

                    for (int cz = Mathf.Max(0, centerCz - span); cz <= Mathf.Min(chunksZ - 1, centerCz + span); cz++)
                    {
                        for (int cx = Mathf.Max(0, centerCx - span); cx <= Mathf.Min(chunksX - 1, centerCx + span); cx++)
                        {
                            Vector3 chunkCenter = ChunkCenterLocal(cx, cz);
                            float dx = chunkCenter.x - local.x;
                            float dz = chunkCenter.z - local.z;
                            float d2 = dx * dx + dz * dz;
                            long key = PackChunk(cx, cz);
                            if (d2 <= fullR2)
                            {
                                desiredFull.Add(key);
                                desiredLod.Remove(key);
                            }
                            else if (d2 <= lodR2 && !desiredFull.Contains(key))
                            {
                                desiredLod.Add(key);
                            }
                        }
                    }
                }
            }

            float unloadR = Mathf.Max(LodRadius, StreamRadius) + StreamUnloadExtra;
            float unloadR2 = unloadR * unloadR;
            unloadBuffer.Clear();
            foreach (KeyValuePair<long, StreamChunk> pair in loaded)
            {
                if (desiredFull.Contains(pair.Key) || desiredLod.Contains(pair.Key))
                {
                    continue;
                }

                if (forceLoadAll || chunksX * chunksZ <= Mathf.Max(1, LoadAllBelowChunkCount))
                {
                    continue;
                }

                StreamChunk chunk = pair.Value;
                Vector3 center = ChunkCenterLocal(chunk.Cx, chunk.Cz);
                bool nearAny = false;
                for (int f = 0; f < focusPoints.Count; f++)
                {
                    float dx = center.x - focusPoints[f].x;
                    float dz = center.z - focusPoints[f].z;
                    if (dx * dx + dz * dz <= unloadR2)
                    {
                        nearAny = true;
                        break;
                    }
                }

                if (!nearAny)
                {
                    unloadBuffer.Add(pair.Key);
                }
            }

            int unloaded = 0;
            for (int i = 0; i < unloadBuffer.Count && unloaded < Mathf.Max(1, MaxChunkUnloadsPerFrame); i++)
            {
                UnloadChunk(unloadBuffer[i]);
                unloaded++;
            }

            // Upgrade LOD -> full when the player walks in.
            foreach (long key in desiredFull)
            {
                if (loaded.TryGetValue(key, out StreamChunk chunk) && chunk.IsLod)
                {
                    SetChunkQuality(chunk, lod: false);
                }
            }

            loadBuffer.Clear();
            foreach (long key in desiredFull)
            {
                if (!loaded.ContainsKey(key))
                {
                    loadBuffer.Add(key);
                }
            }

            foreach (long key in desiredLod)
            {
                if (!loaded.ContainsKey(key) && !desiredFull.Contains(key))
                {
                    loadBuffer.Add(key);
                }
            }

            if (loadBuffer.Count > 1 && focusPoints.Count > 0)
            {
                Vector3 focus = focusPoints[0];
                loadBuffer.Sort((a, b) =>
                {
                    UnpackChunk(a, out int ax, out int az);
                    UnpackChunk(b, out int bx, out int bz);
                    Vector3 ca = ChunkCenterLocal(ax, az);
                    Vector3 cb = ChunkCenterLocal(bx, bz);
                    float da = (ca.x - focus.x) * (ca.x - focus.x) + (ca.z - focus.z) * (ca.z - focus.z);
                    float db = (cb.x - focus.x) * (cb.x - focus.x) + (cb.z - focus.z) * (cb.z - focus.z);
                    return da.CompareTo(db);
                });
            }

            int loadedNow = 0;
            for (int i = 0; i < loadBuffer.Count && loadedNow < Mathf.Max(1, MaxChunkLoadsPerFrame); i++)
            {
                if (loaded.Count >= Mathf.Max(8, MaxLoadedChunks))
                {
                    break;
                }

                UnpackChunk(loadBuffer[i], out int cx, out int cz);
                bool lod = desiredLod.Contains(loadBuffer[i]) && !desiredFull.Contains(loadBuffer[i]);
                LoadChunk(cx, cz, lod);
                loadedNow++;
            }
        }

        private void TickChunkFades(float dt)
        {
            float speed = 1f / Mathf.Max(0.05f, ChunkFadeInSeconds);
            foreach (StreamChunk chunk in loaded.Values)
            {
                if (chunk.Fade >= 0.999f)
                {
                    chunk.Fade = 1f;
                    continue;
                }

                float before = chunk.Fade;
                chunk.Fade = Mathf.MoveTowards(chunk.Fade, 1f, speed * dt);
                if (Mathf.Abs(chunk.Fade - before) > 0.0001f)
                {
                    for (int c = 0; c < chunk.ColumnDirty.Length; c++)
                    {
                        chunk.ColumnDirty[c] = true;
                    }

                    chunk.AnyColumnDirty = true;
                }
            }
        }

        private void CollectFocusPoints()
        {
            focusPoints.Clear();
            float focusPad = Mathf.Max(LodRadius, StreamRadius);
            MiniVanPlayer[] players = FindObjectsByType<MiniVanPlayer>(FindObjectsSortMode.None);
            for (int i = 0; i < players.Length; i++)
            {
                MiniVanPlayer player = players[i];
                if (player == null)
                {
                    continue;
                }

                Vector3 local = transform.InverseTransformPoint(player.transform.position);
                if (Mathf.Abs(local.x) <= HalfX + focusPad && Mathf.Abs(local.z) <= HalfZ + focusPad)
                {
                    focusPoints.Add(local);
                }
            }

            if (focusPoints.Count == 0)
            {
                focusPoints.Add(Vector3.zero);
            }
        }

        private Vector3 ChunkCenterLocal(int cx, int cz)
        {
            int cellMinX = cx * chunkStride;
            int cellMaxX = Mathf.Min(gx - 1, (cx + 1) * chunkStride - 1);
            int cellMinZ = cz * chunkStride;
            int cellMaxZ = Mathf.Min(gz - 1, (cz + 1) * chunkStride - 1);
            float px = -HalfX + (cellMinX + cellMaxX) * 0.5f * CellSize;
            float pz = -HalfZ + (cellMinZ + cellMaxZ) * 0.5f * CellSize;
            return new Vector3(px, 0f, pz);
        }

        private void LoadChunk(int cx, int cz, bool lod)
        {
            long key = PackChunk(cx, cz);
            if (loaded.ContainsKey(key))
            {
                return;
            }

            EnsureChunkRoot();
            EnsureMeatMaterial();

            StreamChunk chunk = new StreamChunk
            {
                Cx = cx,
                Cz = cz,
                CellMinX = cx * chunkStride,
                CellMaxX = Mathf.Min(gx - 1, (cx + 1) * chunkStride - 1),
                CellMinZ = cz * chunkStride,
                CellMaxZ = Mathf.Min(gz - 1, (cz + 1) * chunkStride - 1),
                Fade = 0f
            };

            chunk.NodeMinX = Mathf.Max(0, chunk.CellMinX - 1);
            chunk.NodeMaxX = Mathf.Min(nx - 1, chunk.CellMaxX + 1);
            chunk.NodeMinZ = Mathf.Max(0, chunk.CellMinZ - 1);
            chunk.NodeMaxZ = Mathf.Min(nz - 1, chunk.CellMaxZ + 1);
            chunk.LnX = chunk.NodeMaxX - chunk.NodeMinX + 1;
            chunk.LnZ = chunk.NodeMaxZ - chunk.NodeMinZ + 1;

            int columns = chunk.LnX * chunk.LnZ;
            chunk.Field = new float[chunk.LnX * ny * chunk.LnZ];
            chunk.Bump = new float[chunk.Field.Length];
            chunk.BaseDensity = new float[columns];
            chunk.DisplayGrowth = new float[columns];
            chunk.MorphFrom = new float[columns];
            chunk.MorphTo = new float[columns];
            chunk.ColumnDirty = new bool[columns];
            chunk.CellVertex = new int[chunk.LnX * gy * chunk.LnZ];

            GameObject go = new GameObject(lod ? $"MeatChunkLod_{cx}_{cz}" : $"MeatChunk_{cx}_{cz}");
            go.transform.SetParent(chunkRoot, false);
            chunk.Go = go;
            chunk.Filter = go.AddComponent<MeshFilter>();
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = meatMaterial;
            chunk.Collider = go.AddComponent<MeshCollider>();
            chunk.Collider.cookingOptions = MeshColliderCookingOptions.UseFastMidphase;

            int pulse = CurrentPulse;
            if (isMorphing)
            {
                FillChunkDensity(chunk, chunk.MorphFrom, morphFromPulse);
                FillChunkDensity(chunk, chunk.MorphTo, morphToPulse);
                float t = SmoothStep01(0f, 1f, morphElapsed / Mathf.Max(0.1f, PulseMorphSeconds));
                float intro = IntroFactor();
                for (int c = 0; c < columns; c++)
                {
                    chunk.BaseDensity[c] = Mathf.Lerp(chunk.MorphFrom[c], chunk.MorphTo[c], t);
                    chunk.DisplayGrowth[c] = chunk.BaseDensity[c] * intro;
                    chunk.ColumnDirty[c] = true;
                }
            }
            else
            {
                FillChunkDensity(chunk, chunk.BaseDensity, pulse);
                float intro = IntroFactor();
                bool snapFull = !Application.isPlaying || intro >= 0.999f;
                for (int c = 0; c < columns; c++)
                {
                    float target = chunk.BaseDensity[c] * intro;
                    chunk.DisplayGrowth[c] = snapFull ? target : 0f;
                    chunk.ColumnDirty[c] = true;
                }
            }

            chunk.AnyColumnDirty = true;
            EnsureChunkBump(chunk, CurrentSeed);
            loaded[key] = chunk;
            SetChunkQuality(chunk, lod);
        }

        private void SetChunkQuality(StreamChunk chunk, bool lod)
        {
            chunk.IsLod = lod;
            chunk.MeshStride = lod ? 2 : 1;
            if (chunk.Go != null)
            {
                chunk.Go.name = lod ? $"MeatChunkLod_{chunk.Cx}_{chunk.Cz}" : $"MeatChunk_{chunk.Cx}_{chunk.Cz}";
            }

            if (chunk.Collider != null)
            {
                chunk.Collider.enabled = !lod;
                if (lod && chunk.HasCollisionMesh)
                {
                    chunk.Collider.sharedMesh = null;
                    chunk.HasCollisionMesh = false;
                }
            }

            for (int c = 0; c < chunk.ColumnDirty.Length; c++)
            {
                chunk.ColumnDirty[c] = true;
            }

            chunk.AnyColumnDirty = true;
            chunk.MeshDirty = true;
        }

        private void UnloadChunk(long key)
        {
            if (!loaded.TryGetValue(key, out StreamChunk chunk))
            {
                return;
            }

            loaded.Remove(key);
            if (chunk.Mesh != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(chunk.Mesh);
                }
                else
                {
                    DestroyImmediate(chunk.Mesh);
                }
            }

            if (chunk.Go != null)
            {
                DestroyObject(chunk.Go);
            }
        }

        private void UnloadAllChunks()
        {
            unloadBuffer.Clear();
            foreach (long key in loaded.Keys)
            {
                unloadBuffer.Add(key);
            }

            for (int i = 0; i < unloadBuffer.Count; i++)
            {
                UnloadChunk(unloadBuffer[i]);
            }
        }

        private void FillChunkDensity(StreamChunk chunk, float[] dst, int pulse)
        {
            float halfX = HalfX;
            float halfZ = HalfZ;
            int seed = CurrentSeed;
            for (int lk = 0; lk < chunk.LnZ; lk++)
            {
                float pz = -halfZ + (chunk.NodeMinZ + lk) * CellSize;
                for (int li = 0; li < chunk.LnX; li++)
                {
                    float px = -halfX + (chunk.NodeMinX + li) * CellSize;
                    dst[LCol(chunk, li, lk)] = EvaluateWallDensity(px, pz, seed, pulse);
                }
            }
        }

        private void EnsureChunkBump(StreamChunk chunk, int seed)
        {
            float halfX = HalfX;
            float halfZ = HalfZ;
            float s = BlobScale;
            float ox = seed * 0.0131f;
            float oz = seed * 0.0177f;

            for (int lk = 0; lk < chunk.LnZ; lk++)
            {
                float pz = -halfZ + (chunk.NodeMinZ + lk) * CellSize;
                for (int lj = 0; lj < ny; lj++)
                {
                    float py = yMin + lj * CellSize;
                    int rowBase = chunk.LnX * lj + chunk.LnX * ny * lk;
                    for (int li = 0; li < chunk.LnX; li++)
                    {
                        float px = -halfX + (chunk.NodeMinX + li) * CellSize;
                        float a = Mathf.PerlinNoise(px * s + ox, pz * s + oz);
                        float b = Mathf.PerlinNoise(py * s * 1.6f + 31.7f + ox, px * s * 1.1f + 11.3f);
                        float c = Mathf.PerlinNoise(pz * s * 1.2f + 5.1f, py * s * 1.4f + 17.9f + oz);
                        chunk.Bump[rowBase + li] = (a * 0.5f + b * 0.28f + c * 0.22f - 0.5f) * 2f;
                    }
                }
            }
        }

        // ---------------------------------------------------------------- field / mesh

        private void BuildFieldColumn(StreamChunk chunk, int li, int lk)
        {
            int c = LCol(chunk, li, lk);
            float g = chunk.DisplayGrowth[c] * chunk.Fade;
            float top = g * (WallHeight + CellSize) - CellSize;
            float amp = BlobAmplitude * SmoothStep01(0.06f, 0.42f, g);
            int slab = chunk.LnX * ny * lk;
            int wi = chunk.NodeMinX + li;
            int wk = chunk.NodeMinZ + lk;

            for (int j = 0; j < ny; j++)
            {
                int idx = slab + chunk.LnX * j + li;
                if (j == 0 || j == ny - 1)
                {
                    chunk.Field[idx] = -1f;
                    continue;
                }

                float y = yMin + j * CellSize;
                float solid = top - y + chunk.Bump[idx] * amp;
                float carve = GetBurn3D(wi, j, wk);
                if (carve > 0f)
                {
                    // Dig a real cavity through the shell instead of flattening the whole column.
                    solid -= carve * (WallHeight * BurnCarveDepth + CellSize);
                }

                chunk.Field[idx] = solid;
            }
        }

        private void FlushColumnUpdates()
        {
            foreach (StreamChunk chunk in loaded.Values)
            {
                if (!chunk.AnyColumnDirty)
                {
                    continue;
                }

                for (int c = 0; c < chunk.ColumnDirty.Length; c++)
                {
                    if (!chunk.ColumnDirty[c])
                    {
                        continue;
                    }

                    chunk.ColumnDirty[c] = false;
                    int li = c % chunk.LnX;
                    int lk = c / chunk.LnX;
                    BuildFieldColumn(chunk, li, lk);
                }

                chunk.AnyColumnDirty = false;
                chunk.MeshDirty = true;
            }
        }

        private void MarkWorldColumnDirty(int i, int k)
        {
            foreach (StreamChunk chunk in loaded.Values)
            {
                if (i < chunk.NodeMinX || i > chunk.NodeMaxX || k < chunk.NodeMinZ || k > chunk.NodeMaxZ)
                {
                    continue;
                }

                int li = i - chunk.NodeMinX;
                int lk = k - chunk.NodeMinZ;
                int c = LCol(chunk, li, lk);
                chunk.ColumnDirty[c] = true;
                chunk.AnyColumnDirty = true;
            }
        }

        private void RebuildDirtyChunks(int budget)
        {
            if (loaded.Count == 0)
            {
                return;
            }

            rebuildCursorKeys.Clear();
            foreach (long key in loaded.Keys)
            {
                rebuildCursorKeys.Add(key);
            }

            int built = 0;
            int count = rebuildCursorKeys.Count;
            for (int scanned = 0; scanned < count && built < Mathf.Max(1, budget); scanned++)
            {
                rebuildCursor = (rebuildCursor + 1) % count;
                StreamChunk chunk = loaded[rebuildCursorKeys[rebuildCursor]];
                if (!chunk.MeshDirty)
                {
                    continue;
                }

                BuildChunkMesh(chunk);
                chunk.MeshDirty = false;
                built++;
            }
        }

        private void RebuildAllLoadedChunks()
        {
            foreach (StreamChunk chunk in loaded.Values)
            {
                for (int lk = 0; lk < chunk.LnZ; lk++)
                {
                    for (int li = 0; li < chunk.LnX; li++)
                    {
                        BuildFieldColumn(chunk, li, lk);
                    }
                }

                BuildChunkMesh(chunk);
                chunk.MeshDirty = false;
                chunk.AnyColumnDirty = false;
                Array.Clear(chunk.ColumnDirty, 0, chunk.ColumnDirty.Length);
            }
        }

        private void BuildChunkMesh(StreamChunk chunk)
        {
            verts.Clear();
            normals.Clear();
            colors.Clear();
            tris.Clear();

            int vMinX = Mathf.Max(chunk.NodeMinX, chunk.CellMinX - 1) - chunk.NodeMinX;
            int vMinZ = Mathf.Max(chunk.NodeMinZ, chunk.CellMinZ - 1) - chunk.NodeMinZ;
            // Local cell indices usable for surface nets (need +1 node).
            int localCellMaxX = Mathf.Min(chunk.LnX - 2, chunk.CellMaxX - chunk.NodeMinX);
            int localCellMaxZ = Mathf.Min(chunk.LnZ - 2, chunk.CellMaxZ - chunk.NodeMinZ);

            for (int lk = 0; lk < chunk.LnZ - 1; lk++)
            {
                for (int j = 0; j < gy; j++)
                {
                    for (int li = 0; li < chunk.LnX - 1; li++)
                    {
                        chunk.CellVertex[LCIdx(chunk, li, j, lk)] = -1;
                    }
                }
            }

            float halfX = HalfX;
            float halfZ = HalfZ;

            for (int lk = vMinZ; lk <= localCellMaxZ; lk++)
            {
                for (int j = 0; j < gy; j++)
                {
                    for (int li = vMinX; li <= localCellMaxX; li++)
                    {
                        int mask = 0;
                        for (int c = 0; c < 8; c++)
                        {
                            float v = chunk.Field[LNIdx(chunk, li + CornerX[c], j + CornerY[c], lk + CornerZ[c])];
                            corner[c] = v;
                            if (v > 0f)
                            {
                                mask |= 1 << c;
                            }
                        }

                        if (mask == 0 || mask == 255)
                        {
                            continue;
                        }

                        float sx = 0f, sy = 0f, sz = 0f;
                        int crossings = 0;
                        for (int e = 0; e < 12; e++)
                        {
                            int a = EdgeA[e];
                            int b = EdgeB[e];
                            if (((mask >> a) & 1) == ((mask >> b) & 1))
                            {
                                continue;
                            }

                            float va = corner[a];
                            float t = Mathf.Clamp01(va / (va - corner[b]));
                            sx += CornerX[a] + (CornerX[b] - CornerX[a]) * t;
                            sy += CornerY[a] + (CornerY[b] - CornerY[a]) * t;
                            sz += CornerZ[a] + (CornerZ[b] - CornerZ[a]) * t;
                            crossings++;
                        }

                        if (crossings == 0)
                        {
                            continue;
                        }

                        float inv = 1f / crossings;
                        int wi = chunk.NodeMinX + li;
                        int wk = chunk.NodeMinZ + lk;
                        Vector3 pos = new Vector3(
                            -halfX + (wi + sx * inv) * CellSize,
                            yMin + (j + sy * inv) * CellSize,
                            -halfZ + (wk + sz * inv) * CellSize);

                        Vector3 gradient = new Vector3(
                            corner[1] + corner[2] + corner[5] + corner[6] - corner[0] - corner[3] - corner[4] - corner[7],
                            corner[4] + corner[5] + corner[6] + corner[7] - corner[0] - corner[1] - corner[2] - corner[3],
                            corner[2] + corner[3] + corner[6] + corner[7] - corner[0] - corner[1] - corner[4] - corner[5]);
                        Vector3 normal = gradient.sqrMagnitude > 1e-8f ? -gradient.normalized : Vector3.up;

                        chunk.CellVertex[LCIdx(chunk, li, j, lk)] = verts.Count;
                        verts.Add(pos);
                        normals.Add(normal);
                        colors.Add(new Color(SampleBurnWorld(pos.x, pos.y, pos.z), 0f, 0f, 1f));
                    }
                }
            }

            int emitMinX = chunk.CellMinX - chunk.NodeMinX;
            int emitMinZ = chunk.CellMinZ - chunk.NodeMinZ;
            int emitMaxX = Mathf.Min(localCellMaxX, chunk.CellMaxX - chunk.NodeMinX);
            int emitMaxZ = Mathf.Min(localCellMaxZ, chunk.CellMaxZ - chunk.NodeMinZ);

            if (verts.Count > 0)
            {
                for (int lk = emitMinZ; lk <= emitMaxZ; lk++)
                {
                    for (int j = 0; j < gy; j++)
                    {
                        for (int li = emitMinX; li <= emitMaxX; li++)
                        {
                            float v0 = chunk.Field[LNIdx(chunk, li, j, lk)];
                            bool in0 = v0 > 0f;

                            if (j >= 1 && lk >= 1)
                            {
                                float v1 = chunk.Field[LNIdx(chunk, li + 1, j, lk)];
                                if (in0 != (v1 > 0f))
                                {
                                    EmitQuad(chunk,
                                        LCIdx(chunk, li, j - 1, lk - 1), LCIdx(chunk, li, j, lk - 1),
                                        LCIdx(chunk, li, j, lk), LCIdx(chunk, li, j - 1, lk),
                                        v1 > v0 ? Vector3.left : Vector3.right);
                                }
                            }

                            if (li >= 1 && lk >= 1)
                            {
                                float v1 = chunk.Field[LNIdx(chunk, li, j + 1, lk)];
                                if (in0 != (v1 > 0f))
                                {
                                    EmitQuad(chunk,
                                        LCIdx(chunk, li - 1, j, lk - 1), LCIdx(chunk, li, j, lk - 1),
                                        LCIdx(chunk, li, j, lk), LCIdx(chunk, li - 1, j, lk),
                                        v1 > v0 ? Vector3.down : Vector3.up);
                                }
                            }

                            if (li >= 1 && j >= 1)
                            {
                                float v1 = chunk.Field[LNIdx(chunk, li, j, lk + 1)];
                                if (in0 != (v1 > 0f))
                                {
                                    EmitQuad(chunk,
                                        LCIdx(chunk, li - 1, j - 1, lk), LCIdx(chunk, li, j - 1, lk),
                                        LCIdx(chunk, li, j, lk), LCIdx(chunk, li - 1, j, lk),
                                        v1 > v0 ? Vector3.back : Vector3.forward);
                                }
                            }
                        }
                    }
                }
            }

            UploadChunk(chunk);
        }

        private void EmitQuad(StreamChunk chunk, int c0, int c1, int c2, int c3, Vector3 outward)
        {
            int a = chunk.CellVertex[c0];
            int b = chunk.CellVertex[c1];
            int c = chunk.CellVertex[c2];
            int d = chunk.CellVertex[c3];
            if (a < 0 || b < 0 || c < 0 || d < 0)
            {
                return;
            }

            Vector3 pa = verts[a];
            if (Vector3.Dot(Vector3.Cross(verts[b] - pa, verts[c] - pa), outward) < 0f)
            {
                int swap = b;
                b = d;
                d = swap;
            }

            tris.Add(a);
            tris.Add(b);
            tris.Add(c);
            tris.Add(a);
            tris.Add(c);
            tris.Add(d);
        }

        private void UploadChunk(StreamChunk chunk)
        {
            if (chunk.Mesh == null)
            {
                chunk.Mesh = new Mesh { name = "MeatChunk" };
                chunk.Mesh.MarkDynamic();
                chunk.Mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                chunk.Filter.sharedMesh = chunk.Mesh;
            }

            chunk.Mesh.Clear();

            if (tris.Count == 0)
            {
                if (chunk.HasCollisionMesh)
                {
                    chunk.Collider.sharedMesh = null;
                    chunk.HasCollisionMesh = false;
                }

                return;
            }

            chunk.Mesh.SetVertices(verts);
            chunk.Mesh.SetNormals(normals);
            chunk.Mesh.SetColors(colors);
            chunk.Mesh.SetTriangles(tris, 0, true);

            if (chunk.IsLod)
            {
                if (chunk.HasCollisionMesh)
                {
                    chunk.Collider.sharedMesh = null;
                    chunk.HasCollisionMesh = false;
                }

                chunk.Collider.enabled = false;
                return;
            }

            chunk.Collider.enabled = true;
            chunk.Collider.sharedMesh = null;
            chunk.Collider.sharedMesh = chunk.Mesh;
            chunk.HasCollisionMesh = true;
        }

        /// <summary>
        /// Occupancy sample for burning: uses loaded field when present, otherwise procedural density.
        /// </summary>
        private float SampleOccupancyLocal(Vector3 local, float intro)
        {
            if (TrySampleLoadedField(local, out float fieldValue))
            {
                return fieldValue;
            }

            float density = SampleDensityWorld(local.x, local.z, CurrentPulse) * intro;
            if (density <= 0.01f)
            {
                return -1f;
            }

            float top = density * (WallHeight + CellSize) - CellSize;
            return top - local.y;
        }

        private bool TrySampleLoadedField(Vector3 local, out float value)
        {
            value = -1f;
            float fx = (local.x + HalfX) / CellSize;
            float fy = (local.y - yMin) / CellSize;
            float fz = (local.z + HalfZ) / CellSize;
            int i0 = Mathf.FloorToInt(fx);
            int j0 = Mathf.FloorToInt(fy);
            int k0 = Mathf.FloorToInt(fz);
            if (j0 < 0 || j0 >= ny - 1)
            {
                return false;
            }

            StreamChunk chunk = FindChunkContainingNode(i0, k0);
            if (chunk == null)
            {
                return false;
            }

            int li = i0 - chunk.NodeMinX;
            int lk = k0 - chunk.NodeMinZ;
            if (li < 0 || lk < 0 || li >= chunk.LnX - 1 || lk >= chunk.LnZ - 1)
            {
                return false;
            }

            float tx = Mathf.Clamp01(fx - i0);
            float ty = Mathf.Clamp01(fy - j0);
            float tz = Mathf.Clamp01(fz - k0);

            float x00 = Mathf.Lerp(chunk.Field[LNIdx(chunk, li, j0, lk)], chunk.Field[LNIdx(chunk, li + 1, j0, lk)], tx);
            float x10 = Mathf.Lerp(chunk.Field[LNIdx(chunk, li, j0 + 1, lk)], chunk.Field[LNIdx(chunk, li + 1, j0 + 1, lk)], tx);
            float x01 = Mathf.Lerp(chunk.Field[LNIdx(chunk, li, j0, lk + 1)], chunk.Field[LNIdx(chunk, li + 1, j0, lk + 1)], tx);
            float x11 = Mathf.Lerp(chunk.Field[LNIdx(chunk, li, j0 + 1, lk + 1)], chunk.Field[LNIdx(chunk, li + 1, j0 + 1, lk + 1)], tx);
            value = Mathf.Lerp(Mathf.Lerp(x00, x10, ty), Mathf.Lerp(x01, x11, ty), tz);
            return true;
        }

        private StreamChunk FindChunkContainingNode(int i, int k)
        {
            int cx = Mathf.Clamp(i / chunkStride, 0, chunksX - 1);
            int cz = Mathf.Clamp(k / chunkStride, 0, chunksZ - 1);
            if (loaded.TryGetValue(PackChunk(cx, cz), out StreamChunk chunk))
            {
                return chunk;
            }

            // Border nodes may belong to neighbor chunk halo.
            for (int dz = -1; dz <= 1; dz++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int ncx = cx + dx;
                    int ncz = cz + dz;
                    if (ncx < 0 || ncz < 0 || ncx >= chunksX || ncz >= chunksZ)
                    {
                        continue;
                    }

                    if (loaded.TryGetValue(PackChunk(ncx, ncz), out StreamChunk other) &&
                        i >= other.NodeMinX && i <= other.NodeMaxX &&
                        k >= other.NodeMinZ && k <= other.NodeMaxZ)
                    {
                        return other;
                    }
                }
            }

            return null;
        }

        // ---------------------------------------------------------------- pulse & network

        private void ApplyPulse()
        {
            if (IsSpawned && IsServer)
            {
                networkPulse.Value += 1;
            }
            else if (!IsSpawned)
            {
                localPulse++;
                BeginPatternMorph(CurrentSeed, localPulse);
            }

            ClearBurnsLocal();
        }

        private void ScheduleNextPulse()
        {
            nextPulseTime = Time.time + Mathf.Max(1f, PulseIntervalSeconds);
        }

        private void HandleSeedOrPulseChanged(int previous, int next)
        {
            BeginPatternMorph(CurrentSeed, CurrentPulse);
            ClearBurnsLocal();
        }

        private void HandleClientConnected(ulong clientId)
        {
            if (!IsServer || clientId == NetworkManager.ServerClientId || burns.Count == 0)
            {
                return;
            }

            int[] ix = new int[burns.Count];
            int[] iy = new int[burns.Count];
            int[] iz = new int[burns.Count];
            byte[] amounts = new byte[burns.Count];
            int n = 0;
            foreach (KeyValuePair<long, BurnCell> pair in burns)
            {
                UnpackVoxel(pair.Key, out int i, out int j, out int k);
                ix[n] = i;
                iy[n] = j;
                iz[n] = k;
                amounts[n] = (byte)Mathf.RoundToInt(Mathf.Clamp01(pair.Value.Amount * 0.5f) * 255f);
                n++;
            }

            BurnSnapshotClientRpc(ix, iy, iz, amounts, new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } }
            });
        }

        [ClientRpc]
        private void BurnStrokeClientRpc(Vector3 localCenter, float radius, float strength)
        {
            if (IsServer)
            {
                return;
            }

            PaintBurn(localCenter, radius, strength);
        }

        [ClientRpc]
        private void BurnSnapshotClientRpc(int[] ix, int[] iy, int[] iz, byte[] amounts, ClientRpcParams rpcParams = default)
        {
            EnsureLattice();
            if (ix == null || iy == null || iz == null || amounts == null ||
                ix.Length != amounts.Length || iy.Length != amounts.Length || iz.Length != amounts.Length)
            {
                return;
            }

            burns.Clear();
            float now = Time.time;
            for (int n = 0; n < amounts.Length; n++)
            {
                float amount = (amounts[n] / 255f) * 2f;
                if (amount <= 0.001f)
                {
                    continue;
                }

                int i = Mathf.Clamp(ix[n], 0, nx - 1);
                int j = Mathf.Clamp(iy[n], 0, ny - 1);
                int k = Mathf.Clamp(iz[n], 0, nz - 1);
                burns[PackVoxel(i, j, k)] = new BurnCell { Amount = amount, Expire = now + BurnHoldSeconds };
                MarkWorldColumnDirty(i, k);
            }
        }

        private void ClearBurnsLocal()
        {
            if (burns.Count == 0)
            {
                return;
            }

            burnKeyBuffer.Clear();
            foreach (long key in burns.Keys)
            {
                burnKeyBuffer.Add(key);
            }

            burns.Clear();
            for (int i = 0; i < burnKeyBuffer.Count; i++)
            {
                UnpackVoxel(burnKeyBuffer[i], out int wi, out int wj, out int wk);
                MarkWorldColumnDirty(wi, wk);
            }
        }

        private void FlushStreaming()
        {
            // Burst-load the whole desired set (spawn / editor preview), still capped by MaxLoadedChunks.
            int guard = Mathf.Max(8, MaxLoadedChunks + 4);
            for (int i = 0; i < guard; i++)
            {
                int before = loaded.Count;
                int oldLoads = MaxChunkLoadsPerFrame;
                int oldUnloads = MaxChunkUnloadsPerFrame;
                MaxChunkLoadsPerFrame = Mathf.Max(MaxChunkLoadsPerFrame, 64);
                MaxChunkUnloadsPerFrame = Mathf.Max(MaxChunkUnloadsPerFrame, 64);
                TickStreaming();
                MaxChunkLoadsPerFrame = oldLoads;
                MaxChunkUnloadsPerFrame = oldUnloads;
                if (loaded.Count == before)
                {
                    break;
                }
            }
        }

        private void ResetGrowthAndRebuild()
        {
            EnsureRuntimeObjects();
            EnsureLattice();
            burns.Clear();
            spawnTime = Time.time;
            UnloadAllChunks();
            SnapPattern(CurrentSeed, CurrentPulse);
            FlushStreaming();
            RebuildAllLoadedChunks();
        }

        // ---------------------------------------------------------------- setup

        private void EnsureLattice()
        {
            int wantGx = GridCellsX;
            int wantGz = GridCellsZ;
            int wantGy = GridCellsVertical;
            int stride = Mathf.Max(2, ChunkCells);
            int wantChunksX = Mathf.CeilToInt(wantGx / (float)stride);
            int wantChunksZ = Mathf.CeilToInt(wantGz / (float)stride);

            if (gridsReady && wantGx == gx && wantGy == gy && wantGz == gz &&
                stride == chunkStride && wantChunksX == chunksX && wantChunksZ == chunksZ)
            {
                return;
            }

            UnloadAllChunks();

            gx = wantGx;
            gz = wantGz;
            gy = wantGy;
            nx = gx + 1;
            ny = gy + 1;
            nz = gz + 1;
            yMin = -CellSize;
            chunkStride = stride;
            chunksX = wantChunksX;
            chunksZ = wantChunksZ;
            forceLoadAll = chunksX * chunksZ <= Mathf.Max(1, LoadAllBelowChunkCount);
            gridsReady = true;
            cachedSeed = int.MinValue;
            cachedPulse = int.MinValue;
            isMorphing = false;
        }

        private void EnsureChunkRoot()
        {
            if (chunkRoot != null)
            {
                return;
            }

            Transform existing = transform.Find("MeatChunks");
            if (existing != null)
            {
                chunkRoot = existing;
            }
            else
            {
                GameObject go = new GameObject("MeatChunks");
                chunkRoot = go.transform;
                chunkRoot.SetParent(transform, false);
            }

            chunkRoot.localPosition = Vector3.zero;
            chunkRoot.localRotation = Quaternion.identity;
            chunkRoot.localScale = Vector3.one;
        }

        private void EnsureRuntimeObjects()
        {
            EnsureChunkRoot();
            EnsureMeatMaterial();
            DropLegacyChild("MeatMesh");
            DropLegacyChild("Walls");
            EnsureGround();
        }

        private void DropLegacyChild(string childName)
        {
            Transform stale = transform.Find(childName);
            if (stale != null)
            {
                DestroyObject(stale.gameObject);
            }
        }

        private void EnsureGround()
        {
            Transform existing = transform.Find("MeatGround");
            if (!ShowGroundPatch)
            {
                if (existing != null)
                {
                    DestroyObject(existing.gameObject);
                }

                return;
            }

            // Huge ground cubes for 2000m zones are pointless; skip visual plate above 400m.
            if (ZoneSizeX > 400f || ZoneSizeY > 400f)
            {
                if (existing != null)
                {
                    DestroyObject(existing.gameObject);
                }

                return;
            }

            if (existing != null)
            {
                existing.localScale = new Vector3(ZoneSizeX, 0.12f, ZoneSizeY);
                return;
            }

            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "MeatGround";
            Transform groundRoot = ground.transform;
            groundRoot.SetParent(transform, false);
            groundRoot.localPosition = new Vector3(0f, -0.06f, 0f);
            groundRoot.localScale = new Vector3(ZoneSizeX, 0.12f, ZoneSizeY);

            Collider col = ground.GetComponent<Collider>();
            if (col != null)
            {
                DestroyComponent(col);
            }

            Renderer renderer = ground.GetComponent<Renderer>();
            if (renderer != null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                {
                    shader = Shader.Find("Standard");
                }

                Color groundColor = new Color(0.11f, 0.03f, 0.032f, 1f);
                Material gmat = new Material(shader) { name = "MeatGroundRuntime", color = groundColor };
                if (gmat.HasProperty("_BaseColor"))
                {
                    gmat.SetColor("_BaseColor", groundColor);
                }

                renderer.sharedMaterial = gmat;
            }
        }

        private void EnsureMeatMaterial()
        {
            if (meatMaterial != null)
            {
                return;
            }

            if (MeatMaterial != null)
            {
                // Play mode gets an instance so MotionDistort does not dirty the project asset.
                meatMaterial = Application.isPlaying ? new Material(MeatMaterial) : MeatMaterial;
            }
            else
            {
                Shader shader = Shader.Find("MiniVanGame/MeatMazeOrganic");
                if (shader == null)
                {
                    shader = Shader.Find("Universal Render Pipeline/Lit");
                }

                if (shader == null)
                {
                    shader = Shader.Find("Standard");
                }

                Color meat = new Color(0.46f, 0.11f, 0.1f, 1f);
                meatMaterial = new Material(shader) { name = "MeatMazeRuntime", color = meat };
                if (meatMaterial.HasProperty("_BaseColor"))
                {
                    meatMaterial.SetColor("_BaseColor", meat);
                }
            }

            ApplyVisualMaterialParams();
        }

        private void ApplyVisualMaterialParams()
        {
            if (meatMaterial == null)
            {
                return;
            }

            if (meatMaterial.HasProperty("_BreathAmount"))
            {
                meatMaterial.SetFloat("_BreathAmount", MotionDistort);
            }

            if (meatMaterial.HasProperty("_BreathSpeed"))
            {
                meatMaterial.SetFloat("_BreathSpeed", MotionSpeed);
            }
        }

        private static void DestroyComponent(Component component)
        {
            if (Application.isPlaying)
            {
                Destroy(component);
            }
            else
            {
                DestroyImmediate(component);
            }
        }

        private static void DestroyObject(GameObject go)
        {
            if (Application.isPlaying)
            {
                Destroy(go);
            }
            else
            {
                DestroyImmediate(go);
            }
        }

        // ---------------------------------------------------------------- helpers

        private float IntroFactor() =>
            Mathf.Clamp01((Time.time - spawnTime) / Mathf.Max(0.1f, InitialGrowSeconds));

        private bool InsideLocalBounds(Vector3 local)
        {
            return local.x >= -HalfX && local.x <= HalfX &&
                   local.z >= -HalfZ && local.z <= HalfZ &&
                   local.y >= yMin && local.y <= yMin + gy * CellSize;
        }

        private float SampleNoise(float px, float pz, int seed, int pulse)
        {
            float angular = Mathf.Clamp01(NoiseAngular);
            float organic = SampleOrganicNoise(px, pz, seed, pulse);
            if (angular <= 0.001f)
            {
                return organic;
            }

            float square = SampleSquareNoise(px, pz, seed, pulse);
            return Mathf.Clamp01(Mathf.Lerp(organic, square, angular));
        }

        private float SampleOrganicNoise(float px, float pz, int seed, int pulse)
        {
            float scaleX = Mathf.Max(0.005f, NoiseScaleX);
            float scaleY = Mathf.Max(0.005f, NoiseScaleY);
            float ox = seed * 0.013f + pulse * 0.09f;
            float oz = seed * 0.017f - pulse * 0.06f;
            float x = px * scaleX + ox;
            float z = pz * scaleY + oz;

            float warpAmt = 0.9f * (1f - Mathf.Clamp01(NoiseAngular));
            float warp = Mathf.PerlinNoise(x * 1.7f + 19.1f, z * 1.7f + 7.3f) - 0.5f;
            float n = Mathf.PerlinNoise(x + warp * warpAmt, z - warp * warpAmt * 0.85f);
            float detail = Mathf.PerlinNoise(x * 2.3f + 41.7f, z * 2.3f + 13.9f);
            float ridge = 1f - Mathf.Abs(n * 2f - 1f);
            return Mathf.Clamp01(Mathf.Lerp(n, ridge, 0.45f) * 0.82f + detail * 0.18f);
        }

        private float SampleSquareNoise(float px, float pz, int seed, int pulse)
        {
            float scaleX = Mathf.Max(0.005f, NoiseScaleX);
            float scaleY = Mathf.Max(0.005f, NoiseScaleY);
            float ox = seed * 0.013f + pulse * 0.09f;
            float oz = seed * 0.017f - pulse * 0.06f;
            float x = px * scaleX + ox;
            float z = pz * scaleY + oz;

            float cellX = Mathf.Floor(x);
            float cellZ = Mathf.Floor(z);
            float fx = x - cellX;
            float fz = z - cellZ;

            float jx = 0.28f + 0.44f * Hash01(cellX, cellZ, seed * 0.31f + 1.7f);
            float jz = 0.28f + 0.44f * Hash01(cellX, cellZ, seed * 0.47f + 9.1f);

            float cheby = Mathf.Max(Mathf.Abs(fx - jx), Mathf.Abs(fz - jz));
            float gate = Hash01(cellX + 17f, cellZ - 9f, seed * 0.19f + pulse * 0.07f);
            return Mathf.Clamp01(cheby * 1.55f * Mathf.Lerp(0.72f, 1.18f, gate));
        }

        private static float Hash01(float ix, float iz, float salt)
        {
            float n = Mathf.Sin(ix * 127.1f + iz * 311.7f + salt * 74.7f) * 43758.5453f;
            return n - Mathf.Floor(n);
        }

        private static float SmoothStep01(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge0) / Mathf.Max(1e-5f, edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        private static int LCol(StreamChunk chunk, int li, int lk) => li + chunk.LnX * lk;

        private int LNIdx(StreamChunk chunk, int li, int j, int lk) =>
            li + chunk.LnX * j + chunk.LnX * ny * lk;

        private int LCIdx(StreamChunk chunk, int li, int j, int lk) =>
            li + chunk.LnX * j + chunk.LnX * gy * lk;

        private static long PackChunk(int cx, int cz) => ((long)cx << 32) ^ (uint)cz;

        private static void UnpackChunk(long key, out int cx, out int cz)
        {
            cx = (int)(key >> 32);
            cz = (int)(key & 0xffffffff);
        }

        private static long PackVoxel(int i, int j, int k) =>
            ((long)(i & 0xFFFFF) << 40) | ((long)(j & 0xFF) << 32) | (uint)(k & 0xFFFFF);

        private static void UnpackVoxel(long key, out int i, out int j, out int k)
        {
            i = (int)((key >> 40) & 0xFFFFF);
            j = (int)((key >> 32) & 0xFF);
            k = (int)(key & 0xFFFFF);
        }

        private bool IsServerAuthority()
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            {
                return true;
            }

            return IsServer;
        }

        private void OnValidate()
        {
            if (meatMaterial != null)
            {
                ApplyVisualMaterialParams();
            }
        }

        private void OnDestroy()
        {
            UnloadAllChunks();
            if (meatMaterial != null && meatMaterial != MeatMaterial)
            {
                Destroy(meatMaterial);
            }
        }

#if UNITY_EDITOR
        [ContextMenu("Rebuild Preview")]
        public void EditorRebuildPreview()
        {
            EnsureRuntimeObjects();
            EnsureLattice();
            burns.Clear();
            spawnTime = Time.time - InitialGrowSeconds;
            UnloadAllChunks();
            SnapPattern(CurrentSeed, CurrentPulse);
            forceLoadAll = chunksX * chunksZ <= Mathf.Max(1, LoadAllBelowChunkCount) ||
                           Mathf.Max(ZoneSizeX, ZoneSizeY) <= LodRadius * 2f;
            FlushStreaming();

            foreach (StreamChunk chunk in loaded.Values)
            {
                for (int c = 0; c < chunk.DisplayGrowth.Length; c++)
                {
                    chunk.DisplayGrowth[c] = chunk.BaseDensity[c];
                }
            }

            RebuildAllLoadedChunks();
        }

        [ContextMenu("Clear Preview")]
        public void EditorClearPreview()
        {
            UnloadAllChunks();
            gridsReady = false;
        }

        public string EditorPreviewStats()
        {
            int vertexCount = 0;
            int triangleCount = 0;
            float sum = 0f;
            int columns = 0;
            foreach (StreamChunk chunk in loaded.Values)
            {
                columns += chunk.BaseDensity.Length;
                for (int i = 0; i < chunk.BaseDensity.Length; i++)
                {
                    sum += chunk.BaseDensity[i];
                }

                if (chunk.Mesh != null)
                {
                    vertexCount += chunk.Mesh.vertexCount;
                    triangleCount += (int)(chunk.Mesh.GetIndexCount(0) / 3);
                }
            }

            return string.Format(
                "cells {0}x{1}x{2}, chunkGrid {3}x{4}, loaded {5}/{6}, columns {7}, avg density {8:F3}, verts {9}, tris {10}",
                gx, gy, gz, chunksX, chunksZ, loaded.Count, chunksX * chunksZ, columns,
                columns > 0 ? sum / columns : 0f, vertexCount, triangleCount);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.8f);
            Gizmos.DrawWireCube(Vector3.up * (WallHeight * 0.5f), new Vector3(ZoneSizeX, WallHeight, ZoneSizeY));
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.35f);
            Gizmos.DrawWireSphere(Vector3.zero, StreamRadius);
            Gizmos.color = new Color(0.95f, 0.75f, 0.2f, 0.25f);
            Gizmos.DrawWireSphere(Vector3.zero, LodRadius);
        }
#endif
    }
}
