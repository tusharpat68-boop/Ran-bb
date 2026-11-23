using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using CyberRunner.Audio;
using CyberRunner.PowerUps;
using CyberRunner.Progression;

namespace CyberRunner.UI
{
    /// <summary>
    /// Full-featured Phase 5 Progression Hub overlay (Skins Shop, Power-Up Upgrades,
    /// Daily & Career Missions, 7-Day Daily Rewards, Achievements, and Local Top-5 Leaderboard).
    /// 100% in-game Energy Coins (zero IAP), portrait-optimized with touch scrolling.
    /// </summary>
    public sealed class ProgressionOverlayPanel : MonoBehaviour
    {
        private enum HubTab
        {
            Skins = 0,
            Upgrades = 1,
            Missions = 2,
            Rewards = 3
        }

        private ProgressionManager progression;
        private Font uiFont;

        private GameObject rootPanel;
        private Text energyHeaderText;
        private Text feedbackBannerText;
        private RectTransform scrollContent;
        private ScrollRect scrollRect;

        private Image[] tabBackgrounds;
        private Outline[] tabBorders;
        private Text[] tabLabels;

        private HubTab activeTab = HubTab.Skins;
        private float feedbackTimer;
        private bool built;

        public bool IsOpen => rootPanel != null && rootPanel.activeSelf;

        public void Initialize(Transform parentSafeArea, ProgressionManager progressionManager, Font font)
        {
            progression = progressionManager;
            uiFont = font;

            if (!built)
            {
                BuildOverlay(parentSafeArea);
                built = true;
            }

            if (progression != null)
            {
                progression.OnProgressionChanged -= HandleProgressionChanged;
                progression.OnProgressionChanged += HandleProgressionChanged;
            }
        }

        private void OnDestroy()
        {
            if (progression != null)
            {
                progression.OnProgressionChanged -= HandleProgressionChanged;
            }
        }

        private void Update()
        {
            if (feedbackTimer > 0f)
            {
                feedbackTimer -= Time.unscaledDeltaTime;
                if (feedbackTimer <= 0f && feedbackBannerText != null)
                {
                    feedbackBannerText.text = string.Empty;
                }
            }
        }

        public void Open(int defaultTabIndex = -1)
        {
            if (rootPanel == null) return;

            if (defaultTabIndex >= 0 && defaultTabIndex <= 3)
            {
                activeTab = (HubTab)defaultTabIndex;
            }
            else if (progression != null && progression.TotalUnclaimedRewards > 0)
            {
                // Auto-focus Rewards or Missions if player has unclaimed rewards waiting!
                activeTab = progression.DailyRewards.CanClaimToday(progression.Data) ||
                            progression.Achievements.CountClaimable(progression.Data) > 0
                    ? HubTab.Rewards
                    : HubTab.Missions;
            }

            rootPanel.SetActive(true);
            RefreshAll();
        }

        public void Close()
        {
            if (rootPanel != null)
            {
                rootPanel.SetActive(false);
            }
            CyberAudioManager.Instance?.PlaySfx(CyberSfxType.UiClick);
        }

        private void HandleProgressionChanged()
        {
            if (IsOpen)
            {
                RefreshAll();
            }
        }

        private void SelectTab(HubTab tab)
        {
            activeTab = tab;
            CyberAudioManager.Instance?.PlaySfx(CyberSfxType.UiClick);
            HapticsManager.Light();
            RefreshAll();
            if (scrollRect != null)
            {
                scrollRect.verticalNormalizedPosition = 1f;
            }
        }

        private void RefreshAll()
        {
            if (!built || progression == null || progression.Data == null) return;

            if (energyHeaderText != null)
            {
                energyHeaderText.text = $"ENERGY: {progression.EnergyBalance}";
            }

            UpdateTabVisuals();
            RebuildTabContent();
        }

