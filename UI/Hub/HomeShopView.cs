using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.UI;

namespace Augments
{
	// SHOP: spend Data Fragments on title-screen cosmetics. Data Fragments belong to the account, not to a character, and are earned
	// only by playing with the mod and by completing Orders (see AccountStore), so nothing here depends on a character's items.
	//
	// Pick an item on the left; the panel on the right shows it with its price and a button. Buying is a two-step action (BUY, then
	// CONFIRM within a few seconds) so a stray click cannot spend your fragments. A bought item can be equipped from here or from COSMETICS.
	internal class HomeShopView : HubElementBase
	{
		private const int Pad = 24;

		private sealed class ShopItem
		{
			public RewardKind Kind;        // Scheme, Frame, PodSkin, Title or Emblem
			public int Index;              // into MenuLook.All or MenuLook.Frames (schemes and frames only)
			public OperationReward Reward; // the item itself (Pod skins, titles and emblems only)
			public string Name;
			public string Blurb;
			public int Price;
			public string UnlockId;
			public string Hint;            // for a secret reward: the riddle or hint that names it
			public bool FromUnits;         // true for the Fallen Unit rewards (the text says where they came from)

			// An ending reward has no price: it is earned, never bought. Until then it shows as a secret with only the riddle.
			public bool Earned => Price < 0;
			public bool Locked => Earned && !Owned;
			public string DisplayName => Locked ? (Kind == RewardKind.PodSkin ? "SECRET POD" : "SECRET TITLE") : Name;
			public string ShownBlurb => !Earned ? Blurb : (Locked ? (FromUnits ? "A secret reward. Hint: \"" : "Earned by finishing a hidden ending. Hint: \"") + Hint + "\"" : Blurb + (FromUnits ? " Earned from the Fallen Units." : " Earned from a hidden ending."));

			public bool IsScheme => Kind == RewardKind.Scheme;
			public bool IsFrame => Kind == RewardKind.Frame;
			public bool IsTier => Kind == RewardKind.TierDesign;

			public string KindLabel
			{
				get
				{
					switch (Kind)
					{
						case RewardKind.Scheme: return "COLOUR SCHEME";
						case RewardKind.Frame: return "PANEL FRAME";
						case RewardKind.TierDesign: return "TIER DESIGN";
						default: return Reward.KindLabel;
					}
				}
			}

			public bool Owned
			{
				get
				{
					switch (Kind)
					{
						case RewardKind.Scheme: return MenuLook.SchemeOwned(Index);
						case RewardKind.Frame: return MenuLook.FrameOwned(Index);
						case RewardKind.TierDesign: return MenuLook.TierDesignOwned(Index);
						default: return Operations.IsOwned(Reward);
					}
				}
			}

			public bool Equipped
			{
				get
				{
					switch (Kind)
					{
						case RewardKind.Scheme: return MenuLook.SelectedIndex == Index;
						case RewardKind.Frame: return MenuLook.FrameIndex == Index;
						case RewardKind.TierDesign: return MenuLook.TierDesignIndex == Index;
						default: return Operations.IsEquipped(Reward);
					}
				}
			}
		}

		// The shelves, in tab order. Each tab shows one kind of item.
		private static readonly RewardKind[] Categories = { RewardKind.Scheme, RewardKind.Frame, RewardKind.TierDesign, RewardKind.PodSkin, RewardKind.Title, RewardKind.Emblem };
		private static readonly string[] CategoryNames = { "SCHEMES", "FRAMES", "TIERS", "PODS", "TITLES", "EMBLEMS" };

		private static ShopItem MakeItem(RewardKind kind, string id, int price)
		{
			var reward = new OperationReward { Kind = kind, Id = id };
			return new ShopItem { Kind = kind, Reward = reward, Name = reward.Name, Blurb = reward.Description, Price = price, UnlockId = reward.UnlockId };
		}

		private static List<ShopItem> items;

