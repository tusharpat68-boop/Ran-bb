using System;
using System.Collections.Generic;
using UnityEngine;
using CyberRunner.Collectibles;
using CyberRunner.Environment;
using CyberRunner.Player;
using CyberRunner.PowerUps;

namespace CyberRunner.Progression
{
    /// <summary>
    /// Central Phase 5 Progression Controller:
    /// coordinates versioned offline SaveSystem, Career & Daily Missions, 8 Achievements,
    /// 7-Day Daily Rewards, Cosmetic Character Skin Shop, In-Game Energy Power-Up Upgrades (no IAP),
    /// and offline Local Top-5 Leaderboard.
    /// </summary>
    public sealed class ProgressionManager : MonoBehaviour
    {
        public const int MaxPowerUpUpgradeLevel = 3;
        public static readonly int[] PowerUpUpgradeCosts = { 150, 350, 700 };

        [SerializeField] private CyberRunnerVisualRig playerVisualRig;
        [SerializeField] private PowerUpManager powerUpManager;
        [SerializeField] private CoinPoolManager coinPool;

        public event Action OnProgressionChanged;
        public event Action<string, Color> OnProgressionToast;

        public SaveSystem Save { get; private set; }
        public MissionSystem Missions { get; } = new MissionSystem();
        public AchievementSystem Achievements { get; } = new AchievementSystem();
        public DailyRewardSystem DailyRewards { get; } = new DailyRewardSystem();

        public PlayerSaveData Data => Save != null ? Save.Data : null;
        public int EnergyBalance => Data != null ? Data.energyCoins : 0;

        public int TotalUnclaimedRewards
        {
            get
            {
                if (Data == null) return 0;
                int total = Missions.CountClaimable(Data) + Achievements.CountClaimable(Data);
                if (DailyRewards.CanClaimToday(Data)) total++;
                return total;
            }
        }

        public void Configure(CyberRunnerVisualRig rig, PowerUpManager powerUps, CoinPoolManager coins)
        {
            playerVisualRig = rig;
            powerUpManager = powerUps;
            coinPool = coins;
        }

        private void Awake()
        {
            Save = SaveSystem.EnsureInstance();
            if (playerVisualRig == null) playerVisualRig = FindFirstObjectByType<CyberRunnerVisualRig>();
            if (powerUpManager == null) powerUpManager = FindFirstObjectByType<PowerUpManager>();
            if (coinPool == null) coinPool = FindFirstObjectByType<CoinPoolManager>();

            Missions.InitializeForDate(Data);
            Achievements.EvaluateAll(Data);
            ApplyPersistedLoadoutToGameplay();
        }

        public void ApplyPersistedLoadoutToGameplay()
        {
            if (Data == null) return;

            // Apply equipped cosmetic skin to live 3D runner rig
            CharacterSkinDefinition skin = CharacterSkinCatalog.GetById(Data.equippedSkinId);
            if (playerVisualRig != null)
            {
                playerVisualRig.ApplySkin(skin.SuitColor, skin.PrimaryNeonColor, skin.SecondaryAccentColor);
            }

            // Apply upgraded power-up levels & equipped ability
            if (powerUpManager != null)
            {
                powerUpManager.SetUpgradeLevels(Data.powerUpLevels);
                powerUpManager.SetEquippedAbility((PowerUpType)Mathf.Clamp(Data.equippedAbilityIndex, 0, PowerUpCatalog.PowerUpCount - 1));
            }

            if (coinPool != null)
            {
                coinPool.SyncTotalCoinsFromSave(Data.energyCoins);
            }
        }

        public void RecordBiomeVisited(BiomeType biome)
        {
            if (Data == null) return;
            int bit = 1 << (int)biome;
            if ((Data.reachedBiomesMask & bit) == 0)
            {
                Data.reachedBiomesMask |= bit;
                int newly = Achievements.EvaluateAll(Data);
                Save.SaveNow();
                if (newly > 0)
                {
                    OnProgressionToast?.Invoke("ACHIEVEMENT UNLOCKED!", new Color(1.0f, 0.84f, 0.14f, 1f));
                }
                OnProgressionChanged?.Invoke();
            }
        }

