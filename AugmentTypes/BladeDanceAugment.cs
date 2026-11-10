using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Augments.Core;

namespace Augments
{
    public class BladeDanceAugment : Augment
    {
        // Tuning
        private const int Blinks = 8;
        private const int BlinkIntervalTicks = 15;                     // 8 blinks x 15 ticks = the full 2s
        private const int DurationTicks = Blinks * BlinkIntervalTicks; // 120 ticks = 2s
        private const int CooldownTicks = 3000;                        // 50s
        private const int BaseSlashDamage = 90;                        // run through AugmentScaling.Scale
        private const float SearchRadius = 600f;

        public override string Id => "blade_dance";
        public override string DisplayName => "Blade Dance";
        public override string Description =>
            $"{AugmentText.Active("Active:")} Press [{AugmentText.Key(Augments.BladeDanceKeybind)}] with a {AugmentText.Trigger("melee weapon")} in hand to become " +
            $"{AugmentText.Defense("invulnerable")} for {AugmentText.Duration("2 seconds")} while you blink between nearby enemies, " +
            $"cutting each one you reach for {AugmentText.SpecialDamage($"{AugmentScaling.Scale(BaseSlashDamage)} damage")} ({Blinks} slashes). " +
            $"{AugmentText.Cooldown("50s cooldown")}.";

        public override string GetDetailedDescription(Player player) =>
            Description + "\n" +
            AugmentText.Note("(Nothing happens, and no cooldown is spent, if no enemy is in range.)\n" +
            "(The slash damage is fixed: it does not depend on your weapon, but does scale with game progression. Your melee crit chance still applies.)");

        public override AugmentRarity Rarity => AugmentRarity.Legendary;
        public override AugmentClass Class => AugmentClass.Melee;
        public override ModKeybind ActiveModKeybind => Augments.BladeDanceKeybind;
        public override int CooldownRemaining => GetCooldown(LocalPlayerState.Player);

        // All of this runs on the owner's own game only (the teleport, the invulnerability and the
        // slash projectiles are all owner-authoritative in Terraria), so plain per-player state is enough.
        private static readonly Dictionary<int, int> cooldownTimer = new();
        private static readonly Dictionary<int, int> danceTimer = new();
        private static readonly Dictionary<int, int> blinksRemaining = new();
        private static readonly Dictionary<int, bool> danceActive = new();
        private static readonly Dictionary<int, int> slashDamage = new();
        private static readonly Dictionary<int, int> slashCrit = new();
        private static readonly Dictionary<int, HashSet<int>> cutThisRound = new();

        public static int GetCooldown(Player player) =>
            player != null && cooldownTimer.TryGetValue(player.whoAmI, out int cd) ? cd : 0;

        public static bool IsDancing(Player player) =>
            player != null && danceActive.TryGetValue(player.whoAmI, out bool a) && a;

        public static void ResetCooldown(Player player)
        {
            if (player != null)
                cooldownTimer[player.whoAmI] = 0;
        }

        public override void OnAcquire(Player player)
        {
            int who = player.whoAmI;
            cooldownTimer[who] = 0;
            danceActive[who] = false;
        }

        private static bool HoldingMeleeWeapon(Player player)
        {
            Item item = player.HeldItem;
            return item != null && !item.IsAir && item.damage > 0 && item.CountsAsClass(DamageClass.Melee)
                && item.pick == 0 && item.axe == 0 && item.hammer == 0;
        }

