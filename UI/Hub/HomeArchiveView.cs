using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.UI;

namespace Augments
{
	// ARCHIVE: every plugin in the mod, with a search box, rarity and class filters, a scrolling card grid and a
	// detail panel. It needs no player data, so it works on the title screen exactly as it will in a world.
	//
	// Plugins are read from AugmentDatabase once, the first time the section is shown. Each plugin's text is made safe
	// (colour tags removed, characters the in-game font cannot draw replaced) and built only when it is selected, inside
	// try/catch, so one plugin with an odd description cannot break the page. Debug plugins are left out.
	internal class HomeArchiveView : HubElementBase
	{
		// True while the search box has the keyboard. The page's Esc handling checks this so that Esc first
		// leaves the search box and only then goes back to HOME.
		public static bool IsTyping { get; private set; }

		private sealed class Entry
		{
			public Augment A;
			public string Name;
			public string NameLower;
			public string IdLower;
			public AugmentRarity Rarity;
			public AugmentClass Class;
			public string Desc;   // null until built
			public string DescLower;
			public string Detail;
		}

		private sealed class TextLine
		{
			public string Text;
			public int Style; // 0 description, 1 label, 2 detail note
			public float Y;
			public float H;
		}

		private static readonly string[] RarityLabels = { "ALL", "COMMON", "RARE", "EPIC", "LEGENDARY" };
		private static readonly AugmentRarity[] RarityValues = { AugmentRarity.Common, AugmentRarity.Rare, AugmentRarity.Epic, AugmentRarity.Legendary };
		private static readonly string[] ClassLabels = { "ALL", "MELEE", "RANGED", "MAGIC", "SUMMON", "SUPPORT", "UNIVERSAL" };
		private static readonly AugmentClass[] ClassValues = { AugmentClass.Melee, AugmentClass.Ranged, AugmentClass.Magic, AugmentClass.Summon, AugmentClass.Support, AugmentClass.Universal };

		private const int Pad = 24;
		private const int CardH = 50;
		private const int Gap = 8;
		private const int MinCardW = 150;
		private const int BottomPad = 40;

		private readonly AutomataHomeUIState state;
		private readonly HubScrollbar bar;

		private List<Entry> all;
		private List<Entry> shown = new List<Entry>();
		private Entry selected;
		private Entry hovered;

		private int rarityFilter = -1; // -1 = ALL, otherwise an index into RarityValues
		private int classFilter = -1;  // -1 = ALL, otherwise an index into ClassValues
		private string search = "";

		private bool searchFocused;
		private int blinkCount;
		private bool blinkOn = true;

		private float detailScroll;
		private float detailTotalHeight;
		private Entry detailBuiltFor;
		private float detailBuiltWidth = -1f;
		private readonly List<TextLine> detailLines = new List<TextLine>();

		private Vector2 lastSize;

		// The plugins the last saved character has collected (from the profile file). Empty when there is no profile.
		private HashSet<string> collected = new HashSet<string>();
		private ProfileStore.CharacterRecord collectedFrom;

		private void RefreshCollected()
		{
			ProfileStore.CharacterRecord profile = ProfileStore.Selected;
			if (ReferenceEquals(profile, collectedFrom))
				return;

			collectedFrom = profile;
			collected = profile?.EverOwned != null ? new HashSet<string>(profile.EverOwned) : new HashSet<string>();
		}

		// The scrolling parts are drawn through clipping regions, so a card or line that is half scrolled out is cut cleanly
		// at the edge instead of disappearing.
		private readonly HubClipRegion gridClip;
		private readonly HubClipRegion textClip;

		public HomeArchiveView(AutomataHomeUIState state)
		{
			this.state = state;
			bar = new HubScrollbar();
			bar.VisibleWhen = () => IsShown;
			Append(bar);

			gridClip = new HubClipRegion(DrawGrid) { VisibleWhen = () => IsShown };
			Append(gridClip);
			textClip = new HubClipRegion(DrawDetailText) { VisibleWhen = () => IsShown };
			Append(textClip);
		}

