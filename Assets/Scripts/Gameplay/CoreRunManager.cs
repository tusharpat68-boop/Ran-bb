using System;
using UnityEngine;
using CyberRunner.Audio;
using CyberRunner.CameraSystem;
using CyberRunner.Collectibles;
using CyberRunner.Core;
using CyberRunner.Environment;
using CyberRunner.Obstacles;
using CyberRunner.Player;
using CyberRunner.PowerUps;
using CyberRunner.Progression;

namespace CyberRunner.Gameplay
{
    public struct Phase2RunSummary
    {
        public int FinalScore;
        public int BestScore;
        public bool IsNewBestScore;
        public int DistanceMeters;
        public int BestDistanceMeters;
        public int RunCoins;
        public int BonusRewardCoins;
        public int TotalCoins;
        public int PowerUpsUsed;
        public int DifficultyTierReached;
        public string CrashCause;
        public BiomeType ReachedBiome;
        public GameModeType Mode;
    }

    /// <summary>
    /// Phase 4 Core Run Orchestrator.
    /// Coordinates PlayerController, DifficultyManager, ScoreManager, PowerUpManager (6 power-ups),
    /// PowerUpPoolManager, EndlessTrackManager (5 biomes), BiomeAtmosphereController,
    /// ObstaclePoolManager, CoinPoolManager, CyberCameraController, Pause & Game Over states,
    /// Android hardware back button, and automatic pause on app interruption.
    /// </summary>
    public sealed class CoreRunManager : MonoBehaviour
    {
        private const string BestDistancePrefsKey = "CyberRunner_BestDistance_v1";
        private const float TimeChallengeDurationSeconds = 90f;

        [Header("Core Systems")]
        [SerializeField] private GameConfig config;
        [SerializeField] private PlayerController playerController;
        [SerializeField] private MobileInputController inputController;
        [SerializeField] private CyberCameraController cameraController;
        [SerializeField] private EndlessTrackManager trackManager;
        [SerializeField] private BiomeAtmosphereController atmosphereController;
        [SerializeField] private ObstaclePoolManager obstaclePool;
        [SerializeField] private CoinPoolManager coinPool;
        [SerializeField] private PowerUpPoolManager powerUpPool;
        [SerializeField] private PowerUpManager powerUpManager;
        [SerializeField] private DifficultyManager difficultyManager;
        [SerializeField] private ScoreManager scoreManager;
        [SerializeField] private ProgressionManager progressionManager;

        [Header("Spawn & Mode")]
        [SerializeField] private Vector3 playerSpawnPosition = new Vector3(0f, 0.05f, 4f);
        [SerializeField] private GameModeType activeGameMode = GameModeType.Endless;

        public event Action OnRunStarted;
        public event Action<RunState> OnRunStateChanged;
        public event Action<BiomeType> OnBiomeEntered;
        public event Action<string> OnPlayerStumbled;
        public event Action<string, Color> OnGameplayFeedback;
        public event Action<Phase2RunSummary> OnRunEnded;

        public RunState CurrentRunState { get; private set; } = RunState.Running;
        public bool IsRunActive => CurrentRunState == RunState.Running;
        public GameModeType ActiveGameMode => activeGameMode;
        public float TimeChallengeRemaining { get; private set; }
        public int BestDistanceMeters { get; private set; }
        public BiomeType CurrentBiome => trackManager != null ? trackManager.ActiveBiome : BiomeType.NeonCity;

        public PlayerController Player => playerController;
        public ObstaclePoolManager ObstaclePool => obstaclePool;
        public CoinPoolManager CoinPool => coinPool;
        public PowerUpPoolManager PowerUpPool => powerUpPool;
        public PowerUpManager PowerUps => powerUpManager;
        public DifficultyManager Difficulty => difficultyManager;
        public ScoreManager Score => scoreManager;
        public ProgressionManager Progression => progressionManager;

        private float lastTrackedDistance;

        public void Configure(
            GameConfig gameConfig,
            PlayerController player,
            MobileInputController input,
            CyberCameraController cam,
            EndlessTrackManager track,
            BiomeAtmosphereController atmosphere,
            ObstaclePoolManager obstacles,
            CoinPoolManager coins,
            PowerUpPoolManager powerUpsPool,
            PowerUpManager powerUpsMgr,
            DifficultyManager difficulty,
            ScoreManager score,
            ProgressionManager progression = null)
        {
            config = gameConfig;
            playerController = player;
            inputController = input;
            cameraController = cam;
            trackManager = track;
            atmosphereController = atmosphere;
            obstaclePool = obstacles;
            coinPool = coins;
            powerUpPool = powerUpsPool;
            powerUpManager = powerUpsMgr;
            difficultyManager = difficulty;
            scoreManager = score;
            progressionManager = progression;
        }

