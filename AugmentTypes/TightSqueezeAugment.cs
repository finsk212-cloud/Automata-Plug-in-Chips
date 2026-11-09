using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Augments.Core;

namespace Augments
{
    public class TightSqueezeAugment : Augment
    {
        public override string Id => "tight_squeeze";
        public override string DisplayName => "Tight Squeeze";
        public override string Description =>
            $"Multi-projectile ranged weapons have their spread angle reduced by {AugmentText.Trigger("45%")}. " +
            $"Striking a single enemy with 3 or more projectiles from the same blast inflicts {AugmentText.Trigger("Concussive Fracture")}, making it take {AugmentText.BonusDamage("+12% bonus ranged damage")} for {AugmentText.Duration("4s")}.";

        public override string GetDetailedDescription(Player player) =>
            Description + "\n" +
            AugmentText.Note("(Affects all shotguns, spread bows, and multi-shot ranged ammunition. Bonus damage applies from all players and damage sources while fractured.)");

        public override AugmentRarity Rarity => AugmentRarity.Rare;
        public override AugmentClass Class => AugmentClass.Ranged;
    }

    public class TightSqueezeGlobalProjectile : GlobalProjectile
    {
        public override bool InstancePerEntity => true;

        public override void OnSpawn(Projectile projectile, IEntitySource source)
        {
            if (projectile.owner != Main.myPlayer)
                return;

            if (!projectile.friendly || projectile.hostile || projectile.minion || projectile.sentry)
                return;

            if (!projectile.CountsAsClass(DamageClass.Ranged))
                return;

            Player player = Main.player[projectile.owner];
            if (!player.active || player.dead)
                return;

            var ap = player.GetModPlayer<AugmentPlayer>();
            if (!ap.HasAugment("tight_squeeze"))
                return;

            if (source is EntitySource_ItemUse_WithAmmo || source is EntitySource_ItemUse)
            {
                float speed = projectile.velocity.Length();
                if (speed > 0.2f)
                {
                    float aimAngle = (Main.MouseWorld - projectile.position).ToRotation();
                    float currentAngle = projectile.velocity.ToRotation();
                    float diff = MathHelper.WrapAngle(currentAngle - aimAngle);

                    // Pull spread 45% tighter towards the aim angle
                    if (Math.Abs(diff) > 0.02f)
                    {
                        float newAngle = aimAngle + diff * 0.55f;
                        projectile.velocity = newAngle.ToRotationVector2() * speed;
                    }
                }
            }
        }
    }

    public class TightSqueezeGlobalNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        public int ConcussiveFractureTimer = 0;
        private readonly List<int> recentHits = new();

        public override void ResetEffects(NPC npc)
        {
            if (ConcussiveFractureTimer > 0)
                ConcussiveFractureTimer--;

            // Clean hits older than 12 ticks
            int currentTick = (int)Main.GameUpdateCount;
            recentHits.RemoveAll(t => currentTick - t > 12);
        }

        public override void OnHitByProjectile(NPC npc, Projectile projectile, NPC.HitInfo hit, int damageDone)
        {
            if (!projectile.CountsAsClass(DamageClass.Ranged))
                return;

            if (projectile.owner < 0 || projectile.owner >= Main.maxPlayers)
                return;

            Player player = Main.player[projectile.owner];
            if (!player.active)
                return;

            var ap = player.GetModPlayer<AugmentPlayer>();
            if (!ap.HasAugment("tight_squeeze"))
                return;

            recentHits.Add((int)Main.GameUpdateCount);

            if (recentHits.Count >= 3 && ConcussiveFractureTimer <= 30)
            {
                ConcussiveFractureTimer = 240; // 4 seconds
                SoundEngine.PlaySound(SoundID.Item10 with { Pitch = -0.3f, Volume = 0.6f }, npc.position);

                for (int i = 0; i < 14; i++)
                {
                    Dust d = Dust.NewDustDirect(npc.position, npc.width, npc.height, DustID.Smoke, 0f, 0f, 120, new Color(251, 146, 60), 1.2f);
                    d.velocity *= 1.5f;
                }
            }
        }

        public override void ModifyHitByProjectile(NPC npc, Projectile projectile, ref NPC.HitModifiers modifiers)
        {
            if (ConcussiveFractureTimer > 0 && projectile.CountsAsClass(DamageClass.Ranged))
            {
                modifiers.FinalDamage *= 1.12f;
            }
        }

        public override void DrawEffects(NPC npc, ref Color drawColor)
        {
            if (ConcussiveFractureTimer > 0 && Main.rand.NextBool(5))
            {
                Dust d = Dust.NewDustDirect(npc.position, npc.width, npc.height, DustID.AmberBolt, 0f, -1f, 100, default, 0.9f);
                d.noGravity = true;
            }
        }
    }
}
