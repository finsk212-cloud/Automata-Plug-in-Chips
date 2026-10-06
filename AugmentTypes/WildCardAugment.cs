using Terraria;
using Augments.Core;

namespace Augments
{
    public class WildCardAugment : Augment
    {
        public override string Id => "wild_card";
        public override string DisplayName => "Wild Card";
        public override string Description
        {
            get
            {
                var ap = Main.LocalPlayer?.GetModPlayer<AugmentPlayer>();
                float fortune = ap?.TotalFortune ?? 0f;
                string scalingNote = fortune > 0f
                    ? $" (All outcomes boosted by +{fortune * 100f:0.#}% Fortune)"
                    : "";

                return $"Grants {AugmentText.Crit("+5% Fortune")} (World Luck & lucky trigger chance). Crits randomly trigger ONE of four effects, 25% chance each: " +
                       $"{AugmentText.Healing("heal 5 HP")}; {AugmentText.MovementSpeed("+50% movement speed")} for " +
                       $"{AugmentText.Duration("1.5s")}; {AugmentText.Duration("~1 second")} of invincibility; or an " +
                       $"immediate bonus strike for {AugmentText.BonusDamage("+20% bonus damage")}.{scalingNote}";
            }
        }

        public override string GetDetailedDescription(Player player)
        {
            float fortune = player?.GetModPlayer<AugmentPlayer>()?.TotalFortune ?? 0f;
            int fortunePercent = (int)System.Math.Round(fortune * 100f);
            float mult = 1f + fortune;
            int heal = (int)System.Math.Round(HealAmount * mult);
            int bonusDamage = (int)System.Math.Round(20f * mult);

            return $"Grants {AugmentText.Crit("+5% Fortune")} (World Luck & lucky trigger chance). Crits randomly trigger ONE of four effects (25% chance each, scaled by {fortunePercent}% Fortune):\n" +
                   $"• {AugmentText.Healing($"heal {heal} HP")} [c/94A3B8:(5 × {mult:0.00})]\n" +
                   $"• {AugmentText.MovementSpeed("+50% movement speed")}\n" +
                   $"• {AugmentText.Duration("~1 second")} invincibility\n" +
                   $"• bonus strike for {AugmentText.BonusDamage($"+{bonusDamage}% damage")} [c/94A3B8:(20% × {mult:0.00})]";
        }

        public override AugmentRarity Rarity => AugmentRarity.Rare;
        public override AugmentClass Class => AugmentClass.Universal;
        public override string FamilyId => AugmentFamilyRegistry.FortuneId;

        public override bool IsLuckyThemed => true;
        public override float FortuneBonus => 0.05f;

        private const int HealAmount = 5;
        private const int SpeedDurationTicks = 90;
        private const float SpeedBonus = 0.5f;
        private const int InvulnerabilityTicks = 60;
        private const float BonusStrikeDamagePercent = 0.2f;

        // NPC.HitModifiers (the struct ModifyHitNPCWithItem/Proj receives)
        // has no public way to read whether this hit will be a crit - only
        // a private override plus write-only SetCrit()/DisableCrit() - the
        // actual crit boolean is rolled by the caller and only shows up
        // later as NPC.HitInfo.Crit once the hit is fully resolved. So all
        // four outcomes (including the bonus-damage one) have to be rolled
        // here in OnHit, where hit.Crit is reliable - rolling some outcomes
        // in OnHit and others in ModifyHit would be two separate, desynced
        // RNG calls pretending to be one choice. The bonus-damage outcome is
        // therefore an immediate follow-up strike (SimpleStrikeNPC, same as
        // LuckyStrikeAugment) rather than a FlatBonusDamage modification of
        // the original hit, since that hit's modifiers are already locked in
        // by the time OnHit fires.
        public override void OnHitNPCWithItem(Player player, Item item, NPC target, NPC.HitInfo hit)
        {
            if (hit.Crit)
                RollOutcome(player, target, hit);
        }

        public override void OnHitNPCWithProj(Player player, Projectile proj, NPC target, NPC.HitInfo hit)
        {
            if (hit.Crit)
                RollOutcome(player, target, hit);
        }

        private void RollOutcome(Player player, NPC target, NPC.HitInfo hit)
        {
            var ap = player.GetModPlayer<AugmentPlayer>();
            float fortuneMult = 1f + ap.TotalFortune;
            int roll = Main.rand.Next(4);

            switch (roll)
            {
                case 0:
                    int healing = ScaleHitEffect((int)System.MathF.Round(HealAmount * fortuneMult));
                    player.statLife = System.Math.Min(player.statLife + healing, player.statLifeMax2);
                    player.HealEffect(healing);
                    break;
                case 1:
                    ap.WildCardSpeedTicks = ScaleHitEffect((int)System.MathF.Round(SpeedDurationTicks * fortuneMult));
                    break;
                case 2:
                    ap.WildCardInvulnTicks = ScaleHitEffect((int)System.MathF.Round(InvulnerabilityTicks * fortuneMult));
                    break;
                case 3:
                    target.SimpleStrikeNPC(ScaleHitEffect((int)(hit.Damage * BonusStrikeDamagePercent * fortuneMult)), player.direction);
                    break;
            }
        }

        public override void OnUpdate(Player player)
        {
            var ap = player.GetModPlayer<AugmentPlayer>();
            if (ap.WildCardSpeedTicks > 0)
                ap.WildCardSpeedTicks--;

            // FreeDodge-style hits get their own automatic invuln window, but
            // a manually-started one like this isn't reliable unless it's
            // re-forced every tick - same lesson MirrorImageAugment already
            // learned.
            if (ap.WildCardInvulnTicks > 0)
            {
                player.immune = true;
                player.immuneTime = ap.WildCardInvulnTicks;
                ap.WildCardInvulnTicks--;
            }
        }

        // Movement speed must be applied here, not OnUpdate alone - this
        // project's established lesson (see FightOrFlightAugment) is that
        // maxRunSpeed/accRunSpeed/runAcceleration get recalculated from
        // scratch every frame and have to be multiplied in PostUpdateRunSpeeds
        // to actually stick.
        public override void PostUpdateRunSpeeds(Player player)
        {
            if (player.GetModPlayer<AugmentPlayer>().WildCardSpeedTicks <= 0)
                return;

            player.maxRunSpeed *= 1f + SpeedBonus;
            player.accRunSpeed *= 1f + SpeedBonus;
            player.runAcceleration *= 1f + SpeedBonus;
        }
    }
}
