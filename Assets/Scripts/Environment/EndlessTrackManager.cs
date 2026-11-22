using System;
using UnityEngine;
using CyberRunner.Collectibles;
using CyberRunner.Core;
using CyberRunner.Obstacles;

namespace CyberRunner.Environment
{
    /// <summary>
    /// Phase 3 modular pooled endless track & 5-biome environment generator:
    /// Neon City -> Cyber Underground -> Sky Highway -> Industrial Zone -> Digital Core.
    /// Zero runtime Instantiate/Destroy; dynamically recycles segments, toggles biome prop groups,
    /// spawns fair jumpable Sky Highway Road Gaps, and guarantees reachable obstacle patterns.
    /// </summary>
    public sealed class EndlessTrackManager : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private GameConfig config;
        [SerializeField] private Transform playerTransform;
        [SerializeField] private ObstaclePoolManager obstaclePool;
        [SerializeField] private CoinPoolManager coinPool;

        [Header("Shared Mobile URP Materials")]
        [SerializeField] private Material roadNeonCityMaterial;
        [SerializeField] private Material roadUndergroundMaterial;
        [SerializeField] private Material roadSkyHighwayMaterial;
        [SerializeField] private Material roadIndustrialMaterial;
        [SerializeField] private Material roadDigitalCoreMaterial;
        [SerializeField] private Material buildingDarkMaterial;
        [SerializeField] private Material steelIndustrialMaterial;
        [SerializeField] private Material neonCyanMaterial;
        [SerializeField] private Material neonPurpleMaterial;
        [SerializeField] private Material neonMagentaMaterial;
        [SerializeField] private Material neonGreenMaterial;
        [SerializeField] private Material hazardRedMaterial;
        [SerializeField] private Material hazardAmberMaterial;

        public event Action<BiomeType> OnActiveBiomeChanged;

        public BiomeType StartingBiome { get; private set; } = BiomeType.NeonCity;
        public BiomeType ActiveBiome { get; private set; } = BiomeType.NeonCity;

