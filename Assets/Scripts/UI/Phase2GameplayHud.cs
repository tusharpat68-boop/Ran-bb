using System.Text;
using UnityEngine;
using UnityEngine.UI;
using CyberRunner.Collectibles;
using CyberRunner.Core;
using CyberRunner.Gameplay;
using CyberRunner.Player;

namespace CyberRunner.UI
{
    /// <summary>
    /// Phase 2 futuristic dark-navy & neon-cyan mobile HUD and Crash/Restart overlay.
    /// Displays live distance, speed, Energy Coins, active 3-lane indicator, FPS,
    /// stumble/slide feedback, FPS cap toggle, and instant zero-lag RUN AGAIN flow.
    /// </summary>
    public sealed class Phase2GameplayHud : MonoBehaviour
    {
        [SerializeField] private CoreRunManager runManager;
        [SerializeField] private PlayerController playerController;
        [SerializeField] private CoinPoolManager coinPool;

        private Text distanceText;
        private Text speedFpsText;
        private Text coinsText;
        private Text laneText;
        private Text statusToastText;
        private Text fpsToggleLabel;

        private GameObject crashPanel;
        private Text crashCauseText;
        private Text crashStatsText;

        private readonly StringBuilder sb = new StringBuilder(160);
        private Font uiFont;

        private float fpsTimer;
        private int fpsFrameCount;
        private float currentFps;
        private float hudRefreshTimer;
        private float toastTimer;

        public void Configure(CoreRunManager manager, PlayerController player, CoinPoolManager coins)
        {
            runManager = manager;
            playerController = player;
            coinPool = coins;
        }

        private void Awake()
        {
            if (runManager == null) runManager = FindFirstObjectByType<CoreRunManager>();
            if (playerController == null) playerController = FindFirstObjectByType<PlayerController>();
            if (coinPool == null) coinPool = FindFirstObjectByType<CoinPoolManager>();

            ResolveFont();
            BuildHudCanvas();
        }

        private void OnEnable()
        {
            if (runManager != null)
            {
                runManager.OnRunStarted += HandleRunStarted;
                runManager.OnPlayerStumbled += HandleStumbled;
                runManager.OnRunEnded += HandleRunEnded;
            }

            if (coinPool != null)
            {
                coinPool.OnCoinCountChanged += HandleCoinsChanged;
            }

            if (playerController != null)
            {
                playerController.OnLaneChanged += HandleLaneChanged;
                playerController.OnSlideStarted += HandleSlideStarted;
            }
        }

        private void OnDisable()
        {
            if (runManager != null)
            {
                runManager.OnRunStarted -= HandleRunStarted;
                runManager.OnPlayerStumbled -= HandleStumbled;
                runManager.OnRunEnded -= HandleRunEnded;
            }

            if (coinPool != null)
            {
                coinPool.OnCoinCountChanged -= HandleCoinsChanged;
            }

            if (playerController != null)
            {
                playerController.OnLaneChanged -= HandleLaneChanged;
                playerController.OnSlideStarted -= HandleSlideStarted;
            }
        }

        private void Update()
        {
            fpsFrameCount++;
            fpsTimer += Time.unscaledDeltaTime;
            if (fpsTimer >= 0.4f)
            {
                currentFps = fpsFrameCount / fpsTimer;
                fpsFrameCount = 0;
                fpsTimer = 0f;
            }

            if (toastTimer > 0f)
            {
                toastTimer -= Time.unscaledDeltaTime;
                if (toastTimer <= 0f && statusToastText != null)
                {
                    statusToastText.text = string.Empty;
                }
            }

            hudRefreshTimer += Time.unscaledDeltaTime;
            if (hudRefreshTimer >= 0.1f)
            {
                hudRefreshTimer = 0f;
                RefreshLiveTelemetry();
            }
        }

        private void RefreshLiveTelemetry()
        {
            if (playerController == null)
            {
                return;
            }

            if (distanceText != null)
            {
                sb.Clear();
                sb.Append(Mathf.FloorToInt(playerController.DistanceTraveled)).Append(" m");
                distanceText.text = sb.ToString();
            }

            if (speedFpsText != null)
            {
                sb.Clear();
                sb.Append(playerController.CurrentForwardSpeed.ToString("F1")).Append(" m/s  |  ");
                sb.Append(Mathf.RoundToInt(currentFps)).Append(" FPS");
                speedFpsText.text = sb.ToString();
            }
        }

        private void HandleRunStarted()
        {
            if (crashPanel != null)
            {
                crashPanel.SetActive(false);
            }

            HandleCoinsChanged(coinPool != null ? coinPool.RunCoins : 0, coinPool != null ? coinPool.TotalCoins : 0);
            HandleLaneChanged(playerController != null ? playerController.CurrentLane : 0);
            ShowToast("SWIPE: L/R LANES  |  UP JUMP  |  DOWN SLIDE", new Color(0.0f, 0.94f, 1.0f, 1f), 2.4f);
        }

