using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using Terraria;
using Terraria.GameContent;

namespace Augments
{
	public enum HubSkin
	{
		Chassis, // the mod's current dark-navy + cyan look
		YoRHa    // cream paper + charcoal, closer to the NieR: Automata menus
	}

	// Colours for the Automata hub. Every hub element reads these at draw time, so switching the skin
	// repaints the whole screen instantly.
	public static class HubTheme
	{
		public static HubSkin Skin = HubSkin.YoRHa;

		public static void CycleSkin()
		{
			Skin = Skin == HubSkin.Chassis ? HubSkin.YoRHa : HubSkin.Chassis;
		}

		public static string SkinName => Skin == HubSkin.Chassis ? "CHASSIS" : "YORHA";

		private static bool Light => Skin == HubSkin.YoRHa;

		// On the title screen the colours come from the scheme picked on the COSMETICS page (MenuLook). In a world (the /hub
		// window) they come from the SKIN button as before.
		private static bool OnTitle => Main.gameMenu;

		// Full-screen dim behind the hub.
		public static Color Backdrop => Light ? new Color(28, 26, 22) * 0.72f : new Color(3, 6, 12) * 0.78f;
		// Main window fill.
		public static Color Panel => OnTitle ? MenuLook.Current.Panel : Light ? new Color(205, 199, 180) : new Color(10, 16, 28);
		// Boxes sitting on the window.
		public static Color PanelAlt => OnTitle ? MenuLook.Current.PanelAlt : Light ? new Color(190, 184, 164) : new Color(15, 24, 40);
		public static Color Line => OnTitle ? MenuLook.Current.Line : Light ? new Color(92, 86, 74) : new Color(51, 65, 85);
		public static Color LineSoft => OnTitle ? MenuLook.Current.Line * 0.45f : Light ? new Color(92, 86, 74) * 0.45f : new Color(30, 41, 59);
		public static Color Text => OnTitle ? MenuLook.Current.Text : Light ? new Color(66, 61, 52) : new Color(226, 232, 240);
		public static Color Dim => OnTitle ? MenuLook.Current.Dim : Light ? new Color(116, 110, 96) : new Color(129, 146, 170);
		// Selection bars and key accents.
		public static Color Accent => OnTitle ? MenuLook.Current.Accent : Light ? new Color(72, 67, 58) : new Color(56, 189, 248);
		public static Color OnAccent => OnTitle ? MenuLook.Current.OnAccent : Light ? new Color(214, 208, 189) : new Color(7, 18, 30);
		public static Color Warn => OnTitle ? MenuLook.Current.Warn : Light ? new Color(172, 78, 48) : new Color(251, 146, 60);
		public static Color Danger => new Color(190, 60, 70);
	}

	// Small drawing helpers so the hub's elements stay short. Everything is flat rectangles, like the game's menus.
	internal static class HubDraw
	{
		public static Texture2D Pixel => TextureAssets.MagicPixel.Value;
		public static DynamicSpriteFont Font => FontAssets.MouseText.Value;

		// While true (set by pages that want the sharpest text), text is placed on whole screen pixels. The interface is drawn scaled by the
		// player's UI scale (1.47 for example), so a position that is whole in UI units usually lands between two pixels, and text drawn
		// between pixels is blurred by the filter. Off everywhere else so the other pages are not touched.
		public static bool SnapToPixels;

		private static float PixelScale
		{
			get
			{
				float m = Main.UIScaleMatrix.M11;
				return m > 0.1f ? m : 1f;
			}
		}

		// A UI position moved to the nearest whole screen pixel.
		public static float Snap(float v)
		{
			float m = PixelScale;
			return (float)Math.Round(v * m) / m;
		}

		// A text scale nudged (by at most 9%) so that scale x UI scale is exactly 1 or 2: the font is drawn at a whole-number
		// ratio of its real size, which keeps the letter shapes clean instead of stretched by a fraction. Used for text that is both measured
		// and drawn through the same value, so the layout stays consistent.
		private static readonly float[] CrispTargets = { 1f, 2f };

