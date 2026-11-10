using Terraria;
using Terraria.ModLoader;

namespace Augments
{
    public class SwarmTacticsAugment : Augment
    {
        public override string Id => "swarm_tactics";
        public override bool RunsKillHookOnKillerClient => true;
        public override string DisplayName => "Swarm Tactics";
        public override string Description =>
            $"Minion kills {AugmentText.Healing("restore 5 HP")}.\n" +
            AugmentText.Note("(Requires your minion to score the kill.)");

        public override AugmentRarity Rarity => AugmentRarity.Common;
        public override AugmentClass Class => AugmentClass.Summon;

        private const int HealAmount = 5;

        // Direct hits just tag the target - the actual heal happens on kill
        // credit (OnKillNPC below), since that's the only path that also
        // catches kills finished off by a DoT debuff the minion applied earlier.
        public override void OnHitNPCWithItem(Player player, Item item, NPC target, NPC.HitInfo hit)
        {
            if (item.CountsAsClass(DamageClass.Summon))
                target.GetGlobalNPC<AugmentSwarmTacticsNPC>().TagSummonHit(player.whoAmI);
        }

        public override void OnHitNPCWithProj(Player player, Projectile proj, NPC target, NPC.HitInfo hit)
        {
            if (proj.CountsAsClass(DamageClass.Summon))
                target.GetGlobalNPC<AugmentSwarmTacticsNPC>().TagSummonHit(player.whoAmI);
        }

        // Fires on kill credit (see AugmentGlobalNPC.OnKill, keyed off
        // npc.lastInteraction) regardless of whether the killing blow was a
        // direct hit or a later DoT tick - covers both in one place.
        public override void OnKillNPC(Player player, NPC npc)
        {
            var marker = npc.GetGlobalNPC<AugmentSwarmTacticsNPC>();
            if (!marker.IsTaggedBy(player.whoAmI))
                return;

            marker.ClearTag();
            player.statLife = System.Math.Min(player.statLife + HealAmount, player.statLifeMax2);
            player.HealEffect(HealAmount);
        }
    }
}
