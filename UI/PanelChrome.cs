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
		public static readonly Color PanelBackground = new Color(10, 16, 28, 250);
		public static readonly Color PanelBorder = new Color(30, 41, 59);
		public static readonly Color Divider = new Color(30, 41, 59) * 0.90f;
		public static readonly Color Accent = new Color(56, 189, 248);

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

		// Inner hairline highlight + flush corner accent notches.
		public static void DrawFrame(SpriteBatch spriteBatch, Rectangle bRect)
		{
			Texture2D pixel = TextureAssets.MagicPixel.Value;

			Color innerHairline = Color.White * 0.05f;
			spriteBatch.Draw(pixel, new Rectangle(bRect.X + 1, bRect.Y + 1, bRect.Width - 2, 1), innerHairline);
			spriteBatch.Draw(pixel, new Rectangle(bRect.X + 1, bRect.Bottom - 2, bRect.Width - 2, 1), innerHairline);
			spriteBatch.Draw(pixel, new Rectangle(bRect.X + 1, bRect.Y + 1, 1, bRect.Height - 2), innerHairline);
			spriteBatch.Draw(pixel, new Rectangle(bRect.Right - 2, bRect.Y + 1, 1, bRect.Height - 2), innerHairline);

			Color cornerAccent = Accent * 0.70f;
			const int clen = 5;
			const int cthk = 2;
			spriteBatch.Draw(pixel, new Rectangle(bRect.X + 1, bRect.Y + 1, clen, cthk), cornerAccent);
			spriteBatch.Draw(pixel, new Rectangle(bRect.X + 1, bRect.Y + 1, cthk, clen), cornerAccent);
			spriteBatch.Draw(pixel, new Rectangle(bRect.Right - clen - 1, bRect.Y + 1, clen, cthk), cornerAccent);
			spriteBatch.Draw(pixel, new Rectangle(bRect.Right - cthk - 1, bRect.Y + 1, cthk, clen), cornerAccent);
			spriteBatch.Draw(pixel, new Rectangle(bRect.X + 1, bRect.Bottom - cthk - 1, clen, cthk), cornerAccent);
			spriteBatch.Draw(pixel, new Rectangle(bRect.X + 1, bRect.Bottom - clen - 1, cthk, clen), cornerAccent);
			spriteBatch.Draw(pixel, new Rectangle(bRect.Right - clen - 1, bRect.Bottom - cthk - 1, clen, cthk), cornerAccent);
			spriteBatch.Draw(pixel, new Rectangle(bRect.Right - cthk - 1, bRect.Bottom - clen - 1, cthk, clen), cornerAccent);
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
}
