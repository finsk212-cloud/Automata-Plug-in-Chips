using System.Collections.Generic;
using Terraria;

namespace Augments
{
    // On a crit, Twin Strike makes every owned augment's on-hit hook run a second time for the very same hit.
    // An augment that must act once per hit (spill the leftover damage, add a streak stack, tally damage) asks this guard
    // "is this the first time I'm seeing this hit?". A hit is identified by two small numbers (for example the target's
    // slot and the projectile's slot) within one game tick; the memory is cleared as soon as the tick changes.
    internal sealed class PerHitGuard
    {
        private uint tick;
        private readonly HashSet<int> seen = new();

        public bool IsFirst(int a, int b = 0)
        {
            if (tick != Main.GameUpdateCount)
            {
                tick = Main.GameUpdateCount;
                seen.Clear();
            }

            return seen.Add(a * 4096 + b);
        }
    }
}
