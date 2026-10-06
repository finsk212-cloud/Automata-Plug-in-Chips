using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ReLogic.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.Events;
using Terraria.ID;
using Terraria.ModLoader;

namespace Augments
{
	public class PinnedStatWidget
	{
		public AugmentHiddenStatsDrawer.StatTooltipType StatType;
		public Vector2 Position;
		public bool IsDragging;
		public Vector2 DragOffset;
		public float Width = 140f;
		public float Height = 22f;
		public double LastClickTime = 0;
	}

	// NieR:Automata themed slide-out Hidden Stats Diagnostics Drawer.
	// Compact, semi-transparent HUD displaying live hidden gameplay variables
	// with rich hover tooltips and double-click pinning to screen.
	public static class AugmentHiddenStatsDrawer
	{
		public enum StatTooltipType
		{
			TotalLuck,
			Ladybug,
			Torches,
			GardenGnome,
			CoinLuck,
			DropRateBoost,
			FishingPower,
			FishingTime,
			FishingWeather,
			FishingMoon,
			CrateChance,
			PoolRequirement,
			ArmorPenetration,
			DamageReduction,
			LifeRegen,
			StationaryBonus,
			Invincibility,
			EnemyAggro,
			DamageRollLuck,
			FlightDuration,
			MiningSpeed,
			ItemGrabRange,
			EnemySpawnRate
		}

		private const float PanelWidth = 275f;
		private const float PanelHeight = 422f;
		private const float TabWidth = 16f;
		private const float TabHeight = 38f;

		private static float slideProgress = 0f;
		private static bool isOpen = false;
		private static bool wasMouseLeft = false;
		private static float hoverTimer = 0f;

		private static double totalTimer = 0;
		private static StatTooltipType? lastDrawerRowClicked = null;
		private static double lastDrawerRowClickTime = 0;
		private static StatTooltipType? currentHoveredRowStat = null;

		private static readonly Color ThemeGold = new Color(245, 180, 50);
		private static readonly Color MutedSlate = new Color(148, 163, 184);
		private static readonly Color ValueWhite = new Color(245, 248, 252);
		private static readonly Color LightGreen = new Color(134, 239, 172);
		private static readonly Color BrightGreen = new Color(34, 197, 94);
		private static readonly Color LightRed = new Color(252, 165, 165);
		private static readonly Color BrightRed = new Color(239, 68, 68);

		// Tooltip state captured during Draw Diagnostics
		private static string tooltipTitle = null;
		private static readonly List<(string text, Color color)> tooltipLines = new List<(string, Color)>();

		// Pinned screen widgets dictionary
		private static readonly Dictionary<StatTooltipType, PinnedStatWidget> pinnedWidgets = new Dictionary<StatTooltipType, PinnedStatWidget>();

		public static void Update(GameTime gameTime)
		{
			if (Main.dedServ || Main.gameMenu)
				return;

			if (AugmentConfig.Instance != null && !AugmentConfig.Instance.EnableHiddenStatsPanel)
			{
				isOpen = false;
				slideProgress = 0f;
				return;
			}

			try
			{
				float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
				totalTimer += dt;

				Player player = Main.LocalPlayer;
				if (player == null || !player.active)
					return;

				bool isRightDocked = AugmentConfig.Instance != null && AugmentConfig.Instance.DockOnRightSide;
				float startY = Math.Max(10f, (Main.screenHeight - PanelHeight) / 2f);
				float tabY = startY + (PanelHeight - TabHeight) / 2f;

				float currentPanelX = isRightDocked
					? Main.screenWidth - (PanelWidth * slideProgress)
					: -PanelWidth + (PanelWidth * slideProgress);

				float currentTabX = isRightDocked
					? currentPanelX - TabWidth
					: currentPanelX + PanelWidth;

				Rectangle tabRect = new Rectangle((int)currentTabX, (int)tabY, (int)TabWidth, (int)TabHeight);
				Rectangle panelRect = new Rectangle((int)currentPanelX, (int)startY, (int)PanelWidth, (int)PanelHeight);

				Point mouse = new Point(Main.mouseX, Main.mouseY);
				bool mouseOverTab = tabRect.Contains(mouse);
				bool mouseOverPanel = slideProgress > 0.1f && panelRect.Contains(mouse);
				bool justClicked = Main.mouseLeft && !wasMouseLeft;

				// 1. UPDATE PINNED SCREEN WIDGETS (drag & double-click to remove)
				StatTooltipType? widgetToRemove = null;
				foreach (var w in pinnedWidgets.Values)
				{
					Rectangle widgetRect = new Rectangle((int)w.Position.X, (int)w.Position.Y, (int)w.Width, (int)w.Height);
					bool mouseOverWidget = widgetRect.Contains(mouse);

					if (w.IsDragging)
					{
						player.mouseInterface = true;
						w.Position = new Vector2(mouse.X, mouse.Y) - w.DragOffset;
						w.Position.X = Math.Clamp(w.Position.X, 4f, Math.Max(4f, Main.screenWidth - w.Width - 4f));
						w.Position.Y = Math.Clamp(w.Position.Y, 4f, Math.Max(4f, Main.screenHeight - w.Height - 4f));

						if (!Main.mouseLeft)
						{
							w.IsDragging = false;
						}
					}
					else if (mouseOverWidget)
					{
						player.mouseInterface = true;

						if (justClicked)
						{
							if ((totalTimer - w.LastClickTime) < 0.35)
							{
								// Double-click pinned widget: REMOVE IT
								widgetToRemove = w.StatType;
								SoundEngine.PlaySound(SoundID.MenuClose);
							}
							else
							{
								// Start dragging
								w.IsDragging = true;
								w.DragOffset = new Vector2(mouse.X, mouse.Y) - w.Position;
								w.LastClickTime = totalTimer;
							}
						}
					}
				}

				if (widgetToRemove.HasValue)
				{
					pinnedWidgets.Remove(widgetToRemove.Value);
				}

				// 2. DRAWER ROW DOUBLE-CLICK TO PIN / UNPIN
				if (justClicked && mouseOverPanel && currentHoveredRowStat.HasValue)
				{
					StatTooltipType stat = currentHoveredRowStat.Value;
					if (lastDrawerRowClicked == stat && (totalTimer - lastDrawerRowClickTime) < 0.35)
					{
						// Double-click detected on stat row!
						TogglePin(stat, isRightDocked);
						lastDrawerRowClicked = null;
						lastDrawerRowClickTime = 0;
					}
					else
					{
						lastDrawerRowClicked = stat;
						lastDrawerRowClickTime = totalTimer;
					}
				}

				// 3. Handle ESC key to close drawer
				if (isOpen && Main.keyState.IsKeyDown(Keys.Escape))
				{
					isOpen = false;
					SoundEngine.PlaySound(SoundID.MenuClose);
				}

				bool isHoverMode = AugmentConfig.Instance != null && AugmentConfig.Instance.DrawerOpenMode == TelemetryOpenMode.HoverToOpen;

				if (isHoverMode)
				{
					if (mouseOverTab || mouseOverPanel)
					{
						hoverTimer = 0.25f;
						if (!isOpen)
						{
							isOpen = true;
							SoundEngine.PlaySound(SoundID.MenuOpen);
						}
					}
					else if (isOpen)
					{
						hoverTimer -= dt;
						if (hoverTimer <= 0f)
						{
							isOpen = false;
							SoundEngine.PlaySound(SoundID.MenuClose);
						}
					}
				}
				else
				{
					// ClickToToggle mode (Accident-proof for combat)
					if (justClicked)
					{
						if (mouseOverTab)
						{
							isOpen = !isOpen;
							SoundEngine.PlaySound(isOpen ? SoundID.MenuOpen : SoundID.MenuClose);
						}
						else if (isOpen && !mouseOverPanel && pinnedWidgets.Count == 0)
						{
							// Click outside panel closes it (only if not clicking a pinned widget)
							isOpen = false;
							SoundEngine.PlaySound(SoundID.MenuClose);
						}
					}
				}

				// Smooth slide interpolation
				float targetProgress = isOpen ? 1f : 0f;
				slideProgress = MathHelper.Lerp(slideProgress, targetProgress, 0.22f);
				if (Math.Abs(slideProgress - targetProgress) < 0.003f)
					slideProgress = targetProgress;

				// Prevent weapon attacks when interacting with tab or open panel
				if (mouseOverTab || (isOpen && mouseOverPanel))
				{
					player.mouseInterface = true;
				}

				wasMouseLeft = Main.mouseLeft;
			}
			catch
			{
				// Defensive catch ensures UI calculations never block the main game thread or player input
			}
		}

		private static void TogglePin(StatTooltipType stat, bool isRightDocked)
		{
			if (pinnedWidgets.ContainsKey(stat))
			{
				pinnedWidgets.Remove(stat);
				SoundEngine.PlaySound(SoundID.MenuClose);
			}
			else
			{
				int count = pinnedWidgets.Count;
				float startX = isRightDocked ? 20f : Main.screenWidth - 170f;
				float startY = 120f + (count * 28f);
				pinnedWidgets[stat] = new PinnedStatWidget
				{
					StatType = stat,
					Position = new Vector2(startX, startY)
				};
				SoundEngine.PlaySound(SoundID.MenuOpen);
			}
		}

