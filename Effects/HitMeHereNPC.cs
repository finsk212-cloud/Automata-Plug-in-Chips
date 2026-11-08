using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace Augments
{
    public class HitMeHereNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        public const int MarkDurationTicks = 240; // 4 seconds
        public const int RuptureCooldownTicks = 60; // 1 second internal cooldown after rupture before rescanning

        public int WeakpointTimer;
        public int RuptureCooldown;
        public int MarkedByPlayer = -1;
        public Vector2 WeakpointOffset;

        public bool IsMarked => WeakpointTimer > 0;

        public void ApplyScan(int playerIndex, Vector2 offset, int duration = MarkDurationTicks)
        {
            if (RuptureCooldown > 0)
                return;

            WeakpointTimer = duration;
            MarkedByPlayer = playerIndex;
            WeakpointOffset = offset;
        }

        public void Rupture()
        {
            WeakpointTimer = 0;
            RuptureCooldown = RuptureCooldownTicks;
            MarkedByPlayer = -1;
        }

        public override void PostAI(NPC npc)
        {
            if (RuptureCooldown > 0)
                RuptureCooldown--;

            if (WeakpointTimer <= 0)
            {
                MarkedByPlayer = -1;
                return;
            }

            WeakpointTimer--;

            // Ambient scan particles
            if (!npc.friendly && !npc.townNPC && !npc.dontTakeDamage)
            {
                Vector2 center = npc.Center + WeakpointOffset;
                if (Main.rand.NextBool(3))
                {
                    Vector2 dPos = center + Main.rand.NextVector2Circular(14f, 14f);
                    Dust d = Dust.NewDustPerfect(
                        dPos,
                        DustID.Electric,
                        Main.rand.NextVector2Circular(0.5f, 0.5f),
                        100,
                        new Color(56, 189, 248),
                        0.7f
                    );
                    d.noGravity = true;
                }

                Lighting.AddLight(center, 0.1f, 0.35f, 0.5f);
            }
        }

        public override void PostDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            if (!IsMarked || npc.friendly || npc.townNPC || npc.dontTakeDamage)
                return;

            // Only draw for the marking player or players with Hit Me Here
            if (MarkedByPlayer != Main.myPlayer && !Main.LocalPlayer.GetModPlayer<AugmentPlayer>().HasAugment("hit_me_here"))
                return;

            Vector2 worldPos = npc.Center + WeakpointOffset;
            Vector2 drawPos = worldPos - screenPos;

            // NieR-style cybernetic targeting reticle
            DrawReticle(spriteBatch, drawPos, WeakpointTimer);
        }

        private static void DrawReticle(SpriteBatch spriteBatch, Vector2 pos, int timer)
        {
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            float pulse = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 8f) * 2f;
            float baseSize = 14f + pulse;
            int s = (int)Math.Round(baseSize);

            Color cyan = new Color(56, 189, 248) * 0.9f;
            Color coreCyan = new Color(224, 242, 254) * 0.95f;
            Color crimson = new Color(244, 63, 94) * 0.85f;

            int cx = (int)Math.Round(pos.X);
            int cy = (int)Math.Round(pos.Y);

            // Draw 4 corner brackets [ ]
            int bracketLen = 5;
            // Top-left
            spriteBatch.Draw(pixel, new Rectangle(cx - s, cy - s, bracketLen, 1), cyan);
            spriteBatch.Draw(pixel, new Rectangle(cx - s, cy - s, 1, bracketLen), cyan);

            // Top-right
            spriteBatch.Draw(pixel, new Rectangle(cx + s - bracketLen + 1, cy - s, bracketLen, 1), cyan);
            spriteBatch.Draw(pixel, new Rectangle(cx + s, cy - s, 1, bracketLen), cyan);

            // Bottom-left
            spriteBatch.Draw(pixel, new Rectangle(cx - s, cy + s, bracketLen, 1), cyan);
            spriteBatch.Draw(pixel, new Rectangle(cx - s, cy + s - bracketLen + 1, 1, bracketLen), cyan);

            // Bottom-right
            spriteBatch.Draw(pixel, new Rectangle(cx + s - bracketLen + 1, cy + s, bracketLen, 1), cyan);
            spriteBatch.Draw(pixel, new Rectangle(cx + s, cy + s - bracketLen + 1, 1, bracketLen), cyan);

            // Center diamond pip
            spriteBatch.Draw(pixel, new Rectangle(cx - 1, cy - 2, 3, 5), coreCyan);
            spriteBatch.Draw(pixel, new Rectangle(cx - 2, cy - 1, 5, 3), coreCyan);
            spriteBatch.Draw(pixel, new Rectangle(cx, cy, 1, 1), crimson);
        }
    }
}
