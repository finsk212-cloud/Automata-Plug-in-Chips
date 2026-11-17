using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.UI;

namespace Augments
{
	// The full CAMPAIGN page: every tier in ONE row, laid out like a pass track. A rail with a node for each tier runs along the top
	// and fills as you earn XP; under each node is the tier's reward as a card, colour coded by kind. Drag the row with the mouse (or use
	// the wheel) to see further; it glides a little after you let go. Click a reached tier to claim it, click a claimed item to equip it
	// (or take it off again). Only the tiers on screen are drawn, so the page stays light. It is opened from the banner on the home
	// screen and left with Esc or the HOME tab.
	internal class HomeOperationView : HubElementBase
	{
		private const int Pad = 24;
		private const int Gap = 12;
		private const float DragThreshold = 6f;

		private readonly HubClipRegion trackClip;

		private float scroll;          // how far the row is scrolled, in UI units
		private float velocity;        // glide after a drag, units per tick
		private bool dragging;
		private bool dragMoved;        // the mouse travelled far enough that this was a drag, not a click
		private float dragStartMouseX;
		private float lastMouseX;
		private bool wasShown;

		private string message = "";
		private long messageUntil;

		public HomeOperationView()
		{
			// The row is drawn inside a clipping region so a tier cut off at the edge is clipped cleanly instead of spilling out.
			trackClip = new HubClipRegion(DrawTrack) { VisibleWhen = () => IsShown };
			Append(trackClip);
		}

		// A colour for each kind of reward, so the row can be read at a glance. (Fixed hues, so they stay recognisable in every scheme.)
		private static Color KindColor(RewardKind kind)
		{
			switch (kind)
			{
				case RewardKind.Fragments: return MenuLook.Current.Amber;
				case RewardKind.Scheme: return new Color(158, 118, 226);
				case RewardKind.Frame: return new Color(96, 146, 226);
				case RewardKind.PodSkin: return new Color(88, 188, 142);
				case RewardKind.Title: return new Color(214, 96, 84);
				default: return new Color(72, 182, 194);
			}
		}

		private static float Wave(float seconds, float speed) => 0.5f + 0.5f * (float)Math.Sin(seconds * speed * MathHelper.TwoPi);

		// --- Layout ---

		private struct Layout
		{
			public Rectangle XpBar;
			public Rectangle ClaimAll;
			public Rectangle Track;   // the visible window onto the row
			public Rectangle Detail;
			public int RailY;
			public int CellTop;
			public int CellW;
			public int CellH;
		}

		private static Layout LayOut(Rectangle r)
		{
			var l = new Layout();
			l.XpBar = new Rectangle(r.X + Pad, r.Y + 106, r.Width - Pad * 2 - 204, 18);
			l.ClaimAll = new Rectangle(r.Right - Pad - 192, r.Y + 98, 192, 34);

			int detailH = 58;
			l.Detail = new Rectangle(r.X + Pad, r.Bottom - 40 - detailH, r.Width - Pad * 2, detailH);

			int top = r.Y + 146;
			int bottom = l.Detail.Y - 10;
			int trackH = Math.Max(170, bottom - top);
			l.Track = new Rectangle(r.X + Pad, top, r.Width - Pad * 2, trackH);
			l.RailY = top + 12;
			l.CellTop = top + 30;
			l.CellH = Math.Min(400, trackH - 30 - 12); // the last 12 units hold the scroll indicator
			l.CellW = Math.Max(150, Math.Min(196, (int)(l.CellH * 0.58f)));
			return l;
		}

		private static float TotalWidth(Layout l, Operation op) => op.TierCount * (l.CellW + Gap) - Gap;

		private static float MaxScroll(Layout l, Operation op) => Math.Max(0f, TotalWidth(l, op) - l.Track.Width);

		private Rectangle CellRect(Layout l, int tier)
		{
			return new Rectangle(l.Track.X + (tier - 1) * (l.CellW + Gap) - (int)scroll, l.CellTop, l.CellW, l.CellH);
		}

		// The tier under a point, or 0. Only the part of a cell that is inside the visible window counts.
		private int TierAt(Layout l, Operation op, Point p)
		{
			if (!l.Track.Contains(p))
				return 0;

			for (int tier = 1; tier <= op.TierCount; tier++)
			{
				if (CellRect(l, tier).Contains(p))
					return tier;
			}
			return 0;
		}

		// --- Input ---

		public override void LeftMouseDown(UIMouseEvent evt)
		{
			base.LeftMouseDown(evt);
			dragMoved = false; // every press starts fresh, so a drag never makes the next click on a button get ignored
			Rectangle r = GetDimensions().ToRectangle();
			Layout l = LayOut(r);
			if (!l.Track.Contains(evt.MousePosition.ToPoint()))
				return;

			dragging = true;
			velocity = 0f;
			dragStartMouseX = Main.MouseScreen.X;
			lastMouseX = Main.MouseScreen.X;
		}

		public override void ScrollWheel(UIScrollWheelEvent evt)
		{
			base.ScrollWheel(evt);
			Rectangle r = GetDimensions().ToRectangle();
			velocity = 0f;
			scroll = MathHelper.Clamp(scroll - evt.ScrollWheelValue * 0.5f, 0f, MaxScroll(LayOut(r), Operations.Current));
		}

