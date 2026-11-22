using System.Collections.Generic;
using UnityEngine;
using CyberRunner.Core;

namespace CyberRunner.Obstacles
{
    /// <summary>
    /// Pre-warmed object pool manager for all 7 cyberpunk obstacle categories across Phases 2 & 3.
    /// Zero runtime Instantiate/Destroy during active runs; single centralized TickActive loop.
    /// </summary>
    public sealed class ObstaclePoolManager : MonoBehaviour
    {
        [Header("Shared Mobile URP Materials")]
        [SerializeField] private Material buildingDarkMaterial;
        [SerializeField] private Material trainBodyMaterial;
        [SerializeField] private Material neonCyanMaterial;
        [SerializeField] private Material neonPurpleMaterial;
        [SerializeField] private Material neonMagentaMaterial;
        [SerializeField] private Material hazardRedMaterial;
        [SerializeField] private Material hazardAmberMaterial;

        private readonly Dictionary<ObstacleType, SimpleObjectPool<ObstacleController>> pools =
            new Dictionary<ObstacleType, SimpleObjectPool<ObstacleController>>(7);

        private readonly List<ObstacleController> activeObstacles = new List<ObstacleController>(56);
        private bool initialized;

        public int ActiveCount => activeObstacles.Count;

        public void ConfigureMaterials(
            Material buildingDark,
            Material trainBody,
            Material neonCyan,
            Material neonPurple,
            Material hazardRed,
            Material hazardAmber,
            Material neonMagenta = null)
        {
            buildingDarkMaterial = buildingDark;
            trainBodyMaterial = trainBody;
            neonCyanMaterial = neonCyan;
            neonPurpleMaterial = neonPurple;
            hazardRedMaterial = hazardRed;
            hazardAmberMaterial = hazardAmber;
            neonMagentaMaterial = neonMagenta;
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

            pools[ObstacleType.LowRoadBarrier] = new SimpleObjectPool<ObstacleController>(
                CreateLowRoadBarrier, transform, initialCapacity: 12);

            pools[ObstacleType.LaserGate] = new SimpleObjectPool<ObstacleController>(
                CreateLaserGate, transform, initialCapacity: 12);

            pools[ObstacleType.TallCyberBlock] = new SimpleObjectPool<ObstacleController>(
                CreateTallCyberBlock, transform, initialCapacity: 12);

            pools[ObstacleType.MovingCyberTrain] = new SimpleObjectPool<ObstacleController>(
                CreateMovingCyberTrain, transform, initialCapacity: 4);

            pools[ObstacleType.HoverSecurityDrone] = new SimpleObjectPool<ObstacleController>(
                CreateHoverSecurityDrone, transform, initialCapacity: 8);

            pools[ObstacleType.RotatingIndustrialBar] = new SimpleObjectPool<ObstacleController>(
                CreateRotatingIndustrialBar, transform, initialCapacity: 8);

            pools[ObstacleType.ElectricEnergyWall] = new SimpleObjectPool<ObstacleController>(
                CreateElectricEnergyWall, transform, initialCapacity: 10);

            initialized = true;
        }

        public ObstacleController Spawn(ObstacleType type, int laneIndex, float laneWidth, float zPosition)
        {
            InitializeIfNeeded();

            ObstacleController obstacle = pools[type].Rent();
            float worldX = laneIndex * laneWidth;
            obstacle.ActivateAt(laneIndex, new Vector3(worldX, 0f, zPosition));
            activeObstacles.Add(obstacle);
            return obstacle;
        }

        public void TickActive(float deltaTime, float playerZ)
        {
            for (int i = activeObstacles.Count - 1; i >= 0; i--)
            {
                ObstacleController obs = activeObstacles[i];
                obs.Tick(deltaTime, playerZ);

                float backEdgeZ = obs.transform.position.z + obs.HalfLengthZ;
                if (backEdgeZ < playerZ - 14f)
                {
                    ReturnAtIndex(i);
                }
            }
        }

        public void ReturnAll()
        {
            for (int i = activeObstacles.Count - 1; i >= 0; i--)
            {
                ReturnAtIndex(i);
            }
        }

        private void ReturnAtIndex(int index)
        {
            ObstacleController obs = activeObstacles[index];
            int lastIndex = activeObstacles.Count - 1;
            activeObstacles[index] = activeObstacles[lastIndex];
            activeObstacles.RemoveAt(lastIndex);

            if (pools.TryGetValue(obs.Type, out SimpleObjectPool<ObstacleController> pool))
            {
                pool.Return(obs);
            }
        }

