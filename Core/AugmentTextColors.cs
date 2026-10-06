using Microsoft.Xna.Framework;

namespace Augments
{
    public static class AugmentTextColors
    {
        // Core Combat & Stats
        public const string BonusDamageHex = "FF5C5C";   // Damage, bonus damage, increased damage (Crisp crimson red)
        public const string CritHex = "FFC83B";          // Crit, crits, critical chance, critical strikes (Radiant gold/amber)
        public const string HealingHex = "4ADE80";       // Heal, healing, HP, health regen (Emerald jade green)
        public const string DefenseHex = "5EADFF";       // Defense, damage reduction, armor (Aegis shield blue)
        public const string SpecialDamageHex = "38BDF8"; // Special proc / burst damage (Luminous cyan)
        public const string ManaHex = "60A5FA";          // Mana, mana cost (Mystic blue)
        public const string AttackSpeedHex = "FB923C";   // Attack speed (Swift amber-orange)
        public const string MovementSpeedHex = "2DD4BF"; // Movement speed (Wind teal/turquoise)

        // Timing & Structure
        public const string OnHitHex = "FF3EA5";         // on hit, on-hit combat triggers (Vivid electric fuchsia/magenta)
        public const string TriggerHex = "FFA733";       // on kill, charges, stacks (Trigger amber)
        public const string DurationHex = "A5C8E8";      // 2s, 3s, 5s, seconds (Soft ice slate)
        public const string CooldownHex = "94A3B8";      // cooldown, seconds cooldown (Subdued slate gray)
        public const string ActiveHex = "FDE047";        // "Active:" label on keybind-triggered augments (Bright prompt yellow)
        public const string SupportClassHex = "8CE6A0";  // Support class references in descriptions (Pastel sage green)

        // Specific Statuses & Debuffs
        public const string BleedHex = "F87171";         // Bleeding, hemorrhage DoT (Blood coral)
        public const string FrostburnHex = "67E8F9";      // Frostburn debuff name (Frost aqua)
        public const string IchorHex = "FBBF24";         // Ichor debuff name (Ichor gold)
        public const string ImmobilizeHex = "C084FC";    // Slow, freeze, immobilize (Soft lavender)
        public const string BloodMoonHex = "FF6B6B";     // Blood Moon event keyword (Clean coral crimson)
        public const string SolarEclipseHex = "FACC15";  // Solar Eclipse event keyword (Solar gold)
        public const string NoteHex = "94A3B8";          // Technical / footnote text (Muted slate)

        public static readonly Color BonusDamage = new Color(255, 92, 92);
        public static readonly Color Crit = new Color(255, 200, 59);
        public static readonly Color Healing = new Color(74, 222, 128);
        public static readonly Color Defense = new Color(94, 173, 255);
        public static readonly Color SpecialDamage = new Color(56, 189, 248);
        public static readonly Color Mana = new Color(96, 165, 250);
        public static readonly Color AttackSpeed = new Color(251, 146, 60);
        public static readonly Color MovementSpeed = new Color(45, 212, 191);

        public static readonly Color OnHit = new Color(255, 62, 165);
        public static readonly Color Trigger = new Color(255, 167, 51);
        public static readonly Color Duration = new Color(165, 200, 232);
        public static readonly Color Cooldown = new Color(148, 163, 184);
        public static readonly Color Active = new Color(253, 224, 71);
        public static readonly Color SupportClass = new Color(140, 230, 160);

        public static readonly Color Bleed = new Color(248, 113, 113);
        public static readonly Color Frostburn = new Color(103, 232, 249);
        public static readonly Color Ichor = new Color(251, 191, 36);
        public static readonly Color Immobilize = new Color(192, 132, 252);
        public static readonly Color BloodMoon = new Color(255, 107, 107);
        public static readonly Color SolarEclipse = new Color(250, 204, 21);
        public static readonly Color Note = new Color(148, 163, 184);

        public static string GetRarityHex(AugmentRarity rarity) => rarity switch
        {
            AugmentRarity.Common => "B9C8E1",
            AugmentRarity.Rare => "55C3FF",
            AugmentRarity.Epic => "C373FF",
            AugmentRarity.Legendary => "FFB92D",
            _ => "B9C8E1"
        };
    }
}