        private void Awake()
        {
            SettingsManager.EnsureInstance();
            PerformanceOptimizer perf = PerformanceOptimizer.EnsureInstance();
            CyberAudioManager.EnsureInstance();
            BestDistanceMeters = Mathf.Max(0, PlayerPrefs.GetInt(BestDistancePrefsKey, 0));

            if (config == null)
            {
                config = Resources.Load<GameConfig>("GameConfig") ?? GameConfig.CreateRuntimeDefault();
            }

            if (playerController == null) playerController = FindFirstObjectByType<PlayerController>();
            if (inputController == null) inputController = FindFirstObjectByType<MobileInputController>();
            if (cameraController == null) cameraController = FindFirstObjectByType<CyberCameraController>();
            if (trackManager == null) trackManager = FindFirstObjectByType<EndlessTrackManager>();
            if (atmosphereController == null) atmosphereController = FindFirstObjectByType<BiomeAtmosphereController>();
            if (obstaclePool == null) obstaclePool = FindFirstObjectByType<ObstaclePoolManager>();
            if (coinPool == null) coinPool = FindFirstObjectByType<CoinPoolManager>();
            if (powerUpPool == null) powerUpPool = FindFirstObjectByType<PowerUpPoolManager>();
            if (powerUpManager == null) powerUpManager = FindFirstObjectByType<PowerUpManager>();
            if (difficultyManager == null) difficultyManager = FindFirstObjectByType<DifficultyManager>();
            if (scoreManager == null) scoreManager = FindFirstObjectByType<ScoreManager>();
            if (progressionManager == null) progressionManager = FindFirstObjectByType<ProgressionManager>();

            if (cameraController != null)
            {
                perf.RegisterMainCamera(cameraController.GetComponent<Camera>());
            }

            if (progressionManager != null && progressionManager.Data != null)
            {
                BestDistanceMeters = Mathf.Max(BestDistanceMeters, progressionManager.Data.bestDistanceMeters);
            }
        }

        private void OnEnable()
        {
            if (playerController != null)
            {
                playerController.OnLaneChanged += HandlePlayerLaneChanged;
                playerController.OnJumpStarted += HandlePlayerJumpStarted;
                playerController.OnSlideStarted += HandlePlayerSlideStarted;
                playerController.OnCoinTouched += HandleCoinTouched;
                playerController.OnPowerUpTouched += HandlePowerUpTouched;
                playerController.OnStumbled += HandlePlayerStumbled;
                playerController.OnCrashed += HandlePlayerCrashed;
                playerController.ObstacleImpactInterceptor = InterceptObstacleImpact;
            }

            if (inputController != null)
            {
                inputController.OnBackPressed += HandleBackButtonPressed;
            }

            if (trackManager != null)
            {
                trackManager.OnActiveBiomeChanged += HandleBiomeChanged;
            }

            if (powerUpManager != null)
            {
                powerUpManager.OnPowerUpActivated += HandlePowerUpActivated;
                powerUpManager.OnShieldAbsorbedImpact += HandleShieldAbsorbed;
                powerUpManager.OnDroneNeutralizedObstacle += HandleDroneNeutralized;
            }

            if (AppLifecycleManager.Instance != null)
            {
                AppLifecycleManager.Instance.OnAppPauseStateChanged += HandleAndroidAppPause;
            }

            if (progressionManager != null)
            {
                progressionManager.OnProgressionToast += HandleProgressionToast;
            }
        }

        private void OnDisable()
        {
            if (playerController != null)
            {
                playerController.OnLaneChanged -= HandlePlayerLaneChanged;
                playerController.OnJumpStarted -= HandlePlayerJumpStarted;
                playerController.OnSlideStarted -= HandlePlayerSlideStarted;
                playerController.OnCoinTouched -= HandleCoinTouched;
                playerController.OnPowerUpTouched -= HandlePowerUpTouched;
                playerController.OnStumbled -= HandlePlayerStumbled;
                playerController.OnCrashed -= HandlePlayerCrashed;
                playerController.ObstacleImpactInterceptor = null;
            }

            if (inputController != null)
            {
                inputController.OnBackPressed -= HandleBackButtonPressed;
            }

            if (trackManager != null)
            {
                trackManager.OnActiveBiomeChanged -= HandleBiomeChanged;
            }

            if (powerUpManager != null)
            {
                powerUpManager.OnPowerUpActivated -= HandlePowerUpActivated;
                powerUpManager.OnShieldAbsorbedImpact -= HandleShieldAbsorbed;
                powerUpManager.OnDroneNeutralizedObstacle -= HandleDroneNeutralized;
            }

            if (AppLifecycleManager.Instance != null)
            {
                AppLifecycleManager.Instance.OnAppPauseStateChanged -= HandleAndroidAppPause;
            }

            if (progressionManager != null)
            {
                progressionManager.OnProgressionToast -= HandleProgressionToast;
            }
        }

