using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Augments.Core;

namespace Augments
{
    public class HexDetonationAugment : Augment
    {
        public override string Id => "hex_detonation";
        public override string DisplayName => "Hex Detonation";
        public override string Description =>
            $"Magic hits stack {AugmentText.Trigger("Hex")} on enemies, up to {AugmentText.Trigger("5 stacks")} for {AugmentText.Duration("6s")}. " +
            $"The next magic hit on a fully hexed enemy detonates it, dealing {AugmentText.SpecialDamage("200% of that hit's damage")} to nearby enemies.";

        public override string GetDetailedDescription(Player player) =>
            Description + "\n" +
            AugmentText.Note("(Each stack refreshes the timer, an enemy gains at most 1 stack per 0.2s, and it cannot be hexed again for 2s after detonating. The blast never applies Hex itself.)");

        public override AugmentRarity Rarity => AugmentRarity.Legendary;
        public override AugmentClass Class => AugmentClass.Magic;

        private const float DetonationMultiplier = 2f;
        private const float BlastRadius = 120f;
        private const float BlastKnockback = 3f;

        public override void OnHitNPCWithItem(Player player, Item item, NPC target, NPC.HitInfo hit)
        {
            if (item.CountsAsClass(DamageClass.Magic))
                RegisterMagicHit(player, target, hit);
        }

        public override void OnHitNPCWithProj(Player player, Projectile proj, NPC target, NPC.HitInfo hit)
        {
            if (proj.CountsAsClass(DamageClass.Magic))
                RegisterMagicHit(player, target, hit);
        }

        private static void RegisterMagicHit(Player player, NPC target, NPC.HitInfo hit)
        {
            if (player.whoAmI != Main.myPlayer || target.friendly || target.townNPC)
                return;

            var hex = target.GetGlobalNPC<HexDetonationNPC>();
            if (!hex.IsFullyStacked)
            {
                hex.TryAddStack();
                return;
            }

            hex.Detonated();
            Detonate(player, target, hit.Damage);
        }

        private static void Detonate(Player player, NPC target, int hitDamage)
        {
            int damage = System.Math.Max(1, (int)(hitDamage * DetonationMultiplier));
            Vector2 center = target.Center;

            SpawnEffects(center);

            foreach (NPC npc in Main.npc)
            {
                if (!npc.active || npc.friendly || npc.townNPC || npc.dontTakeDamage)
                    continue;

                if (npc.Distance(center) > BlastRadius)
                    continue;

                Vector2 direction = npc.Center - center;
                int hitDirection = direction.X == 0f ? player.direction : (direction.X > 0f ? 1 : -1);

                // SimpleStrikeNPC is multiplayer-safe and does not re-enter the hit hooks, so the blast cannot stack Hex.
                npc.SimpleStrikeNPC(damage, hitDirection, false, BlastKnockback, DamageClass.Magic, false);
                AugmentDamageTracker.RecordChipHit("hex_detonation", damage, false);
            }
        }

        private static void SpawnEffects(Vector2 center)
        {
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.7f, Pitch = 0.3f }, center);

            const int RingDust = 32;
            for (int i = 0; i < RingDust; i++)
            {
                Vector2 direction = (MathHelper.TwoPi * i / RingDust).ToRotationVector2();
                Dust ring = Dust.NewDustPerfect(center + direction * 8f, DustID.PurpleTorch, direction * 6.5f, 100, default, 1.5f);
                ring.noGravity = true;
            }

            for (int i = 0; i < 12; i++)
            {
                Dust spark = Dust.NewDustPerfect(center, DustID.GoldFlame, Main.rand.NextVector2Circular(5f, 5f), 100, default, 1.2f);
                spark.noGravity = true;
            }
        }
    }
}
