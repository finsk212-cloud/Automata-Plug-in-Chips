using System;
using Terraria;

namespace Augments
{
	// On the title screen the player in the first slot was made before this mod loaded, so it has no AugmentPlayer. A plugin description
	// that reads the player (the Fortune ones, Wild Card and Wild Magic do) then throws, and the Archive and the Home slideshow showed no
	// text for it. Read() tries the normal way first and, if that throws, reads again with a fresh player (made after the mod loaded)
	// standing in that slot for the duration of the call.
	internal static class HubPreviewPlayer
	{
		private static Player preview;

		public static string Read(Func<string> read)
		{
			try
			{
				return read();
			}
			catch
			{
			}

			int slot = Main.myPlayer;
			if (slot < 0 || slot >= Main.player.Length)
				return "";

			Player real = Main.player[slot];
			try
			{
				if (preview == null)
					preview = new Player();
				Main.player[slot] = preview;
				return read();
			}
			catch
			{
				return "";
			}
			finally
			{
				Main.player[slot] = real; // always put the real one back
			}
		}
	}
}
