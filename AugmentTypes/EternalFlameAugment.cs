using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Augments
{
    public class EternalFlameAugment : Augment
    {
        public override string Id => "eternal_flame";
        public override string DisplayName => "Eternal Flame";
        public override string Description =>
            $"Permanently immune to fire-based debuffs (On Fire!, Hellfire, Cursed Inferno, Shadowflame, Burning). " +
            $"Each time one would have been applied, gain {AugmentText.BonusDamage("+15% magic damage")} " +
            $"for {AugmentText.Duration("4 seconds")} instead.";

        public override AugmentRarity Rarity => AugmentRarity.Rare;
        public override AugmentClass Class => AugmentClass.Magic;

        private const float BonusDamagePercent = 0.15f;
        private const int BoostDurationTicks = 240;

        // tModLoader has no hook that fires when a buff is about to be
        // applied - only PreUpdateBuffs/PostUpdateBuffs each tick, after the
        // buff is already on the player. So instead of setting buffImmune
        // (which would silently block it with no way to detect the attempt),
        // we let it land, strip it the instant we see it, and start the
        // bonus window then. Once DelBuff removes it, the next tick's check
        // comes up empty until a genuinely new application lands - no
        // re-triggering every tick off the same hit.
        private static readonly int[] FireDebuffs =
        {
            BuffID.OnFire,
            BuffID.OnFire3,
            BuffID.CursedInferno,
            BuffID.ShadowFlame,
            BuffID.Burning
        };

        public override void OnUpdate(Player player)
        {
            var ap = player.GetModPlayer<AugmentPlayer>();
            if (ap.EternalFlameBoostTicks > 0)
                ap.EternalFlameBoostTicks--;

            for (int i = 0; i < player.buffType.Length; i++)
            {
                if (player.buffType[i] == 0 || System.Array.IndexOf(FireDebuffs, player.buffType[i]) < 0)
                    continue;

                player.DelBuff(i);
                if (Main.netMode == NetmodeID.MultiplayerClient)
                    NetMessage.SendData(MessageID.PlayerBuffs, number: player.whoAmI);
                ap.EternalFlameBoostTicks = BoostDurationTicks;
                break;
            }
        }

        public override void ModifyHitNPCWithItem(Player player, Item item, NPC target, ref NPC.HitModifiers modifiers)
        {
            if (player.GetModPlayer<AugmentPlayer>().EternalFlameBoostTicks > 0 && item.DamageType == DamageClass.Magic)
                modifiers.FlatBonusDamage += (int)(item.damage * BonusDamagePercent);
        }

        public override void ModifyHitNPCWithProj(Player player, Projectile proj, NPC target, ref NPC.HitModifiers modifiers)
        {
            if (player.GetModPlayer<AugmentPlayer>().EternalFlameBoostTicks > 0 && proj.DamageType == DamageClass.Magic)
                modifiers.FlatBonusDamage += (int)(proj.damage * BonusDamagePercent);
        }
    }
}
