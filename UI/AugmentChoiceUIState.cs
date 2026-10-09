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
	public class AugmentChoiceUIState : UIState
	{
		private UIPanel backPanel;
		private readonly List<AugmentChoiceCard> cards = new List<AugmentChoiceCard>();
		private readonly HashSet<string> currentChoiceIds = new HashSet<string>();
		private RerollButton rerollButton;
		private RerollButton skipButton;
		private UIText capNoticeText;
		private ModalButton minimizeButton;
		private RestoreIcon restoreIcon;
		private bool isMinimized;
		public bool IsMinimized => isMinimized;

		private static readonly SoundStyle ChipInstallSound = new SoundStyle("Augments/Sounds/ChipInstall")
		{
			Volume = 0.90f,
			PitchVariance = 0.05f
		};

		// Rarity tier this popup's cards were rolled at - a reroll must stay
		// on this same tier, not re-roll a fresh (possibly different) one.
		private AugmentRarity currentRarity;

		// Tracks how many times reroll has been used for this popup:
		// 0 = first reroll (free)
		// 1 = second reroll (costs 4 Machine Cores)
		// >= 2 = cannot reroll anymore
		private int rerollCount;
		private bool rerollPending;
		private bool networkReward;

		// --- Keystone confirmation overlay ---
		// Appended/removed from `this` (the root UIState, not backPanel) so it
		// draws on top of and blocks clicks to everything behind it - cards,
		// reroll button, all of it - without needing a whole new interface
		// layer. It's still just part of the same "Augments: Choice UI" layer
		// AugmentUISystem already draws this state through.
		private UIPanel confirmOverlay;
		private UIText confirmMessageText;
		private Augment pendingKeystone;
		private bool pendingSkipConfirm;

		private const float PanelWidth = 880f;
		private const float CardWidth = 268f;
		private const float CardSpacing = 20f;
		private const float CardsTop = 104f;
		private const float BottomMargin = 18f;

		// Dedicated strip below the cards for the reroll and skip buttons
		private const float RerollGap = 16f;
		private const float RerollButtonHeight = 32f;

		private const float ConfirmBoxWidth = 520f;
		private const float ConfirmBoxHeight = 260f;
		private const float ConfirmButtonWidth = 180f;
		private const float ConfirmButtonHeight = 36f;

		public override void OnInitialize()
		{
			backPanel = new ChoiceBackPanel();
			backPanel.Width.Set(PanelWidth, 0f);
			backPanel.Height.Set(CardsTop + AugmentChoiceCard.MinCardHeight + RerollGap + RerollButtonHeight + BottomMargin, 0f);
			backPanel.HAlign = 0.5f;
			backPanel.VAlign = 0.5f;
			backPanel.SetPadding(0f);
			backPanel.BackgroundColor = new Color(10, 16, 28, 250);
			backPanel.BorderColor = new Color(30, 41, 59);

			UIText titleText = new UIText("CHOOSE A PLUGIN", 1.22f)
			{
				HAlign = 0.5f,
				TextColor = new Color(248, 250, 252)
			};
			titleText.Top.Set(16f, 0f);
			backPanel.Append(titleText);

			UIText subtitle = new UIText("Select a plugin to install into your neural frame", 0.76f)
			{
				HAlign = 0.5f,
				TextColor = new Color(148, 163, 184)
			};
			subtitle.Top.Set(44f, 0f);
			backPanel.Append(subtitle);

			capNoticeText = new UIText("", 0.76f)
			{
				HAlign = 0.5f,
				TextColor = new Color(248, 113, 113)
			};
			capNoticeText.Top.Set(68f, 0f);
			backPanel.Append(capNoticeText);

			// Minimize Button (ends at 862px, exact 18px buffer from right edge)
			minimizeButton = new ModalButton("-", new Color(10, 16, 28, 240), new Color(18, 30, 52, 250), new Color(56, 189, 248));
			minimizeButton.Width.Set(24f, 0f);
			minimizeButton.Height.Set(24f, 0f);
			minimizeButton.Left.Set(838f, 0f);
			minimizeButton.Top.Set(10f, 0f);
			minimizeButton.Clicked += HandleMinimizeClicked;
			backPanel.Append(minimizeButton);

			// Reroll and Skip sit side by side on the same row, centered as a pair
			float rerollRowTop = CardsTop + AugmentChoiceCard.MinCardHeight + RerollGap;
			const float buttonGap = 16f;
			const float rerollWidth = 200f;
			const float skipWidth = 140f;
			float pairWidth = rerollWidth + buttonGap + skipWidth;

			rerollButton = new RerollButton();
			rerollButton.Width.Set(rerollWidth, 0f);
			rerollButton.Height.Set(RerollButtonHeight, 0f);
			rerollButton.Left.Set(-pairWidth / 2f, 0.5f);
			rerollButton.Top.Set(rerollRowTop, 0f);
			rerollButton.Clicked += HandleRerollClicked;
			backPanel.Append(rerollButton);

			skipButton = new RerollButton(new Color(36, 14, 18, 240), new Color(52, 20, 26, 250), new Color(248, 113, 113));
			skipButton.Width.Set(skipWidth, 0f);
			skipButton.Height.Set(RerollButtonHeight, 0f);
			skipButton.Left.Set(-pairWidth / 2f + rerollWidth + buttonGap, 0.5f);
			skipButton.Top.Set(rerollRowTop, 0f);
			skipButton.SetEnabled(true, "Skip");
			skipButton.Clicked += HandleSkipClicked;
			backPanel.Append(skipButton);

			Append(backPanel);

			BuildConfirmOverlay();
			BuildRestoreIcon();
		}

		// Built once and kept around, but only appended to the root UIState
		// while the panel is minimized - the underlying choices/reroll state
		// live on regardless of whether backPanel or restoreIcon is showing.
		private void BuildRestoreIcon()
		{
			restoreIcon = new RestoreIcon();
			restoreIcon.Width.Set(48f, 0f);
			restoreIcon.Height.Set(48f, 0f);
			restoreIcon.HAlign = 0.5f;
			restoreIcon.VAlign = 0.85f;
			restoreIcon.Clicked += HandleRestoreClicked;
		}

		// Built once and kept around, but only appended to the root UIState
		// (and so only drawn/clickable) while a Keystone pick is actually
		// pending confirmation.
		private void BuildConfirmOverlay()
		{
			confirmOverlay = new UIPanel();
			confirmOverlay.Width.Set(0f, 1f);
			confirmOverlay.Height.Set(0f, 1f);
			confirmOverlay.SetPadding(0f);
			confirmOverlay.BackgroundColor = Color.Black * 0.6f;
			confirmOverlay.BorderColor = Color.Transparent;

			var confirmBox = new UIPanel();
			confirmBox.SetPadding(0f);
			confirmBox.Width.Set(ConfirmBoxWidth, 0f);
			confirmBox.Height.Set(ConfirmBoxHeight, 0f);
			confirmBox.HAlign = 0.5f;
			confirmBox.VAlign = 0.5f;
			confirmBox.BackgroundColor = new Color(10, 16, 28, 250);
			confirmBox.BorderColor = new Color(248, 113, 113);
			confirmOverlay.Append(confirmBox);

			UIText confirmTitleText = new UIText("Confirm", 1.05f)
			{
				HAlign = 0.5f,
				TextColor = new Color(248, 113, 113)
			};
			confirmTitleText.Top.Set(14f, 0f);
			confirmBox.Append(confirmTitleText);

			confirmMessageText = new UIText("", 0.8f)
			{
				HAlign = 0.5f,
				IsWrapped = true
			};
			confirmMessageText.Width.Set(-40f, 1f);
			confirmMessageText.Height.Set(120f, 0f);
			confirmMessageText.Top.Set(50f, 0f);
			confirmBox.Append(confirmMessageText);

			const float buttonGap = 20f;
			float pairWidth = ConfirmButtonWidth * 2f + buttonGap;

			var confirmButton = new ModalButton("Confirm", new Color(48, 18, 22, 240), new Color(72, 24, 30, 250), new Color(248, 113, 113));
			confirmButton.Width.Set(ConfirmButtonWidth, 0f);
			confirmButton.Height.Set(ConfirmButtonHeight, 0f);
			confirmButton.Left.Set(-pairWidth / 2f, 0.5f);
			confirmButton.VAlign = 1f;
			confirmButton.Top.Set(-18f, 0f);
			confirmButton.Clicked += HandleConfirmOverlayConfirmed;
			confirmBox.Append(confirmButton);

			var cancelButton = new ModalButton("Cancel", new Color(14, 20, 32, 240), new Color(22, 32, 54, 250), new Color(56, 189, 248));
			cancelButton.Width.Set(ConfirmButtonWidth, 0f);
			cancelButton.Height.Set(ConfirmButtonHeight, 0f);
			cancelButton.Left.Set(-pairWidth / 2f + ConfirmButtonWidth + buttonGap, 0.5f);
			cancelButton.VAlign = 1f;
			cancelButton.Top.Set(-18f, 0f);
			cancelButton.Clicked += HandleConfirmOverlayCanceled;
			confirmBox.Append(cancelButton);
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			RefreshRerollButton();

			if (!isMinimized)
			{
				Main.LocalPlayer.mouseInterface = true;
			}
			else if (restoreIcon != null && restoreIcon.ContainsPoint(Main.MouseScreen))
			{
				Main.LocalPlayer.mouseInterface = true;
			}
		}

		public override void Draw(SpriteBatch spriteBatch)
		{
			base.Draw(spriteBatch);
			AugmentChoiceCard.DrawActiveTooltip(spriteBatch);
		}

		public bool IsMouseOverRestore => isMinimized && restoreIcon != null && restoreIcon.ContainsPoint(Main.MouseScreen);

		private RarityBracket currentBracket;

		// Call this right before showing the panel - replaces whatever cards
		// were there with a fresh set built from the given augments, and resets
		// this popup's reroll allowance.
		public void SetChoices(List<Augment> choices, AugmentRarity rarity, RarityBracket bracket = RarityBracket.PreHardmode, bool networkReward = false, int rerollCount = 0)
		{
			currentRarity = rarity;
			currentBracket = bracket;
			this.rerollCount = rerollCount;
			rerollPending = false;
			this.networkReward = networkReward;
			pendingSkipConfirm = false;
			HideKeystoneConfirm();

			// Fresh popup always starts un-minimized, regardless of how the
			// previous one was left.
			isMinimized = false;
			RemoveChild(restoreIcon);
			if (backPanel.Parent == null)
				Append(backPanel);

			RebuildCards(choices);
			RefreshCapNotice();
			RefreshRerollButton();
		}

		private void RebuildCards(List<Augment> choices)
		{
			foreach (var card in cards)
				backPanel.RemoveChild(card);
			cards.Clear();
			currentChoiceIds.Clear();

			int count = choices.Count;
			float totalWidth = count * CardWidth + (count - 1) * CardSpacing;
			float startX = (PanelWidth - totalWidth) / 2f;

			// Dynamically determine the maximum required height across all cards in this roll
			float maxRequired = AugmentChoiceCard.MinCardHeight;
			for (int i = 0; i < count; i++)
			{
				float reqH = AugmentChoiceCard.CalculateRequiredHeight(choices[i], CardWidth);
				if (reqH > maxRequired)
					maxRequired = reqH;
			}

			float cardHeight = maxRequired;
			float desiredPanelHeight = CardsTop + cardHeight + RerollGap + RerollButtonHeight + BottomMargin;

			// Responsive clamp for 720p / high UI scale
			float maxAllowedHeight = Math.Max(480f, Main.screenHeight - 36f);
			float finalPanelHeight = Math.Min(desiredPanelHeight, maxAllowedHeight);
			backPanel.Height.Set(finalPanelHeight, 0f);

			// Position Reroll and Skip buttons dynamically beneath the cards
			float rerollRowTop = CardsTop + cardHeight + RerollGap;
			if (rerollRowTop + RerollButtonHeight + BottomMargin > finalPanelHeight)
				rerollRowTop = finalPanelHeight - BottomMargin - RerollButtonHeight;

			rerollButton.Top.Set(rerollRowTop, 0f);
			skipButton.Top.Set(rerollRowTop, 0f);

			for (int i = 0; i < count; i++)
			{
				var card = new AugmentChoiceCard(choices[i], CardWidth, cardHeight, i);
				card.Left.Set(startX + i * (CardWidth + CardSpacing), 0f);
				card.Top.Set(CardsTop, 0f);
				card.OnAugmentChosen += HandleAugmentChosen;

				cards.Add(card);
				currentChoiceIds.Add(choices[i].Id);
				backPanel.Append(card);
			}
		}

		// Keystone picks need confirmation first (see ShowKeystoneConfirm) since
		// they permanently lock their family's siblings out of every future
		// popup - everything else still grants immediately, exactly as before.
		private void HandleAugmentChosen(Augment augment)
		{
			if (augment.KeystoneFamily != null)
				ShowKeystoneConfirm(augment);
			else
				GrantAndClose(augment);
		}

		private void ShowKeystoneConfirm(Augment augment)
		{
			pendingKeystone = augment;

			var siblingNames = new List<string>();
			foreach (var other in AugmentDatabase.All)
			{
				if (other.KeystoneFamily == augment.KeystoneFamily && other.Id != augment.Id)
					siblingNames.Add(other.DisplayName);
			}

			confirmMessageText.SetText(
				$"Choosing {augment.DisplayName} will permanently exclude {JoinWithAnd(siblingNames)} " +
				"from ever being offered to you again. This cannot be undone.");

			Append(confirmOverlay);
		}

		private void HideKeystoneConfirm()
		{
			pendingKeystone = null;
			RemoveChild(confirmOverlay);
		}

		private void HandleSkipClicked()
		{
			SoundEngine.PlaySound(SoundID.MenuTick);
			pendingSkipConfirm = true;
			pendingKeystone = null;

			confirmMessageText.SetText(
				"Are you sure you want to skip this plugin selection? " +
				"This reward will be forfeited and cannot be recovered.");

			Append(confirmOverlay);
		}

		private void HandleConfirmOverlayConfirmed()
		{
			if (pendingSkipConfirm)
			{
				pendingSkipConfirm = false;
				RemoveChild(confirmOverlay);
				SoundEngine.PlaySound(SoundID.MenuClose);
				ModContent.GetInstance<AugmentUISystem>().HidePanel();
				return;
			}

			if (pendingKeystone == null)
				return;

			GrantAndClose(pendingKeystone);
		}

		// Dismisses the confirmation and returns to the original 3-card
		// choice without granting anything - the cards underneath were never
		// touched, so a different card can still be picked instead.
		private void HandleConfirmOverlayCanceled()
		{
			SoundEngine.PlaySound(SoundID.MenuClose);
			pendingSkipConfirm = false;
			HideKeystoneConfirm();
		}

		private void HandleMinimizeClicked()
		{
			// Don't allow minimizing while a Keystone/Skip confirmation is up -
			// it's modal and meant to block interaction with everything behind
			// it, so swapping the panel out from under it would look broken.
			if (confirmOverlay.Parent != null)
				return;

			SoundEngine.PlaySound(SoundID.MenuClose);
			isMinimized = true;
			RemoveChild(backPanel);
			Append(restoreIcon);
		}

		private void HandleRestoreClicked()
		{
			SoundEngine.PlaySound(SoundID.MenuOpen);
			isMinimized = false;
			RemoveChild(restoreIcon);
			Append(backPanel);
		}

		private void GrantAndClose(Augment augment)
		{
			var player = Main.LocalPlayer;
			var ap = player.GetModPlayer<AugmentPlayer>();

			ap.ChooseReward(augment);

			pendingKeystone = null;
			RemoveChild(confirmOverlay);
			SoundEngine.PlaySound(ChipInstallSound);
			ModContent.GetInstance<AugmentUISystem>().HidePanel();
		}

		private void RefreshCapNotice()
		{
			var ap = Main.LocalPlayer.GetModPlayer<AugmentPlayer>();
			capNoticeText.SetText(ap.Owned.Count >= AugmentPlayer.MaxOwnedAugments
				? "⚠ NOTICE: Neural Frame full (5/5) — Selection will be archived at Mistress 2B & dismantled for Machine Cores."
				: "");
		}

		private static string JoinWithAnd(List<string> names)
		{
			if (names.Count == 0)
				return "";
			if (names.Count == 1)
				return names[0];

			return string.Join(", ", names.GetRange(0, names.Count - 1)) + " and " + names[names.Count - 1];
		}

		private void HandleRerollClicked()
		{
			// Dev mode: unlimited free rerolls (local rolls only; the server still enforces limits in multiplayer)
			bool devFree = AugmentListUIState.IsDevMode && !networkReward;

			if (rerollPending || (!devFree && rerollCount >= 2))
				return;

			var player = Main.LocalPlayer;
			int essenceType = ModContent.ItemType<AugmentEssenceItem>();

			if (!devFree && rerollCount == 1)
			{
				if (player.CountItem(essenceType) < 4)
				{
					Main.NewText("Not enough Machine Cores to reroll (Requires 4).", 255, 80, 80);
					SoundEngine.PlaySound(SoundID.MenuClose);
					RefreshRerollButton();
					return;
				}
			}

			if (networkReward)
			{
				rerollPending = true;
				SoundEngine.PlaySound(SoundID.Item37);
				AugmentNet.SendRerollRequest(player);
				RefreshRerollButton();
				return;
			}

			var augmentPlayer = player.GetModPlayer<AugmentPlayer>();
			if (!AugmentRewardLogic.TryRollRewardChoices(augmentPlayer, currentBracket, currentChoiceIds, out List<Augment> newChoices, out AugmentRarity newRarity))
			{
				Main.NewText("No other plugins are available.", 255, 100, 100);
				RefreshRerollButton();
				return;
			}

			if (!devFree)
			{
				if (rerollCount == 1)
				{
					for (int i = 0; i < 4; i++)
						player.ConsumeItem(essenceType);
				}

				rerollCount++;
			}

			currentRarity = newRarity;
			SoundEngine.PlaySound(SoundID.Item37);
			RebuildCards(newChoices);
			RefreshRerollButton();
		}

		private void RefreshRerollButton()
		{
			if (rerollPending)
			{
				rerollButton.SetEnabled(false, "Rerolling...");
				return;
			}

			if (AugmentListUIState.IsDevMode && !networkReward)
			{
				rerollButton.SetEnabled(true, "Reroll (Dev: Free)");
				return;
			}

			if (rerollCount >= 2)
			{
				rerollButton.SetEnabled(false, "No Rerolls Left");
				return;
			}

			if (rerollCount == 0)
			{
				rerollButton.SetEnabled(true, "Reroll (Free)");
				return;
			}

			// rerollCount == 1: costs 4 Machine Cores
			int essenceType = ModContent.ItemType<AugmentEssenceItem>();
			int essenceCount = Main.LocalPlayer.CountItem(essenceType);

			if (essenceCount >= 4)
			{
				rerollButton.SetEnabled(true, "Reroll (4 Cores)");
			}
			else
			{
				rerollButton.SetEnabled(false, "Need 4 Cores");
			}
		}

		// Small standalone clickable panel - same manual hover/click approach
		// AugmentChoiceCard/AugmentShopEntry use, to match this mod's existing
		// UI button style. "Disabled" here is just a grey visual hint - the
		// real gating lives in AugmentChoiceUIState.HandleRerollClicked.
		private class RerollButton : UIElement
		{
			public event Action Clicked;

			private static readonly Color DefaultIdleColor = new Color(10, 16, 28, 245);
			private static readonly Color DefaultHoverColor = new Color(18, 32, 54, 250);
			private static readonly Color DisabledColor = new Color(10, 14, 24, 200);

			private readonly Color idleColor;
			private readonly Color hoverColor;
			private readonly Color activeBorderColor;
			private string currentLabel;
			private bool enabledState = true;
			private bool isHovered = false;

			public RerollButton() : this(DefaultIdleColor, DefaultHoverColor, new Color(56, 189, 248))
			{
			}

			public RerollButton(Color idleColor, Color hoverColor, Color activeBorderColor)
			{
				this.idleColor = idleColor;
				this.hoverColor = hoverColor;
				this.activeBorderColor = activeBorderColor;
				currentLabel = "Reroll (1 Core)";
			}

			public void SetEnabled(bool enabled, string label)
			{
				currentLabel = label;
				enabledState = enabled;
			}

			public override void LeftClick(UIMouseEvent evt)
			{
				base.LeftClick(evt);
				if (enabledState)
				{
					SoundEngine.PlaySound(SoundID.MenuTick);
					Clicked?.Invoke();
				}
			}

			public override void MouseOver(UIMouseEvent evt)
			{
				base.MouseOver(evt);
				isHovered = true;
				if (enabledState)
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

				Color bg;
				Color border;
				Color textColor;

				if (!enabledState)
				{
					bg = DisabledColor;
					border = new Color(25, 33, 48);
					textColor = new Color(100, 116, 139);
				}
				else if (isHovered)
				{
					bg = hoverColor;
					border = activeBorderColor;
					textColor = Color.White;
				}
				else
				{
					bg = idleColor;
					border = new Color(30, 41, 59);
					textColor = new Color(226, 232, 240);
				}

				// Ambient hover underglow
				if (isHovered && enabledState)
				{
					spriteBatch.Draw(pixel, new Rectangle(rect.X - 1, rect.Y - 1, rect.Width + 2, rect.Height + 2), border * 0.12f);
				}

				// Chassis Fill
				spriteBatch.Draw(pixel, rect, bg);

				// 1px Inner Hairline Highlight Accent
				Color innerHairline = Color.White * (isHovered && enabledState ? 0.08f : 0.04f);
				spriteBatch.Draw(pixel, new Rectangle(rect.X + 1, rect.Y + 1, rect.Width - 2, 1), innerHairline);
				spriteBatch.Draw(pixel, new Rectangle(rect.X + 1, rect.Bottom - 2, rect.Width - 2, 1), innerHairline);
				spriteBatch.Draw(pixel, new Rectangle(rect.X + 1, rect.Y + 1, 1, rect.Height - 2), innerHairline);
				spriteBatch.Draw(pixel, new Rectangle(rect.Right - 2, rect.Y + 1, 1, rect.Height - 2), innerHairline);

				// 1px Outer Border (clean, no corner ticks)
				spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 1), border);
				spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), border);
				spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, 1, rect.Height), border);
				spriteBatch.Draw(pixel, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), border);

				// Centered typography with baseline optical centering
				var font = FontAssets.MouseText.Value;
				Vector2 scale = new Vector2(0.80f);
				Vector2 textSize = ChatManager.GetStringSize(font, currentLabel, scale);
				Vector2 textPos = new Vector2(
					rect.X + (rect.Width - textSize.X) * 0.5f,
					rect.Y + (rect.Height - 13.5f) * 0.5f - 1.5f
				);
				ChatManager.DrawColorCodedStringWithShadow(spriteBatch, font, currentLabel, textPos, textColor, 0f, Vector2.Zero, scale);
			}
		}

		private class ModalButton : UIElement
		{
			public event Action Clicked;

			private readonly string label;
			private readonly Color idleColor;
			private readonly Color hoverColor;
			private readonly Color hoverBorderColor;
			private bool isHovered;

			public ModalButton(string label, Color idleColor, Color hoverColor, Color? hoverBorderColor = null)
			{
				this.label = label;
				this.idleColor = idleColor;
				this.hoverColor = hoverColor;
				this.hoverBorderColor = hoverBorderColor ?? new Color(56, 189, 248);
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

				Color bg = isHovered ? hoverColor : idleColor;
				Color border = isHovered ? hoverBorderColor : new Color(30, 41, 59);

				if (isHovered)
				{
					spriteBatch.Draw(pixel, new Rectangle(rect.X - 1, rect.Y - 1, rect.Width + 2, rect.Height + 2), border * 0.12f);
				}

				spriteBatch.Draw(pixel, rect, bg);

				Color innerHairline = Color.White * (isHovered ? 0.08f : 0.04f);
				spriteBatch.Draw(pixel, new Rectangle(rect.X + 1, rect.Y + 1, rect.Width - 2, 1), innerHairline);
				spriteBatch.Draw(pixel, new Rectangle(rect.X + 1, rect.Bottom - 2, rect.Width - 2, 1), innerHairline);
				spriteBatch.Draw(pixel, new Rectangle(rect.X + 1, rect.Y + 1, 1, rect.Height - 2), innerHairline);
				spriteBatch.Draw(pixel, new Rectangle(rect.Right - 2, rect.Y + 1, 1, rect.Height - 2), innerHairline);

				spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 1), border);
				spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), border);
				spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, 1, rect.Height), border);
				spriteBatch.Draw(pixel, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), border);

				var font = FontAssets.MouseText.Value;
				Vector2 scale = new Vector2(0.85f);
				Vector2 textSize = ChatManager.GetStringSize(font, label, scale);
				Vector2 textPos = new Vector2(
					rect.X + (rect.Width - textSize.X) * 0.5f,
					rect.Y + (rect.Height - 14f) * 0.5f - 1.5f
				);
				ChatManager.DrawColorCodedStringWithShadow(spriteBatch, font, label, textPos, isHovered ? Color.White : new Color(203, 213, 225), 0f, Vector2.Zero, scale);
			}
		}

		private class RestoreIcon : UIElement
		{
			public event Action Clicked;

			private static readonly Color BaseBorderColor = new Color(56, 189, 248);
			private const float PulseSpeed = 2.2f;

			private float pulseTimer;
			private bool isHovered;

			public override void Update(GameTime gameTime)
			{
				base.Update(gameTime);
				pulseTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
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

			protected override void DrawSelf(SpriteBatch spriteBatch)
			{
				CalculatedStyle dims = GetDimensions();
				var rect = new Rectangle((int)dims.X, (int)dims.Y, (int)dims.Width, (int)dims.Height);
				Texture2D pixel = TextureAssets.MagicPixel.Value;

				float pulse = (float)Math.Sin(pulseTimer * PulseSpeed) * 0.5f + 0.5f;
				Color border = isHovered ? Color.White : Color.Lerp(BaseBorderColor, new Color(250, 204, 21), pulse * 0.5f);

				spriteBatch.Draw(pixel, rect, new Color(10, 16, 28, 250));

				Color innerHairline = Color.White * (isHovered ? 0.08f : 0.04f);
				spriteBatch.Draw(pixel, new Rectangle(rect.X + 1, rect.Y + 1, rect.Width - 2, 1), innerHairline);
				spriteBatch.Draw(pixel, new Rectangle(rect.X + 1, rect.Bottom - 2, rect.Width - 2, 1), innerHairline);
				spriteBatch.Draw(pixel, new Rectangle(rect.X + 1, rect.Y + 1, 1, rect.Height - 2), innerHairline);
				spriteBatch.Draw(pixel, new Rectangle(rect.Right - 2, rect.Y + 1, 1, rect.Height - 2), innerHairline);

				spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 1), border);
				spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), border);
				spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, 1, rect.Height), border);
				spriteBatch.Draw(pixel, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), border);

				var font = FontAssets.MouseText.Value;
				Vector2 scale = new Vector2(1.1f);
				Vector2 textSize = ChatManager.GetStringSize(font, "?", scale);
				Vector2 textPos = new Vector2(
					rect.X + (rect.Width - textSize.X) * 0.5f,
					rect.Y + (rect.Height - 18f) * 0.5f - 1.5f
				);
				ChatManager.DrawColorCodedStringWithShadow(spriteBatch, font, "?", textPos, isHovered ? Color.White : border, 0f, Vector2.Zero, scale);
			}
		}

		private class ChoiceBackPanel : UIPanel
		{
			protected override void DrawSelf(SpriteBatch spriteBatch)
			{
				base.DrawSelf(spriteBatch);

				CalculatedStyle dims = GetDimensions();
				Texture2D pixel = TextureAssets.MagicPixel.Value;

				PanelChrome.DrawFrame(spriteBatch, dims.ToRectangle());

				// Header horizontal divider spanning X = 18f to X = 862f (exact 18px bilateral symmetry)
				int divY = (int)dims.Y + 86;
				spriteBatch.Draw(pixel, new Rectangle((int)dims.X + 18, divY, (int)dims.Width - 36, 1), new Color(30, 41, 59) * 0.90f);

				// Center diamond node
				int midX = (int)dims.X + (int)(dims.Width * 0.5f);
				spriteBatch.Draw(pixel, new Rectangle(midX - 1, divY - 1, 3, 3), new Color(56, 189, 248) * 0.85f);
				spriteBatch.Draw(pixel, new Rectangle(midX, divY, 1, 1), Color.White * 0.9f);
			}
		}
	}
}
