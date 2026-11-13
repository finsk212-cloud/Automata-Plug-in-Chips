using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace Augments
{
	// Static placeholder art for each hub tab. This only shows WHERE things go: lists, grids, stat rows,
	// bars and locked areas. Nothing here is connected to real data yet.
	internal static class HubPages
	{
		// Multiplier for the page-switch flicker, applied to every colour drawn here.
		public static float Fade = 1f;

		private static Color F(Color c) => c * Fade;

		private static readonly Color RarityCommon = new Color(150, 163, 180);
		private static readonly Color RarityRare = new Color(70, 140, 255);
		private static readonly Color RarityEpic = new Color(176, 96, 255);
		private static readonly Color RarityLegendary = new Color(255, 150, 60);

		private static Color Rarity(int i)
		{
			switch (i % 4)
			{
				case 0: return RarityCommon;
				case 1: return RarityRare;
				case 2: return RarityEpic;
				default: return RarityLegendary;
			}
		}

		public static void DrawPage(SpriteBatch sb, int tab, Rectangle area)
		{
			switch (tab)
			{
				case 0: Briefing(sb, area); break;
				case 1: Archive(sb, area); break;
				case 2: UnitRecord(sb, area); break;
				case 3: Orders(sb, area); break;
				case 4: Shop(sb, area); break;
				case 5: Cosmetics(sb, area); break;
				default: Hacking(sb, area); break;
			}
		}

		// ------------------------------------------------------------------ shared pieces

		// Index square, big title, subtitle and rule. Returns the area left for the page body.
		private static Rectangle PageHeader(SpriteBatch sb, Rectangle a, string index, string title, string subtitle)
		{
			var sq = new Rectangle(a.X, a.Y + 2, 38, 38);
			HubDraw.Fill(sb, sq, F(HubTheme.Accent));
			float iw = HubDraw.Width(index, 0.9f, 1f);
			HubDraw.Text(sb, index, new Vector2(sq.X + (sq.Width - iw) / 2f, sq.Y + (sq.Height - HubDraw.LineHeight(0.9f)) / 2f), F(HubTheme.OnAccent), 0.9f, 1f);

			HubDraw.Text(sb, title, new Vector2(a.X + 52, a.Y - 2), F(HubTheme.Text), 1.25f, 3f);
			HubDraw.Text(sb, subtitle, new Vector2(a.X + 53, a.Y + 28), F(HubTheme.Dim), 0.68f, 1.5f);

			HubDraw.HLine(sb, a.X, a.Y + 50, a.Width, F(HubTheme.Line));
			HubDraw.Fill(sb, new Rectangle(a.X, a.Y + 49, 8, 3), F(HubTheme.Accent));
			HubDraw.Fill(sb, new Rectangle(a.Right - 8, a.Y + 49, 8, 3), F(HubTheme.Accent));

			return new Rectangle(a.X, a.Y + 66, a.Width, Math.Max(0, a.Height - 66));
		}

		// Small orange section label with a diamond.
		private static void SectionLabel(SpriteBatch sb, string text, int x, int y)
		{
			HubDraw.Diamond(sb, new Vector2(x + 4, y + 8), 6f, F(HubTheme.Warn));
			HubDraw.Text(sb, text, new Vector2(x + 14, y), F(HubTheme.Warn), 0.7f, 2f);
		}

		private static void Box(SpriteBatch sb, Rectangle r, bool selected = false)
		{
			HubDraw.Fill(sb, r, F(HubTheme.PanelAlt));
			HubDraw.Border(sb, r, F(selected ? HubTheme.Accent : HubTheme.LineSoft), selected ? 2 : 1);
		}

		private static void CenteredText(SpriteBatch sb, string text, Rectangle r, Color c, float scale, float tracking = 0f)
		{
			float w = HubDraw.Width(text, scale, tracking);
			HubDraw.Text(sb, text, new Vector2(r.X + (r.Width - w) / 2f, r.Y + (r.Height - HubDraw.LineHeight(scale)) / 2f), c, scale, tracking);
		}

		private static void Bar(SpriteBatch sb, Rectangle r, float fraction)
		{
			HubDraw.Border(sb, r, F(HubTheme.Line));
			int inner = (int)((r.Width - 4) * MathHelper.Clamp(fraction, 0f, 1f));
			HubDraw.Fill(sb, new Rectangle(r.X + 2, r.Y + 2, inner, r.Height - 4), F(HubTheme.Accent));
		}

		// ------------------------------------------------------------------ 01 briefing

		private static void Briefing(SpriteBatch sb, Rectangle a)
		{
			Rectangle c = PageHeader(sb, a, "01", "BRIEFING", "LATEST TRANSMISSIONS FROM THE BUNKER");
			int gap = 14;
			int leftW = (int)(c.Width * 0.50f);
			var list = new Rectangle(c.X, c.Y, leftW, c.Height);
			var detail = new Rectangle(list.Right + gap, c.Y, c.Width - leftW - gap, c.Height);

			// Left: new plugins list.
			SectionLabel(sb, "NEW PLUGINS", list.X, list.Y);
			string[] names = { "SCAPEGOAT", "EMERGENCY AID", "KEEN EYE", "SOFT LANDING", "SHARED DOSE" };
			string[] tags = { "RARE  /  SUPPORT", "RARE  /  SUPPORT", "COMMON  /  SUPPORT", "COMMON  /  SUPPORT", "COMMON  /  SUPPORT" };
			int rowGap = 6;
			int rowsTop = list.Y + 26;
			int rowH = Math.Max(38, Math.Min(62, (list.Bottom - rowsTop - rowGap * 4) / 5));
			for (int i = 0; i < 5; i++)
			{
				var row = new Rectangle(list.X, rowsTop + i * (rowH + rowGap), list.Width, rowH);
				bool sel = i == 0;
				if (sel)
					HubDraw.Fill(sb, row, F(HubTheme.Accent));
				else
					Box(sb, row);

				Color main = sel ? F(HubTheme.OnAccent) : F(HubTheme.Text);
				Color sub = sel ? F(HubTheme.OnAccent) * 0.75f : F(HubTheme.Dim);
				HubDraw.Diamond(sb, new Vector2(row.X + 20, row.Y + rowH / 2f), 9f, sel ? main : F(Rarity(i < 2 ? 1 : 0)));
				HubDraw.Text(sb, names[i], new Vector2(row.X + 38, row.Y + 7), main, 0.88f, 1.5f);
				HubDraw.Text(sb, tags[i], new Vector2(row.X + 38, row.Y + rowH - 23), sub, 0.62f, 1.5f);

				var badge = new Rectangle(row.Right - 52, row.Y + (rowH - 20) / 2, 40, 20);
				HubDraw.Border(sb, badge, sel ? F(HubTheme.OnAccent) : F(HubTheme.Warn));
				CenteredText(sb, "NEW", badge, sel ? F(HubTheme.OnAccent) : F(HubTheme.Warn), 0.6f, 1f);
			}

			// Right: detail card.
			Box(sb, detail);
			HubDraw.Corners(sb, detail, F(HubTheme.Accent), 12, 2);
			int px = detail.X + 18;
			int innerW = detail.Width - 36;

			var icon = new Rectangle(px, detail.Y + 18, 70, 70);
			HubDraw.Fill(sb, icon, F(HubTheme.Panel));
			HubDraw.Border(sb, icon, F(HubTheme.Line));
			HubDraw.Diamond(sb, icon.Center.ToVector2(), 26f, F(Rarity(1)));
			HubDraw.Text(sb, "SCAPEGOAT", new Vector2(icon.Right + 16, icon.Y + 4), F(HubTheme.Text), 1.0f, 2f);
			HubDraw.Text(sb, "RARE  /  SUPPORT", new Vector2(icon.Right + 16, icon.Y + 32), F(Rarity(1)), 0.66f, 1.5f);
			HubDraw.Text(sb, "ADDED IN  1.3.0", new Vector2(icon.Right + 16, icon.Y + 50), F(HubTheme.Dim), 0.62f, 1.5f);

			int ty = icon.Bottom + 18;
			foreach (string line in HubDraw.Wrap("Every debuff a nearby teammate gets is removed from them and applied to you instead.", innerW, 0.78f))
			{
				HubDraw.Text(sb, line, new Vector2(px, ty), F(HubTheme.Text), 0.78f);
				ty += (int)HubDraw.LineHeight(0.78f) + 2;
			}

			// Version slider.
			int sliderTop = detail.Bottom - 132;
			SectionLabel(sb, "VERSION", px, sliderTop);
			int trackY = sliderTop + 42;
			HubDraw.HLine(sb, px, trackY, innerW, F(HubTheme.Line));
			string[] versions = { "1.0", "1.1", "1.2", "1.3" };
			for (int i = 0; i < versions.Length; i++)
			{
				int nx = px + (int)(innerW * (i / (float)(versions.Length - 1))) - (i == versions.Length - 1 ? 1 : 0);
				HubDraw.Fill(sb, new Rectangle(nx, trackY - 5, 1, 11), F(HubTheme.Line));
				float vw = HubDraw.Width(versions[i], 0.62f, 1f);
				HubDraw.Text(sb, versions[i], new Vector2(MathHelper.Clamp(nx - vw / 2f, px, px + innerW - vw), trackY + 10), F(HubTheme.Dim), 0.62f, 1f);
			}
			HubDraw.Fill(sb, new Rectangle(px + innerW - 7, trackY - 6, 12, 12), F(HubTheme.Accent));

			// Full patch notes button.
			var notes = new Rectangle(px, detail.Bottom - 50, innerW, 32);
			HubDraw.Border(sb, notes, F(HubTheme.Accent));
			HubDraw.CursorRight(sb, notes.X + 12, notes.Y + notes.Height / 2, 10, F(HubTheme.Accent));
			HubDraw.Text(sb, "FULL PATCH NOTES", new Vector2(notes.X + 32, notes.Y + (notes.Height - HubDraw.LineHeight(0.78f)) / 2f), F(HubTheme.Text), 0.78f, 2f);
		}

		// ------------------------------------------------------------------ 02 plugin archive

		private static void Archive(SpriteBatch sb, Rectangle a)
		{
			Rectangle c = PageHeader(sb, a, "02", "ARCHIVE", "EVERY PLUGIN RECORDED IN THE BUNKER");

			// Search box and counter.
			var search = new Rectangle(c.X, c.Y, (int)(c.Width * 0.58f), 34);
			Box(sb, search);
			HubDraw.Text(sb, "SEARCH PLUGINS", new Vector2(search.X + 14, search.Y + (search.Height - HubDraw.LineHeight(0.74f)) / 2f), F(HubTheme.Dim), 0.74f, 2f);
			HubDraw.Fill(sb, new Rectangle(search.X + 168, search.Y + 9, 2, 16), F(HubTheme.Accent) * (Main.GlobalTimeWrappedHourly % 1f < 0.5f ? 1f : 0f));
			HubDraw.TextRight(sb, "COLLECTED  000 / 000", c.Right, c.Y + 9, F(HubTheme.Dim), 0.7f, 1.5f);

			// Filter chips.
			string[] chips = { "ALL", "COMMON", "RARE", "EPIC", "LEGENDARY" };
			int cx = c.X;
			int chipY = c.Y + 46;
			for (int i = 0; i < chips.Length; i++)
			{
				float tw = HubDraw.Width(chips[i], 0.66f, 1.5f);
				int cw = (int)tw + (i == 0 ? 26 : 40);
				var chip = new Rectangle(cx, chipY, cw, 26);
				if (i == 0)
					HubDraw.Fill(sb, chip, F(HubTheme.Accent));
				else
				{
					HubDraw.Border(sb, chip, F(HubTheme.Line));
					HubDraw.Fill(sb, new Rectangle(chip.X + 9, chip.Y + 10, 6, 6), F(Rarity(i - 1)));
				}
				HubDraw.Text(sb, chips[i], new Vector2(chip.X + (i == 0 ? 13 : 24), chip.Y + 5), i == 0 ? F(HubTheme.OnAccent) : F(HubTheme.Text), 0.66f, 1.5f);
				cx += cw + 8;
			}

			// Slot grid.
			// Cells are stretched so the grid exactly fills the page width, and rows fill the height.
			int gridTop = chipY + 42;
			int cgap = 10;
			int cols = Math.Max(1, (c.Width + cgap) / (70 + cgap));
			int cell = (c.Width - (cols - 1) * cgap) / cols;
			int rows = Math.Max(1, (c.Bottom - gridTop + cgap) / (cell + cgap));
			for (int i = 0; i < cols * rows; i++)
			{
				var slot = new Rectangle(c.X + (i % cols) * (cell + cgap), gridTop + (i / cols) * (cell + cgap), cell, cell);
				bool locked = i % 5 == 4;
				Box(sb, slot, i == 0);
				if (locked)
				{
					HubDraw.Stripes(sb, slot, F(HubTheme.Line) * 0.5f);
					CenteredText(sb, "?", slot, F(HubTheme.Dim), 1.0f);
				}
				else
				{
					HubDraw.Fill(sb, new Rectangle(slot.X + 1, slot.Y + 1, slot.Width - 2, 3), F(Rarity(i)));
					HubDraw.Diamond(sb, slot.Center.ToVector2() + new Vector2(0, 2), 18f, F(Rarity(i)) * 0.9f);
				}
			}
		}

		// ------------------------------------------------------------------ 03 unit record

		private static void UnitRecord(SpriteBatch sb, Rectangle a)
		{
			Rectangle c = PageHeader(sb, a, "03", "PROFILE", "YOUR SERVICE FILE");
			int portraitW = Math.Min(210, c.Width / 2 - 10);
			var portrait = new Rectangle(c.X, c.Y, portraitW, Math.Min(260, c.Height - 48));
			Box(sb, portrait);
			HubDraw.Corners(sb, portrait, F(HubTheme.Accent), 12, 2);
			HubDraw.HLine(sb, portrait.X + 14, portrait.Center.Y - 14, portrait.Width - 28, F(HubTheme.LineSoft));
			HubDraw.VLine(sb, portrait.Center.X, portrait.Y + 14, portrait.Height - 56, F(HubTheme.LineSoft));
			HubDraw.Diamond(sb, new Vector2(portrait.Center.X, portrait.Center.Y - 14), 34f, F(HubTheme.Line) * 0.6f);
			// Real numbers from the saved profile (the last character you played) when there is one.
			ProfileStore.CharacterRecord rec = ProfileStore.Selected;
			CenteredText(sb, rec != null ? "UNIT ONLINE" : "NO SIGNAL", new Rectangle(portrait.X, portrait.Bottom - 34, portrait.Width, 26), F(HubTheme.Dim), 0.7f, 2f);

			var plate = new Rectangle(portrait.X, portrait.Bottom + 10, portrait.Width, 30);
			HubDraw.Fill(sb, plate, F(HubTheme.Accent));
			string plateText = rec != null ? rec.Name.ToUpperInvariant() : "UNIT  ----";
			while (plateText.Length > 3 && HubDraw.Width(plateText, 0.78f, 2f) > plate.Width - 24)
				plateText = plateText.Substring(0, plateText.Length - 1);
			HubDraw.Text(sb, plateText, new Vector2(plate.X + 12, plate.Y + (plate.Height - HubDraw.LineHeight(0.78f)) / 2f), F(HubTheme.OnAccent), 0.78f, 2f);

			// The title and emblem chosen from Operation rewards (they belong to the account, so every character shows them on the title screen).
			if (Main.gameMenu)
			{
				string title = Cosmetic.EquippedTitle;
				if (title.Length > 0)
					CenteredText(sb, title, new Rectangle(plate.X, plate.Bottom + 4, plate.Width, 20), F(HubTheme.Warn), 0.62f, 2f);

				string emblem = Cosmetic.EquippedEmblem;
				if (emblem.Length > 0)
					OperationArt.Emblem(sb, emblem, new Rectangle(portrait.Right - 38, portrait.Y + 10, 26, 26), F(HubTheme.Accent));
			}

			// Stats.
			int sx = portrait.Right + 26;
			int sw = c.Right - sx;
			SectionLabel(sb, "SERVICE DATA", sx, c.Y);
			string[] labels = { "RANK", "MACHINE CORES", "BOSSES DEFEATED", "PLUGINS COLLECTED", "BEST DPS" };
			int totalPlugins = 0;
			try
			{
				foreach (Augment plugin in AugmentDatabase.All)
				{
					if (!plugin.Id.StartsWith("debug", StringComparison.OrdinalIgnoreCase))
						totalPlugins++;
				}
			}
			catch
			{
			}

			string[] values = rec == null
				? new[] { "--", "--", "--", "--", "--" }
				: new[] { rec.Rank.ToString(), rec.MachineCores.ToString(), rec.BossesDefeated.ToString(), rec.Collected + " / " + totalPlugins, "--" };
			for (int i = 0; i < labels.Length; i++)
			{
				int ry = c.Y + 30 + i * 32;
				HubDraw.Text(sb, labels[i], new Vector2(sx, ry), F(HubTheme.Text), 0.78f, 1.5f);
				float lw = HubDraw.Width(labels[i], 0.78f, 1.5f);
				HubDraw.DottedH(sb, sx + (int)lw + 10, ry + 13, Math.Max(0, sw - (int)lw - 10 - 46), F(HubTheme.Line));
				HubDraw.TextRight(sb, values[i], c.Right, ry, F(HubTheme.Text), 0.78f, 1f);
			}

			int by = c.Y + 30 + labels.Length * 32 + 10;
			HubDraw.Text(sb, "NEXT RANK", new Vector2(sx, by), F(HubTheme.Dim), 0.66f, 2f);
			Bar(sb, new Rectangle(sx, by + 20, sw, 14), rec != null ? rec.RankProgress : 0.35f);

			// Endings A to E.
			int ey = by + 54;
			SectionLabel(sb, "ENDINGS", sx, ey);
			string letters = "ABCDE";
			for (int i = 0; i < 5; i++)
			{
				var box = new Rectangle(sx + i * 44, ey + 24, 36, 36);
				Box(sb, box);
				HubDraw.Stripes(sb, box, F(HubTheme.Line) * 0.5f, 5);
				CenteredText(sb, letters[i].ToString(), box, F(HubTheme.Dim), 0.8f);
			}
		}

		// ------------------------------------------------------------------ 04 orders

		private static void Orders(SpriteBatch sb, Rectangle a)
		{
			Rectangle c = PageHeader(sb, a, "04", "ORDERS", "DIRECTIVES ISSUED BY COMMAND");

			string[] subs = { "DAILY", "WEEKLY" };
			int sx = c.X;
			for (int i = 0; i < subs.Length; i++)
			{
				int sw = (int)HubDraw.Width(subs[i], 0.66f, 1.5f) + 28;
				var chip = new Rectangle(sx, c.Y, sw, 26);
				if (i == 0)
					HubDraw.Fill(sb, chip, F(HubTheme.Accent));
				else
					HubDraw.Border(sb, chip, F(HubTheme.Line));
				CenteredText(sb, subs[i], chip, i == 0 ? F(HubTheme.OnAccent) : F(HubTheme.Text), 0.66f, 1.5f);
				sx += sw + 8;
			}

			string[] titles = { "DEFEAT A BOSS WITH COMMON CHIPS ONLY", "HEAL 2,000 HP WITH THE MEDI GUN", "COLLECT 3 NEW PLUGINS" };
			string[] descs = { "Use only Common-rarity plugins for the whole fight.", "Total healing from the Medi Gun beam and Overclock.", "Pick up three plugins you have never owned." };
			string[] status = { "ACTIVE", "ACTIVE", "CLAIMED" };
			float[] progress = { 0f, 0.4f, 1f };
			string[] counts = { "0 / 1", "800 / 2000", "3 / 3" };
			int top = c.Y + 40;
			int cgap = 10;
			int cardH = Math.Max(70, Math.Min(98, (c.Bottom - top - cgap * 2) / 3));
			for (int i = 0; i < 3; i++)
			{
				var card = new Rectangle(c.X, top + i * (cardH + cgap), c.Width, cardH);
				bool done = i == 2;
				Box(sb, card, i == 0);
				HubDraw.Fill(sb, new Rectangle(card.X, card.Y, 5, card.Height), F(done ? HubTheme.Dim : HubTheme.Accent));
				HubDraw.Text(sb, titles[i], new Vector2(card.X + 20, card.Y + 9), done ? F(HubTheme.Dim) : F(HubTheme.Text), 0.8f, 1.5f);
				HubDraw.Text(sb, descs[i], new Vector2(card.X + 20, card.Y + 32), F(HubTheme.Dim), 0.68f);
				HubDraw.TextRight(sb, "+ 5 CORES", card.Right - 16, card.Y + 9, done ? F(HubTheme.Dim) : F(HubTheme.Warn), 0.78f, 1.5f);
				HubDraw.TextRight(sb, status[i], card.Right - 16, card.Bottom - 24, F(HubTheme.Dim), 0.62f, 2f);
				Bar(sb, new Rectangle(card.X + 20, card.Bottom - 22, (int)(card.Width * 0.5f), 10), progress[i]);
				HubDraw.Text(sb, counts[i], new Vector2(card.X + 20 + (int)(card.Width * 0.5f) + 12, card.Bottom - 25), F(HubTheme.Dim), 0.62f, 1f);
			}
		}

		// ------------------------------------------------------------------ 05 bunker shop

		private static void Shop(SpriteBatch sb, Rectangle a)
		{
			Rectangle c = PageHeader(sb, a, "05", "SHOP", "EMIL'S WARES, PAID IN MACHINE CORES");

			var bar = new Rectangle(c.X, c.Y, c.Width, 34);
			Box(sb, bar);
			HubDraw.Text(sb, "OPERATOR  EMIL", new Vector2(bar.X + 14, bar.Y + (bar.Height - HubDraw.LineHeight(0.74f)) / 2f), F(HubTheme.Text), 0.74f, 2f);
			HubDraw.TextRight(sb, "MACHINE CORES  000", bar.Right - 14, bar.Y + (bar.Height - HubDraw.LineHeight(0.74f)) / 2f, F(HubTheme.Warn), 0.74f, 2f);

			int cols = 4;
			int cgap = 10;
			int top = c.Y + 48;
			int cellW = (c.Width - (cols - 1) * cgap) / cols;
			int cellH = Math.Max(90, Math.Min(150, (c.Bottom - top - cgap) / 2));
			for (int i = 0; i < 8; i++)
			{
				var cell = new Rectangle(c.X + (i % cols) * (cellW + cgap), top + (i / cols) * (cellH + cgap), cellW, cellH);
				Box(sb, cell, i == 0);
				var icon = new Rectangle(cell.X + 12, cell.Y + 12, 44, 44);
				HubDraw.Fill(sb, icon, F(HubTheme.Panel));
				HubDraw.Border(sb, icon, F(HubTheme.Line));
				HubDraw.Diamond(sb, icon.Center.ToVector2(), 16f, F(Rarity(i)));
				HubDraw.Text(sb, "ITEM NAME", new Vector2(cell.X + 12, cell.Y + 64), F(HubTheme.Text), 0.7f, 1.5f);
				HubDraw.Diamond(sb, new Vector2(cell.X + 17, cell.Bottom - 21), 6f, F(HubTheme.Warn));
				HubDraw.Text(sb, "000", new Vector2(cell.X + 28, cell.Bottom - 29), F(HubTheme.Warn), 0.72f, 1f);
			}
		}

		// ------------------------------------------------------------------ 06 cosmetics

		private static void Cosmetics(SpriteBatch sb, Rectangle a)
		{
			Rectangle c = PageHeader(sb, a, "06", "COSMETICS", "SKINS, POD COLOURS, FRAMES AND BANNERS");

			string[] cats = { "SKIN", "POD", "CHIP FRAME", "BANNER" };
			int cx = c.X;
			for (int i = 0; i < cats.Length; i++)
			{
				int w = (int)HubDraw.Width(cats[i], 0.66f, 1.5f) + 28;
				var chip = new Rectangle(cx, c.Y, w, 26);
				if (i == 0)
					HubDraw.Fill(sb, chip, F(HubTheme.Accent));
				else
					HubDraw.Border(sb, chip, F(HubTheme.Line));
				CenteredText(sb, cats[i], chip, i == 0 ? F(HubTheme.OnAccent) : F(HubTheme.Text), 0.66f, 1.5f);
				cx += w + 8;
			}

			// Skin cards. The first two are the real skins you can preview with the SKIN button.
			string[] names = { "CHASSIS", "YORHA", "RESISTANCE" };
			Color[] back = { new Color(10, 16, 28), new Color(205, 199, 180), new Color(88, 104, 78) };
			Color[] accent = { new Color(56, 189, 248), new Color(72, 67, 58), new Color(214, 190, 120) };
			int top = c.Y + 42;
			int cgap = 14;
			int cardW = (c.Width - 2 * cgap) / 3;
			int cardH = Math.Max(120, Math.Min(230, c.Bottom - top));
			for (int i = 0; i < 3; i++)
			{
				var card = new Rectangle(c.X + i * (cardW + cgap), top, cardW, cardH);
				bool equipped = (i == 0 && HubTheme.Skin == HubSkin.Chassis) || (i == 1 && HubTheme.Skin == HubSkin.YoRHa);
				bool locked = i == 2;
				Box(sb, card, equipped);

				var preview = new Rectangle(card.X + 10, card.Y + 10, card.Width - 20, card.Height - 74);
				HubDraw.Fill(sb, preview, back[i] * Fade);
				HubDraw.Border(sb, preview, F(HubTheme.Line));
				HubDraw.Fill(sb, new Rectangle(preview.X + 10, preview.Y + 10, preview.Width / 2, 14), accent[i] * Fade);
				HubDraw.Fill(sb, new Rectangle(preview.X + 10, preview.Y + 32, preview.Width - 20, 3), accent[i] * (0.5f * Fade));
				HubDraw.Fill(sb, new Rectangle(preview.X + 10, preview.Y + 42, preview.Width - 50, 3), accent[i] * (0.3f * Fade));
				if (locked)
				{
					HubDraw.Stripes(sb, preview, Color.Black * (0.35f * Fade));
					CenteredText(sb, "LOCKED", preview, Color.White * (0.85f * Fade), 0.7f, 2f);
				}

				HubDraw.Text(sb, names[i], new Vector2(card.X + 12, card.Bottom - 54), F(HubTheme.Text), 0.8f, 2f);
				string tag = locked ? "LOCKED" : (equipped ? "EQUIPPED" : "OWNED");
				HubDraw.Text(sb, tag, new Vector2(card.X + 12, card.Bottom - 28), equipped ? F(HubTheme.Warn) : F(HubTheme.Dim), 0.62f, 2f);
			}
		}

		// ------------------------------------------------------------------ 07 hacking

		private static void Hacking(SpriteBatch sb, Rectangle a)
		{
			Rectangle c = PageHeader(sb, a, "07", "HACKING", "RESTRICTED  //  CLEARANCE REQUIRED");
			var box = new Rectangle(c.X + c.Width / 8, c.Y + 10, c.Width * 3 / 4, Math.Min(300, c.Height - 20));
			Box(sb, box);
			HubDraw.Stripes(sb, box, F(HubTheme.Line) * 0.4f, 8);
			HubDraw.Brackets(sb, box, F(HubTheme.Danger), 18, 3);

			CenteredText(sb, "ACCESS DENIED", new Rectangle(box.X, box.Y + box.Height / 2 - 40, box.Width, 40), F(HubTheme.Danger), 1.4f, 5f);
			CenteredText(sb, "CLEARANCE LEVEL INSUFFICIENT", new Rectangle(box.X, box.Y + box.Height / 2 + 4, box.Width, 24), F(HubTheme.Dim), 0.72f, 3f);
			CenteredText(sb, "REQUIREMENT:  [ ?  ?  ? ]", new Rectangle(box.X, box.Y + box.Height / 2 + 34, box.Width, 24), F(HubTheme.Dim), 0.66f, 3f);
		}

		// ------------------------------------------------------------------ right column

		private static readonly string[] PodQuotes =
		{
			"New transmissions received. Review the latest plugins and patch notes.",
			"Archive scan complete. Unrecorded plugins are shown as unknown.",
			"Service file loaded. Rank and records are not connected in this preview.",
			"Directives are issued daily. Completion rewards Machine Cores.",
			"Proposal: spend Machine Cores here. Prices are not final.",
			"Appearance settings. The skin button below previews both looks.",
			"Warning: access to this terminal is restricted."
		};

		public static void DrawSide(SpriteBatch sb, int tab, Rectangle r)
		{
			// Pod box.
			int podH = 150;
			var podBox = new Rectangle(r.X, r.Y, r.Width, podH);
			Box(sb, podBox);
			SectionLabel(sb, "POD 042", podBox.X + 12, podBox.Y + 10);

			Texture2D pod = AugmentAdvisoryHUD.GetPodTexture();
			if (pod != null)
			{
				float bob = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 3f) * 2f;
				sb.Draw(pod, new Vector2(podBox.X + 18, podBox.Y + 50 + bob), null, Color.White, 0f, Vector2.Zero, 2.5f, SpriteEffects.None, 0f);
			}

			int tx = podBox.X + 76;
			int ty = podBox.Y + 40;
			foreach (string line in HubDraw.Wrap(PodQuotes[Math.Max(0, Math.Min(tab, PodQuotes.Length - 1))], podBox.Right - tx - 12, 0.7f))
			{
				HubDraw.Text(sb, line, new Vector2(tx, ty), HubTheme.Text, 0.7f);
				ty += (int)HubDraw.LineHeight(0.7f) + 2;
			}

			// Selection details.
			int detailTop = podBox.Bottom + 12;
			int detailH = 138;
			var detail = new Rectangle(r.X, detailTop, r.Width, detailH);
			Box(sb, detail);
			SectionLabel(sb, "SELECTION", detail.X + 12, detail.Y + 10);
			string[] keys = { "NAME", "TYPE", "STATUS" };
			for (int i = 0; i < keys.Length; i++)
			{
				int ry = detail.Y + 40 + i * 28;
				HubDraw.Text(sb, keys[i], new Vector2(detail.X + 14, ry), HubTheme.Dim, 0.68f, 1.5f);
				float kw = HubDraw.Width(keys[i], 0.68f, 1.5f);
				HubDraw.DottedH(sb, detail.X + 14 + (int)kw + 8, ry + 11, Math.Max(0, detail.Width - 28 - (int)kw - 8 - 24), HubTheme.Line);
				HubDraw.TextRight(sb, "--", detail.Right - 14, ry, HubTheme.Text, 0.68f, 1f);
			}

			// Notice box: a compact fixed-height strip under the details, not stretched to the window bottom.
			int noticeTop = detail.Bottom + 12;
			var notice = new Rectangle(r.X, noticeTop, r.Width, Math.Min(96, Math.Max(0, r.Bottom - noticeTop)));
			if (notice.Height >= 56)
			{
				HubDraw.Border(sb, notice, HubTheme.Warn * 0.8f);
				HubDraw.Stripes(sb, new Rectangle(notice.X, notice.Y, notice.Width, 8), HubTheme.Warn * 0.5f, 2);
				HubDraw.Text(sb, "NOTICE", new Vector2(notice.X + 12, notice.Y + 16), HubTheme.Warn, 0.66f, 2f);
				int ny = notice.Y + 34;
				foreach (string line in HubDraw.Wrap("Layout preview. Nothing here is connected to the game yet.", notice.Width - 24, 0.66f))
				{
					if (ny + 14 > notice.Bottom)
						break;
					HubDraw.Text(sb, line, new Vector2(notice.X + 12, ny), HubTheme.Dim, 0.66f);
					ny += (int)HubDraw.LineHeight(0.66f) + 2;
				}
			}
		}
	}
}
