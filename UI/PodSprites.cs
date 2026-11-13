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

			Palette palette = MakePalette(Cosmetic.PodSkins[skinIndex].Tint);
			tex = new Texture2D(Main.graphics.GraphicsDevice, Width, Height);
			var data = new Color[Width * Height];
			for (int y = 0; y < Height; y++)
			{
				string row = Mask[y];
				for (int x = 0; x < Width; x++)
					data[y * Width + x] = ColorOf(row[x], palette);
			}

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
		internal static void DrawBlocks(SpriteBatch sb, Rectangle box, Color skin)
		{
			Palette palette = MakePalette(skin);
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
					HubDraw.Fill(sb, new Rectangle(left, top, Math.Max(1, right - left), Math.Max(1, bottom - top)), ColorOf(ch, palette));
					x = x2;
				}
			}
		}
		// Frees the textures when the mod unloads. Disposing has to happen on the main thread.
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
	}
}
