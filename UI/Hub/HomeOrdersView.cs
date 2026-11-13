using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Augments
{
	// ORDERS: the goals that pay Data Fragments (see Missions). Three columns, one per group. Progress is read from the account,
	// so it is the same for every character. Orders can only advance while you play in a world, and pay out the moment they finish.
	internal class HomeOrdersView : HubElementBase
	{
		private const int Pad = 24;
		private const int ColGap = 16;

		protected override void DrawContent(SpriteBatch sb)
		{
			Rectangle r = GetDimensions().ToRectangle();

			HubDraw.Fill(sb, r, HubTheme.Panel);
			HubDraw.Border(sb, r, HubTheme.Line);
			MenuLook.DrawFrame(sb, r, HubTheme.Accent);

			// Header, laid out like the Archive's.
			var sq = new Rectangle(r.X + Pad, r.Y + 16, 38, 38);
			HubDraw.Fill(sb, sq, HubTheme.Accent);
			float iw = HubDraw.Width("04", 0.9f, 1f);
			HubDraw.Text(sb, "04", new Vector2(sq.X + (sq.Width - iw) / 2f, sq.Y + (sq.Height - HubDraw.LineHeight(0.9f)) / 2f), HubTheme.OnAccent, 0.9f, 1f);
			HubDraw.Text(sb, "ORDERS", new Vector2(r.X + Pad + 52, r.Y + 12), HubTheme.Text, 1.25f, 3f);
			HubDraw.Text(sb, "GOALS THAT PAY DATA FRAGMENTS", new Vector2(r.X + Pad + 53, r.Y + 42), HubTheme.Dim, 0.66f, 1.5f);

			int done = 0;
			foreach (Mission m in Missions.All)
			{
				if (AccountStore.IsMissionDone(m.Id))
					done++;
			}
			HubDraw.TextRight(sb, "DATA FRAGMENTS  " + AccountStore.Balance, r.Right - Pad, r.Y + 14, HubTheme.Warn, 0.72f, 2f);
			HubDraw.TextRight(sb, "COMPLETED " + done + " / " + Missions.All.Length, r.Right - Pad, r.Y + 36, HubTheme.Dim, 0.66f, 2f);
			HubDraw.HLine(sb, r.X + Pad, r.Y + 68, r.Width - Pad * 2, HubTheme.Line);

			// Group the orders, keeping the order they are listed in.
			var groups = new List<string>();
			foreach (Mission m in Missions.All)
			{
				if (!groups.Contains(m.Group))
					groups.Add(m.Group);
			}

			int top = r.Y + 88;
			int bottom = r.Bottom - 40;
			int colW = (r.Width - Pad * 2 - (groups.Count - 1) * ColGap) / groups.Count;

			int tallest = 0;
			foreach (string g in groups)
				tallest = Math.Max(tallest, CountIn(g));

			int cardGap = 10;
			int cardH = Math.Max(64, Math.Min(98, (bottom - top - 30 - (tallest - 1) * cardGap) / Math.Max(1, tallest)));

			double stretch = AccountEarningPlayer.AliveStretch;
			for (int gi = 0; gi < groups.Count; gi++)
			{
				int x = r.X + Pad + gi * (colW + ColGap);
				HubDraw.Diamond(sb, new Vector2(x + 4, top + 8), 6f, HubTheme.Warn);
				HubDraw.Text(sb, groups[gi], new Vector2(x + 14, top), HubTheme.Warn, 0.7f, 2f);

				int row = 0;
				foreach (Mission m in Missions.All)
				{
					if (m.Group != groups[gi])
						continue;

					var card = new Rectangle(x, top + 30 + row * (cardH + cardGap), colW, cardH);
					if (card.Bottom > bottom + 4)
						break;

					DrawCard(sb, card, m, stretch);
					row++;
				}
			}

			HubDraw.TextFit(sb, "ORDERS ADVANCE ONLY WHILE YOU PLAY IN A WORLD WITH THE WINDOW FOCUSED     PLAYING ALSO EARNS 2 DATA FRAGMENTS A MINUTE", new Vector2(r.X + Pad, r.Bottom - 26), HubTheme.Dim, 0.6f, 2f, r.Width - Pad * 2);
		}

		private static int CountIn(string group)
		{
			int n = 0;
			foreach (Mission m in Missions.All)
			{
				if (m.Group == group)
					n++;
			}
			return n;
		}

		private static void DrawCard(SpriteBatch sb, Rectangle card, Mission m, double aliveStretch)
		{
			bool done = AccountStore.IsMissionDone(m.Id);
			double progress = Missions.Progress(m, aliveStretch);
			float fraction = m.Target > 0 ? (float)(progress / m.Target) : 0f;

			HubDraw.Fill(sb, card, HubTheme.PanelAlt);
			HubDraw.Border(sb, card, HubTheme.LineSoft);
			HubDraw.Fill(sb, new Rectangle(card.X, card.Y, 5, card.Height), done ? HubTheme.Dim : HubTheme.Accent);

			// Reward on the right, status under it.
			string reward = "+" + m.Reward;
			HubDraw.TextRight(sb, reward, card.Right - 14, card.Y + 9, done ? HubTheme.Dim : HubTheme.Warn, 0.8f, 1.5f);
			HubDraw.TextRight(sb, done ? "DONE" : "OPEN", card.Right - 14, card.Y + 30, HubTheme.Dim, 0.56f, 2f);

			// Title, shrunk so it never runs into the reward.
			float titleScale = 0.8f;
			float room = card.Width - 20 - 14 - HubDraw.Width(reward, 0.8f, 1.5f) - 14;
			float tw = HubDraw.Width(m.Title, titleScale, 1.5f);
			if (tw > room)
				titleScale *= room / tw;
			HubDraw.Text(sb, m.Title, new Vector2(card.X + 20, card.Y + 9), done ? HubTheme.Dim : HubTheme.Text, titleScale, 1.5f);

			// Description: wrapped to two lines when the card is tall enough, otherwise one line shrunk to fit.
			float descScale = 0.62f;
			if (card.Height >= 80)
			{
				int dy = card.Y + 32;
				int lines = 0;
				foreach (string line in HubDraw.Wrap(m.Description, card.Width - 40, descScale))
				{
					HubDraw.Text(sb, line, new Vector2(card.X + 20, dy), HubTheme.Dim, descScale);
					dy += (int)HubDraw.LineHeight(descScale) + 1;
					if (++lines >= 2)
						break;
				}
			}
			else
			{
				float dw = HubDraw.Width(m.Description, descScale);
				float droom = card.Width - 40;
				if (dw > droom)
					descScale *= droom / dw;
				HubDraw.Text(sb, m.Description, new Vector2(card.X + 20, card.Y + 30), HubTheme.Dim, descScale);
			}

			// Progress bar and its numbers along the bottom.
			var bar = new Rectangle(card.X + 20, card.Bottom - 20, card.Width - 40 - 96, 10);
			HubDraw.Border(sb, bar, HubTheme.Line);
			int fill = (int)((bar.Width - 4) * MathHelper.Clamp(fraction, 0f, 1f));
			HubDraw.Fill(sb, new Rectangle(bar.X + 2, bar.Y + 2, fill, bar.Height - 4), done ? HubTheme.Dim : HubTheme.Accent);

			string numbers = Missions.Format(m, progress) + " / " + Missions.Format(m, m.Target);
			float ns = 0.54f;
			float nw = HubDraw.Width(numbers, ns, 1f);
			if (nw > 90f)
				ns *= 90f / nw;
			HubDraw.TextRight(sb, numbers, card.Right - 14, card.Bottom - 22, HubTheme.Dim, ns, 1f);
		}
	}
}
