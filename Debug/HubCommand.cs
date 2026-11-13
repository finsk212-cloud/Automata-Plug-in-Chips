using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace Augments
{
	// /hub opens the Automata hub inside a world, so the layout can be checked without going back to the title screen.
	public class HubCommand : ModCommand
	{
		public override string Command => "hub";
		public override CommandType Type => CommandType.Chat;
		public override string Usage => "/hub";
		public override string Description => "Open the Automata hub screen (layout preview).";

		public override void Action(CommandCaller caller, string input, string[] args)
		{
			if (Main.dedServ)
				return;

			AutomataHubSystem.OpenInGame();
		}
	}
}
