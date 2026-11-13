using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Augments.Core;

namespace Augments
{
    public class OneInTheChamberAugment : Augment
    {
        public override string Id => "one_in_the_chamber";
        public override string DisplayName => "One in the Chamber";
        public override string Description =>
            $"Pausing attack for {AugmentText.Duration("0.7 seconds")} chambers a {AugmentText.Trigger("Hyper-Velocity Round")}. " +
            $"The next shot gains {AugmentText.Velocity("+100% velocity")}, pierces {AugmentText.Trigger("3 additional enemies")}, and deals {AugmentText.BonusDamage("+35% damage")}.";

        public override string GetDetailedDescription(Player player) =>
            Description + "\n" +
            AugmentText.Note("(Rewards rhythmic, timed trigger pulls. Spraying full-auto only supercharges the initial shot.)");

        public override AugmentRarity Rarity => AugmentRarity.Rare;
        public override AugmentClass Class => AugmentClass.Ranged;

        private const int RequiredPauseTicks = 42; // 0.7 seconds
        private static readonly Dictionary<int, int> pauseTicks = new();
        private static readonly Dictionary<int, bool> isPrimed = new();

        public override void OnUpdate(Player player)
        {
            if (player.dead || !player.active)
                return;

            int who = player.whoAmI;

            // Other players' chambers: their own game tells us when they are loaded or fired (ChamberPrimed
            // packet). Timing the pause here would only drift, since we can't see their trigger pulls.
            if (who != Main.myPlayer)
            {
                if (remotePrimed.TryGetValue(who, out bool shown) && shown && Main.GameUpdateCount % 6 == 0)
                {
                    Dust d = Dust.NewDustDirect(player.position, player.width, player.height, DustID.Electric, 0f, 0f, 120, new Color(56, 189, 248), 0.7f);
                    d.noGravity = true;
                    d.velocity *= 0.3f;
                }
                return;
            }

            pauseTicks.TryGetValue(who, out int ticks);
            isPrimed.TryGetValue(who, out bool primed);

            ticks++;
            pauseTicks[who] = ticks;

            if (ticks >= RequiredPauseTicks && !primed)
            {
                isPrimed[who] = true;
                AugmentNet.SendChamberPrimed(true);
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
                AugmentNet.SendChamberPrimed(false);
                return true;
            }

            return false;
        }

        // Other players' loaded state, as reported by their own game.
        private static readonly Dictionary<int, bool> remotePrimed = new();

        public static void SetRemotePrimed(int who, bool primed)
        {
            remotePrimed[who] = primed;
            if (primed && who >= 0 && who < Main.maxPlayers && Main.player[who].active)
            {
                Player p = Main.player[who];
                SoundEngine.PlaySound(SoundID.Item37 with { Pitch = 0.5f, Volume = 0.3f }, p.Center);
                for (int i = 0; i < 6; i++)
                {
                    Dust d = Dust.NewDustDirect(p.position, p.width, p.height, DustID.Electric, 0f, 0f, 120, new Color(56, 189, 248), 0.8f);
                    d.noGravity = true;
                }
            }
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

        // The owner decides which shot is a Hyper-Velocity Round; other players' games only see the
        // projectile, so the flag travels with it so they can draw the trail too.
        public override void SendExtraAI(Projectile projectile, BitWriter bitWriter, BinaryWriter binaryWriter)
        {
            bitWriter.WriteBit(IsHyperShot);
        }

        public override void ReceiveExtraAI(Projectile projectile, BitReader bitReader, BinaryReader binaryReader)
        {
            IsHyperShot = bitReader.ReadBit();
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
