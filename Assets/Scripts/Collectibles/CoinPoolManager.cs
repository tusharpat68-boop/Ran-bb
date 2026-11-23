using System;
using System.Collections.Generic;
using UnityEngine;
using CyberRunner.Core;

namespace CyberRunner.Collectibles
{
    /// <summary>
    /// Pre-warmed Energy Coin pool and formation spawner (straight trails, jump arcs, slide trails).
    /// Handles safe run/total coin accounting with anti-duplication guards.
    /// </summary>
    public sealed class CoinPoolManager : MonoBehaviour
    {
        private const string TotalCoinsPrefsKey = "CyberRunner_TotalEnergyCoins_v1";

        [Header("Shared Mobile URP Materials")]
        [SerializeField] private Material coinGoldMaterial;
        [SerializeField] private Material neonCyanMaterial;

        public event Action<int, int> OnCoinCountChanged; // (runCoins, totalCoins)

        public int RunCoins { get; private set; }
        public int TotalCoins { get; private set; }
        public int ActiveCount => activeCoins.Count;

        private SimpleObjectPool<EnergyCoin> pool;
        private readonly List<EnergyCoin> activeCoins = new List<EnergyCoin>(72);
        private bool initialized;

        public void ConfigureMaterials(Material coinGold, Material neonCyan)
        {
            coinGoldMaterial = coinGold;
            neonCyanMaterial = neonCyan;
        }

        private void Awake()
        {
            LoadTotalCoins();
            InitializeIfNeeded();
        }

        public void InitializeIfNeeded()
        {
            if (initialized)
            {
                return;
            }

            EnsureMaterials();
            pool = new SimpleObjectPool<EnergyCoin>(CreateEnergyCoin, transform, initialCapacity: 64);
            initialized = true;
        }

        public void ResetRunCoins()
        {
            RunCoins = 0;
            OnCoinCountChanged?.Invoke(RunCoins, TotalCoins);
        }

        public void SyncTotalCoinsFromSave(int savedBalance)
        {
            TotalCoins = Mathf.Max(0, savedBalance);
            OnCoinCountChanged?.Invoke(RunCoins, TotalCoins);
        }

        public void SaveTotalCoins()
        {
            PlayerPrefs.SetInt(TotalCoinsPrefsKey, Mathf.Max(0, TotalCoins));
            PlayerPrefs.Save();
        }

        private void LoadTotalCoins()
        {
            TotalCoins = Mathf.Max(0, PlayerPrefs.GetInt(TotalCoinsPrefsKey, 0));
        }

        /// <summary>
        /// Claims a coin safely (guarded against duplicate collection) and increments balances.
        /// </summary>
        public bool CollectCoin(EnergyCoin coin, int multiplier = 1)
        {
            if (coin == null || !coin.TryCollect())
            {
                return false;
            }

            int award = Mathf.Max(1, coin.CoinValue * Mathf.Max(1, multiplier));
            RunCoins += award;
            TotalCoins += award;
            OnCoinCountChanged?.Invoke(RunCoins, TotalCoins);
            return true;
        }

        /// <summary>
        /// Spawns a straight line of energy coins along a single lane.
        /// </summary>
        public void SpawnLine(int laneIndex, float laneWidth, float startZ, int count, float spacing = 2.4f, float heightY = 1.05f)
        {
            InitializeIfNeeded();
            float x = laneIndex * laneWidth;

            for (int i = 0; i < count; i++)
            {
                SpawnSingle(new Vector3(x, heightY, startZ + (i * spacing)));
            }
        }

        /// <summary>
        /// Spawns a parabolic jump arc of coins centered over a jumpable low barrier.
        /// </summary>
        public void SpawnJumpArc(int laneIndex, float laneWidth, float centerZ, int count = 5, float spanZ = 9.0f, float baseHeightY = 1.0f, float peakBoostY = 1.85f)
        {
            InitializeIfNeeded();
            float x = laneIndex * laneWidth;
            int safeCount = Mathf.Max(3, count);

            for (int i = 0; i < safeCount; i++)
            {
                float u = (float)i / (safeCount - 1); // 0..1
                float z = centerZ - (spanZ * 0.5f) + (u * spanZ);
                float parabola = 4f * u * (1f - u);   // peaks at 1.0 when u = 0.5
                float y = baseHeightY + (parabola * peakBoostY);
                SpawnSingle(new Vector3(x, y, z));
            }
        }