        public void SaveEquippedAbility(PowerUpType ability)
        {
            if (Data == null) return;
            Data.equippedAbilityIndex = (int)ability;
            Save.SaveNow();
            OnProgressionChanged?.Invoke();
        }

        /// <summary>
        /// Commits a finished run's telemetry into career stats, missions, achievements, and local leaderboard.
        /// </summary>
        public void RecordCompletedRun(
            int finalScore,
            int distanceMeters,
            int runCoinsEarned,
            int bonusCoinsEarned,
            int powerUpsUsed,
            int difficultyTier,
            BiomeType reachedBiome)
        {
            if (Data == null) return;

            int totalEarned = Mathf.Max(0, runCoinsEarned) + Mathf.Max(0, bonusCoinsEarned);
            Save.AddEnergy(totalEarned);

            Data.totalRuns++;
            Data.totalDistanceMeters += Mathf.Max(0, distanceMeters);
            Data.totalCoinsCollected += totalEarned;
            Data.totalPowerUpsUsed += Mathf.Max(0, powerUpsUsed);
            if (finalScore > Data.bestScore) Data.bestScore = finalScore;
            if (distanceMeters > Data.bestDistanceMeters) Data.bestDistanceMeters = distanceMeters;
            if (difficultyTier > Data.bestDifficultyTier) Data.bestDifficultyTier = difficultyTier;
            Data.reachedBiomesMask |= (1 << (int)reachedBiome);

            RecordLocalLeaderboardEntry(finalScore, distanceMeters, BiomeCatalog.GetProfile(reachedBiome).DisplayName);

            Missions.InitializeForDate(Data);
            Missions.RecordRunResults(Data, distanceMeters, runCoinsEarned, powerUpsUsed, finalScore);
            int unlockedAch = Achievements.EvaluateAll(Data);

            Save.SaveNow();
            if (coinPool != null) coinPool.SyncTotalCoinsFromSave(Data.energyCoins);

            if (unlockedAch > 0)
            {
                OnProgressionToast?.Invoke($"ACHIEVEMENT UNLOCKED (+{unlockedAch})!", new Color(1.0f, 0.84f, 0.14f, 1f));
            }

            OnProgressionChanged?.Invoke();
        }

        // --- COSMETIC SKIN SHOP (In-Game Energy Only) ---

        public bool IsSkinUnlocked(string skinId)
        {
            return Data != null && Data.unlockedSkinIds != null && Data.unlockedSkinIds.Contains(skinId);
        }

        public bool IsSkinEquipped(string skinId)
        {
            return Data != null && string.Equals(Data.equippedSkinId, skinId, StringComparison.Ordinal);
        }

        public bool TryBuyOrEquipSkin(string skinId, out string message)
        {
            message = string.Empty;
            if (Data == null)
            {
                message = "Save unavailable";
                return false;
            }

            CharacterSkinDefinition skin = CharacterSkinCatalog.GetById(skinId);
            if (!string.Equals(skin.Id, skinId, StringComparison.Ordinal))
            {
                message = "Invalid skin ID";
                return false;
            }

            if (IsSkinUnlocked(skin.Id))
            {
                Data.equippedSkinId = skin.Id;
                Save.SaveNow();
                ApplyPersistedLoadoutToGameplay();
                message = $"EQUIPPED {skin.DisplayName}";
                OnProgressionToast?.Invoke(message, skin.PrimaryNeonColor);
                OnProgressionChanged?.Invoke();
                return true;
            }

            if (!Save.TrySpendEnergy(skin.EnergyCost))
            {
                message = $"NEED {skin.EnergyCost} ENERGY";
                OnProgressionToast?.Invoke(message, new Color(1.0f, 0.30f, 0.35f, 1f));
                return false;
            }

            Data.unlockedSkinIds.Add(skin.Id);
            Data.equippedSkinId = skin.Id;
            Achievements.EvaluateAll(Data);
            Save.SaveNow();
            ApplyPersistedLoadoutToGameplay();

            message = $"UNLOCKED {skin.DisplayName}!";
            OnProgressionToast?.Invoke(message, skin.PrimaryNeonColor);
            OnProgressionChanged?.Invoke();
            return true;
        }

        // --- POWER-UP UPGRADE SHOP (In-Game Energy Only) ---

