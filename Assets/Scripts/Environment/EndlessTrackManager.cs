using UnityEngine;
using CyberRunner.Collectibles;
using CyberRunner.Core;
using CyberRunner.Obstacles;

namespace CyberRunner.Environment
{
    /// <summary>
    /// Phase 2 modular pooled endless highway generator with mathematically fair obstacle
    /// and energy-coin pattern spawning.
    /// Guarantees that every spawned row preserves at least one fair, reachable path
    /// (open lane, jumpable barrier, or slide-under laser gate) and zero runtime Destroy calls.
    /// </summary>
    public sealed class EndlessTrackManager : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private GameConfig config;
        [SerializeField] private Transform playerTransform;
        [SerializeField] private ObstaclePoolManager obstaclePool;
        [SerializeField] private CoinPoolManager coinPool;

        [Header("Shared Mobile URP Materials")]
        [SerializeField] private Material roadDarkMaterial;
        [SerializeField] private Material neonCyanMaterial;
        [SerializeField] private Material neonPurpleMaterial;
        [SerializeField] private Material buildingDarkMaterial;

        private Transform[] segments;
        private float segmentLength;
        private float laneWidth;
        private float roadWidth;
        private float furthestSegmentZ;
        private int totalSegmentsSpawned;
        private int lastSafeLane;
        private float lastObstacleEndZ;
        private bool built;

        public void Configure(
            GameConfig gameConfig,
            Transform player,
            ObstaclePoolManager obstacles,
            CoinPoolManager coins,
            Material roadDark,
            Material neonCyan,
            Material neonPurple,
            Material buildingDark)
        {
            config = gameConfig;
            playerTransform = player;
            obstaclePool = obstacles;
            coinPool = coins;
            roadDarkMaterial = roadDark;
            neonCyanMaterial = neonCyan;
            neonPurpleMaterial = neonPurple;
            buildingDarkMaterial = buildingDark;
        }

        private void Awake()
        {
            if (config == null)
            {
                config = Resources.Load<GameConfig>("GameConfig") ?? GameConfig.CreateRuntimeDefault();
            }

            segmentLength = config.trackSegmentLength;
            laneWidth = config.laneWidth;
            // Road width comfortably spans 3 lanes (3 * 2.9 = 8.7m) plus outer neon shoulders.
            roadWidth = (laneWidth * 3f) + 1.1f;

            EnsureMaterials();
            BuildSegmentPoolIfNeeded();
        }

        /// <summary>
        /// Resets the endless track, clears all pooled obstacles and coins, and seeds the safe intro runway.
        /// </summary>
        public void ResetTrack()
        {
            BuildSegmentPoolIfNeeded();

            if (obstaclePool != null)
            {
                obstaclePool.ReturnAll();
            }

            if (coinPool != null)
            {
                coinPool.ReturnAll();
            }

            totalSegmentsSpawned = 0;
            lastSafeLane = 0;
            lastObstacleEndZ = 0f;

            for (int i = 0; i < segments.Length; i++)
            {
                float z = i * segmentLength;
                segments[i].position = new Vector3(0f, 0f, z);
                furthestSegmentZ = z;
                PopulateSegment(z, totalSegmentsSpawned, config.baseRunSpeed);
                totalSegmentsSpawned++;
            }
        }

        /// <summary>
        /// Recycles passed highway segments ahead of the player and populates them with fair patterns.
        /// </summary>
        public void TickTrack(float playerZ, float currentForwardSpeed)
        {
            if (!built)
            {
                return;
            }

            float recycleThresholdZ = playerZ - (segmentLength * 1.15f);
            for (int i = 0; i < segments.Length; i++)
            {
                Transform seg = segments[i];
                if (seg.position.z < recycleThresholdZ)
                {
                    furthestSegmentZ += segmentLength;
                    seg.position = new Vector3(0f, 0f, furthestSegmentZ);
                    PopulateSegment(furthestSegmentZ, totalSegmentsSpawned, currentForwardSpeed);
                    totalSegmentsSpawned++;
                }
            }
        }