        private void UpdateTabVisuals()
        {
            Color cyan = new Color(0.0f, 0.94f, 1.0f, 1f);
            Color dimBorder = new Color(0.24f, 0.34f, 0.50f, 0.85f);
            Color activeFill = new Color(0.0f, 0.22f, 0.32f, 0.96f);
            Color idleFill = new Color(0.05f, 0.08f, 0.16f, 0.92f);

            int missionClaims = progression.Missions.CountClaimable(progression.Data);
            int rewardClaims = progression.Achievements.CountClaimable(progression.Data) +
                               (progression.DailyRewards.CanClaimToday(progression.Data) ? 1 : 0);

            string[] labels =
            {
                "SKINS",
                "UPGRADES",
                missionClaims > 0 ? $"MISSIONS ({missionClaims})" : "MISSIONS",
                rewardClaims > 0 ? $"REWARDS ({rewardClaims})" : "REWARDS"
            };

            for (int i = 0; i < 4; i++)
            {
                bool isSel = (int)activeTab == i;
                tabBackgrounds[i].color = isSel ? activeFill : idleFill;
                tabBorders[i].effectColor = isSel ? cyan : dimBorder;
                tabLabels[i].text = labels[i];
                tabLabels[i].color = isSel ? cyan : Color.white;
            }
        }