        public int GetPowerUpLevel(PowerUpType type)
        {
            if (Data == null || Data.powerUpLevels == null) return 0;
            int idx = (int)type;
            return idx >= 0 && idx < Data.powerUpLevels.Length ? Data.powerUpLevels[idx] : 0;
        }

        public int GetNextPowerUpUpgradeCost(PowerUpType type)
        {
            int level = GetPowerUpLevel(type);
            if (level >= MaxPowerUpUpgradeLevel) return -1;
            return PowerUpUpgradeCosts[Mathf.Clamp(level, 0, PowerUpUpgradeCosts.Length - 1)];
        }

        public bool TryUpgradePowerUp(PowerUpType type, out string message)
        {
            message = string.Empty;
            if (Data == null)
            {
                message = "Save unavailable";
                return false;
            }

            int level = GetPowerUpLevel(type);
            if (level >= MaxPowerUpUpgradeLevel)
            {
                message = "MAX LEVEL";
                return false;
            }

            int cost = GetNextPowerUpUpgradeCost(type);
            if (!Save.TrySpendEnergy(cost))
            {
                message = $"NEED {cost} ENERGY";
                OnProgressionToast?.Invoke(message, new Color(1.0f, 0.30f, 0.35f, 1f));
                return false;
            }

            Data.powerUpLevels[(int)type] = level + 1;
            Save.SaveNow();
            ApplyPersistedLoadoutToGameplay();

            PowerUpDefinition def = PowerUpCatalog.Get(type);
            message = $"{def.ShortLabel} UPGRADED TO LV.{level + 1}!";
            OnProgressionToast?.Invoke(message, def.AccentColor);
            OnProgressionChanged?.Invoke();
            return true;
        }

        // --- CLAIMS: MISSIONS, ACHIEVEMENTS, DAILY REWARD ---

        public bool TryClaimMission(MissionDefinition def)
        {
            if (Data == null) return false;
            int reward = Missions.ClaimReward(Data, def);
            if (reward <= 0) return false;

            Save.AddEnergy(reward);
            Save.SaveNow();
            if (coinPool != null) coinPool.SyncTotalCoinsFromSave(Data.energyCoins);
            OnProgressionToast?.Invoke($"MISSION CLAIMED +{reward} ENERGY!", new Color(0.0f, 0.94f, 1.0f, 1f));
            OnProgressionChanged?.Invoke();
            return true;
        }

        public bool TryClaimAchievement(AchievementDefinition def)
        {
            if (Data == null) return false;
            int reward = Achievements.ClaimReward(Data, def);
            if (reward <= 0) return false;

            Save.AddEnergy(reward);
            Save.SaveNow();
            if (coinPool != null) coinPool.SyncTotalCoinsFromSave(Data.energyCoins);
            OnProgressionToast?.Invoke($"ACHIEVEMENT CLAIMED +{reward} ENERGY!", new Color(1.0f, 0.84f, 0.14f, 1f));
            OnProgressionChanged?.Invoke();
            return true;
        }

        public bool TryClaimDailyReward()
        {
            if (Data == null) return false;
            int reward = DailyRewards.ClaimTodayReward(Data);
            if (reward <= 0) return false;

            Save.AddEnergy(reward);
            Save.SaveNow();
            if (coinPool != null) coinPool.SyncTotalCoinsFromSave(Data.energyCoins);
            OnProgressionToast?.Invoke($"DAILY REWARD CLAIMED +{reward} ENERGY!", new Color(0.18f, 1.0f, 0.52f, 1f));
            OnProgressionChanged?.Invoke();
            return true;
        }

        private void RecordLocalLeaderboardEntry(int score, int distanceMeters, string biomeName)
        {
            if (score <= 0 || Data == null) return;

            List<LocalScoreEntry> board = Data.localLeaderboard;
            board.Add(new LocalScoreEntry
            {
                score = score,
                distanceMeters = distanceMeters,
                biomeName = biomeName,
                dateUtc = DateTime.UtcNow.ToString("MM-dd")
            });

            board.Sort((a, b) => b.score.CompareTo(a.score));
            while (board.Count > 5)
            {
                board.RemoveAt(board.Count - 1);
            }
        }
    }
}
