using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ReLogic.Content;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent.UI.Elements;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace Augments
{
	// The sections of the Automata title screen. The order here is the order of the top bar.
	public enum HomeSection
	{
		Home,
		Archive,
		Shop,
		Orders,
		Cosmetics,
		Profile,
		PatchNotes,
		Operation // reached from the home screen card, not from the top bar
	}

	// The Automata title screen. It replaces the vanilla button column and is ONE page with a top bar:
	//
	//   top bar     always visible: HOME, ARCHIVE, SHOP, ORDERS, COSMETICS, PROFILE, PATCH NOTES.
	//               The current section is highlighted, and clicking another one swaps the middle in place.
	//   HOME        button list on the left, the new-plugins slideshow in the middle, a PATCH NOTES button under it
	//   ARCHIVE...  layout previews of the other sections (clearly marked PREVIEW, nothing is connected yet)
	//   PATCH NOTES the real changelog, with a section index, a scrollbar and a BACK button
	//   top-right   currency box (placeholder numbers: there is no save data on the title screen)
	//
	// Esc goes back to HOME from any other section. AutomataMusic already draws HUD text in all four corners and a
	// title card at top centre (about y 56 to 144), so everything here sits below y 168.
	public class AutomataHomeUIState : UIState
	{
		internal const int Margin = 64;
		internal const int BarY = 76;      // the top row: navigation bar, level with the currency box
		internal const int BarH = 46;
		internal const int HomeTop = 146;  // where the title card and the home preview start, just under the top row

		// On the vanilla pages (Multiplayer, Settings, ...) the title card stays at the top in the middle, so the strip goes under it:
		// a little higher on the pages whose first item starts high (Settings and the Join via IP screen).
		internal const int VanillaBarY = 176;
		internal const int VanillaBarYHigh = 148;
		internal const int VanillaCurrencyY = 104;
		internal const int ContentTop = 244;
		internal const int BottomSafe = 112;
		internal const int LeftColW = 290;

		// The default scheme matches AutomataMusic's menu (ivory banner, charcoal text, bone idle text). These read the
		// scheme picked on the COSMETICS page (see MenuLook), so changing it repaints the whole page.
		internal static Color Ivory => MenuLook.Current.Ivory;
		internal static Color Charcoal => MenuLook.Current.Charcoal;
		internal static Color Idle => MenuLook.Current.Idle;
		internal static Color Backing => MenuLook.Current.Backing;
		internal static Color Amber => MenuLook.Current.Amber;
		internal static Color Muted => MenuLook.Current.Muted;

		public HomeSection Section { get; private set; }

		// Counts down after a section change so the content can flicker in.
		internal int SwitchTicks { get; private set; }
		internal const int SwitchDuration = 14;

		// Counts down when the page first appears, for a short fade up from black.
		internal int OpenTicks { get; private set; }
		internal const int OpenDuration = 18;

		// Where the section content sits (set by Relayout), so the transition effect can cover exactly that area.
		internal Rectangle ContentArea { get; private set; }

		internal bool IsHome => Section == HomeSection.Home;
		// The sections that are still layout previews. The Archive, Shop, Orders and Cosmetics are real now and have their own views.
		internal bool IsPreview => Section > HomeSection.Archive && Section <= HomeSection.Profile && Section != HomeSection.Cosmetics && Section != HomeSection.Shop && Section != HomeSection.Orders && Section != HomeSection.Operation;
		internal bool IsPatchNotes => Section == HomeSection.PatchNotes;

		private HomeRoot root;
		private HomeTopBar topBar;
		private readonly List<HomeMenuButton> buttons = new List<HomeMenuButton>();
		private HomeSlideshow slideshow;
		private HomePatchButton patchButton;
		private HomeSectionView sectionView;
		private HomeArchiveView archiveView;
		private HomeCosmeticsView cosmeticsView;
		private HomeShopView shopView;
		private HomeOrdersView ordersView;
		private HomeOperationView operationView;
		private HomeOperationCard operationCard;
		private HomePatchNotesView patchView;
		private HomeCurrency currency;
		private Vector2 lastSize;
		private int lastCardRight = -1;

		public override void OnActivate()
		{
			// Every time the page is shown (including coming back from a game or another menu), start on HOME,
			// unless a tab on a vanilla page (Multiplayer, Settings) asked for a particular section.
			Section = AutomataHubSystem.TakePendingSection();
			SwitchTicks = 0;
			OpenTicks = OpenDuration;
		}

		public override void OnDeactivate()
		{
			// Leaving the page (Multiplayer, Settings, a world...): stop any showcase clip.
			ShowcaseVideo.Stop();
		}

		public override void OnInitialize()
		{
			root = new HomeRoot();
			root.Width.Set(0f, 1f);
			root.Height.Set(0f, 1f);
			Append(root);

			AddButton("SINGLE PLAYER", AutomataHubSystem.GoSinglePlayer);
			AddButton("MULTIPLAYER", AutomataHubSystem.GoMultiplayer);
			AddButton("SETTINGS", AutomataHubSystem.GoSettings);
			AddButton("EXIT", AutomataHubSystem.GoExit);
			AddButton("MODS / WORKSHOP", AutomataHubSystem.GoClassic);

			slideshow = new HomeSlideshow { VisibleWhen = () => IsHome };
			root.Append(slideshow);

			patchButton = new HomePatchButton(this) { VisibleWhen = () => IsHome };
			root.Append(patchButton);

			sectionView = new HomeSectionView(this) { VisibleWhen = () => IsPreview };
			root.Append(sectionView);

			archiveView = new HomeArchiveView(this) { VisibleWhen = () => Section == HomeSection.Archive };
			root.Append(archiveView);

			operationView = new HomeOperationView { VisibleWhen = () => Section == HomeSection.Operation };
			root.Append(operationView);

			operationCard = new HomeOperationCard(this) { VisibleWhen = () => IsHome };
			root.Append(operationCard);

			ordersView = new HomeOrdersView { VisibleWhen = () => Section == HomeSection.Orders };
			root.Append(ordersView);

			shopView = new HomeShopView { VisibleWhen = () => Section == HomeSection.Shop };
			root.Append(shopView);

			cosmeticsView = new HomeCosmeticsView(this) { VisibleWhen = () => Section == HomeSection.Cosmetics };
			root.Append(cosmeticsView);

			patchView = new HomePatchNotesView(this) { VisibleWhen = () => IsPatchNotes };
			root.Append(patchView);

			topBar = new HomeTopBar(this);
			root.Append(topBar);

			currency = new HomeCurrency();
			root.Append(currency);

			// Last child = drawn on top of everything: the boot-up fade and the section-switch glitch.
			root.Append(new HomeTransitionOverlay(this));

			// Above even that: the first-launch "Share anonymous stats?" question. It only shows until it has been answered.
			root.Append(new HomeStatsPrompt(this));
		}

		private void AddButton(string label, Action onClick)
		{
			var b = new HomeMenuButton(label, onClick) { VisibleWhen = () => IsHome };
			buttons.Add(b);
			root.Append(b);
		}

		public void SetSection(HomeSection section)
		{
			if (section == Section)
				return;

			Section = section;
			if (section == HomeSection.PatchNotes)
				patchView?.Opened();
			lastSize = Vector2.Zero; // the bar, currency box and logo move between HOME and the other sections, so lay out again
			SwitchTicks = SwitchDuration;
			SoundEngine.PlaySound(SoundID.MenuOpen);
		}

		public override void Update(GameTime gameTime)
		{
			try
			{
				if (SwitchTicks > 0)
					SwitchTicks--;
				if (OpenTicks > 0)
					OpenTicks--;

				// Esc steps back to HOME from any other section. While the Archive's search box has the keyboard,
				// Esc only leaves the box (the box handles that itself), so it does not also jump to HOME.
				if (!IsHome && !HomeArchiveView.IsTyping && Main.keyState.IsKeyDown(Keys.Escape) && Main.oldKeyState.IsKeyUp(Keys.Escape))
					SetSection(HomeSection.Home);
					// The title card's width is only known once it has been drawn, so lay out again when it changes.
					if (BunkerMenuTheme.CardRight != lastCardRight)
					{
						lastCardRight = BunkerMenuTheme.CardRight;
						lastSize = Vector2.Zero;
					}

					Relayout();
					base.Update(gameTime);
			}
			catch (Exception e)
			{
				AutomataHubSystem.ReportFailure(e);
			}
		}

		// Positions everything from the page's own size (in UI units, so it follows UI scale and resolution).
		private void Relayout()
		{
			CalculatedStyle dims = root.GetDimensions();
			var size = new Vector2(dims.Width, dims.Height);
			if (size.X < 200f || size.Y < 200f || size == lastSize)
				return;
			lastSize = size;

			float w = size.X;
			float h = size.Y;

			// Top bar on the top row, centred. It keeps clear of the currency box on the right and of the same amount on the left.
			// On HOME the bar is on the top row; on every other section the title card is back at the top in the middle, so the bar sits under it.
			float barW = IsHome ? topBar.Fit(Math.Max(560f, w - 2f * (Margin + 260f + 20f))) : topBar.Fit(w - 2f * Margin);
			topBar.Left.Set((float)Math.Round((w - barW) / 2f), 0f);
			topBar.Top.Set(IsHome ? BarY : VanillaBarY, 0f);
			topBar.Width.Set(barW, 0f);
			topBar.Height.Set(BarH, 0f);

			// Left button column (HOME only), under the title card. The Mods / Workshop button sits a little apart from the four main ones.
			float menuTop = HomeTop + 112f;
			for (int i = 0; i < buttons.Count; i++)
			{
				float extra = i == buttons.Count - 1 ? 22f : 0f;
				buttons[i].Left.Set(Margin, 0f);
				buttons[i].Top.Set(menuTop + i * 52f + extra, 0f);
				buttons[i].Width.Set(LeftColW, 0f);
				buttons[i].Height.Set(42f, 0f);
			}

			// HOME: the preview starts at the top, in the space to the right of the title card and the buttons. Under it come the
			// Operation banner and then the patch notes button.
			float logoRight = BunkerMenuTheme.CardRight > 0 ? BunkerMenuTheme.CardRight : Margin + LeftColW;
			float regionLeft = Math.Max(Margin + LeftColW + 40f, logoRight + 24f);
			float regionW = Math.Max(440f, (w - Margin) - regionLeft);
			float slideW = MathHelper.Clamp(regionW, 440f, 860f);
			const float opH = 96f;
			const float patchH = 44f;
			const float gap = 12f;
			float availH = h - HomeTop - BottomSafe;
			const float extras = HomeSlideshow.ChromeHeight;
			float mediaH = MathHelper.Clamp(slideW * 9f / 16f, 110f, Math.Max(110f, availH - extras - (gap + opH) - (gap + patchH)));

			float frameH = extras + mediaH;
			float top = HomeTop;
			float left = (float)Math.Round(regionLeft + (regionW - slideW) / 2f);

			slideshow.Left.Set(left, 0f);
			slideshow.Top.Set(top, 0f);
			slideshow.Width.Set(slideW, 0f);
			slideshow.Height.Set(frameH, 0f);
			slideshow.MediaHeight = (int)mediaH;

			operationCard.Left.Set(left, 0f);
			operationCard.Top.Set(top + frameH + gap, 0f);
			operationCard.Width.Set(slideW, 0f);
			operationCard.Height.Set(opH, 0f);

			patchButton.Left.Set(left, 0f);
			patchButton.Top.Set(top + frameH + gap + opH + gap, 0f);
			patchButton.Width.Set(slideW, 0f);
			patchButton.Height.Set(patchH, 0f);
			// Full-width content area for the other sections.
			float contentW = Math.Min(w - 2f * Margin, 1100f);
			float contentH = Math.Max(240f, h - ContentTop - BottomSafe);
			float contentLeft = (float)Math.Round((w - contentW) / 2f);
			ContentArea = new Rectangle((int)contentLeft, ContentTop, (int)contentW, (int)contentH);
			foreach (UIElement view in new UIElement[] { sectionView, archiveView, shopView, ordersView, operationView, cosmeticsView, patchView })
			{
				view.Left.Set(contentLeft, 0f);
				view.Top.Set(ContentTop, 0f);
				view.Width.Set(contentW, 0f);
				view.Height.Set(contentH, 0f);
			}

			// Top-right currency box, under AutomataMusic's corner text.
			currency.Left.Set(w - Margin - 260f, 0f);
			currency.Top.Set(IsHome ? BarY - 8f : VanillaCurrencyY, 0f);
			currency.Width.Set(260f, 0f);
			currency.Height.Set(64f, 0f);

			root.Recalculate();
		}
	}

	// Drawn on top of the whole page: a short fade up from black when the page appears, and a few glitch bars across the
	// content area right after the section changes. It never takes mouse clicks.
	internal class HomeTransitionOverlay : HubElementBase
	{
		private readonly AutomataHomeUIState state;

		public HomeTransitionOverlay(AutomataHomeUIState state)
		{
			this.state = state;
			Width.Set(0f, 1f);
			Height.Set(0f, 1f);
			IgnoresMouseInteraction = true;
		}

		protected override void DrawContent(SpriteBatch sb)
		{
			Rectangle screen = GetDimensions().ToRectangle();

			if (state.OpenTicks > 0)
				HubDraw.Fill(sb, screen, Color.Black * (0.65f * state.OpenTicks / AutomataHomeUIState.OpenDuration));

			int t = state.SwitchTicks;
			Rectangle area = state.ContentArea;
			if (t > 0 && area.Width > 0)
			{
				float progress = 1f - t / (float)AutomataHomeUIState.SwitchDuration;
				int seed = (int)state.Section * 7 + t;
				for (int i = 0; i < 4; i++)
				{
					int bandY = area.Y + (int)(((seed * 37 + i * 91) % 100) / 100f * (area.Height - 8));
					int bandH = 2 + (seed + i) % 4;
					int shift = ((seed * 13 + i * 29) % 41) - 20;
					HubDraw.Fill(sb, new Rectangle(area.X + Math.Max(0, shift), bandY, area.Width - Math.Abs(shift), bandH), AutomataHomeUIState.Ivory * (0.32f * (1f - progress)));
				}
			}
		}
	}

	// Full-screen layer. Only dims the background when AutomataMusic's Earth scene is not being drawn.
	internal class HomeRoot : HubElementBase
	{
		protected override void DrawContent(SpriteBatch sb)
		{
			if (AutomataHubSystem.AutomataMusicThemeActive)
				return;

			HubDraw.Fill(sb, GetDimensions().ToRectangle(), Color.Black * 0.45f);
		}
	}

	// The one navigation: a bar of icon + label tabs. The current section is an ivory block.
	internal class HomeTopBar : HubElementBase
	{
		private static readonly string[] Labels = { "HOME", "ARCHIVE", "SHOP", "ORDERS", "COSMETICS", "PROFILE", "PATCH NOTES" };

		// Which section is current, and what clicking a tab does. On the home page these read and change the page's
		// section; on the vanilla pages (Multiplayer, Settings) no section is current and a click goes back home.
		private readonly Func<HomeSection> current;
		private readonly Action<HomeSection> select;
		private float[] widths = new float[Labels.Length];
		private float scale = 0.72f;
		private float tracking = 2f;

		public HomeTopBar(AutomataHomeUIState state) : this(() => state.Section, state.SetSection)
		{
		}

		public HomeTopBar(Func<HomeSection> current, Action<HomeSection> select)
		{
			this.current = current;
			this.select = select;
		}

		// Works out tab widths from the measured text and shrinks the type if the bar would be too wide.
		// Returns the total bar width.
		public float Fit(float maxWidth)
		{
			scale = 0.72f;
			tracking = 2f;
			float total = 0f;
			for (int attempt = 0; attempt < 4; attempt++)
			{
				total = 0f;
				for (int i = 0; i < Labels.Length; i++)
				{
					widths[i] = 18f + 14f + 8f + HubDraw.Width(Labels[i], scale, tracking) + 18f;
					total += widths[i];
				}

				if (total <= maxWidth)
					break;

				scale -= 0.06f;
				tracking -= 0.4f;
			}

			return total;
		}

		private Rectangle TabRect(Rectangle bar, int i)
		{
			float x = bar.X;
			for (int k = 0; k < i; k++)
				x += widths[k];
			return new Rectangle((int)Math.Round(x), bar.Y, (int)Math.Round(widths[i]), bar.Height);
		}

		// Which tab (as an index) is under the point, or -1.
		public int HitTab(Rectangle bar, Point p)
		{
			for (int i = 0; i < Labels.Length; i++)
			{
				if (TabRect(bar, i).Contains(p))
					return i;
			}

			return -1;
		}

		public override void LeftClick(UIMouseEvent evt)
		{
			base.LeftClick(evt);
			AutomataHubSystem.Guard(() =>
			{
				int tab = HitTab(GetDimensions().ToRectangle(), evt.MousePosition.ToPoint());
				if (tab >= 0)
					select((HomeSection)tab);
			});
		}

		private int lastHover = -1;

		protected override void DrawContent(SpriteBatch sb)
		{
			DrawBar(sb, GetDimensions().ToRectangle(), Main.MouseScreen.ToPoint());
		}

		// Draws the bar into the given rectangle. Used by the home page and, with its own rectangle and mouse position,
		// by the strip drawn over the vanilla Multiplayer and Settings pages.
		public void DrawBar(SpriteBatch sb, Rectangle bar, Point mouse)
		{
			HubDraw.Fill(sb, bar, AutomataHomeUIState.Backing * 0.92f);
			HubDraw.Border(sb, bar, AutomataHomeUIState.Ivory * 0.4f);
			HubDraw.Corners(sb, new Rectangle(bar.X - 4, bar.Y - 4, bar.Width + 8, bar.Height + 8), AutomataHomeUIState.Ivory, 12, 2);

			int hover = HitTab(bar, mouse);

			if (hover != lastHover)
			{
				if (hover >= 0)
					SoundEngine.PlaySound(SoundID.MenuTick);
				lastHover = hover;
			}

			for (int i = 0; i < Labels.Length; i++)
			{
				Rectangle r = TabRect(bar, i);
				bool active = (int)current() == i;
				Color fg = active ? AutomataHomeUIState.Charcoal : (hover == i ? AutomataHomeUIState.Ivory : AutomataHomeUIState.Idle);

				if (active)
					HubDraw.Fill(sb, new Rectangle(r.X + 1, r.Y + 1, r.Width - 1, r.Height - 2), AutomataHomeUIState.Ivory);
				else if (hover == i)
					HubDraw.Fill(sb, new Rectangle(r.X + 1, r.Y + 1, r.Width - 1, r.Height - 2), AutomataHomeUIState.Ivory * 0.14f);

				if (i > 0 && !active && (int)current() != i - 1)
					HubDraw.VLine(sb, r.X, r.Y + 10, r.Height - 20, AutomataHomeUIState.Ivory * 0.18f);

				DrawIcon(sb, (HomeSection)i, r.X + 18, r.Y + (r.Height - 14) / 2, fg);

				// The font's line box has extra space below the capital letters, so text centred by line height
				// looks about 3 units too high next to an icon centred in the bar. Nudge it down to the same centre line.
				float ty = r.Y + (r.Height - HubDraw.LineHeight(scale)) / 2f + 4.2f * scale;
				HubDraw.Text(sb, Labels[i], new Vector2(r.X + 18 + 14 + 8, ty), fg, scale, tracking);

				// A glowing dot on the PATCH NOTES tab while the newest notes have not been opened.
				if (i == (int)HomeSection.PatchNotes && !active && PatchNotesData.IsUnread)
					HubDraw.GlowDot(sb, new Vector2(r.Right - 10, r.Y + r.Height / 2f), 2.5f);
			}
		}

		// Small pixel icons, 14 x 14.
		private static void DrawIcon(SpriteBatch sb, HomeSection s, int x, int y, Color c)
		{
			switch (s)
			{
				case HomeSection.Home:
					for (int i = 0; i < 7; i++)
						HubDraw.Fill(sb, new Rectangle(x + 6 - i, y + i, i * 2 + 2, 1), c);
					HubDraw.Fill(sb, new Rectangle(x + 2, y + 7, 10, 6), c);
					break;
				case HomeSection.Archive:
					HubDraw.Fill(sb, new Rectangle(x, y, 6, 6), c);
					HubDraw.Fill(sb, new Rectangle(x + 8, y, 6, 6), c);
					HubDraw.Fill(sb, new Rectangle(x, y + 8, 6, 6), c);
					HubDraw.Fill(sb, new Rectangle(x + 8, y + 8, 6, 6), c);
					break;
				case HomeSection.Shop:
					HubDraw.Diamond(sb, new Vector2(x + 7, y + 7), 9f, c); // 13 tall, close to the other icons' 14
					break;
				case HomeSection.Orders:
					for (int i = 0; i < 3; i++)
					{
						HubDraw.Fill(sb, new Rectangle(x, y + 1 + i * 4, 2, 2), c);
						HubDraw.Fill(sb, new Rectangle(x + 4, y + 1 + i * 4, 10, 2), c);
					}
					break;
				case HomeSection.Cosmetics:
					HubDraw.Fill(sb, new Rectangle(x + 6, y, 2, 14), c);
					HubDraw.Fill(sb, new Rectangle(x, y + 6, 14, 2), c);
					HubDraw.Fill(sb, new Rectangle(x + 2, y + 2, 2, 2), c);
					HubDraw.Fill(sb, new Rectangle(x + 10, y + 2, 2, 2), c);
					HubDraw.Fill(sb, new Rectangle(x + 2, y + 10, 2, 2), c);
					HubDraw.Fill(sb, new Rectangle(x + 10, y + 10, 2, 2), c);
					break;
				case HomeSection.Profile:
					HubDraw.Fill(sb, new Rectangle(x + 4, y, 6, 6), c);
					HubDraw.Fill(sb, new Rectangle(x + 1, y + 8, 12, 6), c);
					break;
				default: // patch notes: a page with lines
					HubDraw.Border(sb, new Rectangle(x + 2, y, 10, 14), c);
					HubDraw.Fill(sb, new Rectangle(x + 4, y + 4, 6, 1), c);
					HubDraw.Fill(sb, new Rectangle(x + 4, y + 7, 6, 1), c);
					HubDraw.Fill(sb, new Rectangle(x + 4, y + 10, 4, 1), c);
					break;
			}
		}
	}

	// One entry in the left list. Idle: bone text. Hover: ivory banner with charcoal text, like AutomataMusic's buttons.
	internal class HomeMenuButton : HubElementBase
	{
		private readonly string label;
		private readonly Action onClick;
		private bool hovered;
		private int hoverTicks; // 0..8: how far the highlight banner has slid in

		public HomeMenuButton(string label, Action onClick)
		{
			this.label = label;
			this.onClick = onClick;
		}

		protected override void UpdateContent(GameTime gameTime)
		{
			if (!IsShown)
			{
				hoverTicks = 0;
				return;
			}

			hoverTicks = hovered ? Math.Min(8, hoverTicks + 1) : Math.Max(0, hoverTicks - 1);
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
			AutomataHubSystem.Guard(onClick);
		}

		protected override void DrawContent(SpriteBatch sb)
		{
			Rectangle r = GetDimensions().ToRectangle();

			// Shrink the text if it would not fit inside the button (this is what caused the overlap before).
			float scale = 0.84f;
			float textW = HubDraw.Width(label, scale, 2.5f);
			float room = r.Width - 34f - 14f;
			if (textW > room)
				scale *= room / textW;
			float y = r.Y + (r.Height - HubDraw.LineHeight(scale)) / 2f;

			// The ivory banner slides in from the left over a few ticks, and the label nudges right with it.
			float ease = 1f - (1f - hoverTicks / 8f) * (1f - hoverTicks / 8f);
			if (hoverTicks > 0)
			{
				var banner = new Rectangle(r.X, r.Y, Math.Max(2, (int)(r.Width * ease)), r.Height);
				HubDraw.Fill(sb, banner, AutomataHomeUIState.Ivory);
				if (hoverTicks >= 8)
					HubDraw.Corners(sb, r, AutomataHomeUIState.Charcoal, 6, 2);

				bool covered = banner.Right > r.X + 34 + textW * 0.5f;
				if (covered)
				{
					HubDraw.Fill(sb, new Rectangle(r.X + 14, r.Y + r.Height / 2 - 3, 6, 6), AutomataHomeUIState.Charcoal);
					HubDraw.Text(sb, label, new Vector2(r.X + 34 + 4 * ease, y), AutomataHomeUIState.Charcoal, scale, 2.5f);
					return;
				}
			}

			HubDraw.Fill(sb, new Rectangle(r.X + 15, r.Y + r.Height / 2 - 2, 4, 4), AutomataHomeUIState.Ivory * 0.45f);
			HubDraw.Text(sb, label, new Vector2(r.X + 35, y + 1), Color.Black * 0.85f, scale, 2.5f);
			HubDraw.Text(sb, label, new Vector2(r.X + 34, y), AutomataHomeUIState.Idle, scale, 2.5f);
		}
	}

	// Wide ivory banner under the slideshow. It says exactly what it does and goes to the PATCH NOTES section.
	internal class HomePatchButton : HubElementBase
	{
		private readonly AutomataHomeUIState state;
		private bool hovered;

		public HomePatchButton(AutomataHomeUIState state)
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
			AutomataHubSystem.Guard(() => state.SetSection(HomeSection.PatchNotes));
		}

		private static string VersionText()
		{
			try
			{
				return "v" + ModContent.GetInstance<Augments>().Version;
			}
			catch
			{
				return "";
			}
		}

		protected override void DrawContent(SpriteBatch sb)
		{
			Rectangle r = GetDimensions().ToRectangle();
			Color bg = hovered ? AutomataHomeUIState.Charcoal : AutomataHomeUIState.Ivory;
			Color fg = hovered ? AutomataHomeUIState.Ivory : AutomataHomeUIState.Charcoal;

			HubDraw.Fill(sb, r, bg);
			HubDraw.Border(sb, r, AutomataHomeUIState.Ivory);
			HubDraw.Corners(sb, new Rectangle(r.X - 4, r.Y - 4, r.Width + 8, r.Height + 8), AutomataHomeUIState.Ivory, 10, 2);

			float scale = 0.8f;
			string left = "READ THE PATCH NOTES";
			float y = r.Y + (r.Height - HubDraw.LineHeight(scale)) / 2f;
			HubDraw.CursorRight(sb, r.X + 16, r.Y + r.Height / 2, 12, fg);
			HubDraw.Text(sb, left, new Vector2(r.X + 40, y), fg, scale, 2.5f);

			// The version note only shows if there is room next to the label.
			string right = VersionText() + "  //  WHAT'S NEW";
			float leftEnd = r.X + 40 + HubDraw.Width(left, scale, 2.5f) + 24f;
			float rightW = HubDraw.Width(right, 0.62f, 2f);
			if (r.Right - 16 - rightW > leftEnd)
			{
				HubDraw.TextRight(sb, right, r.Right - 16, r.Y + (r.Height - HubDraw.LineHeight(0.62f)) / 2f, fg * 0.8f, 0.62f, 2f);

				// A glowing dot in front of the version note while the newest notes have not been opened.
				if (PatchNotesData.IsUnread && r.Right - 16 - rightW - 16 > leftEnd)
					HubDraw.GlowDot(sb, new Vector2(r.Right - 16 - rightW - 12, r.Y + r.Height / 2f), 3f);
			}
		}
	}

	// Top-right box: Data Fragments (the account currency, earned by playing and by Orders) and the selected character's rank.
	internal class HomeCurrency : HubElementBase
	{
		protected override void DrawContent(SpriteBatch sb)
		{
			Paint(sb, GetDimensions().ToRectangle());
		}

		// Also used by the strip drawn over the vanilla Multiplayer and Settings pages.
		public static void Paint(SpriteBatch sb, Rectangle r)
		{
			HubDraw.Fill(sb, r, AutomataHomeUIState.Backing * 0.92f);
			HubDraw.Border(sb, r, AutomataHomeUIState.Ivory * 0.4f);
			HubDraw.Corners(sb, r, AutomataHomeUIState.Ivory * 0.9f, 10, 2);

			// Data Fragments belong to the account and are never taken from items or Machine Cores. The rank is the selected character's.
			ProfileStore.CharacterRecord profile = ProfileStore.Selected;
			string cores = AccountStore.Balance.ToString("000");
			string rank = profile != null ? profile.Rank.ToString("00") : "--";

			OperationArt.Fragment(sb, new Rectangle(r.X + 6, r.Y + 6, 28, 28), AutomataHomeUIState.Amber);
			HubDraw.Text(sb, "DATA FRAGMENTS", new Vector2(r.X + 46, r.Y + 11), AutomataHomeUIState.Muted, 0.62f, 2f);
			HubDraw.TextRight(sb, cores, r.Right - 16, r.Y + 7, AutomataHomeUIState.Amber, 1.0f, 2f);
			HubDraw.HLine(sb, r.X + 14, r.Y + 36, r.Width - 28, AutomataHomeUIState.Ivory * 0.2f);
			HubDraw.Text(sb, "UNIT RANK", new Vector2(r.X + 46, r.Y + 42), AutomataHomeUIState.Muted, 0.58f, 2f);
			HubDraw.TextRight(sb, rank, r.Right - 16, r.Y + 41, AutomataHomeUIState.Idle, 0.7f, 2f);
		}
	}

	// ARCHIVE, SHOP, ORDERS, COSMETICS and PROFILE: the hub's page layouts on a cream panel, marked as a preview.
	internal class HomeSectionView : HubElementBase
	{
		private readonly AutomataHomeUIState state;

		public HomeSectionView(AutomataHomeUIState state)
		{
			this.state = state;
		}

		// Which of the hub's layouts each section uses.
		private static int HubTab(HomeSection s)
		{
			switch (s)
			{
				case HomeSection.Archive: return 1;
				case HomeSection.Profile: return 2;
				case HomeSection.Orders: return 3;
				case HomeSection.Shop: return 4;
				default: return 5; // cosmetics
			}
		}

		protected override void DrawContent(SpriteBatch sb)
		{
			Rectangle r = GetDimensions().ToRectangle();
			HubDraw.Fill(sb, r, HubTheme.Panel);
			HubDraw.Border(sb, r, HubTheme.Line);
			MenuLook.DrawFrame(sb, r, HubTheme.Accent);

			// Visible preview marking, so unfinished sections cannot be mistaken for working ones.
			// PROFILE shows real numbers once a character has been saved, and then drops the marking.
			ProfileStore.CharacterRecord profile = ProfileStore.Selected;
			bool realData = state.Section == HomeSection.Profile && profile != null;
			if (!realData)
			{
				var tag = new Rectangle(r.Right - 24 - 92, r.Y + 14, 92, 24);
				HubDraw.Border(sb, tag, HubTheme.Warn);
				float tw = HubDraw.Width("PREVIEW", 0.66f, 2f);
				HubDraw.Text(sb, "PREVIEW", new Vector2(tag.X + (tag.Width - tw) / 2f, tag.Y + (tag.Height - HubDraw.LineHeight(0.66f)) / 2f), HubTheme.Warn, 0.66f, 2f);
				HubDraw.Text(sb, "LAYOUT PREVIEW  //  NOT CONNECTED TO GAME DATA YET", new Vector2(r.X + 24, r.Bottom - 26), HubTheme.Dim, 0.62f, 2f);
			}
			else
			{
				HubDraw.Text(sb, "FROM YOUR LAST SAVED SESSION AS " + profile.Name.ToUpperInvariant(), new Vector2(r.X + 24, r.Bottom - 26), HubTheme.Dim, 0.62f, 2f);
			}

			var inner = new Rectangle(r.X + 24, r.Y + 20, r.Width - 48, r.Height - 20 - 36);
			int t = state.SwitchTicks;
			HubPages.Fade = t > 0 ? 0.25f + 0.75f * (1f - t / (float)AutomataHomeUIState.SwitchDuration) : 1f;
			HubPages.DrawPage(sb, HubTab(state.Section), inner);
			HubPages.Fade = 1f;
				// With more than one saved character, the arrows pick whose record the Profile page shows.
				if (realData)
				{
					CharacterSwitcher.Draw(sb, CharacterSwitcher.RectFor(r), Main.MouseScreen.ToPoint());
					ProfilePicker.Draw(sb, HubPages.ProfileFavouriteSlots(inner), profile, r, Main.MouseScreen.ToPoint());
						CharacterSwitcher.DrawList(sb, CharacterSwitcher.RectFor(r), Main.MouseScreen.ToPoint());
				}
				else
				{
					ProfilePicker.Close(); CharacterSwitcher.Close();
				}
			}

			// The area the page is drawn in (the same rectangle DrawContent hands to HubPages), so clicks land on what was drawn.
			private Rectangle PageArea()
			{
				Rectangle r = GetDimensions().ToRectangle();
				return new Rectangle(r.X + 24, r.Y + 20, r.Width - 48, r.Height - 20 - 36);
			}

			public override void LeftClick(UIMouseEvent evt)
			{
				base.LeftClick(evt);
				if (state.Section != HomeSection.Profile)
					return;

				AutomataHubSystem.Guard(() =>
				{
					Rectangle r = GetDimensions().ToRectangle();
					Point p = evt.MousePosition.ToPoint();
					if (CharacterSwitcher.Click(CharacterSwitcher.RectFor(r), p))
					{
						ProfilePicker.Close();
						return;
					}

					ProfilePicker.Click(HubPages.ProfileFavouriteSlots(PageArea()), ProfileStore.Selected, r, p);
				});
			}

			public override void RightClick(UIMouseEvent evt)
			{
				base.RightClick(evt);
				if (state.Section != HomeSection.Profile)
					return;

				AutomataHubSystem.Guard(() => ProfilePicker.RightClick(HubPages.ProfileFavouriteSlots(PageArea()), ProfileStore.Selected, evt.MousePosition.ToPoint()));
			}

			public override void ScrollWheel(UIScrollWheelEvent evt)
			{
				base.ScrollWheel(evt);
				if (state.Section == HomeSection.Profile)
					{ CharacterSwitcher.Wheel(evt.ScrollWheelValue); ProfilePicker.Wheel(evt.ScrollWheelValue); }
			}
		}

	// PATCH NOTES: the real changelog. Left: section index with jump links. Right: scrolling text.
	internal class HomePatchNotesView : HubElementBase
	{
		private const int IndexW = 300;
		private const int BodyTop = 88;
		private const int BottomPad = 40;

		private class PLine
		{
			public string Text;
			public int X;
			public float Y;
			public float H;
			public float Scale;
			public float Tracking;
			public int Style; // 0 text, 1 dim, 2 heading, 3 version, 4 soft text
			public int Marker; // 0 none, 1 diamond, 2 section square, 3 dash, 4 rule, 7 direction icon
			public int Kind;   // 0 plain text, 10 plugin change card header, 11 "label: old => new" line
			public int Dir;    // -1 nerf, +1 buff, 0 adjusted
			public string Name, Rarity, Tag, Label, Old, New;
		}

		// Colours of the plugin change cards. A nerf is red, a buff is green, an adjustment is amber.
		private static readonly Color NerfColor = new Color(196, 70, 62);
		private static readonly Color BuffColor = new Color(84, 140, 70);
		private static readonly Color AdjustColor = new Color(176, 132, 40);

		private static Color DirColor(int dir) => dir < 0 ? NerfColor : dir > 0 ? BuffColor : AdjustColor;

		// Fixed hues, not the tier colours of the colour scheme: in the Nier scheme Epic is ivory, which would disappear on the page.
		private static Color RarityColorOf(string rarity)
		{
			if (!Enum.TryParse(rarity, true, out AugmentRarity r))
				return HubTheme.Dim;

			switch (r)
			{
				case AugmentRarity.Rare: return new Color(74, 120, 176);
				case AugmentRarity.Epic: return new Color(128, 84, 176);
				case AugmentRarity.Legendary: return new Color(196, 140, 30);
				default: return new Color(128, 122, 106);
			}
		}

		// The game font has no triangle or arrow symbols, so these are drawn from rectangles: up for a buff, down for a nerf, a diamond
		// for an adjustment.
		private static void DrawDirIcon(SpriteBatch sb, int x, float cy, int dir)
		{
			Color c = DirColor(dir);
			if (dir == 0)
				HubDraw.SmoothDiamond(sb, new Vector2(x + 6.5f, cy), 5.5f, c);
			else
				HubDraw.Triangle(sb, new Vector2(x + 6.5f, cy), 13f, 9f, dir > 0, c);
		}

		private const int ArrowW = 14;

		// A right-pointing arrow between the old and the new value.
		private static void DrawArrow(SpriteBatch sb, int x, float cy, Color c)
		{
			HubDraw.Fill(sb, new Rectangle(x, (int)cy - 1, 8, 2), c);
			HubDraw.TriangleRight(sb, new Vector2(x + 10.25f, cy), 6.5f, 9f, c);
		}

		private readonly AutomataHomeUIState state;
		private readonly HubScrollbar bar;
		private readonly List<PLine> lines = new List<PLine>();
		// One row of the index on the left: a version heading ("PATCH NOTES 1.4.2") or one of the sections under it.
		private class Anchor
		{
			public string Text;
			public float Y;
			public bool Header;
			public bool Latest; // the newest version heading, the one that glows while it is unread
				public int Version;  // 0 for the newest version, 1 for the one before it, and so on
			}

			// The row numbers that are shown in the index. Every version heading is shown, and the sections under a version only while it is the
			// newest or the one being read, so three or four versions still fit.
			private List<int> IndexRows(float scroll)
			{
				int active = 0;
				for (int i = 0; i < anchors.Count; i++)
				{
					if (anchors[i].Y <= scroll + 40f)
						active = i;
				}

				int activeVersion = anchors.Count > 0 ? anchors[active].Version : 0;
				var rows = new List<int>();
				for (int i = 0; i < anchors.Count; i++)
				{
					if (anchors[i].Header || anchors[i].Version == 0 || anchors[i].Version == activeVersion)
						rows.Add(i);
				}

				return rows;
			}

		private readonly List<Anchor> anchors = new List<Anchor>();

		// Whether the newest notes had not been read when this page was opened. Kept for as long as the page stays open, so the marker
		// is still there while you read, and gone the next time you come back.
		private bool openedUnread;

		// Called when the PATCH NOTES section is opened: remembers whether it was new, and marks it as read.
		public void Opened()
		{
			openedUnread = PatchNotesData.IsUnread;
			PatchNotesData.MarkRead();
		}

		private static string HeaderText(string version)
		{
			const string word = "Version ";
			return "PATCH NOTES " + (version.StartsWith(word, StringComparison.OrdinalIgnoreCase) ? version.Substring(word.Length) : version).ToUpperInvariant();
		}
		private float laidOutWidth = -1f;
		private float laidOutUi = -1f;
		private float totalHeight;

		public HomePatchNotesView(AutomataHomeUIState state)
		{
			this.state = state;
			bar = new HubScrollbar();
			bar.VisibleWhen = () => IsShown;
			bar.Left.Set(-(24f + 8f), 1f);
			bar.Top.Set(BodyTop, 0f);
			bar.Height.Set(-(BodyTop + BottomPad), 1f);
			Append(bar);

			// The notes scroll inside a clipping region, so a line half scrolled out is cut cleanly at the edge.
			bodyClip = new HubClipRegion(DrawBody) { VisibleWhen = () => IsShown };
			Append(bodyClip);
		}

		private readonly HubClipRegion bodyClip;

		private Rectangle BodyRect(Rectangle r)
		{
			int x = r.X + 24 + IndexW + 22;
			return new Rectangle(x, r.Y + BodyTop, r.Right - 24 - 16 - x, Math.Max(40, r.Height - BodyTop - BottomPad));
		}

		// Sized from the measured text (label, gap, key hint, padding) so the two never overlap.
		private static Rectangle BackRect(Rectangle r)
		{
			float w = 14f + HubDraw.Width("< BACK TO HOME", 0.72f, 2f) + 22f + HubDraw.Width("[ESC]", 0.56f, 1f) + 12f;
			int width = (int)Math.Ceiling(w);
			return new Rectangle(r.Right - 24 - width, r.Y + 16, width, 30);
		}

		private static Rectangle IndexRow(Rectangle r, int i) => new Rectangle(r.X + 24, r.Y + BodyTop + 28 + i * 28, IndexW, 26);

		public override void ScrollWheel(UIScrollWheelEvent evt)
		{
			base.ScrollWheel(evt);
			bar.ViewPosition -= evt.ScrollWheelValue * 0.5f;
		}

		public override void LeftClick(UIMouseEvent evt)
		{
			base.LeftClick(evt);
			AutomataHubSystem.Guard(() =>
			{
				Rectangle r = GetDimensions().ToRectangle();
				Point p = evt.MousePosition.ToPoint();

				if (BackRect(r).Contains(p))
				{
					state.SetSection(HomeSection.Home);
					return;
				}

				List<int> shown = IndexRows(bar.ViewPosition);
				for (int k = 0; k < shown.Count; k++)
				{
					if (IndexRow(r, k).Contains(p))
					{
						SoundEngine.PlaySound(SoundID.MenuTick);
						bar.ViewPosition = Math.Max(0f, anchors[shown[k]].Y - 6f);
						return;
					}
				}
			});
		}

		protected override void UpdateContent(GameTime gameTime)
		{
			if (!IsShown)
				return;

			Rectangle r = GetDimensions().ToRectangle();
			if (r.Width < 100)
				return;

			Rectangle body = BodyRect(r);
			bodyClip.Place(body, r);
			if (Math.Abs(body.Width - laidOutWidth) > 0.5f || Math.Abs(Main.UIScale - laidOutUi) > 0.001f)
				Build(body.Width);

			bar.SetView(body.Height, Math.Max(totalHeight, body.Height));
		}

		// Wraps all the notes for the current width. Done once per width change, never per frame.
		private void Build(float width)
		{
			laidOutWidth = width;
			laidOutUi = Main.UIScale;
			lines.Clear();
			anchors.Clear();
			float y = 0f;

			void Add(string text, int x, float scale, float tracking, int style, int marker, float height)
			{
				lines.Add(new PLine { Text = text, X = x, Y = y, Scale = scale, Tracking = tracking, Style = style, Marker = marker, H = height });
			}

			// A plugin change card is a header line, a quote and stat lines. Its coloured bar is as tall as all of them together.
			PLine openCard = null;
			void CloseCard()
			{
				if (openCard == null)
					return;
				openCard.H = Math.Max(24f, y - openCard.Y - 2f);
				y += 14f;
				openCard = null;
			}

			bool firstVersion = true;
			int versionIndex = -1;
			foreach (PatchBlock b in PatchNotesData.Blocks)
			{
				if (b.Kind != PatchKind.Quote && b.Kind != PatchKind.Stat)
					CloseCard();

				switch (b.Kind)
				{
					case PatchKind.Card:
						openCard = new PLine { Kind = 10, Y = y, H = 24f, Name = b.Title, Rarity = b.Rarity, Tag = b.Tag, Dir = b.Dir };
						lines.Add(openCard);
						y += 28f;
						break;

					case PatchKind.Quote:
						WrapInto(b.Body, 18, width, HubDraw.Crisp(0.7f), 1, ref y);
						y += 4f;
						break;

					case PatchKind.Stat:
						AddStat(b, width, ref y);
						break;

					case PatchKind.Version:
						// A heavy rule and a big gap mark where the previous version ends, so its notes don't run into the next one.
						if (versionIndex >= 0)
						{
							y += 40f;
							lines.Add(new PLine { Y = y, Marker = 5, H = 6f });
							y += 36f;
						}
						versionIndex++;
						anchors.Add(new Anchor { Text = HeaderText(b.Body), Y = y, Header = true, Latest = firstVersion, Version = versionIndex });
						firstVersion = false;
						Add(b.Body.ToUpperInvariant(), 0, HubDraw.Crisp(1.1f), 3f, 3, 0, 28f);
						y += 44f;
						break;

					case PatchKind.Section:
						if (y > 0f)
							y += 20f;
						anchors.Add(new Anchor { Text = b.Body, Y = y, Version = Math.Max(0, versionIndex) });
						Add(b.Body.ToUpperInvariant(), 20, HubDraw.Crisp(0.9f), 1.5f, 2, 2, 22f);
						lines.Add(new PLine { Y = y + 26f, Marker = 4, H = 1f });
						y += 40f;
						break;

					case PatchKind.Item:
						if (b.Title.Length > 0)
						{
							Add(b.Title, 18, HubDraw.Crisp(0.82f), 0.5f, 0, 1, 20f);
							y += 22f;
							WrapInto(b.Body, 18, width, HubDraw.Crisp(0.72f), 4, ref y);
						}
						else
						{
							WrapInto(b.Body, 18, width, HubDraw.Crisp(0.74f), 4, ref y, marker: 1);
						}
						y += 8f;
						break;

					case PatchKind.Sub:
					{
						int indent = 18 + 18 * b.Level;
						string text = b.Title.Length > 0 ? (b.Body.Length > 0 ? b.Title + ": " + b.Body : b.Title) : b.Body;
						WrapInto(text, indent, width, HubDraw.Crisp(0.68f), 1, ref y, marker: 3);
						y += 4f;
						break;
					}

					default:
						WrapInto(b.Body, 0, width, HubDraw.Crisp(0.72f), 1, ref y);
						y += 6f;
						break;
				}
			}

			CloseCard();
			totalHeight = y + 24f;
		}

		// One line of a card. "Label: old => new" is drawn with the old value dimmed and crossed out, then an arrow, then the new
		// value. A line without an arrow (or one too wide for the page) is plain wrapped text with the same direction icon.
		private void AddStat(PatchBlock b, float width, ref float y)
		{
			float scale = HubDraw.Crisp(0.72f);
			const int textX = 36;
			string text = b.Body;

			int arrowAt = text.IndexOf(" => ", StringComparison.Ordinal);
			if (arrowAt > 0)
			{
				string left = text.Substring(0, arrowAt).Trim();
				string right = text.Substring(arrowAt + 4).Trim();
				string label = "";
				string old = left;
				int colon = left.LastIndexOf(": ", StringComparison.Ordinal);
				if (colon >= 0)
				{
					label = left.Substring(0, colon + 2);
					old = left.Substring(colon + 2);
				}

				float total = HubDraw.Width(label, scale) + HubDraw.Width(old, scale) + 8f + ArrowW + 8f + HubDraw.Width(right, scale) + 1f;
				if (total <= width - textX - 8f)
				{
					float lh = HubDraw.LineHeight(scale) + 3f;
					lines.Add(new PLine { Kind = 11, Marker = 7, Dir = b.Dir, X = textX, Y = y, H = lh, Scale = scale, Label = label, Old = old, New = right });
					y += lh + 3f;
					return;
				}

				text = left + " -> " + right;
			}

			WrapInto(text, textX, width, scale, 0, ref y, marker: 7, dir: b.Dir);
			y += 3f;
		}

		private void WrapInto(string text, int indent, float width, float scale, int style, ref float y, int marker = 0, int dir = 0)
		{
			if (string.IsNullOrEmpty(text))
				return;

			float lh = HubDraw.LineHeight(scale) + 3f;
			bool first = true;
			foreach (string line in HubDraw.Wrap(text, width - indent - 8f, scale))
			{
				lines.Add(new PLine { Text = line, X = indent, Y = y, Scale = scale, Tracking = 0f, Style = style, Marker = first ? marker : 0, Dir = dir, H = lh });
				y += lh;
				first = false;
			}
		}

		private Color StyleColor(int style)
		{
			switch (style)
			{
				case 1: return HubTheme.Dim;
				case 2: return HubTheme.Text;
				case 3: return HubTheme.Text;
				case 4: return HubTheme.Text * 0.82f;
				default: return HubTheme.Text;
			}
		}

		protected override void DrawContent(SpriteBatch sb)
		{
			// This page draws its text on whole screen pixels, so it stays sharp at any UI scale and while scrolling.
			bool snap = HubDraw.SnapToPixels;
			HubDraw.SnapToPixels = true;
			try
			{
				DrawPage(sb);
			}
			finally
			{
				HubDraw.SnapToPixels = snap;
			}
		}

		private void DrawPage(SpriteBatch sb)
		{
			Rectangle r = GetDimensions().ToRectangle();
			HubDraw.Fill(sb, r, HubTheme.Panel);
			HubDraw.Border(sb, r, HubTheme.Line);
			MenuLook.DrawFrame(sb, r, HubTheme.Accent);

			// Header: title, version, rule, and a clear BACK button.
			var sq = new Rectangle(r.X + 24, r.Y + 16, 38, 38);
			HubDraw.Fill(sb, sq, HubTheme.Accent);
			HubDraw.Border(sb, new Rectangle(sq.X + 11, sq.Y + 8, 16, 22), HubTheme.OnAccent);
			HubDraw.Fill(sb, new Rectangle(sq.X + 15, sq.Y + 14, 8, 1), HubTheme.OnAccent);
			HubDraw.Fill(sb, new Rectangle(sq.X + 15, sq.Y + 18, 8, 1), HubTheme.OnAccent);
			HubDraw.Fill(sb, new Rectangle(sq.X + 15, sq.Y + 22, 5, 1), HubTheme.OnAccent);
			HubDraw.BigText(sb, "PATCH NOTES", new Vector2(r.X + 76, r.Y + 6), HubTheme.Text, HubDraw.BigScale(34f), 3f);
			string version = PatchNotesData.VersionLabel.Length > 0 ? PatchNotesData.VersionLabel : "WHAT'S NEW";
			HubDraw.Text(sb, version + "   //   SCROLL OR PICK A SECTION", new Vector2(r.X + 77, r.Y + 42), HubTheme.Dim, 0.66f, 1.5f);
			HubDraw.HLine(sb, r.X + 24, r.Y + 68, r.Width - 48, HubTheme.Line);

			Rectangle back = BackRect(r);
			bool backHot = back.Contains(Main.MouseScreen.ToPoint());
			HubDraw.Fill(sb, back, backHot ? HubTheme.Accent : Color.Transparent);
			HubDraw.Border(sb, back, HubTheme.Accent);
			Color backText = backHot ? HubTheme.OnAccent : HubTheme.Text;
			HubDraw.Text(sb, "< BACK TO HOME", new Vector2(back.X + 14, back.Y + (back.Height - HubDraw.LineHeight(0.72f)) / 2f), backText, 0.72f, 2f);
			HubDraw.TextRight(sb, "[ESC]", back.Right - 10, back.Y + (back.Height - HubDraw.LineHeight(0.56f)) / 2f, backText * 0.7f, 0.56f, 1f);

			// Nothing to show.
			if (PatchNotesData.Error != null || PatchNotesData.Blocks.Count == 0)
			{
				string msg = PatchNotesData.Error ?? "No patch notes.";
				float mw = HubDraw.Width(msg, 0.8f, 1f);
				HubDraw.Text(sb, msg, new Vector2(r.X + (r.Width - mw) / 2f, r.Y + r.Height / 2f - 10), HubTheme.Dim, 0.8f, 1f);
				return;
			}

			float scroll = bar.ViewPosition;

			// Section index.
			HubDraw.Diamond(sb, new Vector2(r.X + 28, r.Y + BodyTop + 8), 5f, HubTheme.Warn);
			HubDraw.Text(sb, "SECTIONS", new Vector2(r.X + 40, r.Y + BodyTop), HubTheme.Warn, 0.7f, 2f);
			int active = 0;
			for (int i = 0; i < anchors.Count; i++)
			{
				if (anchors[i].Y <= scroll + 40f)
					active = i;
			}
			float idx = HubDraw.Crisp(0.68f);
			List<int> shownRows = IndexRows(scroll);
			for (int k = 0; k < shownRows.Count; k++)
			{
				int i = shownRows[k];
				Rectangle row = IndexRow(r, k);
				if (row.Bottom > r.Bottom - BottomPad + 10)
					break;

				Anchor a = anchors[i];
				bool hot = row.Contains(Main.MouseScreen.ToPoint());
				if (i == active)
					HubDraw.Fill(sb, row, HubTheme.Accent);
				else if (hot)
					HubDraw.Fill(sb, row, HubTheme.PanelAlt);

				Color c = i == active ? HubTheme.OnAccent : (hot ? HubTheme.Text : HubTheme.Dim);
				float ty = row.Y + (row.Height - HubDraw.LineHeight(idx)) / 2f;

				if (a.Header)
				{
					// "PATCH NOTES 1.4.2": the version heading. A glowing dot sits at the start of the newest one until it has been read, and the
					// word NEW at its end; older versions get a plain square.
					Color hc = i == active ? HubTheme.OnAccent : HubTheme.Text;
					if (a.Latest && openedUnread)
						HubDraw.GlowDot(sb, new Vector2(row.X + 13, row.Y + row.Height / 2f), 3f);
					else
						HubDraw.Fill(sb, new Rectangle(row.X + 10, row.Y + row.Height / 2 - 2, 5, 5), i == active ? HubTheme.OnAccent : HubTheme.Dim);

					float tx = row.X + 26;
					float maxW = IndexW - 26 - (a.Latest && openedUnread ? 44 : 10);
					string head = Fit(a.Text, maxW, idx);
					HubDraw.Text(sb, head, new Vector2(tx, ty), hc, idx, 0.5f);
					HubDraw.Text(sb, head, new Vector2(tx + HubDraw.OnePx, ty), hc, idx, 0.5f);
					if (a.Latest && openedUnread)
						HubDraw.TextRight(sb, "NEW", row.Right - 10, ty, HubDraw.Unread, 0.62f, 1.5f);
				}
				else
				{
					// A section under its version: indented, with a short tick in front.
					HubDraw.Fill(sb, new Rectangle(row.X + 26, row.Y + row.Height / 2, 6, 1), i == active ? HubTheme.OnAccent : HubTheme.LineSoft);
					HubDraw.Text(sb, Fit(a.Text, IndexW - 48, idx), new Vector2(row.X + 38, ty), c, idx, 0.5f);
				}
			}

			HubDraw.VLine(sb, r.X + 24 + IndexW + 10, r.Y + BodyTop, r.Height - BodyTop - BottomPad, HubTheme.LineSoft);

			// (the notes themselves are drawn by DrawBody inside the clipping region, which is a child of this view)
			HubDraw.Text(sb, "MOUSE WHEEL SCROLLS", new Vector2(r.X + 24, r.Bottom - 26), HubTheme.Dim, 0.6f, 2f);
		}

		// The notes, drawn inside a clipping region: lines that are partly scrolled out are cut at the edge, not dropped.
		private void DrawBody(SpriteBatch sb, Rectangle body)
		{
			bool snap = HubDraw.SnapToPixels;
			HubDraw.SnapToPixels = true;
			try
			{
				DrawBodyLines(sb, body);
			}
			finally
			{
				HubDraw.SnapToPixels = snap;
			}
		}

		private void DrawBodyLines(SpriteBatch sb, Rectangle body)
		{
			if (PatchNotesData.Error != null || PatchNotesData.Blocks.Count == 0)
				return;

			float scroll = bar.ViewPosition;
			foreach (PLine l in lines)
			{
				float sy = body.Y + l.Y - scroll;
				if (sy + l.H < body.Y || sy > body.Bottom)
					continue;

				if (l.Marker == 4)
				{
					HubDraw.HLine(sb, body.X, (int)sy, body.Width, HubTheme.LineSoft);
					continue;
				}

				if (l.Marker == 5)
				{
					// End-of-version rule: a thick bar over a thin one.
					for (int t = 0; t < 3; t++)
						HubDraw.HLine(sb, body.X, (int)sy + t, body.Width, HubTheme.Text);
					HubDraw.HLine(sb, body.X, (int)sy + 5, body.Width, HubTheme.Line);
					continue;
				}

				if (l.Kind == 10)
				{
					DrawCardHeader(sb, body, l, sy);
					continue;
				}

				Color c = StyleColor(l.Style);
				if (l.Kind == 11)
				{
					DrawStatLine(sb, body, l, sy);
					continue;
				}

				if (l.Marker == 7)
					DrawDirIcon(sb, body.X + l.X - 18, sy + HubDraw.CapMid(l.Scale), l.Dir);

				if (l.Marker == 2)
				{
					HubDraw.Fill(sb, new Rectangle(body.X, (int)sy + 4, 10, 10), HubTheme.Accent);
				}
				else if (l.Marker == 1)
				{
					HubDraw.Diamond(sb, new Vector2(body.X + l.X - 10, sy + HubDraw.LineHeight(l.Scale) / 2f + 1), 3f, HubTheme.Warn);
				}
				else if (l.Marker == 3)
				{
					HubDraw.Fill(sb, new Rectangle(body.X + l.X - 10, (int)(sy + HubDraw.LineHeight(l.Scale) / 2f), 5, 1), HubTheme.Dim);
				}

				HubDraw.Text(sb, l.Text, new Vector2(body.X + l.X, sy), c, l.Scale, l.Tracking);
			}
		}

		// The first line of a plugin change card: its name and a NERF / BUFF / ADJUSTED tag, with a bar in the rarity colour running down the
		// left side of the whole card (the colour is the tier, so there is no tier text).
		private static void DrawCardHeader(SpriteBatch sb, Rectangle body, PLine l, float sy)
		{
			Color rc = RarityColorOf(l.Rarity);
			HubDraw.Fill(sb, new Rectangle(body.X + 2, (int)sy, 4, (int)l.H), rc);

			float nameScale = HubDraw.Crisp(0.86f);
			HubDraw.Text(sb, l.Name, new Vector2(body.X + 18, sy), HubTheme.Text, nameScale, 0.5f);
			float x = body.X + 18 + HubDraw.Width(l.Name, nameScale, 0.5f) + 12f;

			if (!string.IsNullOrEmpty(l.Tag))
			{
				// The tag box is centred on the middle of the name's letters, and the tag's own letters are centred in the box.
				const float tagScale = 0.56f;
				int boxH = (int)Math.Round(HubDraw.LineHeight(tagScale) * 0.5f) + 10;
				int boxY = (int)Math.Round(sy + HubDraw.CapMid(nameScale) - boxH / 2f);
				var tag = new Rectangle((int)x, boxY, (int)Math.Round(HubDraw.Width(l.Tag, tagScale, 1f)) + 16, boxH);
				HubDraw.Fill(sb, tag, DirColor(l.Dir));
				HubDraw.Text(sb, l.Tag, new Vector2(tag.X + 8, tag.Y + boxH / 2f - HubDraw.CapMid(tagScale)), Color.White * 0.95f, tagScale, 1f);
			}
		}

		// "Label: old => new": the old value dimmed with a line through it, an arrow, then the new value in the normal colour (drawn twice,
		// one pixel apart, so it stands out as the number that matters).
		private static void DrawStatLine(SpriteBatch sb, Rectangle body, PLine l, float sy)
		{
			// The strike-through, the arrow and the icon all sit on the middle of the letters.
			float cy = sy + HubDraw.CapMid(l.Scale);
			DrawDirIcon(sb, body.X + l.X - 18, cy, l.Dir);

			float x = body.X + l.X;
			if (l.Label.Length > 0)
			{
				HubDraw.Text(sb, l.Label, new Vector2(x, sy), HubTheme.Text, l.Scale);
				x += HubDraw.Width(l.Label, l.Scale);
			}

			float ow = HubDraw.Width(l.Old, l.Scale);
			HubDraw.Text(sb, l.Old, new Vector2(x, sy), HubTheme.Dim, l.Scale);
			HubDraw.Fill(sb, new Rectangle((int)x, (int)cy, (int)ow, 1), HubTheme.Dim);
			x += ow + 8f;

			DrawArrow(sb, (int)x, cy, HubTheme.Dim);
			x += ArrowW + 8f;

			HubDraw.Text(sb, l.New, new Vector2(x, sy), HubTheme.Text, l.Scale);
			HubDraw.Text(sb, l.New, new Vector2(x + HubDraw.OnePx, sy), HubTheme.Text, l.Scale);
		}

		// Shortens a label with "..." until it fits the given width.
		private static string Fit(string text, float maxWidth, float scale)
		{
			if (HubDraw.Width(text, scale, 0.5f) <= maxWidth)
				return text;

			while (text.Length > 4 && HubDraw.Width(text + "...", scale, 0.5f) > maxWidth)
				text = text.Substring(0, text.Length - 1);
			return text.TrimEnd() + "...";
		}
	}

	// One plugin shown in the slideshow. Built once from the plugin list, with every piece of text made safe.
	internal class HomeSlide
	{
		public string Id;
		public string Name;
		public string Rarity;
		public string ClassName;
		public string Description;
		public Augment Source;
		public bool IsPlaceholder;

		// Fetched every time it is drawn, not cached: vanilla item textures load on demand and may still be a
		// blank placeholder at the title screen, so this picks the real one up as soon as it is loaded.
		public Texture2D Icon
		{
			get
			{
				try
				{
					Texture2D t = Source?.Icon;
					return t != null && t.Width >= 8 && t.Height >= 8 ? t : null;
				}
				catch
				{
					return null;
				}
			}
		}

		// Optional screenshot from Assets/Showcase/<plugin id>.png. Looked up once, on first use.
		private bool mediaLooked;
		private Texture2D media;

		public Texture2D Media
		{
			get
			{
				if (mediaLooked)
					return media;

				mediaLooked = true;
				try
				{
					string path = "Augments/Assets/Showcase/" + Id;
					if (!IsPlaceholder && ModContent.HasAsset(path))
						media = ModContent.Request<Texture2D>(path, AssetRequestMode.ImmediateLoad).Value;
				}
				catch
				{
					media = null;
				}

				return media;
			}
		}

		public Color RarityColor
		{
			get
			{
				switch (Rarity)
				{
					case "RARE": return new Color(70, 140, 255);
					case "EPIC": return new Color(176, 96, 255);
					case "LEGENDARY": return new Color(255, 150, 60);
					default: return new Color(170, 180, 195);
				}
			}
		}

		// Plugin descriptions carry chat colour tags like [c/94A3B8:text]. Keep the text, drop the tags.
		private static readonly Regex ColourTag = new Regex(@"\[c/[0-9A-Fa-f]{3,8}:([^\]]*)\]", RegexOptions.Compiled);
		private static readonly Regex OtherTag = new Regex(@"\[[A-Za-z]+/?[^\]:]*:[^\]]*\]", RegexOptions.Compiled);

		public static string Clean(string text)
		{
			if (string.IsNullOrEmpty(text))
				return "";

			text = ColourTag.Replace(text, "$1");
			text = OtherTag.Replace(text, "");
			return text.Replace("\r", " ").Replace("\n", " ").Trim();
		}

		public static HomeSlide From(Augment a)
		{
			var slide = new HomeSlide
			{
				Id = a.Id,
				Name = Clean(a.DisplayName).ToUpperInvariant(),
				Rarity = a.Rarity.ToString().ToUpperInvariant(),
				ClassName = a.Class.ToString().ToUpperInvariant()
			};

			try
			{
				slide.Description = HubPreviewPlayer.Read(() => Clean(a.Description));
			}
			catch
			{
				slide.Description = "";
			}

			slide.Source = a;
			return slide;
		}
	}

	// The HOME showcase: the newest plugins one after another, with a progress bar, dots and arrows.
	// Each slide shows a screenshot from Assets/Showcase/<id>.png when one exists, otherwise a labelled placeholder
	// where the video or image will go.
	internal class HomeSlideshow : HubElementBase
	{
		public const float ChromeHeight = 146f; // everything in the frame that is not the media area
		// The plugins the slideshow features, in this order. To change what is shown, change these ids
		// (a plugin's id is the `Id` in its file in AugmentTypes). The media file for each one is
		// Assets/Showcase/<id>.png. If none of the ids are found, the newest plugins are shown instead.
		private static readonly string[] FeaturedIds =
		{
			"long_distance_relationship",
			"run_as_administrator",
			"get_over_here"
		};

		private const int FallbackSlideCount = 6;
		private const int SlideTicks = 480;     // 8 seconds
		private const int FadeTicks = 16;

		public int MediaHeight = 200;

		private List<HomeSlide> slides;
		private Texture2D clipFrame; // the current video frame, fetched in UpdateContent
		private int index;
		private int tick;
		private int fade;
		private bool hovered;

		public override void MouseOver(UIMouseEvent evt)
		{
			base.MouseOver(evt);
			hovered = true;
		}

		public override void MouseOut(UIMouseEvent evt)
		{
			base.MouseOut(evt);
			hovered = false;
		}

		private void EnsureSlides()
		{
			if (slides != null)
				return;

			slides = new List<HomeSlide>();

			// The featured plugins first, in order. Each one is built on its own, so one bad plugin
			// cannot take the others down.
			foreach (string id in FeaturedIds)
			{
				try
				{
					Augment a = AugmentDatabase.GetById(id);
					if (a != null)
						slides.Add(HomeSlide.From(a));
				}
				catch
				{
				}
			}

			// Nothing featured was found: show the newest plugins instead.
			if (slides.Count == 0)
			{
				try
				{
					List<Augment> all = AugmentDatabase.All;
					int taken = 0;
					for (int i = all.Count - 1; i >= 0 && taken < FallbackSlideCount; i--)
					{
						try
						{
							slides.Add(HomeSlide.From(all[i]));
							taken++;
						}
						catch
						{
						}
					}
				}
				catch
				{
				}
			}

			if (slides.Count == 0)
			{
				slides.Add(new HomeSlide
				{
					Id = "none",
					Name = "NO TRANSMISSIONS",
					Rarity = "COMMON",
					ClassName = "SYSTEM",
					Description = "No new plugins to show yet.",
					IsPlaceholder = true
				});
			}
		}

		// How long the current slide stays: at least 8 seconds, and long enough to show a whole video clip.
		private int DwellTicks()
		{
			HomeSlide cur = slides[Math.Min(index, slides.Count - 1)];
			if (cur.IsPlaceholder || !ShowcaseVideo.HasClip(cur.Id))
				return SlideTicks;

			float seconds = ShowcaseVideo.GetDurationSeconds(cur.Id);
			if (seconds <= 0f)
				seconds = 11f; // length not known until the clip has started once: long enough for the longest clip

			return Math.Max(SlideTicks, (int)(seconds * 60f) + 30);
		}

		private void Go(int delta)
		{
			EnsureSlides();
			index = (index + delta + slides.Count) % slides.Count;
			tick = 0;
			fade = FadeTicks;
		}

		public override void LeftClick(UIMouseEvent evt)
		{
			base.LeftClick(evt);
			AutomataHubSystem.Guard(() =>
			{
				EnsureSlides();
				Rectangle r = GetDimensions().ToRectangle();
				Rectangle media = MediaRect(r);
				Point p = evt.MousePosition.ToPoint();

				// Left and right edges of the media area step through the slides.
				if (new Rectangle(media.X, media.Y, 44, media.Height).Contains(p))
				{
					SoundEngine.PlaySound(SoundID.MenuTick);
					Go(-1);
				}
				else if (new Rectangle(media.Right - 44, media.Y, 44, media.Height).Contains(p))
				{
					SoundEngine.PlaySound(SoundID.MenuTick);
					Go(1);
				}
				else
				{
					// The dots along the bottom jump to a slide.
					for (int i = 0; i < slides.Count; i++)
					{
						if (DotRect(r, i).Contains(p))
						{
							SoundEngine.PlaySound(SoundID.MenuTick);
							index = i;
							tick = 0;
							fade = FadeTicks;
						}
					}
				}
			});
		}

		private Rectangle MediaRect(Rectangle frame) => new Rectangle(frame.X + 10, frame.Y + 10, frame.Width - 20, MediaHeight);

		// Picks the largest size (down to a floor) at which the label fits in the given text width. If the full label
		// does not fit even at the smallest size, tries the short version. Returns null when nothing fits (the
		// label is then simply not drawn, rather than covering the picture).
		private static string FitLabel(string text, string shortText, float maxTextWidth, out float scale)
		{
			scale = 0.62f;
			string[] candidates = shortText != null ? new[] { text, shortText } : new[] { text };
			foreach (string candidate in candidates)
			{
				for (float trial = 0.62f; trial >= 0.44f; trial -= 0.02f)
				{
					if (HubDraw.Width(candidate, trial, 2f) <= maxTextWidth)
					{
						scale = trial;
						return candidate;
					}
				}
			}

			return null;
		}

		private Rectangle DotRect(Rectangle frame, int i)
		{
			int count = slides?.Count ?? 1;
			int total = count * 16 - 6;
			int x = frame.X + (frame.Width - total) / 2 + i * 16;
			return new Rectangle(x - 3, frame.Bottom - 22, 16, 18);
		}

		protected override void UpdateContent(GameTime gameTime)
		{
			// Not on the HOME section: make sure no clip keeps running in the background.
			if (!IsShown)
			{
				ShowcaseVideo.Stop();
				clipFrame = null;
				return;
			}

			EnsureSlides();

			// The video frame is fetched here, in the update step, and only drawn later. Decoding a frame changes
			// graphics-device state, which must not happen in the middle of vanilla's own menu drawing.
			HomeSlide current = slides[Math.Min(index, slides.Count - 1)];
			clipFrame = current.IsPlaceholder ? null : ShowcaseVideo.GetFrame(current.Id);

			if (fade > 0)
				fade--;
			if (hovered)
				return;

			tick++;
			if (tick >= DwellTicks() && slides.Count > 1)
				Go(1);
		}

		protected override void DrawContent(SpriteBatch sb)
		{
			EnsureSlides();
			HomeSlide s = slides[Math.Min(index, slides.Count - 1)];
			Rectangle r = GetDimensions().ToRectangle();
			float alpha = fade > 0 ? 0.35f + 0.65f * (1f - fade / (float)FadeTicks) : 1f;

			HubDraw.Fill(sb, r, AutomataHomeUIState.Backing * 0.92f);
			HubDraw.Border(sb, r, AutomataHomeUIState.Ivory * 0.4f);
			HubDraw.Corners(sb, new Rectangle(r.X - 4, r.Y - 4, r.Width + 8, r.Height + 8), AutomataHomeUIState.Ivory, 14, 2);

			// Media area.
			Rectangle media = MediaRect(r);
			HubDraw.Fill(sb, media, new Color(22, 24, 28));
			// Order: the video clip (Assets/Showcase/<id>.ogv), then the still image (.png), then the emblem.
			Texture2D clip = clipFrame;
			Texture2D tex = s.Media;
			Rectangle picture = media; // where the picture really is (narrower than the media area when the video has side bars)
			if (clip != null)
			{
				// Keep the video's own shape: scale to fit inside the media area, centred.
				float fitScale = Math.Min(media.Width / (float)clip.Width, media.Height / (float)clip.Height);
				int dw = (int)(clip.Width * fitScale);
				int dh = (int)(clip.Height * fitScale);
				var dest = new Rectangle(media.X + (media.Width - dw) / 2, media.Y + (media.Height - dh) / 2, dw, dh);
				sb.Draw(clip, dest, Color.White * alpha);
				picture = dest;
			}
			else if (tex != null)
			{
				sb.Draw(tex, media, Color.White * alpha);
			}
			else
			{
				HubDraw.Stripes(sb, media, AutomataHomeUIState.Ivory * 0.06f, 8);

				// A faint scan line drifting down the empty media area.
				int scan = (int)((Main.GlobalTimeWrappedHourly * 36f) % Math.Max(1, media.Height));
				HubDraw.Fill(sb, new Rectangle(media.X, media.Y + scan, media.Width, 1), AutomataHomeUIState.Ivory * 0.12f);

				// Emblem in the plugin's rarity colour, so the empty area still looks intentional.
				// Three nested diamonds make an outlined shape with a solid core.
				Texture2D icon = s.Icon;
				Vector2 emblem = new Vector2(media.Center.X, media.Center.Y - 14);
				float emblemSize = Math.Min(media.Height * 0.32f, 64f);
				HubDraw.Diamond(sb, emblem, emblemSize, s.RarityColor * (0.55f * alpha));
				HubDraw.Diamond(sb, emblem, emblemSize - 3f, new Color(22, 24, 28));
				HubDraw.Diamond(sb, emblem, emblemSize * 0.45f, s.RarityColor * (0.8f * alpha));
				if (icon != null)
				{
					float fit = Math.Min(64f / icon.Width, 64f / icon.Height);
					fit = Math.Max(1f, (float)Math.Floor(fit * 2f) / 2f);
					Vector2 iconPos = new Vector2(media.Center.X - icon.Width * fit / 2f, media.Center.Y - icon.Height * fit / 2f - 14);
					sb.Draw(icon, iconPos, null, Color.White * alpha, 0f, Vector2.Zero, fit, SpriteEffects.None, 0f);
				}
				string slot = s.IsPlaceholder ? "" : "SHOWCASE VIDEO SLOT";
				float sw = HubDraw.Width(slot, 0.62f, 3f);
				HubDraw.Text(sb, slot, new Vector2(media.Center.X - sw / 2f, media.Bottom - 40), AutomataHomeUIState.Muted, 0.62f, 3f);
				if (!s.IsPlaceholder)
				{
					string hint = "Assets/Showcase/" + s.Id + ".ogv  (or .png)";
					float hw = HubDraw.Width(hint, 0.54f);
					HubDraw.Text(sb, hint, new Vector2(media.Center.X - hw / 2f, media.Bottom - 22), AutomataHomeUIState.Muted * 0.7f, 0.54f);
				}
			}
			HubDraw.Border(sb, media, AutomataHomeUIState.Ivory * 0.25f);

			// "NEW PLUGIN" tag (top-left of the media) and the counter (top-right), exactly where they were.
			// When the picture leaves black bars at the sides, each label is sized to fit inside its bar so it
			// never covers the picture. With no bars (the picture fills the area) they sit in the corners as usual.
			int leftBar = picture.X - media.X;
			int rightBar = media.Right - picture.Right;
			bool barred = leftBar > 0 || rightBar > 0;
			const int margin = 8;
			const int padding = 8; // inside each label, each side
			const int gapToPicture = 6;

			string tagText = s.IsPlaceholder ? "NO DATA" : "NEW PLUGIN";
			float tagMaxText = barred ? leftBar - margin - gapToPicture - padding * 2 : float.MaxValue;
			string tagShown = FitLabel(tagText, s.IsPlaceholder ? null : "NEW", tagMaxText, out float tagScale);
			if (tagShown != null)
			{
				int tagW = (int)HubDraw.Width(tagShown, tagScale, 2f) + padding * 2;
				var tag = new Rectangle(media.X + (barred ? margin : 10), media.Y + 10, tagW, 24);
				HubDraw.Fill(sb, tag, AutomataHomeUIState.Ivory);
				HubDraw.Text(sb, tagShown, new Vector2(tag.X + padding, tag.Y + (tag.Height - HubDraw.LineHeight(tagScale)) / 2f), AutomataHomeUIState.Charcoal, tagScale, 2f);
			}

			string counter = (index + 1).ToString("00") + " / " + slides.Count.ToString("00");
			float counterMaxText = barred ? rightBar - margin - gapToPicture - padding * 2 : float.MaxValue;
			string counterShown = FitLabel(counter, null, counterMaxText, out float counterScale);
			if (counterShown != null)
			{
				int counterW = (int)HubDraw.Width(counterShown, counterScale, 2f) + padding * 2;
				var counterBox = new Rectangle(media.Right - (barred ? margin : 10) - counterW, media.Y + 10, counterW, 24);
				HubDraw.Fill(sb, counterBox, AutomataHomeUIState.Backing * 0.85f);
				HubDraw.Text(sb, counterShown, new Vector2(counterBox.X + padding, counterBox.Y + (counterBox.Height - HubDraw.LineHeight(counterScale)) / 2f), AutomataHomeUIState.Idle, counterScale, 2f);
			}

			// Arrows on the media's left and right edges, brighter while the mouse is over the slideshow.
			Color arrow = AutomataHomeUIState.Ivory * (hovered ? 0.95f : 0.45f);
			int ay = media.Center.Y;
			// The tip of each arrow points outward: left arrow tip at the left, right arrow tip at the right.
			for (int i = 0; i < 9; i++)
			{
				HubDraw.Fill(sb, new Rectangle(media.X + 18 + i, ay - i, 1, i * 2 + 1), arrow);
				HubDraw.Fill(sb, new Rectangle(media.Right - 18 - i - 1, ay - i, 1, i * 2 + 1), arrow);
			}

			// Caption.
			int cy = media.Bottom + 10;
			float progress = tick / (float)DwellTicks();
			HubDraw.Fill(sb, new Rectangle(media.X, cy, media.Width, 2), AutomataHomeUIState.Ivory * 0.15f);
			HubDraw.Fill(sb, new Rectangle(media.X, cy, (int)(media.Width * MathHelper.Clamp(progress, 0f, 1f)), 2), AutomataHomeUIState.Ivory);

			// The plugin name types itself in while the slide fades in.
			string nameShown = s.Name;
			if (fade > 0)
			{
				int typed = (int)(s.Name.Length * (1f - fade / (float)FadeTicks) * 1.4f);
				nameShown = s.Name.Substring(0, Math.Max(0, Math.Min(typed, s.Name.Length)));
			}
			HubDraw.Text(sb, nameShown, new Vector2(media.X, cy + 10), AutomataHomeUIState.Ivory * alpha, 1.0f, 3f);
			Color rc = s.RarityColor * alpha;
			string meta = s.Rarity + "   /   " + s.ClassName;
			HubDraw.TextRight(sb, meta, media.Right, cy + 14, rc, 0.62f, 2f);

			int ty = cy + 40;
			List<string> lines = HubDraw.Wrap(s.Description, media.Width, 0.72f);
			int maxLines = 3;
			for (int i = 0; i < lines.Count && i < maxLines; i++)
			{
				string line = lines[i];
				if (i == maxLines - 1 && lines.Count > maxLines)
					line = line.TrimEnd() + " ...";
				HubDraw.Text(sb, line, new Vector2(media.X, ty), AutomataHomeUIState.Idle * alpha, 0.72f);
				ty += (int)HubDraw.LineHeight(0.72f) + 2;
			}

			// Dots.
			for (int i = 0; i < slides.Count; i++)
			{
				Rectangle d = DotRect(r, i);
				var dot = new Rectangle(d.X + 3, d.Y + 6, 10, 6);
				HubDraw.Fill(sb, dot, i == index ? AutomataHomeUIState.Ivory : AutomataHomeUIState.Ivory * 0.25f);
			}
		}
	}
}