		// Everything for sale: every colour scheme and frame that costs something (the free ones are already yours).
		private static List<ShopItem> Items
		{
			get
			{
				if (items != null)
					return items;

				items = new List<ShopItem>();
				for (int i = 1; i < MenuLook.All.Length; i++)
					if (MenuLook.SchemePrices[i] >= 0) items.Add(new ShopItem { Kind = RewardKind.Scheme, Index = i, Name = MenuLook.All[i].Name, Blurb = MenuLook.All[i].Blurb, Price = MenuLook.SchemePrices[i], UnlockId = MenuLook.SchemeUnlockId(i) });
				for (int i = 1; i < MenuLook.Frames.Length; i++)
					if (MenuLook.FramePrices[i] >= 0) items.Add(new ShopItem { Kind = RewardKind.Frame, Index = i, Name = MenuLook.Frames[i].Name, Blurb = MenuLook.Frames[i].Blurb, Price = MenuLook.FramePrices[i], UnlockId = MenuLook.FrameUnlockId(i) });
				for (int i = 1; i < MenuLook.TierDesigns.Length; i++)
					if (MenuLook.TierDesignPrices[i] > 0) items.Add(new ShopItem { Kind = RewardKind.TierDesign, Index = i, Name = MenuLook.TierDesigns[i].Name, Blurb = MenuLook.TierDesigns[i].Blurb.Replace("\n", " "), Price = MenuLook.TierDesignPrices[i], UnlockId = MenuLook.TierDesignUnlockId(i) });
				foreach (Cosmetic.PodSkinInfo skin in Cosmetic.PodSkins)
					if (Cosmetic.PodSkinPrice(skin.Id) >= 0) items.Add(MakeItem(RewardKind.PodSkin, skin.Id, Cosmetic.PodSkinPrice(skin.Id)));
				foreach (string[] title in Cosmetic.Titles)
					if (Cosmetic.TitlePrice(title[0]) >= 0) items.Add(MakeItem(RewardKind.Title, title[0], Cosmetic.TitlePrice(title[0])));
				// The hidden-ending rewards (a Pod skin and a title for each ending). Not for sale: they show as secrets until earned.
				for (int e = 0; e < Endings.All.Length; e++)
				{
					ShopItem pod = MakeItem(RewardKind.PodSkin, Endings.RewardPods[e], -1);
					pod.Hint = Endings.All[e].Riddle;
					items.Add(pod);
					ShopItem title = MakeItem(RewardKind.Title, Endings.RewardTitles[e], -1);
					title.Hint = Endings.All[e].Riddle;
					items.Add(title);
				}
				// The Fallen Unit rewards (see FallenUnitLogs). Also secrets until earned.
				for (int r = 0; r < FallenUnitLogs.RewardIds.Length; r++)
				{
					ShopItem pod = MakeItem(RewardKind.PodSkin, FallenUnitLogs.RewardIds[r], -1);
					pod.Hint = FallenUnitLogs.RewardHints[r];
						pod.FromUnits = true;
					items.Add(pod);
					ShopItem title = MakeItem(RewardKind.Title, FallenUnitLogs.RewardIds[r], -1);
					title.Hint = FallenUnitLogs.RewardHints[r];
						title.FromUnits = true;
					items.Add(title);
				}
				foreach (string[] emblem in Cosmetic.Emblems)
					if (Cosmetic.EmblemPrice(emblem[0]) >= 0) items.Add(MakeItem(RewardKind.Emblem, emblem[0], Cosmetic.EmblemPrice(emblem[0])));
				return items;
			}
		}

		private readonly HubClipRegion gridClip;
		private float scrollY;          // how far the card grid is scrolled (the Pods and Titles tabs hold more cards than fit)

		public HomeShopView()
		{
			gridClip = new HubClipRegion(DrawGrid) { VisibleWhen = () => IsShown };
			Append(gridClip);
		}

		private int selected;
		private int category;           // which tab is open
		private List<ShopItem> shown;    // the items on the open tab
		private List<ShopItem> Shown => shown ??= Items.FindAll(i => i.Kind == Categories[category]);
		private long confirmUntil;   // Environment.TickCount64 until which a BUY press is waiting for its CONFIRM
		private string message = "";
		private long messageUntil;

		// --- Layout ---

		private struct Layout
		{
			public Rectangle Left;
			public Rectangle Detail;
			public Rectangle Button;
			public int Columns;
			public int CardW;
			public int CardH;
		}

