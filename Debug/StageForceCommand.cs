using System;
using Microsoft.Xna.Framework;
using Terraria.ModLoader;

namespace Augments
{
    // Chat-type so it runs on the player's own client, which is where chip damage and
    // tooltips are calculated. It never touches the world, so no boss kill is needed.
    public class StageForceCommand : ModCommand
    {
        public override string Command => "augmentforce";
        public override CommandType Type => CommandType.Chat;
        public override string Usage => "/augmentforce <number 0-10 | off>";
        public override string Description => "Debug: pin the damage scaling stage on this client for testing.";

        public override void Action(CommandCaller caller, string input, string[] args)
        {
            if (!AugmentNet.EnableDebugCommandsInMultiplayer)
            {
                caller.Reply("Developer tools are disabled. Enable them in the mod's server config (Enable Developer Tools).", Color.Orange);
                return;
            }

            if (args.Length == 1 && args[0].Equals("off", StringComparison.OrdinalIgnoreCase))
            {
                AugmentScaling.DebugOverride = null;
                caller.Reply("Stage override cleared; the real world progress applies again.", Color.LightGreen);
                return;
            }

            if (args.Length == 1 && int.TryParse(args[0], out int n) && n >= 0 && n <= (int)ScalingStage.ExoMechsOrSupremeCalamitas)
            {
                AugmentScaling.DebugOverride = (ScalingStage)n;
                caller.Reply($"Stage forced to {AugmentScaling.StageName((ScalingStage)n)} (x{AugmentScaling.FormatMultiplier(AugmentScaling.Multiplier)}).", Color.LightGreen);
                return;
            }

            string list = "";
            for (int i = 0; i <= (int)ScalingStage.ExoMechsOrSupremeCalamitas; i++)
                list += $"{i}={AugmentScaling.StageName((ScalingStage)i)}; ";
            caller.Reply(Usage, Color.Yellow);
            caller.Reply(list, Color.Yellow);
        }
    }
}
