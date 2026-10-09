using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.ID;
using Terraria.UI;
using Terraria.UI.Chat;

namespace Augments
{
	// A beautifully styled row in the shop's "Buy Back" or "Remove" list.
	// Features class icons, rarity badges, smooth hover lighting, and themed action buttons.
	public class AugmentShopEntry : UIPanel
	{
		private readonly Augment augment;
		private readonly string actionLabel;
		private readonly Action<Augment> onAction;
		private readonly bool isBuyAction;
		private readonly bool isPermanent;
		private bool isHovered;

		private readonly ActionButton actionButton;

		public AugmentShopEntry(Augment augment, string actionLabel, Action<Augment> onAction, string secondaryLabel = null, Action<Augment> onSecondary = null)
		{
			this.augment = augment;
			this.actionLabel = actionLabel;
			this.onAction = onAction;
			this.isBuyAction = actionLabel.StartsWith("Buy", StringComparison.OrdinalIgnoreCase) || actionLabel.StartsWith("Install", StringComparison.OrdinalIgnoreCase);
			this.isPermanent = onAction == null;

			SetPadding(0f);
			// Extra right margin so the 1px border isn't clipped by the list's scissor rect.
			Left.Set(2f, 0f);
			Width.Set(-8f, 1f);
			Height.Set(54f, 0f);

			bool stacked = secondaryLabel != null && onSecondary != null;
			actionButton = new ActionButton(actionLabel, isBuyAction, isPermanent, this, augment);
			actionButton.Width.Set(116f, 0f);
			actionButton.Height.Set(stacked ? 22f : 26f, 0f);
			actionButton.HAlign = 1f;
			if (stacked)
				actionButton.Top.Set(28f, 0f);
			else
				actionButton.VAlign = 0.5f;
			actionButton.Left.Set(-8f, 0f);
			if (!isPermanent && onAction != null)
			{
				actionButton.Clicked += () => onAction(augment);
			}
			Append(actionButton);

			if (stacked)
			{
				var secondary = new ActionButton(secondaryLabel, false, false, this, augment, true);
				secondary.Width.Set(116f, 0f);
				secondary.Height.Set(22f, 0f);
				secondary.HAlign = 1f;
				secondary.Top.Set(4f, 0f);
				secondary.Left.Set(-8f, 0f);
				secondary.Clicked += () => onSecondary(augment);
				Append(secondary);
			}
		}

		public override void MouseOver(UIMouseEvent evt)
		{
			base.MouseOver(evt);
			isHovered = true;
			if (actionButton == null || !actionButton.ContainsPoint(Main.MouseScreen))
			{
				AugmentListEntry.HoveredAugment = augment;
			}
			else if (AugmentListEntry.HoveredAugment == augment)
			{
				AugmentListEntry.HoveredAugment = null;
			}
			SoundEngine.PlaySound(SoundID.MenuTick);
		}

		public override void MouseOut(UIMouseEvent evt)
		{
			base.MouseOut(evt);
			isHovered = false;
			if (AugmentListEntry.HoveredAugment == augment)
				AugmentListEntry.HoveredAugment = null;
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			if (actionButton != null && actionButton.ContainsPoint(Main.MouseScreen))
			{
				if (AugmentListEntry.HoveredAugment == augment)
				{
					AugmentListEntry.HoveredAugment = null;
				}
			}
		}