		public override void LeftClick(UIMouseEvent evt)
		{
			base.LeftClick(evt);
			if (dragMoved)
				return; // the mouse was dragged: that was scrolling, not a click

			AutomataHubSystem.Guard(() =>
			{
				Operation op = Operations.Current;
				Rectangle r = GetDimensions().ToRectangle();
				Layout l = LayOut(r);
				Point p = evt.MousePosition.ToPoint();

				if (l.ClaimAll.Contains(p) && Operations.ClaimableCount(op) > 0)
				{
					int claimed = 0;
					for (int tier = 1; tier <= op.TierCount; tier++)
					{
						if (Operations.TryClaim(op, tier))
							claimed++;
					}
					if (claimed > 0)
					{
						SoundEngine.PlaySound(SoundID.Item35);
						Say(claimed + " REWARD" + (claimed > 1 ? "S" : "") + " CLAIMED");
					}
					return;
				}

				int hit = TierAt(l, op, p);
				if (hit == 0)
					return;

				OperationReward reward = op.Rewards[hit - 1];
				if (Operations.IsClaimable(op, hit))
				{
					if (Operations.TryClaim(op, hit))
					{
						SoundEngine.PlaySound(SoundID.Item35);
						Say("CLAIMED: " + reward.Name);
					}
				}
				else if (Operations.IsClaimed(op, hit) && reward.EquipSlot != null)
				{
					bool wasEquipped = Operations.IsEquipped(reward);
					Operations.Equip(reward);
					SoundEngine.PlaySound(SoundID.MenuOpen);
					Say(wasEquipped && reward.Kind != RewardKind.Scheme && reward.Kind != RewardKind.Frame ? "TAKEN OFF: " + reward.Name : "EQUIPPED: " + reward.Name);
				}
			});
		}

		private void Say(string text)
		{
			message = text;
			messageUntil = Environment.TickCount64 + 3500;
		}

		protected override void UpdateContent(GameTime gameTime)
		{
			if (!IsShown)
			{
				wasShown = false;
				dragging = false;
				return;
			}

			Rectangle r = GetDimensions().ToRectangle();
			if (r.Width < 100)
				return;

			Operation op = Operations.Current;
			Layout l = LayOut(r);
			trackClip.Place(l.Track, r);

			// On opening, bring the next reward (or the first one waiting to be claimed) into the middle of the window.
			if (!wasShown)
			{
				wasShown = true;
				int focus = Math.Min(op.TierCount, Operations.TierReached(op) + 1);
				for (int tier = 1; tier <= op.TierCount; tier++)
				{
					if (Operations.IsClaimable(op, tier))
					{
						focus = tier;
						break;
					}
				}
				scroll = (focus - 1) * (l.CellW + Gap) - (l.Track.Width - l.CellW) / 2f;
				velocity = 0f;
			}

			if (dragging)
			{
				if (!Main.mouseLeft)
				{
					dragging = false; // released: keep the speed so the row glides on
				}
				else
				{
					float x = Main.MouseScreen.X;
					float dx = x - lastMouseX;
					lastMouseX = x;
					scroll -= dx;
					velocity = -dx;
					if (Math.Abs(x - dragStartMouseX) > DragThreshold)
						dragMoved = true;
				}
			}
			else if (Math.Abs(velocity) > 0.05f)
			{
				scroll += velocity;
				velocity *= 0.92f;
			}
			else
			{
				velocity = 0f;
			}

			scroll = MathHelper.Clamp(scroll, 0f, MaxScroll(l, op));
		}

		// --- Drawing: the page around the row ---

