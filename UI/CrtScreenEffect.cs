using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace Augments
{
	// The "old computer screen" turn on and turn off, used by the Plugins menu.
	//
	// Turning off: the panel squashes flat into a bright line, the line shrinks sideways into a dot, and the dot goes out.
	// Turning on is the same thing played backwards. `open` is how far on the screen is: 0 = off, 1 = fully on.
	//
	//   open 0.00 to 0.15   a dot that grows
	//   open 0.15 to 0.50   a bright line that stretches out sideways
	//   open 0.50 to 1.00   the panel unsquashing from the line to its full height
	//
	// The panel is first drawn normally into an off-screen picture (a render target), and that picture is then drawn squashed. Drawing
	// the real panel through a squashing transform would not work, because the panel's scrolling lists restart the sprite batch with
	// their own matrix and would ignore it.
	public static class CrtScreenEffect
	{
		private static RenderTarget2D target;
		private static bool failed;

		private static readonly Color Beam = new Color(232, 224, 190);

		public static void Unload()
		{
			RenderTarget2D old = target;
			target = null;
			if (old != null)
				Main.QueueMainThreadAction(() => old.Dispose());
		}

		private static float EaseOut(float t) => 1f - (1f - t) * (1f - t);

		private static void BeginUi(SpriteBatch sb)
		{
			// The same settings the game uses for its own interface layers.
			sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.UIScaleMatrix);
		}

		private static bool EnsureTarget(GraphicsDevice gd)
		{
			int w = gd.Viewport.Width;
			int h = gd.Viewport.Height;
			if (w < 16 || h < 16)
				return false;

			if (target == null || target.IsDisposed || target.Width != w || target.Height != h)
			{
				RenderTarget2D old = target;
				target = new RenderTarget2D(gd, w, h);
				if (old != null && !old.IsDisposed)
					old.Dispose();
			}

			return true;
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
			var px = new Rectangle((int)(panelUi.X * m), (int)(panelUi.Y * m), (int)(panelUi.Width * m), (int)(panelUi.Height * m));
			float cx = px.Center.X;
			float cy = px.Center.Y;

			bool batchOpen = true;
			bool targetSet = false;
			RenderTargetBinding[] previous = null;

			try
			{
				if (open > 0.5f)
				{
					if (!EnsureTarget(gd))
					{
						drawContent();
						return;
					}

					// 1. The panel, drawn normally, into the off-screen picture.
					previous = gd.GetRenderTargets();
					sb.End();
					batchOpen = false;
					gd.SetRenderTarget(target);
					targetSet = true;
					gd.Clear(Color.Transparent);
					BeginUi(sb);
					batchOpen = true;
					drawContent();
					sb.End();
					batchOpen = false;
					if (previous.Length == 0)
						gd.SetRenderTarget(null);
					else
						gd.SetRenderTargets(previous);
					targetSet = false;

					// 2. That picture, squashed towards the middle of the panel, with a bright wash that grows as it gets thinner.
					float t = (open - 0.5f) / 0.5f;
					float sy = MathHelper.Lerp(0.012f, 1f, EaseOut(t));
					Matrix squash = Matrix.CreateTranslation(-cx, -cy, 0f) * Matrix.CreateScale(1f, sy, 1f) * Matrix.CreateTranslation(cx, cy, 0f);
					sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, null, squash);
					batchOpen = true;
					sb.Draw(target, Vector2.Zero, Color.White);
					HubDraw.Fill(sb, px, Beam * (0.55f * (1f - t)));
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

				if (targetSet)
				{
					if (previous == null || previous.Length == 0)
						gd.SetRenderTarget(null);
					else
						gd.SetRenderTargets(previous);
				}

				// Leave the sprite batch open for the layer, exactly as it was handed to us.
				BeginUi(sb);
			}
		}
	}
}
