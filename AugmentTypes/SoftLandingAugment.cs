using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent;
using Terraria.ID;

namespace Augments
{
    public class SoftLandingAugment : Augment
    {
        public override string Id => "soft_landing";
        public override string DisplayName => "Soft Landing";
        public override string Description =>
            $"You and nearby teammates take {AugmentText.Defense("no fall damage")} while inside your support aura.";
        public override AugmentRarity Rarity => AugmentRarity.Common;
        public override AugmentClass Class => AugmentClass.Support;
        public override bool HasAuraEffect => true;
        public override Texture2D Icon => TextureAssets.Item[ItemID.LuckyHorseshoe].Value;
        // Pull-based aura: SupportAuraPlayer.UpdateEquips
    }
}
