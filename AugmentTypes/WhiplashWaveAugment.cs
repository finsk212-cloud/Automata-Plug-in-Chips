using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using Augments.Core;

namespace Augments
{
    public class WhiplashWaveAugment : Augment
    {
        public override string Id => "whiplash_wave";
        public override string DisplayName => "Whiplash Wave";
        public override string Description =>
            $"When your whip cracks, a sonic wave flies out from its tip for {AugmentText.BonusDamage("40% of the whip's damage")}. It pierces 3 enemies and triggers {AugmentText.OnHit("on-hit")} effects at 50% effectiveness. Wave size and distance scale with your whip's range.";

        public override string GetDetailedDescription(Player player) =>
            Description + "\n" +
            AugmentText.Note("(Stops at walls. Applies no Whip Cracker stacks, Focus tags or Nanites.)");

        public override AugmentRarity Rarity => AugmentRarity.Epic;
        public override AugmentClass Class => AugmentClass.Summon;

        private const float DamageMultiplier = 0.4f;
        private const float WaveSpeed = 16f;
        private const float ReferenceReach = 200f;
        private const float MinScale = 0.8f;
        private const float MaxScale = 3f;

        internal static float ScaleForReach(float reach) => MathHelper.Clamp(reach / ReferenceReach, MinScale, MaxScale);

        public override void OnShootProjectile(Player player, Item item, Projectile projectile)
        {
            if (projectile.owner != Main.myPlayer || projectile.DamageType != DamageClass.SummonMeleeSpeed)
                return;

            Vector2 direction = projectile.velocity;
            if (direction.LengthSquared() < 0.01f)
                direction = new Vector2(player.direction, 0f);
            direction.Normalize();

            // The whip cracks (and plays its sound) halfway through the swing, when it is fully extended.
            WhiplashWaveWhip whip = projectile.GetGlobalProjectile<WhiplashWaveWhip>();
            whip.Armed = true;
            whip.Direction = direction;
            whip.CrackTick = Math.Max(1, (int)(player.itemAnimationMax * (projectile.extraUpdates + 1) / 2f));
        }

        internal static void SpawnWave(Projectile whip, Vector2 position, Vector2 direction, float scale)
        {
            Player player = Main.player[whip.owner];
            int damage = Math.Max(1, (int)(whip.damage * DamageMultiplier));

            int index = Projectile.NewProjectile(
                player.GetSource_FromThis(),
                position,
                direction * WaveSpeed,
                ModContent.ProjectileType<WhiplashWaveProjectile>(),
                damage,
                whip.knockBack * 0.5f,
                player.whoAmI,
                scale
            );

            if (index < 0 || index >= Main.maxProjectiles)
                return;

            Projectile wave = Main.projectile[index];
            wave.originalDamage = damage;
            wave.CritChance = whip.CritChance;
            wave.ArmorPenetration = whip.ArmorPenetration;
        }
    }

    // Counts a whip's updates and fires the wave from its tip at the moment of the crack.
    public class WhiplashWaveWhip : GlobalProjectile
    {
        public override bool InstancePerEntity => true;

        public bool Armed;
        public Vector2 Direction;
        public int CrackTick;
        private int ticks;

        public override void PostAI(Projectile projectile)
        {
            if (!Armed)
                return;

            ticks++;
            if (ticks < CrackTick)
                return;

            Armed = false;

            List<Vector2> points = new List<Vector2>();
            Projectile.FillWhipControlPoints(projectile, points);
            Vector2 tip = points.Count > 0 ? points[points.Count - 1] : projectile.Center;

            Player owner = Main.player[projectile.owner];
            Vector2 center = owner.MountedCenter;

            // The wave leaves at the whip's reach but always flies toward the cursor.
            float reach = Vector2.Distance(center, tip);
            if (reach < 16f)
                reach = 16f;

            Vector2 direction = Main.MouseWorld - center;
            if (direction.LengthSquared() < 1f)
                direction = Direction;
            direction.Normalize();

            // Lifted slightly so a whip cracked along the ground does not spawn the wave inside the terrain.
            WhiplashWaveAugment.SpawnWave(projectile, center + direction * reach + new Vector2(0f, -14f), direction, WhiplashWaveAugment.ScaleForReach(reach));
        }
    }
}