		// Where the detail panel's scrolling text sits: below the icon, name and rule, above the bottom edge.
		private static Rectangle DetailTextRect(Rectangle box) =>
			new Rectangle(box.X + 20, box.Y + 104, box.Width - 40, Math.Max(20, box.Height - 104 - 16));

		// --- Layout (shared by drawing, clicking and scrolling) ---

		private struct Frame
		{
			public Rectangle Search;
			public Rectangle Grid;
			public Rectangle Detail;
			public int LeftX;
			public int LeftW;
			public int RarityY;
			public int ClassY;
		}

		private static Frame LayOut(Rectangle r)
		{
			int bodyTop = r.Y + 84;
			int detailW = (int)MathHelper.Clamp(r.Width * 0.36f, 300f, 420f);
			int gap = 20;
			int leftX = r.X + Pad;
			int leftW = r.Width - Pad * 2 - detailW - gap;

			var f = new Frame
			{
				LeftX = leftX,
				LeftW = leftW,
				Search = new Rectangle(leftX, bodyTop, leftW, 32),
				RarityY = bodyTop + 42,
				ClassY = bodyTop + 74
			};

			int gridTop = bodyTop + 108;
			f.Grid = new Rectangle(leftX, gridTop, leftW - 16, Math.Max(40, r.Bottom - BottomPad - gridTop));
			f.Detail = new Rectangle(leftX + leftW + gap, bodyTop, detailW, Math.Max(80, r.Bottom - BottomPad - bodyTop));
			return f;
		}

		// A row of filter chips that shrinks its text until the whole row fits.
		private static Rectangle[] ChipRects(string[] labels, int x, int y, int maxWidth, out float scale)
		{
			scale = 0.62f;
			float total = 0f;
			float[] widths = new float[labels.Length];
			for (int attempt = 0; attempt < 6; attempt++)
			{
				total = 0f;
				for (int i = 0; i < labels.Length; i++)
				{
					widths[i] = HubDraw.Width(labels[i], scale, 1.5f) + 22f;
					total += widths[i];
				}

				total += 6f * (labels.Length - 1);
				if (total <= maxWidth)
					break;
				scale -= 0.03f;
			}

			var rects = new Rectangle[labels.Length];
			float cx = x;
			for (int i = 0; i < labels.Length; i++)
			{
				rects[i] = new Rectangle((int)Math.Round(cx), y, (int)Math.Round(widths[i]), 24);
				cx += widths[i] + 6f;
			}

			return rects;
		}

		private static void GridMetrics(Rectangle grid, out int cols, out int cardW)
		{
			cols = Math.Max(1, (grid.Width + Gap) / (MinCardW + Gap));
			cardW = (grid.Width - (cols - 1) * Gap) / cols;
		}

		private Rectangle CardRect(Rectangle grid, int index, int cols, int cardW, float scroll)
		{
			int row = index / cols;
			int col = index % cols;
			return new Rectangle(grid.X + col * (cardW + Gap), grid.Y + (int)(row * (CardH + Gap) - scroll), cardW, CardH);
		}

		// --- Data ---

		private static string Sanitize(string s) => PatchNotesData.Clean(HomeSlide.Clean(s));

		private void EnsureEntries()
		{
			if (all != null)
				return;

			all = new List<Entry>();
			try
			{
				foreach (Augment a in AugmentDatabase.All)
				{
					try
					{
						string name = Sanitize(a.DisplayName);
						if (name.StartsWith("[DEBUG]", StringComparison.OrdinalIgnoreCase) || a.Id.StartsWith("debug", StringComparison.OrdinalIgnoreCase))
							continue;

						all.Add(new Entry
						{
							A = a,
							Name = name,
							NameLower = name.ToLowerInvariant(),
							IdLower = a.Id.ToLowerInvariant(),
							Rarity = a.Rarity,
							Class = a.Class
						});
					}
					catch
					{
						// one odd plugin is skipped, the rest still show
					}
				}
			}
			catch
			{
			}

			all.Sort((x, y) => string.Compare(x.Name, y.Name, StringComparison.OrdinalIgnoreCase));
			Refilter(true);
		}

