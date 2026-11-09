using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Augments.Core;

namespace Augments
{
    public class TurretSyndromeAugment : Augment
    {
        public override string Id => "turret_syndrome";
        public override string DisplayName => "Turret Syndrome";
        public override string Description =>
            $"Permanently grants {AugmentText.Defense("+1 max sentry slot")}. Active sentries become {AugmentText.Trigger("mobile hovering drones")} " +
            $"that slowly orbit around you, dealing {AugmentText.BonusDamage("+25% sentry damage")}.";

        public override AugmentRarity Rarity => AugmentRarity.Legendary;
        public override AugmentClass Class => AugmentClass.Summon;

        public const float SentryDamageBonus = 0.25f;

        public override void UpdateEquips(Player player)
        {
            player.maxTurrets += 1;
        }

        public override void ModifyHitNPCWithProj(Player player, Projectile proj, NPC target, ref NPC.HitModifiers modifiers, AugmentHitSource source, float effectiveness)
        {
            bool isSentry = proj.sentry || (proj.type >= ProjectileID.None && proj.type < ProjectileID.Sets.SentryShot.Length && ProjectileID.Sets.SentryShot[proj.type]);
            if (isSentry)
            {
                modifiers.SourceDamage += SentryDamageBonus;
            }
        }

        public static List<Projectile> GetActiveSentries(Player player)
        {
            var list = new List<Projectile>();
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile p = Main.projectile[i];
                if (p.active && p.owner == player.whoAmI && p.sentry)
                {
                    list.Add(p);
                }
            }
            return list;
        }

        public static void AnchorSentryToFormation(Player player, Projectile sentry)
        {
            List<Projectile> sentries = GetActiveSentries(player);
            int index = sentries.IndexOf(sentry);
            if (index < 0)
                return;

            int count = sentries.Count;
            Vector2 targetPos = CalculateSentrySlot(player, index, count);

            // Neutralize any gravity or tile velocity from vanilla AI
            sentry.tileCollide = false;
            sentry.velocity = Vector2.Zero;
            sentry.gfxOffY = 0f;

            // Direct smooth lock to orbiting position
            sentry.Center = targetPos;

            // Subtle repulsor drone thrust particles & light under sentry base
            if (Main.rand.NextBool(4))
            {
                Vector2 exhaustPos = sentry.Bottom;
                Dust d = Dust.NewDustPerfect(
                    exhaustPos + new Vector2(Main.rand.NextFloat(-8f, 8f), 2f),
                    DustID.OrangeTorch,
                    new Vector2(0f, Main.rand.NextFloat(1.5f, 3f)),
                    100,
                    default,
                    Main.rand.NextFloat(0.8f, 1.1f)
                );
                d.noGravity = true;
                Lighting.AddLight(exhaustPos, 0.30f, 0.18f, 0.05f);
            }
        }

        public static Vector2 CalculateSentrySlot(Player player, int index, int count)
        {
            // Slow, majestic elliptical orbit around the player (~0.65 rad/sec)
            float orbitSpeed = 0.65f;
            float baseAngle = (float)Main.GlobalTimeWrappedHourly * orbitSpeed;
            float angle = baseAngle + index * (MathHelper.TwoPi / Math.Max(1, count));

            float rx = 95f + count * 6f;
            float ry = 55f + count * 3f;
            float bob = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 3f + index * 1.5f) * 4f;

            return player.Center + new Vector2(
                (float)Math.Cos(angle) * rx,
                (float)Math.Sin(angle) * ry - 15f + bob
            );
        }
    }

    public class TurretSyndromeGlobalProjectile : GlobalProjectile
    {
        public override bool InstancePerEntity => false;

        public override bool PreAI(Projectile projectile)
        {
            if (projectile.active && projectile.sentry && projectile.owner >= 0 && projectile.owner < Main.maxPlayers)
            {
                Player player = Main.player[projectile.owner];
                if (player.active && !player.dead && player.GetModPlayer<AugmentPlayer>().HasAugment("turret_syndrome"))
                {
                    // Disable tile collision and zero out any gravity before vanilla AI can use it
                    projectile.tileCollide = false;
                    projectile.velocity = Vector2.Zero;
                }
            }
            return true;
        }

        public override void PostAI(Projectile projectile)
        {
            if (projectile.active && projectile.sentry && projectile.owner >= 0 && projectile.owner < Main.maxPlayers)
            {
                Player player = Main.player[projectile.owner];
                if (player.active && !player.dead && player.GetModPlayer<AugmentPlayer>().HasAugment("turret_syndrome"))
                {
                    TurretSyndromeAugment.AnchorSentryToFormation(player, projectile);
                }
            }
        }
    }
}
