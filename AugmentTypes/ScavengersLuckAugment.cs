using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using Augments.Core;

namespace Augments
{
    public class ScavengersLuckAugment : Augment
    {
        public override string Id => "scavengers_luck";
        public override string DisplayName => "Scavenger's Luck";
        public override string Description =>
            $"Grants {AugmentText.Crit("+5% Fortune")} (World Luck & lucky trigger chance). Defeated enemies have a {FormatCurrentChance(ProcChance)} " +
            $"to grant {AugmentText.Crit("+15% crit chance")} for {AugmentText.Duration("5s")}.";

        public override string GetDetailedDescription(Player player) =>
            $"Grants {AugmentText.Crit("+5% Fortune")} (World Luck & lucky trigger chance). Defeated enemies have a {FormatFortuneChance(ProcChance, player)} " +
            $"to grant {AugmentText.Crit("+15% crit chance")} for {AugmentText.Duration("5s")}.";

        public override AugmentRarity Rarity => AugmentRarity.Rare;
        public override AugmentClass Class => AugmentClass.Universal;
        public override string FamilyId => AugmentFamilyRegistry.FortuneId;

        public override bool IsLuckyThemed => true;
        public override float FortuneBonus => 0.05f;

        private const float ProcChance = 0.2f;
        private const int BuffDurationTicks = 300;
        private const float CritBonus = 15f;

        // Shows "+15%" in the cooldown/status row while the crit buff is
        // active, same StatusValue mechanism AdaptiveArmorAugment uses for
        // its growing defense bonus.
        public override int? StatusValue => LocalPlayerState.ScavengersLuckBuffTicks > 0 ? (int)CritBonus : (int?)null;
        public override Color StatusValueColor => AugmentTextColors.Crit;
        public override string StatusValueSuffix => "%";

        // Hooking kill credit (see AugmentGlobalNPC.OnKill, keyed off
        // npc.lastInteraction) instead of a hit-based check - this catches
        // kills finished off by a DoT tick too, same shared system
        // SwarmTacticsAugment/PlagueBearerAugment rely on. No DamageType
        // restriction here, so it procs off kills from any damage class.
        public override void OnKillNPC(Player player, NPC npc)
        {
            if (Main.rand.NextFloat() < ProcChance * (1f + player.GetModPlayer<AugmentPlayer>().TotalFortune))
                player.GetModPlayer<AugmentPlayer>().ScavengersLuckBuffTicks = BuffDurationTicks;
        }

        public override void OnUpdate(Player player)
        {
            var ap = player.GetModPlayer<AugmentPlayer>();
            if (ap.ScavengersLuckBuffTicks > 0)
                ap.ScavengersLuckBuffTicks--;
        }

        public override void ModifyWeaponCrit(Player player, Item item, ref float crit)
        {
            if (player.GetModPlayer<AugmentPlayer>().ScavengersLuckBuffTicks > 0)
                crit += CritBonus;
        }
    }
}
