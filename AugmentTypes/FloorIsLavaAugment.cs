using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Augments.Core;

namespace Augments
{
    public class FloorIsLavaAugment : Augment
    {
        public override string Id => "floor_is_lava";
        public override string DisplayName => "Floor is Lava";
        public override string Description =>
            $"Ranged explosions leave a lingering {AugmentText.Trigger("Napalm Plasma")} pool for {AugmentText.Duration("3s")} that deals continuous {AugmentText.SpecialDamage("14 fire damage")} and slows enemies by {AugmentText.Immobilize("25%")}. " +
            $"You take {AugmentText.Defense("75% less self-damage")} from your own explosives.";

        public override string GetDetailedDescription(Player player) =>
            AugmentScaling.ApplyToText(Description, GetScalingEntries()) + "\n" +
            AugmentText.Note("(Plasma pool ticks 3 times per second and scales with world tier and ranged damage. Rocket jumping and point-blank blasts deal minimal self-harm.)");

        public override AugmentRarity Rarity => AugmentRarity.Legendary;
        public override AugmentClass Class => AugmentClass.Ranged;

        public const int BaseDamage = 14;

        public override (string label, int baseValue)[] GetScalingEntries()
        {
            return new[] { ("Napalm burn", BaseDamage) };
        }

        public override void ModifyHurt(Player player, ref Player.HurtModifiers modifiers)
        {
            if (modifiers.DamageSource.SourceProjectileLocalIndex >= 0)
            {
                int projIdx = modifiers.DamageSource.SourceProjectileLocalIndex;
                if (projIdx >= 0 && projIdx < Main.maxProjectiles)
                {
                    Projectile p = Main.projectile[projIdx];
                    if (p.active && p.owner == player.whoAmI)
                    {
                        modifiers.FinalDamage *= 0.25f; // 75% less self-inflicted explosive damage
                    }
                }
            }
        }
    }

    public class FloorIsLavaGlobalProjectile : GlobalProjectile
    {
        public override bool InstancePerEntity => true;

        public override void OnKill(Projectile projectile, int timeLeft)
        {
            if (projectile.owner != Main.myPlayer)
                return;

            Player player = Main.player[projectile.owner];
            if (!player.active || player.dead)
                return;

            var ap = player.GetModPlayer<AugmentPlayer>();
            if (!ap.HasAugment("floor_is_lava"))
                return;

            if (projectile.type == ModContent.ProjectileType<NapalmPlasmaPool>())
                return;

            if (!PartyPoppersAugment.IsExplosiveRanged(projectile))
                return;

            int baseDmg = AugmentScaling.Scale(FloorIsLavaAugment.BaseDamage);
            int finalDmg = Math.Max(1, (int)player.GetTotalDamage(DamageClass.Ranged).ApplyTo(baseDmg));
            int poolType = ModContent.ProjectileType<NapalmPlasmaPool>();

            IEntitySource source = projectile.GetSource_FromThis();
            Projectile.NewProjectile(source, projectile.Center, Vector2.Zero, poolType, finalDmg, 0f, projectile.owner);
        }
    }
}
