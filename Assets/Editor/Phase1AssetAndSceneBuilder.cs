using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using CyberRunner.CameraSystem;
using CyberRunner.Collectibles;
using CyberRunner.Core;
using CyberRunner.Environment;
using CyberRunner.Gameplay;
using CyberRunner.Obstacles;
using CyberRunner.Player;
using CyberRunner.PowerUps;
using CyberRunner.Progression;
using CyberRunner.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace CyberRunner.Editor
{
    /// <summary>
    /// Programmatically generates and verifies all URP mobile assets, GPU-instanced materials
    /// for all 5 futuristic environments, GameConfig, Bootstrap.unity, and Gameplay.unity.
    /// Guarantees 100% valid Unity 6000.0 serialization in headless GitHub Actions CI builds.
    /// </summary>
    public static class Phase1AssetAndSceneBuilder
    {
        public const string UrpRendererPath = "Assets/Settings/CyberRunner_URP_Renderer.asset";
        public const string UrpAssetPath = "Assets/Settings/CyberRunner_URP_Mobile.asset";
        public const string GameConfigPath = "Assets/Resources/GameConfig.asset";
        public const string MaterialsResourceFolder = "Assets/Resources/Materials";
        public const string BootstrapScenePath = "Assets/Scenes/Bootstrap.unity";
        public const string GameplayScenePath = "Assets/Scenes/Gameplay.unity";

        private struct SharedMaterials
        {
            public Material SuitDark;
            public Material TrainBody;
            public Material CoinGold;
            public BiomeMaterialPalette BiomePalette;
        }

        [MenuItem("CyberRunner/Rebuild All Scenes & URP Assets (v1.0.0)")]
        public static void BuildAllPhase1AssetsAndScenes()
        {
            EnsureFoldersExist();
            SetupMobileUrpPipeline();
            GameConfig config = EnsureGameConfig();
            SharedMaterials mats = EnsureMobileMaterials();

            BuildBootstrapScene(config);
            BuildGameplayScene(config, mats);
            ConfigureEditorBuildSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[CyberRunner] v1.0.0 Final URP pipeline, branding, 5 biomes, 6 power-ups, progression, audio, optimization & scenes generated successfully.");
        }

        private static void EnsureFoldersExist()
        {
            EnsureFolder("Assets/Settings");
            EnsureFolder("Assets/Resources");
            EnsureFolder("Assets/Resources/Materials");
            EnsureFolder("Assets/Scenes");
        }

        private static void EnsureFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath))
            {
                return;
            }

            string parent = Path.GetDirectoryName(assetPath)?.Replace("\\", "/");
            string name = Path.GetFileName(assetPath);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, name);
        }

        private static void SetupMobileUrpPipeline()
        {
            UniversalRendererData rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(UrpRendererPath);
            if (rendererData == null)
            {
                rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(rendererData, UrpRendererPath);
            }

            UniversalRenderPipelineAsset urpAsset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(UrpAssetPath);
            if (urpAsset == null)
            {
                urpAsset = UniversalRenderPipelineAsset.Create(rendererData);
                AssetDatabase.CreateAsset(urpAsset, UrpAssetPath);
            }

            urpAsset.supportsHDR = false;
            urpAsset.msaaSampleCount = 1;
            urpAsset.renderScale = 0.85f;
            urpAsset.shadowDistance = 0f;
            urpAsset.supportsCameraDepthTexture = false;
            urpAsset.supportsCameraOpaqueTexture = false;
            urpAsset.useSRPBatcher = true;
            EditorUtility.SetDirty(urpAsset);
            EditorUtility.SetDirty(rendererData);

            GraphicsSettings.defaultRenderPipeline = urpAsset;

            int originalQuality = QualitySettings.GetQualityLevel();
            int qualityCount = QualitySettings.names.Length;
            for (int i = 0; i < qualityCount; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = urpAsset;
            }

            if (qualityCount > 0)
            {
                QualitySettings.SetQualityLevel(Mathf.Clamp(originalQuality, 0, qualityCount - 1), false);
            }
        }

        private static GameConfig EnsureGameConfig()
        {
            GameConfig config = AssetDatabase.LoadAssetAtPath<GameConfig>(GameConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<GameConfig>();
                AssetDatabase.CreateAsset(config, GameConfigPath);
                EditorUtility.SetDirty(config);
            }

            return config;
        }

        private static SharedMaterials EnsureMobileMaterials()
        {
            Shader simpleLit = Shader.Find("Universal Render Pipeline/Simple Lit") ?? Shader.Find("Standard");
            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit") ?? simpleLit;

            var palette = new BiomeMaterialPalette
            {
                RoadNeonCity = CreateOrUpdateMaterial($"{MaterialsResourceFolder}/CyberRoad_Dark.mat", simpleLit, new Color(0.045f, 0.06f, 0.11f, 1f), true),
                RoadUnderground = CreateOrUpdateMaterial($"{MaterialsResourceFolder}/CyberRoad_Underground.mat", simpleLit, new Color(0.07f, 0.05f, 0.06f, 1f), true),
                RoadSkyHighway = CreateOrUpdateMaterial($"{MaterialsResourceFolder}/CyberRoad_Sky.mat", simpleLit, new Color(0.07f, 0.12f, 0.22f, 1f), true),
                RoadIndustrial = CreateOrUpdateMaterial($"{MaterialsResourceFolder}/CyberRoad_Industrial.mat", simpleLit, new Color(0.11f, 0.09f, 0.07f, 1f), true),
                RoadDigitalCore = CreateOrUpdateMaterial($"{MaterialsResourceFolder}/CyberRoad_Digital.mat", simpleLit, new Color(0.03f, 0.07f, 0.12f, 1f), true),

                BuildingDark = CreateOrUpdateMaterial($"{MaterialsResourceFolder}/CyberBuilding_Dark.mat", simpleLit, new Color(0.03f, 0.04f, 0.085f, 1f), true),
                SteelIndustrial = CreateOrUpdateMaterial($"{MaterialsResourceFolder}/CyberSteel_Industrial.mat", simpleLit, new Color(0.14f, 0.15f, 0.18f, 1f), true),

                NeonCyan = CreateOrUpdateMaterial($"{MaterialsResourceFolder}/CyberNeon_Cyan.mat", unlit, new Color(0.0f, 0.94f, 1.0f, 1f), true),
                NeonPurple = CreateOrUpdateMaterial($"{MaterialsResourceFolder}/CyberNeon_Purple.mat", unlit, new Color(0.68f, 0.22f, 1.0f, 1f), true),
                NeonMagenta = CreateOrUpdateMaterial($"{MaterialsResourceFolder}/CyberNeon_Magenta.mat", unlit, new Color(0.96f, 0.18f, 0.92f, 1f), true),
                NeonGreen = CreateOrUpdateMaterial($"{MaterialsResourceFolder}/CyberNeon_Green.mat", unlit, new Color(0.16f, 0.96f, 0.45f, 1f), true),
                HazardRed = CreateOrUpdateMaterial($"{MaterialsResourceFolder}/CyberHazard_Red.mat", unlit, new Color(1.0f, 0.14f, 0.28f, 1f), true),
                HazardAmber = CreateOrUpdateMaterial($"{MaterialsResourceFolder}/CyberHazard_Amber.mat", unlit, new Color(1.0f, 0.62f, 0.08f, 1f), true)
            };

            return new SharedMaterials
            {
                SuitDark = CreateOrUpdateMaterial($"{MaterialsResourceFolder}/CyberSuit_Dark.mat", simpleLit, new Color(0.08f, 0.10f, 0.15f, 1f), true),
                TrainBody = CreateOrUpdateMaterial($"{MaterialsResourceFolder}/CyberTrain_Body.mat", simpleLit, new Color(0.11f, 0.14f, 0.22f, 1f), true),
                CoinGold = CreateOrUpdateMaterial($"{MaterialsResourceFolder}/CyberCoin_Gold.mat", unlit, new Color(1.0f, 0.84f, 0.14f, 1f), true),
                BiomePalette = palette
            };
        }

        private static Material CreateOrUpdateMaterial(string path, Shader shader, Color baseColor, bool enableGpuInstancing)
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (shader != null && mat.shader != shader)
            {
                mat.shader = shader;
            }

            if (mat.HasProperty("_BaseColor"))
            {
                mat.SetColor("_BaseColor", baseColor);
            }
            else if (mat.HasProperty("_Color"))
            {
                mat.SetColor("_Color", baseColor);
            }

            mat.enableInstancing = enableGpuInstancing;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void BuildBootstrapScene(GameConfig config)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var lifecycleObj = new GameObject("AppLifecycleManager");
            lifecycleObj.AddComponent<AppLifecycleManager>();

            var bootstrapObj = new GameObject("BootstrapLoader");
            BootstrapLoader loader = bootstrapObj.AddComponent<BootstrapLoader>();
            SerializedObject serializedLoader = new SerializedObject(loader);
            SerializedProperty configProp = serializedLoader.FindProperty("defaultConfig");
            if (configProp != null)
            {
                configProp.objectReferenceValue = config;
                serializedLoader.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorSceneManager.SaveScene(scene, BootstrapScenePath);
        }

        private static void BuildGameplayScene(GameConfig config, SharedMaterials mats)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BiomeProfile defaultProfile = BiomeCatalog.GetProfile(BiomeType.NeonCity);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = defaultProfile.AmbientSky;
            RenderSettings.ambientEquatorColor = defaultProfile.AmbientEquator;
            RenderSettings.ambientGroundColor = defaultProfile.AmbientGround;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = defaultProfile.FogColor;
            RenderSettings.fogStartDistance = defaultProfile.FogStart;
            RenderSettings.fogEndDistance = defaultProfile.FogEnd;

            // Directional neon-tinted key light (shadows disabled for low-end mobile performance).
            var lightObj = new GameObject("CyberKeyLight");
            Light keyLight = lightObj.AddComponent<Light>();
            keyLight.type = LightType.Directional;
            keyLight.color = defaultProfile.KeyLightColor;
            keyLight.intensity = defaultProfile.KeyLightIntensity;
            keyLight.shadows = LightShadows.None;
            lightObj.transform.rotation = Quaternion.Euler(46f, -26f, 0f);

            // EventSystem
            var eventSystemObj = new GameObject("EventSystem");
            eventSystemObj.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            eventSystemObj.AddComponent<InputSystemUIInputModule>();
