using System;
using UnityEngine;

namespace CyberRunner.Audio
{
    public enum CyberSfxType
    {
        UiClick = 0,
        LaneSwitch = 1,
        Jump = 2,
        Slide = 3,
        CoinPickup = 4,
        PowerUpPickup = 5,
        ShieldBlock = 6,
        DroneZap = 7,
        SpeedRam = 8,
        Stumble = 9,
        CrashGameOver = 10,
        RewardClaim = 11
    }

    /// <summary>
    /// Lightweight procedural synthwave music & SFX generator producing crisp 22050 Hz mono AudioClips
    /// at runtime with zero copyright dependencies and minimal RAM footprint (&lt; 1.2 MB total).
    /// </summary>
    public static class ProceduralSynthAudio
    {
        private const int SampleRate = 22050;
        private const float TwoPi = 6.28318530718f;

        /// <summary>
        /// Generates a seamless 8-second, 120 BPM futuristic cyberpunk synthwave loop.
        /// </summary>
        public static AudioClip CreateCyberpunkMusicLoop()
        {
            const float duration = 8.0f; // Exactly 16 beats at 120 BPM (seamless loop)
            int totalSamples = (int)(SampleRate * duration);
            float[] buffer = new float[totalSamples];

            // Dark A-minor cyberpunk bass progression (A1, F1, G1, E1)
            float[] bassHz = { 55.0f, 55.0f, 43.65f, 43.65f, 49.0f, 49.0f, 41.20f, 41.20f };
            // Futuristic synth arp notes (A3, C4, E4, G4, A4, E4, C4, B3)
            float[] arpHz = { 220.0f, 261.63f, 329.63f, 392.0f, 440.0f, 329.63f, 261.63f, 246.94f };

            float bassPhase = 0f;
            float arpPhase = 0f;
            float padPhaseA = 0f;
            float padPhaseB = 0f;
            uint rng = 0x5A17C9B3u;

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / SampleRate;
                float beat = t * 2.0f;                 // 120 BPM = 2 beats/sec
                float beatFrac = beat - Mathf.Floor(beat);
                int barHalf = ((int)(t)) % bassHz.Length;
                int sixteenth = ((int)(t * 8.0f)) % arpHz.Length;
                float sixteenthFrac = (t * 8.0f) - Mathf.Floor(t * 8.0f);

                // 1. Punchy synthwave kick on every beat
                float kickEnv = Mathf.Exp(-beatFrac * 14f);
                float kickFreq = 115f * Mathf.Exp(-beatFrac * 18f) + 38f;
                float kick = Mathf.Sin(TwoPi * kickFreq * beatFrac * 0.5f) * kickEnv * 0.34f;

                // 2. Soft off-beat hi-hat
                float hatPhase = (beat + 0.5f) - Mathf.Floor(beat + 0.5f);
                float hatEnv = Mathf.Exp(-hatPhase * 34f);
                rng = rng * 1664525u + 1013904223u;
                float noise = ((rng & 0xFFFFu) / 32768.0f) - 1.0f;
                float hat = noise * hatEnv * 0.06f;

                // 3. Pulsing sawtooth cyber bassline (sidechained slightly to kick)
                float sidechain = Mathf.Clamp01(beatFrac * 3.5f);
                bassPhase += bassHz[barHalf] / SampleRate;
                if (bassPhase >= 1f) bassPhase -= 1f;
                float sawBass = (2f * bassPhase - 1f) * 0.65f + Mathf.Sin(TwoPi * bassPhase) * 0.35f;
                float bassEnv = Mathf.Exp(-((t * 4f) - Mathf.Floor(t * 4f)) * 2.2f);
                float bass = sawBass * bassEnv * sidechain * 0.24f;

                // 4. Shimmering neon 16th-note arpeggio
                arpPhase += arpHz[sixteenth] / SampleRate;
                if (arpPhase >= 1f) arpPhase -= 1f;
                float arpWave = Mathf.Sin(TwoPi * arpPhase) + 0.35f * Mathf.Sin(TwoPi * arpPhase * 2f);
                float arpEnv = Mathf.Exp(-sixteenthFrac * 6.5f);
                float arp = arpWave * arpEnv * 0.12f;

                // 5. Atmospheric cyber pad (fifth interval)
                padPhaseA += (bassHz[barHalf] * 4f) / SampleRate;
                padPhaseB += (bassHz[barHalf] * 6f) / SampleRate;
                if (padPhaseA >= 1f) padPhaseA -= 1f;
                if (padPhaseB >= 1f) padPhaseB -= 1f;
                float pad = (Mathf.Sin(TwoPi * padPhaseA) + Mathf.Sin(TwoPi * padPhaseB) * 0.7f) * 0.06f;

                float sample = Mathf.Clamp(kick + hat + bass + arp + pad, -0.92f, 0.92f);
                buffer[i] = sample;
            }

            AudioClip clip = AudioClip.Create("CyberRunner_BGM_Loop", totalSamples, 1, SampleRate, false);
            clip.SetData(buffer, 0);
            return clip;
        }