		public static void Draw(SpriteBatch spriteBatch)
		{
			if (Main.dedServ || Main.gameMenu)
				return;

			if (AugmentConfig.Instance != null && !AugmentConfig.Instance.EnableHiddenStatsPanel)
				return;

			try
			{
				Player player = Main.LocalPlayer;
				if (player == null || !player.active || player.dead)
					return;

				bool isRightDocked = AugmentConfig.Instance != null && AugmentConfig.Instance.DockOnRightSide;
				float startY = Math.Max(10f, (Main.screenHeight - PanelHeight) / 2f);
				float tabY = startY + (PanelHeight - TabHeight) / 2f;

				float currentPanelX = isRightDocked
					? Main.screenWidth - (PanelWidth * slideProgress)
					: -PanelWidth + (PanelWidth * slideProgress);

				float currentTabX = isRightDocked
					? currentPanelX - TabWidth
					: currentPanelX + PanelWidth;

				Rectangle tabRect = new Rectangle((int)currentTabX, (int)tabY, (int)TabWidth, (int)TabHeight);
				Rectangle panelRect = new Rectangle((int)currentPanelX, (int)startY, (int)PanelWidth, (int)PanelHeight);

				Point mouse = new Point(Main.mouseX, Main.mouseY);
				bool mouseOverTab = tabRect.Contains(mouse);

				// Reset active tooltip & hovered row state
				tooltipTitle = null;
				tooltipLines.Clear();
				currentHoveredRowStat = null;

				// 1. Draw Arrow Tab
				DrawArrowTab(spriteBatch, tabRect, isRightDocked, mouseOverTab);

				// 2. Draw Diagnostics Panel (if sliding out or open)
				if (slideProgress > 0.01f)
				{
					DrawDiagnosticsPanel(spriteBatch, panelRect, player, slideProgress, isRightDocked, mouse);

					// 3. Draw Hover Tooltip if hovering a stat row
					if (!string.IsNullOrEmpty(tooltipTitle) && tooltipLines.Count > 0 && slideProgress > 0.85f)
					{
						DrawStatTooltip(spriteBatch, panelRect, isRightDocked, mouse);
					}
				}

				// 4. Draw All Pinned Widgets (Always visible on screen)
				DrawPinnedWidgets(spriteBatch, player);
			}
			catch
			{
				// Defensive catch ensures drawer rendering exceptions never crash the interface layer
			}
		}

		private static void DrawArrowTab(SpriteBatch spriteBatch, Rectangle tabRect, bool isRightDocked, bool isHovered)
		{
			Color bgColor = isHovered ? new Color(16, 24, 36, 220) : new Color(8, 12, 18, 180);
			Color borderColor = isHovered ? Color.Lerp(ThemeGold, Color.White, 0.45f) : ThemeGold * 0.70f;

			// Tab ambient shadow
			spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(tabRect.X - 1, tabRect.Y - 1, tabRect.Width + 2, tabRect.Height + 2), new Color(0, 0, 0, 100));

			// Translucent chassis fill
			spriteBatch.Draw(TextureAssets.MagicPixel.Value, tabRect, bgColor);

			// High-tech 1px tab border with corner notch
			DrawHighTechBorder(spriteBatch, tabRect, borderColor);

			// Razor-sharp geometric chevron arrow
			int cx = tabRect.X + tabRect.Width / 2;
			int cy = tabRect.Y + tabRect.Height / 2;
			Color chevronColor = isHovered ? Color.White : ThemeGold;

			// When drawer is closed: point inward (open). When drawer is open: point outward (close).
			bool pointRight = isRightDocked ? isOpen : !isOpen;