		public static float Crisp(float scale)
		{
			float u = PixelScale;
			if (Math.Abs(u - 1f) < 0.01f)
				return scale;

			foreach (float target in CrispTargets)
			{
				if (Math.Abs(scale * u - target) <= 0.09f * target)
					return target / u;
			}
			return scale;
		}

		// Smooth shapes: small white pictures with soft edges, tinted when drawn. The old triangles and diamonds were stacks of one-pixel rows,
		// which look stepped when the interface is scaled by a fraction. Each shape is made at exactly the size it is drawn on the screen (and
		// drawn one to one at a whole screen pixel), so its soft edge is never squeezed by the filter. Made on first use, one per size.
		private static readonly int[] DiamondSizes = { 12, 16, 24, 32, 48, 64, 96 };
		private static readonly Dictionary<(int kind, int w, int h), Texture2D> shapes = new Dictionary<(int, int, int), Texture2D>();

		private static bool ShapeInside(int kind, float px, float py)
		{
			switch (kind)
			{
				case 0: return Math.Abs(px - 0.5f) <= 0.5f * py;           // triangle pointing up
				case 1: return Math.Abs(px - 0.5f) <= 0.5f * (1f - py);    // pointing down
				case 2: return Math.Abs(py - 0.5f) <= 0.5f * (1f - px);    // pointing right
				default: return Math.Abs(px - 0.5f) + Math.Abs(py - 0.5f) <= 0.5f; // diamond
			}
		}

		private static Texture2D GetShape(int kind, int w, int h)
		{
			w = Math.Max(2, w);
			h = Math.Max(2, h);
			var key = (kind, w, h);
			if (shapes.TryGetValue(key, out Texture2D tex) && !tex.IsDisposed)
				return tex;

			const int ss = 4;
			var data = new Color[w * h];
			for (int y = 0; y < h; y++)
			{
				for (int x = 0; x < w; x++)
				{
					int hit = 0;
					for (int sy = 0; sy < ss; sy++)
						for (int sx = 0; sx < ss; sx++)
							if (ShapeInside(kind, (x + (sx + 0.5f) / ss) / w, (y + (sy + 0.5f) / ss) / h))
								hit++;
					float a = hit / (float)(ss * ss);
					data[y * w + x] = new Color(a, a, a, a); // the game blends with premultiplied colour
				}
			}
			tex = new Texture2D(Main.graphics.GraphicsDevice, w, h);
			tex.SetData(data);
			shapes[key] = tex;
			return tex;
		}

		public static void UnloadShapes()
		{
			var all = new List<Texture2D>(shapes.Values);
			shapes.Clear();
			Main.QueueMainThreadAction(() =>
			{
				foreach (Texture2D t in all)
					t?.Dispose();
			});
		}

		// One screen pixel, in UI units.
		public static float OnePx => 1f / PixelScale;

		// Draws a shape of `w` x `h` UI units centred on `center`: made at its exact pixel size and placed on the pixel grid.
		private static void DrawShape(SpriteBatch sb, int kind, Vector2 center, float w, float h, Color c)
		{
			float m = PixelScale;
			int pw = Math.Max(2, (int)Math.Round(w * m));
			int ph = Math.Max(2, (int)Math.Round(h * m));
			Texture2D tex = GetShape(kind, pw, ph);
			var pos = new Vector2((float)Math.Round(center.X * m - pw / 2f) / m, (float)Math.Round(center.Y * m - ph / 2f) / m);
			sb.Draw(tex, pos, null, c, 0f, Vector2.Zero, 1f / m, SpriteEffects.None, 0f);
		}

		// A triangle `w` wide and `h` tall, pointing up or down, centred on `center`.
		public static void Triangle(SpriteBatch sb, Vector2 center, float w, float h, bool up, Color c)
		{
			DrawShape(sb, up ? 0 : 1, center, w, h, c);
		}

		// A triangle pointing to the right: `h` tall, `length` from its flat edge to its tip.
		public static void TriangleRight(SpriteBatch sb, Vector2 center, float length, float h, Color c)
		{
			DrawShape(sb, 2, center, length, h, c);
		}