		// Builds just the plain description (used by search, which looks through every plugin's text).
		private static void EnsureDesc(Entry e)
		{
			if (e.Desc != null)
				return;

			try
			{
				e.Desc = HubPreviewPlayer.Read(() => Sanitize(e.A.Description));
			}
			catch
			{
				e.Desc = "";
			}

			e.DescLower = e.Desc.ToLowerInvariant();
		}

		// Builds the description and the detailed notes (used by the detail panel).
		private static void EnsureText(Entry e)
		{
			EnsureDesc(e);
			if (e.Detail != null)
				return;

			e.Detail = "";
			try
			{
				string full = HubPreviewPlayer.Read(() => Sanitize(e.A.GetDetailedDescription(Main.LocalPlayer)));
				if (e.Desc.Length > 0 && full.StartsWith(e.Desc, StringComparison.Ordinal))
					full = full.Substring(e.Desc.Length).Trim();
				e.Detail = full == e.Desc ? "" : full;
			}
			catch
			{
			}
		}

		private void Refilter(bool resetScroll)
		{
			// Every word typed must appear somewhere in the plugin's name, id or description text. So "fortune" finds the
			// plugins named Fortune... and also any plugin whose description mentions fortune.
			string[] words = search.Trim().ToLowerInvariant().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
			if (words.Length > 0)
			{
				foreach (Entry entry in all)
					EnsureDesc(entry); // quick after the first search: each plugin's text is only built once
			}

			shown = all.Where(e =>
				(rarityFilter < 0 || e.Rarity == RarityValues[rarityFilter]) &&
				(classFilter < 0 || e.Class == ClassValues[classFilter]) &&
				words.All(w => e.NameLower.Contains(w) || e.IdLower.Contains(w) || (e.DescLower != null && e.DescLower.Contains(w)))).ToList();

			// Plugins whose NAME matches come first; the rest (matched by description) follow. The order within each group stays alphabetical.
			if (words.Length > 0)
				shown = shown.OrderBy(e => words.All(w => e.NameLower.Contains(w)) ? 0 : 1).ToList();

			if (selected == null || !shown.Contains(selected))
			{
				selected = shown.Count > 0 ? shown[0] : null;
				detailScroll = 0f;
			}

			if (resetScroll)
				bar.ViewPosition = 0f;
		}

		private static Color RarityColor(AugmentRarity rarity)
		{
			return RarityLook.Of(rarity); // the shared tier colours (Epic and Legendary move)
		}

		// --- Input ---

		private void FocusSearch(bool focus)
		{
			if (focus && !searchFocused)
			{
				Main.clrInput();
				searchFocused = true;
				blinkCount = 0;
				blinkOn = true;
			}
			else if (!focus)
			{
				searchFocused = false;
			}

			IsTyping = searchFocused;
		}

		public override void ScrollWheel(UIScrollWheelEvent evt)
		{
			base.ScrollWheel(evt);
			if (all == null)
				return;

			Rectangle r = GetDimensions().ToRectangle();
			Frame f = LayOut(r);
			if (f.Detail.Contains(Main.MouseScreen.ToPoint()))
				detailScroll = MathHelper.Clamp(detailScroll - evt.ScrollWheelValue * 0.4f, 0f, Math.Max(0f, detailTotalHeight - DetailTextRect(f.Detail).Height));
			else
				bar.ViewPosition -= evt.ScrollWheelValue * 0.5f;
		}

