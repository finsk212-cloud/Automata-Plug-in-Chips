using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace Augments
{
    // A cluster bomblet thrown out of a Danger Close shell's blast. It arcs through the air and goes off
    // on the first enemy, the first wall, or when its fuse runs out. Same blast-hitbox approach as the shell.
    // ai[0] is 1 once it has exploded (synced, so every client draws the burst together).
    public class DangerCloseBombletProjectile : ModProjectile
    {
        private const int BlastSize = 78;
        private const int ExplosionTicks = 9;
        private const int FuseTicks = 38;

        public override string Texture => "Terraria/Images/MagicPixel";

        private bool Exploded => Projectile.ai[0] == 1f;
        private bool blastShown;

        public override void SetDefaults()
        {
            var tag = Projectile.GetGlobalProjectile<AugmentProjectileTag>();
            tag.IsAugmentProcDamage = true;
            tag.SourceAugmentId = "danger_close";
            tag.CanTriggerOnHitAugments = false;
            tag.PreventEchoChamberCopy = true;
            tag.PreventRicochetEngineCopy = true;

            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = -1;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = FuseTicks;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI()
        {
            if (Exploded)
            {
                ShowBlast();
                Projectile.velocity = Vector2.Zero;
                return;
            }

            Projectile.velocity.Y = System.Math.Min(Projectile.velocity.Y + 0.35f, 14f);
            Projectile.rotation += 0.35f;

            if (Main.rand.NextBool(2))
            {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Torch, Vector2.Zero, 100, default, 0.9f);
                d.noGravity = true;
            }

            if (Projectile.owner == Main.myPlayer && Projectile.timeLeft <= 2)
                Detonate();
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            Detonate();
        }

        public override bool OnTileCollide(Vector2 oldVelocity)
        {
            // Only the owner decides when it goes off (like the shell); other players just stop it at the wall and wait
            // for the owner's explosion to arrive.
            if (Projectile.owner == Main.myPlayer)
                Detonate();
            else
                Projectile.velocity = Vector2.Zero;

            return false;
        }

        private void Detonate()
        {
            if (Exploded)
                return;

            Projectile.ai[0] = 1f;
            Projectile.velocity = Vector2.Zero;
            Projectile.tileCollide = false;
            Projectile.timeLeft = ExplosionTicks;
            GrowToBlastSize();
            Projectile.netUpdate = true;
            ShowBlast();
        }

        private void ShowBlast()
        {
            if (blastShown)
                return;

            blastShown = true;

            // Non-owners never ran Detonate, so give them the same short life as the owner.
            Projectile.timeLeft = ExplosionTicks;
            if (Projectile.width != BlastSize)
                MatchBlastSize();

            if (Main.dedServ)
                return;

            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.35f, Pitch = 0.4f }, Projectile.Center);

            for (int i = 0; i < 9; i++)
            {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Torch, Main.rand.NextVector2Circular(5f, 5f), 100, default, 1.3f);
                d.noGravity = true;
            }

            Lighting.AddLight(Projectile.Center, 0.8f, 0.4f, 0.08f);
        }

        // Other players receive the owner's already-grown top-left corner (the owner re-centres when growing), so
        // they must only change the size. Growing around their own centre would put the blast far off to one side.
        private void MatchBlastSize()
        {
            if (Projectile.owner == Main.myPlayer)
            {
                GrowToBlastSize();
                return;
            }

            Projectile.width = BlastSize;
            Projectile.height = BlastSize;
        }

        private void GrowToBlastSize()
        {
            Vector2 center = Projectile.Center;
            Projectile.width = BlastSize;
            Projectile.height = BlastSize;
            Projectile.Center = center;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            if (Exploded)
            {
                float progress = 1f - Projectile.timeLeft / (float)ExplosionTicks;
                float fade = 1f - progress;
                float radius = MathHelper.Lerp(6f, BlastSize * 0.5f, progress);
                DangerCloseVisuals.Hex(Projectile.Center, radius, progress * 2f, 2f, DangerCloseVisuals.Amber * fade);
                DangerCloseVisuals.Polygon(Projectile.Center, (1f - progress) * 9f, 4, progress, 2f, DangerCloseVisuals.Beige * fade);
                return false;
            }

            // A little spinning red diamond.
            DangerCloseVisuals.Polygon(Projectile.Center, 6f, 4, Projectile.rotation, 2f, DangerCloseVisuals.Red);
            return false;
        }
    }
}
