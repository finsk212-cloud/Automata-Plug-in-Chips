using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using Augments.Core;

namespace Augments
{
    public class NecromancersCourtAugment : Augment
    {
        public override string Id => "necromancers_court";
        public override string DisplayName => "Necromancer's Court";
        public override string Description =>
            $"Summon kills raise ghost minions for {AugmentText.Duration("6s")}.";

        public override AugmentRarity Rarity => AugmentRarity.Epic;
        public override AugmentClass Class => AugmentClass.Summon;
        public override string FamilyId => AugmentFamilyRegistry.HivemindId;

        private const int GhostCount = 2;
        private const int GhostDamage = 5;

        public override (string label, int baseValue)[] GetScalingEntries()
        {
            return new[] { ("Ghost damage", GhostDamage) };
        }

        private const int GhostLifetimeTicks = 360;

        public override void OnHitNPCWithProj(Player player, Projectile proj, NPC target, NPC.HitInfo hit)
        {
            if (!proj.CountsAsClass(DamageClass.Summon) || target.life > 0)
                return;

            AugmentProjectileTag sourceTag = proj.GetGlobalProjectile<AugmentProjectileTag>();
            if (sourceTag.NecromancersCourtGhost || sourceTag.SourceMinionProjectileType < 0)
                return;

            Projectile sourceMinion = FindSourceMinion(player, proj, sourceTag);
            SpawnGhosts(player, target.Center, sourceTag.SourceMinionProjectileType, sourceMinion);
        }

        private static Projectile FindSourceMinion(Player player, Projectile killingProjectile, AugmentProjectileTag sourceTag)
        {
            if (killingProjectile.minion && killingProjectile.type == sourceTag.SourceMinionProjectileType)
                return killingProjectile;

            foreach (Projectile projectile in Main.projectile)
            {
                if (projectile.active && projectile.owner == player.whoAmI &&
                    projectile.identity == sourceTag.SourceMinionProjectileIdentity &&
                    projectile.type == sourceTag.SourceMinionProjectileType)
                {
                    return projectile;
                }
            }

            return null;
        }

        private static void SpawnGhosts(Player player, Vector2 center, int minionType, Projectile sourceMinion)
        {
            for (int i = 0; i < GhostCount; i++)
            {
                float direction = i == 0 ? -1f : 1f;
                Vector2 offset = new Vector2(direction * 20f, -12f);
                Vector2 velocity = new Vector2(direction * 3f, -2f);

                int projectileIndex = Projectile.NewProjectile(
                    player.GetSource_FromThis(),
                    center + offset,
                    velocity,
                    minionType,
                    AugmentScaling.Scale(GhostDamage),
                    0f,
                    player.whoAmI,
                    sourceMinion?.ai[0] ?? 0f,
                    sourceMinion?.ai[1] ?? 0f,
                    sourceMinion?.ai[2] ?? 0f
                );

                if (projectileIndex < 0 || projectileIndex >= Main.maxProjectiles)
                    continue;

                Projectile ghost = Main.projectile[projectileIndex];
                ghost.damage = AugmentScaling.Scale(GhostDamage);
                ghost.originalDamage = AugmentScaling.Scale(GhostDamage);
                ghost.minionSlots = 0f;
                ghost.alpha = System.Math.Max(ghost.alpha, 110);

                AugmentProjectileTag ghostTag = ghost.GetGlobalProjectile<AugmentProjectileTag>();
                ghostTag.IsAugmentProcDamage = true;
                ghostTag.CanTriggerOnHitAugments = false;
                ghostTag.PreventEchoChamberCopy = true;
                ghostTag.PreventRicochetEngineCopy = true;
                ghostTag.NecromancersCourtGhost = true;
                ghostTag.NecromancersCourtGhostTimeLeft = GhostLifetimeTicks;
                ghost.netUpdate = true;
            }
        }
    }
}