		private static Layout LayOut(Rectangle r)
		{
			var l = new Layout();
			int top = r.Y + 88;
			int bottom = r.Bottom - 40;
			int gap = 16;
			int detailW = Math.Min(340, (r.Width - Pad * 2 - gap) * 2 / 5);
			l.Left = new Rectangle(r.X + Pad, top, r.Width - Pad * 2 - gap - detailW, Math.Max(80, bottom - top));
			l.Detail = new Rectangle(l.Left.Right + gap, top, detailW, l.Left.Height);

			l.Columns = l.Left.Width >= 520 ? 2 : 1;
			l.CardW = (l.Left.Width - (l.Columns - 1) * 12) / l.Columns;
			l.CardH = 92;
			l.Button = new Rectangle(l.Detail.X + 18, l.Detail.Bottom - 18 - 40, l.Detail.Width - 36, 40);
			return l;
		}

		private static Rectangle CardRect(Layout l, int i, float scroll = 0f)
		{
			int col = i % l.Columns;
			int row = i / l.Columns;
			return new Rectangle(l.Left.X + col * (l.CardW + 12), l.Left.Y + 58 + row * (l.CardH + 10) - (int)scroll, l.CardW, l.CardH);
		}

		// The part of the left side the cards live in (below the tabs). Cards scroll inside it and are cut at its edges.
		private static Rectangle GridRect(Layout l)
		{
			int top = l.Left.Y + 54;
			return new Rectangle(l.Left.X, top, l.Left.Width, Math.Max(0, l.Left.Bottom - top));
		}

		private float MaxScroll(Layout l)
		{
			int rows = (Shown.Count + l.Columns - 1) / l.Columns;
			float content = 4 + rows * (l.CardH + 10);
			return Math.Max(0f, content - GridRect(l).Height);
		}

		public override void ScrollWheel(UIScrollWheelEvent evt)
		{
			base.ScrollWheel(evt);
			Layout l = LayOut(GetDimensions().ToRectangle());
			scrollY = MathHelper.Clamp(scrollY - evt.ScrollWheelValue * 0.5f, 0f, MaxScroll(l));
		}

		// The clip region gets its size every update, even while this page is hidden (an unsized clip region cuts away everything drawn after it).
		protected override void UpdateContent(GameTime gameTime)
		{
			Rectangle r = GetDimensions().ToRectangle();
			if (r.Width > 0 && r.Height > 0)
				gridClip.Place(GridRect(LayOut(r)), r);
		}

		// The tab row sits between the heading and the cards.
		private static Rectangle TabRect(Layout l, int i)
		{
			int gap = 6;
			int w = (l.Left.Width - gap * (Categories.Length - 1)) / Categories.Length;
			return new Rectangle(l.Left.X + i * (w + gap), l.Left.Y + 22, w, 24);
		}

		// --- Clicks ---

		public override void LeftClick(UIMouseEvent evt)
		{
			base.LeftClick(evt);
			AutomataHubSystem.Guard(() =>
			{
				Rectangle r = GetDimensions().ToRectangle();
				Layout l = LayOut(r);
				Point p = evt.MousePosition.ToPoint();

				for (int i = 0; i < Categories.Length; i++)
				{
					if (TabRect(l, i).Contains(p))
					{
						if (category != i)
						{
							category = i;
							shown = null;
							selected = 0;
							scrollY = 0f;
							confirmUntil = 0;
							SoundEngine.PlaySound(SoundID.MenuTick);
						}
						return;
					}
				}

				for (int i = 0; i < Shown.Count; i++)
				{
					if (GridRect(l).Contains(p) && CardRect(l, i, scrollY).Contains(p))
					{
						if (selected != i)
						{
							selected = i;
							confirmUntil = 0;
							SoundEngine.PlaySound(SoundID.MenuTick);
						}
						return;
					}
				}

				if (l.Button.Contains(p) && selected >= 0 && selected < Shown.Count)
					PressButton(Shown[selected]);
			});
		}

