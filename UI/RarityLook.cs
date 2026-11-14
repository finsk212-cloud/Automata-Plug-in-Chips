using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;

namespace Augments
{
	// How the four plugin tiers look, in one place. Everything that shows a tier (plugin cards, the detail panel, tooltips, the choice
	// popup, the Gacha, the shop, the Archive) reads from here, so the whole mod agrees.
	//
	// There can be several tier styles. Only the first, the NieR one, is switched on for now; the others are kept here for later (the
	// shop and the Campaign), and `Style` is the one place that picks which is used.
	internal enum TierStyle
	{
		Nier, // the base look: the Cosmetics colour scheme, flat, with rank shown by marks (see below)
		Ice   // animated: Epic drifts through ice colours, Legendary cycles faster with a ring and a circling light
	}

	// The DESIGN of the tier borders, chosen on the COSMETICS page. It changes how the borders are built, never their colours: those always
// come from the colour scheme.
internal enum TierDesign
{
	Nier, // the default: thin line, corner brackets, double border, cream plate
	Holo,   // rounded corners, clean flat lines (the glow is a separate unlock for later)
	Shadow, // a hard block shadow behind the card that gets deeper with the tier
	Folder  // a file folder: the tab on top gets wider with the tier
}

internal static class RarityLook
	{
		public static TierStyle Style = TierStyle.Nier;

		// Set for a moment by the Cosmetics page to draw a sample of a design that is not the equipped one.
		internal static TierDesign? SampleDesign;
		public static TierDesign Design => SampleDesign ?? (Enum.IsDefined(typeof(TierDesign), MenuLook.TierDesignIndex) ? (TierDesign)MenuLook.TierDesignIndex : TierDesign.Nier);
		public static bool Shadow => Style == TierStyle.Nier && Design == TierDesign.Shadow;
		public static bool Folder => Style == TierStyle.Nier && Design == TierDesign.Folder;
		// Any design other than the default NieR one (they draw their own frame and skip the NieR corner marks and pips).
		public static bool CustomDesign => Style == TierStyle.Nier && Design != TierDesign.Nier;
		public static bool Holo => Style == TierStyle.Nier && Design == TierDesign.Holo;

		// --- The NieR style -------------------------------------------------------------------------------------------------------
		// NieR: Automata's menus have almost no colour: they are paper tones, and importance is shown by shape. So the tiers follow the
		// colour scheme chosen on the COSMETICS page and get brighter with rank, and each rank also has its own mark:
		//
		//   Common     the dimmest tone, a thin border, no mark
		//   Rare       a brighter tone, a full border, one pip along the top edge
		//   Epic       the brightest tone, a full border with a second line inside it (double border), two pips
		//   Legendary  the scheme's amber accent, and the card turns into a bright plate with dark text and fine stripes, three pips
		//
		// Nothing moves, so it is calm, and it still reads without relying on colour.

		private static Color NierCommon => Color.Lerp(MenuLook.Current.Muted, MenuLook.Current.Idle, 0.45f);
		private static Color NierRare => MenuLook.Current.Idle;
		private static Color NierEpic => MenuLook.Current.Ivory;
		private static Color NierLegendary => MenuLook.Current.Amber;

		// Whether a card of this tier is drawn as a bright plate with dark text (Legendary, in the NieR style).
		public static bool Inverted(AugmentRarity rarity) => Style == TierStyle.Nier && rarity == AugmentRarity.Legendary;

		// The plate and the colour of the text and icon on it.
		public static Color Plate => MenuLook.Current.Ivory;
		public static Color OnPlate => MenuLook.Current.Charcoal;
		// The scheme's amber, a little darker, for the frame and band on a Legendary plate (the plain amber is a touch too bright on a light plate).
		public static Color PlateAccent => Darken(MenuLook.Current.Amber, 0.86f);

		// How many pips a tier has along the top edge of its card.
		public static int Pips(AugmentRarity rarity)
		{
			switch (rarity)
			{
				case AugmentRarity.Rare:
					return 1;
				case AugmentRarity.Epic: return 2;
				case AugmentRarity.Legendary: return 3;
				default: return 0;
			}
		}

