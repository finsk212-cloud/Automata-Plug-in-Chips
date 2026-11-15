using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace Augments
{
	// Draws the saved character (the real look: hair, armour, dyes) inside the Profile page's portrait box.
	//
	// On the title screen the characters live in Main.PlayerList, the same list the vanilla "Select Player" screen draws from, so the
	// character is found there by name. If it cannot be found, or drawing it ever fails, this just returns false and the page keeps
	// its plain diamond. After one failure it stops trying, so a problem here can never break the title screen every frame.
	internal static class ProfilePortrait
	{
		private static bool failed;

		// A bare character is about 56 pixels tall once its sprite is drawn.
		private const float SpriteHeight = 56f;

		private static Player Find(string name)
		{
			if (string.IsNullOrEmpty(name))
				return null;

			if (Main.gameMenu)
			{
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

			Vector2 oldPosition = p.position;
			int oldDirection = p.direction;
			try
			{
				float scale = MathHelper.Clamp((box.Height - 90f) / SpriteHeight, 1f, 2.8f);

				// The renderer scales a character about its top left corner, so place that corner so the scaled figure is centred
				// in the upper part of the box (the "UNIT ONLINE" text sits under it).
				Vector2 centre = new Vector2(box.Center.X, box.Y + (box.Height - 34f) * 0.5f + 6f);
				Vector2 topLeft = centre - new Vector2(p.width, p.height) * scale * 0.5f;

				p.direction = 1;
				p.position = topLeft + Main.screenPosition;
				p.PlayerFrame();

				Main.PlayerRenderer.DrawPlayer(Main.Camera, p, p.position, 0f, p.fullRotationOrigin, 0f, scale);
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
				p.position = oldPosition;
				p.direction = oldDirection;
			}
		}
	}
}