		protected override void DrawSelf(SpriteBatch spriteBatch)
		{
			CalculatedStyle dims = GetDimensions();
			int rx = (int)dims.X;
			int ry = (int)dims.Y;
			int rw = (int)dims.Width;
			int rh = (int)dims.Height;

			Rectangle rect = new Rectangle(rx, ry, rw, rh);
			Texture2D pixel = TextureAssets.MagicPixel.Value;

			Color rarityColor = AugmentListEntry.RarityColor(augment.Rarity);
			if (augment.Rarity == AugmentRarity.Common)
				rarityColor = new Color(225, 230, 240);

			// 1. Ambient underglow on hover
			if (isHovered)
			{
				spriteBatch.Draw(pixel, new Rectangle(rx - 1, ry - 1, rw + 2, rh + 2), rarityColor * 0.05f);
			}

			// 2. Cybernetic Chassis Fill (#0A101C)
			Color bgColor = isHovered ? new Color(16, 24, 42) * 0.96f : new Color(10, 16, 28) * 0.94f;
			spriteBatch.Draw(pixel, rect, bgColor);

			// 3. 1px Inner Hairline Highlight Accent
			Color innerHairline = Color.White * (isHovered ? 0.08f : 0.04f);
			spriteBatch.Draw(pixel, new Rectangle(rx + 1, ry + 1, rw - 2, 1), innerHairline);
			spriteBatch.Draw(pixel, new Rectangle(rx + 1, ry + rh - 2, rw - 2, 1), innerHairline);
			spriteBatch.Draw(pixel, new Rectangle(rx + 1, ry + 1, 1, rh - 2), innerHairline);
			spriteBatch.Draw(pixel, new Rectangle(rx + rw - 2, ry + 1, 1, rh - 2), innerHairline);

			// 4. 1px Outer Border (clean, no corner ticks)
			Color borderColor = isHovered ? rarityColor * 0.75f : Color.Lerp(new Color(30, 41, 59), rarityColor, 0.30f) * 0.70f;
			spriteBatch.Draw(pixel, new Rectangle(rx, ry, rw, 1), borderColor);
			spriteBatch.Draw(pixel, new Rectangle(rx, ry + rh - 1, rw, 1), borderColor);
			spriteBatch.Draw(pixel, new Rectangle(rx, ry, 1, rh), borderColor);
			spriteBatch.Draw(pixel, new Rectangle(rx + rw - 1, ry, 1, rh), borderColor);

			// 5. Unboxed Class Icon (floated directly, perfectly centered vertically)
			Texture2D iconTex = AugmentSlotElement.GetClassIcon(augment.Class);
			if (iconTex != null)
			{
				float iconScale = Math.Min(24f / iconTex.Width, 24f / iconTex.Height);
				Vector2 iconPos = new Vector2(
					rect.X + 14f,
					rect.Y + (rect.Height - iconTex.Height * iconScale) * 0.5f
				);
				spriteBatch.Draw(iconTex, iconPos, null, rarityColor, 0f, Vector2.Zero, iconScale, SpriteEffects.None, 0f);
			}

			// 6. Name & Subtitle
			var font = FontAssets.MouseText.Value;
			float textLeft = rect.X + 46f;

			// Display Name
			Color nameColor = isHovered ? Color.Lerp(rarityColor, Color.White, 0.35f) : rarityColor;
			Vector2 namePos = new Vector2(textLeft, rect.Y + 9f);
			ChatManager.DrawColorCodedStringWithShadow(
				spriteBatch, font, augment.DisplayName, namePos, nameColor, 0f, Vector2.Zero, new Vector2(0.85f)
			);

			// Subtitle: Unboxed RARITY • CLASS
			string rarName = AugmentTooltipDrawer.GetRarityDisplayName(augment.Rarity);
			string clsName = AugmentTooltipDrawer.GetClassDisplayName(augment.Class);
			Vector2 subPos = new Vector2(textLeft, rect.Y + 28f);
			Vector2 subScale = new Vector2(0.72f);

			ChatManager.DrawColorCodedStringWithShadow(spriteBatch, font, rarName, subPos, rarityColor * 0.85f, 0f, Vector2.Zero, subScale);
			float curSubX = subPos.X + ChatManager.GetStringSize(font, rarName, subScale).X;

			ChatManager.DrawColorCodedStringWithShadow(spriteBatch, font, "  •  ", new Vector2(curSubX, subPos.Y), new Color(100, 116, 139), 0f, Vector2.Zero, subScale);
			curSubX += ChatManager.GetStringSize(font, "  •  ", subScale).X;

			Color clsCol = AugmentTooltipDrawer.GetClassColor(augment.Class);
			ChatManager.DrawColorCodedStringWithShadow(spriteBatch, font, clsName, new Vector2(curSubX, subPos.Y), clsCol * 0.85f, 0f, Vector2.Zero, subScale);
		}

