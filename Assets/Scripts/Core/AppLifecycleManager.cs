using System;
using UnityEngine;

namespace CyberRunner.Core
{
    /// <summary>
    /// Manages Android application lifecycle, screen orientation, wake-lock,
    /// target frame rate, and pause/focus interruption events.
    /// Persists across scene loads as a lightweight singleton.
    /// </summary>
    public sealed class AppLifecycleManager : MonoBehaviour
    {
        public static AppLifecycleManager Instance { get; private set; }

        [SerializeField] private int targetFrameRate = 60;
        [SerializeField] private bool preventScreenSleep = true;

        /// <summary>
        /// Fired when Android suspends or resumes the app (incoming call, home button, notification shade).
        /// </summary>
        public event Action<bool> OnAppPauseStateChanged;

        /// <summary>
        /// Fired when application focus changes.
        /// </summary>
        public event Action<bool> OnAppFocusStateChanged;

        public int CurrentTargetFrameRate => targetFrameRate;
        public bool IsAppPaused { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            ApplyMobileRuntimeSettings();
        }

        private void ApplyMobileRuntimeSettings()
        {
            // Lock to Portrait orientation as required for CYBER RUNNER on Android.
            Screen.orientation = ScreenOrientation.Portrait;
            Screen.autorotateToPortrait = true;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = false;
            Screen.autorotateToLandscapeRight = false;

            Screen.sleepTimeout = preventScreenSleep
                ? SleepTimeout.NeverSleep
                : SleepTimeout.SystemSetting;

            // Disable VSync so Application.targetFrameRate governs 30/60 FPS presets cleanly on Android.
            QualitySettings.vSyncCount = 0;
            SetTargetFrameRate(targetFrameRate);
        }

        /// <summary>
        /// Updates the target frame rate cleanly (typically 30 for low-end 2GB RAM devices or 60 for supported devices).
        /// </summary>
        public void SetTargetFrameRate(int fps)
        {
            targetFrameRate = fps <= 30 ? 30 : 60;
            Application.targetFrameRate = targetFrameRate;
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            IsAppPaused = pauseStatus;
            OnAppPauseStateChanged?.Invoke(pauseStatus);
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            OnAppFocusStateChanged?.Invoke(hasFocus);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
