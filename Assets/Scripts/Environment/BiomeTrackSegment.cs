using UnityEngine;

namespace CyberRunner.Environment
{
    /// <summary>
    /// Shared mobile URP material palette used across all 5 cyberpunk environments.
    /// </summary>
    public struct BiomeMaterialPalette
    {
        public Material RoadNeonCity;
        public Material RoadUnderground;
        public Material RoadSkyHighway;
        public Material RoadIndustrial;
        public Material RoadDigitalCore;

        public Material BuildingDark;
        public Material SteelIndustrial;

        public Material NeonCyan;
        public Material NeonPurple;
        public Material NeonMagenta;
        public Material NeonGreen;
        public Material HazardRed;
        public Material HazardAmber;
    }

    /// <summary>
    /// Pooled 40m highway segment with pre-built prop groups for all 5 futuristic biomes,
    /// split road deck supporting fair jumpable Sky Highway Road Gaps, and lightweight
    /// centralized ambient animations (flying cars, tunnel fans, robotic arms, digital rings).
    /// Zero Instantiate/Destroy on biome switches or recycling.
    /// </summary>
    public sealed class BiomeTrackSegment : MonoBehaviour
    {
        public const float GapCenterLocalZ = 20.0f;
        public const float GapLength = 6.0f;

        public BiomeType AssignedBiome { get; private set; }
        public bool HasRoadGap { get; private set; }
        public float SegmentLength { get; private set; }

        private BiomeMaterialPalette palette;
        private float laneWidth;
        private float roadWidth;

        // Road & gap geometry
        private MeshRenderer[] deckRenderers;
        private MeshRenderer[] curbRenderers;
        private MeshRenderer[] dividerRenderers;
        private GameObject gapBridgeGroup;
        private GameObject gapWarningGroup;

        // Pre-built biome prop containers (toggled with SetActive, zero GC)
        private GameObject neonCityGroup;
        private GameObject undergroundGroup;
        private GameObject skyHighwayGroup;
        private GameObject industrialGroup;
        private GameObject digitalCoreGroup;

        // Lightweight animated ambient prop references
        private Transform[] neonFlyingCars;
        private Transform[] undergroundFans;
        private Transform[] skyFlyingCars;
        private Transform[] industrialRobotArms;
        private Transform[] digitalEnergyRings;
        private float ambientClock;

        public void BuildSegment(int segmentIndex, float segLength, float laneW, BiomeMaterialPalette mats)
        {
            SegmentLength = segLength;
            laneWidth = laneW;
            roadWidth = (laneWidth * 3f) + 1.1f;
            palette = mats;
            ambientClock = segmentIndex * 1.7f;

            BuildSplitRoadDeck();
            BuildNeonCityGroup(segmentIndex);
            BuildUndergroundGroup(segmentIndex);
            BuildSkyHighwayGroup(segmentIndex);
            BuildIndustrialGroup(segmentIndex);
            BuildDigitalCoreGroup(segmentIndex);

            ConfigureForBiome(BiomeType.NeonCity, hasRoadGap: false);
        }

        public void ConfigureForBiome(BiomeType biome, bool hasRoadGap)
        {
            AssignedBiome = biome;
            HasRoadGap = hasRoadGap;

            if (gapBridgeGroup != null)
            {
                gapBridgeGroup.SetActive(!hasRoadGap);
            }

            if (gapWarningGroup != null)
            {
                gapWarningGroup.SetActive(hasRoadGap);
            }

            if (neonCityGroup != null) neonCityGroup.SetActive(biome == BiomeType.NeonCity);
            if (undergroundGroup != null) undergroundGroup.SetActive(biome == BiomeType.CyberUnderground);
            if (skyHighwayGroup != null) skyHighwayGroup.SetActive(biome == BiomeType.SkyHighway);
            if (industrialGroup != null) industrialGroup.SetActive(biome == BiomeType.IndustrialZone);
            if (digitalCoreGroup != null) digitalCoreGroup.SetActive(biome == BiomeType.DigitalCore);

            ApplyRoadMaterials(biome);
        }