			// Draw clean 2px-thick pixel chevron (9px tall)
			for (int dy = -4; dy <= 4; dy++)
			{
				int offset = 2 - Math.Abs(dy) / 2;
				int x = pointRight ? (cx - 2 + offset) : (cx + 1 - offset);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(x, cy + dy, 2, 1), chevronColor);
			}
		}

		private static void DrawDiagnosticsPanel(SpriteBatch spriteBatch, Rectangle rect, Player player, float progress, bool isRightDocked, Point mouse)
		{
			// Soft ambient drop shadow
			spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(rect.X - 2, rect.Y - 2, rect.Width + 4, rect.Height + 4), new Color(0, 0, 0, (int)(120 * progress)));

			// Translucent Chassis Fill (see-through dark slate, does not obstruct game)
			Color panelBg = new Color(8, 14, 22, 185);
			spriteBatch.Draw(TextureAssets.MagicPixel.Value, rect, panelBg * progress);

			// Subtle scanline tint
			spriteBatch.Draw(TextureAssets.MagicPixel.Value, rect, ThemeGold * (0.025f * progress));

			// High-tech 1px outer border with corner brackets
			DrawHighTechBorder(spriteBatch, rect, ThemeGold * (0.75f * progress));

			DynamicSpriteFont font = FontAssets.MouseText.Value;
			float leftX = rect.X + 18f;
			float curY = rect.Y + 8f;
			float rightAlignX = rect.Right - 12f;

			// --- HEADER ---
			string headerTitle = "[ DIAGNOSTICS ]";
			DrawCleanText(spriteBatch, font, headerTitle, new Vector2(leftX, curY), ThemeGold, progress, 0.78f);
			curY += 16f;

			DrawDivider(spriteBatch, rect, curY, ThemeGold, progress);
			curY += 6f;

			// --- SECTION 1: LUCK & DROPS ---
			DrawCleanText(spriteBatch, font, "LUCK & DROPS", new Vector2(leftX, curY), ThemeGold, progress, 0.72f);
			curY += 15f;

			float netLuck = player.luck;
			string luckRating = netLuck switch
			{
				>= 0.35f => "Very Lucky",
				> 0.05f => "Lucky",
				< -0.35f => "Cursed",
				< -0.05f => "Unlucky",
				_ => "Neutral"
			};

			Color luckColor = GetStatColor(netLuck, 0.05f, 0.25f);
			string luckText = (netLuck >= 0f ? $"+{netLuck:0.00}" : $"{netLuck:0.00}") + $" ({luckRating})";
			RenderInteractiveRow(spriteBatch, font, rect, leftX, rightAlignX, curY, "Total Luck", luckText, luckColor, progress, 0.70f, mouse, StatTooltipType.TotalLuck);
			curY += 13f;

			// Ladybug
			float ladybugVal = player.ladyBugLuckTimeLeft > 0 ? 0.2f : (player.ladyBugLuckTimeLeft < 0 ? -0.2f : 0f);
			Color ladybugCol = GetStatColor(ladybugVal, 0.05f, 0.15f);
			string ladybugStr = player.ladyBugLuckTimeLeft > 0 ? "+0.20 (Lucky)" : (player.ladyBugLuckTimeLeft < 0 ? "-0.20 (Unlucky)" : "None");
			RenderInteractiveRow(spriteBatch, font, rect, leftX, rightAlignX, curY, "  Ladybug", ladybugStr, ladybugCol, progress, 0.65f, mouse, StatTooltipType.Ladybug);
			curY += 12f;

			// Torches
			Color torchCol = GetStatColor(player.torchLuck, 0.02f, 0.10f);
			string torchStr = player.torchLuck != 0 ? (player.torchLuck > 0 ? $"+{player.torchLuck:0.00}" : $"{player.torchLuck:0.00}") : "0.00";
			RenderInteractiveRow(spriteBatch, font, rect, leftX, rightAlignX, curY, "  Torches", torchStr, torchCol, progress, 0.65f, mouse, StatTooltipType.Torches);
			curY += 12f;

			// Garden Gnome
			Color gnomeCol = player.HasGardenGnomeNearby ? BrightGreen : ValueWhite;
			RenderInteractiveRow(spriteBatch, font, rect, leftX, rightAlignX, curY, "  Garden Gnome", player.HasGardenGnomeNearby ? "+0.20 (Active)" : "None", gnomeCol, progress, 0.65f, mouse, StatTooltipType.GardenGnome);
			curY += 12f;

			// Shimmer Coin
			Color coinCol = player.coinLuck > 0 ? LightGreen : ValueWhite;
			RenderInteractiveRow(spriteBatch, font, rect, leftX, rightAlignX, curY, "  Coin Luck", player.coinLuck > 0 ? $"+{player.coinLuck:0.00}" : "None", coinCol, progress, 0.65f, mouse, StatTooltipType.CoinLuck);
			curY += 12f;

			// Rare Drop Chance Boost
			float dropBoost = netLuck * 25f;
			Color dropCol = GetStatColor(dropBoost, 1f, 6f);
			RenderInteractiveRow(spriteBatch, font, rect, leftX, rightAlignX, curY, "  Drop Rate Boost", (dropBoost >= 0 ? $"+{dropBoost:0.0}%" : $"{dropBoost:0.0}%"), dropCol, progress, 0.65f, mouse, StatTooltipType.DropRateBoost);
			curY += 15f;

			DrawDivider(spriteBatch, rect, curY, ThemeGold, progress);
			curY += 6f;

			// --- SECTION 2: FISHING ---
			DrawCleanText(spriteBatch, font, "FISHING", new Vector2(leftX, curY), ThemeGold, progress, 0.72f);
			curY += 15f;

			// Live Fishing Power (Base + Multipliers)
			int basePower = player.fishingSkill;
			var (timeText, timeDelta) = GetFishingTimeMultiplier();
			var (weatherText, weatherDelta) = GetFishingWeatherMultiplier();
			var (moonText, moonDelta) = GetFishingMoonMultiplier();
			float totalFishingMult = (1f + timeDelta) * (1f + weatherDelta) * (1f + moonDelta);
			int effectivePower = (int)Math.Round(basePower * totalFishingMult);

			string fishPowerText = basePower > 0 ? $"{effectivePower} ({basePower} Base)" : "0 (No Rod / Bait)";
			Color fishPowerCol = basePower > 0 ? BrightGreen : ValueWhite;
			RenderInteractiveRow(spriteBatch, font, rect, leftX, rightAlignX, curY, "Fishing Power", fishPowerText, fishPowerCol, progress, 0.70f, mouse, StatTooltipType.FishingPower);
			curY += 13f;

			// Time modifier
			Color timeCol = GetStatColor(timeDelta, 0.05f, 0.20f);
			RenderInteractiveRow(spriteBatch, font, rect, leftX, rightAlignX, curY, "  Time of Day", timeText, timeCol, progress, 0.65f, mouse, StatTooltipType.FishingTime);
			curY += 12f;

			// Weather modifier
			Color weatherCol = GetStatColor(weatherDelta, 0.05f, 0.20f);
			RenderInteractiveRow(spriteBatch, font, rect, leftX, rightAlignX, curY, "  Weather", weatherText, weatherCol, progress, 0.65f, mouse, StatTooltipType.FishingWeather);
			curY += 12f;

			// Moon modifier
			Color moonCol = GetStatColor(moonDelta, 0.02f, 0.08f);
			RenderInteractiveRow(spriteBatch, font, rect, leftX, rightAlignX, curY, "  Moon Phase", moonText, moonCol, progress, 0.65f, mouse, StatTooltipType.FishingMoon);
			curY += 12f;

			// Crate chance
			Color crateCol = player.cratePotion ? BrightGreen : ValueWhite;
			RenderInteractiveRow(spriteBatch, font, rect, leftX, rightAlignX, curY, "  Crate Chance", player.cratePotion ? "25% (Potion)" : "10% (Base)", crateCol, progress, 0.65f, mouse, StatTooltipType.CrateChance);
			curY += 12f;

			// Water pool requirement
			RenderInteractiveRow(spriteBatch, font, rect, leftX, rightAlignX, curY, "  Pool Requirement", "300+ Water Tiles", ValueWhite, progress, 0.65f, mouse, StatTooltipType.PoolRequirement);
			curY += 15f;

			DrawDivider(spriteBatch, rect, curY, ThemeGold, progress);
			curY += 6f;

			// --- SECTION 3: COMBAT & SURVIVAL ---
			DrawCleanText(spriteBatch, font, "COMBAT & SURVIVAL", new Vector2(leftX, curY), ThemeGold, progress, 0.72f);
			curY += 15f;

			// Flat Armor Penetration
			var (armorPen, penClass) = GetEffectiveArmorPenetration(player);
			Color penCol = GetStatColor(armorPen, 1f, 15f);
			string penText = armorPen > 0
				? (string.IsNullOrEmpty(penClass) ? $"{armorPen} DEF Ignored" : $"{armorPen} DEF ({penClass})")
				: "0";
			RenderInteractiveRow(spriteBatch, font, rect, leftX, rightAlignX, curY, "Armor Penetration", penText, penCol, progress, 0.70f, mouse, StatTooltipType.ArmorPenetration);
			curY += 13f;

			// Damage Reduction (% DR / Endurance)
			float dr = player.endurance;
			float drPct = dr * 100f;
			string drText = dr > 0f ? $"+{drPct:0.0}% DR" : "0.0% (Base)";
			Color drCol = dr > 0f ? (drPct >= 20f ? BrightGreen : LightGreen) : ValueWhite;
			RenderInteractiveRow(spriteBatch, font, rect, leftX, rightAlignX, curY, "Damage Reduction", drText, drCol, progress, 0.65f, mouse, StatTooltipType.DamageReduction);
			curY += 12f;

			// Natural Life Regen (HP/s)
			float hpPerSec = player.lifeRegen / 2f;
			string regenText = (hpPerSec >= 0 ? $"+{hpPerSec:0.0}" : $"{hpPerSec:0.0}") + " HP/s";
			Color regenCol = hpPerSec > 0 ? (hpPerSec >= 4f ? BrightGreen : LightGreen) : (hpPerSec < 0 ? BrightRed : ValueWhite);
			RenderInteractiveRow(spriteBatch, font, rect, leftX, rightAlignX, curY, "Life Regen", regenText, regenCol, progress, 0.65f, mouse, StatTooltipType.LifeRegen);
			curY += 12f;

			// Stationary Regen Bonus
			bool isStationary = player.velocity == Vector2.Zero;
			string stationText = isStationary ? "2x (Active Idle)" : "2x (When Idle)";
			Color stationCol = isStationary ? BrightGreen : ValueWhite;
			RenderInteractiveRow(spriteBatch, font, rect, leftX, rightAlignX, curY, "  Standing Bonus", stationText, stationCol, progress, 0.65f, mouse, StatTooltipType.StationaryBonus);
			curY += 12f;

			// Invincibility (Shows live countdown if currently hit)
			string iFrameText;
			Color iFrameCol;
			if (player.immuneTime > 0)
			{
				iFrameText = $"[IMMUNE] {player.immuneTime}t left";
				iFrameCol = BrightGreen;
			}
			else
			{
				iFrameText = player.longInvince ? "1.33s (Extended)" : "0.67s (Normal)";
				iFrameCol = player.longInvince ? LightGreen : ValueWhite;
			}
			RenderInteractiveRow(spriteBatch, font, rect, leftX, rightAlignX, curY, "Invincibility", iFrameText, iFrameCol, progress, 0.65f, mouse, StatTooltipType.Invincibility);
			curY += 12f;

			// Aggro / Threat
			int aggro = player.aggro;
			Color aggroCol = aggro < 0 ? LightGreen : aggro > 0 ? LightRed : ValueWhite;
			string aggroText = aggro < 0 ? $"{aggro} (Stealth)" : aggro > 0 ? $"+{aggro} (Targeted)" : "Neutral";
			RenderInteractiveRow(spriteBatch, font, rect, leftX, rightAlignX, curY, "Enemy Aggro", aggroText, aggroCol, progress, 0.65f, mouse, StatTooltipType.EnemyAggro);
			curY += 12f;

			// Damage Roll Luck
			float rollBiasPct = Math.Abs(netLuck) * 100f;
			string biasText = netLuck > 0
				? $"+{rollBiasPct:0.#}% High Roll Chance"
				: netLuck < 0
					? $"-{rollBiasPct:0.#}% Low Roll Bias"
					: "Normal (85-115%)";
			Color biasCol = GetStatColor(netLuck, 0.05f, 0.25f);
			RenderInteractiveRow(spriteBatch, font, rect, leftX, rightAlignX, curY, "Damage Roll Luck", biasText, biasCol, progress, 0.65f, mouse, StatTooltipType.DamageRollLuck);
			curY += 15f;

			DrawDivider(spriteBatch, rect, curY, ThemeGold, progress);
			curY += 6f;

			// --- SECTION 4: MOBILITY & UTILITY ---
			DrawCleanText(spriteBatch, font, "MOBILITY & UTILITY", new Vector2(leftX, curY), ThemeGold, progress, 0.72f);
			curY += 15f;

			// Wing & Boot Flight Time
			int wingTicks = player.wingTimeMax;
			int rocketTicks = player.rocketTimeMax;
			int totalFlightTicks = wingTicks + rocketTicks;
			float flightSeconds = totalFlightTicks / 60f;
			string flightText = flightSeconds > 0 ? $"{flightSeconds:0.00}s ({totalFlightTicks}t)" : "0s (No Wings)";
			Color flightCol = flightSeconds >= 2.5f ? BrightGreen : (flightSeconds > 0 ? LightGreen : ValueWhite);
			RenderInteractiveRow(spriteBatch, font, rect, leftX, rightAlignX, curY, "Flight Duration", flightText, flightCol, progress, 0.65f, mouse, StatTooltipType.FlightDuration);
			curY += 12f;

			// Mining Speed (% delay reduction)
			float pickSpeedBonus = (1f - player.pickSpeed) * 100f;
			string miningText = pickSpeedBonus > 0 ? $"+{pickSpeedBonus:0}% Faster" : "0% (Base Delay)";
			Color miningCol = pickSpeedBonus >= 40f ? BrightGreen : (pickSpeedBonus > 0 ? LightGreen : ValueWhite);
			RenderInteractiveRow(spriteBatch, font, rect, leftX, rightAlignX, curY, "Mining Speed", miningText, miningCol, progress, 0.65f, mouse, StatTooltipType.MiningSpeed);
			curY += 12f;

			// Item Grab Range
			int grabPixels = player.GetItemGrabRange(new Item());
			float grabTiles = grabPixels / 16f;
			string grabText = $"{grabTiles:0.1} Tiles";
			Color grabCol = grabTiles >= 8f ? BrightGreen : (grabTiles > 3.5f ? LightGreen : ValueWhite);
			RenderInteractiveRow(spriteBatch, font, rect, leftX, rightAlignX, curY, "Item Grab Range", grabText, grabCol, progress, 0.65f, mouse, StatTooltipType.ItemGrabRange);
			curY += 12f;

			// Enemy Spawn Rate / Pressure
			float spawnRateMult = 1f;
			if (player.enemySpawns) spawnRateMult *= 2.0f;
			if (player.ZoneWaterCandle) spawnRateMult *= 1.33f;
			if (player.calmed) spawnRateMult *= 0.83f;
			if (player.ZonePeaceCandle) spawnRateMult *= 0.77f;
			if (player.sunflower) spawnRateMult *= 0.83f;
			if (player.townNPCs > 0 && !player.ZoneShadowCandle)
			{
				spawnRateMult *= (player.townNPCs >= 3 ? 0.10f : 0.40f);
			}

			float spawnDelta = (spawnRateMult - 1f) * 100f;
			string spawnText = spawnDelta > 0 ? $"+{spawnDelta:0}% Rate" : (spawnDelta < 0 ? $"{spawnDelta:0}% Rate" : "1.0x (Normal)");
			Color spawnCol = spawnDelta > 0 ? BrightGreen : (spawnDelta < 0 ? LightRed : ValueWhite);
			RenderInteractiveRow(spriteBatch, font, rect, leftX, rightAlignX, curY, "Enemy Spawns", spawnText, spawnCol, progress, 0.65f, mouse, StatTooltipType.EnemySpawnRate);
			curY += 15f;

			// --- FOOTER NOTE (Positioned dynamically below utility section - never overlaps) ---
			DrawDivider(spriteBatch, rect, curY, ThemeGold, progress);
			curY += 6f;

			string modeHint = (AugmentConfig.Instance != null && AugmentConfig.Instance.DrawerOpenMode == TelemetryOpenMode.HoverToOpen)
				? "[ Mode: Hover to Open ]"
				: "[ ESC or Click Tab to Close ]";
			DrawCleanText(spriteBatch, font, modeHint, new Vector2(leftX, curY), MutedSlate * 0.75f, progress, 0.60f);
		}

		private static void RenderInteractiveRow(SpriteBatch spriteBatch, DynamicSpriteFont font, Rectangle panelRect, float leftX, float rightX, float y, string label, string value, Color valueColor, float progress, float scale, Point mouse, StatTooltipType tooltipType)
		{
			// Row bounds check
			Rectangle rowRect = new Rectangle(panelRect.X + 8, (int)y - 1, panelRect.Width - 16, 12);
			bool isHovered = rowRect.Contains(mouse);

			if (isHovered && progress > 0.8f)
			{
				// Set hovered row for double-click tracking
				currentHoveredRowStat = tooltipType;

				// Subtle amber row highlight bar
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, rowRect, ThemeGold * (0.12f * progress));

				// Populate active tooltip
				PopulateStatTooltip(tooltipType);
			}

			// Pinned status indicator in drawer
			bool isPinned = pinnedWidgets.ContainsKey(tooltipType);
			if (isPinned)
			{
				DrawPixelDiamond(spriteBatch, (int)panelRect.X + 9, (int)y + 6, 2, ThemeGold * progress);
			}

			// Label
			Color labelColor = isHovered ? Color.White : (isPinned ? ThemeGold * 0.95f : MutedSlate);
			DrawCleanText(spriteBatch, font, label, new Vector2(leftX, y), labelColor, progress, scale);

			// Right-aligned Value
			Vector2 valSz = font.MeasureString(value) * scale;
			Vector2 valPos = new Vector2(rightX - valSz.X, y);
			DrawCleanText(spriteBatch, font, value, valPos, valueColor, progress, scale);
		}

		private static void DrawStatTooltip(SpriteBatch spriteBatch, Rectangle panelRect, bool isRightDocked, Point mouse)
		{
			DynamicSpriteFont font = FontAssets.MouseText.Value;
			const float lineSpacing = 13f;
			const float headerSpacing = 28f;
			float tipHeight = headerSpacing + (tooltipLines.Count * lineSpacing) + 20f;

			// Calculate maximum line width dynamically so box always encloses text
			float maxLineWidth = 0f;
			Vector2 titleSz = font.MeasureString($"[ {tooltipTitle} ]") * 0.76f;
			maxLineWidth = Math.Max(maxLineWidth, titleSz.X);

			foreach (var (line, _) in tooltipLines)
			{
				if (!string.IsNullOrEmpty(line))
				{
					Vector2 sz = font.MeasureString(line) * 0.65f;
					maxLineWidth = Math.Max(maxLineWidth, sz.X);
				}
			}

			// Measure hint line
			string hintText = "• Double-click to pin/unpin on screen";
			Vector2 hintSz = font.MeasureString(hintText) * 0.60f;
			maxLineWidth = Math.Max(maxLineWidth, hintSz.X);

			// Provide 14px padding left + 18px clearance on right
			float tipWidth = (float)Math.Ceiling(maxLineWidth + 32f);

			// Position next to panel, aligned with mouse Y
			float tipX = isRightDocked
				? panelRect.Left - tipWidth - 12f
				: panelRect.Right + 12f;

			// If tooltip extends off the right screen edge, flip it to the left side of the panel
			if (!isRightDocked && tipX + tipWidth > Main.screenWidth - 10f)
			{
				tipX = panelRect.Left - tipWidth - 12f;
			}

			// Screen edge boundary clamping
			tipX = Math.Clamp(tipX, 10f, Math.Max(10f, Main.screenWidth - tipWidth - 10f));

			float tipY = Math.Clamp(mouse.Y - 20f, 10f, Math.Max(10f, Main.screenHeight - tipHeight - 10f));
			Rectangle tipRect = new Rectangle((int)tipX, (int)tipY, (int)tipWidth, (int)tipHeight);

			// Drop shadow
			spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(tipRect.X - 2, tipRect.Y - 2, tipRect.Width + 4, tipRect.Height + 4), new Color(0, 0, 0, 160));

			// Translucent chassis
			spriteBatch.Draw(TextureAssets.MagicPixel.Value, tipRect, new Color(8, 14, 22, 240));

			// High-tech 1px border with corner brackets
			DrawHighTechBorder(spriteBatch, tipRect, ThemeGold * 0.85f);

			float textX = tipRect.X + 12f;
			float curY = tipRect.Y + 8f;

			// Title
			DrawCleanText(spriteBatch, font, $"[ {tooltipTitle} ]", new Vector2(textX, curY), ThemeGold, 1f, 0.76f);
			curY += 16f;

			// Hairline divider
			int divW = tipRect.Width - 24;
			spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(tipRect.X + 12, (int)curY, divW, 1), ThemeGold * 0.30f);
			curY += 6f;

			// Content lines
			foreach (var (line, col) in tooltipLines)
			{
				if (string.IsNullOrEmpty(line))
				{
					curY += 4f;
					continue;
				}

				DrawCleanText(spriteBatch, font, line, new Vector2(textX, curY), col, 1f, 0.65f);
				curY += lineSpacing;
			}

			// Double-click pin hint at bottom of tooltip
			curY += 4f;
			DrawCleanText(spriteBatch, font, hintText, new Vector2(textX, curY), ThemeGold * 0.90f, 1f, 0.60f);
		}

		private static void DrawPinnedWidgets(SpriteBatch spriteBatch, Player player)
		{
			if (pinnedWidgets.Count == 0)
				return;

			DynamicSpriteFont font = FontAssets.MouseText.Value;
			Point mouse = new Point(Main.mouseX, Main.mouseY);

			foreach (var w in pinnedWidgets.Values)
			{
				var (label, value, valColor) = GetStatDisplayData(w.StatType, player);

				Vector2 labelSz = font.MeasureString(label) * 0.65f;
				Vector2 sepSz = font.MeasureString(" • ") * 0.65f;
				Vector2 valSz = font.MeasureString(value) * 0.65f;

				w.Width = (float)Math.Ceiling(18f + labelSz.X + sepSz.X + valSz.X + 12f);
				w.Height = 22f;

				Rectangle rect = new Rectangle((int)w.Position.X, (int)w.Position.Y, (int)w.Width, (int)w.Height);
				bool hovered = rect.Contains(mouse);

				// 1. Ambient drop shadow
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(rect.X - 1, rect.Y - 1, rect.Width + 2, rect.Height + 2), new Color(0, 0, 0, 140));

				// 2. Translucent dark chassis (sleek NieR styling)
				Color bgCol = hovered || w.IsDragging ? new Color(16, 24, 36, 235) : new Color(8, 14, 22, 205);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, rect, bgCol);

				// 3. 1px High-tech border with corner notches
				Color borderCol = w.IsDragging ? Color.White : (hovered ? Color.Lerp(ThemeGold, Color.White, 0.45f) : ThemeGold * 0.70f);
				DrawHighTechBorder(spriteBatch, rect, borderCol);

				// 4. Diamond pin marker (crisp pixel diamond, vertically centered)
				int cy = (int)(rect.Y + rect.Height / 2f);
				DrawPixelDiamond(spriteBatch, rect.X + 9, cy, 2, ThemeGold);

				// 5. Label
				float textX = rect.X + 18f;
				float textY = rect.Y + 3.5f;
				DrawCleanText(spriteBatch, font, label, new Vector2(textX, textY), MutedSlate, 1f, 0.65f);

				// 6. Separator dot
				textX += labelSz.X;
				DrawCleanText(spriteBatch, font, " • ", new Vector2(textX, textY), ThemeGold * 0.45f, 1f, 0.65f);

				// 7. Value
				textX += sepSz.X;
				DrawCleanText(spriteBatch, font, value, new Vector2(textX, textY), valColor, 1f, 0.65f);
			}
		}

		private static (string label, string value, Color color) GetStatDisplayData(StatTooltipType type, Player player)
		{
			float netLuck = player.luck;
			switch (type)
			{
				case StatTooltipType.TotalLuck:
					return ("Luck", (netLuck >= 0 ? $"+{netLuck:0.00}" : $"{netLuck:0.00}"), GetStatColor(netLuck, 0.05f, 0.25f));

				case StatTooltipType.Ladybug:
					float ladybugVal = player.ladyBugLuckTimeLeft > 0 ? 0.2f : (player.ladyBugLuckTimeLeft < 0 ? -0.2f : 0f);
					string ladybugStr = player.ladyBugLuckTimeLeft > 0 ? "+0.20" : (player.ladyBugLuckTimeLeft < 0 ? "-0.20" : "None");
					return ("Ladybug", ladybugStr, GetStatColor(ladybugVal, 0.05f, 0.15f));

				case StatTooltipType.Torches:
					string torchStr = player.torchLuck != 0 ? (player.torchLuck > 0 ? $"+{player.torchLuck:0.00}" : $"{player.torchLuck:0.00}") : "0.00";
					return ("Torches", torchStr, GetStatColor(player.torchLuck, 0.02f, 0.10f));

				case StatTooltipType.GardenGnome:
					return ("Gnome", player.HasGardenGnomeNearby ? "+0.20" : "None", player.HasGardenGnomeNearby ? BrightGreen : ValueWhite);

				case StatTooltipType.CoinLuck:
					return ("Coins", player.coinLuck > 0 ? $"+{player.coinLuck:0.00}" : "None", player.coinLuck > 0 ? LightGreen : ValueWhite);

				case StatTooltipType.DropRateBoost:
					float dropBoost = netLuck * 25f;
					return ("Drop Rate", (dropBoost >= 0 ? $"+{dropBoost:0.0}%" : $"{dropBoost:0.0}%"), GetStatColor(dropBoost, 1f, 6f));

				case StatTooltipType.FishingPower:
					int basePower = player.fishingSkill;
					var (_, timeDelta) = GetFishingTimeMultiplier();
					var (_, weatherDelta) = GetFishingWeatherMultiplier();
					var (_, moonDelta) = GetFishingMoonMultiplier();
					float totalFishingMult = (1f + timeDelta) * (1f + weatherDelta) * (1f + moonDelta);
					int effectivePower = (int)Math.Round(basePower * totalFishingMult);
					return ("Fishing", basePower > 0 ? $"{effectivePower} Power" : "0", basePower > 0 ? BrightGreen : ValueWhite);

				case StatTooltipType.FishingTime:
					var (timeText, timeD) = GetFishingTimeMultiplier();
					return ("Fish Time", timeText, GetStatColor(timeD, 0.05f, 0.20f));

				case StatTooltipType.FishingWeather:
					var (weatherText, weatherD) = GetFishingWeatherMultiplier();
					return ("Weather", weatherText, GetStatColor(weatherD, 0.05f, 0.20f));

				case StatTooltipType.FishingMoon:
					var (moonText, moonD) = GetFishingMoonMultiplier();
					return ("Moon", moonText, GetStatColor(moonD, 0.02f, 0.08f));

				case StatTooltipType.CrateChance:
					return ("Crates", player.cratePotion ? "25%" : "10%", player.cratePotion ? BrightGreen : ValueWhite);

				case StatTooltipType.PoolRequirement:
					return ("Pool Req.", "300+ Tiles", ValueWhite);

				case StatTooltipType.ArmorPenetration:
					var (apVal, apClass) = GetEffectiveArmorPenetration(player);
					string apStr = apVal > 0
						? (string.IsNullOrEmpty(apClass) ? $"{apVal} DEF" : $"{apVal} DEF ({apClass})")
						: "0";
					return ("Armor Pen", apStr, GetStatColor(apVal, 1f, 15f));

				case StatTooltipType.DamageReduction:
					float dr = player.endurance;
					float drPct = dr * 100f;
					return ("Damage Red.", dr > 0 ? $"+{drPct:0.0}% DR" : "0.0%", dr > 0 ? (drPct >= 20f ? BrightGreen : LightGreen) : ValueWhite);

				case StatTooltipType.LifeRegen:
					float hpPerSec = player.lifeRegen / 2f;
					return ("Life Regen", (hpPerSec >= 0 ? $"+{hpPerSec:0.0}" : $"{hpPerSec:0.0}") + " HP/s", hpPerSec > 0 ? (hpPerSec >= 4f ? BrightGreen : LightGreen) : (hpPerSec < 0 ? BrightRed : ValueWhite));

				case StatTooltipType.StationaryBonus:
					bool isStationary = player.velocity == Vector2.Zero;
					return ("Idle Bonus", isStationary ? "2x Active" : "2x Idle", isStationary ? BrightGreen : ValueWhite);

				case StatTooltipType.Invincibility:
					if (player.immuneTime > 0)
						return ("Invincible", $"{player.immuneTime}t left", BrightGreen);
					return ("Invincible", player.longInvince ? "1.33s" : "0.67s", player.longInvince ? LightGreen : ValueWhite);

				case StatTooltipType.EnemyAggro:
					int aggro = player.aggro;
					string aggroText = aggro < 0 ? $"{aggro} (Stealth)" : aggro > 0 ? $"+{aggro} (Targeted)" : "Neutral";
					Color aggroCol = aggro < 0 ? LightGreen : aggro > 0 ? LightRed : ValueWhite;
					return ("Enemy Aggro", aggroText, aggroCol);

				case StatTooltipType.DamageRollLuck:
					float rollBiasPct = Math.Abs(netLuck) * 100f;
					string biasText = netLuck > 0 ? $"+{rollBiasPct:0.#}% High" : (netLuck < 0 ? $"-{rollBiasPct:0.#}% Low" : "Normal");
					return ("Roll Luck", biasText, GetStatColor(netLuck, 0.05f, 0.25f));

				case StatTooltipType.FlightDuration:
					int totalFlightTicks = player.wingTimeMax + player.rocketTimeMax;
					float flightSeconds = totalFlightTicks / 60f;
					return ("Flight", flightSeconds > 0 ? $"{flightSeconds:0.0}s" : "0s", flightSeconds >= 2.5f ? BrightGreen : (flightSeconds > 0 ? LightGreen : ValueWhite));

				case StatTooltipType.MiningSpeed:
					float pickSpeedBonus = (1f - player.pickSpeed) * 100f;
					return ("Mining", pickSpeedBonus > 0 ? $"+{pickSpeedBonus:0}%" : "0%", pickSpeedBonus >= 40f ? BrightGreen : (pickSpeedBonus > 0 ? LightGreen : ValueWhite));

				case StatTooltipType.ItemGrabRange:
					float grabTiles = player.GetItemGrabRange(new Item()) / 16f;
					return ("Grab Range", $"{grabTiles:0.1} Tiles", grabTiles >= 8f ? BrightGreen : (grabTiles > 3.5f ? LightGreen : ValueWhite));

				case StatTooltipType.EnemySpawnRate:
					float spawnRateMult = 1f;
					if (player.enemySpawns) spawnRateMult *= 2.0f;
					if (player.ZoneWaterCandle) spawnRateMult *= 1.33f;
					if (player.calmed) spawnRateMult *= 0.83f;
					if (player.ZonePeaceCandle) spawnRateMult *= 0.77f;
					if (player.sunflower) spawnRateMult *= 0.83f;
					if (player.townNPCs > 0 && !player.ZoneShadowCandle)
						spawnRateMult *= (player.townNPCs >= 3 ? 0.10f : 0.40f);
					float spawnDelta = (spawnRateMult - 1f) * 100f;
					string spawnText = spawnDelta > 0 ? $"+{spawnDelta:0}%" : (spawnDelta < 0 ? $"{spawnDelta:0}%" : "1.0x");
					return ("Spawns", spawnText, spawnDelta > 0 ? BrightGreen : (spawnDelta < 0 ? LightRed : ValueWhite));

				default:
					return ("Stat", "0", ValueWhite);
			}
		}

		private static void PopulateStatTooltip(StatTooltipType type)
		{
			tooltipLines.Clear();
			Player player = Main.LocalPlayer;

			switch (type)
			{
				case StatTooltipType.TotalLuck:
					tooltipTitle = "TOTAL LUCK SCORE";
					tooltipLines.Add(("Secret multiplier affecting all RNG in Terraria.", ValueWhite));
					tooltipLines.Add(("Improves rare enemy & boss drops, fishing", ValueWhite));
					tooltipLines.Add(("power, coin drops, and high damage rolls.", ValueWhite));
					tooltipLines.Add(("", Color.Transparent));
					tooltipLines.Add(("+ WHAT INCREASES IT:", BrightGreen));
					tooltipLines.Add(("  • Luck Potions (+0.1 to +0.3)", LightGreen));
					tooltipLines.Add(("  • Biome-matching Torches (+0.2)", LightGreen));
					tooltipLines.Add(("  • Garden Gnomes (+0.2)", LightGreen));
					tooltipLines.Add(("  • Lantern Nights (+0.3)", LightGreen));
					tooltipLines.Add(("  • Shimmer Coins (+0.05)", LightGreen));
					tooltipLines.Add(("  • Freeing live Ladybugs (+0.2)", LightGreen));
					tooltipLines.Add(("", Color.Transparent));
					tooltipLines.Add(("- WHAT DECREASES IT:", BrightRed));
					tooltipLines.Add(("  • Wrong Biome Torches (up to -0.3)", LightRed));
					tooltipLines.Add(("  • Killing/harming Ladybugs (-0.2)", LightRed));
					break;

				case StatTooltipType.Ladybug:
					tooltipTitle = "LADYBUG KARMA";
					tooltipLines.Add(("Releasing live Ladybugs grants good fortune.", ValueWhite));
					tooltipLines.Add(("Accidentally killing one places a curse on you.", ValueWhite));
					tooltipLines.Add(("", Color.Transparent));
					tooltipLines.Add(("+ HOW TO BOOST:", BrightGreen));
					tooltipLines.Add(("  • Catch with Bug Net & release: +0.20", LightGreen));
					tooltipLines.Add(("    luck for 12 to 24 real-time minutes.", ValueWhite));
					tooltipLines.Add(("", Color.Transparent));
					tooltipLines.Add(("- PENALTY:", BrightRed));
					tooltipLines.Add(("  • Striking or using as fishing bait", LightRed));
					tooltipLines.Add(("    imposes a -0.20 to -0.40 curse.", LightRed));
					break;

				case StatTooltipType.Torches:
					tooltipTitle = "BIOME TORCH LUCK";
					tooltipLines.Add(("Torches that match your current underground", ValueWhite));
					tooltipLines.Add(("biome grant luck. Wrong torches ruin it.", ValueWhite));
					tooltipLines.Add(("", Color.Transparent));
					tooltipLines.Add(("+ HOW TO BOOST:", BrightGreen));
					tooltipLines.Add(("  • Desert Torch in Desert, Ice in Snow,", LightGreen));
					tooltipLines.Add(("    Bone in Dungeon, Corrupt in Evil.", LightGreen));
					tooltipLines.Add(("    Gives up to +0.20 luck bonus.", ValueWhite));
					tooltipLines.Add(("", Color.Transparent));
					tooltipLines.Add(("- PENALTY:", BrightRed));
					tooltipLines.Add(("  • Regular wood torches in Snow/Desert", LightRed));
					tooltipLines.Add(("    impose up to -0.30 luck penalty!", LightRed));
					tooltipLines.Add(("  • Tip: Use Torch God's Favor to auto-swap.", ThemeGold));
					break;

				case StatTooltipType.GardenGnome:
					tooltipTitle = "GARDEN GNOME AURA";
					tooltipLines.Add(("Petrified Garden Gnomes radiate good luck.", ValueWhite));
					tooltipLines.Add(("", Color.Transparent));
					tooltipLines.Add(("+ HOW TO BOOST:", BrightGreen));
					tooltipLines.Add(("  • Place a Gnome within ~80 blocks.", LightGreen));
					tooltipLines.Add(("    Grants flat +0.20 luck to nearby players.", ValueWhite));
					tooltipLines.Add(("", Color.Transparent));
					tooltipLines.Add(("  • Note: Multiple gnomes do not stack.", MutedSlate));
					break;

				case StatTooltipType.CoinLuck:
					tooltipTitle = "SHIMMER COIN LUCK";
					tooltipLines.Add(("Tossing coins into the Shimmer pool infuses", ValueWhite));
					tooltipLines.Add(("your character with permanent luck.", ValueWhite));
					tooltipLines.Add(("", Color.Transparent));
					tooltipLines.Add(("+ HOW TO BOOST:", BrightGreen));
					tooltipLines.Add(("  • Throw 1 Platinum Coin into Shimmer for", LightGreen));
					tooltipLines.Add(("    the maximum +0.05 luck bonus.", ValueWhite));
					tooltipLines.Add(("    Lasts 1 full in-game day (24 mins).", ValueWhite));
					break;

				case StatTooltipType.DropRateBoost:
					tooltipTitle = "RARE DROP RATE BONUS";
					tooltipLines.Add(("Extra percent chance for rare items to drop.", ValueWhite));
					tooltipLines.Add(("", Color.Transparent));
					tooltipLines.Add(("+ HOW IT WORKS:", BrightGreen));
					tooltipLines.Add(("  • Derived directly from your Total Luck.", ValueWhite));
					tooltipLines.Add(("  • When a monster or boss dies, positive", ValueWhite));
					tooltipLines.Add(("    luck triggers a bonus reroll if the drop", ValueWhite));
					tooltipLines.Add(("    failed on the first check.", LightGreen));
					break;

				case StatTooltipType.FishingPower:
					tooltipTitle = "TOTAL FISHING POWER";
					tooltipLines.Add(("Determines catch speed, tier of fish hooked,", ValueWhite));
					tooltipLines.Add(("and completely eliminates junk catches.", ValueWhite));
					tooltipLines.Add(("", Color.Transparent));
					tooltipLines.Add(("+ WHAT INCREASES IT:", BrightGreen));
					tooltipLines.Add(("  • Better Rods (Golden Rod = +50)", LightGreen));
					tooltipLines.Add(("  • Better Bait (Master Bait = +50%)", LightGreen));
					tooltipLines.Add(("  • Angler Armor set & Earring (+10)", LightGreen));
					tooltipLines.Add(("  • Fishing Potion (+15)", LightGreen));
					tooltipLines.Add(("  • Multiplied live by Time, Rain & Moon!", ThemeGold));
					tooltipLines.Add(("", Color.Transparent));
					tooltipLines.Add(("- PENALTY:", BrightRed));
					tooltipLines.Add(("  • Pools under 300 tiles cut power up to 75%.", LightRed));
					break;

				case StatTooltipType.FishingTime:
					tooltipTitle = "FISHING TIME MULTIPLIER";
					tooltipLines.Add(("Fish feeding habits shift across the day.", ValueWhite));
					tooltipLines.Add(("", Color.Transparent));
					tooltipLines.Add(("+ BEST FISHING TIMES:", BrightGreen));
					tooltipLines.Add(("  • Dawn (4:30 - 6:00 AM): +30% [Peak]", BrightGreen));
					tooltipLines.Add(("  • Dusk (6:00 - 7:30 PM): +30% [Peak]", BrightGreen));
					tooltipLines.Add(("  • Morning & Afternoon: +10%", LightGreen));
					tooltipLines.Add(("", Color.Transparent));
					tooltipLines.Add(("- PENALTY:", BrightRed));
					tooltipLines.Add(("  • Night (7:30 PM - 4:30 AM): -20% penalty", LightRed));
					break;

				case StatTooltipType.FishingWeather:
					tooltipTitle = "FISHING WEATHER MULTIPLIER";
					tooltipLines.Add(("Rain & storms boost fish feeding activity.", ValueWhite));
					tooltipLines.Add(("", Color.Transparent));
					tooltipLines.Add(("+ WEATHER BONUSES:", BrightGreen));
					tooltipLines.Add(("  • Thunderstorm: +30% Fishing Power", BrightGreen));
					tooltipLines.Add(("  • Regular Rain: +20% Fishing Power", LightGreen));
					tooltipLines.Add(("  • Overcast Clouds: +10% Fishing Power", LightGreen));
					tooltipLines.Add(("  • Clear Skies: 0% Baseline", ValueWhite));
					break;

				case StatTooltipType.FishingMoon:
					tooltipTitle = "LUNAR CYCLE MULTIPLIER";
					tooltipLines.Add(("Tidal gravitational pull influences catches.", ValueWhite));
					tooltipLines.Add(("", Color.Transparent));
					tooltipLines.Add(("+ FAVORABLE PHASES:", BrightGreen));
					tooltipLines.Add(("  • Full Moon: +10% Fishing Power", BrightGreen));
					tooltipLines.Add(("  • Gibbous Moon: +5% Fishing Power", LightGreen));
					tooltipLines.Add(("", Color.Transparent));
					tooltipLines.Add(("- UNFAVORABLE PHASES:", BrightRed));
					tooltipLines.Add(("  • New Moon: -10% Fishing Power", BrightRed));
					tooltipLines.Add(("  • Crescent Moon: -5% Fishing Power", LightRed));
					break;

				case StatTooltipType.CrateChance:
					tooltipTitle = "CRATE CATCH CHANCE";
					tooltipLines.Add(("Chance to hook Wooden, Iron, Gold, or Biome", ValueWhite));
					tooltipLines.Add(("crates instead of regular fish.", ValueWhite));
					tooltipLines.Add(("", Color.Transparent));
					tooltipLines.Add(("+ HOW TO BOOST:", BrightGreen));
					tooltipLines.Add(("  • Base chance is 10% per catch.", ValueWhite));
					tooltipLines.Add(("  • Crate Potion adds +15%, increasing total", LightGreen));
					tooltipLines.Add(("    crate chance to a massive 25%!", BrightGreen));
					break;

				case StatTooltipType.PoolRequirement:
					tooltipTitle = "WATER POOL REQUIREMENT";
					tooltipLines.Add(("Bodies of water must be large enough to avoid", ValueWhite));
					tooltipLines.Add(("hidden severe fishing power penalties.", ValueWhite));
					tooltipLines.Add(("", Color.Transparent));
					tooltipLines.Add(("+ SWEET SPOT:", BrightGreen));
					tooltipLines.Add(("  • 300+ liquid tiles = 100% full power.", LightGreen));
					tooltipLines.Add(("  • Honey only requires 200 tiles.", ValueWhite));
					tooltipLines.Add(("", Color.Transparent));
					tooltipLines.Add(("- PENALTIES:", BrightRed));
					tooltipLines.Add(("  • 75 - 299 tiles: Power cut up to 75%", LightRed));
					tooltipLines.Add(("  • Under 75 tiles: Cannot catch anything!", BrightRed));
					break;

				case StatTooltipType.ArmorPenetration:
					tooltipTitle = "FLAT ARMOR PENETRATION";
					tooltipLines.Add(("Ignores enemy defense when dealing damage.", ValueWhite));
					tooltipLines.Add(("", Color.Transparent));

					int genericPen = (int)player.GetTotalArmorPenetration(DamageClass.Generic);
					int meleePen = (int)player.GetTotalArmorPenetration(DamageClass.Melee);
					int rangedPen = (int)player.GetTotalArmorPenetration(DamageClass.Ranged);
					int magicPen = (int)player.GetTotalArmorPenetration(DamageClass.Magic);
					int summonPen = (int)player.GetTotalArmorPenetration(DamageClass.Summon);

					bool hasClassSpecific = (meleePen > genericPen) || (rangedPen > genericPen) || (magicPen > genericPen) || (summonPen > genericPen);
					if (hasClassSpecific)
					{
						tooltipLines.Add(("+ ACTIVE CLASS BREAKDOWN:", BrightGreen));
						if (genericPen > 0)
							tooltipLines.Add(($"  • Universal (All): {genericPen} DEF", LightGreen));
						if (rangedPen > genericPen)
							tooltipLines.Add(($"  • Ranged: {rangedPen} DEF (+{rangedPen - genericPen})", ThemeGold));
						if (meleePen > genericPen)
							tooltipLines.Add(($"  • Melee: {meleePen} DEF (+{meleePen - genericPen})", ThemeGold));
						if (magicPen > genericPen)
							tooltipLines.Add(($"  • Magic: {magicPen} DEF (+{magicPen - genericPen})", ThemeGold));
						if (summonPen > genericPen)
							tooltipLines.Add(($"  • Summon: {summonPen} DEF (+{summonPen - genericPen})", ThemeGold));
						tooltipLines.Add(("", Color.Transparent));
					}

					tooltipLines.Add(("+ DAMAGE FORMULA:", BrightGreen));
					tooltipLines.Add(("  • Normal Mode: 1 Pen = +0.5 damage", ValueWhite));
					tooltipLines.Add(("  • Expert/Master: 1 Pen = +1.0 damage!", BrightGreen));
					tooltipLines.Add(("", Color.Transparent));
					tooltipLines.Add(("+ WHAT INCREASES IT:", BrightGreen));
					tooltipLines.Add(("  • Shark Tooth & Stinger Necklaces (+5)", LightGreen));
					tooltipLines.Add(("  • Sharpening Station (+12 melee)", LightGreen));
					tooltipLines.Add(("  • Ichor Flask / Bullets (-15 enemy DEF)", LightGreen));
					tooltipLines.Add(("  • Plug-in Chips (Armor Piercer +18)", ThemeGold));
					break;

				case StatTooltipType.DamageReduction:
					tooltipTitle = "DAMAGE REDUCTION (% DR)";
					tooltipLines.Add(("Direct percentage of incoming damage ignored", ValueWhite));
					tooltipLines.Add(("AFTER defense flat reduction is applied.", ValueWhite));
					tooltipLines.Add(("", Color.Transparent));
					tooltipLines.Add(("+ WHAT INCREASES IT:", BrightGreen));
					tooltipLines.Add(("  • Worm Scarf (+17% DR)", LightGreen));
					tooltipLines.Add(("  • Endurance Potion (+10% DR)", LightGreen));
					tooltipLines.Add(("  • Frozen Shield (+25% DR under 50% HP)", LightGreen));
					tooltipLines.Add(("  • Beetle Armor (+15% / +30% / +45% DR)", BrightGreen));
					tooltipLines.Add(("  • Solar Flare Armor (+30% DR)", LightGreen));
					break;

				case StatTooltipType.LifeRegen:
					tooltipTitle = "NATURAL HEALTH REGEN";
					tooltipLines.Add(("Health passively recovered every second.", ValueWhite));
					tooltipLines.Add(("", Color.Transparent));
					tooltipLines.Add(("+ HOW IT WORKS:", BrightGreen));
					tooltipLines.Add(("  • 1 Regen stat = 0.5 HP per second.", ValueWhite));
					tooltipLines.Add(("  • Moving cuts natural regeneration in half.", LightRed));
					tooltipLines.Add(("  • Standing still doubles natural regeneration!", BrightGreen));
					tooltipLines.Add(("", Color.Transparent));
					tooltipLines.Add(("+ REGEN BOOSTERS:", BrightGreen));
					tooltipLines.Add(("  • Campfire (+0.5 HP/s) / Heart Lantern (+1.0)", LightGreen));
					tooltipLines.Add(("  • Honey Pool (+1.0 HP/s) / Regen Potion (+2.0)", LightGreen));
					tooltipLines.Add(("  • Shiny Stone (+10 to +40 HP/s while still)", ThemeGold));
					break;

				case StatTooltipType.StationaryBonus:
					tooltipTitle = "STATIONARY REGEN BONUS";
					tooltipLines.Add(("Terraria rewards standing still with double", ValueWhite));
					tooltipLines.Add(("natural health recovery speed.", ValueWhite));
					tooltipLines.Add(("", Color.Transparent));
					tooltipLines.Add(("+ IDLE VS MOVING:", BrightGreen));
					tooltipLines.Add(("  • Stationary: Full 2x regeneration speed.", BrightGreen));
					tooltipLines.Add(("  • Moving: Natural regen speed cut by 50%.", LightRed));
					tooltipLines.Add(("", Color.Transparent));
					tooltipLines.Add(("  • Turns green whenever you stand completely still.", ThemeGold));
					break;

				case StatTooltipType.Invincibility:
					tooltipTitle = "INVINCIBILITY (I-FRAMES)";
					tooltipLines.Add(("Duration of immunity granted after taking a hit,", ValueWhite));
					tooltipLines.Add(("preventing rapid consecutive damage.", ValueWhite));
					tooltipLines.Add(("", Color.Transparent));
					tooltipLines.Add(("+ BASELINE:", ValueWhite));
					tooltipLines.Add(("  • 40 ticks = 0.67 seconds base window.", ValueWhite));
					tooltipLines.Add(("", Color.Transparent));
					tooltipLines.Add(("+ HOW TO EXTEND:", BrightGreen));
					tooltipLines.Add(("  • Cross Necklace or Star Veil doubles this", LightGreen));
					tooltipLines.Add(("    to 80 ticks (1.33 seconds total)!", BrightGreen));
					tooltipLines.Add(("", Color.Transparent));
					tooltipLines.Add(("  • When hit, tracks active remaining ticks live.", ThemeGold));
					break;

				case StatTooltipType.EnemyAggro:
					tooltipTitle = "ENEMY AGGRO / THREAT";
					tooltipLines.Add(("Determines how eagerly enemies prioritize", ValueWhite));
					tooltipLines.Add(("targeting you over teammates and minions.", ValueWhite));
					tooltipLines.Add(("", Color.Transparent));
					tooltipLines.Add(("+ TANK GEAR (+AGGRO):", LightRed));
					tooltipLines.Add(("  • Turtle Armor (+250) / Beetle (+400)", LightRed));
					tooltipLines.Add(("  • Flesh Knuckles (+400) / Paladin's Shield", LightRed));
					tooltipLines.Add(("", Color.Transparent));
					tooltipLines.Add(("+ STEALTH GEAR (-AGGRO):", BrightGreen));
					tooltipLines.Add(("  • Shroomite in stealth (-750 aggro)", LightGreen));
					tooltipLines.Add(("  • Vortex in stealth (-1200 aggro)", LightGreen));
					tooltipLines.Add(("  • Putrid Scent (-400 aggro)", LightGreen));
					break;

				case StatTooltipType.DamageRollLuck:
					tooltipTitle = "DAMAGE ROLL LUCK BIAS";
					tooltipLines.Add(("All weapons roll random damage from 85% to 115%.", ValueWhite));
					tooltipLines.Add(("", Color.Transparent));
					tooltipLines.Add(("+ HIGH LUCK EFFECT:", BrightGreen));
					tooltipLines.Add(("  • When Luck > 0, you have a % chance equal", ValueWhite));
					tooltipLines.Add(("    to your Luck to roll damage TWICE and", LightGreen));
					tooltipLines.Add(("    keep the HIGHER number!", BrightGreen));
					tooltipLines.Add(("", Color.Transparent));
					tooltipLines.Add(("- LOW LUCK PENALTY:", BrightRed));
					tooltipLines.Add(("  • When Luck < 0, rolls twice and keeps", LightRed));
					tooltipLines.Add(("    the LOWER number.", BrightRed));
					break;

				case StatTooltipType.FlightDuration:
					tooltipTitle = "WING FLIGHT DURATION";
					tooltipLines.Add(("Total airborne flight time provided by your", ValueWhite));
					tooltipLines.Add(("equipped wings and rocket boots before gliding.", ValueWhite));
					tooltipLines.Add(("", Color.Transparent));
					tooltipLines.Add(("+ FLIGHT TIERS:", BrightGreen));
					tooltipLines.Add(("  • Basic Wings (Angel/Demon): 1.67s (100t)", ValueWhite));
					tooltipLines.Add(("  • Mid Wings (Leaf/Frozen): 2.17s to 2.50s", LightGreen));
					tooltipLines.Add(("  • High Wings (Steampunk/Hoverboard): 3.00s", LightGreen));
					tooltipLines.Add(("  • Celestial Wings (Solar/Stardust): 3.00s", BrightGreen));
					tooltipLines.Add(("  • Rocket/Spectre/Terraspark Boots add bonus flight!", ThemeGold));
					break;

				case StatTooltipType.MiningSpeed:
					tooltipTitle = "MINING & EXCAVATION SPEED";
					tooltipLines.Add(("Reduces tool delay between pickaxe swings.", ValueWhite));
					tooltipLines.Add(("", Color.Transparent));
					tooltipLines.Add(("+ WHAT INCREASES IT:", BrightGreen));
					tooltipLines.Add(("  • Mining Potion (-25% tool delay)", LightGreen));
					tooltipLines.Add(("  • Ancient Chisel (-25% tool delay)", LightGreen));
					tooltipLines.Add(("  • Mining Armor 3-piece set (-30% tool delay)", LightGreen));
					tooltipLines.Add(("  • Slice of Cake buff (-20% tool delay)", LightGreen));
					tooltipLines.Add(("  • Hard cap is -70% to -80% delay reduction.", MutedSlate));
					break;

				case StatTooltipType.ItemGrabRange:
					tooltipTitle = "ITEM MAGNET / GRAB RANGE";
					tooltipLines.Add(("Distance in tiles that dropped items, coins, hearts,", ValueWhite));
					tooltipLines.Add(("and stars gravitate towards your character.", ValueWhite));
					tooltipLines.Add(("", Color.Transparent));
					tooltipLines.Add(("+ WHAT INCREASES IT:", BrightGreen));
					tooltipLines.Add(("  • Baseline reach: ~2.6 tiles (42px).", ValueWhite));
					tooltipLines.Add(("  • Treasure Magnet (+5.0 tiles reach)", LightGreen));
					tooltipLines.Add(("  • Gold Ring (+10.0 tiles reach for coins)", LightGreen));
					tooltipLines.Add(("  • Celestial Magnet (+18.75 tiles for mana stars)", LightGreen));
					tooltipLines.Add(("  • Heartreach Potion (+18.75 tiles for hearts)", BrightGreen));
					break;

				case StatTooltipType.EnemySpawnRate:
					tooltipTitle = "ENEMY SPAWN PRESSURE";
					tooltipLines.Add(("Controls monster spawn rate and max capacity.", ValueWhite));
					tooltipLines.Add(("Essential metric for optimizing mob farming.", ValueWhite));
					tooltipLines.Add(("", Color.Transparent));
					tooltipLines.Add(("+ WHAT INCREASES SPAWNS (FARMING):", BrightGreen));
					tooltipLines.Add(("  • Battle Potion: +100% Rate & Max Spawns", BrightGreen));
					tooltipLines.Add(("  • Water Candle: +33% Rate & +50% Spawns", LightGreen));
					tooltipLines.Add(("  • Blood Moon / Eclipse / Graveyard / Jungle", LightGreen));
					tooltipLines.Add(("", Color.Transparent));
					tooltipLines.Add(("- WHAT LOWERS SPAWNS (SAFETY):", BrightRed));
					tooltipLines.Add(("  • Calming Potion (-17%) / Peace Candle (-23%)", LightRed));
					tooltipLines.Add(("  • Sunflowers (-17%) / Town NPCs (up to -90%)", LightRed));
					break;
			}
		}

		private static void DrawCleanText(SpriteBatch spriteBatch, DynamicSpriteFont font, string text, Vector2 pos, Color color, float progress, float scale)
		{
			// Clean 1px drop shadow
			Vector2 shadowPos = pos + new Vector2(1f, 1f);
			spriteBatch.DrawString(font, text, shadowPos, Color.Black * (0.65f * progress), 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
			spriteBatch.DrawString(font, text, pos, color * progress, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
		}

		// Color determination helper strictly following user specifications:
		// - Default = Crisp White
		// - Increased a little = Light Green
		// - Increased a lot = Vivid Emerald Green
		// - Reduced a little = Light Coral Red
		// - Reduced a lot = Vivid Danger Red
		private static Color GetStatColor(float delta, float smallThresh = 0.05f, float largeThresh = 0.20f, bool invert = false)
		{
			if (invert) delta = -delta;

			if (Math.Abs(delta) < 0.001f)
				return ValueWhite;

			if (delta > 0)
			{
				return delta >= largeThresh ? BrightGreen : LightGreen;
			}
			else
			{
				return Math.Abs(delta) >= largeThresh ? BrightRed : LightRed;
			}
		}

		private static (string text, float delta) GetFishingTimeMultiplier()
		{
			double time = Main.time;
			bool day = Main.dayTime;

			if (day)
			{
				if (time < 5400) // 4:30 AM - 6:00 AM (Dawn Peak)
					return ("+30% (Dawn)", 0.30f);
				if (time < 16200) // 6:00 AM - 9:00 AM
					return ("+10% (Morning)", 0.10f);
				if (time < 37800) // 9:00 AM - 3:00 PM
					return ("0% (Noon)", 0.00f);
				if (time < 48600) // 3:00 PM - 6:00 PM
					return ("+10% (Afternoon)", 0.10f);
				return ("+30% (Dusk)", 0.30f); // 6:00 PM - 7:30 PM (Dusk Peak)
			}
			else
			{
				if (time < 16200 || time > 27000)
					return ("-20% (Night)", -0.20f);
				return ("-20% (Midnight)", -0.20f);
			}
		}

		private static (string text, float delta) GetFishingWeatherMultiplier()
		{
			if (Main.IsItStorming)
				return ("+30% (Storm)", 0.30f);
			if (Main.IsItRaining)
				return ("+20% (Rain)", 0.20f);
			if (Main.cloudAlpha > 0.35f)
				return ("+10% (Overcast)", 0.10f);
			return ("0% (Clear)", 0.00f);
		}

		private static (string text, float delta) GetFishingMoonMultiplier()
		{
			return Main.moonPhase switch
			{
				0 => ("+10% (Full Moon)", 0.10f),
				1 or 7 => ("+5% (Gibbous)", 0.05f),
				4 => ("-10% (New Moon)", -0.10f),
				3 or 5 => ("-5% (Crescent)", -0.05f),
				_ => ("0% (Half Moon)", 0.00f)
			};
		}

		private static (int pen, string classLabel) GetEffectiveArmorPenetration(Player player)
		{
			int generic = (int)player.GetTotalArmorPenetration(DamageClass.Generic);
			int melee = (int)player.GetTotalArmorPenetration(DamageClass.Melee);
			int ranged = (int)player.GetTotalArmorPenetration(DamageClass.Ranged);
			int magic = (int)player.GetTotalArmorPenetration(DamageClass.Magic);
			int summon = (int)player.GetTotalArmorPenetration(DamageClass.Summon);

			// If the player is actively holding a damage-dealing weapon, prioritize that weapon's class
			if (player.HeldItem != null && !player.HeldItem.IsAir && player.HeldItem.damage > 0 && player.HeldItem.DamageType != DamageClass.Generic)
			{
				int heldPen = (int)player.GetTotalArmorPenetration(player.HeldItem.DamageType);
				string name = player.HeldItem.DamageType == DamageClass.Ranged ? "Ranged"
					: player.HeldItem.DamageType == DamageClass.Melee ? "Melee"
					: player.HeldItem.DamageType == DamageClass.Magic ? "Magic"
					: player.HeldItem.DamageType == DamageClass.Summon ? "Summon"
					: player.HeldItem.DamageType.DisplayName.Value;
				return (heldPen, heldPen > generic ? name : "");
			}

			// Otherwise, display highest class-specific penetration (e.g. Armor Piercer gives Ranged)
			int maxPen = Math.Max(generic, Math.Max(Math.Max(melee, ranged), Math.Max(magic, summon)));
			if (maxPen > generic)
			{
				if (maxPen == ranged) return (ranged, "Ranged");
				if (maxPen == melee) return (melee, "Melee");
				if (maxPen == magic) return (magic, "Magic");
				if (maxPen == summon) return (summon, "Summon");
			}

			return (generic, "");
		}

		private static void DrawDivider(SpriteBatch spriteBatch, Rectangle rect, float y, Color themeColor, float progress)
		{
			int lineW = rect.Width - 24;
			spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(rect.X + 12, (int)y, lineW, 1), themeColor * (0.22f * progress));
		}

		private static void DrawPixelDiamond(SpriteBatch spriteBatch, int cx, int cy, int size, Color color)
		{
			for (int dy = -size; dy <= size; dy++)
			{
				int span = size - Math.Abs(dy);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - span, cy + dy, span * 2 + 1, 1), color);
			}
		}

		private static void DrawHighTechBorder(SpriteBatch spriteBatch, Rectangle rect, Color color)
		{
			// Outer hairline lines
			spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(rect.X, rect.Y, rect.Width, 1), color * 0.65f);
			spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), color * 0.65f);
			spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(rect.X, rect.Y, 1, rect.Height), color * 0.65f);
			spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), color * 0.65f);

			// Iconic NieR Corner brackets (tiny 4px notches)
			const int cornerLen = 4;
			const int cornerThick = 2;

			// Top-left
			spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(rect.X, rect.Y, cornerLen, cornerThick), color);
			spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(rect.X, rect.Y, cornerThick, cornerLen), color);

			// Top-right
			spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(rect.Right - cornerLen, rect.Y, cornerLen, cornerThick), color);
			spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(rect.Right - cornerThick, rect.Y, cornerThick, cornerLen), color);

			// Bottom-left
			spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(rect.X, rect.Bottom - cornerThick, cornerLen, cornerThick), color);
			spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(rect.X, rect.Bottom - cornerLen, cornerThick, cornerLen), color);

			// Bottom-right
			spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(rect.Right - cornerLen, rect.Bottom - cornerThick, cornerLen, cornerThick), color);
			spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(rect.Right - cornerThick, rect.Bottom - cornerLen, cornerThick, cornerLen), color);
		}
	}
}
