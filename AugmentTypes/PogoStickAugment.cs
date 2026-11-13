using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Augments.Core;

namespace Augments
{
    // Two parts. The movement trick: fall onto an enemy and you stomp it (a melee hit) and bounce off, with your jump
    // refreshed. The reward: every kill you make in a row without touching the ground, by stomp or by swing, adds a stack
    // of melee damage.
    //
    // All of it is the attacker's own game (the stomp check, velocity and the streak live there). Other players see the
    // bounce puff, the sound and the streak number through the relay (PluginEffects.PogoBounce).
    public class PogoStickAugment : Augment
    {
        // Tuning
        private const float BounceSpeed = 9.5f;            // upward speed after a bounce
        private const float StackBonus = 0.08f;            // +8% melee damage per stack
        private const int MaxStacks = 10;
        private const int BounceCooldownTicks = 12;        // at most one stomp every 0.2 seconds
        private const int StreakTimeoutTicks = 240;        // 4 seconds without a kill ends the streak (so flying forever can't hold it)
        private const float StompDamageMultiplier = 1.5f;  // of your held melee weapon's damage
        private const int FallbackStompDamage = 40;        // used when you aren't holding a melee weapon (run through AugmentScaling.Scale)
        private const int StompImmunityTicks = 12;         // a short grace so the enemy you land on can't hit you back that instant
        private const float StompSideMargin = 24f;         // how far beyond the player's width, on each side, a near miss still counts
        private const float StompReachDown = 14f;          // how far below the feet the stomp box reaches (it triggers just before touching)
        private const float StompDepthFraction = 0.75f;    // the feet must be above this fraction of the enemy's height, counted from its top

        public override string Id => "pogo_stick";
        public override string DisplayName => "Pogo Stick";
        public override string Description =>
            $"Falling onto an enemy {AugmentText.Trigger("stomps it")} and bounces you up.\n" +
            $"Stomp damage: {AugmentText.BonusDamage("150% of your melee weapon's damage")}, or {AugmentText.SpecialDamage($"{FallbackStompDamage} damage")} without one.\n" +
            $"Each kill in a row without landing: {AugmentText.BonusDamage("+8% melee damage")}, up to {AugmentText.Trigger("10 stacks")}.\n" +
            $"Stacks reset when you land, or after {AugmentText.Duration("4 seconds")} without a kill.";

        public override (string label, int baseValue)[] GetScalingEntries()
        {
            return new[] { ("Stomp damage without a melee weapon", FallbackStompDamage) };
        }

        public override string GetDetailedDescription(Player player) =>
            AugmentScaling.ApplyToText(Description + "\n" +
            AugmentText.Note("(The bounce also refreshes your jumps, wing flight and rocket boots. The stack bonus applies to stomps as well, and your stacks show as an icon next to your cooldowns. " +
            "The 4 second timer is there so wings and mounts can't hold the stacks forever, and every new kill restarts it. " +
            "Kills from normal melee swings while you are off the ground add stacks too, not only stomps.\n" +
            "A stomp needs you to be falling onto the top of an enemy. The area is generous: it reaches a good way past the sides of your feet and just below them, " +
            "and counts anywhere on the top three quarters of the enemy, so a near miss on its head still counts. " +
            "It is a flat hit, so it doesn't trigger other augments. The enemy you land on can't hurt you with its body (neither can others for a moment after a stomp), " +
            "but its attacks and projectiles still can. It doesn't work on critters or town NPCs, while grappling, or more than once every 0.2 seconds. Boing.)"),
            GetScalingEntries());

        public override AugmentRarity Rarity => AugmentRarity.Rare;
        public override AugmentClass Class => AugmentClass.Melee;
        public override Texture2D Icon => TextureAssets.Item[ItemID.PogoStick].Value;

        // Only the local player's own streak matters: hit hooks and the stomp check only ever run on the attacker's game.
        private static int streak;
        private static uint lastBounceTick;
        private static uint lastKillTick;
        private static int groundedTicks;
        private static readonly PerHitGuard guard = new();

        public override int? StatusValue => streak > 0 ? streak : null;
        public override Color StatusValueColor => new Color(255, 215, 90);
        public override string StatusValueSuffix => "x";