		public override void LeftClick(UIMouseEvent evt)
		{
			base.LeftClick(evt);
			AutomataHubSystem.Guard(() =>
			{
				EnsureEntries();
				Rectangle r = GetDimensions().ToRectangle();
				Frame f = LayOut(r);
				Point p = evt.MousePosition.ToPoint();

				if (f.Search.Contains(p))
				{
					// The small "x" at the right end of the box clears the search.
					if (search.Length > 0 && p.X >= f.Search.Right - 26)
					{
						search = "";
						Refilter(true);
						SoundEngine.PlaySound(SoundID.MenuTick);
					}
					else
					{
						FocusSearch(true);
					}
					return;
				}

				Rectangle[] rarityChips = ChipRects(RarityLabels, f.LeftX, f.RarityY, f.LeftW, out _);
				for (int i = 0; i < rarityChips.Length; i++)
				{
					if (rarityChips[i].Contains(p))
					{
						rarityFilter = i - 1;
						Refilter(true);
						SoundEngine.PlaySound(SoundID.MenuTick);
						return;
					}
				}

				Rectangle[] classChips = ChipRects(ClassLabels, f.LeftX, f.ClassY, f.LeftW, out _);
				for (int i = 0; i < classChips.Length; i++)
				{
					if (classChips[i].Contains(p))
					{
						classFilter = i - 1;
						Refilter(true);
						SoundEngine.PlaySound(SoundID.MenuTick);
						return;
					}
				}

				if (f.Grid.Contains(p))
				{
					GridMetrics(f.Grid, out int cols, out int cardW);
					for (int i = 0; i < shown.Count; i++)
					{
						Rectangle card = CardRect(f.Grid, i, cols, cardW, bar.ViewPosition);
						if (card.Contains(p)) // the click is already known to be inside the grid, so a half-visible card counts
						{
							selected = shown[i];
							detailScroll = 0f;
							SoundEngine.PlaySound(SoundID.MenuTick);
							return;
						}
					}
				}
			});
		}

		protected override void UpdateContent(GameTime gameTime)
		{
			if (!IsShown)
			{
				if (searchFocused)
					FocusSearch(false);
				return;
			}

			EnsureEntries();
			RefreshCollected();

			Rectangle r = GetDimensions().ToRectangle();
			if (r.Width < 200)
				return;

			Frame f = LayOut(r);
			gridClip.Place(f.Grid, r);
			textClip.Place(DetailTextRect(f.Detail), r);

			var size = new Vector2(r.Width, r.Height);
			if (size != lastSize)
			{
				lastSize = size;
				bar.Left.Set(f.Grid.Right + 8 - r.X, 0f);
				bar.Top.Set(f.Grid.Y - r.Y, 0f);
				bar.Height.Set(f.Grid.Height, 0f);
				Recalculate();
			}

			GridMetrics(f.Grid, out int cols, out _);
			int rows = (shown.Count + cols - 1) / cols;
			float total = Math.Max(0, rows * (CardH + Gap) - Gap);
			bar.SetView(f.Grid.Height, Math.Max(total, f.Grid.Height));

			// A click anywhere outside the search box takes the keyboard back.
			if (searchFocused && Main.mouseLeft && !f.Search.Contains(Main.MouseScreen.ToPoint()))
				FocusSearch(false);

			hovered = null;
			if (IsMouseHovering)
			{
				Point mouse = Main.MouseScreen.ToPoint();
				if (f.Grid.Contains(mouse))
				{
					GridMetrics(f.Grid, out int c, out int w);
					for (int i = 0; i < shown.Count; i++)
					{
						Rectangle card = CardRect(f.Grid, i, c, w, bar.ViewPosition);
						if (card.Contains(mouse))
						{
							hovered = shown[i];
							break;
						}
					}
				}
			}
		}

		// --- Drawing ---

