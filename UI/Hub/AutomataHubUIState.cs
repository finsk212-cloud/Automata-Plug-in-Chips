using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.UI;

namespace Augments
{
	// The Automata hub: a NieR-style terminal window with a tab list on the left, the page in the middle
	// and a Pod 042 column on the right. This is the LAYOUT ONLY: every page is static placeholder art.
	//
	// It is host-agnostic. AutomataHubSystem shows it either on the title screen (Main.MenuUI) or in-game
	// (IngameFancyUI), and nothing in here reads the local player, because no character exists on the title screen.
	public class AutomataHubUIState : UIState
	{
		public const int HeaderH = 64;
		public const int FooterH = 44;
		public const int NavW = 256;
		public const int SideW = 296;
		public const int Gap = 12;

		public static readonly string[] TabNames =
		{
			"BRIEFING", "ARCHIVE", "PROFILE", "ORDERS", "SHOP", "COSMETICS", "HACKING"
		};

		public int CurrentTab { get; private set; }

		// Counts down after a tab switch so the page can flicker in.
		internal int SwitchTicks { get; private set; }
		internal const int SwitchDuration = 14;

		// On narrow windows the right-hand Pod column is hidden and the page takes its space,
		// because the page would otherwise get too small to lay out.
		public const float CompactBelow = 1250f;
		public static bool Compact { get; private set; }

		private HubFrame frame;
		private HubPage page;
		private HubSide side;

		public override void OnInitialize()
		{
			Compact = false;

			var backdrop = new HubBackdrop();
			backdrop.Width.Set(0f, 1f);
			backdrop.Height.Set(0f, 1f);
			Append(backdrop);

			frame = new HubFrame();
			frame.Width.Set(0f, 0.92f);
			frame.MaxWidth.Set(1320f, 0f);
			frame.Height.Set(0f, 0.88f);
			frame.MaxHeight.Set(780f, 0f);
			frame.HAlign = 0.5f;
			frame.VAlign = 0.5f;
			backdrop.Append(frame);

			// Header: back button, top right.
			var back = new HubButton("BACK", "ESC", AutomataHubSystem.Close, danger: true);
			back.Width.Set(124f, 0f);
			back.Height.Set(32f, 0f);
			back.Left.Set(-(124f + 18f), 1f);
			back.Top.Set(16f, 0f);
			frame.Append(back);

			// Footer: skin preview button, bottom right.
			var skin = new HubButton(null, null, HubTheme.CycleSkin) { LabelProvider = () => "SKIN  " + HubTheme.SkinName };
			skin.Width.Set(170f, 0f);
			skin.Height.Set(28f, 0f);
			skin.Left.Set(-(170f + 18f), 1f);
			skin.Top.Set(-(FooterH - 8f), 1f);
			frame.Append(skin);

			// Left column: tab list.
			for (int i = 0; i < TabNames.Length; i++)
			{
				var item = new HubNavItem(this, i);
				item.Left.Set(18f, 0f);
				item.Top.Set(HeaderH + 18f + i * 50f, 0f);
				item.Width.Set(NavW - 18f, 0f);
				item.Height.Set(42f, 0f);
				frame.Append(item);
			}

			// Middle: the page.
			page = new HubPage(this);
			page.Left.Set(NavW + Gap + 6f, 0f);
			page.Top.Set(HeaderH + 18f, 0f);
			page.Width.Set(-(NavW + SideW + Gap * 2 + 12f + 18f), 1f);
			page.Height.Set(-(HeaderH + FooterH + 36f), 1f);
			frame.Append(page);

			// Right column: Pod 042 and details.
			side = new HubSide(this);
			side.Left.Set(-(SideW + 18f), 1f);
			side.Top.Set(HeaderH + 18f, 0f);
			side.Width.Set(SideW, 0f);
			side.Height.Set(-(HeaderH + FooterH + 36f), 1f);
			frame.Append(side);
		}

		public void SelectTab(int index)
		{
			if (index == CurrentTab || index < 0 || index >= TabNames.Length)
				return;

			CurrentTab = index;
			SwitchTicks = SwitchDuration;
			SoundEngine.PlaySound(SoundID.MenuOpen);
		}

