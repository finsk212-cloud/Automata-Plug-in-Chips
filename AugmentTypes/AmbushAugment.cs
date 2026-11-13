using Terraria;
using Terraria.ModLoader;

namespace Augments
{
    public class AmbushAugment : Augment
    {
        public override string Id => "ambush";
        public override string DisplayName => "Ambush";
        public override string Description =>
            $"Stand still for {AugmentText.Duration("2 seconds")} to prime your next melee hit. The primed hit deals " +
            $"{AugmentText.BonusDamage("+25% bonus damage")} based on the weapon's base damage.";

        public override AugmentRarity Rarity => AugmentRarity.Common;
        public override AugmentClass Class => AugmentClass.Melee;

        private const int StillTicksRequired = 120;
        private const float BonusDamagePercent = 0.25f;

        public override void OnUpdate(Player player)
        {
            var ap = player.GetModPlayer<AugmentPlayer>();
            bool isStill = player.velocity.LengthSquared() < 0.25f;

            if (!isStill)
            {
                ap.AmbushStillTicks = 0;
                return;
            }

            if (ap.AmbushReady)
                return;

            ap.AmbushStillTicks++;
            if (ap.AmbushStillTicks == StillTicksRequired)
            {
                ap.AmbushReady = true;
                // "Primed" popup is a local-feedback message for the owner only.
                if (player.whoAmI == Main.myPlayer)
                    Main.NewText("Ambush primed!", 200, 200, 255);
            }
        }

        public override void ModifyHitNPCWithItem(Player player, Item item, NPC target, ref NPC.HitModifiers modifiers)
        {
            if (player.GetModPlayer<AugmentPlayer>().AmbushReady && item.CountsAsClass(DamageClass.Melee))
                Consume(player, ref modifiers, item.damage);
        }

        public override void ModifyHitNPCWithProj(Player player, Projectile proj, NPC target, ref NPC.HitModifiers modifiers)
        {
            if (player.GetModPlayer<AugmentPlayer>().AmbushReady && proj.CountsAsClass(DamageClass.Melee))
                Consume(player, ref modifiers, proj.damage);
        }

        private static void Consume(Player player, ref NPC.HitModifiers modifiers, int baseDamage)
        {
            var ap = player.GetModPlayer<AugmentPlayer>();
            modifiers.FlatBonusDamage += (int)(baseDamage * BonusDamagePercent);
            ap.AmbushReady = false;
            ap.AmbushStillTicks = 0;
        }
    }
}
