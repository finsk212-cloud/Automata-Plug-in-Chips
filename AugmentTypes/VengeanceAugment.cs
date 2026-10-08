using Terraria;
using Terraria.ModLoader;
using Augments.Core;

namespace Augments
{
    public class VengeanceAugment : Augment
    {
        public override string Id => "vengeance";
        public override string DisplayName => "Vengeance";
        public override string Description =>
            $"When struck by a direct enemy attack, retaliate for {AugmentText.BonusDamage("8 damage")}.\n" +
            AugmentText.Note("(Does not trigger from enemy projectiles.)");

        public override AugmentRarity Rarity => AugmentRarity.Rare;
        public override AugmentClass Class => AugmentClass.Universal;

        private const int RetaliationDamage = 8;

        public override void OnHitByNPC(Player player, NPC npc, Player.HurtInfo hurtInfo)
        {
            npc.SimpleStrikeNPC(RetaliationDamage, player.direction);
            if (player.whoAmI == Main.myPlayer)
                AugmentDamageTracker.RecordChipHit("vengeance", RetaliationDamage, false);
        }
    }
}
