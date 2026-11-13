using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Augments
{
    public class SunderAugment : Augment
    {
        public override string Id => "sunder";
        public override string DisplayName => "Sunder";
        public override string Description =>
            $"Melee {AugmentText.Crit("crits")} apply {AugmentText.Ichor("Ichor")} for {AugmentText.Duration("3 seconds")}, reducing enemy defense briefly.";

        public override AugmentRarity Rarity => AugmentRarity.Common;
        public override AugmentClass Class => AugmentClass.Melee;

        private const int IchorDurationTicks = 180;

        public override void OnHitNPCWithItem(Player player, Item item, NPC target, NPC.HitInfo hit)
        {
            if (hit.Crit && item.CountsAsClass(DamageClass.Melee))
                ApplyIchor(target);
        }

        public override void OnHitNPCWithProj(Player player, Projectile proj, NPC target, NPC.HitInfo hit)
        {
            if (hit.Crit && proj.CountsAsClass(DamageClass.Melee))
                ApplyIchor(target);
        }

        private static void ApplyIchor(NPC target)
        {
            target.AddBuff(BuffID.Ichor, IchorDurationTicks);
            if (Main.netMode != NetmodeID.SinglePlayer)
                NetMessage.SendData(MessageID.NPCBuffs, number: target.whoAmI);
        }
    }
}
