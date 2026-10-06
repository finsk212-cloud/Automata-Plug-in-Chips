using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Augments
{
    public class AugmentDebugCommand : ModCommand
    {
        public override string Command => "augment";
        public override CommandType Type => CommandType.Chat;
        public override string Usage => "/augment add|remove|sell|buyback <id or number> | /augment addall | /augment clear | /augment list";
        public override string Description => "Debug: add, remove, sell, buy back, clear, or list plug-in chips.";

        public override void Action(CommandCaller caller, string input, string[] args)
        {
            if (!AugmentNet.EnableDebugCommandsInMultiplayer)
            {
                caller.Reply("Developer tools are disabled. Enable them in the mod's server config (Enable Developer Tools).", Color.Orange);
                return;
            }

            var augmentPlayer = caller.Player.GetModPlayer<AugmentPlayer>();

            if (args.Length == 0)
            {
                caller.Reply(Usage, Color.Yellow);
                return;
            }

            string sub = args[0].ToLower();

            if (sub == "list")
            {
                const int AugmentsPerPage = 8;
                int maxPage = System.Math.Max(1, (AugmentDatabase.All.Count + AugmentsPerPage - 1) / AugmentsPerPage);
                int page = 1;

                if (args.Length >= 2 && int.TryParse(args[1], out int requestedPage))
                    page = System.Math.Clamp(requestedPage, 1, maxPage);

                caller.Reply($"Plug-in Chips page {page}/{maxPage}", Color.Yellow);

                int startIndex = (page - 1) * AugmentsPerPage;
                int endIndex = System.Math.Min(startIndex + AugmentsPerPage, AugmentDatabase.All.Count);

                for (int i = startIndex; i < endIndex; i++)
                {
                    var augment = AugmentDatabase.All[i];
                    string status = augmentPlayer.HasAugment(augment.Id)
                        ? "owned"
                        : augmentPlayer.SoldAugmentIds.Contains(augment.Id) ? "sold" : augmentPlayer.EverOwnedIds.Contains(augment.Id) ? "ever owned" : "new";
                    caller.Reply($"{i + 1}. {augment.Id} - {augment.DisplayName} ({augment.Rarity}, {status})", Color.White);
                }

                return;
            }

            if (sub == "addall")
            {
				RunMutation(caller, DebugAugmentCommandType.AddAll);
                return;
            }

            if (sub == "clear" || sub == "removeall" || sub == "reset")
            {
				RunMutation(caller, DebugAugmentCommandType.Clear);
                return;
            }

            if (args.Length < 2)
            {
                caller.Reply(Usage, Color.Yellow);
                return;
            }

            var selectedAugment = GetAugmentFromArg(args[1]);

            if (selectedAugment == null)
            {
                caller.Reply("No augment found. Use /augment list to see valid numbers and ids.", Color.Red);
                return;
            }

            switch (sub)
            {
                case "add":
					RunMutation(caller, DebugAugmentCommandType.Add, selectedAugment.Id);
                    break;

                case "remove":
					RunMutation(caller, DebugAugmentCommandType.Remove, selectedAugment.Id);
                    break;

				case "sell":
					RunMutation(caller, DebugAugmentCommandType.Sell, selectedAugment.Id);
					break;

				case "buyback":
					RunMutation(caller, DebugAugmentCommandType.BuyBack, selectedAugment.Id);
					break;

                default:
                    caller.Reply(Usage, Color.Yellow);
                    break;
            }
        }

		private static void RunMutation(CommandCaller caller, DebugAugmentCommandType command, string augmentId = "")
		{
			if (Main.netMode == NetmodeID.MultiplayerClient)
			{
				if (!AugmentNet.EnableDebugCommandsInMultiplayer)
				{
					caller.Reply("Multiplayer debug commands are disabled.", Color.Orange);
					return;
				}

				AugmentNet.SendDebugCommandRequest(command, augmentId);
				return;
			}

			AugmentNet.ApplyDebugCommand(caller.Player, command, augmentId);
		}

        private Augment GetAugmentFromArg(string arg)
        {
            if (int.TryParse(arg, out int number))
            {
                int index = number - 1;

                if (index >= 0 && index < AugmentDatabase.All.Count)
                    return AugmentDatabase.All[index];

                return null;
            }

            return AugmentDatabase.GetById(arg);
        }
    }
}
