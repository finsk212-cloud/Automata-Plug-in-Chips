using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.UI;

namespace Augments
{
	// COSMETICS: equip what you own. Two rows: the colour scheme of the title screen, and the corner brackets around the panels.
	// Each card shows a small mock-up. A card you own equips on click; one you do not own shows its price and takes you to the
	// SHOP when clicked. Choices are remembered in the account file (see AccountStore).
	internal class HomeCosmeticsView : HubElementBase
	{
		private const int Pad = 24;
		private const int Gap = 14;

		private readonly AutomataHomeUIState state;

		public HomeCosmeticsView(AutomataHomeUIState state)
		{
			this.state = state;
		}

		// Which card is at a point: (row, index), row 0 = schemes, 1 = frames; (-1, -1) if none.
		private static (int row, int index) CardAt(Rectangle r, Point p)
		{
			for (int i = 0; i < MenuLook.All.Length; i++)
			{
				if (SchemeCard(r, i).Contains(p))
					return (0, i);
			}
			for (int i = 0; i < MenuLook.Frames.Length; i++)
			{
				if (FrameCard(r, i).Contains(p))
					return (1, i);
			}
			return (-1, -1);
		}

		public override void LeftClick(UIMouseEvent evt)
		{
			base.LeftClick(evt);
			AutomataHubSystem.Guard(() =>
			{
				Rectangle panel = GetDimensions().ToRectangle();
				if (PodButton(panel).Contains(evt.MousePosition.ToPoint()))
				{
					PodPetPlayer.SetSetting(!PodPetPlayer.SettingOn);
					SoundEngine.PlaySound(SoundID.MenuTick);
					return;
				}

				(int row, int index) = CardAt(GetDimensions().ToRectangle(), evt.MousePosition.ToPoint());
				if (row < 0)
					return;

				bool owned = row == 0 ? MenuLook.SchemeOwned(index) : MenuLook.FrameOwned(index);
				if (!owned)
				{
					// Not owned yet: the Shop sells it, or if it is an Operation reward, the Operation page is where to earn it.
					int price = row == 0 ? MenuLook.SchemePrices[index] : MenuLook.FramePrices[index];
					state.SetSection(price < 0 ? HomeSection.Operation : HomeSection.Shop);
					return;
				}

				SoundEngine.PlaySound(SoundID.MenuOpen);
				if (row == 0)
					MenuLook.SelectScheme(index);
				else
					MenuLook.SelectFrame(index);
			});
		}

		// The button that shows or hides your Pod pet (the little Pod that floats beside you in game).
		private static Rectangle PodButton(Rectangle r) => new Rectangle(r.Right - Pad - 150, r.Y + 8, 150, 24);

		// --- Layout ---

		private static int SchemeRowTop(Rectangle r) => r.Y + 112;

		private static int SchemeCardHeight(Rectangle r)
		{
			int avail = r.Height - 112 - 40;           // below the header, above the footer
			int frameBlock = 28 + FrameCardHeight + 14; // the frame row's label, cards and gap
			return Math.Max(110, Math.Min(210, avail - frameBlock));
		}

		private const int FrameCardHeight = 84;

		private static Rectangle SchemeCard(Rectangle r, int i)
		{
			int count = MenuLook.All.Length;
			int availW = r.Width - Pad * 2;
			int cardW = (availW - (count - 1) * Gap) / count;
			return new Rectangle(r.X + Pad + i * (cardW + Gap), SchemeRowTop(r), cardW, SchemeCardHeight(r));
		}

		private static Rectangle FrameCard(Rectangle r, int i)
		{
			int count = MenuLook.Frames.Length;
			int availW = r.Width - Pad * 2;
			int cardW = (availW - (count - 1) * Gap) / count;
			int top = SchemeRowTop(r) + SchemeCardHeight(r) + 14 + 28;
			return new Rectangle(r.X + Pad + i * (cardW + Gap), top, cardW, FrameCardHeight);
		}

		// --- Drawing ---

