using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Augments.Core;

namespace Augments
{
    public class FloorIsLavaAugment : Augment
    {
        public override string Id => "floor_is_lava";
        public override string DisplayName => "Floor is Lava";
        public override string Description =>
            $"Ranged explosions splash {AugmentText.Trigger("Napalm Plasma")} outward. The plasma lands on the ground as several pools (and sometimes sticks to a wall) that burn for {AugmentText.Duration("4 seconds")}, dealing continuous {AugmentText.SpecialDamage("22 fire damage")}. " +
            $"Enemies standing in the plasma (pools or wall flames) suffer {AugmentText.Trigger("Thermite Melt")}: ignoring {AugmentText.BonusDamage("20 defense")}, taking {AugmentText.BonusDamage("+15% ranged damage")} from all sources, and being slowed by {AugmentText.Immobilize("25%")}. " +
            $"You take {AugmentText.Defense("75% less self-damage")} from your own explosives.";

        public override string GetDetailedDescription(Player player) =>
            AugmentScaling.ApplyToText(Description, GetScalingEntries()) + "\n" +
            AugmentText.Note("(The splash covers ~9 tiles. Wall flames burn for 3 seconds. Each pool ticks 4 times per second, scaling with world tier and ranged damage. Thermite Melt lingers for 3 seconds after contact. Rocket jumping deals minimal self-harm.)");

        public override AugmentRarity Rarity => AugmentRarity.Legendary;
        public override AugmentClass Class => AugmentClass.Ranged;

        public const int BaseDamage = 22;

        public override (string label, int baseValue)[] GetScalingEntries()
        {
            return new[] { ("Napalm burn", BaseDamage) };
        }

        public override void ModifyHurt(Player player, ref Player.HurtModifiers modifiers)
        {
            if (modifiers.DamageSource.SourceProjectileLocalIndex >= 0)
            {
                int projIdx = modifiers.DamageSource.SourceProjectileLocalIndex;
                if (projIdx >= 0 && projIdx < Main.maxProjectiles)
                {
                    Projectile p = Main.projectile[projIdx];
                    if (p.active && p.owner == player.whoAmI)
                    {
                        modifiers.FinalDamage *= 0.25f; // 75% less self-inflicted explosive damage
                    }
                }
            }
        }
    }

    public class FloorIsLavaGlobalProjectile : GlobalProjectile
    {
        public override bool InstancePerEntity => true;

        public override void OnKill(Projectile projectile, int timeLeft)
        {
            if (projectile.owner != Main.myPlayer)
                return;

            Player player = Main.player[projectile.owner];
            if (!player.active || player.dead)
                return;

            var ap = player.GetModPlayer<AugmentPlayer>();
            if (!ap.HasAugment("floor_is_lava"))
                return;

            if (projectile.type == ModContent.ProjectileType<NapalmPlasmaPool>())
                return;

            // Party Poppers' bomblets are explosions too: each one drops a single blob when it goes off.
            bool isBomblet = projectile.type == ModContent.ProjectileType<PartyPopperBomblet>();
            if (!isBomblet && !PartyPoppersAugment.IsExplosiveRanged(projectile))
                return;

            int baseDmg = AugmentScaling.Scale(FloorIsLavaAugment.BaseDamage);
            int finalDmg = Math.Max(1, (int)player.GetTotalDamage(DamageClass.Ranged).ApplyTo(baseDmg));

            // Echo Chamber echoes (and bomblets they release) trigger chips at half effectiveness.
            if (projectile.GetGlobalProjectile<AugmentProjectileTag>().IsEchoChamberEcho)
                finalDmg = Math.Max(1, finalDmg / 2);
            int poolType = ModContent.ProjectileType<NapalmPlasmaPool>();

            // The napalm splashes outward in a spread of blobs; each flies in an arc, lands and burns as
            // its own small pool (the damage per pool is unchanged, so standing in the middle of the
            // spread is not much worse than standing at the edge).
            IEntitySource source = projectile.GetSource_FromThis();
            // A bomblet is small and there are four per rocket, so it only drops one blob.
            int blobs = isBomblet ? 1 : 5;
            for (int i = 0; i < blobs; i++)
            {
                float vx;
                float vy;
                if (blobs == 1)
                {
                    vx = Main.rand.NextFloat(-1.5f, 1.5f);
                    vy = -1.5f;
                }
                else
                {
                    float t = i / (float)(blobs - 1);
                    vx = MathHelper.Lerp(-4f, 4f, t) + Main.rand.NextFloat(-0.5f, 0.5f);
                    vy = i == blobs / 2 ? -1f : -Main.rand.NextFloat(1.5f, 4.5f);
                }
                int blobIndex = Projectile.NewProjectile(source, projectile.Center, new Vector2(vx, vy), poolType, finalDmg, 0f, projectile.owner);
                PartyPopperGlobalProjectile.KeepProcRules(blobIndex);
            }
        }
    }
}
