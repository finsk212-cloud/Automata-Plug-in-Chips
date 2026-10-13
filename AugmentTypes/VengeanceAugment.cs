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
            $"When an enemy damages you, hit it back for {AugmentText.BonusDamage("8 damage")}.\n" +
            AugmentText.Note("(Enemy projectiles don't trigger this.)");

        public override AugmentRarity Rarity => AugmentRarity.Rare;
        public override AugmentClass Class => AugmentClass.Universal;

        private const int RetaliationDamage = 8;

        public override (string label, int baseValue)[] GetScalingEntries()
        {
            return new[] { ("Retaliation damage", RetaliationDamage) };
        }


        public override void OnHitByNPC(Player player, NPC npc, Player.HurtInfo hurtInfo)
        {
            npc.SimpleStrikeNPC(AugmentScaling.Scale(RetaliationDamage), player.direction);
            if (player.whoAmI == Main.myPlayer)
                AugmentDamageTracker.RecordChipHit("vengeance", AugmentScaling.Scale(RetaliationDamage), false);
        }
    }
}
