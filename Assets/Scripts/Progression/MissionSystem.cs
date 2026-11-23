using System;
using System.Collections.Generic;
using UnityEngine;

namespace CyberRunner.Progression
{
    public enum MissionGoalType
    {
        RunDistanceTotal = 0,
        RunDistanceSingle = 1,
        CollectCoinsTotal = 2,
        UsePowerUpsTotal = 3,
        ScoreSingleRun = 4,
        CompleteRunsTotal = 5
    }

    public readonly struct MissionDefinition
    {
        public readonly string Id;
        public readonly string Title;
        public readonly MissionGoalType GoalType;
        public readonly int TargetValue;
        public readonly int RewardEnergy;
        public readonly bool IsDaily;

        public MissionDefinition(string id, string title, MissionGoalType goalType, int targetValue, int rewardEnergy, bool isDaily)
        {
            Id = id;
            Title = title;
            GoalType = goalType;
            TargetValue = targetValue;
            RewardEnergy = rewardEnergy;
            IsDaily = isDaily;
        }
    }

    /// <summary>
    /// Manages Career Missions and rotating Daily Missions with offline persistence and anti-double-claim guards.
    /// </summary>
    public sealed class MissionSystem
    {
        public static readonly MissionDefinition[] CareerCatalog =
        {
            new MissionDefinition("career_dist_300", "Sprint 300m in 1 Run", MissionGoalType.RunDistanceSingle, 300, 60, false),
            new MissionDefinition("career_coins_100", "Collect 100 Energy Coins", MissionGoalType.CollectCoinsTotal, 100, 80, false),
            new MissionDefinition("career_power_5", "Activate 5 Power-Ups", MissionGoalType.UsePowerUpsTotal, 5, 90, false),
            new MissionDefinition("career_runs_5", "Complete 5 Cyber Runs", MissionGoalType.CompleteRunsTotal, 5, 110, false),
            new MissionDefinition("career_dist_1000", "Reach 1,000m in 1 Run", MissionGoalType.RunDistanceSingle, 1000, 180, false),
            new MissionDefinition("career_score_15k", "Score 15,000 in 1 Run", MissionGoalType.ScoreSingleRun, 15000, 220, false),
            new MissionDefinition("career_dist_total_5k", "Travel 5,000m Total", MissionGoalType.RunDistanceTotal, 5000, 300, false),
        };

        private static readonly MissionDefinition[] DailyPool =
        {
            new MissionDefinition("daily_dist_400", "Daily: Run 400m Total", MissionGoalType.RunDistanceTotal, 400, 75, true),
            new MissionDefinition("daily_coins_60", "Daily: Collect 60 Energy", MissionGoalType.CollectCoinsTotal, 60, 75, true),
            new MissionDefinition("daily_power_3", "Daily: Use 3 Power-Ups", MissionGoalType.UsePowerUpsTotal, 3, 80, true),
            new MissionDefinition("daily_runs_3", "Daily: Complete 3 Runs", MissionGoalType.CompleteRunsTotal, 3, 85, true),
            new MissionDefinition("daily_score_8k", "Daily: Score 8,000 in 1 Run", MissionGoalType.ScoreSingleRun, 8000, 95, true),
            new MissionDefinition("daily_dist_single_500", "Daily: Hit 500m in 1 Run", MissionGoalType.RunDistanceSingle, 500, 100, true),
        };

        private readonly List<MissionDefinition> activeDailyDefinitions = new List<MissionDefinition>(3);

        public IReadOnlyList<MissionDefinition> ActiveDailyMissions => activeDailyDefinitions;

        public void InitializeForDate(PlayerSaveData data)
        {
            if (data == null) return;

            string todayUtc = DateTime.UtcNow.ToString("yyyy-MM-dd");
            BuildDailySetForDate(todayUtc);

            // Ensure career entries exist
            for (int i = 0; i < CareerCatalog.Length; i++)
            {
                GetOrCreateEntry(data.careerMissions, CareerCatalog[i].Id);
            }

            // Reset daily missions if calendar day changed
            if (!string.Equals(data.dailyMissionDateUtc, todayUtc, StringComparison.Ordinal))
            {
                data.dailyMissionDateUtc = todayUtc;
                data.dailyMissions.Clear();
                for (int i = 0; i < activeDailyDefinitions.Count; i++)
                {
                    data.dailyMissions.Add(new MissionSaveEntry
                    {
                        id = activeDailyDefinitions[i].Id,
                        progress = 0,
                        claimed = false
                    });
                }
            }
            else
            {
                for (int i = 0; i < activeDailyDefinitions.Count; i++)
                {
                    GetOrCreateEntry(data.dailyMissions, activeDailyDefinitions[i].Id);
                }
            }
        }

