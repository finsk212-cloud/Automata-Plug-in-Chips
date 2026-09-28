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

		private GameTime lastUpdateUiGameTime;

		public override void Load()
		{
			if (Main.dedServ)
				return; // dedicated server has no screen, skip UI setup entirely

			augmentInterface = new UserInterface();
			choiceState = new AugmentChoiceUIState();
			choiceState.Activate(); // forces OnInitialize now, so backPanel always exists

			listInterface = new UserInterface();
			listState = new AugmentListUIState();
			listState.Activate();

			shopInterface = new UserInterface();
			shopState = new AugmentShopUIState();
			shopState.Activate();

			gachaInterface = new UserInterface();
			gachaState = new AugmentGachaUIState();
			gachaState.Activate();

			analyticsInterface = new UserInterface();
			analyticsState = new AugmentAnalyticsUIState();
			analyticsState.Activate();
		}

		// Called every frame - keeps both panels' buttons/hover states responsive.
		public override void UpdateUI(GameTime gameTime)
		{
			lastUpdateUiGameTime = gameTime;

			if (augmentInterface?.CurrentState != null)
				augmentInterface.Update(gameTime);

			if (listInterface?.CurrentState != null)
				listInterface.Update(gameTime);

			if (shopInterface?.CurrentState != null)
				shopInterface.Update(gameTime);

			if (gachaInterface?.CurrentState != null)
				gachaInterface.Update(gameTime);

			if (analyticsInterface?.CurrentState != null)
				analyticsInterface.Update(gameTime);

			AugmentFamilyHUD.Update(gameTime);
			AugmentPinnedHUD.Update(gameTime);
			AugmentAdvisoryHUD.Update(gameTime);

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
				"Augments: Advisory Ticker HUD",
				delegate
				{
					AugmentAdvisoryHUD.Draw(Main.spriteBatch);
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
						listInterface.Draw(Main.spriteBatch, lastUpdateUiGameTime);
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
					return true;
				},
				InterfaceScaleType.UI)
			);
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
		}

		public void HideList()
		{
			listInterface?.SetState(null);
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

		public bool IsListOpen => listInterface?.CurrentState != null;

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

		public bool IsGachaOpen => gachaInterface?.CurrentState != null;
	}
}
