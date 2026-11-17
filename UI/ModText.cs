using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.UI.Chat;

namespace Augments
{
	// Every piece of text this mod draws goes through here instead of calling the game's text functions directly.
	// With the game font picked (the default) each call simply passes straight on to the game's own function, so nothing changes.
	// With the blocky pixel font picked on the Cosmetics page the same text is drawn with PixelFont instead.
	// Only this mod's own text uses these calls, so the rest of the game (and every other mod) keeps its normal font.
	internal static class ModText
	{
		private static readonly Regex ColourTag = new Regex(@"\[c/([0-9A-Fa-f]{6}):([^\]]*)\]", RegexOptions.Compiled);

		public static bool Pixel => PixelFont.Active;

		// Sharp text on every screen: a scale that lands almost on a whole-number ratio of the font once the UI scale is applied is moved onto it
		// (shrink only, at most 1% bigger), and a position is moved onto a whole screen pixel. See HubDraw.FineScale / HubDraw.Snap.
		// Size, Measure and every draw call use the same adjusted scale, so a line is measured at the size it is drawn.
		private static Vector2 Sharp(Vector2 scale)
		{
			if (scale.X <= 0f)
				return scale;

			float f = HubDraw.FineScale(scale.X) / scale.X;
			return f == 1f ? scale : scale * f;
		}

		private static Vector2 SnapPos(Vector2 pos) => new Vector2(HubDraw.Snap(pos.X), HubDraw.Snap(pos.Y));

		// True when this text should be drawn with the pixel font: it is picked, and the text is not squeezed too small for it.
		public static bool UsePixel(DynamicSpriteFont font, float scale)
		{
			return Pixel && PixelFont.CanUse(LineHeight(font), scale);
		}

		private static DynamicSpriteFont lineFont;
		private static float lineHeightOfFont;

		// The height of one line of this font at scale 1 (the pixel font keeps the game's line height so layouts do not move).
		private static float LineHeight(DynamicSpriteFont font)
		{
			if (!ReferenceEquals(font, lineFont))
			{
				lineFont = font;
				lineHeightOfFont = font.MeasureString("A").Y;
			}
			return lineHeightOfFont;
		}

		private static string StripTags(string text)
		{
			return string.IsNullOrEmpty(text) ? "" : ColourTag.Replace(text, "$2");
		}

		// Replaces ChatManager.GetStringSize.
		public static Vector2 Size(DynamicSpriteFont font, string text, Vector2 baseScale, float maxWidth = -1f)
		{
			if (!UsePixel(font, baseScale.X))
				return ChatManager.GetStringSize(font, text, Sharp(baseScale), maxWidth);

			return PixelSize(font, text, baseScale);
		}

		private static Vector2 PixelSize(DynamicSpriteFont font, string text, Vector2 scale)
		{
			string plain = StripTags(text);
			string[] lines = plain.Split('\n');
			float lh = LineHeight(font);
			float width = 0f;
			foreach (string line in lines)
				width = Math.Max(width, PixelFont.Width(line, lh, scale.X, 0f, font));

			float height = lh * scale.Y + (lines.Length - 1) * font.LineSpacing * scale.Y;
			return new Vector2(width, height);
		}

		// Plain measure of one string drawn at one scale, for the places that used font.MeasureString(text) * scale.
		public static Vector2 Measure(DynamicSpriteFont font, string text, float scale)
		{
			if (!UsePixel(font, scale))
				return font.MeasureString(text) * Sharp(new Vector2(scale)).X;

			return PixelSize(font, text, new Vector2(scale));
		}

		// Replaces ChatManager.DrawColorCodedStringWithShadow. [c/RRGGBB:text] colour tags still work.
		public static Vector2 DrawCoded(SpriteBatch sb, DynamicSpriteFont font, string text, Vector2 position, Color baseColor, float rotation, Vector2 origin, Vector2 baseScale, float maxWidth = -1f, float spread = 2f)
		{
			if (!UsePixel(font, baseScale.X))
				return ChatManager.DrawColorCodedStringWithShadow(sb, font, text, rotation == 0f ? SnapPos(position) : position, baseColor, rotation, origin, Sharp(baseScale), maxWidth, spread);

			DrawPixel(sb, font, text, position - origin * baseScale, baseColor, baseScale, true);
			return PixelSize(font, text, baseScale);
		}

		// The same, with no drop shadow.
		public static void DrawPlain(SpriteBatch sb, DynamicSpriteFont font, string text, Vector2 position, Color color, Vector2 scale)
		{
			DrawPixel(sb, font, text, position, color, scale, false);
		}

