using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace Augments
{
    // Good boy. Items dropped by an enemy your summons killed fly straight to you.
    //
    // How it works (and why it is built this way):
    //  * Loot is spawned by the server, but the "my minion killed this" marker only exists on the killer's own
    //    game (hit hooks run there). So the kill hook runs on the killer's client (RunsKillHookOnKillerClient),
    //    exactly like Swarm Tactics, and opens a short "recent kill" window around the dead enemy.
    //  * FetchGlobalItem then flags any freshly arrived item inside that window, locally. Flagged items get a
    //    huge grab range for the local player only, so vanilla's own item magnet reels them in and picks them up.
    //  * Nothing here is sent over the network; each player's flags only exist on their own game.
    public class FetchAugment : Augment
    {
        // How long after the kill (ticks) the window stays open. Covers the drop arriving a little before or after
        // the kill credit in multiplayer.
        public const int WindowTicks = 120;
        // Only items that appeared this recently (ticks since this game first saw them) can be claimed.
        public const int FreshItemTicks = 45;
        // Margin around the dead enemy's hitbox that still counts as "dropped by it", in pixels.
        public const int WindowMargin = 96;
        // Grab range given to flagged items, in pixels (more than a screen in every direction).
        public const int GrabRangePixels = 6000;

        public override string Id => "fetch";
        public override bool RunsKillHookOnKillerClient => true;
        public override string DisplayName => "Fetch";
        public override string Description =>
            $"Items dropped by enemies your {AugmentText.Trigger("summons kill")} fly straight to you.";

        public override string GetDetailedDescription(Player player) =>
            Description + "\n" +
            AugmentText.Note("(Minions, sentries and whips all count. If your last hit was with another weapon, the drops stay where they fall. " +
            "In multiplayer the game decides who an item belongs to, so if a teammate is standing much closer to the drop, they may pick it up first.)");

        public override AugmentRarity Rarity => AugmentRarity.Common;
        public override AugmentClass Class => AugmentClass.Summon;
        public override Texture2D Icon => TextureAssets.Item[ItemID.Bone].Value;

        // Every summon hit marks the enemy as "last hit by this player's summons". A hit with anything else
        // from the same player clears it again, so only kills finished by a summon (or by a debuff a summon
        // applied) count.
        public override void OnHitNPCWithItem(Player player, Item item, NPC target, NPC.HitInfo hit)
        {
            target.GetGlobalNPC<FetchNPC>().Mark(player.whoAmI, item.CountsAsClass(DamageClass.Summon));
        }

        public override void OnHitNPCWithProj(Player player, Projectile proj, NPC target, NPC.HitInfo hit)
        {
            target.GetGlobalNPC<FetchNPC>().Mark(player.whoAmI, proj.CountsAsClass(DamageClass.Summon));
        }

        public override void OnKillNPC(Player player, NPC npc)
        {
            // Items are only reeled in by their owner's own game.
            if (player.whoAmI != Main.myPlayer || Main.dedServ)
                return;

            if (!npc.GetGlobalNPC<FetchNPC>().IsSummonKillBy(player.whoAmI))
                return;

            FetchTracker.OpenWindow(npc.Hitbox);
        }

        public override void OnUpdate(Player player)
        {
            if (player.whoAmI == Main.myPlayer)
                FetchTracker.Tick();
        }
    }

    // Remembers, per enemy, whether the last hit from a given player came from their summons.
    public class FetchNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        private int summonOwner = -1;

        public void Mark(int playerIndex, bool bySummon)
        {
            if (bySummon)
                summonOwner = playerIndex;
            else if (summonOwner == playerIndex)
                summonOwner = -1;
        }

        public bool IsSummonKillBy(int playerIndex) => summonOwner == playerIndex;
    }

    // The "an enemy of mine just died here" windows, local to this game.
    internal static class FetchTracker
    {
        private struct Window
        {
            public Rectangle Area;
            public int TicksLeft;
        }

        private static readonly List<Window> windows = new();

        public static bool HasWindows => windows.Count > 0;

        public static void OpenWindow(Rectangle enemyHitbox)
        {
            enemyHitbox.Inflate(FetchAugment.WindowMargin, FetchAugment.WindowMargin);
            windows.Add(new Window { Area = enemyHitbox, TicksLeft = FetchAugment.WindowTicks });

            // A big fight can kill many enemies in a second; keep only the newest windows.
            if (windows.Count > 24)
                windows.RemoveAt(0);
        }

        public static void Tick()
        {
            for (int i = windows.Count - 1; i >= 0; i--)
            {
                Window w = windows[i];
                w.TicksLeft--;
                if (w.TicksLeft <= 0)
                    windows.RemoveAt(i);
                else
                    windows[i] = w;
            }
        }

        public static bool Claims(Vector2 position)
        {
            foreach (Window w in windows)
            {
                if (w.Area.Contains((int)position.X, (int)position.Y))
                    return true;
            }

            return false;
        }

        public static void Clear() => windows.Clear();
    }

    public class FetchGlobalItem : GlobalItem
    {
        public override bool InstancePerEntity => true;

        // Local only, never synced: this game's own "reel this one in" flag.
        private bool fetched;
        // Ticks since this game first updated the item. Not Item.timeSinceItemSpawned, which spawn protection rewrites.
        private int age;

        public override void Update(Item item, ref float gravity, ref float maxFallSpeed)
        {
            if (Main.dedServ)
                return;

            // Already flagged: vanilla's magnet takes it from here (and does not even call this hook
            // while a player is actually grabbing the item).
            if (fetched)
                return;

            if (age < FetchAugment.FreshItemTicks + 1)
                age++;

            if (age > FetchAugment.FreshItemTicks || !FetchTracker.HasWindows)
                return;

            Player local = Main.LocalPlayer;
            if (local == null || !local.active || local.dead || !local.GetModPlayer<AugmentPlayer>().HasAugment("fetch"))
                return;

            if (FetchTracker.Claims(item.Center))
                fetched = true;
        }

        // Once an item is in a player's inventory it is no longer loot. Without these two resets, the flag
        // would ride along into the inventory and the item would boomerang back if you threw it away later.
        public override bool OnPickup(Item item, Player player)
        {
            fetched = false;
            age = FetchAugment.FreshItemTicks + 1;
            return true;
        }

        public override GlobalItem Clone(Item from, Item to)
        {
            var clone = (FetchGlobalItem)base.Clone(from, to);
            clone.fetched = false;
            clone.age = FetchAugment.FreshItemTicks + 1;
            return clone;
        }

        public override void GrabRange(Item item, Player player, ref int grabRange)
        {
            if (!fetched || player.whoAmI != Main.myPlayer)
                return;

            if (!player.GetModPlayer<AugmentPlayer>().HasAugment("fetch"))
                return;

            grabRange = Math.Max(grabRange, FetchAugment.GrabRangePixels);
        }
    }
}
