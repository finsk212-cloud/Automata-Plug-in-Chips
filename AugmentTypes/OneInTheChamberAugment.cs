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
    public class OneInTheChamberAugment : Augment
    {
        public override string Id => "one_in_the_chamber";
        public override string DisplayName => "One in the Chamber";
        public override string Description =>
            $"Pausing attack for {AugmentText.Duration("0.7s")} chambers a {AugmentText.Trigger("Hyper-Velocity Round")}. " +
            $"The next shot gains {AugmentText.Velocity("+100% velocity")}, pierces {AugmentText.Trigger("3 additional enemies")}, and deals {AugmentText.BonusDamage("+35% damage")}.";

        public override string GetDetailedDescription(Player player) =>
            Description + "\n" +
            AugmentText.Note("(Rewards rhythmic, timed trigger pulls. Spraying full-auto only supercharges the initial shot.)");

        public override AugmentRarity Rarity => AugmentRarity.Rare;
        public override AugmentClass Class => AugmentClass.Ranged;
        public override string FamilyId => AugmentFamilyRegistry.MarksmanId;

        private const int RequiredPauseTicks = 42; // 0.7 seconds
        private static readonly Dictionary<int, int> pauseTicks = new();
        private static readonly Dictionary<int, bool> isPrimed = new();

        public override void OnUpdate(Player player)
        {
            if (player.dead || !player.active)
                return;

            int who = player.whoAmI;
            pauseTicks.TryGetValue(who, out int ticks);
            isPrimed.TryGetValue(who, out bool primed);

            ticks++;
            pauseTicks[who] = ticks;

            if (ticks >= RequiredPauseTicks && !primed)
            {
                isPrimed[who] = true;
                if (who == Main.myPlayer)
                {
                    SoundEngine.PlaySound(SoundID.Item37 with { Pitch = 0.5f, Volume = 0.4f });
                    for (int i = 0; i < 6; i++)
                    {
                        Dust d = Dust.NewDustDirect(player.position, player.width, player.height, DustID.Electric, 0f, 0f, 120, new Color(56, 189, 248), 0.8f);
                        d.noGravity = true;
                    }
                }
            }
        }

        public static bool ConsumePrimedShot(Player player)
        {
            int who = player.whoAmI;
            pauseTicks[who] = 0;

            if (isPrimed.TryGetValue(who, out bool primed) && primed)
            {
                isPrimed[who] = false;
                return true;
            }

            return false;
        }
    }

    public class OneInTheChamberGlobalProjectile : GlobalProjectile
    {
        public override bool InstancePerEntity => true;

        public bool IsHyperShot = false;

        public override void OnSpawn(Projectile projectile, IEntitySource source)
        {
            if (projectile.owner != Main.myPlayer)
                return;

            if (!projectile.friendly || projectile.hostile || projectile.minion || projectile.sentry)
                return;

            if (!projectile.CountsAsClass(DamageClass.Ranged))
                return;

            if (projectile.GetGlobalProjectile<AugmentProjectileTag>().IsAugmentProcDamage)
                return;

            Player player = Main.player[projectile.owner];
            if (!player.active || player.dead)
                return;

            var ap = player.GetModPlayer<AugmentPlayer>();
            if (!ap.HasAugment("one_in_the_chamber"))
                return;

            if (OneInTheChamberAugment.ConsumePrimedShot(player))
            {
                IsHyperShot = true;
                projectile.velocity *= 2.0f;
                projectile.damage = (int)(projectile.damage * 1.35f);
                projectile.penetrate = projectile.penetrate < 0 ? projectile.penetrate : projectile.penetrate + 3;

                SoundEngine.PlaySound(SoundID.Item38 with { Pitch = 0.2f, Volume = 0.6f }, projectile.position);
            }
        }

        public override void PostAI(Projectile projectile)
        {
            if (!IsHyperShot)
                return;

            // Mach cone sonic slipstream
            if (Main.rand.NextBool(2))
            {
                Dust d = Dust.NewDustPerfect(
                    projectile.Center,
                    DustID.Electric,
                    projectile.velocity * -0.12f + Main.rand.NextVector2Circular(0.8f, 0.8f),
                    100,
                    new Color(56, 189, 248),
                    Main.rand.NextFloat(0.7f, 1.1f)
                );
                d.noGravity = true;
            }
        }
    }
}