		// Gives the page the Pod column's space when the window is narrow.
		private void ApplyLayout()
		{
			// Page starts at NavW + Gap + 6. Normal: leave room for the Pod column. Compact: just an 18px margin.
			float width = Compact
				? -(NavW + Gap + 6f + 18f)
				: -(NavW + SideW + Gap * 2 + 12f + 18f);
			page.Width.Set(width, 1f);
			page.Recalculate();
		}

		public override void Update(GameTime gameTime)
		{
			try
			{
				UpdateInner(gameTime);
			}
			catch (Exception e)
			{
				AutomataHubSystem.ReportFailure(e);
			}
		}

		private void UpdateInner(GameTime gameTime)
		{
			base.Update(gameTime);

			if (SwitchTicks > 0)
				SwitchTicks--;

			if (frame != null && page != null)
			{
				bool compact = frame.GetDimensions().Width < CompactBelow;
				if (compact != Compact)
				{
					Compact = compact;
					ApplyLayout();
				}
			}

			// The title screen has no in-game Esc handling for our screen, so do it here.
			// In-game, IngameFancyUI closes itself on Esc.
			if (Main.gameMenu && Main.keyState.IsKeyDown(Keys.Escape) && Main.oldKeyState.IsKeyUp(Keys.Escape))
				AutomataHubSystem.Close();
		}
	}

	// Full-screen dim, dot grid, scanlines and thin top/bottom rules behind the window.
	internal class HubBackdrop : HubElementBase
	{
		protected override void DrawContent(SpriteBatch spriteBatch)
		{
			Rectangle r = GetDimensions().ToRectangle();
			HubDraw.Fill(spriteBatch, r, HubTheme.Backdrop);

			// Dot grid.
			Color dot = HubTheme.Dim * 0.10f;
			for (int y = r.Y + 12; y < r.Bottom; y += 28)
				for (int x = r.X + 12; x < r.Right; x += 28)
					HubDraw.Fill(spriteBatch, new Rectangle(x, y, 1, 1), dot);

			// Scanlines.
			Color scan = Color.Black * 0.10f;
			for (int y = r.Y; y < r.Bottom; y += 4)
				HubDraw.Fill(spriteBatch, new Rectangle(r.X, y, r.Width, 1), scan);

			// Thin rules across the very top and bottom of the screen, with tick marks.
			Color rule = HubTheme.Line * 0.7f;
			HubDraw.HLine(spriteBatch, r.X, r.Y + 10, r.Width, rule);
			HubDraw.HLine(spriteBatch, r.X, r.Bottom - 11, r.Width, rule);
			for (int x = r.X + 40; x < r.Right; x += 120)
			{
				HubDraw.Fill(spriteBatch, new Rectangle(x, r.Y + 10, 1, 5), rule);
				HubDraw.Fill(spriteBatch, new Rectangle(x, r.Bottom - 15, 1, 5), rule);
			}
		}
	}

