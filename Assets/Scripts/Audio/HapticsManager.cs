using UnityEngine;
using CyberRunner.Core;

namespace CyberRunner.Audio
{
    /// <summary>
    /// Android-native tactile haptic feedback helper respecting SettingsManager.VibrationEnabled.
    /// Uses Android VibrationEffect (API 26+) with safe fallback to Handheld.Vibrate on API 24-25.
    /// </summary>
    public static class HapticsManager
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        private static AndroidJavaObject vibrator;
        private static int androidSdkInt = -1;
        private static bool initialized;
#endif

        public static void Light() => Trigger(18, 70);
        public static void Medium() => Trigger(35, 140);
        public static void Heavy() => Trigger(75, 220);

        private static void Trigger(long milliseconds, int amplitude)
        {
            if (SettingsManager.Instance != null && !SettingsManager.Instance.VibrationEnabled)
            {
                return;
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                EnsureAndroidVibrator();
                if (vibrator == null)
                {
                    Handheld.Vibrate();
                    return;
                }

                if (androidSdkInt >= 26)
                {
                    using (var vibrationEffectClass = new AndroidJavaClass("android.os.VibrationEffect"))
                    {
                        AndroidJavaObject effect = vibrationEffectClass.CallStatic<AndroidJavaObject>(
                            "createOneShot", milliseconds, Mathf.Clamp(amplitude, 1, 255));
                        vibrator.Call("vibrate", effect);
                    }
                }
                else
                {
                    vibrator.Call("vibrate", milliseconds);
                }
            }
            catch
            {
                // Ignore cleanly on devices lacking vibration hardware or permission
            }
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static void EnsureAndroidVibrator()
        {
            if (initialized) return;
            initialized = true;

            using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
            {
                androidSdkInt = version.GetStatic<int>("SDK_INT");
            }

            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            {
                if (activity != null)
                {
                    vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                }
            }
        }
#endif
    }
}
