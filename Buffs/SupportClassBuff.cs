using Terraria;
using Terraria.ModLoader;

namespace Augments
{
    // Display-only buff showing the player is actively playing Support class.
    // Applied every tick by AugmentPlayer.PostUpdate while any Support augment is owned.
    // Short duration (3 ticks) so it expires almost instantly if the augment is removed.
    // Texture: placeholder — flag for art pass.
    public class SupportClassBuff : ModBuff
    {
        public override string Texture => "Terraria/Images/Buff_88";

        public override void SetStaticDefaults()
        {
            Main.buffNoSave[Type] = true;
            Main.buffNoTimeDisplay[Type] = true;
        }
    }
}
