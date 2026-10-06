using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Augments.Core;

namespace Augments
{
    public class AstralSiphonAugment : Augment
    {
        public override string Id => "astral_siphon";
        public override string DisplayName => "Astral Siphon";
        public override string Description =>
            $"Magic {AugmentText.Crit("crits")} restore {AugmentText.HP("5 HP")} and {AugmentText.Mana("15 mana")}.";

        public override AugmentRarity Rarity => AugmentRarity.Epic;
        public override AugmentClass Class => AugmentClass.Magic;

        public const int HealAmount = 5;
        public const int ManaAmount = 15;

        public override void OnHitNPCWithProj(Player player, Projectile proj, NPC target, NPC.HitInfo hit, AugmentHitSource source, float effectiveness)
        {
            if (source == AugmentHitSource.AugmentProc || !hit.Crit)
                return;

            if (!proj.CountsAsClass(DamageClass.Magic) && proj.DamageType != DamageClass.Magic)
                return;

            ApplySiphon(player, target);
        }

        public override void OnHitNPCWithItem(Player player, Item item, NPC target, NPC.HitInfo hit, AugmentHitSource source, float effectiveness)
        {
            if (source == AugmentHitSource.AugmentProc || !hit.Crit)
                return;

            if (!item.CountsAsClass(DamageClass.Magic) && item.DamageType != DamageClass.Magic)
                return;

            ApplySiphon(player, target);
        }

        private static void ApplySiphon(Player player, NPC target)
        {
            if (player.whoAmI != Main.myPlayer)
                return;

            if (player.statLife < player.statLifeMax2)
            {
                int actualHeal = System.Math.Min(HealAmount, player.statLifeMax2 - player.statLife);
                player.statLife += actualHeal;
                player.HealEffect(actualHeal, true);
            }

            if (player.statMana < player.statManaMax2)
            {
                int actualMana = System.Math.Min(ManaAmount, player.statManaMax2 - player.statMana);
                player.statMana += actualMana;
                player.ManaEffect(actualMana);
            }

            for (int i = 0; i < 3; i++)
            {
                Vector2 vel = Main.rand.NextVector2Circular(2f, 2f);
                Dust d = Dust.NewDustPerfect(target.Center, DustID.DungeonSpirit, vel, 100, default, 1.1f);
                d.noGravity = true;
            }
        }
    }
}