		// A smooth diamond, the same size as Diamond() with this `size`.
		public static void SmoothDiamond(SpriteBatch sb, Vector2 center, float size, Color c)
		{
			// The glow dot breathes, so its size changes every frame. Pick one of a few ready sizes (never more than half again off) instead
			// of making a new picture each frame, and draw that one scaled.
			float m = PixelScale;
			float d = size * 1.4f + 1f;
			float want = d * m;
			int bucket = 12;
			foreach (int b in DiamondSizes)
			{
				bucket = b;
				if (b >= want * 0.9f)
					break;
			}
			Texture2D tex = GetShape(3, bucket, bucket);
			sb.Draw(tex, center, null, c, 0f, new Vector2(bucket / 2f, bucket / 2f), want / bucket, SpriteEffects.None, 0f);
		}

		public static void Fill(SpriteBatch sb, Rectangle r, Color c)
		{
			if (r.Width > 0 && r.Height > 0)
				sb.Draw(Pixel, r, c);
		}

		public static void Border(SpriteBatch sb, Rectangle r, Color c, int t = 1)
		{
			Fill(sb, new Rectangle(r.X, r.Y, r.Width, t), c);
			Fill(sb, new Rectangle(r.X, r.Bottom - t, r.Width, t), c);
			Fill(sb, new Rectangle(r.X, r.Y, t, r.Height), c);
			Fill(sb, new Rectangle(r.Right - t, r.Y, t, r.Height), c);
		}

		// Heavy L-shaped brackets on the four corners of a rectangle.
		public static void Brackets(SpriteBatch sb, Rectangle r, Color c, int len = 14, int t = 2)
		{
			Fill(sb, new Rectangle(r.X, r.Y, len, t), c);
			Fill(sb, new Rectangle(r.X, r.Y, t, len), c);
			Fill(sb, new Rectangle(r.Right - len, r.Y, len, t), c);
			Fill(sb, new Rectangle(r.Right - t, r.Y, t, len), c);
			Fill(sb, new Rectangle(r.X, r.Bottom - t, len, t), c);
			Fill(sb, new Rectangle(r.X, r.Bottom - len, t, len), c);
			Fill(sb, new Rectangle(r.Right - len, r.Bottom - t, len, t), c);
			Fill(sb, new Rectangle(r.Right - t, r.Bottom - len, t, len), c);
		}

		// Brackets in the FRAME style picked on the COSMETICS page: thin (the default), long and heavy, a bracket inside a bracket,
		// or brackets with circuit nodes. The title screen's corner marks all go through this, so the choice shapes every corner.
		// (MenuLook.DrawFrame and the frame previews draw the raw Brackets themselves, so they do not apply the style twice.)
		public static void Corners(SpriteBatch sb, Rectangle r, Color c, int len = 14, int t = 2)
		{
			switch (MenuLook.FrameIndex)
			{
				case 1:
					Brackets(sb, r, c, (int)(len * 1.5f), t + 1);
					break;
				case 2:
					Brackets(sb, new Rectangle(r.X - 3, r.Y - 3, r.Width + 6, r.Height + 6), c, len + 2, t);
					Brackets(sb, r, c * 0.85f, Math.Max(4, len / 2), 1);
					break;
				case 3:
				{
					Brackets(sb, r, c, len, t);
					int n = len >= 12 ? 5 : 3;
					foreach (Point p in new[] { new Point(r.X, r.Y), new Point(r.Right, r.Y), new Point(r.X, r.Bottom), new Point(r.Right, r.Bottom) })
						Fill(sb, new Rectangle(p.X - n / 2, p.Y - n / 2, n, n), c);
					break;
				}
				default:
					Brackets(sb, r, c, len, t);
					break;
			}
		}

		public static void HLine(SpriteBatch sb, int x, int y, int w, Color c)
		{
			Fill(sb, new Rectangle(x, y, w, 1), c);
		}

