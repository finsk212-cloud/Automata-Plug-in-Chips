using Terraria.ModLoader;

namespace Augments
{
    public class QueensSwarmNPC : GlobalNPC
    {
        public const int StacksRequired = 5;

        public override bool InstancePerEntity => true;

        // After this enemy has released a swarm, it cannot release another for this long (3 seconds).
        public const int EnemyCooldownTicks = 180;

        private int swarmStacks;
        private uint nextReleaseTick;

        // Returns the stack count reached by this hit. Hitting the threshold
        // immediately consumes all stacks so the next hit begins at one.
        // While the enemy is cooling down, or the player's own cooldown is running (canRelease is false),
        // the last stack is held back: the enemy waits at one below the threshold instead of wasting the stacks.
        public int AddStack(bool canRelease)
        {
            bool cooling = Terraria.Main.GameUpdateCount < nextReleaseTick;

            if (swarmStacks + 1 >= StacksRequired && (cooling || !canRelease))
                return swarmStacks;

            swarmStacks++;
            int reachedStacks = swarmStacks;

            if (swarmStacks >= StacksRequired)
            {
                swarmStacks = 0;
                nextReleaseTick = Terraria.Main.GameUpdateCount + EnemyCooldownTicks;
            }

            return reachedStacks;
        }
    }
}
