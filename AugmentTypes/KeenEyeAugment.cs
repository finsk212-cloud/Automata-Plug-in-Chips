using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent;
using Terraria.ID;

namespace Augments
{
    public class KeenEyeAugment : Augment
    {
        public const float CritBonus = 8f;

        public override string Id => "keen_eye";
        public override string DisplayName => "Keen Eye";
        public override string Description =>
            $"Nearby teammates gain {AugmentText.Crit("+8% critical strike chance")}.";

        public override string GetDetailedDescription(Terraria.Player player) =>
            Description + "\n" +
            AugmentText.Note("Does not affect you.");
        public override AugmentRarity Rarity => AugmentRarity.Common;
        public override AugmentClass Class => AugmentClass.Support;
        public override bool HasAuraEffect => true;
        public override Texture2D Icon => TextureAssets.Item[ItemID.NightVisionHelmet].Value;
        // Pull-based aura: SupportAuraPlayer.UpdateEquips
    }
}