        private void PopulateSegment(float segmentStartZ, int segmentIndex, float forwardSpeed)
        {
            if (obstaclePool == null || coinPool == null)
            {
                return;
            }

            // First segments are obstacle-free intro runway with inviting Energy Coin lines.
            if (segmentIndex < config.safeIntroSegments)
            {
                if (segmentIndex == 0)
                {
                    coinPool.SpawnLine(0, laneWidth, segmentStartZ + 18f, count: 5, spacing: 2.5f);
                }
                else
                {
                    int introLane = (segmentIndex % 2 == 1) ? -1 : 1;
                    coinPool.SpawnLine(introLane, laneWidth, segmentStartZ + 12f, count: 6, spacing: 2.5f);
                    lastSafeLane = introLane;
                }
                return;
            }

            // Compute fair spacing scaled with speed so every lane transition, jump, or slide is comfortable.
            float minRowSpacing = Mathf.Clamp(forwardSpeed * 1.05f, 16f, 24f);
            float cursorZ = Mathf.Max(segmentStartZ + 7f, lastObstacleEndZ + minRowSpacing);
            float segmentEndMargin = segmentStartZ + segmentLength - 6f;

            // Progressively unlock richer patterns as the player travels farther.
            int maxPatternPool = segmentIndex < 4 ? 4 : (segmentIndex < 8 ? 6 : 8);

            while (cursorZ <= segmentEndMargin)
            {
                int patternId = Random.Range(0, maxPatternPool);
                float extraClearance = SpawnFairPattern(patternId, cursorZ);
                lastObstacleEndZ = cursorZ + extraClearance;
                cursorZ = lastObstacleEndZ + minRowSpacing;
            }
        }

        /// <summary>
        /// Spawns one mathematically fair obstacle & coin row, updating lastSafeLane.
        /// Returns any extra forward Z clearance required (e.g., for long moving trains).
        /// </summary>
        private float SpawnFairPattern(int patternId, float rowZ)
        {
            int nextSafeLane = PickReachableSafeLane(lastSafeLane);

            switch (patternId)
            {
                case 0:
                    // Single Low Road Barrier with a parabolic coin arc right over it (2 open lanes).
                    obstaclePool.Spawn(ObstacleType.LowRoadBarrier, nextSafeLane, laneWidth, rowZ);
                    coinPool.SpawnJumpArc(nextSafeLane, laneWidth, rowZ);
                    lastSafeLane = nextSafeLane;
                    return 2f;

                case 1:
                    // Single Overhead Laser Gate with a low slide-coin trail (2 open lanes).
                    obstaclePool.Spawn(ObstacleType.LaserGate, nextSafeLane, laneWidth, rowZ);
                    coinPool.SpawnSlideTrail(nextSafeLane, laneWidth, rowZ);
                    lastSafeLane = nextSafeLane;
                    return 2f;

                case 2:
                    // Two Tall Security Monoliths + 1 guaranteed reachable Open Lane lined with coins.
                    for (int lane = -1; lane <= 1; lane++)
                    {
                        if (lane != nextSafeLane)
                        {
                            obstaclePool.Spawn(ObstacleType.TallCyberBlock, lane, laneWidth, rowZ);
                        }
                    }
                    coinPool.SpawnLine(nextSafeLane, laneWidth, rowZ - 4.5f, count: 5, spacing: 2.3f);
                    lastSafeLane = nextSafeLane;
                    return 3f;

                case 3:
                    // Barrier + Laser Gate + 1 guaranteed Open Lane.
                    int otherLaneA = WrapLane(nextSafeLane + 1);
                    int otherLaneB = WrapLane(nextSafeLane - 1);
                    obstaclePool.Spawn(ObstacleType.LowRoadBarrier, otherLaneA, laneWidth, rowZ);
                    obstaclePool.Spawn(ObstacleType.LaserGate, otherLaneB, laneWidth, rowZ);
                    coinPool.SpawnLine(nextSafeLane, laneWidth, rowZ - 3.5f, count: 4, spacing: 2.3f);
                    lastSafeLane = nextSafeLane;
                    return 3f;

                case 4:
                    // Triple Low Road Barriers -> Jumpable in ALL 3 lanes (coin arc guides current lane).
                    obstaclePool.Spawn(ObstacleType.LowRoadBarrier, -1, laneWidth, rowZ);
                    obstaclePool.Spawn(ObstacleType.LowRoadBarrier, 0, laneWidth, rowZ);
                    obstaclePool.Spawn(ObstacleType.LowRoadBarrier, 1, laneWidth, rowZ);
                    coinPool.SpawnJumpArc(lastSafeLane, laneWidth, rowZ);
                    return 3f;

                case 5:
                    // Triple Overhead Laser Gates -> Slideable in ALL 3 lanes (low coins guide current lane).
                    obstaclePool.Spawn(ObstacleType.LaserGate, -1, laneWidth, rowZ);
                    obstaclePool.Spawn(ObstacleType.LaserGate, 0, laneWidth, rowZ);
                    obstaclePool.Spawn(ObstacleType.LaserGate, 1, laneWidth, rowZ);
                    coinPool.SpawnSlideTrail(lastSafeLane, laneWidth, rowZ);
                    return 3f;

                case 6:
                    // Hover Security Drone patrol (1 drone + 1 low barrier + 1 guaranteed open lane).
                    int droneLane = WrapLane(nextSafeLane + 1);
                    int barrierLane = WrapLane(nextSafeLane - 1);
                    obstaclePool.Spawn(ObstacleType.HoverSecurityDrone, droneLane, laneWidth, rowZ);
                    obstaclePool.Spawn(ObstacleType.LowRoadBarrier, barrierLane, laneWidth, rowZ);
                    coinPool.SpawnLine(nextSafeLane, laneWidth, rowZ - 4f, count: 5, spacing: 2.2f);
                    lastSafeLane = nextSafeLane;
                    return 3f;

                default:
                    // Approaching Cyber Maglev Train in one non-safe lane + guaranteed open coin lane.
                    int trainLane = WrapLane(nextSafeLane + (Random.value < 0.5f ? 1 : -1));
                    obstaclePool.Spawn(ObstacleType.MovingCyberTrain, trainLane, laneWidth, rowZ + 6f);
                    coinPool.SpawnLine(nextSafeLane, laneWidth, rowZ - 2f, count: 6, spacing: 2.4f);
                    lastSafeLane = nextSafeLane;
                    return 12f;
            }
        }

