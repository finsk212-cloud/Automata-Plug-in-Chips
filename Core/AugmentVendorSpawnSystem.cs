using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace Augments
{
	// Safety net for Mistress 2B's move-in. Vanilla's automatic town-NPC arrival can be
	// held up (many modded residents queued, e.g. with Calamity), so once Skeletron is
	// down and she isn't in the world, she is placed homeless next to a player. Vanilla
	// housing then assigns her any free room as usual. Runs on the server / singleplayer only.
	public class AugmentVendorSpawnSystem : ModSystem
	{
		private const int CheckIntervalTicks = 600;

		public override void PostUpdateWorld()
		{
			if (Main.netMode == NetmodeID.MultiplayerClient || !NPC.downedBoss3)
				return;
			if ((int)Main.GameUpdateCount % CheckIntervalTicks != 0)
				return;

			int vendorType = ModContent.NPCType<AugmentVendorNPC>();
			if (NPC.AnyNPCs(vendorType))
				return;

			Player target = null;
			for (int i = 0; i < Main.maxPlayers; i++)
			{
				Player p = Main.player[i];
				if (p.active && !p.dead)
				{
					target = p;
					break;
				}
			}
			if (target == null)
				return;

			int index = NPC.NewNPC(new EntitySource_WorldEvent(), (int)target.Center.X, (int)target.Bottom.Y - 20, vendorType);
			if (index < 0 || index >= Main.maxNPCs)
				return;

			NPC npc = Main.npc[index];
			npc.homeless = true;
			npc.homeTileX = -1;
			npc.homeTileY = -1;
			npc.netUpdate = true;

			string message = "Mistress 2B has arrived.";
			if (Main.netMode == NetmodeID.Server)
				Terraria.Chat.ChatHelper.BroadcastChatMessage(Terraria.Localization.NetworkText.FromLiteral(message), new Color(120, 220, 255));
			else
				Main.NewText(message, 120, 220, 255);
		}
	}
}
