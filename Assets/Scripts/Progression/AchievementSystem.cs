using System;
using System.Collections.Generic;

namespace CyberRunner.Progression
{
    public readonly struct AchievementDefinition
    {
        public readonly string Id;
        public readonly string Title;
        public readonly string Description;
        public readonly int RewardEnergy;

        public AchievementDefinition(string id, string title, string description, int rewardEnergy)
        {
            Id = id;
            Title = title;
            Description = description;
            RewardEnergy = rewardEnergy;
        }
    }

    /// <summary>
    /// Evaluates and tracks 8 career achievements with Energy Coin rewards.
    /// </summary>
    public sealed class AchievementSystem
    {
        public static readonly AchievementDefinition[] Catalog =
        {
            new AchievementDefinition("first_run", "GRID INITIATE", "Complete your first run", 50),
            new AchievementDefinition("dist_500", "STREET RUNNER", "Reach 500m in one run", 100),
            new AchievementDefinition("dist_1500", "SECTOR BREAKER", "Reach 1,500m in one run", 250),
            new AchievementDefinition("coins_500", "ENERGY HARVESTER", "Collect 500 Energy total", 120),
            new AchievementDefinition("power_10", "OVERCHARGED", "Activate 10 Power-Ups total", 120),
            new AchievementDefinition("biomes_all", "WORLD EXPLORER", "Visit all 5 cyber sectors", 300),
            new AchievementDefinition("score_25k", "CYBER LEGEND", "Score 25,000+ in one run", 250),
            new AchievementDefinition("skin_collector", "STYLE PROTOCOL", "Unlock 3 runner skins", 200),
        };

        public int EvaluateAll(PlayerSaveData data)
        {
            if (data == null) return 0;
            int newlyUnlocked = 0;

            newlyUnlocked += TryUnlock(data, "first_run", data.totalRuns >= 1);
            newlyUnlocked += TryUnlock(data, "dist_500", data.bestDistanceMeters >= 500);
            newlyUnlocked += TryUnlock(data, "dist_1500", data.bestDistanceMeters >= 1500);
            newlyUnlocked += TryUnlock(data, "coins_500", data.totalCoinsCollected >= 500);
            newlyUnlocked += TryUnlock(data, "power_10", data.totalPowerUpsUsed >= 10);
            newlyUnlocked += TryUnlock(data, "biomes_all", (data.reachedBiomesMask & 0x1F) == 0x1F);
            newlyUnlocked += TryUnlock(data, "score_25k", data.bestScore >= 25000);
            newlyUnlocked += TryUnlock(data, "skin_collector", data.unlockedSkinIds != null && data.unlockedSkinIds.Count >= 3);

            return newlyUnlocked;
        }

        public AchievementSaveEntry GetEntry(PlayerSaveData data, string id)
        {
            if (data == null) return null;
            return GetOrCreate(data.achievements, id);
        }

        public bool CanClaim(PlayerSaveData data, AchievementDefinition def)
        {
            AchievementSaveEntry entry = GetEntry(data, def.Id);
            return entry != null && entry.unlocked && !entry.claimed;
        }

        public int ClaimReward(PlayerSaveData data, AchievementDefinition def)
        {
            if (!CanClaim(data, def)) return 0;
            AchievementSaveEntry entry = GetEntry(data, def.Id);
            entry.claimed = true;
            return def.RewardEnergy;
        }

        public int CountClaimable(PlayerSaveData data)
        {
            if (data == null) return 0;
            int count = 0;
            for (int i = 0; i < Catalog.Length; i++)
            {
                if (CanClaim(data, Catalog[i])) count++;
            }
            return count;
        }

        private static int TryUnlock(PlayerSaveData data, string id, bool condition)
        {
            AchievementSaveEntry entry = GetOrCreate(data.achievements, id);
            if (!entry.unlocked && condition)
            {
                entry.unlocked = true;
                return 1;
            }
            return 0;
        }

        private static AchievementSaveEntry GetOrCreate(List<AchievementSaveEntry> list, string id)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (string.Equals(list[i].id, id, StringComparison.Ordinal))
                {
                    return list[i];
                }
            }

            var created = new AchievementSaveEntry { id = id, unlocked = false, claimed = false };
            list.Add(created);
            return created;
        }
    }
}
