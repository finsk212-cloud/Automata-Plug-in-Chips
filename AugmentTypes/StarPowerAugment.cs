using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace Augments
{
    // Picking up a Mana Star makes you rainbow for a few seconds: faster, and enemies you touch get hurt.
    //
    // How it works:
    //  * The owner's game notices the pickup (GlobalItem.OnPickup runs on the local client only) and spawns a
    //    "star aura" projectile that follows the player. The projectile is what hurts enemies, so contact damage
    //    gets normal kill credit, damage numbers and multiplayer sync for free.
    //  * The aura counts its own time down in ai[0], which is synced, so every client agrees on when it ends.
    //  * The aura also tells every client "this player is star-powered right now", which is what the movement
    //    speed bonus reads.
    public class StarPowerAugment : Augment
    {
        public const int DurationTicks = 360;        // 6 seconds
        public const float SpeedBonus = 0.25f;
        public const int BaseTouchDamage = 18;       // run through AugmentScaling.Scale
        public const int TouchCooldownTicks = 30;    // each enemy can be hurt once per 0.5 seconds

        public override string Id => "star_power";
        public override string DisplayName => "Star Power";
        public override string Description =>
            $"Picking up a Mana Star gives you {AugmentText.Trigger("Star Power")} for {AugmentText.Duration("6 seconds")}. " +
            $"{AugmentText.Trigger("Star Power")}: gives {AugmentText.MovementSpeed("+25% movement speed")}, " +
            $"{AugmentText.Defense("immunity to contact damage")}, and enemies you touch take damage.";

        public override string GetDetailedDescription(Player player) =>
            AugmentScaling.ApplyToText(Description + "\n" +
            AugmentText.Note($"(Touching an enemy deals {BaseTouchDamage} damage, at most once every 0.5 seconds per enemy, and scales with game progression. " +
            "The immunity is only for enemies' bodies bumping into you: enemy projectiles, lava, falling and other damage still hurt. " +
            "Picking up another star refreshes the timer. Fallen stars, soul cakes and sugar plums all count. Mario would be proud.)"),
            GetScalingEntries());

        public override (string label, int baseValue)[] GetScalingEntries()
        {
            return new[] { ("Touch damage", BaseTouchDamage) };
        }

        public override AugmentRarity Rarity => AugmentRarity.Common;
        public override AugmentClass Class => AugmentClass.Magic;
        public override Texture2D Icon => TextureAssets.Item[ItemID.FallenStar].Value;

        // The tick each player's aura last confirmed it was alive. "Active" means it did so within the last couple of ticks.
        private static readonly uint[] lastActiveTick = new uint[Main.maxPlayers + 1];

        public static void MarkActive(int playerIndex)
        {
            if (playerIndex >= 0 && playerIndex < lastActiveTick.Length)
                lastActiveTick[playerIndex] = Main.GameUpdateCount;
        }

        public static bool IsActive(Player player)
        {
            if (player == null || player.whoAmI < 0 || player.whoAmI >= lastActiveTick.Length)
                return false;

            uint last = lastActiveTick[player.whoAmI];
            return last != 0 && Main.GameUpdateCount - last <= 2;
        }

        public override void PostUpdateRunSpeeds(Player player)
        {
            if (!IsActive(player))
                return;

            player.maxRunSpeed *= 1f + SpeedBonus;
            player.accRunSpeed *= 1f + SpeedBonus;
            player.runAcceleration *= 1f + SpeedBonus;
        }

        // Star Power makes you immune to bumping into enemies. A "contact" hit is an enemy's body hitting you:
        // it names an enemy as the source but no projectile (enemy projectiles name both).
        public override bool ImmuneTo(Player player, PlayerDeathReason damageSource, int cooldownCounter, bool dodgeable)
        {
            return IsActive(player) && damageSource.SourceNPCIndex >= 0 && damageSource.SourceProjectileLocalIndex < 0;
        }

        public override void OnKill(Player player)
        {
            if (player.whoAmI >= 0 && player.whoAmI < lastActiveTick.Length)
                lastActiveTick[player.whoAmI] = 0;
        }

        // Called by the pickup hook below, on the picker's own game.
        public static void Trigger(Player player)
        {
            if (player.whoAmI != Main.myPlayer || player.dead)
                return;

            int auraType = ModContent.ProjectileType<StarPowerAuraProjectile>();

            // Already star-powered: just refresh the timer on the existing aura.
            foreach (Projectile p in Main.projectile)
            {
                if (p.active && p.type == auraType && p.owner == player.whoAmI)
                {
                    p.ai[0] = DurationTicks;
                    p.netUpdate = true;
                    // Set from outside the projectile's own update, so send it now instead of trusting netUpdate: without
                    // this, other players' copy would run out on the old timer. The aura plays the refresh sound itself,
                    // on every game, when it notices its timer jump up.
                    if (Main.netMode == NetmodeID.MultiplayerClient)
                        NetMessage.SendData(MessageID.SyncProjectile, -1, -1, null, p.whoAmI);
                    return;
                }
            }

            Projectile.NewProjectile(
                player.GetSource_FromThis(),
                player.Center,
                Vector2.Zero,
                auraType,
                AugmentScaling.Scale(BaseTouchDamage),
                3f,
                player.whoAmI,
                DurationTicks);
            // The start sound is played by the aura projectile on its first tick, on every game.
        }
    }

    // Notices Mana Stars (and the holiday items that act like them) being picked up.
    public class StarPowerGlobalItem : GlobalItem
    {
        public override bool OnPickup(Item item, Player player)
        {
            if (player.whoAmI == Main.myPlayer
                && (item.type == ItemID.Star || item.type == ItemID.SoulCake || item.type == ItemID.SugarPlum)
                && player.GetModPlayer<AugmentPlayer>().HasAugment("star_power"))
            {
                StarPowerAugment.Trigger(player);
            }

            return true;
        }
    }
}