		private void PressButton(ShopItem item)
		{
			if (item.Owned)
			{
				if (!item.Equipped)
				{
					if (item.IsScheme)
						MenuLook.SelectScheme(item.Index);
					else if (item.IsFrame)
						MenuLook.SelectFrame(item.Index);
					else if (item.IsTier)
						MenuLook.SelectTierDesign(item.Index);
					else
						Operations.Equip(item.Reward);
					SoundEngine.PlaySound(SoundID.MenuOpen);
				}
				return;
			}

			if (item.Earned || AccountStore.Balance < item.Price)
				return;

			long now = Environment.TickCount64;
			if (now > confirmUntil)
			{
				// First press: wait for a second one.
				confirmUntil = now + 3500;
				SoundEngine.PlaySound(SoundID.MenuTick);
				return;
			}

			confirmUntil = 0;
			if (AccountStore.TrySpend(item.Price, item.UnlockId))
			{
				SoundEngine.PlaySound(SoundID.Item35);
				message = item.DisplayName + " IS YOURS";
				messageUntil = now + 4000;
			}
		}

		// --- Drawing ---

		protected override void DrawContent(SpriteBatch sb)
		{
			Rectangle r = GetDimensions().ToRectangle();
			Layout l = LayOut(r);
			Point mouse = Main.MouseScreen.ToPoint();

			HubDraw.Fill(sb, r, HubTheme.Panel);
			HubDraw.Border(sb, r, HubTheme.Line);
			MenuLook.DrawFrame(sb, r, HubTheme.Accent);

			// Header, laid out like the Archive's.
			var sq = new Rectangle(r.X + Pad, r.Y + 16, 38, 38);
			HubDraw.Fill(sb, sq, HubTheme.Accent);
			float iw = HubDraw.Width("03", 0.9f, 1f);
			HubDraw.Text(sb, "03", new Vector2(sq.X + (sq.Width - iw) / 2f, sq.Y + (sq.Height - HubDraw.LineHeight(0.9f)) / 2f), HubTheme.OnAccent, 0.9f, 1f);
			HubDraw.BigText(sb, "SHOP", new Vector2(r.X + Pad + 52, r.Y + 6), HubTheme.Text, HubDraw.BigScale(34f), 3f);
			HubDraw.Text(sb, "SPEND DATA FRAGMENTS ON COSMETICS", new Vector2(r.X + Pad + 53, r.Y + 42), HubTheme.Dim, 0.66f, 1.5f);
			HubDraw.TextRight(sb, "DATA FRAGMENTS  " + AccountStore.Balance, r.Right - Pad, r.Y + 34, HubTheme.Warn, 0.72f, 2f);
			HubDraw.HLine(sb, r.X + Pad, r.Y + 68, r.Width - Pad * 2, HubTheme.Line);

			// Left: what is for sale.
			HubDraw.Diamond(sb, new Vector2(l.Left.X + 4, l.Left.Y + 8), 6f, HubTheme.Warn);
			HubDraw.Text(sb, "IN STOCK", new Vector2(l.Left.X + 14, l.Left.Y), HubTheme.Warn, 0.7f, 2f);
			HubDraw.TextRight(sb, Shown.Count + " ITEMS", l.Left.Right, l.Left.Y + 1, HubTheme.Dim, 0.6f, 2f);
			DrawTabs(sb, l, mouse);

			// The cards are drawn by the clip region (a child element), which cuts them at the edges of the grid. Keep the scroll in range
			// (a tab with fewer cards, or a taller window, may need less of it) and draw a thin handle beside the grid when it can scroll.
			scrollY = MathHelper.Clamp(scrollY, 0f, MaxScroll(l));
			float maxScroll = MaxScroll(l);
			if (maxScroll > 0f)
			{
				Rectangle grid = GridRect(l);
				var track = new Rectangle(l.Left.Right + 5, grid.Y, 6, grid.Height);
				HubDraw.Fill(sb, track, HubTheme.PanelAlt);
				int handleH = Math.Max(24, (int)(track.Height * grid.Height / (grid.Height + maxScroll)));
				int handleY = track.Y + (int)((track.Height - handleH) * (scrollY / maxScroll));
				HubDraw.Fill(sb, new Rectangle(track.X, handleY, track.Width, handleH), HubTheme.Line);
			}

			DrawDetail(sb, l, selected >= 0 && selected < Shown.Count ? Shown[selected] : null, mouse);

			HubDraw.TextFit(sb, "EARN DATA FRAGMENTS BY PLAYING WITH THE MOD AND BY COMPLETING ORDERS     THEY BELONG TO YOUR ACCOUNT, NOT ONE CHARACTER", new Vector2(r.X + Pad, r.Bottom - 26), HubTheme.Dim, 0.6f, 2f, r.Width - Pad * 2);
		}

