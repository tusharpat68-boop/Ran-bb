using UnityEngine;

namespace CyberRunner.PowerUps
{
    /// <summary>
    /// Pre-allocated, mobile-friendly 3D visual effects attached to the player character
    /// for all six CYBER RUNNER power-ups (including Support Drone companion & firing laser pulse).
    /// Zero particle allocation spikes on 2GB-3GB Android devices.
    /// </summary>
    public sealed class PlayerPowerUpVisuals : MonoBehaviour
    {
        [Header("Shared Mobile URP Materials")]
        [SerializeField] private Material neonCyanMaterial;
        [SerializeField] private Material neonPurpleMaterial;
        [SerializeField] private Material neonMagentaMaterial;
        [SerializeField] private Material neonGreenMaterial;
        [SerializeField] private Material coinGoldMaterial;
        [SerializeField] private Material hazardAmberMaterial;
        [SerializeField] private Material suitDarkMaterial;

        private Transform shieldRing;
        private Transform magnetRing;
        private Transform boostTrailRoot;
        private Transform doubleEnergyHalo;
        private Transform hoverBootsPads;
        private Transform supportDrone;
        private Transform droneLaserBeam;

        private float laserBeamTimer;
        private float animTime;
        private bool built;

        public void ConfigureMaterials(
            Material suitDark,
            Material neonCyan,
            Material neonPurple,
            Material neonMagenta,
            Material neonGreen,
            Material coinGold,
            Material hazardAmber)
        {
            suitDarkMaterial = suitDark;
            neonCyanMaterial = neonCyan;
            neonPurpleMaterial = neonPurple;
            neonMagentaMaterial = neonMagenta;
            neonGreenMaterial = neonGreen;
            coinGoldMaterial = coinGold;
            hazardAmberMaterial = hazardAmber;
        }

        private void Awake()
        {
            EnsureMaterials();
            BuildVisualsIfNeeded();
        }

        public void SetEffectActive(PowerUpType type, bool active)
        {
            BuildVisualsIfNeeded();
            switch (type)
            {
                case PowerUpType.CyberShield:
                    if (shieldRing != null) shieldRing.gameObject.SetActive(active);
                    break;
                case PowerUpType.EnergyMagnet:
                    if (magnetRing != null) magnetRing.gameObject.SetActive(active);
                    break;
                case PowerUpType.SpeedBoost:
                    if (boostTrailRoot != null) boostTrailRoot.gameObject.SetActive(active);
                    break;
                case PowerUpType.DoubleEnergy:
                    if (doubleEnergyHalo != null) doubleEnergyHalo.gameObject.SetActive(active);
                    break;
                case PowerUpType.HoverBoots:
                    if (hoverBootsPads != null) hoverBootsPads.gameObject.SetActive(active);
                    break;
                case PowerUpType.DroneSupport:
                    if (supportDrone != null) supportDrone.gameObject.SetActive(active);
                    if (!active && droneLaserBeam != null) droneLaserBeam.gameObject.SetActive(false);
                    break;
            }
        }

        public void DisableAll()
        {
            for (int i = 0; i < PowerUpCatalog.PowerUpCount; i++)
            {
                SetEffectActive((PowerUpType)i, false);
            }
            laserBeamTimer = 0f;
        }

        /// <summary>
        /// Fires a brief visible cyan laser pulse from the companion Support Drone to a neutralized obstacle.
        /// </summary>
        public void FireDroneLaserAt(Vector3 targetWorldPos)
        {
            BuildVisualsIfNeeded();
            if (supportDrone == null || droneLaserBeam == null)
            {
                return;
            }

            Vector3 origin = supportDrone.position;
            Vector3 toTarget = targetWorldPos - origin;
            float dist = Mathf.Max(0.5f, toTarget.magnitude);

            droneLaserBeam.gameObject.SetActive(true);
            droneLaserBeam.position = origin + (toTarget * 0.5f);
            droneLaserBeam.rotation = Quaternion.LookRotation(toTarget.normalized);
            droneLaserBeam.localScale = new Vector3(0.14f, 0.14f, dist);
            laserBeamTimer = 0.16f;
        }

