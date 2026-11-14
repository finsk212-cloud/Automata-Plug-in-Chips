using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using Augments.Core;

namespace Augments
{
    // Developer testing for the anonymous stats. Runs only on your own computer and NEVER sends anything: with no server address
    // filled in, the collector only keeps the numbers in memory so you can look at them with "show".
    public class StatsDebugCommand : ModCommand
    {
        public override string Command => "statsdebug";
        public override CommandType Type => CommandType.Chat;
        public override string Usage => "/statsdebug [on | off | show | prompt | resetprompt]";
        public override string Description => "Debug: test the anonymous balance stats locally (nothing is ever sent).";

        public override void Action(CommandCaller caller, string input, string[] args)
        {
            if (!AugmentNet.EnableDebugCommandsInMultiplayer)
            {
                caller.Reply("Developer tools are disabled. Enable them in the mod's server config (Enable Developer Tools).", Color.Orange);
                return;
            }

            string sub = args.Length > 0 ? args[0].ToLowerInvariant() : "show";
            switch (sub)
            {
                case "on":
                    StatsCollector.DevLocal = true;
                    caller.Reply("Stats test is ON. Fight something with plugins equipped, then type /statsdebug show. Nothing is sent anywhere.", Color.LightGreen);
                    break;

                case "off":
                    StatsCollector.DevLocal = false;
                    StatsCollector.Clear();
                    caller.Reply("Stats test is OFF and the numbers were cleared.", Color.LightGreen);
                    break;

                case "show":
                    if (!StatsCollector.Enabled)
                    {
                        caller.Reply("Stats are not collecting. Type /statsdebug on first.", Color.Orange);
                        break;
                    }
                    foreach (string line in StatsCollector.DescribePending(8))
                        caller.Reply(line, Color.Yellow);
                    break;

                case "prompt":
                    StatsConsent.ForcePrompt = true;
                    caller.Reply("The stats prompt will show on the title screen (leave the world). Nothing is sent. Use /statsdebug resetprompt to forget your answer.", Color.LightGreen);
                    break;

                case "resetprompt":
                    StatsConsent.ResetForTesting();
                    caller.Reply("Forgot the stats answer and switched sharing off.", Color.LightGreen);
                    break;

                default:
                    caller.Reply(Usage, Color.Yellow);
                    break;
            }
        }
    }
}