		// The NieR style's marks for a card: the pips, and for Epic the second border line inside the first. Call it after the card's
		// border is drawn. `colour` is the border colour (the pips use it too, or the dark text colour on a Legendary plate).
		public static void DrawTierMarks(SpriteBatch sb, Rectangle rect, AugmentRarity rarity, Color colour)
		{
			if (Style != TierStyle.Nier)
				return;

			Texture2D px = TextureAssets.MagicPixel.Value;

			if (rarity == AugmentRarity.Epic || rarity == AugmentRarity.Legendary)
			{
				Color inner = (rarity == AugmentRarity.Legendary ? OnPlate : colour) * 0.55f;
				var r = new Rectangle(rect.X + 4, rect.Y + 4, rect.Width - 8, rect.Height - 8);
				sb.Draw(px, new Rectangle(r.X, r.Y, r.Width, 1), inner);
				sb.Draw(px, new Rectangle(r.X, r.Bottom - 1, r.Width, 1), inner);
				sb.Draw(px, new Rectangle(r.X, r.Y, 1, r.Height), inner);
				sb.Draw(px, new Rectangle(r.Right - 1, r.Y, 1, r.Height), inner);
			}

			int n = Pips(rarity);
			Color pip = rarity == AugmentRarity.Legendary ? OnPlate : colour;
			// Right-aligned (not centred), so the pips never sit under the NEW tab on the left.
			for (int i = 0; i < n; i++)
			{
				int x = rect.Right - 10 - (n - 1 - i) * 6 - 3;
				sb.Draw(px, new Rectangle(x, rect.Y + 8, 3, 3), pip); // below the border and the inner line, not on them
			}
		}

		// Draws coloured-tag text. Dark text on the cream plate has to be drawn WITHOUT the black drop shadow the game normally adds, or each
		// letter gets a smudged black outline and becomes hard to read.
		public static void DrawCoded(SpriteBatch sb, ReLogic.Graphics.DynamicSpriteFont font, string text, Vector2 pos, Color color, Vector2 scale, bool shadow)
		{
			if (shadow)
			{
				ModText.DrawCoded(sb, font, text, pos, color, 0f, Vector2.Zero, scale);
				return;
			}

			if (ModText.UsePixel(font, scale.X))
			{
				ModText.DrawPlain(sb, font, text, pos, color, scale);
				return;
			}

			// No shadow: draw the text in pieces ourselves. [c/RRGGBB:text] tags change the colour of their piece; everything else
			// (including things like "[Z]") is plain text. This does not use the game's own no-shadow call, which did not read the tags.
			float alpha = color.A / 255f;
			Vector2 p = pos;
			int last = 0;
			foreach (System.Text.RegularExpressions.Match m in ColourTag.Matches(text ?? ""))
			{
				if (m.Index > last)
					DrawPiece(sb, font, text.Substring(last, m.Index - last), ref p, color, scale);

				int v = Convert.ToInt32(m.Groups[1].Value, 16);
				Color tagColor = new Color((v >> 16) & 255, (v >> 8) & 255, v & 255) * alpha;
				DrawPiece(sb, font, m.Groups[2].Value, ref p, tagColor, scale);
				last = m.Index + m.Length;
			}

			if (text != null && last < text.Length)
				DrawPiece(sb, font, text.Substring(last), ref p, color, scale);
		}

		private static readonly System.Text.RegularExpressions.Regex ColourTag =
			new System.Text.RegularExpressions.Regex(@"\[c/([0-9A-Fa-f]{6}):([^\]]*)\]", System.Text.RegularExpressions.RegexOptions.Compiled);

