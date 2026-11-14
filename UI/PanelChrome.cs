using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.ID;
using Terraria.UI;

namespace Augments
{
	// Single source of truth for the look shared by every Automata panel
	// (Plugins, Analytics, Shop, Gacha, Choice): colors, frame accents and the close button.
	public static class PanelChrome
	{
		// The in-game panels were drawn in a fixed navy and cyan. Every colour here now follows the colour scheme picked on the
		// COSMETICS page (MenuLook), the same one the title screen uses, so a scheme repaints the in-game windows too.
		public static Color PanelBackground => Tone(new Color(10, 16, 28, 250));
		public static Color PanelBorder => Tone(new Color(30, 41, 59));
		public static Color Divider => Tone(new Color(30, 41, 59)) * 0.90f;
		public static Color Accent => Tone(new Color(56, 189, 248));

		// "Machine Cores: 12" as colour-tagged text in the scheme colours (label in the scheme's idle tone, count in its amber).
		public static string CoreCounter(int count)
		{
			return $"[c/{MenuLook.Current.Idle.Hex3()}:Machine Cores:] [c/{MenuLook.Current.Amber.Hex3()}:{count:N0}]";
		}

		// The accent of a combat stat card (DPS, damage, blocked, time, crits): the scheme's own colours, instead of the old
		// green, cyan, teal, violet and gold, so the DPS cards match the rest of the screen.
		public static Color StatAccent(PinnedStatType type)
		{
			MenuLook.Scheme s = MenuLook.Current;
			switch (type)
			{
				case PinnedStatType.LiveDPS: return s.Ivory;
				case PinnedStatType.TotalDamage: return s.Idle;
				case PinnedStatType.HitsAndCrits: return s.Amber;
				case PinnedStatType.DamageBlocked: return Color.Lerp(s.Muted, Color.White, 0.35f);
				default: return Color.Lerp(s.Muted, s.Ivory, 0.35f);
			}
		}

		private static int toneScheme = -1;
		private static float toneHue, toneSat;

		// Recolours one of the old blue colours into the selected scheme. Only the blue family (cyan, navy, slate) is touched: the
		// colour keeps its brightness, takes the scheme's hue, and takes saturation in proportion to how colourful the scheme is.
		// Greys, whites, greens, reds, golds and purples are left alone, so rarity colours and warnings keep their meaning.
		// The old accent cyan itself becomes the scheme's exact bright colour.
		public static Color Tone(Color c)
		{
			if (c.R == 56 && c.G == 189 && c.B == 248)
			{
				Color a = MenuLook.Current.Ivory;
				return new Color(a.R, a.G, a.B, (int)c.A);
			}

			float r = c.R / 255f, g = c.G / 255f, b = c.B / 255f;
			float max = Math.Max(r, Math.Max(g, b));
			float d = max - Math.Min(r, Math.Min(g, b));
			if (d < 0.0001f || max <= 0f)
				return c;

			float s = d / max;
			if (s < 0.10f)
				return c;

			float h = max == r ? 60f * (((g - b) / d) % 6f) : max == g ? 60f * ((b - r) / d + 2f) : 60f * ((r - g) / d + 4f);
			if (h < 0f)
				h += 360f;
			if (h < 175f || h > 260f)
				return c;

			if (toneScheme != MenuLook.SelectedIndex)
			{
				toneScheme = MenuLook.SelectedIndex;
				Color a = MenuLook.Current.Ivory;
				float am = Math.Max(a.R, Math.Max(a.G, a.B)) / 255f;
				float ad = am - Math.Min(a.R, Math.Min(a.G, a.B)) / 255f;
				float ar = a.R / 255f, ag = a.G / 255f, ab = a.B / 255f;
				float ah = ad < 0.0001f ? 0f : am == ar ? 60f * (((ag - ab) / ad) % 6f) : am == ag ? 60f * ((ab - ar) / ad + 2f) : 60f * ((ar - ag) / ad + 4f);
				toneHue = ah < 0f ? ah + 360f : ah;
				toneSat = am <= 0f ? 0f : ad / am / 0.774f; // 0.774 is the saturation of the old cyan
			}

			float ns = Math.Min(1f, s * toneSat);
			return FromHsv(toneHue, ns, max, c.A);
		}

