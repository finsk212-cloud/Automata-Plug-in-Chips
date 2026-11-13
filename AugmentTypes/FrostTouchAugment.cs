using Augments.Core;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Augments
{
    public class FrostTouchAugment : Augment
    {
        public override string Id => "frost_touch";
        public override string DisplayName => "Frost Touch";
        public override string Description =>
            $"Magic {AugmentText.Crit("crits")} apply {AugmentText.Frostburn("Frostburn")} for {AugmentText.Duration("4 seconds")}.";

        public override AugmentRarity Rarity => AugmentRarity.Common;
        public override AugmentClass Class => AugmentClass.Magic;
        public override string FamilyId => AugmentFamilyRegistry.CryoId;

        private const int FrostburnDurationTicks = 240;

        public override void OnHitNPCWithItem(Player player, Item item, NPC target, NPC.HitInfo hit)
        {
            if (hit.Crit && item.DamageType == DamageClass.Magic)
                ApplyFrostburn(target);
        }

        public override void OnHitNPCWithProj(Player player, Projectile proj, NPC target, NPC.HitInfo hit)
        {
            if (hit.Crit && proj.DamageType == DamageClass.Magic)
                ApplyFrostburn(target);
        }

        private static void ApplyFrostburn(NPC target)
        {
            target.AddBuff(BuffID.Frostburn, FrostburnDurationTicks);
            if (Main.netMode != NetmodeID.SinglePlayer)
                NetMessage.SendData(MessageID.NPCBuffs, number: target.whoAmI);
        }
    }
}