        /// <summary>
        /// Generates a crisp procedural cyberpunk SFX clip for the requested gameplay event.
        /// </summary>
        public static AudioClip CreateSfx(CyberSfxType type)
        {
            switch (type)
            {
                case CyberSfxType.UiClick:
                    return BuildClip("SFX_UiClick", 0.065f, (t, u, n) =>
                        Mathf.Sin(TwoPi * Mathf.Lerp(920f, 1380f, u) * t) * Mathf.Exp(-u * 6f) * 0.45f);

                case CyberSfxType.LaneSwitch:
                    return BuildClip("SFX_LaneSwitch", 0.095f, (t, u, n) =>
                        (Mathf.Sin(TwoPi * Mathf.Lerp(260f, 640f, u) * t) * 0.35f + n * 0.16f) * Mathf.Sin(u * Mathf.PI) * 0.6f);

                case CyberSfxType.Jump:
                    return BuildClip("SFX_Jump", 0.16f, (t, u, n) =>
                        Mathf.Sin(TwoPi * Mathf.Lerp(240f, 760f, u * u) * t) * (1f - u) * 0.55f);

                case CyberSfxType.Slide:
                    return BuildClip("SFX_Slide", 0.22f, (t, u, n) =>
                        (Mathf.Sin(TwoPi * Mathf.Lerp(320f, 120f, u) * t) * 0.35f + n * 0.22f) * Mathf.Exp(-u * 2.5f) * 0.5f);

                case CyberSfxType.CoinPickup:
                    return BuildClip("SFX_Coin", 0.14f, (t, u, n) =>
                    {
                        float freq = u < 0.38f ? 1174.66f : 1760.0f; // D6 -> A6 crystalline chime
                        return (Mathf.Sin(TwoPi * freq * t) + 0.3f * Mathf.Sin(TwoPi * freq * 2f * t)) * Mathf.Exp(-u * 5.2f) * 0.45f;
                    });

                case CyberSfxType.PowerUpPickup:
                    return BuildClip("SFX_PowerUp", 0.34f, (t, u, n) =>
                    {
                        float f = u < 0.33f ? 440f : (u < 0.66f ? 554.37f : 880f);
                        return (Mathf.Sin(TwoPi * f * t) + 0.4f * Mathf.Sin(TwoPi * f * 1.5f * t)) * (1f - u * 0.7f) * 0.48f;
                    });

                case CyberSfxType.ShieldBlock:
                    return BuildClip("SFX_ShieldBlock", 0.28f, (t, u, n) =>
                        (Mathf.Sin(TwoPi * Mathf.Lerp(620f, 180f, u) * t) * 0.55f + n * 0.20f) * Mathf.Exp(-u * 4f) * 0.65f);

                case CyberSfxType.DroneZap:
                    return BuildClip("SFX_DroneZap", 0.15f, (t, u, n) =>
                        Mathf.Sin(TwoPi * Mathf.Lerp(1900f, 340f, u) * t) * Mathf.Exp(-u * 5f) * 0.50f);

                case CyberSfxType.SpeedRam:
                    return BuildClip("SFX_SpeedRam", 0.22f, (t, u, n) =>
                        (Mathf.Sin(TwoPi * Mathf.Lerp(180f, 65f, u) * t) * 0.6f + n * 0.35f) * Mathf.Exp(-u * 4.5f) * 0.65f);

                case CyberSfxType.Stumble:
                    return BuildClip("SFX_Stumble", 0.24f, (t, u, n) =>
                        (Mathf.Sin(TwoPi * Mathf.Lerp(210f, 95f, u) * t) * 0.5f + n * 0.35f) * Mathf.Exp(-u * 5f) * 0.6f);

                case CyberSfxType.CrashGameOver:
                    return BuildClip("SFX_Crash", 0.52f, (t, u, n) =>
                        (Mathf.Sin(TwoPi * Mathf.Lerp(280f, 48f, u) * t) * 0.55f + n * 0.42f * Mathf.Exp(-u * 3f)) * (1f - u) * 0.72f);

                case CyberSfxType.RewardClaim:
                    return BuildClip("SFX_Reward", 0.40f, (t, u, n) =>
                    {
                        float f = u < 0.25f ? 523.25f : (u < 0.50f ? 659.25f : (u < 0.75f ? 783.99f : 1046.5f));
                        return (Mathf.Sin(TwoPi * f * t) + 0.35f * Mathf.Sin(TwoPi * f * 2f * t)) * Mathf.Exp(-u * 2.4f) * 0.48f;
                    });

                default:
                    return BuildClip("SFX_Default", 0.08f, (t, u, n) => Mathf.Sin(TwoPi * 660f * t) * (1f - u) * 0.4f);
            }
        }

        private static AudioClip BuildClip(string name, float durationSeconds, Func<float, float, float, float> sampleFunc)
        {
            int count = Mathf.Max(64, (int)(SampleRate * durationSeconds));
            float[] data = new float[count];
            uint rng = 0x13579BDFu;

            for (int i = 0; i < count; i++)
            {
                float t = (float)i / SampleRate;
                float u = (float)i / (count - 1);
                rng = rng * 1664525u + 1013904223u;
                float noise = ((rng & 0xFFFFu) / 32768.0f) - 1.0f;

                // Subtle fade-in/out edge guard to eliminate clicks
                float edge = Mathf.Min(1f, i / 24f) * Mathf.Min(1f, (count - 1 - i) / 36f);
                data[i] = Mathf.Clamp(sampleFunc(t, u, noise) * edge, -0.95f, 0.95f);
            }

            AudioClip clip = AudioClip.Create(name, count, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
