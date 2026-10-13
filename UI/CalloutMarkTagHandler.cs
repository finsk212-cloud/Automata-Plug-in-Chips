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

		private class MarkSnippet : TextSnippet
		{
			public MarkSnippet(string text) : base(text) { }

			public override bool UniqueDraw(bool justCheckingString, out Vector2 size, SpriteBatch spriteBatch, Vector2 position = default, Color color = default, float scale = 1f)
			{
				size = new Vector2(15f * scale, 16f * scale);

				// Color.Black is the shadow pass; the icon has its own outline, so skip it.
				if (justCheckingString || color == Color.Black)
					return true;

				Texture2D pixel = TextureAssets.MagicPixel.Value;
				Rectangle one = new Rectangle(0, 0, 1, 1);
				Vector2 origin = new Vector2(0.5f, 0.5f);
				Vector2 center = position + new Vector2(size.X * 0.5f, 19f * scale);

				spriteBatch.Draw(pixel, center, one, new Color(20, 20, 20) * 0.7f, MathHelper.PiOver4, origin, 9.5f * scale, SpriteEffects.None, 0f);
				spriteBatch.Draw(pixel, center, one, new Color(255, 205, 70), MathHelper.PiOver4, origin, 7f * scale, SpriteEffects.None, 0f);
				return true;
			}
		}
	}
}