        private void EnsureMaterials()
        {
            buildingDarkMaterial = buildingDarkMaterial ?? Resources.Load<Material>("Materials/CyberBuilding_Dark");
            trainBodyMaterial = trainBodyMaterial ?? Resources.Load<Material>("Materials/CyberTrain_Body");
            neonCyanMaterial = neonCyanMaterial ?? Resources.Load<Material>("Materials/CyberNeon_Cyan");
            neonPurpleMaterial = neonPurpleMaterial ?? Resources.Load<Material>("Materials/CyberNeon_Purple");
            neonMagentaMaterial = neonMagentaMaterial ?? Resources.Load<Material>("Materials/CyberNeon_Magenta");
            hazardRedMaterial = hazardRedMaterial ?? Resources.Load<Material>("Materials/CyberHazard_Red");
            hazardAmberMaterial = hazardAmberMaterial ?? Resources.Load<Material>("Materials/CyberHazard_Amber");

            Shader simpleLit = Shader.Find("Universal Render Pipeline/Simple Lit") ?? Shader.Find("Standard");
            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit") ?? simpleLit;

            buildingDarkMaterial = buildingDarkMaterial ?? CreateFallback(simpleLit, new Color(0.06f, 0.08f, 0.14f, 1f));
            trainBodyMaterial = trainBodyMaterial ?? CreateFallback(simpleLit, new Color(0.11f, 0.14f, 0.22f, 1f));
            neonCyanMaterial = neonCyanMaterial ?? CreateFallback(unlit, new Color(0.0f, 0.94f, 1.0f, 1f));
            neonPurpleMaterial = neonPurpleMaterial ?? CreateFallback(unlit, new Color(0.68f, 0.22f, 1.0f, 1f));
            neonMagentaMaterial = neonMagentaMaterial ?? CreateFallback(unlit, new Color(0.96f, 0.18f, 0.92f, 1f));
            hazardRedMaterial = hazardRedMaterial ?? CreateFallback(unlit, new Color(1.0f, 0.14f, 0.28f, 1f));
            hazardAmberMaterial = hazardAmberMaterial ?? CreateFallback(unlit, new Color(1.0f, 0.62f, 0.08f, 1f));
        }

        private static Material CreateFallback(Shader shader, Color color)
        {
            if (shader == null)
            {
                return null;
            }

            var mat = new Material(shader);
            mat.SetColor("_BaseColor", color);
            mat.enableInstancing = true;
            return mat;
        }

        private ObstacleController CreateLowRoadBarrier()
        {
            var root = new GameObject("Obstacle_LowRoadBarrier");
            BoxCollider col = root.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.center = new Vector3(0f, 0.52f, 0f);
            col.size = new Vector3(2.25f, 1.04f, 0.72f);

            ObstacleController ctrl = root.AddComponent<ObstacleController>();
            ctrl.InitializeSpec(
                ObstacleType.LowRoadBarrier, "ROAD BARRIER",
                canJumpOver: true, canSlideUnder: false,
                clearanceBottomY: 0f, clearanceTopY: 1.04f, halfLengthZ: 0.45f,
                approachSpeedZ: 0f, motionActivationDist: 0f, droneHoverAmp: 0f, droneHoverFreq: 0f);

            Transform tr = root.transform;
            CreatePart("LeftFoot", PrimitiveType.Cube, tr, new Vector3(-0.98f, 0.46f, 0f), new Vector3(0.28f, 0.92f, 0.68f), buildingDarkMaterial);
            CreatePart("RightFoot", PrimitiveType.Cube, tr, new Vector3(0.98f, 0.46f, 0f), new Vector3(0.28f, 0.92f, 0.68f), buildingDarkMaterial);
            CreatePart("BarrierBody", PrimitiveType.Cube, tr, new Vector3(0f, 0.58f, 0f), new Vector3(2.10f, 0.54f, 0.42f), buildingDarkMaterial);
            CreatePart("AmberTopStrip", PrimitiveType.Cube, tr, new Vector3(0f, 0.96f, 0f), new Vector3(2.28f, 0.16f, 0.48f), hazardAmberMaterial);
            CreatePart("AmberFrontGlow", PrimitiveType.Cube, tr, new Vector3(0f, 0.58f, -0.23f), new Vector3(1.85f, 0.22f, 0.05f), hazardAmberMaterial);
            return ctrl;
        }

