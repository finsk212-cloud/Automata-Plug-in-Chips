using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace Augments
{
	public class AugmentUISystem : ModSystem
	{
		// --- Boss-kill choice popup ---
		private UserInterface augmentInterface;
		private AugmentChoiceUIState choiceState;

		// --- "Your Augments" browsable list ---
		private UserInterface listInterface;
		private AugmentListUIState listState;

		// --- Vendor shop panel ---
		private UserInterface shopInterface;
		private AugmentShopUIState shopState;

		// --- Vendor gacha decryption panel ---
		private UserInterface gachaInterface;
		private AugmentGachaUIState gachaState;

		// --- Combat Analytics telemetry panel ---
		private UserInterface analyticsInterface;
		private AugmentAnalyticsUIState analyticsState;

		// --- Banner Station slot window ---
		private UserInterface bannerInterface;
		private BannerStationUIState bannerState;

		private GameTime lastUpdateUiGameTime;

		public override void Load()
		{
			if (Main.dedServ)
				return; // dedicated server has no screen, skip UI setup entirely

			augmentInterface = new UserInterface();
			listInterface = new UserInterface();
			shopInterface = new UserInterface();
			gachaInterface = new UserInterface();
			analyticsInterface = new UserInterface();
			bannerInterface = new UserInterface();
			BuildStates();
		}

		// The colour scheme the panels were built with. Panels set many of their colours once, when they are built, so when the
		// scheme is changed on the title screen they are built again (see UpdateUI), the next time none of them is open.
		private int builtScheme = -2;

		private void BuildStates()
		{
			choiceState = new AugmentChoiceUIState();
			choiceState.Activate(); // forces OnInitialize now, so backPanel always exists

			listState = new AugmentListUIState();
			listState.Activate();

			shopState = new AugmentShopUIState();
			shopState.Activate();

			gachaState = new AugmentGachaUIState();
			gachaState.Activate();

			analyticsState = new AugmentAnalyticsUIState();
			analyticsState.Activate();

			bannerState = new BannerStationUIState();
			bannerState.Activate();

			builtScheme = MenuLook.SelectedIndex;
		}

		// The Plugins menu turns on and off like an old computer screen (see CrtScreenEffect). 0 = off, 1 = fully on.
		private float listOpen = 1f;
		// +1 while turning on, -1 while turning off, 0 when settled.
		private int listAnimDir;
		private const float ListOnSeconds = 0.30f;
		private const float ListOffSeconds = 0.38f;

		private bool AnyPanelOpen =>
			augmentInterface?.CurrentState != null || listInterface?.CurrentState != null || shopInterface?.CurrentState != null
			|| gachaInterface?.CurrentState != null || analyticsInterface?.CurrentState != null || bannerInterface?.CurrentState != null;

		// Called every frame - keeps both panels' buttons/hover states responsive.
		public override void UpdateUI(GameTime gameTime)
		{
			lastUpdateUiGameTime = gameTime;

			if (builtScheme != MenuLook.SelectedIndex && !AnyPanelOpen)
				BuildStates();

			if (augmentInterface?.CurrentState != null)
				augmentInterface.Update(gameTime);

			if (listAnimDir != 0)
			{
				float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
				if (listAnimDir > 0)
				{
					listOpen += dt / ListOnSeconds;
					if (listOpen >= 1f)
					{
						listOpen = 1f;
						listAnimDir = 0;
					}
				}
				else
				{
					listOpen -= dt / ListOffSeconds;
					if (listOpen <= 0f)
					{
						listOpen = 0f;
						listAnimDir = 0;
						listInterface?.SetState(null);
					}
				}
			}

			if (listInterface?.CurrentState != null && listAnimDir >= 0)
				listInterface.Update(gameTime);

			if (shopInterface?.CurrentState != null)
				shopInterface.Update(gameTime);

			if (gachaInterface?.CurrentState != null)
				gachaInterface.Update(gameTime);

			if (analyticsInterface?.CurrentState != null)
				analyticsInterface.Update(gameTime);

			if (bannerInterface?.CurrentState != null)
				bannerInterface.Update(gameTime);

			AugmentFamilyHUD.Update(gameTime);
			AugmentPinnedHUD.Update(gameTime);
			AugmentAdvisoryHUD.Update(gameTime);
			EndingCallHUD.Update(gameTime);
			AugmentHiddenStatsDrawer.Update(gameTime);

			if (IsPlayerInputBlocked())
			{
				if (Main.LocalPlayer != null && Main.LocalPlayer.active)
				{
					Main.LocalPlayer.mouseInterface = true;
				}
			}
		}

		// Slots both panels into Terraria's actual draw order.
		public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
		{
			int mouseTextIndex = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Mouse Text"));
			if (mouseTextIndex == -1)
				return;

			// Insertion order matters here: each Insert places its layer
			// immediately before whatever currently sits at mouseTextIndex.
			// So the layer inserted FIRST in code ends up drawn LAST (on top).
			// We want: Auras (bottom) -> List UI -> Shop UI -> Choice UI -> Charges -> Cooldowns -> Family HUD -> Tooltip (top) -> Mouse Text.
			// So in code: insert Tooltip first, ..., List UI, then Auras last.
			layers.Insert(mouseTextIndex, new LegacyGameInterfaceLayer(
				"Augments: Sanitize Mouse Text",
				delegate
				{
					if (!string.IsNullOrEmpty(Main.hoverItemName))
					{
						if (Main.hoverItemName.Contains("[Augments]"))
							Main.hoverItemName = Main.hoverItemName.Replace("[Augments]", "").Trim();
						if (Main.hoverItemName.Contains("[augments]"))
							Main.hoverItemName = Main.hoverItemName.Replace("[augments]", "").Trim();
						if (Main.hoverItemName.EndsWith(" N P C"))
							Main.hoverItemName = Main.hoverItemName.Substring(0, Main.hoverItemName.Length - 6);
						else if (Main.hoverItemName.EndsWith(" NPC"))
							Main.hoverItemName = Main.hoverItemName.Substring(0, Main.hoverItemName.Length - 4);
					}
					return true;
				},
				InterfaceScaleType.UI)
			);

			layers.Insert(mouseTextIndex, new LegacyGameInterfaceLayer(
				"Augments: Tooltip",
				delegate
				{
					AugmentTooltipDrawer.DrawIfHovering(Main.spriteBatch);
					return true;
				},
				InterfaceScaleType.UI)
			);

			layers.Insert(mouseTextIndex, new LegacyGameInterfaceLayer(
				"Augments: Combat Analytics UI",
				delegate
				{
					if (lastUpdateUiGameTime != null && analyticsInterface?.CurrentState != null)
					{
						analyticsInterface.Draw(Main.spriteBatch, lastUpdateUiGameTime);
						if (Main.LocalPlayer != null && Main.LocalPlayer.active)
							Main.LocalPlayer.mouseInterface = true;
					}
					return true;
				},
				InterfaceScaleType.UI)
			);

			layers.Insert(mouseTextIndex, new LegacyGameInterfaceLayer(
				"Augments: Pinned Telemetry HUD",
				delegate
				{
					AugmentPinnedHUD.Draw(Main.spriteBatch);
					return true;
				},
				InterfaceScaleType.UI)
			);

			layers.Insert(mouseTextIndex, new LegacyGameInterfaceLayer(
				"Augments: Hidden Stats Drawer",
				delegate
				{
					AugmentHiddenStatsDrawer.Draw(Main.spriteBatch);
					return true;
				},
				InterfaceScaleType.UI)
			);

			layers.Insert(mouseTextIndex, new LegacyGameInterfaceLayer(
				"Augments: Family HUD",
				delegate
				{
					AugmentFamilyHUD.Draw(Main.spriteBatch);
					return true;
				},
				InterfaceScaleType.UI)
			);

			layers.Insert(mouseTextIndex, new LegacyGameInterfaceLayer(
				"Augments: Cooldowns",
				delegate
				{
					AugmentCooldownDrawer.DrawCooldowns(Main.spriteBatch);
					return true;
				},
				InterfaceScaleType.UI)
			);

			layers.Insert(mouseTextIndex, new LegacyGameInterfaceLayer(
				"Augments: Charges",
				delegate
				{
					AugmentChargeDrawer.DrawCharges(Main.spriteBatch);
					return true;
				},
				InterfaceScaleType.UI)
			);

			layers.Insert(mouseTextIndex, new LegacyGameInterfaceLayer(
				"Augments: Medi Gun HUD",
				delegate
				{
					MediGunHUDOverlay.Draw(Main.spriteBatch);
					return true;
				},
				InterfaceScaleType.Game)
			);

			layers.Insert(mouseTextIndex, new LegacyGameInterfaceLayer(
				"Augments: Choice UI",
				delegate
				{
					if (lastUpdateUiGameTime != null && augmentInterface?.CurrentState != null)
						augmentInterface.Draw(Main.spriteBatch, lastUpdateUiGameTime);
					return true;
				},
				InterfaceScaleType.UI)
			);

			layers.Insert(mouseTextIndex, new LegacyGameInterfaceLayer(
				"Augments: Banner Station UI",
				delegate
				{
					if (lastUpdateUiGameTime != null && bannerInterface?.CurrentState != null)
						bannerInterface.Draw(Main.spriteBatch, lastUpdateUiGameTime);
					return true;
				},
				InterfaceScaleType.UI)
			);

			layers.Insert(mouseTextIndex, new LegacyGameInterfaceLayer(
				"Augments: Gacha UI",
				delegate
				{
					if (lastUpdateUiGameTime != null && gachaInterface?.CurrentState != null)
						gachaInterface.Draw(Main.spriteBatch, lastUpdateUiGameTime);
					return true;
				},
				InterfaceScaleType.UI)
			);

			layers.Insert(mouseTextIndex, new LegacyGameInterfaceLayer(
				"Augments: Shop UI",
				delegate
				{
					if (lastUpdateUiGameTime != null && shopInterface?.CurrentState != null)
						shopInterface.Draw(Main.spriteBatch, lastUpdateUiGameTime);
					return true;
				},
				InterfaceScaleType.UI)
			);

			layers.Insert(mouseTextIndex, new LegacyGameInterfaceLayer(
				"Augments: List UI",
				delegate
				{
					if (lastUpdateUiGameTime != null && listInterface?.CurrentState != null)
						DrawList();
					return true;
				},
				InterfaceScaleType.UI)
			);

			// The Commander's call for an earned ending. Inserted before the tips, so it is drawn on top of them.
			layers.Insert(mouseTextIndex, new LegacyGameInterfaceLayer(
				"Augments: Ending Call",
				delegate
				{
					EndingCallHUD.Draw(Main.spriteBatch);
					return true;
				},
				InterfaceScaleType.UI)
			);

			// Pod 042's tips are drawn under every panel (inserted after them), so an open window always covers a tip instead of the tip
			// floating over it.
			layers.Insert(mouseTextIndex, new LegacyGameInterfaceLayer(
				"Augments: Advisory Ticker HUD",
				delegate
				{
					AugmentAdvisoryHUD.Draw(Main.spriteBatch);
					return true;
				},
				InterfaceScaleType.UI)
			);

			// Inserted last = drawn first = beneath all UI panels but above the game world.
			layers.Insert(mouseTextIndex, new LegacyGameInterfaceLayer(
				"Augments: Auras",
				delegate
				{
					AugmentAuraDrawer.DrawAuras(Main.spriteBatch);
					AugmentAuraDrawer.DrawChronobreakRings(Main.spriteBatch);
					return true;
				},
				InterfaceScaleType.UI)
			);

			// While the Automata hub is open in-game, switch off every HUD layer above (they sit after the
			// fullscreen UI in the draw order and would paint over the hub).
			if (AutomataHubSystem.IsOpenInGame)
			{
				foreach (GameInterfaceLayer layer in layers)
				{
					if (layer.Name.StartsWith("Augments:") && layer.Name != "Augments: Sanitize Mouse Text")
						layer.Active = false;
				}
			}
		}

		// --- Choice popup controls ---

		public void ShowChoices(List<Augment> choices, AugmentRarity rarity, RarityBracket bracket = RarityBracket.PreHardmode, bool networkReward = false, int rerollCount = 0)
		{
			choiceState.SetChoices(choices, rarity, bracket, networkReward, rerollCount);
			augmentInterface?.SetState(choiceState);
			if (rerollCount == 0)
				SoundEngine.PlaySound(SoundID.Research);
		}

		public void HidePanel()
		{
			augmentInterface?.SetState(null);
		}

		public bool IsOpen => augmentInterface?.CurrentState != null;
		public bool IsChoiceOpenAndActive => augmentInterface?.CurrentState != null && choiceState != null && !choiceState.IsMinimized;

		public bool IsPlayerInputBlocked()
		{
			if (Main.dedServ)
				return false;

			// Boss choice popup (active modal selection)
			if (IsChoiceOpenAndActive)
				return true;

			// Choice popup minimized, hovering over restore icon
			if (choiceState != null && choiceState.IsMouseOverRestore)
				return true;

			// Plugins Menu (active modal window)
			if (IsListOpen)
				return true;

			// Vendor Shop (active modal window)
			if (IsShopOpen)
				return true;

			// Decryption Chamber (active modal window)
			if (IsGachaOpen)
				return true;

			// Combat Analytics (active modal dashboard)
			if (IsAnalyticsOpen)
				return true;

			// Pinned HUD being dragged or manipulated with Alt/Shift
			if (AugmentPinnedHUD.IsInteractingAny)
				return true;

			return false;
		}

		public bool IsAnyOtherPanelOpenThanAnalytics()
		{
			if (IsListOpen)
				return true;
			if (IsShopOpen)
				return true;
			if (IsGachaOpen)
				return true;
			if (IsChoiceOpenAndActive)
				return true;
			if (AugmentPinnedHUD.IsInteractingAny)
				return true;
			return false;
		}

		public bool IsAnyOtherPanelOpenThanList()
		{
			if (IsAnalyticsOpen)
				return true;
			if (IsShopOpen)
				return true;
			if (IsGachaOpen)
				return true;
			if (IsChoiceOpenAndActive)
				return true;
			if (AugmentPinnedHUD.IsInteractingAny)
				return true;
			return false;
		}

		public bool IsTypingAnywhere()
		{
			if (Terraria.GameInput.PlayerInput.WritingText)
				return true;
			if (Main.drawingPlayerChat || Main.editSign || Main.editChest)
				return true;
			if (Main.CurrentInputTextTakerOverride != null)
				return true;
			if (KeybindEditor.BlocksInput)
				return true;
			if (listState != null && listState.IsSearchFocused)
				return true;
			if (analyticsState != null && analyticsState.IsSearchFocused)
				return true;
			return false;
		}

		public void RefreshOpenPlayerPanels()
		{
			if (IsListOpen)
				listState.Refresh();
			if (IsShopOpen)
				shopState.Refresh();
			if (IsGachaOpen)
				gachaState.Refresh();
			if (IsAnalyticsOpen)
				analyticsState.Refresh();
		}

		// --- Combat Analytics telemetry controls ---

		public void ShowAnalytics()
		{
			analyticsState?.Refresh();
			analyticsInterface?.SetState(analyticsState);
			SoundEngine.PlaySound(SoundID.MenuOpen);
		}

		public void HideAnalytics()
		{
			analyticsInterface?.SetState(null);
			SoundEngine.PlaySound(SoundID.MenuClose);
		}

		public void ToggleAnalytics()
		{
			if (IsAnalyticsOpen)
			{
				if (analyticsState != null && analyticsState.IsSearchFocused)
					return;
				HideAnalytics();
			}
			else
			{
				if (IsTypingAnywhere() || IsAnyOtherPanelOpenThanAnalytics())
					return;
				ShowAnalytics();
			}
		}

		public bool IsAnalyticsOpen => analyticsInterface?.CurrentState != null;

		// --- "Your Augments" list controls ---

		public void ShowList()
		{
			listState.Refresh();
			listInterface?.SetState(listState);

			// Turn the screen on. If it was still turning off, it turns back on from where it is.
			if (listAnimDir == 0)
				listOpen = 0f;
			listAnimDir = 1;
		}

		// One frame of the Plugins menu: drawn normally when settled, through the screen effect while turning on or off.
		private void DrawList()
		{
			if (listAnimDir == 0 && listOpen >= 1f)
			{
				listInterface.Draw(Main.spriteBatch, lastUpdateUiGameTime);
				return;
			}

			CrtScreenEffect.Draw(Main.spriteBatch, listOpen, listState.PanelBounds, () => listInterface.Draw(Main.spriteBatch, lastUpdateUiGameTime));
		}

		public void HideList()
		{
			if (listInterface?.CurrentState == null || listAnimDir < 0)
				return;

			// Turn the screen off; the menu is removed when the effect has finished (see UpdateUI).
			listAnimDir = -1;
		}

		public void ToggleList()
		{
			if (IsListOpen)
			{
				if (listState != null && listState.IsSearchFocused)
					return;
				HideList();
			}
			else
			{
				if (IsTypingAnywhere() || IsAnyOtherPanelOpenThanList())
					return;
				ShowList();
			}
		}

		public bool IsListOpen => listInterface?.CurrentState != null && listAnimDir >= 0;

		// --- Vendor shop panel controls ---

		public void ShowShop()
		{
			if (shopState == null)
				return;

			var ap = Main.LocalPlayer?.GetModPlayer<AugmentPlayer>();
			if (ap != null)
			{
				AugmentAdvisoryHUD.TriggerSmartAdvisory(ap, "first_vendor", "[POD 042]", "All plugins can be removed for a 100% full Machine Core refund at the shop, so feel free to experiment with different setups.");
			}

			if (IsGachaOpen)
				HideGacha();

			int essenceType = ModContent.ItemType<AugmentEssenceItem>();
			for (int i = 0; i < Main.maxItems; i++)
			{
				Item it = Main.item[i];
				if (it.active && it.type == essenceType && Vector2.Distance(it.Center, Main.LocalPlayer.Center) < 600f)
				{
					Main.LocalPlayer.QuickSpawnItem(Main.LocalPlayer.GetSource_FromThis(), essenceType, it.stack);
					it.active = false;
					it.type = ItemID.None;
					if (Main.netMode == NetmodeID.Server)
						NetMessage.SendData(MessageID.SyncItem, -1, -1, null, i);
				}
			}

			shopState.ResetTab();
			shopState.Refresh();
			shopInterface?.SetState(shopState);
		}

		public void HideShop()
		{
			shopInterface?.SetState(null);
		}

		public void ToggleShop()
		{
			if (IsShopOpen)
				HideShop();
			else
				ShowShop();
		}

		public bool IsShopOpen => shopInterface?.CurrentState != null;

		// --- Vendor gacha decryption panel controls ---

		public void ShowGacha()
		{
			if (gachaState == null)
				return;

			if (IsShopOpen)
				HideShop();

			int essenceType = ModContent.ItemType<AugmentEssenceItem>();
			for (int i = 0; i < Main.maxItems; i++)
			{
				Item it = Main.item[i];
				if (it.active && it.type == essenceType && Vector2.Distance(it.Center, Main.LocalPlayer.Center) < 600f)
				{
					Main.LocalPlayer.QuickSpawnItem(Main.LocalPlayer.GetSource_FromThis(), essenceType, it.stack);
					it.active = false;
					it.type = ItemID.None;
					if (Main.netMode == NetmodeID.Server)
						NetMessage.SendData(MessageID.SyncItem, -1, -1, null, i);
				}
			}

			gachaState.Refresh();
			gachaInterface?.SetState(gachaState);
		}

		public void HideGacha()
		{
			gachaInterface?.SetState(null);
		}

		public void ToggleGacha()
		{
			if (IsGachaOpen)
				HideGacha();
			else
				ShowGacha();
		}

		// Server reply to a gacha roll request (multiplayer).
		public void OnGachaResult(string augmentId)
		{
			gachaState?.OnGachaResult(augmentId);
		}

		public bool IsGachaOpen => gachaInterface?.CurrentState != null;

		// --- Banner Station slot window controls ---

		public bool IsBannerStationOpen => bannerInterface?.CurrentState != null;

		public void ShowBannerStation(int left, int top)
		{
			if (bannerState == null)
				return;

			// Right-clicking the station that is already open closes it, like a chest.
			if (IsBannerStationOpen && bannerState.StationLeft == left && bannerState.StationTop == top)
			{
				HideBannerStation();
				return;
			}

			Main.playerInventory = true;
			bannerState.Open(left, top);
			bannerInterface.SetState(bannerState);
			SoundEngine.PlaySound(SoundID.MenuOpen);
		}

		public void HideBannerStation()
		{
			if (!IsBannerStationOpen)
				return;

			bannerInterface.SetState(null);
			SoundEngine.PlaySound(SoundID.MenuClose);
		}
	}
}
