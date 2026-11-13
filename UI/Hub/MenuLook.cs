using System;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace Augments
{
	// The colour schemes of the Automata title screen, chosen on the COSMETICS page.
	//
	// Every colour the home page draws with (AutomataHomeUIState.Ivory, Charcoal, Idle, Backing, Amber, Muted) is read from
	// the scheme that is selected here at draw time, so picking a scheme repaints the whole title screen at once.
	// The choice is one word in a small text file in the Terraria save folder (AugmentsMenuLook.txt) and is read once while
	// the mod loads. It is cosmetic only: nothing in the game's rules reads it.
	internal static class MenuLook
	{
		internal sealed class Scheme
		{
			public string Id;       // saved in the file
			public string Name;     // shown on the card
			public string Blurb;    // one line under the name
			public Color Ivory;     // main bright colour: highlights, borders, selected tab
			public Color Idle;      // normal text
			public Color Charcoal;  // dark text drawn on top of the bright colour
			public Color Backing;   // dark fill behind panels
			public Color Amber;     // currency and accents
			public Color Muted;     // small grey labels

			// The big panels (Archive, Cosmetics, the preview pages): fill, boxes on it, lines, text, accent and so on.
			public Color Panel, PanelAlt, Line, Text, Dim, Accent, OnAccent, Warn;
		}

		internal static readonly Scheme[] All =
		{
			new Scheme
			{
				Id = "standard", Name = "STANDARD", Blurb = "THE ORIGINAL CREAM AND CHARCOAL",
				Ivory = new Color(234, 229, 212), Idle = new Color(212, 206, 191), Charcoal = new Color(32, 34, 38),
				Backing = new Color(12, 13, 17), Amber = new Color(226, 160, 84), Muted = new Color(150, 146, 134),
				Panel = new Color(205, 199, 180), PanelAlt = new Color(190, 184, 164), Line = new Color(92, 86, 74),
				Text = new Color(66, 61, 52), Dim = new Color(116, 110, 96), Accent = new Color(72, 67, 58),
				OnAccent = new Color(214, 208, 189), Warn = new Color(172, 78, 48)
			},
			new Scheme
			{
				Id = "alert", Name = "ALERT", Blurb = "RED STATUS, ALL UNITS ON GUARD",
				Ivory = new Color(236, 150, 128), Idle = new Color(222, 176, 160), Charcoal = new Color(34, 22, 22),
				Backing = new Color(20, 11, 12), Amber = new Color(240, 196, 110), Muted = new Color(164, 130, 122),
				Panel = new Color(214, 190, 182), PanelAlt = new Color(200, 174, 166), Line = new Color(110, 70, 64),
				Text = new Color(70, 40, 38), Dim = new Color(130, 96, 90), Accent = new Color(92, 48, 44),
				OnAccent = new Color(224, 202, 196), Warn = new Color(188, 56, 40)
			},
			new Scheme
			{
				Id = "terminal", Name = "TERMINAL", Blurb = "GREEN PHOSPHOR, OLD BUNKER SCREEN",
				Ivory = new Color(150, 224, 160), Idle = new Color(158, 204, 166), Charcoal = new Color(14, 30, 18),
				Backing = new Color(8, 16, 10), Amber = new Color(226, 200, 100), Muted = new Color(110, 150, 118),
				Panel = new Color(12, 24, 16), PanelAlt = new Color(17, 34, 22), Line = new Color(52, 96, 64),
				Text = new Color(176, 230, 186), Dim = new Color(108, 150, 118), Accent = new Color(150, 224, 160),
				OnAccent = new Color(10, 24, 14), Warn = new Color(226, 200, 100)
			},
			new Scheme
			{
				Id = "protocol", Name = "PROTOCOL", Blurb = "COOL CYAN, THE POD'S OWN COLOURS",
				Ivory = new Color(138, 210, 238), Idle = new Color(168, 200, 216), Charcoal = new Color(12, 24, 34),
				Backing = new Color(8, 14, 22), Amber = new Color(238, 180, 96), Muted = new Color(116, 144, 160),
				Panel = new Color(10, 18, 28), PanelAlt = new Color(15, 27, 42), Line = new Color(48, 76, 100),
				Text = new Color(200, 226, 240), Dim = new Color(118, 150, 170), Accent = new Color(138, 210, 238),
				OnAccent = new Color(8, 18, 28), Warn = new Color(238, 180, 96)
			},
			new Scheme
			{
				Id = "void", Name = "VOID", Blurb = "DEEP VIOLET, CAMPAIGN 01 REWARD",
				Ivory = new Color(196, 160, 236), Idle = new Color(178, 160, 208), Charcoal = new Color(22, 14, 32),
				Backing = new Color(14, 9, 22), Amber = new Color(240, 190, 120), Muted = new Color(130, 114, 152),
				Panel = new Color(16, 10, 26), PanelAlt = new Color(24, 16, 38), Line = new Color(84, 60, 118),
				Text = new Color(222, 204, 244), Dim = new Color(136, 116, 164), Accent = new Color(196, 160, 236),
				OnAccent = new Color(18, 10, 28), Warn = new Color(240, 190, 120)
			},
			new Scheme
			{
				Id = "ember", Name = "EMBER", Blurb = "BURNT ORANGE, CAMPAIGN 01 REWARD",
				Ivory = new Color(240, 170, 96), Idle = new Color(214, 178, 140), Charcoal = new Color(36, 22, 12),
				Backing = new Color(22, 13, 8), Amber = new Color(255, 214, 120), Muted = new Color(160, 128, 100),
				Panel = new Color(26, 15, 9), PanelAlt = new Color(36, 22, 13), Line = new Color(112, 70, 40),
				Text = new Color(246, 214, 176), Dim = new Color(166, 128, 96), Accent = new Color(240, 170, 96),
				OnAccent = new Color(26, 14, 6), Warn = new Color(255, 214, 120)
			}
		};

		// How much to scale the default (cream) colours so they become this scheme's bright colour, per red/green/blue.
		// The title card, corner text and menu buttons are drawn in cream and multiplied by this (see Tint). It is also passed to\r\n		// AutomataMusic with each lyrics-draw call so the lyrics banner matches.
		public static Vector3 TintFactor
		{
			get
			{
				Color a = Current.Ivory;
				Color d = All[0].Ivory;
				return new Vector3(a.R / (float)d.R, a.G / (float)d.G, a.B / (float)d.B);
			}
		}

		// Scales a cream colour's red, green and blue so it becomes the selected scheme's bright colour; alpha is kept.
		// Used by the title-screen backdrop, title card, corner text and menu buttons.
		public static Color Tint(Color c)
		{
			if (selected == 0)
				return c;

			Vector3 f = TintFactor;
			return new Color(
				(int)Math.Min(255f, c.R * f.X),
				(int)Math.Min(255f, c.G * f.Y),
				(int)Math.Min(255f, c.B * f.Z),
				(int)c.A);
		}

		private static int selected;
		private static int frameSelected;

		// What each colour scheme costs in Data Fragments, in the same order as All. The first one is free.
		public static readonly int[] SchemePrices = { 0, 250, 250, 300, -1, -1 }; // -1: not for sale, an Operation reward

		public static Scheme Current => All[selected];
		public static int SelectedIndex => selected;

		public static string SchemeUnlockId(int index) => "scheme:" + All[index].Id;
		public static bool SchemeOwned(int index) => index == 0 || AccountStore.IsUnlocked(SchemeUnlockId(index));

		// --- Panel frames: the corner brackets drawn around every big panel on the title screen ---

		internal sealed class FrameStyle
		{
			public string Id;
			public string Name;
			public string Blurb;
		}

		internal static readonly FrameStyle[] Frames =
		{
			new FrameStyle { Id = "classic", Name = "CLASSIC", Blurb = "THIN CORNER BRACKETS" },
			new FrameStyle { Id = "heavy", Name = "HEAVY", Blurb = "LONG, THICK BRACKETS" },
			new FrameStyle { Id = "twin", Name = "TWIN", Blurb = "A BRACKET INSIDE A BRACKET" },
			new FrameStyle { Id = "circuit", Name = "CIRCUIT", Blurb = "BRACKETS WITH CIRCUIT NODES" }
		};

		public static readonly int[] FramePrices = { 0, 200, 350, -1 }; // -1: not for sale, an Operation reward

		public static int FrameIndex => frameSelected;
		public static string FrameUnlockId(int index) => "frame:" + Frames[index].Id;
		public static bool FrameOwned(int index) => index == 0 || AccountStore.IsUnlocked(FrameUnlockId(index));

		// Draws the selected frame around `panel`.
		public static void DrawFrame(Microsoft.Xna.Framework.Graphics.SpriteBatch sb, Rectangle panel, Color color)
		{
			switch (frameSelected)
			{
				case 1:
					HubDraw.Brackets(sb, new Rectangle(panel.X - 6, panel.Y - 6, panel.Width + 12, panel.Height + 12), color, 26, 4);
					break;
				case 2:
					HubDraw.Brackets(sb, new Rectangle(panel.X - 10, panel.Y - 10, panel.Width + 20, panel.Height + 20), color, 18, 2);
					HubDraw.Brackets(sb, new Rectangle(panel.X - 4, panel.Y - 4, panel.Width + 8, panel.Height + 8), color, 9, 2);
					break;
				case 3:
				{
					var outer = new Rectangle(panel.X - 6, panel.Y - 6, panel.Width + 12, panel.Height + 12);
					HubDraw.Brackets(sb, outer, color, 20, 2);
					// A small square node at each corner and in the middle of each side.
					foreach (Point node in new[] { new Point(outer.X, outer.Y), new Point(outer.Right, outer.Y), new Point(outer.X, outer.Bottom), new Point(outer.Right, outer.Bottom),
						new Point(outer.Center.X, outer.Y), new Point(outer.Center.X, outer.Bottom), new Point(outer.X, outer.Center.Y), new Point(outer.Right, outer.Center.Y) })
						HubDraw.Fill(sb, new Rectangle(node.X - 2, node.Y - 2, 5, 5), color);
					break;
				}
				default:
					HubDraw.Brackets(sb, new Rectangle(panel.X - 5, panel.Y - 5, panel.Width + 10, panel.Height + 10), color, 16, 2);
					break;
			}
		}

		// Reads the equipped scheme and frame from the account. Something that is equipped but not owned (a copied or edited
		// file) falls back to the free one. The account has to be loaded first.
		public static void Load()
		{
			selected = FindEquipped("scheme", i => All[i].Id, All.Length, SchemeOwned);
			frameSelected = FindEquipped("frame", i => Frames[i].Id, Frames.Length, FrameOwned);
		}

		private static int FindEquipped(string slot, Func<int, string> idOf, int count, Func<int, bool> owned)
		{
			string id = AccountStore.Equipped(slot, idOf(0));
			for (int i = 0; i < count; i++)
			{
				if (string.Equals(idOf(i), id, StringComparison.OrdinalIgnoreCase))
					return owned(i) ? i : 0;
			}
			return 0;
		}

		public static void SelectScheme(int index)
		{
			if (index < 0 || index >= All.Length || index == selected || !SchemeOwned(index))
				return;

			selected = index;
			AccountStore.Equip("scheme", All[index].Id);
		}

		public static void SelectFrame(int index)
		{
			if (index < 0 || index >= Frames.Length || index == frameSelected || !FrameOwned(index))
				return;

			frameSelected = index;
			AccountStore.Equip("frame", Frames[index].Id);
		}
	}
}