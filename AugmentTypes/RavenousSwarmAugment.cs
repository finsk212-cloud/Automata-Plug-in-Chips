using Terraria;
using Terraria.ModLoader;

namespace Augments
{
    public class RavenousSwarmAugment : Augment
    {
        public override string Id => "ravenous_swarm";
        public override string DisplayName => "Ravenous Swarm";
        public override string Description =>
            $"Minion kills have a {FormatCurrentChance(ProcChance)} to grant {AugmentText.BonusDamage("+1 max minion slot")}.\n" +
            AugmentText.Note("(Capped at +3 total slots per session.)");

        public override string GetDetailedDescription(Player player) =>
            $"Minion kills have a {FormatFortuneChance(ProcChance, player)} to grant {AugmentText.BonusDamage("+1 max minion slot")}.\n" +
            AugmentText.Note("(Capped at +3 total slots per session.)");

        public override AugmentRarity Rarity => AugmentRarity.Epic;
        public override AugmentClass Class => AugmentClass.Summon;

        private const float ProcChance = 0.05f;
        private const int MaxSlotsGranted = 3;

        public override int? StatusValue => LocalPlayerState.RavenousSwarmSlotsGranted > 0 ? LocalPlayerState.RavenousSwarmSlotsGranted : (int?)null;
        public override Microsoft.Xna.Framework.Color StatusValueColor => AugmentTextColors.Trigger;

        // Direct hits just tag the target - the actual roll happens on kill
        // credit (OnKillNPC below), since that's the only path that also
        // catches kills finished off by a DoT debuff the minion applied
        // earlier. Same shared tagging system SwarmTacticsAugment uses.
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

            var ap = player.GetModPlayer<AugmentPlayer>();
            if (ap.RavenousSwarmSlotsGranted >= MaxSlotsGranted)
                return;

            if (RollFortuneChance(player, ProcChance))
                ap.RavenousSwarmSlotsGranted++;
        }

        // player.maxMinions gets reset to its base value of 1 in
        // Player.ResetEffects() every single frame, before equips/buffs
        // re-add their bonuses on top - a one-time player.maxMinions++ inside
        // OnKillNPC would just get wiped out on the very next frame. Re-apply
        // the running slotsGranted total here every tick instead, same as
        // every other persistent stat bonus in this mod (defense, etc).
        public override void UpdateEquips(Player player)
        {
            player.maxMinions += player.GetModPlayer<AugmentPlayer>().RavenousSwarmSlotsGranted;
        }

        // Intentionally session-only, not saved/loaded - RavenousSwarmSlotsGranted
        // (and therefore the maxMinions bonus it reapplies) naturally resets to 0
        // on the next mod/game load along with every other in-memory augment
        // field. That's the confirmed design, not a gap to patch with
        // SaveCustomData.
    }
}