		private class ActionButton : UIElement
		{
			public event Action Clicked;

			private readonly string label;
			private readonly bool isBuy;
			private readonly bool disabled;
			private readonly bool neutral;
			private readonly AugmentShopEntry parentEntry;
			private readonly Augment parentAugment;
			private bool isHovered;

			public ActionButton(string label, bool isBuy, bool disabled, AugmentShopEntry parentEntry, Augment parentAugment, bool neutral = false)
			{
				this.neutral = neutral;
				this.label = label;
				this.isBuy = isBuy;
				this.disabled = disabled;
				this.parentEntry = parentEntry;
				this.parentAugment = parentAugment;
			}

			public override void LeftClick(UIMouseEvent evt)
			{
				base.LeftClick(evt);
				if (!disabled)
				{
					SoundEngine.PlaySound(isBuy ? SoundID.Item4 : SoundID.MenuClose);
					Clicked?.Invoke();
				}
			}

			public override void MouseOver(UIMouseEvent evt)
			{
				base.MouseOver(evt);
				// Disable tooltip when hovering the buy / sell button
				if (AugmentListEntry.HoveredAugment == parentAugment)
				{
					AugmentListEntry.HoveredAugment = null;
				}
				if (!disabled)
				{
					isHovered = true;
					SoundEngine.PlaySound(SoundID.MenuTick);
				}
			}

			public override void MouseOut(UIMouseEvent evt)
			{
				base.MouseOut(evt);
				isHovered = false;
				// If mouse left the button but is still within the parent card, restore tooltip
				if (parentEntry != null && parentEntry.ContainsPoint(Main.MouseScreen))
				{
					AugmentListEntry.HoveredAugment = parentAugment;
				}
			}

			protected override void DrawSelf(SpriteBatch spriteBatch)
			{
				CalculatedStyle dims = GetDimensions();
				var rect = new Rectangle((int)dims.X, (int)dims.Y, (int)dims.Width, (int)dims.Height);
				Texture2D pixel = TextureAssets.MagicPixel.Value;

				Color textColor;

				if (disabled)
				{
					textColor = new Color(100, 116, 139);
				}
				else if (neutral)
				{
					textColor = isHovered ? Color.White : new Color(104, 194, 216);
				}
				else if (isBuy)
				{
					textColor = isHovered ? Color.White : new Color(74, 222, 128);
				}
				else
				{
					// Dismantle / Refund
					textColor = isHovered ? Color.White : new Color(248, 113, 113);
				}

				// Soft ambient hover wash — ZERO outline boxes or border lines
				if (isHovered && !disabled)
				{
					Color washColor = neutral ? new Color(56, 189, 248, 35) : isBuy ? new Color(34, 197, 94, 35) : new Color(239, 68, 68, 35);
					spriteBatch.Draw(pixel, rect, washColor);
				}

				// Pure unboxed typography, perfectly centered vertically and horizontally
				var font = FontAssets.MouseText.Value;
				Vector2 scale = new Vector2(0.72f);
				Vector2 textSize = ChatManager.GetStringSize(font, label, scale);
				Vector2 textPos = new Vector2(
					rect.X + (rect.Width - textSize.X) * 0.5f,
					rect.Y + (rect.Height - 12.2f) * 0.5f - 1.5f
				);
				ChatManager.DrawColorCodedStringWithShadow(spriteBatch, font, label, textPos, textColor, 0f, Vector2.Zero, scale);
			}
		}
	}
}
