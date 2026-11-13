using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.UI;

namespace Augments
{
	// Base for every element on the Automata title-screen pages. The pages are drawn INSIDE vanilla's own
	// menu code, so an unhandled exception here could stop the game from reaching its menu at all.
	// This base catches draw and update errors, reports them once, and lets AutomataHubSystem fall back to
	// the vanilla menu instead of crashing.
	internal abstract class HubElementBase : UIElement
	{
		// When set and it returns false, the element is hidden: not drawn and not clickable.
		// Lets one page swap its sections in place without creating and removing elements.
		public Func<bool> VisibleWhen;

		protected bool IsShown => VisibleWhen == null || VisibleWhen();

		public override bool ContainsPoint(Vector2 point)
		{
			return IsShown && base.ContainsPoint(point);
		}

		protected abstract void DrawContent(SpriteBatch spriteBatch);

		protected virtual void UpdateContent(GameTime gameTime)
		{
		}

		protected sealed override void DrawSelf(SpriteBatch spriteBatch)
		{
			if (!IsShown)
				return;

			try
			{
				DrawContent(spriteBatch);
			}
			catch (Exception e)
			{
				AutomataHubSystem.ReportFailure(e);
			}
		}

		public override void Update(GameTime gameTime)
		{
			try
			{
				base.Update(gameTime);
				UpdateContent(gameTime);
			}
			catch (Exception e)
			{
				AutomataHubSystem.ReportFailure(e);
			}
		}
	}

	// A rectangle that clips whatever is drawn inside it, so scrolled content is cut cleanly at the edge instead of whole
	// rows or lines being dropped. The parent draws its scrolling content through this element's callback.
	// It never takes mouse input: clicks and the wheel go to the parent as usual.
	internal class HubClipRegion : HubElementBase
	{
		private Rectangle placed;

		// The game's UI clips only an element's CHILDREN when OverflowHidden is set, not what the element draws itself.
		// So the drawing happens in a child element that fills this region, and everything it draws is cut at the edge.
		private sealed class ClipContent : HubElementBase
		{
			private readonly Action<SpriteBatch, Rectangle> draw;

			public ClipContent(Action<SpriteBatch, Rectangle> draw)
			{
				this.draw = draw;
				Width.Set(0f, 1f);
				Height.Set(0f, 1f);
				IgnoresMouseInteraction = true;
			}

			protected override void DrawContent(SpriteBatch spriteBatch)
			{
				draw?.Invoke(spriteBatch, GetDimensions().ToRectangle());
			}
		}

		public HubClipRegion(Action<SpriteBatch, Rectangle> draw)
		{
			OverflowHidden = true;
			IgnoresMouseInteraction = true;

			// A hidden region still draws its children, so the child checks the region's visibility itself.
			var content = new ClipContent(draw);
			content.VisibleWhen = () => IsShown;
			Append(content);
		}

		// Puts the region over `target` (screen units). `parent` is the parent element's own rectangle.
		public void Place(Rectangle target, Rectangle parent)
		{
			if (target == placed)
				return;

			placed = target;
			Left.Set(target.X - parent.X, 0f);
			Top.Set(target.Y - parent.Y, 0f);
			Width.Set(target.Width, 0f);
			Height.Set(target.Height, 0f);
			Recalculate();
		}

		// The region itself draws nothing; its child does (see above).
		protected override void DrawContent(SpriteBatch spriteBatch)
		{
		}
	}
}