        // Called from AugmentPlayer.ProcessTriggers when the keybind is pressed.
        public static void TryActivate(Player player)
        {
            if (player.whoAmI != Main.myPlayer || player.dead || IsDancing(player) || GetCooldown(player) > 0)
                return;

            if (!HoldingMeleeWeapon(player))
            {
                Main.NewText("Blade Dance needs a melee weapon in hand.", 255, 100, 100);
                return;
            }

            if (FindTarget(player, player.Center, null) == null)
            {
                Main.NewText("Blade Dance: no enemies in range.", 255, 100, 100);
                return;
            }

            int who = player.whoAmI;
            danceActive[who] = true;
            danceTimer[who] = 0;
            blinksRemaining[who] = Blinks;
            slashDamage[who] = AugmentScaling.Scale(BaseSlashDamage);
            slashCrit[who] = (int)player.GetTotalCritChance(DamageClass.Melee);
            cutThisRound[who] = new HashSet<int>();

            SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.65f, Pitch = 0.4f }, player.Center);
            for (int i = 0; i < 18; i++)
            {
                Vector2 vel = Main.rand.NextVector2Circular(4.5f, 4.5f);
                int dType = Main.rand.NextBool() ? DustID.CrimsonTorch : DustID.SilverCoin;
                Dust d = Dust.NewDustPerfect(player.Center, dType, vel, 80, default, 1.3f);
                d.noGravity = true;
            }
        }

        public override void OnUpdate(Player player)
        {
            int who = player.whoAmI;

            if (player.dead || !player.active)
            {
                danceActive[who] = false;
                return;
            }

            if (cooldownTimer.TryGetValue(who, out int cd) && cd > 0)
                cooldownTimer[who] = cd - 1;

            if (!IsDancing(player) || who != Main.myPlayer)
                return;

            // Untouchable and hovering for the whole dance.
            player.immune = true;
            player.immuneTime = Math.Max(player.immuneTime, 6);
            player.immuneNoBlink = true;
            player.velocity = Vector2.Zero;
            player.fallStart = (int)(player.position.Y / 16f);

            if (Main.rand.NextBool(2))
            {
                int dType = Main.rand.NextBool() ? DustID.CrimsonTorch : DustID.SilverCoin;
                Dust d = Dust.NewDustDirect(player.position, player.width, player.height, dType, 0f, 0f, 100, default, 1.2f);
                d.noGravity = true;
                d.velocity *= 0.2f;
            }

            int timer = danceTimer.TryGetValue(who, out int t) ? t : 0;

            if (timer % BlinkIntervalTicks == 0 && blinksRemaining.TryGetValue(who, out int left) && left > 0)
            {
                if (!Blink(player))
                {
                    EndDance(player); // nobody left to cut
                    return;
                }
                blinksRemaining[who] = left - 1;
            }

            danceTimer[who] = timer + 1;
            if (timer + 1 >= DurationTicks)
                EndDance(player);
        }

        private static void EndDance(Player player)
        {
            int who = player.whoAmI;
            danceActive[who] = false;
            blinksRemaining[who] = 0;
            cooldownTimer[who] = CooldownTicks;
            player.immuneTime = Math.Min(player.immuneTime, 2);
            SoundEngine.PlaySound(SoundID.Item105 with { Volume = 0.6f, Pitch = 0.2f }, player.Center);
        }

        // Nearest living hostile to 'from' within range, preferring ones not yet cut this round.
        private static NPC FindTarget(Player player, Vector2 from, HashSet<int> alreadyCut)
        {
            NPC best = null;
            NPC bestAny = null;
            float bestDistSq = SearchRadius * SearchRadius;
            float bestAnyDistSq = bestDistSq;

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.friendly || npc.dontTakeDamage || npc.life <= 0 || npc.immortal || npc.CountsAsACritter)
                    continue;

                float distSq = Vector2.DistanceSquared(from, npc.Center);
                if (distSq >= bestAnyDistSq)
                    continue;

                bestAnyDistSq = distSq;
                bestAny = npc;

                if (alreadyCut != null && alreadyCut.Contains(i))
                    continue;
                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    best = npc;
                }
            }

            return best ?? bestAny;
        }

        // One blink: jump beside the next enemy and slash it. Returns false if there is nothing to hit.
        private static bool Blink(Player player)
        {
            int who = player.whoAmI;
            HashSet<int> cut = cutThisRound[who];

            NPC target = FindTarget(player, player.Center, cut);
            if (target == null)
                return false;

            // Everything in range has been cut once: start another round.
            if (cut.Contains(target.whoAmI))
                cut.Clear();
            cut.Add(target.whoAmI);

            Vector2 from = player.Center;
            MoveBesideTarget(player, target);
            Vector2 to = player.Center;

            // Trail between the two spots.
            int steps = Math.Clamp((int)(Vector2.Distance(from, to) / 24f), 2, 40);
            for (int i = 0; i <= steps; i++)
            {
                int dType = Main.rand.NextBool() ? DustID.CrimsonTorch : DustID.SilverCoin;
                Dust d = Dust.NewDustPerfect(Vector2.Lerp(from, to, i / (float)steps), dType, Main.rand.NextVector2Circular(0.8f, 0.8f), 100, default, 1.1f);
                d.noGravity = true;
            }

            Vector2 toward = (target.Center - player.Center).SafeNormalize(Vector2.UnitX);
            float angle = toward.ToRotation() + Main.rand.NextFloat(-0.6f, 0.6f);
            Vector2 dir = angle.ToRotationVector2();

            int projId = Projectile.NewProjectile(
                player.GetSource_FromThis(),
                target.Center - dir * 45f,
                dir * 18f,
                ModContent.ProjectileType<BladeDanceSlashProjectile>(),
                slashDamage.TryGetValue(who, out int dmg) ? dmg : 50,
                5.5f,
                who);

            if (projId >= 0 && projId < Main.maxProjectiles)
                Main.projectile[projId].CritChance = slashCrit.TryGetValue(who, out int crit) ? crit : 4;

            SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.70f, Pitch = Main.rand.NextFloat(-0.1f, 0.3f) }, target.Center);
            return true;
        }

        // Puts the player beside the target (left or right, then above) in a spot that isn't inside tiles.
        // If every spot is blocked the player stays put and the slash still lands.
        private static void MoveBesideTarget(Player player, NPC target)
        {
            float side = target.width / 2f + player.width / 2f + 10f;
            bool rightFirst = Main.rand.NextBool();
            Vector2[] offsets =
            {
                new Vector2(rightFirst ? side : -side, 0f),
                new Vector2(rightFirst ? -side : side, 0f),
                new Vector2(0f, -(target.height / 2f + player.height / 2f + 10f)),
            };

            foreach (Vector2 offset in offsets)
            {
                Vector2 center = target.Center + offset;
                Vector2 topLeft = center - new Vector2(player.width / 2f, player.height / 2f);
                if (Collision.SolidCollision(topLeft, player.width, player.height))
                    continue;

                player.position = topLeft;
                player.velocity = Vector2.Zero;
                player.direction = target.Center.X >= center.X ? 1 : -1;
                player.RemoveAllGrapplingHooks();
                player.fallStart = (int)(player.position.Y / 16f);
                return;
            }
        }
    }
}
