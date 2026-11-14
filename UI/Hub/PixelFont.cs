using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;

namespace Augments
{
	// A small blocky pixel font drawn from rectangles, used for this mod's own text when the "Blocky pixels" font is picked on the
	// Cosmetics page. It touches nothing outside the mod: HubDraw asks it for the text it draws, and nothing else does.
	//
	// Every letter is a 5 x 7 grid of "pixels". The font is capitals, numbers and common symbols; a lowercase letter is drawn as its
	// capital, and a character it does not have (a rare symbol) is drawn with the game's own font so nothing goes missing.
	internal static class PixelFont
	{
		private sealed class Glyph
		{
			public int Width;               // columns from the first lit column to the last
			public List<(int row, int start, int length)> Runs = new List<(int, int, int)>();
		}

		private static readonly Dictionary<char, Glyph> glyphs = new Dictionary<char, Glyph>();

		// While a Cosmetics card previews a font, this forces it on regardless of what is equipped.
		public static int? Sample;

		// The blocky pixel font was taken out as a Cosmetics choice, so this is always off and every call passes straight to the game font.
		public static bool Active => false;

		private static void Def(char ch, params string[] rows)
		{
			int min = int.MaxValue, max = -1;
			foreach (string row in rows)
			{
				for (int i = 0; i < row.Length; i++)
				{
					if (row[i] == '#')
					{
						min = Math.Min(min, i);
						max = Math.Max(max, i);
					}
				}
			}

			var g = new Glyph { Width = max < 0 ? 3 : max - min + 1 };
			for (int r = 0; r < rows.Length; r++)
			{
				int i = 0;
				while (i < rows[r].Length)
				{
					if (rows[r][i] != '#')
					{
						i++;
						continue;
					}
					int start = i;
					while (i < rows[r].Length && rows[r][i] == '#')
						i++;
					g.Runs.Add((r, start - min, i - start));
				}
			}
			glyphs[ch] = g;
		}

		static PixelFont()
		{
			Def('A', ".###.", "#...#", "#...#", "#####", "#...#", "#...#", "#...#");
			Def('B', "####.", "#...#", "#...#", "####.", "#...#", "#...#", "####.");
			Def('C', ".###.", "#...#", "#....", "#....", "#....", "#...#", ".###.");
			Def('D', "####.", "#...#", "#...#", "#...#", "#...#", "#...#", "####.");
			Def('E', "#####", "#....", "#....", "####.", "#....", "#....", "#####");
			Def('F', "#####", "#....", "#....", "####.", "#....", "#....", "#....");
			Def('G', ".###.", "#...#", "#....", "#.###", "#...#", "#...#", ".###.");
			Def('H', "#...#", "#...#", "#...#", "#####", "#...#", "#...#", "#...#");
			Def('I', "###", ".#.", ".#.", ".#.", ".#.", ".#.", "###");
			Def('J', "....#", "....#", "....#", "....#", "....#", "#...#", ".###.");
			Def('K', "#...#", "#..#.", "#.#..", "##...", "#.#..", "#..#.", "#...#");
			Def('L', "#....", "#....", "#....", "#....", "#....", "#....", "#####");
			Def('M', "#...#", "##.##", "#.#.#", "#.#.#", "#...#", "#...#", "#...#");
			Def('N', "#...#", "##..#", "#.#.#", "#..##", "#...#", "#...#", "#...#");
			Def('O', ".###.", "#...#", "#...#", "#...#", "#...#", "#...#", ".###.");
			Def('P', "####.", "#...#", "#...#", "####.", "#....", "#....", "#....");
			Def('Q', ".###.", "#...#", "#...#", "#...#", "#.#.#", "#..#.", ".##.#");
			Def('R', "####.", "#...#", "#...#", "####.", "#.#..", "#..#.", "#...#");
			Def('S', ".####", "#....", "#....", ".###.", "....#", "....#", "####.");
			Def('T', "#####", "..#..", "..#..", "..#..", "..#..", "..#..", "..#..");
			Def('U', "#...#", "#...#", "#...#", "#...#", "#...#", "#...#", ".###.");
			Def('V', "#...#", "#...#", "#...#", "#...#", "#...#", ".#.#.", "..#..");
			Def('W', "#...#", "#...#", "#...#", "#.#.#", "#.#.#", "##.##", "#...#");
			Def('X', "#...#", "#...#", ".#.#.", "..#..", ".#.#.", "#...#", "#...#");
			Def('Y', "#...#", "#...#", ".#.#.", "..#..", "..#..", "..#..", "..#..");
			Def('Z', "#####", "....#", "...#.", "..#..", ".#...", "#....", "#####");

			Def('0', ".###.", "#...#", "#..##", "#.#.#", "##..#", "#...#", ".###.");
			Def('1', "..#..", ".##..", "..#..", "..#..", "..#..", "..#..", ".###.");
			Def('2', ".###.", "#...#", "....#", "...#.", "..#..", ".#...", "#####");
			Def('3', "####.", "....#", "....#", ".###.", "....#", "....#", "####.");
			Def('4', "...#.", "..##.", ".#.#.", "#..#.", "#####", "...#.", "...#.");
			Def('5', "#####", "#....", "####.", "....#", "....#", "#...#", ".###.");
			Def('6', ".###.", "#....", "#....", "####.", "#...#", "#...#", ".###.");
			Def('7', "#####", "....#", "...#.", "..#..", ".#...", ".#...", ".#...");
			Def('8', ".###.", "#...#", "#...#", ".###.", "#...#", "#...#", ".###.");
			Def('9', ".###.", "#...#", "#...#", ".####", "....#", "....#", ".###.");

			Def('.', ".", ".", ".", ".", ".", ".", "#");
			Def(',', "..", "..", "..", "..", "..", ".#", "#.");
			Def(':', ".", ".", "#", ".", ".", "#", ".");
			Def(';', ".", ".", "#", ".", ".", "#", "#");
			Def('!', "#", "#", "#", "#", "#", ".", "#");
			Def('?', ".###.", "#...#", "....#", "...#.", "..#..", ".....", "..#..");
			Def('-', "...", "...", "...", "###", "...", "...", "...");
			Def('+', "...", ".#.", ".#.", "###", ".#.", ".#.", "...");
			Def('=', "...", "...", "###", "...", "###", "...", "...");
			Def('_', ".....", ".....", ".....", ".....", ".....", ".....", "#####");
			Def('/', "....#", "....#", "...#.", "..#..", ".#...", "#....", "#....");
			Def('\\', "#....", "#....", ".#...", "..#..", "...#.", "....#", "....#");
			Def('%', "##..#", "##..#", "...#.", "..#..", ".#...", "#..##", "#..##");
			Def('\'', "#", "#", ".", ".", ".", ".", ".");
			Def('"', "#.#", "#.#", "...", "...", "...", "...", "...");
			Def('(', "..#", ".#.", "#..", "#..", "#..", ".#.", "..#");
			Def(')', "#..", ".#.", "..#", "..#", "..#", ".#.", "#..");
			Def('[', "##", "#.", "#.", "#.", "#.", "#.", "##");
			Def(']', "##", ".#", ".#", ".#", ".#", ".#", "##");
			Def('<', "...", "..#", ".#.", "#..", ".#.", "..#", "...");
			Def('>', "...", "#..", ".#.", "..#", ".#.", "#..", "...");
			Def('|', "#", "#", "#", "#", "#", "#", "#");
			Def('*', ".....", "#.#.#", ".###.", "#####", ".###.", "#.#.#", ".....");
			Def('#', ".#.#.", "#####", ".#.#.", ".#.#.", "#####", ".#.#.", ".....");
			Def(' ', "   ");
		}

