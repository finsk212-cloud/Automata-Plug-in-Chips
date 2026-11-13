using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace Augments
{
    // Everything you see during Run as Administrator, drawn on one projectile that follows the caster. Like the
    // Long Distance Relationship portal, it exists on every player's game, so every player sees (and hears) it, and no
    // extra network message is needed. It runs on its own clock from the moment it appears, in two short acts:
    //
    //   1. ADMIN (6 seconds): a small terminal box above the caster types "> sudo cast" and confirms root access, with a
    //      bar draining as the admin time runs out, and one green ring around the caster.
    //   2. ACCESS DENIED (3 seconds): a red padlock, "ACCESS DENIED", and a bar counting down until casting returns.
    //
    // The real gameplay timers live in RunAsAdministratorAugment; this is only the show, so a small drift between
    // the two can't change anything but the picture.
    public class AdminAuraProjectile : ModProjectile
    {
        private const int ActiveTicks = RunAsAdministratorAugment.ActiveTicks;
        private const int LockoutTicks = RunAsAdministratorAugment.LockoutTicks;
        public const int TotalTicks = ActiveTicks + LockoutTicks;

        public override string Texture => "Terraria/Images/MagicPixel";

        private bool introPlayed;
        private bool lockoutPlayed;

        public override void SetDefaults()
        {
            Projectile.width = 20;
            Projectile.height = 42;
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
            Player owner = Main.player[Projectile.owner];
            if (!owner.active || owner.dead)
            {
                Projectile.Kill();
                return;
            }

            Projectile.Center = owner.Center;
            int elapsed = Elapsed;

            // Act one begins: the power-up sound and a small green burst, once per game.
            if (!introPlayed)
            {
                introPlayed = true;
                if (!Main.dedServ)
                {
                    SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.8f, Pitch = 0.3f }, owner.Center);
                    for (int i = 0; i < 12; i++)
                    {
                        Dust d = Dust.NewDustPerfect(owner.Center, DustID.GreenTorch, Main.rand.NextVector2Circular(4f, 4f), 100, default, 1.3f);
                        d.noGravity = true;
                    }
                }
            }

            // Act two begins: the denied buzz and a small red puff, once per game.
            if (!lockoutPlayed && elapsed >= ActiveTicks)
            {
                lockoutPlayed = true;
                if (!Main.dedServ)
                {
                    SoundEngine.PlaySound(SoundID.Item15 with { Volume = 0.7f, Pitch = -0.5f }, owner.Center);
                    for (int i = 0; i < 10; i++)
                    {
                        Dust d = Dust.NewDustPerfect(owner.Center, DustID.RedTorch, Main.rand.NextVector2Circular(3f, 3f), 100, default, 1.2f);
                        d.noGravity = true;
                    }
                }
            }

            if (elapsed < ActiveTicks)
                Lighting.AddLight(owner.Center, 0.15f, 0.6f, 0.3f);
            else
                Lighting.AddLight(owner.Center, 0.6f, 0.12f, 0.1f);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Vector2 center = Main.player[Projectile.owner].Center;
            int elapsed = Elapsed;

            if (elapsed < ActiveTicks)
                DrawAdmin(center, elapsed);
            else
                DrawDenied(center, elapsed - ActiveTicks);

            return false;
        }

        // A thin outlined box, the building block of both acts.
        private static void DrawBox(Vector2 center, float width, float height, float thickness, Color color)
        {
            AutomataFx.Rect(center + new Vector2(0f, -height / 2f), width, thickness, 0f, color);
            AutomataFx.Rect(center + new Vector2(0f, height / 2f), width, thickness, 0f, color);
            AutomataFx.Rect(center + new Vector2(-width / 2f, 0f), thickness, height, 0f, color);
            AutomataFx.Rect(center + new Vector2(width / 2f, 0f), thickness, height, 0f, color);
        }

        // A bar that shrinks as time runs out, with the seconds left under it.
        private static void DrawTimerBar(Vector2 center, float width, float remaining, string label, Color fill, Color text, float fade)
        {
            DrawBox(center, width, 9f, 1.2f, AutomataFx.White * (0.7f * fade));
            AutomataFx.Rect(center + new Vector2(-(width - 4f) / 2f * (1f - remaining), 0f), (width - 4f) * remaining, 5f, 0f, fill * (0.9f * fade));
            DangerCloseVisuals.Text(label, center + new Vector2(0f, 14f), text * (0.85f * fade), 0.5f);
        }

        // --- Act one: administrator ---

        private void DrawAdmin(Vector2 center, int t)
        {
            float fade = Math.Min(1f, (t + 1) / 8f) * Math.Min(1f, (ActiveTicks - t) / 12f);
            Color green = AutomataFx.Green;
            Color textGreen = new Color(110, 255, 160);

            // One slow green ring around the caster.
            AutomataFx.DashedRing(center, 48f, 12, 0.6f, t * 0.04f, 2.5f, green * (0.75f * fade));

            // The terminal box above the caster: the command being typed, then the reply.
            Vector2 window = center + new Vector2(0f, -90f);
            DrawBox(window, 140f, 38f, 1.5f, green * (0.85f * fade));

            float left = window.X - 62f;
            string command = "> sudo cast";
            int typed = Math.Min(command.Length, (int)(t / 1.6f));
            bool cursor = (t / 8) % 2 == 0;
            AutomataFx.TextLeft(command.Substring(0, typed) + (typed < command.Length || cursor ? "_" : ""), new Vector2(left, window.Y - 7f), new Color(225, 250, 235) * fade, 0.6f);
            if (t >= 24)
                AutomataFx.TextLeft("[ OK ] root access", new Vector2(left, window.Y + 9f), textGreen * fade, 0.55f);

            // The admin time left, draining.
            float remaining = (ActiveTicks - t) / (float)ActiveTicks;
            DrawTimerBar(window + new Vector2(0f, 32f), 100f, remaining, ((ActiveTicks - t) / 60f).ToString("0.0") + " seconds", green, textGreen, fade);
        }

        // --- Act two: access denied ---

        private void DrawDenied(Vector2 center, int t)
        {
            float fade = Math.Min(1f, (t + 1) / 4f) * Math.Min(1f, (LockoutTicks - t) / 15f);
            Color alarm = AutomataFx.Alarm;

            // The padlock, "ACCESS DENIED" (blinking a little), and the countdown until casting comes back.
            AutomataFx.PixelPadlock(center + new Vector2(0f, -116f), 4f, alarm * fade);

            float blink = (t % 24) < 18 ? 1f : 0.5f;
            DangerCloseVisuals.Text("ACCESS DENIED", center + new Vector2(0f, -86f), new Color(255, 80, 80) * (fade * blink), 1f);

            float remaining = (LockoutTicks - t) / (float)LockoutTicks;
            DrawTimerBar(center + new Vector2(0f, -62f), 100f, remaining, "RETRY IN " + ((LockoutTicks - t) / 60f).ToString("0.0") + " seconds", alarm, new Color(255, 170, 170), fade);
        }
    }
}
