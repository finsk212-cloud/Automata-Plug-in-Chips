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
    public class PrismaticFractureAugment : Augment
    {
        public override string Id => "prismatic_fracture";
        public override string DisplayName => "Prismatic Fracture";
        public override string Description =>
            $"Magic hits shatter into {AugmentText.SpecialDamage("3 Homing Prismatic Shards")} dealing {AugmentText.BonusDamage("30% damage")}.\n" +
            $"Magic {AugmentText.Crit("Critical Strikes")} release {AugmentText.Trigger("5 shards")} and inflict {AugmentText.Immobilize("Prismatic Daze")} for {AugmentText.Duration("3s")}.";

        public override string GetDetailedDescription(Player player) =>
            Description + "\n" +
            AugmentText.Note("(Every spell hit fractures into seeking crystal shards. Critical hits unleash a 5-shard rainbow burst and inflict Prismatic Daze, slowing target movement by 25%.)");

        public override AugmentRarity Rarity => AugmentRarity.Epic;
        public override AugmentClass Class => AugmentClass.Magic;

        private const int CadenceTicks = 6;
        private static readonly Dictionary<int, int> procCooldown = new();

        public override void OnUpdate(Player player)
        {
            int who = player.whoAmI;
            if (procCooldown.TryGetValue(who, out int cd) && cd > 0)
                procCooldown[who] = cd - 1;
        }

        public override void OnHitNPCWithProj(
            Player player,
            Projectile proj,
            NPC target,
            NPC.HitInfo hit,
            AugmentHitSource source,
            float effectiveness)
        {
            if (player.whoAmI != Main.myPlayer)
                return;

            if (source != AugmentHitSource.NormalAttack)
                return;

            var tag = proj.GetGlobalProjectile<AugmentProjectileTag>();
            if (tag.IsAugmentProcDamage || proj.minion || proj.sentry)
                return;

            bool isMagic = proj.CountsAsClass(DamageClass.Magic) || proj.DamageType == DamageClass.Magic;
            if (!isMagic)
                return;

            int who = player.whoAmI;
            if (procCooldown.TryGetValue(who, out int cd) && cd > 0)
                return;

            procCooldown[who] = CadenceTicks;

            int count = hit.Crit ? 5 : 3;
            int shardDmg = Math.Max(6, (int)(hit.SourceDamage * 0.30f));

            float baseAngle = Main.rand.NextFloat(MathHelper.TwoPi);
            for (int i = 0; i < count; i++)
            {
                float angle = baseAngle + (MathHelper.TwoPi / count * i) + Main.rand.NextFloat(-0.2f, 0.2f);
                Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(7f, 10f);

                int pIndex = Projectile.NewProjectile(
                    player.GetSource_FromThis(),
                    target.Center,
                    vel,
                    ModContent.ProjectileType<PrismaticShardProjectile>(),
                    shardDmg,
                    2.5f,
                    player.whoAmI,
                    (float)i / count
                );

                if (pIndex >= 0 && pIndex < Main.maxProjectiles)
                {
                    Main.projectile[pIndex].CritChance = proj.CritChance;
                }
            }

            if (hit.Crit)
            {
                // Inflict Prismatic Daze (genuine 25% movement speed reduction for 3s)
                target.GetGlobalNPC<AugmentSlowNPC>().ApplySlow(180, 0.25f);
                if (Main.netMode == NetmodeID.MultiplayerClient)
                    AugmentNet.SendApplyNPCEffectSlow(target.whoAmI, 180, 0.25f);

                SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.55f, Pitch = 0.35f }, target.Center);

                for (int i = 0; i < 16; i++)
                {
                    int dType = Main.rand.NextBool() ? DustID.RainbowMk2 : DustID.Enchanted_Pink;
                    Vector2 vel = Main.rand.NextVector2Circular(4f, 4f);
                    Dust d = Dust.NewDustPerfect(target.Center, dType, vel, 80, default, 1.3f);
                    d.noGravity = true;
                }
            }
            else
            {
                SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.35f, Pitch = 0.1f }, target.Center);
            }
        }
    }
}