		protected override void DrawContent(SpriteBatch sb)
		{
			Operation op = Operations.Current;
			Rectangle r = GetDimensions().ToRectangle();
			Layout l = LayOut(r);
			Point mouse = Main.MouseScreen.ToPoint();
			float t = HubClock.Seconds;

			HubDraw.Fill(sb, r, HubTheme.Panel);
			HubDraw.Border(sb, r, HubTheme.Line);
			MenuLook.DrawFrame(sb, r, HubTheme.Accent);

			// Header: badge, title, and a big tier readout on the right.
			var sq = new Rectangle(r.X + Pad, r.Y + 16, 44, 44);
			HubDraw.Fill(sb, sq, HubTheme.Accent);
			HubDraw.Brackets(sb, new Rectangle(sq.X - 3, sq.Y - 3, sq.Width + 6, sq.Height + 6), HubTheme.Warn, 8, 2);
			float badgeScale = HubDraw.BigScale(26f);
			float iw = HubDraw.BigWidth(op.Number, badgeScale, 1f);
			HubDraw.BigText(sb, op.Number, new Vector2(sq.X + (sq.Width - iw) / 2f, sq.Y + (sq.Height - 26f) / 2f - 1f), HubTheme.OnAccent, badgeScale, 1f);
			HubDraw.BigText(sb, "CAMPAIGN " + op.Number, new Vector2(r.X + Pad + 60, r.Y + 8), HubTheme.Text, HubDraw.BigScale(36f), 2f);
			HubDraw.Text(sb, op.Name, new Vector2(r.X + Pad + 61, r.Y + 46), HubTheme.Dim, 0.7f, 3f);

			int reached = Operations.TierReached(op);
			string ofText = "/ " + op.TierCount;
			float ofW = HubDraw.Width(ofText, 0.9f, 2f);
			HubDraw.TextRight(sb, ofText, r.Right - Pad, r.Y + 34, HubTheme.Dim, 0.9f, 2f);
			string big = reached.ToString("00");
			float bigScale = HubDraw.BigScale(54f);
			float bigW = HubDraw.BigWidth(big, bigScale, 2f);
			HubDraw.BigTextRight(sb, big, r.Right - Pad - (int)ofW - 14, r.Y + 8, HubTheme.Warn, bigScale, 2f);
			HubDraw.Text(sb, "TIER", new Vector2(r.Right - Pad - ofW - 14 - bigW, r.Y + 4), HubTheme.Dim, 0.52f, 3f);

			// The balance, with the crystal, in the middle of the header.
			int balX = r.X + r.Width / 2 - 70;
			OperationArt.Fragment(sb, new Rectangle(balX, r.Y + 14, 34, 34), MenuLook.Current.Amber);
			HubDraw.Text(sb, "DATA FRAGMENTS", new Vector2(balX + 42, r.Y + 14), HubTheme.Dim, 0.52f, 2f);
			HubDraw.BigText(sb, AccountStore.Balance.ToString(), new Vector2(balX + 42, r.Y + 27), HubTheme.Text, HubDraw.BigScale(24f), 1f);
			HubDraw.HLine(sb, r.X + Pad, r.Y + 76, r.Width - Pad * 2, HubTheme.Line);

			// XP bar: thick, with tier marks, a moving shine on the filled part, and a text line above it.
			string xpText = reached >= op.TierCount
				? "CAMPAIGN COMPLETE"
				: (Operations.Xp(op) % op.XpPerTier) + " / " + op.XpPerTier + " XP TO TIER " + (reached + 1);
			HubDraw.Text(sb, xpText, new Vector2(l.XpBar.X, l.XpBar.Y - 17), HubTheme.Dim, 0.6f, 2f);
			HubDraw.Fill(sb, l.XpBar, HubTheme.PanelAlt);
			HubDraw.Border(sb, l.XpBar, HubTheme.Line, 2);
			int fill = (int)((l.XpBar.Width - 6) * Operations.ProgressToNext(op));
			if (fill > 0)
			{
				HubDraw.Fill(sb, new Rectangle(l.XpBar.X + 3, l.XpBar.Y + 3, fill, l.XpBar.Height - 6), HubTheme.Accent);
				if (fill > 14)
				{
					int shine = (int)(((t * 0.5f) % 1f) * (fill + 36f)) - 18;
					int s0 = Math.Max(0, shine);
					int s1 = Math.Min(fill, shine + 16);
					if (s1 > s0)
						HubDraw.Fill(sb, new Rectangle(l.XpBar.X + 3 + s0, l.XpBar.Y + 3, s1 - s0, l.XpBar.Height - 6), Color.White * 0.4f);
				}
			}
			for (int tick = 1; tick < 4; tick++)
				HubDraw.Fill(sb, new Rectangle(l.XpBar.X + 3 + (l.XpBar.Width - 6) * tick / 4, l.XpBar.Bottom - 7, 1, 4), HubTheme.Line);

			// Claim all: flashes when something is waiting.
			int ready = Operations.ClaimableCount(op);
			bool canClaim = ready > 0;
			bool hoverClaim = canClaim && l.ClaimAll.Contains(mouse);
			Color claimFill = !canClaim ? HubTheme.PanelAlt : (hoverClaim ? HubTheme.Accent : Color.Lerp(HubTheme.Warn, HubTheme.Accent, Wave(t, 1.4f)));
			HubDraw.Fill(sb, l.ClaimAll, claimFill);
			HubDraw.Border(sb, l.ClaimAll, canClaim ? HubTheme.Warn : HubTheme.LineSoft, 2);
			string claimLabel = canClaim ? "CLAIM ALL (" + ready + ")" : "NOTHING TO CLAIM";
			float cs = 0.68f;
			float cw = HubDraw.Width(claimLabel, cs, 2f);
			if (cw > l.ClaimAll.Width - 16)
				cs *= (l.ClaimAll.Width - 16) / cw;
			cw = HubDraw.Width(claimLabel, cs, 2f);
			HubDraw.Text(sb, claimLabel, new Vector2(l.ClaimAll.X + (l.ClaimAll.Width - cw) / 2f, l.ClaimAll.Y + (l.ClaimAll.Height - HubDraw.LineHeight(cs)) / 2f),
				!canClaim ? HubTheme.Dim : HubTheme.OnAccent, cs, 2f);

			// (the rail and the cards are drawn by the clipping region, DrawTrack)

			// Scroll indicator under the row.
			float total = TotalWidth(l, op);
			float maxScroll = MaxScroll(l, op);
			if (maxScroll > 0f)
			{
				var rail = new Rectangle(l.Track.X, l.Track.Bottom - 6, l.Track.Width, 4);
				HubDraw.Fill(sb, rail, HubTheme.LineSoft);
				float thumbW = Math.Max(30f, rail.Width * (l.Track.Width / total));
				float thumbX = rail.X + (rail.Width - thumbW) * (scroll / maxScroll);
				HubDraw.Fill(sb, new Rectangle((int)thumbX, rail.Y, (int)thumbW, rail.Height), HubTheme.Accent);
			}

			DrawDetail(sb, l.Detail, op, TierAt(l, op, mouse));

			HubDraw.TextFit(sb, "DRAG THE ROW OR USE THE MOUSE WHEEL TO SEE MORE TIERS     XP: 5 A MINUTE OF ACTIVE PLAY, PLUS EVERY ORDER'S FRAGMENT REWARD     NO TIMER, THIS CAMPAIGN NEVER EXPIRES",
				new Vector2(r.X + Pad, r.Bottom - 26), HubTheme.Dim, 0.6f, 2f, r.Width - Pad * 2);
		}

