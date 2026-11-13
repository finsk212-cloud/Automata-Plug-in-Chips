using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Augments
{
    public class KickbackAugment : Augment
    {
        public override string Id => "kickback";
        public override string DisplayName => "Kickback";
        public override string Description =>
            $"Each shotgun or rocket launcher shot adds a {AugmentText.Trigger("stack")} (max {MaxStacks}). " +
            $"Every stack gives those weapons {AugmentText.BonusDamage("+15% damage")}. " +
            $"Stacks reset after {AugmentText.Duration("3 seconds")} without firing one.\n" +
            $"Firing one also pushes you {AugmentText.MovementSpeed("backwards")}, opposite your aim, and much harder up or down than sideways. " +
            $"Rocket launchers kick harder than shotguns.";

        public override string GetDetailedDescription(Player player) =>
            Description + "\n" +
            AugmentText.Note("(Works on the ground and in the air. No push while grappling. Shotguns are any gun that fires several bullets per shot; launchers are anything that fires rockets. Also detected on other mods' weapons, including Calamity's.)");

        public override AugmentRarity Rarity => AugmentRarity.Epic;
        public override AugmentClass Class => AugmentClass.Ranged;

        private const int MaxStacks = 10;
        private const float DamagePerStack = 0.15f;
        private const int StackResetTicks = 180; // 3s

        private const float ShotgunPush = 9f;
        private const float RocketPush = 14f;
        private const float VerticalPushMultiplier = 2.5f;
        private const float HorizontalSpeedCap = 22f;
        private const float VerticalSpeedCap = 32f;

        // Local player's stacks. The shots, the damage and the push all belong to the shooter's own game.
        private static int stacks;
        private static int stackTimer;

        // Per-use bookkeeping: one use can spawn several projectiles in the same tick.
        private static uint lastUseTick;
        private static int projectilesThisUse;
        private static bool kickedThisUse;
        private static bool stackedThisUse;
        private static Projectile firstProjectileThisUse;

        public override void OnAcquire(Player player)
        {
            stacks = 0;
            stackTimer = 0;
        }

        public override void OnUpdate(Player player)
        {
            if (player.whoAmI != Main.myPlayer)
                return;

            if (stackTimer > 0 && --stackTimer == 0)
                stacks = 0;
        }

        // Launchers: anything that fires rockets (vanilla and most modded launchers), plus
        // ranged modded items with "Launcher" in their class name for launchers that use custom ammo.
        private static bool IsRocketWeapon(Item item) =>
            item.useAmmo == AmmoID.Rocket || ModdedRangedNameContains(item, "Launcher");

        // Guns that are shotguns by name only matter for modded weapons that use custom ammo;
        // everything that fires bullets is caught by the "several bullets in one shot" check instead.
        private static bool IsNamedShotgun(Item item) => ModdedRangedNameContains(item, "Shotgun");

        private static bool ModdedRangedNameContains(Item item, string part) =>
            item.ModItem != null
            && item.CountsAsClass(DamageClass.Ranged)
            && item.ModItem.Name.Contains(part, StringComparison.OrdinalIgnoreCase);

        // One stack per use; every projectile of that use gets the same bonus.
        private static void GainStack()
        {
            if (stackedThisUse)
                return;
            stackedThisUse = true;

            stacks = Math.Min(MaxStacks, stacks + 1);
            stackTimer = StackResetTicks;
        }

        private static void Boost(Projectile projectile)
        {
            if (projectile != null && projectile.active && projectile.damage > 0)
                projectile.damage = (int)(projectile.damage * (1f + DamagePerStack * stacks));
        }

        private static void Kick(Player player, Projectile projectile, float push)
        {
            if (kickedThisUse)
                return;
            kickedThisUse = true;

            if (player.grapCount > 0 || projectile.velocity == Vector2.Zero)
                return;

            Vector2 delta = -Vector2.Normalize(projectile.velocity) * push;
            delta.Y *= VerticalPushMultiplier;

            Vector2 old = player.velocity;
            Vector2 now = old + delta;

            // Cap each direction separately so a hard vertical kick isn't limited by the sideways cap.
            float capX = Math.Max(Math.Abs(old.X), HorizontalSpeedCap);
            float capY = Math.Max(Math.Abs(old.Y), VerticalSpeedCap);
            player.velocity = new Vector2(
                MathHelper.Clamp(now.X, -capX, capX),
                MathHelper.Clamp(now.Y, -capY, capY));
        }

        public override void OnShootProjectile(Player player, Item item, Projectile projectile)
        {
            if (player.whoAmI != Main.myPlayer)
                return;

            bool rocket = IsRocketWeapon(item);
            bool namedShotgun = !rocket && IsNamedShotgun(item);
            bool bulletGun = !rocket && !namedShotgun && item.useAmmo == AmmoID.Bullet;
            if (!rocket && !namedShotgun && !bulletGun)
                return;

            if (lastUseTick != Main.GameUpdateCount)
            {
                lastUseTick = Main.GameUpdateCount;
                projectilesThisUse = 0;
                kickedThisUse = false;
                stackedThisUse = false;
                firstProjectileThisUse = null;
            }
            projectilesThisUse++;
            if (projectilesThisUse == 1)
                firstProjectileThisUse = projectile;

            if (rocket || namedShotgun)
            {
                GainStack();
                Boost(projectile);
                Kick(player, projectile, rocket ? RocketPush : ShotgunPush);
                return;
            }

            // A bullet gun is a shotgun once its second bullet appears in the same shot. The first
            // bullet already spawned, so it gets its bonus now (nothing can have hit yet this tick).
            if (projectilesThisUse < 2)
                return;

            GainStack();
            if (projectilesThisUse == 2)
                Boost(firstProjectileThisUse);
            Boost(projectile);
            Kick(player, projectile, ShotgunPush);
        }
    }
}
