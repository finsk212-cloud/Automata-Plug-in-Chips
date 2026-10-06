using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Augments.Buffs
{
	public class MediGunCorrosionBuff : ModBuff
	{
		public override string Texture => "Terraria/Images/Buff_69";

		public override void SetStaticDefaults()
		{
			Main.buffNoSave[Type] = true;
			Main.buffNoTimeDisplay[Type] = false;
			Main.debuff[Type] = true;
		}

		public override void Update(NPC npc, ref int buffIndex)
		{
			npc.GetGlobalNPC<MediGunCorrosionNPC>().Corroded = true;
		}
	}
}
