using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Augments.Core;

namespace Augments
{
    public class SugarRushAugment : Augment
    {
        public override string Id => "sugar_rush";
        public override string DisplayName => "Sugar Rush";
        public override string Description =>
            $"Drinking Mana Potions no longer inflicts the damage penalty of Mana Sickness. " +
            $"Instead, excess mana grants {AugmentText.Duration("Sugar Rush")}, giving decaying {AugmentText.BonusDamage("+15% magic damage")} and {AugmentText.Crit("+10% magic crit")} over {AugmentText.Duration("5s")}.";

        public override string GetDetailedDescription(Player player) =>
            Description + "\n" +
            AugmentText.Note("(Completely negates the standard -25% to -50% magic damage reduction from Mana Sickness. Bonus decays smoothly over 5 seconds.)");

        public override AugmentRarity Rarity => AugmentRarity.Rare;
        public override AugmentClass Class => AugmentClass.Magic;

        private static readonly Dictionary<int, int> timers = new();
        private const int MaxDurationTicks = 300; // 5 seconds

        public override void OnUpdate(Player player)
        {
            if (player.dead || !player.active)
                return;

            // Completely negate the Mana Sickness damage penalty
            player.manaSickReduction = 0f;

            int who = player.whoAmI;
            bool hasManaSick = player.HasBuff(BuffID.ManaSickness);

            timers.TryGetValue(who, out int timer);

            // Trigger or refresh on mana sickness activation
            if (hasManaSick && timer <= 0)
            {
                timer = MaxDurationTicks;
                timers[who] = timer;
                if (who == Main.myPlayer)
                {
                    SoundEngine.PlaySound(SoundID.Item29 with { Pitch = 0.3f, Volume = 0.7f });
                }
            }

            if (timer > 0)
            {
                timer--;
                timers[who] = timer;

                float ratio = (float)timer / MaxDurationTicks;
                player.GetDamage(DamageClass.Magic) += 0.15f * ratio;
                player.GetCritChance(DamageClass.Magic) += 10f * ratio;

                // Subtle sugar rush sparkle aura
                if (Main.rand.NextBool(4))
                {
                    Dust d = Dust.NewDustDirect(
                        player.position,
                        player.width,
                        player.height,
                        DustID.Enchanted_Pink,
                        Main.rand.NextFloat(-1.5f, 1.5f),
                        Main.rand.NextFloat(-2f, -0.5f),
                        100,
                        new Color(244, 114, 182),
                        0.9f
                    );
                    d.noGravity = true;
                }
            }
        }
    }
}
