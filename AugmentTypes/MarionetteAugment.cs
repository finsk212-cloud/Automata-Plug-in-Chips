using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Augments.Core;

namespace Augments
{
    // Legendary Summoner: the player's minions hover around the mouse cursor instead of the player,
    // and attack whatever enemy is near it. Everything here runs on the owner's own game only: the
    // cursor only exists there, and the minions' AI is owner-authoritative (positions reach other
    // clients through normal projectile sync).
    public class MarionetteAugment : Augment
    {
        public override string Id => "marionette";
        public override string DisplayName => "Marionette";
        public override string Description =>
            $"Your {AugmentText.Trigger("minions")} follow your {AugmentText.Trigger("mouse cursor")} instead of you and attack enemies near it. " +
            $"Minions deal {AugmentText.BonusDamage("+15% damage")}.";

        public override string GetDetailedDescription(Player player) =>
            Description + "\n" +
            AugmentText.Note("(Minions hover in a ring around the cursor. Put the cursor on an enemy and they converge on it; sentries and minion shots are unaffected.)");

        public override AugmentRarity Rarity => AugmentRarity.Legendary;
        public override AugmentClass Class => AugmentClass.Summon;

        public const float MinionDamageBonus = 0.15f;

        // Enemies this close to the cursor pull the minions in; once chosen, the same enemy is
        // kept until the cursor moves this far from it (so the target doesn't flicker).
        private const float PickRadius = 280f;
        private const float KeepRadius = 420f;

        // Rebuilt every tick on the local player: which minion sits in which ring slot.
        private static readonly Dictionary<int, int> slotByProjectile = new();
        private static int slotCount;
        private static bool cursorHasTarget;
        private static bool weSetTarget;

        internal static bool CursorHasTarget => cursorHasTarget;

        internal static bool TryGetSlot(Projectile projectile, out int index, out int count)
        {
            count = slotCount;
            return slotByProjectile.TryGetValue(projectile.whoAmI, out index);
        }

        // Whole minions only: body segments (e.g. worm-style minions) take 0 slots and follow their head.
        internal static bool IsSteerable(Projectile p, int owner) =>
            p.active && p.owner == owner && p.minion && !p.sentry && p.minionSlots > 0f;

        public override void OnAcquire(Player player) => Reset(player);

        private static void Reset(Player player)
        {
            slotByProjectile.Clear();
            slotCount = 0;
            cursorHasTarget = false;
            weSetTarget = false;
        }

        public override void OnUpdate(Player player)
        {
            if (Main.dedServ || player.whoAmI != Main.myPlayer)
                return;

            if (player.dead)
            {
                Reset(player);
                return;
            }

            slotByProjectile.Clear();
            slotCount = 0;
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                if (IsSteerable(Main.projectile[i], player.whoAmI))
                    slotByProjectile[i] = slotCount++;
            }

            // Point vanilla minion targeting at the enemy nearest the cursor.
            Vector2 cursor = Main.MouseWorld;
            NPC current = player.MinionAttackTargetNPC >= 0 && player.MinionAttackTargetNPC < Main.maxNPCs
                ? Main.npc[player.MinionAttackTargetNPC] : null;

            if (current != null && current.CanBeChasedBy() && Vector2.Distance(current.Center, cursor) < KeepRadius)
            {
                cursorHasTarget = true;
                return;
            }

            NPC best = null;
            float bestDist = PickRadius;
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (!npc.active || !npc.CanBeChasedBy())
                    continue;

                float dist = Vector2.Distance(npc.Center, cursor);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = npc;
                }
            }

            if (best != null)
            {
                player.MinionAttackTargetNPC = best.whoAmI;
                weSetTarget = true;
                cursorHasTarget = true;
            }
            else
            {
                if (weSetTarget)
                    player.MinionAttackTargetNPC = -1;
                weSetTarget = false;
                cursorHasTarget = false;
            }
        }

        public override void ModifyHitNPCWithProj(Player player, Projectile proj, NPC target, ref NPC.HitModifiers modifiers, AugmentHitSource source, float effectiveness)
        {
            if (proj.minion && !proj.sentry)
                modifiers.SourceDamage += MinionDamageBonus;
        }

        // Where a minion should idle: a slowly turning ring around the cursor.
        internal static Vector2 SlotPosition(Vector2 cursor, int index, int count)
        {
            count = Math.Max(1, count);
            float radius = count == 1 ? 0f : Math.Min(36f + count * 6f, 120f);
            float angle = (float)Main.GlobalTimeWrappedHourly * 0.9f + index * (MathHelper.TwoPi / count);
            float bob = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 3f + index * 1.3f) * 5f;
            return cursor + new Vector2((float)Math.Cos(angle) * radius, (float)Math.Sin(angle) * radius + bob);
        }
    }

    public class MarionetteGlobalProjectile : GlobalProjectile
    {
        public override bool InstancePerEntity => false;

        public override void PostAI(Projectile projectile)
        {
            if (Main.dedServ || !projectile.active || !projectile.minion || projectile.owner != Main.myPlayer)
                return;

            // An enemy near the cursor: let the minion's own attack AI deal with it.
            if (MarionetteAugment.CursorHasTarget)
                return;

            Player player = Main.player[projectile.owner];
            if (!player.active || player.dead || !player.GetModPlayer<AugmentPlayer>().HasAugment("marionette"))
                return;

            if (!MarionetteAugment.TryGetSlot(projectile, out int index, out int count))
                return;

            Vector2 slot = MarionetteAugment.SlotPosition(Main.MouseWorld, index, count);
            Vector2 toSlot = slot - projectile.Center;
            float dist = toSlot.Length();

            // Ease in: fast when far, gentle when close.
            float speed = Math.Clamp(dist * 0.15f, 0f, 20f);
            Vector2 desired = dist > 1f ? toSlot / dist * speed : Vector2.Zero;

            projectile.tileCollide = false;
            projectile.velocity = Vector2.Lerp(projectile.velocity, desired, 0.3f);

            if (Math.Abs(projectile.velocity.X) > 0.5f)
                projectile.spriteDirection = projectile.direction = projectile.velocity.X > 0f ? 1 : -1;

            // Other clients only see the minion through projectile sync.
            if (Main.netMode == NetmodeID.MultiplayerClient && Main.GameUpdateCount % 5 == 0)
                projectile.netUpdate = true;
        }
    }
}
