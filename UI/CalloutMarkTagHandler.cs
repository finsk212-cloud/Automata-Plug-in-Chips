using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.UI.Chat;

namespace Augments
{
	// Inline chat tag "[cmark:x]" that draws the Callout marker (the gold diamond shown above marked enemies)
	// inside description text. Registered in Augments.Load. The text after the colon is ignored.
	public class CalloutMarkTagHandler : ITagHandler
	{
		public const string TagName = "cmark";

		public TextSnippet Parse(string text, Color baseColor = default, string options = null)
		{
			return new MarkSnippet(text);
		}

		// Pixel-exact diamond: dark edge, light rim, gold fill. Shared by the description icon and the marker over
		// enemies so both look the same. Drawn as 1px rows, so there is no rotation blur.
		public static void DrawDiamond(SpriteBatch spriteBatch, Vector2 center, int radius, float opacity)
		{
			Texture2D pixel = TextureAssets.MagicPixel.Value;
			int cx = (int)System.Math.Round(center.X);
			int cy = (int)System.Math.Round(center.Y);

			FillDiamond(spriteBatch, pixel, cx, cy, radius + 1, new Color(12, 14, 20) * (0.75f * opacity));
			FillDiamond(spriteBatch, pixel, cx, cy, radius, new Color(255, 238, 170) * opacity);
			FillDiamond(spriteBatch, pixel, cx, cy, System.Math.Max(1, radius - 2), new Color(255, 190, 40) * opacity);
		}

		private static void FillDiamond(SpriteBatch spriteBatch, Texture2D pixel, int cx, int cy, int radius, Color color)
		{
			for (int dy = -radius; dy <= radius; dy++)
			{
				int half = radius - System.Math.Abs(dy);
				spriteBatch.Draw(pixel, new Rectangle(cx - half, cy + dy, half * 2 + 1, 1), color);
			}
		}

		private class MarkSnippet : TextSnippet
		{
			public MarkSnippet(string text) : base(text) { }

			public override bool UniqueDraw(bool justCheckingString, out Vector2 size, SpriteBatch spriteBatch, Vector2 position = default, Color color = default, float scale = 1f)
			{
				int radius = System.Math.Max(4, (int)System.Math.Round(7f * scale));
				size = new Vector2(radius * 2 + 5f, 14f * scale);

				// Color.Black is the shadow pass; the icon has its own edge, so skip it.
				if (justCheckingString || color == Color.Black)
					return true;

				// Sits on the text's x-height line (about the middle of the lowercase letters).
				Vector2 center = position + new Vector2(radius + 1f, 8f * scale);
				DrawDiamond(spriteBatch, center, radius, 1f);
				return true;
			}
		}
	}
}