        public void RecordRunResults(PlayerSaveData data, int runDistance, int runCoins, int runPowerUps, int runScore)
        {
            if (data == null) return;

            UpdateGroup(CareerCatalog, data.careerMissions, runDistance, runCoins, runPowerUps, runScore);
            UpdateGroup(activeDailyDefinitions, data.dailyMissions, runDistance, runCoins, runPowerUps, runScore);
        }

        public MissionSaveEntry GetProgress(PlayerSaveData data, MissionDefinition def)
        {
            if (data == null) return null;
            List<MissionSaveEntry> list = def.IsDaily ? data.dailyMissions : data.careerMissions;
            return GetOrCreateEntry(list, def.Id);
        }

        public bool CanClaim(PlayerSaveData data, MissionDefinition def)
        {
            MissionSaveEntry entry = GetProgress(data, def);
            return entry != null && !entry.claimed && entry.progress >= def.TargetValue;
        }

        public int ClaimReward(PlayerSaveData data, MissionDefinition def)
        {
            if (!CanClaim(data, def))
            {
                return 0;
            }

            MissionSaveEntry entry = GetProgress(data, def);
            entry.claimed = true;
            return def.RewardEnergy;
        }

        public int CountClaimable(PlayerSaveData data)
        {
            if (data == null) return 0;
            int count = 0;
            for (int i = 0; i < activeDailyDefinitions.Count; i++)
            {
                if (CanClaim(data, activeDailyDefinitions[i])) count++;
            }
            for (int i = 0; i < CareerCatalog.Length; i++)
            {
                if (CanClaim(data, CareerCatalog[i])) count++;
            }
            return count;
        }

        /// <summary>
        /// Returns the first incomplete mission so Mission Mode can display a live objective banner on HUD.
        /// </summary>
        public MissionDefinition GetFeaturedActiveMission(PlayerSaveData data)
        {
            if (data != null)
            {
                for (int i = 0; i < activeDailyDefinitions.Count; i++)
                {
                    MissionSaveEntry e = GetProgress(data, activeDailyDefinitions[i]);
                    if (e != null && e.progress < activeDailyDefinitions[i].TargetValue)
                    {
                        return activeDailyDefinitions[i];
                    }
                }
                for (int i = 0; i < CareerCatalog.Length; i++)
                {
                    MissionSaveEntry e = GetProgress(data, CareerCatalog[i]);
                    if (e != null && e.progress < CareerCatalog[i].TargetValue)
                    {
                        return CareerCatalog[i];
                    }
                }
            }

            return activeDailyDefinitions.Count > 0 ? activeDailyDefinitions[0] : CareerCatalog[0];
        }

        private void BuildDailySetForDate(string dateStr)
        {
            activeDailyDefinitions.Clear();
            int seed = 17;
            for (int i = 0; i < dateStr.Length; i++)
            {
                seed = (seed * 31) + dateStr[i];
            }

            int startIdx = Mathf.Abs(seed) % DailyPool.Length;
            for (int i = 0; i < 3; i++)
            {
                activeDailyDefinitions.Add(DailyPool[(startIdx + (i * 2)) % DailyPool.Length]);
            }
        }

        private static void UpdateGroup(
            IReadOnlyList<MissionDefinition> defs,
            List<MissionSaveEntry> entries,
            int runDistance,
            int runCoins,
            int runPowerUps,
            int runScore)
        {
            for (int i = 0; i < defs.Count; i++)
            {
                MissionDefinition def = defs[i];
                MissionSaveEntry entry = GetOrCreateEntry(entries, def.Id);
                if (entry.claimed)
                {
                    continue;
                }

                switch (def.GoalType)
                {
                    case MissionGoalType.RunDistanceTotal:
                        entry.progress = Mathf.Min(def.TargetValue, entry.progress + Mathf.Max(0, runDistance));
                        break;
                    case MissionGoalType.RunDistanceSingle:
                        entry.progress = Mathf.Min(def.TargetValue, Mathf.Max(entry.progress, runDistance));
                        break;
                    case MissionGoalType.CollectCoinsTotal:
                        entry.progress = Mathf.Min(def.TargetValue, entry.progress + Mathf.Max(0, runCoins));
                        break;
                    case MissionGoalType.UsePowerUpsTotal:
                        entry.progress = Mathf.Min(def.TargetValue, entry.progress + Mathf.Max(0, runPowerUps));
                        break;
                    case MissionGoalType.ScoreSingleRun:
                        entry.progress = Mathf.Min(def.TargetValue, Mathf.Max(entry.progress, runScore));
                        break;
                    case MissionGoalType.CompleteRunsTotal:
                        entry.progress = Mathf.Min(def.TargetValue, entry.progress + 1);
                        break;
                }
            }
        }

        private static MissionSaveEntry GetOrCreateEntry(List<MissionSaveEntry> list, string id)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (string.Equals(list[i].id, id, StringComparison.Ordinal))
                {
                    return list[i];
                }
            }

            var created = new MissionSaveEntry { id = id, progress = 0, claimed = false };
            list.Add(created);
            return created;
        }
    }
}