		// --- Drawing: the rail and the cards (inside the clipping region) ---

		private void DrawTrack(SpriteBatch sb, Rectangle region)
		{
			Operation op = Operations.Current;
			Rectangle r = GetDimensions().ToRectangle();
			Layout l = LayOut(r);
			Point mouse = Main.MouseScreen.ToPoint();
			float t = HubClock.Seconds;
			int hoverTier = dragging && dragMoved ? 0 : TierAt(l, op, mouse);
			int reached = Operations.TierReached(op);

			// The rail: a dim line through every tier, filled up to how far you have got, with a bright head.
			int step = l.CellW + Gap;
			int firstCenter = l.Track.X + l.CellW / 2 - (int)scroll;
			int railLeft = firstCenter - l.CellW / 2 - Gap;
			int railRight = firstCenter + (op.TierCount - 1) * step + l.CellW / 2 + Gap;
			HubDraw.Fill(sb, new Rectangle(railLeft, l.RailY - 2, railRight - railLeft, 4), HubTheme.LineSoft);

			float progressTiers = reached >= op.TierCount ? op.TierCount : reached + Operations.ProgressToNext(op);
			int headX = progressTiers >= 1f ? firstCenter + (int)((progressTiers - 1f) * step) : railLeft + (int)(progressTiers * (firstCenter - railLeft));
			if (headX > railLeft)
			{
				HubDraw.Fill(sb, new Rectangle(railLeft, l.RailY - 2, headX - railLeft, 4), HubTheme.Accent);
				HubDraw.Diamond(sb, new Vector2(headX, l.RailY), 6f + 1.5f * Wave(t, 1.2f), HubTheme.Warn);
			}

			for (int tier = 1; tier <= op.TierCount; tier++)
			{
				Rectangle cell = CellRect(l, tier);
				if (cell.Right < l.Track.X - 4 || cell.X > l.Track.Right + 4)
					continue;

				// The node on the rail above the card.
				Color kind = KindColor(op.Rewards[tier - 1].Kind);
				bool nodeReached = tier <= reached;
				HubDraw.Diamond(sb, new Vector2(cell.Center.X, l.RailY), 8f, HubTheme.Panel);
				if (nodeReached)
					HubDraw.Diamond(sb, new Vector2(cell.Center.X, l.RailY), 6f, kind);
				else if (tier == reached + 1)
					HubDraw.Diamond(sb, new Vector2(cell.Center.X, l.RailY), 5f, kind * (0.4f + 0.6f * Wave(t, 1.4f)));
				else
					HubDraw.Diamond(sb, new Vector2(cell.Center.X, l.RailY), 4f, HubTheme.Line);

				DrawCell(sb, cell, op, tier, tier == hoverTier, reached, t);
			}
		}

