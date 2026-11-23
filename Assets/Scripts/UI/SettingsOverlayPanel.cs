using System;
using UnityEngine;
using UnityEngine.UI;
using CyberRunner.Audio;
using CyberRunner.Core;

namespace CyberRunner.UI
{
    /// <summary>
    /// Futuristic dark-navy/cyan/purple Settings Overlay accessible from both Main Menu and Pause Menu.
    /// Controls Music, SFX, Android Haptics/Vibration, 30/60 FPS, Graphics Quality (Low/Med/High),
    /// and Swipe Sensitivity with instant persistence.
    /// </summary>
    public sealed class SettingsOverlayPanel : MonoBehaviour
    {
        private Font uiFont;
        private GameObject rootPanel;

        private Text musicLabel;
        private Text sfxLabel;
        private Text vibLabel;
        private Text fpsLabel;
        private Text graphicsLabel;
        private Text swipeLabel;
        private Text perfOverlayLabel;
        private Text statusNoteText;

        private Action onClosed;
        private bool built;

        public bool IsOpen => rootPanel != null && rootPanel.activeSelf;

        public void Initialize(Transform parentSafeArea, Font font, Action onClosedCallback = null)
        {
            uiFont = font;
            onClosed = onClosedCallback;

            if (!built)
            {
                BuildOverlay(parentSafeArea);
                built = true;
            }

            SettingsManager settings = SettingsManager.EnsureInstance();
            settings.OnSettingsChanged -= RefreshLabels;
            settings.OnSettingsChanged += RefreshLabels;
            RefreshLabels();
        }

        private void OnDestroy()
        {
            if (SettingsManager.Instance != null)
            {
                SettingsManager.Instance.OnSettingsChanged -= RefreshLabels;
            }
        }

        public void Open()
        {
            if (rootPanel == null) return;
            RefreshLabels();
            rootPanel.SetActive(true);
            CyberAudioManager.Instance?.PlaySfx(CyberSfxType.UiClick);
            HapticsManager.Light();
        }

        public void Close()
        {
            if (rootPanel == null) return;
            rootPanel.SetActive(false);
            CyberAudioManager.Instance?.PlaySfx(CyberSfxType.UiClick);
            onClosed?.Invoke();
        }

        private void RefreshLabels()
        {
            SettingsManager s = SettingsManager.Instance;
            if (s == null || !built) return;

            if (musicLabel != null)
            {
                musicLabel.text = s.MusicMuted
                    ? "SYNTH MUSIC: MUTED"
                    : $"SYNTH MUSIC: {Mathf.RoundToInt(s.MusicVolume * 100f)}%";
            }

            if (sfxLabel != null)
            {
                sfxLabel.text = s.SfxMuted
                    ? "SOUND FX: MUTED"
                    : $"SOUND FX: {Mathf.RoundToInt(s.SfxVolume * 100f)}%";
            }

            if (vibLabel != null)
            {
                vibLabel.text = s.VibrationEnabled ? "VIBRATION / HAPTICS: ON" : "VIBRATION / HAPTICS: OFF";
            }

            if (fpsLabel != null)
            {
                fpsLabel.text = s.TargetFps <= 30 ? "TARGET FPS: 30 (BATTERY)" : "TARGET FPS: 60 (SMOOTH)";
            }

            if (graphicsLabel != null)
            {
                graphicsLabel.text = $"GRAPHICS QUALITY: {s.GraphicsPreset.ToString().ToUpperInvariant()}";
            }

            if (swipeLabel != null)
            {
                swipeLabel.text = $"SWIPE SENSITIVITY: {s.SwipeSensitivity.ToString().ToUpperInvariant()}";
            }

            if (perfOverlayLabel != null)
            {
                perfOverlayLabel.text = s.ShowPerformanceOverlay ? "PERF MONITOR: ON" : "PERF MONITOR: OFF";
            }

            if (statusNoteText != null)
            {
                PerformanceOptimizer perf = PerformanceOptimizer.Instance;
                if (perf != null)
                {
                    statusNoteText.text = $"DEVICE: {perf.DeviceTierLabel} ({perf.DeviceSystemRamMb} MB RAM)";
                }
            }
        }

