using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace Augments
{
	// The Pod sprite, in every Pod skin and with every Pod upgrade. One place builds its colours so the HUD, the Pod pet that floats
	// beside players, the hub window and the Campaign page icons all agree. A skin keeps the sprite's light and dark areas and takes
	// the skin's colour. An upgrade lights a small coloured LED on each wing.
	//
	// The design follows the Pods of NieR: Automata: a dark, boxy body with a bevelled top edge, panel seams, a single lens in the
	// middle, two side arms with joints, a short antenna with a signal light, and a thruster underneath.
	internal static class PodSprites
	{
		public const int Width = 20;
		public const int Height = 18;

		// Where the middle of the lens is, and the point that sits on the Pod's position when it is drawn.
		public static readonly Vector2 Origin = new Vector2(10f, 9f);
		public static readonly Vector2 Lens = new Vector2(10f, 10.5f);
		public static readonly Vector2 Antenna = new Vector2(10f, 0.5f);
		public static readonly Vector2 Nozzle = new Vector2(10f, 18f);

		// Left arm (4) + body (12) + right arm (4). Letters are colours, see ColorOf.
		internal static readonly string[] Mask =
		{
			"    " + "     RR     " + "    ",
			"    " + "     AA     " + "    ",
			"    " + "     AA     " + "    ",
			"    " + " .......... " + "    ",
			"    " + ".HHHHHHHHHH." + "    ",
			" .. " + ".HBBBBBBBBS." + " .. ",
			".LL." + ".BBBBBBBBMS." + ".LL.",
			".LH." + ".DDDDDDDDDD." + ".HL.",
			".LLl" + ".BBBEEEEMMS." + "lLL.",
			".LLl" + ".BBBEeeEMMS." + "lLL.",
			".LLl" + ".BBBEWeEMMS." + "lLL.",
			".LL." + ".BBBEeeEMMS." + ".LL.",
			".LS." + ".BBBEEEEMMS." + ".SL.",
			".LL." + ".DDDDDDDDDD." + ".LL.",
			" .. " + ".SSGGGGGGSS." + " .. ",
			"    " + " .......... " + "    ",
			"    " + "    .TT.    " + "    ",
			"    " + "     TT     " + "    "
		};

		// The cells of the body a light band can pass over (the Glory to Mankind pod), and where its sparkles twinkle, in sprite cells.
		internal static readonly Vector2[] GlintCells = BuildGlintCells();
		internal static readonly Vector2[] SparkleSpots = { new Vector2(-2f, 0f), new Vector2(20f, 3f), new Vector2(2f, 17f), new Vector2(18f, 16f), new Vector2(10f, -2f), new Vector2(-3f, 10f) };

		private static Vector2[] BuildGlintCells()
		{
			var cells = new List<Vector2>();
			for (int y = 0; y < Height; y++)
			{
				for (int x = 0; x < Width; x++)
				{
					if ("BHMSAL".IndexOf(Mask[y][x]) >= 0)
						cells.Add(new Vector2(x, y));
				}
			}
			return cells.ToArray();
		}

		internal struct Palette
		{
			public Color Outline, Antenna, Highlight, Body, Mid, Shadow, Seam, Arm, ArmJoint, EyeOuter, EyeBright, EyeGlint, Accent, Signal, Thruster;
		}

		// White means the normal dark steel Pod; any other colour recolours it.
		internal static Palette MakePalette(Color skin)
		{
			var p = new Palette
			{
				Outline = new Color(10, 13, 20, 255),
				Antenna = new Color(150, 160, 178, 255),
				Highlight = new Color(206, 214, 228, 255),
				Body = new Color(128, 139, 156, 255),
				Mid = new Color(92, 102, 120, 255),
				Shadow = new Color(58, 66, 82, 255),
				Seam = new Color(30, 36, 48, 255),
				Arm = new Color(78, 88, 106, 255),
				ArmJoint = new Color(36, 42, 54, 255),
				EyeOuter = new Color(26, 40, 58, 255),
				EyeBright = new Color(70, 200, 250, 255),
				EyeGlint = new Color(245, 252, 255, 255),
				Accent = new Color(30, 118, 170, 255),
				Signal = new Color(226, 70, 58, 255),
				Thruster = new Color(14, 165, 233, 235)
			};

			if (skin == Color.White)
				return p;

			Color Metal(Color c)
			{
				float k = (c.R * 0.3f + c.G * 0.59f + c.B * 0.11f) / 255f * 1.6f;
				return new Color((int)Math.Min(255f, skin.R * k), (int)Math.Min(255f, skin.G * k), (int)Math.Min(255f, skin.B * k), 255);
			}

			p.Antenna = Metal(p.Antenna);
			p.Highlight = Metal(p.Highlight);
			p.Body = Metal(p.Body);
			p.Mid = Metal(p.Mid);
			p.Shadow = Metal(p.Shadow);
			p.Seam = Metal(p.Seam);
			p.Arm = Metal(p.Arm);
			p.ArmJoint = Metal(p.ArmJoint);
			p.EyeOuter = new Color((int)(skin.R * 0.28f), (int)(skin.G * 0.28f), (int)(skin.B * 0.28f), 255);
			p.EyeBright = skin;
			p.Accent = new Color((int)(skin.R * 0.6f), (int)(skin.G * 0.6f), (int)(skin.B * 0.6f), 255);
			p.Signal = skin;
			p.Thruster = new Color(skin.R, skin.G, skin.B, 235);
			return p;
		}

		// The palette for a skin: a hidden-ending style if it has one, otherwise the tint recolouring.
		internal static Palette MakePalette(Cosmetic.PodSkinInfo skin)
		{
			return string.IsNullOrEmpty(skin.Style) ? MakePalette(skin.Tint) : StylePalette(skin.Style);
		}

		// The skin's style name ("" for an ordinary tinted skin), by position in Cosmetic.PodSkins.
		internal static string StyleOf(int skinIndex)
		{
			skinIndex = Math.Max(0, Math.Min(skinIndex, Cosmetic.PodSkins.Length - 1));
			return Cosmetic.PodSkins[skinIndex].Style ?? "";
		}

		// The looks of the five ending skins (A pearl, B sticker, C hologram, D scarred, E glory). A colour with an alpha below 255 is see-through.
		private static Palette StylePalette(string style)
		{
			Palette p = MakePalette(Color.White);
			Color C(int r, int g, int b, int a = 255) => new Color(r, g, b, a);
			switch (style)
			{
				case "pearl":
					p.Highlight = C(252, 254, 255); p.Body = C(226, 233, 243); p.Mid = C(192, 202, 217); p.Shadow = C(152, 162, 180);
					p.Seam = C(112, 122, 142); p.Arm = C(202, 210, 224); p.ArmJoint = C(122, 132, 152); p.Antenna = C(200, 210, 225);
					p.EyeOuter = C(40, 60, 80); p.EyeBright = C(150, 235, 255); p.EyeGlint = C(255, 255, 255); p.Accent = C(120, 190, 220);
					p.Signal = C(130, 238, 255); p.Thruster = C(160, 232, 255, 235);
					break;
				case "sticker":
					// A plain grey pod with one yellow smiley sticker on its side (see Overlay).
					p.Highlight = C(190, 194, 200); p.Body = C(168, 172, 180); p.Mid = C(146, 150, 158); p.Shadow = C(120, 124, 132);
					p.Seam = C(96, 100, 108); p.Arm = C(156, 160, 168); p.ArmJoint = C(90, 94, 102); p.Antenna = C(176, 180, 188);
					p.EyeOuter = C(70, 74, 82); p.EyeBright = C(230, 232, 236); p.EyeGlint = C(255, 255, 255); p.Accent = C(130, 134, 142);
					p.Signal = C(176, 180, 188); p.Thruster = C(176, 180, 188, 235);
					break;
				case "hologram":
					// A teal projection: a little see-through, with dark scan lines (see StyleTweak).
					p.Outline = C(40, 170, 170);
					p.Highlight = C(170, 255, 245, 235); p.Body = C(60, 190, 190, 225); p.Mid = C(40, 150, 160, 225); p.Shadow = C(30, 110, 125, 225);
					p.Seam = C(24, 90, 105); p.Arm = C(50, 160, 170, 225); p.ArmJoint = C(30, 110, 125); p.Antenna = C(120, 230, 225);
					p.EyeOuter = C(10, 60, 70); p.EyeBright = C(220, 255, 250); p.EyeGlint = C(255, 255, 255); p.Accent = C(40, 150, 160);
					p.Signal = C(170, 255, 245); p.Thruster = C(100, 230, 225, 235);
					break;				case "scarred":
					p.Highlight = C(150, 130, 118); p.Body = C(110, 95, 88); p.Mid = C(84, 72, 66); p.Shadow = C(56, 48, 46);
					p.Seam = C(26, 20, 20); p.Arm = C(96, 82, 74); p.ArmJoint = C(40, 30, 28); p.Antenna = C(130, 112, 100);
					p.EyeOuter = C(60, 20, 20); p.EyeBright = C(255, 70, 60); p.EyeGlint = C(255, 200, 190); p.Accent = C(150, 70, 40);
					p.Signal = C(255, 60, 50); p.Thruster = C(255, 120, 60, 235);
					break;
				case "glory":
					p.Outline = C(44, 30, 8);
					p.Highlight = C(255, 248, 200); p.Body = C(238, 196, 84); p.Mid = C(190, 140, 40); p.Shadow = C(128, 86, 20);
					p.Seam = C(28, 18, 4); p.Arm = C(214, 166, 56); p.ArmJoint = C(60, 40, 8); p.Antenna = C(250, 220, 130);
					p.EyeOuter = C(24, 16, 4); p.EyeBright = C(255, 244, 190); p.EyeGlint = C(255, 255, 250); p.Accent = C(255, 226, 120);
					p.Signal = C(255, 246, 200); p.Thruster = C(255, 220, 120, 235);
					break;
			}
			return p;
		}

		// Pixels drawn on top of the sprite for a style (the cracks and the patch plate of the scarred pod), in sprite cells.
		private static readonly List<(int x, int y, Color c)> NoOverlay = new List<(int x, int y, Color c)>();
		private static List<(int x, int y, Color c)> scarredOverlay;
		private static List<(int x, int y, Color c)> stickerOverlay;

		// The smiley sticker: a yellow square on the left of the body with two eyes and a smile.
		private static List<(int x, int y, Color c)> BuildSticker()
		{
			var list = new List<(int x, int y, Color c)>();
			var yellow = new Color(251, 191, 36);
			var ink = new Color(17, 17, 17);
			for (int y = 9; y <= 11; y++)
				for (int x = 5; x <= 7; x++)
					list.Add((x, y, yellow));
			list.Add((5, 10, ink));
			list.Add((7, 10, ink));
			list.Add((6, 11, ink));
			return list;
		}

		internal static List<(int x, int y, Color c)> Overlay(string style)
		{
			if (style == "sticker")
				return stickerOverlay ??= BuildSticker();
			if (style != "scarred")
				return NoOverlay;
			if (scarredOverlay != null)
				return scarredOverlay;

			var list = new List<(int x, int y, Color c)>();
			var crack = new Color(26, 18, 16);
			foreach ((int x, int y) in new[] { (6, 5), (7, 6), (7, 7), (8, 8), (8, 9), (9, 10), (9, 11), (10, 12), (12, 4), (12, 5), (13, 6) })
				list.Add((x, y, crack));
			for (int y = 9; y <= 11; y++)
				for (int x = 13; x <= 14; x++)
					list.Add((x, y, new Color(111, 127, 148)));
			list.Add((13, 9, new Color(169, 184, 204)));
			list.Add((14, 11, new Color(74, 88, 108)));
			scarredOverlay = list;
			return list;
		}

		// The hologram's dark scan lines: every second row of the body is darker.
		private static Color StyleTweak(string style, int y, char ch, Color c)
		{
			if (style != "hologram" || (y & 1) != 0 || "BMSHL".IndexOf(ch) < 0)
				return c;
			return new Color((int)(c.R * 0.62f), (int)(c.G * 0.62f), (int)(c.B * 0.62f), c.A);
		}

		// The game blends with premultiplied colour, so a see-through pixel must have its colour scaled by its alpha.
		private static Color Premul(Color c) => c.A == 255 ? c : new Color((int)(c.R * c.A / 255f), (int)(c.G * c.A / 255f), (int)(c.B * c.A / 255f), (int)c.A);
		private static Color ColorOf(char c, in Palette p)
		{
			switch (c)
			{
				case '.': return p.Outline;
				case 'A': return p.Antenna;
				case 'H': return p.Highlight;
				case 'B': return p.Body;
				case 'M': return p.Mid;
				case 'S': return p.Shadow;
				case 'D': return p.Seam;
				case 'L': return p.Arm;
				case 'l': return p.ArmJoint;
				case 'E': return p.EyeOuter;
				case 'e': return p.EyeBright;
				case 'W': return p.EyeGlint;
				case 'G': return p.Accent;
				case 'R': return p.Signal;
				case 'T': return p.Thruster;
				default: return Color.Transparent;
			}
		}

		// One texture per skin and set of upgrades, built the first time it is needed (the graphics device is not ready while the mod
		// loads). `modules` is the colours of the upgrade LEDs, in slot order (at most three are shown).
		private static readonly Dictionary<long, Texture2D> textures = new Dictionary<long, Texture2D>();

		// A soft round glow (white, fading to nothing at the edge), tinted when drawn. Used behind the Glory to Mankind pod.
		private static Texture2D halo;

		internal static Texture2D Halo()
		{
			if (Main.dedServ || Main.graphics?.GraphicsDevice == null)
				return null;
			if (halo != null && !halo.IsDisposed)
				return halo;

			const int n = 48;
			var data = new Color[n * n];
			for (int y = 0; y < n; y++)
			{
				for (int x = 0; x < n; x++)
				{
					float dx = (x + 0.5f - n / 2f) / (n / 2f);
					float dy = (y + 0.5f - n / 2f) / (n / 2f);
					float d = (float)Math.Sqrt(dx * dx + dy * dy);
					float a = d >= 1f ? 0f : (1f - d) * (1f - d);
					data[y * n + x] = new Color(a, a, a, a); // premultiplied
				}
			}
			halo = new Texture2D(Main.graphics.GraphicsDevice, n, n);
			halo.SetData(data);
			return halo;
		}

		private static long KeyOf(int skinIndex, IReadOnlyList<Color> modules)
		{
			long key = skinIndex + 1;
			if (modules != null)
			{
				for (int i = 0; i < Math.Min(3, modules.Count); i++)
					key = key * 1000003L + modules[i].PackedValue;
			}
			return key;
		}

		internal static Texture2D Get(int skinIndex, IReadOnlyList<Color> modules = null)
		{
			if (Main.dedServ || Main.graphics?.GraphicsDevice == null)
				return null;

			skinIndex = Math.Max(0, Math.Min(skinIndex, Cosmetic.PodSkins.Length - 1));
			long key = KeyOf(skinIndex, modules);
			if (textures.TryGetValue(key, out Texture2D tex) && tex != null && !tex.IsDisposed)
				return tex;

			Cosmetic.PodSkinInfo info = Cosmetic.PodSkins[skinIndex];
			Palette palette = MakePalette(info);
			tex = new Texture2D(Main.graphics.GraphicsDevice, Width, Height);
			var data = new Color[Width * Height];
			for (int y = 0; y < Height; y++)
			{
				string row = Mask[y];
				for (int x = 0; x < Width; x++)
					data[y * Width + x] = Premul(StyleTweak(info.Style, y, row[x], ColorOf(row[x], palette)));
			}

			foreach ((int ox, int oy, Color oc) in Overlay(info.Style))
				data[oy * Width + ox] = Premul(oc);

			// Upgrade LEDs: a 2x2 light on each arm, one slot per upgrade (rows 6-7, 9-10 and 12-13), bright with a dark ring.
			if (modules != null)
			{
				for (int slot = 0; slot < Math.Min(3, modules.Count); slot++)
				{
					int y0 = 6 + slot * 3;
					Color led = modules[slot];
					Color bright = Color.Lerp(led, Color.White, 0.45f);
					foreach (int x0 in new[] { 1, 17 })
					{
						data[y0 * Width + x0] = bright;
						data[y0 * Width + x0 + 1] = led;
						data[(y0 + 1) * Width + x0] = led;
						data[(y0 + 1) * Width + x0 + 1] = led;
					}
				}
			}

			tex.SetData(data);
			textures[key] = tex;
			return tex;
		}

		// Draws the Pod into `box` as flat blocks (no texture needed), in a skin colour. Used for icons. The scale is a fraction of a
		// pixel per cell, rounded at the block edges, so any box size fills nicely without gaps.
		internal static void DrawBlocks(SpriteBatch sb, Rectangle box, Color skin) => DrawBlocks(sb, box, MakePalette(skin), "");

		// The same for a skin that may have an ending style (see StylePalette).
		internal static void DrawBlocks(SpriteBatch sb, Rectangle box, Cosmetic.PodSkinInfo skin) => DrawBlocks(sb, box, MakePalette(skin), skin.Style ?? "");

		private static void DrawBlocks(SpriteBatch sb, Rectangle box, Palette palette, string style)
		{
			float s = Math.Max(1f, Math.Min(box.Width / (float)Width, box.Height / (float)Height));
			if (s >= 2f)
				s = (float)Math.Floor(s); // whole pixels when there is room, so it stays crisp
			int ox = box.X + (int)Math.Round((box.Width - s * Width) / 2f);
			int oy = box.Y + (int)Math.Round((box.Height - s * Height) / 2f);
			for (int y = 0; y < Height; y++)
			{
				string row = Mask[y];
				int top = oy + (int)Math.Round(y * s);
				int bottom = oy + (int)Math.Round((y + 1) * s);
				int x = 0;
				while (x < Width)
				{
					char ch = row[x];
					if (ch == ' ')
					{
						x++;
						continue;
					}

					int x2 = x;
					while (x2 < Width && row[x2] == ch)
						x2++;

					int left = ox + (int)Math.Round(x * s);
					int right = ox + (int)Math.Round(x2 * s);
					HubDraw.Fill(sb, new Rectangle(left, top, Math.Max(1, right - left), Math.Max(1, bottom - top)), Premul(StyleTweak(style, y, ch, ColorOf(ch, palette))));
					x = x2;
				}
			}

			foreach ((int cx, int cy, Color oc) in Overlay(style))
			{
				int left = ox + (int)Math.Round(cx * s);
				int top = oy + (int)Math.Round(cy * s);
				HubDraw.Fill(sb, new Rectangle(left, top, Math.Max(1, ox + (int)Math.Round((cx + 1) * s) - left), Math.Max(1, oy + (int)Math.Round((cy + 1) * s) - top)), Premul(oc));
			}
		}

		// Frees the textures when the mod unloads. Disposing has to happen on the main thread.
		internal static void Unload()
		{
			var old = new List<Texture2D>(textures.Values);
			if (halo != null)
				old.Add(halo);
			halo = null;
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
	}
}