        private void Start()
        {
            EnterMainMenu();
        }

        public void EnterMainMenu()
        {
            if (obstaclePool != null) obstaclePool.InitializeIfNeeded();
            if (powerUpPool != null)
            {
                powerUpPool.InitializeIfNeeded();
                powerUpPool.ReturnAll();
            }
            if (coinPool != null)
            {
                coinPool.InitializeIfNeeded();
                coinPool.ResetRunCoins();
            }
            if (powerUpManager != null) powerUpManager.ResetForNewRun();
            if (difficultyManager != null) difficultyManager.ResetDifficulty();
            if (scoreManager != null) scoreManager.ResetForNewRun();

            if (trackManager != null)
            {
                if (atmosphereController != null)
                {
                    atmosphereController.ApplyImmediate(trackManager.StartingBiome);
                }
                trackManager.ResetTrack();
            }

            if (playerController != null)
            {
                playerController.EnterMenuShowcaseMode(playerSpawnPosition);
            }

            if (cameraController != null)
            {
                cameraController.SetMenuShowcaseMode(true);
            }

            CyberAudioManager.Instance?.SetMusicDucked(false);
            PerformanceOptimizer.Instance?.PerformSafeBetweenRunCleanup();
            SetRunState(RunState.MainMenu);
        }

        public void StartRunFromMenu()
        {
            RestartRun();
        }

        public void ReturnToMainMenu()
        {
            CyberAudioManager.Instance?.PlaySfx(CyberSfxType.UiClick);
            EnterMainMenu();
        }

        public void CycleGameModeInMenu()
        {
            activeGameMode = (GameModeType)(((int)activeGameMode + 1) % 4);
        }

        public void CycleBiomePreviewInMenu()
        {
            if (trackManager != null)
            {
                trackManager.CycleStartingBiome();
                if (atmosphereController != null)
                {
                    atmosphereController.ApplyImmediate(trackManager.StartingBiome);
                }
                trackManager.ResetTrack();
            }
        }

        public void SetGameMode(GameModeType mode)
        {
            activeGameMode = mode;
            RestartRun();
        }

        public GameModeType ToggleEndlessAndTimeChallenge()
        {
            activeGameMode = (GameModeType)(((int)activeGameMode + 1) % 4);
            RestartRun();
            return activeGameMode;
        }

        public void SwitchToNextBiomeAndRestart()
        {
            if (trackManager != null)
            {
                trackManager.CycleStartingBiome();
            }

            RestartRun();
        }

        public void PauseGame()
        {
            if (CurrentRunState != RunState.Running)
            {
                return;
            }

            SetRunState(RunState.Paused);
            if (playerController != null) playerController.SetPaused(true);
            CyberAudioManager.Instance?.SetMusicDucked(true);
            CyberAudioManager.Instance?.PlaySfx(CyberSfxType.UiClick);
            HapticsManager.Light();
        }

        public void ResumeGame()
        {
            if (CurrentRunState != RunState.Paused)
            {
                return;
            }

            SetRunState(RunState.Running);
            if (playerController != null) playerController.SetPaused(false);
            CyberAudioManager.Instance?.SetMusicDucked(false);
            CyberAudioManager.Instance?.PlaySfx(CyberSfxType.UiClick);
        }

        public void TogglePause()
        {
            if (CurrentRunState == RunState.Running) PauseGame();
            else if (CurrentRunState == RunState.Paused) ResumeGame();
        }

        public bool ActivateEquippedAbility()
        {
            if (CurrentRunState != RunState.Running || powerUpManager == null)
            {
                return false;
            }

            return powerUpManager.TryActivateEquippedAbility();
        }

