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

        // Floating heal text is grouped: one "+5" per second instead of five "+1".
        private const int PulsesPerText = 5;

        // The drone stays on its patient for at least a second, and only switches
        // when someone else is clearly worse off, so it doesn't flicker between
        // two equally hurt players.
        private const int MinHoldTicks = 60;
        private const float SwitchMargin = 0.15f;

        private const float EnemyAwareRange = 450f;

        private int targetIndex = -1;
        private int lastTargetIndex = -1;
        private int enemyIndex = -1;
        private int stickyTicks;
        private int pulseTimer;
        private int pulsesSinceText;
        private int healedSinceText;
        private int blinkTicksLeft;
        private int ticksToNextBlink = 150;
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

            if (t % RetargetIntervalTicks == 0)
            {
                targetIndex = PickTarget(owner);
                enemyIndex = targetIndex >= 0 ? -1 : FindNearestEnemy();
            }
            else if (targetIndex >= 0 && !IsValidTarget(owner, Main.player[targetIndex]))
            {
                targetIndex = -1;
            }

            stickyTicks = targetIndex >= 0 ? stickyTicks + 1 : 0;

            if (targetIndex != lastTargetIndex)
            {
                lastTargetIndex = targetIndex;
                pulsesSinceText = 0;
                healedSinceText = 0;
                pulseTimer = 0;
            }

            FollowOwner(owner, t);

            if (targetIndex >= 0)
            {
                pulseTimer++;
                if (pulseTimer >= HealIntervalTicks)
                {
                    pulseTimer = 0;
                    HealPulse(owner);
                }
            }

            UpdateBlink();
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

            // Facing: the patient first, then a nearby enemy (leaning toward it),
            // otherwise where the owner faces, with an occasional look the other way.
            int direction = owner.direction;
            float lean = 0f;
            if (targetIndex >= 0)
            {
                direction = Main.player[targetIndex].Center.X >= Projectile.Center.X ? 1 : -1;
            }
            else if (enemyIndex >= 0 && Main.npc[enemyIndex].active)
            {
                direction = Main.npc[enemyIndex].Center.X >= Projectile.Center.X ? 1 : -1;
                lean = direction * 0.14f;
            }
            else
            {
                float cycle = t % 420f;
                if (cycle >= 300f && cycle < 360f)
                    direction = -owner.direction;
            }

            Projectile.spriteDirection = direction;
            float desiredRotation = Projectile.velocity.X * 0.04f + lean;
            Projectile.rotation = MathHelper.Lerp(Projectile.rotation, desiredRotation, 0.15f);
        }

        // Runs once per heal pulse on every client and the server.
        private void HealPulse(Player owner)
        {
            Player target = Main.player[targetIndex];
            if (!IsValidTarget(owner, target))
                return;

            // The server (or singleplayer) applies the actual heal. Floating text is
            // suppressed on four pulses and shown as a single grouped "+5" on the fifth.
            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                pulsesSinceText++;
                bool showText = pulsesSinceText >= PulsesPerText;
                int display = showText ? healedSinceText + HealPerPulse : 0;

                int healed = SupportEffects.ServerHealPlayer(target, HealPerPulse, display);
                if (showText)
                {
                    pulsesSinceText = 0;
                    healedSinceText = 0;
                }
                else
                {
                    healedSinceText += healed;
                }
            }

            // The owner's client records the healing for the analytics panel.
            if (Projectile.owner == Main.myPlayer)
                AugmentDamageTracker.RecordHealing(AutopilotAugment.ChipId, HealPerPulse);
        }

        private int PickTarget(Player owner)
        {
            int best = FindTarget(owner);
            if (best == targetIndex)
                return best;

            if (targetIndex >= 0 && best >= 0 && IsValidTarget(owner, Main.player[targetIndex]))
            {
                float currentRatio = LifeRatio(Main.player[targetIndex]);
                float bestRatio = LifeRatio(Main.player[best]);
                if (stickyTicks < MinHoldTicks || bestRatio > currentRatio - SwitchMargin)
                    return targetIndex;
            }

            stickyTicks = 0;
            return best;
        }

        private static float LifeRatio(Player player)
        {
            int maxLife = Math.Max(player.statLifeMax, player.statLifeMax2);
            return maxLife > 0 ? (float)player.statLife / maxLife : 1f;
        }

        private int FindNearestEnemy()
        {
            int best = -1;
            float bestDistanceSq = EnemyAwareRange * EnemyAwareRange;
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (!npc.active || !npc.CanBeChasedBy())
                    continue;

                float d = Vector2.DistanceSquared(npc.Center, Projectile.Center);
                if (d < bestDistanceSq)
                {
                    bestDistanceSq = d;
                    best = i;
                }
            }

            return best;
        }

        // Occasional blink of the lens while idle.
        private void UpdateBlink()
        {
            if (blinkTicksLeft > 0)
            {
                blinkTicksLeft--;
                return;
            }

            if (--ticksToNextBlink <= 0)
            {
                blinkTicksLeft = 7;
                ticksToNextBlink = Main.rand.Next(150, 420);
            }
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

            if (blinkTicksLeft > 0)
            {
                // Cover the lens (a 4x5 area on the front of the sprite) and leave a thin slit.
                Texture2D pixel = TextureAssets.MagicPixel.Value;
                Vector2 lensOffset = new Vector2(8f * Projectile.spriteDirection, -0.5f).RotatedBy(Projectile.rotation);
                DrawSquare(pixel, Projectile.Center + lensOffset, 5, Color.Lerp(new Color(44, 48, 60), lightColor, 0.4f));
                DrawSquare(pixel, Projectile.Center + lensOffset, 2, new Color(150, 35, 28));
            }

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