        /// <summary>
        /// Spawns a low ground-skimming trail guiding the player to slide under a laser gate or drone.
        /// </summary>
        public void SpawnSlideTrail(int laneIndex, float laneWidth, float centerZ, int count = 4, float spacing = 2.2f)
        {
            float startZ = centerZ - ((count - 1) * spacing * 0.5f);
            SpawnLine(laneIndex, laneWidth, startZ, count, spacing, heightY: 0.48f);
        }

        private void SpawnSingle(Vector3 worldPos)
        {
            EnergyCoin coin = pool.Rent();
            coin.ActivateAt(worldPos, 1);
            activeCoins.Add(coin);
        }

        /// <summary>
        /// Awards validated bonus Energy Coins (e.g. run milestone or mission rewards).
        /// </summary>
        public void GrantBonusCoins(int bonusAmount)
        {
            if (bonusAmount <= 0)
            {
                return;
            }

            TotalCoins += bonusAmount;
            OnCoinCountChanged?.Invoke(RunCoins, TotalCoins);
        }

        /// <summary>
        /// Ticks all active coins in one centralized loop (including Energy Magnet attraction)
        /// and recycles collected or passed coins.
        /// </summary>
        public void TickActive(
            float deltaTime,
            Vector3 playerPosition,
            bool magnetActive = false,
            float magnetRadius = 12.5f,
            Action<EnergyCoin> onMagnetCollected = null)
        {
            float radiusSq = magnetRadius * magnetRadius;
            for (int i = activeCoins.Count - 1; i >= 0; i--)
            {
                EnergyCoin coin = activeCoins[i];
                bool animDone = coin.Tick(deltaTime, playerPosition, magnetActive, radiusSq, out bool reachedByMagnet);

                if (reachedByMagnet && !coin.IsCollected)
                {
                    onMagnetCollected?.Invoke(coin);
                }

                if (animDone || (!coin.IsCollected && coin.transform.position.z < playerPosition.z - 10f))
                {
                    ReturnAtIndex(i);
                }
            }
        }

        public void ReturnAll()
        {
            for (int i = activeCoins.Count - 1; i >= 0; i--)
            {
                ReturnAtIndex(i);
            }
        }

        private void ReturnAtIndex(int index)
        {
            EnergyCoin coin = activeCoins[index];
            int lastIndex = activeCoins.Count - 1;
            activeCoins[index] = activeCoins[lastIndex];
            activeCoins.RemoveAt(lastIndex);
            pool.Return(coin);
        }

        private void EnsureMaterials()
        {
            coinGoldMaterial = coinGoldMaterial ?? Resources.Load<Material>("Materials/CyberCoin_Gold");
            neonCyanMaterial = neonCyanMaterial ?? Resources.Load<Material>("Materials/CyberNeon_Cyan");

            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Standard");
            if (coinGoldMaterial == null && unlit != null)
            {
                coinGoldMaterial = new Material(unlit);
                coinGoldMaterial.SetColor("_BaseColor", new Color(1.0f, 0.84f, 0.14f, 1f));
                coinGoldMaterial.enableInstancing = true;
            }

            if (neonCyanMaterial == null && unlit != null)
            {
                neonCyanMaterial = new Material(unlit);
                neonCyanMaterial.SetColor("_BaseColor", new Color(0.0f, 0.94f, 1.0f, 1f));
                neonCyanMaterial.enableInstancing = true;
            }
        }

        private EnergyCoin CreateEnergyCoin()
        {
            var root = new GameObject("Collectible_EnergyCoin");

            SphereCollider col = root.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 0.62f;

            EnergyCoin coin = root.AddComponent<EnergyCoin>();

            var visualRoot = new GameObject("Visual");
            visualRoot.transform.SetParent(root.transform, false);

            // Stylized glowing futuristic energy prism (outer gold core + inner cyan energy ring).
            CreateVisualPart("EnergyCore", PrimitiveType.Cube, visualRoot.transform, new Vector3(0.42f, 0.42f, 0.42f), coinGoldMaterial);
            CreateVisualPart("EnergyRing", PrimitiveType.Cube, visualRoot.transform, new Vector3(0.56f, 0.12f, 0.56f), neonCyanMaterial);

            coin.SetVisualTransform(visualRoot.transform);
            return coin;
        }

        private static void CreateVisualPart(string name, PrimitiveType type, Transform parent, Vector3 scale, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;

            Collider col = go.GetComponent<Collider>();
            if (col != null)
            {
                Destroy(col);
            }

            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localScale = scale;

            MeshRenderer renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null && material != null)
            {
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }
    }
}
