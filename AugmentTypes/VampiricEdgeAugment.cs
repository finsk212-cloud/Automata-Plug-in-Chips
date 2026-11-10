using Terraria;
using Terraria.ModLoader;

namespace Augments
{
    public class VampiricEdgeAugment : Augment
    {
        public override string Id => "vampiric_edge";
        public override bool RunsKillHookOnKillerClient => true;
        public override string DisplayName => "Vampiric Edge";
        public override string Description =>
            $"Melee kills {AugmentText.Healing("restore 3 HP")}.\n" +
            AugmentText.Note("(Requires your direct hit to score the kill.)");

        public override AugmentRarity Rarity => AugmentRarity.Common;
        public override AugmentClass Class => AugmentClass.Melee;

        private const int HealAmount = 3;

        public override void OnHitNPCWithItem(Player player, Item item, NPC target, NPC.HitInfo hit)
        {
            if (item.CountsAsClass(DamageClass.Melee))
                target.GetGlobalNPC<AugmentVampiricEdgeNPC>().TagMeleeHit(player.whoAmI);
        }

        public override void OnHitNPCWithProj(Player player, Projectile proj, NPC target, NPC.HitInfo hit)
        {
            if (proj.CountsAsClass(DamageClass.Melee))
                target.GetGlobalNPC<AugmentVampiricEdgeNPC>().TagMeleeHit(player.whoAmI);
        }

        public override void OnKillNPC(Player player, NPC npc)
        {
            var marker = npc.GetGlobalNPC<AugmentVampiricEdgeNPC>();
            if (!marker.IsTaggedBy(player.whoAmI))
                return;

            marker.ClearTag();
            player.statLife = System.Math.Min(player.statLife + HealAmount, player.statLifeMax2);
            player.HealEffect(HealAmount);
        }
    }
}
