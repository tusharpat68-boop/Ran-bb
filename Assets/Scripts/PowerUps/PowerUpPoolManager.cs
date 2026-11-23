using System.Collections.Generic;
using UnityEngine;
using CyberRunner.Core;

namespace CyberRunner.PowerUps
{
    /// <summary>
    /// Pre-warmed pool manager for in-world futuristic Power-Up pickups.
    /// Zero runtime Instantiate/Destroy; centralized single-loop animation and recycling.
    /// </summary>
    public sealed class PowerUpPoolManager : MonoBehaviour
    {
        [Header("Shared Mobile URP Materials")]
        [SerializeField] private Material neonCyanMaterial;
        [SerializeField] private Material neonPurpleMaterial;
        [SerializeField] private Material neonMagentaMaterial;
        [SerializeField] private Material neonGreenMaterial;
        [SerializeField] private Material coinGoldMaterial;
        [SerializeField] private Material hazardAmberMaterial;

        private SimpleObjectPool<PowerUpPickup> pool;
        private readonly List<PowerUpPickup> activePickups = new List<PowerUpPickup>(12);
        private bool initialized;

        public void ConfigureMaterials(
            Material neonCyan,
            Material neonPurple,
            Material neonMagenta,
            Material neonGreen,
            Material coinGold,
            Material hazardAmber)
        {
            neonCyanMaterial = neonCyan;
            neonPurpleMaterial = neonPurple;
            neonMagentaMaterial = neonMagenta;
            neonGreenMaterial = neonGreen;
            coinGoldMaterial = coinGold;
            hazardAmberMaterial = hazardAmber;
        }

        private void Awake()
        {
            InitializeIfNeeded();
        }

        public void InitializeIfNeeded()
        {
            if (initialized)
            {
                return;
            }

            EnsureMaterials();
            pool = new SimpleObjectPool<PowerUpPickup>(CreatePickup, transform, initialCapacity: 8);
            initialized = true;
        }

        public PowerUpPickup Spawn(PowerUpType type, int laneIndex, float laneWidth, float zPosition, float heightY = 1.25f)
        {
            InitializeIfNeeded();

            PowerUpPickup pickup = pool.Rent();
            Vector3 worldPos = new Vector3(laneIndex * laneWidth, heightY, zPosition);
            pickup.ActivateAt(type, worldPos, GetMaterialForType(type));
            activePickups.Add(pickup);
            return pickup;
        }

        public void TickActive(float deltaTime, float playerZ)
        {
            for (int i = activePickups.Count - 1; i >= 0; i--)
            {
                PowerUpPickup pickup = activePickups[i];
                if (pickup.IsCollected || pickup.transform.position.z < playerZ - 10f)
                {
                    ReturnAtIndex(i);
                    continue;
                }

                pickup.Tick(deltaTime);
            }
        }

        public void ReturnAll()
        {
            for (int i = activePickups.Count - 1; i >= 0; i--)
            {
                ReturnAtIndex(i);
            }
        }

        private void ReturnAtIndex(int index)
        {
            PowerUpPickup pickup = activePickups[index];
            int lastIndex = activePickups.Count - 1;
            activePickups[index] = activePickups[lastIndex];
            activePickups.RemoveAt(lastIndex);
            pool.Return(pickup);
        }

        private Material GetMaterialForType(PowerUpType type)
        {
            switch (type)
            {
                case PowerUpType.EnergyMagnet: return coinGoldMaterial;
                case PowerUpType.CyberShield: return neonCyanMaterial;
                case PowerUpType.SpeedBoost: return neonMagentaMaterial;
                case PowerUpType.DoubleEnergy: return neonGreenMaterial;
                case PowerUpType.HoverBoots: return neonPurpleMaterial;
                default: return hazardAmberMaterial;
            }
        }

