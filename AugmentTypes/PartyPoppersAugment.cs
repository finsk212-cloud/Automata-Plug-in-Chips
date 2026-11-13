using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Augments.Core;

namespace Augments
{
    public class PartyPoppersAugment : Augment
    {
        public override string Id => "party_poppers";
        public override string DisplayName => "Party Poppers";
        public override string Description =>
            $"Rockets and explosive ranged weapons gain {AugmentText.BonusDamage("+15% damage")} and a {AugmentText.Trigger("+30% blast radius")}. " +
            $"When an explosive detonates, it releases {AugmentText.Trigger("4 bouncy micro-bomblets")} that explode on contact.";

        public override string GetDetailedDescription(Player player) =>
            Description + "\n" +
            AugmentText.Note("(Affects all rocket launchers, grenade launchers, and explosive ammunition. Bomblets deal 35% of the original hit's damage and bounce up to 2 times.)");

        public override AugmentRarity Rarity => AugmentRarity.Epic;
        public override AugmentClass Class => AugmentClass.Ranged;

        public static bool IsExplosiveRanged(Projectile p)
        {
            if (p == null || !p.active || !p.friendly || p.hostile || p.minion || p.sentry)
                return false;
            if (!p.CountsAsClass(DamageClass.Ranged))
                return false;
            if (p.type == ModContent.ProjectileType<PartyPopperBomblet>())
                return false;
            // Other chips' proc projectiles don't count, but Echo Chamber echoes are real shots from the
            // player's weapon, so an echoed rocket pops confetti and (with Floor is Lava) napalm too.
            AugmentProjectileTag tag = p.GetGlobalProjectile<AugmentProjectileTag>();
            if (tag.IsAugmentProcDamage && !tag.IsEchoChamberEcho)
                return false;

            // Anything fired by a rocket-ammo weapon counts, even if the projectile isn't one of the vanilla explosive kinds
            // (the Celebration's firework rockets are like that).
            return tag.FromRocketLauncher || p.aiStyle == ProjAIStyleID.Explosive || ProjectileID.Sets.Explosive[p.type];
        }

        public override void OnProjectileSpawn(Player player, Projectile projectile)
        {
            if (projectile.owner != player.whoAmI)
                return;

            if (IsExplosiveRanged(projectile))
            {
                // +15% damage bonus on explosive ranged weapons
                projectile.damage = (int)(projectile.damage * 1.15f);
            }
        }
    }

    public class PartyPopperGlobalProjectile : GlobalProjectile
    {
        public override bool InstancePerEntity => true;

        // A bomblet spawned by an echoed rocket inherits the echo's tag, including "may trigger on-hit
        // chips". Bomblets must never do that, so put their own rules back (the echo flag stays, so
        // Floor is Lava can still halve their napalm).
        internal static void KeepProcRules(int projectileIndex)
        {
            if (projectileIndex < 0 || projectileIndex >= Main.maxProjectiles)
                return;

            AugmentProjectileTag tag = Main.projectile[projectileIndex].GetGlobalProjectile<AugmentProjectileTag>();
            tag.CanTriggerOnHitAugments = false;
            tag.PreventEchoChamberCopy = true;
            tag.PreventRicochetEngineCopy = true;
        }

        public override void OnKill(Projectile projectile, int timeLeft)
        {
            if (projectile.owner != Main.myPlayer)
                return;

            Player player = Main.player[projectile.owner];
            if (!player.active || player.dead)
                return;

            var ap = player.GetModPlayer<AugmentPlayer>();
            if (!ap.HasAugment("party_poppers"))
                return;

            if (!PartyPoppersAugment.IsExplosiveRanged(projectile))
                return;

            int bombletDamage = Math.Max(1, (int)(projectile.damage * 0.35f));
            int bombletType = ModContent.ProjectileType<PartyPopperBomblet>();

            for (int i = 0; i < 4; i++)
            {
                Vector2 vel = new Vector2(
                    Main.rand.NextFloat(-4.5f, 4.5f),
                    Main.rand.NextFloat(-6.0f, -2.5f)
                );

                IEntitySource source = projectile.GetSource_FromThis();
                int bombletIndex = Projectile.NewProjectile(source, projectile.Center, vel, bombletType, bombletDamage, 2.5f, projectile.owner);
                KeepProcRules(bombletIndex);
            }
        }
    }
}
