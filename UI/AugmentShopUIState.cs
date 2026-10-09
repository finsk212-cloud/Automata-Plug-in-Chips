using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;
using Terraria.UI.Chat;

namespace Augments
{
	// The vendor's dedicated chip storage panel: "Buy Back" and "Dismantle".
	public class AugmentShopUIState : UIState
	{
		private ShopBackPanel backPanel;
		private ColoredLabel essenceLabel;
		private UndoReforgeBar undoReforgeBar;
		private UIList buyBackList;
		private UIList removeList;
		private UIText subtitleText;
		private ShopTabButton storageTabBtn;
		private ShopTabButton loadoutTabBtn;
		private UIElement loadoutView;
		private UIText lockStatusText;
		private UIText slotInfoText;
		private readonly LoadoutCard[] loadoutCards = new LoadoutCard[AugmentPlayer.MaxLoadouts];
		private readonly List<UIElement> storageElements = new List<UIElement>();
		private bool showLoadouts;

		private readonly UIParticleSystem shopParticles = new UIParticleSystem(70);

		private const float PanelWidth = 800f;
		private const float PanelHeight = 520f;
		private const float ListsTop = 126f;

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);

			if (backPanel != null && backPanel.ContainsPoint(Main.MouseScreen))
			{
				Main.LocalPlayer.mouseInterface = true;
			}

			shopParticles.Update();

			// Keep the swap-lock readout live while the Loadouts tab is open.
			if (showLoadouts && Main.GameUpdateCount % 20 == 0 && Main.LocalPlayer != null)
				RefreshLoadoutView(Main.LocalPlayer.GetModPlayer<AugmentPlayer>());