		// Draws the cards. Called by the clip region, so everything here is cut at the edge of the grid.
		private void DrawGrid(SpriteBatch sb, Rectangle clipArea)
		{
			Layout l = LayOut(GetDimensions().ToRectangle());
			Point mouse = Main.MouseScreen.ToPoint();
			Rectangle grid = GridRect(l);
			for (int i = 0; i < Shown.Count; i++)
			{
				Rectangle card = CardRect(l, i, scrollY);
				if (card.Bottom < grid.Y || card.Y > grid.Bottom)
					continue;
				DrawCard(sb, card, Shown[i], i == selected, grid.Contains(mouse) && card.Contains(mouse));
			}
		}

		private void DrawTabs(SpriteBatch sb, Layout l, Point mouse)
		{
			for (int i = 0; i < Categories.Length; i++)
			{
				Rectangle tab = TabRect(l, i);
				bool open = i == category;
				bool hover = tab.Contains(mouse);
				HubDraw.Fill(sb, tab, open ? HubTheme.Accent : (hover ? HubTheme.PanelAlt * 0.8f : HubTheme.PanelAlt));
				HubDraw.Border(sb, tab, open ? HubTheme.Accent : (hover ? HubTheme.Text : HubTheme.LineSoft), open ? 2 : 1);

				string name = CategoryNames[i];
				float scale = 0.58f;
				float width = HubDraw.Width(name, scale, 2f);
				if (width > tab.Width - 8)
					scale *= (tab.Width - 8) / width;
				width = HubDraw.Width(name, scale, 2f);
				HubDraw.Text(sb, name, new Vector2(tab.X + (tab.Width - width) / 2f, tab.Y + (tab.Height - HubDraw.LineHeight(scale)) / 2f), open ? HubTheme.OnAccent : HubTheme.Text, scale, 2f);
			}
		}