        /// <summary>
        /// Animates lightweight environmental props ONLY for the currently active biome.
        /// </summary>
        public void TickAmbient(float deltaTime)
        {
            ambientClock += deltaTime;

            switch (AssignedBiome)
            {
                case BiomeType.NeonCity:
                    TickFlyingCars(neonFlyingCars, deltaTime, 16f);
                    break;

                case BiomeType.CyberUnderground:
                    if (undergroundFans != null)
                    {
                        for (int i = 0; i < undergroundFans.Length; i++)
                        {
                            if (undergroundFans[i] != null)
                            {
                                undergroundFans[i].Rotate(0f, 220f * deltaTime, 0f, Space.Self);
                            }
                        }
                    }
                    break;

                case BiomeType.SkyHighway:
                    TickFlyingCars(skyFlyingCars, deltaTime, 22f);
                    break;

                case BiomeType.IndustrialZone:
                    if (industrialRobotArms != null)
                    {
                        for (int i = 0; i < industrialRobotArms.Length; i++)
                        {
                            if (industrialRobotArms[i] != null)
                            {
                                float dir = (i % 2 == 0) ? 1f : -1f;
                                float swing = Mathf.Sin((ambientClock * 2.1f) + (i * 1.4f)) * 26f * dir;
                                industrialRobotArms[i].localRotation = Quaternion.Euler(0f, 0f, swing);
                            }
                        }
                    }
                    break;

                case BiomeType.DigitalCore:
                    if (digitalEnergyRings != null)
                    {
                        for (int i = 0; i < digitalEnergyRings.Length; i++)
                        {
                            if (digitalEnergyRings[i] != null)
                            {
                                float speed = (i % 2 == 0) ? 38f : -32f;
                                digitalEnergyRings[i].Rotate(0f, 0f, speed * deltaTime, Space.Self);
                            }
                        }
                    }
                    break;
            }
        }

        private void TickFlyingCars(Transform[] cars, float dt, float speed)
        {
            if (cars == null)
            {
                return;
            }

            for (int i = 0; i < cars.Length; i++)
            {
                Transform car = cars[i];
                if (car == null)
                {
                    continue;
                }

                Vector3 lp = car.localPosition;
                float dir = (i % 2 == 0) ? -1f : 1f;
                lp.z += dir * speed * dt;

                if (lp.z < -4f) lp.z = SegmentLength + 4f;
                else if (lp.z > SegmentLength + 4f) lp.z = -4f;

                car.localPosition = lp;
            }
        }

        private void ApplyRoadMaterials(BiomeType biome)
        {
            Material roadMat;
            Material curbMat;
            Material dividerMat;

            switch (biome)
            {
                case BiomeType.CyberUnderground:
                    roadMat = palette.RoadUnderground;
                    curbMat = palette.HazardRed;
                    dividerMat = palette.HazardAmber;
                    break;

                case BiomeType.SkyHighway:
                    roadMat = palette.RoadSkyHighway;
                    curbMat = palette.NeonCyan;
                    dividerMat = palette.NeonCyan;
                    break;

                case BiomeType.IndustrialZone:
                    roadMat = palette.RoadIndustrial;
                    curbMat = palette.HazardAmber;
                    dividerMat = palette.NeonGreen;
                    break;

                case BiomeType.DigitalCore:
                    roadMat = palette.RoadDigitalCore;
                    curbMat = palette.NeonMagenta;
                    dividerMat = palette.NeonCyan;
                    break;

                default:
                    roadMat = palette.RoadNeonCity;
                    curbMat = palette.NeonCyan;
                    dividerMat = palette.NeonPurple;
                    break;
            }

            SetSharedMaterial(deckRenderers, roadMat);
            SetSharedMaterial(curbRenderers, curbMat);
            SetSharedMaterial(dividerRenderers, dividerMat);
        }