		public static void VLine(SpriteBatch sb, int x, int y, int h, Color c)
		{
			Fill(sb, new Rectangle(x, y, 1, h), c);
		}

		// Dotted rule, used between a stat label and its value.
		public static void DottedH(SpriteBatch sb, int x, int y, int w, Color c, int step = 4)
		{
			for (int i = 0; i < w; i += step)
				Fill(sb, new Rectangle(x + i, y, 1, 1), c);
		}

		// Horizontal stripes, used for locked or empty areas.
		public static void Stripes(SpriteBatch sb, Rectangle r, Color c, int step = 6)
		{
			for (int y = r.Y + 2; y < r.Bottom; y += step)
				Fill(sb, new Rectangle(r.X + 1, y, Math.Max(0, r.Width - 2), 1), c);
		}

		// Diamond built from one-pixel rows. (MagicPixel is NOT a 1x1 texture, so scaling it by a number
		// makes huge streaks; only ever draw it into a destination rectangle.) `size` is the side of the
		// equivalent square, so the diamond is about 1.4x that tall.
		public static void Diamond(SpriteBatch sb, Vector2 center, float size, Color c)
		{
			int half = Math.Max(1, (int)Math.Round(size * 0.7f));
			int cx = (int)Math.Round(center.X);
			int cy = (int)Math.Round(center.Y);
			for (int i = -half; i <= half; i++)
			{
				int w = half - Math.Abs(i);
				Fill(sb, new Rectangle(cx - w, cy + i, w * 2 + 1, 1), c);
			}
		}

		// Where the middle of the capital letters is, measured down from the top of a line of text at this scale. The game font keeps its letters
		// in the upper part of the line box (the lower part is room for descenders), so this is about 36% of the line height, not 50%.
		// Anything that has to sit level with text (icons, arrows, tag boxes, strike-through lines) is centred on this.
		public static float CapMid(float scale) => LineHeight(scale) * 0.36f;

		// The colour of the "new" glow: a warm orange that shows on both the cream pages and the dark bars.
		public static readonly Color Unread = new Color(238, 112, 48);

		// A glowing dot for "new, not read yet": a solid core with two soft halos that slowly breathe.
		public static void GlowDot(SpriteBatch sb, Vector2 center, float size = 3f)
		{
			float pulse = 0.5f + 0.5f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 4f);
			SmoothDiamond(sb, center, size * (2.6f + 0.7f * pulse), Unread * (0.10f + 0.12f * pulse));
			SmoothDiamond(sb, center, size * 1.7f, Unread * (0.28f + 0.2f * pulse));
			SmoothDiamond(sb, center, size, Unread);
		}

		// Right-pointing menu cursor made of stacked rows.
		public static void CursorRight(SpriteBatch sb, int x, int centerY, int height, Color c)
		{
			int half = height / 2;
			for (int i = -half; i <= half; i++)
			{
				int w = Math.Max(1, half - Math.Abs(i) + 1);
				Fill(sb, new Rectangle(x, centerY + i, w, 1), c);
			}
		}

		// Measuring text is slow, and letter-spaced text needs every letter measured. The pages draw a great deal of it every frame,
		// so each letter is measured once and remembered (with its one-letter string, to avoid making a new string per letter).
		private static DynamicSpriteFont cachedFont;
		private static readonly Dictionary<char, float> charWidths = new Dictionary<char, float>();
		private static readonly Dictionary<char, string> charStrings = new Dictionary<char, string>();
		private static float cachedLineHeight;

		private static void EnsureFontCache()
		{
			DynamicSpriteFont f = Font;
			if (ReferenceEquals(f, cachedFont))
				return;

			cachedFont = f;
			charWidths.Clear();
			charStrings.Clear();
			cachedLineHeight = f.MeasureString("A").Y;
		}

		private static float CharWidth(char ch)
		{
			if (!charWidths.TryGetValue(ch, out float w))
			{
				string s = ch.ToString();
				w = Font.MeasureString(s).X;
				charWidths[ch] = w;
				charStrings[ch] = s;
			}
			return w;
		}

