using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Augments
{
    public class PluginDebugCommand : ModCommand
    {
        public override string Command => "plugin";
        public override CommandType Type => CommandType.Chat;
        public override string Usage => "/plugin add <player|me> <plug-in name, id or number>";
        public override string Description => "Debug: give a plug-in chip to a player (installs it, or stashes it if their slots are full).";

        public override void Action(CommandCaller caller, string input, string[] args)
        {
            if (!AugmentNet.EnableDebugCommandsInMultiplayer)
            {
                caller.Reply("Developer tools are disabled. Enable them in the mod's server config (Enable Developer Tools).", Color.Orange);
                return;
            }

            if (args.Length < 3 || !args[0].Equals("add", StringComparison.OrdinalIgnoreCase))
            {
                caller.Reply(Usage, Color.Yellow);
                return;
            }

            Player target = FindPlayer(caller, args[1]);
            if (target == null)
            {
                caller.Reply($"No player found matching '{args[1]}'.", Color.Red);
                return;
            }

            string chipQuery = string.Join(" ", args, 2, args.Length - 2);
            Augment augment = FindAugment(chipQuery);
            if (augment == null)
            {
                caller.Reply($"No plug-in found matching '{chipQuery}'. Use /augment list to see names.", Color.Red);
                return;
            }

            if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                AugmentNet.SendDebugGivePlugin(target.whoAmI, augment.Id);
                caller.Reply($"Requested {augment.DisplayName} for {target.name}.", Color.LightGreen);
                return;
            }

            if (AugmentNet.GiveDebugPlugin(target, augment.Id))
                caller.Reply($"Gave {augment.DisplayName} to {target.name}.", Color.LightGreen);
            else
                caller.Reply($"{target.name} already has {augment.DisplayName} (installed, stashed or sold).", Color.Orange);
        }

        private static Player FindPlayer(CommandCaller caller, string query)
        {
            if (query.Equals("me", StringComparison.OrdinalIgnoreCase))
                return caller.Player;

            Player partial = null;
            foreach (Player p in Main.player)
            {
                if (!p.active)
                    continue;
                if (p.name.Equals(query, StringComparison.OrdinalIgnoreCase))
                    return p;
                if (partial == null && p.name.StartsWith(query, StringComparison.OrdinalIgnoreCase))
                    partial = p;
            }

            return partial;
        }

        private static string Normalize(string s)
        {
            return s.Replace("_", "").Replace(" ", "").Replace("-", "").ToLowerInvariant();
        }

        private static Augment FindAugment(string query)
        {
            if (int.TryParse(query, out int number))
                return number >= 1 && number <= AugmentDatabase.All.Count ? AugmentDatabase.All[number - 1] : null;

            Augment byId = AugmentDatabase.GetById(query);
            if (byId != null)
                return byId;

            string key = Normalize(query);
            Augment partial = null;
            foreach (Augment a in AugmentDatabase.All)
            {
                string name = Normalize(a.DisplayName);
                if (name == key || Normalize(a.Id) == key)
                    return a;
                if (partial == null && name.Contains(key))
                    partial = a;
            }

            return partial;
        }
    }
}
