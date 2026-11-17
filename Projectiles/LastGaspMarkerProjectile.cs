using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Augments
{
    // The Last Gasp timer. It follows the player (who is alive at 0 health and can move) and does no damage: every
    // client draws a small Pod message box above the player's head that types out "Unit failure in N seconds."
    // The plugin spawns the blast when the rampage ends.
    public class LastGaspMarkerProjectile : ModProjectile
    {
        private const int TotalTicks = LastGaspAugment.RampageTicks + 4;

        // The message box, in pixels around the point above the player's head.
        private const float BoxWidth = 184f;
        private const float BoxHeight = 58f;
        private const float BoxLift = 80f;

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
            // It only shows the timer. It never hits anything.
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Default;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = TotalTicks;
        }

        private int Elapsed => TotalTicks - Projectile.timeLeft;

        public override void AI()
        {
            int elapsed = Elapsed;

            // Stay on the player, on every client (other players' positions are synced).
            Player owner = Main.player[Projectile.owner];
            if (owner.active)
                Projectile.Center = owner.Center;

            Lighting.AddLight(Projectile.Center, 0.5f, 0.12f, 0.08f);

            // Grey data-sparks drifting up off the player.
            if (elapsed < LastGaspAugment.RampageTicks && Main.rand.NextBool(3))
            {
                Vector2 spot = Projectile.Center + new Vector2(Main.rand.NextFloat(-14f, 14f), Main.rand.NextFloat(-20f, 20f));
                Dust spark = Dust.NewDustPerfect(spot, DustID.SilverCoin, new Vector2(0f, -Main.rand.NextFloat(0.5f, 1.4f)), 100, default, 0.8f);
                spark.noGravity = true;
            }
        }

        // The first `shown` characters of a line, plus a blinking cursor while it is still being typed.
        private static string Typed(string text, int shown, int elapsed)
        {
            shown = Math.Clamp(shown, 0, text.Length);
            string visible = text.Substring(0, shown);
            return shown < text.Length && (elapsed / 4) % 2 == 0 ? visible + "_" : visible;
        }

        private static void LeftText(string text, Vector2 worldPos, Color color, float scale)
        {
            ModText.DrawBorder(Main.spriteBatch, text, worldPos - Main.screenPosition, color, scale, 0f, 0.5f);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            int elapsed = Elapsed;
            if (elapsed >= LastGaspAugment.RampageTicks)
                return false; // the blast draws itself from here

            float fade = Math.Min(1f, (elapsed + 1) / 8f) * Math.Min(1f, (LastGaspAugment.RampageTicks - elapsed) / 10f);
            int secondsLeft = Math.Max(1, (int)Math.Ceiling((LastGaspAugment.RampageTicks - elapsed) / 60f));
            bool lastSecond = secondsLeft <= 1;

            // Short glitch: the box jumps sideways for a few ticks, now and then.
            bool glitch = (elapsed / 3) % 9 == 0;
            Vector2 boxCenter = Projectile.Center + new Vector2(glitch ? (elapsed % 2 == 0 ? 5f : -5f) : 0f, -BoxLift);
            Vector2 topLeft = boxCenter + new Vector2(-BoxWidth / 2f, -BoxHeight / 2f);
            Vector2 topRight = boxCenter + new Vector2(BoxWidth / 2f, -BoxHeight / 2f);
            Vector2 bottomLeft = boxCenter + new Vector2(-BoxWidth / 2f, BoxHeight / 2f);
            Vector2 bottomRight = boxCenter + new Vector2(BoxWidth / 2f, BoxHeight / 2f);

            // It blinks red like an alarm: every 10 ticks, and every 5 ticks in the last second.
            bool blinkOn = (elapsed / (lastSecond ? 5 : 10)) % 2 == 0;

            Color beige = DangerCloseVisuals.Beige;
            Color red = DangerCloseVisuals.Red;

            // Dark panel with a beige frame and a little pointer down to the player.
            DangerCloseVisuals.Line(boxCenter + new Vector2(-BoxWidth / 2f, 0f), boxCenter + new Vector2(BoxWidth / 2f, 0f), BoxHeight, (blinkOn ? new Color(58, 12, 10) : new Color(20, 19, 16)) * (0.9f * fade));
            DangerCloseVisuals.Line(topLeft, topRight, 2f, (blinkOn ? red : beige) * fade);
            DangerCloseVisuals.Line(bottomLeft, bottomRight, 2f, (blinkOn ? red : beige) * fade);
            DangerCloseVisuals.Line(topLeft, bottomLeft, 2f, (blinkOn ? red : beige) * fade);
            DangerCloseVisuals.Line(topRight, bottomRight, 2f, (blinkOn ? red : beige) * fade);
            DangerCloseVisuals.Polygon(boxCenter + new Vector2(0f, BoxHeight / 2f + 6f), 7f, 3, MathHelper.PiOver2, 2f, (blinkOn ? red : beige) * fade);

            // The Pod's message, typed out; the number in it updates every second.
            Vector2 left = boxCenter + new Vector2(-BoxWidth / 2f + 10f, 0f);
            Color dim = new Color(185, 178, 154) * fade;
            Color light = new Color(232, 226, 204) * fade;
            Color alert = new Color(240, 60, 45) * fade;

            string line1 = "Unit failure in " + secondsLeft + (secondsLeft == 1 ? " second." : " seconds.");
            string line2 = "Keep fighting.";

            LeftText("POD 042", left + new Vector2(0f, -19f), dim, 0.65f);
            LeftText(Typed(line1, elapsed * 2, elapsed), left + new Vector2(0f, -3f), blinkOn ? alert : light, 0.72f);
            LeftText(Typed(line2, (elapsed - 16) * 2, elapsed), left + new Vector2(0f, 14f), alert, 0.72f);

            return false;
        }
    }
}
