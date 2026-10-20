using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Augments.Core;

namespace Augments
{
    public class TrickShotAugment : Augment
    {
        public override string Id => "trick_shot";
        public override string DisplayName => "Trick Shot";
        public override string Description =>
            $"Your arrows and bullets {AugmentText.Trigger("bounce off walls")} up to 3 times instead of vanishing.\n" +
            $"Each bounce gives the shot {AugmentText.BonusDamage("+30% damage")} (stacking).\n" +
            $"A shot that has bounced always {AugmentText.Crit("crits")} and gains {AugmentText.Defense("+8 armor penetration")}.\n" +
            $"Your arrows and bullets also fly {AugmentText.MovementSpeed("10% faster")}.\n" +
            AugmentText.Note("(A shot disappears 3 seconds after its last bounce. Rockets, grenades and other special projectiles are unaffected. If you also have Phase Round, shots pass through walls instead of bouncing.)");

        public override AugmentRarity Rarity => AugmentRarity.Epic;
        public override AugmentClass Class => AugmentClass.Ranged;

        private const float VelocityBonus = 1.10f;
        private const float DamagePerBounce = 0.30f;
        private const float ArmorPenetration = 8f;

        public override void OnProjectileSpawn(Player player, Projectile projectile)
        {
            if (projectile.owner != player.whoAmI)
                return;

            if (!PhaseRoundProjectile.CanPhase(projectile))
                return;

            projectile.GetGlobalProjectile<TrickShotProjectile>().Active = true;
            projectile.velocity *= VelocityBonus;
            projectile.netUpdate = true; // send the bounce flag so every client bounces the shot too
        }

        public override void ModifyHitNPCWithProj(Player player, Projectile proj, NPC target, ref NPC.HitModifiers modifiers, AugmentHitSource source, float effectiveness)
        {
            if (source == AugmentHitSource.AugmentProc)
                return;

            TrickShotProjectile shot = proj.GetGlobalProjectile<TrickShotProjectile>();
            if (!shot.Active || shot.Bounces <= 0)
                return;

            modifiers.FlatBonusDamage += proj.damage * DamagePerBounce * shot.Bounces;
            modifiers.ArmorPenetration += ArmorPenetration;
            modifiers.SetCrit();
        }
    }

    // Lets arrows and bullets bounce off tiles and removes them a while after their last bounce.
    public class TrickShotProjectile : GlobalProjectile
    {
        public override bool InstancePerEntity => true;

        public bool Active;
        public int Bounces;
        private int ticksSinceBounce;

        private const int MaxBounces = 3;
        private const int MaxTicksAfterBounce = 180;

        public override void SendExtraAI(Projectile projectile, BitWriter bitWriter, BinaryWriter binaryWriter)
        {
            bitWriter.WriteBit(Active);
            if (Active)
                binaryWriter.Write((byte)Bounces);
        }

        public override void ReceiveExtraAI(Projectile projectile, BitReader bitReader, BinaryReader binaryReader)
        {
            Active = bitReader.ReadBit();
            if (Active)
                Bounces = binaryReader.ReadByte();
        }

        public override bool OnTileCollide(Projectile projectile, Vector2 oldVelocity)
        {
            if (!Active || Bounces >= MaxBounces)
                return true;

            if (projectile.velocity.X != oldVelocity.X)
                projectile.velocity.X = -oldVelocity.X;
            if (projectile.velocity.Y != oldVelocity.Y)
                projectile.velocity.Y = -oldVelocity.Y;

            Bounces++;
            ticksSinceBounce = 0;

            // Bounced shots don't also spawn Ricochet Engine shots.
            projectile.GetGlobalProjectile<AugmentProjectileTag>().PreventRicochetEngineCopy = true;

            for (int i = 0; i < 4; i++)
            {
                Dust dust = Dust.NewDustDirect(projectile.position, projectile.width, projectile.height, DustID.Electric, 0f, 0f, 150, default, 0.8f);
                dust.noGravity = true;
                dust.velocity *= 0.6f;
            }

            if (projectile.owner == Main.myPlayer)
                projectile.netUpdate = true;

            return false;
        }

        public override void PostAI(Projectile projectile)
        {
            if (!Active || Bounces <= 0)
                return;

            ticksSinceBounce++;
            if (ticksSinceBounce > MaxTicksAfterBounce)
                projectile.Kill();
        }
    }
}