        private void RebuildTabContent()
        {
            for (int i = scrollContent.childCount - 1; i >= 0; i--)
            {
                Destroy(scrollContent.GetChild(i).gameObject);
            }

            switch (activeTab)
            {
                case HubTab.Skins:
                    PopulateSkinsTab();
                    break;
                case HubTab.Upgrades:
                    PopulateUpgradesTab();
                    break;
                case HubTab.Missions:
                    PopulateMissionsTab();
                    break;
                case HubTab.Rewards:
                    PopulateRewardsTab();
                    break;
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(scrollContent);
        }

        private void PopulateSkinsTab()
        {
            CreateSectionHeader("COSMETIC RUNNER SUITS (IN-GAME ENERGY ONLY)");

            CharacterSkinDefinition[] skins = CharacterSkinCatalog.AllSkins;
            for (int i = 0; i < skins.Length; i++)
            {
                CharacterSkinDefinition skin = skins[i];
                bool unlocked = progression.IsSkinUnlocked(skin.Id);
                bool equipped = progression.IsSkinEquipped(skin.Id);

                string btnText;
                Color btnColor;
                Color borderColor;

                if (equipped)
                {
                    btnText = "EQUIPPED";
                    btnColor = new Color(0.06f, 0.24f, 0.18f, 0.95f);
                    borderColor = new Color(0.18f, 1.0f, 0.55f, 1f);
                }
                else if (unlocked)
                {
                    btnText = "EQUIP";
                    btnColor = new Color(0.0f, 0.22f, 0.30f, 0.95f);
                    borderColor = new Color(0.0f, 0.94f, 1.0f, 1f);
                }
                else
                {
                    bool canAfford = progression.EnergyBalance >= skin.EnergyCost;
                    btnText = $"BUY {skin.EnergyCost} E";
                    btnColor = canAfford ? new Color(0.16f, 0.08f, 0.30f, 0.95f) : new Color(0.12f, 0.12f, 0.16f, 0.9f);
                    borderColor = canAfford ? new Color(0.72f, 0.28f, 1.0f, 1f) : new Color(0.36f, 0.36f, 0.44f, 0.8f);
                }

                string capturedId = skin.Id;
                CreateRowCard(
                    skin.DisplayName,
                    skin.Tagline,
                    skin.PrimaryNeonColor,
                    skin.SecondaryAccentColor,
                    btnText,
                    btnColor,
                    borderColor,
                    !equipped,
                    () =>
                    {
                        bool ok = progression.TryBuyOrEquipSkin(capturedId, out string msg);
                        CyberAudioManager.Instance?.PlaySfx(ok ? CyberSfxType.RewardClaim : CyberSfxType.Stumble);
                        if (ok) HapticsManager.Medium();
                        ShowFeedback(msg, skin.PrimaryNeonColor);
                        RefreshAll();
                    });
            }
        }

        private void PopulateUpgradesTab()
        {
            CreateSectionHeader("POWER-UP OVERCLOCK UPGRADES (+1.5s DURATION, -2s COOLDOWN)");

            for (int i = 0; i < PowerUpCatalog.PowerUpCount; i++)
            {
                PowerUpType type = (PowerUpType)i;
                PowerUpDefinition def = PowerUpCatalog.Get(type);
                int lv = progression.GetPowerUpLevel(type);
                int cost = progression.GetNextPowerUpUpgradeCost(type);
                float dur = def.BaseDuration + (lv * 1.5f);
                float cd = Mathf.Max(10f, def.AbilityCooldown - (lv * 2.0f));

                bool maxed = cost < 0;
                bool canAfford = !maxed && progression.EnergyBalance >= cost;

                string subtitle = $"LV.{lv}/{ProgressionManager.MaxPowerUpUpgradeLevel}  |  {dur:F1}s Active  |  {cd:F0}s Cooldown";
                string btnText = maxed ? "MAX LV" : $"UP {cost} E";
                Color btnFill = maxed
                    ? new Color(0.06f, 0.22f, 0.16f, 0.92f)
                    : (canAfford ? new Color(0.0f, 0.22f, 0.30f, 0.95f) : new Color(0.12f, 0.12f, 0.16f, 0.9f));
                Color btnBorder = maxed
                    ? new Color(0.20f, 1.0f, 0.56f, 1f)
                    : (canAfford ? def.AccentColor : new Color(0.36f, 0.36f, 0.44f, 0.8f));

                PowerUpType capturedType = type;
                CreateRowCard(
                    def.DisplayName,
                    subtitle,
                    def.AccentColor,
                    def.AccentColor,
                    btnText,
                    btnFill,
                    btnBorder,
                    !maxed,
                    () =>
                    {
                        bool ok = progression.TryUpgradePowerUp(capturedType, out string msg);
                        CyberAudioManager.Instance?.PlaySfx(ok ? CyberSfxType.RewardClaim : CyberSfxType.Stumble);
                        if (ok) HapticsManager.Medium();
                        ShowFeedback(msg, def.AccentColor);
                        RefreshAll();
                    });
            }
        }

        private void PopulateMissionsTab()
        {
            PlayerSaveData data = progression.Data;

            CreateSectionHeader($"DAILY MISSIONS ({data.dailyMissionDateUtc})");
            IReadOnlyList<MissionDefinition> dailies = progression.Missions.ActiveDailyMissions;
            for (int i = 0; i < dailies.Count; i++)
            {
                AddMissionRow(dailies[i], data);
            }

            CreateSectionHeader("CAREER MISSIONS");
            MissionDefinition[] careers = MissionSystem.CareerCatalog;
            for (int i = 0; i < careers.Length; i++)
            {
                AddMissionRow(careers[i], data);
            }
        }

        private void AddMissionRow(MissionDefinition def, PlayerSaveData data)
        {
            MissionSaveEntry entry = progression.Missions.GetProgress(data, def);
            int prog = entry != null ? entry.progress : 0;
            bool claimed = entry != null && entry.claimed;
            bool ready = !claimed && prog >= def.TargetValue;

            string sub = $"Progress: {prog} / {def.TargetValue}   |   Reward: +{def.RewardEnergy} E";
            string btnLabel = claimed ? "CLAIMED" : (ready ? $"CLAIM +{def.RewardEnergy}" : $"{Mathf.FloorToInt((prog * 100f) / Mathf.Max(1, def.TargetValue))}%");
            Color fill = ready
                ? new Color(0.0f, 0.28f, 0.22f, 0.96f)
                : new Color(0.08f, 0.11f, 0.18f, 0.9f);
            Color border = ready
                ? new Color(0.16f, 1.0f, 0.55f, 1f)
                : (claimed ? new Color(0.32f, 0.42f, 0.52f, 0.7f) : new Color(0.0f, 0.68f, 0.86f, 0.8f));

            MissionDefinition captured = def;
            CreateRowCard(
                def.Title,
                sub,
                def.IsDaily ? new Color(1.0f, 0.78f, 0.16f, 1f) : new Color(0.0f, 0.94f, 1.0f, 1f),
                new Color(0.68f, 0.22f, 1.0f, 1f),
                btnLabel,
                fill,
                border,
                ready,
                () =>
                {
                    if (progression.TryClaimMission(captured))
                    {
                        CyberAudioManager.Instance?.PlaySfx(CyberSfxType.RewardClaim);
                        HapticsManager.Medium();
                        ShowFeedback($"CLAIMED +{captured.RewardEnergy} ENERGY!", new Color(0.16f, 1.0f, 0.55f, 1f));
                        RefreshAll();
                    }
                });
        }

        private void PopulateRewardsTab()
        {
            PlayerSaveData data = progression.Data;

            // 1. 7-Day Daily Login Reward Card
            CreateSectionHeader("7-DAY DAILY LOGIN REWARD");
            bool canClaimDaily = progression.DailyRewards.CanClaimToday(data);
            int dayIdx = progression.DailyRewards.GetCurrentRewardDayIndex(data);
            int dailyAmount = progression.DailyRewards.GetTodayRewardAmount(data);
            string dailyTitle = $"DAY {dayIdx + 1} / 7  —  +{dailyAmount} ENERGY";
            string dailySub = canClaimDaily
                ? $"Streak: {data.dailyRewardStreak} days  |  Ready to claim today!"
                : $"Streak: {data.dailyRewardStreak} days  |  Come back tomorrow!";

            CreateRowCard(
                dailyTitle,
                dailySub,
                new Color(0.18f, 1.0f, 0.55f, 1f),
                new Color(1.0f, 0.84f, 0.14f, 1f),
                canClaimDaily ? $"CLAIM +{dailyAmount}" : "CLAIMED",
                canClaimDaily ? new Color(0.0f, 0.30f, 0.22f, 0.96f) : new Color(0.08f, 0.11f, 0.18f, 0.9f),
                canClaimDaily ? new Color(0.18f, 1.0f, 0.55f, 1f) : new Color(0.32f, 0.42f, 0.52f, 0.7f),
                canClaimDaily,
                () =>
                {
                    if (progression.TryClaimDailyReward())
                    {
                        CyberAudioManager.Instance?.PlaySfx(CyberSfxType.RewardClaim);
                        HapticsManager.Medium();
                        ShowFeedback($"DAILY BONUS +{dailyAmount} ENERGY!", new Color(0.18f, 1.0f, 0.55f, 1f));
                        RefreshAll();
                    }
                });

            // 2. Career Achievements
            CreateSectionHeader("ACHIEVEMENTS");
            AchievementDefinition[] achs = AchievementSystem.Catalog;
            for (int i = 0; i < achs.Length; i++)
            {
                AchievementDefinition def = achs[i];
                AchievementSaveEntry entry = progression.Achievements.GetEntry(data, def.Id);
                bool unlocked = entry != null && entry.unlocked;
                bool claimed = entry != null && entry.claimed;
                bool ready = unlocked && !claimed;

                string btnText = claimed ? "CLAIMED" : (ready ? $"CLAIM +{def.RewardEnergy}" : "LOCKED");
                Color fill = ready ? new Color(0.24f, 0.18f, 0.04f, 0.96f) : new Color(0.08f, 0.10f, 0.17f, 0.9f);
                Color border = ready ? new Color(1.0f, 0.84f, 0.14f, 1f) : new Color(0.32f, 0.40f, 0.54f, 0.75f);

                AchievementDefinition captured = def;
                CreateRowCard(
                    def.Title,
                    $"{def.Description}  (+{def.RewardEnergy} E)",
                    unlocked ? new Color(1.0f, 0.84f, 0.14f, 1f) : new Color(0.36f, 0.44f, 0.56f, 1f),
                    new Color(0.0f, 0.94f, 1.0f, 1f),
                    btnText,
                    fill,
                    border,
                    ready,
                    () =>
                    {
                        if (progression.TryClaimAchievement(captured))
                        {
                            CyberAudioManager.Instance?.PlaySfx(CyberSfxType.RewardClaim);
                            HapticsManager.Medium();
                            ShowFeedback($"ACHIEVEMENT +{captured.RewardEnergy} E!", new Color(1.0f, 0.84f, 0.14f, 1f));
                            RefreshAll();
                        }
                    });
            }

            // 3. Offline Local Top-5 Leaderboard
            CreateSectionHeader("LOCAL TOP-5 RECORDS (OFFLINE)");
            List<LocalScoreEntry> board = data.localLeaderboard;
            if (board == null || board.Count == 0)
            {
                CreateSectionHeader("  Complete a run to record your first high score!");
            }
            else
            {
                for (int i = 0; i < board.Count; i++)
                {
                    LocalScoreEntry e = board[i];
                    CreateRowCard(
                        $"#{i + 1}  SCORE: {e.score}",
                        $"{e.distanceMeters} m  |  {e.biomeName}  |  {e.dateUtc}",
                        new Color(0.0f, 0.94f, 1.0f, 1f),
                        new Color(0.68f, 0.22f, 1.0f, 1f),
                        $"#{i + 1}",
                        new Color(0.06f, 0.10f, 0.18f, 0.9f),
                        new Color(0.0f, 0.72f, 0.88f, 0.8f),
                        false,
                        null);
                }
            }
        }

        private void ShowFeedback(string message, Color color)
        {
            if (feedbackBannerText == null) return;
            feedbackBannerText.text = message;
            feedbackBannerText.color = color;
            feedbackTimer = 2.0f;
        }

        private void BuildOverlay(Transform parentSafeArea)
        {
            var rootGo = new GameObject("ProgressionHubOverlay");
            rootGo.transform.SetParent(parentSafeArea, false);
            RectTransform rt = rootGo.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            Image bg = rootGo.AddComponent<Image>();
            bg.color = new Color(0.02f, 0.04f, 0.10f, 0.94f);
            bg.raycastTarget = true;
            rootPanel = rootGo;

            // Main Hub Card
            var cardGo = new GameObject("HubCard");
            cardGo.transform.SetParent(rootGo.transform, false);
            RectTransform cardRt = cardGo.AddComponent<RectTransform>();
            cardRt.anchorMin = new Vector2(0.04f, 0.04f);
            cardRt.anchorMax = new Vector2(0.96f, 0.96f);
            cardRt.offsetMin = Vector2.zero;
            cardRt.offsetMax = Vector2.zero;

            Image cardImg = cardGo.AddComponent<Image>();
            cardImg.color = new Color(0.04f, 0.07f, 0.15f, 0.98f);
            Outline cardOutline = cardGo.AddComponent<Outline>();
            cardOutline.effectColor = new Color(0.0f, 0.94f, 1.0f, 0.95f);
            cardOutline.effectDistance = new Vector2(3f, -3f);
            cardGo.AddComponent<UiPanelAnimator>();

            // Header Title + Energy Balance + Close Button
            Text title = CreateText("HubTitle", cardGo.transform, "CYBER HUB", 40, new Color(0.0f, 0.94f, 1.0f, 1f), TextAnchor.MiddleLeft);
            title.rectTransform.anchorMin = new Vector2(0.04f, 0.91f);
            title.rectTransform.anchorMax = new Vector2(0.46f, 0.985f);

            energyHeaderText = CreateText("EnergyHeader", cardGo.transform, "ENERGY: 0", 30, new Color(1.0f, 0.84f, 0.14f, 1f), TextAnchor.MiddleRight);
            energyHeaderText.rectTransform.anchorMin = new Vector2(0.42f, 0.91f);
            energyHeaderText.rectTransform.anchorMax = new Vector2(0.80f, 0.985f);

            CreateButton("CloseHubBtn", cardGo.transform, new Vector2(0.82f, 0.918f), new Vector2(0.965f, 0.980f),
                new Color(0.24f, 0.06f, 0.12f, 0.95f), new Color(1.0f, 0.22f, 0.34f, 1f), "CLOSE", 24, Close);

            // 4 Tab Buttons
            tabBackgrounds = new Image[4];
            tabBorders = new Outline[4];
            tabLabels = new Text[4];
            string[] initTabs = { "SKINS", "UPGRADES", "MISSIONS", "REWARDS" };
            for (int i = 0; i < 4; i++)
            {
                float x0 = 0.03f + (i * 0.237f);
                float x1 = x0 + 0.227f;
                HubTab captured = (HubTab)i;

                var tabGo = new GameObject($"Tab_{i}");
                tabGo.transform.SetParent(cardGo.transform, false);
                RectTransform tRt = tabGo.AddComponent<RectTransform>();
                tRt.anchorMin = new Vector2(x0, 0.845f);
                tRt.anchorMax = new Vector2(x1, 0.902f);
                tRt.offsetMin = Vector2.zero;
                tRt.offsetMax = Vector2.zero;

                Image tImg = tabGo.AddComponent<Image>();
                tImg.color = new Color(0.05f, 0.08f, 0.16f, 0.92f);
                Outline tOut = tabGo.AddComponent<Outline>();
                tOut.effectColor = new Color(0.24f, 0.34f, 0.50f, 0.85f);
                tOut.effectDistance = new Vector2(2f, -2f);

                Button btn = tabGo.AddComponent<Button>();
                btn.targetGraphic = tImg;
                btn.onClick.AddListener(() => SelectTab(captured));

                Text lbl = CreateText("Label", tabGo.transform, initTabs[i], 20, Color.white, TextAnchor.MiddleCenter);
                lbl.rectTransform.anchorMin = Vector2.zero;
                lbl.rectTransform.anchorMax = Vector2.one;

                tabBackgrounds[i] = tImg;
                tabBorders[i] = tOut;
                tabLabels[i] = lbl;
            }

            // Feedback banner
            feedbackBannerText = CreateText("FeedbackBanner", cardGo.transform, string.Empty, 22, new Color(0.18f, 1.0f, 0.55f, 1f), TextAnchor.MiddleCenter);
            feedbackBannerText.rectTransform.anchorMin = new Vector2(0.04f, 0.800f);
            feedbackBannerText.rectTransform.anchorMax = new Vector2(0.96f, 0.840f);

            // ScrollRect Viewport
            var scrollGo = new GameObject("ScrollView");
            scrollGo.transform.SetParent(cardGo.transform, false);
            RectTransform sRt = scrollGo.AddComponent<RectTransform>();
            sRt.anchorMin = new Vector2(0.03f, 0.025f);
            sRt.anchorMax = new Vector2(0.97f, 0.796f);
            sRt.offsetMin = Vector2.zero;
            sRt.offsetMax = Vector2.zero;

            scrollGo.AddComponent<Image>().color = new Color(0.02f, 0.05f, 0.10f, 0.70f);
            scrollGo.AddComponent<RectMask2D>();

            scrollRect = scrollGo.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Elastic;
            scrollRect.scrollSensitivity = 24f;

            var contentGo = new GameObject("Content");
            contentGo.transform.SetParent(scrollGo.transform, false);
            scrollContent = contentGo.AddComponent<RectTransform>();
            scrollContent.anchorMin = new Vector2(0f, 1f);
            scrollContent.anchorMax = new Vector2(1f, 1f);
            scrollContent.pivot = new Vector2(0.5f, 1f);
            scrollContent.offsetMin = Vector2.zero;
            scrollContent.offsetMax = Vector2.zero;

            VerticalLayoutGroup vlg = contentGo.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(14, 14, 14, 18);
            vlg.spacing = 12f;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            ContentSizeFitter csf = contentGo.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.content = scrollContent;
            scrollRect.viewport = sRt;

            rootPanel.SetActive(false);
        }

        private void CreateSectionHeader(string title)
        {
            var go = new GameObject("SectionHeader");
            go.transform.SetParent(scrollContent, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0f, 40f);

            LayoutElement le = go.AddComponent<LayoutElement>();
            le.preferredHeight = 40f;

            Text txt = go.AddComponent<Text>();
            txt.font = uiFont;
            txt.text = title;
            txt.fontSize = 21;
            txt.color = new Color(0.0f, 0.90f, 1.0f, 0.92f);
            txt.alignment = TextAnchor.LowerLeft;
            txt.raycastTarget = false;
        }

        private void CreateRowCard(
            string title,
            string subtitle,
            Color swatchA,
            Color swatchB,
            string buttonText,
            Color buttonFill,
            Color buttonBorder,
            bool interactable,
            UnityEngine.Events.UnityAction onAction)
        {
            var card = new GameObject("RowCard");
            card.transform.SetParent(scrollContent, false);
            RectTransform rt = card.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0f, 118f);

            LayoutElement le = card.AddComponent<LayoutElement>();
            le.preferredHeight = 118f;

            Image bg = card.AddComponent<Image>();
            bg.color = new Color(0.06f, 0.10f, 0.20f, 0.92f);

            Outline cardOut = card.AddComponent<Outline>();
            cardOut.effectColor = new Color(0.18f, 0.30f, 0.48f, 0.75f);
            cardOut.effectDistance = new Vector2(1.5f, -1.5f);

            // Dual Neon Swatch strip on the left
            CreateSwatch("SwatchA", card.transform, new Vector2(0.015f, 0.50f), new Vector2(0.042f, 0.88f), swatchA);
            CreateSwatch("SwatchB", card.transform, new Vector2(0.015f, 0.12f), new Vector2(0.042f, 0.48f), swatchB);

            Text titleTxt = CreateText("Title", card.transform, title, 25, Color.white, TextAnchor.MiddleLeft);
            titleTxt.rectTransform.anchorMin = new Vector2(0.06f, 0.48f);
            titleTxt.rectTransform.anchorMax = new Vector2(0.68f, 0.92f);

            Text subTxt = CreateText("Subtitle", card.transform, subtitle, 19, new Color(0.72f, 0.82f, 0.96f, 0.92f), TextAnchor.MiddleLeft);
            subTxt.rectTransform.anchorMin = new Vector2(0.06f, 0.08f);
            subTxt.rectTransform.anchorMax = new Vector2(0.68f, 0.50f);

            Button btn = CreateButton("ActionBtn", card.transform, new Vector2(0.70f, 0.18f), new Vector2(0.975f, 0.82f),
                buttonFill, buttonBorder, buttonText, 22, onAction);
            btn.interactable = interactable && onAction != null;
        }

        private static void CreateSwatch(string name, Transform parent, Vector2 min, Vector2 max, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            Image img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
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

        private Button CreateButton(
            string name,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Color fill,
            Color border,
            string label,
            int fontSize,
            UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            Image img = go.AddComponent<Image>();
            img.color = fill;

            Outline outl = go.AddComponent<Outline>();
            outl.effectColor = border;
            outl.effectDistance = new Vector2(2f, -2f);

            Button btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            if (onClick != null)
            {
                btn.onClick.AddListener(onClick);
            }

            Text txt = CreateText("Label", go.transform, label, fontSize, Color.white, TextAnchor.MiddleCenter);
            txt.rectTransform.anchorMin = Vector2.zero;
            txt.rectTransform.anchorMax = Vector2.one;
            return btn;
        }
    }
}