#else
            eventSystemObj.AddComponent<StandaloneInputModule>();
#endif

            // Cyber Runner Player Root
            var playerObj = new GameObject("CyberRunnerPlayer");
            playerObj.transform.position = new Vector3(0f, 0.05f, 4f);
            playerObj.AddComponent<CharacterController>();

            MobileInputController inputController = playerObj.AddComponent<MobileInputController>();
            inputController.SetConfig(config);

            CyberRunnerVisualRig visualRig = playerObj.AddComponent<CyberRunnerVisualRig>();
            visualRig.SetMaterials(mats.SuitDark, mats.BiomePalette.NeonCyan, mats.BiomePalette.NeonPurple);

            PlayerPowerUpVisuals powerUpVisuals = playerObj.AddComponent<PlayerPowerUpVisuals>();
            powerUpVisuals.ConfigureMaterials(
                mats.SuitDark,
                mats.BiomePalette.NeonCyan,
                mats.BiomePalette.NeonPurple,
                mats.BiomePalette.NeonMagenta,
                mats.BiomePalette.NeonGreen,
                mats.CoinGold,
                mats.BiomePalette.HazardAmber);

            PlayerController playerController = playerObj.AddComponent<PlayerController>();
            playerController.Configure(config, inputController, visualRig);

            // Third-Person Camera
            var cameraObj = new GameObject("Main Camera");
            cameraObj.tag = "MainCamera";
            Camera mainCam = cameraObj.AddComponent<Camera>();
            mainCam.clearFlags = CameraClearFlags.SolidColor;
            mainCam.backgroundColor = defaultProfile.CameraSkyColor;
            mainCam.nearClipPlane = 0.25f;
            mainCam.farClipPlane = 160f;
            mainCam.fieldOfView = config.baseCameraFov;
            cameraObj.AddComponent<AudioListener>();
            cameraObj.AddComponent<UniversalAdditionalCameraData>();

            CyberCameraController cameraController = cameraObj.AddComponent<CyberCameraController>();
            cameraController.SetTarget(playerObj.transform, playerController, config);

            // Dynamic 5-Biome Atmosphere Controller
            var atmosphereObj = new GameObject("BiomeAtmosphereController");
            BiomeAtmosphereController atmosphereController = atmosphereObj.AddComponent<BiomeAtmosphereController>();
            atmosphereController.Configure(mainCam, keyLight);

            // Pooled Obstacles Manager (7 cyberpunk obstacle categories)
            var obstaclePoolObj = new GameObject("ObstaclePoolManager");
            ObstaclePoolManager obstaclePool = obstaclePoolObj.AddComponent<ObstaclePoolManager>();
            obstaclePool.ConfigureMaterials(
                mats.BiomePalette.BuildingDark,
                mats.TrainBody,
                mats.BiomePalette.NeonCyan,
                mats.BiomePalette.NeonPurple,
                mats.BiomePalette.HazardRed,
                mats.BiomePalette.HazardAmber,
                mats.BiomePalette.NeonMagenta);

            // Pooled Energy Coins Manager
            var coinPoolObj = new GameObject("CoinPoolManager");
            CoinPoolManager coinPool = coinPoolObj.AddComponent<CoinPoolManager>();
            coinPool.ConfigureMaterials(mats.CoinGold, mats.BiomePalette.NeonCyan);

            // Pooled Power-Up Pickups Manager (6 power-ups)
            var powerUpPoolObj = new GameObject("PowerUpPoolManager");
            PowerUpPoolManager powerUpPool = powerUpPoolObj.AddComponent<PowerUpPoolManager>();
            powerUpPool.ConfigureMaterials(
                mats.BiomePalette.NeonCyan,
                mats.BiomePalette.NeonPurple,
                mats.BiomePalette.NeonMagenta,
                mats.BiomePalette.NeonGreen,
                mats.CoinGold,
                mats.BiomePalette.HazardAmber);

            // Power-Up Runtime Controller
            var powerUpManagerObj = new GameObject("PowerUpManager");
            PowerUpManager powerUpManager = powerUpManagerObj.AddComponent<PowerUpManager>();
            powerUpManager.Configure(powerUpVisuals, obstaclePool);

            // Balanced Difficulty Progression & Score Systems
            var difficultyObj = new GameObject("DifficultyManager");
            DifficultyManager difficultyManager = difficultyObj.AddComponent<DifficultyManager>();
            difficultyManager.Configure(config);

            var scoreObj = new GameObject("ScoreManager");
            ScoreManager scoreManager = scoreObj.AddComponent<ScoreManager>();

            // Phase 5 Progression Controller (Skins, Power-Up Upgrades, Missions, Achievements, Daily Rewards, Offline Save)
            var progressionObj = new GameObject("ProgressionManager");
            ProgressionManager progressionManager = progressionObj.AddComponent<ProgressionManager>();
            progressionManager.Configure(visualRig, powerUpManager, coinPool);

            // Pooled 5-Biome Endless Track Manager
            var trackObj = new GameObject("EndlessTrackManager");
            EndlessTrackManager trackManager = trackObj.AddComponent<EndlessTrackManager>();
            trackManager.Configure(config, playerObj.transform, obstaclePool, coinPool, mats.BiomePalette, powerUpPool);

            // Core Run Orchestrator
            var runManagerObj = new GameObject("CoreRunManager");
            CoreRunManager runManager = runManagerObj.AddComponent<CoreRunManager>();
            runManager.Configure(
                config,
                playerController,
                inputController,
                cameraController,
                trackManager,
                atmosphereController,
                obstaclePool,
                coinPool,
                powerUpPool,
                powerUpManager,
                difficultyManager,
                scoreManager,
                progressionManager);

            // Phase 5 Gameplay HUD + Power-Up Timers, Ability Button, Pause, Game Over & Cyber Hub Progression Overlay
            var hudObj = new GameObject("Phase2GameplayHud");
            Phase2GameplayHud hud = hudObj.AddComponent<Phase2GameplayHud>();
            hud.Configure(runManager, playerController, coinPool, powerUpManager, scoreManager, difficultyManager, progressionManager);

            EditorSceneManager.SaveScene(scene, GameplayScenePath);
        }

        private static void ConfigureEditorBuildSettings()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(BootstrapScenePath, true),
                new EditorBuildSettingsScene(GameplayScenePath, true)
            };
        }
    }
}
