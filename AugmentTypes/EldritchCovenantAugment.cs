using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace Augments
{
    public class EldritchCovenantAugment : Augment
    {
        public override string Id => "eldritch_covenant";
        public override string DisplayName => "Eldritch Covenant";
        public override string Description =>
            $"Spending {AugmentText.Mana("mana")} builds corruption. At full corruption, your next magic hit opens an eldritch eye for {AugmentText.Duration("4s")}.";

        public override AugmentRarity Rarity => AugmentRarity.Legendary;
        public override AugmentClass Class => AugmentClass.Magic;
        public override bool IsCharging => LocalPlayerState.EldritchCovenantCorruption > 0 || LocalPlayerState.EldritchCovenantManaProgress > 0;
        public override int ChargeIndicatorPercent => LocalPlayerState.EldritchCovenantCorruption * 10 + LocalPlayerState.EldritchCovenantManaProgress;
        public override Texture2D Icon => TextureAssets.Projectile[ProjectileID.PhantasmalEye].Value;

        private const int ManaPerCorruption = 10;
        private const int MaxCorruption = 10;
        private const int LaserDamage = 40;

        public override (string label, int baseValue)[] GetScalingEntries()
        {
            return new[] { ("Laser damage", LaserDamage) };
        }


        public override void OnConsumeMana(Player player, Item item, int manaConsumed)
        {
            var ap = player.GetModPlayer<AugmentPlayer>();
            if (ap.EldritchCovenantCorruption >= MaxCorruption || manaConsumed <= 0)
                return;

            ap.EldritchCovenantManaProgress += manaConsumed;
            int corruptionGained = ap.EldritchCovenantManaProgress / ManaPerCorruption;
            ap.EldritchCovenantManaProgress %= ManaPerCorruption;
            ap.EldritchCovenantCorruption = Math.Min(MaxCorruption, ap.EldritchCovenantCorruption + corruptionGained);

            if (ap.EldritchCovenantCorruption >= MaxCorruption)
                ap.EldritchCovenantManaProgress = 0;
        }

        public override void OnHitNPCWithItem(Player player, Item item, NPC target, NPC.HitInfo hit)
        {
            if (item.DamageType == DamageClass.Magic)
                TryOpenEye(player, target);
        }

        public override void OnHitNPCWithProj(Player player, Projectile proj, NPC target, NPC.HitInfo hit)
        {
            if (proj.DamageType == DamageClass.Magic)
                TryOpenEye(player, target);
        }

        private void TryOpenEye(Player player, NPC target)
        {
            var ap = player.GetModPlayer<AugmentPlayer>();
            if (ap.EldritchCovenantCorruption < MaxCorruption)
                return;

            ap.EldritchCovenantCorruption = 0;
            Projectile.NewProjectile(
                player.GetSource_FromThis(),
                target.Center + new Vector2(0f, -80f),
                Vector2.Zero,
                ModContent.ProjectileType<EldritchEyeProjectile>(),
                AugmentScaling.Scale(LaserDamage),
                0f,
                player.whoAmI
            );
        }
    }
}