		protected override void DrawContent(SpriteBatch sb)
		{
			EnsureEntries();
			Rectangle r = GetDimensions().ToRectangle();
			Frame f = LayOut(r);

			HubDraw.Fill(sb, r, HubTheme.Panel);
			HubDraw.Border(sb, r, HubTheme.Line);
			MenuLook.DrawFrame(sb, r, HubTheme.Accent);

			// Header.
			var sq = new Rectangle(r.X + Pad, r.Y + 16, 38, 38);
			HubDraw.Fill(sb, sq, HubTheme.Accent);
			float iw = HubDraw.Width("02", 0.9f, 1f);
			HubDraw.Text(sb, "02", new Vector2(sq.X + (sq.Width - iw) / 2f, sq.Y + (sq.Height - HubDraw.LineHeight(0.9f)) / 2f), HubTheme.OnAccent, 0.9f, 1f);
			HubDraw.Text(sb, "ARCHIVE", new Vector2(r.X + Pad + 52, r.Y + 12), HubTheme.Text, 1.25f, 3f);
			HubDraw.Text(sb, "EVERY PLUGIN IN THE MOD", new Vector2(r.X + Pad + 53, r.Y + 42), HubTheme.Dim, 0.66f, 1.5f);
			HubDraw.TextRight(sb, shown.Count + " / " + all.Count + " PLUGINS", r.Right - Pad, r.Y + 34, HubTheme.Dim, 0.72f, 2f);
			if (collectedFrom != null)
			{
				int have = all.Count(e => collected.Contains(e.A.Id));
				HubDraw.TextRight(sb, "COLLECTED " + have + " / " + all.Count, r.Right - Pad, r.Y + 14, HubTheme.Warn, 0.72f, 2f);
			}
			HubDraw.HLine(sb, r.X + Pad, r.Y + 68, r.Width - Pad * 2, HubTheme.Line);

			DrawSearch(sb, f.Search);
			DrawChips(sb, RarityLabels, f.LeftX, f.RarityY, f.LeftW, rarityFilter + 1, true);
			DrawChips(sb, ClassLabels, f.LeftX, f.ClassY, f.LeftW, classFilter + 1, false);
			// (the card grid and the detail text are drawn by the clipping regions, which are children of this view)
			DrawDetail(sb, f.Detail);

			HubDraw.Text(sb, searchFocused ? "TYPE TO SEARCH     [ENTER] DONE" : "CLICK A PLUGIN FOR DETAILS     MOUSE WHEEL SCROLLS", new Vector2(r.X + Pad, r.Bottom - 26), HubTheme.Dim, 0.6f, 2f);
		}

		private void DrawSearch(SpriteBatch sb, Rectangle box)
		{
			if (searchFocused)
			{
				// Typing, handled the same way as the in-game plugin list's search box.
				PlayerInput.WritingText = true;
				Main.CurrentInputTextTakerOverride = this;
				Main.instance.HandleIME();

				string input = Main.GetInputText(search).Replace("\r", "").Replace("\n", "");
				if (Main.inputTextEscape)
				{
					Main.inputTextEscape = false;
					FocusSearch(false);
				}
				else if (Main.inputTextEnter)
				{
					Main.inputTextEnter = false;
					FocusSearch(false);
				}

				if (input != search)
				{
					search = input;
					Refilter(true);
				}

				if (++blinkCount >= 20)
				{
					blinkCount = 0;
					blinkOn = !blinkOn;
				}
			}

			HubDraw.Fill(sb, box, HubTheme.PanelAlt);
			HubDraw.Border(sb, box, searchFocused ? HubTheme.Accent : HubTheme.LineSoft, searchFocused ? 2 : 1);

			float scale = 0.74f;
			float ty = box.Y + (box.Height - HubDraw.LineHeight(scale)) / 2f;
			if (search.Length == 0 && !searchFocused)
			{
				HubDraw.Text(sb, "SEARCH NAME OR DESCRIPTION", new Vector2(box.X + 14, ty), HubTheme.Dim, scale, 2f);
			}
			else
			{
				string shownText = search;
				while (shownText.Length > 1 && HubDraw.Width(shownText, scale, 1f) > box.Width - 60)
					shownText = shownText.Substring(1);

				HubDraw.Text(sb, shownText, new Vector2(box.X + 14, ty), HubTheme.Text, scale, 1f);
				if (searchFocused && blinkOn)
					HubDraw.Fill(sb, new Rectangle(box.X + 14 + (int)HubDraw.Width(shownText, scale, 1f) + 3, box.Y + 8, 2, box.Height - 16), HubTheme.Accent);
			}

			if (search.Length > 0)
				HubDraw.TextRight(sb, "x", box.Right - 12, ty, HubTheme.Dim, scale, 0f);
		}

