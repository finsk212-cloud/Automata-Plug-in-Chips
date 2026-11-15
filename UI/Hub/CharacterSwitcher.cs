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
	// The arrows step to the previous or next character; clicking the name opens a list of all of them.
	// It draws nothing when there are fewer than two characters.
	internal static class CharacterSwitcher
	{
		public const int Width = 250;
		public const int Height = 28;

		private const int RowH = 26;
		private const int MaxRows = 8;
		private const int ListPad = 4;

		private static bool open;
		private static int firstRow;

		public static bool Needed => ProfileStore.All.Count > 1;

		public static Rectangle RectFor(Rectangle panel, int pad = 24) => new Rectangle(panel.Right - pad - Width, panel.Y + 12, Width, Height);

		private static Rectangle LeftArrow(Rectangle r) => new Rectangle(r.X, r.Y, Height, Height);
		private static Rectangle RightArrow(Rectangle r) => new Rectangle(r.Right - Height, r.Y, Height, Height);
		private static Rectangle Mid(Rectangle r) => new Rectangle(r.X + Height + 4, r.Y, r.Width - 2 * Height - 8, r.Height);

		private static int VisibleRows => Math.Min(MaxRows, ProfileStore.All.Count);
		private static Rectangle ListRect(Rectangle r) => new Rectangle(r.X, r.Bottom + 4, r.Width, VisibleRows * RowH + 2 * ListPad);

		private static int SelectedIndex()
		{
			ProfileStore.CharacterRecord rec = ProfileStore.Selected;
			for (int i = 0; i < ProfileStore.All.Count; i++)
			{
				if (ReferenceEquals(ProfileStore.All[i], rec))
					return i;
			}
			return 0;
		}

		public static void Close()
		{
			open = false;
			firstRow = 0;
		}

		public static void Draw(SpriteBatch sb, Rectangle r, Point mouse)
		{
			if (!Needed)
			{
				Close();
				return;
			}

			ProfileStore.CharacterRecord rec = ProfileStore.Selected;
			int index = SelectedIndex();

			DrawArrow(sb, LeftArrow(r), false, mouse);
			DrawArrow(sb, RightArrow(r), true, mouse);

			var mid = Mid(r);
			bool midHover = mid.Contains(mouse);
			if (midHover || open)
				HubDraw.Fill(sb, mid, HubTheme.PanelAlt);
			HubDraw.Border(sb, mid, midHover || open ? HubTheme.Accent : HubTheme.LineSoft);

			string counter = (index + 1) + " / " + ProfileStore.All.Count;
			float cw = HubDraw.Width(counter, 0.56f, 1f);

			// A small triangle before the counter says the name box opens a list.
			int tx = (int)(mid.Right - 8 - cw - 12);
			int ty = mid.Center.Y;
			for (int i = 0; i < 5; i++)
				HubDraw.Fill(sb, new Rectangle(tx - (4 - i), ty - 2 + i, (4 - i) * 2 + 1, 1), HubTheme.Dim);

			string name = (rec?.Name ?? "").ToUpperInvariant();
			float scale = 0.66f;
			float room = mid.Width - cw - 40f;
			float nw = HubDraw.Width(name, scale, 1.5f);
			if (nw > room)
				scale *= room / nw;

			HubDraw.Text(sb, name, new Vector2(mid.X + 8, mid.Y + (mid.Height - HubDraw.LineHeight(scale)) / 2f), HubTheme.Text, scale, 1.5f);
			HubDraw.TextRight(sb, counter, mid.Right - 8, mid.Y + (mid.Height - HubDraw.LineHeight(0.56f)) / 2f, HubTheme.Dim, 0.56f, 1f);
		}

		// The open list of characters. Drawn last by the page, so it sits on top of everything else.
		public static void DrawList(SpriteBatch sb, Rectangle r, Point mouse)
		{
			if (!open)
				return;

			if (!Needed)
			{
				Close();
				return;
			}

			var all = ProfileStore.All;
			int rows = VisibleRows;
			firstRow = Math.Max(0, Math.Min(Math.Max(0, all.Count - rows), firstRow));

			Rectangle list = ListRect(r);
			HubDraw.Fill(sb, list, HubTheme.Panel);
			HubDraw.Border(sb, list, HubTheme.Accent);
			HubDraw.Corners(sb, list, HubTheme.Accent, 8, 2);

			int selected = SelectedIndex();
			int hover = RowAt(list, mouse);
			for (int i = 0; i < rows; i++)
			{
				int index = firstRow + i;
				ProfileStore.CharacterRecord c = all[index];
				var row = new Rectangle(list.X + ListPad, list.Y + ListPad + i * RowH, list.Width - 2 * ListPad - (all.Count > rows ? 8 : 0), RowH - 2);
				bool hot = index == hover;

				if (hot)
					HubDraw.Fill(sb, row, HubTheme.Accent);
				else if (index % 2 == 0)
					HubDraw.Fill(sb, row, HubTheme.PanelAlt * 0.6f);

				// The character being shown has a bar at the left.
				if (index == selected)
					HubDraw.Fill(sb, new Rectangle(row.X, row.Y, 4, row.Height), hot ? HubTheme.OnAccent : HubTheme.Warn);

				string rank = "RANK " + c.Rank.ToString("00");
				float rankW = HubDraw.Width(rank, 0.5f, 1f);
				string name = (c.Name ?? "").ToUpperInvariant();
				float maxW = row.Width - 12 - rankW - 20;
				while (name.Length > 3 && HubDraw.Width(name, 0.6f, 1f) > maxW)
					name = name.Substring(0, name.Length - 1);

				Color text = hot ? HubTheme.OnAccent : HubTheme.Text;
				HubDraw.Text(sb, name, new Vector2(row.X + 12, row.Y + (row.Height - HubDraw.LineHeight(0.6f)) / 2f), text, 0.6f, 1f);
				HubDraw.TextRight(sb, rank, row.Right - 6, row.Y + (row.Height - HubDraw.LineHeight(0.5f)) / 2f, hot ? HubTheme.OnAccent : HubTheme.Dim, 0.5f, 1f);
			}

			// A thin scroll marker when there are more characters than fit.
			if (all.Count > rows)
			{
				var track = new Rectangle(list.Right - ListPad - 5, list.Y + ListPad, 4, rows * RowH);
				HubDraw.Fill(sb, track, HubTheme.LineSoft);
				int thumbH = Math.Max(14, track.Height * rows / all.Count);
				int range = track.Height - thumbH;
				int thumbY = track.Y + (all.Count - rows == 0 ? 0 : range * firstRow / (all.Count - rows));
				HubDraw.Fill(sb, new Rectangle(track.X, thumbY, track.Width, thumbH), HubTheme.Accent);
			}
		}

		private static int RowAt(Rectangle list, Point p)
		{
			var rows = new Rectangle(list.X + ListPad, list.Y + ListPad, list.Width - 2 * ListPad, VisibleRows * RowH);
			if (!rows.Contains(p))
				return -1;

			int index = firstRow + (p.Y - rows.Y) / RowH;
			return index >= 0 && index < ProfileStore.All.Count ? index : -1;
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

		// Handles a click. Returns true if it was on the control or its open list (and moves the selection).
		// A click anywhere else closes the list and returns false, so the page can use the click for something else.
		public static bool Click(Rectangle r, Point p)
		{
			if (!Needed)
			{
				Close();
				return false;
			}

			if (open)
			{
				Rectangle list = ListRect(r);
				if (list.Contains(p))
				{
					int row = RowAt(list, p);
					if (row >= 0)
					{
						ProfileStore.Select(ProfileStore.All[row]);
						SoundEngine.PlaySound(SoundID.MenuTick);
						Close();
					}
					return true;
				}
			}

			if (!r.Contains(p))
			{
				Close();
				return false;
			}

			if (LeftArrow(r).Contains(p))
			{
				Close();
				ProfileStore.CycleSelected(-1);
				SoundEngine.PlaySound(SoundID.MenuTick);
			}
			else if (RightArrow(r).Contains(p))
			{
				Close();
				ProfileStore.CycleSelected(1);
				SoundEngine.PlaySound(SoundID.MenuTick);
			}
			else
			{
				// The name box: open or close the list, scrolled so the shown character is in view.
				open = !open;
				firstRow = open ? Math.Max(0, Math.Min(Math.Max(0, ProfileStore.All.Count - MaxRows), SelectedIndex() - MaxRows / 2)) : 0;
				SoundEngine.PlaySound(open ? SoundID.MenuOpen : SoundID.MenuClose);
			}

			return true;
		}

		public static void Wheel(int wheelValue)
		{
			if (!open || ProfileStore.All.Count <= MaxRows)
				return;

			firstRow = Math.Max(0, Math.Min(ProfileStore.All.Count - MaxRows, firstRow - Math.Sign(wheelValue)));
		}
	}
}
