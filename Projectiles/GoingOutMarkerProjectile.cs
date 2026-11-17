using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace Augments
{
    // The Pod's self-destruct countdown. It follows the player (who is still alive at 0 health and can move) and
    // does no damage itself: every client just draws it. The plugin spawns the blast when the countdown ends.
    public class GoingOutMarkerProjectile : ModProjectile
    {
        private const int TotalTicks = GoingOutWithABangAugment.CountdownTicks + 4;

        public override string Texture => "Terraria/Images/MagicPixel";

        public override void SetDefaults()
        {
            AugmentProjectileTag tag = Projectile.GetGlobalProjectile<AugmentProjectileTag>();
            tag.IsAugmentProcDamage = true;
            tag.SourceAugmentId = "death_blast";
            tag.CanTriggerOnHitAugments = false;
            tag.PreventEchoChamberCopy = true;
            tag.PreventRicochetEngineCopy = true;

            Projectile.width = 16;
            Projectile.height = 16;
            // It only counts down. It never hits anything.
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Default;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = TotalTicks;
        }

        private int Elapsed => TotalTicks - Projectile.timeLeft;

        private int lastNumber = -1;

        public override void AI()
        {
            int elapsed = Elapsed;

            // Stay on the player, on every client (other players' positions are synced).
            Player owner = Main.player[Projectile.owner];
            if (owner.active)
                Projectile.Center = owner.Center;

            Lighting.AddLight(Projectile.Center, 0.5f, 0.12f, 0.08f);

            // A beep each time the Pod's number changes, on every client.
            int number = Number(elapsed);
            if (number != lastNumber && elapsed < GoingOutWithABangAugment.CountdownTicks)
            {
                lastNumber = number;
                if (!Main.dedServ)
                    SoundEngine.PlaySound(SoundID.Item35 with { Volume = 0.8f, Pitch = -0.2f + (3 - number) * 0.25f }, Projectile.Center);
            }

            // Data-sparks drifting up from the Pod's reticle.
            if (elapsed < GoingOutWithABangAugment.CountdownTicks && Main.rand.NextBool(2))
            {
                Vector2 spot = Projectile.Center + Main.rand.NextVector2CircularEdge(70f, 70f);
                Dust spark = Dust.NewDustPerfect(spot, DustID.SilverCoin, new Vector2(0f, -Main.rand.NextFloat(0.6f, 1.6f)), 100, default, 0.8f);
                spark.noGravity = true;
            }
        }

        // 3, 2, 1 (and stays on 1 for the last stretch).
        private static int Number(int elapsed) =>
            Math.Max(1, 3 - elapsed / GoingOutWithABangAugment.TicksPerNumber);

        public override bool PreDraw(ref Color lightColor)
        {
            int elapsed = Elapsed;
            if (elapsed >= GoingOutWithABangAugment.CountdownTicks)
                return false; // the blast draws itself from here

            float fade = Math.Min(1f, (elapsed + 1) / 8f);
            float pulse = 0.72f + 0.28f * (float)Math.Sin(elapsed * 0.5f);
            int number = Number(elapsed);
            float numberPhase = (elapsed % GoingOutWithABangAugment.TicksPerNumber) / (float)GoingOutWithABangAugment.TicksPerNumber;

            // Short glitch: the whole readout jumps sideways for a few ticks, now and then.
            bool glitch = (elapsed / 3) % 7 == 0;
            Vector2 c = Projectile.Center + (glitch ? new Vector2(elapsed % 2 == 0 ? 6f : -6f, 0f) : Vector2.Zero);

            Color beige = DangerCloseVisuals.Beige;
            Color red = DangerCloseVisuals.Red;

            // The blast radius, so everyone can see how far it will reach.
            DangerCloseVisuals.Polygon(c, GoingOutBlastProjectile.Radius, 48, elapsed * 0.004f, 1.5f, red * (0.28f * fade));

            // Three warning triangles around the countdown.
            for (int i = 0; i < 3; i++)
            {
                float angle = -MathHelper.PiOver2 + MathHelper.TwoPi * i / 3f;
                Vector2 at = c + angle.ToRotationVector2() * 88f;
                DangerCloseVisuals.Polygon(at, 15f, 3, angle, 2.5f, red * (pulse * fade));
                DangerCloseVisuals.Line(at - new Vector2(0f, 4f), at + new Vector2(0f, 3f), 2f, red * fade);
            }

            // Two hexagons turning against each other; the outer one closes in on every number.
            DangerCloseVisuals.Hex(c, MathHelper.Lerp(70f, 52f, numberPhase), elapsed * 0.015f, 3f, beige * (pulse * fade));
            DangerCloseVisuals.Hex(c, 40f, -elapsed * 0.02f, 1.5f, beige * (0.6f * fade));

            // The big number.
            Color numberColor = number == 1 ? new Color(240, 60, 45) : new Color(222, 214, 186);
            DangerCloseVisuals.Text(number.ToString(), c, numberColor * fade, 2.6f);

            // Pod-style readout: a red bar with the words, and the time left.
            DangerCloseVisuals.Line(c + new Vector2(-104f, 110f), c + new Vector2(104f, 110f), 20f, red * (0.85f * fade));
            DangerCloseVisuals.Text("SELF-DESTRUCT", c + new Vector2(0f, 110f), new Color(24, 22, 18) * fade, 0.8f);
            DangerCloseVisuals.Text("// SEQUENCE INITIATED //", c + new Vector2(0f, -112f), new Color(222, 214, 186) * fade, 0.7f);
            float secondsLeft = (GoingOutWithABangAugment.CountdownTicks - elapsed) / 60f;
            DangerCloseVisuals.Text("T-" + secondsLeft.ToString("0.0") + " seconds", c + new Vector2(0f, 134f), new Color(222, 214, 186) * (0.8f * fade), 0.7f);

            return false;
        }
    }
}