		private static void DrawPiece(SpriteBatch sb, ReLogic.Graphics.DynamicSpriteFont font, string piece, ref Vector2 p, Color color, Vector2 scale)
		{
			if (string.IsNullOrEmpty(piece))
				return;

			ReLogic.Graphics.DynamicSpriteFontExtensionMethods.DrawString(sb, font, piece, p, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
			p.X += font.MeasureString(piece).X * scale.X;
		}
		// --- The Holo glass design: rounded corners, and a soft glow that grows with the tier ------------------------------------------
		//   Common     a thin rounded line
		//   Rare       a rounded line with a second thin line inside it
		//   Epic       a thicker line with a thin line inside it (and a soft glow, when HoloGlow is on)
		//   Legendary  a rounded cream plate with an amber line (and a stronger glow, when HoloGlow is on)

		// --- Hard shadow and Folder tabs ---------------------------------------------------------------------------------------------
		//   Hard shadow  a solid block shadow behind the card, offset down and right, that gets bigger and stronger with the tier
		//   Folder tabs  a file folder: the tab on top gets wider with the tier (none, short, half, long)

		private static Color LineOf(AugmentRarity rarity, bool hovered)
		{
			Color col = Of(rarity);
			return rarity == AugmentRarity.Legendary ? (hovered ? MenuLook.Current.Amber : PlateAccent) : (hovered ? Light(col) : col);
		}

		private static int BoxThickness(AugmentRarity rarity) => rarity == AugmentRarity.Epic || rarity == AugmentRarity.Legendary ? 3 : 2;

		private static int ShadowOffset(AugmentRarity rarity)
		{
			switch (rarity)
			{
				case AugmentRarity.Rare: return 4;
				case AugmentRarity.Epic: return 5;
				case AugmentRarity.Legendary: return 6;
				default: return 3;
			}
		}

		private static Color ShadowColor(AugmentRarity rarity)
		{
			Color c = Of(rarity);
			switch (rarity)
			{
				case AugmentRarity.Rare: return c * 0.45f;
				case AugmentRarity.Epic: return c * 0.7f;
				case AugmentRarity.Legendary: return MenuLook.Current.Amber;
				default: return c * 0.22f;
			}
		}

		// The shadow behind `rect`: only the two strips that stick out to the right and below (the card covers the rest).
		private static void ShadowStrips(SpriteBatch sb, Rectangle rect, AugmentRarity rarity)
		{
			Texture2D px = TextureAssets.MagicPixel.Value;
			int o = ShadowOffset(rarity);
			Color c = ShadowColor(rarity);
			sb.Draw(px, new Rectangle(rect.Right, rect.Y + o, o, rect.Height), c);
			sb.Draw(px, new Rectangle(rect.X + o, rect.Bottom, rect.Width - o, o), c);
		}

		// The border of a card for these designs: 2px, 3px with a thin line inside for Epic, and a dark thin line inside the Legendary plate.
		private static void LinedBox(SpriteBatch sb, Rectangle rect, AugmentRarity rarity, bool hovered)
		{
			Color line = LineOf(rarity, hovered);
			HubDraw.Border(sb, rect, rarity == AugmentRarity.Common ? line * 0.75f : line, BoxThickness(rarity));
			if (rarity == AugmentRarity.Epic)
				HubDraw.Border(sb, new Rectangle(rect.X + 5, rect.Y + 5, rect.Width - 10, rect.Height - 10), line * 0.55f, 1);
			else if (rarity == AugmentRarity.Legendary)
				HubDraw.Border(sb, new Rectangle(rect.X + 5, rect.Y + 5, rect.Width - 10, rect.Height - 10), OnPlate * 0.4f, 1);
		}

		private const int FolderTabHeight = 8;

		private static float FolderTabFraction(AugmentRarity rarity)
		{
			switch (rarity)
			{
				case AugmentRarity.Rare: return 0.22f;
				case AugmentRarity.Epic: return 0.45f;
				case AugmentRarity.Legendary: return 0.7f;
				default: return 0f; // Common has no tab
			}
		}

		// The folder tab, sticking up above `body`. `fill` is the colour behind it (the card's own), used to open the tab into the body.
		private static void DrawFolderTab(SpriteBatch sb, Rectangle body, AugmentRarity rarity, Color line, int th, Color fill)
		{
			int tw = (int)(body.Width * FolderTabFraction(rarity));
			if (tw <= 0)
				return;

			Texture2D px = TextureAssets.MagicPixel.Value;
			int top = body.Y - FolderTabHeight;
			int slant = FolderTabHeight / 2;
			for (int i = 0; i < FolderTabHeight + th; i++) // the fill follows the slanted edge, row by row
				sb.Draw(px, new Rectangle(body.X, top + i, tw + Math.Min(i, FolderTabHeight) / 2, 1), fill);
			sb.Draw(px, new Rectangle(body.X, top, tw, th), line);
			sb.Draw(px, new Rectangle(body.X, top, th, FolderTabHeight + th), line);
			for (int i = 0; i < FolderTabHeight; i++)
				sb.Draw(px, new Rectangle(body.X + tw + i / 2, top + i, th, 1), line);
			// Open the tab into the body: remove the body's top edge under it.
			sb.Draw(px, new Rectangle(body.X + th, body.Y, tw + slant - th, th), fill);
		}

		// Where a card's own content (icon, name) goes inside the element, for designs that take some of the room for themselves.
		public static Rectangle ContentRect(Rectangle rect, AugmentRarity rarity)
		{
			if (Shadow)
			{
				int o = ShadowOffset(rarity);
				return new Rectangle(rect.X, rect.Y, rect.Width - o, rect.Height - o);
			}
			if (Folder)
				return new Rectangle(rect.X, rect.Y + FolderTabHeight, rect.Width, rect.Height - FolderTabHeight);
			return rect;
		}

		// A whole plugin card (fill and frame) in a design other than the default, for the plugin grid. `rect` is the element; everything
		// is drawn inside it.
		public static void DrawDesignCard(SpriteBatch sb, Rectangle rect, AugmentRarity rarity, Color chassis, bool hovered, bool selected, Color selectColor)
		{
			Texture2D px = TextureAssets.MagicPixel.Value;
			bool plate = rarity == AugmentRarity.Legendary;
			Rectangle body = ContentRect(rect, rarity);

			switch (Design)
			{
				case TierDesign.Holo:
				{
					int rad = HoloRadius(rect);
					if (plate)
						DrawPlate(sb, rect);
					else
						RoundFill(sb, rect, chassis, rad);
					if (selected && !plate)
						RoundFill(sb, rect, selectColor * 0.10f, rad);
					DrawHoloFrame(sb, rect, rarity, hovered);
					if (selected)
						RoundBorder(sb, rect, selectColor, 4, rad);
					break;
				}

				case TierDesign.Shadow:
				{
					ShadowStrips(sb, body, rarity);
					if (plate)
						DrawPlate(sb, body);
					else
						sb.Draw(px, body, chassis);
					if (selected && !plate)
						sb.Draw(px, body, selectColor * 0.10f);
					LinedBox(sb, body, rarity, hovered);
					if (selected)
						HubDraw.Border(sb, body, selectColor, 4);
					break;
				}

				case TierDesign.Folder:
				{
					Color tabFill = plate ? Plate : chassis;
					if (plate)
						DrawPlate(sb, body);
					else
						sb.Draw(px, body, chassis);
					if (selected && !plate)
						sb.Draw(px, body, selectColor * 0.10f);
					LinedBox(sb, body, rarity, hovered);
					DrawFolderTab(sb, body, rarity, LineOf(rarity, hovered), BoxThickness(rarity), tabFill);
					if (selected)
					{
						HubDraw.Border(sb, body, selectColor, 4);
						DrawFolderTab(sb, body, rarity, selectColor, 4, tabFill);
					}
					break;
				}
			}
		}
		// The soft glow around Epic and Legendary is switched OFF: it is being kept as an exclusive unlock for later. Turn this on (or tie it to
		// an unlock) and the glow comes back; nothing else needs to change.
		public static bool HoloGlow = false;

		public static int HoloRadius(Rectangle r) => Math.Clamp(Math.Min(r.Width, r.Height) / 12, 4, 9);

		// How many pixels in from the edge a row of a quarter circle (radius `rad`) starts: row 0 is the very edge.
		private static int RunStart(int rad, int row)
		{
			double dy = rad - row - 0.5;
			double dx = Math.Sqrt(Math.Max(0.0, rad * (double)rad - dy * dy));
			return Math.Max(0, (int)Math.Ceiling(rad - dx - 0.5));
		}

		// A filled rectangle with rounded corners.
		public static void RoundFill(SpriteBatch sb, Rectangle r, Color c, int rad)
		{
			Texture2D px = TextureAssets.MagicPixel.Value;
			rad = Math.Max(1, Math.Min(rad, Math.Min(r.Width, r.Height) / 2));
			sb.Draw(px, new Rectangle(r.X + rad, r.Y, r.Width - rad * 2, r.Height), c);
			sb.Draw(px, new Rectangle(r.X, r.Y + rad, rad, r.Height - rad * 2), c);
			sb.Draw(px, new Rectangle(r.Right - rad, r.Y + rad, rad, r.Height - rad * 2), c);
			for (int j = 0; j < rad; j++)
			{
				int i0 = RunStart(rad, j);
				int w = rad - i0;
				if (w <= 0)
					continue;
				sb.Draw(px, new Rectangle(r.X + i0, r.Y + j, w, 1), c);
				sb.Draw(px, new Rectangle(r.Right - rad, r.Y + j, w, 1), c);
				sb.Draw(px, new Rectangle(r.X + i0, r.Bottom - 1 - j, w, 1), c);
				sb.Draw(px, new Rectangle(r.Right - rad, r.Bottom - 1 - j, w, 1), c);
			}
		}

		// The outline of a rounded rectangle, `th` pixels thick.
		public static void RoundBorder(SpriteBatch sb, Rectangle r, Color c, int th, int rad)
		{
			Texture2D px = TextureAssets.MagicPixel.Value;
			rad = Math.Max(1, Math.Min(rad, Math.Min(r.Width, r.Height) / 2));
			th = Math.Max(1, Math.Min(th, rad));
			sb.Draw(px, new Rectangle(r.X + rad, r.Y, r.Width - rad * 2, th), c);
			sb.Draw(px, new Rectangle(r.X + rad, r.Bottom - th, r.Width - rad * 2, th), c);
			sb.Draw(px, new Rectangle(r.X, r.Y + rad, th, r.Height - rad * 2), c);
			sb.Draw(px, new Rectangle(r.Right - th, r.Y + rad, th, r.Height - rad * 2), c);

			int rin = rad - th;
			for (int j = 0; j < rad; j++)
			{
				int i0 = RunStart(rad, j);
				int i1 = rad;
				double dy = rad - j - 0.5;
				if (rin > 0 && Math.Abs(dy) < rin)
					i1 = (int)Math.Ceiling(rad - Math.Sqrt(rin * (double)rin - dy * dy) - 0.5);
				i1 = Math.Min(rad, Math.Max(i0, i1));
				int w = i1 - i0;
				if (w <= 0)
					continue;
				sb.Draw(px, new Rectangle(r.X + i0, r.Y + j, w, 1), c);
				sb.Draw(px, new Rectangle(r.Right - i1, r.Y + j, w, 1), c);
				sb.Draw(px, new Rectangle(r.X + i0, r.Bottom - 1 - j, w, 1), c);
				sb.Draw(px, new Rectangle(r.Right - i1, r.Bottom - 1 - j, w, 1), c);
			}
		}

		// The Holo glass border and glow of a card (the fill behind it is the caller's: RoundFill, or DrawPlate for Legendary).
		public static void DrawHoloFrame(SpriteBatch sb, Rectangle rect, AugmentRarity rarity, bool hovered)
		{
			int rad = HoloRadius(rect);
			Color col = Of(rarity);
			Color amber = MenuLook.Current.Amber;
			Color line = rarity == AugmentRarity.Legendary ? (hovered ? amber : PlateAccent) : (hovered ? Light(col) : col);
			float breathe = (float)(Math.Sin(Main.GlobalTimeWrappedHourly * 2.2) * 0.5 + 0.5);
			var outer = new Rectangle(rect.X - 3, rect.Y - 3, rect.Width + 6, rect.Height + 6);
			var inner = new Rectangle(rect.X + 5, rect.Y + 5, rect.Width - 10, rect.Height - 10);
			int innerRad = Math.Max(2, rad - 4);

			switch (rarity)
			{
				case AugmentRarity.Rare:
					RoundBorder(sb, rect, line, 2, rad);
					RoundBorder(sb, new Rectangle(rect.X + 4, rect.Y + 4, rect.Width - 8, rect.Height - 8), line * 0.5f, 1, Math.Max(2, rad - 3));
					break;

				case AugmentRarity.Epic:
					if (HoloGlow)
						RoundBorder(sb, outer, line * (0.12f + 0.08f * breathe), 3, rad + 3);
					RoundBorder(sb, rect, line, 3, rad);
					RoundBorder(sb, inner, line * 0.55f, 1, innerRad);
					break;

				case AugmentRarity.Legendary:
					if (HoloGlow)
						RoundBorder(sb, outer, amber * (0.20f + 0.12f * breathe), 3, rad + 3);
					RoundBorder(sb, rect, line, 3, rad);
					RoundBorder(sb, inner, OnPlate * 0.4f, 1, innerRad);
					break;

				default:
					RoundBorder(sb, rect, line * 0.75f, 2, rad);
					break;
			}
		}
		// The fine stripes on a Legendary plate.
		public static void DrawPlate(SpriteBatch sb, Rectangle rect)
		{
			if (Holo)
			{
				RoundFill(sb, rect, Plate, HoloRadius(rect)); // a clean rounded plate, no stripes
				return;
			}

			Texture2D px = TextureAssets.MagicPixel.Value;
			sb.Draw(px, rect, Plate);
			Color stripe = OnPlate * 0.10f;
			for (int y = rect.Y + 3; y < rect.Bottom - 2; y += 4)
				sb.Draw(px, new Rectangle(rect.X + 2, y, rect.Width - 4, 1), stripe);
		}

		// --- Shared by the popup cards and the detail panel ---------------------------------------------------------------------------

		public const int TierBandHeight = 26;

		public static Color Darken(Color c, float k) => new Color((int)(c.R * k), (int)(c.G * k), (int)(c.B * k), (int)c.A);

		// Mixes the colour of every [c/RRGGBB:...] tag a little toward white, so the same colours (red, blue, orange...) read softer on a dark
		// panel. These are fixed colours: they do not change with the Cosmetics scheme.
		public static Color Pale(Color c, float k) => new Color((int)MathHelper.Lerp(c.R, 255f, k), (int)MathHelper.Lerp(c.G, 255f, k), (int)MathHelper.Lerp(c.B, 255f, k), (int)c.A);

		public static string PaleTags(string text, float k)
		{
			return System.Text.RegularExpressions.Regex.Replace(text ?? "", @"\[c/([0-9A-Fa-f]{6}):", m =>
			{
				int v = Convert.ToInt32(m.Groups[1].Value, 16);
				int r = (int)MathHelper.Lerp((v >> 16) & 255, 255f, k), g = (int)MathHelper.Lerp((v >> 8) & 255, 255f, k), b = (int)MathHelper.Lerp(v & 255, 255f, k);
				return $"[c/{r:X2}{g:X2}{b:X2}:";
			});
		}

// Multiplies the colour of every [c/RRGGBB:...] tag, so bright text colours still read on a light background.
		public static string DarkenTags(string text, float k)
		{
			return System.Text.RegularExpressions.Regex.Replace(text ?? "", @"\[c/([0-9A-Fa-f]{6}):", m =>
			{
				int v = Convert.ToInt32(m.Groups[1].Value, 16);
				int r = (int)(((v >> 16) & 255) * k), g = (int)(((v >> 8) & 255) * k), b = (int)((v & 255) * k);
				return $"[c/{r:X2}{g:X2}{b:X2}:";
			});
		}

		// The frame of a card in the NieR style (see the list at the top): the border, and for Rare, Epic and Legendary their extra marks.
		// `epicPlate` makes Epic a cream plate too (the detail panel does that); Legendary is always the plate.
		public static void DrawTierFrame(SpriteBatch sb, Rectangle rect, AugmentRarity rarity, bool hovered, bool epicPlate = false, bool closedInner = false)
		{
			if (Holo)
			{
				DrawHoloFrame(sb, rect, rarity, hovered);
				return;
			}
			if (Shadow)
			{
				ShadowStrips(sb, rect, rarity);
				LinedBox(sb, rect, rarity, hovered);
				return;
			}
			if (Folder)
			{
				LinedBox(sb, rect, rarity, hovered);
				DrawFolderTab(sb, rect, rarity, LineOf(rarity, hovered), BoxThickness(rarity), rarity == AugmentRarity.Legendary ? Plate : PanelChrome.PanelBackground);
				return;
			}

			Texture2D px = TextureAssets.MagicPixel.Value;
			Color col = Of(rarity);
			Color line = hovered ? Light(col) : col;
			bool plate = rarity == AugmentRarity.Legendary || (rarity == AugmentRarity.Epic && epicPlate);

			switch (rarity)
			{
				case AugmentRarity.Rare:
					// Rare: long corner brackets joined by a quieter edge (Epic has the full-strength border with a second line inside).
					HubDraw.Border(sb, rect, line * 0.6f, 2);
					HubDraw.Brackets(sb, rect, line, 16, 3);
					break;

				case AugmentRarity.Epic:
				{
					Color border = plate ? OnPlate : line; // ivory on a cream plate would vanish, so the border turns dark
					HubDraw.Border(sb, rect, border, 2);
					DrawInnerLine(sb, rect, border * 0.7f, closedInner);
					HubDraw.Brackets(sb, new Rectangle(rect.X - 5, rect.Y - 5, rect.Width + 10, rect.Height + 10), line, 12, 2);

					break;
				}

				case AugmentRarity.Legendary:
				{
					// The Legendary frame is the scheme's amber, a little darker so it holds on the light plate; it follows the Cosmetics colour.
					
					Color frame = hovered ? MenuLook.Current.Amber : PlateAccent;
					HubDraw.Border(sb, rect, frame, 2);
					DrawInnerLine(sb, rect, OnPlate * 0.55f, closedInner);
					HubDraw.Brackets(sb, new Rectangle(rect.X - 6, rect.Y - 6, rect.Width + 12, rect.Height + 12), frame, 16, 3);
					break;
				}

default:
					HubDraw.Border(sb, rect, line * 0.65f, 1);
					break;
			}

			// The pips along the top edge: small beads sitting ON the border line, so they never run into a title below them.
			int pips = Pips(rarity);
			Color pip = plate ? OnPlate : line;
			int midX = rect.X + rect.Width / 2;
			for (int i = 0; i < pips; i++)
				sb.Draw(px, new Rectangle(midX + (int)Math.Round((i - (pips - 1) * 0.5f) * 9f) - 2, rect.Y - 2, 5, 5), pip);
		}

		// The thin line inside the frame, open at the bottom: the tier band closes it, so no line runs through the band.
		private static void DrawInnerLine(SpriteBatch sb, Rectangle rect, Color c, bool closed = false)
		{
			int left = rect.X + 4, right = rect.Right - 5, top = rect.Y + 4, bottom = closed ? rect.Bottom - 5 : rect.Bottom - TierBandHeight - 1;
			HubDraw.Fill(sb, new Rectangle(left, top, right - left + 1, 1), c);
			HubDraw.Fill(sb, new Rectangle(left, top, 1, bottom - top), c);
			HubDraw.Fill(sb, new Rectangle(right, top, 1, bottom - top), c);
			if (closed)
				HubDraw.Fill(sb, new Rectangle(left, bottom, right - left + 1, 1), c); // a card with no tier band closes the line itself
		}

		// The band along the bottom of a card with the tier named in large spaced capitals.
		public static void DrawTierBand(SpriteBatch sb, Rectangle rect, AugmentRarity rarity, bool hovered, bool epicPlate = false)
		{
			Texture2D px = TextureAssets.MagicPixel.Value;
			Color col = Of(rarity);
			Color line = hovered ? Light(col) : col;
			// Holo: the band sits a little further in, so its corners stay inside the rounded card.
			var band = Holo
				? new Rectangle(rect.X + 7, rect.Bottom - TierBandHeight - 4, rect.Width - 14, TierBandHeight - 2)
				: new Rectangle(rect.X + 2, rect.Bottom - TierBandHeight, rect.Width - 4, TierBandHeight - 2);
			Color text;

			switch (rarity)
			{
				case AugmentRarity.Rare:
					sb.Draw(px, band, col * 0.10f);
					sb.Draw(px, new Rectangle(band.X, band.Y, band.Width, 1), line);
					text = line;
					break;

				case AugmentRarity.Epic:
					if (epicPlate)
					{
						sb.Draw(px, band, OnPlate); // a dark band on the cream plate
						text = col;
					}
					else
					{
						sb.Draw(px, new Rectangle(band.X, band.Y, band.Width, 1), line);
						sb.Draw(px, new Rectangle(band.X, band.Y + 3, band.Width, 1), line);
						text = line;
					}
					break;

				case AugmentRarity.Legendary:
					sb.Draw(px, band, hovered ? MenuLook.Current.Amber : PlateAccent);
					text = OnPlate;
					break;

				default:
					sb.Draw(px, new Rectangle(band.X, band.Y, band.Width, 1), col * 0.45f);
					text = col * 0.85f;
					break;
			}

			string label = rarity.ToString().ToUpperInvariant();
			const float scale = 0.74f;
			const float tracking = 4f;
			// The measured width ends with one trailing letter gap, so leave it out to centre the word. Vertically, centre in the part of the
			// band BELOW the lines drawn along its top edge (Epic has two), not in the whole band.
			float w = HubDraw.Width(label, scale, tracking) - tracking;
			int topInset = (rarity == AugmentRarity.Epic && !epicPlate) ? 5 : (rarity == AugmentRarity.Rare || rarity == AugmentRarity.Common) ? 1 : 0;
			float textX = (float)Math.Round(band.X + (band.Width - w) / 2f);
			float textY = (float)Math.Round(band.Y + topInset + (band.Height - topInset - HubDraw.LineHeight(scale)) / 2f + 1f);
			HubDraw.Text(sb, label, new Vector2(textX, textY), text, scale, tracking);
		}
		// --- The Ice style (kept for later) ----------------------------------------------------------------------------------------
		// Epic: pale steel, lavender and aqua drift into each other (6 second loop). Legendary: a brighter cycle with a flash of pale gold
		// that runs 2.5 times faster, plus a breathing outer ring and a light that circles the card (DrawLegendaryAura).

		private static readonly Color[] EpicCycle =
		{
			new Color(154, 184, 208), new Color(184, 168, 224), new Color(138, 208, 216)
		};

		private static readonly Color[] LegendaryCycle =
		{
			new Color(234, 244, 255), new Color(138, 208, 240), new Color(184, 164, 240), new Color(246, 232, 184)
		};

		private const float EpicPeriod = 6f;
		private const float LegendaryPeriod = 2.4f; // (periods divide an hour, so the clock wrapping never makes a jump)

		// --- What everything else reads ---------------------------------------------------------------------------------------------

		public static Color Common => Style == TierStyle.Nier ? NierCommon : new Color(200, 195, 178);
		public static Color Rare => Style == TierStyle.Nier ? NierRare : new Color(96, 160, 214);
		public static Color Epic => Of(AugmentRarity.Epic);
		public static Color Legendary => Of(AugmentRarity.Legendary);

		public static Color CommonLight => Light(Common);
		public static Color RareLight => Light(Rare);
		public static Color EpicLight => Light(Epic);
		public static Color LegendaryLight => Light(Legendary);

		// The colour of a tier right now. `phase` (0 to 1) shifts where in the loop a card is; only the Ice style uses it.
		public static Color Of(AugmentRarity rarity, float phase = 0f)
		{
			if (Style == TierStyle.Nier)
			{
				switch (rarity)
				{
					case AugmentRarity.Rare: return NierRare;
					case AugmentRarity.Epic: return NierEpic;
					case AugmentRarity.Legendary: return NierLegendary;
					default: return NierCommon;
				}
			}

			switch (rarity)
			{
				case AugmentRarity.Rare: return Rare;
				case AugmentRarity.Epic: return Cycle(EpicCycle, EpicPeriod, phase);
				case AugmentRarity.Legendary: return Cycle(LegendaryCycle, LegendaryPeriod, phase);
				default: return Common;
			}
		}

		// A brighter version of a colour, for highlights and hover.
		public static Color Light(Color c) => Color.Lerp(c, Color.White, 0.4f);

		// A phase for a card, from where it is on screen, so neighbours are at different points of an animated loop.
		public static float PhaseAt(int x, int y) => ((x * 0.0031f) + (y * 0.0047f)) % 1f;

		private static Color Cycle(Color[] colours, float period, float phase)
		{
			float u = (Main.GlobalTimeWrappedHourly / period + phase) % 1f;
			if (u < 0f)
				u += 1f;

			float f = u * colours.Length;
			int i = (int)f;
			float k = f - i;
			k = k * k * (3f - 2f * k); // ease, so each colour holds for a moment before moving on
			return Color.Lerp(colours[i % colours.Length], colours[(i + 1) % colours.Length], k);
		}

		// Ice style only (it does nothing in the NieR style): a breathing second ring just outside a Legendary card, and a bright light
		// with a short tail that travels around the frame. Draw it before the card's own border so the border stays on top.
		public static void DrawLegendaryAura(SpriteBatch sb, Rectangle rect, float phase, bool hovered)
		{
			if (Style != TierStyle.Ice)
				return;

			Texture2D px = TextureAssets.MagicPixel.Value;
			Color ring = Of(AugmentRarity.Legendary, phase + 0.33f);
			float breathe = 0.5f + 0.5f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2.6f + phase * 6.28f);
			float a = (hovered ? 0.55f : 0.28f) + 0.22f * breathe;
			var outer = new Rectangle(rect.X - 3, rect.Y - 3, rect.Width + 6, rect.Height + 6);
			sb.Draw(px, new Rectangle(outer.X, outer.Y, outer.Width, 1), ring * a);
			sb.Draw(px, new Rectangle(outer.X, outer.Bottom - 1, outer.Width, 1), ring * a);
			sb.Draw(px, new Rectangle(outer.X, outer.Y, 1, outer.Height), ring * a);
			sb.Draw(px, new Rectangle(outer.Right - 1, outer.Y, 1, outer.Height), ring * a);

			int perimeter = 2 * (rect.Width + rect.Height);
			float travel = (Main.GlobalTimeWrappedHourly * 0.32f + phase) % 1f;
			Color head = Light(Of(AugmentRarity.Legendary, phase));
			for (int i = 0; i < 5; i++)
			{
				float s = ((travel * perimeter) - i * 3f + perimeter) % perimeter;
				Point p = OnPerimeter(rect, s);
				int size = i == 0 ? 4 : 3;
				sb.Draw(px, new Rectangle(p.X - size / 2, p.Y - size / 2, size, size), head * (1f - i * 0.2f));
			}
		}

		private static Point OnPerimeter(Rectangle r, float s)
		{
			if (s < r.Width)
				return new Point(r.X + (int)s, r.Y);
			s -= r.Width;
			if (s < r.Height)
				return new Point(r.Right, r.Y + (int)s);
			s -= r.Height;
			if (s < r.Width)
				return new Point(r.Right - (int)s, r.Bottom);
			s -= r.Width;
			return new Point(r.X, r.Bottom - (int)s);
		}
	}
}