		private static string CharString(char ch)
		{
			if (!charStrings.TryGetValue(ch, out string s))
			{
				CharWidth(ch);
				s = charStrings[ch];
			}
			return s;
		}

		public static float Width(string text, float scale, float tracking = 0f)
		{
			if (string.IsNullOrEmpty(text))
				return 0f;
			if (PixelFont.Active)
			{
				EnsureFontCache();
				if (PixelFont.CanUse(cachedLineHeight, scale))
					return PixelFont.Width(text, cachedLineHeight, scale, tracking, Font);
			}

			if (tracking <= 0f)
				return Font.MeasureString(text).X * scale;

			EnsureFontCache();
			float w = 0f;
			foreach (char ch in text)
				w += CharWidth(ch) * scale + tracking;
			return w - tracking;
		}

		// --- Big text ---
		// The normal font is small, so blowing it up to 2 or 3 times its size makes it blurry. Large numbers and titles use the game's
		// large display font instead, which stays sharp at that size. Sizes are given as a line height in UI units.
		private sealed class GlyphCache
		{
			public DynamicSpriteFont Font;
			public readonly Dictionary<char, float> Widths = new Dictionary<char, float>();
			public readonly Dictionary<char, string> Strings = new Dictionary<char, string>();
			public float Line;

			public void Ensure(DynamicSpriteFont f)
			{
				if (ReferenceEquals(f, Font))
					return;
				Font = f;
				Widths.Clear();
				Strings.Clear();
				Line = f.MeasureString("A").Y;
			}

			public float Width(char ch)
			{
				if (!Widths.TryGetValue(ch, out float w))
				{
					string s = ch.ToString();
					w = Font.MeasureString(s).X;
					Widths[ch] = w;
					Strings[ch] = s;
				}
				return w;
			}

			public string Str(char ch)
			{
				if (!Strings.TryGetValue(ch, out string s))
				{
					Width(ch);
					s = Strings[ch];
				}
				return s;
			}
		}

		private static readonly GlyphCache bigCache = new GlyphCache();

		// The scale that gives the big font a line height of `lineHeight` units.
		public static float BigScale(float lineHeight)
		{
			bigCache.Ensure(FontAssets.DeathText.Value);
			return lineHeight / bigCache.Line;
		}

		public static float BigWidth(string text, float scale, float tracking = 0f)
		{
			if (string.IsNullOrEmpty(text))
				return 0f;

			bigCache.Ensure(FontAssets.DeathText.Value);
			if (PixelFont.Active)
				return PixelFont.Width(text, bigCache.Line, scale, tracking, bigCache.Font, true);

			float w = 0f;
			foreach (char ch in text)
				w += bigCache.Width(ch) * scale + tracking;
			return w - tracking;
		}