		private void DrawChips(SpriteBatch sb, string[] labels, int x, int y, int maxWidth, int selectedIndex, bool rarityRow)
		{
			Rectangle[] rects = ChipRects(labels, x, y, maxWidth, out float scale);
			Point mouse = Main.MouseScreen.ToPoint();
			for (int i = 0; i < rects.Length; i++)
			{
				Rectangle chip = rects[i];
				bool active = i == selectedIndex;
				bool hot = IsMouseHovering && chip.Contains(mouse);

				if (active)
					HubDraw.Fill(sb, chip, HubTheme.Accent);
				else if (hot)
					HubDraw.Fill(sb, chip, HubTheme.PanelAlt);
				HubDraw.Border(sb, chip, active ? HubTheme.Accent : HubTheme.Line);

				float textX = chip.X + 11;
				if (rarityRow && i > 0)
				{
					HubDraw.Fill(sb, new Rectangle(chip.X + 8, chip.Y + 9, 6, 6), RarityColor(RarityValues[i - 1]));
					textX = chip.X + 20;
				}

				Color c = active ? HubTheme.OnAccent : HubTheme.Text;
				HubDraw.Text(sb, labels[i], new Vector2(textX, chip.Y + (chip.Height - HubDraw.LineHeight(scale)) / 2f + 1f), c, scale, 1.5f);
			}
		}

		private void DrawGrid(SpriteBatch sb, Rectangle grid)
		{
			if (shown.Count == 0)
			{
				HubDraw.Text(sb, "NO PLUGINS MATCH", new Vector2(grid.X + 8, grid.Y + 10), HubTheme.Dim, 0.8f, 2f);
				return;
			}

			GridMetrics(grid, out int cols, out int cardW);
			float scroll = bar.ViewPosition;
			for (int i = 0; i < shown.Count; i++)
			{
				Rectangle card = CardRect(grid, i, cols, cardW, scroll);
				if (card.Bottom < grid.Y)
					continue;
				if (card.Y > grid.Bottom)
					break;
				// Cards that are partly scrolled out are drawn too: the clipping region cuts them at the grid's edge.

				Entry e = shown[i];
				bool isSelected = e == selected;
				bool isHot = e == hovered;
					bool plate = RarityLook.Inverted(e.Rarity);
					Color ink = plate ? RarityLook.OnPlate : (isHot ? Color.Lerp(MenuLook.Current.Ivory, Color.White, 0.35f) : MenuLook.Current.Ivory);

					// The same look as the in-game plugins menu: a dark card with the tier's own frame, and Legendary as a bright plate with dark text.
					HubDraw.Fill(sb, card, isHot ? PanelChrome.Tone(new Color(14, 22, 38)) : PanelChrome.Tone(new Color(10, 16, 28)));
					if (plate)
						RarityLook.DrawPlate(sb, card);
					RarityLook.DrawTierFrame(sb, card, e.Rarity, isHot, false, true);

					// The selected card takes the scheme's amber (dark ink on a plate), like in the game menu.
					if (isSelected)
					{
						Color pick = plate ? RarityLook.OnPlate : MenuLook.Current.Amber;
						if (!plate)
							HubDraw.Fill(sb, card, pick * 0.10f);
						HubDraw.Border(sb, card, pick, 3);
					}

					Texture2D icon = AugmentSlotElement.GetClassIcon(e.Class);
					if (icon != null)
					{
						float fit = Math.Min(22f / icon.Width, 22f / icon.Height);
						var dest = new Rectangle(card.X + 14, card.Y + (card.Height - (int)(icon.Height * fit)) / 2, (int)(icon.Width * fit), (int)(icon.Height * fit));
						sb.Draw(icon, dest, ink);
					}

					// A small tick on the plugins this character has collected (the same green as the in-game check).
					if (collected.Contains(e.A.Id))
					{
						Color tick = plate ? RarityLook.Darken(AugmentTextColors.Healing, 0.55f) : AugmentTextColors.Healing;
						int tx = card.Right - 20;
						int tyy = card.Y + 10;
						HubDraw.Fill(sb, new Rectangle(tx, tyy + 4, 2, 2), tick);
						HubDraw.Fill(sb, new Rectangle(tx + 2, tyy + 6, 2, 2), tick);
						HubDraw.Fill(sb, new Rectangle(tx + 4, tyy + 4, 2, 2), tick);
						HubDraw.Fill(sb, new Rectangle(tx + 6, tyy + 2, 2, 2), tick);
						HubDraw.Fill(sb, new Rectangle(tx + 8, tyy, 2, 2), tick);
					}

					float scale = 0.66f;
					List<string> lines = HubDraw.Wrap(e.Name, card.Width - 46 - 8, scale);
					int shownLines = Math.Min(2, lines.Count);
					float lh = HubDraw.LineHeight(scale);
					float ty = card.Y + (card.Height - lh * shownLines) / 2f;
					for (int l = 0; l < shownLines; l++)
						HubDraw.Text(sb, lines[l], new Vector2(card.X + 44, ty + l * lh), ink, scale, 0f);
			}
		}

