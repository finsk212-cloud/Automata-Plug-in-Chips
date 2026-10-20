using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace Augments
{
    public class DeadWeightAugment : Augment
    {
        public override string Id => "dead_weight";
        public override string DisplayName => "Dead Weight";
        public override string Description =>
            $"The slower your melee weapon, the harder it hits, up to {AugmentText.BonusDamage("+40% damage")}. " +
            $"Hits with very slow weapons also slam the ground, hurling nearby enemies away. {AugmentText.Cooldown("0.6s cooldown")}.";

        public override string GetDetailedDescription(Player player) =>
            Description + "\n" +
            AugmentText.Note("(Bonus starts at 30 use time and maxes at 60. Slam needs 45+ use time, deals no damage, ignores knockback resistance and never moves bosses.)");

        public override AugmentRarity Rarity => AugmentRarity.Epic;
        public override AugmentClass Class => AugmentClass.Melee;
        public override int CooldownRemaining => LocalPlayerState.DeadWeightCooldown;

        private const float BonusStartTicks = 30f;
        private const float BonusMaxTicks = 60f;
        private const float MaxBonus = 0.4f;
        private const int SlamMinTicks = 45;
        private const float SlamRadius = 250f;
        private const float MinSlamSpeed = 7f;
        private const float MaxSlamSpeed = 10f;
        private const float MaxKnockbackArgument = 60f;
        private const int CooldownTicks = 36;

        public override void OnUpdate(Player player)
        {
            var ap = player.GetModPlayer<AugmentPlayer>();
            if (ap.DeadWeightCooldown > 0)
                ap.DeadWeightCooldown--;
        }

        public override void ModifyHitNPCWithItem(Player player, Item item, NPC target, ref NPC.HitModifiers modifiers)
        {
            if (item.CountsAsClass(DamageClass.Melee))
                modifiers.FinalDamage *= 1f + DamageBonus(item.useAnimation);
        }

        public override void ModifyHitNPCWithProj(Player player, Projectile proj, NPC target, ref NPC.HitModifiers modifiers)
        {
            // Thrust and flail style weapons hit through a projectile, so the weight comes from the held weapon.
            if (TryGetHeldMeleeWeight(player, proj, out int weight))
                modifiers.FinalDamage *= 1f + DamageBonus(weight);
        }

        public override void OnHitNPCWithItem(Player player, Item item, NPC target, NPC.HitInfo hit)
        {
            if (item.CountsAsClass(DamageClass.Melee))
                TrySlam(player, target, item.useAnimation);
        }

        public override void OnHitNPCWithProj(Player player, Projectile proj, NPC target, NPC.HitInfo hit)
        {
            if (TryGetHeldMeleeWeight(player, proj, out int weight))
                TrySlam(player, target, weight);
        }

        private static bool TryGetHeldMeleeWeight(Player player, Projectile proj, out int weight)
        {
            weight = 0;
            if (!proj.CountsAsClass(DamageClass.Melee))
                return false;

            Item held = player.HeldItem;
            if (held == null || held.IsAir || held.damage <= 0 || !held.CountsAsClass(DamageClass.Melee))
                return false;

            weight = held.useAnimation;
            return true;
        }

        private static float DamageBonus(int useTicks)
        {
            float t = MathHelper.Clamp((useTicks - BonusStartTicks) / (BonusMaxTicks - BonusStartTicks), 0f, 1f);
            return MaxBonus * t;
        }

        private static void TrySlam(Player player, NPC target, int useTicks)
        {
            if (player.whoAmI != Main.myPlayer || useTicks < SlamMinTicks)
                return;

            var ap = player.GetModPlayer<AugmentPlayer>();
            if (ap.DeadWeightCooldown > 0)
                return;

            ap.DeadWeightCooldown = CooldownTicks;

            float weightT = MathHelper.Clamp((useTicks - SlamMinTicks) / (BonusMaxTicks - SlamMinTicks), 0f, 1f);
            float speed = MathHelper.Lerp(MinSlamSpeed, MaxSlamSpeed, weightT);

            SpawnSlamEffects(target.Center, weightT);

            foreach (NPC npc in Main.npc)
            {
                if (!npc.active || npc.friendly || npc.townNPC || npc.boss || npc.dontTakeDamage || npc.immortal)
                    continue;

                // Bosses and enemies with no knockback at all stay put.
                if (npc.knockBackResist <= 0f)
                    continue;

                if (npc.Distance(target.Center) > SlamRadius)
                    continue;

                float dx = npc.Center.X - player.Center.X;
                int hitDirection = Math.Abs(dx) < 1f ? player.direction : (dx > 0f ? 1 : -1);

                // Knockback is multiplied by the enemy's resistance, so dividing it back out gives every enemy the same launch speed.
                float knockback = Math.Min(MaxKnockbackArgument, speed / npc.knockBackResist);

                // SimpleStrikeNPC handles netUpdate internally and is multiplayer-safe.
                npc.SimpleStrikeNPC(0, hitDirection, false, knockback, DamageClass.Melee, true);
            }
        }

        private static void SpawnSlamEffects(Vector2 center, float weightT)
        {
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.9f, Pitch = -0.45f }, center);
            SoundEngine.PlaySound(SoundID.Item70 with { Volume = 0.5f, Pitch = -0.3f }, center);

            const int RingDust = 48;
            float ringSpeed = 9f + 4f * weightT;
            for (int i = 0; i < RingDust; i++)
            {
                Vector2 direction = (MathHelper.TwoPi * i / RingDust).ToRotationVector2();
                Dust dust = Dust.NewDustPerfect(
                    center + direction * 10f,
                    i % 3 == 0 ? DustID.Smoke : DustID.Torch,
                    direction * ringSpeed,
                    90,
                    default,
                    Main.rand.NextFloat(1.4f, 1.9f)
                );
                dust.noGravity = true;
            }

            for (int i = 0; i < 14; i++)
            {
                Vector2 velocity = new Vector2(Main.rand.NextFloat(-6f, 6f), Main.rand.NextFloat(-5f, -0.5f));
                Dust dust = Dust.NewDustPerfect(center, DustID.Smoke, velocity, 120, default, Main.rand.NextFloat(1.2f, 1.8f));
                dust.noGravity = true;
            }
        }
    }
}