        private BiomeTrackSegment[] segments;
        private BiomeMaterialPalette palette;
        private float segmentLength;
        private float laneWidth;
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
            BiomeMaterialPalette materialPalette)
        {
            config = gameConfig;
            playerTransform = player;
            obstaclePool = obstacles;
            coinPool = coins;

            roadNeonCityMaterial = materialPalette.RoadNeonCity;
            roadUndergroundMaterial = materialPalette.RoadUnderground;
            roadSkyHighwayMaterial = materialPalette.RoadSkyHighway;
            roadIndustrialMaterial = materialPalette.RoadIndustrial;
            roadDigitalCoreMaterial = materialPalette.RoadDigitalCore;
            buildingDarkMaterial = materialPalette.BuildingDark;
            steelIndustrialMaterial = materialPalette.SteelIndustrial;
            neonCyanMaterial = materialPalette.NeonCyan;
            neonPurpleMaterial = materialPalette.NeonPurple;
            neonMagentaMaterial = materialPalette.NeonMagenta;
            neonGreenMaterial = materialPalette.NeonGreen;
            hazardRedMaterial = materialPalette.HazardRed;
            hazardAmberMaterial = materialPalette.HazardAmber;
        }

        private void Awake()
        {
            if (config == null)
            {
                config = Resources.Load<GameConfig>("GameConfig") ?? GameConfig.CreateRuntimeDefault();
            }

            segmentLength = config.trackSegmentLength;
            laneWidth = config.laneWidth;

            BuildMaterialPalette();
            BuildSegmentPoolIfNeeded();
        }

        public void SetStartingBiome(BiomeType biome)
        {
            StartingBiome = biome;
        }

        public BiomeType CycleStartingBiome()
        {
            int next = ((int)StartingBiome + 1) % BiomeCatalog.BiomeCount;
            StartingBiome = (BiomeType)next;
            return StartingBiome;
        }

        /// <summary>
        /// Resets all 6 pooled track segments, obstacles, and coins starting in StartingBiome.
        /// </summary>
        public void ResetTrack()
        {
            BuildSegmentPoolIfNeeded();

            if (obstaclePool != null) obstaclePool.ReturnAll();
            if (coinPool != null) coinPool.ReturnAll();

            totalSegmentsSpawned = 0;
            lastSafeLane = 0;
            lastObstacleEndZ = 0f;
            ActiveBiome = StartingBiome;

            for (int i = 0; i < segments.Length; i++)
            {
                float z = i * segmentLength;
                BiomeType segBiome = GetBiomeForWorldZ(z);
                bool hasGap = ShouldSegmentHaveGap(segBiome, totalSegmentsSpawned);

                segments[i].transform.position = new Vector3(0f, 0f, z);
                segments[i].ConfigureForBiome(segBiome, hasGap);
                furthestSegmentZ = z;

                PopulateSegment(z, totalSegmentsSpawned, segBiome, hasGap, config.baseRunSpeed);
                totalSegmentsSpawned++;
            }

            OnActiveBiomeChanged?.Invoke(ActiveBiome);
        }

        /// <summary>
        /// Recycles passed segments ahead of the player, animates active ambient props,
        /// and emits OnActiveBiomeChanged the moment the runner crosses into a new sector.
        /// </summary>
        public void TickTrack(float playerZ, float currentForwardSpeed, float deltaTime)
        {
            if (!built)
            {
                return;
            }

            BiomeType playerBiome = GetBiomeForWorldZ(playerZ);
            if (playerBiome != ActiveBiome)
            {
                ActiveBiome = playerBiome;
                OnActiveBiomeChanged?.Invoke(ActiveBiome);
            }

            float recycleThresholdZ = playerZ - (segmentLength * 1.15f);
            for (int i = 0; i < segments.Length; i++)
            {
                BiomeTrackSegment seg = segments[i];
                if (seg.transform.position.z < recycleThresholdZ)
                {
                    furthestSegmentZ += segmentLength;
                    BiomeType nextBiome = GetBiomeForWorldZ(furthestSegmentZ);
                    bool hasGap = ShouldSegmentHaveGap(nextBiome, totalSegmentsSpawned);

                    seg.transform.position = new Vector3(0f, 0f, furthestSegmentZ);
                    seg.ConfigureForBiome(nextBiome, hasGap);

                    PopulateSegment(furthestSegmentZ, totalSegmentsSpawned, nextBiome, hasGap, currentForwardSpeed);
                    totalSegmentsSpawned++;
                }

                seg.TickAmbient(deltaTime);
            }
        }

        private BiomeType GetBiomeForWorldZ(float worldZ)
        {
            float interval = Mathf.Max(80f, config.biomeTransitionIntervalMeters);
            int steps = Mathf.Max(0, Mathf.FloorToInt(Mathf.Max(0f, worldZ) / interval));
            int biomeIndex = ((int)StartingBiome + steps) % BiomeCatalog.BiomeCount;
            return (BiomeType)biomeIndex;
        }

        private bool ShouldSegmentHaveGap(BiomeType biome, int segmentIndex)
        {
            if (segmentIndex < config.safeIntroSegments)
            {
                return false;
            }

            if (biome != BiomeType.SkyHighway)
            {
                return false;
            }

            return UnityEngine.Random.value < config.skyHighwayGapChance;
        }

        private void PopulateSegment(float segmentStartZ, int segmentIndex, BiomeType biome, bool hasRoadGap, float forwardSpeed)
        {
            if (obstaclePool == null || coinPool == null)
            {
                return;
            }

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

            // If this Sky Highway segment has an open Road Gap (local z = 17..23), keep takeoff/landing 100% clear
            // and place a rewarding parabolic Energy Coin arc right across the chasm!
            float gapWorldCenter = segmentStartZ + BiomeTrackSegment.GapCenterLocalZ;
            if (hasRoadGap)
            {
                coinPool.SpawnJumpArc(lastSafeLane, laneWidth, gapWorldCenter, count: 6, spanZ: 10.0f, baseHeightY: 1.0f, peakBoostY: 2.0f);
            }

            float minRowSpacing = Mathf.Clamp(forwardSpeed * 1.05f, 16f, 24f);
            float cursorZ = Mathf.Max(segmentStartZ + 7f, lastObstacleEndZ + minRowSpacing);
            float segmentEndMargin = segmentStartZ + segmentLength - 6f;

            while (cursorZ <= segmentEndMargin)
            {
                // Never spawn any obstacle near an open Road Gap takeoff or landing zone!
                if (hasRoadGap && Mathf.Abs(cursorZ - gapWorldCenter) < 12.5f)
                {
                    cursorZ = gapWorldCenter + 12.5f;
                    continue;
                }

                float extraClearance = SpawnBiomeFairPattern(biome, segmentIndex, cursorZ);
                lastObstacleEndZ = cursorZ + extraClearance;
                cursorZ = lastObstacleEndZ + minRowSpacing;
            }
        }

        /// <summary>
        /// Selects a fair, reachable obstacle/coin pattern tailored to the active futuristic biome.
        /// </summary>
        private float SpawnBiomeFairPattern(BiomeType biome, int segmentIndex, float rowZ)
        {
            int nextSafeLane = PickReachableSafeLane(lastSafeLane);
            int otherA = WrapLane(nextSafeLane + 1);
            int otherB = WrapLane(nextSafeLane - 1);
            int roll = UnityEngine.Random.Range(0, 100);

            switch (biome)
            {
                case BiomeType.CyberUnderground:
                    if (roll < 26)
                    {
                        // Approaching underground maglev train + open coin lane
                        obstaclePool.Spawn(ObstacleType.MovingCyberTrain, otherA, laneWidth, rowZ + 6f);
                        coinPool.SpawnLine(nextSafeLane, laneWidth, rowZ - 2f, count: 6, spacing: 2.3f);
                        lastSafeLane = nextSafeLane;
                        return 12f;
                    }
                    if (roll < 55)
                    {
                        // Triple underground laser trap (slide clearable in all lanes)
                        obstaclePool.Spawn(ObstacleType.LaserGate, -1, laneWidth, rowZ);
                        obstaclePool.Spawn(ObstacleType.LaserGate, 0, laneWidth, rowZ);
                        obstaclePool.Spawn(ObstacleType.LaserGate, 1, laneWidth, rowZ);
                        coinPool.SpawnSlideTrail(lastSafeLane, laneWidth, rowZ);
                        return 3f;
                    }
                    if (roll < 80)
                    {
                        // Security drone + laser gate + open coin lane
                        obstaclePool.Spawn(ObstacleType.HoverSecurityDrone, otherA, laneWidth, rowZ);
                        obstaclePool.Spawn(ObstacleType.LaserGate, otherB, laneWidth, rowZ);
                        coinPool.SpawnLine(nextSafeLane, laneWidth, rowZ - 3.5f, count: 5, spacing: 2.2f);
                        lastSafeLane = nextSafeLane;
                        return 3f;
                    }
                    return SpawnStandardOpenLaneBlockers(nextSafeLane, rowZ, ObstacleType.TallCyberBlock);

                case BiomeType.SkyHighway:
                    if (roll < 35)
                    {
                        obstaclePool.Spawn(ObstacleType.HoverSecurityDrone, otherA, laneWidth, rowZ);
                        obstaclePool.Spawn(ObstacleType.LowRoadBarrier, otherB, laneWidth, rowZ);
                        coinPool.SpawnLine(nextSafeLane, laneWidth, rowZ - 3.5f, count: 5, spacing: 2.2f);
                        lastSafeLane = nextSafeLane;
                        return 3f;
                    }
                    if (roll < 70)
                    {
                        obstaclePool.Spawn(ObstacleType.LowRoadBarrier, nextSafeLane, laneWidth, rowZ);
                        coinPool.SpawnJumpArc(nextSafeLane, laneWidth, rowZ);
                        lastSafeLane = nextSafeLane;
                        return 2.5f;
                    }
                    return SpawnTripleLowBarriers(rowZ);

                case BiomeType.IndustrialZone:
                    if (roll < 36)
                    {
                        // Spinning industrial hazard rotor with jumpable coin arc!
                        obstaclePool.Spawn(ObstacleType.RotatingIndustrialBar, nextSafeLane, laneWidth, rowZ);
                        obstaclePool.Spawn(ObstacleType.TallCyberBlock, otherA, laneWidth, rowZ);
                        coinPool.SpawnJumpArc(nextSafeLane, laneWidth, rowZ);
                        lastSafeLane = nextSafeLane;
                        return 3.5f;
                    }
                    if (roll < 64)
                    {
                        // Dual industrial rotors + open safe lane
                        obstaclePool.Spawn(ObstacleType.RotatingIndustrialBar, otherA, laneWidth, rowZ);
                        obstaclePool.Spawn(ObstacleType.RotatingIndustrialBar, otherB, laneWidth, rowZ);
                        coinPool.SpawnLine(nextSafeLane, laneWidth, rowZ - 3.5f, count: 5, spacing: 2.2f);
                        lastSafeLane = nextSafeLane;
                        return 3f;
                    }
                    if (roll < 84)
                    {
                        obstaclePool.Spawn(ObstacleType.MovingCyberTrain, otherB, laneWidth, rowZ + 6f);
                        coinPool.SpawnLine(nextSafeLane, laneWidth, rowZ - 2f, count: 5, spacing: 2.4f);
                        lastSafeLane = nextSafeLane;
                        return 12f;
                    }
                    return SpawnStandardOpenLaneBlockers(nextSafeLane, rowZ, ObstacleType.TallCyberBlock);

                case BiomeType.DigitalCore:
                    if (roll < 38)
                    {
                        // Dual Electric Energy Walls + guaranteed open neon coin channel
                        return SpawnStandardOpenLaneBlockers(nextSafeLane, rowZ, ObstacleType.ElectricEnergyWall);
                    }
                    if (roll < 68)
                    {
                        // Energy wall + laser gate + open lane
                        obstaclePool.Spawn(ObstacleType.ElectricEnergyWall, otherA, laneWidth, rowZ);
                        obstaclePool.Spawn(ObstacleType.LaserGate, otherB, laneWidth, rowZ);
                        coinPool.SpawnLine(nextSafeLane, laneWidth, rowZ - 4f, count: 5, spacing: 2.2f);
                        lastSafeLane = nextSafeLane;
                        return 3f;
                    }
                    if (roll < 85)
                    {
                        // Triple laser sweep (slide clearable in all lanes)
                        obstaclePool.Spawn(ObstacleType.LaserGate, -1, laneWidth, rowZ);
                        obstaclePool.Spawn(ObstacleType.LaserGate, 0, laneWidth, rowZ);
                        obstaclePool.Spawn(ObstacleType.LaserGate, 1, laneWidth, rowZ);
                        coinPool.SpawnSlideTrail(lastSafeLane, laneWidth, rowZ);
                        return 3f;
                    }
                    obstaclePool.Spawn(ObstacleType.RotatingIndustrialBar, otherA, laneWidth, rowZ);
                    obstaclePool.Spawn(ObstacleType.HoverSecurityDrone, otherB, laneWidth, rowZ);
                    coinPool.SpawnLine(nextSafeLane, laneWidth, rowZ - 3.5f, count: 5, spacing: 2.2f);
                    lastSafeLane = nextSafeLane;
                    return 3f;

                default: // NeonCity
                    if (roll < 25)
                    {
                        obstaclePool.Spawn(ObstacleType.LowRoadBarrier, nextSafeLane, laneWidth, rowZ);
                        coinPool.SpawnJumpArc(nextSafeLane, laneWidth, rowZ);
                        lastSafeLane = nextSafeLane;
                        return 2f;
                    }
                    if (roll < 48)
                    {
                        obstaclePool.Spawn(ObstacleType.LaserGate, nextSafeLane, laneWidth, rowZ);
                        coinPool.SpawnSlideTrail(nextSafeLane, laneWidth, rowZ);
                        lastSafeLane = nextSafeLane;
                        return 2f;
                    }
                    if (roll < 70)
                    {
                        return SpawnStandardOpenLaneBlockers(nextSafeLane, rowZ, ObstacleType.TallCyberBlock);
                    }
                    if (roll < 85 && segmentIndex >= 4)
                    {
                        obstaclePool.Spawn(ObstacleType.MovingCyberTrain, otherA, laneWidth, rowZ + 6f);
                        coinPool.SpawnLine(nextSafeLane, laneWidth, rowZ - 2f, count: 6, spacing: 2.4f);
                        lastSafeLane = nextSafeLane;
                        return 12f;
                    }
                    return SpawnTripleLowBarriers(rowZ);
            }
        }

        private float SpawnStandardOpenLaneBlockers(int safeLane, float rowZ, ObstacleType blockerType)
        {
            for (int lane = -1; lane <= 1; lane++)
            {
                if (lane != safeLane)
                {
                    obstaclePool.Spawn(blockerType, lane, laneWidth, rowZ);
                }
            }

            coinPool.SpawnLine(safeLane, laneWidth, rowZ - 4.5f, count: 5, spacing: 2.3f);
            lastSafeLane = safeLane;
            return 3f;
        }

        private float SpawnTripleLowBarriers(float rowZ)
        {
            obstaclePool.Spawn(ObstacleType.LowRoadBarrier, -1, laneWidth, rowZ);
            obstaclePool.Spawn(ObstacleType.LowRoadBarrier, 0, laneWidth, rowZ);
            obstaclePool.Spawn(ObstacleType.LowRoadBarrier, 1, laneWidth, rowZ);
            coinPool.SpawnJumpArc(lastSafeLane, laneWidth, rowZ);
            return 3f;
        }

        private static int PickReachableSafeLane(int currentSafe)
        {
            int delta = UnityEngine.Random.Range(-1, 2);
            return Mathf.Clamp(currentSafe + delta, -1, 1);
        }

        private static int WrapLane(int lane)
        {
            if (lane > 1) return -1;
            if (lane < -1) return 1;
            return lane;
        }

        private void BuildMaterialPalette()
        {
            Shader simpleLit = Shader.Find("Universal Render Pipeline/Simple Lit") ?? Shader.Find("Standard");
            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit") ?? simpleLit;

            palette = new BiomeMaterialPalette
            {
                RoadNeonCity = ResolveMat(roadNeonCityMaterial, "Materials/CyberRoad_Dark", simpleLit, new Color(0.045f, 0.06f, 0.11f, 1f)),
                RoadUnderground = ResolveMat(roadUndergroundMaterial, "Materials/CyberRoad_Underground", simpleLit, new Color(0.07f, 0.05f, 0.06f, 1f)),
                RoadSkyHighway = ResolveMat(roadSkyHighwayMaterial, "Materials/CyberRoad_Sky", simpleLit, new Color(0.07f, 0.12f, 0.22f, 1f)),
                RoadIndustrial = ResolveMat(roadIndustrialMaterial, "Materials/CyberRoad_Industrial", simpleLit, new Color(0.11f, 0.09f, 0.07f, 1f)),
                RoadDigitalCore = ResolveMat(roadDigitalCoreMaterial, "Materials/CyberRoad_Digital", simpleLit, new Color(0.03f, 0.07f, 0.12f, 1f)),

                BuildingDark = ResolveMat(buildingDarkMaterial, "Materials/CyberBuilding_Dark", simpleLit, new Color(0.03f, 0.04f, 0.085f, 1f)),
                SteelIndustrial = ResolveMat(steelIndustrialMaterial, "Materials/CyberSteel_Industrial", simpleLit, new Color(0.14f, 0.15f, 0.18f, 1f)),

                NeonCyan = ResolveMat(neonCyanMaterial, "Materials/CyberNeon_Cyan", unlit, new Color(0.0f, 0.94f, 1.0f, 1f)),
                NeonPurple = ResolveMat(neonPurpleMaterial, "Materials/CyberNeon_Purple", unlit, new Color(0.68f, 0.22f, 1.0f, 1f)),
                NeonMagenta = ResolveMat(neonMagentaMaterial, "Materials/CyberNeon_Magenta", unlit, new Color(0.96f, 0.18f, 0.92f, 1f)),
                NeonGreen = ResolveMat(neonGreenMaterial, "Materials/CyberNeon_Green", unlit, new Color(0.16f, 0.96f, 0.45f, 1f)),
                HazardRed = ResolveMat(hazardRedMaterial, "Materials/CyberHazard_Red", unlit, new Color(1.0f, 0.14f, 0.28f, 1f)),
                HazardAmber = ResolveMat(hazardAmberMaterial, "Materials/CyberHazard_Amber", unlit, new Color(1.0f, 0.62f, 0.08f, 1f))
            };
        }

        private static Material ResolveMat(Material assigned, string resourcePath, Shader fallbackShader, Color fallbackColor)
        {
            if (assigned != null) return assigned;
            Material loaded = Resources.Load<Material>(resourcePath);
            if (loaded != null) return loaded;
            if (fallbackShader == null) return null;

            var mat = new Material(fallbackShader);
            mat.SetColor("_BaseColor", fallbackColor);
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
            segments = new BiomeTrackSegment[count];

            for (int i = 0; i < count; i++)
            {
                var segObj = new GameObject($"BiomeTrackSegment_{i}");
                segObj.transform.SetParent(transform, false);
                BiomeTrackSegment seg = segObj.AddComponent<BiomeTrackSegment>();
                seg.BuildSegment(i, segmentLength, laneWidth, palette);
                segments[i] = seg;
            }

            built = true;
        }
    }
}
