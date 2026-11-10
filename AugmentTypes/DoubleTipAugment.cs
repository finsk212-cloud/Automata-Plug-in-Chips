using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Augments.Core;

namespace Augments
{
    public class DoubleTipAugment : Augment
    {
        public override string Id => "double_tip";
        public override string DisplayName => "Double Tip";
        public override string Description =>
            $"Alternating between different magic weapons triggers {AugmentText.Trigger("Double Tip")} for {AugmentText.Duration("5 seconds")}:\n" +
            $"• Spells cost {AugmentText.Mana("-50% mana")}\n" +
            $"• Gain {AugmentText.BonusDamage("+15% magic damage")}\n" +
            $"• Magic casts fire an additional {AugmentText.SpecialDamage("Twin Harmonic Projectile")} dealing 40% damage.";

        public override string GetDetailedDescription(Player player) =>
            Description + "\n" +
            AugmentText.Note("(Swap between two different magic staves or tomes to keep Double Tip constantly refreshed.)");

        public override AugmentRarity Rarity => AugmentRarity.Epic;
        public override AugmentClass Class => AugmentClass.Magic;

        private const int BuffDurationTicks = 300; // 5 seconds
        private static readonly Dictionary<int, int> lastItemType = new();
        private static readonly Dictionary<int, int> activeTimer = new();
        private static readonly Dictionary<int, int> twinCooldown = new();

        public static bool IsActiveFor(Player player)
        {
            return activeTimer.TryGetValue(player.whoAmI, out int t) && t > 0;
        }

        public static int GetTimer(Player player)
        {
            return activeTimer.TryGetValue(player.whoAmI, out int t) ? t : 0;
        }

        public override bool IsCharging => IsActiveFor(LocalPlayerState.Player);
        public override int ChargeIndicatorPercent => (int)(GetTimer(LocalPlayerState.Player) / (float)BuffDurationTicks * 100f);
        public override Color StatusValueColor => AugmentTextColors.Trigger;

        public override void OnUpdate(Player player)
        {
            if (player.dead || !player.active)
                return;

            int who = player.whoAmI;

            // Decrement active buff timer
            if (activeTimer.TryGetValue(who, out int t) && t > 0)
            {
                activeTimer[who] = t - 1;

                // Subtle active aura particles
                if (who == Main.myPlayer && Main.rand.NextBool(6))
                {
                    int dType = Main.rand.NextBool() ? DustID.MagicMirror : DustID.PinkTorch;
                    Dust d = Dust.NewDustDirect(player.position, player.width, player.height, dType, 0f, 0f, 120, default, 0.9f);
                    d.noGravity = true;
                    d.velocity *= 0.3f;
                }
            }

            if (twinCooldown.TryGetValue(who, out int cd) && cd > 0)
                twinCooldown[who] = cd - 1;

            // Spell-weaving weapon swap detection
            Item held = player.HeldItem;
            if (held != null && !held.IsAir && (held.CountsAsClass(DamageClass.Magic) || held.DamageType == DamageClass.Magic))
            {
                lastItemType.TryGetValue(who, out int last);
                if (last != 0 && last != held.type)
                {
                    activeTimer[who] = BuffDurationTicks;

                    if (who == Main.myPlayer)
                    {
                        SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.50f, Pitch = 0.35f }, player.Center);
                        for (int i = 0; i < 14; i++)
                        {
                            Vector2 vel = Main.rand.NextVector2Circular(3.5f, 3.5f);
                            int dType = Main.rand.NextBool() ? DustID.MagicMirror : DustID.PinkTorch;
                            Dust d = Dust.NewDustPerfect(player.Center, dType, vel, 80, default, 1.2f);
                            d.noGravity = true;
                        }
                    }
                }
                lastItemType[who] = held.type;
            }
        }

        public override void UpdateEquips(Player player)
        {
            if (IsActiveFor(player))
            {
                player.GetDamage(DamageClass.Magic) += 0.15f;
            }
        }

        public override void ModifyManaCost(Player player, Item item, ref float reduce, ref float mult)
        {
            if (IsActiveFor(player) && (item.CountsAsClass(DamageClass.Magic) || item.DamageType == DamageClass.Magic))
            {
                mult *= 0.5f;
            }
        }

        public override void OnShootProjectile(Player player, Item item, Projectile projectile)
        {
            if (!IsActiveFor(player) || player.whoAmI != Main.myPlayer)
                return;

            if (projectile.GetGlobalProjectile<AugmentProjectileTag>().IsAugmentProcDamage || projectile.minion || projectile.sentry)
                return;

            if (!item.CountsAsClass(DamageClass.Magic) && item.DamageType != DamageClass.Magic)
                return;

            int who = player.whoAmI;
            if (twinCooldown.TryGetValue(who, out int cd) && cd > 0)
                return;

            // Small internal cooldown to prevent shotgun/beam floods
            twinCooldown[who] = 4;

            IEntitySource source = projectile.GetSource_FromThis();
            Vector2 twinVel = projectile.velocity.RotatedBy(Main.rand.NextFloat(-0.08f, 0.08f)) * 0.96f;
            int twinDmg = Math.Max(1, (int)(projectile.damage * 0.40f));
            int twinId = Projectile.NewProjectile(source, projectile.Center, twinVel, projectile.type, twinDmg, projectile.knockBack * 0.5f, player.whoAmI);
            if (twinId >= 0 && twinId < Main.maxProjectiles)
            {
                Projectile twin = Main.projectile[twinId];
                var twinTag = twin.GetGlobalProjectile<AugmentProjectileTag>();
                twinTag.IsAugmentProcDamage = true;
                twinTag.CanTriggerOnHitAugments = false;
                twinTag.PreventEchoChamberCopy = true;
                twinTag.PreventRicochetEngineCopy = true;
            }

            // Muzzle spark
            for (int i = 0; i < 4; i++)
            {
                Dust d = Dust.NewDustPerfect(projectile.Center, DustID.PinkTorch, projectile.velocity * 0.2f + Main.rand.NextVector2Circular(1f, 1f), 100, default, 1.1f);
                d.noGravity = true;
            }
        }
    }
}
