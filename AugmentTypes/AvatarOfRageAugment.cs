using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Augments
{
    public class AvatarOfRageAugment : Augment
    {
        public override string Id => "type_b_berserker_protocol";
        public override string DisplayName => "Type-B: Berserker Protocol";
        public override string Description =>
            $"While below {AugmentText.HP("50% HP")}, enter {AugmentText.Crit("Berserk Overclock")}: gain " +
            $"{AugmentText.BonusDamage("+40% damage")}, {AugmentText.MovementSpeed("+15% movement speed")}, and {AugmentText.AttackSpeed("+10% attack speed")}. " +
            $"Potion sickness duration is increased by {AugmentText.Duration("+15 seconds")}.";

        public override AugmentRarity Rarity => AugmentRarity.Epic;
        public override AugmentClass Class => AugmentClass.Universal;

        public override string KeystoneFamily => "path_of_the_berserker";
        public override bool IsPermanent => true;

        private const float BonusDamagePercent = 0.40f;
        private const float MoveSpeedBonus = 0.15f;
        private const float AttackSpeedBonus = 0.10f;

        private static bool IsBerserk(Player player) => player.statLife <= (int)(player.statLifeMax2 * 0.5f);

        public override void UpdateEquips(Player player)
        {
            if (IsBerserk(player))
            {
                player.moveSpeed += MoveSpeedBonus;
                player.GetAttackSpeed(DamageClass.Generic) += AttackSpeedBonus;

                if (Main.rand.NextBool(4))
                {
                    Dust d = Dust.NewDustDirect(player.position, player.width, player.height, DustID.CrimsonTorch, 0f, 0f, 150, default, 1.1f);
                    d.noGravity = true;
                    d.velocity *= 0.5f;
                }
            }
        }

        public override void ModifyHitNPCWithItem(Player player, Item item, NPC target, ref NPC.HitModifiers modifiers)
        {
            if (IsBerserk(player))
                modifiers.FlatBonusDamage += (int)(item.damage * BonusDamagePercent);
        }

        public override void ModifyHitNPCWithProj(Player player, Projectile proj, NPC target, ref NPC.HitModifiers modifiers)
        {
            if (IsBerserk(player))
                modifiers.FlatBonusDamage += (int)(proj.damage * BonusDamagePercent);
        }
    }
}
