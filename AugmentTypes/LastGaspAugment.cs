using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;
using Augments.Core;

namespace Augments
{
    // Legendary, Universal. A hit that would kill you is refused: for 5 seconds your health sits at 0, nothing can
    // hurt you, and you deal +100% damage with +50% attack speed. The screen drains to 0 saturation ("memory fade").
    // When the 5 seconds end a small blast goes off around you, and only then
    // does the death happen (bosses leave once every player is dead, so the blast has to land first).
    // Plugin id stays neutral ("death_blast") because ids are saved with characters.
    // Everything runs on the owner's own game: the timer marker is a normal synced projectile (so everyone sees it)
    // and the owner's game spawns the blast that deals the damage.
    public class LastGaspAugment : Augment
    {
        public override string Id => "death_blast";
        public override string DisplayName => "Last Gasp";
        public override string Description =>
            $"When a hit would {AugmentText.Trigger("kill you")}, you refuse to die: for {AugmentText.Duration("5 seconds")} your health stays at 0, " +
            $"nothing can hurt you, and you deal {AugmentText.BonusDamage("+100% damage")} with {AugmentText.BonusDamage("+50% attack speed")}. " +
            $"Then you die in a small blast that hurts enemies nearby. {AugmentText.Cooldown("180 seconds cooldown")}.";

        public override string GetDetailedDescription(Player player) =>
            Description + "\n" +
            AugmentText.Note("(The screen slowly drains of all color while it lasts. The ending blast deals 2x your max health within about 19 tiles, plus 1% of a boss's max health, and goes off just before you die so bosses are still there to take it. It does not trigger other plugins. A life-saving plugin works first, and if the cooldown is still running a lethal hit kills you as usual.)");

        public override AugmentRarity Rarity => AugmentRarity.Legendary;
        public override AugmentClass Class => AugmentClass.Universal;

        public const int RampageTicks = 5 * 60;
        public const int CooldownTicks = 180 * 60;
        public const float DamageBonus = 1f;            // +100% damage
        public const float AttackSpeedBonus = 0.5f;     // +50% attack speed
        public const int FinaleDamageMultiplier = 2;    // x your max health
        public const float FinaleRadius = 300f;         // about 19 tiles
        private const int KillDelayTicks = 12;          // the blast gets this long to land before you actually die

        // The state of the rampage. Only the owner's game runs it.
        private static int rampageLeft;
        private static int killDelayLeft;
        private static int cooldownLeft;
        private static bool finalDeath;                 // true only for the death this plugin lets through
        private static PlayerDeathReason savedReason;
        private static double savedDamage;
        private static int savedHitDirection;
        private static bool savedPvp;

        public static bool IsCountingDown => rampageLeft > 0 || killDelayLeft > 0;

        // True while every death except the plugin's own final one must be refused.
        internal static bool BlocksDeath => IsCountingDown && !finalDeath;

        private static void ClearRampage()
        {
            rampageLeft = 0;
            killDelayLeft = 0;
            finalDeath = false;
        }

        private static bool IsOwnersGame(Player player) =>
            !Main.dedServ && player.whoAmI == Main.myPlayer;

        public override void OnAcquire(Player player)
        {
            ClearRampage();
            cooldownLeft = 0;
        }

        // Called from AugmentPlayer.PreKill once every life-saving plugin has had its say. Returning true cancels
        // the death: the player stays alive at 0 health while the rampage runs.
        internal static bool TryDelayDeath(Player player, double damage, int hitDirection, bool pvp, PlayerDeathReason reason)
        {
            if (!IsOwnersGame(player) || finalDeath)
                return false; // the final, allowed death goes through

            if (IsCountingDown)
                return true;  // already in the rampage: nothing can kill you now

            if (cooldownLeft > 0)
                return false; // used recently: this one is a normal death

            savedReason = reason;
            savedDamage = damage;
            savedHitDirection = hitDirection;
            savedPvp = pvp;
            rampageLeft = RampageTicks;
            cooldownLeft = CooldownTicks;

            player.statLife = 0;
            player.immune = true;
            player.immuneTime = Math.Max(player.immuneTime, 2);

            Projectile.NewProjectile(
                player.GetSource_FromThis(),
                player.Center,
                Vector2.Zero,
                ModContent.ProjectileType<LastGaspMarkerProjectile>(),
                0,
                0f,
                player.whoAmI);

            return true;
        }

