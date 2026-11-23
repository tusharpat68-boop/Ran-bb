using System;
using UnityEngine;
using CyberRunner.Progression;

namespace CyberRunner.Gameplay
{
    /// <summary>
    /// Tracks real-time score, dynamic score multipliers, best score persistence,
    /// and run reward calculations with zero per-frame allocations.
    /// </summary>
    public sealed class ScoreManager : MonoBehaviour
    {
        private const string BestScorePrefsKey = "CyberRunner_BestScore_v1";

        [Header("Scoring Rates")]
        [SerializeField] private float pointsPerMeter = 10f;
        [SerializeField] private int pointsPerCoin = 25;

        public event Action<int> OnBestScoreBroken;

        public int CurrentScore => Mathf.FloorToInt(rawScore);
        public int BestScore { get; private set; }
        public float ActiveMultiplier { get; private set; } = 1f;
        public int BonusPointsEarned { get; private set; }
        public bool IsNewBestThisRun { get; private set; }

        private float rawScore;

        private void Awake()
        {
            BestScore = Mathf.Max(0, PlayerPrefs.GetInt(BestScorePrefsKey, 0));
            if (SaveSystem.Instance != null && SaveSystem.Instance.Data != null)
            {
                BestScore = Mathf.Max(BestScore, SaveSystem.Instance.Data.bestScore);
            }
        }

        public void ResetForNewRun()
        {
            rawScore = 0f;
            ActiveMultiplier = 1f;
            BonusPointsEarned = 0;
            IsNewBestThisRun = false;
        }

        public void TickDistanceScore(float deltaDistanceMeters, float difficultyMultiplier, bool doubleEnergyActive, bool speedBoostActive)
        {
            if (deltaDistanceMeters <= 0f)
            {
                return;
            }

            float mult = Mathf.Max(1f, difficultyMultiplier);
            if (doubleEnergyActive) mult *= 1.5f;
            if (speedBoostActive) mult *= 1.5f;
            ActiveMultiplier = mult;

            rawScore += deltaDistanceMeters * pointsPerMeter * ActiveMultiplier;
            CheckBestScore();
        }

        public void AddCoinScore(int coinsAwarded)
        {
            if (coinsAwarded <= 0)
            {
                return;
            }

            rawScore += coinsAwarded * pointsPerCoin * ActiveMultiplier;
            CheckBestScore();
        }

        public void AddActionBonus(int basePoints)
        {
            if (basePoints <= 0)
            {
                return;
            }

            int awarded = Mathf.RoundToInt(basePoints * ActiveMultiplier);
            BonusPointsEarned += awarded;
            rawScore += awarded;
            CheckBestScore();
        }

        /// <summary>
        /// Calculates bonus Energy Coin reward from survival score milestones (anti-inflation capped).
        /// </summary>
        public int CalculateRunBonusCoins(int distanceMeters)
        {
            int distanceBonus = Mathf.Max(0, distanceMeters / 100) * 5;
            return Mathf.Clamp(distanceBonus, 0, 250);
        }

        public void SaveBestScore()
        {
            if (CurrentScore > BestScore)
            {
                BestScore = CurrentScore;
            }

            PlayerPrefs.SetInt(BestScorePrefsKey, Mathf.Max(0, BestScore));
            PlayerPrefs.Save();
        }

        private void CheckBestScore()
        {
            int scoreInt = CurrentScore;
            if (scoreInt > BestScore)
            {
                BestScore = scoreInt;
                if (!IsNewBestThisRun)
                {
                    IsNewBestThisRun = true;
                    OnBestScoreBroken?.Invoke(BestScore);
                }
            }
        }
    }
}
