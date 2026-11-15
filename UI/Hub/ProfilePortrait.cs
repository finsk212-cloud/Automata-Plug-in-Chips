using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent.UI.Elements;

namespace Augments
{
	// Draws the saved character (the real look: hair, armour, dyes) inside the Profile page's portrait box.
	//
	// On the title screen the characters live in Main.PlayerList, the same list the vanilla "Select Player" screen draws from, so the
	// character is found there by name and drawn with the game's own character preview element (UICharacter), the one the Select Player
	// screen uses. Calling the player renderer directly drew nothing here, probably because the character's textures were not loaded yet.
	//
	// If the character cannot be found, or drawing it ever fails, this returns false and the page keeps its plain diamond. After one
	// failure it stops trying, so a problem here can never break the title screen every frame.
	internal static class ProfilePortrait
	{
		private static bool failed;
		private static bool triedLoading;

		private static UICharacter preview;
		private static Player previewFor;
		private static float previewScale;

		// The preview element is a square this big (in UI units); the character is drawn in the middle of it.
		private const int PreviewSize = 120;

		private static Player Find(string name)
		{
			if (string.IsNullOrEmpty(name))
				return null;

			if (Main.gameMenu)
			{
				// The game fills this list only when the Select Player screen is opened. If nobody has opened it yet, load it once here
				// (the same call the Select Player screen makes), so the character is there without a visit to that screen.
				if ((Main.PlayerList == null || Main.PlayerList.Count == 0) && !triedLoading)
				{
					triedLoading = true;
					try
					{
						Main.LoadPlayers();
					}
					catch (Exception e)
					{
						Terraria.ModLoader.ModContent.GetInstance<Augments>().Logger.Warn("The Profile page could not load the character list: " + e.Message);
					}
				}

				var list = Main.PlayerList;
				if (list == null)
					return null;

				foreach (var data in list)
				{
					Player p = data?.Player;
					if (p != null && string.Equals(p.name, name, StringComparison.Ordinal))
						return p;
				}

				return null;
			}

			Player local = Main.LocalPlayer;
			return local != null && string.Equals(local.name, name, StringComparison.Ordinal) ? local : null;
		}

		// Returns true when the character was drawn.
		public static bool Draw(SpriteBatch sb, Rectangle box, string name)
		{
			if (failed)
				return false;

			Player p = Find(name);
			if (p == null)
				return false;

			bool oldActive = p.active;
			try
			{
				// Three times the normal size when the box is tall enough, twice on a shorter one. (Calling the renderer directly at 2.8 drew nothing in
				// testing, so if 3 ever shows nothing, go back to 2, which is known to work.)
				float scale = box.Height >= 240 ? 3f : (box.Height >= 200 ? 2f : 1f);
				if (preview == null || !ReferenceEquals(previewFor, p) || previewScale != scale)
				{
					preview = new UICharacter(p, false, false, scale, false);
					previewFor = p;
					previewScale = scale;
				}

				// Centre it in the upper part of the box (the "UNIT ONLINE" text sits under it).
				Vector2 centre = new Vector2(box.Center.X, box.Y + (box.Height - 34f) * 0.5f + 6f);
				preview.Left.Set(centre.X - PreviewSize / 2f, 0f);
				preview.Top.Set(centre.Y - PreviewSize / 2f, 0f);
				preview.Width.Set(PreviewSize, 0f);
				preview.Height.Set(PreviewSize, 0f);
				preview.Recalculate();

				// Characters in the title-screen list are not "active"; the game's own screen draws them anyway, but this costs nothing.
				p.active = true;
				preview.Draw(sb);
				return true;
			}
			catch (Exception e)
			{
				failed = true;
				try
				{
					Terraria.ModLoader.ModContent.GetInstance<Augments>().Logger.Warn("The Profile page could not draw the character and will use the plain picture: " + e.Message);
				}
				catch
				{
				}
				return false;
			}
			finally
			{
				p.active = oldActive;
			}
		}
	}
}
