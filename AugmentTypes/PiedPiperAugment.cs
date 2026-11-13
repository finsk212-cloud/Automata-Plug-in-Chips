using Terraria;

namespace Augments
{
    // NOTE ON DESIGN: true aggro redirection (forcing nearby enemies to target
    // a minion instead of the player) isn't achievable through any vanilla or
    // tModLoader hook - npc.target is always an index into Main.player[], with
    // no equivalent concept of an NPC targeting a projectile/minion. This is a
    // fallback approximation instead: a periodic incoming-damage-reduction
    // window, gated on having an active minion, simulating "your minion is
    // drawing some attention" without literally touching enemy AI targeting.
    public class PiedPiperAugment : Augment
    {
        public override string Id => "pied_piper";
        public override string DisplayName => "Pied Piper";
        public override string Description =>
            $"While you have an active minion, gain {AugmentText.Defense("25% damage reduction")} for {AugmentText.Duration("3 seconds")} every {AugmentText.Cooldown("8 seconds")}.";

        public override AugmentRarity Rarity => AugmentRarity.Epic;
        public override AugmentClass Class => AugmentClass.Summon;

        private const int CooldownTicks = 480;
        private const int DurationTicks = 180;
        private const float IncomingDamageReductionPercent = 0.25f;

        public override int CooldownRemaining => LocalPlayerState.PiedPiperCooldown;

        public override void OnUpdate(Player player)
        {
            var ap = player.GetModPlayer<AugmentPlayer>();
            if (ap.PiedPiperCooldown > 0)
                ap.PiedPiperCooldown--;

            if (ap.PiedPiperDurationRemaining > 0)
                ap.PiedPiperDurationRemaining--;

            if (ap.PiedPiperCooldown <= 0 && HasActiveMinion(player))
            {
                ap.PiedPiperDurationRemaining = DurationTicks;
                ap.PiedPiperCooldown = CooldownTicks;
            }
        }

        // proj.minion is true for the minion's own persistent body (Pygmies,
        // Spider Staff, Hornet, etc), regardless of whether that minion's
        // actual attacks fire as separate non-minion-flagged projectiles
        // (ProjectileID.Sets.MinionShot) - same distinction MinionMomentum
        // already had to learn. Checking for the body is enough here, since
        // all that matters is "is a minion currently out".
        private static bool HasActiveMinion(Player player)
        {
            foreach (var proj in Main.projectile)
            {
                if (proj.active && proj.owner == player.whoAmI && proj.minion)
                    return true;
            }
            return false;
        }

        public override void ModifyHurt(Player player, ref Player.HurtModifiers modifiers)
        {
            if (player.GetModPlayer<AugmentPlayer>().PiedPiperDurationRemaining > 0)
                modifiers.FinalDamage *= 1f - IncomingDamageReductionPercent;
        }
    }
}