        public override void ModifyHitNPCWithItem(Player player, Item item, NPC target, ref NPC.HitModifiers modifiers)
        {
            if (streak > 0 && player.whoAmI == Main.myPlayer && item.CountsAsClass(DamageClass.Melee))
                modifiers.FinalDamage *= 1f + streak * StackBonus;
        }

        public override void ModifyHitNPCWithProj(Player player, Projectile proj, NPC target, ref NPC.HitModifiers modifiers)
        {
            if (streak > 0 && player.whoAmI == Main.myPlayer && proj.CountsAsClass(DamageClass.Melee))
                modifiers.FinalDamage *= 1f + streak * StackBonus;
        }

        public override void OnHitNPCWithItem(Player player, Item item, NPC target, NPC.HitInfo hit, AugmentHitSource source, float effectiveness)
        {
            if (source == AugmentHitSource.AugmentProc || !item.CountsAsClass(DamageClass.Melee))
                return;

            CountSwingKill(player, target, hit);
        }

        public override void OnHitNPCWithProj(Player player, Projectile proj, NPC target, NPC.HitInfo hit, AugmentHitSource source, float effectiveness)
        {
            if (source == AugmentHitSource.AugmentProc || !proj.CountsAsClass(DamageClass.Melee))
                return;

            CountSwingKill(player, target, hit);
        }

        // A kill from an ordinary melee swing, made while off the ground, adds a stack.
        private static void CountSwingKill(Player player, NPC target, NPC.HitInfo hit)
        {
            if (player.whoAmI != Main.myPlayer || player.dead)
                return;

            if (player.velocity.Y == 0f || player.grapCount > 0)
                return;

            // The same hit can reach this hook twice (Twin Strike on a crit): count it once.
            if (!guard.IsFirst(target.whoAmI))
                return;

            if (IsKill(target, hit))
                AddStack(player, false);
        }

        private static void AddStack(Player player, bool bounced)
        {
            streak = Math.Min(MaxStacks, streak + 1);
            lastKillTick = Main.GameUpdateCount;

            // A stomp already shows its own puff; a swing kill just shows the number.
            if (!bounced)
                PluginEffects.Show(PluginEffectId.PogoBounce, player, streak);
        }

        // A kill means the whole enemy (the head, for worms and bosses with segments) is out of life.
        private static bool IsKill(NPC target, NPC.HitInfo hit)
        {
            if (target.friendly || target.townNPC || target.CountsAsACritter || target.catchItem > 0 || target.SpawnedFromStatue)
                return false;

            NPC head = target.realLife >= 0 && target.realLife < Main.maxNPCs ? Main.npc[target.realLife] : target;
            return hit.InstantKill || !head.active || head.life <= 0;
        }

        public override void OnUpdate(Player player)
        {
            if (player.whoAmI != Main.myPlayer)
                return;

            TryStomp(player);

            // The streak ends when you stand on something for a few ticks in a row, or after a while without a kill. Only a
            // few ticks in a row, because at the very top of a jump the vertical speed passes through zero for a single tick.
            if (streak > 0)
            {
                groundedTicks = player.velocity.Y == 0f ? groundedTicks + 1 : 0;
                if (groundedTicks >= 3 || Main.GameUpdateCount - lastKillTick > StreakTimeoutTicks)
                {
                    streak = 0;
                    groundedTicks = 0;
                }
            }
            else
            {
                groundedTicks = 0;
            }
        }

        // Falling onto an enemy's head: stomp it and bounce.
        private void TryStomp(Player player)
        {
            if (player.dead || player.grapCount > 0 || player.velocity.Y <= 1.5f)
                return;

            if (Main.GameUpdateCount - lastBounceTick < BounceCooldownTicks)
                return;

            // A box at the player's feet that is deliberately generous, so a near miss still counts as a stomp: a good
            // way wider than the player on both sides, and reaching a little below the feet on top of the distance
            // they are about to fall this tick.
            Rectangle feet = new Rectangle(
                (int)(player.position.X - StompSideMargin),
                (int)(player.position.Y + player.height - 6f),
                player.width + (int)(StompSideMargin * 2f),
                12 + (int)player.velocity.Y + (int)StompReachDown);

            foreach (NPC npc in Main.npc)
            {
                if (!npc.active || npc.friendly || npc.townNPC || npc.dontTakeDamage || npc.immortal || npc.CountsAsACritter || npc.catchItem > 0)
                    continue;

                if (!feet.Intersects(npc.Hitbox) || !LandsOnTop(player, npc))
                    continue;

                Stomp(player, npc);
                return;
            }
        }

