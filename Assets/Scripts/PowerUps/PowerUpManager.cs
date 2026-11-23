using System;
using UnityEngine;
using CyberRunner.Obstacles;

namespace CyberRunner.PowerUps
{
    /// <summary>
    /// Manages all six CYBER RUNNER power-ups, active duration timers, equipped Special Ability
    /// cooldown, Cyber Shield collision absorption, and Support Drone automatic obstacle neutralization.
    /// Uses fixed-size arrays for zero per-frame heap allocations on low-end Android devices.
    /// </summary>
    public sealed class PowerUpManager : MonoBehaviour
    {
        [SerializeField] private PlayerPowerUpVisuals visuals;
        [SerializeField] private ObstaclePoolManager obstaclePool;
        [SerializeField] private PowerUpType equippedAbility = PowerUpType.CyberShield;

        public event Action<PowerUpType, float> OnPowerUpActivated;
        public event Action<PowerUpType> OnPowerUpExpired;
        public event Action<string> OnShieldAbsorbedImpact;
        public event Action<Vector3> OnDroneNeutralizedObstacle;
        public event Action<PowerUpType> OnEquippedAbilityChanged;

        private readonly float[] remainingTimers = new float[PowerUpCatalog.PowerUpCount];
        private readonly float[] maxDurations = new float[PowerUpCatalog.PowerUpCount];
        private readonly int[] upgradeLevels = new int[PowerUpCatalog.PowerUpCount];

        private float abilityCooldownRemaining;
        private float abilityCooldownTotal = 22f;
        private float droneFireCooldown;

        public int PowerUpsActivatedThisRun { get; private set; }
        public PowerUpType EquippedAbility => equippedAbility;
        public float AbilityCooldownRemaining => abilityCooldownRemaining;
        public float AbilityCooldownNormalized => abilityCooldownTotal > 0f
            ? 1f - Mathf.Clamp01(abilityCooldownRemaining / abilityCooldownTotal)
            : 1f;
        public bool IsAbilityReady => abilityCooldownRemaining <= 0f;

        public bool IsMagnetActive => IsActive(PowerUpType.EnergyMagnet) || IsActive(PowerUpType.DroneSupport);
        public bool IsShieldActive => IsActive(PowerUpType.CyberShield);
        public bool IsSpeedBoostActive => IsActive(PowerUpType.SpeedBoost);
        public bool IsDoubleEnergyActive => IsActive(PowerUpType.DoubleEnergy);
        public bool IsHoverBootsActive => IsActive(PowerUpType.HoverBoots);
        public bool IsDroneSupportActive => IsActive(PowerUpType.DroneSupport);
        public int CoinMultiplier => IsDoubleEnergyActive ? 2 : 1;

        public void Configure(PlayerPowerUpVisuals playerVisuals, ObstaclePoolManager obstacles)
        {
            visuals = playerVisuals;
            obstaclePool = obstacles;
        }

        public void SetUpgradeLevels(int[] levels)
        {
            if (levels == null) return;
            int count = Mathf.Min(levels.Length, upgradeLevels.Length);
            for (int i = 0; i < count; i++)
            {
                upgradeLevels[i] = Mathf.Clamp(levels[i], 0, 3);
            }
            abilityCooldownTotal = GetEffectiveCooldown(equippedAbility);
        }

        public void SetEquippedAbility(PowerUpType type)
        {
            equippedAbility = type;
            abilityCooldownTotal = GetEffectiveCooldown(equippedAbility);
            OnEquippedAbilityChanged?.Invoke(equippedAbility);
        }

        public float GetEffectiveDuration(PowerUpType type)
        {
            PowerUpDefinition def = PowerUpCatalog.Get(type);
            int lv = upgradeLevels[(int)type];
            return def.BaseDuration + (lv * 1.5f);
        }

        public float GetEffectiveCooldown(PowerUpType type)
        {
            PowerUpDefinition def = PowerUpCatalog.Get(type);
            int lv = upgradeLevels[(int)type];
            return Mathf.Max(10f, def.AbilityCooldown - (lv * 2.0f));
        }

        private void Awake()
        {
            if (visuals == null) visuals = FindFirstObjectByType<PlayerPowerUpVisuals>();
            if (obstaclePool == null) obstaclePool = FindFirstObjectByType<ObstaclePoolManager>();
            abilityCooldownTotal = GetEffectiveCooldown(equippedAbility);
        }

