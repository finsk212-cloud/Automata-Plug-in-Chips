using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace Augments
{
    public class ShockwaveAugment : Augment
    {
        public override string Id => "shockwave";
        public override string DisplayName => "Shockwave";
        public override string Description =>
            $"Melee {AugmentText.Crit("crits")} release a kinetic wave that violently pushes nearby enemies away.";

        public override string GetDetailedDescription(Player player) =>
            Description + "\n" +
            AugmentText.Note("(Knockback only; deals no extra damage.)");

        public override AugmentRarity Rarity => AugmentRarity.Rare;
        public override AugmentClass Class => AugmentClass.Melee;

        private const float ShockwaveRange = 150f;
        private const float KnockbackStrength = 6f;

        public override void OnHitNPCWithItem(Player player, Item item, NPC target, NPC.HitInfo hit)
        {
            if (hit.Crit && item.CountsAsClass(DamageClass.Melee))
                PushNearbyTargets(player, target);
        }

        public override void OnHitNPCWithProj(Player player, Projectile proj, NPC target, NPC.HitInfo hit)
        {
            if (hit.Crit && proj.CountsAsClass(DamageClass.Melee))
                PushNearbyTargets(player, target);
        }

        private static void PushNearbyTargets(Player player, NPC target)
        {
            foreach (NPC npc in Main.npc)
            {
                if (!npc.active || npc.friendly || npc.townNPC)
                    continue;

                if (npc.Distance(target.Center) > ShockwaveRange)
                    continue;

                Vector2 direction = npc.Center - player.Center;
                if (direction == Vector2.Zero)
                    continue;

                direction.Normalize();
                // SimpleStrikeNPC handles netUpdate internally and is multiplayer-safe.
                // noHitEffect=true suppresses the 0-damage combat text.
                npc.SimpleStrikeNPC(0, direction.X >= 0f ? 1 : -1, false, KnockbackStrength, DamageClass.Melee, true);
            }
        }
    }
}