		private static void DrawCell(SpriteBatch sb, Rectangle cell, Operation op, int tier, bool hovered, int reached, float t)
		{
			OperationReward reward = op.Rewards[tier - 1];
			bool isReached = tier <= reached;
			bool claimed = Operations.IsClaimed(op, tier);
			bool claimable = Operations.IsClaimable(op, tier);
			bool equipped = claimed && reward.EquipSlot != null && Operations.IsEquipped(reward);
			bool isNext = tier == reached + 1;
			Color kind = KindColor(reward.Kind);
			float pulse = Wave(t, 1.4f);

			// Card body and border: a claimable card pulses, the equipped one is outlined, the one you are working towards is marked.
			HubDraw.Fill(sb, cell, claimed ? HubTheme.PanelAlt * 0.8f : HubTheme.PanelAlt);
			Color border = claimable ? Color.Lerp(HubTheme.Warn, HubTheme.Text, pulse * 0.4f) : (equipped ? HubTheme.Accent : (hovered ? HubTheme.Text : (isNext ? kind : HubTheme.LineSoft)));
			HubDraw.Border(sb, cell, border, claimable || equipped || isNext ? 2 : 1);

			// The kind band at the top: coloured by kind, dimmer while the tier is still locked.
			var band = new Rectangle(cell.X + 1, cell.Y + 1, cell.Width - 2, 26);
			HubDraw.Fill(sb, band, isReached ? kind : kind * 0.55f);
			string kindLabel = reward.KindLabel;
			float ks = 0.5f;
			float kw = HubDraw.Width(kindLabel, ks, 2f);
			if (kw > band.Width - 12)
				ks *= (band.Width - 12) / kw;
			HubDraw.Text(sb, kindLabel, new Vector2(band.X + (band.Width - HubDraw.Width(kindLabel, ks, 2f)) / 2f, band.Y + (band.Height - HubDraw.LineHeight(ks)) / 2f), new Color(24, 26, 30), ks, 2f);

			// Tier number, and a tag on the special rewards that cannot be bought.
			HubDraw.Text(sb, "TIER " + tier, new Vector2(cell.X + 10, band.Bottom + 8), isReached ? HubTheme.Text : HubTheme.Dim, 0.6f, 2f);
			if (reward.Kind == RewardKind.Scheme || reward.Kind == RewardKind.Frame)
				HubDraw.TextRight(sb, "EXCLUSIVE", cell.Right - 10, band.Bottom + 10, kind, 0.46f, 2f);

			// The reward picture on a pedestal. A claimable one gets a glow behind it and sparkles around it.
			int size = Math.Max(64, Math.Min(cell.Width - 36, cell.Height - 178));
			var ped = new Rectangle(cell.Center.X - size / 2, band.Bottom + 30, size, size);
			if (claimable)
				HubDraw.Fill(sb, new Rectangle(ped.X - 5, ped.Y - 5, ped.Width + 10, ped.Height + 10), kind * (0.14f + 0.12f * pulse));
			HubDraw.Fill(sb, ped, HubTheme.Panel * 0.55f);
			HubDraw.Border(sb, ped, claimable ? kind : HubTheme.LineSoft);
			HubDraw.Corners(sb, ped, claimable ? kind : HubTheme.Line, 7, 2);
			var icon = new Rectangle(ped.X + 12, ped.Y + 12, ped.Width - 24, ped.Height - 24);
			OperationArt.RewardIcon(sb, reward, icon, isReached ? Color.White : new Color(255, 255, 255, 150));

			if (!isReached)
			{
				HubDraw.Fill(sb, ped, HubTheme.Panel * 0.3f);
				DrawLock(sb, new Rectangle(ped.Right - 20, ped.Bottom - 20, 14, 14), HubTheme.Dim);
			}
			if (claimable)
			{
				for (int i = 0; i < 3; i++)
				{
					float a = t * 1.6f + i * 2.1f;
					float k = Math.Max(0f, (float)Math.Sin(a * 1.7f));
					var pos = new Vector2(ped.Center.X + (float)Math.Cos(a) * ped.Width * 0.5f, ped.Center.Y + (float)Math.Sin(a * 1.3f) * ped.Height * 0.5f);
					OperationArt.Sparkle(sb, pos, 3.5f * k + 0.5f, Color.White * (0.4f + 0.5f * k));
				}
			}

			// The reward's name, and what it does.
			string name = reward.Name;
			float ns = 0.95f;
			float nw = HubDraw.Width(name, ns, 1.5f);
			float room = cell.Width - 16f;
			if (nw > room)
				ns *= room / nw;
			float ny = ped.Bottom + 14;
			HubDraw.Text(sb, name, new Vector2(cell.X + (cell.Width - HubDraw.Width(name, ns, 1.5f)) / 2f, ny), claimed ? HubTheme.Dim : HubTheme.Text, ns, 1.5f);

			float dy = ny + HubDraw.LineHeight(ns) + 6;
			int lines = 0;
			foreach (string line in WrapCached(reward.Description, cell.Width - 20))
			{
				if (dy + HubDraw.LineHeight(0.5f) > cell.Bottom - 40 || lines >= 3)
					break;
				HubDraw.Text(sb, line, new Vector2(cell.X + (cell.Width - HubDraw.Width(line, 0.5f)) / 2f, dy), HubTheme.Dim, 0.5f);
				dy += HubDraw.LineHeight(0.5f) + 2;
				lines++;
			}

			// The state pill at the bottom of the card.
			var pill = new Rectangle(cell.X + 10, cell.Bottom - 32, cell.Width - 20, 22);
			string state;
			Color pillFill;
			Color pillText;
			if (claimable)
			{
				state = "CLAIM";
				pillFill = Color.Lerp(HubTheme.Warn, kind, pulse * 0.5f);
				pillText = new Color(24, 26, 30);
			}
			else if (equipped)
			{
				state = "EQUIPPED";
				pillFill = HubTheme.Accent;
				pillText = HubTheme.OnAccent;
			}
			else if (claimed)
			{
				state = "CLAIMED";
				pillFill = HubTheme.PanelAlt;
				pillText = HubTheme.Dim;
			}
			else
			{
				long togo = (long)tier * op.XpPerTier - Operations.Xp(op);
				state = togo > 0 ? togo + " XP TO GO" : "LOCKED";
				pillFill = HubTheme.PanelAlt;
				pillText = HubTheme.Dim;
			}

			HubDraw.Fill(sb, pill, pillFill);
			HubDraw.Border(sb, pill, claimable ? HubTheme.Warn : (equipped ? HubTheme.Accent : HubTheme.LineSoft));
			float ss = 0.56f;
			float sw = HubDraw.Width(state, ss, 2f);
			if (sw > pill.Width - 24)
				ss *= (pill.Width - 24) / sw;
			sw = HubDraw.Width(state, ss, 2f);
			HubDraw.Text(sb, state, new Vector2(pill.X + (pill.Width - sw) / 2f, pill.Y + (pill.Height - HubDraw.LineHeight(ss)) / 2f), pillText, ss, 2f);
			if (claimed && !equipped)
				DrawCheck(sb, new Point(pill.X + 14, pill.Center.Y), pillText);
		}

