using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace CyberRunner.Editor
{
    /// <summary>
    /// Programmatically synthesizes original cyberpunk Android App Icon (512x512) and
    /// Branded Splash Emblem (1024x512) at build time so Termux + GitHub Actions builds
    /// ship with crisp, zero-dependency, original artwork.
    /// </summary>
    public static class CyberRunnerBrandingGenerator
    {
        private const string BrandingFolder = "Assets/Resources/Branding";
        private const string IconAssetPath = "Assets/Resources/Branding/CyberRunner_Icon.png";
        private const string SplashAssetPath = "Assets/Resources/Branding/CyberRunner_Splash.png";

        [MenuItem("CyberRunner/Branding/Regenerate App Icon & Splash Assets")]
        public static void GenerateAndAssignBranding()
        {
            EnsureFolder(BrandingFolder);

            GenerateIconPng(IconAssetPath, 512);
            GenerateSplashPng(SplashAssetPath, 1024, 512);

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            ConfigureIconImporter(IconAssetPath);
            ConfigureSplashImporter(SplashAssetPath);

            Texture2D iconTex = AssetDatabase.LoadAssetAtPath<Texture2D>(IconAssetPath);
            if (iconTex != null)
            {
                AssignAndroidIcons(iconTex);
            }

            ConfigureSplashScreen();
            Debug.Log("[CyberRunner] Generated and assigned original 512x512 Android App Icon and Branded Splash Screen.");
        }

        private static void AssignAndroidIcons(Texture2D iconTex)
        {
            // Assign default cross-platform icon
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { iconTex }, IconKind.Any);

            // Assign every Android legacy/launcher icon slot expected by Unity 6
            int[] sizes = PlayerSettings.GetIconSizes(NamedBuildTarget.Android, IconKind.Application);
            if (sizes != null && sizes.Length > 0)
            {
                Texture2D[] androidIcons = new Texture2D[sizes.Length];
                for (int i = 0; i < androidIcons.Length; i++)
                {
                    androidIcons[i] = iconTex;
                }
                PlayerSettings.SetIcons(NamedBuildTarget.Android, androidIcons, IconKind.Application);
            }
        }

        private static void ConfigureSplashScreen()
        {
            PlayerSettings.SplashScreen.show = true;
            PlayerSettings.SplashScreen.backgroundColor = new Color(0.025f, 0.045f, 0.11f, 1f);
            PlayerSettings.SplashScreen.drawMode = PlayerSettings.SplashScreen.DrawMode.UnityLogoBelow;
            PlayerSettings.SplashScreen.animationMode = PlayerSettings.SplashScreen.AnimationMode.Dolly;

            Sprite splashSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SplashAssetPath);
            if (splashSprite != null)
            {
                var logo = PlayerSettings.SplashScreenLogo.Create(2.0f, splashSprite);
                PlayerSettings.SplashScreen.logos = new[] { logo };
            }
        }

        private static void GenerateIconPng(string assetPath, int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color32[] pixels = new Color32[size * size];

            Color deepNavy = new Color(0.02f, 0.04f, 0.10f, 1f);
            Color midIndigo = new Color(0.07f, 0.05f, 0.20f, 1f);
            Color cyan = new Color(0.0f, 0.96f, 1.0f, 1f);
            Color purple = new Color(0.72f, 0.20f, 1.0f, 1f);
            Color whiteHot = new Color(0.88f, 1.0f, 1.0f, 1f);

            float inv = 1f / (size - 1);

            for (int y = 0; y < size; y++)
            {
                float v = y * inv; // 0 bottom -> 1 top
                for (int x = 0; x < size; x++)
                {
                    float u = x * inv; // 0 left -> 1 right
                    float cx = u - 0.5f;
                    float cy = v - 0.5f;
                    float dist = Mathf.Sqrt(cx * cx + cy * cy);

                    // Radial futuristic navy/indigo base
                    Color col = Color.Lerp(midIndigo, deepNavy, Mathf.Clamp01(dist * 1.45f));

                    // Lower synthwave perspective highway grid
                    if (v < 0.44f)
                    {
                        float depth = Mathf.Clamp01((0.44f - v) / 0.44f);
                        float hWave = Mathf.Abs(Mathf.Sin(Mathf.Pow(depth, 0.65f) * 28f));
                        if (hWave > 0.93f)
                        {
                            col = Color.Lerp(col, purple, 0.48f * (1f - depth * 0.4f));
                        }

                        float laneX = cx / Mathf.Max(0.12f, depth + 0.12f);
                        if (Mathf.Abs(Mathf.Abs(laneX) - 0.34f) < 0.032f || Mathf.Abs(Mathf.Abs(laneX) - 1.02f) < 0.045f)
                        {
                            col = Color.Lerp(col, cyan, 0.72f * (1f - depth * 0.2f));
                        }
                    }

                    // Horizon neon sunset glow bar
                    float horizonGlow = Mathf.Exp(-Mathf.Abs(v - 0.44f) * 32f);
                    col = Color.Lerp(col, purple, horizonGlow * 0.45f);

                    // Iconic stylized Cyber Visor / Forward Speed Chevron ("CR") emblem
                    float chevY = v - 0.54f;
                    float absX = Mathf.Abs(cx);
                    float upperEdge = 0.24f - (absX * 0.72f);
                    float lowerEdge = upperEdge - 0.095f;
                    if (absX < 0.31f && chevY <= upperEdge && chevY >= lowerEdge)
                    {
                        float inner = 1f - Mathf.Clamp01(Mathf.Abs(chevY - ((upperEdge + lowerEdge) * 0.5f)) / 0.048f);
                        col = Color.Lerp(cyan, whiteHot, inner * 0.65f);
                    }

                    // Secondary purple lower chevron
                    float chev2Upper = 0.09f - (absX * 0.72f);
                    float chev2Lower = chev2Upper - 0.065f;
                    if (absX < 0.24f && chevY <= chev2Upper && chevY >= chev2Lower)
                    {
                        col = Color.Lerp(purple, cyan, 0.32f);
                    }

                    // Sleek glowing neon circuit border frame
                    float edgeDist = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
                    if (edgeDist < 0.042f && edgeDist > 0.020f)
                    {
                        col = Color.Lerp(cyan, purple, u);
                    }

                    pixels[(y * size) + x] = col;
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            File.WriteAllBytes(assetPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        private static void GenerateSplashPng(string assetPath, int width, int height)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            Color32[] pixels = new Color32[width * height];

            Color cyan = new Color(0.0f, 0.96f, 1.0f, 1f);
            Color purple = new Color(0.70f, 0.22f, 1.0f, 1f);
            Color whiteHot = new Color(0.92f, 1.0f, 1.0f, 1f);

            for (int y = 0; y < height; y++)
            {
                float v = (float)y / (height - 1);
                for (int x = 0; x < width; x++)
                {
                    float u = (float)x / (width - 1);
                    float cx = (u - 0.5f) * 2f; // -1..1
                    float cy = (v - 0.5f) * 2f; // -1..1

                    Color col = new Color(0f, 0f, 0f, 0f);

                    // Sleek wide cyber wings + glowing core diamond
                    float absX = Mathf.Abs(cx);
                    float absY = Mathf.Abs(cy);

                    // Central glowing diamond core
                    float diamond = absX * 2.4f + absY * 1.4f;
                    if (diamond < 0.72f)
                    {
                        float rim = Mathf.Clamp01((0.72f - diamond) / 0.14f);
                        col = Color.Lerp(cyan, whiteHot, rim * 0.6f);
                        col.a = 1f;
                    }
                    else if (diamond < 0.86f)
                    {
                        col = purple;
                        col.a = 1f - ((diamond - 0.72f) / 0.14f);
                    }

                    // Horizontal neon light speed bars radiating outward
                    if (absY < 0.14f && absX > 0.22f && absX < 0.88f)
                    {
                        float taper = 1f - ((absX - 0.22f) / 0.66f);
                        if (absY < 0.12f * taper)
                        {
                            Color barCol = Color.Lerp(cyan, purple, (absX - 0.22f) / 0.66f);
                            barCol.a = Mathf.Clamp01(taper * 1.15f);
                            col = barCol.a > col.a ? barCol : col;
                        }
                    }

                    pixels[(y * width) + x] = col;
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            File.WriteAllBytes(assetPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        private static void ConfigureIconImporter(string path)
        {
            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                importer.textureType = TextureImporterType.Default;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.maxTextureSize = 512;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
        }

        private static void ConfigureSplashImporter(string path)
        {
            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.maxTextureSize = 1024;
                importer.SaveAndReimport();
            }
        }

        private static void EnsureFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath)) return;
            string parent = Path.GetDirectoryName(assetPath)?.Replace("\\", "/");
            string name = Path.GetFileName(assetPath);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