	// The window: panel, structural lines, header and footer text.
	internal class HubFrame : HubElementBase
	{
		protected override void DrawContent(SpriteBatch sb)
		{
			Rectangle r = GetDimensions().ToRectangle();
			HubDraw.Fill(sb, r, HubTheme.Panel);
			HubDraw.Border(sb, r, HubTheme.Line);
			HubDraw.Corners(sb, new Rectangle(r.X - 5, r.Y - 5, r.Width + 10, r.Height + 10), HubTheme.Accent, 18, 2);

			// Header.
			var logo = new Rectangle(r.X + 18, r.Y + 14, 36, 36);
			HubDraw.Fill(sb, logo, HubTheme.Accent);
			HubDraw.Diamond(sb, logo.Center.ToVector2(), 14f, HubTheme.OnAccent);
			HubDraw.Text(sb, "AUTOMATA", new Vector2(r.X + 68, r.Y + 9), HubTheme.Text, 1.3f, 4f);
			HubDraw.Text(sb, "PLUG-IN CHIPS  //  BUNKER TERMINAL", new Vector2(r.X + 69, r.Y + 38), HubTheme.Dim, 0.7f, 2f);

			// Placeholder readouts to the left of the back button.
			int rx = r.Right - 18 - 124 - 28;
			HubDraw.TextRight(sb, "MACHINE CORES  000", rx, r.Y + 12, HubTheme.Warn, 0.8f, 1f);
			HubDraw.TextRight(sb, "UNIT RANK  --", rx, r.Y + 34, HubTheme.Dim, 0.8f, 1f);

			// Header rule with centre node.
			int headY = r.Y + AutomataHubUIState.HeaderH;
			HubDraw.HLine(sb, r.X + 18, headY, r.Width - 36, HubTheme.Line);
			HubDraw.Diamond(sb, new Vector2(r.X + r.Width / 2f, headY + 0.5f), 5f, HubTheme.Accent);

			// Footer rule and text.
			int footY = r.Bottom - AutomataHubUIState.FooterH;
			HubDraw.HLine(sb, r.X + 18, footY, r.Width - 36, HubTheme.Line);
			HubDraw.Text(sb, "AUTOMATA  //  BUNKER TERMINAL  //  LAYOUT PREVIEW", new Vector2(r.X + 20, footY + 15), HubTheme.Dim, 0.68f, 2f);
			HubDraw.TextRight(sb, "[CLICK] SELECT     [ESC] BACK", r.Right - 18 - 170 - 24, footY + 15, HubTheme.Dim, 0.68f, 2f);

			// Vertical rules between the three columns.
			int colTop = headY + 10;
			int colH = footY - headY - 20;
			HubDraw.VLine(sb, r.X + AutomataHubUIState.NavW + AutomataHubUIState.Gap, colTop, colH, HubTheme.LineSoft);
			if (!AutomataHubUIState.Compact)
				HubDraw.VLine(sb, r.Right - AutomataHubUIState.SideW - 18 - AutomataHubUIState.Gap, colTop, colH, HubTheme.LineSoft);
		}
	}

	// Plain button with a hard edge. Used for Back and the skin preview.
	internal class HubButton : HubElementBase
	{
		private readonly string label;
		private readonly string keyHint;
		private readonly Action onClick;
		private readonly bool danger;
		private bool hovered;

		public Func<string> LabelProvider;

		public HubButton(string label, string keyHint, Action onClick, bool danger = false)
		{
			this.label = label;
			this.keyHint = keyHint;
			this.onClick = onClick;
			this.danger = danger;
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
			SoundEngine.PlaySound(SoundID.MenuClose);
			AutomataHubSystem.Guard(onClick);
		}

		protected override void DrawContent(SpriteBatch sb)
		{
			Rectangle r = GetDimensions().ToRectangle();
			Color edge = danger ? HubTheme.Danger : HubTheme.Accent;
			Color fill = hovered ? edge : Color.Transparent;
			Color text = hovered ? (danger ? Color.White : HubTheme.OnAccent) : (danger ? HubTheme.Danger : HubTheme.Text);

			HubDraw.Fill(sb, r, fill);
			HubDraw.Border(sb, r, edge);

			string shown = LabelProvider != null ? LabelProvider() : label;
			float scale = 0.8f;
			float y = r.Y + (r.Height - HubDraw.LineHeight(scale)) / 2f;
			if (keyHint != null)
			{
				HubDraw.Text(sb, shown, new Vector2(r.X + 12, y), text, scale, 2f);
				HubDraw.TextRight(sb, "[" + keyHint + "]", r.Right - 10, y, text * 0.75f, 0.66f, 1f);
			}
			else
			{
				float w = HubDraw.Width(shown, scale, 2f);
				HubDraw.Text(sb, shown, new Vector2(r.X + (r.Width - w) / 2f, y), text, scale, 2f);
			}
		}
	}

	// One entry in the left tab list. The selected one is an inverted bar with a cursor, like the game's menus.
	internal class HubNavItem : HubElementBase
	{
		private readonly AutomataHubUIState state;
		private readonly int index;
		private bool hovered;

