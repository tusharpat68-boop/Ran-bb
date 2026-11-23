using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;

namespace CyberRunner.Editor
{
    /// <summary>
    /// Pre-build Android release gate verifying package identity, Portrait orientation,
    /// IL2CPP + ARM64 compliance, URP pipeline, build scenes, and optional release keystore signing.
    /// </summary>
    public static class CyberRunnerReleaseValidator
    {
        public static void ValidateOrThrow(bool buildAppBundle, bool requireSigning)
        {
            var issues = new List<string>();

            string appId = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
            if (appId != CyberRunnerBuildPipeline.AndroidPackageId)
            {
                issues.Add($"Android package ID mismatch: expected '{CyberRunnerBuildPipeline.AndroidPackageId}', found '{appId}'.");
            }

            if (PlayerSettings.defaultInterfaceOrientation != UIOrientation.Portrait)
            {
                issues.Add("Android orientation must be locked to Portrait.");
            }

            if (PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android) != ScriptingImplementation.IL2CPP)
            {
                issues.Add("Android scripting backend must be IL2CPP.");
            }

            if ((PlayerSettings.Android.targetArchitectures & AndroidArchitecture.ARM64) == 0)
            {
                issues.Add("Android ARM64 architecture must be enabled for Google Play compliance.");
            }

            if ((int)PlayerSettings.Android.minSdkVersion < (int)AndroidSdkVersions.AndroidApiLevel24)
            {
                issues.Add("Android Min SDK must be API 24 (Android 7.0) or higher.");
            }

            if (GraphicsSettings.defaultRenderPipeline == null)
            {
                issues.Add("URP GraphicsSettings.defaultRenderPipeline is null.");
            }

            if (EditorBuildSettings.scenes == null || EditorBuildSettings.scenes.Length < 2)
            {
                issues.Add("Expected at least 2 scenes (Bootstrap.unity and Gameplay.unity) in EditorBuildSettings.");
            }

            if (requireSigning && !PlayerSettings.Android.useCustomKeystore)
            {
                issues.Add("Release signing was requested (-requireSigning true), but no valid custom Android keystore was configured.");
            }

            if (buildAppBundle && !PlayerSettings.Android.useCustomKeystore)
            {
                Debug.LogWarning("[CyberRunnerReleaseValidator] Building .aab without custom release keystore (debug-signed). Configure GitHub Secrets before Play Console upload.");
            }

            if (issues.Count > 0)
            {
                throw new BuildFailedException(
                    "[CyberRunnerReleaseValidator] Release pre-flight failed:\n - " + string.Join("\n - ", issues));
            }

            Debug.Log($"[CyberRunnerReleaseValidator] Pre-flight PASSED | Package: {appId} | Version: {PlayerSettings.bundleVersion} ({PlayerSettings.Android.bundleVersionCode}) | Signed: {PlayerSettings.Android.useCustomKeystore} | AAB: {buildAppBundle}");
        }
    }
}
