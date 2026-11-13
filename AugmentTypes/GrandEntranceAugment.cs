using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace Augments
{
    // Summoning a minion or sentry with a summon weapon calls a bolt of lightning down on the spot where it appears.
    //
    // The bolt is a real projectile (GrandEntranceBoltProjectile), so it is synced like any other: the owner's game deals the
    // damage, and every game draws the bolt and plays the sound from the projectile's own first tick. Nothing needs relaying.
    public class GrandEntranceAugment : Augment
    {
        // Tuning
        public const float DamageFraction = 0.5f;   // the bolt deals this share of the new minion's damage
        public const float BoltKnockback = 10f;
        private const int MinTicksBetweenBolts = 20;   // one weapon use that spawns several minions still gets one bolt

        public override string Id => "grand_entrance";
        public override string DisplayName => "Grand Entrance";
        public override string Description =>
            $"Summoning a minion or sentry calls down lightning on it, dealing {AugmentText.SpecialDamage("50% of its damage")} " +
            "to nearby enemies it can see and knocking them back.";

        public override string GetDetailedDescription(Player player)
        {
            return Description + "\n" + AugmentText.Note(
                "(The bolt strikes the spot where the new minion or sentry appears and hits every enemy within about 7 tiles once. " +
                "It deals half of the damage of the minion or sentry you just summoned, so it grows with your summon damage. " +
                "Walls stop it: an enemy with a solid block between it and the bolt is not hit.\n" +
                "Only minions and sentries summoned by using a summon weapon count. Minions made by other effects, like Necromancer's Court, don't. " +
                "Every summon gets its own bolt, but never more than one every third of a second.)");
        }

        public override AugmentRarity Rarity => AugmentRarity.Common;
        public override AugmentClass Class => AugmentClass.Summon;
        public override Texture2D Icon => TextureAssets.Item[ItemID.ThunderSpear].Value;

        private static uint lastBoltTick;

        // Only called for projectiles made by using an item, which is exactly "summoned with a summon weapon".
        public override void OnShootProjectile(Player player, Item item, Projectile projectile)
        {
            if (player.whoAmI != Main.myPlayer || player.dead)
                return;

            if (!projectile.minion && !projectile.sentry)
                return;

            if (Main.GameUpdateCount - lastBoltTick < MinTicksBetweenBolts)
                return;
            lastBoltTick = Main.GameUpdateCount;

            // Half of the damage of the minion or sentry that was just summoned.
            int boltDamage = System.Math.Max(1, (int)(projectile.damage * DamageFraction));

            Projectile.NewProjectile(
                player.GetSource_FromThis(),
                projectile.Center,
                Vector2.Zero,
                ModContent.ProjectileType<GrandEntranceBoltProjectile>(),
                boltDamage,
                BoltKnockback,
                player.whoAmI);
        }
    }
}
