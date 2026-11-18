using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Augments
{
	// Debug: /fallen teleports you next to the nearest Fallen Unit you have not recovered yet.
	//   /fallen          the nearest one you have not recovered (or the nearest of all, if you took them all)
	//   /fallen list     how many there are, how many you recovered, and how far the nearest is
	//   /fallen spawn N  put one with look N (1 sword grave, 2 lying in flowers, 3 overgrown) where you stand (singleplayer)
	//   /fallen reset    forget which ones you recovered (singleplayer)
	//   /fallen regen    place all of them again in new spots (singleplayer)
	public class FallenUnitDebugCommand : ModCommand
	{
		public override string Command => "fallen";
		public override CommandType Type => CommandType.Chat;
		public override string Usage => "/fallen [list | spawn 1-3 | reset | regen]";
		public override string Description => "Debug: teleport to the nearest Fallen Unit, or list, spawn, reset and place them again.";

		public override void Action(CommandCaller caller, string input, string[] args)
		{
			Player player = caller.Player;
			string sub = args.Length > 0 ? args[0].ToLowerInvariant() : "";

			switch (sub)
			{
				case "":
				case "near":
				case "tp":
					Teleport(caller, player);
					return;

				case "list":
				{
					int mine = 0;
					foreach (FallenUnit unit in FallenUnitSystem.Units)
						if (unit.IsRecoveredBy(player))
							mine++;
					caller.Reply($"{FallenUnitSystem.Units.Count} Fallen Units in this world. You recovered {mine}.", Color.LightGreen);
					FallenUnit nearest = FallenUnitSystem.Nearest(player.Center, player);
					if (nearest != null)
						caller.Reply($"Nearest unrecovered: Unit {nearest.Name}, look {nearest.Look + 1}, {Describe(player, nearest)}.", Color.LightGreen);
					return;
				}
			}

			if (Main.netMode != NetmodeID.SinglePlayer)
			{
				caller.Reply("That part of /fallen only works in singleplayer.", Color.Orange);
				return;
			}

			switch (sub)
			{
				case "spawn":
				{
					int look = -1;
					if (args.Length > 1 && int.TryParse(args[1], out int n) && n >= 1 && n <= FallenUnitArt.LookCount)
						look = n - 1;

					int x = (int)(player.Center.X / 16f);
					int y = FallenUnitSystem.FindFloor(x, (int)(player.position.Y / 16f), 30);
					if (y < 0)
					{
						caller.Reply("No room here: it needs a flat floor 7 tiles wide with 4 tiles of open space above it.", Color.Orange);
						return;
					}
					FallenUnit unit = FallenUnitSystem.Add(x, y, look);
					caller.Reply($"Placed Unit {unit.Name} (look {unit.Look + 1}).", Color.LightGreen);
					return;
				}

				case "reset":
					foreach (FallenUnit unit in FallenUnitSystem.Units)
					{
						unit.RecoveredBy.Remove(player.name);
						unit.Offers.Remove(player.name); // new cards next time
						player.GetModPlayer<FallenUnitPlayer>().ForgetUnit(unit);
					}
					caller.Reply("You can recover every Fallen Unit again, with new plugins in each.", Color.LightGreen);
					return;

				case "regen":
					FallenUnitSystem.Generate();
					caller.Reply($"Placed {FallenUnitSystem.Units.Count} Fallen Units in new spots.", Color.LightGreen);
					return;

				default:
					caller.Reply(Usage, Color.Yellow);
					return;
			}
		}

		private static void Teleport(CommandCaller caller, Player player)
		{
			FallenUnit unit = FallenUnitSystem.Nearest(player.Center, player) ?? FallenUnitSystem.Nearest(player.Center);
			if (unit == null)
			{
				caller.Reply("There are no Fallen Units in this world.", Color.Orange);
				return;
			}

			// Stand three tiles to the side of the body, feet on its floor.
			Vector2 spot = new Vector2((unit.X - 3) * 16 + 8 - player.width / 2f, (unit.Y + 1) * 16 - player.height);
			player.Teleport(spot, 1);
			player.velocity = Vector2.Zero;
			if (Main.netMode == NetmodeID.MultiplayerClient)
				NetMessage.SendData(MessageID.TeleportEntity, -1, -1, null, 0, player.whoAmI, spot.X, spot.Y, 1);

			string state = unit.IsRecoveredBy(player) ? " You already recovered it." : "";
			caller.Reply($"Teleported to Unit {unit.Name} (look {unit.Look + 1}).{state}", Color.LightGreen);
		}

		private static string Describe(Player player, FallenUnit unit)
		{
			Vector2 d = unit.Center - player.Center;
			int tiles = (int)Math.Round(d.Length() / 16f);
			string side = d.X < 0 ? "left" : "right";
			string updown = d.Y < 0 ? "up" : "down";
			return $"{tiles} tiles away ({side}, {updown})";
		}
	}
}