		protected override void DrawContent(SpriteBatch sb)
		{
			Rectangle r = GetDimensions().ToRectangle();

			HubDraw.Fill(sb, r, HubTheme.Panel);
			HubDraw.Border(sb, r, HubTheme.Line);
			MenuLook.DrawFrame(sb, r, HubTheme.Accent);

			// Header, laid out like the Archive's.
			var sq = new Rectangle(r.X + Pad, r.Y + 16, 38, 38);
			HubDraw.Fill(sb, sq, HubTheme.Accent);
			float iw = HubDraw.Width("05", 0.9f, 1f);
			HubDraw.Text(sb, "05", new Vector2(sq.X + (sq.Width - iw) / 2f, sq.Y + (sq.Height - HubDraw.LineHeight(0.9f)) / 2f), HubTheme.OnAccent, 0.9f, 1f);
			HubDraw.Text(sb, "COSMETICS", new Vector2(r.X + Pad + 52, r.Y + 12), HubTheme.Text, 1.25f, 3f);
			HubDraw.Text(sb, "COLOUR SCHEME AND PANEL FRAME", new Vector2(r.X + Pad + 53, r.Y + 42), HubTheme.Dim, 0.66f, 1.5f);
			HubDraw.TextRight(sb, "DATA FRAGMENTS  " + AccountStore.Balance, r.Right - Pad, r.Y + 34, HubTheme.Warn, 0.72f, 2f);
			HubDraw.HLine(sb, r.X + Pad, r.Y + 68, r.Width - Pad * 2, HubTheme.Line);

			// Pod pet switch.
			Rectangle podBtn = PodButton(r);
			bool podOn = PodPetPlayer.SettingOn;
			bool podHover = podBtn.Contains(Main.MouseScreen.ToPoint());
			HubDraw.Fill(sb, podBtn, podOn ? HubTheme.Accent : (podHover ? HubTheme.PanelAlt * 0.8f : HubTheme.PanelAlt));
			HubDraw.Border(sb, podBtn, podOn ? HubTheme.Accent : HubTheme.LineSoft, 2);
			string podLabel = "POD PET  " + (podOn ? "ON" : "OFF");
			float pw = HubDraw.Width(podLabel, 0.62f, 2f);
			HubDraw.Text(sb, podLabel, new Vector2(podBtn.X + (podBtn.Width - pw) / 2f, podBtn.Y + (podBtn.Height - HubDraw.LineHeight(0.62f)) / 2f), podOn ? HubTheme.OnAccent : HubTheme.Text, 0.62f, 2f);

			Point mouse = Main.MouseScreen.ToPoint();
			(int hoverRow, int hoverIndex) = CardAt(r, mouse);

			SectionLabel(sb, "COLOUR SCHEME", r.X + Pad, SchemeRowTop(r) - 28);
			for (int i = 0; i < MenuLook.All.Length; i++)
				DrawSchemeCard(sb, SchemeCard(r, i), i, i == MenuLook.SelectedIndex, hoverRow == 0 && hoverIndex == i);

			SectionLabel(sb, "PANEL FRAME", r.X + Pad, FrameCard(r, 0).Y - 28);
			for (int i = 0; i < MenuLook.Frames.Length; i++)
				DrawFrameCard(sb, FrameCard(r, i), i, i == MenuLook.FrameIndex, hoverRow == 1 && hoverIndex == i);

			HubDraw.TextFit(sb, "CLICK WHAT YOU OWN TO EQUIP IT     LOCKED ITEMS ARE BOUGHT IN THE SHOP WITH DATA FRAGMENTS", new Vector2(r.X + Pad, r.Bottom - 26), HubTheme.Dim, 0.6f, 2f, r.Width - Pad * 2);
		}

		private static void SectionLabel(SpriteBatch sb, string text, int x, int y)
		{
			HubDraw.Diamond(sb, new Vector2(x + 4, y + 8), 6f, HubTheme.Warn);
			HubDraw.Text(sb, text, new Vector2(x + 14, y), HubTheme.Warn, 0.7f, 2f);
		}

