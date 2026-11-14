using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.UI;
using Augments.Core;

namespace Augments
{
	// The first-launch question on the title screen: "Share anonymous stats?". It covers the whole home page with a dim layer
	// and takes every click until the player answers, so it cannot be skipped by accident. SHARE and NO THANKS look identical
	// and have the same size, so neither is pushed. The answer is saved by StatsConsent (the config switch plus a "was asked"
	// note), and the question is not asked again. It only appears when there is a server to send to (see StatsConsent.NeedsPrompt).
	internal class HomeStatsPrompt : HubElementBase
	{
		private const string Title = "SHARE ANONYMOUS STATS?";
		private const string Body = "Share anonymous stats to help balance Plug-in Chips and improve the quality of the mod.";
		private const string Sent = "SENT: THE PLUGIN, THE WEAPON, HOW MUCH DAMAGE IT DID AND HOW LONG THE FIGHT LASTED.";
		private const string Never = "NEVER SENT: YOUR NAME, STEAM ACCOUNT, WORLD NAME OR IP ADDRESS.";
		private const string Foot = "YOU CAN CHANGE THIS ANY TIME IN MOD CONFIG.";

		private const int Pad = 26;
		private const int ButtonH = 46;
		private const int ButtonGap = 16;

		private int lastHover = -1;

		public HomeStatsPrompt(AutomataHomeUIState state)
		{
			Width.Set(0f, 1f);
			Height.Set(0f, 1f);

			// Not while the page is still fading in from black, so the box appears after the screen has settled.
			VisibleWhen = () => StatsConsent.NeedsPrompt && state.OpenTicks <= 0;
		}

		private struct Layout
		{
			public Rectangle Box;
			public Rectangle Share;
			public Rectangle Decline;
			public List<string> Body;
			public List<string> Sent;
			public List<string> Never;
			public int BodyY;
			public int SentY;
			public int NeverY;
			public int FootY;
			public float BodyLine;
			public float SmallLine;
		}

		private static Layout Measure(Rectangle screen)
		{
			int width = Math.Min(600, screen.Width - 80);
			int inner = width - 2 * Pad;

			var l = new Layout
			{
				Body = new List<string>(HubDraw.Wrap(Body, inner, 0.82f)),
				Sent = new List<string>(HubDraw.Wrap(Sent, inner, 0.68f)),
				Never = new List<string>(HubDraw.Wrap(Never, inner, 0.68f)),
				BodyLine = HubDraw.LineHeight(0.82f) + 5f,
				SmallLine = HubDraw.LineHeight(0.68f) + 4f
			};

			int y = Pad + 30 + 16;                       // title and the rule under it
			l.BodyY = y;
			y += (int)Math.Ceiling(l.Body.Count * l.BodyLine) + 14;
			l.SentY = y;
			y += (int)Math.Ceiling(l.Sent.Count * l.SmallLine) + 4;
			l.NeverY = y;
			y += (int)Math.Ceiling(l.Never.Count * l.SmallLine) + 22;
			int buttonsY = y;
			y += ButtonH + 16;
			l.FootY = y;
			y += (int)Math.Ceiling(HubDraw.LineHeight(0.6f)) + Pad;

			l.Box = new Rectangle(screen.X + (screen.Width - width) / 2, screen.Y + Math.Max(40, (screen.Height - y) / 2), width, y);

			int buttonW = (inner - ButtonGap) / 2;
			l.Share = new Rectangle(l.Box.X + Pad, l.Box.Y + buttonsY, buttonW, ButtonH);
			l.Decline = new Rectangle(l.Share.Right + ButtonGap, l.Box.Y + buttonsY, inner - ButtonGap - buttonW, ButtonH);
			return l;
		}

