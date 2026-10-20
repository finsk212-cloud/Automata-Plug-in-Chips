using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Augments
{
    // Per-enemy Hex state for Hex Detonation. Only the casting player's client
    // reads or writes it (the detonation itself is sent as a normal strike).
    public class HexDetonationNPC : GlobalNPC
    {
        public const int StacksRequired = 5;
        public const int StackDurationTicks = 360;
        public const int StackGainIntervalTicks = 12;
        public const int ImmunityTicks = 120;

        public override bool InstancePerEntity => true;

        private int stacks;
        private uint lastGainTick;
        private uint immuneUntilTick;

        public bool IsFullyStacked => stacks >= StacksRequired && !IsExpired();

        // Adds one stack if the enemy is not in its post-detonation grace period and
        // the previous stack was not gained a moment ago. Each stack refreshes the timer.
        public void TryAddStack()
        {
            uint now = Main.GameUpdateCount;
            if (now < immuneUntilTick)
                return;

            if (IsExpired())
                stacks = 0;

            if (stacks > 0 && now - lastGainTick < StackGainIntervalTicks)
                return;

            if (stacks < StacksRequired)
                stacks++;

            lastGainTick = now;
        }

        public void Detonated()
        {
            stacks = 0;
            immuneUntilTick = Main.GameUpdateCount + ImmunityTicks;
        }

        private bool IsExpired() => stacks > 0 && Main.GameUpdateCount - lastGainTick > StackDurationTicks;

        public override void PostAI(NPC npc)
        {
            if (stacks <= 0)
                return;

            if (IsExpired())
            {
                stacks = 0;
                return;
            }

            if (Main.GameUpdateCount % 2 != 0)
                return;

            // One large violet pip per stack above the enemy's head, turning gold and bigger at full stacks.
            bool full = stacks >= StacksRequired;
            int dustType = full ? DustID.GoldFlame : DustID.PurpleTorch;
            float scale = full ? 2.3f : 1.8f;
            float headY = npc.Top.Y - 26f;
            for (int i = 0; i < stacks; i++)
            {
                float offset = (i - (stacks - 1) * 0.5f) * 18f;
                Vector2 position = new Vector2(npc.Center.X + offset, headY);
                Dust dust = Dust.NewDustPerfect(position, dustType, Vector2.Zero, 0, default, scale);
                dust.noGravity = true;
                dust.fadeIn = 1.1f;
                Lighting.AddLight(position, full ? new Vector3(0.9f, 0.7f, 0.2f) : new Vector3(0.5f, 0.2f, 0.8f));
            }

            // A gold ring circling the enemy marks it as ready to detonate.
            if (full)
            {
                float radius = System.Math.Max(npc.width, npc.height) * 0.5f + 12f;
                for (int i = 0; i < 2; i++)
                {
                    float angle = Main.GameUpdateCount * 0.18f + MathHelper.Pi * i;
                    Vector2 position = npc.Center + angle.ToRotationVector2() * radius;
                    Dust orbit = Dust.NewDustPerfect(position, DustID.GoldFlame, Vector2.Zero, 0, default, 1.6f);
                    orbit.noGravity = true;
                }
            }
        }
    }
}
