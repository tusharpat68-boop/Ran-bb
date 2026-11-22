using UnityEngine;

namespace CyberRunner.Environment
{
    /// <summary>
    /// The five original futuristic 2099 cyberpunk environments of CYBER RUNNER.
    /// </summary>
    public enum BiomeType
    {
        NeonCity = 0,
        CyberUnderground = 1,
        SkyHighway = 2,
        IndustrialZone = 3,
        DigitalCore = 4
    }

    /// <summary>
    /// Atmospheric lighting, fog, and UI identity values for a single environment sector.
    /// </summary>
    public readonly struct BiomeProfile
    {
        public readonly BiomeType Type;
        public readonly string SectorCode;
        public readonly string DisplayName;
        public readonly Color FogColor;
        public readonly float FogStart;
        public readonly float FogEnd;
        public readonly Color CameraSkyColor;
        public readonly Color AmbientSky;
        public readonly Color AmbientEquator;
        public readonly Color AmbientGround;
        public readonly Color KeyLightColor;
        public readonly float KeyLightIntensity;
        public readonly Color UiAccentColor;

        public BiomeProfile(
            BiomeType type,
            string sectorCode,
            string displayName,
            Color fogColor,
            float fogStart,
            float fogEnd,
            Color cameraSkyColor,
            Color ambientSky,
            Color ambientEquator,
            Color ambientGround,
            Color keyLightColor,
            float keyLightIntensity,
            Color uiAccentColor)
        {
            Type = type;
            SectorCode = sectorCode;
            DisplayName = displayName;
            FogColor = fogColor;
            FogStart = fogStart;
            FogEnd = fogEnd;
            CameraSkyColor = cameraSkyColor;
            AmbientSky = ambientSky;
            AmbientEquator = ambientEquator;
            AmbientGround = ambientGround;
            KeyLightColor = keyLightColor;
            KeyLightIntensity = keyLightIntensity;
            UiAccentColor = uiAccentColor;
        }
    }

    public static class BiomeCatalog
    {
        public const int BiomeCount = 5;

        public static BiomeProfile GetProfile(BiomeType biome)
        {
            switch (biome)
            {
                case BiomeType.CyberUnderground:
                    return new BiomeProfile(
                        BiomeType.CyberUnderground,
                        "SECTOR 02",
                        "CYBER UNDERGROUND",
                        fogColor: new Color(0.06f, 0.03f, 0.07f, 1f),
                        fogStart: 22f,
                        fogEnd: 105f,
                        cameraSkyColor: new Color(0.03f, 0.015f, 0.04f, 1f),
                        ambientSky: new Color(0.18f, 0.07f, 0.14f, 1f),
                        ambientEquator: new Color(0.10f, 0.04f, 0.08f, 1f),
                        ambientGround: new Color(0.04f, 0.02f, 0.03f, 1f),
                        keyLightColor: new Color(1.0f, 0.56f, 0.44f, 1f),
                        keyLightIntensity: 1.05f,
                        uiAccentColor: new Color(1.0f, 0.25f, 0.40f, 1f));

                case BiomeType.SkyHighway:
                    return new BiomeProfile(
                        BiomeType.SkyHighway,
                        "SECTOR 03",
                        "SKY HIGHWAY",
                        fogColor: new Color(0.05f, 0.11f, 0.24f, 1f),
                        fogStart: 38f,
                        fogEnd: 150f,
                        cameraSkyColor: new Color(0.04f, 0.09f, 0.22f, 1f),
                        ambientSky: new Color(0.18f, 0.30f, 0.52f, 1f),
                        ambientEquator: new Color(0.10f, 0.18f, 0.34f, 1f),
                        ambientGround: new Color(0.04f, 0.07f, 0.14f, 1f),
                        keyLightColor: new Color(0.76f, 0.94f, 1.0f, 1f),
                        keyLightIntensity: 1.40f,
                        uiAccentColor: new Color(0.30f, 0.88f, 1.0f, 1f));

                case BiomeType.IndustrialZone:
                    return new BiomeProfile(
                        BiomeType.IndustrialZone,
                        "SECTOR 04",
                        "INDUSTRIAL ZONE",
                        fogColor: new Color(0.11f, 0.07f, 0.04f, 1f),
                        fogStart: 26f,
                        fogEnd: 120f,
                        cameraSkyColor: new Color(0.08f, 0.05f, 0.03f, 1f),
                        ambientSky: new Color(0.28f, 0.18f, 0.10f, 1f),
                        ambientEquator: new Color(0.16f, 0.10f, 0.06f, 1f),
                        ambientGround: new Color(0.06f, 0.04f, 0.03f, 1f),
                        keyLightColor: new Color(1.0f, 0.78f, 0.48f, 1f),
                        keyLightIntensity: 1.28f,
                        uiAccentColor: new Color(1.0f, 0.65f, 0.12f, 1f));

                case BiomeType.DigitalCore:
                    return new BiomeProfile(
                        BiomeType.DigitalCore,
                        "SECTOR 05",
                        "DIGITAL CORE",
                        fogColor: new Color(0.03f, 0.08f, 0.14f, 1f),
                        fogStart: 30f,
                        fogEnd: 138f,
                        cameraSkyColor: new Color(0.02f, 0.05f, 0.10f, 1f),
                        ambientSky: new Color(0.10f, 0.30f, 0.38f, 1f),
                        ambientEquator: new Color(0.16f, 0.08f, 0.28f, 1f),
                        ambientGround: new Color(0.03f, 0.06f, 0.10f, 1f),
                        keyLightColor: new Color(0.55f, 0.96f, 1.0f, 1f),
                        keyLightIntensity: 1.35f,
                        uiAccentColor: new Color(0.95f, 0.24f, 0.98f, 1f));

                default:
                    return new BiomeProfile(
                        BiomeType.NeonCity,
                        "SECTOR 01",
                        "NEON CITY",
                        fogColor: new Color(0.03f, 0.05f, 0.11f, 1f),
                        fogStart: 32f,
                        fogEnd: 135f,
                        cameraSkyColor: new Color(0.02f, 0.035f, 0.085f, 1f),
                        ambientSky: new Color(0.10f, 0.16f, 0.30f, 1f),
                        ambientEquator: new Color(0.06f, 0.09f, 0.18f, 1f),
                        ambientGround: new Color(0.03f, 0.03f, 0.07f, 1f),
                        keyLightColor: new Color(0.75f, 0.90f, 1.0f, 1f),
                        keyLightIntensity: 1.25f,
                        uiAccentColor: new Color(0.0f, 0.94f, 1.0f, 1f));
            }
        }
    }
}
