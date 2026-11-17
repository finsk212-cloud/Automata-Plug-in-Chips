using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Augments.Core;

namespace Augments
{
    // Legendary, Universal. A "Pain Meter" stores the damage you take. When a hit would kill you, the Pod
    // refuses: your health drops to 0 but you stay alive for a short 3, 2, 1 countdown (you can still move and
    // fight), then you explode for the stored damage and only THEN die. The blast has to go off before the death,
    // because bosses leave as soon as every player is dead and would never take the hit.
    // Plugin id stays neutral ("death_blast") because ids are saved with characters.
    // Everything runs on the owner's own game: the countdown marker is a normal synced projectile (so everyone
    // sees it) and the owner's game spawns the blast that deals the damage.
    public class GoingOutWithABangAugment : Augment
    {
        public override string Id => "death_blast";
        public override string DisplayName => "Going Out With a Bang";
        public override string Description =>
            $"The damage you take over the last {AugmentText.Duration("20 seconds")} is stored. When a hit would {AugmentText.Trigger("kill you")}, " +
            $"your health drops to 0 and your Pod counts down while you keep fighting. Then you explode, dealing {AugmentText.SpecialDamage("the stored damage")} " +
            $"to every enemy nearby: at least {AugmentText.BonusDamage("3x your max health")} and at most {AugmentText.BonusDamage("20x")}. Bosses also take {AugmentText.BonusDamage("1% of their max health")}.";

        public override string GetDetailedDescription(Player player) =>
            Description + "\n" +
            AugmentText.Note("(You can still move and use items during the countdown, and nothing can hurt you. The blast goes off just before you die, so bosses are still there to take it. It only hurts enemies and does not trigger other plugins. The number on the status icon is the multiplier you would deal right now.)");

        public override AugmentRarity Rarity => AugmentRarity.Legendary;
        public override AugmentClass Class => AugmentClass.Universal;

        public const int StorageTicks = 20 * 60;       // damage taken in the last 20 seconds counts
        public const int MinMultiplier = 3;
        public const int MaxMultiplier = 20;
        public const int TicksPerNumber = 45;          // the Pod counts 3, 2, 1
        public const int CountdownTicks = 3 * TicksPerNumber;
        private const int KillDelayTicks = 12;         // the blast gets this long to land before you actually die

        // The damage taken so far, oldest first. Only the owner's own game ever fills it.
        private static readonly Queue<(uint tick, int damage)> pain = new();

        // The countdown. Only the owner's game runs it.
        private static int countdownLeft;
        private static int killDelayLeft;
        private static int blastDamage;
        private static bool finalDeath;                 // true only for the death this plugin lets through
        private static PlayerDeathReason savedReason;
        private static double savedDamage;
        private static int savedHitDirection;
        private static bool savedPvp;

        public static bool IsCountingDown => countdownLeft > 0 || killDelayLeft > 0;

        // True while every death except the plugin's own final one must be refused.
        internal static bool BlocksDeath => IsCountingDown && !finalDeath;

        private static void Clear()
        {
            pain.Clear();
            countdownLeft = 0;
            killDelayLeft = 0;
            finalDeath = false;
        }

        private static void Prune()
        {
            while (pain.Count > 0 && Main.GameUpdateCount - pain.Peek().tick > StorageTicks)
                pain.Dequeue();
        }

        private static long StoredDamage()
        {
            Prune();
            long total = 0;
            foreach (var entry in pain)
                total += entry.damage;
            return total;
        }

        // The blast damage if you died right now: the stored damage, but never under 3x or over 20x max health.
        public static int BlastDamage(Player player)
        {
            long maxLife = Math.Max(1, player.statLifeMax2);
            long damage = Math.Clamp(StoredDamage(), maxLife * MinMultiplier, maxLife * MaxMultiplier);
            return (int)Math.Min(damage, int.MaxValue / 2);
        }

        public static float Multiplier(Player player) =>
            BlastDamage(player) / (float)Math.Max(1, player.statLifeMax2);

        private static bool IsOwnersGame(Player player) =>
            !Main.dedServ && player.whoAmI == Main.myPlayer;

        public override void OnAcquire(Player player) => Clear();

        public override void OnHurt(Player player, Player.HurtInfo info)
        {
            if (!IsOwnersGame(player) || info.Damage <= 0 || IsCountingDown)
                return;

            pain.Enqueue((Main.GameUpdateCount, info.Damage));
        }

        // Called from AugmentPlayer.PreKill once every life-saving plugin has had its say. Returning true cancels
        // the death: the player stays alive at 0 health while the countdown runs.
        internal static bool TryDelayDeath(Player player, double damage, int hitDirection, bool pvp, PlayerDeathReason reason)
        {
            if (!IsOwnersGame(player) || finalDeath)
                return false; // the final, allowed death goes through

            if (IsCountingDown)
                return true;  // already counting: nothing can kill you now

            blastDamage = BlastDamage(player);
            savedReason = reason;
            savedDamage = damage;
            savedHitDirection = hitDirection;
            savedPvp = pvp;
            pain.Clear();
            countdownLeft = CountdownTicks;

            player.statLife = 0;
            player.immune = true;
            player.immuneTime = Math.Max(player.immuneTime, 2);

            Projectile.NewProjectile(
                player.GetSource_FromThis(),
                player.Center,
                Vector2.Zero,
                ModContent.ProjectileType<GoingOutMarkerProjectile>(),
                0,
                0f,
                player.whoAmI);

            return true;
        }

        public override void OnUpdate(Player player)
        {
            if (!IsOwnersGame(player))
                return;

            if (!IsCountingDown)
            {
                if (!player.dead)
                    Prune();
                return;
            }

            if (player.dead)
            {
                // Something else killed the player outright; drop the countdown.
                Clear();
                return;
            }

            // Alive, but on 0 health and untouchable until the blast has gone off.
            player.statLife = 0;
            player.immune = true;
            player.immuneTime = Math.Max(player.immuneTime, 2);

            if (countdownLeft > 0)
            {
                countdownLeft--;
                if (countdownLeft == 0)
                {
                    Projectile.NewProjectile(
                        player.GetSource_FromThis(),
                        player.Center,
                        Vector2.Zero,
                        ModContent.ProjectileType<GoingOutBlastProjectile>(),
                        blastDamage,
                        14f,
                        player.whoAmI);
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

        public override void OnKill(Player player)
        {
            if (IsOwnersGame(player))
                Clear();
        }

        // Shows the multiplier you would deal if you died now (3 to 20).
        public override int? StatusValue
        {
            get
            {
                Player p = LocalPlayerState.Player;
                if (p == null || !p.active || p.dead)
                    return null;

                return (int)Math.Floor(Multiplier(p));
            }
        }

        public override Color StatusValueColor =>
            StatusValue >= MaxMultiplier ? new Color(240, 60, 45) : Color.White;
    }
}
