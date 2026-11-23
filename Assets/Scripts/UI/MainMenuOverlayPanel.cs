using System;
using UnityEngine;
using UnityEngine.UI;
using CyberRunner.Audio;
using CyberRunner.Environment;
using CyberRunner.Gameplay;
using CyberRunner.Progression;

namespace CyberRunner.UI
{
    /// <summary>
    /// Futuristic Dark-Navy / Electric-Cyan / Neon-Purple Main Menu floating over the live 3D runner
    /// and Neon City environment. Zero heavy scene reloads — tapping PLAY launches straight into the run!
    /// </summary>
    public sealed class MainMenuOverlayPanel : MonoBehaviour
    {
        private CoreRunManager runManager;
        private ProgressionManager progression;
        private Font uiFont;

        private GameObject rootPanel;
        private Text statsBannerText;
        private Text suitBadgeText;
        private Text modeButtonLabel;
        private Text sectorButtonLabel;
        private Text hubButtonLabel;
        private Image accentStrip;

        private Action onPlayClicked;
        private Action onOpenHubClicked;
        private Action onOpenSettingsClicked;
        private bool built;

        public bool IsOpen => rootPanel != null && rootPanel.activeSelf;

        public void Initialize(
            Transform parentSafeArea,
            CoreRunManager manager,
            ProgressionManager progressionManager,
            Font font,
            Action onPlay,
            Action onOpenHub,
            Action onOpenSettings)
        {
            runManager = manager;
            progression = progressionManager;
            uiFont = font;
            onPlayClicked = onPlay;
            onOpenHubClicked = onOpenHub;
            onOpenSettingsClicked = onOpenSettings;

            if (!built)
            {
                BuildOverlay(parentSafeArea);
                built = true;
            }

            if (progression != null)
            {
                progression.OnProgressionChanged -= RefreshTelemetry;
                progression.OnProgressionChanged += RefreshTelemetry;
            }

            RefreshTelemetry();
        }

        private void OnDestroy()
        {
            if (progression != null)
            {
                progression.OnProgressionChanged -= RefreshTelemetry;
            }
        }

        public void SetVisible(bool visible)
        {
            if (rootPanel == null) return;
            rootPanel.SetActive(visible);
            if (visible)
            {
                RefreshTelemetry();
            }
        }

        public void RefreshTelemetry()
        {
            if (!built) return;

            int bestScore = progression != null && progression.Data != null ? progression.Data.bestScore : 0;
            int bestDist = progression != null && progression.Data != null ? progression.Data.bestDistanceMeters : 0;
            int energy = progression != null ? progression.EnergyBalance : 0;
            string skinName = progression != null && progression.Data != null
                ? CharacterSkinCatalog.GetById(progression.Data.equippedSkinId).DisplayName
                : "NEON VANGUARD";

            if (statsBannerText != null)
            {
                statsBannerText.text = $"BEST: {bestScore}   |   {bestDist} m   |   ENERGY: {energy}";
            }

            if (suitBadgeText != null)
            {
                suitBadgeText.text = $"ACTIVE RIG: {skinName}";
            }

            if (modeButtonLabel != null && runManager != null)
            {
                modeButtonLabel.text = $"MODE: {GetModeDisplayName(runManager.ActiveGameMode)}";
            }

            if (sectorButtonLabel != null && runManager != null)
            {
                BiomeProfile bp = BiomeCatalog.GetProfile(runManager.CurrentBiome);
                sectorButtonLabel.text = $"ZONE: {bp.DisplayName}";
                if (accentStrip != null) accentStrip.color = bp.UiAccentColor;
            }

            if (hubButtonLabel != null)
            {
                int claims = progression != null ? progression.TotalUnclaimedRewards : 0;
                hubButtonLabel.text = claims > 0 ? $"CYBER HUB ({claims}!)" : "CYBER HUB";
            }
        }