		// The line under a card's name: owned ones say whether they are equipped, locked ones say what they cost.
		private static void DrawStatus(SpriteBatch sb, Rectangle card, bool owned, bool equipped, int price)
		{
			string tag = equipped ? "EQUIPPED" : (owned ? "CLICK TO EQUIP" : (price < 0 ? "CAMPAIGN REWARD" : price + " FRAGMENTS  //  SHOP"));
			Color color = equipped ? HubTheme.Warn : HubTheme.Dim;
			float scale = 0.58f;
			float width = HubDraw.Width(tag, scale, 2f);
			float room = card.Width - 24f;
			if (width > room)
				scale *= room / width;
			HubDraw.Text(sb, tag, new Vector2(card.X + 12, card.Bottom - 22), color, scale, 2f);
		}

		private static void DrawSchemeCard(SpriteBatch sb, Rectangle card, int index, bool equipped, bool hovered)
		{
			MenuLook.Scheme s = MenuLook.All[index];
			bool owned = MenuLook.SchemeOwned(index);

			HubDraw.Fill(sb, card, hovered && !equipped ? HubTheme.PanelAlt * 0.8f : HubTheme.PanelAlt);
			HubDraw.Border(sb, card, equipped ? HubTheme.Accent : (hovered ? HubTheme.Text : HubTheme.LineSoft), equipped ? 2 : 1);

			// The mock-up: a dark "screen" with the scheme's own top bar, left buttons and currency box.
			var screen = new Rectangle(card.X + 10, card.Y + 10, card.Width - 20, card.Height - 80);
			HubDraw.Fill(sb, screen, new Color(6, 8, 14));
			HubDraw.Border(sb, screen, HubTheme.Line);

			var bar = new Rectangle(screen.X + 8, screen.Y + 8, screen.Width - 16, 12);
			HubDraw.Fill(sb, bar, s.Backing);
			HubDraw.Border(sb, bar, s.Ivory * 0.5f);
			int tabs = 5;
			int tabW = (bar.Width - 4) / tabs;
			HubDraw.Fill(sb, new Rectangle(bar.X + 2, bar.Y + 2, tabW - 2, bar.Height - 4), s.Ivory);
			for (int t = 1; t < tabs; t++)
				HubDraw.Fill(sb, new Rectangle(bar.X + 2 + t * tabW + 4, bar.Y + bar.Height / 2 - 1, Math.Max(2, tabW - 12), 2), s.Idle);

			int bx = screen.X + 10;
			int by = bar.Bottom + 10;
			int bw = Math.Max(20, screen.Width / 4);
			HubDraw.Fill(sb, new Rectangle(bx, by, bw, 8), s.Ivory);
			HubDraw.Fill(sb, new Rectangle(bx + 3, by + 3, Math.Max(2, bw / 2), 2), s.Charcoal);
			for (int b = 1; b < 4; b++)
				HubDraw.Fill(sb, new Rectangle(bx + 3, by + b * 13 + 3, Math.Max(2, bw - 14 - b * 3), 2), s.Idle);

			int fx = bx + bw + 12;
			var frame = new Rectangle(fx, by, Math.Max(10, screen.Right - 10 - 44 - fx), Math.Max(10, screen.Bottom - 10 - by));
			HubDraw.Border(sb, frame, s.Ivory * 0.4f);
			HubDraw.Fill(sb, new Rectangle(frame.X + 6, frame.Bottom - 12, Math.Max(2, frame.Width / 2), 2), s.Ivory);
			var cur = new Rectangle(screen.Right - 10 - 40, by, 40, 18);
			HubDraw.Fill(sb, cur, s.Backing);
			HubDraw.Border(sb, cur, s.Ivory * 0.4f);
			HubDraw.Diamond(sb, new Vector2(cur.X + 7, cur.Y + 6), 3f, s.Amber);
			HubDraw.Fill(sb, new Rectangle(cur.X + 12, cur.Y + 5, 22, 2), s.Amber);
			HubDraw.Fill(sb, new Rectangle(cur.X + 8, cur.Y + 12, 26, 2), s.Muted);

			if (!owned)
			{
				HubDraw.Stripes(sb, screen, Color.Black * 0.4f);
				float lw = HubDraw.Width("LOCKED", 0.7f, 2f);
				HubDraw.Text(sb, "LOCKED", new Vector2(screen.Center.X - lw / 2f, screen.Center.Y - HubDraw.LineHeight(0.7f) / 2f), Color.White * 0.9f, 0.7f, 2f);
			}

			HubDraw.Text(sb, s.Name, new Vector2(card.X + 12, card.Bottom - 62), HubTheme.Text, 0.8f, 2f);

			float scale = 0.52f;
			float width = HubDraw.Width(s.Blurb, scale, 1f);
			float room = card.Width - 24f;
			if (width > room)
				scale *= room / width;
			HubDraw.Text(sb, s.Blurb, new Vector2(card.X + 12, card.Bottom - 40), HubTheme.Dim, scale, 1f);

			DrawStatus(sb, card, owned, equipped, MenuLook.SchemePrices[index]);
		}

