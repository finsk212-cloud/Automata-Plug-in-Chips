using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;

namespace Augments
{
	// A Pod upgrade is a plugin that upgrades the Pod floating at your shoulder instead of adding something new next to you.
	// Each one adds a module to the same Pod (a coloured LED on its arms, a new name, new abilities), and any number of them can be
	// active at the same time, one per plugin you have installed.
	//
	// To add another upgrade: add an entry to PodUpgrades.All, put its behaviour in a small class like PodAutopilot, and tick it from
	// PodPetPlayer.TickUpgrades. Whether a player has an upgrade comes from their installed plugins, which the server and every client
	// already know, so nothing extra has to be sent over the network.
	internal sealed class PodUpgrade
	{
		public string ChipId;     // the plugin that gives this upgrade
		public string PodName;    // what the Pod is called with this upgrade ("POD 153")
		public Color Color;       // the colour of its LED and effects
	}

	internal static class PodUpgrades
	{
		public const string BasePodName = "POD 042";

		public static readonly PodUpgrade[] All =
		{
			new PodUpgrade { ChipId = AutopilotAugment.ChipId, PodName = "POD 153", Color = new Color(80, 255, 140) }
		};

		public static bool Has(Player player, PodUpgrade upgrade)
		{
			return player != null && player.active && player.GetModPlayer<AugmentPlayer>().HasAugment(upgrade.ChipId);
		}

		// The upgrades this player has, in the order of All.
		public static void Active(Player player, List<PodUpgrade> into)
		{
			into.Clear();
			foreach (PodUpgrade upgrade in All)
			{
				if (Has(player, upgrade))
					into.Add(upgrade);
			}
		}

		// "POD 042", or the name of the newest upgrade the player has.
		public static string PodName(Player player)
		{
			string name = BasePodName;
			foreach (PodUpgrade upgrade in All)
			{
				if (Has(player, upgrade))
					name = upgrade.PodName;
			}
			return name;
		}
	}
}
