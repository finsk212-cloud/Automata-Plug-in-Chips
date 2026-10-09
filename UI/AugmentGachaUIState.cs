using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
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
	public class AugmentGachaUIState : UIState
	{
		private enum ChamberState
		{
			Idle,
			Decrypting,
			Revealed
		}

		private UIPanel backPanel;
		private ColoredLabel essenceLabel;
		private HeaderStatusRow headerStatusRow;

		// Interactive Content Container
		private ChamberPanel chamberContainer;

		private ChamberState state = ChamberState.Idle;
		private int animTimer;
		private const int RollDuration = 175; // ~2.9 seconds

		private Augment wonAugment;
		private bool wonAugmentArchived;
		private int wonRefundCores;

		private readonly UIParticleSystem particles = new UIParticleSystem(140);

		// Spacious window layout
		private const float PanelWidth = 840f;
		private const float PanelHeight = 560f;

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);

			if (backPanel != null && backPanel.ContainsPoint(Main.MouseScreen))
			{
				Main.LocalPlayer.mouseInterface = true;
			}

			particles.Update();

			CalculatedStyle chamberDims = chamberContainer != null ? chamberContainer.GetDimensions() : default;
			Rectangle chamberRect = new Rectangle((int)chamberDims.X, (int)chamberDims.Y, (int)chamberDims.Width, (int)chamberDims.Height);

			if (state == ChamberState.Idle)
			{
				if (chamberDims.Width > 0 && Main.rand.NextBool(8))
				{
					Color emberColor = Main.rand.NextBool() ? new Color(76, 168, 216) : new Color(212, 184, 114);
					particles.SpawnAmbient(chamberRect, emberColor, scale: 1.8f);
				}
			}
			else if (state == ChamberState.Decrypting)
			{
				animTimer++;

				if (chamberDims.Width > 0 && Main.rand.NextBool(5))
				{
					particles.SpawnAmbient(chamberRect, new Color(76, 168, 216), scale: 1.6f);
				}

				if (animTimer >= RollDuration)
				{
					state = ChamberState.Revealed;
					OnDecryptionRevealed();
					RebuildChamberContent();
				}
			}
			else if (state == ChamberState.Revealed && wonAugment != null)
			{
				if (chamberDims.Width > 0 && Main.rand.NextBool(9))
				{
					Color auraColor = GetRarityColor(wonAugment.Rarity);
					particles.SpawnAmbient(chamberRect, auraColor, scale: 1.8f);
				}
			}
		}

		public override void OnInitialize()
		{
			backPanel = new GachaBackPanel();
			backPanel.Width.Set(PanelWidth, 0f);
			backPanel.Height.Set(PanelHeight, 0f);
			backPanel.HAlign = 0.5f;
			backPanel.VAlign = 0.5f;
			PanelChrome.Style(backPanel, zeroPadding: false);

			// Top Navigation Tab: Return to Storage
			var storageTab = new TabButton("← Plugin Storage & Dismantle", () => ModContent.GetInstance<AugmentUISystem>().ShowShop());
			storageTab.Left.Set(14f, 0f);
			storageTab.Top.Set(10f, 0f);
			storageTab.Width.Set(220f, 0f);
			storageTab.Height.Set(26f, 0f);
			backPanel.Append(storageTab);

			// Machine Cores Badge with refined champagne & cerulean text
			UIPanel essenceBadge = new UIPanel();
			essenceBadge.Width.Set(200f, 0f);
			essenceBadge.Height.Set(26f, 0f);
			essenceBadge.Left.Set(-244f, 1f);
			essenceBadge.Top.Set(10f, 0f);
			essenceBadge.SetPadding(0f);
			essenceBadge.BackgroundColor = new Color(12, 16, 28) * 0.95f;
			essenceBadge.BorderColor = new Color(42, 68, 108) * 0.75f;

			essenceLabel = new ColoredLabel("[c/D4B872:Machine Cores:] [c/68C2D8:0]", 0.84f);
			essenceLabel.Width.Set(0f, 1f);
			essenceLabel.Height.Set(0f, 1f);
			essenceBadge.Append(essenceLabel);
			backPanel.Append(essenceBadge);

			// Close button (shared across all panels)
			PanelChrome.AddCloseButton(backPanel, () => ModContent.GetInstance<AugmentUISystem>().HideGacha(), 12f);

			// Title Header
			UIText title = new UIText("YoRHa Neural Decryption Chamber", 1.25f)
			{
				HAlign = 0.5f,
				TextColor = new Color(234, 216, 176)
			};
			title.Top.Set(42f, 0f);
			backPanel.Append(title);

			// Completely Overhauled Header Status Row (Protocol Badge + 4 Clean Odds Pills)
			headerStatusRow = new HeaderStatusRow
			{
				HAlign = 0.5f
			};
			headerStatusRow.Top.Set(74f, 0f);
			headerStatusRow.Width.Set(PanelWidth - 40f, 0f);
			headerStatusRow.Height.Set(30f, 0f);
			backPanel.Append(headerStatusRow);

			// Main Containment Chamber Container with clean sci-fi markings
			chamberContainer = new ChamberPanel();
			chamberContainer.Width.Set(-28f, 1f);
			chamberContainer.Height.Set(410f, 0f);
			chamberContainer.Left.Set(14f, 0f);
			chamberContainer.Top.Set(120f, 0f);
			chamberContainer.BackgroundColor = new Color(10, 14, 26) * 0.96f;
			chamberContainer.BorderColor = new Color(34, 52, 92) * 0.85f;
			backPanel.Append(chamberContainer);

			Append(backPanel);

			RebuildChamberContent();
		}

		public override void Draw(SpriteBatch spriteBatch)
		{
			base.Draw(spriteBatch);
			particles.Draw(spriteBatch);
		}

		public void Refresh()
		{
			int count = Main.LocalPlayer.CountItem(ModContent.ItemType<AugmentEssenceItem>());
			essenceLabel?.SetText($"[c/D4B872:Machine Cores:] [c/68C2D8:{count:N0}]");

			RarityBracket bracket = BossTierMap.GetCurrentWorldBracket();
			string bracketName = BossTierMap.GetBracketName(bracket);
			RarityRollChances chances = BossRarityRoller.GetChancesForBracket(bracket);

			headerStatusRow?.UpdateData(bracketName, chances);

			if (state != ChamberState.Decrypting)
			{
				state = ChamberState.Idle;
				RebuildChamberContent();
			}
		}

		private void RebuildChamberContent()
		{
			if (chamberContainer == null)
				return;

			chamberContainer.RemoveAllChildren();

			if (state == ChamberState.Idle)
			{
				BuildIdleChamber();
			}
			else if (state == ChamberState.Decrypting)
			{
				BuildDecryptingChamber();
			}
			else if (state == ChamberState.Revealed)
			{
				BuildRevealedChamber();
			}
		}

		private void BuildIdleChamber()
		{
			// Pod graphic preview in the center with holographic pedestal (no square glow box)
			var podView = new PodDisplayCard();
			podView.Width.Set(140f, 0f);
			podView.Height.Set(140f, 0f);
			podView.HAlign = 0.5f;
			podView.Top.Set(30f, 0f);
			chamberContainer.Append(podView);

			var readyLabel = new ColoredLabel("[c/EAD8B0:YoRHa Encrypted Salvage Pod Loaded]", 1.02f)
			{
				HAlign = 0.5f
			};
			readyLabel.Top.Set(185f, 0f);
			readyLabel.Width.Set(500f, 0f);
			readyLabel.Height.Set(24f, 0f);
			chamberContainer.Append(readyLabel);

			var subtext = new ColoredLabel("[c/889EB8:Consume 3 Machine Cores to synthesize neural frequency and extract 1 combat Plugin]", 0.78f)
			{
				HAlign = 0.5f
			};
			subtext.Top.Set(215f, 0f);
			subtext.Width.Set(620f, 0f);
			subtext.Height.Set(22f, 0f);
			chamberContainer.Append(subtext);

			// Decrypt action button - redesigned modern sleek button
			var decryptBtn = new ChamberActionButton(
				"INITIALIZE DECRYPTION",
				"3 Machine Cores",
				isPrimary: true,
				onClick: StartDecryption
			);
			decryptBtn.Width.Set(420f, 0f);
			decryptBtn.Height.Set(44f, 0f);
			decryptBtn.HAlign = 0.5f;
			decryptBtn.Top.Set(265f, 0f);
			chamberContainer.Append(decryptBtn);

			var footer = new ColoredLabel("[c/627A98:Rolls prioritize unowned plugins from your active progression bracket.]", 0.72f)
			{
				HAlign = 0.5f
			};
			footer.Top.Set(345f, 0f);
			footer.Width.Set(600f, 0f);
			footer.Height.Set(18f, 0f);
			chamberContainer.Append(footer);
		}

		private void BuildDecryptingChamber()
		{
			// Clean YoRHa Decryption Scanner - no spinning dotted circles or vertical lines
			var coreView = new NeuralDecryptionCoreView(() => animTimer, RollDuration);
			coreView.Width.Set(-20f, 1f);
			coreView.Height.Set(250f, 0f);
			coreView.HAlign = 0.5f;
			coreView.Top.Set(25f, 0f);
			chamberContainer.Append(coreView);

			var statusLabel = new ColoredLabel("[c/68C2D8:SYNCHRONIZING NEURAL FREQUENCY...]", 1.0f)
			{
				HAlign = 0.5f
			};
			statusLabel.Top.Set(290f, 0f);
			statusLabel.Width.Set(500f, 0f);
			statusLabel.Height.Set(24f, 0f);
			chamberContainer.Append(statusLabel);

			var subLabel = new ColoredLabel("[c/D4B872:Harmonizing combat memory stream]   [c/7A9AB8:•   Deciphering YoRHa Pod telemetry]", 0.78f)
			{
				HAlign = 0.5f
			};
			subLabel.Top.Set(322f, 0f);
			subLabel.Width.Set(600f, 0f);
			subLabel.Height.Set(20f, 0f);
			chamberContainer.Append(subLabel);
		}

		private void BuildRevealedChamber()
		{
			if (wonAugment == null)
			{
				state = ChamberState.Idle;
				BuildIdleChamber();
				return;
			}

			Color rarityColor = GetRarityColor(wonAugment.Rarity);

			// Winning Chip Card Container with strict left-aligned typography
			var chipCard = new RevealedChipCard(wonAugment, rarityColor);
			chipCard.Width.Set(660f, 0f);
			chipCard.Height.Set(220f, 0f);
			chipCard.HAlign = 0.5f;
			chipCard.Top.Set(24f, 0f);
			chamberContainer.Append(chipCard);

			// Action Buttons: Modern, sleek, and gapped with balanced margins
			var decryptAgainBtn = new ChamberActionButton(
				"DECRYPT AGAIN",
				"3 Cores",
				isPrimary: true,
				onClick: StartDecryption
			);
			decryptAgainBtn.Width.Set(310f, 0f);
			decryptAgainBtn.Height.Set(44f, 0f);
			decryptAgainBtn.Left.Set(70f, 0f);
			decryptAgainBtn.Top.Set(326f, 0f);
			chamberContainer.Append(decryptAgainBtn);

			var returnBtn = new ChamberActionButton(
				"← Return to Plugin Storage",
				null,
				isPrimary: false,
				onClick: () => ModContent.GetInstance<AugmentUISystem>().ShowShop()
			);
			returnBtn.Width.Set(310f, 0f);
			returnBtn.Height.Set(44f, 0f);
			returnBtn.Left.Set(-380f, 1f);
			returnBtn.Top.Set(326f, 0f);
			chamberContainer.Append(returnBtn);
		}

		private void StartDecryption()
		{
			var player = Main.LocalPlayer;
			int coreType = ModContent.ItemType<AugmentEssenceItem>();
			if (player.CountItem(coreType) < 3)
			{
				Main.NewText("Requires 3 Machine Cores to decrypt.", 220, 90, 90);
				SoundEngine.PlaySound(SoundID.MenuClose);
				return;
			}

			// Consume 3 Machine Cores
			for (int i = 0; i < 3; i++)
				player.ConsumeItem(coreType);

			Refresh();

			// Roll winning augment using current world progression bracket
			RarityBracket currentBracket = BossTierMap.GetCurrentWorldBracket();
			AugmentRarity rolledRarity = BossRarityRoller.Roll(currentBracket);
			var ap = player.GetModPlayer<AugmentPlayer>();

			// Protocol Partner Bias (20%): If player has 1/2 of an active protocol, 20% chance to bias the reward tier towards the partner chip's rarity
			var missingPartners = ap.GetMissingProtocolPartners();
			if (missingPartners.Count > 0 && Main.rand.NextFloat() < 0.20f)
			{
				var targetPartner = missingPartners[Main.rand.Next(missingPartners.Count)];
				rolledRarity = targetPartner.Rarity;
			}

			var choices = ap.RollChoices(1, rolledRarity, null);
			if (choices.Count > 0)
			{
				wonAugment = choices[0];
			}
			else
			{
				foreach (AugmentRarity fallbackRarity in AugmentRewardLogic.GetRarityFallbackOrder(rolledRarity))
				{
					choices = ap.RollChoices(1, fallbackRarity, null);
					if (choices.Count > 0)
					{
						wonAugment = choices[0];
						break;
					}
				}
				if (wonAugment == null)
				{
					var all = AugmentDatabase.All;
					wonAugment = all[Main.rand.Next(all.Count)];
				}
			}

			// Transition to Decrypting state
			state = ChamberState.Decrypting;
			animTimer = 0;

			// Soft spool-up hum
			SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.70f, Pitch = -0.3f }, player.Center);
			RebuildChamberContent();
		}

		private void OnDecryptionRevealed()
		{
			var player = Main.LocalPlayer;
			if (wonAugment == null)
				return;

			// Sound and particle fanfare based on rarity
			CalculatedStyle chamberDims = chamberContainer != null ? chamberContainer.GetDimensions() : default;
			Vector2 revealCenter = new Vector2(chamberDims.X + chamberDims.Width * 0.5f, chamberDims.Y + 120f);

			Color rarityColor = GetRarityColor(wonAugment.Rarity);
			particles.SpawnBurst(revealCenter, rarityColor, count: 55, maxSpeed: 4.8f);

			switch (wonAugment.Rarity)
			{
				case AugmentRarity.Legendary:
					SoundEngine.PlaySound(SoundID.Item29 with { Volume = 1.0f, Pitch = 0.15f }, player.Center);
					for (int i = 0; i < 35; i++)
					{
						Dust d = Dust.NewDustDirect(player.position, player.width, player.height, DustID.GoldFlame, Main.rand.NextFloat(-4f, 4f), Main.rand.NextFloat(-4f, 4f), 0, default, 1.4f);
						d.noGravity = true;
					}
					break;
				case AugmentRarity.Epic:
					SoundEngine.PlaySound(SoundID.Item100 with { Volume = 0.95f, Pitch = 0.1f }, player.Center);
					for (int i = 0; i < 25; i++)
					{
						Dust d = Dust.NewDustDirect(player.position, player.width, player.height, DustID.PurpleTorch, Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-3f, 3f), 0, default, 1.25f);
						d.noGravity = true;
					}
					break;
				case AugmentRarity.Rare:
					SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.90f, Pitch = 0.1f }, player.Center);
					for (int i = 0; i < 20; i++)
					{
						Dust d = Dust.NewDustDirect(player.position, player.width, player.height, DustID.Electric, Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-3f, 3f), 0, default, 1.15f);
						d.noGravity = true;
					}
					break;
				default:
					SoundEngine.PlaySound(SoundID.Research with { Volume = 0.90f, Pitch = 0.1f }, player.Center);
					for (int i = 0; i < 15; i++)
					{
						Dust d = Dust.NewDustDirect(player.position, player.width, player.height, DustID.Iron, Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-2f, 2f), 0, default, 1.05f);
						d.noGravity = true;
					}
					break;
			}

			// Authoritatively grant or archive the augment so chat announces it at the exact moment of reveal!
			var ap = player.GetModPlayer<AugmentPlayer>();
			wonAugmentArchived = ap.Owned.Count >= AugmentPlayer.MaxOwnedAugments;
			wonRefundCores = wonAugmentArchived ? AugmentPlayer.GetRemoveRefund(wonAugment.Rarity) : 0;
			ap.ChooseReward(wonAugment);
		}

		private static Color GetRarityColor(AugmentRarity rarity)
		{
			return rarity switch
			{
				AugmentRarity.Legendary => new Color(230, 190, 68),  // #E6BE44 Warm Imperial Gold
				AugmentRarity.Epic => new Color(180, 112, 224),       // #B470E0 Royal Orchid
				AugmentRarity.Rare => new Color(76, 168, 216),        // #4CA8D8 Cyber Cerulean
				_ => new Color(184, 196, 212)                         // #B8C4D4 Titanium Silver
			};
		}

		// Floating 2.8x pixel art pod preview with sleek holographic emitter pedestal (no square glow box)
		private class PodDisplayCard : UIElement
		{
			protected override void DrawSelf(SpriteBatch spriteBatch)
			{
				CalculatedStyle d = GetDimensions();
				Texture2D pixel = TextureAssets.MagicPixel.Value;

				if (ModContent.RequestIfExists<Texture2D>("Augments/Items/SealedChipCacheItem", out var cacheAsset))
				{
					Texture2D tex = cacheAsset.Value;
					if (tex != null)
					{
						float bob = (float)Math.Sin(Main.timeForVisualEffects * 0.04f) * 4f;
						Vector2 center = new Vector2(d.X + d.Width * 0.5f, d.Y + d.Height * 0.5f + bob);
						Vector2 origin = tex.Size() * 0.5f;

						// Subtle holographic emitter pedestal line beneath the pod
						float pedestalY = d.Y + d.Height * 0.5f + 48f;
						Color pedestalCol = new Color(54, 92, 138) * 0.65f;
						spriteBatch.Draw(pixel, new Rectangle((int)center.X - 40, (int)pedestalY, 80, 2), pedestalCol);
						spriteBatch.Draw(pixel, new Rectangle((int)center.X - 25, (int)pedestalY + 3, 50, 1), pedestalCol * 0.6f);

						// Crisp pod sprite with 2.8x scaling
						spriteBatch.Draw(tex, center, null, Color.White, 0f, origin, 2.8f, SpriteEffects.None, 0f);
					}
				}
			}
		}

		// Clean YoRHa Decryption Scanner - sleek laser sweep, no spinning dotted circles or vertical lines
		private class NeuralDecryptionCoreView : UIElement
		{
			private readonly Func<int> getTimer;
			private readonly int maxDuration;

			public NeuralDecryptionCoreView(Func<int> getTimer, int maxDuration)
			{
				this.getTimer = getTimer;
				this.maxDuration = maxDuration;
			}

			protected override void DrawSelf(SpriteBatch spriteBatch)
			{
				CalculatedStyle d = GetDimensions();
				Texture2D pixel = TextureAssets.MagicPixel.Value;
				int timer = getTimer();

				Vector2 center = new Vector2(d.X + d.Width * 0.5f, d.Y + 115f);

				// Sound ticks during decryption
				int tickInterval = timer > 120 ? 6 : (timer > 70 ? 9 : 14);
				if (timer % tickInterval == 0 && timer < maxDuration - 10)
				{
					float pitch = -0.2f + (timer / (float)maxDuration) * 0.45f;
					SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = pitch, Volume = 0.8f });
				}

				// Draw Pod
				if (ModContent.RequestIfExists<Texture2D>("Augments/Items/SealedChipCacheItem", out var cacheAsset))
				{
					Texture2D tex = cacheAsset.Value;
					if (tex != null)
					{
						float bob = (float)Math.Sin(timer * 0.08f) * 3f;
						Vector2 podCenter = center + new Vector2(0f, bob);
						Vector2 origin = tex.Size() * 0.5f;

						// Sleek pedestal line
						float pedestalY = center.Y + 46f;
						spriteBatch.Draw(pixel, new Rectangle((int)center.X - 45, (int)pedestalY, 90, 2), new Color(54, 92, 138) * 0.75f);
						spriteBatch.Draw(pixel, new Rectangle((int)center.X - 25, (int)pedestalY + 3, 50, 1), new Color(54, 92, 138) * 0.4f);

						// Draw pod
						spriteBatch.Draw(tex, podCenter, null, Color.White, 0f, origin, 2.8f, SpriteEffects.None, 0f);

						// Clean horizontal scanning laser smoothly sweeping up and down the pod
						float scanProgress = (float)Math.Sin(timer * 0.12f) * 0.5f + 0.5f;
						float scanY = podCenter.Y - 24f + (scanProgress * 48f);
						Color scanCol = new Color(76, 168, 216) * 0.85f;
						spriteBatch.Draw(pixel, new Rectangle((int)center.X - 35, (int)scanY, 70, 2), scanCol);
					}
				}

				// Progress bar below pod
				float barW = 260f;
				float barH = 5f;
				float barX = center.X - barW * 0.5f;
				float barY = center.Y + 70f;

				spriteBatch.Draw(pixel, new Rectangle((int)barX, (int)barY, (int)barW, (int)barH), new Color(14, 20, 34));
				float fillW = barW * MathHelper.Clamp(timer / (float)maxDuration, 0f, 1f);
				spriteBatch.Draw(pixel, new Rectangle((int)barX, (int)barY, (int)fillW, (int)barH), new Color(76, 168, 216) * 0.9f);
				DrawBorder(spriteBatch, new Rectangle((int)barX, (int)barY, (int)barW, (int)barH), new Color(42, 68, 108) * 0.85f, 1);
			}

			private static void DrawBorder(SpriteBatch sb, Rectangle rect, Color col, int th)
			{
				Texture2D pixel = TextureAssets.MagicPixel.Value;
				sb.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, th), col);
				sb.Draw(pixel, new Rectangle(rect.X, rect.Bottom - th, rect.Width, th), col);
				sb.Draw(pixel, new Rectangle(rect.X, rect.Y, th, rect.Height), col);
				sb.Draw(pixel, new Rectangle(rect.Right - th, rect.Y, th, rect.Height), col);
			}
		}

		// Tactical Chamber Panel with subtle sci-fi crosshair and telemetry grid markings
		private class ChamberPanel : UIPanel
		{
			protected override void DrawSelf(SpriteBatch spriteBatch)
			{
				base.DrawSelf(spriteBatch);

				CalculatedStyle d = GetDimensions();
				Rectangle rect = d.ToRectangle();
				Texture2D pixel = TextureAssets.MagicPixel.Value;

				// Subtle corner crosshair reticle markers [+]
				Color gridCol = new Color(42, 62, 98) * 0.42f;
				DrawCrosshair(spriteBatch, pixel, new Vector2(rect.X + 22f, rect.Y + 22f), gridCol);
				DrawCrosshair(spriteBatch, pixel, new Vector2(rect.Right - 22f, rect.Y + 22f), gridCol);
				DrawCrosshair(spriteBatch, pixel, new Vector2(rect.X + 22f, rect.Bottom - 22f), gridCol);
				DrawCrosshair(spriteBatch, pixel, new Vector2(rect.Right - 22f, rect.Bottom - 22f), gridCol);

				// Subtle horizontal telemetry divider lines
				spriteBatch.Draw(pixel, new Rectangle(rect.X + 24, rect.Y + 24, rect.Width - 48, 1), gridCol * 0.45f);
				spriteBatch.Draw(pixel, new Rectangle(rect.X + 24, rect.Bottom - 25, rect.Width - 48, 1), gridCol * 0.45f);

				// Telemetry stamp in bottom-right corner
				var font = FontAssets.MouseText.Value;
				string stamp = "YORHA POD // NEURAL SYNTHESIS ARRAY REV 4.2";
				Vector2 stampSize = ChatManager.GetStringSize(font, stamp, new Vector2(0.64f));
				Vector2 stampPos = new Vector2(rect.Right - stampSize.X - 28f, rect.Bottom - stampSize.Y - 8f);
				ChatManager.DrawColorCodedStringWithShadow(spriteBatch, font, stamp, stampPos, new Color(75, 95, 125) * 0.42f, 0f, Vector2.Zero, new Vector2(0.64f));
			}

			private static void DrawCrosshair(SpriteBatch sb, Texture2D pixel, Vector2 pos, Color col)
			{
				sb.Draw(pixel, new Rectangle((int)pos.X - 5, (int)pos.Y, 11, 1), col);
				sb.Draw(pixel, new Rectangle((int)pos.X, (int)pos.Y - 5, 1, 11), col);
			}
		}

		// Completely Overhauled Header Status Row (Protocol Badge + 4 Clean Odds Pills)
		private class HeaderStatusRow : UIElement
		{
			private string protocolName = "Pre-Hardmode Protocol";
			private RarityRollChances chances;

			public void UpdateData(string protocol, RarityRollChances rollChances)
			{
				protocolName = protocol;
				chances = rollChances;
			}

			protected override void DrawSelf(SpriteBatch spriteBatch)
			{
				CalculatedStyle d = GetDimensions();
				Texture2D pixel = TextureAssets.MagicPixel.Value;
				var font = FontAssets.MouseText.Value;

				float centerY = d.Y + d.Height * 0.5f;

				// 1. Left Badge: Active Protocol Pill
				string protoText = $"Protocol: {protocolName}";
				Vector2 protoSize = ChatManager.GetStringSize(font, protoText, new Vector2(0.78f));
				float protoPillW = protoSize.X + 24f;
				float protoPillH = 24f;
				float protoPillX = d.X + 16f;
				float protoPillY = centerY - protoPillH * 0.5f;

				Rectangle protoRect = new Rectangle((int)protoPillX, (int)protoPillY, (int)protoPillW, (int)protoPillH);
				spriteBatch.Draw(pixel, protoRect, new Color(12, 16, 28) * 0.95f);
				DrawBorder(spriteBatch, protoRect, new Color(42, 68, 108) * 0.75f, 1);
				ChatManager.DrawColorCodedStringWithShadow(spriteBatch, font, $"[c/7A9AB8:Protocol:] [c/68C2D8:{protocolName}]", new Vector2(protoPillX + 12f, protoPillY + (protoPillH - protoSize.Y) * 0.5f), Color.White, 0f, Vector2.Zero, new Vector2(0.78f));

				// 2. Right: 4 Sleek Rarity Odds Pills
				float pillW = 108f;
				float pillH = 24f;
				float gap = 10f;
				float totalOddsW = (pillW * 4f) + (gap * 3f);
				float startOddsX = d.X + d.Width - totalOddsW - 16f;

				DrawOddsPill(spriteBatch, pixel, font, new Rectangle((int)(startOddsX + (pillW + gap) * 0), (int)(centerY - pillH * 0.5f), (int)pillW, (int)pillH), "Common", $"{chances.Common}%", new Color(184, 196, 212), new Color(48, 60, 78));
				DrawOddsPill(spriteBatch, pixel, font, new Rectangle((int)(startOddsX + (pillW + gap) * 1), (int)(centerY - pillH * 0.5f), (int)pillW, (int)pillH), "Rare", $"{chances.Rare}%", new Color(76, 168, 216), new Color(34, 68, 102));
				DrawOddsPill(spriteBatch, pixel, font, new Rectangle((int)(startOddsX + (pillW + gap) * 2), (int)(centerY - pillH * 0.5f), (int)pillW, (int)pillH), "Epic", $"{chances.Epic}%", new Color(180, 112, 224), new Color(68, 42, 98));
				DrawOddsPill(spriteBatch, pixel, font, new Rectangle((int)(startOddsX + (pillW + gap) * 3), (int)(centerY - pillH * 0.5f), (int)pillW, (int)pillH), "Legendary", $"{chances.Legendary}%", new Color(230, 190, 68), new Color(92, 74, 32));
			}

			private static void DrawOddsPill(SpriteBatch sb, Texture2D pixel, DynamicSpriteFont font, Rectangle rect, string tier, string pct, Color textCol, Color borderCol)
			{
				sb.Draw(pixel, rect, new Color(12, 16, 28) * 0.95f);
				DrawBorder(sb, rect, borderCol * 0.85f, 1);

				string label = $"{tier} {pct}";
				Vector2 size = font.MeasureString(label) * 0.72f;
				Vector2 pos = new Vector2(rect.X + (rect.Width - size.X) * 0.5f, rect.Y + (rect.Height - size.Y) * 0.5f);
				Utils.DrawBorderString(sb, label, pos, textCol, 0.72f);
			}

			private static void DrawBorder(SpriteBatch sb, Rectangle rect, Color col, int th)
			{
				Texture2D pixel = TextureAssets.MagicPixel.Value;
				sb.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, th), col);
				sb.Draw(pixel, new Rectangle(rect.X, rect.Bottom - th, rect.Width, th), col);
				sb.Draw(pixel, new Rectangle(rect.X, rect.Y, th, rect.Height), col);
				sb.Draw(pixel, new Rectangle(rect.Right - th, rect.Y, th, rect.Height), col);
			}
		}

		// Revealed Chip Card with exact pixel-matched horizontal alignment
		private class RevealedChipCard : UIPanel
		{
			private readonly Augment augment;
			private readonly Color rarityColor;
			private const float TextLeft = 105f;

			public RevealedChipCard(Augment augment, Color rarityColor)
			{
				this.augment = augment;
				this.rarityColor = rarityColor;
				SetPadding(0f);
				BackgroundColor = new Color(14, 20, 36) * 0.96f;
				BorderColor = rarityColor * 0.85f;

				// Chip Name
				var nameText = new UIText(augment.DisplayName, 1.25f)
				{
					TextColor = rarityColor
				};
				nameText.Left.Set(TextLeft, 0f);
				nameText.Top.Set(18f, 0f);
				Append(nameText);

				// Rarity & Class pill
				string rarityLabel = augment.KeystoneFamily != null ? $"[{augment.Rarity.ToString().ToUpper()} CORE OVERRIDE]" : $"[{augment.Rarity.ToString().ToUpper()}]";
				string rarityHex = augment.Rarity switch
				{
					AugmentRarity.Legendary => "E6BE44",
					AugmentRarity.Epic => "B470E0",
					AugmentRarity.Rare => "4CA8D8",
					_ => "B8C4D4"
				};

				var tierLabel = new ColoredLabel($"[c/{rarityHex}:{rarityLabel}]   •   [c/A2B8D0:Class: {augment.Class}]", 0.82f, centerH: false, centerV: true);
				tierLabel.Left.Set(TextLeft, 0f);
				tierLabel.Top.Set(48f, 0f);
				tierLabel.Width.Set(450f, 0f);
				tierLabel.Height.Set(20f, 0f);
				Append(tierLabel);

				// Status install notice
				var ap = Main.LocalPlayer.GetModPlayer<AugmentPlayer>();
				bool archived = ap.Owned.Count >= AugmentPlayer.MaxOwnedAugments;
				int refund = archived ? AugmentPlayer.GetRemoveRefund(augment.Rarity) : 0;
				string statusNotice = archived
					? $"[c/E8A068:✦ Neural Frame full (5/5) — Archived at Mistress 2B (+{refund} Cores) ✦]"
					: "[c/7AE8A8:✦ Installed directly into your Neural Frame! ✦]";

				var statusLabel = new ColoredLabel(statusNotice, 0.82f)
				{
					HAlign = 0.5f
				};
				statusLabel.Top.Set(182f, 0f);
				statusLabel.Width.Set(560f, 0f);
				statusLabel.Height.Set(22f, 0f);
				Append(statusLabel);
			}

			protected override void DrawSelf(SpriteBatch spriteBatch)
			{
				base.DrawSelf(spriteBatch);

				CalculatedStyle d = GetDimensions();
				var font = FontAssets.MouseText.Value;

				// Soft rarity radial glow behind the card
				float pulse = 0.7f + 0.3f * (float)Math.Sin(Main.timeForVisualEffects * 0.08f);
				Color aura = rarityColor * (0.10f * pulse);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle((int)d.X + 6, (int)d.Y + 6, (int)d.Width - 12, (int)d.Height - 12), aura);

				// Draw class emblem icon
				Texture2D icon = AugmentSlotElement.GetClassIcon(augment.Class);
				if (icon != null)
				{
					Vector2 center = new Vector2(d.X + 52f, d.Y + 54f);
					spriteBatch.Draw(icon, center, null, rarityColor, 0f, icon.Size() * 0.5f, 1.8f, SpriteEffects.None, 0f);
				}

				// Draw description text strictly starting at X = d.X + TextLeft (matching the name and [EPIC] tag)
				if (!string.IsNullOrEmpty(augment.Description))
				{
					var lines = AugmentColorText.Wrap(font, augment.Description, 530f, new Vector2(0.80f));
					float textX = d.X + TextLeft;
					float textY = d.Y + 76f;
					foreach (var line in lines)
					{
						ChatManager.DrawColorCodedStringWithShadow(spriteBatch, font, line, new Vector2(textX, textY), Color.White, 0f, Vector2.Zero, new Vector2(0.80f));
						textY += ChatManager.GetStringSize(font, line, new Vector2(0.80f)).Y + 3f;
					}
				}
			}
		}

		// Modern, sleek sci-fi button with smooth glass gradient and integrated badge
		private class ChamberActionButton : UIElement
		{
			private readonly string titleText;
			private readonly string costBadge;
			private readonly bool isPrimary;
			private readonly Action onClick;
			private bool isHovered;

			public ChamberActionButton(string titleText, string costBadge, bool isPrimary, Action onClick)
			{
				this.titleText = titleText;
				this.costBadge = costBadge;
				this.isPrimary = isPrimary;
				this.onClick = onClick;
			}

			public override void LeftClick(UIMouseEvent evt)
			{
				base.LeftClick(evt);
				SoundEngine.PlaySound(SoundID.MenuTick);
				onClick?.Invoke();
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
				CalculatedStyle d = GetDimensions();
				Texture2D pixel = TextureAssets.MagicPixel.Value;
				var font = FontAssets.MouseText.Value;

				float pulse = 0.6f + 0.4f * (float)Math.Sin(Main.timeForVisualEffects * 0.12f);
				Color bg = isHovered ? new Color(26, 38, 62) : new Color(16, 22, 38);
				Color border = isHovered
					? new Color(88, 150, 215)
					: (isPrimary ? Color.Lerp(new Color(42, 66, 102), new Color(76, 120, 178), pulse * 0.5f) : new Color(38, 56, 88));

				// Main background rect
				Rectangle rect = new Rectangle((int)d.X, (int)d.Y, (int)d.Width, (int)d.Height);
				spriteBatch.Draw(pixel, rect, bg * 0.96f);

				// Top bevel highlight line
				spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 1), new Color(74, 108, 152) * (isHovered ? 0.6f : 0.35f));

				// Clean 1px border
				int thickness = isHovered ? 2 : 1;
				spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), border);
				spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), border);
				spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), border);
				spriteBatch.Draw(pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), border);

				// Draw button content
				if (string.IsNullOrEmpty(costBadge))
				{
					// Single centered text (e.g. Return to Storage)
					Vector2 titleSize = ChatManager.GetStringSize(font, titleText, new Vector2(0.86f));
					Vector2 titlePos = new Vector2(d.X + (d.Width - titleSize.X) * 0.5f, d.Y + (d.Height - titleSize.Y) * 0.5f);
					Color textCol = isHovered ? Color.White : new Color(175, 195, 220);
					ChatManager.DrawColorCodedStringWithShadow(spriteBatch, font, titleText, titlePos, textCol, 0f, Vector2.Zero, new Vector2(0.86f));
				}
				else
				{
					// Primary action with integrated cost badge
					Vector2 titleSize = ChatManager.GetStringSize(font, titleText, new Vector2(0.86f));
					Vector2 costSize = ChatManager.GetStringSize(font, costBadge, new Vector2(0.78f));

					float badgePadH = 12f;
					float badgeW = costSize.X + badgePadH * 2f;
					float badgeH = 22f;
					float spacing = 14f;

					float totalW = titleSize.X + spacing + badgeW;
					float startX = d.X + (d.Width - totalW) * 0.5f;

					// Title text
					float titleY = d.Y + (d.Height - titleSize.Y) * 0.5f;
					Color titleCol = isHovered ? Color.White : new Color(225, 235, 245);
					ChatManager.DrawColorCodedStringWithShadow(spriteBatch, font, titleText, new Vector2(startX, titleY), titleCol, 0f, Vector2.Zero, new Vector2(0.86f));

					// Inset Cost Badge
					float badgeX = startX + titleSize.X + spacing;
					float badgeY = d.Y + (d.Height - badgeH) * 0.5f;
					Rectangle badgeRect = new Rectangle((int)badgeX, (int)badgeY, (int)badgeW, (int)badgeH);

					spriteBatch.Draw(pixel, badgeRect, new Color(10, 14, 24) * 0.9f);
					spriteBatch.Draw(pixel, new Rectangle(badgeRect.X, badgeRect.Y, badgeRect.Width, 1), new Color(212, 184, 114) * 0.4f);
					spriteBatch.Draw(pixel, new Rectangle(badgeRect.X, badgeRect.Bottom - 1, badgeRect.Width, 1), new Color(212, 184, 114) * 0.4f);
					spriteBatch.Draw(pixel, new Rectangle(badgeRect.X, badgeRect.Y, 1, badgeRect.Height), new Color(212, 184, 114) * 0.4f);
					spriteBatch.Draw(pixel, new Rectangle(badgeRect.Right - 1, badgeRect.Y, 1, badgeRect.Height), new Color(212, 184, 114) * 0.4f);

					Vector2 costPos = new Vector2(badgeX + badgePadH, badgeY + (badgeH - costSize.Y) * 0.5f);
					ChatManager.DrawColorCodedStringWithShadow(spriteBatch, font, $"[c/D4B872:{costBadge}]", costPos, Color.White, 0f, Vector2.Zero, new Vector2(0.78f));
				}
			}
		}

		private class TabButton : UIPanel
		{
			private readonly Action onClick;

			public TabButton(string text, Action onClick)
			{
				this.onClick = onClick;

				SetPadding(0f);
				BackgroundColor = new Color(18, 24, 44);
				BorderColor = new Color(42, 64, 102);

				var label = new UIText(text, 0.82f)
				{
					HAlign = 0.5f,
					VAlign = 0.5f,
					TextColor = new Color(165, 195, 230)
				};
				Append(label);
			}

			public override void LeftClick(UIMouseEvent evt)
			{
				base.LeftClick(evt);
				SoundEngine.PlaySound(SoundID.MenuTick);
				onClick?.Invoke();
			}

			public override void MouseOver(UIMouseEvent evt)
			{
				base.MouseOver(evt);
				BackgroundColor = new Color(28, 38, 70);
			}

			public override void MouseOut(UIMouseEvent evt)
			{
				base.MouseOut(evt);
				BackgroundColor = new Color(18, 24, 44);
			}
		}

		private class GachaBackPanel : UIPanel
		{
			protected override void DrawSelf(SpriteBatch spriteBatch)
			{
				base.DrawSelf(spriteBatch);
				PanelChrome.DrawFrame(spriteBatch, GetDimensions().ToRectangle());
			}
		}
	}
}