		private static void DrawPixel(SpriteBatch sb, DynamicSpriteFont font, string text, Vector2 position, Color baseColor, Vector2 scale, bool shadow)
		{
			if (string.IsNullOrEmpty(text))
				return;

			float lh = LineHeight(font);
			Texture2D px = TextureAssets.MagicPixel.Value;
			float alpha = baseColor.A / 255f;
			string[] lines = text.Split('\n');

			for (int li = 0; li < lines.Length; li++)
			{
				Vector2 p = new Vector2(position.X, position.Y + li * font.LineSpacing * scale.Y);
				if (shadow)
					DrawLine(sb, px, font, lh, StripTags(lines[li]), p + new Vector2(1f, 1f), Color.Black * (0.8f * alpha), scale.X);

				int last = 0;
				float x = p.X;
				foreach (Match m in ColourTag.Matches(lines[li]))
				{
					if (m.Index > last)
						x = DrawPiece(sb, px, font, lh, lines[li].Substring(last, m.Index - last), x, p.Y, baseColor, scale.X);

					int v = Convert.ToInt32(m.Groups[1].Value, 16);
					Color tag = new Color((v >> 16) & 255, (v >> 8) & 255, v & 255) * alpha;
					x = DrawPiece(sb, px, font, lh, m.Groups[2].Value, x, p.Y, tag, scale.X);
					last = m.Index + m.Length;
				}

				if (last < lines[li].Length)
					DrawPiece(sb, px, font, lh, lines[li].Substring(last), x, p.Y, baseColor, scale.X);
			}
		}

		private static float DrawPiece(SpriteBatch sb, Texture2D px, DynamicSpriteFont font, float lh, string piece, float x, float y, Color color, float scale)
		{
			if (string.IsNullOrEmpty(piece))
				return x;

			PixelFont.Draw(sb, px, piece, new Vector2(x, y), color, lh, scale, 0f, font);
			// The next piece starts one letter gap after this one.
			return x + PixelFont.Width(piece, lh, scale, 0f, font) + 2f * PixelFont.Unit(lh, scale);
		}

		private static void DrawLine(SpriteBatch sb, Texture2D px, DynamicSpriteFont font, float lh, string line, Vector2 p, Color color, float scale)
		{
			if (line.Length > 0)
				PixelFont.Draw(sb, px, line, p, color, lh, scale, 0f, font);
		}

		// Replaces Utils.DrawBorderString (text with a black outline, anchored by a fraction of its own size).
		public static void DrawBorder(SpriteBatch sb, string text, Vector2 pos, Color color, float scale = 1f, float anchorX = 0f, float anchorY = 0f, int maxCharactersDisplayed = -1)
		{
			DynamicSpriteFont font = FontAssets.MouseText.Value;
			if (!UsePixel(font, scale))
			{
				Utils.DrawBorderString(sb, text, SnapPos(pos), color, Sharp(new Vector2(scale)).X, anchorX, anchorY, maxCharactersDisplayed);
				return;
			}

			Vector2 size = PixelSize(font, text, new Vector2(scale));
			pos -= size * new Vector2(anchorX, anchorY);
			Texture2D px = TextureAssets.MagicPixel.Value;
			float lh = LineHeight(font);
			string plain = StripTags(text);
			Color outline = Color.Black * (color.A / 255f);
			foreach (Vector2 o in new[] { new Vector2(-1f, 0f), new Vector2(1f, 0f), new Vector2(0f, -1f), new Vector2(0f, 1f) })
				PixelFont.Draw(sb, px, plain, pos + o, outline, lh, scale, 0f, font);
			PixelFont.Draw(sb, px, plain, pos, color, lh, scale, 0f, font);
		}

		// Replaces spriteBatch.DrawString(font, text, pos, color, rotation, origin, scale, effects, depth) for the mod's own text.
		public static void DrawString(SpriteBatch sb, DynamicSpriteFont font, string text, Vector2 pos, Color color, float rotation, Vector2 origin, float scale, SpriteEffects effects, float depth)
		{
			if (!UsePixel(font, scale))
			{
				sb.DrawString(font, text, rotation == 0f ? SnapPos(pos) : pos, color, rotation, origin, Sharp(new Vector2(scale)).X, effects, depth);
				return;
			}

			PixelFont.Draw(sb, TextureAssets.MagicPixel.Value, text, pos - origin * scale, color, LineHeight(font), scale, 0f, font);
		}

		public static void DrawString(SpriteBatch sb, DynamicSpriteFont font, string text, Vector2 pos, Color color)
		{
			DrawString(sb, font, text, pos, color, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);
		}
	}
}
