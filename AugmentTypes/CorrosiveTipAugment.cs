using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace Augments
{
    public class CorrosiveTipAugment : Augment
    {
        // Read by MediGunSiphonBeamProjectile.DealSiphonDamage on the beam owner's client.
        public const float SiphonDamageMultiplier = 1.5f;

        public override string Id => "corrosive_tip";
        public override string DisplayName => "Corrosive Tip";
        public override string Description =>
            $"The Medi Gun siphon beam deals {AugmentText.BonusDamage("+50% damage")}.";
        public override AugmentRarity Rarity => AugmentRarity.Common;
        public override AugmentClass Class => AugmentClass.Support;
        public override Texture2D Icon => TextureAssets.Item[ItemID.VialofVenom].Value;
    }
}
