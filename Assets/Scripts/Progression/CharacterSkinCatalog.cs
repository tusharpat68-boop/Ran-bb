using System;
using UnityEngine;

namespace CyberRunner.Progression
{
    public readonly struct CharacterSkinDefinition
    {
        public readonly string Id;
        public readonly string DisplayName;
        public readonly string Tagline;
        public readonly int EnergyCost;
        public readonly Color SuitColor;
        public readonly Color PrimaryNeonColor;
        public readonly Color SecondaryAccentColor;

        public CharacterSkinDefinition(
            string id,
            string displayName,
            string tagline,
            int energyCost,
            Color suitColor,
            Color primaryNeonColor,
            Color secondaryAccentColor)
        {
            Id = id;
            DisplayName = displayName;
            Tagline = tagline;
            EnergyCost = energyCost;
            SuitColor = suitColor;
            PrimaryNeonColor = primaryNeonColor;
            SecondaryAccentColor = secondaryAccentColor;
        }
    }

    /// <summary>
    /// Cosmetic-only futuristic runner skins unlockable strictly with in-game Energy Coins (zero IAP).
    /// </summary>
    public static class CharacterSkinCatalog
    {
        public static readonly CharacterSkinDefinition[] AllSkins =
        {
            new CharacterSkinDefinition(
                "neon_vanguard",
                "NEON VANGUARD",
                "Standard 2099 Intercept Rig",
                0,
                new Color(0.08f, 0.10f, 0.15f, 1f),
                new Color(0.00f, 0.94f, 1.00f, 1f),
                new Color(0.68f, 0.22f, 1.00f, 1f)),

            new CharacterSkinDefinition(
                "crimson_phantom",
                "CRIMSON PHANTOM",
                "Underground Redline Stealth Suit",
                300,
                new Color(0.11f, 0.08f, 0.10f, 1f),
                new Color(1.00f, 0.18f, 0.30f, 1f),
                new Color(1.00f, 0.68f, 0.10f, 1f)),

            new CharacterSkinDefinition(
                "volt_striker",
                "VOLT STRIKER",
                "Overclocked Sector-04 Runner",
                600,
                new Color(0.06f, 0.11f, 0.14f, 1f),
                new Color(0.16f, 1.00f, 0.45f, 1f),
                new Color(0.00f, 0.92f, 1.00f, 1f)),

            new CharacterSkinDefinition(
                "cyber_valkyrie",
                "CYBER VALKYRIE",
                "Sky Highway Alloy Chassis",
                950,
                new Color(0.70f, 0.75f, 0.84f, 1f),
                new Color(1.00f, 0.18f, 0.78f, 1f),
                new Color(0.00f, 0.92f, 1.00f, 1f)),

            new CharacterSkinDefinition(
                "solar_apex",
                "SOLAR APEX",
                "High-Altitude Gold Spec Armor",
                1400,
                new Color(0.15f, 0.12f, 0.09f, 1f),
                new Color(1.00f, 0.84f, 0.16f, 1f),
                new Color(1.00f, 0.28f, 0.18f, 1f)),

            new CharacterSkinDefinition(
                "quantum_zero",
                "QUANTUM ZERO",
                "Digital Core Ghost Prototype",
                2000,
                new Color(0.05f, 0.04f, 0.14f, 1f),
                new Color(0.76f, 0.30f, 1.00f, 1f),
                new Color(0.00f, 1.00f, 0.92f, 1f)),
        };

        public static CharacterSkinDefinition GetById(string skinId)
        {
            if (!string.IsNullOrEmpty(skinId))
            {
                for (int i = 0; i < AllSkins.Length; i++)
                {
                    if (string.Equals(AllSkins[i].Id, skinId, StringComparison.Ordinal))
                    {
                        return AllSkins[i];
                    }
                }
            }

            return AllSkins[0];
        }
    }
}
