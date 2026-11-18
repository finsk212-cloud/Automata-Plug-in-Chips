using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using Augments.Core;

namespace Augments
{
    // Common, Universal. While a fishing pole is in hand:
    //   1. Auto reel: the moment a fish is on the line, a click is pressed for the player, which makes the game
    //      reel in and hand over the catch exactly as if they had clicked.
    //   2. Auto recast: once a line that was reeled in this way has come back, another click is pressed, so the
    //      pole casts again toward the cursor.
    // Fish still take as long to bite as usual. It all runs on the owner's own game by pressing the same "use item"
    // control a player would, so the game's normal fishing code (bait, catches, syncing) does everything else.
    // Plugin id stays neutral ("auto_fishing") because ids are saved with characters.
    public class InstaCastAugment : Augment
    {
        public override string Id => "auto_fishing";
        public override string DisplayName => "Insta Cast";
        public override string Description =>
            $"While you hold a fishing pole, the line {AugmentText.Trigger("reels in")} by itself the moment a fish bites, " +
            $"and then it {AugmentText.Trigger("casts again")} toward your cursor.";

        public override string GetDetailedDescription(Player player) =>
            Description + "\n" +
            AugmentText.Note("(Fish still take as long to bite as usual. Bait is used up as usual, and your fishing power still decides what you catch. It keeps going until you put the pole away or aim off the water. Reel in by hand and it will not cast again for you.)");

        public override AugmentRarity Rarity => AugmentRarity.Common;
        public override AugmentClass Class => AugmentClass.Universal;

        private const int RecastDelayTicks = 12;        // a short pause after the line is back before it casts again

        // True once this plugin has reeled a catch in, until it has cast again. Only the owner's game uses it.
        private static bool recastArmed;
        private static int recastWait;

        private static Projectile FindBobber(Player player)
        {
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile p = Main.projectile[i];
                if (p.active && p.owner == player.whoAmI && p.bobber)
                    return p;
            }

            return null;
        }

        // The same "use item" press a player makes with the mouse.
        private static void Press(Player player)
        {
            player.controlUseItem = true;
            player.releaseUseItem = true;
        }

        public override void OnAcquire(Player player)
        {
            recastArmed = false;
            recastWait = 0;
        }

        // Called every tick from AugmentPlayer.SetControls, after the player's own controls are read and before
        // the game uses the held item.
        internal static void ApplyControls(Player player)
        {
            if (Main.dedServ || player.whoAmI != Main.myPlayer || player.dead || player.CCed || player.noItems)
                return;

            // Never press anything while a menu, chat or the plugin screens are open.
            if (Main.playerInventory || Main.drawingPlayerChat || Main.gameMenu || !Main.hasFocus
                || ModContent.GetInstance<AugmentUISystem>()?.IsPlayerInputBlocked() == true)
                return;

            Item held = player.HeldItem;
            if (held == null || held.IsAir || held.fishingPole <= 0)
            {
                recastArmed = false;
                return;
            }

            Projectile bobber = FindBobber(player);
            if (bobber != null)
            {
                recastWait = RecastDelayTicks;

                // Only while it floats; once it is being reeled in (ai[0] >= 1) it is left alone.
                // A negative ai[1] with a catch waiting in localAI[1] means a fish is on the line.
                if (bobber.ai[0] == 0f && bobber.ai[1] < 0f && bobber.localAI[1] != 0f)
                {
                    // Click to reel it in, and remember to cast again afterwards.
                    Press(player);
                    recastArmed = true;
                }

                return;
            }

            // No line out. If this plugin just reeled one in, cast again once it has been gone a moment.
            if (!recastArmed)
                return;

            if (recastWait > 0)
            {
                recastWait--;
                return;
            }

            if (player.itemAnimation == 0 && player.itemTime == 0)
            {
                Press(player);
                recastArmed = false;
            }
        }
    }
}