        public void ResetForNewRun()
        {
            for (int i = 0; i < remainingTimers.Length; i++)
            {
                remainingTimers[i] = 0f;
                maxDurations[i] = 0f;
            }

            PowerUpsActivatedThisRun = 0;
            abilityCooldownRemaining = 0f;
            abilityCooldownTotal = GetEffectiveCooldown(equippedAbility);
            droneFireCooldown = 0f;

            if (visuals != null)
            {
                visuals.DisableAll();
            }
        }

        public bool IsActive(PowerUpType type)
        {
            return remainingTimers[(int)type] > 0f;
        }

        public float GetRemainingTime(PowerUpType type)
        {
            return remainingTimers[(int)type];
        }

        public float GetNormalizedRemaining(PowerUpType type)
        {
            int idx = (int)type;
            return maxDurations[idx] > 0f ? Mathf.Clamp01(remainingTimers[idx] / maxDurations[idx]) : 0f;
        }

        /// <summary>
        /// Activates (or refreshes) one of the six power-ups cleanly.
        /// </summary>
        public void ActivatePowerUp(PowerUpType type, float durationOverride = 0f)
        {
            float duration = durationOverride > 0f ? durationOverride : GetEffectiveDuration(type);
            int idx = (int)type;

            remainingTimers[idx] = Mathf.Max(remainingTimers[idx], duration);
            maxDurations[idx] = Mathf.Max(maxDurations[idx], duration);
            PowerUpsActivatedThisRun++;

            if (visuals != null)
            {
                visuals.SetEffectActive(type, true);
            }

            OnPowerUpActivated?.Invoke(type, remainingTimers[idx]);
        }

        /// <summary>
        /// Activates the player's equipped special ability from the HUD Ability Button if ready.
        /// </summary>
        public bool TryActivateEquippedAbility()
        {
            if (!IsAbilityReady)
            {
                return false;
            }

            abilityCooldownTotal = GetEffectiveCooldown(equippedAbility);
            abilityCooldownRemaining = abilityCooldownTotal;
            ActivatePowerUp(equippedAbility);
            return true;
        }

        public PowerUpType CycleEquippedAbility()
        {
            int next = ((int)equippedAbility + 1) % PowerUpCatalog.PowerUpCount;
            equippedAbility = (PowerUpType)next;
            abilityCooldownTotal = GetEffectiveCooldown(equippedAbility);
            abilityCooldownRemaining = 0f;
            OnEquippedAbilityChanged?.Invoke(equippedAbility);
            return equippedAbility;
        }

        /// <summary>
        /// Consumes an active Cyber Shield to save the runner from an obstacle impact.
        /// </summary>
        public bool TryConsumeShield(string obstacleName)
        {
            int idx = (int)PowerUpType.CyberShield;
            if (remainingTimers[idx] <= 0f)
            {
                return false;
            }

            remainingTimers[idx] = 0f;
            maxDurations[idx] = 0f;
            if (visuals != null)
            {
                visuals.SetEffectActive(PowerUpType.CyberShield, false);
            }

            OnPowerUpExpired?.Invoke(PowerUpType.CyberShield);
            OnShieldAbsorbedImpact?.Invoke(obstacleName);
            return true;
        }

        /// <summary>
        /// Updates power-up countdowns, ability cooldown, visuals, and Drone Support auto-zapper.
        /// </summary>
        public void TickPowerUps(float deltaTime, int playerLane, float playerZ)
        {
            if (abilityCooldownRemaining > 0f)
            {
                abilityCooldownRemaining = Mathf.Max(0f, abilityCooldownRemaining - deltaTime);
            }

            for (int i = 0; i < remainingTimers.Length; i++)
            {
                if (remainingTimers[i] <= 0f)
                {
                    continue;
                }

                remainingTimers[i] -= deltaTime;
                if (remainingTimers[i] <= 0f)
                {
                    remainingTimers[i] = 0f;
                    maxDurations[i] = 0f;
                    PowerUpType expired = (PowerUpType)i;
                    if (visuals != null)
                    {
                        visuals.SetEffectActive(expired, false);
                    }
                    OnPowerUpExpired?.Invoke(expired);
                }
            }

            if (IsDroneSupportActive && obstaclePool != null)
            {
                droneFireCooldown -= deltaTime;
                if (droneFireCooldown <= 0f)
                {
                    if (obstaclePool.NeutralizeNearestInLane(playerLane, playerZ, maxAheadDistance: 28f, out Vector3 hitPos))
                    {
                        droneFireCooldown = 0.85f;
                        if (visuals != null)
                        {
                            visuals.FireDroneLaserAt(hitPos);
                        }
                        OnDroneNeutralizedObstacle?.Invoke(hitPos);
                    }
                }
            }

            if (visuals != null)
            {
                visuals.TickVisuals(deltaTime);
            }
        }
    }
}