		private static Color FromHsv(float h, float s, float v, int alpha)
		{
			float c = v * s;
			float x = c * (1f - Math.Abs(h / 60f % 2f - 1f));
			float m = v - c;
			float r, g, b;
			if (h < 60f) { r = c; g = x; b = 0f; }
			else if (h < 120f) { r = x; g = c; b = 0f; }
			else if (h < 180f) { r = 0f; g = c; b = x; }
			else if (h < 240f) { r = 0f; g = x; b = c; }
			else if (h < 300f) { r = x; g = 0f; b = c; }
			else { r = c; g = 0f; b = x; }
			return new Color((int)((r + m) * 255f + 0.5f), (int)((g + m) * 255f + 0.5f), (int)((b + m) * 255f + 0.5f), alpha);
		}

		public const float CloseSize = 24f;
		public const float CloseTop = 10f;
		public const float CloseRightMargin = 18f;

		// Applies the shared background, border colour and padding to a root panel.
		public static void Style(UIPanel panel, bool zeroPadding = true)
		{
			if (zeroPadding)
				panel.SetPadding(0f);
			panel.BackgroundColor = PanelBackground;
			panel.BorderColor = PanelBorder;
		}

		// A NieR: Automata style texture for the panel background, in the scheme's accent colour and very faint so it never
		// competes with the content: fine CRT scan lines, a grid of small "+" registration marks with dots between them, ruler ticks
		// along the top and bottom edges, and one slow scan band drifting down the panel. Drawn behind everything else.
		public static void DrawTexture(SpriteBatch sb, Rectangle r)
		{
			if (r.Width < 60 || r.Height < 60)
				return;

			Color a = Accent;
			int left = r.X + 3, right = r.Right - 3, top = r.Y + 3, bottom = r.Bottom - 3;

			// Fine horizontal scan lines.
			for (int y = top; y < bottom; y += 4)
				HubDraw.Fill(sb, new Rectangle(left, y, right - left, 1), a * 0.028f);

			// Registration marks and dots on a grid.
			const int step = 48;
			for (int y = top + 20; y < bottom - 8; y += step)
			{
				for (int x = left + 20; x < right - 8; x += step)
				{
					HubDraw.Fill(sb, new Rectangle(x - 2, y, 5, 1), a * 0.13f);
					HubDraw.Fill(sb, new Rectangle(x, y - 2, 1, 5), a * 0.13f);
					HubDraw.Fill(sb, new Rectangle(x + step / 2, y + step / 2, 1, 1), a * 0.17f);
				}
			}

			// Ruler ticks along the top and bottom edges, a longer one every fifth.
			for (int x = left + 8, i = 0; x < right - 8; x += 12, i++)
			{
				int len = i % 5 == 0 ? 6 : 3;
				HubDraw.Fill(sb, new Rectangle(x, top + 1, 1, len), a * 0.16f);
				HubDraw.Fill(sb, new Rectangle(x, bottom - 1 - len, 1, len), a * 0.16f);
			}

			// One slow scan band drifting down.
			int travel = r.Height + 80;
			int bandY = r.Y - 40 + (int)(Main.GameUpdateCount * 0.7f % travel);
			for (int i = 0; i < 6; i++)
			{
				int y0 = Math.Max(top, bandY + i * 4);
				int y1 = Math.Min(bottom, bandY + i * 4 + 4);
				if (y1 > y0)
					HubDraw.Fill(sb, new Rectangle(left, y0, right - left, y1 - y0), a * (0.012f * (6 - Math.Abs(i - 2.5f) * 1.6f)));
			}
		}

