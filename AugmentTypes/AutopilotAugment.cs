using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace Augments
{
    // Spawns and maintains a Pod drone (see AutopilotPodProjectile) that follows
    // the player and heals the most injured nearby ally. The drone itself holds
    // all of the behavior; this chip only makes sure exactly one exists.
    public class AutopilotAugment : Augment
    {
        public const string ChipId = "autopilot";

        public override string Id => ChipId;
        public override string DisplayName => "Autopilot";
        public override string Description =>
            $"Pod 153 drone follows you and heals the lowest-health nearby player for {AugmentText.Healing("5 HP")} per second.";

        public override string GetDetailedDescription(Player player) =>
            Description + "\n" +
            "[c/94A3B8:(Heals 1 HP every 0.2s. Doesn't heal NPCs.)]";

        public override AugmentRarity Rarity => AugmentRarity.Epic;
        public override AugmentClass Class => AugmentClass.Support;

        public override void OnUpdate(Player player)
        {
            if (player.whoAmI != Main.myPlayer || player.dead || Main.GameUpdateCount % 30 != 0)
                return;

            int podType = ModContent.ProjectileType<AutopilotPodProjectile>();
            foreach (Projectile proj in Main.projectile)
            {
                if (proj.active && proj.type == podType && proj.owner == player.whoAmI)
                    return;
            }

            Projectile.NewProjectile(
                player.GetSource_Misc("Autopilot"),
                player.Center + new Vector2(-player.direction * 34f, -46f),
                Vector2.Zero,
                podType,
                0,
                0f,
                player.whoAmI
            );
        }
    }
}
