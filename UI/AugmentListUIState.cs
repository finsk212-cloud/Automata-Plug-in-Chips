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
		Newest,
		Oldest,
		NameAZ,
		NameZA,
		Class,
		RarityHighLow,
		RarityLowHigh
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
		private AugmentSortMode currentSortMode = DefaultSort;
		private bool isFilterMenuOpen = false;

		private CodexFilterButton classBtn;
		private CodexFilterButton sortBtn;
		private CodexFilterButton filterBtn;
		private AugmentFilterPanel filterPanel;
		private CodexDropdown dropdownMenu;
		private UIElement activeDropdownTrigger;

		// New plugins first: the list opens on what was added most recently.
		private const AugmentSortMode DefaultSort = AugmentSortMode.Newest;

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
			backPanel.BackgroundColor = PanelChrome.Tone(new Color(10, 16, 28, 250));
			backPanel.BorderColor = PanelChrome.Tone(new Color(30, 41, 59));

			// Title Header & Secret Dev Badge
			UIElement titleContainer = new UIElement();
			titleContainer.Width.Set(380f, 0f);
			titleContainer.Height.Set(28f, 0f);
			titleContainer.HAlign = 0.5f;
			titleContainer.Top.Set(8f, 0f);

			PluginsTitleElement title = new PluginsTitleElement();
			title.Width.Set(0f, 1f);
			title.Height.Set(0f, 1f);
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

		// Title-screen style glitch on opening and now and then (see PanelGlitch).
		private readonly PanelGlitch panelGlitch = new PanelGlitch(1);

		public override void Draw(SpriteBatch spriteBatch)
		{
			base.Draw(spriteBatch);
			if (backPanel != null)
				panelGlitch.Draw(spriteBatch, backPanel.GetDimensions().ToRectangle());
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
			sortBtn.Width.Set(108f, 0f);
			sortBtn.Height.Set(26f, 0f);
			sortBtn.Clicked += ToggleSortDropdown;
			sortBtn.RightClicked += () =>
			{
				CloseActiveDropdown();
				SetSortMode(DefaultSort);
			};
			backPanel.Append(sortBtn);

			// 3. Filter Button (Left = 796f, Width = 66f, ends at 862f)
			filterBtn = new CodexFilterButton(GetFilterBtnText());
			filterBtn.Top.Set(38f, 0f);
			filterBtn.Left.Set(790f, 0f);
			filterBtn.Width.Set(72f, 0f);
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
			filterPanel.Width.Set(AugmentFilterPanel.PanelW, 0f);
			filterPanel.Height.Set(AugmentFilterPanel.PanelH, 0f);
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
				loadoutButtons[i].BorderColor = unlocked ? PanelChrome.Tone(new Color(30, 42, 66)) : new Color(120, 48, 56);
				loadoutButtons[i].BackgroundColor = unlocked ? PanelChrome.Tone(new Color(10, 16, 28)) : new Color(20, 14, 24);
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
			resetCdsBtn.CustomActiveBorder = PanelChrome.Tone(new Color(100, 220, 255));
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
			currentSortMode = DefaultSort;
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
				new CodexDropdownRow(dropdownMenu, "All Classes", () => SetClassFilter(null), textColor: PanelChrome.Tone(new Color(220, 230, 245)), drawDiamondIcon: true, isSelected: currentClassFilter == null),
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
				new CodexDropdownRow(dropdownMenu, "Recently Added", () => SetSortMode(AugmentSortMode.Newest), isSelected: currentSortMode == AugmentSortMode.Newest),
					new CodexDropdownRow(dropdownMenu, "Oldest First", () => SetSortMode(AugmentSortMode.Oldest), isSelected: currentSortMode == AugmentSortMode.Oldest),
					new CodexDropdownRow(dropdownMenu, "Name (A → Z)", () => SetSortMode(AugmentSortMode.NameAZ), isSelected: currentSortMode == AugmentSortMode.NameAZ),
					new CodexDropdownRow(dropdownMenu, "Name (Z → A)", () => SetSortMode(AugmentSortMode.NameZA), isSelected: currentSortMode == AugmentSortMode.NameZA),
					new CodexDropdownRow(dropdownMenu, "Class (Combat Role)", () => SetSortMode(AugmentSortMode.Class), isSelected: currentSortMode == AugmentSortMode.Class),
					new CodexDropdownRow(dropdownMenu, "Rarity (High → Low)", () => SetSortMode(AugmentSortMode.RarityHighLow), isSelected: currentSortMode == AugmentSortMode.RarityHighLow),
					new CodexDropdownRow(dropdownMenu, "Rarity (Low → High)", () => SetSortMode(AugmentSortMode.RarityLowHigh), isSelected: currentSortMode == AugmentSortMode.RarityLowHigh)
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
				new CodexDropdownRow(dropdownMenu, "Equipped Only", () => SetStatusFilter(AugmentStatusFilter.Equipped), isSelected: currentStatusFilter == AugmentStatusFilter.Equipped),
				new CodexDropdownRow(dropdownMenu, "Unequipped Only", () => SetStatusFilter(AugmentStatusFilter.Unequipped), isSelected: currentStatusFilter == AugmentStatusFilter.Unequipped),
				new CodexDropdownRow(dropdownMenu, "New Only (Recent)", () => SetStatusFilter(AugmentStatusFilter.NewOnly), textColor: MenuLook.Current.Amber, isSelected: currentStatusFilter == AugmentStatusFilter.NewOnly),
				new CodexDropdownRow(true),
				new CodexDropdownRow(dropdownMenu, "All Filters Panel...", () => ToggleFilterMenu(), textColor: PanelChrome.Tone(new Color(180, 195, 220))),
				new CodexDropdownRow(dropdownMenu, "Reset All Filters", () => ResetAllFilters(), textColor: PanelChrome.Tone(new Color(180, 195, 220)))
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
				sortBtn.IsActiveHighlight = currentSortMode != DefaultSort || isSortDropdownOpen;
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
				_ => $"Sort: Newest {arrow}"
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
				hoverTick = Main.GameUpdateCount;
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

			// The title screen's hover glitch: for a moment after the mouse arrives, a letter or two of the label flips to a
			// code glyph and a glitch band tears across the button.
			private uint hoverTick;
			private string shownLabel;
			private const uint DecodeTicks = 18;

			private void UpdateDecode(SpriteBatch spriteBatch, Rectangle rect)
			{
				uint age = Main.GameUpdateCount - hoverTick;
				bool decoding = isHovered && age < DecodeTicks;
				string want = decoding ? PanelGlitch.Decode(TabName, (int)(age / 2) + TabName.Length) : TabName;
				if (want != shownLabel)
				{
					label.SetText(want);
					shownLabel = want;
				}

				if (decoding)
					PanelGlitch.Bands(spriteBatch, rect, (int)(age / 2) + TabName.Length * 5, 1, 1f - age / (float)DecodeTicks);
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
					bg = isHovered ? PanelChrome.Tone(new Color(18, 30, 56)) * 0.98f : PanelChrome.Tone(new Color(14, 24, 46)) * 0.96f;
					border = PanelChrome.Tone(new Color(56, 189, 248));
					label.TextColor = Color.White;
				}
				else if (isHovered)
				{
					bg = PanelChrome.Tone(new Color(14, 22, 38)) * 0.95f;
					border = PanelChrome.Tone(new Color(56, 189, 248)) * 0.75f;
					label.TextColor = new Color(240, 245, 255);
				}
				else
				{
					bg = PanelChrome.Tone(new Color(10, 16, 28)) * 0.92f;
					border = PanelChrome.Tone(new Color(30, 42, 66));
					label.TextColor = PanelChrome.Tone(new Color(160, 175, 205));
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
					spriteBatch.Draw(pixel, new Rectangle(rect.X + 2, rect.Bottom - 2, rect.Width - 4, 2), PanelChrome.Tone(new Color(56, 189, 248)));
				}

				// Corner marks, in the frame style chosen on the COSMETICS page
				PanelChrome.DrawCorners(spriteBatch, rect, border * (isHovered ? 1.0f : 0.70f), 0.4f);

				UpdateDecode(spriteBatch, rect);
			}
		}

		// Right-hand Detail Inspector Panel
		private class AugmentDetailPanel : UIPanel
		{
			private static readonly Regex CooldownRegex = new Regex(
				@"(?:[\s,]+(?:followed\s+by\s+a\s+)?)?(?:\[c\/[0-9a-fA-F]{6}:)?([0-9]+(?:\.[0-9]+)?\s*(?:s|sec|seconds?|minutes?|min|hr|hours?)\s+cooldown)(?:\])?(\s+per\s+player)?\.?",
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
					// Dev button: on the status row at the right, in the scheme colours. Install is the lit button, Uninstall a plain one.
					devEquipBtn.HAlign = 1f;
					devEquipBtn.Left.Set(0f, 0f);
					devEquipBtn.Top.Set(58f, 0f);
					devEquipBtn.Width.Set(88f, 0f);
					devEquipBtn.SetText(currentIsOwned ? "Uninstall" : "Install");
					devEquipBtn.IsActiveHighlight = !currentIsOwned;
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

			// In the NieR style the Legendary detail panel is a cream plate. Text on a plate has to be dark and drawn without the
			// black drop shadow (which smudges it), and the bright colour tags in the description are darkened to stay readable.
			private bool platePanel;

			private void DrawDetailText(SpriteBatch sb, ReLogic.Graphics.DynamicSpriteFont font, string text, Vector2 pos, Color color, float rotation, Vector2 origin, Vector2 scale)
			{
				if (!platePanel)
				{
					ChatManager.DrawColorCodedStringWithShadow(sb, font, text, pos, color, rotation, origin, scale);
					return;
				}

				bool nearWhite = color.R >= 200 && color.G >= 200 && color.B >= 200;
				Color ink = nearWhite ? RarityLook.OnPlate * (color.A / 255f) : RarityLook.Darken(color, 0.5f);
				RarityLook.DrawCoded(sb, font, RarityLook.DarkenTags(text, 0.45f), pos, ink, scale, false);
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
					: PanelChrome.Tone(new Color(30, 42, 66));
				if (currentAugment?.Rarity == AugmentRarity.Common)
					rarityColor = RarityLook.Common;

				// NieR style: the same frame, pips and tier band as the popup cards. Only Legendary is a cream plate; the others stay dark.
				bool nier = RarityLook.Style == TierStyle.Nier && currentAugment != null;
				platePanel = nier && currentAugment.Rarity == AugmentRarity.Legendary;
				float bandInset = nier ? RarityLook.TierBandHeight : 0f;
				if (nier)
				{
					if (platePanel)
						RarityLook.DrawPlate(spriteBatch, rect);
					else
						HubDraw.Fill(spriteBatch, rect, PanelChrome.PanelBackground);

					RarityLook.DrawTierFrame(spriteBatch, rect, currentAugment.Rarity, false);
					RarityLook.DrawTierBand(spriteBatch, rect, currentAugment.Rarity, false);
				}

				if (!nier)
				{
				// Outer Stepped Halos for Epic & Legendary (clean hairline falloff, no solid boxes)
				if (currentAugment?.Rarity == AugmentRarity.Epic)
				{
					Color epicGlow = RarityLook.Epic;
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
					Color legGlow = RarityLook.Legendary;
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
				spriteBatch.Draw(pixel, rect, PanelChrome.Tone(new Color(10, 16, 28)) * 0.94f);

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
						borderColor = Color.Lerp(RarityLook.Rare, RarityLook.RareLight, rarePulse * 0.35f);
					else if (currentAugment.Rarity == AugmentRarity.Epic)
						borderColor = Color.Lerp(RarityLook.Epic, RarityLook.EpicLight, epicPulse * 0.45f);
					else if (currentAugment.Rarity == AugmentRarity.Legendary)
						borderColor = Color.Lerp(RarityLook.Legendary, RarityLook.LegendaryLight, legPulse * 0.45f);
					else
						borderColor = RarityLook.Common;
				}
				else
				{
					borderColor = PanelChrome.Tone(new Color(30, 42, 66));
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
					DrawDetailText(spriteBatch, emptyFont, prompt, pPos, PanelChrome.Tone(new Color(148, 163, 184)) * 0.75f, 0f, Vector2.Zero, new Vector2(0.82f));
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
						Color sheenColor = RarityLook.LegendaryLight;

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
							AugmentSlotElement.DrawSubtleStarSparkle(spriteBatch, sp.x, sp.y, intensity, RarityLook.LegendaryLight);
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
							AugmentSlotElement.DrawSubtleStarSparkle(spriteBatch, sp.x, sp.y, intensity, RarityLook.EpicLight);
						}
					}
				}

				}

				var font = FontAssets.MouseText.Value;
				float x = dims.X + 10f;
				// In the NieR style the header starts lower, below the tier pips on the top edge, so a long name never runs into them.
				float y = dims.Y + (nier ? 22f : 10f);
				float maxTextWidth = dims.Width - 20f;

				// 1. Augment Display Name & Icon
				Color nameColor = platePanel ? RarityLook.OnPlate : AugmentListEntry.RarityColor(currentAugment.Rarity);
				Texture2D classIcon = AugmentSlotElement.GetClassIcon(currentAugment.Class);
				if (classIcon != null)
				{
					Color iconColor = platePanel ? RarityLook.OnPlate : AugmentListEntry.RarityColor(currentAugment.Rarity);
					if (currentAugment.Rarity == AugmentRarity.Common)
						iconColor = RarityLook.Common;

					spriteBatch.Draw(classIcon, new Vector2(x, y - 2f), iconColor);
					float nameH = ChatManager.GetStringSize(font, currentAugment.DisplayName, new Vector2(0.95f)).Y;
					if (nier)
					{
						// NieR style: the class sits right under the name, beside the icon, in small spaced capitals.
						const float classScale = 0.72f;
						const float classTracking = 3f;
						float textLeft = x + classIcon.Width + 8f;

						// A long name shrinks to fit beside the icon instead of running to the panel edge.
						float nameFit = Math.Min(0.95f, (maxTextWidth - classIcon.Width - 8f) / Math.Max(1f, ChatManager.GetStringSize(font, currentAugment.DisplayName, Vector2.One).X));
						nameH = ChatManager.GetStringSize(font, currentAugment.DisplayName, new Vector2(nameFit)).Y;

						float classH = font.MeasureString("M").Y * classScale;
						float blockH = nameH + 1f + classH;
						float top = (float)Math.Round(y - 2f + (classIcon.Height - blockH) * 0.5f);
						DrawDetailText(
							spriteBatch, font, currentAugment.DisplayName, new Vector2(textLeft, top), nameColor, 0f, Vector2.Zero, new Vector2(nameFit)
						);

						// The class in the game's own font (the thin menu font was too faint), spaced out letter by letter, on the same left edge as the name.
						Color classInk = platePanel ? RarityLook.OnPlate : MenuLook.Current.Idle;
						float cx = textLeft + 1f;
						float cy = (float)Math.Round(top + nameH + 1f);
						foreach (char ch in currentAugment.Class.ToString().ToUpperInvariant())
						{
							string s = ch.ToString();
							RarityLook.DrawCoded(spriteBatch, font, s, new Vector2((float)Math.Round(cx), cy), classInk, new Vector2(classScale), !platePanel);
							cx += font.MeasureString(s).X * classScale + classTracking;
						}
						y += classIcon.Height + 4f;
					}
					else
					{
						DrawDetailText(
							spriteBatch, font, currentAugment.DisplayName, new Vector2(x + classIcon.Width + 8f, y + 2f), nameColor, 0f, Vector2.Zero, new Vector2(0.95f)
						);
						y += Math.Max(classIcon.Height, nameH) + 4f;
					}
				}
				else
				{
					DrawDetailText(
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

				// The NieR style shows the tier with the frame, the pips and the band, so it has no stars here.
				if (nier)
					starCount = 0;

				Color starColor = platePanel ? RarityLook.OnPlate : AugmentListEntry.RarityColor(currentAugment.Rarity);
				if (currentAugment.Rarity == AugmentRarity.Common)
					starColor = RarityLook.Common;

				if (RarityStarAsset == null)
					RarityStarAsset = ModContent.Request<Texture2D>("Augments/UI/RarityStar", ReLogic.Content.AssetRequestMode.ImmediateLoad);

				if (nier)
				{
					// the class is already shown in the header
				}
				else if (RarityStarAsset?.IsLoaded == true)
				{
					Texture2D starTex = RarityStarAsset.Value;
					float starSpacing = 17f;

					for (int s = 0; s < starCount; s++)
					{
						float sx = x + s * starSpacing;
						Vector2 starPos = new Vector2(sx, y);

						Color drawColor = starColor;

						if (!nier && currentAugment.Rarity == AugmentRarity.Epic)
						{
							float sPulse = (float)Math.Sin(time * 3f + s * 0.4f) * 0.5f + 0.5f;
							drawColor = Color.Lerp(starColor, RarityLook.EpicLight, sPulse * 0.4f);

							// Subtle star-shaped halo using the star texture itself (no square boxes)
							Color halo = RarityLook.Epic * (0.20f + sPulse * 0.25f);
							spriteBatch.Draw(starTex, starPos + new Vector2(-1f, 0f), halo);
							spriteBatch.Draw(starTex, starPos + new Vector2(1f, 0f), halo);
							spriteBatch.Draw(starTex, starPos + new Vector2(0f, -1f), halo);
							spriteBatch.Draw(starTex, starPos + new Vector2(0f, 1f), halo);
						}
						else if (!nier && currentAugment.Rarity == AugmentRarity.Legendary)
						{
							float sPulse = (float)Math.Sin(time * 4f + s * 0.5f) * 0.5f + 0.5f;
							drawColor = Color.Lerp(starColor, RarityLook.LegendaryLight, sPulse * 0.45f);

							// Subtle star-shaped halo using the star texture itself (no square boxes)
							Color halo = RarityLook.Legendary * (0.25f + sPulse * 0.30f);
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

					float classX = starCount > 0 ? x + starCount * starSpacing + 6f : x;
					Color classColor = PanelChrome.Tone(new Color(200, 210, 230));
					DrawDetailText(
						spriteBatch, font, currentAugment.Class.ToString(), new Vector2(classX, y - 1f), classColor, 0f, Vector2.Zero, new Vector2(0.82f)
					);
					y += Math.Max(starTex.Height, ChatManager.GetStringSize(font, currentAugment.Class.ToString(), new Vector2(0.82f)).Y) + 6f;
				}
				else
				{
					string tierClassText = $"[{currentAugment.Rarity} Tier]  {currentAugment.Class}";
					DrawDetailText(
						spriteBatch, font, tierClassText, new Vector2(x, y), Color.LightGray, 0f, Vector2.Zero, new Vector2(0.78f)
					);
					y += ChatManager.GetStringSize(font, tierClassText, new Vector2(0.78f)).Y + 4f;
				}

				// 3. Ownership Status (Clean unboxed indicator)
				string statusText = currentIsOwned ? "✓ Installed" : "• Not Installed";
				Color statusColor = currentIsOwned ? AugmentTextColors.Healing : PanelChrome.Tone(new Color(148, 163, 184));
				DrawDetailText(
					spriteBatch, font, statusText, new Vector2(x, y), statusColor, 0f, Vector2.Zero, new Vector2(0.80f)
				);

				y += ChatManager.GetStringSize(font, statusText, new Vector2(0.80f)).Y + 8f;

				// 4. Subtle Divider Line (matches rarity tier)
				Color divColor = (platePanel ? RarityLook.OnPlate : AugmentListEntry.RarityColor(currentAugment.Rarity)) * 0.35f;
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

				// Plugins with a key to press are "Active"; every other plugin works on its own, so it is "Passive".
				string kind = currentAugment.ActiveModKeybind != null ? "ACTIVE" : "PASSIVE";
				string headerTitle = isCtrlDown ? kind + " (DETAILED)" : kind;
				if (nier)
				{
					// NieR style: a small square marker and the word in spaced capitals, in the scheme's amber (dark ink on a Legendary plate).
					Color headInk = platePanel ? RarityLook.OnPlate : MenuLook.Current.Amber;
					HubDraw.Fill(spriteBatch, new Rectangle((int)x, (int)Math.Round(y + 5f), 5, 5), headInk);
					float hx = x + 12f;
					foreach (char ch in headerTitle)
					{
						string s = ch.ToString();
						RarityLook.DrawCoded(spriteBatch, font, s, new Vector2((float)Math.Round(hx), (float)Math.Round(y)), headInk, new Vector2(0.72f), !platePanel);
						hx += font.MeasureString(s).X * 0.72f + 3f;
					}
				}
				else
				{
					DrawDetailText(
						spriteBatch, font, headerTitle, new Vector2(x, y), new Color(250, 204, 21), 0f, Vector2.Zero, new Vector2(0.72f)
					);
				}
				y += ChatManager.GetStringSize(font, headerTitle, new Vector2(0.72f)).Y + 4f;

				// 6. Wrapped Description with chat colors (stripping inline cooldown clause)
				ExtractDescriptionAndCooldown(currentAugment, out string effectText, out string cooldownText, isCtrlDown, Main.LocalPlayer);

				float footerTop = !string.IsNullOrEmpty(cooldownText) ? (dims.Y + dims.Height - 64f - bandInset) : (dims.Y + dims.Height - 36f - bandInset);
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
						// The "[key]" inside the description is the rebind button. The two outer brackets are blanked out of the text and the key sits
						// in a small clean box instead, so there is one frame, not brackets inside a box.
						string drawLine = line;
						if (canRebind && !hasKeyLabel && KeybindEditor.TryFindLabel(line, out int labelStart, out int labelLength))
						{
							var chars = line.ToCharArray();
							chars[labelStart] = ' ';
							chars[labelStart + labelLength - 1] = ' ';
							drawLine = new string(chars);

							float labelX = x + ChatManager.GetStringSize(font, drawLine.Substring(0, labelStart), descScale).X;
							float labelW = ChatManager.GetStringSize(font, drawLine.Substring(labelStart, labelLength), descScale).X;

							// The letters sit in the upper part of the font's line box, so the box is centred on the letters (about 41% down the
							// line), not on the whole line height.
							int pillH = (int)Math.Round(lineHeight * 0.66f);
							var pill = new Rectangle((int)Math.Round(labelX) - 1, (int)Math.Round(currentY + lineHeight * 0.41f - pillH * 0.5f), (int)Math.Round(labelW) + 2, pillH);
							keyLabelRect = pill;
							hasKeyLabel = true;

							bool listening = KeybindEditor.IsListeningFor(currentAugment.ActiveModKeybind);
							bool hovered = pill.Contains(Main.mouseX, Main.mouseY);
							float pulse = (float)Math.Sin(time * 8f) * 0.5f + 0.5f;
							Color accent = platePanel ? RarityLook.OnPlate : (listening ? MenuLook.Current.Amber : PanelChrome.Accent);
							float fillAlpha = listening ? 0.14f + pulse * 0.12f : (hovered ? 0.18f : 0.08f);
							Color edge = accent * (listening ? 0.7f + pulse * 0.3f : (hovered ? 0.95f : 0.55f));

							// Fill, then a 1px edge with the four corner pixels left out, which softens the box without rounding it.
							spriteBatch.Draw(pixel, pill, accent * fillAlpha);
							spriteBatch.Draw(pixel, new Rectangle(pill.X + 1, pill.Y, pill.Width - 2, 1), edge);
							spriteBatch.Draw(pixel, new Rectangle(pill.X + 1, pill.Bottom - 1, pill.Width - 2, 1), edge);
							spriteBatch.Draw(pixel, new Rectangle(pill.X, pill.Y + 1, 1, pill.Height - 2), edge);
							spriteBatch.Draw(pixel, new Rectangle(pill.Right - 1, pill.Y + 1, 1, pill.Height - 2), edge);

							if (hovered && !listening)
								Main.instance.MouseText("Click to change this key");
						}

						DrawDetailText(
							spriteBatch, font, platePanel ? drawLine : RarityLook.PaleTags(drawLine, 0.30f), new Vector2(x, currentY), Color.White * 0.95f, 0f, Vector2.Zero, descScale
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
						DrawDetailText(
							spriteBatch, font, "Press any key to bind it  •  Esc to cancel", new Vector2(x, currentY),
							AugmentTextColors.Active * 0.9f, 0f, Vector2.Zero, new Vector2(0.72f)
						);
					}
				}

				// Sleek high-tech mini scroll indicator if content overflows and can be scrolled
				if (maxScroll > 0f)
				{
					float scrollTrackX = dims.X + dims.Width - 8f;
					spriteBatch.Draw(pixel, new Rectangle((int)scrollTrackX, (int)descStartY, 2, (int)availableHeight), PanelChrome.Tone(new Color(30, 42, 66)) * 0.75f);

					float thumbH = Math.Max(14f, availableHeight * (availableHeight / totalContentHeight));
					float thumbY = descStartY + (availableHeight - thumbH) * (descScroll / maxScroll);
					spriteBatch.Draw(pixel, new Rectangle((int)scrollTrackX, (int)thumbY, 2, (int)thumbH), PanelChrome.Tone(new Color(56, 189, 248)) * 0.85f);
				}

				// 8. Cooldown footer displayed at bottom of card with clean telemetry layout
				if (!string.IsNullOrEmpty(cooldownText))
				{
					float cdY = dims.Y + dims.Height - 54f - bandInset;
					// Subtle divider line
					spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle((int)x, (int)(cdY - 6f), (int)maxTextWidth, 1), PanelChrome.Tone(new Color(56, 189, 248)) * 0.35f);

					string cdDisplay = $"COOLDOWN  •  {cooldownText}";
					DrawDetailText(
						spriteBatch, font, cdDisplay, new Vector2(x + 2f, cdY), PanelChrome.Tone(new Color(56, 189, 248)), 0f, Vector2.Zero, new Vector2(0.78f)
					);
				}

				// 9. 50% alpha text under card preview: "Hold [Ctrl] for detailed view"
				float hintY = dims.Y + dims.Height - 28f - bandInset;
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

				Color hintColor = isCtrlDown ? MenuLook.Current.Amber * 0.75f : Color.White * 0.50f;

				// Guarantee hint text never clips outside the card borders
				float maxAllowedWidth = maxTextWidth - 12f;
				Vector2 rawHintSize = ChatManager.GetStringSize(font, hintText, Vector2.One);
				float hintScaleVal = Math.Min(0.66f, maxAllowedWidth / Math.Max(1f, rawHintSize.X));
				var hintScale = new Vector2(hintScaleVal);
				Vector2 hintSize = ChatManager.GetStringSize(font, hintText, hintScale);

				DrawDetailText(
					spriteBatch, font, hintText,
					new Vector2(x + (maxTextWidth - hintSize.X) * 0.5f, hintY),
					hintColor, 0f, Vector2.Zero, hintScale
				);
			}
		}

		// NieR-style panel title: a cream tab hangs from the top edge of the panel with the word in dark spaced capitals (the same cream plate
		// look as a Legendary card). The plate follows the Cosmetics colour scheme.
		private class PluginsTitleElement : UIElement
		{
			private const string Word = "PLUGINS";
			private const float Scale = 0.92f;
			private const float Tracking = 6f;

			protected override void DrawSelf(SpriteBatch spriteBatch)
			{
				var font = FontAssets.MouseText.Value;
				CalculatedStyle dims = GetDimensions();
				float width = 0f;
				foreach (char ch in Word)
					width += font.MeasureString(ch.ToString()).X * Scale + Tracking;
				width -= Tracking;

				// Same centre the old title used (slightly left of the container middle, clear of the DEV badge).
				float cx = dims.X + dims.Width * 0.44f;

				// The plate reaches up to the panel's top edge (this element starts 8px below it) and a little past the text on each side.
				int plateW = (int)Math.Round(width) + 44;
				var plate = new Rectangle((int)Math.Round(cx - plateW * 0.5f), (int)dims.Y - 7, plateW, (int)dims.Height + 7);
				HubDraw.Fill(spriteBatch, plate, RarityLook.Plate);
				// A thin dark line inside the plate, open at the top where the tab joins the panel edge.
				Color line = RarityLook.OnPlate * 0.45f;
				HubDraw.Fill(spriteBatch, new Rectangle(plate.X + 3, plate.Bottom - 4, plate.Width - 6, 1), line);
				HubDraw.Fill(spriteBatch, new Rectangle(plate.X + 3, plate.Y, 1, plate.Height - 3), line);
				HubDraw.Fill(spriteBatch, new Rectangle(plate.Right - 4, plate.Y, 1, plate.Height - 3), line);

				float lineH = font.MeasureString("A").Y * Scale;
				float x = (float)Math.Round(cx - width * 0.5f);
				float y = (float)Math.Round(plate.Y + (plate.Height - 4 - lineH) * 0.5f) + 2f;

				// Dark ink, no drop shadow; drawn twice, one pixel apart, so the letters are bold enough to hold on the light plate.
				Color ink = RarityLook.OnPlate;
				foreach (char ch in Word)
				{
					string s = ch.ToString();
					RarityLook.DrawCoded(spriteBatch, font, s, new Vector2(x, y), ink, new Vector2(Scale), false);
					RarityLook.DrawCoded(spriteBatch, font, s, new Vector2(x + 1f, y), ink, new Vector2(Scale), false);
					x += font.MeasureString(s).X * Scale + Tracking;
				}
			}
		}

		// Secret Dev Badge displayed next to Title when Dev Mode is active
		private class DevBadgeElement : UIPanel
		{
			private readonly AugmentListUIState parent;
			private bool isHovered;

			public DevBadgeElement(AugmentListUIState parent)
			{
				this.parent = parent;
				SetPadding(0f);
				Width.Set(52f, 0f);
				Height.Set(22f, 0f);

				// The "DEV" word is drawn in DrawSelf (spaced capitals, scheme colours), so there is no label child.
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

				float pulse = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 3f) * 0.5f + 0.5f;

				// NieR style: a small framed tag with the word in spaced capitals, in the scheme's amber. The frame breathes slowly.
				CalculatedStyle dims = GetDimensions();
				var rect = new Rectangle((int)dims.X, (int)dims.Y, (int)dims.Width, (int)dims.Height);
				Color amber = MenuLook.Current.Amber;
				Color edge = amber * (0.55f + 0.30f * pulse + (isHovered ? 0.15f : 0f));
				HubDraw.Fill(spriteBatch, rect, PanelChrome.Tone(new Color(10, 16, 28)) * (isHovered ? 0.98f : 0.9f));
				HubDraw.Fill(spriteBatch, new Rectangle(rect.X, rect.Y, rect.Width, 1), edge * 0.6f);
				HubDraw.Fill(spriteBatch, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), edge * 0.6f);
				PanelChrome.DrawCorners(spriteBatch, rect, edge, 0.5f);

				const float scale = 0.5f;
				const float tracking = 4f;
				float w = HubDraw.Width("DEV", scale, tracking) - tracking;
				HubDraw.Text(spriteBatch, "DEV", new Vector2((float)Math.Round(rect.X + (rect.Width - w) / 2f), (float)Math.Round(rect.Y + (rect.Height - HubDraw.LineHeight(scale)) / 2f + 1f)), amber, scale, tracking);

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
				spriteBatch.Draw(pixel, new Rectangle(bRect.X + 18, divY, bRect.Width - 36, 1), PanelChrome.Tone(new Color(30, 41, 59)) * 0.90f);
				int midX = bRect.X + bRect.Width / 2;
				spriteBatch.Draw(pixel, new Rectangle(midX - 1, divY - 1, 3, 3), PanelChrome.Tone(new Color(56, 189, 248)) * 0.85f);
				spriteBatch.Draw(pixel, new Rectangle(midX, divY, 1, 1), Color.White * 0.9f);

				// Faint radar watermark
				Vector2 center = new Vector2(bRect.X + bRect.Width * 0.5f, bRect.Y + bRect.Height * 0.55f);
				Color watermarkCol = PanelChrome.Tone(new Color(56, 189, 248)) * 0.04f;
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
				BackgroundColor = PanelChrome.Tone(new Color(16, 22, 44)) * 0.95f;
				BorderColor = PanelChrome.Tone(new Color(45, 60, 105));
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
					bg = PanelChrome.Tone(new Color(14, 22, 38)) * 0.98f;
					border = PanelChrome.Tone(new Color(56, 189, 248));
				}
				else if (isHovered)
				{
					bg = PanelChrome.Tone(new Color(12, 18, 32)) * 0.96f;
					border = PanelChrome.Tone(new Color(56, 189, 248)) * 0.70f;
				}
				else
				{
					bg = PanelChrome.Tone(new Color(10, 16, 28)) * 0.94f;
					border = PanelChrome.Tone(new Color(30, 42, 66));
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

				// Corner marks, in the frame style chosen on the COSMETICS page
				PanelChrome.DrawCorners(spriteBatch, rect, border * (isHovered ? 1.0f : 0.70f), 0.4f);

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
								PanelChrome.Tone(new Color(110, 185, 255)),
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
							PanelChrome.Tone(new Color(130, 148, 175)) * 0.75f,
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
					Color clearColor = clearHover ? new Color(255, 110, 110) : PanelChrome.Tone(new Color(160, 175, 205)) * 0.8f;
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
			// The title screen's hover glitch: for a moment after the mouse arrives, a letter or two of the label flips to a code glyph.
			private uint hoverTick;
			private string baseText = "";
			private string shownText;
			private const uint DecodeTicks = 18;
			public event Action Clicked;
			public event Action RightClicked;
			private bool isHovered;
			public bool IsActiveHighlight { get; set; }
			public Color CustomActiveBg { get; set; } = PanelChrome.Tone(new Color(18, 34, 62));
			public Color CustomActiveBorder { get; set; } = PanelChrome.Tone(new Color(56, 189, 248));
			public Color BackgroundColor { get; set; } = PanelChrome.Tone(new Color(10, 16, 28));
			public Color BorderColor { get; set; } = PanelChrome.Tone(new Color(30, 42, 66));

			public CodexFilterButton(string initialText, float textScale = 0.72f)
			{
				baseText = initialText ?? "";
				Label = new UIText(initialText, textScale)
				{
					HAlign = 0.5f,
					VAlign = 0.5f
				};
				Append(Label);
			}

			public void SetText(string text)
			{
				baseText = text ?? "";
				shownText = null; // redraw with the new text
			}

			public void SetTextColor(Color color)
			{
				Label.TextColor = color;
			}

			public override void MouseOver(UIMouseEvent evt)
			{
				base.MouseOver(evt);
				isHovered = true;
				hoverTick = Main.GameUpdateCount;
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

			private void UpdateDecode(SpriteBatch spriteBatch, Rectangle rect)
			{
				uint age = Main.GameUpdateCount - hoverTick;
				bool decoding = isHovered && age < DecodeTicks && baseText.Length > 1;
				string want = decoding ? PanelGlitch.Decode(baseText, (int)(age / 2) + baseText.Length) : baseText;
				if (want != shownText)
				{
					Label.SetText(want);
					shownText = want;
				}

				if (decoding)
					PanelGlitch.Bands(spriteBatch, rect, (int)(age / 2) + baseText.Length * 5, 1, 1f - age / (float)DecodeTicks);
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
					bg = isHovered ? PanelChrome.Tone(new Color(16, 24, 42)) * 0.96f : (BackgroundColor != PanelChrome.Tone(new Color(10, 16, 28)) ? BackgroundColor : PanelChrome.Tone(new Color(10, 16, 28)) * 0.94f);
					border = isHovered ? PanelChrome.Tone(new Color(56, 189, 248)) * 0.85f : BorderColor;
					Label.TextColor = isHovered ? Color.White : PanelChrome.Tone(new Color(220, 230, 245));
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

				// Corner marks, in the frame style chosen on the COSMETICS page
				PanelChrome.DrawCorners(spriteBatch, rect, border * (isHovered ? 1.0f : 0.70f), 0.4f);

				UpdateDecode(spriteBatch, rect);
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
			private uint hoverTick;

			public CodexDropdownRow(CodexDropdown parent, string text, Action onClick, Color? textColor = null, Texture2D icon = null, Color? iconColor = null, bool drawDiamondIcon = false, bool isSelected = false)
			{
				parentDropdown = parent;
				Text = text;
				OnClick = onClick;
				TextColor = textColor ?? PanelChrome.Tone(new Color(220, 230, 245));
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
					hoverTick = Main.GameUpdateCount;
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
					spriteBatch.Draw(pixel, new Rectangle(rect.X + 4, divY, rect.Width - 8, 1), PanelChrome.Tone(new Color(35, 48, 72)) * 0.9f);
					return;
				}

				if (IsSelected)
				{
					Color selBg = isHovered ? PanelChrome.Tone(new Color(24, 48, 86)) : PanelChrome.Tone(new Color(16, 34, 62));
					spriteBatch.Draw(pixel, rect, selBg);

					// 3px active indicator bar on left
					spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y + 2, 3, rect.Height - 4), PanelChrome.Tone(new Color(56, 189, 248)));
				}
				else if (isHovered)
				{
					spriteBatch.Draw(pixel, rect, PanelChrome.Tone(new Color(20, 36, 64)) * 0.9f);
					// 1px faint hover border
					spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y + 2, 2, rect.Height - 4), PanelChrome.Tone(new Color(56, 189, 248)) * 0.5f);
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
				// The hover glitch, as on the other buttons: a letter or two flips to a code glyph for a moment.
				uint age = Main.GameUpdateCount - hoverTick;
				string shownText = isHovered && age < 14 && Text.Length > 1 ? PanelGlitch.Decode(Text, (int)(age / 2) + Text.Length) : Text;
				Vector2 scale = new Vector2(0.72f);
				Vector2 textSize = ChatManager.GetStringSize(font, Text, scale);
				float textY = rect.Y + (rect.Height - textSize.Y) / 2f + 1.5f;

				ChatManager.DrawColorCodedStringWithShadow(
					spriteBatch,
					font,
					shownText,
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
				spriteBatch.Draw(pixel, rect, PanelChrome.Tone(new Color(10, 16, 28, 252)));

				// 1px Inner Hairline Highlight Accent
				Color innerHairline = Color.White * 0.05f;
				spriteBatch.Draw(pixel, new Rectangle(rect.X + 1, rect.Y + 1, rect.Width - 2, 1), innerHairline);
				spriteBatch.Draw(pixel, new Rectangle(rect.X + 1, rect.Bottom - 2, rect.Width - 2, 1), innerHairline);
				spriteBatch.Draw(pixel, new Rectangle(rect.X + 1, rect.Y + 1, 1, rect.Height - 2), innerHairline);
				spriteBatch.Draw(pixel, new Rectangle(rect.Right - 2, rect.Y + 1, 1, rect.Height - 2), innerHairline);

				// 1px Outer Border
				Color border = PanelChrome.Tone(new Color(38, 56, 86));
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

			// Layout of the panel: 320 wide, 12px side margins, every row of buttons shares the 296px between them equally, so no button can
			// run past the edge and the rows always line up with each other.
			internal const float PanelW = 320f;
			internal const float PanelH = 344f;
			private const float Margin = 12f;
			private const float Inner = PanelW - Margin * 2f;
			private const float PillH = 22f;

			public AugmentFilterPanel(AugmentListUIState state)
			{
				this.state = state;
				SetPadding(0f);
				BackgroundColor = Color.Transparent;
				BorderColor = Color.Transparent;

				// Title
				UIText title = new UIText("Filters & Sorting", 0.85f)
				{
					TextColor = MenuLook.Current.Amber,
					Top = { Pixels = 8f },
					Left = { Pixels = Margin }
				};
				Append(title);

				// Close button [X]
				var closeBtn = new PanelCloseButton();
				closeBtn.Width.Set(20f, 0f);
				closeBtn.Height.Set(20f, 0f);
				closeBtn.Left.Set(PanelW - Margin + 4f - 20f, 0f);
				closeBtn.Top.Set(5f, 0f);
				closeBtn.Clicked += state.ToggleFilterMenu;
				Append(closeBtn);

				float y = 38f;

				// 1. CLASS FILTER
				AddSectionLabel("CLASS FILTER", y);
				y += 16f;
				AddPillRow(y, new (string, Action<CodexFilterButton>)[]
				{
					("All", b => classButtons.Add((null, b))),
					("Universal", b => classButtons.Add((AugmentClass.Universal, b))),
					("Melee", b => classButtons.Add((AugmentClass.Melee, b))),
					("Ranged", b => classButtons.Add((AugmentClass.Ranged, b)))
				}, b => { var c = classButtons.Find(p => p.btn == b).cls; b.Clicked += () => state.SetClassFilter(c); });
				y += PillH + 3f;
				AddPillRow(y, new (string, Action<CodexFilterButton>)[]
				{
					("Magic", b => classButtons.Add((AugmentClass.Magic, b))),
					("Summon", b => classButtons.Add((AugmentClass.Summon, b))),
					("Support", b => classButtons.Add((AugmentClass.Support, b)))
				}, b => { var c = classButtons.Find(p => p.btn == b).cls; b.Clicked += () => state.SetClassFilter(c); });
				y += PillH + 10f;

				// 2. RARITY TIER
				AddSectionLabel("RARITY TIER", y);
				y += 16f;
				AddPillRow(y, new (string, Action<CodexFilterButton>)[]
				{
					("All", b => rarityButtons.Add((null, b))),
					("Common", b => rarityButtons.Add((AugmentRarity.Common, b))),
					("Rare", b => rarityButtons.Add((AugmentRarity.Rare, b))),
					("Epic", b => rarityButtons.Add((AugmentRarity.Epic, b))),
					("Legendary", b => rarityButtons.Add((AugmentRarity.Legendary, b)))
				}, b =>
				{
					var r = rarityButtons.Find(p => p.btn == b).rar;
					if (r.HasValue)
						b.CustomActiveBorder = RarityLook.Of(r.Value);
					b.Clicked += () => state.SetRarityFilter(r);
				});
				y += PillH + 10f;

				// 3. SORT ORDER: newest first, then oldest, then the rest
				AddSectionLabel("SORT BY", y);
				y += 16f;
				AddPillRow(y, new (string, Action<CodexFilterButton>)[]
				{
					("Newest First", b => sortButtons.Add((AugmentSortMode.Newest, b))),
					("Oldest First", b => sortButtons.Add((AugmentSortMode.Oldest, b)))
				}, b => { var s = sortButtons.Find(p => p.btn == b).sort; b.Clicked += () => state.SetSortMode(s); });
				y += PillH + 3f;
				AddPillRow(y, new (string, Action<CodexFilterButton>)[]
				{
					("A → Z", b => sortButtons.Add((AugmentSortMode.NameAZ, b))),
					("Z → A", b => sortButtons.Add((AugmentSortMode.NameZA, b))),
					("Class", b => sortButtons.Add((AugmentSortMode.Class, b)))
				}, b => { var s = sortButtons.Find(p => p.btn == b).sort; b.Clicked += () => state.SetSortMode(s); });
				y += PillH + 3f;
				AddPillRow(y, new (string, Action<CodexFilterButton>)[]
				{
					("Rarity ▼ High", b => sortButtons.Add((AugmentSortMode.RarityHighLow, b))),
					("Rarity ▲ Low", b => sortButtons.Add((AugmentSortMode.RarityLowHigh, b)))
				}, b => { var s = sortButtons.Find(p => p.btn == b).sort; b.Clicked += () => state.SetSortMode(s); });
				y += PillH + 10f;

				// 4. STATUS
				AddSectionLabel("STATUS", y);
				y += 16f;
				AddPillRow(y, new (string, Action<CodexFilterButton>)[]
				{
					("All", b => statusButtons.Add((AugmentStatusFilter.All, b))),
					("Equipped", b => statusButtons.Add((AugmentStatusFilter.Equipped, b))),
					("Unequipped", b => statusButtons.Add((AugmentStatusFilter.Unequipped, b))),
					("New Only", b => statusButtons.Add((AugmentStatusFilter.NewOnly, b)))
				}, b => { var s = statusButtons.Find(p => p.btn == b).status; b.Clicked += () => state.SetStatusFilter(s); });
				y += PillH + 12f;

				// Bottom buttons, in the scheme colours: Reset is a plain pill, Close / Apply is the lit one.
				AddPillRow(y, new (string, Action<CodexFilterButton>)[]
				{
					("Reset All", b => b.Clicked += state.ResetAllFilters),
					("Close / Apply", b => { b.IsActiveHighlight = true; b.Clicked += state.ToggleFilterMenu; })
				}, null, 24f);
			}

			// One row of equal-width buttons across the panel. `register` is called for each button as it is created (so the caller can
			// keep it in a list); `wire` then runs once the button is registered, to attach its click.
			private void AddPillRow(float y, (string text, Action<CodexFilterButton> register)[] items, Action<CodexFilterButton> wire, float height = PillH)
			{
				const float gap = 4f;
				int n = items.Length;
				for (int i = 0; i < n; i++)
				{
					int x0 = (int)Math.Round(i * (Inner + gap) / n);
					int x1 = (int)Math.Round((i + 1) * (Inner + gap) / n) - (int)gap;
					var btn = CreatePill(items[i].text, x1 - x0, height, Margin + x0, y);
					items[i].register?.Invoke(btn);
					wire?.Invoke(btn);
				}
			}

			private void AddSectionLabel(string text, float y)
			{
				UIText label = new UIText(text, 0.72f)
				{
					TextColor = PanelChrome.Tone(new Color(148, 163, 184)),
					Top = { Pixels = y },
					Left = { Pixels = Margin }
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

				// Chassis fill, the NieR texture, and the scheme's border and corner brackets (the same as every other panel).
				spriteBatch.Draw(pixel, rect, PanelChrome.Tone(new Color(10, 16, 28, 250)));
				PanelChrome.DrawTexture(spriteBatch, rect);

				Color border = PanelChrome.PanelBorder;
				spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 1), border);
				spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), border);
				spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, 1, rect.Height), border);
				spriteBatch.Draw(pixel, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), border);
				PanelChrome.DrawCorners(spriteBatch, new Rectangle(rect.X + 1, rect.Y + 1, rect.Width - 2, rect.Height - 2), PanelChrome.Accent * 0.70f);

				// Divider under the title
				spriteBatch.Draw(pixel, new Rectangle(rect.X + (int)Margin, rect.Y + 31, rect.Width - (int)Margin * 2, 1), PanelChrome.Divider);
			}
		}
	}
}
