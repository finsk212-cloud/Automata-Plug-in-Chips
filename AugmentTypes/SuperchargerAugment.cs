using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace Augments
{
    public class SuperchargerAugment : Augment
    {
        public override string Id => "supercharger";
        public override string DisplayName => "Supercharger";
        public override string Description =>
            $"Increases Medi Gun Overclock charge rate by {AugmentText.AttackSpeed("+15%")}, and refunds {AugmentText.Healing("15% charge")} when Overclock is activated.";
        public override AugmentRarity Rarity => AugmentRarity.Common;
        public override AugmentClass Class => AugmentClass.Support;
        public override Texture2D Icon => TextureAssets.Item[ItemID.FastClock].Value;
    }
}