        private void EnsureMaterials()
        {
            neonCyanMaterial = neonCyanMaterial ?? Resources.Load<Material>("Materials/CyberNeon_Cyan");
            neonPurpleMaterial = neonPurpleMaterial ?? Resources.Load<Material>("Materials/CyberNeon_Purple");
            neonMagentaMaterial = neonMagentaMaterial ?? Resources.Load<Material>("Materials/CyberNeon_Magenta");
            neonGreenMaterial = neonGreenMaterial ?? Resources.Load<Material>("Materials/CyberNeon_Green");
            coinGoldMaterial = coinGoldMaterial ?? Resources.Load<Material>("Materials/CyberCoin_Gold");
            hazardAmberMaterial = hazardAmberMaterial ?? Resources.Load<Material>("Materials/CyberHazard_Amber");

            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Standard");
            neonCyanMaterial = neonCyanMaterial ?? CreateFallback(unlit, new Color(0.0f, 0.94f, 1.0f, 1f));
            neonPurpleMaterial = neonPurpleMaterial ?? CreateFallback(unlit, new Color(0.68f, 0.22f, 1.0f, 1f));
            neonMagentaMaterial = neonMagentaMaterial ?? CreateFallback(unlit, new Color(0.96f, 0.18f, 0.92f, 1f));
            neonGreenMaterial = neonGreenMaterial ?? CreateFallback(unlit, new Color(0.16f, 0.96f, 0.45f, 1f));
            coinGoldMaterial = coinGoldMaterial ?? CreateFallback(unlit, new Color(1.0f, 0.84f, 0.14f, 1f));
            hazardAmberMaterial = hazardAmberMaterial ?? CreateFallback(unlit, new Color(1.0f, 0.62f, 0.08f, 1f));
        }

        private static Material CreateFallback(Shader shader, Color color)
        {
            if (shader == null) return null;
            var mat = new Material(shader);
            mat.SetColor("_BaseColor", color);
            mat.enableInstancing = true;
            return mat;
        }

        private PowerUpPickup CreatePickup()
        {
            var root = new GameObject("Pickup_PowerUpBeacon");
            SphereCollider col = root.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 0.85f;

            PowerUpPickup pickup = root.AddComponent<PowerUpPickup>();

            GameObject core = CreateMesh("Core", PrimitiveType.Cube, root.transform, new Vector3(0.60f, 0.60f, 0.60f), neonCyanMaterial);
            var ringPivot = new GameObject("HaloRing").transform;
            ringPivot.SetParent(root.transform, false);
            CreateMesh("RingTop", PrimitiveType.Cube, ringPivot, new Vector3(0.95f, 0.10f, 0.10f), neonCyanMaterial, new Vector3(0f, 0.48f, 0f));
            CreateMesh("RingBot", PrimitiveType.Cube, ringPivot, new Vector3(0.95f, 0.10f, 0.10f), neonCyanMaterial, new Vector3(0f, -0.48f, 0f));
            CreateMesh("RingL", PrimitiveType.Cube, ringPivot, new Vector3(0.10f, 0.95f, 0.10f), neonCyanMaterial, new Vector3(-0.48f, 0f, 0f));
            CreateMesh("RingR", PrimitiveType.Cube, ringPivot, new Vector3(0.10f, 0.95f, 0.10f), neonCyanMaterial, new Vector3(0.48f, 0f, 0f));

            pickup.SetVisualReferences(core.transform, ringPivot, core.GetComponent<MeshRenderer>());
            return pickup;
        }

        private static GameObject CreateMesh(
            string name,
            PrimitiveType type,
            Transform parent,
            Vector3 scale,
            Material mat,
            Vector3? localPos = null)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;

            Collider col = go.GetComponent<Collider>();
            if (col != null)
            {
                Destroy(col);
            }

            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos ?? Vector3.zero;
            go.transform.localScale = scale;

            MeshRenderer renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null && mat != null)
            {
                renderer.sharedMaterial = mat;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            return go;
        }
    }
}
