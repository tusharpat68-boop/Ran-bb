using UnityEngine;
using CyberRunner.Core;

namespace CyberRunner.Audio
{
    /// <summary>
    /// Persistent mobile-friendly Audio Manager handling cyberpunk synthwave music playback,
    /// volume ducking on pause/game-over, procedural SFX with subtle pitch variation,
    /// and Android audio focus / app interruption handling.
    /// </summary>
    public sealed class CyberAudioManager : MonoBehaviour
    {
        public static CyberAudioManager Instance { get; private set; }

        private AudioSource musicSource;
        private AudioSource sfxSource;
        private AudioClip bgmClip;
        private AudioClip[] sfxClips;

        private bool isDucked;
        private float currentDuckScale = 1f;
        private float lastCoinSfxTime;

        public static CyberAudioManager EnsureInstance()
        {
            if (Instance != null) return Instance;
            CyberAudioManager existing = FindFirstObjectByType<CyberAudioManager>();
            if (existing != null)
            {
                Instance = existing;
                return existing;
            }

            var go = new GameObject("CyberAudioManager");
            return go.AddComponent<CyberAudioManager>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            SetupSourcesAndGenerateClips();
            SettingsManager settings = SettingsManager.EnsureInstance();
            settings.OnSettingsChanged += ApplyVolumeSettings;

            if (AppLifecycleManager.Instance != null)
            {
                AppLifecycleManager.Instance.OnAppPauseStateChanged += HandleAppPause;
                AppLifecycleManager.Instance.OnAppFocusStateChanged += HandleAppFocus;
            }

            ApplyVolumeSettings();
            StartMusic();
        }

        private void OnDestroy()
        {
            if (SettingsManager.Instance != null)
            {
                SettingsManager.Instance.OnSettingsChanged -= ApplyVolumeSettings;
            }

            if (AppLifecycleManager.Instance != null)
            {
                AppLifecycleManager.Instance.OnAppPauseStateChanged -= HandleAppPause;
                AppLifecycleManager.Instance.OnAppFocusStateChanged -= HandleAppFocus;
            }

            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            if (musicSource == null) return;

            float targetDuck = isDucked ? 0.35f : 1.0f;
            currentDuckScale = Mathf.MoveTowards(currentDuckScale, targetDuck, Time.unscaledDeltaTime * 2.5f);

            float baseMusic = SettingsManager.Instance != null ? SettingsManager.Instance.EffectiveMusicVolume : 0.7f;
            musicSource.volume = baseMusic * 0.55f * currentDuckScale;
        }

        public void SetMusicDucked(bool ducked)
        {
            isDucked = ducked;
        }

        public void PlaySfx(CyberSfxType type)
        {
            if (sfxSource == null || sfxClips == null) return;
            float vol = SettingsManager.Instance != null ? SettingsManager.Instance.EffectiveSfxVolume : 0.85f;
            if (vol <= 0.001f) return;

            int idx = (int)type;
            if (idx < 0 || idx >= sfxClips.Length || sfxClips[idx] == null) return;

            // Gentle rate-limit & ascending pitch shimmer on rapid Energy Coin trails
            if (type == CyberSfxType.CoinPickup)
            {
                float now = Time.unscaledTime;
                if (now - lastCoinSfxTime < 0.045f) return;
                sfxSource.pitch = (now - lastCoinSfxTime < 0.32f)
                    ? Mathf.Min(1.32f, sfxSource.pitch + 0.05f)
                    : 1.0f;
                lastCoinSfxTime = now;
            }
            else if (type == CyberSfxType.LaneSwitch)
            {
                sfxSource.pitch = Random.Range(0.95f, 1.08f);
            }
            else
            {
                sfxSource.pitch = 1.0f;
            }

            sfxSource.PlayOneShot(sfxClips[idx], vol);
        }

        private void SetupSourcesAndGenerateClips()
        {
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.loop = true;
            musicSource.playOnAwake = false;
            musicSource.spatialBlend = 0f;

            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.loop = false;
            sfxSource.playOnAwake = false;
            sfxSource.spatialBlend = 0f;

            bgmClip = ProceduralSynthAudio.CreateCyberpunkMusicLoop();
            musicSource.clip = bgmClip;

            int count = System.Enum.GetValues(typeof(CyberSfxType)).Length;
            sfxClips = new AudioClip[count];
            for (int i = 0; i < count; i++)
            {
                sfxClips[i] = ProceduralSynthAudio.CreateSfx((CyberSfxType)i);
            }
        }

        private void StartMusic()
        {
            if (musicSource != null && bgmClip != null && !musicSource.isPlaying)
            {
                musicSource.Play();
            }
        }

        private void ApplyVolumeSettings()
        {
            if (SettingsManager.Instance == null || musicSource == null) return;
            musicSource.mute = SettingsManager.Instance.MusicMuted;
            sfxSource.mute = SettingsManager.Instance.SfxMuted;
        }

        private void HandleAppPause(bool paused)
        {
            AudioListener.pause = paused;
        }

        private void HandleAppFocus(bool hasFocus)
        {
            AudioListener.pause = !hasFocus;
        }
    }
}
