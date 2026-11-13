using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace Augments
{
	// A small "< NAME  1 / 3 >" control for choosing which saved character the Shop, Profile and currency box show.
	// Characters, their Machine Cores and their plugins are all separate saves, so the title screen has to let you look at each one.
	// It draws nothing when there are fewer than two characters.
	internal static class CharacterSwitcher
	{
		public const int Width = 250;
		public const int Height = 28;

		public static bool Needed => ProfileStore.All.Count > 1;

		public static Rectangle RectFor(Rectangle panel, int pad = 24) => new Rectangle(panel.Right - pad - Width, panel.Y + 12, Width, Height);

		private static Rectangle LeftArrow(Rectangle r) => new Rectangle(r.X, r.Y, Height, Height);
		private static Rectangle RightArrow(Rectangle r) => new Rectangle(r.Right - Height, r.Y, Height, Height);

		public static void Draw(SpriteBatch sb, Rectangle r, Point mouse)
		{
			if (!Needed)
				return;

			ProfileStore.CharacterRecord rec = ProfileStore.Selected;
			int index = 0;
			for (int i = 0; i < ProfileStore.All.Count; i++)
			{
				if (ReferenceEquals(ProfileStore.All[i], rec))
					index = i;
			}

			DrawArrow(sb, LeftArrow(r), false, mouse);
			DrawArrow(sb, RightArrow(r), true, mouse);

			var mid = new Rectangle(r.X + Height + 4, r.Y, r.Width - 2 * Height - 8, r.Height);
			HubDraw.Border(sb, mid, HubTheme.LineSoft);

			string counter = (index + 1) + " / " + ProfileStore.All.Count;
			float cw = HubDraw.Width(counter, 0.56f, 1f);
			string name = (rec?.Name ?? "").ToUpperInvariant();
			float scale = 0.66f;
			float room = mid.Width - cw - 26f;
			float nw = HubDraw.Width(name, scale, 1.5f);
			if (nw > room)
				scale *= room / nw;

			HubDraw.Text(sb, name, new Vector2(mid.X + 8, mid.Y + (mid.Height - HubDraw.LineHeight(scale)) / 2f), HubTheme.Text, scale, 1.5f);
			HubDraw.TextRight(sb, counter, mid.Right - 8, mid.Y + (mid.Height - HubDraw.LineHeight(0.56f)) / 2f, HubTheme.Dim, 0.56f, 1f);
		}

		private static void DrawArrow(SpriteBatch sb, Rectangle box, bool right, Point mouse)
		{
			bool hover = box.Contains(mouse);
			HubDraw.Fill(sb, box, hover ? HubTheme.Accent : HubTheme.PanelAlt);
			HubDraw.Border(sb, box, hover ? HubTheme.Accent : HubTheme.LineSoft);

			Color c = hover ? HubTheme.OnAccent : HubTheme.Text;
			int cx = box.Center.X;
			int cy = box.Center.Y;
			for (int i = 0; i < 5; i++)
			{
				// A small triangle drawn as columns, pointing left or right.
				int x = right ? cx - 3 + i : cx + 3 - i;
				HubDraw.Fill(sb, new Rectangle(x, cy - (4 - i), 1, (4 - i) * 2 + 1), c);
			}
		}

		// Handles a click. Returns true if it was on the control (and moves the selection).
		public static bool Click(Rectangle r, Point p)
		{
			if (!Needed || !r.Contains(p))
				return false;

			if (LeftArrow(r).Contains(p))
			{
				ProfileStore.CycleSelected(-1);
				SoundEngine.PlaySound(SoundID.MenuTick);
			}
			else if (RightArrow(r).Contains(p))
			{
				ProfileStore.CycleSelected(1);
				SoundEngine.PlaySound(SoundID.MenuTick);
			}

			return true;
		}
	}
}
