using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace Augments
{
	// The icons of the six plugin classes, in the flat "stencil" style: solid shapes with a few cut-outs, like a stencilled supply crate.
	//
	// They are drawn here in code instead of loaded from image files. Each icon is a short list of shapes in a 32 by 32 space; a shape
	// either fills or cuts (so a later shape can remove part of an earlier one, which is how the stencil gaps are made). The list is
	// turned into a 40 by 40 white texture once, with smoothed edges, and every place that shows a class icon tints that texture with
	// its own colour (the tier colour, or dark text on a Legendary plate).
	//
	//   Melee      two crossed swords
	//   Ranged     a target
	//   Magic      an eye
	//   Summon     a crown
	//   Support    a plus
	//   Universal  a diamond
	internal static class ClassIcons
	{
		private const int Size = 40;
		private const int Samples = 4; // 4 x 4 samples per pixel, for smooth edges

		private delegate bool Shape(float x, float y);

		private struct Op
		{
			public bool Fill; // false = cut
			public Shape Shape;
		}

		private static readonly Dictionary<AugmentClass, Texture2D> textures = new Dictionary<AugmentClass, Texture2D>();

		// The icon texture for a class (white, to be tinted), or null for a class without one.
		internal static Texture2D Get(AugmentClass augmentClass)
		{
			if (Main.dedServ || Main.graphics?.GraphicsDevice == null)
				return null;

			if (textures.TryGetValue(augmentClass, out Texture2D cached) && cached != null && !cached.IsDisposed)
				return cached;

			List<Op> ops = Describe(augmentClass);
			if (ops == null)
				return null;

			Texture2D tex = Render(ops);
			textures[augmentClass] = tex;
			return tex;
		}

		internal static void Unload()
		{
			var old = new List<Texture2D>(textures.Values);
			textures.Clear();
			if (old.Count == 0)
				return;

			Main.QueueMainThreadAction(() =>
			{
				foreach (Texture2D t in old)
				{
					if (t != null && !t.IsDisposed)
						t.Dispose();
				}
			});
		}

		// --- The icons ---

		private static List<Op> Describe(AugmentClass c)
		{
			switch (c)
			{
				case AugmentClass.Melee:
				{
					// One sword as a single outline (no seams between blade, guard and grip).
					Vector2[] sword = P(16, 2, 18.5f, 5, 18.5f, 22, 22, 22, 22, 24.5f, 17.5f, 24.5f, 17.5f, 30, 14.5f, 30, 14.5f, 24.5f, 10, 24.5f, 10, 22, 13.5f, 22, 13.5f, 5);
					Vector2[] blade = P(16, 2, 18.5f, 5, 18.5f, 22, 13.5f, 22, 13.5f, 5);
					return new List<Op>
					{
						Fill(Rot(45, Poly(sword))),
						// The second sword passes over the first, with a thin gap cut around its blade only.
						Cut(Rot(-45, PolyStroke(blade, 1.7f))),
						Cut(Rot(-45, Rect(13.5f, 21, 5, 3.5f))),
						Fill(Rot(-45, Poly(sword)))
					};
				}

				case AugmentClass.Ranged:
					return new List<Op>
					{
						Fill(Circle(16, 16, 13)),
						Cut(Ring(16, 16, 8, 2)),
						Cut(Rect(14.5f, 2, 3, 28)),
						Cut(Rect(2, 14.5f, 28, 3)),
						Fill(Circle(16, 16, 2.6f))
					};

				case AugmentClass.Magic:
					return new List<Op>
					{
						Fill(Poly(Almond())),
						Cut(Circle(16, 16, 7)),
						Fill(Circle(16, 16, 3.4f)),
						Cut(Rect(15.3f, 3, 1.4f, 4)),
						Cut(Rect(15.3f, 25, 1.4f, 4))
					};

				case AugmentClass.Summon:
					return new List<Op>
					{
						Fill(Poly(P(3, 23, 3, 8, 10.5f, 15, 16, 5, 21.5f, 15, 29, 8, 29, 23))),
						Fill(Rect(3, 25.5f, 26, 3.5f))
					};

				case AugmentClass.Support:
					return new List<Op>
					{
						Fill(Poly(P(12, 3, 20, 3, 20, 12, 29, 12, 29, 20, 20, 20, 20, 29, 12, 29, 12, 20, 3, 20, 3, 12, 12, 12)))
					};

				case AugmentClass.Universal:
					return new List<Op>
					{
						Fill(Poly(P(16, 2, 30, 16, 16, 30, 2, 16))),
						Cut(Poly(P(16, 9, 23, 16, 16, 23, 9, 16))),
						Fill(Rect(14, 14, 4, 4))
					};

				default:
					return null;
			}
		}

		// --- Turning shapes into a texture ---

		private static Texture2D Render(List<Op> ops)
		{
			var data = new Color[Size * Size];
			float scale = 32f / Size;
			int total = Samples * Samples;

			for (int py = 0; py < Size; py++)
			{
				for (int px = 0; px < Size; px++)
				{
					int hits = 0;
					for (int sy = 0; sy < Samples; sy++)
					{
						for (int sx = 0; sx < Samples; sx++)
						{
							float x = (px + (sx + 0.5f) / Samples) * scale;
							float y = (py + (sy + 0.5f) / Samples) * scale;

							bool inside = false;
							foreach (Op op in ops)
							{
								if (op.Shape(x, y))
									inside = op.Fill;
							}

							if (inside)
								hits++;
						}
					}

					// Premultiplied white: the game's sprite batch expects colour already multiplied by alpha.
					int a = (int)Math.Round(255f * hits / total);
					data[py * Size + px] = new Color(a, a, a, a);
				}
			}

			var tex = new Texture2D(Main.graphics.GraphicsDevice, Size, Size);
			tex.SetData(data);
			return tex;
		}

		private static Op Fill(Shape s) => new Op { Fill = true, Shape = s };
		private static Op Cut(Shape s) => new Op { Fill = false, Shape = s };

		private static Vector2[] P(params float[] xy)
		{
			var pts = new Vector2[xy.Length / 2];
			for (int i = 0; i < pts.Length; i++)
				pts[i] = new Vector2(xy[i * 2], xy[i * 2 + 1]);
			return pts;
		}

		private static Shape Rect(float x, float y, float w, float h) => (px, py) => px >= x && px < x + w && py >= y && py < y + h;

		private static Shape Circle(float cx, float cy, float r) => (px, py) =>
		{
			float dx = px - cx, dy = py - cy;
			return dx * dx + dy * dy <= r * r;
		};

		private static Shape Ring(float cx, float cy, float r, float width) => (px, py) =>
		{
			float d = (float)Math.Sqrt((px - cx) * (px - cx) + (py - cy) * (py - cy));
			return Math.Abs(d - r) <= width * 0.5f;
		};

		private static Shape Poly(Vector2[] pts) => (px, py) =>
		{
			bool inside = false;
			for (int i = 0, j = pts.Length - 1; i < pts.Length; j = i++)
			{
				if ((pts[i].Y > py) != (pts[j].Y > py) &&
					px < (pts[j].X - pts[i].X) * (py - pts[i].Y) / (pts[j].Y - pts[i].Y) + pts[i].X)
					inside = !inside;
			}
			return inside;
		};

		// A polygon plus a border of the given half-width around it (a thick outline drawn around the shape).
		private static Shape PolyStroke(Vector2[] pts, float half)
		{
			Shape body = Poly(pts);
			return (px, py) =>
			{
				if (body(px, py))
					return true;

				for (int i = 0, j = pts.Length - 1; i < pts.Length; j = i++)
				{
					if (DistanceToSegment(px, py, pts[j], pts[i]) <= half)
						return true;
				}
				return false;
			};
		}

		private static float DistanceToSegment(float px, float py, Vector2 a, Vector2 b)
		{
			Vector2 ab = b - a;
			float len2 = ab.LengthSquared();
			float t = len2 <= 0f ? 0f : MathHelper.Clamp(((px - a.X) * ab.X + (py - a.Y) * ab.Y) / len2, 0f, 1f);
			Vector2 closest = a + ab * t;
			return (float)Math.Sqrt((px - closest.X) * (px - closest.X) + (py - closest.Y) * (py - closest.Y));
		}

		// Turns a shape about the middle of the icon (like the SVG rotate used in the design: clockwise, in degrees).
		private static Shape Rot(float degrees, Shape inner)
		{
			float a = MathHelper.ToRadians(degrees);
			float cos = (float)Math.Cos(a), sin = (float)Math.Sin(a);
			return (px, py) =>
			{
				float dx = px - 16f, dy = py - 16f;
				return inner(16f + dx * cos + dy * sin, 16f - dx * sin + dy * cos);
			};
		}

		// The eye: two curves, from the left corner over the top to the right corner and back underneath.
		private static Vector2[] Almond()
		{
			const int steps = 24;
			var pts = new List<Vector2>();
			Vector2 left = new Vector2(1, 16), right = new Vector2(31, 16);
			for (int i = 0; i < steps; i++)
				pts.Add(Bezier(left, new Vector2(16, 2), right, i / (float)steps));
			for (int i = 0; i < steps; i++)
				pts.Add(Bezier(right, new Vector2(16, 30), left, i / (float)steps));
			return pts.ToArray();
		}

		private static Vector2 Bezier(Vector2 a, Vector2 control, Vector2 b, float t)
		{
			float u = 1f - t;
			return u * u * a + 2f * u * t * control + t * t * b;
		}
	}
}
