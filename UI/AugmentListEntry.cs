using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.UI;
using Terraria.UI.Chat;

namespace Augments
{
	// A single row in the "Your Augments" list. Shows the name always;
	// hovering sets HoveredAugment, which a separate top-level layer
	// (AugmentTooltipDrawer) uses to draw the tooltip box unclipped.
	public class AugmentListEntry : UIPanel
	{
		private readonly Augment augment;

		// Shared across all entries. Tracking it here (rather than drawing
		// the tooltip directly in this panel) avoids the tooltip being
		// clipped to this panel's own bounds.
		public static Augment HoveredAugment;

		public AugmentListEntry(Augment augment)
		{
			this.augment = augment;

			BackgroundColor = RarityColor(augment.Rarity) * 0.3f;
			BorderColor = RarityColor(augment.Rarity);

			UIText nameText = new ModUIText(augment.DisplayName, 0.85f)
			{
				HAlign = 0f,
				VAlign = 0.5f
			};
			nameText.Left.Set(10f, 0f);
			Append(nameText);
		}

		protected override void DrawSelf(SpriteBatch spriteBatch)
		{
			base.DrawSelf(spriteBatch);

			var kb = augment.ActiveModKeybind;
			if (kb == null || kb.GetAssignedKeys().Count > 0) return;

			var font = FontAssets.MouseText.Value;
			const string hint = "No key bound";
			Vector2 hintScale = new Vector2(0.72f);
			Vector2 hintSize = ModText.Size(font, hint, hintScale);

			CalculatedStyle dims = GetInnerDimensions();
			Vector2 hintPos = new Vector2(
				dims.X + dims.Width - hintSize.X,
				dims.Y + (dims.Height - hintSize.Y) * 0.5f
			);

			ModText.DrawCoded(
				spriteBatch, font, hint, hintPos,
				new Color(255, 100, 80) * 0.9f, 0f, Vector2.Zero, hintScale
			);
		}

		public override void MouseOver(UIMouseEvent evt)
		{
			base.MouseOver(evt);
			BackgroundColor = RarityColor(augment.Rarity) * 0.6f;
			HoveredAugment = augment;
		}

		public override void MouseOut(UIMouseEvent evt)
		{
			base.MouseOut(evt);
			BackgroundColor = RarityColor(augment.Rarity) * 0.3f;
			if (HoveredAugment == augment)
				HoveredAugment = null;
		}

		public static Color RarityColor(AugmentRarity rarity)
		{
			return RarityLook.Of(rarity); // the shared tier colours (Epic and Legendary move)
		}
	}
}
