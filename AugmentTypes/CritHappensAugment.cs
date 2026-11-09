using System;
using Terraria;
using Terraria.ModLoader;
using Augments.Core;

namespace Augments
{
    public class CritHappensAugment : Augment
    {
        public override string Id => "crit_happens";
        public override string DisplayName => "Crit Happens";
        public override string Description => $"Minions can now {AugmentText.Crit("Critically Strike")}!";

        public override AugmentRarity Rarity => AugmentRarity.Epic;
        public override AugmentClass Class => AugmentClass.Summon;

        public const float BaseCritBonus = 8f;

        public override void UpdateEquips(Player player)
        {
            // Provides a solid summon crit baseline so summoner sets without crit bonuses still crit reliably
            player.GetCritChance(DamageClass.Summon) += BaseCritBonus;
        }

        public override void ModifyHitNPCWithProj(Player player, Projectile proj, NPC target, ref NPC.HitModifiers modifiers, AugmentHitSource source, float effectiveness)
        {
            bool isSummon = proj.minion || proj.sentry || proj.CountsAsClass(DamageClass.Summon) || proj.DamageType == DamageClass.Summon;
            if (!isSummon)
                return;

            float critChance = Math.Max(player.GetTotalCritChance(DamageClass.Summon), player.GetTotalCritChance(DamageClass.Generic));

            if (Main.rand.NextFloat(100f) < critChance)
            {
                modifiers.SetCrit();
            }
        }
    }
}
