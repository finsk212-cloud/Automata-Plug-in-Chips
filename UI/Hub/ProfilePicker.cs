using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace Augments
{
	// The small list that opens when you click a FAVOURITE PLUGINS slot on the Profile page. It lists only plugins the shown character
	// has collected, strongest rarity first. Click one to pin it, click outside to close, right-click a slot to clear it.
	// The pin is saved by ProfileStore (title screen only: it is cosmetic and nothing in the game's rules reads it).
	internal static class ProfilePicker
	{
		private const int RowH = 26;
		private const int MaxRows = 8;
		private const int PanelW = 280;
		private const int Pad = 8;
		private const int HeaderH = 28;

		private static int slot = -1;
		private static int firstRow;
		private static readonly List<Augment> options = new List<Augment>();
		private static readonly List<string> names = new List<string>();
		private static ProfileStore.CharacterRecord builtFor;

		public static bool IsOpen => slot >= 0;

		public static void Close()
		{
			slot = -1;
			firstRow = 0;
			if (options.Count > 0)
			{
				options.Clear();
				names.Clear();
			}
			builtFor = null;
		}

		private static void Build(ProfileStore.CharacterRecord rec)
		{
			options.Clear();
			names.Clear();
			builtFor = rec;
			if (rec?.EverOwned == null)
				return;

			var found = new List<KeyValuePair<Augment, string>>();
			foreach (string id in rec.EverOwned)
			{
				Augment a = AugmentDatabase.GetById(id);
				if (a == null || a.Id.StartsWith("debug", StringComparison.OrdinalIgnoreCase))
					continue;

				string name = PatchNotesData.Clean(HomeSlide.Clean(a.DisplayName)).ToUpperInvariant();
				if (name.StartsWith("[DEBUG]", StringComparison.Ordinal))
					continue;

				found.Add(new KeyValuePair<Augment, string>(a, name));
			}

			found.Sort((x, y) =>
			{
				int byRarity = ((int)y.Key.Rarity).CompareTo((int)x.Key.Rarity);
				return byRarity != 0 ? byRarity : string.Compare(x.Value, y.Value, StringComparison.Ordinal);
			});

			foreach (var kv in found)
			{
				options.Add(kv.Key);
				names.Add(kv.Value);
			}
		}

		private static int VisibleRows => Math.Min(MaxRows, options.Count);

		private static Rectangle PanelFor(Rectangle slotRect, Rectangle bounds)
		{
			int h = HeaderH + VisibleRows * RowH + Pad * 2;
			int x = Math.Max(bounds.X + 8, Math.Min(slotRect.X, bounds.Right - PanelW - 8));
			int y = slotRect.Bottom + 6;
			if (y + h > bounds.Bottom - 8)
				y = slotRect.Y - 6 - h;
			y = Math.Max(bounds.Y + 8, y);
			return new Rectangle(x, y, PanelW, h);
		}

		private static int SlotAt(Rectangle[] slots, Point p)
		{
			for (int i = 0; i < slots.Length; i++)
			{
				if (slots[i].Contains(p))
					return i;
			}
			return -1;
		}

		private static int RowAt(Rectangle panel, Point p)
		{
			var rows = new Rectangle(panel.X + Pad, panel.Y + HeaderH, panel.Width - 2 * Pad, VisibleRows * RowH);
			if (!rows.Contains(p))
				return -1;

			int index = firstRow + (p.Y - rows.Y) / RowH;
			return index >= 0 && index < options.Count ? index : -1;
		}

		public static void Draw(SpriteBatch sb, Rectangle[] slots, ProfileStore.CharacterRecord rec, Rectangle bounds, Point mouse)
		{
			if (slot < 0)
				return;

			// A different character (or a fresh save of this one) means the list is stale: just close it.
			if (rec == null || !ReferenceEquals(rec, builtFor) || slot >= slots.Length)
			{
				Close();
				return;
			}

			Rectangle panel = PanelFor(slots[slot], bounds);
			HubDraw.Border(sb, slots[slot], HubTheme.Accent, 2);
			HubDraw.Fill(sb, panel, HubTheme.Panel);
			HubDraw.Border(sb, panel, HubTheme.Accent);
			HubDraw.Corners(sb, panel, HubTheme.Accent, 8, 2);

			HubDraw.Text(sb, "PICK A PLUGIN", new Vector2(panel.X + Pad + 4, panel.Y + 8), HubTheme.Warn, 0.66f, 2f);
			HubDraw.TextRight(sb, options.Count + " OWNED", panel.Right - Pad - 4, panel.Y + 9, HubTheme.Dim, 0.56f, 1.5f);
			HubDraw.HLine(sb, panel.X + Pad, panel.Y + HeaderH - 3, panel.Width - 2 * Pad, HubTheme.LineSoft);

			int hover = RowAt(panel, mouse);
			int rows = VisibleRows;
			for (int i = 0; i < rows; i++)
			{
				int index = firstRow + i;
				var row = new Rectangle(panel.X + Pad, panel.Y + HeaderH + i * RowH, panel.Width - 2 * Pad - 8, RowH - 2);
				bool hot = index == hover;
				bool pinnedHere = rec.Favourites != null && rec.Favourites.Contains(options[index].Id);

				if (hot)
					HubDraw.Fill(sb, row, HubTheme.Accent);
				else if (index % 2 == 0)
					HubDraw.Fill(sb, row, HubTheme.PanelAlt * 0.6f);

				HubDraw.Fill(sb, new Rectangle(row.X, row.Y, 4, row.Height), RarityLook.Of(options[index].Rarity));

				string name = names[index];
				float maxW = row.Width - 20 - (pinnedHere ? 54 : 0);
				while (name.Length > 3 && HubDraw.Width(name, 0.6f, 1f) > maxW)
					name = name.Substring(0, name.Length - 1);
				Color text = hot ? HubTheme.OnAccent : HubTheme.Text;
				HubDraw.Text(sb, name, new Vector2(row.X + 12, row.Y + (row.Height - HubDraw.LineHeight(0.6f)) / 2f), text, 0.6f, 1f);
				if (pinnedHere)
					HubDraw.TextRight(sb, "PINNED", row.Right - 6, row.Y + (row.Height - HubDraw.LineHeight(0.5f)) / 2f, hot ? HubTheme.OnAccent : HubTheme.Dim, 0.5f, 1f);
			}

			// A thin scroll marker when the list is longer than the window.
			if (options.Count > rows)
			{
				var track = new Rectangle(panel.Right - Pad - 5, panel.Y + HeaderH, 4, rows * RowH);
				HubDraw.Fill(sb, track, HubTheme.LineSoft);
				int thumbH = Math.Max(14, track.Height * rows / options.Count);
				int range = track.Height - thumbH;
				int thumbY = track.Y + (options.Count - rows == 0 ? 0 : range * firstRow / (options.Count - rows));
				HubDraw.Fill(sb, new Rectangle(track.X, thumbY, track.Width, thumbH), HubTheme.Accent);
			}
		}

		// Left click. Opens, picks or closes. Does nothing if the page has no character.
		public static void Click(Rectangle[] slots, ProfileStore.CharacterRecord rec, Rectangle bounds, Point p)
		{
			if (rec == null)
				return;

			int hit = SlotAt(slots, p);

			if (slot >= 0 && ReferenceEquals(rec, builtFor) && slot < slots.Length)
			{
				Rectangle panel = PanelFor(slots[slot], bounds);
				if (panel.Contains(p))
				{
					int row = RowAt(panel, p);
					if (row >= 0)
					{
						ProfileStore.SetFavourite(rec, slot, options[row].Id);
						SoundEngine.PlaySound(SoundID.MenuOpen);
						Close();
					}
					return;
				}

				// A click outside closes the list; clicking the slot that is open is just a close.
				int open = slot;
				Close();
				SoundEngine.PlaySound(SoundID.MenuClose);
				if (hit < 0 || hit == open)
					return;
			}

			if (hit >= 0)
			{
				Build(rec);
				if (options.Count == 0)
				{
					builtFor = null;
					SoundEngine.PlaySound(SoundID.MenuClose);
					return;
				}

				slot = hit;
				firstRow = 0;
				SoundEngine.PlaySound(SoundID.MenuTick);
			}
		}

		// Right click on a favourite slot clears it.
		public static void RightClick(Rectangle[] slots, ProfileStore.CharacterRecord rec, Point p)
		{
			if (rec == null)
				return;

			int hit = SlotAt(slots, p);
			if (hit < 0)
				return;

			Close();
			if (rec.Favourites != null && hit < rec.Favourites.Count && !string.IsNullOrEmpty(rec.Favourites[hit]))
			{
				ProfileStore.SetFavourite(rec, hit, "");
				SoundEngine.PlaySound(SoundID.MenuClose);
			}
		}

		public static void Wheel(int wheelValue)
		{
			if (slot < 0 || options.Count <= MaxRows)
				return;

			firstRow = Math.Max(0, Math.Min(options.Count - MaxRows, firstRow - Math.Sign(wheelValue)));
		}
	}
}
