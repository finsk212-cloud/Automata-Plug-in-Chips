using System;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Augments
{
	// The Automata title screen theme: the rotating Earth, the title card, the corner text and the menu buttons, all drawn in the
	// colour scheme picked on the COSMETICS page. It used to live in the companion mod Automata: Music and now lives here.
	//
	// The one-way link: this mod asks AutomataMusic (if it is installed) for the menu song and for the lyrics banner, through
	// Mod.Call. Without AutomataMusic the theme still draws, with the normal menu music and no lyrics.
	public class AutomataTitleMenu : ModMenu
	{
		public override string DisplayName => "Automata: Plug-in Chips (Bunker)";

		// The first Automata: Music version that no longer draws the title screen itself. An older one still does, so the
		// theme here stands down next to it instead of drawing a second Earth and a second set of buttons.
		private static readonly Version FirstCompatibleMusicVersion = new Version(1, 1, 0);

		internal static bool CompanionTooOld
		{
			get
			{
				return ModLoader.TryGetMod("AutomataMusic", out Mod music) && music.Version < FirstCompatibleMusicVersion;
			}
		}

		internal static bool ConfigWantsTheme => (AugmentConfig.Instance?.BunkerMenuTheme ?? true) && !CompanionTooOld;

		// True while the Automata theme is the title screen being drawn.
		internal static bool IsActive => Main.gameMenu && ConfigWantsTheme && MenuLoader.CurrentMenu is AutomataTitleMenu;

		private static Mod MusicMod
		{
			get
			{
				ModLoader.TryGetMod("AutomataMusic", out Mod mod);
				return mod;
			}
		}

		public override int Music
		{
			get
			{
				try
				{
					// AutomataMusic answers with the slot of its "Weight of the World" track, or -1 if it has none.
					if (MusicMod?.Call("MenuMusicSlot") is int slot && slot > 0)
						return slot;
				}
				catch (Exception)
				{
					// Fall through to the normal menu music.
				}

				return MusicID.Title;
			}
		}

		public override bool PreDrawLogo(SpriteBatch spriteBatch, ref Vector2 logoDrawCenter, ref float logoRotation, ref float logoScale, ref Color drawColor)
		{
			if (!ConfigWantsTheme)
			{
				BunkerMenuTheme.CardRight = 0;
				return true;
			}

			BunkerMenuTheme.Draw(spriteBatch, logoDrawCenter);
			return false; // the normal Terraria logo is replaced by the title card
		}

		public override void PostDrawLogo(SpriteBatch spriteBatch, Vector2 logoDrawCenter, float logoRotation, float logoScale, Color drawColor)
		{
			try
			{
				// The lyrics are synchronised to the song, so AutomataMusic draws them. The colour factor goes along with the call.
				MusicMod?.Call("DrawMenuLyrics", spriteBatch, MenuLook.TintFactor);
			}
			catch (Exception)
			{
				// No lyrics is better than a broken title screen.
			}
		}

		public override void Unload()
		{
			OrbitalBackdrop.Unload();
		}
	}

	// Everything the theme needs from the game besides drawing: switching it on by default, and hiding the vanilla version
	// number and social buttons that would sit on top of the corner text.
	public class AutomataTitleThemeSystem : ModSystem
	{
		private static bool checkedDefaultMenu;

		public override void Load()
		{
			// The title screen only exists on a client.
			if (Main.dedServ)
				return;

			NierMenuButtons.Load();
			Main.OnPostDraw += OnPostDrawHandler;
			On_Main.DrawVersionNumber += Hook_DrawVersionNumber;
			On_Main.DrawSocialMediaButtons += Hook_DrawSocialMediaButtons;
			On_Main.DrawtModLoaderSocialMediaButtons += Hook_DrawtModLoaderSocialMediaButtons;
			checkedDefaultMenu = false;
		}

		public override void Unload()
		{
			if (Main.dedServ)
				return;

			NierMenuButtons.Unload();
			Main.OnPostDraw -= OnPostDrawHandler;
			On_Main.DrawVersionNumber -= Hook_DrawVersionNumber;
			On_Main.DrawSocialMediaButtons -= Hook_DrawSocialMediaButtons;
			On_Main.DrawtModLoaderSocialMediaButtons -= Hook_DrawtModLoaderSocialMediaButtons;
			checkedDefaultMenu = false;
		}

		public override void PostSetupContent()
		{
			if (Main.dedServ)
				return;

			if (!AutomataTitleMenu.CompanionTooOld && (AugmentConfig.Instance == null || AugmentConfig.Instance.SetAsDefaultMenuTheme))
				ActivateMenuTheme();
		}

		// Makes the Automata theme the selected menu, the same way picking it from the menu list would.
		internal static void ActivateMenuTheme()
		{
			try
			{
				var menu = ModContent.GetInstance<AutomataTitleMenu>();
				if (menu == null)
					return;

				Type menuLoaderType = typeof(MenuLoader);
				FieldInfo switchToMenu = menuLoaderType.GetField("switchToMenu", BindingFlags.NonPublic | BindingFlags.Static);
				FieldInfo lastSelected = menuLoaderType.GetField("LastSelectedModMenu", BindingFlags.NonPublic | BindingFlags.Static);

				switchToMenu?.SetValue(null, menu);
				lastSelected?.SetValue(null, menu.FullName);
			}
			catch (Exception)
			{
				// The theme can still be picked by hand from the menu list.
			}
		}

		private static void OnPostDrawHandler(GameTime gameTime)
		{
			if (checkedDefaultMenu || !Main.gameMenu)
				return;

			checkedDefaultMenu = true;
			if (!AutomataTitleMenu.CompanionTooOld && (AugmentConfig.Instance == null || AugmentConfig.Instance.SetAsDefaultMenuTheme))
			{
				var menu = ModContent.GetInstance<AutomataTitleMenu>();
				if (menu != null && MenuLoader.CurrentMenu != menu)
					ActivateMenuTheme();
			}
		}

		private static void Hook_DrawVersionNumber(On_Main.orig_DrawVersionNumber orig, Color menuColor, float upBump)
		{
			if (AutomataTitleMenu.IsActive)
				return; // the corner text of the theme takes this place

			orig(menuColor, upBump);
		}

		private static void Hook_DrawSocialMediaButtons(On_Main.orig_DrawSocialMediaButtons orig, Color menuColor, float upBump)
		{
			if (AutomataTitleMenu.IsActive)
				return;

			orig(menuColor, upBump);
		}

		private static void Hook_DrawtModLoaderSocialMediaButtons(On_Main.orig_DrawtModLoaderSocialMediaButtons orig, Color menuColor, float upBump)
		{
			if (AutomataTitleMenu.IsActive)
				return;

			orig(menuColor, upBump);
		}
	}
}