        public PowerUpType CycleEquippedAbility()
        {
            if (powerUpManager == null)
            {
                return PowerUpType.CyberShield;
            }

            PowerUpType next = powerUpManager.CycleEquippedAbility();
            if (progressionManager != null)
            {
                progressionManager.SaveEquippedAbility(next);
            }
            PowerUpDefinition def = PowerUpCatalog.Get(next);
            OnGameplayFeedback?.Invoke($"ABILITY EQUIPPED: {def.DisplayName}", def.AccentColor);
            return next;
        }

        public void RestartRun()
        {
            if (obstaclePool != null) obstaclePool.InitializeIfNeeded();
            if (powerUpPool != null)
            {
                powerUpPool.InitializeIfNeeded();
                powerUpPool.ReturnAll();
            }
            if (coinPool != null)
            {
                coinPool.InitializeIfNeeded();
                coinPool.ResetRunCoins();
            }
            if (powerUpManager != null) powerUpManager.ResetForNewRun();
            if (difficultyManager != null) difficultyManager.ResetDifficulty();
            if (scoreManager != null) scoreManager.ResetForNewRun();

            if (trackManager != null)
            {
                if (atmosphereController != null)
                {
                    atmosphereController.ApplyImmediate(trackManager.StartingBiome);
                }
                trackManager.ResetTrack();
            }

            if (playerController != null)
            {
                playerController.ResetForNewRun(playerSpawnPosition);
            }

            if (cameraController != null)
            {
                cameraController.SetMenuShowcaseMode(false);
                cameraController.SnapToTarget();
            }

            CyberAudioManager.Instance?.SetMusicDucked(false);
            lastTrackedDistance = 0f;
            TimeChallengeRemaining = TimeChallengeDurationSeconds;
            SetRunState(RunState.Running);
            OnRunStarted?.Invoke();
        }

        private void Update()
        {
            if (CurrentRunState != RunState.Running || playerController == null)
            {
                return;
            }

            float dt = Time.deltaTime;
            if (dt <= 0f)
            {
                return;
            }

            Vector3 playerPos = playerController.transform.position;
            float dist = playerController.DistanceTraveled;
            float deltaDist = Mathf.Max(0f, dist - lastTrackedDistance);
            lastTrackedDistance = dist;

            // 1. Update balanced difficulty progression
            float targetSpeed = config.baseRunSpeed;
            float minSpacing = 19.5f;
            float trainSpeed = 9.5f;
            float diffMultiplier = 1f;

            if (difficultyManager != null)
            {
                float effectiveDist = activeGameMode == GameModeType.SpecialChallenge ? (dist + 650f) : dist;
                difficultyManager.UpdateDifficulty(effectiveDist);
                targetSpeed = difficultyManager.TargetForwardSpeed;
                minSpacing = difficultyManager.MinObstacleRowSpacing;
                trainSpeed = difficultyManager.TrainApproachSpeed;
                diffMultiplier = difficultyManager.DifficultyScoreMultiplier * (activeGameMode == GameModeType.SpecialChallenge ? 1.5f : 1f);
            }

            // 2. Tick active power-ups, ability cooldown & companion support drone
            bool boostActive = false;
            bool hoverActive = false;
            bool magnetActive = false;
            bool doubleEnergyActive = false;

            if (powerUpManager != null)
            {
                powerUpManager.TickPowerUps(dt, playerController.CurrentLane, playerPos.z);
                boostActive = powerUpManager.IsSpeedBoostActive;
                hoverActive = powerUpManager.IsHoverBootsActive;
                magnetActive = powerUpManager.IsMagnetActive;
                doubleEnergyActive = powerUpManager.IsDoubleEnergyActive;
            }

            playerController.SetExternalMovementModifiers(targetSpeed, boostActive, hoverActive);

            // 3. Tick score
            if (scoreManager != null)
            {
                scoreManager.TickDistanceScore(deltaDist, diffMultiplier, doubleEnergyActive, boostActive);
            }

            // 4. Tick track, obstacles, coins (with Energy Magnet), and power-up pickups
            if (trackManager != null)
            {
                trackManager.TickTrack(playerPos.z, playerController.CurrentForwardSpeed, dt, minSpacing, trainSpeed);
            }

            if (obstaclePool != null)
            {
                obstaclePool.TickActive(dt, playerPos.z);
            }

            if (coinPool != null)
            {
                coinPool.TickActive(dt, playerPos, magnetActive, magnetRadius: 13.0f, onMagnetCollected: HandleCoinTouched);
            }

            if (powerUpPool != null)
            {
                powerUpPool.TickActive(dt, playerPos.z);
            }

            // 5. Time Challenge countdown mode
            if (activeGameMode == GameModeType.TimeChallenge)
            {
                TimeChallengeRemaining = Mathf.Max(0f, TimeChallengeRemaining - dt);
                if (TimeChallengeRemaining <= 0f)
                {
                    EndRun("TIME CHALLENGE COMPLETE!");
                }
            }
        }

