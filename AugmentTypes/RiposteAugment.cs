using Terraria;
using Terraria.ModLoader;

namespace Augments
{
    public class RiposteAugment : Augment
    {
        public override string Id => "riposte";
        public override string DisplayName => "Riposte";
        public override string Description =>
            $"After an enemy hits you, you have {AugmentText.Duration("2 seconds")} to land a melee hit. That hit deals " +
            $"{AugmentText.BonusDamage("+20% bonus damage")} based on the weapon's base damage.";

        public override AugmentRarity Rarity => AugmentRarity.Common;
        public override AugmentClass Class => AugmentClass.Melee;

        private const int WindowTicks = 120;
        private const float BonusDamagePercent = 0.20f;

        public override void OnHurt(Player player, Player.HurtInfo info)
        {
            player.GetModPlayer<AugmentPlayer>().RiposteWindowRemaining = WindowTicks;
        }

        public override void OnUpdate(Player player)
        {
            var ap = player.GetModPlayer<AugmentPlayer>();
            if (ap.RiposteWindowRemaining > 0)
                ap.RiposteWindowRemaining--;
        }

        public override void ModifyHitNPCWithItem(Player player, Item item, NPC target, ref NPC.HitModifiers modifiers)
        {
            if (player.GetModPlayer<AugmentPlayer>().RiposteWindowRemaining > 0 && item.CountsAsClass(DamageClass.Melee))
                Consume(player, ref modifiers, item.damage);
        }

        public override void ModifyHitNPCWithProj(Player player, Projectile proj, NPC target, ref NPC.HitModifiers modifiers)
        {
            if (player.GetModPlayer<AugmentPlayer>().RiposteWindowRemaining > 0 && proj.CountsAsClass(DamageClass.Melee))
                Consume(player, ref modifiers, proj.damage);
        }

        private static void Consume(Player player, ref NPC.HitModifiers modifiers, int baseDamage)
        {
            modifiers.FlatBonusDamage += (int)(baseDamage * BonusDamagePercent);
            player.GetModPlayer<AugmentPlayer>().RiposteWindowRemaining = 0;
        }
    }
}