		public static void BigText(SpriteBatch sb, string text, Vector2 pos, Color c, float scale, float tracking = 0f)
		{
			if (string.IsNullOrEmpty(text))
				return;

			bigCache.Ensure(FontAssets.DeathText.Value);
			if (PixelFont.Active)
			{
				PixelFont.Draw(sb, Pixel, text, pos, c, bigCache.Line, scale, tracking, bigCache.Font, true);
				return;
			}

			float x = SnapToPixels ? Snap(pos.X) : (float)Math.Round(pos.X);
			float y = SnapToPixels ? Snap(pos.Y) : (float)Math.Round(pos.Y);
			foreach (char ch in text)
			{
				sb.DrawString(bigCache.Font, bigCache.Str(ch), new Vector2(SnapToPixels ? Snap(x) : (float)Math.Round(x), y), c, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
				x += bigCache.Width(ch) * scale + tracking;
			}
		}

		public static void BigTextRight(SpriteBatch sb, string text, int rightX, float y, Color c, float scale, float tracking = 0f)
		{
			BigText(sb, text, new Vector2(rightX - BigWidth(text, scale, tracking), y), c, scale, tracking);
		}

		public static float LineHeight(float scale)
		{
			EnsureFontCache();
			return cachedLineHeight * scale;
		}

		// Flat text (no outline). With tracking > 0 each letter is spaced out, the way the game's menu titles are.
		public static void Text(SpriteBatch sb, string text, Vector2 pos, Color c, float scale = 0.9f, float tracking = 0f)
		{
			if (string.IsNullOrEmpty(text))
				return;

			pos = SnapToPixels ? new Vector2(Snap(pos.X), Snap(pos.Y)) : new Vector2((float)Math.Round(pos.X), (float)Math.Round(pos.Y));
			if (PixelFont.Active)
			{
				EnsureFontCache();
				if (PixelFont.CanUse(cachedLineHeight, scale))
				{
					PixelFont.Draw(sb, Pixel, text, pos, c, cachedLineHeight, scale, tracking, Font);
					return;
				}
			}

			if (tracking <= 0f)
			{
				sb.DrawString(Font, text, pos, c, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
				return;
			}

			EnsureFontCache();
			float x = pos.X;
			foreach (char ch in text)
			{
				sb.DrawString(Font, CharString(ch), new Vector2(SnapToPixels ? Snap(x) : (float)Math.Round(x), pos.Y), c, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
				x += CharWidth(ch) * scale + tracking;
			}
		}

		// Draws a line of text, shrinking it if it would be wider than maxWidth, so long footers never run off a panel.
		public static void TextFit(SpriteBatch sb, string text, Vector2 pos, Color c, float scale, float tracking, float maxWidth)
		{
			if (PixelFont.Active)
			{
				// The pixel font grows in whole steps, so shrink by trimming the letter spacing first and then the size.
				for (int i = 0; i < 16 && Width(text, scale, tracking) > maxWidth; i++)
				{
					if (tracking > 0f)
						tracking = Math.Max(0f, tracking - 1f);
					else
						scale *= 0.9f;
				}
				Text(sb, text, pos, c, scale, tracking);
				return;
			}

			float w = Width(text, scale, tracking);
			if (w > maxWidth && w > 0f)
				scale *= maxWidth / w;
			Text(sb, text, pos, c, scale, tracking);
		}

		// Like TextFit, but a line that is too wide is split onto a second line at a space instead of being shrunk until it is hard to read.
		public static void TextWrapFit(SpriteBatch sb, string text, Vector2 pos, Color c, float scale, float tracking, float maxWidth, float lineStep)
		{
			if (Width(text, scale, tracking) <= maxWidth)
			{
				TextFit(sb, text, pos, c, scale, tracking, maxWidth);
				return;
			}

			// Split at the space that leaves the first line as long as possible while still fitting.
			int best = -1;
			for (int i = 1; i < text.Length - 1; i++)
			{
				if (text[i] == ' ' && Width(text.Substring(0, i), scale, tracking) <= maxWidth)
					best = i;
			}

			if (best < 0)
			{
				TextFit(sb, text, pos, c, scale, tracking, maxWidth);
				return;
			}

			TextFit(sb, text.Substring(0, best), pos, c, scale, tracking, maxWidth);
			TextFit(sb, text.Substring(best + 1), new Vector2(pos.X, pos.Y + lineStep), c, scale, tracking, maxWidth);
		}
		public static void TextRight(SpriteBatch sb, string text, int rightX, float y, Color c, float scale = 0.9f, float tracking = 0f)
		{
			Text(sb, text, new Vector2(rightX - Width(text, scale, tracking), y), c, scale, tracking);
		}

		// Greedy word wrap.
		public static List<string> Wrap(string text, float maxWidth, float scale)
		{
			var lines = new List<string>();
			var current = new StringBuilder();
			foreach (string word in text.Split(' '))
			{
				string trial = current.Length == 0 ? word : current + " " + word;
				if (current.Length > 0 && Width(trial, scale) > maxWidth)
				{
					lines.Add(current.ToString());
					current.Clear();
					current.Append(word);
				}
				else
				{
					current.Clear();
					current.Append(trial);
				}
			}

			if (current.Length > 0)
				lines.Add(current.ToString());
			return lines;
		}
	}
}
