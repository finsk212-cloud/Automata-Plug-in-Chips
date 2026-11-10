using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;
using Terraria.UI.Chat;
using ReLogic.Content;

namespace Augments
{
	public enum AugmentSortMode
	{
		NameAZ,
		NameZA,
		RarityHighLow,
		RarityLowHigh,
		Class,
		Newest,
		Oldest
	}

	public enum AugmentStatusFilter
	{
		All,
		Equipped,
		Unequipped,
		NewOnly
	}

	// Clean, Terraria-style in-game Augment Codex & Inventory Slot List.
	// Displays augments in inventory-style slot boxes categorized by tier (Common, Rare, Epic, Legendary),
	// with an interactive inspector panel on the right showing full detailed info when clicked.
	public class AugmentListUIState : UIState
	{
		public static bool IsDevMode = false;
		public static bool IsDevCritMode = false;
		public static Asset<Texture2D> RarityStarAsset;

		private UIPanel backPanel;
		private UIList gridList;
		private UIScrollbar gridScrollbar;
		private AugmentDetailPanel detailPanel;

		private CodexSearchBar searchBar;
		public bool IsSearchFocused => searchBar != null && searchBar.IsFocused;
		private string searchQuery = "";

		private UIElement devBarContainer;
		private readonly CodexFilterButton[] loadoutButtons = new CodexFilterButton[AugmentPlayer.MaxLoadouts];
		private DevBadgeElement devBadge;
		private CodexFilterButton critToggleBtn;
		private int titleClickCount = 0;
		private double lastTitleClickTime = 0;

		private string currentTab = "All";
		private Augment selectedAugment;
		private readonly List<CodexTabButton> tabButtons = new List<CodexTabButton>();

		private AugmentClass? currentClassFilter = null;
		private AugmentRarity? currentRarityFilter = null;
		private AugmentStatusFilter currentStatusFilter = AugmentStatusFilter.All;
		private AugmentSortMode currentSortMode = AugmentSortMode.NameAZ;
		private bool isFilterMenuOpen = false;

		private CodexFilterButton classBtn;
		private CodexFilterButton sortBtn;
		private CodexFilterButton filterBtn;
		private AugmentFilterPanel filterPanel;
		private CodexDropdown dropdownMenu;
		private UIElement activeDropdownTrigger;

		private const float PanelWidth = 880f;
		private const float PanelHeight = 600f;
		private readonly UIParticleSystem listParticles = new UIParticleSystem(70);
		private const int SlotsPerRow = 6;
		private const float SlotWidth = 74f;
		private const float SlotHeight = 84f;
		private const float SlotSpacing = 10f;

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);

			KeybindEditor.Tick();

			if (backPanel != null && backPanel.ContainsPoint(Main.MouseScreen))
			{
				Main.LocalPlayer.mouseInterface = true;
			}
			else if (filterPanel != null && isFilterMenuOpen && filterPanel.ContainsPoint(Main.MouseScreen))
			{
				Main.LocalPlayer.mouseInterface = true;
			}

			if (dropdownMenu != null && dropdownMenu.IsOpen)
			{
				if (dropdownMenu.ContainsPoint(Main.MouseScreen))
				{
					Main.LocalPlayer.mouseInterface = true;
				}
				else if (Main.mouseLeft && Main.mouseLeftRelease)
				{
					if (activeDropdownTrigger == null || !activeDropdownTrigger.ContainsPoint(Main.MouseScreen))
					{
						CloseActiveDropdown();
					}
				}
			}