		public HubNavItem(AutomataHubUIState state, int index)
		{
			this.state = state;
			this.index = index;
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
			AutomataHubSystem.Guard(() => state.SelectTab(index));
		}

		protected override void DrawContent(SpriteBatch sb)
		{
			Rectangle r = GetDimensions().ToRectangle();
			bool selected = state.CurrentTab == index;
			bool locked = index == AutomataHubUIState.TabNames.Length - 1;

			Color text = selected ? HubTheme.OnAccent : (hovered ? HubTheme.Text : (locked ? HubTheme.Dim * 0.8f : HubTheme.Dim));
			if (selected)
			{
				HubDraw.Fill(sb, r, HubTheme.Accent);
				HubDraw.CursorRight(sb, r.X + 8, r.Y + r.Height / 2, 10, HubTheme.OnAccent);
			}
			else
			{
				HubDraw.Fill(sb, r, hovered ? HubTheme.PanelAlt : Color.Transparent);
				HubDraw.Border(sb, r, hovered ? HubTheme.Line : HubTheme.LineSoft);
			}

			float scale = 0.8f;
			float y = r.Y + (r.Height - HubDraw.LineHeight(scale)) / 2f;
			HubDraw.Text(sb, (index + 1).ToString("00"), new Vector2(r.X + 20, y + 1), selected ? HubTheme.OnAccent * 0.7f : HubTheme.Dim * 0.8f, 0.66f, 1f);
			HubDraw.Text(sb, AutomataHubUIState.TabNames[index], new Vector2(r.X + 46, y), text, scale, 1.5f);

			// Small boxed tag on locked tabs, instead of text that runs into the label.
			if (locked)
			{
				var tag = new Rectangle(r.Right - 52, r.Y + (r.Height - 18) / 2, 42, 18);
				HubDraw.Border(sb, tag, text * 0.8f);
				float tw = HubDraw.Width("LOCK", 0.56f, 1f);
				HubDraw.Text(sb, "LOCK", new Vector2(tag.X + (tag.Width - tw) / 2f, tag.Y + (tag.Height - HubDraw.LineHeight(0.56f)) / 2f), text * 0.8f, 0.56f, 1f);
			}
		}
	}

	// The middle column: draws whichever page is selected, with a short flicker when switching.
	internal class HubPage : HubElementBase
	{
		private readonly AutomataHubUIState state;

		public HubPage(AutomataHubUIState state)
		{
			this.state = state;
		}

		protected override void DrawContent(SpriteBatch sb)
		{
			Rectangle r = GetDimensions().ToRectangle();
			int t = state.SwitchTicks;
			float progress = 1f - t / (float)AutomataHubUIState.SwitchDuration;

			HubPages.Fade = t > 0 ? 0.25f + 0.75f * progress : 1f;
			HubPages.DrawPage(sb, state.CurrentTab, r);
			HubPages.Fade = 1f;

			// Glitch bars sliding down the page right after a switch.
			if (t > 0)
			{
				int seed = state.CurrentTab * 7 + t;
				for (int i = 0; i < 3; i++)
				{
					int bandY = r.Y + (int)(((seed * 37 + i * 91) % 100) / 100f * (r.Height - 8));
					int bandH = 2 + (seed + i) % 4;
					int shift = ((seed * 13 + i * 29) % 21) - 10;
					HubDraw.Fill(sb, new Rectangle(r.X + Math.Max(0, shift), bandY, r.Width - Math.Abs(shift), bandH), HubTheme.Accent * (0.35f * (1f - progress)));
				}
			}
		}
	}

	// The right column: Pod 042 dialogue and a details box.
	internal class HubSide : HubElementBase
	{
		private readonly AutomataHubUIState state;

		public HubSide(AutomataHubUIState state)
		{
			this.state = state;
		}

		protected override void DrawContent(SpriteBatch sb)
		{
			if (AutomataHubUIState.Compact)
				return;

			HubPages.DrawSide(sb, state.CurrentTab, GetDimensions().ToRectangle());
		}
	}
}