		// Inner hairline highlight + flush corner accent notches.
		public static void DrawFrame(SpriteBatch spriteBatch, Rectangle bRect)
		{
			DrawTexture(spriteBatch, bRect);

			Texture2D pixel = TextureAssets.MagicPixel.Value;

			Color innerHairline = Color.White * 0.05f;
			spriteBatch.Draw(pixel, new Rectangle(bRect.X + 1, bRect.Y + 1, bRect.Width - 2, 1), innerHairline);
			spriteBatch.Draw(pixel, new Rectangle(bRect.X + 1, bRect.Bottom - 2, bRect.Width - 2, 1), innerHairline);
			spriteBatch.Draw(pixel, new Rectangle(bRect.X + 1, bRect.Y + 1, 1, bRect.Height - 2), innerHairline);
			spriteBatch.Draw(pixel, new Rectangle(bRect.Right - 2, bRect.Y + 1, 1, bRect.Height - 2), innerHairline);

			DrawCorners(spriteBatch, new Rectangle(bRect.X + 1, bRect.Y + 1, bRect.Width - 2, bRect.Height - 2), Accent * 0.70f);
		}

		// Corner marks drawn just inside `r`, in the FRAME style chosen on the COSMETICS page (the same choice that shapes the
		// brackets on the title screen): thin notches, long heavy brackets, a bracket inside a bracket, or brackets with circuit nodes.
		// `size` scales them (1 for a big panel, smaller for a small card).
		public static void DrawCorners(SpriteBatch sb, Rectangle r, Color color, float size = 1f)
		{
			int minSide = Math.Min(r.Width, r.Height);
			int len = Math.Max(3, Math.Min((int)Math.Round(5 * size), minSide / 3));
			int thick = 2;

			// Small things (tab buttons, DPS cards, the Diagnostics tab) only get plain brackets: nodes and long arms would
			// land on their labels or merge into a bar.
			int style = minSide < 40 ? 0 : MenuLook.FrameIndex;
			switch (style)
			{
				case 1: // heavy
					HubDraw.Brackets(sb, r, color, Math.Min(len * 2 + 1, minSide / 3), thick + 1);
					break;
				case 2: // twin
				{
					HubDraw.Brackets(sb, r, color, len + 2, thick);
					Rectangle inner = new Rectangle(r.X + 5, r.Y + 5, r.Width - 10, r.Height - 10);
					if (inner.Width > 20 && inner.Height > 20)
						HubDraw.Brackets(sb, inner, color * 0.8f, Math.Max(3, len - 1), 1);
					break;
				}
				case 3: // circuit
				{
					HubDraw.Brackets(sb, r, color, len + 3, thick);
					int o = 6;
					foreach (Point p in new[] { new Point(r.X + o, r.Y + o), new Point(r.Right - o - 3, r.Y + o), new Point(r.X + o, r.Bottom - o - 3), new Point(r.Right - o - 3, r.Bottom - o - 3) })
						HubDraw.Fill(sb, new Rectangle(p.X, p.Y, 3, 3), color);
					if (r.Width > 120)
					{
						HubDraw.Fill(sb, new Rectangle(r.Center.X - 3, r.Y, 7, 2), color * 0.9f);
						HubDraw.Fill(sb, new Rectangle(r.Center.X - 3, r.Bottom - 2, 7, 2), color * 0.9f);
					}
					break;
				}
				default:
					HubDraw.Brackets(sb, r, color, len, thick);
					break;
			}
		}

		// Header divider with the centre node, inset 18px on both sides.
		public static void DrawHeaderDivider(SpriteBatch spriteBatch, Rectangle bRect, int yOffset)
		{
			Texture2D pixel = TextureAssets.MagicPixel.Value;
			int divY = bRect.Y + yOffset;
			spriteBatch.Draw(pixel, new Rectangle(bRect.X + 18, divY, bRect.Width - 36, 1), Divider);
			int midX = bRect.X + bRect.Width / 2;
			spriteBatch.Draw(pixel, new Rectangle(midX - 1, divY - 1, 3, 3), Accent * 0.85f);
			spriteBatch.Draw(pixel, new Rectangle(midX, divY, 1, 1), Color.White * 0.9f);
		}