        public override void UpdateEquips(Player player)
        {
            if (!IsOwnersGame(player) || rampageLeft <= 0)
                return;

            player.GetDamage(DamageClass.Generic) += DamageBonus;
            player.GetAttackSpeed(DamageClass.Generic) += AttackSpeedBonus;
        }

        public override void OnUpdate(Player player)
        {
            if (!IsOwnersGame(player))
                return;

            if (cooldownLeft > 0)
                cooldownLeft--;

            if (!IsCountingDown)
                return;

            if (player.dead)
            {
                // Something else killed the player outright; drop the rampage.
                ClearRampage();
                return;
            }

            // Alive, but on 0 health and untouchable until the blast has gone off.
            player.statLife = 0;
            player.immune = true;
            player.immuneTime = Math.Max(player.immuneTime, 2);

            if (rampageLeft > 0)
            {
                rampageLeft--;
                if (rampageLeft == 0)
                {
                    int blastDamage = (int)Math.Min((long)Math.Max(1, player.statLifeMax2) * FinaleDamageMultiplier, int.MaxValue / 2);
                    Projectile.NewProjectile(
                        player.GetSource_FromThis(),
                        player.Center,
                        Vector2.Zero,
                        ModContent.ProjectileType<LastGaspBlastProjectile>(),
                        blastDamage,
                        10f,
                        player.whoAmI,
                        FinaleRadius);
                    killDelayLeft = KillDelayTicks;
                }
                return;
            }

            killDelayLeft--;
            if (killDelayLeft <= 0)
            {
                // The blast has landed. Now the death actually happens.
                killDelayLeft = 0;
                finalDeath = true;
                player.immune = false;
                player.immuneTime = 0;
                player.KillMe(savedReason, savedDamage, savedHitDirection, savedPvp);
                finalDeath = false;
            }
        }

        // The rampage ends in death, so only the rampage is cleared; the cooldown keeps running through the respawn.
        public override void OnKill(Player player)
        {
            if (IsOwnersGame(player))
                ClearRampage();
        }

        // Seconds left in the rampage, shown on the status icon; the cooldown shows on the usual cooldown icon.
        public override int? StatusValue =>
            rampageLeft > 0 ? (int)Math.Ceiling(rampageLeft / 60f) : null;

        public override Color StatusValueColor => new Color(240, 60, 45);

        public override int CooldownRemaining => cooldownLeft;
    }

    // The "memory fade": while the rampage runs the screen drains to 0 saturation. It reuses the game's own
    // Graveyard screen filter (the washed-out look of the Graveyard biome), switched on and driven from here,
    // and fades in and out smoothly. Only the rampaging player sees it.
    public class LastGaspSystem : ModSystem
    {
        private const string FilterName = "Graveyard";
        private float strength;     // 0 to 1, how drained the screen is right now
        private bool filterOn;

        public override void PostUpdateEverything()
        {
            if (Main.dedServ)
                return;

            // Fade in over about 0.4 seconds, and back out a little slower.
            float target = LastGaspAugment.IsCountingDown ? 1f : 0f;
            strength = target > strength ? Math.Min(target, strength + 0.05f) : Math.Max(target, strength - 0.03f);

            if (strength > 0f)
            {
                // The game switches this filter off on any tick the player is not in a graveyard, so it is
                // switched back on here, after that, every tick it is needed.
                if (!Filters.Scene[FilterName].IsActive())
                    Filters.Scene.Activate(FilterName, Main.LocalPlayer.Center);

                ScreenShaderData shader = Filters.Scene[FilterName].GetShader();
                shader.UseTargetPosition(Main.LocalPlayer.Center);
                shader.UseProgress(strength);
                shader.UseIntensity(1.2f);
                filterOn = true;
            }
            else if (filterOn)
            {
                filterOn = false;
                if (Filters.Scene[FilterName].IsActive() && Main.GraveyardVisualIntensity <= 0f)
                    Filters.Scene.Deactivate(FilterName);
            }
        }

        public override void OnWorldUnload()
        {
            strength = 0f;
            filterOn = false;
        }
    }
}
