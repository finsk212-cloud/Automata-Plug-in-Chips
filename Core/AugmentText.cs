namespace Augments
{
    public static class AugmentText
    {
        public static string Color(string text, string hex)
        {
            return $"[c/{hex}:{text}]";
        }

        public static string OnHit(string text = "on-hit") => Color(text, AugmentTextColors.OnHitHex);
        public static string Trigger(string text)
        {
            if (text.Equals("on-hit", System.StringComparison.OrdinalIgnoreCase) ||
                text.Equals("on hit", System.StringComparison.OrdinalIgnoreCase))
            {
                return OnHit(text);
            }
            return Color(text, AugmentTextColors.TriggerHex);
        }
        public static string Healing(string text) => Color(text, AugmentTextColors.HealingHex);
        public static string HP(string text) => Color(text, AugmentTextColors.HealingHex);
        public static string Crit(string text) => Color(text, AugmentTextColors.CritHex);
        public static string BonusDamage(string text) => Color(text, AugmentTextColors.BonusDamageHex);
        public static string Defense(string text) => Color(text, AugmentTextColors.DefenseHex);
        public static string AttackSpeed(string text) => Color(text, AugmentTextColors.AttackSpeedHex);
        public static string MovementSpeed(string text) => Color(text, AugmentTextColors.MovementSpeedHex);
        public static string Mana(string text) => Color(text, AugmentTextColors.ManaHex);
        public static string Cooldown(string text) => Color(text, AugmentTextColors.CooldownHex);
        public static string Duration(string text) => Color(text, AugmentTextColors.DurationHex);
        public static string SpecialDamage(string text) => Color(text, AugmentTextColors.SpecialDamageHex);
        public static string Immobilize(string text) => Color(text, AugmentTextColors.ImmobilizeHex);
        public static string Frostburn(string text) => Color(text, AugmentTextColors.FrostburnHex);
        public static string Ichor(string text) => Color(text, AugmentTextColors.IchorHex);
        public static string Bleed(string text) => Color(text, AugmentTextColors.BleedHex);
        public static string SupportClass(string text) => Color(text, AugmentTextColors.SupportClassHex);
        public static string Active(string text) => Color(text, AugmentTextColors.ActiveHex);
        public static string BloodMoon(string text) => Color(text, AugmentTextColors.BloodMoonHex);
        public static string SolarEclipse(string text) => Color(text, AugmentTextColors.SolarEclipseHex);
        public static string Note(string text) => Color(text, AugmentTextColors.NoteHex);

        // The key a plugin is bound to, for descriptions: "Press [" + Key(kb) + "]" shows the live
        // assigned key and is what the plugin list turns into a click-to-rebind button.
        public static string Key(Terraria.ModLoader.ModKeybind keybind)
        {
            string label = KeybindEditor.GetLabel(keybind, out string hex);
            return Color(label, hex);
        }
    }
}