        private bool InterceptObstacleImpact(ObstacleController obstacle)
        {
            if (powerUpManager == null)
            {
                return false;
            }

            // Speed Boost rams straight through obstacles safely!
            if (powerUpManager.IsSpeedBoostActive)
            {
                if (obstaclePool != null) obstaclePool.NeutralizeObstacle(obstacle);
                if (cameraController != null) cameraController.TriggerShake(0.20f, 0.20f);
                if (scoreManager != null) scoreManager.AddActionBonus(50);
                CyberAudioManager.Instance?.PlaySfx(CyberSfxType.SpeedRam);
                HapticsManager.Medium();
                OnGameplayFeedback?.Invoke("SPEED RAM +50!", PowerUpCatalog.Get(PowerUpType.SpeedBoost).AccentColor);
                return true;
            }

            // Cyber Shield absorbs one collision and protects the runner!
            if (powerUpManager.TryConsumeShield(obstacle.DisplayName))
            {
                if (obstaclePool != null) obstaclePool.NeutralizeObstacle(obstacle);
                if (playerController != null) playerController.GrantTemporaryGrace(0.70f);
                if (cameraController != null) cameraController.TriggerShake(0.28f, 0.25f);
                if (scoreManager != null) scoreManager.AddActionBonus(40);
                CyberAudioManager.Instance?.PlaySfx(CyberSfxType.ShieldBlock);
                HapticsManager.Medium();
                return true;
            }

            return false;
        }

        private void HandlePlayerLaneChanged(int newLane)
        {
            if (CurrentRunState == RunState.Running)
            {
                CyberAudioManager.Instance?.PlaySfx(CyberSfxType.LaneSwitch);
                HapticsManager.Light();
            }
        }

        private void HandlePlayerJumpStarted()
        {
            if (CurrentRunState == RunState.Running)
            {
                CyberAudioManager.Instance?.PlaySfx(CyberSfxType.Jump);
                HapticsManager.Light();
            }
        }

        private void HandlePlayerSlideStarted()
        {
            if (CurrentRunState == RunState.Running)
            {
                CyberAudioManager.Instance?.PlaySfx(CyberSfxType.Slide);
                HapticsManager.Light();
            }
        }

        private void HandleCoinTouched(EnergyCoin coin)
        {
            if (CurrentRunState != RunState.Running || coinPool == null)
            {
                return;
            }

            int mult = powerUpManager != null ? powerUpManager.CoinMultiplier : 1;
            if (coinPool.CollectCoin(coin, mult))
            {
                if (scoreManager != null) scoreManager.AddCoinScore(mult);
                CyberAudioManager.Instance?.PlaySfx(CyberSfxType.CoinPickup);
            }
        }

        private void HandlePowerUpTouched(PowerUpPickup pickup)
        {
            if (CurrentRunState != RunState.Running || pickup == null || !pickup.TryCollect())
            {
                return;
            }

            if (powerUpManager != null)
            {
                powerUpManager.ActivatePowerUp(pickup.Type);
            }
        }

        private void HandlePowerUpActivated(PowerUpType type, float duration)
        {
            PowerUpDefinition def = PowerUpCatalog.Get(type);
            CyberAudioManager.Instance?.PlaySfx(CyberSfxType.PowerUpPickup);
            HapticsManager.Medium();
            OnGameplayFeedback?.Invoke($"{def.DisplayName} ACTIVE!", def.AccentColor);
        }

        private void HandleShieldAbsorbed(string obstacleName)
        {
            OnGameplayFeedback?.Invoke($"SHIELD SAVED! ({obstacleName})", PowerUpCatalog.Get(PowerUpType.CyberShield).AccentColor);
        }

        private void HandleDroneNeutralized(Vector3 worldPos)
        {
            if (scoreManager != null)
            {
                scoreManager.AddActionBonus(50);
            }
            CyberAudioManager.Instance?.PlaySfx(CyberSfxType.DroneZap);
            OnGameplayFeedback?.Invoke("DRONE ZAP +50!", PowerUpCatalog.Get(PowerUpType.DroneSupport).AccentColor);
        }