		// Wrapping text measures every word, so the result is remembered (the cards ask every frame).
		private static readonly System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>> wrapCache = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>>();

		private static System.Collections.Generic.List<string> WrapCached(string text, int width)
		{
			string key = width + "|" + text;
			if (!wrapCache.TryGetValue(key, out System.Collections.Generic.List<string> lines))
			{
				lines = HubDraw.Wrap(text, width, 0.5f);
				wrapCache[key] = lines;
			}
			return lines;
		}

		private static void DrawLock(SpriteBatch sb, Rectangle box, Color c)
		{
			// A padlock made of smooth shapes: a body, and an arched shackle on top.
			float cx = box.X + box.Width / 2f;
			float bodyTop = box.Y + box.Height * 0.45f;
			Span<Vector2> body = stackalloc Vector2[]
			{
				new Vector2(box.X, bodyTop), new Vector2(box.Right, bodyTop), new Vector2(box.Right, box.Bottom), new Vector2(box.X, box.Bottom)
			};
			if (!HubDraw.FillPolygon(sb, body, c))
				HubDraw.Fill(sb, new Rectangle(box.X, (int)bodyTop, box.Width, box.Bottom - (int)bodyTop), c);

			float outer = box.Width * 0.29f;      // the shackle's outer radius
			float inner = outer - 1.7f;           // and its inner radius (the wire is 1.7 units thick)
			float arcY = box.Y + outer;
			const int steps = 10;
			var pts = new Vector2[(steps + 1) * 2 + 4];
			int n = 0;
			pts[n++] = new Vector2(cx - outer, bodyTop + 0.5f);
			for (int i = 0; i <= steps; i++)
			{
				float a = MathHelper.Pi + MathHelper.Pi * i / steps;
				pts[n++] = new Vector2(cx + outer * (float)Math.Cos(a), arcY + outer * (float)Math.Sin(a));
			}
			pts[n++] = new Vector2(cx + outer, bodyTop + 0.5f);
			pts[n++] = new Vector2(cx + inner, bodyTop + 0.5f);
			for (int i = steps; i >= 0; i--)
			{
				float a = MathHelper.Pi + MathHelper.Pi * i / steps;
				pts[n++] = new Vector2(cx + inner * (float)Math.Cos(a), arcY + inner * (float)Math.Sin(a));
			}
			pts[n++] = new Vector2(cx - inner, bodyTop + 0.5f);
			if (!HubDraw.FillPolygon(sb, pts, c))
			{
				var shackle = new Rectangle(box.X + 3, box.Y, box.Width - 6, box.Height / 2 + 1);
				HubDraw.Border(sb, shackle, c, 2);
			}
		}
		private static void DrawCheck(SpriteBatch sb, Point p, Color c)
		{
			for (int i = 0; i < 3; i++)
				HubDraw.Fill(sb, new Rectangle(p.X - 4 + i, p.Y - 1 + i, 2, 2), c);
			for (int i = 0; i < 5; i++)
				HubDraw.Fill(sb, new Rectangle(p.X - 1 + i, p.Y + 1 - i, 2, 2), c);
		}

		private void DrawDetail(SpriteBatch sb, Rectangle box, Operation op, int tier)
		{
			HubDraw.Fill(sb, box, HubTheme.PanelAlt);
			HubDraw.Border(sb, box, HubTheme.LineSoft);

			if (Environment.TickCount64 <= messageUntil)
			{
				HubDraw.Text(sb, message, new Vector2(box.X + 16, box.Y + (box.Height - HubDraw.LineHeight(0.85f)) / 2f), HubTheme.Warn, 0.85f, 2f);
				return;
			}

			if (tier == 0)
			{
				HubDraw.Text(sb, "POINT AT A TIER TO SEE ITS REWARD", new Vector2(box.X + 16, box.Y + (box.Height - HubDraw.LineHeight(0.66f)) / 2f), HubTheme.Dim, 0.66f, 2f);
				return;
			}

			OperationReward reward = op.Rewards[tier - 1];
			bool claimed = Operations.IsClaimed(op, tier);
			bool claimable = Operations.IsClaimable(op, tier);

			// A small picture of the reward at the left, then its kind, name and description.
			var thumb = new Rectangle(box.X + 8, box.Y + 6, box.Height - 12, box.Height - 12);
			HubDraw.Fill(sb, thumb, HubTheme.Panel * 0.55f);
			HubDraw.Border(sb, thumb, KindColor(reward.Kind));
			OperationArt.RewardIcon(sb, reward, new Rectangle(thumb.X + 4, thumb.Y + 4, thumb.Width - 8, thumb.Height - 8), Color.White);

			int tx = thumb.Right + 14;
			HubDraw.Text(sb, "TIER " + tier + "  //  " + reward.KindLabel, new Vector2(tx, box.Y + 6), KindColor(reward.Kind), 0.56f, 2f);
			HubDraw.Text(sb, reward.Name, new Vector2(tx, box.Y + 22), HubTheme.Text, 0.9f, 2f);
			HubDraw.TextFit(sb, reward.Description, new Vector2(tx, box.Y + 42), HubTheme.Dim, 0.56f, 0f, box.Width / 2f);

			string status;
			if (claimable)
				status = "CLICK TO CLAIM";
			else if (claimed && reward.EquipSlot != null)
				status = Operations.IsEquipped(reward)
					? (reward.Kind == RewardKind.Scheme || reward.Kind == RewardKind.Frame ? "EQUIPPED" : "EQUIPPED   CLICK TO TAKE OFF")
					: "CLICK TO EQUIP";
			else if (claimed)
				status = "CLAIMED";
			else
				status = Math.Max(0, tier * op.XpPerTier - (int)Math.Min(int.MaxValue, Operations.Xp(op))) + " XP TO GO";

			HubDraw.TextRight(sb, status, box.Right - 16, box.Y + (box.Height - HubDraw.LineHeight(0.72f)) / 2f, claimable ? HubTheme.Warn : HubTheme.Dim, 0.72f, 2f);
		}
	}
	// The Operation banner on the home screen, under the preview: wide, bright and moving so it is hard to miss. It shows the
	// tier, the XP bar, the next reward as a floating picture, and flashes when rewards are waiting. Clicking it opens the OPERATION
	// page. All the motion comes from the clock; nothing here changes game data.
	internal class HomeOperationCard : HubElementBase
	{
		private readonly AutomataHomeUIState state;
		private bool hovered;

