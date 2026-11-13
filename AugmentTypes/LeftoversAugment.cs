using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Augments.Core;

namespace Augments
{
    // When a melee hit kills an enemy, the damage that was more than needed hits the nearest other enemy.
    //
    // Everything runs on the attacker's own game (hit hooks only run there): the spill is applied the same way Arcane
    // Spark applies its extra damage, and the little "pop" at the receiving enemy is relayed so other players see it.
    public class LeftoversAugment : Augment
    {
        // Tuning
        private const int MinOverkill = 5;                 // a smaller leftover isn't worth a plate
        private const float MaxRangePixels = 30f * 16f;    // how far away the next enemy can be

        public override string Id => "leftovers";
        public override string DisplayName => "Leftovers";
        public override string Description =>
            $"When a melee hit kills an enemy, the {AugmentText.BonusDamage("damage left over")} hits the nearest other enemy.";

        public override string GetDetailedDescription(Player player) =>
            Description + "\n" +
            AugmentText.Note("(Only the damage beyond what was needed to kill spills over, once per kill, and the spill never chains. " +
            "It needs at least 5 leftover damage and another enemy within 30 tiles. Critters, town NPCs and statue spawns don't count, and " +
            "other parts of the same worm or boss are not \"other enemies\". The spill is a flat hit, so it doesn't trigger other augments. " +
            "Nothing goes to waste.)");

        public override AugmentRarity Rarity => AugmentRarity.Epic;
        public override AugmentClass Class => AugmentClass.Melee;
        public override Texture2D Icon => TextureAssets.Item[ItemID.Burger].Value;

        // The head of a worm or multi-part boss holds the real life total; its segments just point at it.
        private static NPC HeadOf(NPC npc) =>
            npc.realLife >= 0 && npc.realLife < Main.maxNPCs ? Main.npc[npc.realLife] : npc;

        // Each enemy's life just before the current hit lands. The hit hooks after the fact only tell us the damage dealt,
        // and the enemy's life may already be clamped by then, so this is how the overkill is worked out.
        private static readonly int[] lifeBeforeHit = new int[Main.maxNPCs];
        private static readonly PerHitGuard guard = new();
        private static uint lastEffectTick;

        public override void ModifyHitNPCWithItem(Player player, Item item, NPC target, ref NPC.HitModifiers modifiers)
        {
            lifeBeforeHit[target.whoAmI] = HeadOf(target).life;
        }

        public override void ModifyHitNPCWithProj(Player player, Projectile proj, NPC target, ref NPC.HitModifiers modifiers)
        {
            lifeBeforeHit[target.whoAmI] = HeadOf(target).life;
        }

        public override void OnHitNPCWithItem(Player player, Item item, NPC target, NPC.HitInfo hit, AugmentHitSource source, float effectiveness)
        {
            if (source == AugmentHitSource.AugmentProc || !item.CountsAsClass(DamageClass.Melee))
                return;

            Spill(player, target, hit);
        }

        public override void OnHitNPCWithProj(Player player, Projectile proj, NPC target, NPC.HitInfo hit, AugmentHitSource source, float effectiveness)
        {
            if (source == AugmentHitSource.AugmentProc || !proj.CountsAsClass(DamageClass.Melee))
                return;

            Spill(player, target, hit);
        }

        private void Spill(Player player, NPC target, NPC.HitInfo hit)
        {
            if (player.whoAmI != Main.myPlayer || hit.InstantKill)
                return;

            if (target.friendly || target.townNPC || target.CountsAsACritter || target.catchItem > 0 || target.SpawnedFromStatue)
                return;

            // Only kills count: the whole enemy (its head, for worms) is out of life.
            NPC head = HeadOf(target);
            if (head.active && head.life > 0)
                return;

            // The same hit can reach this hook twice (Twin Strike on a crit): spill only once per hit.
            if (!guard.IsFirst(target.whoAmI))
                return;

            int overkill = hit.Damage - lifeBeforeHit[target.whoAmI];
            if (overkill < MinOverkill)
                return;

            NPC next = FindNextEnemy(target, head);
            if (next == null)
                return;

            var spill = new NPC.HitInfo
            {
                Damage = overkill,
                SourceDamage = overkill,
                HitDirection = next.Center.X >= target.Center.X ? 1 : -1,
                DamageType = DamageClass.Melee,
                Knockback = 0f
            };

            // Credit a kill by the spill to the player, so loot and kill hooks treat it as theirs.
            next.lastInteraction = player.whoAmI;
            next.StrikeNPC(spill);
            if (Main.netMode != NetmodeID.SinglePlayer)
                NetMessage.SendStrikeNPC(next, in spill);

            AugmentDamageTracker.RecordChipHit(Id, overkill, false);

            // The "pop" at the receiving enemy, shown here and relayed to others. A piercing swing in a crowd can spill many
            // times in a moment, so the effect (not the damage) is limited to one every few ticks.
            if (Main.GameUpdateCount - lastEffectTick >= 6)
            {
                lastEffectTick = Main.GameUpdateCount;
                PluginEffects.Show(PluginEffectId.LeftoversPop, player, next.whoAmI);
            }
        }

        private static NPC FindNextEnemy(NPC target, NPC head)
        {
            NPC best = null;
            float bestDistance = MaxRangePixels * MaxRangePixels;

            foreach (NPC candidate in Main.npc)
            {
                if (!candidate.CanBeChasedBy() || candidate.whoAmI == target.whoAmI || candidate.whoAmI == head.whoAmI)
                    continue;

                // Another piece of the same worm or boss is the same enemy.
                if (candidate.realLife >= 0 && candidate.realLife == head.whoAmI)
                    continue;

                float distance = Vector2.DistanceSquared(target.Center, candidate.Center);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = candidate;
                }
            }

            return best;
        }
    }
}