        private void BuildOverlay(Transform parentSafeArea)
        {
            var rootGo = new GameObject("SettingsOverlay");
            rootGo.transform.SetParent(parentSafeArea, false);
            RectTransform rt = rootGo.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            Image bg = rootGo.AddComponent<Image>();
            bg.color = new Color(0.02f, 0.04f, 0.10f, 0.92f);
            bg.raycastTarget = true;
            rootPanel = rootGo;

            var cardGo = new GameObject("SettingsCard");
            cardGo.transform.SetParent(rootGo.transform, false);
            RectTransform cardRt = cardGo.AddComponent<RectTransform>();
            cardRt.anchorMin = new Vector2(0.07f, 0.12f);
            cardRt.anchorMax = new Vector2(0.93f, 0.88f);
            cardRt.offsetMin = Vector2.zero;
            cardRt.offsetMax = Vector2.zero;

            cardGo.AddComponent<Image>().color = new Color(0.04f, 0.08f, 0.17f, 0.98f);
            Outline border = cardGo.AddComponent<Outline>();
            border.effectColor = new Color(0.0f, 0.94f, 1.0f, 0.95f);
            border.effectDistance = new Vector2(3f, -3f);
            cardGo.AddComponent<UiPanelAnimator>();

            Text header = CreateText("SettingsTitle", cardGo.transform, "SYSTEM SETTINGS", 42, new Color(0.0f, 0.94f, 1.0f, 1f), TextAnchor.MiddleCenter);
            header.rectTransform.anchorMin = new Vector2(0.06f, 0.88f);
            header.rectTransform.anchorMax = new Vector2(0.94f, 0.97f);

            Color cyanBtnFill = new Color(0.05f, 0.12f, 0.23f, 0.95f);
            Color cyanBorder = new Color(0.0f, 0.90f, 1.0f, 0.9f);
            Color purpleBtnFill = new Color(0.11f, 0.07f, 0.24f, 0.95f);
            Color purpleBorder = new Color(0.70f, 0.25f, 1.0f, 0.9f);

            musicLabel = CreateRowButton("MusicBtn", cardGo.transform, 0.76f, 0.845f, cyanBtnFill, cyanBorder, "SYNTH MUSIC: 70%", () =>
            {
                SettingsManager.EnsureInstance().CycleMusicVolume();
                CyberAudioManager.Instance?.PlaySfx(CyberSfxType.UiClick);
                HapticsManager.Light();
            });

            sfxLabel = CreateRowButton("SfxBtn", cardGo.transform, 0.655f, 0.74f, cyanBtnFill, cyanBorder, "SOUND FX: 85%", () =>
            {
                SettingsManager.EnsureInstance().CycleSfxVolume();
                CyberAudioManager.Instance?.PlaySfx(CyberSfxType.CoinPickup);
                HapticsManager.Light();
            });

            vibLabel = CreateRowButton("VibBtn", cardGo.transform, 0.55f, 0.635f, cyanBtnFill, cyanBorder, "VIBRATION / HAPTICS: ON", () =>
            {
                SettingsManager.EnsureInstance().ToggleVibration();
                CyberAudioManager.Instance?.PlaySfx(CyberSfxType.UiClick);
                HapticsManager.Medium();
            });

            fpsLabel = CreateRowButton("FpsBtn", cardGo.transform, 0.445f, 0.53f, purpleBtnFill, purpleBorder, "TARGET FPS: 60 (SMOOTH)", () =>
            {
                SettingsManager.EnsureInstance().ToggleTargetFps();
                CyberAudioManager.Instance?.PlaySfx(CyberSfxType.UiClick);
                HapticsManager.Light();
            });

            graphicsLabel = CreateRowButton("GraphicsBtn", cardGo.transform, 0.34f, 0.425f, purpleBtnFill, purpleBorder, "GRAPHICS QUALITY: MEDIUM", () =>
            {
                SettingsManager.EnsureInstance().CycleGraphicsPreset();
                CyberAudioManager.Instance?.PlaySfx(CyberSfxType.UiClick);
                HapticsManager.Light();
            });

            swipeLabel = CreateRowButton("SwipeBtn", cardGo.transform, 0.25f, 0.325f, purpleBtnFill, purpleBorder, "SWIPE SENSITIVITY: NORMAL", () =>
            {
                SettingsManager.EnsureInstance().CycleSwipeSensitivity();
                CyberAudioManager.Instance?.PlaySfx(CyberSfxType.LaneSwitch);
                HapticsManager.Light();
            });

            perfOverlayLabel = CreateRowButton("PerfOverlayBtn", cardGo.transform, 0.165f, 0.238f,
                new Color(0.05f, 0.16f, 0.20f, 0.95f), new Color(0.18f, 1.0f, 0.55f, 0.92f), "PERF MONITOR: OFF", () =>
            {
                SettingsManager.EnsureInstance().TogglePerformanceOverlay();
                CyberAudioManager.Instance?.PlaySfx(CyberSfxType.UiClick);
                HapticsManager.Light();
            });

            statusNoteText = CreateText("SettingsNote", cardGo.transform, "Low-End 2GB RAM Tip: Use LOW Graphics + 30 FPS", 19, new Color(0.70f, 0.82f, 0.96f, 0.88f), TextAnchor.MiddleCenter);
            statusNoteText.rectTransform.anchorMin = new Vector2(0.05f, 0.105f);
            statusNoteText.rectTransform.anchorMax = new Vector2(0.95f, 0.16f);

            CreateRowButton("CloseSettingsBtn", cardGo.transform, 0.025f, 0.10f,
                new Color(0.0f, 0.22f, 0.30f, 0.96f), new Color(0.0f, 0.94f, 1.0f, 1f), "DONE", Close);

            rootPanel.SetActive(false);
        }

        private Text CreateRowButton(
            string name,
            Transform parent,
            float yMin,
            float yMax,
            Color fill,
            Color border,
            string label,
            UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.08f, yMin);
            rt.anchorMax = new Vector2(0.92f, yMax);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            Image img = go.AddComponent<Image>();
            img.color = fill;
            Outline outl = go.AddComponent<Outline>();
            outl.effectColor = border;
            outl.effectDistance = new Vector2(2f, -2f);

            Button btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);

            Text txt = CreateText("Label", go.transform, label, 25, Color.white, TextAnchor.MiddleCenter);
            txt.rectTransform.anchorMin = Vector2.zero;
            txt.rectTransform.anchorMax = Vector2.one;
            return txt;
        }

        private Text CreateText(string name, Transform parent, string content, int fontSize, Color color, TextAnchor align)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            Text text = go.AddComponent<Text>();
            text.font = uiFont;
            text.text = content;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = align;
            text.raycastTarget = false;
            return text;
        }
    }
}
