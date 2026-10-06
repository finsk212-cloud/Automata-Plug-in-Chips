using Terraria;
using Terraria.ModLoader;

namespace Augments
{
    public class PiercingShotAugment : Augment
    {
        public override string Id => "piercing_shot";
        public override string DisplayName => "Piercing Shot";
        public override string Description =>
            $"Ranged projectiles have a {FormatCurrentChance(ProcChance)} to pierce through the first enemy hit.";
        public override string GetDetailedDescription(Player player) =>
            $"Ranged projectiles have a {FormatFortuneChance(ProcChance, player)} to pierce through the first enemy hit.";

        public override AugmentRarity Rarity => AugmentRarity.Rare;
        public override AugmentClass Class => AugmentClass.Ranged;

        private const float ProcChance = 0.20f;

        public override void OnShootProjectile(Player player, Item item, Projectile projectile)
        {
            if (projectile.DamageType != DamageClass.Ranged) return;

            var tag = projectile.GetGlobalProjectile<AugmentProjectileTag>();
            if (tag.IsAugmentProcDamage) return;

            if (!RollFortuneChance(player, ProcChance)) return;

            // Only upgrade non-piercing projectiles (penetrate == 1).
            // Leave infinite-pierce (-1) and already-piercing (> 1) untouched.
            if (projectile.penetrate != 1) return;

            projectile.penetrate = 2;
            // Standard bullets/arrows use the shared NPC.immune[owner] system, so after
            // hitting the first enemy all NPCs are immune to that owner for a few frames —
            // the bullet passes through without registering the second hit.
            // usesLocalNPCImmunity switches to per-NPC tracking (same as chlorophyte bullets),
            // so each enemy independently decides whether it's immune to this projectile.
            // localNPCHitCooldown = -1 means each NPC can be hit at most once per projectile.
            projectile.usesLocalNPCImmunity = true;
            projectile.localNPCHitCooldown = -1;
        }
    }
}