		public override void LeftClick(UIMouseEvent evt)
		{
			base.LeftClick(evt);
			AutomataHubSystem.Guard(() =>
			{
				Layout l = Measure(GetDimensions().ToRectangle());
				Point p = evt.MousePosition.ToPoint();

				if (l.Share.Contains(p))
				{
					SoundEngine.PlaySound(SoundID.MenuOpen);
					StatsConsent.Answer(true);
				}
				else if (l.Decline.Contains(p))
				{
					SoundEngine.PlaySound(SoundID.MenuClose);
					StatsConsent.Answer(false);
				}
			});
		}

		protected override void DrawContent(SpriteBatch sb)
		{
			Rectangle screen = GetDimensions().ToRectangle();
			Layout l = Measure(screen);
			Point mouse = Main.MouseScreen.ToPoint();

			HubDraw.Fill(sb, screen, Color.Black * 0.66f);

			Rectangle box = l.Box;
			HubDraw.Fill(sb, box, AutomataHomeUIState.Backing);
			HubDraw.Border(sb, box, AutomataHomeUIState.Amber);
			HubDraw.Corners(sb, new Rectangle(box.X - 5, box.Y - 5, box.Width + 10, box.Height + 10), AutomataHomeUIState.Amber, 12, 2);

			// Title with a rule under it.
			HubDraw.Diamond(sb, new Vector2(box.X + Pad + 5, box.Y + Pad + 10), 7f, AutomataHomeUIState.Amber);
			HubDraw.Text(sb, Title, new Vector2(box.X + Pad + 20, box.Y + Pad), AutomataHomeUIState.Amber, 0.95f, 2.5f);
			HubDraw.HLine(sb, box.X + Pad, box.Y + Pad + 34, box.Width - 2 * Pad, AutomataHomeUIState.Ivory * 0.25f);

			// The question, then exactly what is and is not sent.
			for (int i = 0; i < l.Body.Count; i++)
				HubDraw.Text(sb, l.Body[i], new Vector2(box.X + Pad, box.Y + l.BodyY + i * l.BodyLine), AutomataHomeUIState.Ivory, 0.82f, 0.5f);
			for (int i = 0; i < l.Sent.Count; i++)
				HubDraw.Text(sb, l.Sent[i], new Vector2(box.X + Pad, box.Y + l.SentY + i * l.SmallLine), AutomataHomeUIState.Idle, 0.68f, 1f);
			for (int i = 0; i < l.Never.Count; i++)
				HubDraw.Text(sb, l.Never[i], new Vector2(box.X + Pad, box.Y + l.NeverY + i * l.SmallLine), AutomataHomeUIState.Idle, 0.68f, 1f);

			// Two identical buttons.
			int hover = l.Share.Contains(mouse) ? 0 : (l.Decline.Contains(mouse) ? 1 : -1);
			if (hover != lastHover)
			{
				if (hover >= 0)
					SoundEngine.PlaySound(SoundID.MenuTick);
				lastHover = hover;
			}

			DrawButton(sb, l.Share, "SHARE", hover == 0);
			DrawButton(sb, l.Decline, "NO THANKS", hover == 1);

			HubDraw.Text(sb, Foot, new Vector2(box.X + Pad, box.Y + l.FootY), AutomataHomeUIState.Muted, 0.6f, 1.5f);
		}

		private static void DrawButton(SpriteBatch sb, Rectangle r, string label, bool hot)
		{
			Color fg = hot ? AutomataHomeUIState.Charcoal : AutomataHomeUIState.Ivory;
			HubDraw.Fill(sb, r, hot ? AutomataHomeUIState.Ivory : AutomataHomeUIState.Ivory * 0.08f);
			HubDraw.Border(sb, r, AutomataHomeUIState.Ivory);

			const float scale = 0.82f;
			float w = HubDraw.Width(label, scale, 2.5f);
			float y = r.Y + (r.Height - HubDraw.LineHeight(scale)) / 2f + 4.2f * scale;
			HubDraw.Text(sb, label, new Vector2(r.X + (r.Width - w) / 2f, y), fg, scale, 2.5f);
		}
	}
}
