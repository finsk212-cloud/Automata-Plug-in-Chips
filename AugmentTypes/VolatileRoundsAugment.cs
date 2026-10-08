using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Augments.Core;

namespace Augments
{
    public class VolatileRoundsAugment : Augment
    {
        public override string Id => "volatile_rounds";
        public override string DisplayName => "Volatile Rounds";
        public override string Description =>
            $"Ranged hits against an enemy with any {AugmentText.Bleed("DoT")} effect active ({AugmentText.Bleed("Bleeding")}, {AugmentText.Frostburn("Frostburn")}, " +
            $"Poisoned, etc.) trigger an explosion dealing {AugmentText.BonusDamage("15% damage")} to nearby enemies.";

        public override AugmentRarity Rarity => AugmentRarity.Rare;
        public override AugmentClass Class => AugmentClass.Ranged;

        private const float ExplosionRange = 150f;
        private const float ExplosionDamagePercent = 0.15f;

        // Verified vanilla damage-dealing DoT debuffs a player can actually
        // inflict on an enemy - explicitly excludes Ichor (defense only, no
        // damage) and Confused (movement only, no damage), and also excludes
        // BuffID.Burning, which despite the name only ever applies to the
        // PLAYER from touching hot tiles/lava, never something inflicted on
        // an NPC by a weapon. BuffID.Bleeding is the genuine vanilla bleed
        // (e.g. from javelins) - separate from this mod's own custom
        // Bloodletter bleed, which is checked directly below since it
        // doesn't use a BuffID at all.
        private static readonly int[] DamagingDotBuffs =
        {
            BuffID.Poisoned,
            BuffID.Venom,
            BuffID.Bleeding,
            BuffID.OnFire,
            BuffID.OnFire3,
            BuffID.Frostburn,
            BuffID.Frostburn2,
            BuffID.CursedInferno,
            BuffID.ShadowFlame,
            BuffID.Daybreak,
        };

        public override void OnHitNPCWithItem(Player player, Item item, NPC target, NPC.HitInfo hit)
        {
            if (item.DamageType == DamageClass.Ranged && HasDamagingDot(target))
                Explode(player, target, hit.Damage, HitEffectiveness);
        }

        public override void OnHitNPCWithProj(Player player, Projectile proj, NPC target, NPC.HitInfo hit)
        {
            if (proj.DamageType == DamageClass.Ranged && HasDamagingDot(target))
                Explode(player, target, hit.Damage, HitEffectiveness);
        }

        private static bool HasDamagingDot(NPC target)
        {
            foreach (int buffType in DamagingDotBuffs)
            {
                if (target.HasBuff(buffType))
                    return true;
            }

            // This mod's own Bloodletter bleed is tracked via a dedicated
            // GlobalNPC, not a vanilla BuffID - target.HasBuff() above can't
            // see it, so it has to be asked about directly.
            return target.GetGlobalNPC<AugmentBleedNPC>().IsActive;
        }

        // Same uncapped nearby-enemy search shape as ChainLightningAugment/
        // TimeWarpAugment, dealing a flat fraction of the triggering hit's
        // damage to everything in range (including the original target).
        private static void Explode(Player player, NPC origin, int hitDamage, float effectiveness)
        {
            int damage = System.Math.Max(1, (int)(hitDamage * ExplosionDamagePercent * effectiveness));

            foreach (NPC npc in Main.npc)
            {
                if (!npc.active || npc.friendly || npc.townNPC)
                    continue;

                if (npc.Distance(origin.Center) <= ExplosionRange)
                {
                    int direction = npc.Center.X >= origin.Center.X ? 1 : -1;
                    npc.SimpleStrikeNPC(damage, direction);
                    if (player.whoAmI == Main.myPlayer)
                        AugmentDamageTracker.RecordChipHit("volatile_rounds", damage, false);
                }
            }
        }
    }
}