        public void TickVisuals(float deltaTime)
        {
            if (!built)
            {
                return;
            }

            animTime += deltaTime;

            if (shieldRing != null && shieldRing.gameObject.activeSelf)
            {
                shieldRing.Rotate(0f, 130f * deltaTime, 45f * deltaTime, Space.Self);
            }

            if (magnetRing != null && magnetRing.gameObject.activeSelf)
            {
                float pulse = 1.0f + (Mathf.Sin(animTime * 9f) * 0.12f);
                magnetRing.localScale = new Vector3(pulse, 1f, pulse);
                magnetRing.Rotate(0f, -150f * deltaTime, 0f, Space.Self);
            }

            if (doubleEnergyHalo != null && doubleEnergyHalo.gameObject.activeSelf)
            {
                doubleEnergyHalo.Rotate(0f, 180f * deltaTime, 0f, Space.Self);
            }

            if (hoverBootsPads != null && hoverBootsPads.gameObject.activeSelf)
            {
                float thrust = 0.85f + (Mathf.Sin(animTime * 22f) * 0.2f);
                hoverBootsPads.localScale = new Vector3(1f, thrust, 1f);
            }

            if (supportDrone != null && supportDrone.gameObject.activeSelf)
            {
                float bob = Mathf.Sin(animTime * 4.5f) * 0.16f;
                supportDrone.localPosition = new Vector3(1.12f, 2.25f + bob, 0.35f);
            }

            if (laserBeamTimer > 0f)
            {
                laserBeamTimer -= deltaTime;
                if (laserBeamTimer <= 0f && droneLaserBeam != null)
                {
                    droneLaserBeam.gameObject.SetActive(false);
                }
            }
        }

