using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Augments.Core;

namespace Augments
{
    public class AvatarOfTheWallAugment : Augment
    {
        public override string Id => "type_d_dreadnought_protocol";
        public override string DisplayName => "Type-D: Dreadnought Protocol";
        public override string Description =>
            $"Grants {AugmentText.Defense("+20 defense")} and {AugmentText.Defense("15% damage reduction")}, but reduces outgoing damage by " +
            $"{AugmentText.BonusDamage("15%")}. Taking {AugmentText.HP("120 cumulative damage")} detonates a kinetic shockwave dealing " +
            $"{AugmentText.BonusDamage("60 damage")} with violent knockback, and activates a " +
            $"{AugmentText.Duration("1.5s")} Energy Barrier that negates all incoming damage.";

        public override AugmentRarity Rarity => AugmentRarity.Epic;
        public override AugmentClass Class => AugmentClass.Universal;

        public override string KeystoneFamily => "path_of_the_berserker";
        public override bool IsPermanent => true;

        private const int DefenseBonus = 20;
        private const float IncomingDamageReductionPercent = 0.15f;
        private const float OutgoingDamageReductionPercent = 0.15f;
        private const int ThresholdDamage = 120;
        private const int BarrierDurationTicks = 90; // 1.5 seconds at 60 fps
        private const float ShockwaveRadius = 200f;
        private const float ShockwaveKnockback = 10f;
        private const int ShockwaveDamage = 60;

        public override bool FreeDodge(Player player, Player.HurtInfo info)
        {
            var ap = player.GetModPlayer<AugmentPlayer>();
            if (ap.BastionBarrierTicks > 0)
                return true;

            return false;
        }

        public override void UpdateEquips(Player player)
        {
            player.statDefense += DefenseBonus;
        }

        public override void OnUpdate(Player player)
        {
            var ap = player.GetModPlayer<AugmentPlayer>();
            if (ap.BastionBarrierTicks > 0)
            {
                player.immune = true;
                player.immuneTime = Math.Max(player.immuneTime, ap.BastionBarrierTicks);
                ap.BastionBarrierTicks--;

                // Cyan barrier energy field particles
                if (Main.rand.NextBool(2))
                {
                    float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                    Vector2 offset = angle.ToRotationVector2() * (player.width * 0.85f);
                    Dust d = Dust.NewDustPerfect(player.Center + offset, DustID.Electric, offset * 0.04f, 100, Color.Cyan, 0.9f);
                    d.noGravity = true;
                }
            }
        }

        public override void ModifyHurt(Player player, ref Player.HurtModifiers modifiers)
        {
            var ap = player.GetModPlayer<AugmentPlayer>();
            if (ap.BastionBarrierTicks > 0)
            {
                // Energy barrier negates all incoming damage
                modifiers.FinalDamage *= 0f;
                modifiers.SetMaxDamage(0);
            }
            else
            {
                modifiers.FinalDamage *= 1f - IncomingDamageReductionPercent;
            }
        }

        public override void OnHurt(Player player, Player.HurtInfo info)
        {
            var ap = player.GetModPlayer<AugmentPlayer>();
            if (ap.BastionBarrierTicks > 0)
                return;

            ap.BastionStoredDamage += info.Damage;
            if (ap.BastionStoredDamage >= ThresholdDamage)
            {
                ap.BastionStoredDamage = 0;
                ap.BastionBarrierTicks = BarrierDurationTicks;
                player.immune = true;
                player.immuneTime = Math.Max(player.immuneTime, BarrierDurationTicks);
                TriggerKineticShockwave(player);
            }
        }

        public static void TriggerKineticShockwave(Player player)
        {
            SoundEngine.PlaySound(SoundID.Item93 with { Volume = 0.9f, Pitch = 0.1f }, player.Center);

            // Expanding shockwave dust ring
            for (int i = 0; i < 28; i++)
            {
                float angle = i * (MathHelper.TwoPi / 28f);
                Vector2 vel = angle.ToRotationVector2() * 6.5f;
                Dust d = Dust.NewDustPerfect(player.Center, DustID.Electric, vel, 100, Color.Cyan, 1.2f);
                d.noGravity = true;
            }

            // Radial knockback and 60 kinetic damage to nearby enemies
            foreach (NPC npc in Main.npc)
            {
                if (!npc.active || npc.friendly || npc.townNPC)
                    continue;

                if (npc.Distance(player.Center) > ShockwaveRadius)
                    continue;

                Vector2 direction = npc.Center - player.Center;
                if (direction == Vector2.Zero)
                    direction = -Vector2.UnitY;

                direction.Normalize();
                npc.SimpleStrikeNPC(ShockwaveDamage, direction.X >= 0f ? 1 : -1, false, ShockwaveKnockback, DamageClass.Generic, false);
                if (player.whoAmI == Main.myPlayer)
                    AugmentDamageTracker.RecordChipHit("type_d_dreadnought_protocol", ShockwaveDamage, false);
            }
        }

        public override void ModifyHitNPCWithItem(Player player, Item item, NPC target, ref NPC.HitModifiers modifiers)
        {
            modifiers.FlatBonusDamage += -(int)(item.damage * OutgoingDamageReductionPercent);
        }

        public override void ModifyHitNPCWithProj(Player player, Projectile proj, NPC target, ref NPC.HitModifiers modifiers)
        {
            modifiers.FlatBonusDamage += -(int)(proj.damage * OutgoingDamageReductionPercent);
        }
    }
}
