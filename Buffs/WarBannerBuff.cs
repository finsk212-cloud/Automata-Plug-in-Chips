using Terraria;
using Terraria.ModLoader;

namespace Augments
{
    // The War Banner raised by War God's Tempo. Two buffs, so every player's game can tell them apart:
    //  - WarBannerBearerBuff sits on the player who raised the banner (full 10s timer). Vanilla syncs player
    //    buffs, so every other player's game sees it and knows whose banner is up and where.
    //  - WarBannerBuff is what allies standing inside the aura give THEMSELVES (10 ticks, refreshed every
    //    tick while in range), the same pull-based pattern as the Warcry aura, so each client only ever
    //    changes its own player.
    // Texture: placeholder - flag for art pass.
    public class WarBannerBearerBuff : ModBuff
    {
        public override string Texture => "Terraria/Images/Buff_2";

        public override void SetStaticDefaults()
        {
            Main.buffNoSave[Type] = true;
        }

        public override void Update(Player player, ref int buffIndex)
        {
            WarBannerBuff.ApplyBonuses(player);
        }
    }

    public class WarBannerBuff : ModBuff
    {
        public const float DamageBonus = 0.15f;
        public const float DamageReduction = 0.10f;

        public override string Texture => "Terraria/Images/Buff_2";

        public override void SetStaticDefaults()
        {
            Main.buffNoSave[Type] = true;
            Main.buffNoTimeDisplay[Type] = true;
        }

        public static void ApplyBonuses(Player player)
        {
            player.GetDamage(DamageClass.Generic) += DamageBonus;
            player.endurance += DamageReduction;
        }

        public override void Update(Player player, ref int buffIndex)
        {
            ApplyBonuses(player);
        }
    }
}