        private ObstacleController CreateLaserGate()
        {
            var root = new GameObject("Obstacle_LaserGate");
            BoxCollider col = root.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.center = new Vector3(0f, 1.86f, 0f);
            col.size = new Vector3(2.30f, 1.36f, 0.55f);

            ObstacleController ctrl = root.AddComponent<ObstacleController>();
            ctrl.InitializeSpec(
                ObstacleType.LaserGate, "LASER GATE",
                canJumpOver: false, canSlideUnder: true,
                clearanceBottomY: 1.18f, clearanceTopY: 2.65f, halfLengthZ: 0.40f,
                approachSpeedZ: 0f, motionActivationDist: 0f, droneHoverAmp: 0f, droneHoverFreq: 0f);

            Transform tr = root.transform;
            CreatePart("LeftEmitter", PrimitiveType.Cube, tr, new Vector3(-1.22f, 1.38f, 0f), new Vector3(0.24f, 2.76f, 0.34f), buildingDarkMaterial);
            CreatePart("RightEmitter", PrimitiveType.Cube, tr, new Vector3(1.22f, 1.38f, 0f), new Vector3(0.24f, 2.76f, 0.34f), buildingDarkMaterial);
            CreatePart("TopBridge", PrimitiveType.Cube, tr, new Vector3(0f, 2.68f, 0f), new Vector3(2.68f, 0.24f, 0.38f), buildingDarkMaterial);
            CreatePart("LaserBeamLow", PrimitiveType.Cube, tr, new Vector3(0f, 1.28f, 0f), new Vector3(2.26f, 0.12f, 0.12f), hazardRedMaterial);
            CreatePart("LaserBeamMid", PrimitiveType.Cube, tr, new Vector3(0f, 1.78f, 0f), new Vector3(2.26f, 0.12f, 0.12f), hazardRedMaterial);
            CreatePart("LaserBeamHigh", PrimitiveType.Cube, tr, new Vector3(0f, 2.28f, 0f), new Vector3(2.26f, 0.12f, 0.12f), hazardRedMaterial);
            CreatePart("WarningSign", PrimitiveType.Cube, tr, new Vector3(0f, 2.68f, -0.21f), new Vector3(1.20f, 0.14f, 0.05f), hazardRedMaterial);
            return ctrl;
        }

        private ObstacleController CreateTallCyberBlock()
        {
            var root = new GameObject("Obstacle_TallCyberBlock");
            BoxCollider col = root.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.center = new Vector3(0f, 1.55f, 0f);
            col.size = new Vector3(2.20f, 3.10f, 1.65f);

            ObstacleController ctrl = root.AddComponent<ObstacleController>();
            ctrl.InitializeSpec(
                ObstacleType.TallCyberBlock, "SECURITY MONOLITH",
                canJumpOver: false, canSlideUnder: false,
                clearanceBottomY: 0f, clearanceTopY: 3.10f, halfLengthZ: 0.90f,
                approachSpeedZ: 0f, motionActivationDist: 0f, droneHoverAmp: 0f, droneHoverFreq: 0f);

            Transform tr = root.transform;
            CreatePart("MonolithBody", PrimitiveType.Cube, tr, new Vector3(0f, 1.55f, 0f), new Vector3(2.16f, 3.10f, 1.60f), buildingDarkMaterial);
            CreatePart("FrontCrimsonCore", PrimitiveType.Cube, tr, new Vector3(0f, 1.65f, -0.82f), new Vector3(0.45f, 2.35f, 0.06f), hazardRedMaterial);
            CreatePart("LeftPurpleTrim", PrimitiveType.Cube, tr, new Vector3(-1.02f, 1.55f, -0.82f), new Vector3(0.10f, 2.95f, 0.08f), neonPurpleMaterial);
            CreatePart("RightPurpleTrim", PrimitiveType.Cube, tr, new Vector3(1.02f, 1.55f, -0.82f), new Vector3(0.10f, 2.95f, 0.08f), neonPurpleMaterial);
            CreatePart("TopCapGlow", PrimitiveType.Cube, tr, new Vector3(0f, 3.12f, 0f), new Vector3(2.22f, 0.12f, 1.66f), hazardRedMaterial);
            return ctrl;
        }