        /// <summary>
        /// Picks a safe lane within at most 1 lane step of the previous safe lane so transitions
        /// are always physically comfortable even at high running speeds.
        /// </summary>
        private static int PickReachableSafeLane(int currentSafe)
        {
            int delta = Random.Range(-1, 2); // -1, 0, or +1
            return Mathf.Clamp(currentSafe + delta, -1, 1);
        }

        private static int WrapLane(int lane)
        {
            if (lane > 1) return -1;
            if (lane < -1) return 1;
            return lane;
        }

        private void EnsureMaterials()
        {
            roadDarkMaterial = roadDarkMaterial ?? Resources.Load<Material>("Materials/CyberRoad_Dark");
            neonCyanMaterial = neonCyanMaterial ?? Resources.Load<Material>("Materials/CyberNeon_Cyan");
            neonPurpleMaterial = neonPurpleMaterial ?? Resources.Load<Material>("Materials/CyberNeon_Purple");
            buildingDarkMaterial = buildingDarkMaterial ?? Resources.Load<Material>("Materials/CyberBuilding_Dark");

            Shader simpleLit = Shader.Find("Universal Render Pipeline/Simple Lit") ?? Shader.Find("Standard");
            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit") ?? simpleLit;

            roadDarkMaterial = roadDarkMaterial ?? CreateFallback(simpleLit, new Color(0.045f, 0.06f, 0.11f, 1f));
            buildingDarkMaterial = buildingDarkMaterial ?? CreateFallback(simpleLit, new Color(0.03f, 0.04f, 0.085f, 1f));
            neonCyanMaterial = neonCyanMaterial ?? CreateFallback(unlit, new Color(0.0f, 0.94f, 1.0f, 1f));
            neonPurpleMaterial = neonPurpleMaterial ?? CreateFallback(unlit, new Color(0.68f, 0.22f, 1.0f, 1f));
        }

        private static Material CreateFallback(Shader shader, Color color)
        {
            if (shader == null)
            {
                return null;
            }

            var mat = new Material(shader);
            mat.SetColor("_BaseColor", color);
            mat.enableInstancing = true;
            return mat;
        }

        private void BuildSegmentPoolIfNeeded()
        {
            if (built)
            {
                return;
            }

            int count = Mathf.Max(4, config.activeTrackSegments);
            segments = new Transform[count];

            for (int i = 0; i < count; i++)
            {
                segments[i] = CreateHighwaySegment(i, i * segmentLength);
            }

            built = true;
        }

