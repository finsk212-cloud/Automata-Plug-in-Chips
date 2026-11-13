using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace Augments
{
    public class LifelineAugment : Augment
    {
        public override string Id => "lifeline";
        public override string DisplayName => "Lifeline";
        public override string Description =>
            $"When a nearby teammate would die, they survive on {AugmentText.HP("1 HP")} instead.";
        public override AugmentRarity Rarity => AugmentRarity.Legendary;
        public override AugmentClass Class => AugmentClass.Support;
        public override bool HasAuraEffect => true;
        public override string CooldownText => "90 seconds cooldown per player";
        public override int CooldownRemaining => LocalPlayerState.LifelineCooldown;

        public override Texture2D Icon => TextureAssets.Item[ItemID.CrossNecklace].Value;
        // No hooks — pull check runs in AugmentPlayer.PreKill on the dying player's client.
    }
}
