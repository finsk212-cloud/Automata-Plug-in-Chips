using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Augments.Core;

namespace Augments
{
    public class ArmorPiercerAugment : Augment
    {
        public override string Id => "armor_piercer";
        public override string DisplayName => "Armor Piercer";
        public override string Description =>
            $"Attacks gain {AugmentText.Defense("+18 armor penetration")}.";

        public override string GetDetailedDescription(Player player) =>
            $"Attacks gain {AugmentText.Defense("+18 armor penetration")}.\n" +
            $"[c/94A3B8:(Ignores up to 18 enemy defense across all damage types. Increases damage by +9 / +13.5 / +18 on Normal / Expert / Master mode)]";

        public override AugmentRarity Rarity => AugmentRarity.Epic;
        public override AugmentClass Class => AugmentClass.Universal;

        private const int ArmorPenetrationAmount = 18;

        public override void UpdateEquips(Player player)
        {
            player.GetArmorPenetration(DamageClass.Generic) += ArmorPenetrationAmount;
        }

        public override void OnHitNPCWithProj(Player player, Projectile proj, NPC target, NPC.HitInfo hit, AugmentHitSource source, float effectiveness)
        {
            if (source == AugmentHitSource.AugmentProc)
                return;

            RecordPenetration(player, target, hit);
        }

        public override void OnHitNPCWithItem(Player player, Item item, NPC target, NPC.HitInfo hit, AugmentHitSource source, float effectiveness)
        {
            if (source == AugmentHitSource.AugmentProc)
                return;

            RecordPenetration(player, target, hit);
        }

        private static void RecordPenetration(Player player, NPC target, NPC.HitInfo hit)
        {
            if (player.whoAmI != Main.myPlayer)
                return;

            float defFactor = Main.masterMode ? 1f : (Main.expertMode ? 0.75f : 0.5f);
            int effectiveDef = target.defense > 0 ? target.defense : ArmorPenetrationAmount;
            int penValue = (int)System.Math.Max(1, System.Math.Round(System.Math.Min(ArmorPenetrationAmount, effectiveDef) * defFactor));

            AugmentDamageTracker.RecordChipHit("armor_piercer", penValue, hit.Crit);

            if (Main.rand.NextBool(4))
            {
                Dust d = Dust.NewDustDirect(target.position, target.width, target.height, DustID.SparksMech, 0f, 0f, 100, default, 1.1f);
                d.noGravity = true;
            }
        }
    }
}
