using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Augments
{
    public class PlagueBearerAugment : Augment
    {
        public override string Id => "plague_bearer";
        public override string DisplayName => "Plague Bearer";
        public override string Description =>
            $"Killing an enemy with active debuffs transfers them to the nearest enemy, " +
            $"{AugmentText.Duration("refreshed at full duration")}.";

        public override string GetDetailedDescription(Player player) =>
            Description + "\n" +
            AugmentText.Note("(Triggers on kills from any damage class.)");

        public override AugmentRarity Rarity => AugmentRarity.Rare;
        public override AugmentClass Class => AugmentClass.Universal;

        private const float SpreadRange = 200f;

        // No generic API exposes a debuff's "default" full duration - only the
        // remaining time on the dying NPC. For debuffs this mod itself applies,
        // mirror that augment's own duration constant; everything else falls
        // back to whatever time was left on the original target.
        private static int GetSpreadDuration(int buffType, int remainingTime)
        {
            switch (buffType)
            {
                case BuffID.Frostburn:
                    return 240; // matches FrostTouchAugment
                case BuffID.Ichor:
                    return 180; // matches SunderAugment
                default:
                    return remainingTime;
            }
        }

        // Hooking kill credit (see AugmentGlobalNPC.OnKill, keyed off
        // npc.lastInteraction) instead of checking target.life <= 0 inside
        // OnHitNPCWithItem/Proj - that direct-hit check misses kills finished
        // off by a debuff tick (Frostburn, Bleed, etc.) since the DoT tick
        // isn't a hit event. OnKillNPC fires for both cases, same as
        // SwarmTacticsAugment relies on for its own DoT-kill coverage.
        public override void OnKillNPC(Player player, NPC npc)
        {
            SpreadDebuff(npc);
        }

        private static void SpreadDebuff(NPC target)
        {
            int debuffType = 0;
            int debuffTime = 0;
            for (int i = 0; i < target.buffType.Length; i++)
            {
                if (target.buffType[i] > 0 && Main.debuff[target.buffType[i]])
                {
                    debuffType = target.buffType[i];
                    debuffTime = target.buffTime[i];
                    break;
                }
            }

            if (debuffType == 0)
                return;

            NPC nearest = null;
            float nearestDist = SpreadRange;
            foreach (NPC npc in Main.npc)
            {
                if (npc == target || !npc.active || npc.friendly || npc.townNPC)
                    continue;

                float dist = npc.Distance(target.Center);
                if (dist <= nearestDist)
                {
                    nearest = npc;
                    nearestDist = dist;
                }
            }

            if (nearest != null)
            {
                nearest.AddBuff(debuffType, GetSpreadDuration(debuffType, debuffTime));
                if (Main.netMode != NetmodeID.SinglePlayer)
                    NetMessage.SendData(MessageID.NPCBuffs, number: nearest.whoAmI);
            }
        }
    }
}
