using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;

namespace Augments
{
    // One shell of the Danger Close bombardment. It falls straight down, ignoring tiles, and goes off either on
    // the first enemy it touches or when it reaches the height of the mark (ai[1]), so it works underground too.
    // The blast is a short-lived large hitbox, so the splash goes through the normal hit path (damage numbers,
    // Combat Analytics, multiplayer) like a vanilla bomb. Same shape as SiegeShellProjectile.
    //
    // ai[0] packs both the variant and the state, and is synced, so every client agrees on both:
    //   0 = normal, falling   1 = heavy, falling   2 = normal, exploded   3 = heavy, exploded
    public class DangerCloseShellProjectile : ModProjectile
    {
        private const int NormalBlastSize = 120;
        private const int HeavyBlastSize = 210;
        private const int ExplosionTicks = 12;

        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.RocketI;

        private bool Exploded => Projectile.ai[0] >= 2f;
        private bool Heavy => ((int)Projectile.ai[0]) % 2 == 1;
        private int BlastSize => Heavy ? HeavyBlastSize : NormalBlastSize;
        private bool blastShown;

        public override void SetDefaults()
        {
            var tag = Projectile.GetGlobalProjectile<AugmentProjectileTag>();
            tag.IsAugmentProcDamage = true;
            tag.SourceAugmentId = "danger_close";
            tag.CanTriggerOnHitAugments = false;
            tag.PreventEchoChamberCopy = true;
            tag.PreventRicochetEngineCopy = true;

            // Tall and thin: it falls ~28 pixels a tick, so a tall hitbox keeps it from skipping over small enemies.
            Projectile.width = 20;
            Projectile.height = 44;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 240;
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

            Projectile.scale = Heavy ? 1.5f : 1f;
            // Keep the last heading once the shell has been stopped (see below), instead of turning sideways.
            if (Projectile.velocity.LengthSquared() > 0.01f)
                Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            Lighting.AddLight(Projectile.Center, 0.6f, 0.3f, 0.05f);
            Dust trail = Dust.NewDustPerfect(Projectile.Center - Projectile.velocity * 0.4f, DustID.Torch, Vector2.Zero, 100, default, Heavy ? 1.9f : 1.4f);
            trail.noGravity = true;

            // Only the owner decides when it goes off; everyone else learns through the synced ai[0]. Until that
            // arrives, other players' copy stops at the mark height instead of falling past it and snapping back.
            if (Projectile.owner != Main.myPlayer)
            {
                if (Projectile.Center.Y >= Projectile.ai[1])
                    Projectile.velocity = Vector2.Zero;
                return;
            }

            if (Projectile.Center.Y >= Projectile.ai[1] || Projectile.timeLeft <= 2)
                Detonate();
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            // The blast sets whatever it hits on fire (the first target hit by the falling shell, and everything in the blast).
            target.AddBuff(BuffID.OnFire3, DangerCloseAugment.BurnTicks);
            Detonate();
        }

        private void Detonate()
        {
            if (Exploded)
                return;

            Projectile.ai[0] += 2f;
            Projectile.velocity = Vector2.Zero;
            Projectile.timeLeft = ExplosionTicks;
            GrowToBlastSize();
            Projectile.netUpdate = true;
            ShowBlast();

            if (Projectile.owner == Main.myPlayer)
                ScatterBomblets();
        }

        // Cluster bomblets thrown up and out of the blast. They are the owner's projectiles, so only the owner spawns them.
        private void ScatterBomblets()
        {
            int count = Heavy ? DangerCloseAugment.BombletsPerHeavyShell : DangerCloseAugment.BombletsPerShell;
            int shellDamage = Heavy ? Projectile.damage / 2 : Projectile.damage;
            int bombletDamage = Math.Max(1, (int)Math.Round(shellDamage * DangerCloseAugment.BombletDamageFraction));
            int bombletType = ModContent.ProjectileType<DangerCloseBombletProjectile>();

            for (int i = 0; i < count; i++)
            {
                Vector2 velocity = new Vector2(Main.rand.NextFloat(-6.5f, 6.5f), Main.rand.NextFloat(-10f, -4f));
                int id = Projectile.NewProjectile(
                    Projectile.GetSource_FromThis(),
                    Projectile.Center,
                    velocity,
                    bombletType,
                    bombletDamage,
                    3f,
                    Projectile.owner);

                if (id >= 0 && id < Main.maxProjectiles)
                    Main.projectile[id].CritChance = Projectile.CritChance;
            }
        }

        // Blast sound, dust and screen punch, once per client. Non-owners reach it through the synced ai[0].
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

            SoundEngine.PlaySound(SoundID.Item14 with { Volume = Heavy ? 0.95f : 0.7f, Pitch = Heavy ? -0.4f : -0.15f }, Projectile.Center);

            int sparks = Heavy ? 40 : 22;
            for (int i = 0; i < sparks; i++)
            {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Torch, Main.rand.NextVector2Circular(Heavy ? 11f : 8f, Heavy ? 11f : 8f), 100, default, Heavy ? 2.4f : 1.9f);
                d.noGravity = true;
            }

            for (int i = 0; i < (Heavy ? 18 : 10); i++)
            {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Smoke, Main.rand.NextVector2Circular(5f, 5f), 100, Color.DarkGray, 1.6f);
                d.noGravity = true;
            }

            // Data shards: the same off-white the marker uses.
            for (int i = 0; i < 8; i++)
            {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.SilverCoin, Main.rand.NextVector2Circular(9f, 9f), 100, default, 1f);
                d.noGravity = true;
            }

            Lighting.AddLight(Projectile.Center, 1f, 0.6f, 0.1f);

            // Heavy shells shake the screen of anyone nearby.
            if (Heavy && Main.LocalPlayer != null && Vector2.Distance(Main.LocalPlayer.Center, Projectile.Center) < 1400f)
            {
                Main.instance.CameraModifiers.Add(new PunchCameraModifier(
                    Projectile.Center, Main.rand.NextVector2CircularEdge(1f, 1f), 6f, 7f, 14, 1400f, "DangerCloseHeavy"));
            }
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
                // A hexagon shockwave and a diamond flash instead of a sprite.
                float progress = 1f - Projectile.timeLeft / (float)ExplosionTicks;
                float fade = 1f - progress;
                float radius = MathHelper.Lerp(12f, BlastSize * 0.55f, 1f - (1f - progress) * (1f - progress));
                Color ring = Heavy ? DangerCloseVisuals.Red : DangerCloseVisuals.Amber;

                DangerCloseVisuals.Hex(Projectile.Center, radius, progress * 1.2f, Heavy ? 4f : 3f, ring * fade);
                DangerCloseVisuals.Hex(Projectile.Center, radius * 0.6f, -progress * 1.6f, 2f, DangerCloseVisuals.Beige * fade);
                DangerCloseVisuals.Polygon(Projectile.Center, (1f - progress) * (Heavy ? 26f : 16f), 4, progress, 3f, DangerCloseVisuals.Beige * fade);
                if (Heavy)
                    DangerCloseVisuals.Hex(Projectile.Center, radius * 1.25f, progress * 0.8f, 2f, DangerCloseVisuals.Red * (fade * 0.6f));
                return false;
            }

            // A thin guide line trailing up the shell's path, so the fall reads from far away.
            Vector2 up = new Vector2(0f, -(Heavy ? 150f : 100f));
            DangerCloseVisuals.Line(Projectile.Center, Projectile.Center + up, Heavy ? 3f : 2f, DangerCloseVisuals.Red * 0.45f);
            return true;
        }
    }
}
