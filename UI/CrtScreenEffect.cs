using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace Augments
{
	// The "old computer screen" turn on and turn off, used by the Plugins menu.
	//
	// Turning off: the picture is cut away from the top and bottom until only a bright line is left, the line shrinks sideways into a dot,
	// and the dot goes out. Turning on is the same thing played backwards. `open` is how far on the screen is: 0 = off, 1 = fully on.
	//
	//   open 0.00 to 0.15   a dot that grows
	//   open 0.15 to 0.50   a bright line that stretches out sideways
	//   open 0.50 to 1.00   the panel opening out of the line, from the middle
	//
	// The panel is drawn normally, but only inside a window (a scissor rectangle) that grows and shrinks around its middle. Nothing is
	// drawn to a different render target, so the world behind the panel is never touched. (An earlier version drew the panel into an
	// off-screen picture and squashed that; on this game it blanked the whole screen while the effect ran.)
	public static class CrtScreenEffect
	{
		private static bool failed;

		private static readonly Color Beam = new Color(232, 224, 190);

		private static readonly RasterizerState Clip = new RasterizerState
		{
			CullMode = CullMode.CullCounterClockwiseFace,
			ScissorTestEnable = true
		};

		private static float EaseOut(float t) => 1f - (1f - t) * (1f - t);

		private static void BeginUi(SpriteBatch sb, RasterizerState raster)
		{
			// The same settings the game uses for its own interface layers.
			sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, raster, null, Main.UIScaleMatrix);
		}

		// Draws one frame. `panelUi` is the panel's rectangle in UI units; `drawContent` draws the panel normally.
		// The sprite batch must be open (as inside an interface layer) and is left open with the interface settings.
		public static void Draw(SpriteBatch sb, float open, Rectangle panelUi, Action drawContent)
		{
			open = MathHelper.Clamp(open, 0f, 1f);

			if (failed || panelUi.Width <= 0 || panelUi.Height <= 0)
			{
				if (open > 0.5f || failed)
					drawContent();
				return;
			}

			float m = Main.UIScaleMatrix.M11;
			if (m < 0.1f)
				m = 1f;

			GraphicsDevice gd = sb.GraphicsDevice;
			Rectangle savedScissor = gd.ScissorRectangle;
			var px = new Rectangle((int)(panelUi.X * m), (int)(panelUi.Y * m), (int)(panelUi.Width * m), (int)(panelUi.Height * m));
			float cx = px.Center.X;
			float cy = px.Center.Y;

			bool batchOpen = true;

			try
			{
				if (open > 0.5f)
				{
					float t = (open - 0.5f) / 0.5f;
					float vis = MathHelper.Lerp(0.012f, 1f, EaseOut(t));

					// The window the panel is drawn in: grows from a thin strip to the whole panel (a little extra room for its corner marks).
					int half = Math.Max(1, (int)((px.Height * vis) / 2f + 14f * m * vis));
					var band = new Rectangle(px.X - (int)(14f * m), (int)cy - half, px.Width + (int)(28f * m), half * 2);
					band = Rectangle.Intersect(band, gd.Viewport.Bounds);

					// 1. The panel, drawn normally but clipped to the window.
					sb.End();
					batchOpen = false;
					BeginUi(sb, Clip);
					batchOpen = true;
					gd.ScissorRectangle = band;
					// Interface elements read the device's current rasterizer state to restore it after a scrolling list has drawn, so it must
					// already be the clipping one, or the clip would be lost part-way through the panel.
					gd.RasterizerState = Clip;
					drawContent();
					sb.End();
					batchOpen = false;

					// 2. A bright wash over the window while it is thin, and a bright edge at its top and bottom, in screen pixels.
					sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);
					batchOpen = true;
					if (t < 0.999f)
					{
						HubDraw.Fill(sb, band, Beam * (0.5f * (1f - t)));
						int edge = Math.Max(2, (int)Math.Round(2f * m));
						HubDraw.Fill(sb, new Rectangle(band.X, band.Y, band.Width, edge), Beam * (1f - t));
						HubDraw.Fill(sb, new Rectangle(band.X, band.Bottom - edge, band.Width, edge), Beam * (1f - t));
					}
					sb.End();
					batchOpen = false;
				}
				else
				{
					// 3. Only the beam is left: a line (then a dot), drawn in screen pixels.
					sb.End();
					batchOpen = false;
					sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);
					batchOpen = true;

					if (open > 0.15f)
					{
						float u = EaseOut((open - 0.15f) / 0.35f);
						int w = Math.Max(6, (int)(px.Width * MathHelper.Lerp(0.03f, 1f, u)));
						int lineH = Math.Max(2, (int)Math.Round(3f * m));
						HubDraw.Fill(sb, new Rectangle((int)cx - w / 2, (int)cy - lineH * 2, w, lineH * 4), Beam * 0.22f);
						HubDraw.Fill(sb, new Rectangle((int)cx - w / 2, (int)cy - lineH / 2, w, lineH), Beam);
					}
					else
					{
						float v = open / 0.15f;
						int size = Math.Max(2, (int)Math.Round((3f + 3f * v) * m));
						HubDraw.Fill(sb, new Rectangle((int)cx - size, (int)cy - size / 2 - 1, size * 2, size + 2), Beam * (0.25f * v));
						HubDraw.Fill(sb, new Rectangle((int)cx - size / 2, (int)cy - size / 2, size, size), Beam * v);
					}

					sb.End();
					batchOpen = false;
				}
			}
			catch (Exception e)
			{
				failed = true;
				ModContent.GetInstance<Augments>().Logger.Warn("The screen effect failed and is switched off: " + e.Message);
			}
			finally
			{
				if (batchOpen)
				{
					try
					{
						sb.End();
					}
					catch (InvalidOperationException)
					{
					}
				}

				gd.ScissorRectangle = savedScissor;
				gd.RasterizerState = Main.Rasterizer;

				// Leave the sprite batch open for the layer, exactly as it was handed to us.
				BeginUi(sb, Main.Rasterizer);
			}
		}
	}
}