        private ObstacleController CreateMovingCyberTrain()
        {
            var root = new GameObject("Obstacle_MovingCyberTrain");
            BoxCollider col = root.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.center = new Vector3(0f, 1.55f, 0f);
            col.size = new Vector3(2.28f, 3.10f, 11.2f);

            ObstacleController ctrl = root.AddComponent<ObstacleController>();
            ctrl.InitializeSpec(
                ObstacleType.MovingCyberTrain, "CYBER MAGLEV TRAIN",
                canJumpOver: false, canSlideUnder: false,
                clearanceBottomY: 0f, clearanceTopY: 3.10f, halfLengthZ: 5.8f,
                approachSpeedZ: 9.5f, motionActivationDist: 78f, droneHoverAmp: 0f, droneHoverFreq: 0f);

            Transform tr = root.transform;
            CreatePart("TrainHull", PrimitiveType.Cube, tr, new Vector3(0f, 1.55f, 0f), new Vector3(2.28f, 2.85f, 11.2f), trainBodyMaterial);
            CreatePart("MaglevSkirt", PrimitiveType.Cube, tr, new Vector3(0f, 0.16f, 0f), new Vector3(2.16f, 0.24f, 10.8f), neonCyanMaterial);
            CreatePart("FrontWindshield", PrimitiveType.Cube, tr, new Vector3(0f, 2.08f, -5.62f), new Vector3(1.88f, 0.85f, 0.08f), neonCyanMaterial);
            CreatePart("HeadlightLeft", PrimitiveType.Cube, tr, new Vector3(-0.76f, 0.92f, -5.62f), new Vector3(0.42f, 0.24f, 0.08f), hazardAmberMaterial);
            CreatePart("HeadlightRight", PrimitiveType.Cube, tr, new Vector3(0.76f, 0.92f, -5.62f), new Vector3(0.42f, 0.24f, 0.08f), hazardAmberMaterial);
            CreatePart("LeftSideStripe", PrimitiveType.Cube, tr, new Vector3(-1.16f, 1.55f, 0f), new Vector3(0.06f, 0.20f, 10.6f), hazardRedMaterial);
            CreatePart("RightSideStripe", PrimitiveType.Cube, tr, new Vector3(1.16f, 1.55f, 0f), new Vector3(0.06f, 0.20f, 10.6f), hazardRedMaterial);
            CreatePart("RoofSpine", PrimitiveType.Cube, tr, new Vector3(0f, 3.02f, 0f), new Vector3(0.55f, 0.14f, 10.4f), neonPurpleMaterial);
            return ctrl;
        }

        private ObstacleController CreateHoverSecurityDrone()
        {
            var root = new GameObject("Obstacle_HoverSecurityDrone");
            BoxCollider col = root.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.center = new Vector3(0f, 1.72f, 0f);
            col.size = new Vector3(1.85f, 1.00f, 1.15f);

            ObstacleController ctrl = root.AddComponent<ObstacleController>();
            ctrl.InitializeSpec(
                ObstacleType.HoverSecurityDrone, "SECURITY DRONE",
                canJumpOver: false, canSlideUnder: true,
                clearanceBottomY: 1.18f, clearanceTopY: 2.30f, halfLengthZ: 0.65f,
                approachSpeedZ: 0f, motionActivationDist: 0f, droneHoverAmp: 0.14f, droneHoverFreq: 3.4f);

            Transform tr = root.transform;
            CreatePart("DroneChassis", PrimitiveType.Sphere, tr, new Vector3(0f, 1.72f, 0f), new Vector3(1.10f, 0.68f, 1.05f), buildingDarkMaterial);
            CreatePart("ScannerEye", PrimitiveType.Cube, tr, new Vector3(0f, 1.70f, -0.52f), new Vector3(0.56f, 0.18f, 0.12f), hazardRedMaterial);
            CreatePart("LeftThrusterWing", PrimitiveType.Cube, tr, new Vector3(-0.78f, 1.76f, 0f), new Vector3(0.52f, 0.16f, 0.44f), trainBodyMaterial);
            CreatePart("RightThrusterWing", PrimitiveType.Cube, tr, new Vector3(0.78f, 1.76f, 0f), new Vector3(0.52f, 0.16f, 0.44f), trainBodyMaterial);
            CreatePart("LeftThrusterGlow", PrimitiveType.Cube, tr, new Vector3(-0.78f, 1.62f, 0f), new Vector3(0.34f, 0.10f, 0.34f), neonCyanMaterial);
            CreatePart("RightThrusterGlow", PrimitiveType.Cube, tr, new Vector3(0.78f, 1.62f, 0f), new Vector3(0.34f, 0.10f, 0.34f), neonCyanMaterial);
            return ctrl;
        }

