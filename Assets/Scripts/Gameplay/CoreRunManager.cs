using System;
using UnityEngine;
using CyberRunner.CameraSystem;
using CyberRunner.Collectibles;
using CyberRunner.Core;
using CyberRunner.Environment;
using CyberRunner.Obstacles;
using CyberRunner.Player;

namespace CyberRunner.Gameplay
{
    public struct Phase2RunSummary
    {
        public int DistanceMeters;
        public int BestDistanceMeters;
        public int RunCoins;
        public int TotalCoins;
        public string CrashCause;
        public BiomeType ReachedBiome;
    }

    /// <summary>
    /// Core Run Orchestrator (Phases 2 & 3).
    /// Coordinates PlayerController, EndlessTrackManager (5 futuristic biomes),
    /// BiomeAtmosphereController, ObstaclePoolManager, CoinPoolManager, and CyberCameraController.
    /// </summary>
    public sealed class CoreRunManager : MonoBehaviour
    {
        private const string BestDistancePrefsKey = "CyberRunner_BestDistance_v1";

        [Header("Core Systems")]
        [SerializeField] private GameConfig config;
        [SerializeField] private PlayerController playerController;
        [SerializeField] private CyberCameraController cameraController;
        [SerializeField] private EndlessTrackManager trackManager;
        [SerializeField] private BiomeAtmosphereController atmosphereController;
        [SerializeField] private ObstaclePoolManager obstaclePool;
        [SerializeField] private CoinPoolManager coinPool;

        [Header("Spawn")]
        [SerializeField] private Vector3 playerSpawnPosition = new Vector3(0f, 0.05f, 4f);

        public event Action OnRunStarted;
        public event Action<BiomeType> OnBiomeEntered;
        public event Action<string> OnPlayerStumbled;
        public event Action<Phase2RunSummary> OnRunEnded;

        public bool IsRunActive { get; private set; }
        public int BestDistanceMeters { get; private set; }
        public BiomeType CurrentBiome => trackManager != null ? trackManager.ActiveBiome : BiomeType.NeonCity;
        public PlayerController Player => playerController;
        public CoinPoolManager CoinPool => coinPool;

        public void Configure(
            GameConfig gameConfig,
            PlayerController player,
            CyberCameraController cam,
            EndlessTrackManager track,
            BiomeAtmosphereController atmosphere,
            ObstaclePoolManager obstacles,
            CoinPoolManager coins)
        {
            config = gameConfig;
            playerController = player;
            cameraController = cam;
            trackManager = track;
            atmosphereController = atmosphere;
            obstaclePool = obstacles;
            coinPool = coins;
        }

        private void Awake()
        {
            BestDistanceMeters = Mathf.Max(0, PlayerPrefs.GetInt(BestDistancePrefsKey, 0));

            if (config == null)
            {
                config = Resources.Load<GameConfig>("GameConfig") ?? GameConfig.CreateRuntimeDefault();
            }

            if (playerController == null) playerController = FindFirstObjectByType<PlayerController>();
            if (cameraController == null) cameraController = FindFirstObjectByType<CyberCameraController>();
            if (trackManager == null) trackManager = FindFirstObjectByType<EndlessTrackManager>();
            if (atmosphereController == null) atmosphereController = FindFirstObjectByType<BiomeAtmosphereController>();
            if (obstaclePool == null) obstaclePool = FindFirstObjectByType<ObstaclePoolManager>();
            if (coinPool == null) coinPool = FindFirstObjectByType<CoinPoolManager>();
        }

        private void OnEnable()
        {
            if (playerController != null)
            {
                playerController.OnCoinTouched += HandleCoinTouched;
                playerController.OnStumbled += HandlePlayerStumbled;
                playerController.OnCrashed += HandlePlayerCrashed;
            }

            if (trackManager != null)
            {
                trackManager.OnActiveBiomeChanged += HandleBiomeChanged;
            }
        }

        private void OnDisable()
        {
            if (playerController != null)
            {
                playerController.OnCoinTouched -= HandleCoinTouched;
                playerController.OnStumbled -= HandlePlayerStumbled;
                playerController.OnCrashed -= HandlePlayerCrashed;
            }

            if (trackManager != null)
            {
                trackManager.OnActiveBiomeChanged -= HandleBiomeChanged;
            }
        }

        private void Start()
        {
            RestartRun();
        }

        /// <summary>
        /// Cycles to the next futuristic environment sector and starts a fresh run immediately.
        /// </summary>
        public void SwitchToNextBiomeAndRestart()
        {
            if (trackManager != null)
            {
                trackManager.CycleStartingBiome();
            }

            RestartRun();
        }

        /// <summary>
        /// Performs a clean, instant, zero-allocation restart of the endless run.
        /// </summary>
        public void RestartRun()
        {
            if (obstaclePool != null) obstaclePool.InitializeIfNeeded();
            if (coinPool != null)
            {
                coinPool.InitializeIfNeeded();
                coinPool.ResetRunCoins();
            }

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
                cameraController.SnapToTarget();
            }

            IsRunActive = true;
            OnRunStarted?.Invoke();
        }

        private void Update()
        {
            if (!IsRunActive || playerController == null)
            {
                return;
            }

            float dt = Time.deltaTime;
            if (dt <= 0f)
            {
                return;
            }

            Vector3 playerPos = playerController.transform.position;

            if (trackManager != null)
            {
                trackManager.TickTrack(playerPos.z, playerController.CurrentForwardSpeed, dt);
            }

            if (obstaclePool != null)
            {
                obstaclePool.TickActive(dt, playerPos.z);
            }

            if (coinPool != null)
            {
                coinPool.TickActive(dt, playerPos);
            }
        }

        private void HandleBiomeChanged(BiomeType nextBiome)
        {
            if (atmosphereController != null)
            {
                atmosphereController.TransitionTo(nextBiome);
            }

            OnBiomeEntered?.Invoke(nextBiome);
        }

        private void HandleCoinTouched(EnergyCoin coin)
        {
            if (!IsRunActive || coinPool == null)
            {
                return;
            }

            coinPool.CollectCoin(coin, multiplier: 1);
        }

        private void HandlePlayerStumbled(string obstacleName)
        {
            if (cameraController != null)
            {
                cameraController.TriggerShake(0.22f, 0.25f);
            }

            OnPlayerStumbled?.Invoke(obstacleName);
        }

        private void HandlePlayerCrashed(string causeName)
        {
            IsRunActive = false;

            if (cameraController != null)
            {
                cameraController.TriggerShake(0.42f, 0.35f);
            }

            int distance = playerController != null ? Mathf.FloorToInt(playerController.DistanceTraveled) : 0;
            if (distance > BestDistanceMeters)
            {
                BestDistanceMeters = distance;
                PlayerPrefs.SetInt(BestDistancePrefsKey, BestDistanceMeters);
            }

            if (coinPool != null)
            {
                coinPool.SaveTotalCoins();
            }
            else
            {
                PlayerPrefs.Save();
            }

            var summary = new Phase2RunSummary
            {
                DistanceMeters = distance,
                BestDistanceMeters = BestDistanceMeters,
                RunCoins = coinPool != null ? coinPool.RunCoins : 0,
                TotalCoins = coinPool != null ? coinPool.TotalCoins : 0,
                CrashCause = string.IsNullOrEmpty(causeName) ? "OBSTACLE" : causeName,
                ReachedBiome = CurrentBiome
            };

            OnRunEnded?.Invoke(summary);
        }
    }
}
