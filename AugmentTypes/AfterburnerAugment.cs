using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace Augments
{
    public class AfterburnerAugment : Augment
    {
        public override string Id => "afterburner";
        public override string DisplayName => "Afterburner";
        public override string Description =>
            $"While airborne: {AugmentText.BonusDamage("+25% ranged damage")}, {AugmentText.Crit("+12% ranged crit chance")} " +
            $"and {AugmentText.AttackSpeed("+10% ranged attack speed")}.\n" +
            $"Firing a ranged weapon in the air pushes you {AugmentText.MovementSpeed("backwards")}, opposite your aim. " +
            $"Slower, heavier weapons push harder.\n" +
            $"You take no fall damage and gain {AugmentText.Duration("+25% wing flight time")}.\n" +
            AugmentText.Note("(No push on the ground or while grappling.)");

        public override AugmentRarity Rarity => AugmentRarity.Epic;
        public override AugmentClass Class => AugmentClass.Ranged;

        private const float DamageBonus = 0.25f;
        private const float CritBonus = 12f;
        private const float AttackSpeedBonus = 0.10f;
        private const float WingTimeMultiplier = 1.25f;

        private const float PushPerUseTime = 0.15f;
        private const float MinPush = 1.2f;
        private const float MaxPush = 5f;
        private const float PushSpeedCap = 12f;

        private static uint lastRecoilTick;

        private static bool IsAirborne(Player player) => player.velocity.Y != 0f;

        public override void UpdateEquips(Player player)
        {
            player.noFallDmg = true;
            player.wingTimeMax = (int)(player.wingTimeMax * WingTimeMultiplier);

            if (!IsAirborne(player))
                return;

            player.GetDamage(DamageClass.Ranged) += DamageBonus;
            player.GetCritChance(DamageClass.Ranged) += CritBonus;
            player.GetAttackSpeed(DamageClass.Ranged) += AttackSpeedBonus;
        }

        public override void OnShootProjectile(Player player, Item item, Projectile projectile)
        {
            if (player.whoAmI != Main.myPlayer || item.DamageType != DamageClass.Ranged)
                return;

            if (!IsAirborne(player) || player.grapCount > 0)
                return;

            // Shotguns and multi-shot weapons spawn several projectiles in one use.
            if (lastRecoilTick == Main.GameUpdateCount)
                return;
            lastRecoilTick = Main.GameUpdateCount;

            if (projectile.velocity == Vector2.Zero)
                return;

            float push = MathHelper.Clamp(item.useTime * PushPerUseTime, MinPush, MaxPush);
            float oldSpeed = player.velocity.Length();
            player.velocity -= Vector2.Normalize(projectile.velocity) * push;

            float cap = System.Math.Max(oldSpeed, PushSpeedCap);
            if (player.velocity.Length() > cap)
                player.velocity = Vector2.Normalize(player.velocity) * cap;
        }
    }
}
