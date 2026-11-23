using UnityEngine;
using CyberRunner.Core;

namespace CyberRunner.Gameplay
{
    /// <summary>
    /// Balanced, non-linear difficulty progression controller (Section 17).
    /// Smoothly scales running speed, obstacle pattern spacing, moving train speed,
    /// and score multiplier over distance without sudden unfair spikes.
    /// </summary>
    public sealed class DifficultyManager : MonoBehaviour
    {
        [SerializeField] private GameConfig config;

        [Header("Balanced Progression Curve")]
        [Tooltip("Distance (meters) required to reach maximum difficulty tier 10.")]
        [SerializeField] private float maxDifficultyDistance = 2400f;
        [SerializeField] private float baseTrainApproachSpeed = 9.0f;
        [SerializeField] private float maxTrainApproachSpeed = 15.0f;

        public int CurrentTier { get; private set; } = 1;
        public float NormalizedDifficulty { get; private set; }
        public float TargetForwardSpeed { get; private set; } = 14f;
        public float TrainApproachSpeed { get; private set; } = 9.0f;
        public float MinObstacleRowSpacing { get; private set; } = 19f;
        public float DifficultyScoreMultiplier { get; private set; } = 1.0f;

        public void Configure(GameConfig gameConfig)
        {
            config = gameConfig;
        }

        private void Awake()
        {
            if (config == null)
            {
                config = Resources.Load<GameConfig>("GameConfig") ?? GameConfig.CreateRuntimeDefault();
            }
            ResetDifficulty();
        }

        public void ResetDifficulty()
        {
            CurrentTier = 1;
            NormalizedDifficulty = 0f;
            TargetForwardSpeed = config != null ? config.baseRunSpeed : 14f;
            TrainApproachSpeed = baseTrainApproachSpeed;
            MinObstacleRowSpacing = 19.5f;
            DifficultyScoreMultiplier = 1.0f;
        }

        /// <summary>
        /// Evaluates balanced difficulty parameters from distance traveled (smooth ease-out curve).
        /// </summary>
        public void UpdateDifficulty(float distanceMeters)
        {
            float baseSpeed = config != null ? config.baseRunSpeed : 14f;
            float maxSpeed = config != null ? config.maxRunSpeed : 30f;

            float linear = Mathf.Clamp01(distanceMeters / Mathf.Max(200f, maxDifficultyDistance));
            // Smooth ease-out curve: early game feels approachable, late game ramps smoothly toward maxSpeed.
            NormalizedDifficulty = 1f - ((1f - linear) * (1f - linear));

            CurrentTier = Mathf.Clamp(1 + Mathf.FloorToInt(NormalizedDifficulty * 9.5f), 1, 10);
            TargetForwardSpeed = Mathf.Lerp(baseSpeed, maxSpeed, NormalizedDifficulty);
            TrainApproachSpeed = Mathf.Lerp(baseTrainApproachSpeed, maxTrainApproachSpeed, NormalizedDifficulty);

            // Always guarantee reaction time scales fairly with speed (~0.95s..1.25s between rows).
            float reactionWindowSeconds = Mathf.Lerp(1.25f, 0.88f, NormalizedDifficulty);
            MinObstacleRowSpacing = Mathf.Clamp(TargetForwardSpeed * reactionWindowSeconds, 16.5f, 26f);

            // Reward surviving into higher tiers with up to 3.0x base score multiplier.
            DifficultyScoreMultiplier = 1.0f + (NormalizedDifficulty * 2.0f);
        }
    }
}
