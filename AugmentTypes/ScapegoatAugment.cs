using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent;
using Terraria.ID;

namespace Augments
{
    public class ScapegoatAugment : Augment
    {
        public override string Id => "scapegoat";
        public override string DisplayName => "Scapegoat";
        public override string Description =>
            $"Every {AugmentText.Trigger("debuff")} a nearby teammate gets is removed from them and {AugmentText.Trigger("applied to you")} instead.";

        public override string GetDetailedDescription(Terraria.Player player) =>
            Description + "\n" +
            AugmentText.Note("Potion Sickness and chip cooldowns are not moved. Only works in multiplayer.");
        public override AugmentRarity Rarity => AugmentRarity.Rare;
        public override AugmentClass Class => AugmentClass.Support;
        public override bool HasAuraEffect => true;
        public override Texture2D Icon => TextureAssets.Item[ItemID.GuideVoodooDoll].Value;
        // Redirect logic: SupportAuraPlayer.PostUpdate + AugmentNet (ScapegoatTransfer / ScapegoatApply)
    }
}