		// The detail panel's description and notes, drawn inside a clipping region so a line half scrolled out is cut cleanly.
		private void DrawDetailText(SpriteBatch sb, Rectangle area)
		{
			if (selected == null)
				return;

			bool plateText = RarityLook.Inverted(selected.Rarity);
			foreach (TextLine line in detailLines)
			{
				float ly = area.Y + line.Y - detailScroll;
				if (ly + line.H < area.Y || ly > area.Bottom)
					continue;

				switch (line.Style)
				{
					case 1:
						HubDraw.Text(sb, line.Text, new Vector2(area.X, ly), plateText ? RarityLook.OnPlate : MenuLook.Current.Amber, 0.66f, 2f);
						break;
					case 2:
						HubDraw.Text(sb, line.Text, new Vector2(area.X, ly), plateText ? RarityLook.OnPlate * 0.75f : MenuLook.Current.Muted, 0.66f, 0f);
						break;
					default:
						HubDraw.Text(sb, line.Text, new Vector2(area.X, ly), plateText ? RarityLook.OnPlate : MenuLook.Current.Ivory, 0.76f, 0f);
						break;
				}
			}
		}

		private void BuildDetail(float width)
		{
			detailBuiltFor = selected;
			detailBuiltWidth = width;
			detailLines.Clear();
			detailTotalHeight = 0f;
			if (selected == null)
				return;

			EnsureText(selected);
			float y = 0f;

			void Add(string text, int style, float scale)
			{
				float lh = HubDraw.LineHeight(scale) + 3f;
				foreach (string line in HubDraw.Wrap(text, width, scale))
				{
					detailLines.Add(new TextLine { Text = line, Style = style, Y = y, H = lh });
					y += lh;
				}
			}

			if (!string.IsNullOrEmpty(selected.Desc))
				Add(selected.Desc, 0, 0.76f);

			if (!string.IsNullOrEmpty(selected.Detail))
			{
				y += 12f;
				detailLines.Add(new TextLine { Text = "DETAILS", Style = 1, Y = y, H = 18f });
				y += 22f;
				Add(selected.Detail, 2, 0.66f);
			}

			detailTotalHeight = y;
		}

