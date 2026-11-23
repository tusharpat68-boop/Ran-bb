using UnityEngine;

namespace CyberRunner.PowerUps
{
    /// <summary>
    /// All six futuristic CYBER RUNNER power-ups specified in Section 8.
    /// </summary>
    public enum PowerUpType
    {
        EnergyMagnet = 0,
        CyberShield = 1,
        SpeedBoost = 2,
        DoubleEnergy = 3,
        HoverBoots = 4,
        DroneSupport = 5
    }

    public readonly struct PowerUpDefinition
    {
        public readonly PowerUpType Type;
        public readonly string DisplayName;
        public readonly string ShortLabel;
        public readonly float BaseDuration;
        public readonly float AbilityCooldown;
        public readonly Color AccentColor;

        public PowerUpDefinition(
            PowerUpType type,
            string displayName,
            string shortLabel,
            float baseDuration,
            float abilityCooldown,
            Color accentColor)
        {
            Type = type;
            DisplayName = displayName;
            ShortLabel = shortLabel;
            BaseDuration = baseDuration;
            AbilityCooldown = abilityCooldown;
            AccentColor = accentColor;
        }
    }

    public static class PowerUpCatalog
    {
        public const int PowerUpCount = 6;

        public static PowerUpDefinition Get(PowerUpType type)
        {
            switch (type)
            {
                case PowerUpType.EnergyMagnet:
                    return new PowerUpDefinition(
                        PowerUpType.EnergyMagnet,
                        "ENERGY MAGNET",
                        "MAGNET",
                        baseDuration: 9.0f,
                        abilityCooldown: 20f,
                        accentColor: new Color(1.0f, 0.78f, 0.14f, 1f));

                case PowerUpType.CyberShield:
                    return new PowerUpDefinition(
                        PowerUpType.CyberShield,
                        "CYBER SHIELD",
                        "SHIELD",
                        baseDuration: 12.0f,
                        abilityCooldown: 22f,
                        accentColor: new Color(0.0f, 0.94f, 1.0f, 1f));

                case PowerUpType.SpeedBoost:
                    return new PowerUpDefinition(
                        PowerUpType.SpeedBoost,
                        "SPEED BOOST",
                        "BOOST",
                        baseDuration: 5.5f,
                        abilityCooldown: 24f,
                        accentColor: new Color(0.96f, 0.20f, 0.92f, 1f));

                case PowerUpType.DoubleEnergy:
                    return new PowerUpDefinition(
                        PowerUpType.DoubleEnergy,
                        "DOUBLE ENERGY",
                        "2X COIN",
                        baseDuration: 10.0f,
                        abilityCooldown: 20f,
                        accentColor: new Color(0.18f, 0.96f, 0.45f, 1f));

                case PowerUpType.HoverBoots:
                    return new PowerUpDefinition(
                        PowerUpType.HoverBoots,
                        "HOVER BOOTS",
                        "HOVER",
                        baseDuration: 8.5f,
                        abilityCooldown: 24f,
                        accentColor: new Color(0.45f, 0.65f, 1.0f, 1f));

                default:
                    return new PowerUpDefinition(
                        PowerUpType.DroneSupport,
                        "DRONE SUPPORT",
                        "DRONE",
                        baseDuration: 10.0f,
                        abilityCooldown: 26f,
                        accentColor: new Color(1.0f, 0.42f, 0.18f, 1f));
            }
        }
    }
}
