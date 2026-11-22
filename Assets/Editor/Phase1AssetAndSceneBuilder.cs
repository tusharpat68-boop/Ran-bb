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
using CyberRunner.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace CyberRunner.Editor
{
    /// <summary>
    /// Programmatically generates and verifies all URP mobile assets, GPU-instanced materials,
    /// GameConfig asset, Bootstrap.unity, and Gameplay.unity scenes (Phase 1 + Phase 2 Core Gameplay).
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
            public Material NeonCyan;
            public Material NeonPurple;
            public Material RoadDark;
            public Material BuildingDark;
            public Material TrainBody;
            public Material HazardRed;
            public Material HazardAmber;
            public Material CoinGold;
        }

        [MenuItem("CyberRunner/Rebuild All Scenes & URP Assets (Phase 2)")]
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
            Debug.Log("[CyberRunner] Phase 2 URP pipeline, materials, GameConfig, and Core Gameplay scenes generated successfully.");
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

            // Mobile-first URP defaults tuned for 2GB-3GB Android devices.
            urpAsset.supportsHDR = false;
            urpAsset.msaaSampleCount = 2;
            urpAsset.renderScale = 0.85f;
            urpAsset.shadowDistance = 28f;
            urpAsset.supportsCameraDepthTexture = false;
            urpAsset.supportsCameraOpaqueTexture = false;
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

            return new SharedMaterials
            {
                SuitDark = CreateOrUpdateMaterial(
                    $"{MaterialsResourceFolder}/CyberSuit_Dark.mat",
                    simpleLit,
                    new Color(0.08f, 0.10f, 0.15f, 1f),
                    enableGpuInstancing: true),

                RoadDark = CreateOrUpdateMaterial(
                    $"{MaterialsResourceFolder}/CyberRoad_Dark.mat",
                    simpleLit,
                    new Color(0.045f, 0.06f, 0.11f, 1f),
                    enableGpuInstancing: true),

                BuildingDark = CreateOrUpdateMaterial(
                    $"{MaterialsResourceFolder}/CyberBuilding_Dark.mat",
                    simpleLit,
                    new Color(0.03f, 0.04f, 0.085f, 1f),
                    enableGpuInstancing: true),

                TrainBody = CreateOrUpdateMaterial(
                    $"{MaterialsResourceFolder}/CyberTrain_Body.mat",
                    simpleLit,
                    new Color(0.11f, 0.14f, 0.22f, 1f),
                    enableGpuInstancing: true),

                NeonCyan = CreateOrUpdateMaterial(
                    $"{MaterialsResourceFolder}/CyberNeon_Cyan.mat",
                    unlit,
                    new Color(0.0f, 0.94f, 1.0f, 1f),
                    enableGpuInstancing: true),

                NeonPurple = CreateOrUpdateMaterial(
                    $"{MaterialsResourceFolder}/CyberNeon_Purple.mat",
                    unlit,
                    new Color(0.68f, 0.22f, 1.0f, 1f),
                    enableGpuInstancing: true),

                HazardRed = CreateOrUpdateMaterial(
                    $"{MaterialsResourceFolder}/CyberHazard_Red.mat",
                    unlit,
                    new Color(1.0f, 0.14f, 0.28f, 1f),
                    enableGpuInstancing: true),

                HazardAmber = CreateOrUpdateMaterial(
                    $"{MaterialsResourceFolder}/CyberHazard_Amber.mat",
                    unlit,
                    new Color(1.0f, 0.62f, 0.08f, 1f),
                    enableGpuInstancing: true),

                CoinGold = CreateOrUpdateMaterial(
                    $"{MaterialsResourceFolder}/CyberCoin_Gold.mat",
                    unlit,
                    new Color(1.0f, 0.84f, 0.14f, 1f),
                    enableGpuInstancing: true)
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

            // Cyberpunk night atmosphere: deep navy fog and ambient lighting.
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.10f, 0.16f, 0.30f, 1f);
            RenderSettings.ambientEquatorColor = new Color(0.06f, 0.09f, 0.18f, 1f);
            RenderSettings.ambientGroundColor = new Color(0.03f, 0.03f, 0.07f, 1f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.03f, 0.05f, 0.11f, 1f);
            RenderSettings.fogStartDistance = 32f;
            RenderSettings.fogEndDistance = 135f;

            // Directional neon-tinted key light (shadows disabled for low-end mobile performance).
            var lightObj = new GameObject("CyberKeyLight");
            Light keyLight = lightObj.AddComponent<Light>();
            keyLight.type = LightType.Directional;
            keyLight.color = new Color(0.75f, 0.90f, 1.0f, 1f);
            keyLight.intensity = 1.25f;
            keyLight.shadows = LightShadows.None;
            lightObj.transform.rotation = Quaternion.Euler(46f, -26f, 0f);

            // EventSystem for UI interaction.
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
            visualRig.SetMaterials(mats.SuitDark, mats.NeonCyan, mats.NeonPurple);

            PlayerController playerController = playerObj.AddComponent<PlayerController>();
            playerController.Configure(config, inputController, visualRig);

            // Third-Person Camera
            var cameraObj = new GameObject("Main Camera");
            cameraObj.tag = "MainCamera";
            Camera mainCam = cameraObj.AddComponent<Camera>();
            mainCam.clearFlags = CameraClearFlags.SolidColor;
            mainCam.backgroundColor = new Color(0.02f, 0.035f, 0.085f, 1f);
            mainCam.nearClipPlane = 0.25f;
            mainCam.farClipPlane = 155f;
            mainCam.fieldOfView = config.baseCameraFov;
            cameraObj.AddComponent<AudioListener>();
            cameraObj.AddComponent<UniversalAdditionalCameraData>();

            CyberCameraController cameraController = cameraObj.AddComponent<CyberCameraController>();
            cameraController.SetTarget(playerObj.transform, playerController, config);

            // Pooled Obstacles Manager
            var obstaclePoolObj = new GameObject("ObstaclePoolManager");
            ObstaclePoolManager obstaclePool = obstaclePoolObj.AddComponent<ObstaclePoolManager>();
            obstaclePool.ConfigureMaterials(
                mats.BuildingDark,
                mats.TrainBody,
                mats.NeonCyan,
                mats.NeonPurple,
                mats.HazardRed,
                mats.HazardAmber);

            // Pooled Energy Coins Manager
            var coinPoolObj = new GameObject("CoinPoolManager");
            CoinPoolManager coinPool = coinPoolObj.AddComponent<CoinPoolManager>();
            coinPool.ConfigureMaterials(mats.CoinGold, mats.NeonCyan);

            // Pooled Modular Endless Track Manager
            var trackObj = new GameObject("EndlessTrackManager");
            EndlessTrackManager trackManager = trackObj.AddComponent<EndlessTrackManager>();
            trackManager.Configure(
                config,
                playerObj.transform,
                obstaclePool,
                coinPool,
                mats.RoadDark,
                mats.NeonCyan,
                mats.NeonPurple,
                mats.BuildingDark);

            // Phase 2 Core Run Orchestrator
            var runManagerObj = new GameObject("CoreRunManager");
            CoreRunManager runManager = runManagerObj.AddComponent<CoreRunManager>();
            runManager.Configure(config, playerController, cameraController, trackManager, obstaclePool, coinPool);

            // Phase 2 Gameplay HUD + Instant Restart Overlay
            var hudObj = new GameObject("Phase2GameplayHud");
            Phase2GameplayHud hud = hudObj.AddComponent<Phase2GameplayHud>();
            hud.Configure(runManager, playerController, coinPool);

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
