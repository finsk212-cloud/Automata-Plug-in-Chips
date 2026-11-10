using Terraria;

namespace Augments
{
    public class VoidStepAugment : Augment
    {
        public override string Id => "void_step";
        public override bool RunsKillHookOnKillerClient => true;
        public override string DisplayName => "Void Step";
        public override string Description =>
            $"Enemy kills grant {AugmentText.Defense("+2% dodge chance")}, stacking up to {AugmentText.Defense("+20%")}. " +
            $"Resets if {AugmentText.Duration("4s")} pass without a kill.\n" +
            AugmentText.Note("(Stacks independently alongside other dodge sources.)");

        public override AugmentRarity Rarity => AugmentRarity.Epic;
        public override AugmentClass Class => AugmentClass.Universal;

        private const int MaxStacks = 10;
        private const float DodgeChancePerStack = 0.02f;
        private const int ResetWindowTicks = 240;
        private const int InvulnerabilityTicks = 80;

        public override int? StatusValue => LocalPlayerState.VoidStepKillStacks > 0 ? (int)System.Math.Round(LocalPlayerState.VoidStepKillStacks * DodgeChancePerStack * 100f) : (int?)null;
        public override string StatusValueSuffix => "%";
        public override Microsoft.Xna.Framework.Color StatusValueColor => AugmentTextColors.Defense;

        // Hooking kill credit (see AugmentGlobalNPC.OnKill, keyed off
        // npc.lastInteraction) instead of a hit-based check - this catches
        // kills finished off by a DoT tick too, same shared system
        // SwarmTacticsAugment/PlagueBearerAugment rely on. No DamageType
        // restriction here, so any kill builds a stack.
        public override void OnKillNPC(Player player, NPC npc)
        {
            var ap = player.GetModPlayer<AugmentPlayer>();
            if (ap.VoidStepKillStacks < MaxStacks)
                ap.VoidStepKillStacks++;

            ap.VoidStepResetTimer = ResetWindowTicks;
        }

        public override void OnUpdate(Player player)
        {
            var ap = player.GetModPlayer<AugmentPlayer>();
            if (ap.VoidStepResetTimer > 0)
            {
                ap.VoidStepResetTimer--;
                if (ap.VoidStepResetTimer == 0)
                    ap.VoidStepKillStacks = 0;
            }

            // FreeDodge negates the hit itself, but its own follow-up invuln
            // window isn't reliable - same lesson MirrorImageAugment already
            // learned. Manually drive the invuln window instead: re-force
            // player.immune/immuneTime every tick for the full duration
            // rather than setting it once and trusting it to survive.
            if (ap.VoidStepInvulnTicks > 0)
            {
                player.immune = true;
                player.immuneTime = ap.VoidStepInvulnTicks;
                ap.VoidStepInvulnTicks--;
            }
        }

        public override bool FreeDodge(Player player, Player.HurtInfo info)
        {
            var ap = player.GetModPlayer<AugmentPlayer>();
            bool result = Main.rand.NextFloat() < ap.VoidStepKillStacks * DodgeChancePerStack;

            if (result)
                ap.VoidStepInvulnTicks = InvulnerabilityTicks;

            return result;
        }
    }
}