        private void HandleCoinsChanged(int runCoins, int totalCoins)
        {
            if (coinsText == null)
            {
                return;
            }

            sb.Clear();
            sb.Append("ENERGY: ").Append(runCoins);
            coinsText.text = sb.ToString();
        }

        private void HandleLaneChanged(int lane)
        {
            if (laneText == null)
            {
                return;
            }

            laneText.text = lane < 0 ? "[ L ]   C    R " : (lane > 0 ? " L    C   [ R ]" : " L   [ C ]   R ");
        }

        private void HandleSlideStarted()
        {
            ShowToast("CYBER SLIDE", new Color(0.68f, 0.35f, 1.0f, 1f), 0.65f);
        }

        private void HandleStumbled(string obstacleName)
        {
            ShowToast("STUMBLE! WATCH OUT", new Color(1.0f, 0.62f, 0.08f, 1f), 1.2f);
        }

        private void HandleRunEnded(Phase2RunSummary summary)
        {
            if (crashPanel == null)
            {
                return;
            }

            if (crashCauseText != null)
            {
                crashCauseText.text = $"IMPACT: {summary.CrashCause}";
            }

            if (crashStatsText != null)
            {
                sb.Clear();
                sb.Append("DISTANCE: ").Append(summary.DistanceMeters).Append(" m\n");
                sb.Append("BEST DISTANCE: ").Append(summary.BestDistanceMeters).Append(" m\n");
                sb.Append("ENERGY COLLECTED: +").Append(summary.RunCoins).Append("\n");
                sb.Append("TOTAL ENERGY: ").Append(summary.TotalCoins);
                crashStatsText.text = sb.ToString();
            }

            crashPanel.SetActive(true);
        }

        private void ShowToast(string message, Color color, float duration)
        {
            if (statusToastText == null)
            {
                return;
            }

            statusToastText.text = message;
            statusToastText.color = color;
            toastTimer = duration;
        }

        private void OnRestartClicked()
        {
            if (runManager != null)
            {
                runManager.RestartRun();
            }
        }

        private void OnToggleFpsClicked()
        {
            int nextFps = Application.targetFrameRate <= 30 ? 60 : 30;
            if (AppLifecycleManager.Instance != null)
            {
                AppLifecycleManager.Instance.SetTargetFrameRate(nextFps);
            }
            else
            {
                Application.targetFrameRate = nextFps;
            }

            UpdateFpsButtonLabel();
        }

        private void UpdateFpsButtonLabel()
        {
            if (fpsToggleLabel != null)
            {
                int cap = Application.targetFrameRate <= 30 ? 30 : 60;
                fpsToggleLabel.text = cap == 30 ? "30 FPS" : "60 FPS";
            }
        }

        private void ResolveFont()
        {
            uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (uiFont == null)
            {
                uiFont = Font.CreateDynamicFontFromOSFont("sans-serif", 28);
            }
        }

        private void BuildHudCanvas()
        {
            var canvasGo = new GameObject("Phase2HudCanvas");
            canvasGo.transform.SetParent(transform, false);

            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            canvasGo.AddComponent<GraphicRaycaster>();

            RectTransform safeRoot = CreatePanel("SafeAreaRoot", canvasGo.transform, Color.clear);
            ApplySafeArea(safeRoot);

            // Top HUD Card (clean, non-blocking)
            RectTransform topBar = CreatePanel("TopBar", safeRoot, new Color(0.03f, 0.06f, 0.13f, 0.78f));
            topBar.anchorMin = new Vector2(0.04f, 0.885f);
            topBar.anchorMax = new Vector2(0.96f, 0.985f);

            RectTransform accentLine = CreatePanel("HeaderCyanAccent", topBar, new Color(0.0f, 0.94f, 1.0f, 0.95f));
            accentLine.anchorMin = new Vector2(0f, 0f);
            accentLine.anchorMax = new Vector2(1f, 0.035f);

            distanceText = CreateText("DistanceText", topBar, "0 m", 44, Color.white, TextAnchor.UpperLeft);
            distanceText.rectTransform.anchorMin = new Vector2(0.03f, 0.44f);
            distanceText.rectTransform.anchorMax = new Vector2(0.48f, 0.94f);

            speedFpsText = CreateText("SpeedFpsText", topBar, "14.0 m/s  |  60 FPS", 23, new Color(0.75f, 0.88f, 1.0f, 0.92f), TextAnchor.LowerLeft);
            speedFpsText.rectTransform.anchorMin = new Vector2(0.03f, 0.08f);
            speedFpsText.rectTransform.anchorMax = new Vector2(0.56f, 0.44f);

            coinsText = CreateText("CoinsText", topBar, "ENERGY: 0", 36, new Color(1.0f, 0.84f, 0.14f, 1f), TextAnchor.UpperRight);
            coinsText.rectTransform.anchorMin = new Vector2(0.50f, 0.46f);
            coinsText.rectTransform.anchorMax = new Vector2(0.97f, 0.94f);

            laneText = CreateText("LaneText", topBar, " L   [ C ]   R ", 25, new Color(0.0f, 0.94f, 1.0f, 1f), TextAnchor.LowerRight);
            laneText.rectTransform.anchorMin = new Vector2(0.56f, 0.08f);
            laneText.rectTransform.anchorMax = new Vector2(0.97f, 0.44f);

            // Center non-blocking toast
            statusToastText = CreateText("StatusToast", safeRoot, string.Empty, 30, new Color(0.0f, 0.94f, 1.0f, 1f), TextAnchor.MiddleCenter);
            statusToastText.rectTransform.anchorMin = new Vector2(0.06f, 0.78f);
            statusToastText.rectTransform.anchorMax = new Vector2(0.94f, 0.85f);

            // Compact bottom-left FPS cap toggle button
            fpsToggleLabel = CreateActionButton(
                "FpsToggleButton",
                safeRoot,
                new Vector2(0.04f, 0.025f),
                new Vector2(0.26f, 0.082f),
                new Color(0.08f, 0.06f, 0.18f, 0.82f),
                new Color(0.68f, 0.22f, 1.0f, 0.95f),
                "60 FPS",
                26,
                OnToggleFpsClicked);
            UpdateFpsButtonLabel();

            BuildCrashOverlay(safeRoot);
        }

