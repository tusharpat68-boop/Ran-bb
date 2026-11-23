using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using CyberRunner.Audio;
using CyberRunner.Progression;

namespace CyberRunner.Core
{
    /// <summary>
    /// Entry-point scene loader for Bootstrap.unity with a sleek cyberpunk boot splash banner.
    /// Initializes persistent singletons (AppLifecycleManager, SaveSystem, SettingsManager,
    /// PerformanceOptimizer, CyberAudioManager) cleanly before loading Gameplay (Main Menu).
    /// </summary>
    public sealed class BootstrapLoader : MonoBehaviour
    {
        [SerializeField] private string initialSceneName = "Gameplay";
        [SerializeField] private GameConfig defaultConfig;
        [SerializeField] private float minimumSplashDuration = 0.75f;

        private Image progressBarFill;

        private IEnumerator Start()
        {
            BuildBootSplashUi();

            float startTime = Time.unscaledTime;
            SetProgress(0.20f);

            EnsureLifecycleManager();
            SaveSystem.EnsureInstance();
            SetProgress(0.45f);

            SettingsManager settings = SettingsManager.EnsureInstance();
            PerformanceOptimizer.EnsureInstance();
            SetProgress(0.70f);

            CyberAudioManager.EnsureInstance();
            if (AppLifecycleManager.Instance != null)
            {
                int targetFps = settings != null ? settings.TargetFps : (defaultConfig != null ? defaultConfig.defaultTargetFps : 60);
                AppLifecycleManager.Instance.SetTargetFrameRate(targetFps);
            }
            SetProgress(0.92f);

            while (Time.unscaledTime - startTime < minimumSplashDuration)
            {
                float u = Mathf.Clamp01((Time.unscaledTime - startTime) / Mathf.Max(0.05f, minimumSplashDuration));
                SetProgress(Mathf.Lerp(0.70f, 1.0f, u));
                yield return null;
            }

            SetProgress(1.0f);

            if (!string.IsNullOrEmpty(initialSceneName) && Application.CanStreamedLevelBeLoaded(initialSceneName))
            {
                SceneManager.LoadScene(initialSceneName, LoadSceneMode.Single);
            }
            else if (SceneManager.sceneCountInBuildSettings > 1)
            {
                SceneManager.LoadScene(1, LoadSceneMode.Single);
            }
        }

        private void SetProgress(float normalized)
        {
            if (progressBarFill != null)
            {
                progressBarFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(normalized), 1f);
            }
        }

        private void BuildBootSplashUi()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                ?? Font.CreateDynamicFontFromOSFont("sans-serif", 32);

            var canvasGo = new GameObject("BootSplashCanvas");
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;

            var bgGo = new GameObject("Backdrop");
            bgGo.transform.SetParent(canvasGo.transform, false);
            RectTransform bgRt = bgGo.AddComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;
            bgGo.AddComponent<Image>().color = new Color(0.025f, 0.045f, 0.11f, 1f);

            CreateBootText(bgGo.transform, font, "CYBER RUNNER", 68, new Color(0.0f, 0.96f, 1.0f, 1f),
                new Vector2(0.06f, 0.52f), new Vector2(0.94f, 0.64f));

            CreateBootText(bgGo.transform, font, "PROTOCOL 2099  //  INITIALIZING NEURAL GRID", 22, new Color(0.74f, 0.45f, 1.0f, 0.95f),
                new Vector2(0.06f, 0.45f), new Vector2(0.94f, 0.52f));

            // Sleek neon loading bar
            var trackGo = new GameObject("BarTrack");
            trackGo.transform.SetParent(bgGo.transform, false);
            RectTransform trackRt = trackGo.AddComponent<RectTransform>();
            trackRt.anchorMin = new Vector2(0.18f, 0.405f);
            trackRt.anchorMax = new Vector2(0.82f, 0.422f);
            trackRt.offsetMin = Vector2.zero;
            trackRt.offsetMax = Vector2.zero;
            trackGo.AddComponent<Image>().color = new Color(0.08f, 0.14f, 0.26f, 1f);

            var fillGo = new GameObject("BarFill");
            fillGo.transform.SetParent(trackGo.transform, false);
            RectTransform fillRt = fillGo.AddComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = new Vector2(0.1f, 1f);
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;
            progressBarFill = fillGo.AddComponent<Image>();
            progressBarFill.color = new Color(0.0f, 0.96f, 1.0f, 1f);

            CreateBootText(bgGo.transform, font, $"v{Application.version}", 20, new Color(0.55f, 0.70f, 0.88f, 0.78f),
                new Vector2(0.1f, 0.05f), new Vector2(0.9f, 0.10f));
        }

        private static void CreateBootText(Transform parent, Font font, string content, int size, Color color, Vector2 min, Vector2 max)
        {
            var go = new GameObject("BootText");
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            Text t = go.AddComponent<Text>();
            t.font = font;
            t.text = content;
            t.fontSize = size;
            t.color = color;
            t.alignment = TextAnchor.MiddleCenter;
            t.raycastTarget = false;
        }

        private static void EnsureLifecycleManager()
        {
            if (AppLifecycleManager.Instance != null)
            {
                return;
            }

            var existing = FindFirstObjectByType<AppLifecycleManager>();
            if (existing != null)
            {
                return;
            }

            var managerObject = new GameObject("AppLifecycleManager");
            managerObject.AddComponent<AppLifecycleManager>();
        }
    }
}
