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
    public class HitMeHereAugment : Augment
    {
        public override string Id => "hit_me_here";
        public override string DisplayName => "Hit Me Here";
        public override string Description =>
            $"Ranged hits scan the target, projecting a glowing cybernetic weakpoint reticle for {AugmentText.Duration("4s")}. " +
            $"Hitting the weakpoint guarantees a {AugmentText.Crit("Critical Strike")} that {AugmentText.BonusDamage("ignores 100% defense")}, " +
            $"ruptures the target, and grants {AugmentText.MovementSpeed("+15% movement speed")} for {AugmentText.Duration("2s")}.";

        public override string GetDetailedDescription(Player player) =>
            Description + "\n" +
            AugmentText.Note("(Scanning places a tactical reticle on the enemy. Precise hits inside the reticle rupture the weakpoint, triggering a defense-piercing crit and speed boost with a 1s cooldown.)");

        public override AugmentRarity Rarity => AugmentRarity.Epic;
        public override AugmentClass Class => AugmentClass.Ranged;
        public override string FamilyId => AugmentFamilyRegistry.MarksmanId;

        private static readonly Dictionary<int, int> speedTimers = new();
        private const int SpeedBoostDurationTicks = 120; // 2 seconds

        public override void OnUpdate(Player player)
        {
            if (player.dead || !player.active)
                return;

            if (speedTimers.TryGetValue(player.whoAmI, out int timer) && timer > 0)
            {
                speedTimers[player.whoAmI] = timer - 1;
                player.moveSpeed += 0.15f;

                if (Main.rand.NextBool(5))
                {
                    Dust d = Dust.NewDustDirect(
                        player.position,
                        player.width,
                        player.height,
                        DustID.Electric,
                        0f,
                        0f,
                        100,
                        new Color(56, 189, 248),
                        0.7f
                    );
                    d.noGravity = true;
                }
            }
        }

        public override void ModifyHitNPCWithProj(Player player, Projectile proj, NPC target, ref NPC.HitModifiers modifiers)
        {
            if (!IsRanged(proj.DamageType) || target.friendly || target.townNPC)
                return;

            var weakpoint = target.GetGlobalNPC<HitMeHereNPC>();
            if (weakpoint.IsMarked && IsWeakpointHit(target, weakpoint, proj.Center, proj.Hitbox))
            {
                modifiers.SetCrit();
                modifiers.ScalingArmorPenetration += 1f;
            }
        }

        public override void OnHitNPCWithProj(Player player, Projectile proj, NPC target, NPC.HitInfo hit)
        {
            if (!IsRanged(proj.DamageType) || target.friendly || target.townNPC)
                return;

            HandleRangedHit(player, target, proj.Center, proj.Hitbox);
        }

        public override void ModifyHitNPCWithItem(Player player, Item item, NPC target, ref NPC.HitModifiers modifiers)
        {
            if (!IsRanged(item.DamageType) || target.friendly || target.townNPC)
                return;

            var weakpoint = target.GetGlobalNPC<HitMeHereNPC>();
            if (weakpoint.IsMarked && IsWeakpointHit(target, weakpoint, player.Center, player.Hitbox))
            {
                modifiers.SetCrit();
                modifiers.ScalingArmorPenetration += 1f;
            }
        }

        public override void OnHitNPCWithItem(Player player, Item item, NPC target, NPC.HitInfo hit)
        {
            if (!IsRanged(item.DamageType) || target.friendly || target.townNPC)
                return;

            HandleRangedHit(player, target, player.Center, player.Hitbox);
        }

        private static bool IsRanged(DamageClass damageClass)
        {
            return damageClass == DamageClass.Ranged || damageClass.CountsAsClass(DamageClass.Ranged);
        }

        private static bool IsWeakpointHit(NPC target, HitMeHereNPC weakpoint, Vector2 hitPos, Rectangle hitBox)
        {
            if (target.width <= 36 || target.height <= 36)
                return true;

            Vector2 wpPos = target.Center + weakpoint.WeakpointOffset;
            float tolerance = Math.Max(28f, Math.Min(target.width, target.height) * 0.35f);
            return Vector2.Distance(hitPos, wpPos) <= tolerance ||
                   hitBox.Intersects(new Rectangle((int)wpPos.X - 16, (int)wpPos.Y - 16, 32, 32));
        }

        private static void HandleRangedHit(Player player, NPC target, Vector2 hitPos, Rectangle hitBox)
        {
            var weakpoint = target.GetGlobalNPC<HitMeHereNPC>();

            if (weakpoint.IsMarked)
            {
                if (IsWeakpointHit(target, weakpoint, hitPos, hitBox))
                {
                    Vector2 wpPos = target.Center + weakpoint.WeakpointOffset;
                    weakpoint.Rupture();
                    speedTimers[player.whoAmI] = SpeedBoostDurationTicks;

                    // Metallic rupture sound & cybernetic burst particles
                    SoundEngine.PlaySound(SoundID.NPCHit4 with { Volume = 0.85f, Pitch = -0.1f }, wpPos);

                    for (int i = 0; i < 16; i++)
                    {
                        Vector2 vel = (MathHelper.TwoPi * i / 16f).ToRotationVector2() * Main.rand.NextFloat(3f, 6.5f);
                        Dust d = Dust.NewDustPerfect(wpPos, DustID.Electric, vel, 100, new Color(56, 189, 248), 1.2f);
                        d.noGravity = true;
                    }
                    for (int i = 0; i < 8; i++)
                    {
                        Dust d = Dust.NewDustPerfect(wpPos, DustID.GemRuby, Main.rand.NextVector2Circular(4f, 4f), 100, new Color(244, 63, 94), 1.1f);
                        d.noGravity = true;
                    }
                }
            }
            else if (weakpoint.RuptureCooldown <= 0)
            {
                // Scan target and project reticle
                Vector2 offset = new Vector2(0f, -MathHelper.Clamp(target.height * 0.22f, 0f, 36f));
                weakpoint.ApplyScan(player.whoAmI, offset);

                if (player.whoAmI == Main.myPlayer)
                {
                    SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.5f, Pitch = 0.5f }, target.Center);
                }
            }
        }
    }
}
