using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent;
using Terraria.ID;

namespace Augments
{
    public class EmergencyAidAugment : Augment
    {
        public const float HealthThreshold = 0.4f;
        // lifeRegen counts half-HP per second: 6 = 3 HP/s.
        public const int LifeRegenBonus = 6;

        public override string Id => "emergency_aid";
        public override string DisplayName => "Emergency Aid";
        public override string Description =>
            $"You and nearby teammates below {AugmentText.HP("40% HP")} regenerate {AugmentText.Healing("3 HP per second")}.";
        public override AugmentRarity Rarity => AugmentRarity.Rare;
        public override AugmentClass Class => AugmentClass.Support;
        public override bool HasAuraEffect => true;
        public override Texture2D Icon => TextureAssets.Item[ItemID.LifeCrystal].Value;
        // Pull-based aura: SupportAuraPlayer.UpdateEquips
    }
}