		private static void DrawSwatch(SpriteBatch sb, Rectangle area, ShopItem item)
		{
			if (item.IsScheme)
			{
				MenuLook.Scheme s = MenuLook.All[item.Index];
				HubDraw.Fill(sb, area, new Color(6, 8, 14));
				HubDraw.Border(sb, area, HubTheme.Line);
				int half = area.Width / 2;
				HubDraw.Fill(sb, new Rectangle(area.X + 6, area.Y + 6, area.Width - 12, 10), s.Backing);
				HubDraw.Fill(sb, new Rectangle(area.X + 8, area.Y + 8, half - 8, 6), s.Ivory);
				HubDraw.Fill(sb, new Rectangle(area.X + half + 4, area.Y + 10, area.Width / 2 - 14, 2), s.Idle);
				HubDraw.Fill(sb, new Rectangle(area.X + 6, area.Y + 22, area.Width - 12, area.Height - 42), s.Panel);
				HubDraw.Fill(sb, new Rectangle(area.X + 10, area.Y + 26, area.Width / 2, 4), s.Accent);
				HubDraw.Fill(sb, new Rectangle(area.X + 6, area.Bottom - 14, area.Width - 12, 8), s.Backing);
				HubDraw.Fill(sb, new Rectangle(area.X + 8, area.Bottom - 12, 12, 4), s.Amber);
			}
			else if (item.IsFrame)
			{
				HubDraw.Fill(sb, area, HubTheme.Panel);
				HubDraw.Border(sb, area, HubTheme.Line);
				var inner = new Rectangle(area.X + 12, area.Y + 12, area.Width - 24, area.Height - 24);
				HubDraw.Fill(sb, inner, HubTheme.PanelAlt);
				if (item.Index == 1)
					HubDraw.Brackets(sb, new Rectangle(inner.X - 5, inner.Y - 5, inner.Width + 10, inner.Height + 10), HubTheme.Accent, 16, 3);
				else
				{
					HubDraw.Brackets(sb, new Rectangle(inner.X - 8, inner.Y - 8, inner.Width + 16, inner.Height + 16), HubTheme.Accent, 12, 2);
					HubDraw.Brackets(sb, new Rectangle(inner.X - 3, inner.Y - 3, inner.Width + 6, inner.Height + 6), HubTheme.Accent, 6, 1);
				}
			}
			else if (item.IsTier)
			{
				// Two sample plugin cards (a Rare and an Epic) drawn by the real card code with this design switched on for a moment.
				HubDraw.Fill(sb, area, HubTheme.Panel);
				HubDraw.Border(sb, area, HubTheme.Line);
				int side = Math.Max(20, Math.Min(area.Height - 28, (area.Width - 36) / 2));
				var rare = new Rectangle(area.Center.X - side - 8, area.Center.Y - side / 2, side, side);
				var epic = new Rectangle(area.Center.X + 8, area.Center.Y - side / 2, side, side);
				Color chassis = PanelChrome.Tone(new Color(10, 16, 28));
				RarityLook.SampleDesign = (TierDesign)item.Index;
				try
				{
					if (item.Index == 1)
					{
						RarityLook.RoundFill(sb, rare, chassis, RarityLook.HoloRadius(rare));
						RarityLook.RoundFill(sb, epic, chassis, RarityLook.HoloRadius(epic));
					}
					else
					{
						HubDraw.Fill(sb, rare, chassis);
						HubDraw.Fill(sb, epic, chassis);
					}
					RarityLook.DrawTierFrame(sb, rare, AugmentRarity.Rare, false);
					RarityLook.DrawTierFrame(sb, epic, AugmentRarity.Epic, false);
				}
				finally
				{
					RarityLook.SampleDesign = null;
				}
			}
			else
			{
				// Pod skins, titles and emblems: the same pictures the Operation page uses, centred on a plain plate.
				HubDraw.Fill(sb, area, HubTheme.Panel);
				HubDraw.Border(sb, area, HubTheme.Line);
				int side = Math.Max(16, Math.Min(area.Width, area.Height) - 16);
				var box = new Rectangle(area.X + (area.Width - side) / 2, area.Y + (area.Height - side) / 2, side, side);
				if (item.Locked)
				{
					// Not earned yet: a dark outline of the pod, or a question mark for a title.
					if (item.Kind == RewardKind.PodSkin)
						AugmentAdvisoryHUD.DrawPodIcon(sb, box, new Color(44, 48, 58));
					else
						{
							float qs = HubDraw.BigScale(Math.Max(24f, box.Height * 0.5f));
							HubDraw.BigText(sb, "?", new Vector2(box.Center.X - HubDraw.BigWidth("?", qs) / 2f, box.Center.Y - box.Height * 0.25f), HubTheme.Dim, qs);
						}
				}
				else
					OperationArt.RewardIcon(sb, item.Reward, box, Color.White);
			}
		}

		private static void DrawCard(SpriteBatch sb, Rectangle card, ShopItem item, bool isSelected, bool hovered)
		{
			HubDraw.Fill(sb, card, hovered && !isSelected ? HubTheme.PanelAlt * 0.8f : HubTheme.PanelAlt);
			HubDraw.Border(sb, card, isSelected ? HubTheme.Accent : (hovered ? HubTheme.Text : HubTheme.LineSoft), isSelected ? 2 : 1);

			var swatch = new Rectangle(card.X + 10, card.Y + 10, card.Height - 20, card.Height - 20);
			DrawSwatch(sb, swatch, item);

			int tx = swatch.Right + 14;
			HubDraw.Text(sb, item.KindLabel, new Vector2(tx, card.Y + 10), HubTheme.Dim, 0.52f, 2f);
			HubDraw.TextFit(sb, item.DisplayName, new Vector2(tx, card.Y + 28), HubTheme.Text, 0.86f, 2f, card.Right - tx - 10);

			string tag;
			Color color;
			if (item.Equipped)
			{
				tag = "EQUIPPED";
				color = HubTheme.Warn;
			}
			else if (item.Owned)
			{
				tag = "OWNED";
				color = HubTheme.Dim;
			}
			else if (item.Earned)
			{
				tag = "LOCKED";
				color = HubTheme.Dim;
			}
			else
			{
				tag = item.Price + " FRAGMENTS";
				color = AccountStore.Balance >= item.Price ? HubTheme.Warn : HubTheme.Dim;
			}

			HubDraw.Text(sb, tag, new Vector2(tx, card.Bottom - 28), color, 0.62f, 2f);
		}

