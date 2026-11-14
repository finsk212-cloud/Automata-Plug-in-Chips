using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using Augments.Core;

namespace Augments
{
    public class QueensSwarmAugment : Augment
    {
        public override string Id => "queens_swarm";
        public override string DisplayName => "Queen's Swarm";
        public override string Description =>
            $"Minion hits build swarm stacks on enemies. At {AugmentText.Trigger("5 stacks")}, release {AugmentText.BonusDamage("6 void bugs")} ({BugDamage} damage each) to swarm nearby enemies. " +
            $"The same enemy can release again after {AugmentText.Duration("3 seconds")}, and you can release once every {AugmentText.Duration("1 second")}.";

        public override AugmentRarity Rarity => AugmentRarity.Rare;
        public override AugmentClass Class => AugmentClass.Summon;
        public override string FamilyId => AugmentFamilyRegistry.HivemindId;

        private const int BugCount = 6;
        private const int BugDamage = 15;
        private const int PlayerCooldownTicks = 60; // you can release one swarm per second in total

        // This player's own cooldown (the swarm is released on the shooter's own game).
        private static uint nextPlayerReleaseTick;

        public override (string label, int baseValue)[] GetScalingEntries()
        {
            return new[] { ("Void bug damage", BugDamage) };
        }


        public override void OnHitNPCWithProj(Player player, Projectile proj, NPC target, NPC.HitInfo hit)
        {
            if (!proj.CountsAsClass(DamageClass.Summon) || !proj.minion)
                return;

            if (proj.GetGlobalProjectile<AugmentProjectileTag>().IsAugmentProcDamage)
                return;

            bool canRelease = Main.GameUpdateCount >= nextPlayerReleaseTick;
            if (target.GetGlobalNPC<QueensSwarmNPC>().AddStack(canRelease) < QueensSwarmNPC.StacksRequired)
                return;

            nextPlayerReleaseTick = Main.GameUpdateCount + PlayerCooldownTicks;
            SpawnVoidBugs(player, target.Center);
        }

        private static void SpawnVoidBugs(Player player, Vector2 center)
        {
            for (int i = 0; i < BugCount; i++)
            {
                float angle = MathHelper.TwoPi * i / BugCount;
                Vector2 direction = angle.ToRotationVector2();

                Projectile.NewProjectile(
                    player.GetSource_FromThis(),
                    center + direction * 18f,
                    direction * 4f,
                    ModContent.ProjectileType<QueensVoidBugProjectile>(),
                    AugmentScaling.Scale(BugDamage),
                    0f,
                    player.whoAmI
                );
            }
        }
    }
}
