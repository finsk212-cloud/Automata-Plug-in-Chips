using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;

namespace Augments
{
	// Draws a soft glowing ring at the 600px aura radius around the local player
	// when they are in Support stance and own at least one aura augment.
	// Three passes (outer bloom → mid halo → bright core) with a slow sine pulse
	// give a neon-ring look without being distracting.
	public static class AugmentAuraDrawer
	{
		private const float AuraRadius = 600f;
		private const int CircleSteps = 64;
		private const int ChronobreakRingTicks = 40;

		// Chronobreak shockwaves: world position plus the game tick they started on.
		private static readonly List<(Vector2 Center, uint Start)> chronobreakRings = new List<(Vector2 Center, uint Start)>();

		public static void AddChronobreakRing(Vector2 worldCenter)
		{
			if (Main.dedServ)
				return;
			chronobreakRings.Add((worldCenter, Main.GameUpdateCount));
		}

		// A single pale cyan-violet ring that expands from the Chronobreak owner out to the
		// 600px aura radius while fading. Drawn regardless of the aura circle's own conditions.
		public static void DrawChronobreakRings(SpriteBatch spriteBatch)
		{
			for (int i = chronobreakRings.Count - 1; i >= 0; i--)
			{
				var ring = chronobreakRings[i];
				float t = (Main.GameUpdateCount - ring.Start) / (float)ChronobreakRingTicks;
				if (t >= 1f)
				{
					chronobreakRings.RemoveAt(i);
					continue;
				}

				float eased = 1f - (1f - t) * (1f - t);
				float fade = 1f - t;
				DrawCircle(spriteBatch, ring.Center, 7f, new Color(150, 120, 255) * (0.10f * fade), AuraRadius * eased);
				DrawCircle(spriteBatch, ring.Center, 2f, new Color(190, 240, 255) * (0.40f * fade), AuraRadius * eased);
			}
		}

		public static void DrawAuras(SpriteBatch spriteBatch)
		{
			var ap = Main.LocalPlayer.GetModPlayer<AugmentPlayer>();
			// Aura circle requires 2+ Support augments (stance level).
			// Effects still work from 1 augment, but the visual only unlocks at 2.
			if (ap.SupportAugmentCount < 2)
				return;
			bool hasAny = false;
			foreach (var a in ap.Owned)
			{
				if (a.HasAuraEffect) { hasAny = true; break; }
			}
			if (!hasAny)
				return;

			// Slow breathing pulse — full cycle ≈ 3 seconds, range 0→1→0.
			float pulse = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2.0) * 0.5f + 0.5f;

			Vector2 center = Main.LocalPlayer.Center;

			// Layer 1: super-wide outer scatter, almost invisible — gives glow spread
			DrawCircle(spriteBatch, center, 22f, new Color(60, 180, 100)  * (0.018f + pulse * 0.010f));
			// Layer 2: wide bloom
			DrawCircle(spriteBatch, center, 10f, new Color(100, 220, 140) * (0.030f + pulse * 0.015f));
			// Layer 3: mid halo
			DrawCircle(spriteBatch, center,  4f, new Color(140, 235, 165) * (0.070f + pulse * 0.030f));
			// Layer 4: faint core — reduced from before so the ring reads as glow, not solid line
			DrawCircle(spriteBatch, center, 1.5f, new Color(210, 255, 225) * (0.22f + pulse * 0.08f));
		}

		private static void DrawCircle(SpriteBatch spriteBatch, Vector2 worldCenter, float thickness, Color color, float radius = AuraRadius)
		{
			float uiScale = Main.UIScale;
			// Convert center to screen space; scale radius by zoom so the circle
			// tracks the actual 600-world-pixel boundary regardless of zoom level.
			Vector2 screenCenter = (worldCenter - Main.screenPosition) / uiScale;
			float screenRadius = radius / Main.GameZoomTarget / uiScale;

			for (int s = 0; s < CircleSteps; s++)
			{
				float a1 = s / (float)CircleSteps * MathHelper.TwoPi;
				float a2 = (s + 1) / (float)CircleSteps * MathHelper.TwoPi;

				Vector2 p1 = screenCenter + new Vector2((float)Math.Cos(a1), (float)Math.Sin(a1)) * screenRadius;
				Vector2 p2 = screenCenter + new Vector2((float)Math.Cos(a2), (float)Math.Sin(a2)) * screenRadius;

				DrawSegment(spriteBatch, p1, p2, color, thickness);
			}
		}

		private static void DrawSegment(SpriteBatch spriteBatch, Vector2 start, Vector2 end, Color color, float thickness)
		{
			Vector2 delta = end - start;
			float len = delta.Length();
			if (len < 0.5f)
				return;

			float angle = (float)Math.Atan2(delta.Y, delta.X);
			spriteBatch.Draw(
				TextureAssets.MagicPixel.Value,
				start,
				new Rectangle(0, 0, 1, 1),
				color,
				angle,
				new Vector2(0f, 0.5f),
				new Vector2(len, thickness),
				SpriteEffects.None,
				0f
			);
		}
	}
}
