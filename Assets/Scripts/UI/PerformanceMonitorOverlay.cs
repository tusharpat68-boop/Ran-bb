using System.Text;
using UnityEngine;
using UnityEngine.UI;
using CyberRunner.Collectibles;
using CyberRunner.Core;
using CyberRunner.Obstacles;
using CyberRunner.PowerUps;

namespace CyberRunner.UI
{
    /// <summary>
    /// Toggleable zero-GC on-device Performance Monitor displaying live FPS, frame time (ms),
    /// active graphics preset & render scale, managed heap vs device RAM, and active object pool counts.
    /// Toggle via Settings -> PERF OVERLAY.
    /// </summary>
    public sealed class PerformanceMonitorOverlay : MonoBehaviour
    {
        private ObstaclePoolManager obstaclePool;
        private CoinPoolManager coinPool;
        private PowerUpPoolManager powerUpPool;
        private Font uiFont;

        private GameObject badgeRoot;
        private Text telemetryText;
        private readonly StringBuilder sb = new StringBuilder(160);
        private float refreshTimer;
        private bool built;

        public void Initialize(
            Transform parentSafeArea,
            Font font,
            ObstaclePoolManager obstacles,
            CoinPoolManager coins,
            PowerUpPoolManager powerUps)
        {
            uiFont = font;
            obstaclePool = obstacles;
            coinPool = coins;
            powerUpPool = powerUps;

            if (!built)
            {
                BuildBadge(parentSafeArea);
                built = true;
            }

            SettingsManager settings = SettingsManager.EnsureInstance();
            settings.OnSettingsChanged -= SyncVisibility;
            settings.OnSettingsChanged += SyncVisibility;
            SyncVisibility();
        }

        private void OnDestroy()
        {
            if (SettingsManager.Instance != null)
            {
                SettingsManager.Instance.OnSettingsChanged -= SyncVisibility;
            }
        }

        private void SyncVisibility()
        {
            if (badgeRoot == null) return;
            bool show = SettingsManager.Instance != null && SettingsManager.Instance.ShowPerformanceOverlay;
            badgeRoot.SetActive(show);
        }

        private void Update()
        {
            if (badgeRoot == null || !badgeRoot.activeSelf || telemetryText == null)
            {
                return;
            }

            refreshTimer += Time.unscaledDeltaTime;
            if (refreshTimer < 0.35f)
            {
                return;
            }
            refreshTimer = 0f;

            PerformanceOptimizer perf = PerformanceOptimizer.Instance;
            SettingsManager set = SettingsManager.Instance;

            int fps = perf != null ? Mathf.RoundToInt(perf.SmoothedFps) : 60;
            float ms = perf != null ? perf.AverageFrameMs : 16.6f;
            int cap = set != null ? set.TargetFps : 60;
            string preset = set != null ? set.GraphicsPreset.ToString().ToUpperInvariant() : "MED";
            float scale = perf != null ? perf.ActiveRenderScale : 0.88f;
            float heapMb = perf != null ? perf.ManagedHeapMb : 0f;
            int sysRam = perf != null ? perf.DeviceSystemRamMb : SystemInfo.systemMemorySize;

            int obsCount = obstaclePool != null ? obstaclePool.ActiveCount : 0;
            int coinCount = coinPool != null ? coinPool.ActiveCount : 0;
            int powCount = powerUpPool != null ? powerUpPool.ActiveCount : 0;

            sb.Clear();
            sb.Append("PERF  ").Append(fps).Append('/').Append(cap).Append(" FPS (")
              .Append(ms.ToString("F1")).Append(" ms)  |  ")
              .Append(preset).Append(' ').Append(Mathf.RoundToInt(scale * 100f)).Append("%\n");
            sb.Append("HEAP: ").Append(heapMb.ToString("F1")).Append(" MB / RAM: ")
              .Append(sysRam).Append(" MB  |  POOLS O:")
              .Append(obsCount).Append(" C:").Append(coinCount).Append(" P:").Append(powCount);

            if (perf != null && perf.LowMemoryTriggered)
            {
                sb.Append(" [LOW-MEM]");
            }

            telemetryText.text = sb.ToString();
        }

        private void BuildBadge(Transform parentSafeArea)
        {
            var go = new GameObject("PerformanceMonitorBadge");
            go.transform.SetParent(parentSafeArea, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.04f, 0.028f);
            rt.anchorMax = new Vector2(0.70f, 0.105f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            Image bg = go.AddComponent<Image>();
            bg.color = new Color(0.02f, 0.05f, 0.11f, 0.80f);
            bg.raycastTarget = false;

            Outline outl = go.AddComponent<Outline>();
            outl.effectColor = new Color(0.18f, 1.0f, 0.55f, 0.85f);
            outl.effectDistance = new Vector2(1.5f, -1.5f);

            var txtGo = new GameObject("TelemetryText");
            txtGo.transform.SetParent(go.transform, false);
            RectTransform tRt = txtGo.AddComponent<RectTransform>();
            tRt.anchorMin = new Vector2(0.03f, 0.05f);
            tRt.anchorMax = new Vector2(0.97f, 0.95f);
            tRt.offsetMin = Vector2.zero;
            tRt.offsetMax = Vector2.zero;

            telemetryText = txtGo.AddComponent<Text>();
            telemetryText.font = uiFont;
            telemetryText.fontSize = 18;
            telemetryText.color = new Color(0.24f, 1.0f, 0.62f, 1f);
            telemetryText.alignment = TextAnchor.MiddleLeft;
            telemetryText.raycastTarget = false;

            badgeRoot = go;
            badgeRoot.SetActive(false);
        }
    }
}
