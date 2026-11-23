using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace CyberRunner.Progression
{
    /// <summary>
    /// Versioned offline-first save manager with atomic file writing, backup fallback,
    /// FNV-1a integrity verification, and seamless migration from Phase 2-4 PlayerPrefs.
    /// Zero external network dependency or API keys required.
    /// </summary>
    public sealed class SaveSystem : MonoBehaviour
    {
        private const string SaveFileName = "cyber_runner_save_v1.json";
        private const string BackupFileName = "cyber_runner_save_v1.bak";
        private const string IntegritySalt = "CYB3R_RUNN3R_2099_OFFLINE_INTEGRITY_V1";

        private const string LegacyCoinsKey = "CyberRunner_TotalEnergyCoins_v1";
        private const string LegacyBestScoreKey = "CyberRunner_BestScore_v1";
        private const string LegacyBestDistanceKey = "CyberRunner_BestDistance_v1";

        public static SaveSystem Instance { get; private set; }

        public event Action<int> OnEnergyBalanceChanged;
        public event Action OnSaveDataUpdated;

        public PlayerSaveData Data { get; private set; }
        public bool LastLoadPassedIntegrity { get; private set; } = true;

        private string savePath;
        private string backupPath;

        public static SaveSystem EnsureInstance()
        {
            if (Instance != null)
            {
                return Instance;
            }

            SaveSystem existing = FindFirstObjectByType<SaveSystem>();
            if (existing != null)
            {
                Instance = existing;
                return existing;
            }

            var go = new GameObject("SaveSystem");
            return go.AddComponent<SaveSystem>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            savePath = Path.Combine(Application.persistentDataPath, SaveFileName);
            backupPath = Path.Combine(Application.persistentDataPath, BackupFileName);

            LoadOrInitialize();
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                SaveNow();
            }
        }

        private void OnApplicationQuit()
        {
            SaveNow();
        }

        public void AddEnergy(int amount)
        {
            if (amount <= 0 || Data == null)
            {
                return;
            }

            // Anti-overflow clamp
            long next = (long)Data.energyCoins + amount;
            Data.energyCoins = (int)Math.Min(next, 99_999_999L);
            PlayerPrefs.SetInt(LegacyCoinsKey, Data.energyCoins);
            OnEnergyBalanceChanged?.Invoke(Data.energyCoins);
            OnSaveDataUpdated?.Invoke();
        }

        public bool TrySpendEnergy(int cost)
        {
            if (cost < 0 || Data == null || Data.energyCoins < cost)
            {
                return false;
            }

            Data.energyCoins -= cost;
            PlayerPrefs.SetInt(LegacyCoinsKey, Data.energyCoins);
            SaveNow();
            OnEnergyBalanceChanged?.Invoke(Data.energyCoins);
            OnSaveDataUpdated?.Invoke();
            return true;
        }

        public void SaveNow()
        {
            if (Data == null)
            {
                return;
            }

            Data.EnsureDefaults();
            Data.schemaVersion = PlayerSaveData.CurrentSchemaVersion;
            Data.integrityHash = ComputeIntegrityHash(Data);

            try
            {
                string json = JsonUtility.ToJson(Data, prettyPrint: true);
                string tempPath = savePath + ".tmp";

                File.WriteAllText(tempPath, json, Encoding.UTF8);
                if (File.Exists(savePath))
                {
                    File.Copy(savePath, backupPath, overwrite: true);
                    File.Delete(savePath);
                }

                File.Move(tempPath, savePath);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SaveSystem] Failed writing save file: {ex.Message}");
            }

            PlayerPrefs.SetInt(LegacyCoinsKey, Data.energyCoins);
            PlayerPrefs.SetInt(LegacyBestScoreKey, Data.bestScore);
            PlayerPrefs.SetInt(LegacyBestDistanceKey, Data.bestDistanceMeters);
            PlayerPrefs.Save();
        }

        private void LoadOrInitialize()
        {
            if (TryLoadFromPath(savePath, out PlayerSaveData loaded))
            {
                Data = loaded;
                MergeHigherLegacyPrefs(Data);
                return;
            }

            if (TryLoadFromPath(backupPath, out PlayerSaveData backup))
            {
                Debug.LogWarning("[SaveSystem] Primary save unreadable or corrupted; restored from backup.");
                Data = backup;
                MergeHigherLegacyPrefs(Data);
                SaveNow();
                return;
            }

            Data = new PlayerSaveData();
            Data.EnsureDefaults();
            MergeHigherLegacyPrefs(Data);
            SaveNow();
        }

        private bool TryLoadFromPath(string path, out PlayerSaveData result)
        {
            result = null;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return false;
            }

            try
            {
                string json = File.ReadAllText(path, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return false;
                }

                PlayerSaveData parsed = JsonUtility.FromJson<PlayerSaveData>(json);
                if (parsed == null)
                {
                    return false;
                }

                parsed.EnsureDefaults();
                string expected = ComputeIntegrityHash(parsed);
                bool hashMatches = string.Equals(parsed.integrityHash, expected, StringComparison.Ordinal);
                LastLoadPassedIntegrity = hashMatches;

                if (!hashMatches)
                {
                    Debug.LogWarning("[SaveSystem] Save integrity mismatch detected; clamping suspicious values safely.");
                    parsed.energyCoins = Mathf.Clamp(parsed.energyCoins, 0, 500_000);
                    parsed.bestScore = Mathf.Clamp(parsed.bestScore, 0, 50_000_000);
                    parsed.bestDistanceMeters = Mathf.Clamp(parsed.bestDistanceMeters, 0, 1_000_000);
                }

                result = parsed;
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SaveSystem] Error loading save at {path}: {ex.Message}");
                return false;
            }
        }

        private static void MergeHigherLegacyPrefs(PlayerSaveData data)
        {
            int legacyCoins = Mathf.Max(0, PlayerPrefs.GetInt(LegacyCoinsKey, 0));
            int legacyBestScore = Mathf.Max(0, PlayerPrefs.GetInt(LegacyBestScoreKey, 0));
            int legacyBestDist = Mathf.Max(0, PlayerPrefs.GetInt(LegacyBestDistanceKey, 0));

            if (legacyCoins > data.energyCoins && data.totalRuns == 0) data.energyCoins = legacyCoins;
            if (legacyBestScore > data.bestScore) data.bestScore = legacyBestScore;
            if (legacyBestDist > data.bestDistanceMeters) data.bestDistanceMeters = legacyBestDist;
        }

        private static string ComputeIntegrityHash(PlayerSaveData d)
        {
            var sb = new StringBuilder(256);
            sb.Append(IntegritySalt).Append('|')
              .Append(d.schemaVersion).Append('|')
              .Append(d.energyCoins).Append('|')
              .Append(d.bestScore).Append('|')
              .Append(d.bestDistanceMeters).Append('|')
              .Append(d.totalRuns).Append('|')
              .Append(d.totalDistanceMeters).Append('|')
              .Append(d.totalCoinsCollected).Append('|')
              .Append(d.equippedSkinId).Append('|')
              .Append(d.unlockedSkinIds != null ? d.unlockedSkinIds.Count : 0).Append('|')
              .Append(d.dailyRewardStreak).Append('|')
              .Append(d.lastDailyClaimDateUtc);

            if (d.powerUpLevels != null)
            {
                for (int i = 0; i < d.powerUpLevels.Length; i++)
                {
                    sb.Append('|').Append(d.powerUpLevels[i]);
                }
            }

            // 64-bit FNV-1a checksum
            ulong hash = 14695981039346656037UL;
            string payload = sb.ToString();
            for (int i = 0; i < payload.Length; i++)
            {
                hash ^= payload[i];
                hash *= 1099511628211UL;
            }

            return hash.ToString("X16");
        }
    }
}
