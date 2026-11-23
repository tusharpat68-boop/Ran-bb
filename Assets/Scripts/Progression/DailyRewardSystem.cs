using System;
using System.Globalization;

namespace CyberRunner.Progression
{
    /// <summary>
    /// 7-day Daily Login Reward calendar using offline UTC dates with streak tracking and once-per-day claim guard.
    /// </summary>
    public sealed class DailyRewardSystem
    {
        public static readonly int[] SevenDayEnergyRewards =
        {
            75,   // Day 1
            125,  // Day 2
            175,  // Day 3
            250,  // Day 4
            325,  // Day 5
            425,  // Day 6
            750   // Day 7 Jackpot!
        };

        public bool CanClaimToday(PlayerSaveData data)
        {
            if (data == null) return false;
            string today = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            return !string.Equals(data.lastDailyClaimDateUtc, today, StringComparison.Ordinal);
        }

        /// <summary>
        /// Returns the 0-based index (0..6) of the next daily reward day to be claimed.
        /// </summary>
        public int GetCurrentRewardDayIndex(PlayerSaveData data)
        {
            if (data == null) return 0;

            if (string.IsNullOrEmpty(data.lastDailyClaimDateUtc))
            {
                return 0;
            }

            DateTime today = DateTime.UtcNow.Date;
            if (!DateTime.TryParseExact(
                    data.lastDailyClaimDateUtc,
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out DateTime lastDate))
            {
                return 0;
            }

            int dayDiff = (today - lastDate.Date).Days;
            if (dayDiff <= 0)
            {
                // Already claimed today; show the day just claimed
                return Math.Max(0, (data.dailyRewardStreak - 1) % SevenDayEnergyRewards.Length);
            }

            if (dayDiff == 1)
            {
                // Consecutive day!
                return data.dailyRewardStreak % SevenDayEnergyRewards.Length;
            }

            // Missed a day -> streak resets to Day 1
            return 0;
        }

        public int GetTodayRewardAmount(PlayerSaveData data)
        {
            int idx = GetCurrentRewardDayIndex(data);
            return SevenDayEnergyRewards[idx];
        }

        public int ClaimTodayReward(PlayerSaveData data)
        {
            if (!CanClaimToday(data))
            {
                return 0;
            }

            int dayIndex = GetCurrentRewardDayIndex(data);
            int reward = SevenDayEnergyRewards[dayIndex];

            data.dailyRewardStreak = dayIndex + 1;
            data.lastDailyClaimDateUtc = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            data.totalDailyClaims++;
            return reward;
        }
    }
}
