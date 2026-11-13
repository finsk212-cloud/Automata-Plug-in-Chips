using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace Augments
{
    public class AvatarOfBalanceAugment : Augment
    {
        public override string Id => "type_s_synchronizer_protocol";
        public override string DisplayName => "Type-S: Synchronizer Protocol";
        public override string Description =>
            $"Every {AugmentText.Duration("10 seconds")}, cycles polarity between {AugmentText.Crit("Offensive Overclock")} " +
            $"({AugmentText.BonusDamage("+15% damage")}, {AugmentText.Crit("+10% crit")}, {AugmentText.Defense("+10 armor penetration")}, " +
            $"but {AugmentText.Healing("halts natural life regeneration")}) and {AugmentText.Healing("Restorative Nano-Repair")} " +
            $"({AugmentText.Defense("+15 defense")}, {AugmentText.Healing("+12 life regen")}, {AugmentText.MovementSpeed("+15% movement speed")}, " +
            $"but {AugmentText.BonusDamage("-15% damage")}).";

        public override AugmentRarity Rarity => AugmentRarity.Epic;
        public override AugmentClass Class => AugmentClass.Universal;

        public override string KeystoneFamily => "path_of_the_berserker";
        public override bool IsPermanent => true;

        private const float CombatDamageBonus = 0.15f;
        private const float CombatCritBonus = 10f;
        private const float CombatArmorPenBonus = 10f;

        private const int RepairDefenseBonus = 15;
        private const int RepairLifeRegenBonus = 12;
        private const float RepairMoveSpeedBonus = 0.15f;
        private const float RepairDamagePenalty = 0.15f;

        private const int CycleDurationTicks = 1200; // 20s total cycle (10s combat, 10s repair)
        private const int PhaseSwitchTick = 600;     // 10s mark
        private const int WarningWindowTicks = 120;  // 2s warning window before polarity shift

        public override void UpdateEquips(Player player)
        {
            var ap = player.GetModPlayer<AugmentPlayer>();
            if (ap.SynchronizerTimer < PhaseSwitchTick)
            {
                // Offensive Overclock: +10 armor penetration and halt natural life regen
                player.GetArmorPenetration(DamageClass.Generic) += CombatArmorPenBonus;
                player.bleed = true;
            }
            else
            {
                // Restorative Nano-Repair: +15 defense, +15% movement speed
                player.statDefense += RepairDefenseBonus;
                player.moveSpeed += RepairMoveSpeedBonus;
            }
        }

        public override void UpdateLifeRegen(Player player)
        {
            var ap = player.GetModPlayer<AugmentPlayer>();
            if (ap.SynchronizerTimer < PhaseSwitchTick)
            {
                // Offensive Overclock halts natural life regeneration
                player.bleed = true;
                if (player.lifeRegen > 0)
                    player.lifeRegen = 0;
                player.lifeRegenTime = 0;
                if (player.lifeRegenCount > 0)
                    player.lifeRegenCount = 0;
            }
            else
            {
                // Restorative Nano-Repair: +12 life regen
                player.lifeRegen += RepairLifeRegenBonus;
            }
        }

        public override void UpdateBadLifeRegen(Player player)
        {
            var ap = player.GetModPlayer<AugmentPlayer>();
            if (ap.SynchronizerTimer < PhaseSwitchTick)
            {
                // Enforce 0 regeneration across all buff and accessory passes
                player.bleed = true;
                if (player.lifeRegen > 0)
                    player.lifeRegen = 0;
                player.lifeRegenTime = 0;
                if (player.lifeRegenCount > 0)
                    player.lifeRegenCount = 0;
            }
        }

        public override void ModifyWeaponCrit(Player player, Item item, ref float crit)
        {
            var ap = player.GetModPlayer<AugmentPlayer>();
            if (ap.SynchronizerTimer < PhaseSwitchTick)
            {
                // Offensive Overclock: +10% crit
                crit += CombatCritBonus;
            }
        }

        public override void OnUpdate(Player player)
        {
            var ap = player.GetModPlayer<AugmentPlayer>();
            int oldTimer = ap.SynchronizerTimer;
            ap.SynchronizerTimer = (ap.SynchronizerTimer + 1) % CycleDurationTicks;

            bool isOverclock = ap.SynchronizerTimer < PhaseSwitchTick;
            int ticksRemaining = isOverclock ? (PhaseSwitchTick - ap.SynchronizerTimer) : (CycleDurationTicks - ap.SynchronizerTimer);
            bool isWarning = ticksRemaining <= WarningWindowTicks;

            // 1. Buff Bar Integration with live countdown
            int overclockBuff = ModContent.BuffType<SynchronizerOverclockBuff>();
            int repairBuff = ModContent.BuffType<SynchronizerRepairBuff>();

            if (isOverclock)
            {
                if (player.HasBuff(repairBuff))
                    player.ClearBuff(repairBuff);

                int buffIdx = player.FindBuffIndex(overclockBuff);
                if (buffIdx == -1)
                    player.AddBuff(overclockBuff, ticksRemaining, quiet: true);
                else
                    player.buffTime[buffIdx] = ticksRemaining;
            }
            else
            {
                if (player.HasBuff(overclockBuff))
                    player.ClearBuff(overclockBuff);

                int buffIdx = player.FindBuffIndex(repairBuff);
                if (buffIdx == -1)
                    player.AddBuff(repairBuff, ticksRemaining, quiet: true);
                else
                    player.buffTime[buffIdx] = ticksRemaining;
            }

            // 2. Pre-Shift Audio Warnings at 2.0s and 1.0s remaining
            if (player.whoAmI == Main.myPlayer && (ticksRemaining == 120 || ticksRemaining == 60))
            {
                SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.35f, Pitch = 0.45f }, player.Center);
            }

            // 3. Phase Shift Transition Effects
            if (oldTimer < PhaseSwitchTick && ap.SynchronizerTimer >= PhaseSwitchTick)
            {
                // Transition to Restorative Nano-Repair (harmonic chime)
                SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.65f, Pitch = 0.35f }, player.Center);
                for (int i = 0; i < 16; i++)
                {
                    Vector2 vel = Main.rand.NextVector2Circular(3.0f, 3.0f);
                    Dust d = Dust.NewDustPerfect(player.Center, DustID.GreenFairy, vel, 100, default, 1.3f);
                    d.noGravity = true;
                }
            }
            else if (oldTimer >= PhaseSwitchTick && ap.SynchronizerTimer < PhaseSwitchTick)
            {
                // Transition to Offensive Overclock (laser charge)
                SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.60f, Pitch = 0.15f }, player.Center);
                for (int i = 0; i < 16; i++)
                {
                    Vector2 vel = Main.rand.NextVector2Circular(3.0f, 3.0f);
                    Dust d = Dust.NewDustPerfect(player.Center, DustID.OrangeTorch, vel, 100, default, 1.3f);
                    d.noGravity = true;
                }
            }

            // 4. Twin Orbiting Polarity Resonators & Dynamic Lighting
            float orbitSpeed = isWarning ? 0.12f : 0.05f;
            float baseAngle = (float)Main.timeForVisualEffects * orbitSpeed;
            float radiusX = 34f;
            float radiusY = 14f;

            for (int i = 0; i < 2; i++)
            {
                float angle = baseAngle + (i * MathHelper.Pi);
                Vector2 offset = new Vector2((float)Math.Cos(angle) * radiusX, (float)Math.Sin(angle) * radiusY);
                Vector2 motePos = player.Center + offset;

                if (isOverclock)
                {
                    // Amber / Orange Overclock Mote
                    Lighting.AddLight(motePos, 0.65f, 0.30f, 0.06f);

                    if (Main.rand.NextBool(isWarning ? 1 : 2))
                    {
                        Dust d = Dust.NewDustPerfect(
                            motePos,
                            isWarning && Main.rand.NextBool(2) ? DustID.Torch : DustID.OrangeTorch,
                            -player.velocity * 0.15f + Main.rand.NextVector2Circular(0.3f, 0.3f),
                            100,
                            default,
                            isWarning ? 1.25f : 1.0f
                        );
                        d.noGravity = true;
                    }
                }
                else
                {
                    // Emerald / Cybernetic Repair Mote
                    Lighting.AddLight(motePos, 0.08f, 0.60f, 0.28f);

                    if (Main.rand.NextBool(isWarning ? 1 : 2))
                    {
                        Dust d = Dust.NewDustPerfect(
                            motePos,
                            isWarning && Main.rand.NextBool(2) ? DustID.TerraBlade : DustID.GreenFairy,
                            new Vector2(0f, -0.3f) + Main.rand.NextVector2Circular(0.2f, 0.2f),
                            100,
                            default,
                            isWarning ? 1.25f : 1.0f
                        );
                        d.noGravity = true;
                    }
                }
            }

            // Ambient player lighting corresponding to polarity
            if (isOverclock)
                Lighting.AddLight(player.Center, 0.30f, 0.15f, 0.03f);
            else
                Lighting.AddLight(player.Center, 0.04f, 0.30f, 0.14f);
        }

        public override void ModifyHitNPCWithItem(Player player, Item item, NPC target, ref NPC.HitModifiers modifiers)
        {
            var ap = player.GetModPlayer<AugmentPlayer>();
            if (ap.SynchronizerTimer < PhaseSwitchTick)
            {
                modifiers.FlatBonusDamage += (int)(item.damage * CombatDamageBonus);
            }
            else
            {
                modifiers.FlatBonusDamage += -(int)(item.damage * RepairDamagePenalty);
            }
        }

        public override void ModifyHitNPCWithProj(Player player, Projectile proj, NPC target, ref NPC.HitModifiers modifiers)
        {
            var ap = player.GetModPlayer<AugmentPlayer>();
            if (ap.SynchronizerTimer < PhaseSwitchTick)
            {
                modifiers.FlatBonusDamage += (int)(proj.damage * CombatDamageBonus);
            }
            else
            {
                modifiers.FlatBonusDamage += -(int)(proj.damage * RepairDamagePenalty);
            }
        }
    }
}