		private void DrawDetail(SpriteBatch sb, Layout l, ShopItem item, Point mouse)
		{
			Rectangle d = l.Detail;
			HubDraw.Fill(sb, d, HubTheme.PanelAlt);
			HubDraw.Border(sb, d, HubTheme.Accent, 2);

			if (item == null)
			{
				HubDraw.Text(sb, "NOTHING SELECTED", new Vector2(d.X + 18, d.Y + 18), HubTheme.Dim, 0.8f, 2f);
				return;
			}

			int x = d.X + 18;
			int y = d.Y + 16;
			HubDraw.Text(sb, item.KindLabel, new Vector2(x, y), HubTheme.Warn, 0.62f, 2f);
			HubDraw.TextFit(sb, item.DisplayName, new Vector2(x, y + 22), HubTheme.Text, 1.2f, 3f, d.Width - 36);

			int previewH = Math.Max(70, Math.Min(130, d.Height - 250));
			var preview = new Rectangle(x, y + 62, d.Width - 36, previewH);
			DrawSwatch(sb, preview, item);

			int ty = preview.Bottom + 12;
			foreach (string line in HubDraw.Wrap(item.ShownBlurb, d.Width - 36, 0.64f))
			{
				HubDraw.Text(sb, line, new Vector2(x, ty), HubTheme.Dim, 0.64f);
				ty += (int)HubDraw.LineHeight(0.64f) + 2;
			}

			ty += 6;
			if (!item.Owned && !item.Earned)
			{
				HubDraw.Text(sb, "PRICE", new Vector2(x, ty), HubTheme.Dim, 0.58f, 2f);
				HubDraw.TextRight(sb, item.Price + " FRAGMENTS", d.Right - 18, ty - 1, HubTheme.Warn, 0.72f, 1.5f);
				ty += 22;
				HubDraw.Text(sb, "YOU HAVE", new Vector2(x, ty), HubTheme.Dim, 0.58f, 2f);
				HubDraw.TextRight(sb, AccountStore.Balance + " FRAGMENTS", d.Right - 18, ty - 1, HubTheme.Text, 0.72f, 1.5f);
			}

			// The one button.
			long now = Environment.TickCount64;
			bool confirming = !item.Owned && now <= confirmUntil;
			string label;
			bool enabled = true;
			if (item.Owned)
			{
				label = item.Equipped ? "EQUIPPED" : "EQUIP";
				enabled = !item.Equipped;
			}
			else if (item.Earned)
			{
				label = "LOCKED";
				enabled = false;
			}
			else if (AccountStore.Balance < item.Price)
			{
				label = "NEED " + (item.Price - AccountStore.Balance) + " MORE";
				enabled = false;
			}
			else
			{
				label = confirming ? "CONFIRM PURCHASE" : "BUY";
			}

			bool hover = enabled && l.Button.Contains(mouse);
			Color fill = !enabled ? HubTheme.PanelAlt : (hover || confirming ? HubTheme.Accent : HubTheme.Panel);
			Color text = !enabled ? HubTheme.Dim : (hover || confirming ? HubTheme.OnAccent : HubTheme.Text);
			HubDraw.Fill(sb, l.Button, fill);
			HubDraw.Border(sb, l.Button, enabled ? HubTheme.Accent : HubTheme.LineSoft, 2);
			float lw = HubDraw.Width(label, 0.8f, 2f);
			HubDraw.Text(sb, label, new Vector2(l.Button.X + (l.Button.Width - lw) / 2f, l.Button.Y + (l.Button.Height - HubDraw.LineHeight(0.8f)) / 2f), text, 0.8f, 2f);

			if (now <= messageUntil)
				HubDraw.Text(sb, message, new Vector2(x, l.Button.Y - 22), HubTheme.Warn, 0.66f, 2f);
		}
	}
}
