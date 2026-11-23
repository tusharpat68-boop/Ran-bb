using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CyberRunner.Core
{
    public enum GraphicsQualityPreset
    {
        Low = 0,
        Medium = 1,
        High = 2
    }

    public enum SwipeSensitivityPreset
    {
        Low = 0,
        Normal = 1,
        High = 2
    }

    /// <summary>
    /// Persistent player settings manager for audio volumes, vibration, FPS target,
    /// graphics quality preset (Low/Medium/High), and touch swipe sensitivity.
    /// </summary>
    public sealed class SettingsManager : MonoBehaviour
    {
        private const string MusicVolKey = "CR_Set_MusicVol";
        private const string SfxVolKey = "CR_Set_SfxVol";
        private const string MusicMuteKey = "CR_Set_MusicMute";
        private const string SfxMuteKey = "CR_Set_SfxMute";
        private const string VibKey = "CR_Set_Vibration";
        private const string FpsKey = "CR_Set_TargetFps";
        private const string GraphicsKey = "CR_Set_Graphics";
        private const string SwipeKey = "CR_Set_SwipeSens";
        private const string PerfOverlayKey = "CR_Set_PerfOverlay";

        public static SettingsManager Instance { get; private set; }

        public event Action OnSettingsChanged;

        public float MusicVolume { get; private set; } = 0.70f;
        public float SfxVolume { get; private set; } = 0.85f;
        public bool MusicMuted { get; private set; }
        public bool SfxMuted { get; private set; }
        public bool VibrationEnabled { get; private set; } = true;
        public int TargetFps { get; private set; } = 60;
        public GraphicsQualityPreset GraphicsPreset { get; private set; } = GraphicsQualityPreset.Medium;
        public SwipeSensitivityPreset SwipeSensitivity { get; private set; } = SwipeSensitivityPreset.Normal;
        public bool ShowPerformanceOverlay { get; private set; }

        public float EffectiveMusicVolume => MusicMuted ? 0f : MusicVolume;
        public float EffectiveSfxVolume => SfxMuted ? 0f : SfxVolume;

        public float SwipeThresholdMultiplier
        {
            get
            {
                switch (SwipeSensitivity)
                {
                    case SwipeSensitivityPreset.Low: return 1.25f;
                    case SwipeSensitivityPreset.High: return 0.75f;
                    default: return 1.0f;
                }
            }
        }

        public static SettingsManager EnsureInstance()
        {
            if (Instance != null) return Instance;
            SettingsManager existing = FindFirstObjectByType<SettingsManager>();
            if (existing != null)
            {
                Instance = existing;
                return existing;
            }

            var go = new GameObject("SettingsManager");
            return go.AddComponent<SettingsManager>();
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
            LoadSettings();
            ApplySystemSettings();
        }

        public void CycleMusicVolume()
        {
            if (MusicMuted)
            {
                MusicMuted = false;
                MusicVolume = 0.40f;
            }
            else if (MusicVolume < 0.55f) MusicVolume = 0.70f;
            else if (MusicVolume < 0.85f) MusicVolume = 1.0f;
            else MusicMuted = true;

            SaveAndNotify();
        }

        public void CycleSfxVolume()
        {
            if (SfxMuted)
            {
                SfxMuted = false;
                SfxVolume = 0.50f;
            }
            else if (SfxVolume < 0.70f) SfxVolume = 0.85f;
            else if (SfxVolume < 0.95f) SfxVolume = 1.0f;
            else SfxMuted = true;

            SaveAndNotify();
        }

        public void ToggleVibration()
        {
            VibrationEnabled = !VibrationEnabled;
            SaveAndNotify();
        }

        public void ToggleTargetFps()
        {
            TargetFps = TargetFps <= 30 ? 60 : 30;
            ApplySystemSettings();
            SaveAndNotify();
        }

        public void SetTargetFps(int fps)
        {
            TargetFps = fps <= 30 ? 30 : 60;
            ApplySystemSettings();
            SaveAndNotify();
        }

        public void CycleGraphicsPreset()
        {
            GraphicsPreset = (GraphicsQualityPreset)(((int)GraphicsPreset + 1) % 3);
            ApplySystemSettings();
            SaveAndNotify();
        }

        public void CycleSwipeSensitivity()
        {
            SwipeSensitivity = (SwipeSensitivityPreset)(((int)SwipeSensitivity + 1) % 3);
            SaveAndNotify();
        }

        public void TogglePerformanceOverlay()
        {
            ShowPerformanceOverlay = !ShowPerformanceOverlay;
            SaveAndNotify();
        }

        public void ApplyHardwareRecommendedDefaults(GraphicsQualityPreset preset, int targetFps)
        {
            GraphicsPreset = preset;
            TargetFps = targetFps <= 30 ? 30 : 60;
            ApplySystemSettings();
            SaveAndNotify();
        }

        private void LoadSettings()
        {
            MusicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MusicVolKey, 0.70f));
            SfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(SfxVolKey, 0.85f));
            MusicMuted = PlayerPrefs.GetInt(MusicMuteKey, 0) == 1;
            SfxMuted = PlayerPrefs.GetInt(SfxMuteKey, 0) == 1;
            VibrationEnabled = PlayerPrefs.GetInt(VibKey, 1) == 1;
            TargetFps = PlayerPrefs.GetInt(FpsKey, 60) <= 30 ? 30 : 60;
            GraphicsPreset = (GraphicsQualityPreset)Mathf.Clamp(PlayerPrefs.GetInt(GraphicsKey, 1), 0, 2);
            SwipeSensitivity = (SwipeSensitivityPreset)Mathf.Clamp(PlayerPrefs.GetInt(SwipeKey, 1), 0, 2);
            ShowPerformanceOverlay = PlayerPrefs.GetInt(PerfOverlayKey, 0) == 1;
        }

        private void ApplySystemSettings()
        {
            if (AppLifecycleManager.Instance != null)
            {
                AppLifecycleManager.Instance.SetTargetFrameRate(TargetFps);
            }
            else
            {
                Application.targetFrameRate = TargetFps;
            }

            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                switch (GraphicsPreset)
                {
                    case GraphicsQualityPreset.Low:
                        urp.renderScale = 0.72f;
                        urp.msaaSampleCount = 1;
                        break;
                    case GraphicsQualityPreset.Medium:
                        urp.renderScale = 0.88f;
                        urp.msaaSampleCount = 1;
                        break;
                    case GraphicsQualityPreset.High:
                        urp.renderScale = 1.0f;
                        urp.msaaSampleCount = 2;
                        break;
                }
            }
        }

        private void SaveAndNotify()
        {
            PlayerPrefs.SetFloat(MusicVolKey, MusicVolume);
            PlayerPrefs.SetFloat(SfxVolKey, SfxVolume);
            PlayerPrefs.SetInt(MusicMuteKey, MusicMuted ? 1 : 0);
            PlayerPrefs.SetInt(SfxMuteKey, SfxMuted ? 1 : 0);
            PlayerPrefs.SetInt(VibKey, VibrationEnabled ? 1 : 0);
            PlayerPrefs.SetInt(FpsKey, TargetFps);
            PlayerPrefs.SetInt(GraphicsKey, (int)GraphicsPreset);
            PlayerPrefs.SetInt(SwipeKey, (int)SwipeSensitivity);
            PlayerPrefs.SetInt(PerfOverlayKey, ShowPerformanceOverlay ? 1 : 0);
            PlayerPrefs.Save();

            OnSettingsChanged?.Invoke();
        }
    }
}
