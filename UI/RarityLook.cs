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

	internal static class RarityLook
	{
		public static TierStyle Style = TierStyle.Nier;

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
				Terraria.UI.Chat.ChatManager.DrawColorCodedStringWithShadow(sb, font, text, pos, color, 0f, Vector2.Zero, scale);
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
		// The fine stripes on a Legendary plate.
		public static void DrawPlate(SpriteBatch sb, Rectangle rect)
		{
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
			var band = new Rectangle(rect.X + 2, rect.Bottom - TierBandHeight, rect.Width - 4, TierBandHeight - 2);
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
