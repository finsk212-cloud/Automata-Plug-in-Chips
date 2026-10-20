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

            if (Main.GameUpdateCount % 4 != 0)
                return;

            // One violet pip per stack above the enemy's head, turning gold at full stacks.
            int dustType = stacks >= StacksRequired ? DustID.GoldFlame : DustID.PurpleTorch;
            for (int i = 0; i < stacks; i++)
            {
                float offset = (i - (stacks - 1) * 0.5f) * 10f;
                Vector2 position = new Vector2(npc.Center.X + offset, npc.Top.Y - 14f);
                Dust dust = Dust.NewDustPerfect(position, dustType, Vector2.Zero, 100, default, 0.9f);
                dust.noGravity = true;
            }
        }
    }
}
