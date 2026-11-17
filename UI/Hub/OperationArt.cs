using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace Augments
{
	// Small pictures for the Operation: one icon per kind of reward, the emblems, and the Data Fragments crystal. Everything is flat
	// rectangles and filled shapes, like the rest of the title screen, so there are no textures to load. Shapes are filled with a
	// simple scanline routine (Poly), which is cheap at these sizes.
	internal static class OperationArt
	{
		// --- Drawing helpers ---

		private static Color Shade(Color c, float k) =>
			new Color(Math.Min(255, (int)(c.R * k)), Math.Min(255, (int)(c.G * k)), Math.Min(255, (int)(c.B * k)), c.A);

		private static Color Fade(Color c, float a) => new Color((int)(c.R * a), (int)(c.G * a), (int)(c.B * a), (int)(c.A * a));

		// Fills any simple polygon, one pixel row at a time (even-odd rule).
		private static void Poly(SpriteBatch sb, ReadOnlySpan<Vector2> p, Color c)
		{
			float minY = float.MaxValue;
			float maxY = float.MinValue;
			for (int i = 0; i < p.Length; i++)
			{
				minY = Math.Min(minY, p[i].Y);
				maxY = Math.Max(maxY, p[i].Y);
			}

			Span<float> xs = stackalloc float[16];
			for (int y = (int)Math.Floor(minY); y < (int)Math.Ceiling(maxY); y++)
			{
				float yc = y + 0.5f;
				int n = 0;
				for (int i = 0; i < p.Length; i++)
				{
					Vector2 a = p[i];
					Vector2 b = p[(i + 1) % p.Length];
					if (((a.Y <= yc && b.Y > yc) || (b.Y <= yc && a.Y > yc)) && n < 16)
						xs[n++] = a.X + (yc - a.Y) * (b.X - a.X) / (b.Y - a.Y);
				}

				for (int i = 1; i < n; i++) // tiny insertion sort
				{
					float v = xs[i];
					int j = i - 1;
					while (j >= 0 && xs[j] > v)
					{
						xs[j + 1] = xs[j];
						j--;
					}
					xs[j + 1] = v;
				}

				for (int i = 0; i + 1 < n; i += 2)
				{
					int x0 = (int)Math.Round(xs[i]);
					int x1 = (int)Math.Round(xs[i + 1]);
					if (x1 > x0)
						HubDraw.Fill(sb, new Rectangle(x0, y, x1 - x0, 1), c);
				}
			}
		}

		private static void Tri(SpriteBatch sb, Vector2 a, Vector2 b, Vector2 c, Color color)
		{
			Poly(sb, stackalloc Vector2[] { a, b, c }, color);
		}

		// A thin line between two points, drawn as small steps.
		private static void Line(SpriteBatch sb, Vector2 a, Vector2 b, Color c)
		{
			int steps = Math.Max(1, (int)Math.Max(Math.Abs(b.X - a.X), Math.Abs(b.Y - a.Y)));
			for (int i = 0; i <= steps; i++)
			{
				Vector2 p = Vector2.Lerp(a, b, i / (float)steps);
				HubDraw.Fill(sb, new Rectangle((int)Math.Round(p.X), (int)Math.Round(p.Y), 1, 1), c);
			}
		}

		// A cut gem: four facets of different brightness, a bright edge on its top left and a dark one on its lower right.
		private static void Gem(SpriteBatch sb, Vector2 c, float s, Color baseColor)
		{
			Vector2 top = c + new Vector2(0f, -0.9f * s);
			Vector2 bottom = c + new Vector2(0f, 0.9f * s);
			Vector2 left = c + new Vector2(-0.58f * s, -0.1f * s);
			Vector2 right = c + new Vector2(0.58f * s, -0.1f * s);
			Vector2 mid = c + new Vector2(0f, -0.1f * s);

			Tri(sb, top, left, mid, Shade(baseColor, 1.25f));
			Tri(sb, top, right, mid, Shade(baseColor, 0.98f));
			Tri(sb, left, bottom, mid, Shade(baseColor, 0.74f));
			Tri(sb, right, bottom, mid, Shade(baseColor, 0.5f));

			Line(sb, top, left, Color.White * 0.55f);
			Line(sb, right, bottom, Shade(baseColor, 0.3f));
		}

		// A four point sparkle.
		internal static void Sparkle(SpriteBatch sb, Vector2 c, float s, Color color)
		{
			int arm = Math.Max(1, (int)Math.Round(s));
			HubDraw.Fill(sb, new Rectangle((int)c.X - arm, (int)c.Y, arm * 2 + 1, 1), color);
			HubDraw.Fill(sb, new Rectangle((int)c.X, (int)c.Y - arm, 1, arm * 2 + 1), color);
			if (arm > 2)
			{
				HubDraw.Fill(sb, new Rectangle((int)c.X - 1, (int)c.Y - 1, 3, 3), color);
			}
		}

		// --- Data Fragments ---

		// The Data Fragments crystal: a faceted shard with two smaller ones drifting beside it and a sparkle that twinkles.
		public static void Fragment(SpriteBatch sb, Rectangle box, Color baseColor)
		{
			float t = HubClock.Seconds;
			Vector2 c = box.Center.ToVector2();
			float s = Math.Min(box.Width, box.Height) / 2f;

			// A soft glow behind it that breathes.
			float glow = 0.12f + 0.08f * (float)Math.Sin(t * 2.2f);
			HubDraw.Diamond(sb, c, s * 0.9f, baseColor * glow);

			float drift = (float)Math.Sin(t * 1.8f) * 0.07f * s;
			Gem(sb, c + new Vector2(-0.82f * s, 0.5f * s + drift), 0.34f * s, baseColor);
			Gem(sb, c + new Vector2(0.84f * s, -0.38f * s - drift), 0.28f * s, baseColor);
			Gem(sb, c + new Vector2(0f, -0.04f * s), 0.78f * s, baseColor);

			float twinkle = Math.Max(0f, (float)Math.Sin(t * 3.4f));
			if (twinkle > 0.05f)
				Sparkle(sb, c + new Vector2(0.55f * s, -0.72f * s), 0.3f * s * twinkle, Color.White * (0.5f + 0.5f * twinkle));
		}

		// The same crystal, small, for labels and the currency box.
		public static void FragmentMini(SpriteBatch sb, Vector2 center, float size, Color baseColor)
		{
			Gem(sb, center, size, baseColor);
		}

		// --- Emblems ---

		// Emblem icons (Profile badges, the Cosmetics page, the title screen). They are drawn in the same stencil style as the plugin class
		// icons (see ClassIcons) so the two match. The box is a square and the id is an emblem id from Cosmetic.Emblems.
		public static void Emblem(SpriteBatch sb, string id, Rectangle box, Color c)
		{
			Texture2D tex = ClassIcons.GetEmblem(id);
			if (tex == null)
				return;

			int size = Math.Min(box.Width, box.Height);
			sb.Draw(tex, new Rectangle(box.Center.X - size / 2, box.Center.Y - size / 2, size, size), c);
		}
		// A badge behind an emblem: an octagon with a rim, so it looks like something you wear.
		private static void Badge(SpriteBatch sb, Rectangle box, Color rim, Color fill)
		{
			Vector2 m = box.Center.ToVector2();
			float r = Math.Min(box.Width, box.Height) / 2f;
			var pts = new Vector2[8];
			var inner = new Vector2[8];
			for (int i = 0; i < 8; i++)
			{
				float angle = MathHelper.Pi / 8f + i * MathHelper.PiOver4;
				var d = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle));
				pts[i] = m + d * r;
				inner[i] = m + d * (r - 3f);
			}
			Poly(sb, pts, rim);
			Poly(sb, inner, fill);
		}

		// --- Reward icons ---

		// How many chevrons a title wears: its rank.
		private static int TitleRank(string id)
		{
			for (int i = 0; i < Cosmetic.Titles.Length; i++)
			{
				if (Cosmetic.Titles[i][0] == id)
					return i + 1;
			}
			return 1;
		}

		// A picture of a reward, for a tier cell or the home banner. The shape depends on the kind.
		public static void RewardIcon(SpriteBatch sb, OperationReward reward, Rectangle box, Color fade)
		{
			float a = fade.A / 255f;
			Vector2 m = box.Center.ToVector2();
			float s = Math.Min(box.Width, box.Height) / 2f;

			switch (reward.Kind)
			{
				case RewardKind.Fragments:
					Fragment(sb, box, Fade(MenuLook.Current.Amber, a));
					break;

				case RewardKind.Scheme:
					SchemeCard(sb, box, FindScheme(reward.Id));
					break;

				case RewardKind.Frame:
				{
					var inner = new Rectangle(box.X + box.Width / 4, box.Y + box.Height / 4, box.Width / 2, box.Height / 2);
					HubDraw.Fill(sb, inner, HubTheme.PanelAlt);
					HubDraw.Border(sb, inner, HubTheme.LineSoft);
					FrameSample(sb, reward.Id, inner, HubTheme.Accent);
					break;
				}

				case RewardKind.PodSkin:
				{
					AugmentAdvisoryHUD.DrawPodIcon(sb, box, Cosmetic.FindPodSkin(reward.Id));
					break;
				}

				case RewardKind.Title:
				{
					// A title earned from a hidden ending is a diamond with the ending's letter.
						int ending = Endings.EndingOfTitle(reward.Id);
						if (ending >= 0)
						{
							EndingMark(sb, box, (char)('A' + ending));
							break;
						}

						// Military rank chevrons: one more for each title, and a star above the highest.
					int rank = TitleRank(reward.Id);
					Color c = HubTheme.Accent;
					float w = 0.8f * s;
					float th = 0.22f * s;
					float step = 0.3f * s;
					float top = m.Y - (rank - 1) * step * 0.5f - 0.2f * s;
					for (int i = 0; i < Math.Min(rank, 5); i++)
					{
						float y = top + i * step;
						Span<Vector2> chevron = stackalloc Vector2[]
						{
							new Vector2(m.X - w, y), new Vector2(m.X, y + 0.45f * s), new Vector2(m.X + w, y),
							new Vector2(m.X + w, y + th), new Vector2(m.X, y + 0.45f * s + th), new Vector2(m.X - w, y + th)
						};
						Poly(sb, chevron, i % 2 == 0 ? c : Shade(c, 0.75f));
					}
					if (rank >= 6)
						Emblem(sb, "star", new Rectangle((int)(m.X - 0.3f * s), (int)(m.Y - s * 0.95f), (int)(0.6f * s), (int)(0.6f * s)), HubTheme.Warn);
					break;
				}

				default: // emblem: on a badge
				{
					Badge(sb, box, HubTheme.Accent, HubTheme.PanelAlt);
					var inner = new Rectangle(box.X + box.Width / 5, box.Y + box.Height / 5, box.Width * 3 / 5, box.Height * 3 / 5);
					Emblem(sb, reward.Id, inner, HubTheme.Accent);
					break;
				}
			}
		}

		// A diamond plate with an ending's letter, the picture of an ending title.
		private static void EndingMark(SpriteBatch sb, Rectangle box, char letter)
		{
			Vector2 m = box.Center.ToVector2();
			float s = Math.Min(box.Width, box.Height) / 2f * 0.9f;
			Span<Vector2> outer = stackalloc Vector2[] { new Vector2(m.X, m.Y - s), new Vector2(m.X + s, m.Y), new Vector2(m.X, m.Y + s), new Vector2(m.X - s, m.Y) };
			Span<Vector2> inner = stackalloc Vector2[] { new Vector2(m.X, m.Y - s * 0.82f), new Vector2(m.X + s * 0.82f, m.Y), new Vector2(m.X, m.Y + s * 0.82f), new Vector2(m.X - s * 0.82f, m.Y) };
			Poly(sb, outer, HubTheme.Accent);
			Poly(sb, inner, HubTheme.PanelAlt);

			float scale = Math.Max(0.5f, s / 14f);
			string text = letter.ToString();
			HubDraw.Text(sb, text, new Vector2(m.X - HubDraw.Width(text, scale) / 2f, m.Y - HubDraw.CapMid(scale)), HubTheme.Accent, scale);
		}
		// A small mock-up of the title screen in a colour scheme.
		private static void SchemeCard(SpriteBatch sb, Rectangle box, MenuLook.Scheme s)
		{
			HubDraw.Fill(sb, box, new Color(6, 8, 14));
			HubDraw.Border(sb, box, HubTheme.Line);

			var bar = new Rectangle(box.X + 5, box.Y + 5, box.Width - 10, Math.Max(6, box.Height / 8));
			HubDraw.Fill(sb, bar, s.Backing);
			HubDraw.Border(sb, bar, s.Ivory * 0.6f);
			HubDraw.Fill(sb, new Rectangle(bar.X + 2, bar.Y + 2, bar.Width / 4, bar.Height - 4), s.Ivory);

			var panel = new Rectangle(box.X + 5, bar.Bottom + 5, box.Width - 10, box.Height - (bar.Bottom - box.Y) - 24);
			HubDraw.Fill(sb, panel, s.Panel);
			HubDraw.Border(sb, panel, s.Line);
			HubDraw.Fill(sb, new Rectangle(panel.X + 4, panel.Y + 4, panel.Width / 2, 4), s.Accent);
			HubDraw.Fill(sb, new Rectangle(panel.X + 4, panel.Y + 12, panel.Width - 8, 2), s.Dim);
			HubDraw.Fill(sb, new Rectangle(panel.X + 4, panel.Y + 18, panel.Width * 2 / 3, 2), s.Dim);

			var foot = new Rectangle(box.X + 5, panel.Bottom + 4, box.Width - 10, 8);
			HubDraw.Fill(sb, foot, s.Backing);
			HubDraw.Fill(sb, new Rectangle(foot.X + 2, foot.Y + 2, 10, 4), s.Amber);
		}

		// The brackets of a panel frame, around `inner`.
		private static void FrameSample(SpriteBatch sb, string id, Rectangle inner, Color c)
		{
			switch (id)
			{
				case "heavy":
					HubDraw.Brackets(sb, new Rectangle(inner.X - 5, inner.Y - 5, inner.Width + 10, inner.Height + 10), c, 14, 3);
					break;
				case "twin":
					HubDraw.Brackets(sb, new Rectangle(inner.X - 8, inner.Y - 8, inner.Width + 16, inner.Height + 16), c, 10, 2);
					HubDraw.Brackets(sb, new Rectangle(inner.X - 3, inner.Y - 3, inner.Width + 6, inner.Height + 6), c, 5, 1);
					break;
				case "circuit":
				{
					var outer = new Rectangle(inner.X - 5, inner.Y - 5, inner.Width + 10, inner.Height + 10);
					HubDraw.Brackets(sb, outer, c, 12, 2);
					foreach (Point node in new[] { new Point(outer.X, outer.Y), new Point(outer.Right, outer.Y), new Point(outer.X, outer.Bottom), new Point(outer.Right, outer.Bottom),
						new Point(outer.Center.X, outer.Y), new Point(outer.Center.X, outer.Bottom), new Point(outer.X, outer.Center.Y), new Point(outer.Right, outer.Center.Y) })
						HubDraw.Fill(sb, new Rectangle(node.X - 2, node.Y - 2, 4, 4), c);
					break;
				}
				default:
					HubDraw.Brackets(sb, new Rectangle(inner.X - 4, inner.Y - 4, inner.Width + 8, inner.Height + 8), c, 9, 2);
					break;
			}
		}

		private static MenuLook.Scheme FindScheme(string id)
		{
			foreach (MenuLook.Scheme s in MenuLook.All)
			{
				if (s.Id == id)
					return s;
			}
			return MenuLook.All[0];
		}
	}
}
