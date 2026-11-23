using UnityEngine;
using UnityEngine.Rendering;
using CyberRunner.Core;

namespace CyberRunner.Environment
{
    /// <summary>
    /// Smoothly interpolates fog, sky clear color, trilight ambient lighting, and directional key light
    /// across all five futuristic environments with zero heap allocations and PerformanceOptimizer LOD fog scaling.
    /// </summary>
    public sealed class BiomeAtmosphereController : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private Light keyLight;
        [SerializeField] private float blendSharpness = 2.4f;

        private BiomeProfile targetProfile;
        private bool isBlending;

        public BiomeType ActiveBiome => targetProfile.Type;

        public void Configure(Camera cam, Light directionalLight)
        {
            targetCamera = cam;
            keyLight = directionalLight;
        }

        private void Awake()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            if (keyLight == null)
            {
                keyLight = FindFirstObjectByType<Light>();
            }

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            ApplyImmediate(BiomeType.NeonCity);
        }

        private void OnEnable()
        {
            if (PerformanceOptimizer.Instance != null)
            {
                PerformanceOptimizer.Instance.OnOptimizationProfileApplied += ReapplyActiveFogScale;
            }
        }

        private void OnDisable()
        {
            if (PerformanceOptimizer.Instance != null)
            {
                PerformanceOptimizer.Instance.OnOptimizationProfileApplied -= ReapplyActiveFogScale;
            }
        }

        private void ReapplyActiveFogScale()
        {
            float fogScale = GetFogScale();
            RenderSettings.fogStartDistance = targetProfile.FogStart * fogScale;
            RenderSettings.fogEndDistance = targetProfile.FogEnd * fogScale;
        }

        private static float GetFogScale()
        {
            return PerformanceOptimizer.Instance != null ? PerformanceOptimizer.Instance.FogDistanceScale : 1f;
        }

        public void ApplyImmediate(BiomeType biome)
        {
            targetProfile = BiomeCatalog.GetProfile(biome);
            isBlending = false;
            float fogScale = GetFogScale();

            RenderSettings.fogColor = targetProfile.FogColor;
            RenderSettings.fogStartDistance = targetProfile.FogStart * fogScale;
            RenderSettings.fogEndDistance = targetProfile.FogEnd * fogScale;
            RenderSettings.ambientSkyColor = targetProfile.AmbientSky;
            RenderSettings.ambientEquatorColor = targetProfile.AmbientEquator;
            RenderSettings.ambientGroundColor = targetProfile.AmbientGround;

            if (targetCamera != null)
            {
                targetCamera.backgroundColor = targetProfile.CameraSkyColor;
            }

            if (keyLight != null)
            {
                keyLight.color = targetProfile.KeyLightColor;
                keyLight.intensity = targetProfile.KeyLightIntensity;
            }
        }

        public void TransitionTo(BiomeType biome)
        {
            targetProfile = BiomeCatalog.GetProfile(biome);
            isBlending = true;
        }

        private void Update()
        {
            if (!isBlending)
            {
                return;
            }

            float dt = Time.deltaTime;
            if (dt <= 0f)
            {
                return;
            }

            float t = 1f - Mathf.Exp(-blendSharpness * dt);
            float fogScale = GetFogScale();
            float desiredFogStart = targetProfile.FogStart * fogScale;
            float desiredFogEnd = targetProfile.FogEnd * fogScale;

            RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, targetProfile.FogColor, t);
            RenderSettings.fogStartDistance = Mathf.Lerp(RenderSettings.fogStartDistance, desiredFogStart, t);
            RenderSettings.fogEndDistance = Mathf.Lerp(RenderSettings.fogEndDistance, desiredFogEnd, t);
            RenderSettings.ambientSkyColor = Color.Lerp(RenderSettings.ambientSkyColor, targetProfile.AmbientSky, t);
            RenderSettings.ambientEquatorColor = Color.Lerp(RenderSettings.ambientEquatorColor, targetProfile.AmbientEquator, t);
            RenderSettings.ambientGroundColor = Color.Lerp(RenderSettings.ambientGroundColor, targetProfile.AmbientGround, t);

            if (targetCamera != null)
            {
                targetCamera.backgroundColor = Color.Lerp(targetCamera.backgroundColor, targetProfile.CameraSkyColor, t);
            }

            if (keyLight != null)
            {
                keyLight.color = Color.Lerp(keyLight.color, targetProfile.KeyLightColor, t);
                keyLight.intensity = Mathf.Lerp(keyLight.intensity, targetProfile.KeyLightIntensity, t);
            }

            // Stop blending once converged to save CPU cycles.
            if (Mathf.Abs(RenderSettings.fogEndDistance - desiredFogEnd) < 0.08f &&
                ColorDelta(RenderSettings.fogColor, targetProfile.FogColor) < 0.004f)
            {
                ApplyImmediate(targetProfile.Type);
            }
        }

        private static float ColorDelta(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b);
        }
    }
}
