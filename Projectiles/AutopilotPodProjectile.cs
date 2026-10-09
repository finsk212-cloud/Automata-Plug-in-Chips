using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace Augments
{
    // The Autopilot chip's drone. Every client (and the server) runs the same
    // follow and targeting logic from synced player data, so no per-frame
    // packets are needed. Only the server (or singleplayer) applies healing,
    // through SupportEffects.ServerHealPlayer, which handles syncing.
    public class AutopilotPodProjectile : ModProjectile
    {
        public override string Texture => "Augments/Projectiles/AutopilotPodProjectile";

        private const float HealRange = 700f;
        private const int HealIntervalTicks = 12; // 5 pulses per second
        private const int HealPerPulse = 1;       // +1 HP each pulse = 5 HP per second
        private const int RetargetIntervalTicks = 6;

        private int targetIndex = -1;
        private int healTimer;
        private Vector2 wireEnd;
        private bool wireReady;

        public override void SetDefaults()
        {
            Projectile.width = 24;
            Projectile.height = 18;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.netImportant = true;
            Projectile.timeLeft = 5;
        }

        public override bool? CanDamage() => false;

        public override void AI()
        {
            Player owner = Main.player[Projectile.owner];
            if (!owner.active || owner.dead)
            {
                Projectile.Kill();
                return;
            }

            // Only the owner decides when the drone goes away (chip removed);
            // the kill is then synced to everyone else.
            if (Projectile.owner == Main.myPlayer && !owner.GetModPlayer<AugmentPlayer>().HasAugment(AutopilotAugment.ChipId))
            {
                Projectile.Kill();
                return;
            }

            Projectile.timeLeft = 5;
            float t = Projectile.localAI[0]++;

            FollowOwner(owner, t);

            if (t % RetargetIntervalTicks == 0)
                targetIndex = FindTarget(owner);
            else if (targetIndex >= 0 && !IsValidTarget(owner, Main.player[targetIndex]))
                targetIndex = -1;

            if (Main.netMode != NetmodeID.MultiplayerClient && targetIndex >= 0)
            {
                healTimer++;
                if (healTimer >= HealIntervalTicks)
                {
                    healTimer = 0;
                    SupportEffects.ServerHealPlayer(Main.player[targetIndex], HealPerPulse);
                }
            }
            else if (targetIndex < 0)
            {
                healTimer = 0;
            }

            UpdateWire(t);

            // Visual tick: a small spark where the wire plugs in, once per pulse.
            if (targetIndex >= 0 && Main.netMode != NetmodeID.Server && t % HealIntervalTicks == 0)
            {
                Dust dust = Dust.NewDustPerfect(wireEnd, DustID.GreenFairy, Main.rand.NextVector2Circular(1f, 1f), 100, default, 0.9f);
                dust.noGravity = true;
            }

            Lighting.AddLight(Projectile.Center, 0.05f, 0.28f, 0.14f);
        }

        private void FollowOwner(Player owner, float t)
        {
            Vector2 anchor = owner.Center + new Vector2(-owner.direction * 34f, -46f + MathF.Sin(t * 0.06f) * 5f);
            Vector2 delta = anchor - Projectile.Center;

            if (delta.LengthSquared() > 1400f * 1400f)
            {
                Projectile.Center = anchor;
                Projectile.velocity = Vector2.Zero;
                wireReady = false;
                return;
            }

            Vector2 desired = delta * 0.12f;
            if (desired.Length() > 18f)
                desired = Vector2.Normalize(desired) * 18f;

            Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.35f);
            Projectile.rotation = Projectile.velocity.X * 0.04f;

            // Face the patient while healing, otherwise face where the owner faces.
            Projectile.spriteDirection = targetIndex >= 0
                ? (Main.player[targetIndex].Center.X >= Projectile.Center.X ? 1 : -1)
                : owner.direction;
        }

        private static bool IsValidTarget(Player owner, Player candidate)
        {
            if (candidate == null || !candidate.active || candidate.dead || candidate.ghost)
                return false;
            if (!SupportEffects.AreAllies(owner, candidate))
                return false;
            if (Vector2.DistanceSquared(candidate.Center, owner.Center) > HealRange * HealRange)
                return false;

            int maxLife = Math.Max(candidate.statLifeMax, candidate.statLifeMax2);
            return maxLife > 0 && candidate.statLife < maxLife;
        }

        // Lowest health percentage wins, so a 100 HP player at 20 HP is
        // preferred over a 500 HP player at 300 HP. Players only, never NPCs.
        private static int FindTarget(Player owner)
        {
            int best = -1;
            float bestRatio = 1f;
            for (int i = 0; i < Main.maxPlayers; i++)
            {
                Player candidate = Main.player[i];
                if (!IsValidTarget(owner, candidate))
                    continue;

                int maxLife = Math.Max(candidate.statLifeMax, candidate.statLifeMax2);
                float ratio = (float)candidate.statLife / maxLife;
                if (ratio < bestRatio)
                {
                    bestRatio = ratio;
                    best = i;
                }
            }

            return best;
        }

        // The loose end of the wire eases toward the patient when healing, and
        // swings gently under the pod otherwise.
        private void UpdateWire(float t)
        {
            Vector2 hang = Projectile.Center + new Vector2(MathF.Sin(t * 0.05f) * 7f, 26f);
            Vector2 desired = targetIndex >= 0 ? Main.player[targetIndex].Center + new Vector2(0f, -4f) : hang;

            wireEnd = wireReady ? Vector2.Lerp(wireEnd, desired, 0.18f) : desired;
            wireReady = true;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            if (Main.netMode == NetmodeID.Server)
                return false;

            DrawWire();

            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            SpriteEffects fx = Projectile.spriteDirection >= 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
            Color drawColor = Color.Lerp(lightColor, Color.White, 0.55f);

            Main.EntitySpriteDraw(tex, pos, null, drawColor, Projectile.rotation, tex.Size() * 0.5f, 1f, fx, 0);
            return false;
        }

        // Everything below is drawn as plain pixel-sized rectangles (no rotation
        // or texture scaling), the same way the rest of the mod's UI draws, so it
        // renders at exact sizes whatever the shared pixel texture's dimensions.
        private static void DrawSquare(Texture2D pixel, Vector2 worldCenter, int size, Color color)
        {
            Vector2 screen = worldCenter - Main.screenPosition;
            Main.spriteBatch.Draw(pixel, new Rectangle((int)MathF.Round(screen.X - size * 0.5f), (int)MathF.Round(screen.Y - size * 0.5f), size, size), color);
        }

        private void DrawWire()
        {
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Vector2 start = Projectile.Center + new Vector2(0f, 6.5f);
            Vector2 end = wireEnd;

            float distance = Vector2.Distance(start, end);
            if (distance > 1500f)
                return;

            float sag = MathHelper.Clamp(distance * 0.35f, 10f, 70f);
            Vector2 control = (start + end) * 0.5f + new Vector2(0f, sag);

            // Dense samples along the curve; dark casing first, bright core on top.
            int samples = Math.Clamp((int)(distance * 0.9f) + 8, 8, 600);
            for (int i = 0; i <= samples; i++)
                DrawSquare(pixel, QuadBezier(start, control, end, i / (float)samples), 3, new Color(14, 70, 38));
            for (int i = 0; i <= samples; i++)
                DrawSquare(pixel, QuadBezier(start, control, end, i / (float)samples), 2, new Color(80, 255, 140));

            // Plug at the loose end.
            DrawSquare(pixel, end, 5, new Color(14, 70, 38));
            DrawSquare(pixel, end, 3, new Color(150, 255, 190));

            // A bright pulse travels down the wire on every heal tick.
            if (targetIndex >= 0)
            {
                float u = (Projectile.localAI[0] % HealIntervalTicks) / HealIntervalTicks;
                DrawSquare(pixel, QuadBezier(start, control, end, u), 4, new Color(210, 255, 225));
            }
        }

        private static Vector2 QuadBezier(Vector2 a, Vector2 b, Vector2 c, float u)
        {
            float inv = 1f - u;
            return inv * inv * a + 2f * inv * u * b + u * u * c;
        }
    }
}
