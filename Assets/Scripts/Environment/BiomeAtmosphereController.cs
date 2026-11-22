using UnityEngine;
using UnityEngine.Rendering;

namespace CyberRunner.Environment
{
    /// <summary>
    /// Smoothly interpolates fog, sky clear color, trilight ambient lighting, and directional key light
    /// across all five futuristic environments with zero heap allocations.
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

        public void ApplyImmediate(BiomeType biome)
        {
            targetProfile = BiomeCatalog.GetProfile(biome);
            isBlending = false;

            RenderSettings.fogColor = targetProfile.FogColor;
            RenderSettings.fogStartDistance = targetProfile.FogStart;
            RenderSettings.fogEndDistance = targetProfile.FogEnd;
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

            RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, targetProfile.FogColor, t);
            RenderSettings.fogStartDistance = Mathf.Lerp(RenderSettings.fogStartDistance, targetProfile.FogStart, t);
            RenderSettings.fogEndDistance = Mathf.Lerp(RenderSettings.fogEndDistance, targetProfile.FogEnd, t);
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
            if (Mathf.Abs(RenderSettings.fogEndDistance - targetProfile.FogEnd) < 0.08f &&
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