		private static Glyph Find(char ch)
		{
			if (glyphs.TryGetValue(char.ToUpperInvariant(ch), out Glyph g))
				return g;
			return null;
		}

		// The size of one "pixel" (one square of a letter) for text of this scale. `lineHeight` is the height of one line of the font the
		// scale is measured in. It grows smoothly with the scale, so every caller that measures text at one size and scales it
		// (most of the mod's layout code does) gets the right answer.
		public static float Unit(float lineHeight, float scale, bool big = false)
		{
			// Never below 0.9, so the smallest text still gets whole one-pixel strokes.
			return Math.Max(0.9f, RawUnit(lineHeight, scale, big));
		}

		private static float RawUnit(float lineHeight, float scale, bool big)
		{
			return lineHeight * scale * (big ? 0.36f : 0.40f) / 7f;
		}

		// The pixel letters stay readable down to a squeezed size; text that was shrunk below that (to fit a narrow space) is drawn
		// with the game font instead of turning into a smudge.
		public static bool CanUse(float lineHeight, float scale, bool big = false)
		{
			return big || RawUnit(lineHeight, scale, false) >= 0.7f;
		}
		// Kept for callers that want a whole number of pixels.
		public static int Cell(float lineHeight, float scale, bool big = false)
		{
			return Math.Max(1, (int)Math.Round(Unit(lineHeight, scale, big)));
		}

		// Each letter takes its own width plus two squares of space. The space after the last letter is not counted in a width.
		private const float LetterGap = 2f;

		public static float Width(string text, float lineHeight, float scale, float tracking, DynamicSpriteFont fallback, bool big = false)
		{
			if (string.IsNullOrEmpty(text))
				return 0f;

			float u = Unit(lineHeight, scale, big);
			float w = 0f;
			foreach (char ch in text)
				w += Advance(ch, u, scale, fallback) + tracking;
			return w - LetterGap * u - tracking;
		}

		private static float Advance(char ch, float u, float scale, DynamicSpriteFont fallback)
		{
			Glyph g = Find(ch);
			if (g != null)
				return (g.Width + LetterGap) * u;
			return fallback.MeasureString(ch.ToString()).X * scale + LetterGap * u;
		}

		public static void Draw(SpriteBatch sb, Texture2D pixel, string text, Vector2 pos, Color color, float lineHeight, float scale, float tracking, DynamicSpriteFont fallback, bool big = false)
		{
			float u = Unit(lineHeight, scale, big);
			float boxHeight = lineHeight * scale;
			// Where the game font puts its capital letters: a little above the middle of the line box.
			float top = pos.Y + boxHeight * 0.35f - 7f * u / 2f;
			// Thin strokes are hard to read, so below two pixels per square every stroke is made one pixel wider.
			int bold = u < 1.9f ? 1 : 0;
			float x = pos.X;

			foreach (char ch in text)
			{
				Glyph g = Find(ch);
				if (g == null)
				{
					sb.DrawString(fallback, ch.ToString(), new Vector2((float)Math.Round(x), pos.Y), color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
					x += Advance(ch, u, scale, fallback) + tracking;
					continue;
				}

				foreach ((int row, int start, int length) in g.Runs)
				{
					int left = (int)Math.Round(x + start * u);
					int right = (int)Math.Round(x + (start + length) * u) + bold;
					int y0 = (int)Math.Round(top + row * u);
					int y1 = (int)Math.Round(top + (row + 1) * u);
					if (right <= left)
						right = left + 1;
					if (y1 <= y0)
						y1 = y0 + 1;
					sb.Draw(pixel, new Rectangle(left, y0, right - left, y1 - y0), color);
				}
				x += (g.Width + LetterGap) * u + tracking;
			}
		}
	}
}
