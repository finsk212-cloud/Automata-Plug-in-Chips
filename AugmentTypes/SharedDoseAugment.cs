using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace Augments
{
    public class SharedDoseAugment : Augment
    {
        public override string Id => "shared_dose";
        public override string DisplayName => "Shared Dose";
        public override string Description =>
            $"Medi Gun Overclock also restores {AugmentText.Healing("50% of its heal")} to you.";
        public override AugmentRarity Rarity => AugmentRarity.Common;
        public override AugmentClass Class => AugmentClass.Support;
        public override Texture2D Icon => TextureAssets.Item[ItemID.HealingPotion].Value;

        // Self-heal for a given Overclock heal, or 0 when the medic doesn't own the plugin.
        public static int GetSelfHeal(Player medic, int overclockHeal) =>
            medic != null && overclockHeal > 0 && medic.GetModPlayer<AugmentPlayer>().HasAugment("shared_dose")
                ? System.Math.Max(1, overclockHeal / 2)
                : 0;
    }
}
