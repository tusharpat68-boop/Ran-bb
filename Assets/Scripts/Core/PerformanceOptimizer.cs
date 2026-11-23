using System;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CyberRunner.Core
{
    /// <summary>
    /// Phase 7 Android Low-End Optimization Controller (2GB-3GB RAM target).
    /// Handles first-launch hardware auto-tiering, URP render scale / MSAA / texture mipmap /
    /// camera far-clip / fog scaling, ambient animation LOD, Android low-memory defense,
    /// safe between-run GC hygiene, and zero-GC rolling performance telemetry.
    /// </summary>
    public sealed class PerformanceOptimizer : MonoBehaviour
    {
        private const string HardwareAutoTunedKey = "CR_Perf_AutoTuned_v1";

        public static PerformanceOptimizer Instance { get; private set; }

        public event Action OnOptimizationProfileApplied;

        // Live Optimization Flags (queried by Camera, Atmosphere, TrackManager)
        public float CameraFarClipPlane { get; private set; } = 135f;
        public float FogDistanceScale { get; private set; } = 0.90f;
        public float ActiveRenderScale { get; private set; } = 0.88f;
        public bool EnableAmbientPropAnimations { get; private set; } = true;
        public bool LowMemoryTriggered { get; private set; }

        // Rolling Telemetry (zero-GC)
        public float SmoothedFps { get; private set; } = 60f;
        public float AverageFrameMs { get; private set; } = 16.6f;
        public float ManagedHeapMb { get; private set; }
        public int DeviceSystemRamMb { get; private set; }
        public string DeviceTierLabel { get; private set; } = "STANDARD";

        private bool pendingDeferredMemoryCleanup;
        private float sampleElapsed;
        private int sampleFrames;
        private Camera cachedMainCamera;

        public static PerformanceOptimizer EnsureInstance()
        {
            if (Instance != null) return Instance;
            PerformanceOptimizer existing = FindFirstObjectByType<PerformanceOptimizer>();
            if (existing != null)
            {
                Instance = existing;
                return existing;
            }

            var go = new GameObject("PerformanceOptimizer");
            return go.AddComponent<PerformanceOptimizer>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Global low-end mobile CPU & physics hygiene
            Physics.autoSyncTransforms = false;
            Application.backgroundLoadingPriority = ThreadPriority.Low;

            DeviceSystemRamMb = SystemInfo.systemMemorySize;
            DetectAndAutoTuneFirstLaunch();

            SettingsManager settings = SettingsManager.EnsureInstance();
            settings.OnSettingsChanged += ApplyActiveProfile;
            Application.lowMemory += HandleAndroidLowMemory;

            ApplyActiveProfile();
        }

        private void OnDestroy()
        {
            if (SettingsManager.Instance != null)
            {
                SettingsManager.Instance.OnSettingsChanged -= ApplyActiveProfile;
            }

            Application.lowMemory -= HandleAndroidLowMemory;

            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (dt <= 0f || dt > 0.5f) return;

            sampleFrames++;
            sampleElapsed += dt;

            if (sampleElapsed >= 0.40f)
            {
                SmoothedFps = sampleFrames / sampleElapsed;
                AverageFrameMs = (sampleElapsed / sampleFrames) * 1000f;
                ManagedHeapMb = Profiler.GetMonoUsedSizeLong() / (1024f * 1024f);
                sampleFrames = 0;
                sampleElapsed = 0f;
            }
        }

        public void RegisterMainCamera(Camera cam)
        {
            cachedMainCamera = cam;
            ApplyCameraClipPlane();
        }

        /// <summary>
        /// Applies Low/Medium/High mobile GPU & CPU budget to URP, QualitySettings, Camera, and Biome LODs.
        /// </summary>
        public void ApplyActiveProfile()
        {
            SettingsManager settings = SettingsManager.Instance;
            GraphicsQualityPreset preset = settings != null ? settings.GraphicsPreset : GraphicsQualityPreset.Medium;

            switch (preset)
            {
                case GraphicsQualityPreset.Low:
                    ActiveRenderScale = 0.72f;
                    CameraFarClipPlane = 105f;
                    FogDistanceScale = 0.74f;
                    EnableAmbientPropAnimations = false;
                    QualitySettings.globalTextureMipmapLimit = 1;
                    QualitySettings.shadows = ShadowQuality.Disable;
                    QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;
                    QualitySettings.lodBias = 0.65f;
                    break;

                case GraphicsQualityPreset.Medium:
                    ActiveRenderScale = 0.88f;
                    CameraFarClipPlane = 135f;
                    FogDistanceScale = 0.90f;
                    EnableAmbientPropAnimations = true;
                    QualitySettings.globalTextureMipmapLimit = 0;
                    QualitySettings.shadows = ShadowQuality.Disable;
                    QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;
                    QualitySettings.lodBias = 0.90f;
                    break;

                case GraphicsQualityPreset.High:
                    ActiveRenderScale = 1.00f;
                    CameraFarClipPlane = 165f;
                    FogDistanceScale = 1.00f;
                    EnableAmbientPropAnimations = true;
                    QualitySettings.globalTextureMipmapLimit = 0;
                    QualitySettings.shadows = ShadowQuality.Disable;
                    QualitySettings.anisotropicFiltering = AnisotropicFiltering.Enable;
                    QualitySettings.lodBias = 1.15f;
                    break;
            }

            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                urp.renderScale = ActiveRenderScale;
                urp.msaaSampleCount = preset == GraphicsQualityPreset.High ? 2 : 1;
                urp.supportsHDR = false;
                urp.shadowDistance = 0f;
                urp.supportsCameraDepthTexture = false;
                urp.supportsCameraOpaqueTexture = false;
            }

            ApplyCameraClipPlane();
            OnOptimizationProfileApplied?.Invoke();
        }

        /// <summary>
        /// Safely performs memory cleanup ONLY during non-gameplay states (Main Menu / Game Over)
        /// so gameplay never suffers a mid-run GC hitch.
        /// </summary>
        public void PerformSafeBetweenRunCleanup()
        {
            if (pendingDeferredMemoryCleanup || ManagedHeapMb > 85f)
            {
                pendingDeferredMemoryCleanup = false;
                Resources.UnloadUnusedAssets();
                GC.Collect(0, GCCollectionMode.Optimized, false);
            }
        }

        private void ApplyCameraClipPlane()
        {
            if (cachedMainCamera == null)
            {
                cachedMainCamera = Camera.main;
            }

            if (cachedMainCamera != null)
            {
                cachedMainCamera.farClipPlane = CameraFarClipPlane;
                cachedMainCamera.allowHDR = false;
                cachedMainCamera.allowMSAA = SettingsManager.Instance != null &&
                                             SettingsManager.Instance.GraphicsPreset == GraphicsQualityPreset.High;
            }
        }

        private void DetectAndAutoTuneFirstLaunch()
        {
            int ram = SystemInfo.systemMemorySize;
            int cores = SystemInfo.processorCount;

            if (ram > 0 && ram <= 3300)
            {
                DeviceTierLabel = "LOW-END (2-3GB)";
            }
            else if (ram > 0 && ram <= 4800)
            {
                DeviceTierLabel = "MID-RANGE (4GB)";
            }
            else
            {
                DeviceTierLabel = "HIGH-END (6GB+)";
            }

            // Only auto-select defaults on first install so manual user choices are always respected
            if (PlayerPrefs.GetInt(HardwareAutoTunedKey, 0) == 1)
            {
                return;
            }

            SettingsManager settings = SettingsManager.EnsureInstance();
            if (ram > 0 && (ram <= 3300 || cores <= 4))
            {
                settings.ApplyHardwareRecommendedDefaults(GraphicsQualityPreset.Low, targetFps: 30);
            }
            else if (ram > 0 && ram <= 4800)
            {
                settings.ApplyHardwareRecommendedDefaults(GraphicsQualityPreset.Medium, targetFps: 60);
            }
            else
            {
                settings.ApplyHardwareRecommendedDefaults(GraphicsQualityPreset.High, targetFps: 60);
            }

            PlayerPrefs.SetInt(HardwareAutoTunedKey, 1);
            PlayerPrefs.Save();
        }

        private void HandleAndroidLowMemory()
        {
            LowMemoryTriggered = true;
            pendingDeferredMemoryCleanup = true;

            // Step down to Low graphics automatically to prevent Android OOM kill on 2GB phones
            if (SettingsManager.Instance != null &&
                SettingsManager.Instance.GraphicsPreset != GraphicsQualityPreset.Low)
            {
                SettingsManager.Instance.ApplyHardwareRecommendedDefaults(GraphicsQualityPreset.Low, targetFps: 30);
            }

            Debug.LogWarning("[PerformanceOptimizer] Android lowMemory signal received -> stepped down to LOW preset & queued safe cleanup.");
        }
    }
}