        private void BuildCrashOverlay(Transform safeRoot)
        {
            RectTransform backdrop = CreatePanel("CrashOverlay", safeRoot, new Color(0.02f, 0.03f, 0.08f, 0.84f));
            crashPanel = backdrop.gameObject;

            RectTransform card = CreatePanel("CrashCard", backdrop, new Color(0.04f, 0.08f, 0.17f, 0.96f));
            card.anchorMin = new Vector2(0.10f, 0.28f);
            card.anchorMax = new Vector2(0.90f, 0.74f);

            Outline cardBorder = card.gameObject.AddComponent<Outline>();
            cardBorder.effectColor = new Color(0.0f, 0.94f, 1.0f, 0.95f);
            cardBorder.effectDistance = new Vector2(3f, -3f);

            Text header = CreateText("CrashHeader", card, "RUN TERMINATED", 48, new Color(1.0f, 0.20f, 0.34f, 1f), TextAnchor.MiddleCenter);
            header.rectTransform.anchorMin = new Vector2(0.05f, 0.78f);
            header.rectTransform.anchorMax = new Vector2(0.95f, 0.94f);

            crashCauseText = CreateText("CrashCause", card, "IMPACT: OBSTACLE", 28, new Color(1.0f, 0.68f, 0.18f, 1f), TextAnchor.MiddleCenter);
            crashCauseText.rectTransform.anchorMin = new Vector2(0.05f, 0.65f);
            crashCauseText.rectTransform.anchorMax = new Vector2(0.95f, 0.77f);

            crashStatsText = CreateText("CrashStats", card, string.Empty, 32, Color.white, TextAnchor.MiddleCenter);
            crashStatsText.rectTransform.anchorMin = new Vector2(0.08f, 0.26f);
            crashStatsText.rectTransform.anchorMax = new Vector2(0.92f, 0.63f);

            CreateActionButton(
                "RunAgainButton",
                card,
                new Vector2(0.14f, 0.06f),
                new Vector2(0.86f, 0.22f),
                new Color(0.0f, 0.22f, 0.30f, 0.96f),
                new Color(0.0f, 0.94f, 1.0f, 1f),
                "RUN AGAIN",
                36,
                OnRestartClicked);

            crashPanel.SetActive(false);
        }

        private RectTransform CreatePanel(string name, Transform parent, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            if (color.a > 0f)
            {
                Image img = go.AddComponent<Image>();
                img.color = color;
                img.raycastTarget = name == "CrashOverlay";
            }

            return rt;
        }

        private Text CreateText(string name, Transform parent, string content, int fontSize, Color color, TextAnchor alignment)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            Text text = go.AddComponent<Text>();
            text.font = uiFont;
            text.text = content;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
            return text;
        }

        private Text CreateActionButton(
            string name,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Color fillColor,
            Color borderColor,
            string label,
            int fontSize,
            UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            Image bg = go.AddComponent<Image>();
            bg.color = fillColor;

            Outline border = go.AddComponent<Outline>();
            border.effectColor = borderColor;
            border.effectDistance = new Vector2(2f, -2f);

            Button btn = go.AddComponent<Button>();
            btn.targetGraphic = bg;
            btn.onClick.AddListener(onClick);

            Text text = CreateText("Label", go.transform, label, fontSize, Color.white, TextAnchor.MiddleCenter);
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            return text;
        }

        private static void ApplySafeArea(RectTransform rectTransform)
        {
            Rect safeArea = Screen.safeArea;
            if (Screen.width <= 0 || Screen.height <= 0)
            {
                return;
            }

            Vector2 anchorMin = safeArea.position;
            Vector2 anchorMax = safeArea.position + safeArea.size;
            anchorMin.x /= Screen.width;
            anchorMin.y /= Screen.height;
            anchorMax.x /= Screen.width;
            anchorMax.y /= Screen.height;

            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }
    }
}