        private void HandleBiomeChanged(BiomeType nextBiome)
        {
            if (atmosphereController != null)
            {
                atmosphereController.TransitionTo(nextBiome);
            }

            if (progressionManager != null)
            {
                progressionManager.RecordBiomeVisited(nextBiome);
            }

            OnBiomeEntered?.Invoke(nextBiome);
        }

        private void HandleProgressionToast(string message, Color color)
        {
            OnGameplayFeedback?.Invoke(message, color);
        }

        private void HandlePlayerStumbled(string obstacleName)
        {
            if (cameraController != null)
            {
                cameraController.TriggerShake(0.22f, 0.25f);
            }

            CyberAudioManager.Instance?.PlaySfx(CyberSfxType.Stumble);
            HapticsManager.Medium();
            OnPlayerStumbled?.Invoke(obstacleName);
        }

        private void HandlePlayerCrashed(string causeName)
        {
            if (cameraController != null)
            {
                cameraController.TriggerShake(0.42f, 0.35f);
            }

            CyberAudioManager.Instance?.PlaySfx(CyberSfxType.CrashGameOver);
            CyberAudioManager.Instance?.SetMusicDucked(true);
            HapticsManager.Heavy();
            EndRun(string.IsNullOrEmpty(causeName) ? "OBSTACLE" : causeName);
        }

        private void EndRun(string reason)
        {
            if (CurrentRunState == RunState.GameOver)
            {
                return;
            }

            SetRunState(RunState.GameOver);
            if (playerController != null)
            {
                playerController.SetPaused(true);
            }

            int distance = playerController != null ? Mathf.FloorToInt(playerController.DistanceTraveled) : 0;
            if (distance > BestDistanceMeters)
            {
                BestDistanceMeters = distance;
                PlayerPrefs.SetInt(BestDistancePrefsKey, BestDistanceMeters);
            }

            int runCoins = coinPool != null ? coinPool.RunCoins : 0;
            int bonusCoins = scoreManager != null ? scoreManager.CalculateRunBonusCoins(distance) : 0;
            int finalScore = scoreManager != null ? scoreManager.CurrentScore : distance * 10;
            int powerUpsUsed = powerUpManager != null ? powerUpManager.PowerUpsActivatedThisRun : 0;
            int tierReached = difficultyManager != null ? difficultyManager.CurrentTier : 1;

            if (scoreManager != null)
            {
                scoreManager.SaveBestScore();
            }

            int totalCoinsAfterRun;
            if (progressionManager != null)
            {
                progressionManager.RecordCompletedRun(
                    finalScore,
                    distance,
                    runCoins,
                    bonusCoins,
                    powerUpsUsed,
                    tierReached,
                    CurrentBiome);
                totalCoinsAfterRun = progressionManager.EnergyBalance;
            }
            else
            {
                if (coinPool != null)
                {
                    if (bonusCoins > 0) coinPool.GrantBonusCoins(bonusCoins);
                    coinPool.SaveTotalCoins();
                }
                totalCoinsAfterRun = coinPool != null ? coinPool.TotalCoins : 0;
            }

            var summary = new Phase2RunSummary
            {
                FinalScore = finalScore,
                BestScore = scoreManager != null ? scoreManager.BestScore : finalScore,
                IsNewBestScore = scoreManager != null && scoreManager.IsNewBestThisRun,
                DistanceMeters = distance,
                BestDistanceMeters = BestDistanceMeters,
                RunCoins = runCoins,
                BonusRewardCoins = bonusCoins,
                TotalCoins = totalCoinsAfterRun,
                PowerUpsUsed = powerUpsUsed,
                DifficultyTierReached = tierReached,
                CrashCause = reason,
                ReachedBiome = CurrentBiome,
                Mode = activeGameMode
            };

            PerformanceOptimizer.Instance?.PerformSafeBetweenRunCleanup();
            OnRunEnded?.Invoke(summary);
        }

        private void HandleBackButtonPressed()
        {
            if (CurrentRunState == RunState.Running)
            {
                PauseGame();
            }
            else if (CurrentRunState == RunState.Paused)
            {
                ResumeGame();
            }
        }

        private void HandleAndroidAppPause(bool paused)
        {
            if (paused && CurrentRunState == RunState.Running)
            {
                PauseGame();
            }
        }

        private void SetRunState(RunState next)
        {
            if (CurrentRunState == next)
            {
                return;
            }

            CurrentRunState = next;
            OnRunStateChanged?.Invoke(next);
        }
    }
}