		private static void DrawFrameCard(SpriteBatch sb, Rectangle card, int index, bool equipped, bool hovered)
		{
			MenuLook.FrameStyle f = MenuLook.Frames[index];
			bool owned = MenuLook.FrameOwned(index);

			HubDraw.Fill(sb, card, hovered && !equipped ? HubTheme.PanelAlt * 0.8f : HubTheme.PanelAlt);
			HubDraw.Border(sb, card, equipped ? HubTheme.Accent : (hovered ? HubTheme.Text : HubTheme.LineSoft), equipped ? 2 : 1);

			// A small sample panel wearing that frame, on the left of the card.
			var sample = new Rectangle(card.X + 22, card.Y + 22, 64, card.Height - 44);
			HubDraw.Fill(sb, sample, HubTheme.Panel);
			HubDraw.Border(sb, sample, HubTheme.Line);
			DrawSample(sb, sample, index, owned ? HubTheme.Accent : HubTheme.Dim);

			int tx = sample.Right + 22;
			HubDraw.Text(sb, f.Name, new Vector2(tx, card.Y + 12), HubTheme.Text, 0.8f, 2f);

			float scale = 0.52f;
			float width = HubDraw.Width(f.Blurb, scale, 1f);
			float room = card.Right - tx - 10f;
			if (width > room)
				scale *= room / width;
			HubDraw.Text(sb, f.Blurb, new Vector2(tx, card.Y + 36), HubTheme.Dim, scale, 1f);

			string tag = equipped ? "EQUIPPED" : (owned ? "CLICK TO EQUIP" : (MenuLook.FramePrices[index] < 0 ? "CAMPAIGN REWARD" : MenuLook.FramePrices[index] + " FRAGMENTS  //  SHOP"));
			float tagScale = 0.56f;
			float tagWidth = HubDraw.Width(tag, tagScale, 2f);
			if (tagWidth > room)
				tagScale *= room / tagWidth;
			HubDraw.Text(sb, tag, new Vector2(tx, card.Bottom - 24), equipped ? HubTheme.Warn : HubTheme.Dim, tagScale, 2f);
		}

		// The same bracket shapes MenuLook.DrawFrame uses, smaller, so the card shows what you would get.
		private static void DrawSample(SpriteBatch sb, Rectangle panel, int style, Color color)
		{
			switch (style)
			{
				case 1:
					HubDraw.Brackets(sb, new Rectangle(panel.X - 4, panel.Y - 4, panel.Width + 8, panel.Height + 8), color, 14, 3);
					break;
				case 2:
					HubDraw.Brackets(sb, new Rectangle(panel.X - 7, panel.Y - 7, panel.Width + 14, panel.Height + 14), color, 10, 2);
					HubDraw.Brackets(sb, new Rectangle(panel.X - 3, panel.Y - 3, panel.Width + 6, panel.Height + 6), color, 5, 1);
					break;
				case 3:
					HubDraw.Brackets(sb, new Rectangle(panel.X - 5, panel.Y - 5, panel.Width + 10, panel.Height + 10), color, 11, 2);
					foreach (Point node in new[] { new Point(panel.X - 5, panel.Y - 5), new Point(panel.Right + 5, panel.Y - 5), new Point(panel.X - 5, panel.Bottom + 5), new Point(panel.Right + 5, panel.Bottom + 5) })
						HubDraw.Fill(sb, new Rectangle(node.X - 2, node.Y - 2, 4, 4), color);
					break;
				default:
					HubDraw.Brackets(sb, new Rectangle(panel.X - 4, panel.Y - 4, panel.Width + 8, panel.Height + 8), color, 9, 2);
					break;
			}
		}
	}
}
