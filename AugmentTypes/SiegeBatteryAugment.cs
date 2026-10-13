using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace Augments
{
    public class SiegeBatteryAugment : Augment
    {
        public override string Id => "siege_battery";
        public override string DisplayName => "Siege Battery";
        public override string Description =>
            $"Every {AugmentText.Cooldown("6s")}, each sentry also fires a siege shell at the {AugmentText.Trigger("highest-HP enemy")} in range " +
            $"for {AugmentText.BonusDamage("400% of its damage")}, with splash.";

        public override string GetDetailedDescription(Player player) =>
            Description + "\n" +
            $"Every sentry fires its own shell, so more sentries means more shells. The shell explodes on the first enemy it touches and damages everything nearby for the same amount. " +
            $"[c/94A3B8:(If no enemy is in range, the shells wait until one is.)]";

        public override AugmentRarity Rarity => AugmentRarity.Legendary;
        public override AugmentClass Class => AugmentClass.Summon;

        private const int CooldownTicks = 360;
        private const float Range = 800f;
        private const float ShellSpeed = 16f;
        private const float DamageMultiplier = 4f;
        private const float ShellKnockback = 4f;

        public override int CooldownRemaining => LocalPlayerState.SiegeBatteryCooldown;

        public override void OnUpdate(Player player)
        {
            if (player.whoAmI != Main.myPlayer)
                return;

            var ap = player.GetModPlayer<AugmentPlayer>();
            if (ap.SiegeBatteryCooldown > 0)
            {
                ap.SiegeBatteryCooldown--;
                return;
            }

            // Ready and waiting for a target: look a few times a second instead of every tick.
            if (player.dead || Main.GameUpdateCount % 10 != 0)
                return;

            if (FireShells(player))
                ap.SiegeBatteryCooldown = CooldownTicks;
        }

        // Each sentry picks the enemy with the most current HP inside its own range and in line of sight, so a boss
        // is chosen over its minions. Returns true if at least one sentry fired; the cooldown only starts then.
        private static bool FireShells(Player player)
        {
            bool fired = false;

            foreach (Projectile sentry in Main.projectile)
            {
                if (!sentry.active || !sentry.sentry || sentry.owner != player.whoAmI || sentry.damage <= 0)
                    continue;

                NPC target = FindHighestLifeTarget(sentry.Center);
                if (target == null)
                    continue;

                Vector2 velocity = (target.Center - sentry.Center).SafeNormalize(Vector2.UnitY) * ShellSpeed;
                int shellDamage = System.Math.Max(1, (int)(sentry.damage * DamageMultiplier));

                Projectile.NewProjectile(
                    player.GetSource_FromThis(),
                    sentry.Center,
                    velocity,
                    ModContent.ProjectileType<SiegeShellProjectile>(),
                    shellDamage,
                    ShellKnockback,
                    player.whoAmI,
                    0f,
                    target.whoAmI
                );

                fired = true;
            }

            return fired;
        }

        private static NPC FindHighestLifeTarget(Vector2 origin)
        {
            NPC best = null;
            float rangeSquared = Range * Range;

            foreach (NPC npc in Main.npc)
            {
                if (!npc.CanBeChasedBy())
                    continue;

                if (Vector2.DistanceSquared(origin, npc.Center) > rangeSquared)
                    continue;

                if (best != null && npc.life <= best.life)
                    continue;

                if (!Collision.CanHit(origin, 1, 1, npc.Center, 1, 1))
                    continue;

                best = npc;
            }

            return best;
        }
    }
}
