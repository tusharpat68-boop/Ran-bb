using System.Text;
using UnityEngine;
using UnityEngine.UI;
using CyberRunner.Audio;
using CyberRunner.Collectibles;
using CyberRunner.Core;
using CyberRunner.Environment;
using CyberRunner.Gameplay;
using CyberRunner.Player;
using CyberRunner.PowerUps;
using CyberRunner.Progression;

namespace CyberRunner.UI
{
    /// <summary>
    /// Phase 5 futuristic in-game HUD, Active Power-Up timers, Special Ability Button,
    /// Pause Overlay, Game Over Summary, and Cyber Hub Progression Overlay (Skins, Upgrades,
    /// Missions, Daily Rewards, Achievements, Local Leaderboard).
    /// Zero per-frame GC allocations; safe-area aware for all Android portrait displays.
    /// </summary>
    public sealed class Phase2GameplayHud : MonoBehaviour
    {
        [SerializeField] private CoreRunManager runManager;
        [SerializeField] private PlayerController playerController;
        [SerializeField] private CoinPoolManager coinPool;
        [SerializeField] private PowerUpManager powerUpManager;
        [SerializeField] private ScoreManager scoreManager;
        [SerializeField] private DifficultyManager difficultyManager;
        [SerializeField] private ProgressionManager progressionManager;

        // Live HUD elements
        private GameObject gameplayHudRoot;
        private Text scoreText;
        private Text multiplierTierText;
        private Text distanceText;
        private Text speedFpsText;
        private Text coinsText;
        private Text sectorBadgeText;
        private Image headerAccentLine;
        private Text statusToastText;
        private Text modeTimerText;

        // Active power-up indicator rows (pre-allocated pool of 3 clean slots)
        private GameObject[] powerSlotRoots;
        private Text[] powerSlotLabels;
        private Image[] powerSlotBars;

        // Bottom Special Ability Button
        private Image abilityButtonBg;
        private Outline abilityButtonBorder;
        private Text abilityButtonLabel;

        // Pause Overlay
        private GameObject pausePanel;
        private Text pauseStatsText;
        private Text pauseFpsLabel;
        private Text pauseModeLabel;
        private Text pauseAbilityLabel;
        private Text pauseHubLabel;

        // Game Over Overlay
        private GameObject gameOverPanel;
        private Text gameOverHeaderText;
        private Text gameOverCauseText;
        private Text gameOverStatsText;
        private Text gameOverHubLabel;

        // Phase 5, 6 & 7 Overlays
        private ProgressionOverlayPanel progressionOverlay;
        private MainMenuOverlayPanel mainMenuOverlay;
        private SettingsOverlayPanel settingsOverlay;
        private PerformanceMonitorOverlay performanceOverlay;

        private readonly StringBuilder sb = new StringBuilder(256);
        private Font uiFont;

        private float fpsTimer;
        private int fpsFrameCount;
        private float currentFps;
        private float hudRefreshTimer;
        private float toastTimer;

        public void Configure(
            CoreRunManager manager,
            PlayerController player,
            CoinPoolManager coins,
            PowerUpManager powerUps = null,
            ScoreManager score = null,
            DifficultyManager difficulty = null,
            ProgressionManager progression = null)
        {
            runManager = manager;
            playerController = player;
            coinPool = coins;
            powerUpManager = powerUps;
            scoreManager = score;
            difficultyManager = difficulty;
            progressionManager = progression;
        }

        private void Awake()
        {
            if (runManager == null) runManager = FindFirstObjectByType<CoreRunManager>();
            if (playerController == null) playerController = FindFirstObjectByType<PlayerController>();
            if (coinPool == null) coinPool = FindFirstObjectByType<CoinPoolManager>();
            if (powerUpManager == null) powerUpManager = FindFirstObjectByType<PowerUpManager>();
            if (scoreManager == null) scoreManager = FindFirstObjectByType<ScoreManager>();
            if (difficultyManager == null) difficultyManager = FindFirstObjectByType<DifficultyManager>();
            if (progressionManager == null) progressionManager = FindFirstObjectByType<ProgressionManager>();

            ResolveFont();
            BuildHudCanvas();
        }

        private void OnEnable()
        {
            if (runManager != null)
            {
                runManager.OnRunStarted += HandleRunStarted;
                runManager.OnRunStateChanged += HandleRunStateChanged;
                runManager.OnBiomeEntered += HandleBiomeEntered;
                runManager.OnPlayerStumbled += HandleStumbled;
                runManager.OnGameplayFeedback += HandleGameplayFeedback;
                runManager.OnRunEnded += HandleRunEnded;
            }

            if (coinPool != null)
            {
                coinPool.OnCoinCountChanged += HandleCoinsChanged;
            }

            if (playerController != null)
            {
                playerController.OnSlideStarted += HandleSlideStarted;
            }

            if (progressionManager != null)
            {
                progressionManager.OnProgressionChanged += RefreshHubBadgeLabels;
            }
        }

