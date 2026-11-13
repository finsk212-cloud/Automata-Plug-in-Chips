using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace Augments
{
    // The portal Long Distance Relationship opens. Pure visual: the spell-moving happens in the augment itself, and
    // stops after exactly PortalTicks.
    //
    // While it works, it is drawn like a hacking-minigame gate: broken rings turning against each other with data
    // cubes circling them, an 8-bit heart in the middle, and little data packets travelling along the link back to the
    // caster. When the time is up the connection drops: the heart breaks in two, a sad face appears with
    // "DISCONNECTED", and a hang-up sound plays. That last part lives for DisconnectTicks after the portal stops working.
    public class LongDistancePortalProjectile : ModProjectile
    {
        private const float Radius = 40f;
        private const int TotalTicks = LongDistanceRelationshipAugment.PortalTicks + LongDistanceRelationshipAugment.DisconnectTicks;

        public override string Texture => "Terraria/Images/MagicPixel";

        // Each client plays the hang-up tones itself, once.
        private bool hungUp;
        // Each client plays the opening sound itself, once.
        private bool introPlayed;
        // The launch counter this game last saw in ai[0], and how many frames of "just used" flash are left.
        private float lastLaunches;
        private int flash;

        public override void SetDefaults()
        {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Default;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = TotalTicks;
        }

        private int Elapsed => TotalTicks - Projectile.timeLeft;
        private bool Disconnected => Elapsed >= LongDistanceRelationshipAugment.PortalTicks;

        public override void AI()
        {
            Player owner = Main.player[Projectile.owner];
            if (!owner.active || owner.dead)
            {
                Projectile.Kill();
                return;
            }

            int elapsed = Elapsed;

            // The opening sound, once per game: every player hears the portal appear.
            if (!introPlayed)
            {
                introPlayed = true;
                lastLaunches = Projectile.ai[0];
                if (!Main.dedServ)
                    SoundEngine.PlaySound(SoundID.Item117 with { Volume = 0.7f, Pitch = -0.2f }, Projectile.Center);
            }

            // ai[0] counts spells launched through the portal (the caster bumps it and syncs it). When it changes,
            // the portal flashes and sparks, on every game, so other players can see it being used.
            if (Projectile.ai[0] != lastLaunches)
            {
                lastLaunches = Projectile.ai[0];
                flash = 8;
                if (!Main.dedServ && !Disconnected)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.PinkTorch, Main.rand.NextVector2Circular(2.5f, 2.5f), 100, default, 1.1f);
                        d.noGravity = true;
                    }
                }
            }

            if (flash > 0)
                flash--;

            if (!Disconnected)
            {
                Lighting.AddLight(Projectile.Center, 0.7f, 0.25f, 0.55f);

                // Sparks spiral in toward the middle of the gate.
                if (Main.rand.NextBool(2))
                {
                    float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                    Vector2 spot = Projectile.Center + angle.ToRotationVector2() * Radius;
                    Dust d = Dust.NewDustPerfect(spot, DustID.PinkTorch, (Projectile.Center - spot) * 0.08f, 100, default, 0.9f);
                    d.noGravity = true;
                }
                return;
            }

            Lighting.AddLight(Projectile.Center, 0.2f, 0.22f, 0.3f);

            if (Main.dedServ)
                return;

            // The call ends: a click, then the busy tone.
            int sinceDisconnect = elapsed - LongDistanceRelationshipAugment.PortalTicks;
            if (!hungUp)
            {
                hungUp = true;
                if (HangUpSound.HasValue)
                    SoundEngine.PlaySound(HangUpSound.Value, Projectile.Center);
                else
                    SoundEngine.PlaySound(SoundID.MenuClose with { Volume = 0.8f, Pitch = -0.3f }, Projectile.Center);

                for (int i = 0; i < 12; i++)
                {
                    Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Smoke, Main.rand.NextVector2Circular(2.5f, 2.5f), 120, new Color(150, 160, 180), 1.1f);
                    d.noGravity = true;
                }
            }
            else if (!HangUpSound.HasValue && (sinceDisconnect == 10 || sinceDisconnect == 20 || sinceDisconnect == 30))
            {
                // No custom sound file: fake a busy tone with three low ticks.
                SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.7f, Pitch = -0.7f }, Projectile.Center);
            }
        }

        // The hang-up sound is a file in the mod's Sounds/LongDistance folder, so it can be swapped without touching the code.
        // If the file isn't there, the sound falls back to the vanilla tones above.
        private const string HangUpSoundPath = "Augments/Sounds/LongDistance/hangup";
        private static bool hangUpChecked;
        private static SoundStyle? hangUpSound;

        private static SoundStyle? HangUpSound
        {
            get
            {
                if (!hangUpChecked)
                {
                    hangUpChecked = true;
                    if (ModContent.HasAsset(HangUpSoundPath))
                        hangUpSound = new SoundStyle(HangUpSoundPath) { Volume = 0.85f };
                }

                return hangUpSound;
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            if (Disconnected)
            {
                DrawDisconnected();
                return false;
            }

            Player owner = Main.player[Projectile.owner];
            int elapsed = Elapsed;
            float fade = Math.Min(1f, elapsed / 10f);
            float pulse = 0.75f + 0.25f * (float)Math.Sin(elapsed * 0.3f);
            Vector2 center = Projectile.Center;

            // Three broken rings turning against each other, like the hacking minigame's target rings.
            float surge = flash / 8f;
            AutomataFx.DashedRing(center, Radius + surge * 6f, 12, 0.6f + surge * 0.3f, elapsed * 0.04f, 3f + surge * 2f, AutomataFx.Pink * ((pulse + surge) * fade));
            AutomataFx.DashedRing(center, Radius * 0.72f, 8, 0.7f, -elapsed * 0.07f, 2f, AutomataFx.White * (0.85f * fade));
            AutomataFx.DashedRing(center, Radius * 0.48f, 5, 0.65f, elapsed * 0.1f, 2f, AutomataFx.Pink * (0.9f * fade));

            // Four data cubes circling the outer ring.
            for (int i = 0; i < 4; i++)
            {
                float angle = elapsed * 0.05f + i * MathHelper.PiOver2;
                AutomataFx.Rect(center + angle.ToRotationVector2() * (Radius + 9f), 5f, 5f, angle, AutomataFx.White * fade);
            }

            // The heart, beating.
            float beat = 1f + 0.18f * (float)Math.Max(0f, Math.Sin(elapsed * 0.22f));
            AutomataFx.PixelHeart(center, 3.2f * beat, AutomataFx.Pink * fade);

            // The link back to the caster: a faint line with data packets streaming toward the portal.
            Vector2 from = owner.Center;
            Vector2 delta = center - from;
            float length = delta.Length();
            if (length > 70f)
            {
                Vector2 dir = delta / length;
                DangerCloseVisuals.Line(from + dir * 30f, center - dir * (Radius + 12f), 1f, AutomataFx.Pink * (0.18f * fade));
                for (float d = 30f + (elapsed * 3f) % 44f; d < length - Radius - 12f; d += 44f)
                    AutomataFx.Rect(from + dir * d, 4f, 4f, elapsed * 0.1f, AutomataFx.Pink * (0.6f * fade));
            }

            DangerCloseVisuals.Text("CONNECTED", center + new Vector2(0f, -Radius - 18f), new Color(255, 160, 210) * (pulse * fade), 0.55f);
            return false;
        }

        // The last second: the heart breaks, the rings die, a sad face and "DISCONNECTED" are left behind.
        private void DrawDisconnected()
        {
            int t = Elapsed - LongDistanceRelationshipAugment.PortalTicks;
            Vector2 center = Projectile.Center;
            float fadeOut = Math.Min(1f, Projectile.timeLeft / 15f);

            Color grey = new Color(150, 160, 185, 0);
            Color red = new Color(255, 80, 80, 0);

            // The rings stutter and collapse inward over the first few frames.
            if (t < 12 && t % 3 != 2)
            {
                float shrink = 1f - t / 12f;
                AutomataFx.DashedRing(center, Radius * shrink, 12, 0.4f, t * 0.2f, 2f, grey * shrink);
                AutomataFx.DashedRing(center, Radius * 0.6f * shrink, 7, 0.5f, -t * 0.3f, 2f, grey * shrink);
            }

            // The heart splits down the middle; the halves drift apart and fall.
            if (t < 22)
            {
                float apart = t * 1.1f;
                float drop = t * t * 0.12f;
                float alpha = 1f - t / 22f;
                AutomataFx.PixelHeartHalf(center, 3.2f, AutomataFx.Pink * alpha, true, apart, drop);
                AutomataFx.PixelHeartHalf(center, 3.2f, AutomataFx.Pink * alpha, false, apart, drop);
            }

            // Then the sad face.
            if (t >= 10)
            {
                float appear = Math.Min(1f, (t - 10) / 8f);
                AutomataFx.PixelSadFace(center, 5f, t, grey * (appear * fadeOut), AutomataFx.Cyan * (appear * fadeOut));
            }

            // The status line flickers in red.
            if (t >= 6 && (t % 12 < 8))
                DangerCloseVisuals.Text("DISCONNECTED", center + new Vector2(0f, -Radius - 18f), new Color(255, 90, 90) * fadeOut, 0.6f);
        }
    }
}