        private ObstacleController CreateRotatingIndustrialBar()
        {
            var root = new GameObject("Obstacle_RotatingIndustrialBar");
            BoxCollider col = root.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.center = new Vector3(0f, 0.56f, 0f);
            col.size = new Vector3(2.25f, 1.10f, 1.65f);

            ObstacleController ctrl = root.AddComponent<ObstacleController>();
            ctrl.InitializeSpec(
                ObstacleType.RotatingIndustrialBar, "INDUSTRIAL ROTOR",
                canJumpOver: true, canSlideUnder: false,
                clearanceBottomY: 0f, clearanceTopY: 1.08f, halfLengthZ: 0.90f,
                approachSpeedZ: 0f, motionActivationDist: 0f, droneHoverAmp: 0f, droneHoverFreq: 0f);

            Transform tr = root.transform;
            CreatePart("RotorPedestal", PrimitiveType.Cylinder, tr, new Vector3(0f, 0.45f, 0f), new Vector3(0.65f, 0.45f, 0.65f), trainBodyMaterial);

            var spinner = new GameObject("SpinnerPivot").transform;
            spinner.SetParent(tr, false);
            spinner.localPosition = new Vector3(0f, 0.88f, 0f);
            CreatePart("BladeMain", PrimitiveType.Cube, spinner, Vector3.zero, new Vector3(2.35f, 0.18f, 0.26f), hazardAmberMaterial);
            CreatePart("BladeCross", PrimitiveType.Cube, spinner, Vector3.zero, new Vector3(0.26f, 0.18f, 2.35f), hazardRedMaterial);
            CreatePart("CenterCap", PrimitiveType.Sphere, spinner, new Vector3(0f, 0.14f, 0f), new Vector3(0.45f, 0.30f, 0.45f), neonCyanMaterial);

            ctrl.SetSpinnerPivot(spinner, 210f);
            return ctrl;
        }

        private ObstacleController CreateElectricEnergyWall()
        {
            var root = new GameObject("Obstacle_ElectricEnergyWall");
            BoxCollider col = root.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.center = new Vector3(0f, 1.50f, 0f);
            col.size = new Vector3(2.25f, 3.00f, 0.60f);

            ObstacleController ctrl = root.AddComponent<ObstacleController>();
            ctrl.InitializeSpec(
                ObstacleType.ElectricEnergyWall, "ENERGY WALL",
                canJumpOver: false, canSlideUnder: false,
                clearanceBottomY: 0f, clearanceTopY: 3.00f, halfLengthZ: 0.45f,
                approachSpeedZ: 0f, motionActivationDist: 0f, droneHoverAmp: 0f, droneHoverFreq: 0f);

            Transform tr = root.transform;
            CreatePart("EmitterL", PrimitiveType.Cube, tr, new Vector3(-1.12f, 1.50f, 0f), new Vector3(0.26f, 3.05f, 0.38f), buildingDarkMaterial);
            CreatePart("EmitterR", PrimitiveType.Cube, tr, new Vector3(1.12f, 1.50f, 0f), new Vector3(0.26f, 3.05f, 0.38f), buildingDarkMaterial);
            CreatePart("PlasmaField", PrimitiveType.Cube, tr, new Vector3(0f, 1.50f, 0f), new Vector3(2.05f, 2.75f, 0.10f), neonMagentaMaterial);
            CreatePart("ArcBar1", PrimitiveType.Cube, tr, new Vector3(0f, 0.70f, -0.06f), new Vector3(2.00f, 0.12f, 0.08f), neonCyanMaterial);
            CreatePart("ArcBar2", PrimitiveType.Cube, tr, new Vector3(0f, 1.50f, -0.06f), new Vector3(2.00f, 0.12f, 0.08f), neonCyanMaterial);
            CreatePart("ArcBar3", PrimitiveType.Cube, tr, new Vector3(0f, 2.30f, -0.06f), new Vector3(2.00f, 0.12f, 0.08f), neonCyanMaterial);
            return ctrl;
        }

        private static void CreatePart(
            string name,
            PrimitiveType primitiveType,
            Transform parent,
            Vector3 localPos,
            Vector3 localScale,
            Material material)
        {
            GameObject go = GameObject.CreatePrimitive(primitiveType);
            go.name = name;

            Collider col = go.GetComponent<Collider>();
            if (col != null)
            {
                Destroy(col);
            }

            Transform tr = go.transform;
            tr.SetParent(parent, false);
            tr.localPosition = localPos;
            tr.localRotation = Quaternion.identity;
            tr.localScale = localScale;

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
