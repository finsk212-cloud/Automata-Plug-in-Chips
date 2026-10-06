using System.ComponentModel;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;

namespace Augments
{
	// Server-side so the host/world owner decides: on a multiplayer server the
	// server's value applies to everyone, in singleplayer it is the player's own.
	public class AugmentServerConfig : ModConfig
	{
		public override ConfigScope Mode => ConfigScope.ServerSide;

		public static AugmentServerConfig Instance => ModContent.GetInstance<AugmentServerConfig>();

		[DefaultValue(false)]
		public bool EnableDeveloperTools;
	}
}
