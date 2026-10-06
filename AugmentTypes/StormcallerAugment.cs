using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Augments.Core;

namespace Augments
{
    public class StormcallerAugment : Augment
    {
        public override string Id => "stormcaller";
        public override string DisplayName => "Stormcaller";
        public override string Description =>
            $"Melee kills have a {FormatCurrentChance(ProcChance)} to call down a lightning strike on a random nearby enemy, " +
            $"dealing 15% of that enemy's {AugmentText.HP("max HP")} as damage.";

        public override string GetDetailedDescription(Player player) =>
            $"Melee kills have a {FormatFortuneChance(ProcChance, player)} to call down a lightning strike on a random nearby enemy, " +
            $"dealing 15% of that enemy's {AugmentText.HP("max HP")} as damage.";

        public override AugmentRarity Rarity => AugmentRarity.Epic;
        public override AugmentClass Class => AugmentClass.Melee;
        public override string FamilyId => AugmentFamilyRegistry.VoltId;

        private const float ProcChance = 0.2f;
        private const float StrikeDamagePercentOfMaxHP = 0.15f;
        private const float StrikeRange = 250f;

        // Direct hits just tag the target - the actual proc happens on kill
        // credit (OnKillNPC below), since that's the only path that also
        // catches kills finished off by a DoT debuff the melee hit applied
        // earlier (see AugmentGlobalNPC.OnKill, keyed off npc.lastInteraction).
        public override void OnHitNPCWithItem(Player player, Item item, NPC target, NPC.HitInfo hit)
        {
            if (item.CountsAsClass(DamageClass.Melee))
                target.GetGlobalNPC<AugmentStormcallerNPC>().TagHit(player.whoAmI);
        }

        public override void OnHitNPCWithProj(Player player, Projectile proj, NPC target, NPC.HitInfo hit)
        {
            if (proj.CountsAsClass(DamageClass.Melee))
                target.GetGlobalNPC<AugmentStormcallerNPC>().TagHit(player.whoAmI);
        }

        public override void OnKillNPC(Player player, NPC npc)
        {
            var marker = npc.GetGlobalNPC<AugmentStormcallerNPC>();
            if (!marker.IsTaggedBy(player.whoAmI))
                return;

            marker.ClearTag();

            bool success = RollFortuneChance(player, ProcChance);

            if (success)
                StrikeRandomNearbyTarget(npc, player);
        }

        private static void StrikeRandomNearbyTarget(NPC deadNpc, Player player)
        {
            var nearby = new List<NPC>();
            foreach (NPC npc in Main.npc)
            {
                if (npc == deadNpc || !npc.active || npc.friendly || npc.townNPC)
                    continue;

                if (npc.Distance(deadNpc.Center) <= StrikeRange)
                    nearby.Add(npc);
            }

            if (nearby.Count == 0)
                return;

            NPC target = nearby[Main.rand.Next(nearby.Count)];
            int damage = (int)(target.lifeMax * StrikeDamagePercentOfMaxHP);

            var hit = new NPC.HitInfo
            {
                Damage = damage,
                SourceDamage = damage,
                HitDirection = target.direction,
                DamageType = DamageClass.Melee
            };

            target.StrikeNPC(hit);
            if (player.whoAmI == Main.myPlayer)
                AugmentDamageTracker.RecordChipHit("stormcaller", damage, false);
            // StrikeNPC does not sync itself. In multiplayer the damage would
            // only apply on the local client; the server's authoritative NPC
            // never takes it and re-syncs back to alive (looks like a "respawn").
            // Mirror SimpleStrikeNPC and relay the strike to the server/clients.
            if (Main.netMode != NetmodeID.SinglePlayer)
                NetMessage.SendStrikeNPC(target, in hit);

            if (AugmentFamilyRegistry.GetOwnedCount(player.GetModPlayer<AugmentPlayer>(), AugmentFamilyRegistry.VoltId) >= 2)
            {
                target.AddBuff(BuffID.Electrified, 240);
                if (Main.netMode != NetmodeID.SinglePlayer)
                    NetMessage.SendData(MessageID.NPCBuffs, number: target.whoAmI);
            }

            SpawnLightningEffect(target);
        }

        // A short burst of electric dust at the target, plus a few sparks
        // trailing upward to read as a bolt falling from above - no new
        // art assets needed.
        private static void SpawnLightningEffect(NPC target)
        {
            for (int i = 0; i < 12; i++)
                Dust.NewDust(target.position, target.width, target.height, DustID.Electric);

            for (int i = 0; i < 6; i++)
            {
                int dustIndex = Dust.NewDust(target.position, target.width, target.height, DustID.Electric);
                Main.dust[dustIndex].velocity.Y = -Main.rand.NextFloat(2f, 6f);
                Main.dust[dustIndex].noGravity = true;
            }
        }
    }
}