        private static void SetSharedMaterial(MeshRenderer[] renderers, Material mat)
        {
            if (renderers == null || mat == null)
            {
                return;
            }

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    renderers[i].sharedMaterial = mat;
                }
            }
        }

        private void BuildSplitRoadDeck()
        {
            float frontLen = GapCenterLocalZ - (GapLength * 0.5f); // 17.0m
            float rearStart = GapCenterLocalZ + (GapLength * 0.5f); // 23.0m
            float rearLen = SegmentLength - rearStart;              // 17.0m

            float edgeX = (roadWidth * 0.5f) - 0.16f;
            float dividerX = laneWidth * 0.5f;

            // Front & rear permanent road slabs
            MeshRenderer frontDeck = CreatePart("RoadDeck_Front", PrimitiveType.Cube, transform,
                new Vector3(0f, -0.5f, frontLen * 0.5f),
                new Vector3(roadWidth, 1.0f, frontLen), palette.RoadNeonCity, keepCollider: true);

            MeshRenderer rearDeck = CreatePart("RoadDeck_Rear", PrimitiveType.Cube, transform,
                new Vector3(0f, -0.5f, rearStart + (rearLen * 0.5f)),
                new Vector3(roadWidth, 1.0f, rearLen), palette.RoadNeonCity, keepCollider: true);

            // Toggleable center bridge slab (deactivated on Sky Highway Road Gap segments!)
            gapBridgeGroup = new GameObject("RoadBridge_GapSection");
            gapBridgeGroup.transform.SetParent(transform, false);

            MeshRenderer gapDeck = CreatePart("RoadDeck_Gap", PrimitiveType.Cube, gapBridgeGroup.transform,
                new Vector3(0f, -0.5f, GapCenterLocalZ),
                new Vector3(roadWidth, 1.0f, GapLength), palette.RoadNeonCity, keepCollider: true);

            // Curbs (split cleanly across front, gap bridge, and rear)
            MeshRenderer cFL = CreatePart("Curb_FrontL", PrimitiveType.Cube, transform,
                new Vector3(-edgeX, 0.05f, frontLen * 0.5f), new Vector3(0.22f, 0.10f, frontLen), palette.NeonCyan, false);
            MeshRenderer cFR = CreatePart("Curb_FrontR", PrimitiveType.Cube, transform,
                new Vector3(edgeX, 0.05f, frontLen * 0.5f), new Vector3(0.22f, 0.10f, frontLen), palette.NeonCyan, false);
            MeshRenderer cGL = CreatePart("Curb_GapL", PrimitiveType.Cube, gapBridgeGroup.transform,
                new Vector3(-edgeX, 0.05f, GapCenterLocalZ), new Vector3(0.22f, 0.10f, GapLength), palette.NeonCyan, false);
            MeshRenderer cGR = CreatePart("Curb_GapR", PrimitiveType.Cube, gapBridgeGroup.transform,
                new Vector3(edgeX, 0.05f, GapCenterLocalZ), new Vector3(0.22f, 0.10f, GapLength), palette.NeonCyan, false);
            MeshRenderer cRL = CreatePart("Curb_RearL", PrimitiveType.Cube, transform,
                new Vector3(-edgeX, 0.05f, rearStart + (rearLen * 0.5f)), new Vector3(0.22f, 0.10f, rearLen), palette.NeonCyan, false);
            MeshRenderer cRR = CreatePart("Curb_RearR", PrimitiveType.Cube, transform,
                new Vector3(edgeX, 0.05f, rearStart + (rearLen * 0.5f)), new Vector3(0.22f, 0.10f, rearLen), palette.NeonCyan, false);

            // Lane dividers (split cleanly across front, gap bridge, and rear)
            MeshRenderer dFL = CreatePart("Div_FrontL", PrimitiveType.Cube, transform,
                new Vector3(-dividerX, 0.015f, frontLen * 0.5f), new Vector3(0.08f, 0.02f, frontLen), palette.NeonPurple, false);
            MeshRenderer dFR = CreatePart("Div_FrontR", PrimitiveType.Cube, transform,
                new Vector3(dividerX, 0.015f, frontLen * 0.5f), new Vector3(0.08f, 0.02f, frontLen), palette.NeonPurple, false);
            MeshRenderer dGL = CreatePart("Div_GapL", PrimitiveType.Cube, gapBridgeGroup.transform,
                new Vector3(-dividerX, 0.015f, GapCenterLocalZ), new Vector3(0.08f, 0.02f, GapLength), palette.NeonPurple, false);
            MeshRenderer dGR = CreatePart("Div_GapR", PrimitiveType.Cube, gapBridgeGroup.transform,
                new Vector3(dividerX, 0.015f, GapCenterLocalZ), new Vector3(0.08f, 0.02f, GapLength), palette.NeonPurple, false);
            MeshRenderer dRL = CreatePart("Div_RearL", PrimitiveType.Cube, transform,
                new Vector3(-dividerX, 0.015f, rearStart + (rearLen * 0.5f)), new Vector3(0.08f, 0.02f, rearLen), palette.NeonPurple, false);
            MeshRenderer dRR = CreatePart("Div_RearR", PrimitiveType.Cube, transform,
                new Vector3(dividerX, 0.015f, rearStart + (rearLen * 0.5f)), new Vector3(0.08f, 0.02f, rearLen), palette.NeonPurple, false);

            deckRenderers = new[] { frontDeck, gapDeck, rearDeck };
            curbRenderers = new[] { cFL, cFR, cGL, cGR, cRL, cRR };
            dividerRenderers = new[] { dFL, dFR, dGL, dGR, dRL, dRR };

            // Glowing jump warning strips shown right before and after an open Road Gap.
            gapWarningGroup = new GameObject("RoadGap_WarningMarkers");
            gapWarningGroup.transform.SetParent(transform, false);
            CreatePart("TakeoffHazardLip", PrimitiveType.Cube, gapWarningGroup.transform,
                new Vector3(0f, 0.04f, frontLen - 0.35f), new Vector3(roadWidth - 0.5f, 0.08f, 0.65f), palette.HazardAmber, false);
            CreatePart("TakeoffGlowBeaconL", PrimitiveType.Cube, gapWarningGroup.transform,
                new Vector3(-edgeX - 0.2f, 0.85f, frontLen - 0.2f), new Vector3(0.28f, 1.7f, 0.28f), palette.HazardAmber, false);
            CreatePart("TakeoffGlowBeaconR", PrimitiveType.Cube, gapWarningGroup.transform,
                new Vector3(edgeX + 0.2f, 0.85f, frontLen - 0.2f), new Vector3(0.28f, 1.7f, 0.28f), palette.HazardAmber, false);
            CreatePart("LandingCyanLip", PrimitiveType.Cube, gapWarningGroup.transform,
                new Vector3(0f, 0.04f, rearStart + 0.35f), new Vector3(roadWidth - 0.5f, 0.08f, 0.55f), palette.NeonCyan, false);
            gapWarningGroup.SetActive(false);
        }

        // MAP 1: NEON CITY — Skyscrapers, Holographic Billboards, Neon Arch, Flying Traffic
        private void BuildNeonCityGroup(int idx)
        {
            neonCityGroup = CreateGroup("Biome_NeonCity");
            Transform root = neonCityGroup.transform;
            float edgeX = (roadWidth * 0.5f) + 0.75f;

            // Neon light pylons
            for (int p = 0; p < 2; p++)
            {
                float z = (p + 0.5f) * (SegmentLength * 0.5f);
                Material glowL = (p + idx) % 2 == 0 ? palette.NeonCyan : palette.NeonPurple;
                Material glowR = (p + idx) % 2 == 0 ? palette.NeonPurple : palette.NeonCyan;
                CreatePart("PylonL", PrimitiveType.Cube, root, new Vector3(-edgeX, 2.3f, z), new Vector3(0.32f, 4.6f, 0.32f), palette.BuildingDark, false);
                CreatePart("PylonGlowL", PrimitiveType.Cube, root, new Vector3(-edgeX, 3.8f, z), new Vector3(0.38f, 1.0f, 0.38f), glowL, false);
                CreatePart("PylonR", PrimitiveType.Cube, root, new Vector3(edgeX, 2.3f, z), new Vector3(0.32f, 4.6f, 0.32f), palette.BuildingDark, false);
                CreatePart("PylonGlowR", PrimitiveType.Cube, root, new Vector3(edgeX, 3.8f, z), new Vector3(0.38f, 1.0f, 0.38f), glowR, false);
            }

            // Overhead neon cyber-tunnel arch
            CreatePart("NeonArchTop", PrimitiveType.Cube, root, new Vector3(0f, 7.2f, SegmentLength * 0.5f), new Vector3(roadWidth + 1.8f, 0.35f, 0.55f), palette.BuildingDark, false);
            CreatePart("NeonArchGlow", PrimitiveType.Cube, root, new Vector3(0f, 6.95f, SegmentLength * 0.5f), new Vector3(roadWidth + 0.6f, 0.14f, 0.22f), palette.NeonCyan, false);

            // Flanking skyscrapers + holographic ad billboards
            float hL = 20f + ((idx * 7) % 16);
            float hR = 24f + ((idx * 11) % 16);
            CreatePart("TowerL", PrimitiveType.Cube, root, new Vector3(-13.8f, (hL * 0.5f) - 2f, SegmentLength * 0.32f), new Vector3(7.6f, hL, 11.5f), palette.BuildingDark, false);
            CreatePart("TowerTrimL", PrimitiveType.Cube, root, new Vector3(-9.95f, hL * 0.54f, SegmentLength * 0.32f), new Vector3(0.16f, hL * 0.66f, 0.4f), palette.NeonCyan, false);
            CreatePart("HoloBillboardL", PrimitiveType.Cube, root, new Vector3(-9.5f, 9.2f, SegmentLength * 0.72f), new Vector3(0.18f, 3.4f, 6.2f), palette.NeonMagenta, false);

            CreatePart("TowerR", PrimitiveType.Cube, root, new Vector3(13.8f, (hR * 0.5f) - 2f, SegmentLength * 0.68f), new Vector3(7.6f, hR, 11.5f), palette.BuildingDark, false);
            CreatePart("TowerTrimR", PrimitiveType.Cube, root, new Vector3(9.95f, hR * 0.54f, SegmentLength * 0.68f), new Vector3(0.16f, hR * 0.66f, 0.4f), palette.NeonPurple, false);
            CreatePart("HoloBillboardR", PrimitiveType.Cube, root, new Vector3(9.5f, 10.6f, SegmentLength * 0.25f), new Vector3(0.18f, 3.2f, 5.8f), palette.NeonCyan, false);

            // Two ambient flying hover-cars overhead
            neonFlyingCars = new[]
            {
                CreateFlyingCar(root, "CityHoverCarA", new Vector3(-6.2f, 10.2f, SegmentLength * 0.2f), palette.NeonCyan),
                CreateFlyingCar(root, "CityHoverCarB", new Vector3(6.4f, 12.0f, SegmentLength * 0.75f), palette.NeonMagenta)
            };
        }

        // MAP 2: CYBER UNDERGROUND — Enclosed Steel Tunnel, Railway Conduit Strips, Spinning Exhaust Fans
        private void BuildUndergroundGroup(int idx)
        {
            undergroundGroup = CreateGroup("Biome_CyberUnderground");
            Transform root = undergroundGroup.transform;
            float wallX = (roadWidth * 0.5f) + 0.85f;
            float tunnelHeight = 7.4f;

            // Side tunnel walls + ceiling roof
            CreatePart("TunnelWallL", PrimitiveType.Cube, root, new Vector3(-wallX - 0.4f, tunnelHeight * 0.5f, SegmentLength * 0.5f), new Vector3(0.8f, tunnelHeight, SegmentLength), palette.BuildingDark, false);
            CreatePart("TunnelWallR", PrimitiveType.Cube, root, new Vector3(wallX + 0.4f, tunnelHeight * 0.5f, SegmentLength * 0.5f), new Vector3(0.8f, tunnelHeight, SegmentLength), palette.BuildingDark, false);
            CreatePart("TunnelCeiling", PrimitiveType.Cube, root, new Vector3(0f, tunnelHeight + 0.35f, SegmentLength * 0.5f), new Vector3((wallX * 2f) + 1.6f, 0.7f, SegmentLength), palette.BuildingDark, false);

            // Glowing red/amber security conduit strips along tunnel walls
            CreatePart("ConduitStripL", PrimitiveType.Cube, root, new Vector3(-wallX + 0.04f, 2.2f, SegmentLength * 0.5f), new Vector3(0.10f, 0.22f, SegmentLength), palette.HazardRed, false);
            CreatePart("ConduitStripR", PrimitiveType.Cube, root, new Vector3(wallX - 0.04f, 2.2f, SegmentLength * 0.5f), new Vector3(0.10f, 0.22f, SegmentLength), palette.HazardRed, false);
            CreatePart("LowerPipeL", PrimitiveType.Cube, root, new Vector3(-wallX + 0.08f, 0.85f, SegmentLength * 0.5f), new Vector3(0.18f, 0.18f, SegmentLength), palette.HazardAmber, false);
            CreatePart("LowerPipeR", PrimitiveType.Cube, root, new Vector3(wallX - 0.08f, 0.85f, SegmentLength * 0.5f), new Vector3(0.18f, 0.18f, SegmentLength), palette.HazardAmber, false);

            // Heavy ribbed steel support arches + ceiling exhaust fans
            undergroundFans = new Transform[2];
            for (int r = 0; r < 2; r++)
            {
                float z = (r + 0.5f) * (SegmentLength * 0.5f);
                CreatePart("RibLeft", PrimitiveType.Cube, root, new Vector3(-wallX + 0.22f, tunnelHeight * 0.5f, z), new Vector3(0.45f, tunnelHeight, 0.75f), palette.SteelIndustrial, false);
                CreatePart("RibRight", PrimitiveType.Cube, root, new Vector3(wallX - 0.22f, tunnelHeight * 0.5f, z), new Vector3(0.45f, tunnelHeight, 0.75f), palette.SteelIndustrial, false);
                CreatePart("RibTop", PrimitiveType.Cube, root, new Vector3(0f, tunnelHeight - 0.28f, z), new Vector3(wallX * 2f, 0.56f, 0.75f), palette.SteelIndustrial, false);
                CreatePart("TunnelLight", PrimitiveType.Cube, root, new Vector3(0f, tunnelHeight - 0.60f, z), new Vector3(3.2f, 0.12f, 0.30f), (r + idx) % 2 == 0 ? palette.HazardAmber : palette.HazardRed, false);

                var fanPivot = new GameObject($"ExhaustFan_{r}").transform;
                fanPivot.SetParent(root, false);
                fanPivot.localPosition = new Vector3(0f, tunnelHeight - 0.15f, z + 6.5f);
                CreatePart("BladeA", PrimitiveType.Cube, fanPivot, Vector3.zero, new Vector3(3.2f, 0.12f, 0.35f), palette.SteelIndustrial, false);
                CreatePart("BladeB", PrimitiveType.Cube, fanPivot, Vector3.zero, new Vector3(0.35f, 0.12f, 3.2f), palette.SteelIndustrial, false);
                undergroundFans[r] = fanPivot;
            }
        }

        // MAP 3: SKY HIGHWAY — Elevated Floating Bridge, Suspension Cables, Distant Spires Below, Sky Traffic
        private void BuildSkyHighwayGroup(int idx)
        {
            skyHighwayGroup = CreateGroup("Biome_SkyHighway");
            Transform root = skyHighwayGroup.transform;
            float edgeX = (roadWidth * 0.5f) + 0.45f;

            // Sleek glowing suspension bridge towers & cables
            for (int p = 0; p < 2; p++)
            {
                float z = (p == 0) ? 7.5f : 32.5f;
                CreatePart("BridgePylonL", PrimitiveType.Cube, root, new Vector3(-edgeX, 4.8f, z), new Vector3(0.42f, 11.2f, 0.48f), palette.SteelIndustrial, false);
                CreatePart("BridgePylonR", PrimitiveType.Cube, root, new Vector3(edgeX, 4.8f, z), new Vector3(0.42f, 11.2f, 0.48f), palette.SteelIndustrial, false);
                CreatePart("BridgeBeaconL", PrimitiveType.Cube, root, new Vector3(-edgeX, 10.6f, z), new Vector3(0.48f, 0.48f, 0.48f), palette.NeonCyan, false);
                CreatePart("BridgeBeaconR", PrimitiveType.Cube, root, new Vector3(edgeX, 10.6f, z), new Vector3(0.48f, 0.48f, 0.48f), palette.NeonCyan, false);
                CreatePart("SkyArchSpan", PrimitiveType.Cube, root, new Vector3(0f, 9.6f, z), new Vector3((edgeX * 2f) + 0.4f, 0.28f, 0.36f), palette.NeonCyan, false);
            }

            // High-altitude horizontal energy cables
            CreatePart("SkyCableL", PrimitiveType.Cube, root, new Vector3(-edgeX, 6.8f, SegmentLength * 0.5f), new Vector3(0.10f, 0.10f, SegmentLength), palette.NeonCyan, false);
            CreatePart("SkyCableR", PrimitiveType.Cube, root, new Vector3(edgeX, 6.8f, SegmentLength * 0.5f), new Vector3(0.10f, 0.10f, SegmentLength), palette.NeonCyan, false);

            // Distant glowing skyscraper tips far below the floating highway (creates dizzying altitude feel)
            float spireOffset = (idx % 2 == 0) ? 10f : 28f;
            CreatePart("DeepSpireL", PrimitiveType.Cube, root, new Vector3(-16f, -15f, spireOffset), new Vector3(6.5f, 22f, 6.5f), palette.BuildingDark, false);
            CreatePart("DeepSpireCapL", PrimitiveType.Cube, root, new Vector3(-16f, -3.8f, spireOffset), new Vector3(6.7f, 0.35f, 6.7f), palette.NeonCyan, false);
            CreatePart("DeepSpireR", PrimitiveType.Cube, root, new Vector3(17f, -16f, SegmentLength - spireOffset), new Vector3(7.0f, 22f, 7.0f), palette.BuildingDark, false);
            CreatePart("DeepSpireCapR", PrimitiveType.Cube, root, new Vector3(17f, -4.8f, SegmentLength - spireOffset), new Vector3(7.2f, 0.35f, 7.2f), palette.NeonPurple, false);

            // Passing high-altitude sky cars flanking & below the bridge
            skyFlyingCars = new[]
            {
                CreateFlyingCar(root, "SkyCarL", new Vector3(-8.4f, 2.4f, SegmentLength * 0.3f), palette.NeonCyan),
                CreateFlyingCar(root, "SkyCarR", new Vector3(8.6f, -1.2f, SegmentLength * 0.8f), palette.HazardAmber)
            };
        }

        // MAP 4: INDUSTRIAL ZONE — Cyber Factories, Energy Generators, Gantries, Animated Robotic Arms
        private void BuildIndustrialGroup(int idx)
        {
            industrialGroup = CreateGroup("Biome_IndustrialZone");
            Transform root = industrialGroup.transform;
            float edgeX = (roadWidth * 0.5f) + 1.1f;

            // Flanking heavy factories & energy generator cores
            CreatePart("FactoryBlockL", PrimitiveType.Cube, root, new Vector3(-12.2f, 6.5f, SegmentLength * 0.5f), new Vector3(9.0f, 13.0f, SegmentLength - 3f), palette.BuildingDark, false);
            CreatePart("FactoryBlockR", PrimitiveType.Cube, root, new Vector3(12.2f, 6.5f, SegmentLength * 0.5f), new Vector3(9.0f, 13.0f, SegmentLength - 3f), palette.BuildingDark, false);

            // Pulsing green/amber energy generator cores along factory walls
            for (int g = 0; g < 2; g++)
            {
                float z = (g + 0.5f) * (SegmentLength * 0.5f);
                CreatePart("GenCoreL", PrimitiveType.Cylinder, root, new Vector3(-edgeX - 1.0f, 2.6f, z), new Vector3(1.1f, 2.5f, 1.1f), (g + idx) % 2 == 0 ? palette.NeonGreen : palette.HazardAmber, false);
                CreatePart("GenCoreR", PrimitiveType.Cylinder, root, new Vector3(edgeX + 1.0f, 2.6f, z), new Vector3(1.1f, 2.5f, 1.1f), (g + idx) % 2 == 0 ? palette.HazardAmber : palette.NeonGreen, false);
            }

            // Heavy steel gantry crane bridge + animated swinging robotic arms overhead
            float gantryZ = SegmentLength * 0.5f;
            CreatePart("GantryPillarL", PrimitiveType.Cube, root, new Vector3(-edgeX, 4.4f, gantryZ), new Vector3(0.65f, 8.8f, 0.85f), palette.SteelIndustrial, false);
            CreatePart("GantryPillarR", PrimitiveType.Cube, root, new Vector3(edgeX, 4.4f, gantryZ), new Vector3(0.65f, 8.8f, 0.85f), palette.SteelIndustrial, false);
            CreatePart("GantryCrossbeam", PrimitiveType.Cube, root, new Vector3(0f, 8.5f, gantryZ), new Vector3((edgeX * 2f) + 1.2f, 0.85f, 1.0f), palette.SteelIndustrial, false);
            CreatePart("GantryHazardStripe", PrimitiveType.Cube, root, new Vector3(0f, 8.05f, gantryZ - 0.52f), new Vector3((edgeX * 2f) - 0.4f, 0.24f, 0.08f), palette.HazardAmber, false);

            industrialRobotArms = new Transform[2];
            for (int a = 0; a < 2; a++)
            {
                float x = (a == 0) ? -edgeX + 0.6f : edgeX - 0.6f;
                var armPivot = new GameObject($"RoboticArm_{a}").transform;
                armPivot.SetParent(root, false);
                armPivot.localPosition = new Vector3(x, 8.1f, gantryZ);
                CreatePart("ArmSegment", PrimitiveType.Cube, armPivot, new Vector3(0f, -1.6f, 0f), new Vector3(0.32f, 3.2f, 0.32f), palette.SteelIndustrial, false);
                CreatePart("ArmClawGlow", PrimitiveType.Cube, armPivot, new Vector3(0f, -3.25f, 0f), new Vector3(0.55f, 0.35f, 0.55f), palette.HazardAmber, false);
                industrialRobotArms[a] = armPivot;
            }
        }

        // MAP 5: DIGITAL CORE — Floating Data Monoliths, Circuit Grid Lines, Rotating Holographic Energy Rings
        private void BuildDigitalCoreGroup(int idx)
        {
            digitalCoreGroup = CreateGroup("Biome_DigitalCore");
            Transform root = digitalCoreGroup.transform;

            // Cross-highway glowing circuit grid strips
            for (int c = 0; c < 4; c++)
            {
                float z = (c + 0.5f) * (SegmentLength * 0.25f);
                if (Mathf.Abs(z - GapCenterLocalZ) < 3.5f)
                {
                    continue;
                }
                CreatePart("CircuitStrip", PrimitiveType.Cube, root, new Vector3(0f, 0.02f, z), new Vector3(roadWidth - 0.4f, 0.02f, 0.14f), palette.NeonCyan, false);
            }

            // Floating cyber data monoliths outside the grid
            for (int m = 0; m < 2; m++)
            {
                float z = (m == 0) ? 10f : 30f;
                float yOffset = (m + idx) % 2 == 0 ? 5.5f : 8.0f;
                CreatePart("DataMonolithL", PrimitiveType.Cube, root, new Vector3(-11.5f, yOffset, z), new Vector3(2.2f, 9.5f, 2.2f), palette.BuildingDark, false);
                CreatePart("DataGlowL", PrimitiveType.Cube, root, new Vector3(-10.35f, yOffset, z), new Vector3(0.14f, 8.8f, 1.4f), palette.NeonCyan, false);

                CreatePart("DataMonolithR", PrimitiveType.Cube, root, new Vector3(11.5f, yOffset + 1.2f, z), new Vector3(2.2f, 9.5f, 2.2f), palette.BuildingDark, false);
                CreatePart("DataGlowR", PrimitiveType.Cube, root, new Vector3(10.35f, yOffset + 1.2f, z), new Vector3(0.14f, 8.8f, 1.4f), palette.NeonMagenta, false);
            }

            // Two rotating digital energy rings framing the mainframe highway
            digitalEnergyRings = new Transform[2];
            for (int r = 0; r < 2; r++)
            {
                float z = (r == 0) ? 12f : 32f;
                var ringPivot = new GameObject($"DigitalRing_{r}").transform;
                ringPivot.SetParent(root, false);
                ringPivot.localPosition = new Vector3(0f, 4.6f, z);
                Material ringMat = r == 0 ? palette.NeonCyan : palette.NeonMagenta;
                float radius = 7.4f;

                CreatePart("RingTop", PrimitiveType.Cube, ringPivot, new Vector3(0f, radius, 0f), new Vector3(radius * 1.45f, 0.20f, 0.24f), ringMat, false);
                CreatePart("RingLeft", PrimitiveType.Cube, ringPivot, new Vector3(-radius, 0f, 0f), new Vector3(0.20f, radius * 1.45f, 0.24f), ringMat, false);
                CreatePart("RingRight", PrimitiveType.Cube, ringPivot, new Vector3(radius, 0f, 0f), new Vector3(0.20f, radius * 1.45f, 0.24f), ringMat, false);
                CreatePart("RingBottom", PrimitiveType.Cube, ringPivot, new Vector3(0f, -radius, 0f), new Vector3(radius * 1.45f, 0.20f, 0.24f), ringMat, false);
                digitalEnergyRings[r] = ringPivot;
            }
        }

        private Transform CreateFlyingCar(Transform parent, string name, Vector3 localPos, Material glowMat)
        {
            var car = new GameObject(name).transform;
            car.SetParent(parent, false);
            car.localPosition = localPos;
            CreatePart("CarBody", PrimitiveType.Cube, car, Vector3.zero, new Vector3(1.1f, 0.42f, 2.5f), palette.SteelIndustrial, false);
            CreatePart("CarLightStrip", PrimitiveType.Cube, car, new Vector3(0f, -0.10f, 0f), new Vector3(1.18f, 0.14f, 2.2f), glowMat, false);
            return car;
        }

        private GameObject CreateGroup(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            return go;
        }

        private static MeshRenderer CreatePart(
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
            if (renderer != null)
            {
                if (material != null)
                {
                    renderer.sharedMaterial = material;
                }
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            return renderer;
        }
    }
}