        // Coming down onto the top of the enemy: the feet are still above the lower quarter of it. Running into the side of
        // an enemy, or climbing out of its lower body, is not a stomp.
        private static bool LandsOnTop(Player player, NPC npc) =>
            player.Bottom.Y <= npc.Top.Y + npc.height * StompDepthFraction;

        private void Stomp(Player player, NPC npc)
        {
            lastBounceTick = Main.GameUpdateCount;

            // The bounce.
            player.velocity.Y = -BounceSpeed;
            player.fallStart = (int)(player.position.Y / 16f);
            player.RefreshExtraJumps();
            // Flight comes back too: wings and rocket boots, not only double-jump accessories.
            player.wingTime = player.wingTimeMax;
            player.rocketTime = player.rocketTimeMax;
            // A moment of grace, so the enemy you landed on can't hit you back in the same instant.
            player.immune = true;
            player.immuneTime = Math.Max(player.immuneTime, StompImmunityTicks);
            player.immuneNoBlink = true;

            // The stomp itself: a flat melee hit.
            Item held = player.HeldItem;
            int baseDamage = held != null && !held.IsAir && held.damage > 0 && held.CountsAsClass(DamageClass.Melee)
                ? player.GetWeaponDamage(held)
                : AugmentScaling.Scale(FallbackStompDamage);
            int damage = Math.Max(1, (int)Math.Round(baseDamage * StompDamageMultiplier * (1f + streak * StackBonus)));

            var stomp = new NPC.HitInfo
            {
                Damage = damage,
                SourceDamage = damage,
                HitDirection = player.direction,
                DamageType = DamageClass.Melee,
                Knockback = 0f
            };

            npc.lastInteraction = player.whoAmI;
            npc.StrikeNPC(stomp);
            if (Main.netMode != NetmodeID.SinglePlayer)
                NetMessage.SendStrikeNPC(npc, in stomp);

            AugmentDamageTracker.RecordChipHit(Id, damage, false);

            bool killed = IsKill(npc, stomp);
            if (killed)
                AddStack(player, true);

            // One puff and sound per stomp; the number only shows when it killed.
            PluginEffects.Show(PluginEffectId.PogoBounce, player, killed ? streak : 0);
        }

        // The enemy's contact check and our stomp check race each other: when you fall onto an enemy, its body hurts you in
        // the same moment you overlap it, before OnUpdate has had a chance to stomp. So the contact hit is caught here:
        // if it comes while you are falling onto the top of that enemy, it is the stomp (done right now) and the hit is
        // refused. For a short while after any stomp, contact hits are refused too (other enemies you land among).
        public override bool ImmuneTo(Player player, PlayerDeathReason damageSource, int cooldownCounter, bool dodgeable)
        {
            if (player.whoAmI != Main.myPlayer || player.dead)
                return false;

            // Only an enemy's body counts: it names an enemy as the source but no projectile.
            int npcIndex = damageSource.SourceNPCIndex;
            if (npcIndex < 0 || npcIndex >= Main.maxNPCs || damageSource.SourceProjectileLocalIndex >= 0)
                return false;

            if (Main.GameUpdateCount - lastBounceTick <= StompImmunityTicks)
                return true;

            NPC npc = Main.npc[npcIndex];
            bool fallingOnTop = player.velocity.Y > 1.5f
                && player.grapCount <= 0
                && !npc.friendly && !npc.townNPC && !npc.dontTakeDamage && !npc.immortal && !npc.CountsAsACritter && npc.catchItem <= 0
                && LandsOnTop(player, npc)
                && Main.GameUpdateCount - lastBounceTick >= BounceCooldownTicks;
            if (!fallingOnTop)
                return false;

            Stomp(player, npc);
            return true;
        }

        public override void OnKill(Player player)
        {
            if (player.whoAmI == Main.myPlayer)
                streak = 0;
        }
    }
}