        private void EnsureMaterials()
        {
            suitDarkMaterial = suitDarkMaterial ?? Resources.Load<Material>("Materials/CyberSuit_Dark");
            neonCyanMaterial = neonCyanMaterial ?? Resources.Load<Material>("Materials/CyberNeon_Cyan");
            neonPurpleMaterial = neonPurpleMaterial ?? Resources.Load<Material>("Materials/CyberNeon_Purple");
            neonMagentaMaterial = neonMagentaMaterial ?? Resources.Load<Material>("Materials/CyberNeon_Magenta");
            neonGreenMaterial = neonGreenMaterial ?? Resources.Load<Material>("Materials/CyberNeon_Green");
            coinGoldMaterial = coinGoldMaterial ?? Resources.Load<Material>("Materials/CyberCoin_Gold");
            hazardAmberMaterial = hazardAmberMaterial ?? Resources.Load<Material>("Materials/CyberHazard_Amber");

            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Standard");
            suitDarkMaterial = suitDarkMaterial ?? CreateFallback(unlit, new Color(0.08f, 0.10f, 0.15f, 1f));
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

        private void BuildVisualsIfNeeded()
        {
            if (built)
            {
                return;
            }

            // 1. Cyber Shield orbiting ring
            shieldRing = new GameObject("FX_CyberShield").transform;
            shieldRing.SetParent(transform, false);
            shieldRing.localPosition = new Vector3(0f, 1.05f, 0f);
            CreateSquareRing(shieldRing, 0.82f, 0.08f, neonCyanMaterial);

            // 2. Energy Magnet pulsing waist ring
            magnetRing = new GameObject("FX_EnergyMagnet").transform;
            magnetRing.SetParent(transform, false);
            magnetRing.localPosition = new Vector3(0f, 0.95f, 0f);
            CreateSquareRing(magnetRing, 0.95f, 0.07f, coinGoldMaterial);

            // 3. Speed Boost slipstream fins
            boostTrailRoot = new GameObject("FX_SpeedBoost").transform;
            boostTrailRoot.SetParent(transform, false);
            CreatePart("BoostWingL", PrimitiveType.Cube, boostTrailRoot, new Vector3(-0.62f, 1.05f, -0.75f), new Vector3(0.08f, 0.20f, 1.85f), neonMagentaMaterial);
            CreatePart("BoostWingR", PrimitiveType.Cube, boostTrailRoot, new Vector3(0.62f, 1.05f, -0.75f), new Vector3(0.08f, 0.20f, 1.85f), neonMagentaMaterial);
            CreatePart("BoostCore", PrimitiveType.Cube, boostTrailRoot, new Vector3(0f, 1.10f, -0.95f), new Vector3(0.14f, 0.14f, 2.20f), neonCyanMaterial);

            // 4. Double Energy overhead green halo
            doubleEnergyHalo = new GameObject("FX_DoubleEnergy").transform;
            doubleEnergyHalo.SetParent(transform, false);
            doubleEnergyHalo.localPosition = new Vector3(0f, 2.15f, 0f);
            CreateSquareRing(doubleEnergyHalo, 0.48f, 0.07f, neonGreenMaterial);

            // 5. Hover Boots glowing plasma pads underneath shoes
            hoverBootsPads = new GameObject("FX_HoverBoots").transform;
            hoverBootsPads.SetParent(transform, false);
            hoverBootsPads.localPosition = new Vector3(0f, -0.06f, 0f);
            CreatePart("HoverPadL", PrimitiveType.Cylinder, hoverBootsPads, new Vector3(-0.18f, -0.10f, 0.04f), new Vector3(0.32f, 0.14f, 0.44f), neonPurpleMaterial);
            CreatePart("HoverPadR", PrimitiveType.Cylinder, hoverBootsPads, new Vector3(0.18f, -0.10f, 0.04f), new Vector3(0.32f, 0.14f, 0.44f), neonCyanMaterial);

            // 6. Companion Support Drone + laser beam
            supportDrone = new GameObject("FX_SupportDrone").transform;
            supportDrone.SetParent(transform, false);
            supportDrone.localPosition = new Vector3(1.12f, 2.25f, 0.35f);
            CreatePart("DroneBody", PrimitiveType.Sphere, supportDrone, Vector3.zero, new Vector3(0.48f, 0.32f, 0.52f), suitDarkMaterial);
            CreatePart("DroneCore", PrimitiveType.Cube, supportDrone, new Vector3(0f, 0f, 0.24f), new Vector3(0.24f, 0.12f, 0.10f), hazardAmberMaterial);
            CreatePart("DroneRing", PrimitiveType.Cube, supportDrone, Vector3.zero, new Vector3(0.76f, 0.06f, 0.16f), neonCyanMaterial);

            GameObject beam = CreatePart("FX_DroneLaserBeam", PrimitiveType.Cube, null, Vector3.zero, new Vector3(0.14f, 0.14f, 1f), neonCyanMaterial);
            droneLaserBeam = beam.transform;
            droneLaserBeam.gameObject.SetActive(false);

            built = true;
            DisableAll();
        }

        private static void CreateSquareRing(Transform parent, float halfExtent, float thickness, Material mat)
        {
            float span = halfExtent * 2f;
            CreatePart("SideF", PrimitiveType.Cube, parent, new Vector3(0f, 0f, halfExtent), new Vector3(span, thickness, thickness), mat);
            CreatePart("SideB", PrimitiveType.Cube, parent, new Vector3(0f, 0f, -halfExtent), new Vector3(span, thickness, thickness), mat);
            CreatePart("SideL", PrimitiveType.Cube, parent, new Vector3(-halfExtent, 0f, 0f), new Vector3(thickness, thickness, span), mat);
            CreatePart("SideR", PrimitiveType.Cube, parent, new Vector3(halfExtent, 0f, 0f), new Vector3(thickness, thickness, span), mat);
        }

        private static GameObject CreatePart(string name, PrimitiveType type, Transform parent, Vector3 localPos, Vector3 scale, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;

            Collider col = go.GetComponent<Collider>();
            if (col != null)
            {
                Destroy(col);
            }

            if (parent != null)
            {
                go.transform.SetParent(parent, false);
            }
            go.transform.localPosition = localPos;
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
