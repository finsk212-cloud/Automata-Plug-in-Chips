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
            $"Minion hits build swarm stacks on enemies. At {AugmentText.Trigger("5 stacks")}, release {AugmentText.BonusDamage("6 void bugs")} (18 damage each) to swarm nearby enemies.";

        public override AugmentRarity Rarity => AugmentRarity.Rare;
        public override AugmentClass Class => AugmentClass.Summon;
        public override string FamilyId => AugmentFamilyRegistry.HivemindId;

        private const int BugCount = 6;
        private const int BugDamage = 18;

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

            if (target.GetGlobalNPC<QueensSwarmNPC>().AddStack() < QueensSwarmNPC.StacksRequired)
                return;

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