		private void DrawDetail(SpriteBatch sb, Rectangle box)
		{
			// Dark like the in-game detail panel; Legendary is the bright plate with dark text.
			bool plate = selected != null && RarityLook.Inverted(selected.Rarity);
			if (plate)
				RarityLook.DrawPlate(sb, box);
			else
				HubDraw.Fill(sb, box, PanelChrome.Tone(new Color(10, 16, 28)));
			HubDraw.Border(sb, box, plate ? RarityLook.PlateAccent : MenuLook.Current.Muted * 0.5f);
			HubDraw.Corners(sb, box, plate ? RarityLook.PlateAccent : MenuLook.Current.Ivory, 12, 2);

			if (selected == null)
			{
				HubDraw.Text(sb, "SELECT A PLUGIN", new Vector2(box.X + 20, box.Y + 24), MenuLook.Current.Muted, 0.8f, 2f);
				return;
			}

			int innerW = box.Width - 40;
			if (detailBuiltFor != selected || Math.Abs(detailBuiltWidth - innerW) > 0.5f)
				BuildDetail(innerW);

			Color rarityColor = RarityColor(selected.Rarity);

			// Class icon in a frame, then name and tags.
			var frame = new Rectangle(box.X + 20, box.Y + 20, 56, 56);
			HubDraw.Fill(sb, frame, plate ? Color.Lerp(RarityLook.Plate, RarityLook.OnPlate, 0.08f) : PanelChrome.Tone(new Color(14, 22, 38)));
			HubDraw.Border(sb, frame, plate ? RarityLook.PlateAccent : rarityColor * 0.8f);
			HubDraw.Fill(sb, new Rectangle(frame.X, frame.Y, frame.Width, 3), plate ? RarityLook.PlateAccent : rarityColor);
			Texture2D icon = AugmentSlotElement.GetClassIcon(selected.Class);
			if (icon != null)
			{
				float fit = Math.Min(34f / icon.Width, 34f / icon.Height);
				int dw = (int)(icon.Width * fit);
				int dh = (int)(icon.Height * fit);
				sb.Draw(icon, new Rectangle(frame.X + (frame.Width - dw) / 2, frame.Y + 4 + (frame.Height - 4 - dh) / 2, dw, dh), plate ? RarityLook.OnPlate : MenuLook.Current.Ivory);
			}

			int textX = frame.Right + 14;
			int textW = box.Right - 20 - textX;
			float nameScale = 0.95f;
			float nameTracking = 1.5f;
			while (nameScale > 0.6f && HubDraw.Width(selected.Name, nameScale, nameTracking) > textW)
			{
				nameScale -= 0.05f;
				nameTracking = Math.Max(0.3f, nameTracking - 0.2f);
			}

			string name = selected.Name;
			while (name.Length > 4 && HubDraw.Width(name, nameScale, nameTracking) > textW)
				name = name.Substring(0, name.Length - 1);
			if (name.Length < selected.Name.Length)
				name = name.TrimEnd() + "...";

			HubDraw.Text(sb, name, new Vector2(textX, frame.Y + 2), plate ? RarityLook.OnPlate : MenuLook.Current.Ivory, nameScale, nameTracking);
			HubDraw.Text(sb, selected.Rarity.ToString().ToUpperInvariant(), new Vector2(textX, frame.Y + 30), plate ? RarityLook.OnPlate : rarityColor, 0.66f, 2f);
			float rw = HubDraw.Width(selected.Rarity.ToString().ToUpperInvariant(), 0.66f, 2f);
			HubDraw.Text(sb, "/  " + selected.Class.ToString().ToUpperInvariant(), new Vector2(textX + rw + 10, frame.Y + 30), plate ? RarityLook.OnPlate * 0.75f : MenuLook.Current.Muted, 0.66f, 2f);
			if (collected.Contains(selected.A.Id))
				HubDraw.Text(sb, "COLLECTED", new Vector2(textX, frame.Y + 48), plate ? RarityLook.OnPlate : MenuLook.Current.Amber, 0.58f, 2f);

			int ruleY = frame.Bottom + 14;
			HubDraw.HLine(sb, box.X + 20, ruleY, box.Width - 40, plate ? RarityLook.OnPlate * 0.35f : MenuLook.Current.Muted * 0.5f);

			// Description and notes scroll with the mouse wheel; the text itself is drawn by DrawDetailText inside a clipping region.
			Rectangle textArea = DetailTextRect(box);
			float maxScroll = Math.Max(0f, detailTotalHeight - textArea.Height);
			detailScroll = MathHelper.Clamp(detailScroll, 0f, maxScroll);

			if (maxScroll > 0f)
			{
				string more = detailScroll < maxScroll - 1f ? "MORE v" : "";
				HubDraw.TextRight(sb, more, box.Right - 20, box.Bottom - 20, plate ? RarityLook.OnPlate * 0.75f : MenuLook.Current.Muted, 0.56f, 1f);
			}
		}
	}
}
