using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace CyberRunner.Editor
{
    /// <summary>
    /// Headless Android build entry point for GitHub Actions (GameCI) and local Editor builds.
    /// Configures Android package identity, Portrait orientation, IL2CPP, URP scenes,
    /// optional release keystore signing, and emits APK or AAB outputs.
    /// </summary>
    public static class CyberRunnerBuildPipeline
    {
        public const string AndroidPackageId = "com.cyberrunner.game";
        public const string ProductName = "CYBER RUNNER";
        public const string CompanyName = "CyberRunnerStudio";

        [MenuItem("CyberRunner/Build/Build Android APK (v1.0.0)")]
        public static void BuildAndroidMenu()
        {
            BuildAndroid();
        }

        /// <summary>
        /// Entry point invoked by game-ci/unity-builder via buildMethod:
        /// CyberRunner.Editor.CyberRunnerBuildPipeline.BuildAndroid
        /// </summary>
        public static void BuildAndroid()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            }

            string[] args = Environment.GetCommandLineArgs();
            string buildPath = GetArgValue(args, "-customBuildPath") ?? "build/Android/CyberRunner.apk";
            string exportType = GetArgValue(args, "-androidExportType") ?? "androidPackage";
            string versionName = GetArgValue(args, "-buildVersion") ?? "1.0.0";
            string versionCodeRaw = GetArgValue(args, "-androidVersionCode") ?? Environment.GetEnvironmentVariable("ANDROID_VERSION_CODE") ?? "1";
            bool includeArmV7 = string.Equals(
                GetArgValue(args, "-includeArmV7") ?? Environment.GetEnvironmentVariable("CYBER_RUNNER_INCLUDE_ARMV7"),
                "true",
                StringComparison.OrdinalIgnoreCase);
            bool requireSigning = string.Equals(
                GetArgValue(args, "-requireSigning") ?? Environment.GetEnvironmentVariable("CYBER_RUNNER_REQUIRE_SIGNING"),
                "true",
                StringComparison.OrdinalIgnoreCase);

            bool buildAppBundle = string.Equals(exportType, "androidAppBundle", StringComparison.OrdinalIgnoreCase)
                || buildPath.EndsWith(".aab", StringComparison.OrdinalIgnoreCase);

            string expectedExt = buildAppBundle ? ".aab" : ".apk";
            if (!buildPath.EndsWith(expectedExt, StringComparison.OrdinalIgnoreCase))
            {
                buildPath = Path.ChangeExtension(buildPath, expectedExt);
            }

            if (!int.TryParse(versionCodeRaw, out int versionCode) || versionCode < 1)
            {
                versionCode = 1;
            }

            ConfigureAndroidPlayerSettings(versionName, versionCode, buildAppBundle, includeArmV7);
            ConfigureOptionalKeystore(args);
            CyberRunnerBrandingGenerator.GenerateAndAssignBranding();
            Phase1AssetAndSceneBuilder.BuildAllPhase1AssetsAndScenes();
            CyberRunnerReleaseValidator.ValidateOrThrow(buildAppBundle, requireSigning);

            string outputDirectory = Path.GetDirectoryName(buildPath);
            if (!string.IsNullOrEmpty(outputDirectory) && !Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            string[] enabledScenes = EditorBuildSettings.scenes
                .Where(s => s.enabled && !string.IsNullOrEmpty(s.path))
                .Select(s => s.path)
                .ToArray();

            if (enabledScenes.Length == 0)
            {
                throw new BuildFailedException("No enabled scenes found in EditorBuildSettings.");
            }

            var buildOptions = new BuildPlayerOptions
            {
                scenes = enabledScenes,
                locationPathName = buildPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.CompressWithLz4
            };

            Debug.Log($"[CyberRunner] Starting Android build -> {buildPath} | Version: {versionName} ({versionCode}) | AAB: {buildAppBundle}");
            BuildReport report = BuildPipeline.BuildPlayer(buildOptions);
            BuildSummary summary = report.summary;

            if (summary.result != BuildResult.Succeeded)
            {
                throw new BuildFailedException(
                    $"[CyberRunner] Android build failed with result {summary.result}. Errors: {summary.totalErrors}, Warnings: {summary.totalWarnings}");
            }

            double sizeMb = summary.totalSize / (1024.0 * 1024.0);
            Debug.Log($"[CyberRunner] Android build SUCCEEDED -> {summary.outputPath} ({sizeMb:F2} MB) in {summary.totalTime.TotalSeconds:F1}s");
        }

        public static void ConfigureAndroidPlayerSettings(
            string versionName,
            int versionCode,
            bool buildAppBundle,
            bool includeArmV7)
        {
            PlayerSettings.companyName = CompanyName;
            PlayerSettings.productName = ProductName;
            PlayerSettings.bundleVersion = versionName;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, AndroidPackageId);

            // Portrait orientation as mandated by CYBER RUNNER Android spec.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;

            // Scripting backend & architectures (IL2CPP + ARM64 required for Google Play; optional ARMv7 for legacy 32-bit phones).
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.Android, ApiCompatibilityLevel.NET_Standard);
            PlayerSettings.Android.targetArchitectures = includeArmV7
                ? (AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7)
                : AndroidArchitecture.ARM64;

            // Android SDK levels: API 24 (Android 7.0) minimum for broad 2GB-3GB phone support; Auto latest target SDK.
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;

            // OpenGLES3 first for maximum stability on low-end 2GB-3GB Android GPUs, with Vulkan available.
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(
                BuildTarget.Android,
                new[] { GraphicsDeviceType.OpenGLES3, GraphicsDeviceType.Vulkan });

            // APK size, incremental GC & low-end 2GB-3GB Android RAM optimization.
            PlayerSettings.stripEngineCode = true;
            PlayerSettings.gcIncremental = true;
            PlayerSettings.bakeCollisionMeshes = true;
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, ManagedStrippingLevel.Low);
            PlayerSettings.SetIl2CppCompilerConfiguration(NamedBuildTarget.Android, Il2CppCompilerConfiguration.Release);
            PlayerSettings.SetIl2CppCodeGeneration(NamedBuildTarget.Android, Il2CppCodeGeneration.OptimizeSize);
            PlayerSettings.Android.bundleVersionCode = versionCode;
            PlayerSettings.Android.splitApplicationBinary = false;
            PlayerSettings.Android.optimizedFramePacing = true;
            PlayerSettings.Android.renderOutsideSafeArea = true;
            EditorUserBuildSettings.buildAppBundle = buildAppBundle;

            EnsureBothInputHandlersEnabled();
        }

        /// <summary>
        /// Ensures ProjectSettings.asset has activeInputHandler = 2 (Both) so Unity Input System
        /// (EnhancedTouch) works immediately while remaining backwards-safe during fresh CI imports.
        /// </summary>
        private static void EnsureBothInputHandlersEnabled()
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (assets == null || assets.Length == 0)
            {
                return;
            }

            SerializedObject playerSettings = new SerializedObject(assets[0]);
            SerializedProperty inputHandlerProp = playerSettings.FindProperty("activeInputHandler");
            if (inputHandlerProp != null && inputHandlerProp.intValue == 0)
            {
                inputHandlerProp.intValue = 2; // 0 = Old, 1 = New Input System, 2 = Both
                playerSettings.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void ConfigureOptionalKeystore(string[] args)
        {
            string keystoreName = (GetArgValue(args, "-androidKeystoreName") ?? Environment.GetEnvironmentVariable("ANDROID_KEYSTORE_NAME"))?.Trim('"');
            string keystorePass = (GetArgValue(args, "-androidKeystorePass") ?? Environment.GetEnvironmentVariable("ANDROID_KEYSTORE_PASS"))?.Trim('"');
            string keyaliasName = (GetArgValue(args, "-androidKeyaliasName") ?? Environment.GetEnvironmentVariable("ANDROID_KEYALIAS_NAME"))?.Trim('"');
            string keyaliasPass = (GetArgValue(args, "-androidKeyaliasPass") ?? Environment.GetEnvironmentVariable("ANDROID_KEYALIAS_PASS"))?.Trim('"');

            string resolvedKeystore = keystoreName;
            if (!string.IsNullOrEmpty(keystoreName) && !File.Exists(resolvedKeystore))
            {
                string projectRootPath = Path.Combine(Directory.GetCurrentDirectory(), keystoreName);
                if (File.Exists(projectRootPath))
                {
                    resolvedKeystore = projectRootPath;
                }
            }

            if (!string.IsNullOrEmpty(resolvedKeystore) && File.Exists(resolvedKeystore))
            {
                PlayerSettings.Android.useCustomKeystore = true;
                PlayerSettings.Android.keystoreName = resolvedKeystore;
                PlayerSettings.Android.keystorePass = keystorePass ?? string.Empty;
                PlayerSettings.Android.keyaliasName = keyaliasName ?? string.Empty;
                PlayerSettings.Android.keyaliasPass = keyaliasPass ?? string.Empty;
                Debug.Log("[CyberRunner] Custom Android release keystore configured.");
            }
            else
            {
                PlayerSettings.Android.useCustomKeystore = false;
                Debug.Log("[CyberRunner] Using default Android debug signing (no custom keystore provided).");
            }
        }

        private static string GetArgValue(string[] args, string flagName)
        {
            // Scan from the end so explicit overrides win and ignore empty flags followed by another -flag.
            for (int i = args.Length - 2; i >= 0; i--)
            {
                if (string.Equals(args[i], flagName, StringComparison.OrdinalIgnoreCase))
                {
                    string candidate = args[i + 1];
                    if (string.IsNullOrWhiteSpace(candidate) || candidate.StartsWith("-", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    return candidate;
                }
            }

            return null;
        }
    }
}
