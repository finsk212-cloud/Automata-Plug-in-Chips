using Microsoft.Xna.Framework;
using Terraria;
using System.IO;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Augments.Core;

namespace Augments
{
    public class PhaseRoundAugment : Augment
    {
        public override string Id          => "phase_round";
        public override string DisplayName => "Phase Round";
        public override string Description =>
            $"Your arrows and bullets pass through {AugmentText.Trigger("tiles")}, so you can hit enemies behind walls.";

        public override string GetDetailedDescription(Player player) =>
            $"Your arrows and bullets pass through {AugmentText.Trigger("tiles")}, so you can hit enemies behind walls. " +
            $"A shot is removed after {AugmentText.Duration("1 second")} spent inside blocks.\n" +
            $"[c/94A3B8:(Rockets, grenades and other special projectiles are not affected.)]";

        public override AugmentRarity Rarity => AugmentRarity.Epic;
        public override AugmentClass  Class  => AugmentClass.Ranged;

        public override void OnProjectileSpawn(Player player, Projectile projectile)
        {
            if (projectile.owner != player.whoAmI)
                return;

            if (!PhaseRoundProjectile.CanPhase(projectile))
                return;

            PhaseRoundProjectile phase = projectile.GetGlobalProjectile<PhaseRoundProjectile>();
            phase.Active = true;
            projectile.tileCollide = false;
            projectile.netUpdate = true; // send the phasing flag so every client draws the shot passing through walls
        }
    }

    // Marks projectiles that are phasing and removes them after too long inside blocks.
    public class PhaseRoundProjectile : GlobalProjectile
    {
        public override bool InstancePerEntity => true;

        public bool Active;
        private int ticksInsideTiles;

        private const int MaxTicksInsideTiles = 60;

        // Plain arrows and bullets only (the same AI style); skips rockets, grenades, boomerangs, minions and proc projectiles.
        public static bool CanPhase(Projectile p)
        {
            if (!p.friendly || p.hostile || p.minion || p.sentry || !p.tileCollide)
                return false;

            if (!p.CountsAsClass(DamageClass.Ranged))
                return false;

            if (p.aiStyle != ProjAIStyleID.Arrow)
                return false;

            return !p.GetGlobalProjectile<AugmentProjectileTag>().IsAugmentProcDamage;
        }

        public override void SendExtraAI(Projectile projectile, BitWriter bitWriter, BinaryWriter binaryWriter)
        {
            bitWriter.WriteBit(Active);
        }

        public override void ReceiveExtraAI(Projectile projectile, BitReader bitReader, BinaryReader binaryReader)
        {
            Active = bitReader.ReadBit();
            if (Active)
                projectile.tileCollide = false;
        }

        public override void PostAI(Projectile projectile)
        {
            if (!Active)
                return;

            if (!Collision.SolidCollision(projectile.position, projectile.width, projectile.height))
                return;

            ticksInsideTiles++;

            if (ticksInsideTiles > MaxTicksInsideTiles)
                projectile.Kill();
        }
    }
}