			listParticles.Update();
			if (backPanel != null && Main.rand.NextBool(8))
			{
				CalculatedStyle pdims = backPanel.GetDimensions();
				if (pdims.Width > 0)
				{
					Rectangle prect = new Rectangle((int)pdims.X, (int)pdims.Y, (int)pdims.Width, (int)pdims.Height);
					Color emberCol = Main.rand.NextBool(3) ? new Color(0, 220, 255) : new Color(60, 140, 240);
					listParticles.SpawnAmbient(prect, emberCol, 1.8f);
				}
			}
		}

		public override void OnDeactivate()
		{
			base.OnDeactivate();
			CloseActiveDropdown();
			KeybindEditor.Cancel();
			if (searchBar != null)
				searchBar.IsFocused = false;
		}

		public override void OnInitialize()
		{
			// Main Background Panel (dark cybernetic style shared with the other panels)
			backPanel = new PluginsBackPanel();
			backPanel.Width.Set(PanelWidth, 0f);
			backPanel.Height.Set(PanelHeight, 0f);
			backPanel.HAlign = 0.5f;
			backPanel.VAlign = 0.5f;
			backPanel.SetPadding(0f);
			backPanel.BackgroundColor = new Color(10, 16, 28, 250);
			backPanel.BorderColor = new Color(30, 41, 59);

			// Title Header & Secret Dev Badge
			UIElement titleContainer = new UIElement();
			titleContainer.Width.Set(380f, 0f);
			titleContainer.Height.Set(28f, 0f);
			titleContainer.HAlign = 0.5f;
			titleContainer.Top.Set(8f, 0f);

			UIText title = new UIText("Plugins", 1.12f)
			{
				HAlign = 0.44f,
				VAlign = 0.5f,
				TextColor = new Color(248, 250, 252)
			};
			titleContainer.Append(title);

			devBadge = new DevBadgeElement(this);
			devBadge.Left.Set(295f, 0f);
			devBadge.Top.Set(3f, 0f);
			titleContainer.Append(devBadge);

			titleContainer.OnLeftClick += (evt, elem) => OnTitleClicked();
			title.OnLeftClick += (evt, elem) => OnTitleClicked();

			backPanel.Append(titleContainer);

			// Close button (shared across all panels)
			PanelChrome.AddCloseButton(backPanel, () => ModContent.GetInstance<AugmentUISystem>().HideList());

			// Search Bar in top-left corner (Left = 18f, Width = 210f)
			searchBar = new CodexSearchBar();
			searchBar.Left.Set(18f, 0f);
			searchBar.Top.Set(8f, 0f);
			searchBar.Width.Set(210f, 0f);
			searchBar.Height.Set(24f, 0f);
			searchBar.OnSearchChanged += OnSearchInputChanged;
			backPanel.Append(searchBar);

			// Tab Buttons Bar across top (Left = 18f)
			CreateTabButtons();

			// Filter & Sort Controls above Detail Panel (Left = 556f, Width = 306f, Top = 38f)
			CreateFilterBarControls();

			// Left: Grid List Container for Inventory Slot Boxes (Left = 18f, Width = 504f)
			gridList = new UIList();
			gridList.ManualSortMethod = _ => { };
			gridList.Top.Set(74f, 0f);
			gridList.Left.Set(18f, 0f);
			gridList.Width.Set(504f, 0f);
			gridList.Height.Set(-125f, 1f);
			gridList.ListPadding = 6f;
			backPanel.Append(gridList);

			// Scrollbar for Grid (Left = 528f, Width = 8f)
			gridScrollbar = new PanelScrollbar();
			gridScrollbar.Top.Set(74f, 0f);
			gridScrollbar.Height.Set(-125f, 1f);
			gridScrollbar.Left.Set(528f, 0f);
			gridScrollbar.Width.Set(8f, 0f);
			gridList.SetScrollbar(gridScrollbar);
			backPanel.Append(gridScrollbar);

			// Right: Detailed Info Inspector Panel (Left = 556f, Width = 306f, ends at 862f)
			detailPanel = new AugmentDetailPanel(this);
			detailPanel.Top.Set(74f, 0f);
			detailPanel.Left.Set(556f, 0f);
			detailPanel.Width.Set(306f, 0f);
			detailPanel.Height.Set(-125f, 1f);
			backPanel.Append(detailPanel);

			// Bottom-Left: Dev Mode Action Bar
			CreateDevBarControls();
			if (IsDevMode)
				backPanel.Append(devBarContainer);

			CreateLoadoutBar();

			Append(backPanel);
		}

		public override void Draw(SpriteBatch spriteBatch)
		{
			base.Draw(spriteBatch);
			listParticles.Draw(spriteBatch);
			AugmentChoiceCard.DrawActiveTooltip(spriteBatch);
		}

		private void CreateTabButtons()
		{
			string[] tabs = { "All", "Common", "Rare", "Epic", "Legendary", "Equipped" };
			float startLeft = 18f;
			float buttonWidth = 79f;
			float gap = 6f;

			for (int i = 0; i < tabs.Length; i++)
			{
				string tab = tabs[i];
				var btn = new CodexTabButton(tab);
				btn.Width.Set(buttonWidth, 0f);
				btn.Height.Set(26f, 0f);
				btn.Top.Set(38f, 0f);
				btn.Left.Set(startLeft + i * (buttonWidth + gap), 0f);
				btn.IsActive = (tab == currentTab);
				btn.Clicked += SwitchTab;

				tabButtons.Add(btn);
				backPanel.Append(btn);
			}
		}

		private static readonly AugmentClass?[] AvailableClasses = new AugmentClass?[]
		{
			null,
			AugmentClass.Universal,
			AugmentClass.Melee,
			AugmentClass.Ranged,
			AugmentClass.Magic,
			AugmentClass.Summon,
			AugmentClass.Support
		};

		private void CreateFilterBarControls()
		{
			dropdownMenu = new CodexDropdown(this);

			// 1. Class Button (Left = 556f, Width = 114f)
			classBtn = new CodexFilterButton(GetClassBtnText());
			classBtn.Top.Set(38f, 0f);
			classBtn.Left.Set(556f, 0f);
			classBtn.Width.Set(114f, 0f);
			classBtn.Height.Set(26f, 0f);
			classBtn.Clicked += ToggleClassDropdown;
			classBtn.RightClicked += () =>
			{
				CloseActiveDropdown();
				SetClassFilter(null);
			};
			backPanel.Append(classBtn);

			// 2. Sort Button (Left = 676f, Width = 114f)
			sortBtn = new CodexFilterButton(GetSortBtnText());
			sortBtn.Top.Set(38f, 0f);
			sortBtn.Left.Set(676f, 0f);
			sortBtn.Width.Set(114f, 0f);
			sortBtn.Height.Set(26f, 0f);
			sortBtn.Clicked += ToggleSortDropdown;
			sortBtn.RightClicked += () =>
			{
				CloseActiveDropdown();
				SetSortMode(AugmentSortMode.NameAZ);
			};
			backPanel.Append(sortBtn);

			// 3. Filter Button (Left = 796f, Width = 66f, ends at 862f)
			filterBtn = new CodexFilterButton(GetFilterBtnText());
			filterBtn.Top.Set(38f, 0f);
			filterBtn.Left.Set(796f, 0f);
			filterBtn.Width.Set(66f, 0f);
			filterBtn.Height.Set(26f, 0f);
			filterBtn.Clicked += ToggleFilterDropdown;
			filterBtn.RightClicked += () =>
			{
				CloseActiveDropdown();
				ResetAllFilters();
			};
			backPanel.Append(filterBtn);

			// Floating Filter & Sort Panel
			filterPanel = new AugmentFilterPanel(this);
			filterPanel.Left.Set(542f, 0f);
			filterPanel.Top.Set(68f, 0f);
			filterPanel.Width.Set(320f, 0f);
			filterPanel.Height.Set(365f, 0f);
		}

		// Bottom-right: three loadout apply buttons under the detail panel.
		private void CreateLoadoutBar()
		{
			for (int i = 0; i < AugmentPlayer.MaxLoadouts; i++)
			{
				int slot = i;
				var btn = new CodexFilterButton("", 0.72f);
				btn.Left.Set(574f + i * 98f, 0f);
				btn.Top.Set(556f, 0f);
				btn.Width.Set(92f, 0f);
				btn.Height.Set(28f, 0f);
				btn.Clicked += () => OnLoadoutClicked(slot);
				loadoutButtons[i] = btn;
				backPanel.Append(btn);
			}
		}

		private void OnLoadoutClicked(int slot)
		{
			var ap = Main.LocalPlayer.GetModPlayer<AugmentPlayer>();
			if (slot >= ap.LoadoutSlotsUnlocked)
			{
				Main.NewText($"Loadout {slot + 1} is locked. Unlock it at Mistress 2B.", 255, 200, 120);
				return;
			}
			if (ap.GetLoadout(slot).Count == 0)
			{
				Main.NewText($"Loadout {slot + 1} is empty. Save it at Mistress 2B.", 255, 200, 120);
				return;
			}
			string reason = ap.GetLoadoutLockReason();
			if (reason != null)
			{
				Main.NewText(reason, 255, 140, 140);
				return;
			}
			ap.RequestLoadoutOp(LoadoutOp.ApplyLoadout, slot);
		}

		private void UpdateLoadoutButtons()
		{
			if (loadoutButtons[0] == null)
				return;

			var ap = Main.LocalPlayer.GetModPlayer<AugmentPlayer>();
			for (int i = 0; i < AugmentPlayer.MaxLoadouts; i++)
			{
				bool unlocked = i < ap.LoadoutSlotsUnlocked;
				bool hasData = unlocked && ap.GetLoadout(i).Count > 0;
				loadoutButtons[i].SetText(!unlocked ? $"{i + 1} · Locked" : hasData ? $"Loadout {i + 1}" : $"{i + 1} · Empty");
				loadoutButtons[i].IsActiveHighlight = hasData && ap.ActiveLoadout == i;
				loadoutButtons[i].BorderColor = unlocked ? new Color(30, 42, 66) : new Color(120, 48, 56);
				loadoutButtons[i].BackgroundColor = unlocked ? new Color(10, 16, 28) : new Color(20, 14, 24);
			}
		}

		private void CreateDevBarControls()
		{
			devBarContainer = new UIElement();
			devBarContainer.Left.Set(18f, 0f);
			devBarContainer.Top.Set(556f, 0f);
			devBarContainer.Width.Set(540f, 0f);
			devBarContainer.Height.Set(28f, 0f);

			var clearAllBtn = new CodexFilterButton("Clear All", 0.72f);
			clearAllBtn.Left.Set(0f, 0f);
			clearAllBtn.Top.Set(0f, 0f);
			clearAllBtn.Width.Set(70f, 0f);
			clearAllBtn.Height.Set(26f, 0f);
			clearAllBtn.CustomActiveBorder = new Color(255, 100, 100);
			clearAllBtn.Clicked += () =>
			{
				if (Main.netMode == NetmodeID.MultiplayerClient)
					AugmentNet.SendDebugCommandRequest(DebugAugmentCommandType.Clear);
				else
					AugmentNet.ApplyDebugCommand(Main.LocalPlayer, DebugAugmentCommandType.Clear);

				SoundEngine.PlaySound(SoundID.Shatter);
				Main.NewText("All equipped plugins cleared!", Color.Yellow);
				PopulateGrid();
				if (selectedAugment != null)
					detailPanel.SetAugment(selectedAugment, false);
			};
			devBarContainer.Append(clearAllBtn);

			var resetCdsBtn = new CodexFilterButton("Reset CDs", 0.72f);
			resetCdsBtn.Left.Set(74f, 0f);
			resetCdsBtn.Top.Set(0f, 0f);
			resetCdsBtn.Width.Set(80f, 0f);
			resetCdsBtn.Height.Set(26f, 0f);
			resetCdsBtn.CustomActiveBorder = new Color(100, 220, 255);
			resetCdsBtn.Clicked += () =>
			{
				Main.LocalPlayer.GetModPlayer<AugmentPlayer>().ResetAllCooldowns();
				SoundEngine.PlaySound(SoundID.MaxMana, Main.LocalPlayer.Center);
				Main.NewText("All plugin cooldowns have been reset to 0!", Color.Cyan);
			};
			devBarContainer.Append(resetCdsBtn);

			critToggleBtn = new CodexFilterButton(IsDevCritMode ? "Crit: ON" : "100% Crit", 0.72f);
			critToggleBtn.Left.Set(158f, 0f);
			critToggleBtn.Top.Set(0f, 0f);
			critToggleBtn.Width.Set(96f, 0f);
			critToggleBtn.Height.Set(26f, 0f);
			critToggleBtn.CustomActiveBorder = new Color(255, 140, 40);
			critToggleBtn.CustomActiveBg = new Color(80, 30, 20);
			critToggleBtn.IsActiveHighlight = IsDevCritMode;
			critToggleBtn.Clicked += () =>
			{
				IsDevCritMode = !IsDevCritMode;
				critToggleBtn.IsActiveHighlight = IsDevCritMode;
				critToggleBtn.SetText(IsDevCritMode ? "Crit: ON" : "100% Crit");
				SoundEngine.PlaySound(IsDevCritMode ? SoundID.Item4 : SoundID.MenuClose);
				Main.NewText(IsDevCritMode
					? "100% Crit Mode ACTIVATED! (All damage will critically strike)"
					: "100% Crit Mode DEACTIVATED.",
					IsDevCritMode ? Color.OrangeRed : Color.Orange);
			};
			devBarContainer.Append(critToggleBtn);

			var spawnDummyBtn = new CodexFilterButton("Spawn Dummy", 0.72f);
			spawnDummyBtn.Left.Set(258f, 0f);
			spawnDummyBtn.Top.Set(0f, 0f);
			spawnDummyBtn.Width.Set(100f, 0f);
			spawnDummyBtn.Height.Set(26f, 0f);
			spawnDummyBtn.CustomActiveBorder = new Color(120, 255, 120);
			spawnDummyBtn.Clicked += () =>
			{
				var player = Main.LocalPlayer;

				// Despawn ALL existing test dummies (both custom and vanilla 488) so there is ONLY ever ONE
				int dummyType = ModContent.NPCType<TestDummyNPC>();
				for (int i = 0; i < Main.maxNPCs; i++)
				{
					if (Main.npc[i].active && (Main.npc[i].type == dummyType || Main.npc[i].type == NPCID.TargetDummy))
					{
						Main.npc[i].active = false;
						Main.npc[i].type = NPCID.None;
						Main.npc[i].netUpdate = true;
					}
				}

				// Spawn 42 pixels in front of player at player's center height (well within sword & weapon reach)
				int spawnX = (int)(player.Center.X + player.direction * 42);
				int spawnY = (int)player.Center.Y;

				if (Main.netMode == NetmodeID.SinglePlayer)
				{
					int npcIndex = NPC.NewNPC(player.GetSource_FromThis(), spawnX, spawnY, dummyType);
					if (npcIndex >= 0 && npcIndex < Main.maxNPCs)
					{
						Main.npc[npcIndex].netUpdate = true;
					}
					SoundEngine.PlaySound(SoundID.Dig, player.Center);
					Main.NewText("✦ [DEV] Target Dummy spawned in front of you! (Right-click button to remove) ✦", Color.LimeGreen);
				}
				else
				{
					Main.NewText("✦ [DEV] Dummy spawn is available in SinglePlayer! ✦", Color.Orange);
				}
			};
			spawnDummyBtn.RightClicked += () =>
			{
				int dummyType = ModContent.NPCType<TestDummyNPC>();
				bool removed = false;
				for (int i = 0; i < Main.maxNPCs; i++)
				{
					if (Main.npc[i].active && (Main.npc[i].type == dummyType || Main.npc[i].type == NPCID.TargetDummy))
					{
						Main.npc[i].active = false;
						Main.npc[i].type = NPCID.None;
						Main.npc[i].netUpdate = true;
						removed = true;
					}
				}
				if (removed)
				{
					SoundEngine.PlaySound(SoundID.NPCDeath1, Main.LocalPlayer.Center);
					Main.NewText("✦ [DEV] Target Dummy removed. ✦", Color.Orange);
				}
			};
			devBarContainer.Append(spawnDummyBtn);

			var testRollBtn = new CodexFilterButton("Test Roll", 0.72f);
			testRollBtn.Left.Set(362f, 0f);
			testRollBtn.Top.Set(0f, 0f);
			testRollBtn.Width.Set(84f, 0f);
			testRollBtn.Height.Set(26f, 0f);
			testRollBtn.CustomActiveBorder = new Color(255, 215, 80);
			testRollBtn.Clicked += () =>
			{
				ModContent.GetInstance<AugmentUISystem>().HideList();
				if (Main.netMode == NetmodeID.SinglePlayer)
					AugmentRewardLogic.GrantReward(Main.LocalPlayer, RarityBracket.FinalCalamity);
				else if (Main.netMode == NetmodeID.MultiplayerClient)
					AugmentNet.SendDebugRewardRequest();

				SoundEngine.PlaySound(SoundID.MenuOpen);
			};
			devBarContainer.Append(testRollBtn);

			var spawnVendorBtn = new CodexFilterButton("2B Vendor", 0.72f);
			spawnVendorBtn.Left.Set(450f, 0f);
			spawnVendorBtn.Top.Set(0f, 0f);
			spawnVendorBtn.Width.Set(90f, 0f);
			spawnVendorBtn.Height.Set(26f, 0f);
			spawnVendorBtn.CustomActiveBorder = new Color(255, 160, 210);
			spawnVendorBtn.Clicked += () =>
			{
				AugmentNet.RequestVendorSpawn(Main.LocalPlayer);
			};
			spawnVendorBtn.RightClicked += () =>
			{
				ModContent.GetInstance<AugmentUISystem>().HideList();
				ModContent.GetInstance<AugmentUISystem>().ShowShop();
				SoundEngine.PlaySound(SoundID.MenuOpen);
			};
			devBarContainer.Append(spawnVendorBtn);
		}

		private void OnTitleClicked()
		{
			double now = Main.gameTimeCache?.TotalGameTime.TotalSeconds ?? 0;
			if (now - lastTitleClickTime > 2.5)
			{
				titleClickCount = 0;
			}
			lastTitleClickTime = now;
			titleClickCount++;

			if (titleClickCount >= 5)
			{
				titleClickCount = 0;
				ToggleDevMode();
			}
			else
			{
				SoundEngine.PlaySound(SoundID.MenuTick);
			}
		}

		public void ToggleDevMode()
		{
			IsDevMode = !IsDevMode;
			if (IsDevMode)
			{
				SoundEngine.PlaySound(SoundID.Item4);
				Main.NewText("✦ [DEV MODE ACTIVATED] ✦ Right-click any augment to toggle equip!", Color.Gold);
			}
			else
			{
				SoundEngine.PlaySound(SoundID.MenuClose);
				Main.NewText("✦ [DEV MODE DEACTIVATED] ✦", Color.Orange);
				if (IsDevCritMode)
				{
					IsDevCritMode = false;
					if (critToggleBtn != null)
					{
						critToggleBtn.IsActiveHighlight = false;
						critToggleBtn.SetText("💥 100% Crit");
					}
				}
			}

			UpdateDevModeVisuals();
			PopulateGrid();
		}

		public void UpdateDevModeVisuals()
		{
			if (backPanel == null)
				return;

			if (IsDevMode)
			{
				if (devBarContainer != null && !backPanel.HasChild(devBarContainer))
					backPanel.Append(devBarContainer);
			}
			else
			{
				if (devBarContainer != null && backPanel.HasChild(devBarContainer))
					backPanel.RemoveChild(devBarContainer);
			}

			if (selectedAugment != null && detailPanel != null)
			{
				bool isOwned = Main.LocalPlayer.GetModPlayer<AugmentPlayer>().HasAugment(selectedAugment.Id);
				detailPanel.SetAugment(selectedAugment, isOwned);
			}
		}

		private void CycleClassForward()
		{
			int idx = Array.IndexOf(AvailableClasses, currentClassFilter);
			int next = (idx + 1) % AvailableClasses.Length;
			SetClassFilter(AvailableClasses[next]);
		}

		private void CycleClassBackward()
		{
			int idx = Array.IndexOf(AvailableClasses, currentClassFilter);
			int prev = (idx - 1 + AvailableClasses.Length) % AvailableClasses.Length;
			SetClassFilter(AvailableClasses[prev]);
		}

		public void SetClassFilter(AugmentClass? newClass)
		{
			currentClassFilter = newClass;
			CloseActiveDropdown();
			UpdateFilterControlStates();
			PopulateGrid();
		}

		private void CycleSortForward()
		{
			const int totalSortModes = 7;
			currentSortMode = (AugmentSortMode)(((int)currentSortMode + 1) % totalSortModes);
			CloseActiveDropdown();
			UpdateFilterControlStates();
			PopulateGrid();
		}

		private void CycleSortBackward()
		{
			const int totalSortModes = 7;
			currentSortMode = (AugmentSortMode)(((int)currentSortMode - 1 + totalSortModes) % totalSortModes);
			CloseActiveDropdown();
			UpdateFilterControlStates();
			PopulateGrid();
		}

		public void SetSortMode(AugmentSortMode mode)
		{
			currentSortMode = mode;
			CloseActiveDropdown();
			UpdateFilterControlStates();
			PopulateGrid();
		}

		public void SetRarityFilter(AugmentRarity? rarity)
		{
			currentRarityFilter = rarity;
			currentStatusFilter = AugmentStatusFilter.All;
			currentTab = rarity.HasValue ? rarity.Value.ToString() : "All";
			CloseActiveDropdown();
			UpdateTabButtons();
			UpdateFilterControlStates();
			PopulateGrid();
		}

		public void SetStatusFilter(AugmentStatusFilter status)
		{
			currentStatusFilter = status;
			if (status == AugmentStatusFilter.Equipped)
			{
				currentRarityFilter = null;
				currentTab = "Equipped";
			}
			else if (status == AugmentStatusFilter.All)
			{
				currentTab = currentRarityFilter.HasValue ? currentRarityFilter.Value.ToString() : "All";
			}
			else
			{
				currentTab = "";
			}
			CloseActiveDropdown();
			UpdateTabButtons();
			UpdateFilterControlStates();
			PopulateGrid();
		}

		public void ResetAllFilters()
		{
			CloseActiveDropdown();
			searchBar?.Clear();
			searchQuery = "";
			currentClassFilter = null;
			currentRarityFilter = null;
			currentStatusFilter = AugmentStatusFilter.All;
			currentSortMode = AugmentSortMode.NameAZ;
			currentTab = "All";
			UpdateTabButtons();
			UpdateFilterControlStates();
			PopulateGrid();
		}

		public void ToggleFilterMenu()
		{
			CloseActiveDropdown();
			isFilterMenuOpen = !isFilterMenuOpen;
			if (isFilterMenuOpen)
			{
				if (!backPanel.HasChild(filterPanel))
					backPanel.Append(filterPanel);
				filterPanel.SyncUI();
			}
			else
			{
				if (backPanel.HasChild(filterPanel))
					backPanel.RemoveChild(filterPanel);
			}
			UpdateFilterControlStates();
		}

		private void OpenDropdown(CodexDropdown dropdown, UIElement trigger)
		{
			if (isFilterMenuOpen)
			{
				if (backPanel.HasChild(filterPanel))
					backPanel.RemoveChild(filterPanel);
				isFilterMenuOpen = false;
			}

			if (dropdownMenu != null && dropdownMenu.IsOpen)
			{
				if (backPanel.HasChild(dropdownMenu))
					backPanel.RemoveChild(dropdownMenu);
			}

			activeDropdownTrigger = trigger;
			if (!backPanel.HasChild(dropdown))
				backPanel.Append(dropdown);

			UpdateFilterControlStates();
		}

		private void CloseActiveDropdown()
		{
			if (dropdownMenu != null && dropdownMenu.IsOpen)
			{
				if (backPanel.HasChild(dropdownMenu))
					backPanel.RemoveChild(dropdownMenu);
				activeDropdownTrigger = null;
				UpdateFilterControlStates();
			}
		}

		private void ToggleClassDropdown()
		{
			if (dropdownMenu != null && dropdownMenu.IsOpen && activeDropdownTrigger == classBtn)
			{
				CloseActiveDropdown();
				return;
			}

			var rows = new List<CodexDropdownRow>
			{
				new CodexDropdownRow(dropdownMenu, "All Classes", () => SetClassFilter(null), textColor: new Color(220, 230, 245), drawDiamondIcon: true, isSelected: currentClassFilter == null),
				new CodexDropdownRow(dropdownMenu, "Universal", () => SetClassFilter(AugmentClass.Universal), textColor: AugmentTooltipDrawer.GetClassColor(AugmentClass.Universal), icon: AugmentSlotElement.GetClassIcon(AugmentClass.Universal), isSelected: currentClassFilter == AugmentClass.Universal),
				new CodexDropdownRow(dropdownMenu, "Melee", () => SetClassFilter(AugmentClass.Melee), textColor: AugmentTooltipDrawer.GetClassColor(AugmentClass.Melee), icon: AugmentSlotElement.GetClassIcon(AugmentClass.Melee), isSelected: currentClassFilter == AugmentClass.Melee),
				new CodexDropdownRow(dropdownMenu, "Ranged", () => SetClassFilter(AugmentClass.Ranged), textColor: AugmentTooltipDrawer.GetClassColor(AugmentClass.Ranged), icon: AugmentSlotElement.GetClassIcon(AugmentClass.Ranged), isSelected: currentClassFilter == AugmentClass.Ranged),
				new CodexDropdownRow(dropdownMenu, "Magic", () => SetClassFilter(AugmentClass.Magic), textColor: AugmentTooltipDrawer.GetClassColor(AugmentClass.Magic), icon: AugmentSlotElement.GetClassIcon(AugmentClass.Magic), isSelected: currentClassFilter == AugmentClass.Magic),
				new CodexDropdownRow(dropdownMenu, "Summon", () => SetClassFilter(AugmentClass.Summon), textColor: AugmentTooltipDrawer.GetClassColor(AugmentClass.Summon), icon: AugmentSlotElement.GetClassIcon(AugmentClass.Summon), isSelected: currentClassFilter == AugmentClass.Summon),
				new CodexDropdownRow(dropdownMenu, "Support", () => SetClassFilter(AugmentClass.Support), textColor: AugmentTooltipDrawer.GetClassColor(AugmentClass.Support), icon: AugmentSlotElement.GetClassIcon(AugmentClass.Support), isSelected: currentClassFilter == AugmentClass.Support)
			};

			dropdownMenu.Populate(rows, 118f, 556f, 65f);
			OpenDropdown(dropdownMenu, classBtn);
		}

		private void ToggleSortDropdown()
		{
			if (dropdownMenu != null && dropdownMenu.IsOpen && activeDropdownTrigger == sortBtn)
			{
				CloseActiveDropdown();
				return;
			}

			var rows = new List<CodexDropdownRow>
			{
				new CodexDropdownRow(dropdownMenu, "Name (A → Z)", () => SetSortMode(AugmentSortMode.NameAZ), isSelected: currentSortMode == AugmentSortMode.NameAZ),
				new CodexDropdownRow(dropdownMenu, "Name (Z → A)", () => SetSortMode(AugmentSortMode.NameZA), isSelected: currentSortMode == AugmentSortMode.NameZA),
				new CodexDropdownRow(dropdownMenu, "Rarity (High → Low)", () => SetSortMode(AugmentSortMode.RarityHighLow), isSelected: currentSortMode == AugmentSortMode.RarityHighLow),
				new CodexDropdownRow(dropdownMenu, "Rarity (Low → High)", () => SetSortMode(AugmentSortMode.RarityLowHigh), isSelected: currentSortMode == AugmentSortMode.RarityLowHigh),
				new CodexDropdownRow(dropdownMenu, "Class (Combat Role)", () => SetSortMode(AugmentSortMode.Class), isSelected: currentSortMode == AugmentSortMode.Class),
				new CodexDropdownRow(dropdownMenu, "Recently Added", () => SetSortMode(AugmentSortMode.Newest), textColor: new Color(250, 204, 21), isSelected: currentSortMode == AugmentSortMode.Newest),
				new CodexDropdownRow(dropdownMenu, "Oldest First", () => SetSortMode(AugmentSortMode.Oldest), isSelected: currentSortMode == AugmentSortMode.Oldest)
			};

			dropdownMenu.Populate(rows, 142f, 676f, 65f);
			OpenDropdown(dropdownMenu, sortBtn);
		}

		private void ToggleFilterDropdown()
		{
			if (dropdownMenu != null && dropdownMenu.IsOpen && activeDropdownTrigger == filterBtn)
			{
				CloseActiveDropdown();
				return;
			}

			var rows = new List<CodexDropdownRow>
			{
				new CodexDropdownRow(dropdownMenu, "All Plugins", () => SetStatusFilter(AugmentStatusFilter.All), isSelected: currentStatusFilter == AugmentStatusFilter.All && currentClassFilter == null && currentRarityFilter == null),
				new CodexDropdownRow(dropdownMenu, "Equipped Only", () => SetStatusFilter(AugmentStatusFilter.Equipped), textColor: new Color(130, 235, 130), isSelected: currentStatusFilter == AugmentStatusFilter.Equipped),
				new CodexDropdownRow(dropdownMenu, "Unequipped Only", () => SetStatusFilter(AugmentStatusFilter.Unequipped), isSelected: currentStatusFilter == AugmentStatusFilter.Unequipped),
				new CodexDropdownRow(dropdownMenu, "New Only (Recent)", () => SetStatusFilter(AugmentStatusFilter.NewOnly), textColor: new Color(250, 204, 21), isSelected: currentStatusFilter == AugmentStatusFilter.NewOnly),
				new CodexDropdownRow(true),
				new CodexDropdownRow(dropdownMenu, "All Filters Panel...", () => ToggleFilterMenu(), textColor: new Color(180, 195, 220)),
				new CodexDropdownRow(dropdownMenu, "Reset All Filters", () => ResetAllFilters(), textColor: new Color(248, 113, 113))
			};

			dropdownMenu.Populate(rows, 136f, 726f, 65f);
			OpenDropdown(dropdownMenu, filterBtn);
		}

		public void UpdateFilterControlStates()
		{
			classBtn?.SetText(GetClassBtnText());
			sortBtn?.SetText(GetSortBtnText());
			filterBtn?.SetText(GetFilterBtnText());

			bool isFiltered = currentClassFilter != null || currentRarityFilter != null || currentStatusFilter != AugmentStatusFilter.All;
			bool isFilterDropdownOpen = dropdownMenu != null && dropdownMenu.IsOpen && activeDropdownTrigger == filterBtn;
			if (filterBtn != null)
			{
				filterBtn.IsActiveHighlight = isFiltered || isFilterMenuOpen || isFilterDropdownOpen;
			}

			if (classBtn != null)
			{
				bool isClassDropdownOpen = dropdownMenu != null && dropdownMenu.IsOpen && activeDropdownTrigger == classBtn;
				classBtn.IsActiveHighlight = currentClassFilter != null || isClassDropdownOpen;
			}

			if (sortBtn != null)
			{
				bool isSortDropdownOpen = dropdownMenu != null && dropdownMenu.IsOpen && activeDropdownTrigger == sortBtn;
				sortBtn.IsActiveHighlight = currentSortMode != AugmentSortMode.NameAZ || isSortDropdownOpen;
			}

			if (filterPanel != null && isFilterMenuOpen)
			{
				filterPanel.SyncUI();
			}
		}

		private void UpdateTabButtons()
		{
			foreach (var btn in tabButtons)
			{
				if (currentStatusFilter == AugmentStatusFilter.Equipped)
				{
					btn.IsActive = (btn.TabName == "Equipped");
				}
				else if (currentRarityFilter.HasValue)
				{
					btn.IsActive = btn.TabName.Equals(currentRarityFilter.Value.ToString(), StringComparison.OrdinalIgnoreCase);
				}
				else if (currentStatusFilter == AugmentStatusFilter.All && !currentRarityFilter.HasValue)
				{
					btn.IsActive = (btn.TabName == "All");
				}
				else
				{
					btn.IsActive = false;
				}
			}
		}

		private string GetClassBtnText()
		{
			string arrow = (dropdownMenu != null && dropdownMenu.IsOpen && activeDropdownTrigger == classBtn) ? "▴" : "▾";
			if (currentClassFilter == null)
				return $"Class: All {arrow}";
			return $"Class: {currentClassFilter.Value} {arrow}";
		}

		private string GetSortBtnText()
		{
			string arrow = (dropdownMenu != null && dropdownMenu.IsOpen && activeDropdownTrigger == sortBtn) ? "▴" : "▾";
			return currentSortMode switch
			{
				AugmentSortMode.NameAZ => $"Sort: A → Z {arrow}",
				AugmentSortMode.NameZA => $"Sort: Z → A {arrow}",
				AugmentSortMode.RarityHighLow => $"Sort: Rarity ▼",
				AugmentSortMode.RarityLowHigh => $"Sort: Rarity ▲",
				AugmentSortMode.Class => $"Sort: Class {arrow}",
				AugmentSortMode.Newest => $"Sort: Newest {arrow}",
				AugmentSortMode.Oldest => $"Sort: Oldest {arrow}",
				_ => $"Sort: A → Z {arrow}"
			};
		}

		private string GetFilterBtnText()
		{
			string arrow = (isFilterMenuOpen || (dropdownMenu != null && dropdownMenu.IsOpen && activeDropdownTrigger == filterBtn)) ? "▴" : "▾";
			return $"Filters {arrow}";
		}

		private void SwitchTab(string newTab)
		{
			CloseActiveDropdown();
			currentTab = newTab;
			if (newTab == "All")
			{
				currentRarityFilter = null;
				currentStatusFilter = AugmentStatusFilter.All;
			}
			else if (newTab == "Equipped")
			{
				currentRarityFilter = null;
				currentStatusFilter = AugmentStatusFilter.Equipped;
			}
			else if (Enum.TryParse<AugmentRarity>(newTab, true, out var parsedRarity))
			{
				currentRarityFilter = parsedRarity;
				currentStatusFilter = AugmentStatusFilter.All;
			}

			UpdateTabButtons();
			UpdateFilterControlStates();
			PopulateGrid();
		}

		private void OnSearchInputChanged(string text)
		{
			CloseActiveDropdown();
			searchQuery = text;
			if (gridScrollbar != null)
				gridScrollbar.ViewPosition = 0f;
			PopulateGrid();
		}

		private static string CleanSearchText(string input)
		{
			if (string.IsNullOrEmpty(input))
				return string.Empty;
			return Regex.Replace(input, @"\[c/[^:]*:([^\]]*)\]", "$1");
		}

		public void Refresh()
		{
			if (gridList == null)
				return;

			if (searchBar != null)
				searchBar.IsFocused = false;

			UpdateDevModeVisuals();
			UpdateFilterControlStates();
			UpdateLoadoutButtons();
			PopulateGrid();

			if (selectedAugment == null)
			{
				var owned = Main.LocalPlayer.GetModPlayer<AugmentPlayer>().Owned;
				if (owned.Count > 0)
					SelectAugment(owned[0]);
				else if (AugmentDatabase.All.Count > 0)
					SelectAugment(AugmentDatabase.All[0]);
			}
			else
			{
				bool isOwned = Main.LocalPlayer.GetModPlayer<AugmentPlayer>().HasAugment(selectedAugment.Id);
				detailPanel.SetAugment(selectedAugment, isOwned);
			}
		}

		private void PopulateGrid()
		{
			float prevScroll = gridScrollbar?.ViewPosition ?? 0f;
			gridList.Clear();
			AugmentListEntry.HoveredAugment = null;

			var ap = Main.LocalPlayer.GetModPlayer<AugmentPlayer>();
			var all = AugmentDatabase.All;

			string query = (searchQuery ?? "").Trim();
			bool hasQuery = !string.IsNullOrEmpty(query);

			var filtered = new List<Augment>();
			foreach (var aug in all)
			{
				if (aug.Id == "debug_full_crit")
					continue;

				// Status filter (Equipped / Unequipped / NewOnly)
				if (currentStatusFilter == AugmentStatusFilter.Equipped && !ap.HasAugment(aug.Id))
					continue;
				if (currentStatusFilter == AugmentStatusFilter.Unequipped && ap.HasAugment(aug.Id))
					continue;
				if (currentStatusFilter == AugmentStatusFilter.NewOnly && !aug.IsRecent)
					continue;

				// Rarity filter
				if (currentRarityFilter.HasValue && aug.Rarity != currentRarityFilter.Value)
					continue;

				// Class filter
				if (currentClassFilter.HasValue && aug.Class != currentClassFilter.Value)
					continue;

				// Search query filter (matches DisplayName, clean Description, or #new / tag:new)
				if (hasQuery)
				{
					if (query.Equals("#new", StringComparison.OrdinalIgnoreCase) || query.Equals("tag:new", StringComparison.OrdinalIgnoreCase))
					{
						if (!aug.IsRecent)
							continue;
					}
					else
					{
						string cleanDesc = CleanSearchText(aug.Description);
						bool nameMatch = aug.DisplayName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
						bool descMatch = cleanDesc.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
						if (!nameMatch && !descMatch)
							continue;
					}
				}

				filtered.Add(aug);
			}

			// Sort (prioritize DisplayName matches when searching)
			filtered.Sort((a, b) =>
			{
				if (hasQuery)
				{
					bool aName = a.DisplayName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
					bool bName = b.DisplayName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
					if (aName != bName)
						return aName ? -1 : 1;
				}

				return currentSortMode switch
				{
					AugmentSortMode.NameAZ => string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase),
					AugmentSortMode.NameZA => string.Compare(b.DisplayName, a.DisplayName, StringComparison.OrdinalIgnoreCase),
					AugmentSortMode.RarityHighLow => ((int)b.Rarity).CompareTo((int)a.Rarity) != 0
						? ((int)b.Rarity).CompareTo((int)a.Rarity)
						: string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase),
					AugmentSortMode.RarityLowHigh => ((int)a.Rarity).CompareTo((int)b.Rarity) != 0
						? ((int)a.Rarity).CompareTo((int)b.Rarity)
						: string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase),
					AugmentSortMode.Class => a.Class.CompareTo(b.Class) != 0
						? a.Class.CompareTo(b.Class)
						: string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase),
					AugmentSortMode.Newest => b.RegistrationOrder.CompareTo(a.RegistrationOrder),
					AugmentSortMode.Oldest => a.RegistrationOrder.CompareTo(b.RegistrationOrder),
					_ => string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase)
				};
			});

			if (filtered.Count == 0)
			{
				string msg = hasQuery
					? $"No plugins found matching \"{query}\"."
					: "No plugins match the current filters.";
				var emptyText = new UIText(msg, 0.9f)
				{
					HAlign = 0.5f
				};
				emptyText.Top.Set(40f, 0f);
				gridList.Add(emptyText);
				return;
			}

			// Group into rows of SlotsPerRow
			var rows = new List<UIElement>();
			UIElement currentRow = null;
			int slotIndexInRow = 0;

			for (int i = 0; i < filtered.Count; i++)
			{
				if (slotIndexInRow == 0)
				{
					currentRow = new UIElement();
					currentRow.Width.Set(0f, 1f);
					currentRow.Height.Set(SlotHeight + 5f, 0f);
					rows.Add(currentRow);
				}

				Augment aug = filtered[i];
				var slot = new AugmentSlotElement(aug)
				{
					IsSelected = (selectedAugment != null && selectedAugment.Id == aug.Id),
					IsOwned = ap.HasAugment(aug.Id)
				};
				slot.Left.Set(slotIndexInRow * (SlotWidth + SlotSpacing), 0f);
				slot.Top.Set(0f, 0f);
				slot.Clicked += SelectAugment;
				slot.RightClicked += OnSlotRightClicked;

				currentRow.Append(slot);

				slotIndexInRow++;
				if (slotIndexInRow >= SlotsPerRow)
					slotIndexInRow = 0;
			}

			gridList.AddRange(rows);

			if (gridScrollbar != null && !hasQuery)
				gridScrollbar.ViewPosition = prevScroll;
		}

		private void SelectAugment(Augment augment)
		{
			selectedAugment = augment;
			bool isOwned = Main.LocalPlayer.GetModPlayer<AugmentPlayer>().HasAugment(augment.Id);
			detailPanel.SetAugment(augment, isOwned);

			// Refresh grid selection state
			PopulateGrid();
		}

		private void OnSlotRightClicked(Augment aug)
		{
			if (!IsDevMode)
				return;

			var ap = Main.LocalPlayer.GetModPlayer<AugmentPlayer>();
			bool isOwned = ap.HasAugment(aug.Id);
			var cmd = isOwned ? DebugAugmentCommandType.Remove : DebugAugmentCommandType.Add;

			if (Main.netMode == NetmodeID.MultiplayerClient)
				AugmentNet.SendDebugCommandRequest(cmd, aug.Id);
			else
				AugmentNet.ApplyDebugCommand(Main.LocalPlayer, cmd, aug.Id);

			SoundEngine.PlaySound(isOwned ? SoundID.MenuClose : SoundID.Item4);
			SelectAugment(aug);
		}

		// Tab button for tier category filters
		private class CodexTabButton : UIElement
		{
			public readonly string TabName;
			public bool IsActive { get; set; }
			public event Action<string> Clicked;

			private bool isHovered;
			private readonly UIText label;

			public CodexTabButton(string tabName)
			{
				TabName = tabName;

				label = new UIText(tabName, 0.75f)
				{
					HAlign = 0.5f,
					VAlign = 0.5f
				};
				Append(label);
			}

			public override void MouseOver(UIMouseEvent evt)
			{
				base.MouseOver(evt);
				isHovered = true;
				SoundEngine.PlaySound(SoundID.MenuTick);
			}

			public override void MouseOut(UIMouseEvent evt)
			{
				base.MouseOut(evt);
				isHovered = false;
			}

			public override void LeftClick(UIMouseEvent evt)
			{
				base.LeftClick(evt);
				SoundEngine.PlaySound(SoundID.MenuTick);
				Clicked?.Invoke(TabName);
			}

			protected override void DrawSelf(SpriteBatch spriteBatch)
			{
				CalculatedStyle dims = GetDimensions();
				var rect = new Rectangle((int)dims.X, (int)dims.Y, (int)dims.Width, (int)dims.Height);
				Texture2D pixel = TextureAssets.MagicPixel.Value;

				Color bg;
				Color border;
				if (IsActive)
				{
					bg = isHovered ? new Color(18, 30, 56) * 0.98f : new Color(14, 24, 46) * 0.96f;
					border = new Color(56, 189, 248);
					label.TextColor = Color.White;
				}
				else if (isHovered)
				{
					bg = new Color(14, 22, 38) * 0.95f;
					border = new Color(56, 189, 248) * 0.75f;
					label.TextColor = new Color(240, 245, 255);
				}
				else
				{
					bg = new Color(10, 16, 28) * 0.92f;
					border = new Color(30, 42, 66);
					label.TextColor = new Color(160, 175, 205);
				}

				// Ambient underglow when active/hovered
				if (IsActive || isHovered)
				{
					float underglowAlpha = IsActive ? (isHovered ? 0.12f : 0.08f) : 0.05f;
					spriteBatch.Draw(pixel, new Rectangle(rect.X - 1, rect.Y - 1, rect.Width + 2, rect.Height + 2), border * underglowAlpha);
				}

				// Chassis Fill
				spriteBatch.Draw(pixel, rect, bg);

				// 1px Inner Hairline Highlight Accent
				Color innerHairline = Color.White * (isHovered ? 0.08f : 0.04f);
				spriteBatch.Draw(pixel, new Rectangle(rect.X + 1, rect.Y + 1, rect.Width - 2, 1), innerHairline);
				spriteBatch.Draw(pixel, new Rectangle(rect.X + 1, rect.Bottom - 2, rect.Width - 2, 1), innerHairline);
				spriteBatch.Draw(pixel, new Rectangle(rect.X + 1, rect.Y + 1, 1, rect.Height - 2), innerHairline);
				spriteBatch.Draw(pixel, new Rectangle(rect.Right - 2, rect.Y + 1, 1, rect.Height - 2), innerHairline);

				// 1px Outer Border
				spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 1), border);
				spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), border);
				spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, 1, rect.Height), border);
				spriteBatch.Draw(pixel, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), border);

				// Bottom active bar indicator (2px high-tech cyan runner when active)
				if (IsActive)
				{
					spriteBatch.Draw(pixel, new Rectangle(rect.X + 2, rect.Bottom - 2, rect.Width - 4, 2), new Color(56, 189, 248));
				}

				// Corner 2px accent ticks
				Color cornerTick = border * (isHovered ? 1.0f : 0.70f);
				spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, 2, 2), cornerTick);
				spriteBatch.Draw(pixel, new Rectangle(rect.Right - 2, rect.Y, 2, 2), cornerTick);
				spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 2, 2, 2), cornerTick);
				spriteBatch.Draw(pixel, new Rectangle(rect.Right - 2, rect.Bottom - 2, 2, 2), cornerTick);
			}
		}

		// Right-hand Detail Inspector Panel
		private class AugmentDetailPanel : UIPanel
		{
			private static readonly Regex CooldownRegex = new Regex(
				@"(?:[\s,]+(?:followed\s+by\s+a\s+)?)?(?:\[c\/[0-9a-fA-F]{6}:)?([0-9]+\s*(?:s|sec|seconds?|minutes?|min|hr|hours?)\s+cooldown)(?:\])?(\s+per\s+player)?\.?",
				RegexOptions.Compiled | RegexOptions.IgnoreCase
			);

			private static void ExtractDescriptionAndCooldown(Augment augment, out string effectText, out string cooldownText, bool detailed = false, Player player = null)
			{
				effectText = (detailed && player != null ? augment.GetDetailedDescription(player) : augment.LiveDescription) ?? "";
				cooldownText = augment.CooldownText;

				var match = CooldownRegex.Match(effectText);
				if (match.Success)
				{
					if (string.IsNullOrEmpty(cooldownText))
					{
						cooldownText = match.Groups[1].Value;
						if (match.Groups[2].Success && !string.IsNullOrWhiteSpace(match.Groups[2].Value))
						{
							cooldownText += " " + match.Groups[2].Value.Trim().TrimEnd('.');
						}
					}
					effectText = CooldownRegex.Replace(effectText, "").TrimEnd();
					if (effectText.EndsWith(","))
						effectText = effectText.Substring(0, effectText.Length - 1).TrimEnd();
					if (!string.IsNullOrEmpty(effectText) && !effectText.EndsWith("."))
						effectText += ".";
				}

				if (!string.IsNullOrEmpty(cooldownText))
				{
					cooldownText = cooldownText.Trim().TrimEnd('.');
				}
			}

			private readonly AugmentListUIState parentState;
			private Augment currentAugment;
			private bool currentIsOwned;
			private CodexFilterButton devEquipBtn;

			// Where the clickable "[key]" in the description was drawn this frame (screen space).
			private Rectangle keyLabelRect;
			private bool hasKeyLabel;

			// Scrolling support for long/detailed descriptions
			private float descScroll = 0f;
			private Augment lastScrolledAugment;
			private bool lastScrolledCtrl;

			public override void ScrollWheel(UIScrollWheelEvent evt)
			{
				base.ScrollWheel(evt);
				descScroll -= Math.Sign(evt.ScrollWheelValue) * 22f;
			}

			public AugmentDetailPanel(AugmentListUIState parentState)
			{
				this.parentState = parentState;
				SetPadding(10f);
				BackgroundColor = Color.Transparent;
				BorderColor = Color.Transparent;

				devEquipBtn = new CodexFilterButton("+ Install", 0.72f);
				devEquipBtn.Width.Set(95f, 0f);
				devEquipBtn.Height.Set(20f, 0f);
				devEquipBtn.Left.Set(-9999f, 0f);
				devEquipBtn.Top.Set(48f, 0f);
				devEquipBtn.Clicked += OnDevEquipClicked;
				Append(devEquipBtn);
			}

			private void OnDevEquipClicked()
			{
				if (currentAugment == null || !IsDevMode)
					return;

				var ap = Main.LocalPlayer.GetModPlayer<AugmentPlayer>();
				bool isOwned = ap.HasAugment(currentAugment.Id);
				var cmd = isOwned ? DebugAugmentCommandType.Remove : DebugAugmentCommandType.Add;

				if (Main.netMode == NetmodeID.MultiplayerClient)
					AugmentNet.SendDebugCommandRequest(cmd, currentAugment.Id);
				else
					AugmentNet.ApplyDebugCommand(Main.LocalPlayer, cmd, currentAugment.Id);

				SoundEngine.PlaySound(isOwned ? SoundID.MenuClose : SoundID.Item4);
				currentIsOwned = !isOwned;
				UpdateDevEquipBtnText();
				parentState?.PopulateGrid();
			}

			public void SetAugment(Augment augment, bool isOwned)
			{
				if (currentAugment != augment)
					descScroll = 0f;
				currentAugment = augment;
				currentIsOwned = isOwned;
				UpdateDevEquipBtnText();
			}

			public void UpdateDevEquipBtnText()
			{
				if (!IsDevMode || currentAugment == null)
				{
					devEquipBtn.Left.Set(-9999f, 0f);
				}
				else
				{
					devEquipBtn.Left.Set(176f, 0f);
					devEquipBtn.Top.Set(48f, 0f);
					devEquipBtn.SetText(currentIsOwned ? "- Uninstall" : "+ Install");
					devEquipBtn.BorderColor = currentIsOwned ? new Color(255, 100, 100) : new Color(100, 255, 140);
					devEquipBtn.SetTextColor(currentIsOwned ? new Color(255, 130, 130) : new Color(130, 255, 160));
				}
			}

			public override void LeftClick(UIMouseEvent evt)
			{
				base.LeftClick(evt);

				if (currentAugment?.ActiveModKeybind == null)
					return;

				if (hasKeyLabel && keyLabelRect.Contains(evt.MousePosition.ToPoint()))
				{
					if (KeybindEditor.IsListeningFor(currentAugment.ActiveModKeybind))
						KeybindEditor.Cancel();
					else
						KeybindEditor.Begin(currentAugment.ActiveModKeybind);
				}
				else if (KeybindEditor.IsListening)
				{
					KeybindEditor.Cancel();
				}
			}

			protected override void DrawSelf(SpriteBatch spriteBatch)
			{
				hasKeyLabel = false;
				KeybindEditor.HoldInput();

				CalculatedStyle dims = GetDimensions();
				var rect = new Rectangle((int)dims.X, (int)dims.Y, (int)dims.Width, (int)dims.Height);
				Texture2D pixel = TextureAssets.MagicPixel.Value;

				float time = (float)Main.GlobalTimeWrappedHourly;
				float rarePulse = (float)Math.Sin(time * 2.5f + (rect.X + rect.Y) * 0.02f) * 0.5f + 0.5f;
				float epicPulse = (float)Math.Sin(time * 3f + (rect.X + rect.Y) * 0.02f) * 0.5f + 0.5f;
				float legPulse = (float)Math.Sin(time * 4f + (rect.X + rect.Y) * 0.02f) * 0.5f + 0.5f;

				Color rarityColor = currentAugment != null 
					? AugmentListEntry.RarityColor(currentAugment.Rarity) 
					: new Color(30, 42, 66);
				if (currentAugment?.Rarity == AugmentRarity.Common)
					rarityColor = new Color(225, 230, 240);

				// Outer Stepped Halos for Epic & Legendary (clean hairline falloff, no solid boxes)
				if (currentAugment?.Rarity == AugmentRarity.Epic)
				{
					Color epicGlow = new Color(185, 110, 255);
					float eGlowA = 0.12f + epicPulse * 0.08f;
					spriteBatch.Draw(pixel, new Rectangle(rect.X - 1, rect.Y - 1, rect.Width + 2, 1), epicGlow * eGlowA);
					spriteBatch.Draw(pixel, new Rectangle(rect.X - 1, rect.Bottom, rect.Width + 2, 1), epicGlow * eGlowA);
					spriteBatch.Draw(pixel, new Rectangle(rect.X - 1, rect.Y - 1, 1, rect.Height + 2), epicGlow * eGlowA);
					spriteBatch.Draw(pixel, new Rectangle(rect.Right, rect.Y - 1, 1, rect.Height + 2), epicGlow * eGlowA);

					spriteBatch.Draw(pixel, new Rectangle(rect.X - 2, rect.Y - 2, rect.Width + 4, 1), epicGlow * (eGlowA * 0.40f));
					spriteBatch.Draw(pixel, new Rectangle(rect.X - 2, rect.Bottom + 1, rect.Width + 4, 1), epicGlow * (eGlowA * 0.40f));
					spriteBatch.Draw(pixel, new Rectangle(rect.X - 2, rect.Y - 2, 1, rect.Height + 4), epicGlow * (eGlowA * 0.40f));
					spriteBatch.Draw(pixel, new Rectangle(rect.Right + 1, rect.Y - 2, 1, rect.Height + 4), epicGlow * (eGlowA * 0.40f));
				}
				else if (currentAugment?.Rarity == AugmentRarity.Legendary)
				{
					Color legGlow = new Color(255, 180, 45);
					float lGlowA = 0.14f + legPulse * 0.10f;
					spriteBatch.Draw(pixel, new Rectangle(rect.X - 1, rect.Y - 1, rect.Width + 2, 1), legGlow * lGlowA);
					spriteBatch.Draw(pixel, new Rectangle(rect.X - 1, rect.Bottom, rect.Width + 2, 1), legGlow * lGlowA);
					spriteBatch.Draw(pixel, new Rectangle(rect.X - 1, rect.Y - 1, 1, rect.Height + 2), legGlow * lGlowA);
					spriteBatch.Draw(pixel, new Rectangle(rect.Right, rect.Y - 1, 1, rect.Height + 2), legGlow * lGlowA);

					spriteBatch.Draw(pixel, new Rectangle(rect.X - 2, rect.Y - 2, rect.Width + 4, 1), legGlow * (lGlowA * 0.45f));
					spriteBatch.Draw(pixel, new Rectangle(rect.X - 2, rect.Bottom + 1, rect.Width + 4, 1), legGlow * (lGlowA * 0.45f));
					spriteBatch.Draw(pixel, new Rectangle(rect.X - 2, rect.Y - 2, 1, rect.Height + 4), legGlow * (lGlowA * 0.45f));
					spriteBatch.Draw(pixel, new Rectangle(rect.Right + 1, rect.Y - 2, 1, rect.Height + 4), legGlow * (lGlowA * 0.45f));
				}

				// Chassis Background (#0A101C at 94% opacity with subtle ambient underglow)
				float underglowA = currentAugment != null 
					? (currentAugment.Rarity == AugmentRarity.Legendary ? 0.05f : (currentAugment.Rarity == AugmentRarity.Epic ? 0.04f : 0.03f)) 
					: 0.02f;
				spriteBatch.Draw(pixel, rect, rarityColor * underglowA);
				spriteBatch.Draw(pixel, rect, new Color(10, 16, 28) * 0.94f);

				// 1px Inner Hairline Highlight Accent
				Color innerHairline = Color.White * 0.06f;
				spriteBatch.Draw(pixel, new Rectangle(rect.X + 1, rect.Y + 1, rect.Width - 2, 1), innerHairline);
				spriteBatch.Draw(pixel, new Rectangle(rect.X + 1, rect.Bottom - 2, rect.Width - 2, 1), innerHairline);
				spriteBatch.Draw(pixel, new Rectangle(rect.X + 1, rect.Y + 1, 1, rect.Height - 2), innerHairline);
				spriteBatch.Draw(pixel, new Rectangle(rect.Right - 2, rect.Y + 1, 1, rect.Height - 2), innerHairline);

				// Border Color
				Color borderColor;
				if (currentAugment != null)
				{
					if (currentAugment.Rarity == AugmentRarity.Rare)
						borderColor = Color.Lerp(new Color(75, 175, 250), new Color(145, 225, 255), rarePulse * 0.35f);
					else if (currentAugment.Rarity == AugmentRarity.Epic)
						borderColor = Color.Lerp(new Color(155, 115, 225), new Color(215, 180, 255), epicPulse * 0.45f);
					else if (currentAugment.Rarity == AugmentRarity.Legendary)
						borderColor = Color.Lerp(new Color(255, 160, 20), new Color(255, 210, 60), legPulse * 0.45f);
					else
						borderColor = new Color(225, 230, 240);
				}
				else
				{
					borderColor = new Color(30, 42, 66);
				}

				// 1px Outer Border
				Color finalBorderCol = borderColor * 0.70f;
				spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 1), finalBorderCol);
				spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), finalBorderCol);
				spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, 1, rect.Height), finalBorderCol);
				spriteBatch.Draw(pixel, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), finalBorderCol);

				// Flush corner accent notches (6x2 and 2x6, strictly inside panel bounds)
				const int cLen = 6;
				const int cThick = 2;
				Color cornerCol = borderColor * 0.90f;

				// Top-Left notch
				spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, cLen, cThick), cornerCol);
				spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, cThick, cLen), cornerCol);
				// Top-Right notch
				spriteBatch.Draw(pixel, new Rectangle(rect.Right - cLen, rect.Y, cLen, cThick), cornerCol);
				spriteBatch.Draw(pixel, new Rectangle(rect.Right - cThick, rect.Y, cThick, cLen), cornerCol);
				// Bottom-Left notch
				spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - cThick, cLen, cThick), cornerCol);
				spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - cLen, cThick, cLen), cornerCol);
				// Bottom-Right notch
				spriteBatch.Draw(pixel, new Rectangle(rect.Right - cLen, rect.Bottom - cThick, cLen, cThick), cornerCol);
				spriteBatch.Draw(pixel, new Rectangle(rect.Right - cThick, rect.Bottom - cLen, cThick, cLen), cornerCol);

				if (currentAugment == null)
				{
					var emptyFont = FontAssets.MouseText.Value;
					string prompt = "Select any plugin slot\nto view specifications.";
					Vector2 pSize = ChatManager.GetStringSize(emptyFont, prompt, new Vector2(0.82f));
					Vector2 pPos = new Vector2(dims.X + (dims.Width - pSize.X) * 0.5f, dims.Y + (dims.Height - pSize.Y) * 0.42f);
					ChatManager.DrawColorCodedStringWithShadow(spriteBatch, emptyFont, prompt, pPos, new Color(148, 163, 184) * 0.75f, 0f, Vector2.Zero, new Vector2(0.82f));
					return;
				}

				// Subtle Glass Sheen Sweep for Legendary tier
				if (currentAugment.Rarity == AugmentRarity.Legendary)
				{
					float sweepPeriod = 6.0f;
					float sweepProgress = (time * 0.75f + (rect.X + rect.Y) * 0.002f) % sweepPeriod;
					if (sweepProgress < 1.1f)
					{
						float t = sweepProgress / 1.1f;
						float sweepCenter = (rect.Width + rect.Height) * t;
						int beamWidth = 18;
						Color sheenColor = new Color(255, 230, 160);

						for (int py = 4; py < rect.Height - 4; py += 2)
						{
							int centerPx = (int)(sweepCenter - py);
							int startPx = Math.Max(4, centerPx - beamWidth / 2);
							int endPx = Math.Min(rect.Width - 4, centerPx + beamWidth / 2);
							if (endPx > startPx)
							{
								float dist = Math.Abs((startPx + endPx) * 0.5f - centerPx);
								float normDist = dist / (beamWidth * 0.5f);
								if (normDist < 1f)
								{
									float falloff = (float)Math.Cos(normDist * MathHelper.PiOver2);
									float beamA = falloff * 0.07f;
									if (beamA > 0.01f)
									{
										spriteBatch.Draw(pixel,
											new Rectangle(rect.X + startPx, rect.Y + py, endPx - startPx, 2),
											sheenColor * beamA);
									}
								}
							}
						}
					}
				}

				// Subtle occasional micro-glint sparkles on panel border
				if (currentAugment.Rarity == AugmentRarity.Legendary)
				{
					(int x, int y, float offset)[] spPoints = new (int, int, float)[]
					{
						(rect.Right - 3, rect.Y + 2, 0.0f),
						(rect.X + 2, rect.Bottom - 2, 2.5f)
					};

					foreach (var sp in spPoints)
					{
						float spPhase = (time * 0.85f + sp.offset + (rect.X * 0.015f)) % 5.0f;
						if (spPhase < 1.0f)
						{
							float prog = spPhase / 1.0f;
							float intensity = (float)Math.Sin(prog * MathHelper.Pi);
							AugmentSlotElement.DrawSubtleStarSparkle(spriteBatch, sp.x, sp.y, intensity, new Color(255, 225, 120));
						}
					}
				}
				else if (currentAugment.Rarity == AugmentRarity.Epic)
				{
					(int x, int y, float offset)[] spPoints = new (int, int, float)[]
					{
						(rect.Right - 3, rect.Y + 2, 0.0f),
						(rect.X + 2, rect.Bottom - 2, 2.5f)
					};

					foreach (var sp in spPoints)
					{
						float spPhase = (time * 0.85f + sp.offset + (rect.X * 0.015f)) % 5.0f;
						if (spPhase < 1.0f)
						{
							float prog = spPhase / 1.0f;
							float intensity = (float)Math.Sin(prog * MathHelper.Pi);
							AugmentSlotElement.DrawSubtleStarSparkle(spriteBatch, sp.x, sp.y, intensity, new Color(215, 160, 255));
						}
					}
				}

				var font = FontAssets.MouseText.Value;
				float x = dims.X + 10f;
				float y = dims.Y + 10f;
				float maxTextWidth = dims.Width - 20f;

				// 1. Augment Display Name & Icon
				Color nameColor = AugmentListEntry.RarityColor(currentAugment.Rarity);
				Texture2D classIcon = AugmentSlotElement.GetClassIcon(currentAugment.Class);
				if (classIcon != null)
				{
					Color iconColor = AugmentListEntry.RarityColor(currentAugment.Rarity);
					if (currentAugment.Rarity == AugmentRarity.Common)
						iconColor = new Color(225, 230, 240);

					spriteBatch.Draw(classIcon, new Vector2(x, y - 2f), iconColor);
					ChatManager.DrawColorCodedStringWithShadow(
						spriteBatch, font, currentAugment.DisplayName, new Vector2(x + classIcon.Width + 8f, y + 2f), nameColor, 0f, Vector2.Zero, new Vector2(0.95f)
					);
					y += Math.Max(classIcon.Height, ChatManager.GetStringSize(font, currentAugment.DisplayName, new Vector2(0.95f)).Y) + 4f;
				}
				else
				{
					ChatManager.DrawColorCodedStringWithShadow(
						spriteBatch, font, currentAugment.DisplayName, new Vector2(x, y), nameColor, 0f, Vector2.Zero, new Vector2(0.95f)
					);
					y += ChatManager.GetStringSize(font, currentAugment.DisplayName, new Vector2(0.95f)).Y + 3f;
				}

				// 2. Rarity Tier Stars & Class Name
				int starCount = currentAugment.Rarity switch
				{
					AugmentRarity.Common => 1,
					AugmentRarity.Rare => 2,
					AugmentRarity.Epic => 3,
					AugmentRarity.Legendary => 4,
					_ => 1
				};

				Color starColor = AugmentListEntry.RarityColor(currentAugment.Rarity);
				if (currentAugment.Rarity == AugmentRarity.Common)
					starColor = new Color(225, 230, 240);

				if (RarityStarAsset == null)
					RarityStarAsset = ModContent.Request<Texture2D>("Augments/UI/RarityStar", ReLogic.Content.AssetRequestMode.ImmediateLoad);

				if (RarityStarAsset?.IsLoaded == true)
				{
					Texture2D starTex = RarityStarAsset.Value;
					float starSpacing = 17f;

					for (int s = 0; s < starCount; s++)
					{
						float sx = x + s * starSpacing;
						Vector2 starPos = new Vector2(sx, y);

						Color drawColor = starColor;

						if (currentAugment.Rarity == AugmentRarity.Epic)
						{
							float sPulse = (float)Math.Sin(time * 3f + s * 0.4f) * 0.5f + 0.5f;
							drawColor = Color.Lerp(starColor, new Color(255, 215, 255), sPulse * 0.4f);

							// Subtle star-shaped halo using the star texture itself (no square boxes)
							Color halo = new Color(190, 130, 255) * (0.20f + sPulse * 0.25f);
							spriteBatch.Draw(starTex, starPos + new Vector2(-1f, 0f), halo);
							spriteBatch.Draw(starTex, starPos + new Vector2(1f, 0f), halo);
							spriteBatch.Draw(starTex, starPos + new Vector2(0f, -1f), halo);
							spriteBatch.Draw(starTex, starPos + new Vector2(0f, 1f), halo);
						}
						else if (currentAugment.Rarity == AugmentRarity.Legendary)
						{
							float sPulse = (float)Math.Sin(time * 4f + s * 0.5f) * 0.5f + 0.5f;
							drawColor = Color.Lerp(starColor, new Color(255, 255, 220), sPulse * 0.45f);

							// Subtle star-shaped halo using the star texture itself (no square boxes)
							Color halo = new Color(255, 180, 40) * (0.25f + sPulse * 0.30f);
							spriteBatch.Draw(starTex, starPos + new Vector2(-1f, 0f), halo);
							spriteBatch.Draw(starTex, starPos + new Vector2(1f, 0f), halo);
							spriteBatch.Draw(starTex, starPos + new Vector2(0f, -1f), halo);
							spriteBatch.Draw(starTex, starPos + new Vector2(0f, 1f), halo);

							if (sPulse > 0.88f)
							{
								AugmentSlotElement.DrawStarSparkle(spriteBatch, (int)(sx + starTex.Width * 0.5f), (int)(y + starTex.Height * 0.5f), (sPulse - 0.88f) / 0.12f);
							}
						}

						spriteBatch.Draw(starTex, starPos, drawColor);
					}

					float classX = x + starCount * starSpacing + 6f;
					Color classColor = new Color(200, 210, 230);
					ChatManager.DrawColorCodedStringWithShadow(
						spriteBatch, font, currentAugment.Class.ToString(), new Vector2(classX, y - 1f), classColor, 0f, Vector2.Zero, new Vector2(0.82f)
					);
					y += Math.Max(starTex.Height, ChatManager.GetStringSize(font, currentAugment.Class.ToString(), new Vector2(0.82f)).Y) + 6f;
				}
				else
				{
					string tierClassText = $"[{currentAugment.Rarity} Tier]  {currentAugment.Class}";
					ChatManager.DrawColorCodedStringWithShadow(
						spriteBatch, font, tierClassText, new Vector2(x, y), Color.LightGray, 0f, Vector2.Zero, new Vector2(0.78f)
					);
					y += ChatManager.GetStringSize(font, tierClassText, new Vector2(0.78f)).Y + 4f;
				}

				// 3. Ownership Status (Clean unboxed indicator)
				string statusText = currentIsOwned ? "✓ Installed" : "• Not Installed";
				Color statusColor = currentIsOwned ? AugmentTextColors.Healing : new Color(148, 163, 184);
				ChatManager.DrawColorCodedStringWithShadow(
					spriteBatch, font, statusText, new Vector2(x, y), statusColor, 0f, Vector2.Zero, new Vector2(0.80f)
				);

				if (currentAugment.IsRecent)
				{
					float statusWidth = ChatManager.GetStringSize(font, statusText, new Vector2(0.80f)).X;
					string recentTag = "★ RECENT (v1.3)";
					ChatManager.DrawColorCodedStringWithShadow(
						spriteBatch, font, recentTag, new Vector2(x + statusWidth + 14f, y), new Color(250, 204, 21), 0f, Vector2.Zero, new Vector2(0.80f)
					);
				}
				y += ChatManager.GetStringSize(font, statusText, new Vector2(0.80f)).Y + 8f;

				// 4. Subtle Divider Line (matches rarity tier)
				Color divColor = AugmentListEntry.RarityColor(currentAugment.Rarity) * 0.35f;
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle((int)x, (int)y, (int)maxTextWidth, 1), divColor);
				y += 8f;

				// 5. Description Header (Clean high-tech uppercase)
				bool isCtrlDown = Main.keyState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.LeftControl) ||
				                  Main.keyState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.RightControl);

				if (currentAugment != lastScrolledAugment || isCtrlDown != lastScrolledCtrl)
				{
					descScroll = 0f;
					lastScrolledAugment = currentAugment;
					lastScrolledCtrl = isCtrlDown;
				}

				string headerTitle = isCtrlDown ? "EFFECT (DETAILED)" : "EFFECT";
				ChatManager.DrawColorCodedStringWithShadow(
					spriteBatch, font, headerTitle, new Vector2(x, y), new Color(250, 204, 21), 0f, Vector2.Zero, new Vector2(0.72f)
				);
				y += ChatManager.GetStringSize(font, headerTitle, new Vector2(0.72f)).Y + 4f;

				// 6. Wrapped Description with chat colors (stripping inline cooldown clause)
				ExtractDescriptionAndCooldown(currentAugment, out string effectText, out string cooldownText, isCtrlDown, Main.LocalPlayer);

				float footerTop = !string.IsNullOrEmpty(cooldownText) ? (dims.Y + dims.Height - 64f) : (dims.Y + dims.Height - 36f);
				float descStartY = y;
				float availableHeight = Math.Max(30f, footerTop - descStartY);

				bool canRebind = currentAugment.ActiveModKeybind != null;
				float baseScaleVal = 0.82f;
				var descScale = new Vector2(baseScaleVal);
				var lines = AugmentColorText.Wrap(font, effectText, maxTextWidth, descScale);

				float totalContentHeight = 0f;
				foreach (var line in lines)
				{
					totalContentHeight += ChatManager.GetStringSize(font, line, descScale).Y + 2f;
				}
				if (canRebind && KeybindEditor.IsListeningFor(currentAugment.ActiveModKeybind))
				{
					totalContentHeight += 26f;
				}

				// If content overflows the available height, auto-fit scale down to 0.70f
				if (totalContentHeight > availableHeight)
				{
					float fitScaleVal = MathHelper.Clamp(baseScaleVal * (availableHeight / totalContentHeight), 0.70f, baseScaleVal);
					if (fitScaleVal < baseScaleVal)
					{
						descScale = new Vector2(fitScaleVal);
						lines = AugmentColorText.Wrap(font, effectText, maxTextWidth, descScale);
						totalContentHeight = 0f;
						foreach (var line in lines)
						{
							totalContentHeight += ChatManager.GetStringSize(font, line, descScale).Y + 2f;
						}
						if (canRebind && KeybindEditor.IsListeningFor(currentAugment.ActiveModKeybind))
						{
							totalContentHeight += 26f;
						}
					}
				}

				float maxScroll = Math.Max(0f, totalContentHeight - availableHeight);
				descScroll = MathHelper.Clamp(descScroll, 0f, maxScroll);

				float currentY = descStartY - descScroll;
				foreach (var line in lines)
				{
					float lineHeight = ChatManager.GetStringSize(font, line, descScale).Y;

					// Only draw line if it's within the visible viewport bounds
					if (currentY + lineHeight >= descStartY && currentY <= footerTop)
					{
						// The "[key]" inside the description is the rebind button: highlight it under the text.
						if (canRebind && !hasKeyLabel && KeybindEditor.TryFindLabel(line, out int labelStart, out int labelLength))
						{
							float labelX = x + ChatManager.GetStringSize(font, line.Substring(0, labelStart), descScale).X;
							float labelW = ChatManager.GetStringSize(font, line.Substring(labelStart, labelLength), descScale).X;

							int padX = 4;
							int pillX = (int)labelX - padX;
							int pillY = (int)currentY;
							int pillW = (int)labelW + padX * 2;
							int pillH = (int)(lineHeight * 0.82f);

							keyLabelRect = new Rectangle(pillX, pillY, pillW, pillH);
							hasKeyLabel = true;

							bool listening = KeybindEditor.IsListeningFor(currentAugment.ActiveModKeybind);
							bool hovered = keyLabelRect.Contains(Main.mouseX, Main.mouseY);
							float pulse = (float)Math.Sin(time * 8f) * 0.5f + 0.5f;
							Color accent = listening ? AugmentTextColors.Active : new Color(125, 211, 252);
							float fillAlpha = listening ? 0.16f + pulse * 0.16f : (hovered ? 0.18f : 0.08f);
							Color boxBorderColor = accent * (listening ? 0.65f + pulse * 0.35f : (hovered ? 0.80f : 0.40f));

							var pill = keyLabelRect;
							spriteBatch.Draw(pixel, pill, accent * fillAlpha);
							spriteBatch.Draw(pixel, new Rectangle(pill.X, pill.Y, pill.Width, 1), boxBorderColor);
							spriteBatch.Draw(pixel, new Rectangle(pill.X, pill.Bottom - 1, pill.Width, 1), boxBorderColor);
							spriteBatch.Draw(pixel, new Rectangle(pill.X, pill.Y, 1, pill.Height), boxBorderColor);
							spriteBatch.Draw(pixel, new Rectangle(pill.Right - 1, pill.Y, 1, pill.Height), boxBorderColor);

							if (hovered && !listening)
								Main.instance.MouseText("Click to change this key");
						}

						ChatManager.DrawColorCodedStringWithShadow(
							spriteBatch, font, line, new Vector2(x, currentY), Color.White * 0.95f, 0f, Vector2.Zero, descScale
						);
					}

					currentY += lineHeight + 2f;
				}

				// 7. While a key is being picked, say so (the key itself lives in the description above)
				if (canRebind && KeybindEditor.IsListeningFor(currentAugment.ActiveModKeybind))
				{
					currentY += 6f;
					if (currentY >= descStartY && currentY <= footerTop)
					{
						ChatManager.DrawColorCodedStringWithShadow(
							spriteBatch, font, "Press any key to bind it  •  Esc to cancel", new Vector2(x, currentY),
							AugmentTextColors.Active * 0.9f, 0f, Vector2.Zero, new Vector2(0.72f)
						);
					}
				}

				// Sleek high-tech mini scroll indicator if content overflows and can be scrolled
				if (maxScroll > 0f)
				{
					float scrollTrackX = dims.X + dims.Width - 8f;
					spriteBatch.Draw(pixel, new Rectangle((int)scrollTrackX, (int)descStartY, 2, (int)availableHeight), new Color(30, 42, 66) * 0.75f);

					float thumbH = Math.Max(14f, availableHeight * (availableHeight / totalContentHeight));
					float thumbY = descStartY + (availableHeight - thumbH) * (descScroll / maxScroll);
					spriteBatch.Draw(pixel, new Rectangle((int)scrollTrackX, (int)thumbY, 2, (int)thumbH), new Color(56, 189, 248) * 0.85f);
				}

				// 8. Cooldown footer displayed at bottom of card with clean telemetry layout
				if (!string.IsNullOrEmpty(cooldownText))
				{
					float cdY = dims.Y + dims.Height - 54f;
					// Subtle divider line
					spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle((int)x, (int)(cdY - 6f), (int)maxTextWidth, 1), new Color(56, 189, 248) * 0.35f);

					string cdDisplay = $"COOLDOWN  •  {cooldownText}";
					ChatManager.DrawColorCodedStringWithShadow(
						spriteBatch, font, cdDisplay, new Vector2(x + 2f, cdY), new Color(56, 189, 248), 0f, Vector2.Zero, new Vector2(0.78f)
					);
				}

				// 9. 50% alpha text under card preview: "Hold [Ctrl] for detailed view"
				float hintY = dims.Y + dims.Height - 28f;
				string hintText;
				if (maxScroll > 0f)
				{
					hintText = isCtrlDown
						? "Release [Ctrl]  •  Scroll with wheel"
						: "Hold [Ctrl]  •  Scroll with wheel";
				}
				else
				{
					hintText = isCtrlDown
						? "Release [Ctrl] for standard view"
						: "Hold [Ctrl] for detailed view";
				}

				Color hintColor = isCtrlDown ? new Color(250, 204, 21) * 0.75f : Color.White * 0.50f;

				// Guarantee hint text never clips outside the card borders
				float maxAllowedWidth = maxTextWidth - 12f;
				Vector2 rawHintSize = ChatManager.GetStringSize(font, hintText, Vector2.One);
				float hintScaleVal = Math.Min(0.66f, maxAllowedWidth / Math.Max(1f, rawHintSize.X));
				var hintScale = new Vector2(hintScaleVal);
				Vector2 hintSize = ChatManager.GetStringSize(font, hintText, hintScale);

				ChatManager.DrawColorCodedStringWithShadow(
					spriteBatch, font, hintText,
					new Vector2(x + (maxTextWidth - hintSize.X) * 0.5f, hintY),
					hintColor, 0f, Vector2.Zero, hintScale
				);
			}
		}

		// Secret Dev Badge displayed next to Title when Dev Mode is active
		private class DevBadgeElement : UIPanel
		{
			private readonly AugmentListUIState parent;
			private readonly UIText label;
			private bool isHovered;

			public DevBadgeElement(AugmentListUIState parent)
			{
				this.parent = parent;
				SetPadding(0f);
				Width.Set(52f, 0f);
				Height.Set(22f, 0f);

				label = new UIText("[DEV]", 0.72f)
				{
					HAlign = 0.5f,
					VAlign = 0.5f,
					TextColor = new Color(255, 215, 80)
				};
				Append(label);
			}

			public override void MouseOver(UIMouseEvent evt)
			{
				base.MouseOver(evt);
				if (IsDevMode)
				{
					isHovered = true;
					SoundEngine.PlaySound(SoundID.MenuTick);
				}
			}

			public override void MouseOut(UIMouseEvent evt)
			{
				base.MouseOut(evt);
				isHovered = false;
			}

			public override void LeftClick(UIMouseEvent evt)
			{
				base.LeftClick(evt);
				if (IsDevMode)
					parent.ToggleDevMode();
			}

			protected override void DrawSelf(SpriteBatch spriteBatch)
			{
				if (!IsDevMode)
					return;

				float time = (float)Main.GlobalTimeWrappedHourly;
				float pulse = (float)Math.Sin(time * 5f) * 0.5f + 0.5f;

				BackgroundColor = new Color(32, 20, 10) * (isHovered ? 0.98f : 0.88f);
				BorderColor = Color.Lerp(new Color(255, 160, 25), new Color(255, 235, 120), pulse);

				base.DrawSelf(spriteBatch);

				if (isHovered)
				{
					Main.instance.MouseText("Click to disable Dev Mode");
				}
			}

			protected override void DrawChildren(SpriteBatch spriteBatch)
			{
				if (IsDevMode)
					base.DrawChildren(spriteBatch);
			}
		}

		// Close button (cybernetic chassis)
		private class PluginsBackPanel : UIPanel
		{
			protected override void DrawSelf(SpriteBatch spriteBatch)
			{
				base.DrawSelf(spriteBatch);

				CalculatedStyle dims = GetDimensions();
				Texture2D pixel = TextureAssets.MagicPixel.Value;
				Rectangle bRect = dims.ToRectangle();

				PanelChrome.DrawFrame(spriteBatch, bRect);

				// Header divider between the tab row and the grid
				int divY = bRect.Y + 69;
				spriteBatch.Draw(pixel, new Rectangle(bRect.X + 18, divY, bRect.Width - 36, 1), new Color(30, 41, 59) * 0.90f);
				int midX = bRect.X + bRect.Width / 2;
				spriteBatch.Draw(pixel, new Rectangle(midX - 1, divY - 1, 3, 3), new Color(56, 189, 248) * 0.85f);
				spriteBatch.Draw(pixel, new Rectangle(midX, divY, 1, 1), Color.White * 0.9f);

				// Faint radar watermark
				Vector2 center = new Vector2(bRect.X + bRect.Width * 0.5f, bRect.Y + bRect.Height * 0.55f);
				Color watermarkCol = new Color(56, 189, 248) * 0.04f;
				foreach (float radius in new[] { 60f, 130f, 200f })
				{
					const int segments = 36;
					for (int i = 0; i < segments; i++)
					{
						float angle = MathHelper.TwoPi * i / segments;
						spriteBatch.Draw(pixel, new Rectangle((int)(center.X + Math.Cos(angle) * radius), (int)(center.Y + Math.Sin(angle) * radius), 2, 2), watermarkCol);
					}
				}
				spriteBatch.Draw(pixel, new Rectangle((int)center.X - 210, (int)center.Y, 420, 1), watermarkCol);
				spriteBatch.Draw(pixel, new Rectangle((int)center.X, (int)center.Y - 210, 1, 420), watermarkCol);
			}
		}

		// Search input bar with live text input, placeholder, clear button, and focus styling
		private class CodexSearchBar : UIPanel
		{
			public string Text { get; private set; } = "";
			public bool IsFocused { get; set; }
			public event Action<string> OnSearchChanged;

			private bool isHovered;
			private int _textBlinkerCount;
			private int _textBlinkerState;

			public CodexSearchBar()
			{
				SetPadding(0f);
				BackgroundColor = new Color(16, 22, 44) * 0.95f;
				BorderColor = new Color(45, 60, 105);
			}

			public void Clear()
			{
				if (!string.IsNullOrEmpty(Text))
				{
					Text = "";
					OnSearchChanged?.Invoke(Text);
				}
			}

			public override void Update(GameTime gameTime)
			{
				base.Update(gameTime);

				if (IsFocused)
				{
					PlayerInput.WritingText = true;
					Main.CurrentInputTextTakerOverride = this;
				}

				Vector2 mousePoint = new Vector2(Main.mouseX, Main.mouseY);
				if (IsFocused && !ContainsPoint(mousePoint) && Main.mouseLeft)
				{
					IsFocused = false;
				}
			}

			public override void LeftClick(UIMouseEvent evt)
			{
				base.LeftClick(evt);

				var dims = GetDimensions();
				if (!string.IsNullOrEmpty(Text) && evt.MousePosition.X >= dims.X + dims.Width - 24f)
				{
					Clear();
					SoundEngine.PlaySound(SoundID.MenuTick);
					return;
				}

				if (!IsFocused)
				{
					Main.clrInput();
					IsFocused = true;
					SoundEngine.PlaySound(SoundID.MenuTick);
				}
			}

			public override void RightClick(UIMouseEvent evt)
			{
				base.RightClick(evt);
				if (!string.IsNullOrEmpty(Text))
				{
					Clear();
					SoundEngine.PlaySound(SoundID.MenuTick);
				}
			}

			public override void MouseOver(UIMouseEvent evt)
			{
				base.MouseOver(evt);
				isHovered = true;
			}

			public override void MouseOut(UIMouseEvent evt)
			{
				base.MouseOut(evt);
				isHovered = false;
			}

			protected override void DrawSelf(SpriteBatch spriteBatch)
			{
				if (IsFocused)
				{
					PlayerInput.WritingText = true;
					Main.CurrentInputTextTakerOverride = this;
					Main.instance.HandleIME();

					string inputText = Main.GetInputText(Text);
					inputText = inputText.Replace("\r", "").Replace("\n", "");

					if (Main.inputTextEscape)
					{
						Main.inputTextEscape = false;
						IsFocused = false;
					}
					else if (Main.inputTextEnter)
					{
						Main.inputTextEnter = false;
						IsFocused = false;
					}

					if (!inputText.Equals(Text))
					{
						Text = inputText;
						OnSearchChanged?.Invoke(Text);
					}

					if (++_textBlinkerCount >= 20)
					{
						_textBlinkerState = (_textBlinkerState + 1) % 2;
						_textBlinkerCount = 0;
					}

				}

				CalculatedStyle dims = GetDimensions();
				var rect = new Rectangle((int)dims.X, (int)dims.Y, (int)dims.Width, (int)dims.Height);
				Texture2D pixel = TextureAssets.MagicPixel.Value;

				Color bg;
				Color border;
				if (IsFocused)
				{
					bg = new Color(14, 22, 38) * 0.98f;
					border = new Color(56, 189, 248);
				}
				else if (isHovered)
				{
					bg = new Color(12, 18, 32) * 0.96f;
					border = new Color(56, 189, 248) * 0.70f;
				}
				else
				{
					bg = new Color(10, 16, 28) * 0.94f;
					border = new Color(30, 42, 66);
				}

				// Ambient focus underglow
				if (IsFocused || isHovered)
				{
					float underglowAlpha = IsFocused ? 0.12f : 0.05f;
					spriteBatch.Draw(pixel, new Rectangle(rect.X - 1, rect.Y - 1, rect.Width + 2, rect.Height + 2), border * underglowAlpha);
				}

				// Chassis Fill
				spriteBatch.Draw(pixel, rect, bg);

				// 1px Inner Hairline Highlight Accent
				Color innerHairline = Color.White * (isHovered ? 0.08f : 0.04f);
				spriteBatch.Draw(pixel, new Rectangle(rect.X + 1, rect.Y + 1, rect.Width - 2, 1), innerHairline);
				spriteBatch.Draw(pixel, new Rectangle(rect.X + 1, rect.Bottom - 2, rect.Width - 2, 1), innerHairline);
				spriteBatch.Draw(pixel, new Rectangle(rect.X + 1, rect.Y + 1, 1, rect.Height - 2), innerHairline);
				spriteBatch.Draw(pixel, new Rectangle(rect.Right - 2, rect.Y + 1, 1, rect.Height - 2), innerHairline);

				// 1px Outer Border
				spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 1), border);
				spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), border);
				spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, 1, rect.Height), border);
				spriteBatch.Draw(pixel, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), border);

				// Corner 2px accent ticks
				Color cornerTick = border * (isHovered ? 1.0f : 0.70f);
				spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, 2, 2), cornerTick);
				spriteBatch.Draw(pixel, new Rectangle(rect.Right - 2, rect.Y, 2, 2), cornerTick);
				spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 2, 2, 2), cornerTick);
				spriteBatch.Draw(pixel, new Rectangle(rect.Right - 2, rect.Bottom - 2, 2, 2), cornerTick);

				Vector2 scale = new Vector2(0.72f);
				var font = FontAssets.MouseText.Value;

				// In Terraria's MouseText font, ChatManager.GetStringSize returns the full line-spacing
				// (including multi-line leading of ~28px). Adding +5f aligns the optical glyph body
				// to the exact vertical center of the 24px-tall search bar.
				float textYOffset = 5f;

				if (string.IsNullOrEmpty(Text))
				{
					if (IsFocused)
					{
						if (_textBlinkerState == 1)
						{
							Vector2 cursorSize = ChatManager.GetStringSize(font, "|", scale);
							float textX = dims.X + (dims.Width - cursorSize.X) / 2f;
							float textY = dims.Y + (dims.Height - cursorSize.Y) / 2f + textYOffset;
							ChatManager.DrawColorCodedStringWithShadow(
								spriteBatch,
								font,
								"|",
								new Vector2(textX, textY),
								new Color(110, 185, 255),
								0f,
								Vector2.Zero,
								scale
							);
						}
					}
					else
					{
						string placeholder = "Search plugins...";
						Vector2 textSize = ChatManager.GetStringSize(font, placeholder, scale);
						float textX = dims.X + (dims.Width - textSize.X) / 2f;
						float textY = dims.Y + (dims.Height - textSize.Y) / 2f + textYOffset;
						ChatManager.DrawColorCodedStringWithShadow(
							spriteBatch,
							font,
							placeholder,
							new Vector2(textX, textY),
							new Color(130, 148, 175) * 0.75f,
							0f,
							Vector2.Zero,
							scale
						);
					}
				}
				else
				{
					string display = Text;
					if (IsFocused && _textBlinkerState == 1)
					{
						display += "|";
					}

					float clearBtnReserve = 24f;
					float maxTextWidth = dims.Width - 16f - clearBtnReserve;
					while (display.Length > 0 && ChatManager.GetStringSize(font, display, scale).X > maxTextWidth)
					{
						display = display.Substring(1);
					}

					Vector2 textSize = ChatManager.GetStringSize(font, display, scale);
					float textX = dims.X + (dims.Width - clearBtnReserve - textSize.X) / 2f + 4f;
					if (textX < dims.X + 8f)
						textX = dims.X + 8f;
					float textY = dims.Y + (dims.Height - textSize.Y) / 2f + textYOffset;

					ChatManager.DrawColorCodedStringWithShadow(
						spriteBatch,
						font,
						display,
						new Vector2(textX, textY),
						Color.White,
						0f,
						Vector2.Zero,
						scale
					);

					// Draw Clear [✕] button
					bool clearHover = isHovered && Main.MouseScreen.X >= dims.X + dims.Width - 22f;
					Color clearColor = clearHover ? new Color(255, 110, 110) : new Color(160, 175, 205) * 0.8f;
					Vector2 xSize = ChatManager.GetStringSize(font, "✕", scale);
					float xX = dims.X + dims.Width - 18f;
					float xY = dims.Y + (dims.Height - xSize.Y) / 2f + textYOffset;

					ChatManager.DrawColorCodedStringWithShadow(
						spriteBatch,
						font,
						"✕",
						new Vector2(xX, xY),
						clearColor,
						0f,
						Vector2.Zero,
						scale
					);
				}

				if (isHovered && !string.IsNullOrEmpty(Text) && Main.MouseScreen.X >= dims.X + dims.Width - 22f)
				{
					Main.instance.MouseText("Clear search");
				}
			}
		}

		// Reusable interactive button/pill for filter bar and popup chips
		private class CodexFilterButton : UIElement
		{
			public readonly UIText Label;
			public event Action Clicked;
			public event Action RightClicked;
			private bool isHovered;
			public bool IsActiveHighlight { get; set; }
			public Color CustomActiveBg { get; set; } = new Color(18, 34, 62);
			public Color CustomActiveBorder { get; set; } = new Color(56, 189, 248);
			public Color BackgroundColor { get; set; } = new Color(10, 16, 28);
			public Color BorderColor { get; set; } = new Color(30, 42, 66);

			public CodexFilterButton(string initialText, float textScale = 0.72f)
			{
				Label = new UIText(initialText, textScale)
				{
					HAlign = 0.5f,
					VAlign = 0.5f
				};
				Append(Label);
			}

			public void SetText(string text)
			{
				Label.SetText(text);
			}

			public void SetTextColor(Color color)
			{
				Label.TextColor = color;
			}

			public override void MouseOver(UIMouseEvent evt)
			{
				base.MouseOver(evt);
				isHovered = true;
				SoundEngine.PlaySound(SoundID.MenuTick);
			}

			public override void MouseOut(UIMouseEvent evt)
			{
				base.MouseOut(evt);
				isHovered = false;
			}

			public override void LeftClick(UIMouseEvent evt)
			{
				base.LeftClick(evt);
				SoundEngine.PlaySound(SoundID.MenuTick);
				Clicked?.Invoke();
			}

			public override void RightClick(UIMouseEvent evt)
			{
				base.RightClick(evt);
				SoundEngine.PlaySound(SoundID.MenuTick);
				RightClicked?.Invoke();
			}

			protected override void DrawSelf(SpriteBatch spriteBatch)
			{
				CalculatedStyle dims = GetDimensions();
				var rect = new Rectangle((int)dims.X, (int)dims.Y, (int)dims.Width, (int)dims.Height);
				Texture2D pixel = TextureAssets.MagicPixel.Value;

				Color bg;
				Color border;
				if (IsActiveHighlight)
				{
					bg = isHovered ? Color.Lerp(CustomActiveBg, Color.White, 0.12f) : CustomActiveBg;
					border = CustomActiveBorder;
					Label.TextColor = Color.White;
				}
				else
				{
					bg = isHovered ? new Color(16, 24, 42) * 0.96f : (BackgroundColor != new Color(10, 16, 28) ? BackgroundColor : new Color(10, 16, 28) * 0.94f);
					border = isHovered ? new Color(56, 189, 248) * 0.85f : BorderColor;
					Label.TextColor = isHovered ? Color.White : new Color(220, 230, 245);
				}

				// Ambient hover underglow
				if (isHovered || IsActiveHighlight)
				{
					float underglowAlpha = isHovered ? 0.12f : 0.05f;
					spriteBatch.Draw(pixel, new Rectangle(rect.X - 1, rect.Y - 1, rect.Width + 2, rect.Height + 2), border * underglowAlpha);
				}

				// Chassis Fill
				spriteBatch.Draw(pixel, rect, bg);

				// 1px Inner Hairline Highlight Accent
				Color innerHairline = Color.White * (isHovered ? 0.08f : 0.04f);
				spriteBatch.Draw(pixel, new Rectangle(rect.X + 1, rect.Y + 1, rect.Width - 2, 1), innerHairline);
				spriteBatch.Draw(pixel, new Rectangle(rect.X + 1, rect.Bottom - 2, rect.Width - 2, 1), innerHairline);
				spriteBatch.Draw(pixel, new Rectangle(rect.X + 1, rect.Y + 1, 1, rect.Height - 2), innerHairline);
				spriteBatch.Draw(pixel, new Rectangle(rect.Right - 2, rect.Y + 1, 1, rect.Height - 2), innerHairline);

				// 1px Outer Border
				spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 1), border);
				spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), border);
				spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, 1, rect.Height), border);
				spriteBatch.Draw(pixel, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), border);

				// Corner 2px accent ticks
				Color cornerTick = border * (isHovered ? 1.0f : 0.70f);
				spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, 2, 2), cornerTick);
				spriteBatch.Draw(pixel, new Rectangle(rect.Right - 2, rect.Y, 2, 2), cornerTick);
				spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 2, 2, 2), cornerTick);
				spriteBatch.Draw(pixel, new Rectangle(rect.Right - 2, rect.Bottom - 2, 2, 2), cornerTick);
			}
		}

		// Sleek NieR-themed dropdown menu and items for filter/sort bar
		private class CodexDropdownRow : UIElement
		{
			private readonly CodexDropdown parentDropdown;
			public readonly string Text;
			public readonly Color TextColor;
			public readonly Texture2D Icon;
			public readonly Color IconColor;
			public readonly bool DrawDiamondIcon;
			public readonly bool IsSelected;
			public readonly bool IsDivider;
			public readonly Action OnClick;
			public bool HasIconSlot { get; set; }
			private bool isHovered;

			public CodexDropdownRow(CodexDropdown parent, string text, Action onClick, Color? textColor = null, Texture2D icon = null, Color? iconColor = null, bool drawDiamondIcon = false, bool isSelected = false)
			{
				parentDropdown = parent;
				Text = text;
				OnClick = onClick;
				TextColor = textColor ?? new Color(220, 230, 245);
				Icon = icon;
				IconColor = iconColor ?? (textColor ?? Color.White);
				DrawDiamondIcon = drawDiamondIcon;
				IsSelected = isSelected;
				IsDivider = false;
			}

			public CodexDropdownRow(bool isDivider)
			{
				IsDivider = isDivider;
				Text = "";
			}

			public override void MouseOver(UIMouseEvent evt)
			{
				base.MouseOver(evt);
				if (!IsDivider)
				{
					isHovered = true;
					SoundEngine.PlaySound(SoundID.MenuTick);
				}
			}

			public override void MouseOut(UIMouseEvent evt)
			{
				base.MouseOut(evt);
				isHovered = false;
			}

			public override void LeftClick(UIMouseEvent evt)
			{
				base.LeftClick(evt);
				if (!IsDivider && OnClick != null)
				{
					SoundEngine.PlaySound(SoundID.MenuTick);
					var action = OnClick;
					parentDropdown?.Close();
					action.Invoke();
				}
			}

			protected override void DrawSelf(SpriteBatch spriteBatch)
			{
				CalculatedStyle dims = GetDimensions();
				Rectangle rect = dims.ToRectangle();
				Texture2D pixel = TextureAssets.MagicPixel.Value;

				if (IsDivider)
				{
					int divY = rect.Y + rect.Height / 2;
					spriteBatch.Draw(pixel, new Rectangle(rect.X + 4, divY, rect.Width - 8, 1), new Color(35, 48, 72) * 0.9f);
					return;
				}

				if (IsSelected)
				{
					Color selBg = isHovered ? new Color(24, 48, 86) : new Color(16, 34, 62);
					spriteBatch.Draw(pixel, rect, selBg);

					// 3px active indicator bar on left
					spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y + 2, 3, rect.Height - 4), new Color(56, 189, 248));
				}
				else if (isHovered)
				{
					spriteBatch.Draw(pixel, rect, new Color(20, 36, 64) * 0.9f);
					// 1px faint hover border
					spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y + 2, 2, rect.Height - 4), new Color(56, 189, 248) * 0.5f);
				}

				float textX;
				if (HasIconSlot)
				{
					if (Icon != null)
					{
						int iconSize = 14;
						int iconX = rect.X + 7;
						int iconY = rect.Y + (rect.Height - iconSize) / 2;
						spriteBatch.Draw(Icon, new Rectangle(iconX, iconY, iconSize, iconSize), IconColor);
					}
					else if (DrawDiamondIcon)
					{
						int iconCenterX = rect.X + 14;
						int iconCenterY = rect.Y + rect.Height / 2;
						DrawPixelDiamond(spriteBatch, pixel, iconCenterX, iconCenterY, TextColor, 5);
					}

					textX = rect.X + 26f;
				}
				else
				{
					textX = rect.X + 10f;
				}

				Color drawTextColor = isHovered ? Color.White : (IsSelected ? TextColor : TextColor * 0.9f);
				var font = FontAssets.MouseText.Value;
				Vector2 scale = new Vector2(0.72f);
				Vector2 textSize = ChatManager.GetStringSize(font, Text, scale);
				float textY = rect.Y + (rect.Height - textSize.Y) / 2f + 1.5f;

				ChatManager.DrawColorCodedStringWithShadow(
					spriteBatch,
					font,
					Text,
					new Vector2(textX, textY),
					drawTextColor,
					0f,
					Vector2.Zero,
					scale
				);
			}
		}

		private class CodexDropdown : UIElement
		{
			private readonly AugmentListUIState state;
			public bool IsOpen => Parent != null;

			public CodexDropdown(AugmentListUIState state)
			{
				this.state = state;
				SetPadding(0f);
			}

			public void Close()
			{
				state.CloseActiveDropdown();
			}

			public void Populate(IEnumerable<CodexDropdownRow> rows, float width, float left, float top)
			{
				RemoveAllChildren();
				bool anyHasIcon = false;
				foreach (var r in rows)
				{
					if (r.Icon != null || r.DrawDiamondIcon)
					{
						anyHasIcon = true;
						break;
					}
				}

				float curY = 4f;
				foreach (var row in rows)
				{
					row.HasIconSlot = anyHasIcon;
					float h = row.IsDivider ? 6f : 24f;
					row.Left.Set(2f, 0f);
					row.Top.Set(curY, 0f);
					row.Width.Set(width - 4f, 0f);
					row.Height.Set(h, 0f);
					Append(row);
					curY += h;
				}
				curY += 4f;

				Left.Set(left, 0f);
				Top.Set(top, 0f);
				Width.Set(width, 0f);
				Height.Set(curY, 0f);
				Recalculate();
			}

			protected override void DrawSelf(SpriteBatch spriteBatch)
			{
				CalculatedStyle dims = GetDimensions();
				Rectangle rect = dims.ToRectangle();
				Texture2D pixel = TextureAssets.MagicPixel.Value;

				// Drop shadow
				spriteBatch.Draw(pixel, new Rectangle(rect.X + 2, rect.Y + 2, rect.Width, rect.Height), Color.Black * 0.6f);

				// Dark panel chassis
				spriteBatch.Draw(pixel, rect, new Color(10, 16, 28, 252));

				// 1px Inner Hairline Highlight Accent
				Color innerHairline = Color.White * 0.05f;
				spriteBatch.Draw(pixel, new Rectangle(rect.X + 1, rect.Y + 1, rect.Width - 2, 1), innerHairline);
				spriteBatch.Draw(pixel, new Rectangle(rect.X + 1, rect.Bottom - 2, rect.Width - 2, 1), innerHairline);
				spriteBatch.Draw(pixel, new Rectangle(rect.X + 1, rect.Y + 1, 1, rect.Height - 2), innerHairline);
				spriteBatch.Draw(pixel, new Rectangle(rect.Right - 2, rect.Y + 1, 1, rect.Height - 2), innerHairline);

				// 1px Outer Border
				Color border = new Color(38, 56, 86);
				spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 1), border);
				spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), border);
				spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, 1, rect.Height), border);
				spriteBatch.Draw(pixel, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), border);
			}
		}

		private static void DrawPixelDiamond(SpriteBatch sb, Texture2D px, int cx, int cy, Color col, int size = 5)
		{
			int half = size / 2;
			for (int r = -half; r <= half; r++)
			{
				int w = (half - Math.Abs(r)) * 2 + 1;
				sb.Draw(px, new Rectangle(cx - w / 2, cy + r, w, 1), col);
			}
		}

		// Floating filter & sort modal panel
		private class AugmentFilterPanel : UIPanel
		{
			private readonly AugmentListUIState state;
			private readonly List<(AugmentClass? cls, CodexFilterButton btn)> classButtons = new();
			private readonly List<(AugmentRarity? rar, CodexFilterButton btn)> rarityButtons = new();
			private readonly List<(AugmentSortMode sort, CodexFilterButton btn)> sortButtons = new();
			private readonly List<(AugmentStatusFilter status, CodexFilterButton btn)> statusButtons = new();

			public AugmentFilterPanel(AugmentListUIState state)
			{
				this.state = state;
				SetPadding(8f);
				BackgroundColor = Color.Transparent;
				BorderColor = Color.Transparent;

				// Title
				UIText title = new UIText("Filters & Sorting", 0.85f)
				{
					TextColor = new Color(250, 204, 21),
					Top = { Pixels = 4f },
					Left = { Pixels = 6f }
				};
				Append(title);

				// Close button [X]
				var closeBtn = new PanelCloseButton();
				closeBtn.Width.Set(20f, 0f);
				closeBtn.Height.Set(20f, 0f);
				closeBtn.HAlign = 1f;
				closeBtn.Top.Set(2f, 0f);
				closeBtn.Left.Set(-2f, 0f);
				closeBtn.Clicked += state.ToggleFilterMenu;
				Append(closeBtn);

				float curY = 28f;

				// 1. CLASS FILTER
				AddSectionLabel("CLASS FILTER", curY);
				curY += 16f;

				(AugmentClass? cls, string name, float w)[] classDefsRow1 = {
					(null, "All", 68f),
					(AugmentClass.Universal, "Universal", 76f),
					(AugmentClass.Melee, "Melee", 72f),
					(AugmentClass.Ranged, "Ranged", 74f)
				};
				float rowX = 4f;
				foreach (var def in classDefsRow1)
				{
					var btn = CreatePill(def.name, def.w, 22f, rowX, curY);
					btn.Clicked += () => state.SetClassFilter(def.cls);
					classButtons.Add((def.cls, btn));
					rowX += def.w + 4f;
				}
				curY += 25f;

				(AugmentClass? cls, string name, float w)[] classDefsRow2 = {
					(AugmentClass.Magic, "Magic", 94f),
					(AugmentClass.Summon, "Summon", 98f),
					(AugmentClass.Support, "Support", 98f)
				};
				rowX = 4f;
				foreach (var def in classDefsRow2)
				{
					var btn = CreatePill(def.name, def.w, 22f, rowX, curY);
					btn.Clicked += () => state.SetClassFilter(def.cls);
					classButtons.Add((def.cls, btn));
					rowX += def.w + 6f;
				}
				curY += 30f;

				// 2. RARITY TIER
				AddSectionLabel("RARITY TIER", curY);
				curY += 16f;

				(AugmentRarity? rar, string name, float w)[] rarityDefs = {
					(null, "All", 52f),
					(AugmentRarity.Common, "Common", 64f),
					(AugmentRarity.Rare, "Rare", 54f),
					(AugmentRarity.Epic, "Epic", 54f),
					(AugmentRarity.Legendary, "Legendary", 70f)
				};
				rowX = 4f;
				foreach (var def in rarityDefs)
				{
					var btn = CreatePill(def.name, def.w, 22f, rowX, curY);
					if (def.rar.HasValue)
					{
						Color rColor = AugmentListEntry.RarityColor(def.rar.Value);
						if (def.rar.Value == AugmentRarity.Common) rColor = new Color(225, 230, 240);
						btn.CustomActiveBorder = rColor;
					}
					btn.Clicked += () => state.SetRarityFilter(def.rar);
					rarityButtons.Add((def.rar, btn));
					rowX += def.w + 2f;
				}
				curY += 30f;

				// 3. SORT ORDER
				AddSectionLabel("SORT BY", curY);
				curY += 16f;

				(AugmentSortMode sort, string name, float w)[] sortDefsRow1 = {
					(AugmentSortMode.NameAZ, "A → Z", 96f),
					(AugmentSortMode.NameZA, "Z → A", 96f),
					(AugmentSortMode.Class, "Class", 98f)
				};
				rowX = 4f;
				foreach (var def in sortDefsRow1)
				{
					var btn = CreatePill(def.name, def.w, 22f, rowX, curY);
					btn.Clicked += () => state.SetSortMode(def.sort);
					sortButtons.Add((def.sort, btn));
					rowX += def.w + 6f;
				}
				curY += 25f;

				(AugmentSortMode sort, string name, float w)[] sortDefsRow2 = {
					(AugmentSortMode.RarityHighLow, "Rarity ▼ (High)", 146f),
					(AugmentSortMode.RarityLowHigh, "Rarity ▲ (Low)", 146f)
				};
				rowX = 4f;
				foreach (var def in sortDefsRow2)
				{
					var btn = CreatePill(def.name, def.w, 22f, rowX, curY);
					btn.Clicked += () => state.SetSortMode(def.sort);
					sortButtons.Add((def.sort, btn));
					rowX += def.w + 10f;
				}
				curY += 25f;

				(AugmentSortMode sort, string name, float w)[] sortDefsRow3 = {
					(AugmentSortMode.Newest, "★ Newest First", 146f),
					(AugmentSortMode.Oldest, "Oldest / Release", 146f)
				};
				rowX = 4f;
				foreach (var def in sortDefsRow3)
				{
					var btn = CreatePill(def.name, def.w, 22f, rowX, curY);
					btn.Clicked += () => state.SetSortMode(def.sort);
					sortButtons.Add((def.sort, btn));
					rowX += def.w + 10f;
				}
				curY += 30f;

				// 4. STATUS
				AddSectionLabel("STATUS", curY);
				curY += 16f;

				(AugmentStatusFilter st, string name, float w)[] statusDefs = {
					(AugmentStatusFilter.All, "All", 60f),
					(AugmentStatusFilter.Equipped, "Equipped", 74f),
					(AugmentStatusFilter.Unequipped, "Unequipped", 80f),
					(AugmentStatusFilter.NewOnly, "★ New Only", 88f)
				};
				rowX = 4f;
				foreach (var def in statusDefs)
				{
					var btn = CreatePill(def.name, def.w, 22f, rowX, curY);
					btn.Clicked += () => state.SetStatusFilter(def.st);
					statusButtons.Add((def.st, btn));
					rowX += def.w + 4f;
				}
				curY += 32f;

				// Bottom Action Buttons
				var resetBtn = new CodexFilterButton("Reset All", 0.75f);
				resetBtn.Width.Set(146f, 0f);
				resetBtn.Height.Set(24f, 0f);
				resetBtn.Left.Set(4f, 0f);
				resetBtn.Top.Set(curY, 0f);
				resetBtn.BackgroundColor = new Color(60, 25, 35);
				resetBtn.BorderColor = new Color(180, 70, 80);
				resetBtn.Clicked += state.ResetAllFilters;
				Append(resetBtn);

				var closeApplyBtn = new CodexFilterButton("Close / Apply", 0.75f);
				closeApplyBtn.Width.Set(146f, 0f);
				closeApplyBtn.Height.Set(24f, 0f);
				closeApplyBtn.Left.Set(156f, 0f);
				closeApplyBtn.Top.Set(curY, 0f);
				closeApplyBtn.BackgroundColor = new Color(25, 60, 40);
				closeApplyBtn.BorderColor = new Color(60, 160, 90);
				closeApplyBtn.Clicked += state.ToggleFilterMenu;
				Append(closeApplyBtn);
			}

			private void AddSectionLabel(string text, float y)
			{
				UIText label = new UIText(text, 0.72f)
				{
					TextColor = new Color(148, 163, 184),
					Top = { Pixels = y },
					Left = { Pixels = 4f }
				};
				Append(label);
			}

			private CodexFilterButton CreatePill(string text, float width, float height, float x, float y)
			{
				var btn = new CodexFilterButton(text, 0.70f);
				btn.Width.Set(width, 0f);
				btn.Height.Set(height, 0f);
				btn.Left.Set(x, 0f);
				btn.Top.Set(y, 0f);
				Append(btn);
				return btn;
			}

			public void SyncUI()
			{
				foreach (var (cls, btn) in classButtons)
				{
					btn.IsActiveHighlight = (cls == state.currentClassFilter);
				}

				foreach (var (rar, btn) in rarityButtons)
				{
					btn.IsActiveHighlight = (rar == state.currentRarityFilter);
				}

				foreach (var (sort, btn) in sortButtons)
				{
					btn.IsActiveHighlight = (sort == state.currentSortMode);
				}

				foreach (var (status, btn) in statusButtons)
				{
					btn.IsActiveHighlight = (status == state.currentStatusFilter);
				}
			}

			public override void LeftClick(UIMouseEvent evt)
			{
				base.LeftClick(evt);
			}

			protected override void DrawSelf(SpriteBatch spriteBatch)
			{
				CalculatedStyle dims = GetDimensions();
				var rect = new Rectangle((int)dims.X, (int)dims.Y, (int)dims.Width, (int)dims.Height);
				Texture2D pixel = TextureAssets.MagicPixel.Value;

				// Ambient drop shadow
				spriteBatch.Draw(pixel, new Rectangle(rect.X - 2, rect.Y - 2, rect.Width + 4, rect.Height + 4), new Color(0, 0, 0, 160));

				// Cybernetic chassis fill (#0A101C)
				spriteBatch.Draw(pixel, rect, new Color(10, 16, 28, 248));

				// 1px Outer border (#1E293B)
				Color outerBorder = new Color(30, 41, 59, 220);
				spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 1), outerBorder);
				spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), outerBorder);
				spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, 1, rect.Height), outerBorder);
				spriteBatch.Draw(pixel, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), outerBorder);

				// 1px Inner hairline highlight accent
				Rectangle innerRect = new Rectangle(rect.X + 1, rect.Y + 1, rect.Width - 2, rect.Height - 2);
				Color innerHairline = new Color(56, 189, 248, 35);
				spriteBatch.Draw(pixel, new Rectangle(innerRect.X, innerRect.Y, innerRect.Width, 1), innerHairline);
				spriteBatch.Draw(pixel, new Rectangle(innerRect.X, innerRect.Bottom - 1, innerRect.Width, 1), innerHairline);
				spriteBatch.Draw(pixel, new Rectangle(innerRect.X, innerRect.Y, 1, innerRect.Height), innerHairline);
				spriteBatch.Draw(pixel, new Rectangle(innerRect.Right - 1, innerRect.Y, 1, innerRect.Height), innerHairline);

				// Flush corner notches (5x2 / 2x5) in cyan (#38BDF8)
				Color cornerColor = new Color(56, 189, 248, 160);
				spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, 5, 2), cornerColor);
				spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, 2, 5), cornerColor);
				spriteBatch.Draw(pixel, new Rectangle(rect.Right - 5, rect.Y, 5, 2), cornerColor);
				spriteBatch.Draw(pixel, new Rectangle(rect.Right - 2, rect.Y, 2, 5), cornerColor);
				spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 2, 5, 2), cornerColor);
				spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 5, 2, 5), cornerColor);
				spriteBatch.Draw(pixel, new Rectangle(rect.Right - 5, rect.Bottom - 2, 5, 2), cornerColor);
				spriteBatch.Draw(pixel, new Rectangle(rect.Right - 2, rect.Bottom - 5, 2, 5), cornerColor);

				// Hairline divider rail below header
				spriteBatch.Draw(pixel, new Rectangle(rect.X + 6, rect.Y + 26, rect.Width - 12, 1), new Color(56, 189, 248, 40));
			}
		}
	}
}
