using System;
using System.Collections.Generic;

namespace CyberRunner.Progression
{
    [Serializable]
    public sealed class MissionSaveEntry
    {
        public string id;
        public int progress;
        public bool claimed;
    }

    [Serializable]
    public sealed class AchievementSaveEntry
    {
        public string id;
        public bool unlocked;
        public bool claimed;
    }

    [Serializable]
    public sealed class LocalScoreEntry
    {
        public int score;
        public int distanceMeters;
        public string biomeName;
        public string dateUtc;
    }

    /// <summary>
    /// Versioned, offline-first player progression save payload serialized to JSON.
    /// Includes an integrity hash verified by SaveSystem on load.
    /// </summary>
    [Serializable]
    public sealed class PlayerSaveData
    {
        public const int CurrentSchemaVersion = 1;

        public int schemaVersion = CurrentSchemaVersion;
        public string integrityHash = string.Empty;

        // Core Wallet & Career Stats
        public int energyCoins;
        public int bestScore;
        public int bestDistanceMeters;
        public int totalRuns;
        public int totalDistanceMeters;
        public int totalCoinsCollected;
        public int totalPowerUpsUsed;
        public int totalObstaclesCleared;
        public int bestDifficultyTier = 1;
        public int reachedBiomesMask = 1; // bitmask for BiomeType 0..4 (NeonCity = bit 0)

        // Customization & Upgrades (100% In-Game Energy Only, Cosmetic Skins)
        public List<string> unlockedSkinIds = new List<string> { "neon_vanguard" };
        public string equippedSkinId = "neon_vanguard";
        public int[] powerUpLevels = new int[6]; // Level 0..3 for each PowerUpType
        public int equippedAbilityIndex = 1;     // Default CyberShield

        // Missions & Daily Missions
        public List<MissionSaveEntry> careerMissions = new List<MissionSaveEntry>();
        public string dailyMissionDateUtc = string.Empty;
        public List<MissionSaveEntry> dailyMissions = new List<MissionSaveEntry>();

        // Achievements
        public List<AchievementSaveEntry> achievements = new List<AchievementSaveEntry>();

        // Daily Login Rewards (7-day cycle)
        public int dailyRewardStreak;
        public string lastDailyClaimDateUtc = string.Empty;
        public int totalDailyClaims;

        // Offline Local Top-5 Leaderboard (Optional online sync can layer on top later)
        public List<LocalScoreEntry> localLeaderboard = new List<LocalScoreEntry>();

        public void EnsureDefaults()
        {
            if (schemaVersion <= 0) schemaVersion = CurrentSchemaVersion;
            if (energyCoins < 0) energyCoins = 0;
            if (bestScore < 0) bestScore = 0;
            if (bestDistanceMeters < 0) bestDistanceMeters = 0;
            if (bestDifficultyTier < 1) bestDifficultyTier = 1;
            if (reachedBiomesMask <= 0) reachedBiomesMask = 1;

            if (unlockedSkinIds == null) unlockedSkinIds = new List<string>();
            if (!unlockedSkinIds.Contains("neon_vanguard")) unlockedSkinIds.Add("neon_vanguard");
            if (string.IsNullOrEmpty(equippedSkinId) || !unlockedSkinIds.Contains(equippedSkinId))
            {
                equippedSkinId = "neon_vanguard";
            }

            if (powerUpLevels == null || powerUpLevels.Length != 6)
            {
                int[] resized = new int[6];
                if (powerUpLevels != null)
                {
                    int copy = Math.Min(powerUpLevels.Length, 6);
                    for (int i = 0; i < copy; i++) resized[i] = Math.Max(0, Math.Min(3, powerUpLevels[i]));
                }
                powerUpLevels = resized;
            }
            else
            {
                for (int i = 0; i < 6; i++)
                {
                    if (powerUpLevels[i] < 0) powerUpLevels[i] = 0;
                    if (powerUpLevels[i] > 3) powerUpLevels[i] = 3;
                }
            }

            if (equippedAbilityIndex < 0 || equippedAbilityIndex >= 6) equippedAbilityIndex = 1;
            if (careerMissions == null) careerMissions = new List<MissionSaveEntry>();
            if (dailyMissions == null) dailyMissions = new List<MissionSaveEntry>();
            if (achievements == null) achievements = new List<AchievementSaveEntry>();
            if (localLeaderboard == null) localLeaderboard = new List<LocalScoreEntry>();
            if (dailyRewardStreak < 0 || dailyRewardStreak > 7) dailyRewardStreak = 0;
        }
    }
}