        private void OnDisable()
        {
            if (runManager != null)
            {
                runManager.OnRunStarted -= HandleRunStarted;
                runManager.OnRunStateChanged -= HandleRunStateChanged;
                runManager.OnBiomeEntered -= HandleBiomeEntered;
                runManager.OnPlayerStumbled -= HandleStumbled;
                runManager.OnGameplayFeedback -= HandleGameplayFeedback;
                runManager.OnRunEnded -= HandleRunEnded;
            }

            if (coinPool != null)
            {
                coinPool.OnCoinCountChanged -= HandleCoinsChanged;
            }

            if (playerController != null)
            {
                playerController.OnSlideStarted -= HandleSlideStarted;
            }

            if (progressionManager != null)
            {
                progressionManager.OnProgressionChanged -= RefreshHubBadgeLabels;
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
            if (hudRefreshTimer >= 0.08f)
            {
                hudRefreshTimer = 0f;
                RefreshLiveTelemetry();
                RefreshPowerUpSlots();
                RefreshAbilityButton();
            }
        }

        private void RefreshLiveTelemetry()
        {
            if (scoreText != null && scoreManager != null)
            {
                sb.Clear();
                sb.Append(scoreManager.CurrentScore);
                scoreText.text = sb.ToString();
            }

            if (multiplierTierText != null && scoreManager != null)
            {
                int tier = difficultyManager != null ? difficultyManager.CurrentTier : 1;
                sb.Clear();
                sb.Append('x').Append(scoreManager.ActiveMultiplier.ToString("F1"));
                sb.Append("  |  TIER ").Append(tier);
                multiplierTierText.text = sb.ToString();
            }

            if (distanceText != null && playerController != null)
            {
                sb.Clear();
                sb.Append(Mathf.FloorToInt(playerController.DistanceTraveled)).Append(" m");
                distanceText.text = sb.ToString();
            }

            if (speedFpsText != null && playerController != null)
            {
                sb.Clear();
                sb.Append(playerController.CurrentForwardSpeed.ToString("F1")).Append(" m/s  |  ");
                sb.Append(Mathf.RoundToInt(currentFps)).Append(" FPS");
                speedFpsText.text = sb.ToString();
            }

            if (modeTimerText != null && runManager != null)
            {
                if (runManager.ActiveGameMode == GameModeType.TimeChallenge)
                {
                    sb.Clear();
                    sb.Append("TIME: ").Append(Mathf.CeilToInt(runManager.TimeChallengeRemaining)).Append('s');
                    modeTimerText.text = sb.ToString();
                }
                else if (runManager.ActiveGameMode == GameModeType.Mission && progressionManager != null && progressionManager.Data != null)
                {
                    MissionDefinition feat = progressionManager.Missions.GetFeaturedActiveMission(progressionManager.Data);
                    modeTimerText.text = $"GOAL: {feat.TargetValue}";
                }
                else if (runManager.ActiveGameMode == GameModeType.SpecialChallenge)
                {
                    modeTimerText.text = "OVERDRIVE x1.5";
                }
                else
                {
                    modeTimerText.text = "ENDLESS";
                }
            }
        }

        private void RefreshPowerUpSlots()
        {
            if (powerSlotRoots == null || powerUpManager == null)
            {
                return;
            }

            int slot = 0;
            for (int i = 0; i < PowerUpCatalog.PowerUpCount && slot < powerSlotRoots.Length; i++)
            {
                PowerUpType type = (PowerUpType)i;
                if (!powerUpManager.IsActive(type))
                {
                    continue;
                }

                PowerUpDefinition def = PowerUpCatalog.Get(type);
                float rem = powerUpManager.GetRemainingTime(type);
                float norm = powerUpManager.GetNormalizedRemaining(type);

                powerSlotRoots[slot].SetActive(true);
                sb.Clear();
                sb.Append(def.ShortLabel).Append(' ').Append(rem.ToString("F1")).Append('s');
                powerSlotLabels[slot].text = sb.ToString();
                powerSlotBars[slot].color = def.AccentColor;
                powerSlotBars[slot].rectTransform.anchorMax = new Vector2(Mathf.Clamp01(norm), 0.24f);
                slot++;
            }

            for (; slot < powerSlotRoots.Length; slot++)
            {
                powerSlotRoots[slot].SetActive(false);
            }
        }

        private void RefreshAbilityButton()
        {
            if (powerUpManager == null || abilityButtonLabel == null)
            {
                return;
            }

            PowerUpDefinition def = PowerUpCatalog.Get(powerUpManager.EquippedAbility);
            if (powerUpManager.IsAbilityReady)
            {
                sb.Clear();
                sb.Append(def.ShortLabel).Append("\nREADY");
                abilityButtonLabel.text = sb.ToString();
                abilityButtonLabel.color = Color.white;
                if (abilityButtonBg != null) abilityButtonBg.color = new Color(def.AccentColor.r * 0.28f, def.AccentColor.g * 0.28f, def.AccentColor.b * 0.28f, 0.92f);
                if (abilityButtonBorder != null) abilityButtonBorder.effectColor = def.AccentColor;
            }
            else
            {
                sb.Clear();
                sb.Append(def.ShortLabel).Append('\n').Append(Mathf.CeilToInt(powerUpManager.AbilityCooldownRemaining)).Append('s');
                abilityButtonLabel.text = sb.ToString();
                abilityButtonLabel.color = new Color(0.68f, 0.76f, 0.88f, 0.85f);
                if (abilityButtonBg != null) abilityButtonBg.color = new Color(0.05f, 0.08f, 0.14f, 0.78f);
                if (abilityButtonBorder != null) abilityButtonBorder.effectColor = new Color(0.32f, 0.42f, 0.56f, 0.7f);
            }
        }

        private void HandleRunStarted()
        {
            if (gameplayHudRoot != null) gameplayHudRoot.SetActive(true);
            if (mainMenuOverlay != null) mainMenuOverlay.SetVisible(false);
            if (pausePanel != null) pausePanel.SetActive(false);
            if (gameOverPanel != null) gameOverPanel.SetActive(false);
            if (progressionOverlay != null) progressionOverlay.Close();
            if (settingsOverlay != null) settingsOverlay.Close();

            HandleCoinsChanged(coinPool != null ? coinPool.RunCoins : 0, coinPool != null ? coinPool.TotalCoins : 0);
            ApplyBiomeBadge(runManager != null ? runManager.CurrentBiome : BiomeType.NeonCity, announce: true);
            RefreshPauseToggleLabels();
        }

        private void HandleRunStateChanged(RunState state)
        {
            bool isMenu = state == RunState.MainMenu;
            if (gameplayHudRoot != null) gameplayHudRoot.SetActive(!isMenu);
            if (mainMenuOverlay != null) mainMenuOverlay.SetVisible(isMenu);

            if (isMenu)
            {
                if (pausePanel != null) pausePanel.SetActive(false);
                if (gameOverPanel != null) gameOverPanel.SetActive(false);
                if (progressionOverlay != null) progressionOverlay.Close();
                if (settingsOverlay != null) settingsOverlay.Close();
                return;
            }

            if (pausePanel != null)
            {
                bool showPause = state == RunState.Paused;
                pausePanel.SetActive(showPause);
                if (showPause)
                {
                    RefreshPauseStats();
                }
            }
        }

        private void RefreshPauseStats()
        {
            if (pauseStatsText == null)
            {
                return;
            }

            int score = scoreManager != null ? scoreManager.CurrentScore : 0;
            int dist = playerController != null ? Mathf.FloorToInt(playerController.DistanceTraveled) : 0;
            int coins = coinPool != null ? coinPool.RunCoins : 0;
            BiomeProfile profile = BiomeCatalog.GetProfile(runManager != null ? runManager.CurrentBiome : BiomeType.NeonCity);

            sb.Clear();
            sb.Append("SCORE: ").Append(score).Append('\n');
            sb.Append("DISTANCE: ").Append(dist).Append(" m   |   ENERGY: ").Append(coins).Append('\n');
            sb.Append("SECTOR: ").Append(profile.DisplayName);
            pauseStatsText.text = sb.ToString();
            RefreshPauseToggleLabels();
        }

        private void RefreshPauseToggleLabels()
        {
            if (pauseFpsLabel != null)
            {
                int cap = Application.targetFrameRate <= 30 ? 30 : 60;
                pauseFpsLabel.text = cap == 30 ? "FPS CAP: 30" : "FPS CAP: 60";
            }

            if (pauseModeLabel != null && runManager != null)
            {
                if (runManager.ActiveGameMode == GameModeType.TimeChallenge) pauseModeLabel.text = "MODE: TIME 90s";
                else if (runManager.ActiveGameMode == GameModeType.Mission) pauseModeLabel.text = "MODE: MISSION";
                else if (runManager.ActiveGameMode == GameModeType.SpecialChallenge) pauseModeLabel.text = "MODE: OVERDRIVE";
                else pauseModeLabel.text = "MODE: ENDLESS";
            }

            if (pauseAbilityLabel != null && powerUpManager != null)
            {
                pauseAbilityLabel.text = $"ABILITY: {PowerUpCatalog.Get(powerUpManager.EquippedAbility).ShortLabel}";
            }

            RefreshHubBadgeLabels();
        }

        private void RefreshHubBadgeLabels()
        {
            int claims = progressionManager != null ? progressionManager.TotalUnclaimedRewards : 0;
            string hubText = claims > 0 ? $"CYBER HUB / SHOP ({claims}!)" : "CYBER HUB / SHOP";
            if (pauseHubLabel != null) pauseHubLabel.text = hubText;
            if (gameOverHubLabel != null) gameOverHubLabel.text = hubText;
        }

        private void HandleBiomeEntered(BiomeType biome)
        {
            ApplyBiomeBadge(biome, announce: true);
        }

        private void ApplyBiomeBadge(BiomeType biome, bool announce)
        {
            BiomeProfile profile = BiomeCatalog.GetProfile(biome);

            if (sectorBadgeText != null)
            {
                sectorBadgeText.text = $"{profile.SectorCode} // {profile.DisplayName}";
                sectorBadgeText.color = profile.UiAccentColor;
            }

            if (headerAccentLine != null)
            {
                headerAccentLine.color = profile.UiAccentColor;
            }

            if (announce)
            {
                ShowToast($"{profile.SectorCode} // {profile.DisplayName}", profile.UiAccentColor, 2.0f);
            }
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

        private void HandleSlideStarted()
        {
            ShowToast("CYBER SLIDE", new Color(0.68f, 0.35f, 1.0f, 1f), 0.55f);
        }

        private void HandleStumbled(string obstacleName)
        {
            ShowToast("STUMBLE! WATCH OUT", new Color(1.0f, 0.62f, 0.08f, 1f), 1.15f);
        }

        private void HandleGameplayFeedback(string message, Color color)
        {
            ShowToast(message, color, 1.5f);
        }

        private void HandleRunEnded(Phase2RunSummary summary)
        {
            if (pausePanel != null) pausePanel.SetActive(false);
            if (gameOverPanel == null) return;

            BiomeProfile profile = BiomeCatalog.GetProfile(summary.ReachedBiome);

            if (gameOverHeaderText != null)
            {
                gameOverHeaderText.text = summary.IsNewBestScore ? "NEW HIGH SCORE!" : "RUN TERMINATED";
                gameOverHeaderText.color = summary.IsNewBestScore
                    ? new Color(0.0f, 0.94f, 1.0f, 1f)
                    : new Color(1.0f, 0.22f, 0.34f, 1f);
            }

            if (gameOverCauseText != null)
            {
                gameOverCauseText.text = summary.Mode == GameModeType.TimeChallenge && summary.CrashCause.Contains("TIME")
                    ? summary.CrashCause
                    : $"IMPACT: {summary.CrashCause}";
            }

            if (gameOverStatsText != null)
            {
                sb.Clear();
                sb.Append("FINAL SCORE: ").Append(summary.FinalScore).Append('\n');
                sb.Append("BEST SCORE: ").Append(summary.BestScore).Append('\n');
                sb.Append("DISTANCE: ").Append(summary.DistanceMeters).Append(" m  (BEST: ").Append(summary.BestDistanceMeters).Append(" m)\n");
                sb.Append("ENERGY: +").Append(summary.RunCoins);
                if (summary.BonusRewardCoins > 0)
                {
                    sb.Append("  (+").Append(summary.BonusRewardCoins).Append(" BONUS)");
                }
                sb.Append('\n');
                sb.Append("TOTAL ENERGY: ").Append(summary.TotalCoins).Append('\n');
                sb.Append("SECTOR: ").Append(profile.DisplayName).Append("  |  TIER ").Append(summary.DifficultyTierReached);
                gameOverStatsText.text = sb.ToString();
            }

            RefreshHubBadgeLabels();
            gameOverPanel.SetActive(true);
        }

        private void ShowToast(string message, Color color, float duration)
        {
            if (statusToastText == null) return;
            statusToastText.text = message;
            statusToastText.color = color;
            toastTimer = duration;
        }

        private void OnPauseButtonClicked() => runManager?.TogglePause();
        private void OnResumeClicked()
        {
            if (progressionOverlay != null) progressionOverlay.Close();
            if (settingsOverlay != null) settingsOverlay.Close();
            runManager?.ResumeGame();
        }
        private void OnRestartClicked()
        {
            if (progressionOverlay != null) progressionOverlay.Close();
            if (settingsOverlay != null) settingsOverlay.Close();
            CyberAudioManager.Instance?.PlaySfx(CyberSfxType.UiClick);
            runManager?.RestartRun();
        }
        private void OnReturnToMenuClicked()
        {
            if (progressionOverlay != null) progressionOverlay.Close();
            if (settingsOverlay != null) settingsOverlay.Close();
            runManager?.ReturnToMainMenu();
        }
        private void OnOpenHubClicked()
        {
            CyberAudioManager.Instance?.PlaySfx(CyberSfxType.UiClick);
            HapticsManager.Light();
            if (progressionOverlay != null) progressionOverlay.Open();
        }
        private void OnOpenSettingsClicked()
        {
            if (settingsOverlay != null) settingsOverlay.Open();
        }
        private void OnAbilityClicked() => runManager?.ActivateEquippedAbility();
        private void OnNextZoneClicked() => runManager?.SwitchToNextBiomeAndRestart();

        private void OnToggleModeClicked()
        {
            runManager?.ToggleEndlessAndTimeChallenge();
            RefreshPauseToggleLabels();
        }

        private void OnCycleAbilityClicked()
        {
            runManager?.CycleEquippedAbility();
            RefreshPauseToggleLabels();
            RefreshAbilityButton();
        }

        private void OnToggleFpsClicked()
        {
            SettingsManager.EnsureInstance().ToggleTargetFps();
            CyberAudioManager.Instance?.PlaySfx(CyberSfxType.UiClick);
            RefreshPauseToggleLabels();
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
            var canvasGo = new GameObject("Phase4HudCanvas");
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

            RectTransform hudRootRect = CreatePanel("GameplayHudRoot", safeRoot, Color.clear);
            gameplayHudRoot = hudRootRect.gameObject;

            // Top HUD Card (leaves top-right clean for Pause button)
            RectTransform topBar = CreatePanel("TopBar", hudRootRect, new Color(0.03f, 0.06f, 0.13f, 0.80f));
            topBar.anchorMin = new Vector2(0.04f, 0.865f);
            topBar.anchorMax = new Vector2(0.82f, 0.986f);

            RectTransform accentRect = CreatePanel("HeaderAccent", topBar, new Color(0.0f, 0.94f, 1.0f, 0.95f));
            accentRect.anchorMin = new Vector2(0f, 0f);
            accentRect.anchorMax = new Vector2(1f, 0.032f);
            headerAccentLine = accentRect.GetComponent<Image>();

            sectorBadgeText = CreateText("SectorBadge", topBar, "SECTOR 01 // NEON CITY", 20, new Color(0.0f, 0.94f, 1.0f, 1f), TextAnchor.UpperLeft);
            sectorBadgeText.rectTransform.anchorMin = new Vector2(0.03f, 0.74f);
            sectorBadgeText.rectTransform.anchorMax = new Vector2(0.68f, 0.96f);

            modeTimerText = CreateText("ModeTimer", topBar, "ENDLESS", 20, new Color(0.76f, 0.86f, 1.0f, 0.95f), TextAnchor.UpperRight);
            modeTimerText.rectTransform.anchorMin = new Vector2(0.66f, 0.74f);
            modeTimerText.rectTransform.anchorMax = new Vector2(0.97f, 0.96f);

            scoreText = CreateText("ScoreText", topBar, "0", 46, Color.white, TextAnchor.MiddleLeft);
            scoreText.rectTransform.anchorMin = new Vector2(0.03f, 0.34f);
            scoreText.rectTransform.anchorMax = new Vector2(0.54f, 0.76f);

            coinsText = CreateText("CoinsText", topBar, "ENERGY: 0", 30, new Color(1.0f, 0.84f, 0.14f, 1f), TextAnchor.MiddleRight);
            coinsText.rectTransform.anchorMin = new Vector2(0.50f, 0.42f);
            coinsText.rectTransform.anchorMax = new Vector2(0.97f, 0.76f);

            distanceText = CreateText("DistanceText", topBar, "0 m", 24, new Color(0.0f, 0.94f, 1.0f, 1f), TextAnchor.LowerLeft);
            distanceText.rectTransform.anchorMin = new Vector2(0.03f, 0.06f);
            distanceText.rectTransform.anchorMax = new Vector2(0.28f, 0.34f);

            multiplierTierText = CreateText("MultTierText", topBar, "x1.0  |  TIER 1", 21, new Color(0.84f, 0.60f, 1.0f, 1f), TextAnchor.LowerCenter);
            multiplierTierText.rectTransform.anchorMin = new Vector2(0.28f, 0.06f);
            multiplierTierText.rectTransform.anchorMax = new Vector2(0.64f, 0.34f);

            speedFpsText = CreateText("SpeedFpsText", topBar, "14.0 m/s | 60 FPS", 19, new Color(0.74f, 0.86f, 0.98f, 0.88f), TextAnchor.LowerRight);
            speedFpsText.rectTransform.anchorMin = new Vector2(0.62f, 0.06f);
            speedFpsText.rectTransform.anchorMax = new Vector2(0.97f, 0.34f);

            // Top-right Pause Button
            CreateActionButton(
                "PauseButton",
                hudRootRect,
                new Vector2(0.84f, 0.905f),
                new Vector2(0.96f, 0.986f),
                new Color(0.05f, 0.09f, 0.19f, 0.88f),
                new Color(0.0f, 0.94f, 1.0f, 0.95f),
                "| |",
                36,
                OnPauseButtonClicked);

            // Active Power-Up Countdown Slots (top-left below header, non-blocking)
            powerSlotRoots = new GameObject[3];
            powerSlotLabels = new Text[3];
            powerSlotBars = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                float topY = 0.848f - (i * 0.050f);
                RectTransform slotRect = CreatePanel($"PowerSlot_{i}", hudRootRect, new Color(0.03f, 0.06f, 0.13f, 0.76f));
                slotRect.anchorMin = new Vector2(0.04f, topY - 0.042f);
                slotRect.anchorMax = new Vector2(0.40f, topY);

                Text lbl = CreateText("Label", slotRect, string.Empty, 22, Color.white, TextAnchor.UpperLeft);
                lbl.rectTransform.anchorMin = new Vector2(0.06f, 0.28f);
                lbl.rectTransform.anchorMax = new Vector2(0.96f, 0.96f);

                RectTransform barRect = CreatePanel("FillBar", slotRect, new Color(0.0f, 0.94f, 1.0f, 1f));
                barRect.anchorMin = new Vector2(0f, 0f);
                barRect.anchorMax = new Vector2(1f, 0.24f);

                powerSlotRoots[i] = slotRect.gameObject;
                powerSlotLabels[i] = lbl;
                powerSlotBars[i] = barRect.GetComponent<Image>();
                powerSlotRoots[i].SetActive(false);
            }

            // Center non-blocking feedback toast
            statusToastText = CreateText("StatusToast", hudRootRect, string.Empty, 28, new Color(0.0f, 0.94f, 1.0f, 1f), TextAnchor.MiddleCenter);
            statusToastText.rectTransform.anchorMin = new Vector2(0.06f, 0.72f);
            statusToastText.rectTransform.anchorMax = new Vector2(0.94f, 0.79f);

            // Bottom-right Special Ability Button (Section 3 & 13)
            var abilityGo = new GameObject("AbilityButton");
            abilityGo.transform.SetParent(hudRootRect, false);
            RectTransform abRt = abilityGo.AddComponent<RectTransform>();
            abRt.anchorMin = new Vector2(0.72f, 0.028f);
            abRt.anchorMax = new Vector2(0.96f, 0.125f);
            abRt.offsetMin = Vector2.zero;
            abRt.offsetMax = Vector2.zero;

            abilityButtonBg = abilityGo.AddComponent<Image>();
            abilityButtonBg.color = new Color(0.0f, 0.22f, 0.30f, 0.92f);
            abilityButtonBorder = abilityGo.AddComponent<Outline>();
            abilityButtonBorder.effectColor = new Color(0.0f, 0.94f, 1.0f, 1f);
            abilityButtonBorder.effectDistance = new Vector2(3f, -3f);

            Button abBtn = abilityGo.AddComponent<Button>();
            abBtn.targetGraphic = abilityButtonBg;
            abBtn.onClick.AddListener(OnAbilityClicked);

            abilityButtonLabel = CreateText("AbilityLabel", abilityGo.transform, "SHIELD\nREADY", 26, Color.white, TextAnchor.MiddleCenter);
            abilityButtonLabel.rectTransform.anchorMin = Vector2.zero;
            abilityButtonLabel.rectTransform.anchorMax = Vector2.one;

            // Main Menu Overlay (floating cleanly over live 3D runner!)
            mainMenuOverlay = gameObject.AddComponent<MainMenuOverlayPanel>();
            mainMenuOverlay.Initialize(
                safeRoot,
                runManager,
                progressionManager,
                uiFont,
                () => runManager?.StartRunFromMenu(),
                OnOpenHubClicked,
                OnOpenSettingsClicked);

            BuildPauseOverlay(safeRoot);
            BuildGameOverOverlay(safeRoot);

            progressionOverlay = gameObject.AddComponent<ProgressionOverlayPanel>();
            progressionOverlay.Initialize(safeRoot, progressionManager, uiFont);

            settingsOverlay = gameObject.AddComponent<SettingsOverlayPanel>();
            settingsOverlay.Initialize(safeRoot, uiFont, RefreshPauseToggleLabels);

            performanceOverlay = gameObject.AddComponent<PerformanceMonitorOverlay>();
            performanceOverlay.Initialize(
                safeRoot,
                uiFont,
                runManager != null ? runManager.ObstaclePool : null,
                coinPool,
                runManager != null ? runManager.PowerUpPool : null);

            RefreshHubBadgeLabels();
            if (runManager != null && runManager.CurrentRunState == RunState.MainMenu)
            {
                gameplayHudRoot.SetActive(false);
                mainMenuOverlay.SetVisible(true);
            }
            else
            {
                mainMenuOverlay.SetVisible(false);
            }
        }

        private void BuildPauseOverlay(Transform safeRoot)
        {
            RectTransform backdrop = CreatePanel("PauseOverlay", safeRoot, new Color(0.02f, 0.03f, 0.08f, 0.84f));
            pausePanel = backdrop.gameObject;

            RectTransform card = CreatePanel("PauseCard", backdrop, new Color(0.04f, 0.08f, 0.17f, 0.96f));
            card.anchorMin = new Vector2(0.07f, 0.12f);
            card.anchorMax = new Vector2(0.93f, 0.88f);

            Outline border = card.gameObject.AddComponent<Outline>();
            border.effectColor = new Color(0.0f, 0.94f, 1.0f, 0.95f);
            border.effectDistance = new Vector2(3f, -3f);
            card.gameObject.AddComponent<UiPanelAnimator>();

            Text header = CreateText("PauseHeader", card, "SYSTEM PAUSED", 44, new Color(0.0f, 0.94f, 1.0f, 1f), TextAnchor.MiddleCenter);
            header.rectTransform.anchorMin = new Vector2(0.05f, 0.87f);
            header.rectTransform.anchorMax = new Vector2(0.95f, 0.97f);

            pauseStatsText = CreateText("PauseStats", card, string.Empty, 25, Color.white, TextAnchor.MiddleCenter);
            pauseStatsText.rectTransform.anchorMin = new Vector2(0.06f, 0.71f);
            pauseStatsText.rectTransform.anchorMax = new Vector2(0.94f, 0.87f);

            CreateActionButton("ResumeBtn", card, new Vector2(0.10f, 0.60f), new Vector2(0.90f, 0.695f),
                new Color(0.0f, 0.24f, 0.30f, 0.96f), new Color(0.0f, 0.94f, 1.0f, 1f), "RESUME", 32, OnResumeClicked);

            pauseHubLabel = CreateActionButton("PauseHubBtn", card, new Vector2(0.10f, 0.49f), new Vector2(0.90f, 0.575f),
                new Color(0.14f, 0.08f, 0.28f, 0.96f), new Color(1.0f, 0.84f, 0.14f, 1f), "CYBER HUB / SHOP", 27, OnOpenHubClicked);

            CreateActionButton("PauseSettingsBtn", card, new Vector2(0.10f, 0.38f), new Vector2(0.49f, 0.465f),
                new Color(0.06f, 0.12f, 0.22f, 0.95f), new Color(0.0f, 0.94f, 1.0f, 0.9f), "SETTINGS", 24, OnOpenSettingsClicked);

            CreateActionButton("PauseRestartBtn", card, new Vector2(0.51f, 0.38f), new Vector2(0.90f, 0.465f),
                new Color(0.10f, 0.07f, 0.24f, 0.95f), new Color(0.68f, 0.22f, 1.0f, 0.9f), "RESTART", 24, OnRestartClicked);

            // Quick toggles inside Pause Menu
            pauseAbilityLabel = CreateActionButton("AbilityCycleBtn", card, new Vector2(0.08f, 0.265f), new Vector2(0.49f, 0.35f),
                new Color(0.06f, 0.12f, 0.22f, 0.92f), new Color(0.0f, 0.94f, 1.0f, 0.85f), "ABILITY: SHIELD", 21, OnCycleAbilityClicked);

            CreateActionButton("PauseNextZoneBtn", card, new Vector2(0.51f, 0.265f), new Vector2(0.92f, 0.35f),
                new Color(0.06f, 0.12f, 0.22f, 0.92f), new Color(0.0f, 0.94f, 1.0f, 0.85f), "NEXT SECTOR >>", 21, OnNextZoneClicked);

            pauseModeLabel = CreateActionButton("ModeToggleBtn", card, new Vector2(0.08f, 0.155f), new Vector2(0.49f, 0.24f),
                new Color(0.08f, 0.08f, 0.20f, 0.92f), new Color(0.68f, 0.22f, 1.0f, 0.85f), "MODE: ENDLESS", 21, OnToggleModeClicked);

            pauseFpsLabel = CreateActionButton("PauseFpsBtn", card, new Vector2(0.51f, 0.155f), new Vector2(0.92f, 0.24f),
                new Color(0.08f, 0.08f, 0.20f, 0.92f), new Color(0.68f, 0.22f, 1.0f, 0.85f), "FPS CAP: 60", 21, OnToggleFpsClicked);

            CreateActionButton("PauseMainMenuBtn", card, new Vector2(0.10f, 0.045f), new Vector2(0.90f, 0.125f),
                new Color(0.16f, 0.06f, 0.12f, 0.92f), new Color(1.0f, 0.28f, 0.42f, 0.9f), "MAIN MENU", 25, OnReturnToMenuClicked);

            pausePanel.SetActive(false);
        }

        private void BuildGameOverOverlay(Transform safeRoot)
        {
            RectTransform backdrop = CreatePanel("GameOverOverlay", safeRoot, new Color(0.02f, 0.03f, 0.08f, 0.86f));
            gameOverPanel = backdrop.gameObject;

            RectTransform card = CreatePanel("GameOverCard", backdrop, new Color(0.04f, 0.08f, 0.17f, 0.97f));
            card.anchorMin = new Vector2(0.08f, 0.13f);
            card.anchorMax = new Vector2(0.92f, 0.87f);

            Outline cardBorder = card.gameObject.AddComponent<Outline>();
            cardBorder.effectColor = new Color(0.0f, 0.94f, 1.0f, 0.95f);
            cardBorder.effectDistance = new Vector2(3f, -3f);
            card.gameObject.AddComponent<UiPanelAnimator>();

            gameOverHeaderText = CreateText("GameOverHeader", card, "RUN TERMINATED", 44, new Color(1.0f, 0.20f, 0.34f, 1f), TextAnchor.MiddleCenter);
            gameOverHeaderText.rectTransform.anchorMin = new Vector2(0.05f, 0.85f);
            gameOverHeaderText.rectTransform.anchorMax = new Vector2(0.95f, 0.96f);

            gameOverCauseText = CreateText("GameOverCause", card, "IMPACT: OBSTACLE", 25, new Color(1.0f, 0.68f, 0.18f, 1f), TextAnchor.MiddleCenter);
            gameOverCauseText.rectTransform.anchorMin = new Vector2(0.05f, 0.76f);
            gameOverCauseText.rectTransform.anchorMax = new Vector2(0.95f, 0.85f);

            gameOverStatsText = CreateText("GameOverStats", card, string.Empty, 26, Color.white, TextAnchor.MiddleCenter);
            gameOverStatsText.rectTransform.anchorMin = new Vector2(0.06f, 0.36f);
            gameOverStatsText.rectTransform.anchorMax = new Vector2(0.94f, 0.75f);

            gameOverHubLabel = CreateActionButton(
                "GameOverHubButton",
                card,
                new Vector2(0.10f, 0.245f),
                new Vector2(0.90f, 0.34f),
                new Color(0.14f, 0.08f, 0.28f, 0.96f),
                new Color(1.0f, 0.84f, 0.14f, 1f),
                "CYBER HUB / SHOP",
                27,
                OnOpenHubClicked);

            CreateActionButton(
                "RunAgainButton",
                card,
                new Vector2(0.10f, 0.135f),
                new Vector2(0.90f, 0.23f),
                new Color(0.0f, 0.22f, 0.30f, 0.96f),
                new Color(0.0f, 0.94f, 1.0f, 1f),
                "RUN AGAIN",
                32,
                OnRestartClicked);

            CreateActionButton(
                "GameOverMenuButton",
                card,
                new Vector2(0.10f, 0.035f),
                new Vector2(0.90f, 0.115f),
                new Color(0.14f, 0.06f, 0.14f, 0.92f),
                new Color(0.70f, 0.25f, 1.0f, 0.9f),
                "MAIN MENU",
                25,
                OnReturnToMenuClicked);

            gameOverPanel.SetActive(false);
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
                img.raycastTarget = name == "PauseOverlay" || name == "GameOverOverlay";
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
            if (Screen.width <= 0 || Screen.height <= 0) return;

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