		// Adds the standard close button to a panel's top-right corner.
		// panelPadding is the panel's own padding (0 for every panel except legacy ones).
		public static PanelCloseButton AddCloseButton(UIPanel panel, Action onClose, float panelPadding = 0f)
		{
			var close = new PanelCloseButton();
			close.Width.Set(CloseSize, 0f);
			close.Height.Set(CloseSize, 0f);
			close.Top.Set(CloseTop - panelPadding, 0f);
			close.Left.Set(-(CloseSize + CloseRightMargin) + panelPadding, 1f);
			close.Clicked += onClose;
			panel.Append(close);
			return close;
		}
	}

	// The panel title: a cream tab that hangs from the top edge of its panel, with the word in dark, bold, spaced capitals (the same cream
	// plate look as a Legendary card). Append it to the panel; it centres itself on the panel and ignores the mouse. `bottom` is how far
	// down from the panel's top edge the tab reaches.
	public class PanelTitleTab : UIElement
	{
		private readonly string word;
		private readonly int bottom;
		private readonly float scale;
		private readonly float tracking;

		public PanelTitleTab(string word, int bottom = 36, float scale = 0.92f, float tracking = 5f)
		{
			this.word = word.ToUpperInvariant();
			this.bottom = bottom;
			this.scale = scale;
			this.tracking = tracking;
			IgnoresMouseInteraction = true;
			Width.Set(0f, 1f);
			Height.Set(0f, 0f);
		}

		protected override void DrawSelf(SpriteBatch spriteBatch)
		{
			if (Parent == null)
				return;

			var font = Terraria.GameContent.FontAssets.MouseText.Value;
			Rectangle panel = Parent.GetDimensions().ToRectangle();

			float width = 0f;
			foreach (char ch in word)
				width += ModText.Measure(font, ch.ToString(), scale).X + tracking;
			width -= tracking;

			int plateW = (int)Math.Round(width) + 44;
			var plate = new Rectangle(panel.X + (panel.Width - plateW) / 2, panel.Y + 1, plateW, bottom - 1);
			HubDraw.Fill(spriteBatch, plate, RarityLook.Plate);

			// A thin dark line inside the plate, open at the top where the tab joins the panel edge.
			Color line = RarityLook.OnPlate * 0.45f;
			HubDraw.Fill(spriteBatch, new Rectangle(plate.X + 3, plate.Bottom - 4, plate.Width - 6, 1), line);
			HubDraw.Fill(spriteBatch, new Rectangle(plate.X + 3, plate.Y, 1, plate.Height - 3), line);
			HubDraw.Fill(spriteBatch, new Rectangle(plate.Right - 4, plate.Y, 1, plate.Height - 3), line);

			float lineH = font.MeasureString("A").Y * scale;
			float x = (float)Math.Round(plate.X + (plate.Width - width) * 0.5f);
			float y = (float)Math.Round(plate.Y + (plate.Height - 4 - lineH) * 0.5f) + 2f;

			// Dark ink, no drop shadow; drawn twice, one pixel apart, so the letters hold up on the light plate.
			Color ink = RarityLook.OnPlate;
			foreach (char ch in word)
			{
				string s = ch.ToString();
				RarityLook.DrawCoded(spriteBatch, font, s, new Vector2(x, y), ink, new Vector2(scale), false);
				RarityLook.DrawCoded(spriteBatch, font, s, new Vector2(x + 1f, y), ink, new Vector2(scale), false);
				x += ModText.Measure(font, s, scale).X + tracking;
			}
		}
	}
	// The one close button used by every panel and popup.
	public class PanelCloseButton : UIElement
	{
		public event Action Clicked;
		private bool isHovered;

		public override void LeftClick(UIMouseEvent evt)
		{
			base.LeftClick(evt);
			SoundEngine.PlaySound(SoundID.MenuClose);
			Clicked?.Invoke();
		}

