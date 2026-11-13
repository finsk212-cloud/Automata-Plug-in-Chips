using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace Augments
{
    // The targeting marker Danger Close drops where the cursor was. It does no damage itself: it draws a
    // YoRHa-style targeting reticle on every client, and on the owner's game it releases the shells once the
    // warning is over.
    // ai[1] = the owner's ranged crit chance, handed on to each shell. Projectile.damage = the shell damage.
    public class DangerCloseMarkerProjectile : ModProjectile
    {
        // Warning, then one shell every few ticks, plus a few ticks of margin before the marker goes away.
        public const int TotalTicks = DangerCloseAugment.WarningTicks + DangerCloseAugment.ShellCount * DangerCloseAugment.ShellIntervalTicks + 5;

        // Outer edge of the reticle: where the shells can land, plus half a blast.
        private const float Radius = DangerCloseAugment.SpreadRadius + 30f;
        private const float BeamHeight = 1400f;

        public override string Texture => "Terraria/Images/MagicPixel";

        public override void SetDefaults()
        {
            AugmentProjectileTag tag = Projectile.GetGlobalProjectile<AugmentProjectileTag>();
            tag.IsAugmentProcDamage = true;
            tag.SourceAugmentId = "danger_close";
            tag.CanTriggerOnHitAugments = false;
            tag.PreventEchoChamberCopy = true;
            tag.PreventRicochetEngineCopy = true;

            Projectile.width = 16;
            Projectile.height = 16;
            // It only marks the spot. It never hits anything, so it stays unfriendly and class-less
            // (otherwise ranged chips could try to copy it or count it as a shot).
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Default;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = TotalTicks;
        }

        // How many shells the owner's game has already released. localAI is never synced, which is right:
        // only the owner spawns shells.
        private int ShellsSpawned
        {
            get => (int)Projectile.localAI[0];
            set => Projectile.localAI[0] = value;
        }

        private int Elapsed => TotalTicks - Projectile.timeLeft;

        // Each game plays the "marked" sound once, when the marker first appears for it.
        private bool introPlayed;

        public override void AI()
        {
            if (!introPlayed)
            {
                introPlayed = true;
                if (!Main.dedServ)
                    SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.7f, Pitch = -0.3f }, Projectile.Center);
            }

            Lighting.AddLight(Projectile.Center, 0.5f, 0.12f, 0.08f);

            // A few data-sparks drifting up off the reticle edge, on every client.
            if (Elapsed < DangerCloseAugment.WarningTicks && Main.rand.NextBool(2))
            {
                Vector2 spot = Projectile.Center + Main.rand.NextVector2CircularEdge(Radius, Radius);
                Dust spark = Dust.NewDustPerfect(spot, DustID.SilverCoin, new Vector2(0f, -Main.rand.NextFloat(0.6f, 1.6f)), 100, default, 0.8f);
                spark.noGravity = true;
            }

            if (Projectile.owner != Main.myPlayer)
                return;

            // 'while' rather than 'if' so a skipped tick can never lose a shell.
            while (ShellsSpawned < DangerCloseAugment.ShellCount
                && Elapsed >= DangerCloseAugment.WarningTicks + ShellsSpawned * DangerCloseAugment.ShellIntervalTicks)
            {
                SpawnShell(ShellsSpawned);
                ShellsSpawned++;
            }
        }

        private void SpawnShell(int index)
        {
            bool heavy = (index + 1) % DangerCloseAugment.HeavyEvery == 0;

            // The first shell lands dead centre; the rest scatter. Drop height and speed vary a little,
            // so the salvo does not land in a tidy line.
            float offsetX = index == 0 ? 0f : Main.rand.NextFloat(-DangerCloseAugment.SpreadRadius, DangerCloseAugment.SpreadRadius);
            float dropHeight = Main.rand.NextFloat(560f, 780f);
            float fallSpeed = Main.rand.NextFloat(24f, 32f);
            Vector2 start = new Vector2(Projectile.Center.X + offsetX, Math.Max(48f, Projectile.Center.Y - dropHeight));

            int id = Projectile.NewProjectile(
                Projectile.GetSource_FromThis(),
                start,
                new Vector2(0f, fallSpeed),
                ModContent.ProjectileType<DangerCloseShellProjectile>(),
                heavy ? Projectile.damage * 2 : Projectile.damage,
                heavy ? 11f : 6f,
                Projectile.owner,
                heavy ? 1f : 0f,       // ai[0]: 0 = normal shell falling, 1 = heavy shell falling (see the shell)
                Projectile.Center.Y);  // ai[1]: the height at which the shell goes off

            if (id >= 0 && id < Main.maxProjectiles)
                Main.projectile[id].CritChance = (int)Projectile.ai[1];
        }

        public override bool PreDraw(ref Color lightColor)
        {
            int elapsed = Elapsed;
            bool warning = elapsed < DangerCloseAugment.WarningTicks;
            float countdown = MathHelper.Clamp(elapsed / (float)DangerCloseAugment.WarningTicks, 0f, 1f);
            float fade = Math.Min(1f, Projectile.timeLeft / 14f) * Math.Min(1f, (elapsed + 1) / 8f);
            float pulse = 0.72f + 0.28f * (float)Math.Sin(elapsed * 0.5f);

            // Short glitch: the whole reticle jumps sideways for a few ticks, now and then.
            bool glitch = (elapsed / 3) % 6 == 0;
            Vector2 center = Projectile.Center + (glitch ? new Vector2(elapsed % 2 == 0 ? 7f : -7f, 0f) : Vector2.Zero);

            Color main = warning ? DangerCloseVisuals.Beige : DangerCloseVisuals.Red;
            Color alert = DangerCloseVisuals.Red;
            Color amber = DangerCloseVisuals.Amber;

            // Data beam from the sky down to the mark, with dashes travelling down it.
            DangerCloseVisuals.Line(center, center + new Vector2(0f, -BeamHeight), 2f, DangerCloseVisuals.Beige * (0.16f * fade));
            for (int i = 0; i < 8; i++)
            {
                float travel = BeamHeight - ((i * 175f + elapsed * 15f) % BeamHeight);
                DangerCloseVisuals.Line(center + new Vector2(0f, -travel), center + new Vector2(0f, -travel - 26f), 3f, amber * (0.55f * fade));
            }

            // Two hexagons turning against each other, plus tick marks around the outside.
            DangerCloseVisuals.Hex(center, Radius, elapsed * 0.012f, 3f, main * (pulse * fade));
            DangerCloseVisuals.Hex(center, Radius * 0.82f, -elapsed * 0.02f, 1.5f, main * (0.6f * fade));
            for (int i = 0; i < 24; i++)
            {
                Vector2 dir = (MathHelper.TwoPi * i / 24f).ToRotationVector2();
                float len = i % 6 == 0 ? 16f : 8f;
                DangerCloseVisuals.Line(center + dir * (Radius + 6f), center + dir * (Radius + 6f + len), 2f, DangerCloseVisuals.Beige * (0.7f * fade));
            }

            // HUD corner brackets.
            float half = Radius * 0.92f;
            DangerCloseVisuals.Bracket(center + new Vector2(-half, -half), 1f, 1f, 26f, 3f, DangerCloseVisuals.Beige * fade);
            DangerCloseVisuals.Bracket(center + new Vector2(half, -half), -1f, 1f, 26f, 3f, DangerCloseVisuals.Beige * fade);
            DangerCloseVisuals.Bracket(center + new Vector2(-half, half), 1f, -1f, 26f, 3f, DangerCloseVisuals.Beige * fade);
            DangerCloseVisuals.Bracket(center + new Vector2(half, half), -1f, -1f, 26f, 3f, DangerCloseVisuals.Beige * fade);

            // Scan line sweeping across the reticle.
            float scanY = (float)Math.Sin(elapsed * 0.12f) * Radius * 0.9f;
            float scanHalf = (float)Math.Sqrt(Math.Max(0f, Radius * Radius - scanY * scanY));
            DangerCloseVisuals.Line(center + new Vector2(-scanHalf, scanY), center + new Vector2(scanHalf, scanY), 1.5f, amber * (0.5f * fade));

            // The lock-on: a hexagon closing in on a diamond, with a crosshair.
            if (warning)
                DangerCloseVisuals.Hex(center, MathHelper.Lerp(Radius, 18f, countdown), elapsed * 0.05f, 2f, alert * (0.9f * fade));
            DangerCloseVisuals.Polygon(center, 9f, 4, 0f, 2f, alert * fade);
            DangerCloseVisuals.Line(center + new Vector2(-26f, 0f), center + new Vector2(-12f, 0f), 2f, DangerCloseVisuals.Beige * fade);
            DangerCloseVisuals.Line(center + new Vector2(12f, 0f), center + new Vector2(26f, 0f), 2f, DangerCloseVisuals.Beige * fade);
            DangerCloseVisuals.Line(center + new Vector2(0f, -26f), center + new Vector2(0f, -12f), 2f, DangerCloseVisuals.Beige * fade);
            DangerCloseVisuals.Line(center + new Vector2(0f, 12f), center + new Vector2(0f, 26f), 2f, DangerCloseVisuals.Beige * fade);

            // Pod-style readout.
            Color textColor = new Color(222, 214, 186) * fade;
            Color alertText = new Color(240, 60, 45) * (fade * (elapsed % 10 < 6 ? 1f : 0.45f));
            DangerCloseVisuals.Text("// DANGER CLOSE //", center + new Vector2(0f, -Radius - 34f), textColor, 0.8f);
            // The countdown runs to the moment the first shell actually lands (it takes ~25 ticks to fall).
            int impactTick = DangerCloseAugment.WarningTicks + 25;
            bool beforeImpact = elapsed < impactTick;
            string readout = beforeImpact
                ? "T-" + ((impactTick - elapsed) / 60f).ToString("0.0") + " seconds"
                : "IMPACT";
            DangerCloseVisuals.Text(readout, center + new Vector2(0f, Radius + 30f), beforeImpact ? textColor : alertText, 0.8f);
            DangerCloseVisuals.Text(DangerCloseAugment.ShellCount + " RDS", center + new Vector2(0f, Radius + 52f), textColor * 0.7f, 0.6f);

            return false;
        }
    }
}