		public HomeOperationCard(AutomataHomeUIState state)
		{
			this.state = state;
		}

		public override void MouseOver(UIMouseEvent evt)
		{
			base.MouseOver(evt);
			hovered = true;
			SoundEngine.PlaySound(SoundID.MenuTick);
		}

		public override void MouseOut(UIMouseEvent evt)
		{
			base.MouseOut(evt);
			hovered = false;
		}

		public override void LeftClick(UIMouseEvent evt)
		{
			base.LeftClick(evt);
			AutomataHubSystem.Guard(() => state.SetSection(HomeSection.Operation));
		}

		// 0..1 repeating wave, `speed` cycles per second.
		private static float Wave(float seconds, float speed) => 0.5f + 0.5f * (float)Math.Sin(seconds * speed * MathHelper.TwoPi);

		protected override void DrawContent(SpriteBatch sb)
		{
			Operation op = Operations.Current;
			Rectangle r = GetDimensions().ToRectangle();
			float t = HubClock.Seconds; // the smooth clock, not TickCount64 (see HubClock)
			float pulse = Wave(t, 0.7f);
			int ready = Operations.ClaimableCount(op);
			Color bright = AutomataHomeUIState.Ivory;

			// A pulsing halo, kept inside the 12 unit gap above and below so it never touches its neighbours.
			for (int i = 0; i < 2; i++)
			{
				int grow = 2 + i * 2 + (int)(pulse * 2f);
				HubDraw.Border(sb, new Rectangle(r.X - grow, r.Y - grow, r.Width + grow * 2, r.Height + grow * 2), bright * ((0.3f - i * 0.1f) * (0.5f + pulse * 0.5f) * (hovered ? 1.5f : 1f)));
			}

			HubDraw.Fill(sb, r, AutomataHomeUIState.Backing * 0.96f);
			HubDraw.Border(sb, r, ready > 0 ? Color.Lerp(bright, AutomataHomeUIState.Amber, Wave(t, 1.6f)) : bright * (hovered ? 1f : 0.75f), 2);

			// --- Left block: solid bright, with stripes sliding across it ---
			var block = new Rectangle(r.X, r.Y, 124, r.Height);
			HubDraw.Fill(sb, block, bright);
			int shift = (int)(t * 40f) % 16;
			for (int sx = block.X - 16 + shift; sx < block.Right; sx += 16)
			{
				int x0 = Math.Max(sx, block.X);
				int x1 = Math.Min(sx + 6, block.Right);
				if (x1 > x0)
					HubDraw.Fill(sb, new Rectangle(x0, block.Y, x1 - x0, block.Height), AutomataHomeUIState.Charcoal * 0.1f);
			}

			if ((int)(t * 2f) % 2 == 0)
				HubDraw.Fill(sb, new Rectangle(block.X + 12, block.Y + 13, 9, 9), AutomataHomeUIState.Charcoal);
			HubDraw.Text(sb, "CAMPAIGN", new Vector2(block.X + 28, block.Y + 8), AutomataHomeUIState.Charcoal, 0.66f, 2.5f);
			HubDraw.BigText(sb, op.Number, new Vector2(block.X + 12, block.Y + 24), AutomataHomeUIState.Charcoal, HubDraw.BigScale(48f), 2f);
			HubDraw.Text(sb, "LIVE", new Vector2(block.Right - 12 - HubDraw.Width("LIVE", 0.54f, 3f), block.Bottom - 20), AutomataHomeUIState.Charcoal, 0.54f, 3f);

			// --- Middle: name (with a short colour-split glitch now and then), tier, XP bar ---
			bool narrow = r.Width < 620;
			int rightW = narrow ? 76 : 260;
			int mx = block.Right + 18;
			int mw = r.Right - rightW - mx - 14;

			float ns = 1.0f;
			float nw = HubDraw.Width(op.Name, ns, 2.5f);
			if (nw > mw)
				ns *= mw / nw;
			int ny = r.Y + 8;
			if (t % 3.2f < 0.28f)
			{
				HubDraw.Text(sb, op.Name, new Vector2(mx - 2, ny), new Color(215, 85, 75) * 0.6f, ns, 2.5f);
				HubDraw.Text(sb, op.Name, new Vector2(mx + 2, ny), new Color(60, 170, 185) * 0.6f, ns, 2.5f);
			}
			HubDraw.Text(sb, op.Name, new Vector2(mx, ny), bright, ns, 2.5f);

			int reached = Operations.TierReached(op);
			HubDraw.BigText(sb, "TIER " + reached.ToString("00"), new Vector2(mx, r.Y + 33), AutomataHomeUIState.Idle, HubDraw.BigScale(26f), 2f);
			HubDraw.TextRight(sb, "OF " + op.TierCount, mx + mw, r.Y + 40, AutomataHomeUIState.Muted, 0.62f, 3f);

			var bar = new Rectangle(mx, r.Y + 66, mw, 16);
			HubDraw.Fill(sb, bar, AutomataHomeUIState.Charcoal * 0.7f);
			HubDraw.Border(sb, bar, bright * 0.8f);
			int fill = (int)((bar.Width - 4) * Operations.ProgressToNext(op));
			HubDraw.Fill(sb, new Rectangle(bar.X + 2, bar.Y + 2, fill, bar.Height - 4), bright);
			if (fill > 12)
			{
				// A bright shine sweeping along the filled part.
				int shine = (int)(((t * 0.6f) % 1f) * (fill + 30f)) - 15;
				int sx0 = Math.Max(0, shine);
				int sx1 = Math.Min(fill, shine + 14);
				if (sx1 > sx0)
					HubDraw.Fill(sb, new Rectangle(bar.X + 2 + sx0, bar.Y + 2, sx1 - sx0, bar.Height - 4), Color.White * 0.7f);
			}
			for (int tick = 1; tick < 10; tick++)
				HubDraw.Fill(sb, new Rectangle(bar.X + 2 + (bar.Width - 4) * tick / 10, bar.Bottom - 5, 1, 3), AutomataHomeUIState.Charcoal * 0.6f);

			// --- Right: the next reward as a floating picture, with its name ---
			int rx = r.Right - rightW;
			int next = Operations.NextTier(op);
			var picture = new Rectangle(rx, r.Y + 12, 56, 56);
			HubDraw.Border(sb, picture, bright * 0.6f);
			if (next != 0)
			{
				OperationReward reward = op.Rewards[next - 1];
				int bob = (int)Math.Round(Math.Sin(t * 2.4f) * 4f);
				OperationArt.RewardIcon(sb, reward, new Rectangle(picture.X + 10, picture.Y + 10 + bob, 36, 36), Color.White);

				if (!narrow)
				{
					int textX = picture.Right + 12;
					int textW = r.Right - 14 - textX;
					HubDraw.Text(sb, "NEXT  T" + next, new Vector2(textX, picture.Y + 6), AutomataHomeUIState.Muted, 0.56f, 2f);
					float rs = 0.78f;
					float rw = HubDraw.Width(reward.Name, rs, 1.5f);
					if (rw > textW)
						rs *= textW / rw;
					HubDraw.Text(sb, reward.Name, new Vector2(textX, picture.Y + 26), bright, rs, 1.5f);
				}
			}
			else if (!narrow)
			{
				HubDraw.Text(sb, "ALL REWARDS REACHED", new Vector2(picture.Right + 12, picture.Y + 20), bright, 0.66f, 2f);
			}

			// --- Bottom right: flashes when rewards are waiting, otherwise invites a click ---
			var strip = new Rectangle(rx, r.Bottom - 26, r.Right - 14 - rx, 18);
			if (ready > 0)
			{
				HubDraw.Fill(sb, strip, Color.Lerp(bright, AutomataHomeUIState.Amber, Wave(t, 1.6f)));
				string text = ready + (ready > 1 ? " REWARDS READY" : " REWARD READY");
				float ts = 0.58f;
				float tw = HubDraw.Width(text, ts, 2f);
				if (tw > strip.Width - 8)
					ts *= (strip.Width - 8) / tw;
				tw = HubDraw.Width(text, ts, 2f);
				HubDraw.Text(sb, text, new Vector2(strip.X + (strip.Width - tw) / 2f, strip.Y + (strip.Height - HubDraw.LineHeight(ts)) / 2f), AutomataHomeUIState.Charcoal, ts, 2f);
			}
			else
			{
				HubDraw.Border(sb, strip, bright * (hovered ? 1f : 0.55f));
				string text = narrow ? "OPEN" : "OPEN CAMPAIGN";
				float tw = HubDraw.Width(text, 0.54f, 2f);
				HubDraw.Text(sb, text, new Vector2(strip.X + (strip.Width - tw) / 2f, strip.Y + (strip.Height - HubDraw.LineHeight(0.54f)) / 2f), bright, 0.54f, 2f);
			}

			// --- A faint scanline sweeping across the banner ---
			float sweep = (t * 0.4f) % 1f;
			int sxs = r.X + (int)(sweep * (r.Width - 2));
			HubDraw.Fill(sb, new Rectangle(sxs, r.Y + 1, 2, r.Height - 2), bright * 0.2f);
		}
	}
}