		public override void MouseOver(UIMouseEvent evt)
		{
			base.MouseOver(evt);
			isHovered = true;
			SoundEngine.PlaySound(SoundID.MenuTick);
		}

		public override void MouseOut(UIMouseEvent evt)
		{
			base.MouseOut(evt);
			isHovered = false;
		}

		protected override void DrawSelf(SpriteBatch spriteBatch)
		{
			CalculatedStyle dims = GetDimensions();
			var rect = new Rectangle((int)dims.X, (int)dims.Y, (int)dims.Width, (int)dims.Height);
			Texture2D pixel = TextureAssets.MagicPixel.Value;

			Color bg = isHovered ? new Color(110, 28, 36) * 0.95f : new Color(60, 18, 24) * 0.90f;
			Color border = isHovered ? new Color(255, 100, 100) : new Color(140, 45, 55);

			if (isHovered)
				spriteBatch.Draw(pixel, new Rectangle(rect.X - 1, rect.Y - 1, rect.Width + 2, rect.Height + 2), border * 0.15f);

			spriteBatch.Draw(pixel, rect, bg);
			spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 1), border);
			spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), border);
			spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, 1, rect.Height), border);
			spriteBatch.Draw(pixel, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), border);

			// Pixel-drawn cross so it is exactly centred regardless of font metrics.
			Color cross = isHovered ? new Color(255, 235, 235) : new Color(240, 160, 160);
			int span = Math.Max(6, (Math.Min(rect.Width, rect.Height) / 2 - 2) & ~1);
			int steps = span - 1;
			int x0 = rect.X + (rect.Width - span) / 2;
			int y0 = rect.Y + (rect.Height - span) / 2;
			for (int i = 0; i < steps; i++)
			{
				spriteBatch.Draw(pixel, new Rectangle(x0 + i, y0 + i, 2, 2), cross);
				spriteBatch.Draw(pixel, new Rectangle(x0 + steps - 1 - i, y0 + i, 2, 2), cross);
			}
		}
	}

	// The title screen's glitch, for the in-game panels. Everything is drawn on top of a panel, in the scheme's own colours
	// (a bright band and a thin amber "colour-split" band beside it, like the title card's split), and never takes a click.
	//
	//   * Opening: the panel comes up from a dark flash with a scan line running down it and glitch bands tearing across it.
	//   * Idle: every ten seconds or so a short burst of thin bands tears across the panel and the corner marks jump sideways.
	//
	// Give each panel one instance and call Draw(...) once per frame after the panel has drawn. It notices a new opening
	// by itself (the panel was not drawn on the frames just before).
	public sealed class PanelGlitch
	{
		public const int OpenTicks = 24;
		private const int BurstTicks = 10;

		private readonly int salt;
		private uint lastDraw;
		private int age = int.MaxValue / 2;

		public PanelGlitch(int salt = 0)
		{
			this.salt = salt;
		}

		// How strong the opening effect still is, 1 right at the start and 0 when finished.
		public float OpenStrength => Math.Max(0f, 1f - age / (float)OpenTicks);

		public void Draw(SpriteBatch sb, Rectangle r, bool withFlash = true)
		{
			uint now = Main.GameUpdateCount;
			uint gap = now - lastDraw;
			if (gap > 3u)
				age = 0; // it was not on screen a moment ago, so it has just opened
			else
				age = Math.Min(age + (int)gap, OpenTicks + 1);
			lastDraw = now;

			float open = OpenStrength;
			float idle = IdleStrength(now, salt);
			if (open <= 0f && idle <= 0f)
				return;

			if (open > 0f)
			{
				if (withFlash)
					HubDraw.Fill(sb, r, Color.Black * (0.50f * open * open));

				// A scan line sweeping down the panel.
				int sweepY = r.Y + (int)(r.Height * (1f - open));
				Color accent = PanelChrome.Accent;
				HubDraw.Fill(sb, new Rectangle(r.X, sweepY - 1, r.Width, 2), accent * (0.55f * open));
				HubDraw.Fill(sb, new Rectangle(r.X, sweepY - 8, r.Width, 7), accent * (0.10f * open));

				Bands(sb, r, age / 2 + salt, 6, open);
				Ghost(sb, r, age / 2 + salt, open);
			}

			if (idle > 0f)
			{
				int seed = (int)(now / 2) + salt * 31;
				Bands(sb, r, seed, 3, idle * 0.7f);
				Ghost(sb, r, seed, idle);
			}
		}

		// 0 most of the time; 1 fading to 0 over a few ticks once in a long while (a different moment for each salt).
		public static float IdleStrength(uint now, int salt)
		{
			int cycle = 560 + (Math.Abs(salt) * 53) % 300;
			int phase = (int)((now + (uint)(Math.Abs(salt) * 211)) % (uint)cycle);
			return phase < BurstTicks ? 1f - phase / (float)BurstTicks : 0f;
		}

		// Horizontal bands of the panel shifted sideways, each with a thin colour-split twin.
		public static void Bands(SpriteBatch sb, Rectangle r, int seed, int count, float k)
		{
			if (r.Width < 20 || r.Height < 12 || k <= 0f)
				return;

			Color accent = PanelChrome.Accent;
			Color split = MenuLook.Current.Amber;
			for (int i = 0; i < count; i++)
			{
				int y = r.Y + Hash(seed, i) % Math.Max(1, r.Height - 8);
				int h = 2 + Hash(seed, i + 17) % 4;
				int shift = Hash(seed, i + 41) % 41 - 20;
				int x = r.X + Math.Max(0, shift);
				int w = r.Width - Math.Abs(shift);
				HubDraw.Fill(sb, new Rectangle(x, y, w, h), accent * (0.30f * k));
				HubDraw.Fill(sb, new Rectangle(Math.Min(r.Right - 4, x + 3), y + 1, Math.Max(0, w - 3), 1), split * (0.45f * k));
			}
		}

		// The corner marks jump sideways for a moment, so the frame itself looks like it is glitching.
		public static void Ghost(SpriteBatch sb, Rectangle r, int seed, float k)
		{
			int jump = (Hash(seed, 3) % 2 == 0 ? 1 : -1) * (2 + Hash(seed, 5) % 3);
			PanelChrome.DrawCorners(sb, new Rectangle(r.X + 1 + jump, r.Y + 1, r.Width - 2, r.Height - 2), MenuLook.Current.Amber * (0.55f * k));
		}

		private static readonly char[] DecodeGlyphs = { '0', '1', 'X', 'M', '_', '/', '-', '9', 'T', 'A' };

		// The title screen's "digital decode": a letter or two of the text swapped for a code glyph. Used while a button is
		// first hovered; call it with a seed that changes every couple of ticks.
		public static string Decode(string text, int seed, int slots = 2)
		{
			if (string.IsNullOrEmpty(text))
				return text;

			char[] chars = text.ToCharArray();
			for (int i = 0; i < slots; i++)
			{
				int at = Hash(seed, i + 70) % chars.Length;
				if (chars[at] != ' ')
					chars[at] = DecodeGlyphs[Hash(seed, i + 80) % DecodeGlyphs.Length];
			}
			return new string(chars);
		}

		private static int Hash(int a, int b)
		{
			unchecked
			{
				int h = a * 73856093 ^ b * 19349663;
				h ^= h >> 13;
				h *= 1274126177;
				h ^= h >> 16;
				return h & 0x7fffffff;
			}
		}
	}

	// Shared scrollbar: track flush with the panels it sits beside.
	public class PanelScrollbar : UIScrollbar
	{
		private bool dragging;
		private float dragOffset;

		public PanelScrollbar()
		{
			Width.Set(8f, 0f);
		}

		protected bool IsDragging => dragging;

		// The handle exactly as it is drawn (5px inset at the top and bottom of the track). Drawing, grabbing and dragging
		// all use this one rectangle, so they always agree.
		protected Rectangle GetHandle()
		{
			CalculatedStyle dims = GetDimensions();
			Rectangle inner = GetInnerDimensions().ToRectangle();
			inner.Y += 5;
			inner.Height -= 10;
			float max = Math.Max(MaxViewSize, 1f);
			int handleH = Math.Max(16, (int)(inner.Height * (ViewSize / max)));
			int handleY = inner.Y + (int)(inner.Height * (ViewPosition / max));
			handleY = Math.Min(handleY, inner.Bottom - handleH);
			return new Rectangle((int)dims.X + 1, handleY, (int)dims.Width - 2, handleH);
		}

		// Grabbing the handle starts a drag. A click on the empty track still jumps there (the base class does that).
		public override void LeftMouseDown(UIMouseEvent evt)
		{
			if (CanScroll && evt.Target == this)
			{
				Rectangle handle = GetHandle();
				if (handle.Contains(evt.MousePosition.ToPoint()))
				{
					dragging = true;
					dragOffset = evt.MousePosition.Y - handle.Y;
					return;
				}
			}

			base.LeftMouseDown(evt);
		}

		public override void LeftMouseUp(UIMouseEvent evt)
		{
			dragging = false;
			base.LeftMouseUp(evt);
		}

		// While the handle is held, the view follows the mouse. The base UIScrollbar does this inside its own DrawSelf,
		// which this class replaces without calling it, so dragging never moved anything until this was added.
		protected void UpdateDrag()
		{
			if (!dragging)
				return;

			if (!Main.mouseLeft)
			{
				dragging = false; // the button was released somewhere else
				return;
			}

			Rectangle inner = GetInnerDimensions().ToRectangle();
			inner.Y += 5;
			inner.Height -= 10;
			float max = Math.Max(MaxViewSize, 1f);
			float position = (UserInterface.ActiveInstance.MousePosition.Y - dragOffset - inner.Y) / Math.Max(1f, inner.Height) * max;
			ViewPosition = MathHelper.Clamp(position, 0f, Math.Max(0f, MaxViewSize - ViewSize));
		}

		protected override void DrawSelf(SpriteBatch spriteBatch)
		{
			UpdateDrag();

			if (!CanScroll)
				return;

			CalculatedStyle dims = GetDimensions();
			Rectangle trackRect = new Rectangle((int)dims.X, (int)dims.Y, (int)dims.Width, (int)dims.Height);
			Texture2D pixel = TextureAssets.MagicPixel.Value;
			bool hot = IsMouseHovering || dragging;

			// Track: flush with the grid/detail panels (same top and bottom edge)
			spriteBatch.Draw(pixel, trackRect, PanelChrome.Tone(new Color(10, 16, 32)) * 0.92f);
			Color edge = PanelChrome.Tone(new Color(30, 41, 59));
			spriteBatch.Draw(pixel, new Rectangle(trackRect.X, trackRect.Y, 1, trackRect.Height), edge);
			spriteBatch.Draw(pixel, new Rectangle(trackRect.Right - 1, trackRect.Y, 1, trackRect.Height), edge);
			spriteBatch.Draw(pixel, new Rectangle(trackRect.X, trackRect.Y, trackRect.Width, 1), edge);
			spriteBatch.Draw(pixel, new Rectangle(trackRect.X, trackRect.Bottom - 1, trackRect.Width, 1), edge);

			// Handle (the same rectangle used for grabbing and dragging)
			Rectangle handle = GetHandle();
			spriteBatch.Draw(pixel, handle, (hot ? PanelChrome.Tone(new Color(86, 130, 190)) : PanelChrome.Tone(new Color(52, 82, 130))));
			spriteBatch.Draw(pixel, new Rectangle(handle.X, handle.Y, handle.Width, 1), PanelChrome.Accent * 0.55f);
		}
	}
}