			if (backPanel != null && Main.rand.NextBool(8))
			{
				CalculatedStyle dims = backPanel.GetDimensions();
				if (dims.Width > 0)
				{
					Rectangle rect = new Rectangle((int)dims.X, (int)dims.Y, (int)dims.Width, (int)dims.Height);
					Color emberCol = Main.rand.NextBool(3) ? new Color(0, 220, 255) : new Color(60, 140, 240);
					shopParticles.SpawnAmbient(rect, emberCol, 1.8f);
				}
			}
		}

		public override void Draw(SpriteBatch spriteBatch)
		{
			base.Draw(spriteBatch);
			shopParticles.Draw(spriteBatch);
		}

		public override void OnInitialize()
		{
			backPanel = new ShopBackPanel();
			backPanel.Width.Set(PanelWidth, 0f);
			backPanel.Height.Set(PanelHeight, 0f);
			backPanel.HAlign = 0.5f;
			backPanel.VAlign = 0.5f;
			backPanel.SetPadding(0f);
			backPanel.BackgroundColor = new Color(10, 16, 28, 250);
			backPanel.BorderColor = new Color(30, 41, 59);

			// Close Button in top-right corner
			var closeButton = new CloseButton();
			closeButton.Width.Set(24f, 0f);
			closeButton.Height.Set(24f, 0f);
			closeButton.Top.Set(10f, 0f);
			closeButton.Left.Set(762f, 0f);
			closeButton.Clicked += () => ModContent.GetInstance<AugmentUISystem>().HideShop();
			backPanel.Append(closeButton);

			// Currency Badge (pure unboxed typography in top-right)
			var essenceBadge = new EssenceBadge();
			essenceBadge.Width.Set(210f, 0f);
			essenceBadge.Height.Set(22f, 0f);
			essenceBadge.Left.Set(544f, 0f);
			essenceBadge.Top.Set(12f, 0f);

			essenceLabel = new ColoredLabel("[c/D4B872:Machine Cores:] [c/68C2D8:0]", 0.82f);
			essenceBadge.Append(essenceLabel);
			backPanel.Append(essenceBadge);

			// Title Header
			UIText title = new UIText("Plugin Storage", 1.15f)
			{
				HAlign = 0.5f,
				TextColor = new Color(248, 250, 252)
			};
			title.Top.Set(18f, 0f);
			backPanel.Append(title);

			// Subtitle
			subtitleText = new UIText("Mistress 2B's Archive — Stash or dismantle equipped plugins, re-acquire archived ones", 0.76f)
			{
				HAlign = 0.5f,
				TextColor = new Color(148, 163, 184)
			};
			subtitleText.Top.Set(42f, 0f);
			backPanel.Append(subtitleText);

			// Tabs (top-left)
			storageTabBtn = new ShopTabButton("Storage");
			storageTabBtn.Left.Set(18f, 0f);
			storageTabBtn.Top.Set(12f, 0f);
			storageTabBtn.Width.Set(80f, 0f);
			storageTabBtn.Height.Set(24f, 0f);
			storageTabBtn.Clicked += () => SetTab(false);
			backPanel.Append(storageTabBtn);

			loadoutTabBtn = new ShopTabButton("Loadouts");
			loadoutTabBtn.Left.Set(104f, 0f);
			loadoutTabBtn.Top.Set(12f, 0f);
			loadoutTabBtn.Width.Set(80f, 0f);
			loadoutTabBtn.Height.Set(24f, 0f);
			loadoutTabBtn.Clicked += () => SetTab(true);
			backPanel.Append(loadoutTabBtn);

			// Optional Undo Reforge Bar (spans 18f to 782f)
			undoReforgeBar = new UndoReforgeBar(TryUndoReforge);
			undoReforgeBar.Left.Set(18f, 0f);
			undoReforgeBar.Width.Set(764f, 0f);
			undoReforgeBar.Top.Set(71f, 0f);
			undoReforgeBar.Height.Set(22f, 0f);

			// Column Headers (symmetrical at Left = 18f and Left = 410f)
			UIText buyBackHeader = new UIText("Stash & Archive", 0.85f)
			{
				HAlign = 0f,
				TextColor = new Color(148, 210, 255)
			};
			buyBackHeader.Left.Set(18f, 0f);
			buyBackHeader.Top.Set(98f, 0f);
			backPanel.Append(buyBackHeader);
			storageElements.Add(buyBackHeader);

			UIText removeHeader = new UIText("Equipped (Dismantle)", 0.85f)
			{
				HAlign = 0f,
				TextColor = new Color(251, 146, 60)
			};
			removeHeader.Left.Set(410f, 0f);
			removeHeader.Top.Set(98f, 0f);
			backPanel.Append(removeHeader);
			storageElements.Add(removeHeader);

			// Left List: Buy Back (Left = 18f, Width = 356f, Scrollbar = 378f)
			buyBackList = new UIList();
			buyBackList.ManualSortMethod = _ => { };
			buyBackList.Top.Set(ListsTop, 0f);
			buyBackList.Left.Set(18f, 0f);
			buyBackList.Width.Set(356f, 0f);
			buyBackList.Height.Set(-(ListsTop + 14f), 1f);
			buyBackList.ListPadding = 6f;
			backPanel.Append(buyBackList);
			storageElements.Add(buyBackList);

			ShopScrollbar buyBackScrollbar = new ShopScrollbar();
			buyBackScrollbar.Top.Set(ListsTop, 0f);
			buyBackScrollbar.Height.Set(-(ListsTop + 14f), 1f);
			buyBackScrollbar.Left.Set(378f, 0f);
			buyBackScrollbar.Width.Set(8f, 0f);
			buyBackList.SetScrollbar(buyBackScrollbar);
			backPanel.Append(buyBackScrollbar);
			storageElements.Add(buyBackScrollbar);

			// Right List: Remove (Left = 410f, Width = 356f, Scrollbar = 770f)
			removeList = new UIList();
			removeList.ManualSortMethod = _ => { };
			removeList.Top.Set(ListsTop, 0f);
			removeList.Left.Set(410f, 0f);
			removeList.Width.Set(356f, 0f);
			removeList.Height.Set(-(ListsTop + 14f), 1f);
			removeList.ListPadding = 6f;
			backPanel.Append(removeList);
			storageElements.Add(removeList);

			ShopScrollbar removeScrollbar = new ShopScrollbar();
			removeScrollbar.Top.Set(ListsTop, 0f);
			removeScrollbar.Height.Set(-(ListsTop + 14f), 1f);
			removeScrollbar.Left.Set(770f, 0f);
			removeScrollbar.Width.Set(8f, 0f);
			removeList.SetScrollbar(removeScrollbar);
			backPanel.Append(removeScrollbar);
			storageElements.Add(removeScrollbar);

			BuildLoadoutView();

			Append(backPanel);
		}

		private void BuildLoadoutView()
		{
			loadoutView = new UIElement();
			loadoutView.Left.Set(18f, 0f);
			loadoutView.Top.Set(78f, 0f);
			loadoutView.Width.Set(764f, 0f);
			loadoutView.Height.Set(420f, 0f);

			lockStatusText = new UIText("", 0.78f) { TextColor = new Color(148, 163, 184) };
			lockStatusText.HAlign = 1f;
			lockStatusText.Top.Set(4f, 0f);
			loadoutView.Append(lockStatusText);

			slotInfoText = new UIText("", 0.78f) { TextColor = new Color(148, 163, 184) };
			slotInfoText.Top.Set(4f, 0f);
			loadoutView.Append(slotInfoText);

			const float cardWidth = 244f;
			for (int i = 0; i < AugmentPlayer.MaxLoadouts; i++)
			{
				int slot = i;
				var card = new LoadoutCard(slot, () => SaveLoadoutSlot(slot), () => ApplyLoadoutSlot(slot), () => BuyLoadoutSlot(slot));
				card.Left.Set(i * (cardWidth + 16f), 0f);
				card.Top.Set(30f, 0f);
				card.Width.Set(cardWidth, 0f);
				card.Height.Set(292f, 0f);
				loadoutCards[i] = card;
				loadoutView.Append(card);
			}

			var help = new UIText("Swaps use your equipped plugins and your stash. Locked during boss fights and briefly after damage.\nArchived plugins are skipped until bought back. Bind hotkeys in Settings > Controls > Mod Controls.", 0.72f)
			{
				TextColor = new Color(110, 124, 150)
			};
			help.Top.Set(340f, 0f);
			loadoutView.Append(help);
		}

		private void SetTab(bool loadouts)
		{
			showLoadouts = loadouts;
			foreach (var e in storageElements)
			{
				if (loadouts && backPanel.HasChild(e))
					backPanel.RemoveChild(e);
				else if (!loadouts && !backPanel.HasChild(e))
					backPanel.Append(e);
			}
			if (loadouts && !backPanel.HasChild(loadoutView))
				backPanel.Append(loadoutView);
			else if (!loadouts && backPanel.HasChild(loadoutView))
				backPanel.RemoveChild(loadoutView);

			backPanel.ShowColumns = !loadouts;
			subtitleText.SetText(loadouts
				? "Mistress 2B's Archive — Save, unlock and apply plugin loadouts"
				: "Mistress 2B's Archive — Stash or dismantle equipped plugins, re-acquire archived ones");
			Refresh();
		}

		public void ResetTab()
		{
			if (backPanel != null && showLoadouts)
				SetTab(false);
		}

		private void SaveLoadoutSlot(int slot)
		{
			Main.LocalPlayer.GetModPlayer<AugmentPlayer>().RequestLoadoutOp(LoadoutOp.SaveLoadout, slot);
		}

		private void ApplyLoadoutSlot(int slot)
		{
			var ap = Main.LocalPlayer.GetModPlayer<AugmentPlayer>();
			string reason = ap.GetLoadoutLockReason();
			if (reason != null)
			{
				Main.NewText(reason, 255, 140, 140);
				return;
			}
			ap.RequestLoadoutOp(LoadoutOp.ApplyLoadout, slot);
		}

		private void BuyLoadoutSlot(int slot)
		{
			var player = Main.LocalPlayer;
			int cost = AugmentPlayer.GetLoadoutSlotCost(slot);
			if (player.CountItem(ModContent.ItemType<AugmentEssenceItem>(), cost) < cost)
			{
				Main.NewText("Not enough Machine Cores.", 255, 80, 80);
				return;
			}
			player.GetModPlayer<AugmentPlayer>().RequestLoadoutOp(LoadoutOp.BuyLoadoutSlot, slot);
		}

		private void StashOwned(Augment augment)
		{
			var ap = Main.LocalPlayer.GetModPlayer<AugmentPlayer>();
			string reason = ap.GetLoadoutLockReason(true);
			if (reason != null)
			{
				Main.NewText(reason, 255, 140, 140);
				return;
			}
			ap.RequestLoadoutOp(LoadoutOp.Stash, 0, augment.Id);
		}

		private void InstallStashed(Augment augment)
		{
			var ap = Main.LocalPlayer.GetModPlayer<AugmentPlayer>();
			if (ap.Owned.Count >= AugmentPlayer.MaxOwnedAugments)
			{
				Main.NewText("Plugin slots full.", 255, 80, 80);
				return;
			}
			string reason = ap.GetLoadoutLockReason(true);
			if (reason != null)
			{
				Main.NewText(reason, 255, 140, 140);
				return;
			}
			ap.RequestLoadoutOp(LoadoutOp.Unstash, 0, augment.Id);
		}

		public void Refresh()
		{
			buyBackList.Clear();
			removeList.Clear();

			var player = Main.LocalPlayer;
			var augmentPlayer = player.GetModPlayer<AugmentPlayer>();

			int buyBackCount = 0;
			foreach (var id in augmentPlayer.StashedIds)
			{
				var stashed = AugmentDatabase.GetById(id);
				if (stashed == null)
					continue;

				var stashEntry = new AugmentShopEntry(stashed, "Install (Free)", InstallStashed);
				stashEntry.Width.Set(0f, 1f);
				stashEntry.Height.Set(54f, 0f);
				buyBackList.Add(stashEntry);
				buyBackCount++;
			}
			foreach (var id in augmentPlayer.SoldAugmentIds)
			{
				var augment = AugmentDatabase.GetById(id);
				if (augment == null || augment.Class == AugmentClass.Support)
					continue;

				int buyBackCost = AugmentPlayer.GetBuyBackCost(augment.Rarity);
				var entry = new AugmentShopEntry(augment, $"Buy ({buyBackCost} Core{(buyBackCost > 1 ? "s" : "")})", BuyBack);
				entry.Width.Set(0f, 1f);
				entry.Height.Set(54f, 0f);
				buyBackList.Add(entry);
				buyBackCount++;
			}

			if (buyBackCount == 0)
			{
				var empty = new UIText("Nothing stashed or archived.\nStashed and dismantled plugins appear here.", 0.8f)
				{
					HAlign = 0.5f,
					TextColor = new Color(130, 145, 175)
				};
				empty.Top.Set(40f, 0f);
				buyBackList.Add(empty);
			}

			int removeCount = 0;
			foreach (var augment in augmentPlayer.Owned)
			{
				AugmentShopEntry entry;
				if (augment.IsPermanent)
				{
					entry = new AugmentShopEntry(augment, "Permanent", null);
				}
				else
				{
					int removeRefund = AugmentPlayer.GetRemoveRefund(augment.Rarity);
					string label = removeRefund > 0 ? $"Remove (+{removeRefund} Core{(removeRefund > 1 ? "s" : "")})" : "Remove (Free)";
					entry = new AugmentShopEntry(augment, label, SellOwned, "Stash (Free)", StashOwned);
				}
				entry.Width.Set(0f, 1f);
				entry.Height.Set(54f, 0f);
				removeList.Add(entry);
				removeCount++;
			}

			if (removeCount == 0)
			{
				var empty = new UIText("No plugins currently equipped.", 0.8f)
				{
					HAlign = 0.5f,
					TextColor = new Color(130, 145, 175)
				};
				empty.Top.Set(40f, 0f);
				removeList.Add(empty);
			}

			RefreshEssenceText();
			RefreshUndoReforgeBar();
			RefreshLoadoutView(augmentPlayer);
		}

		private void RefreshLoadoutView(AugmentPlayer ap)
		{
			storageTabBtn.IsActive = !showLoadouts;
			loadoutTabBtn.IsActive = showLoadouts;
			if (!showLoadouts)
				return;

			string reason = ap.GetLoadoutLockReason();
			lockStatusText.SetText("● " + (reason ?? "Swaps ready"));
			lockStatusText.TextColor = reason == null ? new Color(74, 222, 128) : new Color(248, 180, 100);
			slotInfoText.SetText($"Slots unlocked: {ap.LoadoutSlotsUnlocked}/{AugmentPlayer.MaxLoadouts}");
			foreach (var card in loadoutCards)
				card.Bind(ap);
		}

		private void RefreshUndoReforgeBar()
		{
			var augmentPlayer = Main.LocalPlayer.GetModPlayer<AugmentPlayer>();
			bool owns = augmentPlayer.HasAugment("reforgers_patience") && !showLoadouts;
			bool pending = owns && augmentPlayer.HasPendingReforgeUndo;

			if (!owns)
			{
				if (backPanel.HasChild(undoReforgeBar))
					backPanel.RemoveChild(undoReforgeBar);
				undoReforgeBar.SetState(false, false, null, 0);
				return;
			}

			if (!backPanel.HasChild(undoReforgeBar))
				backPanel.Append(undoReforgeBar);

			undoReforgeBar.SetState(true, pending, pending ? augmentPlayer.LastReforgedItem : null, pending ? augmentPlayer.LastReforgeCost : 0);
		}

		private void TryUndoReforge()
		{
			var augmentPlayer = Main.LocalPlayer.GetModPlayer<AugmentPlayer>();
			if (augmentPlayer.TryUndoLastReforge())
				RefreshUndoReforgeBar();
		}

		private void RefreshEssenceText()
		{
			int count = Main.LocalPlayer.CountItem(ModContent.ItemType<AugmentEssenceItem>());
			essenceLabel?.SetText($"[c/D4B872:Machine Cores:] [c/68C2D8:{count:N0}]");
		}

		private void BuyBack(Augment augment)
		{
			var player = Main.LocalPlayer;
			var augmentPlayer = player.GetModPlayer<AugmentPlayer>();

			if (augmentPlayer.Owned.Count >= AugmentPlayer.MaxOwnedAugments)
			{
				Main.NewText("Plugin slots full.", 255, 80, 80);
				return;
			}

			int cost = AugmentPlayer.GetBuyBackCost(augment.Rarity);
			if (player.CountItem(ModContent.ItemType<AugmentEssenceItem>(), cost) < cost)
			{
				Main.NewText("Not enough Machine Cores.", 255, 80, 80);
				return;
			}

			if (Main.netMode == Terraria.ID.NetmodeID.MultiplayerClient)
				AugmentNet.SendVendorBuyBackRequest(augment.Id);
			else if (augmentPlayer.BuyBackSoldAugmentByIdServerAuthoritative(augment.Id))
				Refresh();
		}

		private void SellOwned(Augment augment)
		{
			var player = Main.LocalPlayer;
			var augmentPlayer = player.GetModPlayer<AugmentPlayer>();
			if (Main.netMode == Terraria.ID.NetmodeID.MultiplayerClient)
				AugmentNet.SendVendorSellRequest(augment.Id);
			else if (augmentPlayer.SellAugmentByIdServerAuthoritative(augment.Id))
				Refresh();
		}

		// Shared flat button chrome: chassis fill, hairline, 1px border, centered label.
		private static void DrawFlatButton(SpriteBatch spriteBatch, Rectangle rect, string label, bool hovered, bool active, bool enabled, Color accent)
		{
			Texture2D pixel = TextureAssets.MagicPixel.Value;
			Color bg = !enabled ? new Color(10, 16, 28) * 0.95f
				: active ? new Color(18, 34, 62)
				: hovered ? new Color(18, 32, 56) * 0.98f : new Color(12, 22, 40) * 0.94f;
			Color border = !enabled ? new Color(30, 42, 60)
				: active ? accent
				: hovered ? accent * 0.9f : new Color(30, 58, 92);
			Color textColor = !enabled ? new Color(100, 116, 139) : (hovered || active) ? Color.White : new Color(220, 235, 250);

			if (hovered && enabled)
				spriteBatch.Draw(pixel, new Rectangle(rect.X - 1, rect.Y - 1, rect.Width + 2, rect.Height + 2), border * 0.12f);

			spriteBatch.Draw(pixel, rect, bg);
			Color hair = Color.White * (hovered && enabled ? 0.08f : 0.04f);
			spriteBatch.Draw(pixel, new Rectangle(rect.X + 1, rect.Y + 1, rect.Width - 2, 1), hair);
			spriteBatch.Draw(pixel, new Rectangle(rect.X + 1, rect.Bottom - 2, rect.Width - 2, 1), hair);
			spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 1), border);
			spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), border);
			spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, 1, rect.Height), border);
			spriteBatch.Draw(pixel, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), border);

			var font = FontAssets.MouseText.Value;
			Vector2 scale = new Vector2(0.72f);
			Vector2 size = ChatManager.GetStringSize(font, label, scale);
			Vector2 pos = new Vector2(rect.X + (rect.Width - size.X) * 0.5f, rect.Y + (rect.Height - size.Y) * 0.5f + 5f);
			ChatManager.DrawColorCodedStringWithShadow(spriteBatch, font, label, pos, textColor, 0f, Vector2.Zero, scale);
		}

		private class ShopTabButton : UIElement
		{
			public event Action Clicked;
			public bool IsActive;
			private readonly string label;
			private bool isHovered;

			public ShopTabButton(string label)
			{
				this.label = label;
			}

			public override void LeftClick(UIMouseEvent evt)
			{
				base.LeftClick(evt);
				SoundEngine.PlaySound(SoundID.MenuTick);
				Clicked?.Invoke();
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

			protected override void DrawSelf(SpriteBatch spriteBatch)
			{
				CalculatedStyle d = GetDimensions();
				DrawFlatButton(spriteBatch, new Rectangle((int)d.X, (int)d.Y, (int)d.Width, (int)d.Height), label, isHovered, IsActive, true, new Color(56, 189, 248));
			}
		}

		private class ShopButton : UIElement
		{
			public event Action Clicked;
			public string Label = "";
			public bool Enabled = true;
			public Color Accent = new Color(56, 189, 248);
			private bool isHovered;

			public override void LeftClick(UIMouseEvent evt)
			{
				base.LeftClick(evt);
				if (!Enabled)
					return;
				SoundEngine.PlaySound(SoundID.MenuTick);
				Clicked?.Invoke();
			}

			public override void MouseOver(UIMouseEvent evt)
			{
				base.MouseOver(evt);
				isHovered = true;
				if (Enabled)
					SoundEngine.PlaySound(SoundID.MenuTick);
			}

			public override void MouseOut(UIMouseEvent evt)
			{
				base.MouseOut(evt);
				isHovered = false;
			}

			protected override void DrawSelf(SpriteBatch spriteBatch)
			{
				CalculatedStyle d = GetDimensions();
				DrawFlatButton(spriteBatch, new Rectangle((int)d.X, (int)d.Y, (int)d.Width, (int)d.Height), Label, isHovered, false, Enabled, Accent);
			}
		}

		// One loadout slot: title, saved chips, and Save / Apply / Unlock buttons.
		private class LoadoutCard : UIElement
		{
			private readonly int slot;
			private readonly ShopButton saveBtn;
			private readonly ShopButton applyBtn;
			private readonly ShopButton buyBtn;
			private bool unlocked;
			private bool isActive;
			private IReadOnlyList<string> chipIds = new List<string>();
			private HashSet<string> available = new HashSet<string>();
			private HashSet<string> equipped = new HashSet<string>();

			public LoadoutCard(int slot, Action onSave, Action onApply, Action onBuy)
			{
				this.slot = slot;
				SetPadding(0f);

				saveBtn = new ShopButton { Label = "Save current setup" };
				saveBtn.Left.Set(12f, 0f);
				saveBtn.Top.Set(206f, 0f);
				saveBtn.Width.Set(-24f, 1f);
				saveBtn.Height.Set(30f, 0f);
				saveBtn.Clicked += onSave;
				Append(saveBtn);

				applyBtn = new ShopButton { Label = "Apply", Accent = new Color(74, 222, 128) };
				applyBtn.Left.Set(12f, 0f);
				applyBtn.Top.Set(244f, 0f);
				applyBtn.Width.Set(-24f, 1f);
				applyBtn.Height.Set(30f, 0f);
				applyBtn.Clicked += onApply;
				Append(applyBtn);

				buyBtn = new ShopButton { Label = "Unlock", Accent = new Color(250, 204, 21) };
				buyBtn.Left.Set(12f, 0f);
				buyBtn.Top.Set(244f, 0f);
				buyBtn.Width.Set(-24f, 1f);
				buyBtn.Height.Set(30f, 0f);
				buyBtn.Clicked += onBuy;
			}

			public void Bind(AugmentPlayer ap)
			{
				unlocked = slot < ap.LoadoutSlotsUnlocked;
				chipIds = ap.GetLoadout(slot);
				isActive = unlocked && chipIds.Count > 0 && ap.ActiveLoadout == slot;
				equipped = new HashSet<string>(ap.OwnedIds);
				available = new HashSet<string>(ap.OwnedIds);
				available.UnionWith(ap.StashedIds);

				if (unlocked)
				{
					if (!HasChild(saveBtn)) Append(saveBtn);
					if (!HasChild(applyBtn)) Append(applyBtn);
					if (HasChild(buyBtn)) RemoveChild(buyBtn);
					applyBtn.Enabled = chipIds.Count > 0;
				}
				else
				{
					if (HasChild(saveBtn)) RemoveChild(saveBtn);
					if (HasChild(applyBtn)) RemoveChild(applyBtn);
					if (!HasChild(buyBtn)) Append(buyBtn);

					int cost = AugmentPlayer.GetLoadoutSlotCost(slot);
					bool next = slot == ap.LoadoutSlotsUnlocked;
					buyBtn.Enabled = next;
					buyBtn.Label = next ? $"Unlock ({cost} Cores)" : "Unlock previous slot first";
				}
			}

			protected override void DrawSelf(SpriteBatch spriteBatch)
			{
				CalculatedStyle d = GetDimensions();
				var rect = new Rectangle((int)d.X, (int)d.Y, (int)d.Width, (int)d.Height);
				Texture2D pixel = TextureAssets.MagicPixel.Value;
				Color accent = isActive ? new Color(56, 189, 248) : unlocked ? new Color(30, 41, 59) : new Color(120, 48, 56);

				spriteBatch.Draw(pixel, rect, new Color(10, 16, 28) * 0.94f);
				Color hair = Color.White * 0.04f;
				spriteBatch.Draw(pixel, new Rectangle(rect.X + 1, rect.Y + 1, rect.Width - 2, 1), hair);
				spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 1), accent);
				spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), accent);
				spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, 1, rect.Height), accent);
				spriteBatch.Draw(pixel, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), accent);

				var font = FontAssets.MouseText.Value;
				ChatManager.DrawColorCodedStringWithShadow(spriteBatch, font, $"Loadout {slot + 1}", new Vector2(rect.X + 12f, rect.Y + 10f), new Color(248, 250, 252), 0f, Vector2.Zero, new Vector2(0.9f));

				string status = !unlocked ? "Locked" : isActive ? "Active" : chipIds.Count == 0 ? "Empty" : $"{chipIds.Count} plugin{(chipIds.Count == 1 ? "" : "s")}";
				Color statusCol = !unlocked ? new Color(250, 204, 21) : isActive ? new Color(56, 189, 248) : new Color(148, 163, 184);
				ChatManager.DrawColorCodedStringWithShadow(spriteBatch, font, status, new Vector2(rect.X + 12f, rect.Y + 32f), statusCol, 0f, Vector2.Zero, new Vector2(0.72f));
				spriteBatch.Draw(pixel, new Rectangle(rect.X + 12, rect.Y + 56, rect.Width - 24, 1), new Color(30, 41, 59));

				// Five slot rows show the capacity; filled rows carry a rarity-colored edge.
				for (int row = 0; row < 5; row++)
				{
					var r = new Rectangle(rect.X + 12, rect.Y + 66 + row * 26, rect.Width - 24, 22);
					Augment a = unlocked && row < chipIds.Count ? AugmentDatabase.GetById(chipIds[row]) : null;
					if (a == null)
					{
						spriteBatch.Draw(pixel, r, new Color(8, 13, 24) * (unlocked ? 0.9f : 0.6f));
						Color edge = new Color(22, 32, 50) * (unlocked ? 1f : 0.6f);
						spriteBatch.Draw(pixel, new Rectangle(r.X, r.Y, r.Width, 1), edge);
						spriteBatch.Draw(pixel, new Rectangle(r.X, r.Bottom - 1, r.Width, 1), edge);
						spriteBatch.Draw(pixel, new Rectangle(r.X, r.Y, 1, r.Height), edge);
						spriteBatch.Draw(pixel, new Rectangle(r.Right - 1, r.Y, 1, r.Height), edge);
						ChatManager.DrawColorCodedStringWithShadow(spriteBatch, font, (row + 1).ToString(), new Vector2(r.X + 9f, r.Y + 5f), new Color(55, 68, 92), 0f, Vector2.Zero, new Vector2(0.68f));
						continue;
					}

					bool here = available.Contains(chipIds[row]);
					Color rc = a.Rarity == AugmentRarity.Common ? new Color(225, 230, 240) : AugmentListEntry.RarityColor(a.Rarity);
					spriteBatch.Draw(pixel, r, new Color(16, 26, 44) * 0.95f);
					spriteBatch.Draw(pixel, new Rectangle(r.X, r.Y, 3, r.Height), here ? rc : new Color(70, 82, 104));
					Color col = here ? rc : new Color(100, 116, 139);
					string tag = equipped.Contains(chipIds[row]) ? "" : here ? "  (stash)" : "  (archived)";
					ChatManager.DrawColorCodedStringWithShadow(spriteBatch, font, a.DisplayName + tag, new Vector2(r.X + 11f, r.Y + 5f), col, 0f, Vector2.Zero, new Vector2(0.72f));
				}

				if (!unlocked)
				{
					Vector2 sz = ChatManager.GetStringSize(font, "Locked slot", new Vector2(0.8f));
					ChatManager.DrawColorCodedStringWithShadow(spriteBatch, font, "Locked slot", new Vector2(rect.X + (rect.Width - sz.X) * 0.5f, rect.Y + 66f + 52f - sz.Y * 0.5f + 4f), new Color(250, 204, 21) * 0.8f, 0f, Vector2.Zero, new Vector2(0.8f));
				}
				else if (chipIds.Count == 0)
				{
					Vector2 sz = ChatManager.GetStringSize(font, "Nothing saved yet", new Vector2(0.74f));
					ChatManager.DrawColorCodedStringWithShadow(spriteBatch, font, "Nothing saved yet", new Vector2(rect.X + (rect.Width - sz.X) * 0.5f, rect.Y + 66f + 52f - sz.Y * 0.5f + 4f), new Color(130, 145, 175), 0f, Vector2.Zero, new Vector2(0.74f));
				}
			}
		}

		private class ShopBackPanel : UIPanel
		{
			public bool ShowColumns = true;

			protected override void DrawSelf(SpriteBatch spriteBatch)
			{
				base.DrawSelf(spriteBatch);

				CalculatedStyle dims = GetDimensions();
				Texture2D pixel = TextureAssets.MagicPixel.Value;
				Color divColor = new Color(30, 41, 59) * 0.90f;
				Rectangle bRect = dims.ToRectangle();

				// Inner 1px hairline highlight
				Color innerHairline = Color.White * 0.05f;
				spriteBatch.Draw(pixel, new Rectangle(bRect.X + 1, bRect.Y + 1, bRect.Width - 2, 1), innerHairline);
				spriteBatch.Draw(pixel, new Rectangle(bRect.X + 1, bRect.Bottom - 2, bRect.Width - 2, 1), innerHairline);
				spriteBatch.Draw(pixel, new Rectangle(bRect.X + 1, bRect.Y + 1, 1, bRect.Height - 2), innerHairline);
				spriteBatch.Draw(pixel, new Rectangle(bRect.Right - 2, bRect.Y + 1, 1, bRect.Height - 2), innerHairline);

				// Flush corner accent notches
				Color cornerAccent = new Color(56, 189, 248) * 0.70f;
				const int clen = 5;
				const int cthk = 2;
				spriteBatch.Draw(pixel, new Rectangle(bRect.X + 1, bRect.Y + 1, clen, cthk), cornerAccent);
				spriteBatch.Draw(pixel, new Rectangle(bRect.X + 1, bRect.Y + 1, cthk, clen), cornerAccent);
				spriteBatch.Draw(pixel, new Rectangle(bRect.Right - clen - 1, bRect.Y + 1, clen, cthk), cornerAccent);
				spriteBatch.Draw(pixel, new Rectangle(bRect.Right - cthk - 1, bRect.Y + 1, cthk, clen), cornerAccent);
				spriteBatch.Draw(pixel, new Rectangle(bRect.X + 1, bRect.Bottom - cthk - 1, clen, cthk), cornerAccent);
				spriteBatch.Draw(pixel, new Rectangle(bRect.X + 1, bRect.Bottom - clen - 1, cthk, clen), cornerAccent);
				spriteBatch.Draw(pixel, new Rectangle(bRect.Right - clen - 1, bRect.Bottom - cthk - 1, clen, cthk), cornerAccent);
				spriteBatch.Draw(pixel, new Rectangle(bRect.Right - cthk - 1, bRect.Bottom - clen - 1, cthk, clen), cornerAccent);

				// Faint radar watermark
				Vector2 center = new Vector2(bRect.X + bRect.Width * 0.5f, bRect.Y + bRect.Height * 0.58f);
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

				// Header horizontal divider under subtitle
				int divY1 = (int)dims.Y + 66;
				spriteBatch.Draw(pixel, new Rectangle((int)dims.X + 18, divY1, (int)dims.Width - 36, 1), divColor);
				int nodeX = bRect.X + bRect.Width / 2;
				spriteBatch.Draw(pixel, new Rectangle(nodeX - 1, divY1 - 1, 3, 3), new Color(56, 189, 248) * 0.85f);
				spriteBatch.Draw(pixel, new Rectangle(nodeX, divY1, 1, 1), Color.White * 0.9f);

				if (!ShowColumns)
					return;

				// Column headers horizontal divider under headers
				int divY2 = (int)dims.Y + 120;
				spriteBatch.Draw(pixel, new Rectangle((int)dims.X + 18, divY2, (int)dims.Width - 36, 1), divColor);

				// Center vertical divider between columns
				int midX = (int)dims.X + (int)(dims.Width * 0.5f);
				int listStartY = divY1 + 1;
				int listHeight = (int)dims.Height - (divY1 - (int)dims.Y) - 16;
				spriteBatch.Draw(pixel, new Rectangle(midX, listStartY, 1, listHeight), divColor);
			}
		}

		// Modern scrollbar that only renders when the list has enough items to scroll
		private class ShopScrollbar : UIScrollbar
		{
			public ShopScrollbar()
			{
				Width.Set(8f, 0f);
			}

			protected override void DrawSelf(SpriteBatch spriteBatch)
			{
				if (!CanScroll)
					return;

				CalculatedStyle dims = GetDimensions();
				Rectangle trackRect = new Rectangle((int)dims.X, (int)dims.Y, (int)dims.Width, (int)dims.Height);

				// Dark sleek cybernetic track backing
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, trackRect, new Color(10, 16, 32) * 0.92f);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(trackRect.X, trackRect.Y, 1, trackRect.Height), new Color(34, 48, 86) * 0.6f);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(trackRect.Right - 1, trackRect.Y, 1, trackRect.Height), new Color(34, 48, 86) * 0.6f);

				base.DrawSelf(spriteBatch);
			}
		}

		private class EssenceBadge : UIElement
		{
			public EssenceBadge()
			{
				SetPadding(0f);
			}

			protected override void DrawSelf(SpriteBatch spriteBatch)
			{
				// Pure unboxed typography: no borders, fills, or corner ticks
				base.DrawSelf(spriteBatch);
			}
		}

		private class CloseButton : UIElement
		{
			public event Action Clicked;
			private bool isHovered;

			public override void LeftClick(UIMouseEvent evt)
			{
				base.LeftClick(evt);
				SoundEngine.PlaySound(SoundID.MenuClose);
				Clicked?.Invoke();
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

			protected override void DrawSelf(SpriteBatch spriteBatch)
			{
				CalculatedStyle dims = GetDimensions();
				var rect = new Rectangle((int)dims.X, (int)dims.Y, (int)dims.Width, (int)dims.Height);
				Texture2D pixel = TextureAssets.MagicPixel.Value;

				if (isHovered)
				{
					// Subtle soft red hover wash, NO harsh outline box
					spriteBatch.Draw(pixel, rect, new Color(239, 68, 68, 35));
				}

				var font = FontAssets.MouseText.Value;
				Vector2 xSize = ChatManager.GetStringSize(font, "✕", new Vector2(0.85f));
				Vector2 xPos = new Vector2(
					rect.X + (rect.Width - xSize.X) * 0.5f,
					rect.Y + (rect.Height - xSize.Y) * 0.5f + 1f
				);
				ChatManager.DrawColorCodedStringWithShadow(spriteBatch, font, "✕", xPos, isHovered ? new Color(248, 113, 113) : new Color(148, 163, 184), 0f, Vector2.Zero, new Vector2(0.85f));
			}
		}

		private class UndoReforgeBar : UIElement
		{
			private readonly Action onUndo;
			private string text = "";
			private bool isHovered;

			private bool owns;
			private bool enabled;

			public UndoReforgeBar(Action onUndo)
			{
				this.onUndo = onUndo;
			}

			public void SetState(bool owns, bool pending, Item item, int cost)
			{
				this.owns = owns;
				Width.Set(-28f, owns ? 1f : 0f);
				Height.Set(owns ? 26f : 0f, 0f);

				if (!owns)
					return;

				enabled = pending;
				text = pending
					? $"Undo Reforge: {item.Name} (+{Main.ValueToCoins(cost)})"
					: "Undo Reforge: nothing to undo";
			}

			public override void LeftClick(UIMouseEvent evt)
			{
				if (!owns)
					return;

				base.LeftClick(evt);
				if (enabled)
				{
					SoundEngine.PlaySound(SoundID.Item4);
					onUndo();
				}
			}

			public override void MouseOver(UIMouseEvent evt)
			{
				base.MouseOver(evt);
				if (owns && enabled)
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

			protected override void DrawSelf(SpriteBatch spriteBatch)
			{
				if (!owns)
					return;

				CalculatedStyle dims = GetDimensions();
				var rect = new Rectangle((int)dims.X, (int)dims.Y, (int)dims.Width, (int)dims.Height);
				Texture2D pixel = TextureAssets.MagicPixel.Value;

				Color bg;
				Color border;
				Color textColor;

				if (!enabled)
				{
					bg = new Color(10, 16, 28) * 0.95f;
					border = new Color(30, 42, 60);
					textColor = new Color(120, 130, 150);
				}
				else
				{
					bg = isHovered ? new Color(18, 32, 56) * 0.98f : new Color(12, 22, 40) * 0.94f;
					border = isHovered ? new Color(56, 189, 248) : new Color(30, 58, 92);
					textColor = isHovered ? Color.White : new Color(220, 235, 250);
				}

				if (isHovered && enabled)
				{
					spriteBatch.Draw(pixel, new Rectangle(rect.X - 1, rect.Y - 1, rect.Width + 2, rect.Height + 2), border * 0.12f);
				}

				spriteBatch.Draw(pixel, rect, bg);

				Color innerHairline = Color.White * (isHovered ? 0.08f : 0.04f);
				spriteBatch.Draw(pixel, new Rectangle(rect.X + 1, rect.Y + 1, rect.Width - 2, 1), innerHairline);
				spriteBatch.Draw(pixel, new Rectangle(rect.X + 1, rect.Bottom - 2, rect.Width - 2, 1), innerHairline);
				spriteBatch.Draw(pixel, new Rectangle(rect.X + 1, rect.Y + 1, 1, rect.Height - 2), innerHairline);
				spriteBatch.Draw(pixel, new Rectangle(rect.Right - 2, rect.Y + 1, 1, rect.Height - 2), innerHairline);

				// 1px Outer Border (clean, no corner ticks)
				spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 1), border);
				spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), border);
				spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, 1, rect.Height), border);
				spriteBatch.Draw(pixel, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), border);

				var font = FontAssets.MouseText.Value;
				Vector2 textSize = ChatManager.GetStringSize(font, text, new Vector2(0.75f));
				Vector2 textPos = new Vector2(
					rect.X + (rect.Width - textSize.X) * 0.5f,
					rect.Y + (rect.Height - textSize.Y) * 0.5f + 1f
				);
				ChatManager.DrawColorCodedStringWithShadow(spriteBatch, font, text, textPos, textColor, 0f, Vector2.Zero, new Vector2(0.75f));
			}
		}
	}
}
