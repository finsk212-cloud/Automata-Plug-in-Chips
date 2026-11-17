using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace Augments
{
	// Owns the Automata title-screen pages and the hooks that show them.
	//
	// HOME PAGE. When the game wants to show the plain root menu (Main.menuMode == 0), a hook on
	// Main.DrawMenu swaps in the Automata home page instead (a title-screen UI page, shown with
	// Main.MenuUI and menuMode 888, the same way tModLoader's own menus work). The vanilla button column is
	// never drawn, so there is nothing to hide. Vanilla screens that say "Back" set menuMode to 0 again,
	// and the hook simply puts the home page back.
	//
	// WAYS OUT. Because this runs on every launch, it has several ways to hand control back to the vanilla menu:
	//   * the "Use Automata Title Menu" setting in the mod's config turns it off completely;
	//   * the home page has a CLASSIC MENU button, and the classic menu shows a small AUTOMATA MENU button to come back;
	//   * any exception on these pages is logged once and switches the pages off for the rest of the session.
	public class AutomataHubSystem : ModSystem
	{
		private static AutomataHubUIState state;      // the 7-tab hub window
		private static AutomataHomeUIState home;      // the new home page
		private static bool homeDisabled;             // set after a failure
		private static bool classicSession;           // the player chose the classic menu this session
		private static bool pendingFallback;          // a title-screen page failed; hand the screen back after this frame
		private static bool pendingCloseInGame;       // the in-game hub failed; close it next tick
		private static bool failureLogged;
		private static bool wasLeftDown;

		// While the Multiplayer (12) or Settings (11) page is showing, the top bar and currency box are drawn over it so it
		// feels like part of the same menu (see DrawVanillaChrome).
		private static HomeSection? pendingSection; // the section the home page should open on when it comes back
		private static HomeTopBar vanillaBar;

		// Corner button position and size, in screen pixels. Kept below AutomataMusic's top-left HUD text (about y 40 to 90).
		private static readonly Rectangle CornerButton = new Rectangle(28, 104, 250, 46);

		public static bool ConfigWantsHome => AugmentConfig.Instance?.UseAutomataTitleMenu ?? true;
		private static bool HomeWanted => home != null && !homeDisabled && !classicSession && ConfigWantsHome;

		// True when the Automata menu theme (Earth backdrop, corner text, title card) is the one being drawn.
		public static bool AutomataMusicThemeActive
		{
			get
			{
				try
				{
					return AutomataTitleMenu.IsActive;
				}
				catch
				{
					return false;
				}
			}
		}

		public override void Load()
		{
			if (Main.dedServ)
				return;

			// Read the in-game patch notes once, here, so the title screen never touches the file.
			PatchNotesData.Load(Mod);
			ShowcaseVideo.Prepare(Mod); // copy any showcase clips out of the mod file now, while it is loading
			ProfileStore.Load();        // the saved character summary, shown on the title screen
			AccountStore.Load();        // Data Fragments, purchases and what is equipped (the account file)
			MenuLook.Load();            // the title-screen colour scheme and frame, from the account

			state = new AutomataHubUIState();
			state.Activate(); // builds the elements now, same as the other panels

			home = new AutomataHomeUIState();
			home.Activate();

			On_Main.DrawMenu += DrawMenuHook;
		}

		public override void Unload()
		{
			On_Main.DrawMenu -= DrawMenuHook;
			ShowcaseVideo.Unload();
			HubDraw.UnloadShapes();
			ProfileStore.Unload();
			AccountStore.Unload();

			// If one of our pages is still showing on the title screen, take it down.
			try
			{
				UIState current = Main.MenuUI?.CurrentState;
				if (current != null && (current == state || current == home))
				{
					Main.MenuUI.SetState(null);
					if (Main.menuMode == 888)
						Main.menuMode = 0;
				}
			}
			catch
			{
			}

			state = null;
			home = null;
			homeDisabled = false;
			classicSession = false;
			pendingFallback = false;
			pendingCloseInGame = false;
			failureLogged = false;
		}

		public override void PostUpdateEverything()
		{
			if (pendingCloseInGame && !Main.gameMenu)
			{
				pendingCloseInGame = false;
				IngameFancyUI.Close();
			}
		}

		// --- Safety ---

		// Runs a click handler so that a bug in it falls back to the vanilla menu instead of crashing.
		public static void Guard(Action action)
		{
			try
			{
				action?.Invoke();
			}
			catch (Exception e)
			{
				ReportFailure(e);
			}
		}

		// Called by any element that throws. Logs once, then asks for the vanilla menu to be restored.
		public static void ReportFailure(Exception e)
		{
			if (!failureLogged)
			{
				failureLogged = true;
				ModContent.GetInstance<Augments>().Logger.Error("Automata menu pages failed and were switched off for this session: " + e);
			}

			if (Main.gameMenu)
			{
				homeDisabled = true;
				pendingFallback = true;
			}
			else
			{
				pendingCloseInGame = true;
			}
		}

		// --- Opening and closing the hub window ---

		// True while the hub is showing inside a world (opened with /hub). The mod's HUD layers use this to
		// stand down, because they would otherwise draw on top of the hub.
		public static bool IsOpenInGame => state != null && !Main.gameMenu && Main.InGameUI?.CurrentState == state;

		public static void OpenInGame()
		{
			if (state == null || Main.gameMenu)
				return;

			SoundEngine.PlaySound(SoundID.MenuOpen);
			IngameFancyUI.OpenUIState(state);
		}

		// Shows the hub window on the title screen, starting on the given tab (0 = Briefing / patch notes).
		public static void OpenHub(int tab = 0)
		{
			if (state == null)
				return;

			state.SelectTab(tab);
			SoundEngine.PlaySound(SoundID.MenuOpen);
			Main.MenuUI.SetState(state);
			Main.menuMode = 888; // "show the title-screen UI page"
		}

		// Back button of the hub window. On the title screen this returns to menuMode 0, and the hook
		// then shows the home page again (or the vanilla menu if the home page is off).
		public static void Close()
		{
			if (state == null)
				return;

			if (Main.gameMenu)
			{
				Main.MenuUI.SetState(null);
				Main.menuMode = 0;
			}
			else
			{
				IngameFancyUI.Close();
			}
		}

		// --- Navigation used by the home page's buttons ---

		public static void GoSinglePlayer()
		{
			SoundEngine.PlaySound(SoundID.MenuOpen);
			Main.OpenCharacterSelectUI();
		}

		public static void GoMultiplayer()
		{
			SoundEngine.PlaySound(SoundID.MenuOpen);
			Main.MenuUI.SetState(null);
			Main.menuMode = 12;
		}

		public static void GoSettings()
		{
			SoundEngine.PlaySound(SoundID.MenuOpen);
			Main.MenuUI.SetState(null);
			Main.menuMode = 11;
		}

		// The section the home page should show when it opens: the one a tab on a vanilla page asked for, else HOME.
		public static HomeSection TakePendingSection()
		{
			HomeSection section = pendingSection ?? HomeSection.Home;
			pendingSection = null;
			return section;
		}

		public static void GoExit()
		{
			try
			{
				Main.SaveSettings();
			}
			catch
			{
			}

			Main.instance.Exit();
		}

		// Shows the plain vanilla menu (which has Workshop, Mods and Mod Config) until the player comes back.
		public static void GoClassic()
		{
			SoundEngine.PlaySound(SoundID.MenuClose);
			classicSession = true;
			Main.MenuUI.SetState(null);
			Main.menuMode = 0;
		}

		// --- Title-screen hook ---

		private static void DrawMenuHook(On_Main.orig_DrawMenu orig, Main self, GameTime gameTime)
		{
			// Before vanilla draws: if it is about to show the root menu, show our home page instead.
			try
			{
				if (Main.menuMode == 0 && HomeWanted)
				{
					// Vanilla's own root-menu frame never runs now. Put the "no multiplayer session in progress"
					// flags back to what the root menu would have, so that Multiplayer -> Back -> Single Player
					// cannot carry on into the multiplayer flow.
					Main.netMode = NetmodeID.SinglePlayer;
					Main.menuMultiplayer = false;
					Main.menuServer = false;

					Main.MenuUI.SetState(home);
					Main.menuMode = 888;
				}
			}
			catch (Exception e)
			{
				ReportFailure(e);
			}

			orig(self, gameTime);

			try
			{
				AfterMenuDrawn();
			}
			catch (Exception e)
			{
				ReportFailure(e);
			}
		}

		private static void AfterMenuDrawn()
		{
			// A page threw while drawing: take it down now that vanilla has finished its frame.
			if (pendingFallback)
			{
				pendingFallback = false;
				UIState current = Main.MenuUI?.CurrentState;
				if (current != null && (current == state || current == home))
				{
					Main.MenuUI.SetState(null);
					Main.menuMode = 0;
				}
			}

			// A click in another window must not count, so only read the mouse while the game is the active window.
			bool leftDown = Main.instance.IsActive && Mouse.GetState().LeftButton == ButtonState.Pressed;
			bool clicked = leftDown && !wasLeftDown;
			wasLeftDown = leftDown;

			// The top bar and currency box are drawn over the vanilla pages whose numbers are known from the log:
			// Multiplayer (menu 12), the Join via IP screen (13), Settings (menu 11) and the eight Settings sub-pages. The
			// character list and world list are the game's own UI pages (menu 888) and are left alone. Only while the Automata menu is on.
			if (!homeDisabled && !classicSession && ConfigWantsHome && state != null && !Main.dedServ
				&& Main.gameMenu && IsChromePage(Main.menuMode))
			{
				DrawVanillaChrome(clicked);
			}

			// The small corner button only exists on the plain root menu.
			if (homeDisabled || state == null || Main.dedServ || Main.menuMode != 0)
				return;

			if (!ConfigWantsHome)
				DrawCornerButton("AUTOMATA", "BUNKER TERMINAL", clicked, () => OpenHub(0));
			else if (classicSession)
				DrawCornerButton("AUTOMATA MENU", "BACK TO HOME", clicked, () => classicSession = false);
		}

		// Settings sub-pages, read from the game log by opening each entry in turn: General 112, Interface 1112, Video 1111,
		// Volume 26, Cursor 1125, Controls 1127, Language 1213, tModLoader Settings 10017. Every one returns to 11 on Back.
		private static bool IsSettingsSubPage(int mode)
		{
			switch (mode)
			{
				case 112:
				case 1112:
				case 1111:
				case 26:
				case 1125:
				case 1127:
				case 1213:
				case 10017:
					return true;
				default:
					return false;
			}
		}

		private static bool IsChromePage(int mode)
		{
			// 13 is the "Join via IP" address screen, reached from the character list (read from the log).
			return mode == 11 || mode == 12 || mode == 13 || IsSettingsSubPage(mode);
		}

		// Draws the top bar and currency box over a vanilla page. Everything is laid out in UI units, exactly like the
		// home page, so the strip sits in the same place whichever page you are on.
		private static void DrawVanillaChrome(bool clicked)
		{
			SpriteBatch sb = Main.spriteBatch;
			float uiScale = Main.UIScale > 0.01f ? Main.UIScale : 1f;

			bool began = false;
			try
			{
				sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Matrix.CreateScale(uiScale, uiScale, 1f));
				began = true;
			}
			catch (InvalidOperationException)
			{
			}

			try
			{
				// With our own batch the mouse is in raw pixels, so convert it to UI units. Inside the game's batch,
				// the game's mouse is already in UI units.
				MouseState rawMouse = Mouse.GetState();
				Point mouse = began ? new Point((int)(rawMouse.X / uiScale), (int)(rawMouse.Y / uiScale)) : new Point(Main.mouseX, Main.mouseY);

				float width = Main.graphics.GraphicsDevice.Viewport.Width / uiScale;
				if (vanillaBar == null)
					vanillaBar = new HomeTopBar(() => (HomeSection)(-1), _ => { }); // no tab is current on a vanilla page

				// The title card is at the top in the middle on these pages, so the strip sits under it. The Settings pages and the
				// Join via IP screen start their content higher, so there the strip sits a little higher too.
				bool high = Main.menuMode == 11 || Main.menuMode == 13 || IsSettingsSubPage(Main.menuMode);
				int barY = high ? AutomataHomeUIState.VanillaBarYHigh : AutomataHomeUIState.VanillaBarY;

				float barW = vanillaBar.Fit(width - 2f * AutomataHomeUIState.Margin);

				var barRect = new Rectangle((int)Math.Round((width - barW) / 2f), barY, (int)Math.Round(barW), AutomataHomeUIState.BarH);
				vanillaBar.DrawBar(sb, barRect, mouse);
				HomeCurrency.Paint(sb, new Rectangle((int)(width - AutomataHomeUIState.Margin - 260f), AutomataHomeUIState.VanillaCurrencyY, 260, 64));

				if (clicked)
				{
					int tab = vanillaBar.HitTab(barRect, mouse);
					if (tab >= 0)
					{
						// Back to the home page, opened on the section that was clicked.
						pendingSection = (HomeSection)tab;
						SoundEngine.PlaySound(SoundID.MenuOpen);

						// Settings change as you click them, but the file is only written when you press Back. Leaving from
						// a tab would skip that, so write it here.
						if (Main.menuMode == 11 || IsSettingsSubPage(Main.menuMode))
						{
							try
							{
								Main.SaveSettings();
							}
							catch (Exception e)
							{
								ModContent.GetInstance<Augments>().Logger.Warn("Settings could not be saved when leaving the Settings pages: " + e.Message);
							}
						}

						Main.menuMode = 0;
					}
				}
			}
			finally
			{
				if (began)
					sb.End();
			}
		}

		private static void DrawCornerButton(string title, string subtitle, bool clicked, Action onClick)
		{
			SpriteBatch sb = Main.spriteBatch;

			// Find out whether the game still has a sprite batch open. Begin throws if one is, and then we
			// just draw into it instead of starting our own.
			bool began = false;
			try
			{
				sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Matrix.Identity);
				began = true;
			}
			catch (InvalidOperationException)
			{
			}

			try
			{
				// With our own batch, positions are raw screen pixels. Inside the game's batch, use the game's mouse.
				MouseState rawMouse = Mouse.GetState();
				Point mouse = began ? new Point(rawMouse.X, rawMouse.Y) : new Point(Main.mouseX, Main.mouseY);
				bool hovered = CornerButton.Contains(mouse);

				Rectangle r = CornerButton;
				Color fill = hovered ? HubTheme.Accent : HubTheme.Panel * 0.94f;
				Color text = hovered ? HubTheme.OnAccent : HubTheme.Text;
				Color sub = hovered ? HubTheme.OnAccent * 0.75f : HubTheme.Dim;

				HubDraw.Fill(sb, r, fill);
				HubDraw.Border(sb, r, HubTheme.Accent);
				HubDraw.Corners(sb, new Rectangle(r.X - 4, r.Y - 4, r.Width + 8, r.Height + 8), HubTheme.Accent, 10, 2);
				HubDraw.CursorRight(sb, r.X + 12, r.Y + r.Height / 2, 12, text);
				HubDraw.Text(sb, title, new Vector2(r.X + 34, r.Y + 6), text, 0.9f, 2.5f);
				HubDraw.Text(sb, subtitle, new Vector2(r.X + 35, r.Y + 27), sub, 0.6f, 2f);

				if (hovered && clicked)
				{
					SoundEngine.PlaySound(SoundID.MenuOpen);
					onClick?.Invoke();
				}
			}
			finally
			{
				if (began)
					sb.End();
			}
		}
	}
}
