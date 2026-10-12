using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace Augments
{
    // World-type so it runs on the server, where the Calamity stage flags are authoritative.
    public class StageDebugCommand : ModCommand
    {
        public override string Command => "augmentstage";
        public override CommandType Type => CommandType.World;
        public override string Usage => "/augmentstage [list | set <stage> | clear]";
        public override string Description => "Debug: show or set the Calamity stage used by chip damage scaling.";

        public override void Action(CommandCaller caller, string input, string[] args)
        {
            if (!AugmentNet.EnableDebugCommandsInMultiplayer)
            {
                caller.Reply("Developer tools are disabled. Enable them in the mod's server config (Enable Developer Tools).", Color.Orange);
                return;
            }

            if (args.Length == 0 || args[0].Equals("list", StringComparison.OrdinalIgnoreCase))
            {
                ShowStatus(caller);
                caller.Reply("Stages you can set: providence, dog, yharon, exo. Use '/augmentstage set <stage>' (marks that stage and the ones before it) or '/augmentstage clear'.", Color.Yellow);
                return;
            }

            if (args[0].Equals("clear", StringComparison.OrdinalIgnoreCase))
            {
                AugmentScalingSystem.ClearAll();
                caller.Reply("Cleared the tracked Calamity stages (Calamity's own flags still count).", Color.LightGreen);
                ShowStatus(caller);
                return;
            }

            if (args[0].Equals("set", StringComparison.OrdinalIgnoreCase) && args.Length >= 2)
            {
                CalamityBoss? boss = args[1].ToLowerInvariant() switch
                {
                    "providence" or "prov" => CalamityBoss.Providence,
                    "dog" or "devourer" or "devourerofgods" => CalamityBoss.DevourerOfGods,
                    "yharon" => CalamityBoss.Yharon,
                    "exo" or "exomechs" or "scal" or "calamitas" => CalamityBoss.ExoMechsOrSupremeCalamitas,
                    _ => null
                };

                if (boss == null)
                {
                    caller.Reply($"Unknown stage '{args[1]}'. Use providence, dog, yharon or exo.", Color.Red);
                    return;
                }

                for (int i = 0; i <= (int)boss.Value; i++)
                    AugmentScalingSystem.SetDown((CalamityBoss)i, true);

                ShowStatus(caller);
                return;
            }

            caller.Reply(Usage, Color.Yellow);
        }

        private static void ShowStatus(CommandCaller caller)
        {
            ScalingStage stage = AugmentScaling.CurrentStage;
            caller.Reply($"Damage scaling stage: {AugmentScaling.StageName(stage)} (x{AugmentScaling.FormatMultiplier(AugmentScaling.Multiplier)}). Calamity loaded: {AugmentScalingSystem.HasCalamity}.", Color.LightGreen);
        }
    }
}
