using System.Text;
using UnityEngine;
using UnityEngine.UI;
using CyberRunner.Core;
using CyberRunner.Player;

namespace CyberRunner.UI
{
    /// <summary>
    /// Lightweight Phase 1 cyberpunk telemetry HUD for verifying Android gameplay,
    /// swipe responsiveness, jump physics, and 30/60 FPS stability on real devices.
    /// Zero per-frame GC allocations after initial setup; safe-area aware for notched screens.
    /// </summary>
    public sealed class Phase1DebugHud : MonoBehaviour
    {
        [SerializeField] private PlayerController playerController;
        [SerializeField] private MobileInputController inputController;

        private Text distanceText;
        private Text telemetryText;
        private Text fpsToggleLabel;
        private readonly StringBuilder sb = new StringBuilder(160);

        private float fpsTimer;
        private int fpsFrameCount;
        private float currentFps;
        private float hudRefreshTimer;
        private Font uiFont;

        public void Configure(PlayerController player, MobileInputController input)
        {
            playerController = player;
            inputController = input;
        }

        private void Awake()
        {
            if (playerController == null)
            {
                playerController = FindFirstObjectByType<PlayerController>();
            }

            if (inputController == null)
            {
                inputController = FindFirstObjectByType<MobileInputController>();
            }

            ResolveFont();
            BuildHudCanvas();
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

            hudRefreshTimer += Time.unscaledDeltaTime;
            if (hudRefreshTimer >= 0.1f)
            {
                hudRefreshTimer = 0f;
                RefreshText();
            }
        }

        private void RefreshText()
        {
            if (playerController == null)
            {
                return;
            }

            if (distanceText != null)
            {
                sb.Clear();
                sb.Append((int)playerController.DistanceTraveled);
                sb.Append(" m");
                distanceText.text = sb.ToString();
            }

            if (telemetryText != null)
            {
                sb.Clear();
                sb.Append("STATE: ").Append(playerController.CurrentState);
                sb.Append("  |  SPD: ").Append(playerController.CurrentForwardSpeed.ToString("F1")).Append(" m/s\n");
                sb.Append("FPS: ").Append(Mathf.RoundToInt(currentFps));
                if (inputController != null)
                {
                    sb.Append("  |  INPUT: ").Append(inputController.LastInputLabel);
                }
                telemetryText.text = sb.ToString();
            }
        }

        private void OnJumpButtonClicked()
        {
            if (inputController != null)
            {
                inputController.TriggerJumpFromUI();
            }
            else if (playerController != null)
            {
                playerController.RequestJump();
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
                int currentCap = Application.targetFrameRate <= 30 ? 30 : 60;
                fpsToggleLabel.text = currentCap == 30 ? "CAP: 30 FPS" : "CAP: 60 FPS";
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
            var canvasGo = new GameObject("Phase1HudCanvas");
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

            // Safe-area container so notches/cutouts never overlap HUD elements.
            RectTransform safeRoot = CreatePanel("SafeAreaRoot", canvasGo.transform, Color.clear);
            ApplySafeArea(safeRoot);

            // Top cyberpunk status header bar
            RectTransform topBar = CreatePanel("TopBar", safeRoot, new Color(0.03f, 0.06f, 0.13f, 0.78f));
            topBar.anchorMin = new Vector2(0.04f, 0.875f);
            topBar.anchorMax = new Vector2(0.96f, 0.985f);
            topBar.offsetMin = Vector2.zero;
            topBar.offsetMax = Vector2.zero;

            // Neon cyan bottom accent strip on header
            RectTransform accentLine = CreatePanel("HeaderCyanAccent", topBar, new Color(0.0f, 0.94f, 1.0f, 0.95f));
            accentLine.anchorMin = new Vector2(0f, 0f);
            accentLine.anchorMax = new Vector2(1f, 0.035f);
            accentLine.offsetMin = Vector2.zero;
            accentLine.offsetMax = Vector2.zero;

            // Title & Distance
            Text titleText = CreateText("TitleText", topBar, "CYBER RUNNER  //  PHASE 1", 26, new Color(0.0f, 0.94f, 1.0f, 1f), TextAnchor.UpperLeft);
            titleText.rectTransform.anchorMin = new Vector2(0.03f, 0.62f);
            titleText.rectTransform.anchorMax = new Vector2(0.68f, 0.94f);

            distanceText = CreateText("DistanceText", topBar, "0 m", 42, Color.white, TextAnchor.UpperRight);
            distanceText.rectTransform.anchorMin = new Vector2(0.66f, 0.52f);
            distanceText.rectTransform.anchorMax = new Vector2(0.97f, 0.95f);

            telemetryText = CreateText("TelemetryText", topBar, "STATE: Running | SPD: 14.0 m/s", 24, new Color(0.80f, 0.90f, 1.0f, 0.95f), TextAnchor.LowerLeft);
            telemetryText.rectTransform.anchorMin = new Vector2(0.03f, 0.08f);
            telemetryText.rectTransform.anchorMax = new Vector2(0.97f, 0.56f);

            // Bottom-left 30/60 FPS cap button (for testing low-end Android performance target)
            fpsToggleLabel = CreateActionButton(
                "FpsToggleButton",
                safeRoot,
                new Vector2(0.04f, 0.03f),
                new Vector2(0.42f, 0.105f),
                new Color(0.10f, 0.07f, 0.22f, 0.85f),
                new Color(0.68f, 0.22f, 1.0f, 1f),
                "CAP: 60 FPS",
                OnToggleFpsClicked);
            UpdateFpsButtonLabel();

            // Bottom-right JUMP button (supplement to Swipe Up for accessibility & quick verification)
            CreateActionButton(
                "JumpButton",
                safeRoot,
                new Vector2(0.60f, 0.03f),
                new Vector2(0.96f, 0.105f),
                new Color(0.02f, 0.16f, 0.24f, 0.88f),
                new Color(0.0f, 0.94f, 1.0f, 1f),
                "SWIPE UP / JUMP",
                OnJumpButtonClicked);
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
                img.raycastTarget = false;
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

            Text text = CreateText("Label", go.transform, label, 28, Color.white, TextAnchor.MiddleCenter);
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
