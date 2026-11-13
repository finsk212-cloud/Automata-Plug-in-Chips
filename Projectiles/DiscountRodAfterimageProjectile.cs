using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace Augments
{
    // The backup the Rod of Discount leaves where you were standing. It is drawn as a flickering hologram of your
    // silhouette with a backup progress counter, and when the backup "completes" it bursts into data cubes.
    //
    // ai[0] is 1 once it has exploded (synced). ai[1], ai[2] = where the caster landed. The afterimage plays the
    // teleport sounds and the dust trail between the two spots on its first tick, and because every player's game
    // has this projectile, every player sees and hears the jump.
    public class DiscountRodAfterimageProjectile : ModProjectile
    {
        private const int WarningTicks = 60;
        private const int ExplosionTicks = 14;
        private const int BlastSize = 170;

        public override string Texture => "Terraria/Images/MagicPixel";

        private bool Exploded => Projectile.ai[0] == 1f;
        private bool blastShown;
        private bool introPlayed;
        private int Elapsed => WarningTicks + ExplosionTicks - Projectile.timeLeft;

        public override void SetDefaults()
        {
            var tag = Projectile.GetGlobalProjectile<AugmentProjectileTag>();
            tag.IsAugmentProcDamage = true;
            tag.SourceAugmentId = "discount_rod";
            tag.CanTriggerOnHitAugments = false;
            tag.PreventEchoChamberCopy = true;
            tag.PreventRicochetEngineCopy = true;

            Projectile.width = 24;
            Projectile.height = 48;
            // Harmless until it goes off.
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = WarningTicks + ExplosionTicks;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI()
        {
            if (!introPlayed)
            {
                introPlayed = true;
                PlayTeleportEffects();
            }

            if (Exploded)
            {
                ShowBlast();
                return;
            }

            Lighting.AddLight(Projectile.Center, 0.2f, 0.5f, 0.65f);

            // A little data drifting up off the hologram.
            if (Main.rand.NextBool(3))
            {
                Dust d = Dust.NewDustPerfect(Projectile.Center + new Vector2(Main.rand.NextFloat(-10f, 10f), 20f), DustID.IceTorch, new Vector2(0f, -Main.rand.NextFloat(0.8f, 1.8f)), 100, default, 0.8f);
                d.noGravity = true;
            }

            // Only the owner decides when it goes off; everyone else learns through the synced ai[0].
            if (Projectile.owner == Main.myPlayer && Elapsed >= WarningTicks)
                Detonate();
        }

        // The jump itself: a sound at both ends and a trail of data between them. Runs once per game.
        private void PlayTeleportEffects()
        {
            if (Main.dedServ)
                return;

            Vector2 from = Projectile.Center;
            Vector2 landing = new Vector2(Projectile.ai[1], Projectile.ai[2]);

            SoundEngine.PlaySound(SoundID.Item8, from);
            SoundEngine.PlaySound(SoundID.Item8, landing);

            int steps = Math.Clamp((int)(Vector2.Distance(from, landing) / 20f), 3, 40);
            for (int i = 0; i <= steps; i++)
            {
                Dust d = Dust.NewDustPerfect(Vector2.Lerp(from, landing, i / (float)steps), i % 2 == 0 ? DustID.IceTorch : DustID.WhiteTorch, Main.rand.NextVector2Circular(1f, 1f), 100, default, 1.3f);
                d.noGravity = true;
            }
        }

        private void Detonate()
        {
            if (Exploded)
                return;

            Projectile.ai[0] = 1f;
            Projectile.friendly = true;
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

            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.8f, Pitch = 0.3f }, Projectile.Center);

            for (int i = 0; i < 26; i++)
            {
                int type = i % 2 == 0 ? DustID.IceTorch : DustID.WhiteTorch;
                Dust d = Dust.NewDustPerfect(Projectile.Center, type, Main.rand.NextVector2Circular(9f, 9f), 100, default, 1.6f);
                d.noGravity = true;
            }

            Lighting.AddLight(Projectile.Center, 0.5f, 0.9f, 1.1f);
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
            Vector2 center = Projectile.Center;
            int seed = Projectile.identity;

            if (Exploded)
            {
                int frame = ExplosionTicks - Projectile.timeLeft;
                float progress = frame / (float)ExplosionTicks;
                float fade = 1f - progress;
                float eased = 1f - (1f - progress) * (1f - progress);

                // A burst of data cubes: squares of different sizes flying outward and spinning.
                for (int i = 0; i < 30; i++)
                {
                    float angle = AutomataFx.Rand(seed, i, 1) * MathHelper.TwoPi;
                    float distance = BlastSize * 0.6f * eased * (0.35f + 0.65f * AutomataFx.Rand(seed, i, 2));
                    float size = MathHelper.Lerp(15f, 3f, progress) * (0.6f + AutomataFx.Rand(seed, i, 3) * 0.8f);
                    float spin = AutomataFx.Rand(seed, i, 4) * MathHelper.Pi + progress * 4f;
                    Color color = i % 3 == 0 ? AutomataFx.White : AutomataFx.Cyan;
                    AutomataFx.Rect(center + angle.ToRotationVector2() * distance, size, size, spin, color * fade);
                }

                // Glitch bars tearing across the blast for the first few frames.
                if (progress < 0.75f)
                {
                    for (int i = 0; i < 6; i++)
                    {
                        if ((frame + i) % 3 == 0)
                            continue;

                        float y = (AutomataFx.Rand(seed, i, 5) - 0.5f) * BlastSize * 0.9f;
                        float width = BlastSize * (0.5f + AutomataFx.Rand(seed, i, 6) * 0.8f);
                        float x = (AutomataFx.Rand(seed, i, 7) - 0.5f) * BlastSize * 0.3f;
                        AutomataFx.Rect(center + new Vector2(x, y), width, 2f + AutomataFx.Rand(seed, i, 8) * 5f, 0f, AutomataFx.White * (fade * 0.8f));
                    }
                }

                // The white flash in the middle.
                AutomataFx.Rect(center, fade * 70f, fade * 70f, progress * 2f, AutomataFx.White * fade);
                return false;
            }

            // The backup: a hologram of the silhouette that glitches harder the closer it gets to finishing.
            int elapsed = Elapsed;
            float t = MathHelper.Clamp(elapsed / (float)WarningTicks, 0f, 1f);
            float pulse = 0.7f + 0.3f * (float)Math.Sin(elapsed * 0.5f);
            AutomataFx.Hologram(center, 1f, elapsed, 1.5f + t * 4f, AutomataFx.Cyan * pulse);

            // A scan line travelling up and down the figure.
            float scan = -24f + (elapsed * 0.06f % 1f) * 48f;
            AutomataFx.Rect(center + new Vector2(0f, scan), 30f, 2f, 0f, AutomataFx.White * 0.9f);

            // The pad under its feet.
            AutomataFx.Rect(center + new Vector2(0f, 26f), 46f, 2f, 0f, AutomataFx.Cyan * 0.8f);

            // Progress bar and counter.
            Vector2 barCenter = center + new Vector2(0f, 42f);
            AutomataFx.Rect(barCenter + new Vector2(0f, -5f), 62f, 1.5f, 0f, AutomataFx.White * 0.7f);
            AutomataFx.Rect(barCenter + new Vector2(0f, 5f), 62f, 1.5f, 0f, AutomataFx.White * 0.7f);
            AutomataFx.Rect(barCenter + new Vector2(-30.5f, 0f), 1.5f, 11f, 0f, AutomataFx.White * 0.7f);
            AutomataFx.Rect(barCenter + new Vector2(30.5f, 0f), 1.5f, 11f, 0f, AutomataFx.White * 0.7f);
            AutomataFx.Rect(barCenter + new Vector2(-29f + 29f * t, 0f), 58f * t, 7f, 0f, AutomataFx.Cyan * 0.9f);

            DangerCloseVisuals.Text("BACKUP " + (int)(t * 100f) + "%", barCenter + new Vector2(0f, 20f), new Color(190, 235, 255) * pulse, 0.55f);
            return false;
        }
    }
}
