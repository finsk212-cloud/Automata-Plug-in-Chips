using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Augments.Core;

namespace Augments
{
    public class ChainLightningAugment : Augment
    {
        public override string Id => "chain_lightning";
        public override string DisplayName => "Chain Lightning";
        public override string Description =>
            $"Melee hits deal {AugmentText.SpecialDamage("15 damage")} {AugmentText.OnHit("on-hit")} and chain electricity to up to 2 nearby enemies, dealing 50% of your {AugmentText.OnHit("on-hit")} damage.";

        public override AugmentRarity Rarity => AugmentRarity.Rare;
        public override AugmentClass Class => AugmentClass.Melee;
        public override string FamilyId => AugmentFamilyRegistry.VoltId;

        public const int BaseOnHitDamage = 15;

        public override (string label, int baseValue)[] GetScalingEntries()
        {
            return new[] { ("On-hit damage", BaseOnHitDamage) };
        }
        private const float ChainRange = 380f;
        private const int MaxChainTargets = 2;
        private const float ChainDamageFraction = 0.5f;

        public override void OnHitNPCWithItem(Player player, Item item, NPC target, NPC.HitInfo hit, AugmentHitSource source, float effectiveness)
        {
            if (source == AugmentHitSource.AugmentProc)
                return;

            if (item.CountsAsClass(DamageClass.Melee) || item.DamageType == DamageClass.Melee)
                ApplyOnHitDamage(player, target);
        }

        public override void OnHitNPCWithProj(Player player, Projectile proj, NPC target, NPC.HitInfo hit, AugmentHitSource source, float effectiveness)
        {
            if (source == AugmentHitSource.AugmentProc)
                return;

            if (proj.CountsAsClass(DamageClass.Melee) || proj.DamageType == DamageClass.Melee)
                ApplyOnHitDamage(player, target);
        }

        public override void OnHitNPCWithItem(Player player, Item item, NPC target, NPC.HitInfo hit)
        {
            if (item.CountsAsClass(DamageClass.Melee) || item.DamageType == DamageClass.Melee)
                ApplyOnHitDamage(player, target);
        }

        public override void OnHitNPCWithProj(Player player, Projectile proj, NPC target, NPC.HitInfo hit)
        {
            if (proj.CountsAsClass(DamageClass.Melee) || proj.DamageType == DamageClass.Melee)
                ApplyOnHitDamage(player, target);
        }

        private static void ApplyOnHitDamage(Player player, NPC target)
        {
            var ap = player.GetModPlayer<AugmentPlayer>();
            int damage = AugmentScaling.Scale(BaseOnHitDamage);
            ap.RecordOnHitDamage(damage);
            Strike(player, target, damage);
            if (player.whoAmI == Main.myPlayer)
            {
                AugmentDamageTracker.RecordChipHit("chain_lightning", damage, false);
            }
        }

        private static void Strike(Player player, NPC target, int damage)
        {
            var hit = new NPC.HitInfo
            {
                Damage = damage,
                SourceDamage = damage,
                HitDirection = player.direction,
                DamageType = DamageClass.Melee,
                HideCombatText = true
            };

            target.StrikeNPC(hit);
            if (Main.netMode != NetmodeID.SinglePlayer)
                NetMessage.SendStrikeNPC(target, in hit);

            CombatText.NewText(target.Hitbox, AugmentTextColors.SpecialDamage, damage);
        }

        public static void ChainToNearbyTargets(Player player, NPC target, NPC.HitInfo hit, int onHitDamage)
        {
            var ap = player.GetModPlayer<AugmentPlayer>();

            bool voltActive = AugmentFamilyRegistry.GetOwnedCount(ap, AugmentFamilyRegistry.VoltId) >= 2;
            if (voltActive)
            {
                target.AddBuff(BuffID.Electrified, 180);
                if (Main.netMode != NetmodeID.SinglePlayer)
                    NetMessage.SendData(MessageID.NPCBuffs, number: target.whoAmI);
            }

            // If the player has no on-hit damage from plugins, Chain Lightning deals nothing extra.
            if (onHitDamage <= 0)
                return;

            if (ap.ChainLightningCooldown > 0)
                return;

            ap.ChainLightningCooldown = 6;

            var nearby = new List<NPC>();
            foreach (NPC npc in Main.npc)
            {
                if (npc == target || !npc.active || npc.friendly || npc.townNPC || npc.dontTakeDamage)
                    continue;

                if (npc.Distance(target.Center) <= ChainRange)
                    nearby.Add(npc);
            }

            if (nearby.Count == 0)
                return;

            nearby.Sort((a, b) => a.Distance(target.Center).CompareTo(b.Distance(target.Center)));

            SoundEngine.PlaySound(SoundID.Item93 with { Volume = 0.55f, Pitch = 0.2f }, target.Center);

            int chainDamage = Math.Max(1, (int)Math.Round(onHitDamage * ChainDamageFraction));
            for (int i = 0; i < nearby.Count && i < MaxChainTargets; i++)
            {
                NPC chainTarget = nearby[i];
                int direction = chainTarget.Center.X >= target.Center.X ? 1 : -1;

                var chainHit = new NPC.HitInfo
                {
                    Damage = chainDamage,
                    SourceDamage = chainDamage,
                    HitDirection = direction,
                    DamageType = DamageClass.Melee,
                    Crit = hit.Crit,
                    Knockback = 1f,
                    HideCombatText = true
                };

                chainTarget.StrikeNPC(chainHit);
                if (player.whoAmI == Main.myPlayer)
                {
                    AugmentDamageTracker.RecordChipHit("chain_lightning", chainDamage, hit.Crit);
                }
                if (Main.netMode != NetmodeID.SinglePlayer)
                    NetMessage.SendStrikeNPC(chainTarget, in chainHit);

                CombatText.NewText(chainTarget.Hitbox, AugmentTextColors.SpecialDamage, chainDamage, hit.Crit);

                if (voltActive)
                {
                    chainTarget.AddBuff(BuffID.Electrified, 180);
                    if (Main.netMode != NetmodeID.SinglePlayer)
                        NetMessage.SendData(MessageID.NPCBuffs, number: chainTarget.whoAmI);
                }

                // Electric spark line connecting the targets
                Vector2 diff = chainTarget.Center - target.Center;
                float dist = diff.Length();
                int dustCount = Math.Max(3, (int)(dist / 14f));
                for (int d = 0; d <= dustCount; d++)
                {
                    Vector2 p = Vector2.Lerp(target.Center, chainTarget.Center, d / (float)dustCount);
                    Dust electricDust = Dust.NewDustPerfect(
                        p + Main.rand.NextVector2Circular(3.5f, 3.5f),
                        DustID.Electric,
                        Vector2.Zero,
                        80,
                        default,
                        Main.rand.NextFloat(0.9f, 1.25f)
                    );
                    electricDust.noGravity = true;
                }
            }
        }
    }
}
