using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Augments
{
	// The small coloured chip the Pod upgrades show next to the Pod: a little arrow pointing the way, then a few words ("closer, below").
	// Shared by the Ore Sniffer and the Watch Your Step upgrades so they look like they belong together.
	internal static class PodHintChip
	{
		private const float TextScale = 0.56f;
		private const float ArrowSize = 9f;

		// Draws the chip centred on `centreX` with its top edge at `top`, and returns its height. `delta` is the direction the arrow points.
		internal static int Draw(SpriteBatch sb, float centreX, float top, string text, Vector2 delta, Color fill, Color ink, float alpha)
		{
			float textW = HubDraw.Width(text, TextScale, 0.5f);
			int boxH = (int)Math.Round(HubDraw.LineHeight(TextScale) * 0.5f) + 10;
			int boxW = (int)Math.Round(textW) + (int)ArrowSize + 5 + 16;
			var chip = new Rectangle((int)Math.Round(centreX - boxW / 2f), (int)Math.Round(top), boxW, boxH);
			HubDraw.Fill(sb, new Rectangle(chip.X - 1, chip.Y - 1, chip.Width + 2, chip.Height + 2), Color.Black * (0.75f * alpha));
			HubDraw.Fill(sb, chip, fill * alpha);

			Vector2 d = delta.LengthSquared() > 0.01f ? Vector2.Normalize(delta) : new Vector2(0f, 1f);
			Vector2 p = new Vector2(-d.Y, d.X);
			Vector2 c = new Vector2(chip.X + 8f + ArrowSize / 2f, chip.Y + boxH / 2f);
			Span<Vector2> arrow = stackalloc Vector2[] { c + d * (ArrowSize / 2f), c - d * (ArrowSize / 2f) + p * (ArrowSize / 2.4f), c - d * (ArrowSize / 2f) - p * (ArrowSize / 2.4f) };
			HubDraw.FillPolygon(sb, arrow, ink * alpha);
			HubDraw.Text(sb, text, new Vector2(chip.X + 8f + ArrowSize + 5f, chip.Y + boxH / 2f - HubDraw.CapMid(TextScale)), ink * alpha, TextScale, 0.5f);
			return boxH;
		}
	}
}