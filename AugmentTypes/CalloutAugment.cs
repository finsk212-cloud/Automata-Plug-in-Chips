using Terraria;

namespace Augments
{
    public class CalloutAugment : Augment
    {
        public override string Id => "callout";
        public override string DisplayName => "Callout";
        public override string Description =>
            $"Your hits {AugmentText.Trigger("mark")} enemies for {AugmentText.Duration("5s")}, shown as a {AugmentText.Color("gold diamond", "FFCD46")} above them. " +
            $"Marked enemies take {AugmentText.BonusDamage("+10% damage")} from your teammates.";

        public override string GetDetailedDescription(Player player) =>
            Description + "\n" +
            "The bonus applies to every teammate's damage, including their minions, but not to your own. " +
            "Hitting a marked enemy again refreshes the mark. The diamond blinks during the last second.";

        public override AugmentRarity Rarity => AugmentRarity.Rare;
        public override AugmentClass Class => AugmentClass.Support;

        public override void OnAugmentHitNPC(Player player, NPC target, NPC.HitInfo hit, AugmentHitSource source, float effectiveness)
        {
            // Only the owner's real attacks mark; chip procs would keep it up for free.
            if (player.whoAmI != Main.myPlayer || source != AugmentHitSource.NormalAttack)
                return;

            if (target.friendly || target.townNPC || target.dontTakeDamage)
                return;

            target.GetGlobalNPC<AugmentCalloutNPC>().ApplyMark(player.whoAmI);
            AugmentNet.SendApplyNPCEffectCallout(target.whoAmI, AugmentCalloutNPC.DurationTicks, player.whoAmI);
        }
    }
}
