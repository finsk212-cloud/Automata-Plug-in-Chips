using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Augments
{
	// Debug only, to be removed: /end a b c d e (or /end all, /end reset) hands out endings so you can see how they look in game.
	// Singleplayer only, like the endings themselves. The Profile page shows the result after you leave the world (that saves the character).
	public class EndingsDebugCommand : ModCommand
	{
		public override string Command => "end";
		public override CommandType Type => CommandType.Chat;
		public override string Usage => "/end <A B C D E | all | reset | status>";
		public override string Description => "Debug: complete endings to preview them (singleplayer only).";

		public override void Action(CommandCaller caller, string input, string[] args)
		{
			if (Main.netMode != NetmodeID.SinglePlayer)
			{
				caller.Reply("Endings only work in singleplayer.", Color.Orange);
				return;
			}

			EndingsPlayer endings = caller.Player.GetModPlayer<EndingsPlayer>();

			if (args.Length == 0)
			{
				caller.Reply(Usage, Color.Yellow);
				return;
			}

			foreach (string raw in args)
			{
				// Allow "/end a,b,c" as well as "/end a b c".
				foreach (string part in raw.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
				{
					string arg = part.Trim();

					if (arg.Equals("reset", StringComparison.OrdinalIgnoreCase) || arg.Equals("off", StringComparison.OrdinalIgnoreCase))
					{
						endings.DebugClear();
						caller.Reply("All endings cleared.", Color.LightGreen);
					}
					else if (arg.Equals("status", StringComparison.OrdinalIgnoreCase))
					{
						List<string> lines = endings.DebugStatus();
						if (lines.Count == 0)
							caller.Reply("No boss fight is being followed right now (a boss must be alive within range).", Color.Yellow);
						foreach (string line in lines)
							caller.Reply(line, Color.LightGreen);
						caller.Reply("earned mask = " + endings.EarnedMask, Color.LightGreen);
					}
					else if (arg.Equals("all", StringComparison.OrdinalIgnoreCase))
					{
						for (int i = 0; i < Endings.All.Length; i++)
							endings.Grant(i);
					}
					else if (arg.Length == 1 && char.ToUpperInvariant(arg[0]) >= 'A' && char.ToUpperInvariant(arg[0]) < 'A' + Endings.All.Length)
					{
						endings.Grant(char.ToUpperInvariant(arg[0]) - 'A');
					}
					else
					{
						caller.Reply("Unknown ending \"" + arg + "\". " + Usage, Color.Yellow);
					}
				}
			}
		}
	}
}