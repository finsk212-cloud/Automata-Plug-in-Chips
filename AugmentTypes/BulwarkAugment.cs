using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Augments.Core;

namespace Augments
{
    public class BulwarkAugment : Augment
    {
        public override string Id => "bulwark";
        public override string DisplayName => "Bulwark";
        public override string Description =>
            $"Going {AugmentText.Duration("10 seconds")} without taking damage charges a shield that completely " +
            "blocks your next hit, then resets and must recharge.";

        public override AugmentRarity Rarity => AugmentRarity.Rare;
        public override AugmentClass Class => AugmentClass.Universal;
        public override string FamilyId => AugmentFamilyRegistry.BastionId;

        public const int NormalChargeTicksRequired = 600; // 10s
        public const int AcceleratedChargeTicksRequired = 450; // 7.5s (25% reduction with Bastion Protocol)

        public static int GetChargeTicksRequired(Player player)
        {
            var ap = player.GetModPlayer<AugmentPlayer>();
            if (AugmentFamilyRegistry.GetOwnedCount(ap, AugmentFamilyRegistry.BastionId) >= 2 && ap.AdaptiveArmorDefenseBonus >= 10)
                return AcceleratedChargeTicksRequired;

            return NormalChargeTicksRequired;
        }

        public override void OnUpdate(Player player)
        {
            var ap = player.GetModPlayer<AugmentPlayer>();
            int required = GetChargeTicksRequired(player);
            if (ap.BulwarkNoDamageTicks < required)
                ap.BulwarkNoDamageTicks++;
            else if (ap.BulwarkNoDamageTicks > required)
                ap.BulwarkNoDamageTicks = required;
        }

        // Unlike MirrorImageAugment, no manual invincibility-window
        // re-assertion is needed here - Bulwark's recharge already
        // guarantees a long natural gap before it can trigger again, so
        // there's no risk of it firing twice in quick succession.
        public override bool FreeDodge(Player player, Player.HurtInfo info)
        {
            var ap = player.GetModPlayer<AugmentPlayer>();
            int required = GetChargeTicksRequired(player);
            if (ap.BulwarkNoDamageTicks < required)
                return false;

            ap.BulwarkNoDamageTicks = 0;

            // Bastion Protocol: Concussive Deflection & Stack Preservation
            if (AugmentFamilyRegistry.GetOwnedCount(ap, AugmentFamilyRegistry.BastionId) >= 2)
            {
                TriggerConcussiveDeflection(player, ap);
            }

            return true;
        }

        public override void OnHurt(Player player, Player.HurtInfo info)
        {
            player.GetModPlayer<AugmentPlayer>().BulwarkNoDamageTicks = 0;
        }

        private static void TriggerConcussiveDeflection(Player player, AugmentPlayer ap)
        {
            int bonusDamage = ap.AdaptiveArmorDefenseBonus * 5;
            int shockwaveDamage = 50 + bonusDamage;
            float blastRadius = 160f;

            // Sound effects
            SoundEngine.PlaySound(SoundID.Item93, player.Center); // Heavy electric / shield pulse
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.55f }, player.Center); // Concussive impact thud

            // Visual burst: Bastion Cyan and Titanium particles
            for (int i = 0; i < 32; i++)
            {
                float angle = MathHelper.TwoPi * (i / 32f);
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(4.5f, 8.5f);
                Dust d = Dust.NewDustPerfect(player.Center, DustID.Electric, velocity, 0, default, 1.15f);
                d.noGravity = true;

                Dust d2 = Dust.NewDustPerfect(player.Center, DustID.Titanium, velocity * 0.65f, 0, default, 1.35f);
                d2.noGravity = true;
            }

            if (player.whoAmI == Main.myPlayer)
            {
                foreach (NPC npc in Main.npc)
                {
                    if (!npc.active || npc.friendly || npc.townNPC || npc.dontTakeDamage)
                        continue;

                    if (npc.Distance(player.Center) <= blastRadius)
                    {
                        Vector2 dir = npc.Center - player.Center;
                        if (dir == Vector2.Zero)
                            dir = new Vector2(player.direction, 0f);
                        dir.Normalize();

                        int hitDirection = dir.X >= 0f ? 1 : -1;
                        npc.SimpleStrikeNPC(shockwaveDamage, hitDirection, false, 8.5f, DamageClass.Generic, false);
                        if (player.whoAmI == Main.myPlayer)
                            AugmentDamageTracker.RecordChipHit("bulwark", shockwaveDamage, false);
                    }
                }
            }
        }
    }
}
