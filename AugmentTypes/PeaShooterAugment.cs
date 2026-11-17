using Terraria;

namespace Augments
{
    // A Pod upgrade. It upgrades the Pod already floating at your shoulder to Pod 218, which shoots a small, weak bolt at the nearest enemy
    // every second. The behaviour lives in PodShooter (ticked by PodPetPlayer on the owner's machine) and the bolt is PodBoltProjectile.
    // See PodUpgrades for how upgrades work.
    public class PeaShooterAugment : Augment
    {
        public const string ChipId = "pod_shoot";

        public override string Id => ChipId;
        public override string DisplayName => "Upgrade Pod: Pea Shooter";
        public override string Description =>
            "Your Pod becomes Pod 218 and shoots a small bolt at the nearest enemy every 1 second.";

        public override string GetDetailedDescription(Player player) =>
            Description + "\n" +
            "[c/94A3B8:(Each bolt deals " + PodShooter.BaseDamage + " damage, plus a small part of your damage bonuses, so it is a little helper and never a weapon. It picks the closest enemy within about 35 tiles that it can see, and when none is around it waits and fires as soon as one shows up. Upgrades the Pod you already have instead of adding another, and works alongside other Pod upgrades. It still works while the Pod is hidden.)]";

        public override AugmentRarity Rarity => AugmentRarity.Common;
        public override AugmentClass Class => AugmentClass.Universal;
    }
}