        private Transform CreateHighwaySegment(int index, float zPos)
        {
            var root = new GameObject($"HighwaySegment_{index}");
            Transform tr = root.transform;
            tr.SetParent(transform, false);
            tr.position = new Vector3(0f, 0f, zPos);

            // Solid highway deck (top surface strictly at Y = 0.0m, BoxCollider kept for CharacterController).
            CreateMesh("RoadDeck", PrimitiveType.Cube, tr,
                new Vector3(0f, -0.5f, segmentLength * 0.5f),
                new Vector3(roadWidth, 1.0f, segmentLength),
                roadDarkMaterial,
                keepCollider: true);

            // Outer neon edge curbs.
            float edgeX = (roadWidth * 0.5f) - 0.16f;
            CreateMesh("LeftNeonCurb", PrimitiveType.Cube, tr,
                new Vector3(-edgeX, 0.05f, segmentLength * 0.5f),
                new Vector3(0.22f, 0.10f, segmentLength),
                neonCyanMaterial,
                keepCollider: false);

            CreateMesh("RightNeonCurb", PrimitiveType.Cube, tr,
                new Vector3(edgeX, 0.05f, segmentLength * 0.5f),
                new Vector3(0.22f, 0.10f, segmentLength),
                neonCyanMaterial,
                keepCollider: false);

            // Crisp 3-lane divider lines at exactly +/- (laneWidth * 0.5f).
            float dividerX = laneWidth * 0.5f;
            CreateMesh("LaneDividerLeft", PrimitiveType.Cube, tr,
                new Vector3(-dividerX, 0.015f, segmentLength * 0.5f),
                new Vector3(0.08f, 0.02f, segmentLength),
                neonPurpleMaterial,
                keepCollider: false);

            CreateMesh("LaneDividerRight", PrimitiveType.Cube, tr,
                new Vector3(dividerX, 0.015f, segmentLength * 0.5f),
                new Vector3(0.08f, 0.02f, segmentLength),
                neonPurpleMaterial,
                keepCollider: false);

            // Futuristic roadside light pylons and flanking neon city skyscraper silhouettes.
            float spacing = segmentLength * 0.5f;
            for (int p = 0; p < 2; p++)
            {
                float localZ = (p + 0.5f) * spacing;
                Material leftGlow = (p + index) % 2 == 0 ? neonCyanMaterial : neonPurpleMaterial;
                Material rightGlow = (p + index) % 2 == 0 ? neonPurpleMaterial : neonCyanMaterial;
                CreatePylon(tr, -edgeX - 0.65f, localZ, leftGlow);
                CreatePylon(tr, edgeX + 0.65f, localZ, rightGlow);
            }

            float leftHeight = 18f + ((index * 7) % 14);
            float rightHeight = 22f + ((index * 11) % 16);
            CreateSkyscraperSilhouette(tr, -13.8f, segmentLength * 0.35f, leftHeight, neonCyanMaterial);
            CreateSkyscraperSilhouette(tr, 13.8f, segmentLength * 0.72f, rightHeight, neonPurpleMaterial);

            return tr;
        }

        private void CreatePylon(Transform parent, float xPos, float zPos, Material glowMat)
        {
            CreateMesh("PylonPost", PrimitiveType.Cube, parent,
                new Vector3(xPos, 2.2f, zPos),
                new Vector3(0.32f, 4.4f, 0.32f),
                buildingDarkMaterial,
                keepCollider: false);

            CreateMesh("PylonGlow", PrimitiveType.Cube, parent,
                new Vector3(xPos, 3.8f, zPos),
                new Vector3(0.38f, 0.95f, 0.38f),
                glowMat,
                keepCollider: false);
        }

        private void CreateSkyscraperSilhouette(Transform parent, float xPos, float zPos, float height, Material trimMat)
        {
            CreateMesh("SkyscraperBody", PrimitiveType.Cube, parent,
                new Vector3(xPos, (height * 0.5f) - 2f, zPos),
                new Vector3(7.5f, height, 11f),
                buildingDarkMaterial,
                keepCollider: false);

            float trimX = xPos > 0f ? xPos - 3.78f : xPos + 3.78f;
            CreateMesh("SkyscraperNeonTrim", PrimitiveType.Cube, parent,
                new Vector3(trimX, height * 0.52f, zPos),
                new Vector3(0.14f, height * 0.65f, 0.35f),
                trimMat,
                keepCollider: false);
        }

        private static void CreateMesh(
            string name,
            PrimitiveType primitiveType,
            Transform parent,
            Vector3 localPos,
            Vector3 localScale,
            Material material,
            bool keepCollider)
        {
            GameObject go = GameObject.CreatePrimitive(primitiveType);
            go.name = name;

            if (!keepCollider)
            {
                Collider col = go.GetComponent<Collider>();
                if (col != null)
                {
                    Destroy(col);
                }
            }

            Transform tr = go.transform;
            tr.SetParent(parent, false);
            tr.localPosition = localPos;
            tr.localRotation = Quaternion.identity;
            tr.localScale = localScale;

            MeshRenderer renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null && material != null)
            {
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }
    }
}
