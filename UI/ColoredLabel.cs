using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent;
using Terraria.UI;
using Terraria.UI.Chat;

namespace Augments
{
	public class ColoredLabel : UIElement
	{
		private string text;
		private readonly float scale;
		private readonly bool centerHorizontal;
		private readonly bool centerVertical;

		public ColoredLabel(string text, float scale = 0.85f, bool centerH = true, bool centerV = true)
		{
			this.text = text;
			this.scale = scale;
			this.centerHorizontal = centerH;
			this.centerVertical = centerV;
		}

		public void SetText(string newText)
		{
			text = newText;
		}

		protected override void DrawSelf(SpriteBatch spriteBatch)
		{
			if (string.IsNullOrEmpty(text))
				return;

			CalculatedStyle d = GetDimensions();
			var font = FontAssets.MouseText.Value;
			Vector2 size = ModText.Size(font, text, new Vector2(scale));

			float x = (centerHorizontal && d.Width > size.X) ? d.X + (d.Width - size.X) * 0.5f : d.X;
			float y = (centerVertical && d.Height > size.Y) ? d.Y + (d.Height - size.Y) * 0.5f : d.Y;

			ModText.DrawCoded(spriteBatch, font, text, new Vector2(x, y), Color.White, 0f, Vector2.Zero, new Vector2(scale));
		}
	}
}
