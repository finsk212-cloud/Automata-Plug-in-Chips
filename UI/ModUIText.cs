using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.UI;

namespace Augments
{
	// The game's UIText, but when the blocky pixel font is picked on the Cosmetics page it draws its text with that font.
	// With the game font picked it simply is a UIText. Only this mod's own labels use this class.
	internal class ModUIText : UIText
	{
		private readonly float myScale;

		public ModUIText(string text, float textScale = 1f, bool large = false) : base(text, textScale, large)
		{
			myScale = textScale;
		}

		protected override void DrawSelf(SpriteBatch spriteBatch)
		{
			if (!ModText.UsePixel(FontAssets.MouseText.Value, myScale))
			{
				base.DrawSelf(spriteBatch);
				return;
			}

			CalculatedStyle inner = GetInnerDimensions();
			string text = Text ?? "";
			if (text.Length == 0)
				return;

			DynamicSpriteFont font = FontAssets.MouseText.Value;
			float scale = myScale;

			// Wrap long text to the width of the box, since the game's own wrapping measured the game font.
			string shown = IsWrapped ? Wrap(font, text, scale, inner.Width) : text;

			Vector2 size = ModText.Size(font, shown, new Vector2(scale));
			if (DynamicallyScaleDownToWidth && size.X > inner.Width && size.X > 0f)
			{
				scale *= inner.Width / size.X;
				size = ModText.Size(font, shown, new Vector2(scale));
			}

			Vector2 pos = inner.Position();
			pos.X += (inner.Width - size.X) * TextOriginX;
			pos.Y += (inner.Height - size.Y) * TextOriginY;
			ModText.DrawBorder(spriteBatch, shown, pos, TextColor, scale);
		}

		private static string Wrap(DynamicSpriteFont font, string text, float scale, float width)
		{
			var sb = new StringBuilder();
			foreach (string paragraph in text.Split('\n'))
			{
				string line = "";
				foreach (string word in paragraph.Split(' '))
				{
					string trial = line.Length == 0 ? word : line + " " + word;
					if (line.Length > 0 && ModText.Size(font, trial, new Vector2(scale)).X > width)
					{
						sb.Append(line).Append('\n');
						line = word;
					}
					else
					{
						line = trial;
					}
				}
				sb.Append(line).Append('\n');
			}
			return sb.ToString().TrimEnd('\n');
		}
	}
}