        private static string GetModeDisplayName(GameModeType mode)
        {
            switch (mode)
            {
                case GameModeType.Mission: return "MISSION";
                case GameModeType.TimeChallenge: return "TIME 90s";
                case GameModeType.SpecialChallenge: return "OVERDRIVE";
                default: return "ENDLESS";
            }
        }

        private void BuildOverlay(Transform parentSafeArea)
        {
            var rootGo = new GameObject("MainMenuOverlay");
            rootGo.transform.SetParent(parentSafeArea, false);
            RectTransform rt = rootGo.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rootGo.AddComponent<UiPanelAnimator>();
            rootPanel = rootGo;

            // Top Futuristic Title & Telemetry Header (leaves center open so the live 3D Runner shines!)
            RectTransform headerCard = CreateCard("MenuHeaderCard", rootGo.transform,
                new Vector2(0.05f, 0.77f), new Vector2(0.95f, 0.975f),
                new Color(0.03f, 0.06f, 0.14f, 0.84f), new Color(0.0f, 0.94f, 1.0f, 0.92f));

            var stripGo = new GameObject("HeaderAccentStrip");
            stripGo.transform.SetParent(headerCard, false);
            RectTransform stripRt = stripGo.AddComponent<RectTransform>();
            stripRt.anchorMin = new Vector2(0f, 0f);
            stripRt.anchorMax = new Vector2(1f, 0.03f);
            stripRt.offsetMin = Vector2.zero;
            stripRt.offsetMax = Vector2.zero;
            accentStrip = stripGo.AddComponent<Image>();
            accentStrip.color = new Color(0.0f, 0.94f, 1.0f, 1f);

            Text subtitle = CreateText("YearBadge", headerCard, "PROTOCOL 2099  //  ENDLESS CYBERNETIC RUNNER", 20, new Color(0.74f, 0.50f, 1.0f, 0.96f), TextAnchor.MiddleCenter);
            subtitle.rectTransform.anchorMin = new Vector2(0.04f, 0.76f);
            subtitle.rectTransform.anchorMax = new Vector2(0.96f, 0.94f);

            Text title = CreateText("GameTitle", headerCard, "CYBER RUNNER", 62, new Color(0.0f, 0.96f, 1.0f, 1f), TextAnchor.MiddleCenter);
            title.rectTransform.anchorMin = new Vector2(0.04f, 0.36f);
            title.rectTransform.anchorMax = new Vector2(0.96f, 0.78f);

            statsBannerText = CreateText("StatsBanner", headerCard, "BEST: 0  |  0 m  |  ENERGY: 0", 23, new Color(1.0f, 0.86f, 0.22f, 1f), TextAnchor.MiddleCenter);
            statsBannerText.rectTransform.anchorMin = new Vector2(0.03f, 0.16f);
            statsBannerText.rectTransform.anchorMax = new Vector2(0.97f, 0.36f);

            suitBadgeText = CreateText("SuitBadge", headerCard, "ACTIVE RIG: NEON VANGUARD", 19, new Color(0.76f, 0.90f, 1.0f, 0.90f), TextAnchor.MiddleCenter);
            suitBadgeText.rectTransform.anchorMin = new Vector2(0.04f, 0.04f);
            suitBadgeText.rectTransform.anchorMax = new Vector2(0.96f, 0.17f);

            // Bottom Action Dock (cleanly reveals 3D Runner in the center viewport!)
            RectTransform dock = CreateCard("MenuBottomDock", rootGo.transform,
                new Vector2(0.05f, 0.035f), new Vector2(0.95f, 0.345f),
                new Color(0.03f, 0.06f, 0.14f, 0.86f), new Color(0.68f, 0.22f, 1.0f, 0.88f));

            // Mode & Zone selector row
            modeButtonLabel = CreateButton("ModeSelectBtn", dock,
                new Vector2(0.04f, 0.69f), new Vector2(0.49f, 0.91f),
                new Color(0.06f, 0.11f, 0.22f, 0.95f), new Color(0.0f, 0.90f, 1.0f, 0.85f),
                "MODE: ENDLESS", 23, () =>
                {
                    CyberAudioManager.Instance?.PlaySfx(CyberSfxType.UiClick);
                    HapticsManager.Light();
                    runManager?.CycleGameModeInMenu();
                    RefreshTelemetry();
                });

            sectorButtonLabel = CreateButton("SectorSelectBtn", dock,
                new Vector2(0.51f, 0.69f), new Vector2(0.96f, 0.91f),
                new Color(0.06f, 0.11f, 0.22f, 0.95f), new Color(0.0f, 0.90f, 1.0f, 0.85f),
                "ZONE: NEON CITY", 22, () =>
                {
                    CyberAudioManager.Instance?.PlaySfx(CyberSfxType.LaneSwitch);
                    HapticsManager.Light();
                    runManager?.CycleBiomePreviewInMenu();
                    RefreshTelemetry();
                });

            // Primary Glowing PLAY CTA with gentle pulse
            Text playLbl = CreateButton("PlayCtaBtn", dock,
                new Vector2(0.04f, 0.34f), new Vector2(0.96f, 0.63f),
                new Color(0.0f, 0.28f, 0.36f, 0.98f), new Color(0.0f, 0.98f, 1.0f, 1f),
                "PLAY  >>", 44, () =>
                {
                    CyberAudioManager.Instance?.PlaySfx(CyberSfxType.PowerUpPickup);
                    HapticsManager.Medium();
                    onPlayClicked?.Invoke();
                });
            playLbl.transform.parent.gameObject.AddComponent<UiPanelAnimator>().Configure(enablePulse: true);

            // Bottom secondary row: CYBER HUB & SETTINGS
            hubButtonLabel = CreateButton("HubMenuBtn", dock,
                new Vector2(0.04f, 0.07f), new Vector2(0.56f, 0.28f),
                new Color(0.14f, 0.08f, 0.28f, 0.96f), new Color(1.0f, 0.84f, 0.14f, 1f),
                "CYBER HUB", 26, () =>
                {
                    CyberAudioManager.Instance?.PlaySfx(CyberSfxType.UiClick);
                    HapticsManager.Light();
                    onOpenHubClicked?.Invoke();
                });

            CreateButton("SettingsMenuBtn", dock,
                new Vector2(0.59f, 0.07f), new Vector2(0.96f, 0.28f),
                new Color(0.07f, 0.10f, 0.20f, 0.95f), new Color(0.70f, 0.25f, 1.0f, 0.95f),
                "SETTINGS", 26, () =>
                {
                    CyberAudioManager.Instance?.PlaySfx(CyberSfxType.UiClick);
                    HapticsManager.Light();
                    onOpenSettingsClicked?.Invoke();
                });
        }

        private static RectTransform CreateCard(string name, Transform parent, Vector2 min, Vector2 max, Color fill, Color border)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            go.AddComponent<Image>().color = fill;
            Outline outl = go.AddComponent<Outline>();
            outl.effectColor = border;
            outl.effectDistance = new Vector2(3f, -3f);
            return rt;
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

        private Text CreateButton(
            string name,
            Transform parent,
            Vector2 min,
            Vector2 max,
            Color fill,
            Color border,
            string label,
            int fontSize,
            UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            Image img = go.AddComponent<Image>();
            img.color = fill;
            Outline outl = go.AddComponent<Outline>();
            outl.effectColor = border;
            outl.effectDistance = new Vector2(2.5f, -2.5f);

            Button btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);

            Text txt = CreateText("Label", go.transform, label, fontSize, Color.white, TextAnchor.MiddleCenter);
            txt.rectTransform.anchorMin = Vector2.zero;
            txt.rectTransform.anchorMax = Vector2.one;
            return txt;
        }
    }
}
