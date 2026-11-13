using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace Augments
{
    // Active: press the key to mark the spot under your cursor. A moment later a salvo of shells falls on it.
    //
    // Everything is owner-authoritative, the same as Blade Dance: the marker, the shells and their bomblets are
    // normal projectiles the owner spawns (Terraria syncs those by itself), so no custom packet is needed and the
    // cooldown only has to live on the owner's own game.
    public class DangerCloseAugment : Augment
    {
        // Tuning
        public const int ShellCount = 20;
        public const int BaseShellDamage = 45;       // run through AugmentScaling.Scale
        public const int HeavyEvery = 5;             // every 5th shell is a heavy shell
        public const int BaseHeavyDamage = BaseShellDamage * 2;
        public const int BaseBombletDamage = 18;     // shown in the Ctrl view; the real value is 40% of the scaled shell damage
        public const float BombletDamageFraction = 0.4f;
        public const int BombletsPerShell = 2;
        public const int BombletsPerHeavyShell = 5;
        public const int BurnTicks = 240;
        public const int WarningTicks = 65;          // first shell leaves the sky here; it takes ~25 ticks to land, so ~1.5s to first impact
        public const int ShellIntervalTicks = 3;     // gap between shells
        public const int CooldownTicks = 2700;       // 45s
        public const float SpreadRadius = 200f;      // shells land within this many pixels left/right of the mark

        public override string Id => "danger_close";
        public override string DisplayName => "Danger Close";
        public override string Description =>
            $"{AugmentText.Active("Active:")} Press [{AugmentText.Key(Augments.DangerCloseKeybind)}] to mark your cursor. " +
            $"After {AugmentText.Duration("1.5 seconds")}, {ShellCount} shells rain down on it, each exploding for " +
            $"{AugmentText.SpecialDamage($"{BaseShellDamage} damage")} and setting enemies on fire. {AugmentText.Cooldown("45 seconds cooldown")}.";

        public override string GetDetailedDescription(Player player)
        {
            string details = AugmentText.Note(
                "(The first shell lands dead centre, the rest scatter 200 pixels to either side. Every 5th shell is a heavy shell: a bigger blast, " +
                $"{BaseHeavyDamage} damage, more knockback, and it shakes the screen.\n" +
                $"Every shell bursts into {BombletsPerShell} bomblets (heavy shells: {BombletsPerHeavyShell}) that arc out and each explode for {BaseBombletDamage} damage.\n" +
                "The damage is fixed: it does not depend on your weapon, but does scale with game progression. Your ranged crit chance still applies.\n" +
                "Shells ignore walls and explode at the height of the mark, so you can bombard enemies underground. The mark can go anywhere on screen, no weapon needed.)");

            return AugmentScaling.ApplyToText(Description + "\n" + details, GetScalingEntries());
        }

        public override (string label, int baseValue)[] GetScalingEntries()
        {
            return new[]
            {
                ("Shell damage", BaseShellDamage),
                ("Heavy shell damage", BaseHeavyDamage),
                ("Bomblet damage", BaseBombletDamage),
            };
        }

        public override AugmentRarity Rarity => AugmentRarity.Legendary;
        public override AugmentClass Class => AugmentClass.Ranged;
        public override ModKeybind ActiveModKeybind => Augments.DangerCloseKeybind;
        public override int CooldownRemaining => GetCooldown(LocalPlayerState.Player);
        public override Texture2D Icon => TextureAssets.Item[ItemID.RocketLauncher].Value;

        private static readonly Dictionary<int, int> cooldownTimer = new();

        public static int GetCooldown(Player player) =>
            player != null && cooldownTimer.TryGetValue(player.whoAmI, out int cd) ? cd : 0;

        public static void ResetCooldown(Player player)
        {
            if (player != null)
                cooldownTimer[player.whoAmI] = 0;
        }

        public override void OnAcquire(Player player)
        {
            cooldownTimer[player.whoAmI] = 0;
        }

        public override void OnUpdate(Player player)
        {
            if (cooldownTimer.TryGetValue(player.whoAmI, out int cd) && cd > 0)
                cooldownTimer[player.whoAmI] = cd - 1;
        }

        // Called from AugmentPlayer.ProcessTriggers when the keybind is pressed.
        public static void TryActivate(Player player)
        {
            if (player.whoAmI != Main.myPlayer || player.dead || GetCooldown(player) > 0)
                return;

            // Keep the mark inside the world so the shells always have somewhere to fall.
            Vector2 mark = Main.MouseWorld;
            mark.X = MathHelper.Clamp(mark.X, 64f, Main.maxTilesX * 16f - 64f);
            mark.Y = MathHelper.Clamp(mark.Y, 64f, Main.maxTilesY * 16f - 64f);

            // ai[1] carries the owner's ranged crit chance to the shells (the damage itself is fixed).
            int crit = (int)player.GetTotalCritChance(DamageClass.Ranged);
            int markerId = Projectile.NewProjectile(
                player.GetSource_FromThis(),
                mark,
                Vector2.Zero,
                ModContent.ProjectileType<DangerCloseMarkerProjectile>(),
                AugmentScaling.Scale(BaseShellDamage),
                0f,
                player.whoAmI,
                0f,
                crit);

            if (markerId < 0 || markerId >= Main.maxProjectiles)
                return;

            cooldownTimer[player.whoAmI] = CooldownTicks;
            // The "marked" sound is played by the marker projectile itself on its first tick, on every game.
        }
    }
}
