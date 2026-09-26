using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;
using Terraria.UI.Chat;
using Augments.Core;

namespace Augments
{
	// Modular in-game HUD docked on the right side of the screen.
	// Displays tracked Plug-in Chip Family synergies whenever the player owns >= 1 member chip.
	// Positioned below the minimap and info accessories.
	// Support-type families display a prominent plus sign (cross) icon.
	// Hovering the icon smoothly slides out an inspection panel with pixel-aligned typography and status.
	public static class AugmentFamilyHUD
	{
		private const float IconWidth = 42f;
		private const float IconHeight = 46f;
		private const float RightMargin = 12f;
		private const float StartY = 430f; // Lowered further as requested
		private const float Spacing = 8f;
		private const float MinPanelWidth = 330f;
		private const float SlideSpeed = 7f;
		public const string SupportStanceId = "__support_stance";

		private static float globalTimer;
		private static bool wasMouseLeft = false;
		private static readonly Dictionary<string, float> hoverProgress = new();
		private static readonly Dictionary<string, Rectangle> iconBounds = new();
		private static readonly Dictionary<string, Rectangle> panelBounds = new();

		private static (float startY, float spacing) GetDynamicLayout(AugmentPlayer ap)
		{
			int activeCount = 0;
			if (ap.SupportAugmentCount >= 1)
				activeCount++;

			foreach (var kv in AugmentFamilyRegistry.Families)
			{
				if (AugmentFamilyRegistry.GetOwnedCount(ap, kv.Key) > 0)
					activeCount++;
			}

			float spacing = Spacing;
			float totalHeight = 40f + activeCount * IconHeight + Math.Max(0, activeCount - 1) * spacing;
			float desiredStartY = StartY;

			// If default StartY pushes icons off the screen bottom, slide up smoothly
			float bottomMargin = 16f;
			if (desiredStartY + (totalHeight - 40f) > Main.screenHeight - bottomMargin)
			{
				desiredStartY = Main.screenHeight - bottomMargin - (totalHeight - 40f);
			}

			// Don't collide with minimap area at top right
			const float minStartY = 260f;
			if (desiredStartY < minStartY)
			{
				desiredStartY = minStartY;
				// If still overflowing with minStartY, reduce spacing
				float available = Main.screenHeight - bottomMargin - desiredStartY - 40f;
				if (activeCount > 1 && totalHeight > available)
				{
					spacing = Math.Clamp((available - activeCount * IconHeight) / (activeCount - 1), 2f, Spacing);
				}
			}

			return (desiredStartY, spacing);
		}

		public static void Update(GameTime gameTime)
		{
			if (Main.dedServ || Main.gameMenu)
				return;

			float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
			globalTimer += dt;

			var player = Main.LocalPlayer;
			if (player == null || !player.active)
				return;

			var ap = player.GetModPlayer<AugmentPlayer>();
			if (ap == null)
				return;

			Point mouse = new Point(Main.mouseX, Main.mouseY);
			float iconX = Main.screenWidth - IconWidth - RightMargin;
			var (startY, spacing) = GetDynamicLayout(ap);
			float currentY = startY;

			Rectangle tipBtn = new Rectangle((int)iconX, (int)startY - 76, (int)IconWidth, 32);
			Rectangle analyticsBtn = new Rectangle((int)iconX, (int)startY - 40, (int)IconWidth, 32);
			bool justClicked = Main.mouseLeft && !wasMouseLeft;
			if (tipBtn.Contains(mouse) && justClicked)
			{
				AugmentAdvisoryHUD.TriggerNextRandomTip();
				Main.blockMouse = true;
			}
			else if (analyticsBtn.Contains(mouse) && justClicked)
			{
				ModContent.GetInstance<AugmentUISystem>()?.ToggleAnalytics();
				Main.blockMouse = true;
			}
			wasMouseLeft = Main.mouseLeft;

			if (ap.SupportAugmentCount >= 1)
			{
				string id = SupportStanceId;
				if (!hoverProgress.ContainsKey(id))
					hoverProgress[id] = 0f;

				Rectangle iRect = new Rectangle((int)iconX, (int)currentY, (int)IconWidth, (int)IconHeight);
				iconBounds[id] = iRect;
				currentY += IconHeight + spacing;

				panelBounds.TryGetValue(id, out var pRect);
				bool isHovered = false;

				Rectangle generousIcon = new Rectangle(iRect.X - 5, iRect.Y - 5, iRect.Width + 10, iRect.Height + 10);
				if (generousIcon.Contains(mouse))
				{
					isHovered = true;
				}
				else if (hoverProgress[id] > 0.02f && pRect != Rectangle.Empty)
				{
					Rectangle generousPanel = new Rectangle(pRect.X - 5, pRect.Y - 5, pRect.Width + 10, pRect.Height + 10);
					if (generousPanel.Contains(mouse))
						isHovered = true;
				}

				float target = isHovered ? 1f : 0f;
				float current = hoverProgress[id];
				if (current < target)
					hoverProgress[id] = Math.Min(target, current + dt * SlideSpeed);
				else if (current > target)
					hoverProgress[id] = Math.Max(target, current - dt * SlideSpeed);
			}
			else
			{
				hoverProgress[SupportStanceId] = 0f;
				panelBounds[SupportStanceId] = Rectangle.Empty;
			}

			foreach (var kv in AugmentFamilyRegistry.Families)
			{
				string id = kv.Key;
				var fam = kv.Value;
				int ownedCount = AugmentFamilyRegistry.GetOwnedCount(ap, id);

				if (ownedCount <= 0)
				{
					hoverProgress[id] = 0f;
					panelBounds[id] = Rectangle.Empty;
					continue;
				}

				if (!hoverProgress.ContainsKey(id))
					hoverProgress[id] = 0f;

				// Calculate exact icon rect for this family directly
				Rectangle iRect = new Rectangle((int)iconX, (int)currentY, (int)IconWidth, (int)IconHeight);
				iconBounds[id] = iRect;
				currentY += IconHeight + spacing;

				panelBounds.TryGetValue(id, out var pRect);

				bool isHovered = false;

				// 1. Hovering on or immediately around the docked icon (with 5px generous margin)
				Rectangle generousIcon = new Rectangle(iRect.X - 5, iRect.Y - 5, iRect.Width + 10, iRect.Height + 10);
				if (generousIcon.Contains(mouse))
				{
					isHovered = true;
				}
				// 2. Hovering on the slideout panel while it is open or opening
				else if (hoverProgress[id] > 0.02f && pRect != Rectangle.Empty)
				{
					// Generous panel zone covering the panel AND extending into the icon seam
					Rectangle generousPanel = new Rectangle(pRect.X - 5, pRect.Y - 5, pRect.Width + 10, pRect.Height + 10);
					if (generousPanel.Contains(mouse))
					{
						isHovered = true;
					}
				}

				float target = isHovered ? 1f : 0f;
				float current = hoverProgress[id];
				if (current < target)
					hoverProgress[id] = Math.Min(target, current + dt * SlideSpeed);
				else if (current > target)
					hoverProgress[id] = Math.Max(target, current - dt * SlideSpeed);
			}
		}

		public static void Draw(SpriteBatch spriteBatch)
		{
			if (Main.dedServ || Main.gameMenu)
				return;

			var player = Main.LocalPlayer;
			if (player == null || !player.active)
				return;

			var ap = player.GetModPlayer<AugmentPlayer>();
			if (ap == null)
				return;

			var font = FontAssets.MouseText.Value;
			float iconX = Main.screenWidth - IconWidth - RightMargin;
			var (startY, spacing) = GetDynamicLayout(ap);
			float currentY = startY;

			Point mouse = new Point(Main.mouseX, Main.mouseY);

			// 0. Tactical Advisory (Tip) Button
			Rectangle tipBtn = new Rectangle((int)iconX, (int)startY - 76, (int)IconWidth, 32);
			bool tipHover = tipBtn.Contains(mouse);
			Color tipTheme = new Color(56, 189, 248); // Pod 042 cyan

			spriteBatch.Draw(TextureAssets.MagicPixel.Value, tipBtn, new Color(8, 14, 28, 245));
			if (tipHover)
			{
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, tipBtn, tipTheme * 0.25f);
			}
			DrawHighTechBorder(spriteBatch, tipBtn, tipHover ? Color.Lerp(tipTheme, Color.White, 0.35f) : tipTheme * 0.75f);

			// High-Tech Telemetry Icon: Pixel-art Pod 042 Visor & Sensor
			int tcx = tipBtn.X + tipBtn.Width / 2;
			int tcy = tipBtn.Y + 9;
			Texture2D pix = TextureAssets.MagicPixel.Value;
			Color podBody = tipHover ? Color.White : new Color(180, 205, 230);
			spriteBatch.Draw(pix, new Rectangle(tcx - 5, tcy - 3, 10, 5), podBody * 0.75f);
			spriteBatch.Draw(pix, new Rectangle(tcx - 1, tcy - 5, 2, 2), podBody * 0.85f);
			float pulse = (float)Math.Sin(globalTimer * 3.5f) * 0.5f + 0.5f;
			Color eyeCol = Color.Lerp(tipTheme, Color.White, pulse * 0.5f);
			spriteBatch.Draw(pix, new Rectangle(tcx - 2, tcy - 2, 4, 2), tipHover ? Color.White : eyeCol);

			string tipLabel = "TIP";
			Vector2 tipSz = ChatManager.GetStringSize(font, tipLabel, new Vector2(0.52f));
			Vector2 tipPos = new Vector2(tcx - tipSz.X * 0.5f, tipBtn.Y + 18f);
			ChatManager.DrawColorCodedStringWithShadow(spriteBatch, font, tipLabel, tipPos, tipHover ? Color.White : tipTheme, 0f, Vector2.Zero, new Vector2(0.52f));

			if (tipHover)
			{
				Main.instance.MouseText("Tactical Advisory [POD 042]\nClick to trigger tactical tip, field intel, or advice\nClick active tip at bottom to cycle next");
			}

			// 1. Combat Analytics (DPS) Button
			Rectangle analyticsBtn = new Rectangle((int)iconX, (int)startY - 40, (int)IconWidth, 32);
			bool btnHover = analyticsBtn.Contains(mouse);
			bool isAnalyticsOpen = ModContent.GetInstance<AugmentUISystem>()?.IsAnalyticsOpen == true;
			float dpsVal = AugmentDamageTracker.GetCurrentDPS();
			bool hasActiveCombat = dpsVal > 0f || AugmentDamageTracker.SessionDuration > 0f;
			Color btnTheme = isAnalyticsOpen 
				? new Color(74, 222, 128) 
				: (hasActiveCombat ? new Color(56, 189, 248) : new Color(130, 160, 200));

			// Background
			spriteBatch.Draw(TextureAssets.MagicPixel.Value, analyticsBtn, new Color(8, 14, 28, 245));
			if (btnHover)
			{
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, analyticsBtn, btnTheme * 0.25f);
			}

			// Border with high tech corners
			DrawHighTechBorder(spriteBatch, analyticsBtn, btnHover ? Color.Lerp(btnTheme, Color.White, 0.35f) : btnTheme * 0.75f);

			// High-Tech Telemetry Icon: 4 animated waveform bars
			int bcx = analyticsBtn.X + analyticsBtn.Width / 2;
			int barBaseY = analyticsBtn.Y + 16;
			float animPhase = globalTimer * (hasActiveCombat ? 8f : 2.5f);

			int[] barHeights = new int[4];
			barHeights[0] = 4 + (int)(Math.Sin(animPhase) * 2f + 2f);
			barHeights[1] = 6 + (int)(Math.Sin(animPhase + 1.2f) * 3f + 3f);
			barHeights[2] = 8 + (int)(Math.Sin(animPhase + 2.4f) * 4f + 4f);
			barHeights[3] = 5 + (int)(Math.Sin(animPhase + 3.6f) * 2.5f + 2.5f);

			int[] barXOffsets = { -9, -3, 3, 9 };
			for (int bi = 0; bi < 4; bi++)
			{
				int bx = bcx + barXOffsets[bi];
				int bh = Math.Clamp(barHeights[bi], 3, 12);
				Rectangle barRect = new Rectangle(bx, barBaseY - bh, 3, bh);
				Color barCol = btnHover ? Color.White : Color.Lerp(btnTheme, Color.White, bi * 0.15f);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, barRect, barCol);
			}

			// Label underneath: "DPS"
			string label = "DPS";
			Vector2 lSz = ChatManager.GetStringSize(font, label, new Vector2(0.52f));
			Vector2 lPos = new Vector2(bcx - lSz.X * 0.5f, analyticsBtn.Y + 18f);
			ChatManager.DrawColorCodedStringWithShadow(spriteBatch, font, label, lPos, btnHover ? Color.White : btnTheme, 0f, Vector2.Zero, new Vector2(0.52f));

			if (btnHover)
			{
				Main.instance.MouseText("Combat Analytics [L]\nClick to view live DPS and damage breakdown\nAlt + Drag to move pinned widgets");
			}

			if (ap.SupportAugmentCount >= 1)
			{
				string id = SupportStanceId;
				hoverProgress.TryGetValue(id, out float progress);
				Rectangle iconRect = new Rectangle((int)iconX, (int)currentY, (int)IconWidth, (int)IconHeight);
				iconBounds[id] = iconRect;

				// 1. Draw Slideout Panel (if expanding)
				if (progress > 0.01f)
				{
					DrawSupportStanceSlideoutPanel(spriteBatch, font, ap, iconRect, progress);
				}
				else
				{
					panelBounds[id] = Rectangle.Empty;
				}

				// 2. Draw Docked Icon Box
				DrawSupportStanceDockedIcon(spriteBatch, font, ap, iconRect);

				currentY += IconHeight + spacing;
			}

			foreach (var kv in AugmentFamilyRegistry.Families)
			{
				string id = kv.Key;
				var fam = kv.Value;
				int ownedCount = AugmentFamilyRegistry.GetOwnedCount(ap, id);

				if (ownedCount <= 0)
					continue;

				bool isActive = ownedCount >= fam.MaxMembers;
				hoverProgress.TryGetValue(id, out float progress);

				Rectangle iconRect = new Rectangle((int)iconX, (int)currentY, (int)IconWidth, (int)IconHeight);
				iconBounds[id] = iconRect;

				// 1. Draw Slideout Panel (if expanding)
				if (progress > 0.01f)
				{
					DrawSlideoutPanel(spriteBatch, font, fam, ap, ownedCount, iconRect, progress);
				}
				else
				{
					panelBounds[id] = Rectangle.Empty;
				}

				// 2. Draw Docked Icon Box
				DrawDockedIcon(spriteBatch, font, fam, ownedCount, isActive, iconRect);

				currentY += IconHeight + spacing;
			}
		}

		private static void DrawDockedIcon(SpriteBatch spriteBatch, DynamicSpriteFont font, AugmentFamily fam, int ownedCount, bool isActive, Rectangle rect)
		{
			float pulse = (float)Math.Sin(globalTimer * 3.5f) * 0.5f + 0.5f;

			// Glowing aura for completed sets
			if (isActive)
			{
				int auraDist = (int)(2f + pulse * 3f);
				Rectangle auraRect = new Rectangle(rect.X - auraDist, rect.Y - auraDist, rect.Width + auraDist * 2, rect.Height + auraDist * 2);
				Color auraCol = fam.ThemeColor * (0.15f + pulse * 0.20f);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, auraRect, auraCol);
			}

			// Background
			Color bg = new Color(8, 14, 28) * 0.95f;
			spriteBatch.Draw(TextureAssets.MagicPixel.Value, rect, bg);

			bool hasUnlockedThreshold = false;
			foreach (var thresh in fam.ThresholdBonuses.Keys)
			{
				if (ownedCount >= thresh)
				{
					hasUnlockedThreshold = true;
					break;
				}
			}

			// Border
			Color borderColor = isActive
				? Color.Lerp(fam.ThemeColor, Color.White, pulse * 0.35f)
				: hasUnlockedThreshold
					? Color.Lerp(fam.ThemeColor, new Color(120, 150, 180), 0.30f)
					: Color.Lerp(fam.ThemeColor, new Color(90, 110, 130), 0.50f);
			DrawHighTechBorder(spriteBatch, rect, borderColor);

			int cx = rect.X + rect.Width / 2;

			// Upper Section: Family Graphic
			DrawFamilyGraphic(spriteBatch, fam, cx, rect.Y + 15, isActive || hasUnlockedThreshold);

			// Subtle separator rail between icon and count
			spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(rect.X + 4, rect.Y + 26, rect.Width - 8, 1), fam.ThemeColor * (isActive ? 0.35f : 0.15f));

			// Lower Section: Clean integrated count bar
			Rectangle countArea = new Rectangle(rect.X + 2, rect.Y + 27, rect.Width - 4, rect.Height - 29);
			spriteBatch.Draw(TextureAssets.MagicPixel.Value, countArea, new Color(4, 7, 14) * 0.85f);

			string countText = $"{ownedCount}/{fam.MaxMembers}";
			Vector2 numScale = new Vector2(0.55f);
			Vector2 numSize = ChatManager.GetStringSize(font, countText, numScale);
			float numX = cx - numSize.X * 0.5f;
			float numY = countArea.Y + (countArea.Height - numSize.Y) * 0.5f + 8f;
			Color countColor = (isActive || hasUnlockedThreshold) ? AugmentTextColors.Healing : new Color(175, 195, 220);
			ChatManager.DrawColorCodedStringWithShadow(spriteBatch, font, countText, new Vector2(numX, numY), countColor, 0f, Vector2.Zero, numScale);
		}

		private static void DrawFamilyGraphic(SpriteBatch spriteBatch, AugmentFamily fam, int cx, int cy, bool isActive)
		{
			Color iconCol = isActive
				? Color.Lerp(fam.ThemeColor, Color.White, 0.25f)
				: Color.Lerp(fam.ThemeColor, new Color(170, 190, 210), 0.40f);

			// Check custom family ID icons first
			if (fam.Id == AugmentFamilyRegistry.KineticId)
			{
				// Forward Momentum Chevron / Battering Ram
				Color ramOutline = new Color(4, 8, 14) * 0.90f;

				// Left Chevron Outline
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 7, cy - 7, 3, 14), ramOutline);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 5, cy - 5, 3, 10), ramOutline);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 3, cy - 3, 3, 6), ramOutline);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 1, cy - 1, 3, 2), ramOutline);

				// Right Chevron / Ramhead Outline
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 1, cy - 7, 3, 14), ramOutline);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx + 1, cy - 5, 3, 10), ramOutline);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx + 3, cy - 3, 3, 6), ramOutline);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx + 5, cy - 1, 3, 2), ramOutline);

				// Left Chevron Fill
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 6, cy - 5, 2, 10), iconCol);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 4, cy - 3, 2, 6), iconCol);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 2, cy - 1, 2, 2), iconCol);

				// Right Chevron / Ramhead Fill
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx, cy - 5, 2, 10), iconCol);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx + 2, cy - 3, 2, 6), iconCol);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx + 4, cy - 1, 2, 2), iconCol);

				// Active highlights
				if (isActive)
				{
					spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx + 1, cy - 3, 2, 6), Color.White * 0.85f);
					spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx + 3, cy - 1, 2, 2), Color.White * 0.95f);
				}
				return;
			}

			if (fam.Id == AugmentFamilyRegistry.FortuneId || fam.Type == FamilyType.Utility)
			{
				// Lucky 5-Pip Golden Die
				Color dieOutline = new Color(4, 8, 14) * 0.90f;

				// Outline
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 6, cy - 8, 12, 16), dieOutline);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 8, cy - 6, 16, 12), dieOutline);

				// Body Fill (Amber Gold)
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 5, cy - 7, 10, 14), iconCol);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 7, cy - 5, 14, 10), iconCol);

				// Inner Die Face contrast border/accent
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 5, cy - 5, 10, 10), Color.Lerp(iconCol, new Color(40, 20, 0), 0.25f));

				// Dice Pips: classic 5-dot face pattern
				Color pipColor = new Color(15, 20, 32);
				Color pipHighlight = isActive ? Color.White * 0.95f : new Color(255, 240, 180);

				// 4 Corner pips (2x2 each)
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 4, cy - 4, 2, 2), pipColor);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx + 2, cy - 4, 2, 2), pipColor);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 4, cy + 2, 2, 2), pipColor);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx + 2, cy + 2, 2, 2), pipColor);

				// Center pip (2x2) - sparkling highlight when active!
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 1, cy - 1, 2, 2), pipHighlight);
				return;
			}

			if (fam.Id == AugmentFamilyRegistry.CryoId)
			{
				// Procedural Pixel-Art Frost Crystal / Snowflake
				Color iceOutline = new Color(4, 8, 14) * 0.90f;

				// Dark Contrast Outlines
				// Vertical & Horizontal Cross
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 2, cy - 8, 4, 16), iceOutline);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 8, cy - 2, 16, 4), iceOutline);
				// 4 Diagonal Branches
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 6, cy - 6, 4, 4), iceOutline);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx + 2, cy - 6, 4, 4), iceOutline);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 6, cy + 2, 4, 4), iceOutline);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx + 2, cy + 2, 4, 4), iceOutline);

				// Glacial Ice Cyan Fills (#38BDF8)
				// Central Cross
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 1, cy - 7, 2, 14), iconCol);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 7, cy - 1, 14, 2), iconCol);
				// 4 Diagonal Arms
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 5, cy - 5, 2, 2), iconCol);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx + 3, cy - 5, 2, 2), iconCol);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 5, cy + 3, 2, 2), iconCol);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx + 3, cy + 3, 2, 2), iconCol);

				// Branching V-tips on 4 main cardinal axes
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 3, cy - 5, 6, 1), iconCol);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 3, cy + 4, 6, 1), iconCol);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 5, cy - 3, 1, 6), iconCol);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx + 4, cy - 3, 1, 6), iconCol);

				// Bright Diamond Core
				Color coreColor = isActive ? Color.White : Color.Lerp(iconCol, Color.White, 0.45f);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 2, cy - 2, 4, 4), coreColor);

				// Sparkling Glacial Highlights when active
				if (isActive)
				{
					spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 1, cy - 1, 2, 2), Color.White);
					spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx, cy - 7, 1, 2), Color.White * 0.9f);
					spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx, cy + 5, 1, 2), Color.White * 0.9f);
					spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 7, cy, 2, 1), Color.White * 0.9f);
					spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx + 5, cy, 2, 1), Color.White * 0.9f);
				}
				return;
			}

			if (fam.Id == AugmentFamilyRegistry.VoltId)
			{
				// Procedural Pixel-Art Double Lightning Bolt / High-Voltage Spark
				Color voltOutline = new Color(4, 8, 14) * 0.90f;

				// Dark Contrast Outlines
				// Primary Bolt (Left/Main)
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 2, cy - 8, 5, 5), voltOutline);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 5, cy - 4, 6, 5), voltOutline);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 7, cy - 1, 8, 4), voltOutline);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 5, cy + 2, 5, 4), voltOutline);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 6, cy + 5, 4, 4), voltOutline);

				// Secondary Twin Bolt (Right/Offset)
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx + 2, cy - 7, 4, 4), voltOutline);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx + 1, cy - 4, 5, 4), voltOutline);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx + 2, cy - 1, 4, 4), voltOutline);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx + 2, cy + 2, 3, 4), voltOutline);

				// Electric Violet Fills (#8B5CF6)
				// Primary Bolt
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 1, cy - 7, 3, 4), iconCol);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 4, cy - 3, 4, 3), iconCol);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 6, cy, 6, 2), iconCol);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 4, cy + 2, 3, 3), iconCol);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 5, cy + 5, 2, 3), iconCol);

				// Secondary Twin Bolt
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx + 3, cy - 6, 2, 3), iconCol);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx + 2, cy - 3, 3, 2), iconCol);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx + 3, cy - 1, 2, 3), iconCol);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx + 3, cy + 2, 1, 3), iconCol);

				// Active High-Voltage Spark Highlights
				if (isActive)
				{
					// Pure white electrified core along primary bolt
					spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 3, cy - 1, 3, 2), Color.White);
					spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 1, cy - 6, 1, 2), Color.White * 0.9f);
					spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 5, cy + 6, 1, 1), Color.White * 0.95f);

					// Twin bolt spark highlight
					spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx + 3, cy - 2, 1, 2), Color.White);

					// Ambient discharge sparks
					spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx + 6, cy - 4, 1, 1), Color.White * 0.85f);
					spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 7, cy + 3, 1, 1), Color.White * 0.85f);
				}
				return;
			}

			if (fam.Id == AugmentFamilyRegistry.HivemindId)
			{
				// Procedural Pixel-Art Micro-Drone / Nanite Cell Cluster
				Color droneOutline = new Color(4, 8, 14) * 0.90f;

				// Dark Contrast Outlines
				// Center Nanite Core Chassis (Hexagonal)
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 4, cy - 3, 8, 6), droneOutline);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 3, cy - 4, 6, 8), droneOutline);

				// 4 Satellite Micro-Drones (Top, Bottom, Left, Right)
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 2, cy - 8, 5, 4), droneOutline);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 2, cy + 5, 5, 4), droneOutline);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 8, cy - 2, 4, 5), droneOutline);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx + 5, cy - 2, 4, 5), droneOutline);

				// Linkage Struts
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 1, cy - 5, 3, 2), droneOutline);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 1, cy + 4, 3, 2), droneOutline);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 5, cy - 1, 2, 3), droneOutline);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx + 4, cy - 1, 2, 3), droneOutline);

				// Toxic Neon Lime Fills (#10B981)
				// Center Chassis
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 3, cy - 2, 6, 4), iconCol);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 2, cy - 3, 4, 6), iconCol);

				// Satellite Drones
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 1, cy - 7, 3, 2), iconCol);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 1, cy + 6, 3, 2), iconCol);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 7, cy - 1, 2, 3), iconCol);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx + 6, cy - 1, 2, 3), iconCol);

				// Bus Struts
				Color strutCol = Color.Lerp(iconCol, new Color(4, 8, 14), 0.35f);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx, cy - 5, 1, 2), strutCol);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx, cy + 4, 1, 2), strutCol);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 5, cy, 2, 1), strutCol);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx + 4, cy, 2, 1), strutCol);

				// Orbital Swarm Nanite Motes (4 Corners)
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 5, cy - 5, 1, 1), iconCol);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx + 5, cy - 5, 1, 1), iconCol);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 5, cy + 5, 1, 1), iconCol);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx + 5, cy + 5, 1, 1), iconCol);

				// Optical Sensor / Power Core
				Color coreColor = isActive ? Color.White : Color.Lerp(iconCol, Color.White, 0.45f);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 1, cy - 1, 2, 2), coreColor);

				// Active Drone Emitters and Swarm Highlights
				if (isActive)
				{
					// Satellite drone emitter glints
					spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx, cy - 7, 1, 1), Color.White);
					spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx, cy + 7, 1, 1), Color.White);
					spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 7, cy, 1, 1), Color.White);
					spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx + 7, cy, 1, 1), Color.White);

					// Corner nanite motes sparkle
					spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 5, cy - 5, 1, 1), Color.White * 0.9f);
					spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx + 5, cy - 5, 1, 1), Color.White * 0.9f);
					spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 5, cy + 5, 1, 1), Color.White * 0.9f);
					spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx + 5, cy + 5, 1, 1), Color.White * 0.9f);
				}
				return;
			}

			if (fam.Id == AugmentFamilyRegistry.MarksmanId)
			{
				// Procedural Pixel-Art Sniper Crosshairs / Reticle Targeting Matrix
				Color scopeOutline = new Color(4, 8, 14) * 0.90f;

				// Dark Contrast Outlines
				// Outer Ring Housing
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 5, cy - 9, 10, 4), scopeOutline);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 5, cy + 5, 10, 4), scopeOutline);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 9, cy - 5, 4, 10), scopeOutline);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx + 5, cy - 5, 4, 10), scopeOutline);
				// Diagonal corner junctions
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 7, cy - 7, 4, 4), scopeOutline);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx + 3, cy - 7, 4, 4), scopeOutline);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 7, cy + 3, 4, 4), scopeOutline);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx + 3, cy + 3, 4, 4), scopeOutline);

				// Crosshair lines outline
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 1, cy - 7, 3, 4), scopeOutline);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 1, cy + 3, 3, 4), scopeOutline);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 7, cy - 1, 4, 3), scopeOutline);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx + 3, cy - 1, 4, 3), scopeOutline);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 2, cy - 2, 4, 4), scopeOutline);

				// Laser Ruby Fills (#F43F5E)
				// Ring Arc Segments
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 4, cy - 8, 8, 2), iconCol);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 4, cy + 6, 8, 2), iconCol);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 8, cy - 4, 2, 8), iconCol);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx + 6, cy - 4, 2, 8), iconCol);
				// Diagonal Bevels
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 6, cy - 6, 2, 2), iconCol);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx + 4, cy - 6, 2, 2), iconCol);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 6, cy + 4, 2, 2), iconCol);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx + 4, cy + 4, 2, 2), iconCol);

				// Crosshair Inset Reticle Lines
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx, cy - 7, 1, 4), iconCol);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx, cy + 3, 1, 4), iconCol);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 7, cy, 4, 1), iconCol);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx + 3, cy, 4, 1), iconCol);

				// Center Precision Reticle Pin
				Color pinColor = isActive ? Color.White : Color.Lerp(iconCol, Color.White, 0.40f);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 1, cy - 1, 2, 2), pinColor);

				// Active State: Lock-on Brackets & Laser Emitters
				if (isActive)
				{
					// Reticle tick flares
					spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx, cy - 7, 1, 1), Color.White);
					spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx, cy + 6, 1, 1), Color.White);
					spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 7, cy, 1, 1), Color.White);
					spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx + 6, cy, 1, 1), Color.White);

					// 4 Tactical Corner Lock-On Pins
					Color bracketCol = Color.White * 0.90f;
					spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 8, cy - 8, 3, 1), bracketCol);
					spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 8, cy - 7, 1, 2), bracketCol);
					spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx + 5, cy - 8, 3, 1), bracketCol);
					spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx + 7, cy - 7, 1, 2), bracketCol);
					spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 8, cy + 7, 3, 1), bracketCol);
					spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 8, cy + 5, 1, 2), bracketCol);
					spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx + 5, cy + 7, 3, 1), bracketCol);
					spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx + 7, cy + 5, 1, 2), bracketCol);
				}
				return;
			}

			if (fam.Id == AugmentFamilyRegistry.ArcaneSurgeId)
			{
				// Procedural Pixel-Art Arcane Singularity / Pulsating Mana Core
				Color coreOutline = new Color(4, 8, 14) * 0.90f;

				// Dark Contrast Outlines
				// Outer Rhombus & Satellite Mote Outlines
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 2, cy - 7, 4, 3), coreOutline);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 4, cy - 5, 8, 4), coreOutline);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 5, cy - 2, 10, 5), coreOutline);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 4, cy + 2, 8, 4), coreOutline);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 2, cy + 5, 4, 3), coreOutline);

				// 4 Satellite Mana Mote Outlines
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 2, cy - 9, 4, 4), coreOutline);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 2, cy + 6, 4, 4), coreOutline);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 9, cy - 2, 4, 4), coreOutline);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx + 6, cy - 2, 4, 4), coreOutline);

				// Cosmic Indigo Fills (#818CF8)
				// Central Mana Crystal (Diamond)
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 1, cy - 6, 2, 2), iconCol);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 3, cy - 4, 6, 3), iconCol);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 4, cy - 1, 8, 3), iconCol);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 3, cy + 2, 6, 3), iconCol);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 1, cy + 5, 2, 2), iconCol);

				// 4 Satellite Mana Motes
				Color moteCol = Color.Lerp(iconCol, Color.White, 0.25f);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 1, cy - 8, 2, 2), moteCol);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 1, cy + 7, 2, 2), moteCol);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 8, cy - 1, 2, 2), moteCol);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx + 7, cy - 1, 2, 2), moteCol);

				// Inner Facet Highlight
				Color innerFacet = isActive ? Color.White : Color.Lerp(iconCol, Color.White, 0.45f);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 2, cy - 2, 4, 4), innerFacet);

				// Active Flares & Glints
				if (isActive)
				{
					// Core center singularity flare
					spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 1, cy - 1, 2, 2), Color.White);

					// Satellite motes shine
					spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx, cy - 8, 1, 1), Color.White);
					spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx, cy + 7, 1, 1), Color.White);
					spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 8, cy, 1, 1), Color.White);
					spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx + 7, cy, 1, 1), Color.White);

					// Diagonal cosmic energy motes
					spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 5, cy - 5, 1, 1), Color.White * 0.9f);
					spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx + 5, cy - 5, 1, 1), Color.White * 0.9f);
					spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 5, cy + 5, 1, 1), Color.White * 0.9f);
					spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx + 5, cy + 5, 1, 1), Color.White * 0.9f);
				}
				return;
			}

			if (fam.Id == AugmentFamilyRegistry.BastionId)
			{
				// Procedural Pixel-Art Heavy Kinetic Aegis / Fortified Barrier Matrix
				Color aegisOutline = new Color(4, 8, 14) * 0.90f;

				// Dark Contrast Outlines
				// Top Crown / Crest Rim
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 8, cy - 8, 16, 4), aegisOutline);
				// Upper Shield Body
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 9, cy - 6, 18, 6), aegisOutline);
				// Tapering Lower Body (Kite Chevron)
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 7, cy, 14, 4), aegisOutline);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 5, cy + 3, 10, 4), aegisOutline);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 3, cy + 6, 6, 3), aegisOutline);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 1, cy + 8, 2, 2), aegisOutline);

				// Bastion Cyan Fills (#38BDF8)
				// Top Crown Flat Rim
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 7, cy - 7, 14, 2), iconCol);
				// Shield Body Plating
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 8, cy - 5, 16, 4), iconCol);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 6, cy - 1, 12, 4), iconCol);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 4, cy + 3, 8, 3), iconCol);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 2, cy + 6, 4, 2), iconCol);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 1, cy + 8, 2, 1), iconCol);

				// Inner Kinetic Reinforcement Inset (Darker Bevel)
				Color innerBevel = Color.Lerp(iconCol, new Color(4, 8, 14), 0.40f);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 5, cy - 4, 10, 2), innerBevel);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 4, cy - 2, 8, 3), innerBevel);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 2, cy + 1, 4, 3), innerBevel);

				// Central Kinetic Power Core / Bulwark Diamond Lens
				Color coreCol = isActive ? Color.White : Color.Lerp(iconCol, Color.White, 0.45f);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 2, cy - 2, 4, 2), coreCol);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 1, cy - 3, 2, 4), coreCol);

				// Active Flares & Shield Glints
				if (isActive)
				{
					// Core center diamond glint
					spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 1, cy - 2, 2, 2), Color.White);

					// Top shoulder bastion emitters
					spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 6, cy - 7, 2, 1), Color.White);
					spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx + 4, cy - 7, 2, 1), Color.White);

					// Lower chevron tip flare
					spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 1, cy + 7, 2, 1), Color.White * 0.95f);
				}
				return;
			}

			if (fam.Id == AugmentFamilyRegistry.GunslingerId)
			{
				// Procedural Pixel-Art 6-Chamber Revolver Cylinder / Gatling Rotary Cluster
				Color cylOutline = new Color(4, 8, 14) * 0.90f;
				Color boreColor = new Color(4, 7, 14);

				// Dark Contrast Cylinder Outlines
				// Top and bottom flats
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 5, cy - 9, 10, 3), cylOutline);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 5, cy + 6, 10, 3), cylOutline);
				// Left and right flats
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 9, cy - 5, 3, 10), cylOutline);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx + 6, cy - 5, 3, 10), cylOutline);
				// Diagonal corner junctions
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 7, cy - 7, 4, 4), cylOutline);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx + 3, cy - 7, 4, 4), cylOutline);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 7, cy + 3, 4, 4), cylOutline);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx + 3, cy + 3, 4, 4), cylOutline);

				// High-Caliber Rotary Gold Cylinder Fills (#EAB308)
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 7, cy - 4, 14, 8), iconCol);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 4, cy - 7, 8, 14), iconCol);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 6, cy - 6, 12, 12), iconCol);

				// 6 Chamber Bores (Hollow Chambers)
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 1, cy - 6, 2, 2), boreColor); // 12 o'clock (Firing Chamber)
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 1, cy + 4, 2, 2), boreColor); // 6 o'clock
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 5, cy - 3, 2, 2), boreColor); // 10 o'clock
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx + 3, cy - 3, 2, 2), boreColor); // 2 o'clock
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 5, cy + 1, 2, 2), boreColor); // 8 o'clock
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx + 3, cy + 1, 2, 2), boreColor); // 4 o'clock

				// Central Spindle Axis Pin
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 1, cy - 1, 2, 2), boreColor);
				Color spindleCol = isActive ? Color.White : Color.Lerp(iconCol, Color.White, 0.45f);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx, cy, 1, 1), spindleCol);

				// Active Spin & Heat Glints
				if (isActive)
				{
					// Top chamber firing ignition flash
					spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx, cy - 6, 1, 1), Color.White);
					spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 1, cy - 8, 2, 1), Color.White);

					// Rotary chamber rim glints
					spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx + 3, cy - 3, 1, 1), Color.White * 0.90f);
					spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx + 3, cy + 1, 1, 1), Color.White * 0.75f);
					spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 1, cy + 4, 1, 1), Color.White * 0.60f);

					// Outer rotary spin streaks
					spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx + 6, cy - 2, 1, 4), Color.White * 0.85f);
					spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 7, cy - 2, 1, 4), Color.White * 0.85f);
				}
				return;
			}

			if (fam.Id == AugmentFamilyRegistry.LasherId)
			{
				// Procedural Pixel-Art Coiled Barbed Bullwhip / Resonant Kinetic Lash
				Color whipOutline = new Color(4, 8, 14) * 0.90f;

				// Dark Contrast Outlines
				// Handle & Pommel
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 8, cy + 5, 5, 5), whipOutline);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 6, cy + 2, 5, 4), whipOutline);
				// Lower sweep
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 4, cy + 3, 7, 4), whipOutline);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx + 1, cy + 2, 6, 4), whipOutline);
				// Right ascending arc
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx + 4, cy - 3, 4, 7), whipOutline);
				// Top loop & cracker
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 2, cy - 8, 7, 4), whipOutline);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx + 3, cy - 9, 7, 4), whipOutline);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx + 6, cy - 9, 4, 5), whipOutline);
				// Inner coil loop
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 4, cy - 5, 4, 5), whipOutline);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 2, cy - 3, 4, 4), whipOutline);

				// Fills
				// Grip Handle (Leather Brown Wrap)
				Color handleCol = new Color(120, 53, 15);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 7, cy + 6, 2, 3), handleCol);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 5, cy + 3, 2, 3), handleCol);
				// Brass Ferrule / Collar
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 3, cy + 2, 2, 2), new Color(250, 204, 21));

				// Neural Amber Whip Cord (#F97316)
				// Lower sweep
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 3, cy + 4, 5, 2), iconCol);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx + 2, cy + 3, 3, 2), iconCol);
				// Right arc
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx + 5, cy - 1, 2, 4), iconCol);
				// Top loop
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx + 3, cy - 5, 3, 2), iconCol);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 1, cy - 6, 4, 2), iconCol);
				// Inner coil
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 3, cy - 4, 2, 3), iconCol);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 1, cy - 2, 2, 2), iconCol);

				// Snapping Barbed Cracker Tip
				Color tipCol = Color.Lerp(iconCol, Color.White, 0.35f);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx + 4, cy - 7, 3, 2), tipCol);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx + 7, cy - 8, 2, 2), tipCol);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx + 8, cy - 6, 1, 2), tipCol);

				// Active Kinetic Crackling & Sonic Flares
				if (isActive)
				{
					// Cracker tip sonic ignition flash
					spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx + 7, cy - 8, 2, 2), Color.White);

					// Sonic shockwave emission sparks
					spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx + 8, cy - 10, 1, 1), Color.White);
					spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx + 10, cy - 7, 1, 1), Color.White);
					spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx + 6, cy - 10, 1, 1), Color.White);

					// Energy pulse glints along the cord
					spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx + 5, cy, 1, 2), Color.White * 0.90f);
					spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx + 1, cy - 6, 2, 1), Color.White * 0.90f);
					spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 1, cy + 4, 2, 1), Color.White * 0.80f);
				}
				return;
			}

			switch (fam.Type)
			{
				case FamilyType.Offense:
					// Signature Offense Broadsword (Upright blade, crossguard, hilt, pommel)
					Color swordOutline = new Color(4, 8, 14) * 0.90f;
					// Outlines
					spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 2, cy - 10, 4, 3), swordOutline);
					spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 3, cy - 8, 6, 10), swordOutline);
					spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 8, cy + 1, 16, 5), swordOutline);
					spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 2, cy + 5, 4, 6), swordOutline);
					spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 3, cy + 10, 6, 4), swordOutline);

					// Fills
					spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 1, cy - 9, 2, 2), iconCol);
					spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 2, cy - 7, 4, 9), iconCol);
					spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 7, cy + 2, 14, 3), iconCol);
					spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 1, cy + 5, 2, 5), Color.Lerp(iconCol, Color.Black, 0.35f));
					spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 2, cy + 10, 4, 2), iconCol);

					// Active highlights
					if (isActive)
					{
						spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 1, cy - 6, 2, 7), Color.White * 0.85f);
						spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 1, cy + 2, 2, 2), Color.White * 0.95f);
					}
					break;

				case FamilyType.Support:
				default:
					// Signature Support Plus Sign / Cross (18x18 total, 6px thick)
					// 1px dark contrast outline
					Color outline = new Color(4, 8, 14) * 0.90f;
					spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 10, cy - 4, 20, 8), outline);
					spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 4, cy - 10, 8, 20), outline);

					// Main crossbars
					spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 9, cy - 3, 18, 6), iconCol);
					spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 3, cy - 9, 6, 18), iconCol);

					// Center core highlight when active
					if (isActive)
						spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 1, cy - 1, 2, 2), Color.White * 0.9f);
					break;
			}
		}

		private static void DrawSlideoutPanel(SpriteBatch spriteBatch, DynamicSpriteFont font, AugmentFamily fam, AugmentPlayer ap, int ownedCount, Rectangle iconRect, float progress)
		{
			const float pad = 14f;
			const float lineSpacing = 3f;

			bool isActive = ownedCount >= fam.MaxMembers;

			// Pre-calculate heights & positions for pixel-perfect alignment
			Vector2 titleScale = new Vector2(0.82f);
			Vector2 statusScale = new Vector2(0.72f);
			Vector2 sectionScale = new Vector2(0.68f);
			Vector2 itemScale = new Vector2(0.76f);
			Vector2 perkScale = new Vector2(0.72f);

			string titleText = $"{fam.DisplayName.ToUpper()} PROTOCOL";
			string statusText = isActive 
				? $"PROTOCOL ACTIVE  •  {fam.MaxMembers}/{fam.MaxMembers} INSTALLED" 
				: $"IN PROGRESS  •  {ownedCount}/{fam.MaxMembers} INSTALLED";

			float curHeight = pad;
			float maxContentWidth = 320f;

			void Measure(string text, Vector2 scale, float extraPad = 36f)
			{
				float w = ChatManager.GetStringSize(font, text, scale).X + extraPad;
				if (w > maxContentWidth)
					maxContentWidth = w;
			}

			// Header block
			Vector2 tSz = ChatManager.GetStringSize(font, titleText, titleScale);
			curHeight += tSz.Y + 2f;
			Measure(titleText, titleScale, 36f);

			Vector2 sSz = ChatManager.GetStringSize(font, statusText, statusScale);
			curHeight += sSz.Y + 8f;
			Measure(statusText, statusScale, 36f);

			string fortuneStatLine = fam.Id == AugmentFamilyRegistry.FortuneId
				? $"Active Fortune: +{(int)System.MathF.Round(ap.TotalFortune * 100f)}%  •  World Luck: +{ap.Player.luck:0.00}"
				: null;
			if (fortuneStatLine != null)
			{
				curHeight += ChatManager.GetStringSize(font, fortuneStatLine, statusScale).Y + 4f;
				Measure(fortuneStatLine, statusScale, 36f);
			}

			// Divider 1
			curHeight += 6f;

			// Set Bonuses Section
			string bSectionHeader = "PROTOCOL SPECIFICATIONS";
			curHeight += ChatManager.GetStringSize(font, bSectionHeader, sectionScale).Y + 6f;
			Measure(bSectionHeader, sectionScale, 36f);

			foreach (var kv in fam.ThresholdBonuses)
			{
				int thresh = kv.Key;
				var bonus = kv.Value;
				string bonusLine = $"✓ ({thresh}) {bonus.Title}  •  Active";

				curHeight += ChatManager.GetStringSize(font, bonusLine, itemScale).Y + lineSpacing;
				Measure(bonusLine, itemScale, 36f);

				foreach (var desc in bonus.Descriptions)
				{
					string perkLine = $"• {desc.Trim()}";
					curHeight += ChatManager.GetStringSize(font, perkLine, perkScale).Y + lineSpacing;
					Measure(perkLine, perkScale, 48f);
				}
				curHeight += 4f;
			}

			// Divider 2
			curHeight += 8f;

			// Synergy Members Section
			string mSectionHeader = "ASSIGNED PLUGINS";
			curHeight += ChatManager.GetStringSize(font, mSectionHeader, sectionScale).Y + 6f;
			Measure(mSectionHeader, sectionScale, 36f);

			foreach (var memberId in fam.MemberIds)
			{
				Augment m = AugmentDatabase.GetById(memberId);
				string mName = m?.DisplayName ?? memberId;
				string mLine = $"✓ {mName}  •  INSTALLED";

				curHeight += ChatManager.GetStringSize(font, mLine, itemScale).Y + lineSpacing;
				Measure(mLine, itemScale, 36f);
			}

			curHeight += pad;
			float panelHeight = curHeight;
			float panelWidth = Math.Max(MinPanelWidth, (float)Math.Ceiling(maxContentWidth));

			// Slide animation calculation - flush against icon edge (no gap), slides smoothly in from the left
			float finalX = iconRect.X - panelWidth + 1f;
			float currentX = finalX - (1f - progress) * 16f;
			float panelY = Math.Clamp(iconRect.Y - 16f, 10f, Math.Max(10f, Main.screenHeight - panelHeight - 10f));

			Rectangle panelRect = new Rectangle((int)currentX, (int)panelY, (int)panelWidth, (int)panelHeight);
			panelBounds[fam.Id] = panelRect;

			// Ambient drop shadow (2px expansion)
			spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(panelRect.X - 2, panelRect.Y - 2, panelRect.Width + 4, panelRect.Height + 4), new Color(0, 0, 0, (int)(160 * progress)));

			// Cybernetic chassis background with subtle ambient underglow - fully opaque fill so underlying panels don't bleed through
			spriteBatch.Draw(TextureAssets.MagicPixel.Value, panelRect, new Color(10, 16, 28) * progress);
			spriteBatch.Draw(TextureAssets.MagicPixel.Value, panelRect, fam.ThemeColor * (0.04f * progress));

			// High-tech panel border
			DrawHighTechBorder(spriteBatch, panelRect, fam.ThemeColor * (0.85f * progress));

			// RENDER CONTENT
			float drawY = panelRect.Y + pad;
			float leftX = panelRect.X + 16f;

			// 1. Title (Centered)
			Vector2 titleSz = ChatManager.GetStringSize(font, titleText, titleScale);
			Vector2 titlePos = new Vector2(panelRect.X + (panelRect.Width - titleSz.X) * 0.5f, drawY);
			ChatManager.DrawColorCodedStringWithShadow(spriteBatch, font, titleText, titlePos, fam.ThemeColor * progress, 0f, Vector2.Zero, titleScale);
			drawY += titleSz.Y + 2f;

			// 2. Status Subtitle (Centered, clean inline typography)
			Vector2 statusSz = ChatManager.GetStringSize(font, statusText, statusScale);
			Vector2 statusPos = new Vector2(panelRect.X + (panelRect.Width - statusSz.X) * 0.5f, drawY);
			Color statusCol = isActive ? AugmentTextColors.Healing : new Color(148, 163, 184);
			ChatManager.DrawColorCodedStringWithShadow(spriteBatch, font, statusText, statusPos, statusCol * progress, 0f, Vector2.Zero, statusScale);
			drawY += statusSz.Y + 6f;

			if (fortuneStatLine != null)
			{
				Vector2 fSz = ChatManager.GetStringSize(font, fortuneStatLine, statusScale);
				Vector2 fPos = new Vector2(panelRect.X + (panelRect.Width - fSz.X) * 0.5f, drawY);
				ChatManager.DrawColorCodedStringWithShadow(spriteBatch, font, fortuneStatLine, fPos, new Color(255, 215, 120) * progress, 0f, Vector2.Zero, statusScale);
				drawY += fSz.Y + 6f;
			}

			// Divider 1 (Full width sleek hairline rail)
			DrawDivider(spriteBatch, panelRect, drawY, fam.ThemeColor, progress);
			drawY += 8f;

			// 4. Set Bonuses Section
			string bonusSectionHeader = "PROTOCOL SPECIFICATIONS";
			ChatManager.DrawColorCodedStringWithShadow(spriteBatch, font, bonusSectionHeader, new Vector2(leftX, drawY), new Color(250, 204, 21) * progress, 0f, Vector2.Zero, sectionScale);
			drawY += ChatManager.GetStringSize(font, bonusSectionHeader, sectionScale).Y + 6f;

			foreach (var kv in fam.ThresholdBonuses)
			{
				int thresh = kv.Key;
				var bonus = kv.Value;
				bool unlocked = ownedCount >= thresh;

				string sym = unlocked ? "✓" : "•";
				Color symCol = unlocked ? AugmentTextColors.Healing : new Color(100, 116, 139);
				string threshStr = $" ({thresh}) ";
				Color threshCol = unlocked ? new Color(56, 189, 248) : new Color(100, 116, 139);
				string title = bonus.Title;
				Color titleCol = unlocked ? Color.White : new Color(148, 163, 184);
				string tag = unlocked ? "Active" : "Locked";
				Color tagCol = unlocked ? AugmentTextColors.Healing : new Color(100, 116, 139);

				float curX = leftX;

				// Symbol (✓ or •)
				ChatManager.DrawColorCodedStringWithShadow(spriteBatch, font, sym, new Vector2(curX, drawY), symCol * progress, 0f, Vector2.Zero, itemScale);
				curX += ChatManager.GetStringSize(font, sym, itemScale).X;

				// Threshold (e.g. " (2) ")
				ChatManager.DrawColorCodedStringWithShadow(spriteBatch, font, threshStr, new Vector2(curX, drawY), threshCol * progress, 0f, Vector2.Zero, itemScale);
				curX += ChatManager.GetStringSize(font, threshStr, itemScale).X;

				// Title
				ChatManager.DrawColorCodedStringWithShadow(spriteBatch, font, title, new Vector2(curX, drawY), titleCol * progress, 0f, Vector2.Zero, itemScale);
				curX += ChatManager.GetStringSize(font, title, itemScale).X;

				// Separator
				ChatManager.DrawColorCodedStringWithShadow(spriteBatch, font, "  •  ", new Vector2(curX, drawY), new Color(71, 85, 105) * progress, 0f, Vector2.Zero, itemScale);
				curX += ChatManager.GetStringSize(font, "  •  ", itemScale).X;

				// Status Tag (Active or Locked)
				ChatManager.DrawColorCodedStringWithShadow(spriteBatch, font, tag, new Vector2(curX, drawY), tagCol * progress, 0f, Vector2.Zero, itemScale);

				drawY += ChatManager.GetStringSize(font, title, itemScale).Y + lineSpacing;

				// Indented perk descriptions
				float perkIndent = leftX + 16f;
				foreach (var desc in bonus.Descriptions)
				{
					string perkLine = $"• {desc.Trim()}";
					Color perkCol = unlocked ? new Color(210, 238, 225) : new Color(125, 138, 158);
					ChatManager.DrawColorCodedStringWithShadow(spriteBatch, font, perkLine, new Vector2(perkIndent, drawY), perkCol * progress, 0f, Vector2.Zero, perkScale);
					drawY += ChatManager.GetStringSize(font, perkLine, perkScale).Y + lineSpacing;
				}

				drawY += 4f;
			}

			drawY += 2f;
			// Divider 2 (Full width sleek hairline rail)
			DrawDivider(spriteBatch, panelRect, drawY, fam.ThemeColor, progress);
			drawY += 8f;

			// 5. Synergy Members Section
			string membersSectionHeader = "ASSIGNED PLUGINS";
			ChatManager.DrawColorCodedStringWithShadow(spriteBatch, font, membersSectionHeader, new Vector2(leftX, drawY), new Color(250, 204, 21) * progress, 0f, Vector2.Zero, sectionScale);
			drawY += ChatManager.GetStringSize(font, membersSectionHeader, sectionScale).Y + 6f;

			foreach (var memberId in fam.MemberIds)
			{
				Augment m = AugmentDatabase.GetById(memberId);
				string mName = m?.DisplayName ?? memberId;
				bool owned = ap.HasAugment(memberId);

				string mSymbol = owned ? "✓" : "•";
				Color mSymbolCol = owned ? AugmentTextColors.Healing : new Color(100, 116, 139);
				Color mNameCol = owned ? Color.White : new Color(148, 163, 184);

				// Draw symbol
				Vector2 symSz = ChatManager.GetStringSize(font, mSymbol, itemScale);
				ChatManager.DrawColorCodedStringWithShadow(spriteBatch, font, mSymbol, new Vector2(leftX, drawY), mSymbolCol * progress, 0f, Vector2.Zero, itemScale);

				// Draw name
				Vector2 nameSz = ChatManager.GetStringSize(font, mName, itemScale);
				ChatManager.DrawColorCodedStringWithShadow(spriteBatch, font, mName, new Vector2(leftX + 16f, drawY), mNameCol * progress, 0f, Vector2.Zero, itemScale);

				// Draw status
				string mStatus = owned ? "  •  INSTALLED" : "  •  NOT INSTALLED";
				Color mStatusCol = owned ? AugmentTextColors.Healing * progress : new Color(100, 116, 139) * progress;
				ChatManager.DrawColorCodedStringWithShadow(spriteBatch, font, mStatus, new Vector2(leftX + 16f + nameSz.X, drawY), mStatusCol, 0f, Vector2.Zero, itemScale);

				drawY += symSz.Y + lineSpacing;
			}
		}

		private static void DrawSupportStanceDockedIcon(SpriteBatch spriteBatch, DynamicSpriteFont font, AugmentPlayer ap, Rectangle rect)
		{
			int count = ap.SupportAugmentCount;
			bool isActive = count >= 2;
			Color themeCol = new Color(74, 222, 128); // Emerald Green
			float pulse = (float)Math.Sin(globalTimer * 3.5f) * 0.5f + 0.5f;

			// Glowing aura when active
			if (isActive)
			{
				int auraDist = (int)(2f + pulse * 3f);
				Rectangle auraRect = new Rectangle(rect.X - auraDist, rect.Y - auraDist, rect.Width + auraDist * 2, rect.Height + auraDist * 2);
				Color auraCol = themeCol * (0.15f + pulse * 0.20f);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, auraRect, auraCol);
			}

			// Background
			Color bg = new Color(8, 14, 28) * 0.95f;
			spriteBatch.Draw(TextureAssets.MagicPixel.Value, rect, bg);

			// Border
			Color borderColor = isActive
				? Color.Lerp(themeCol, Color.White, pulse * 0.35f)
				: Color.Lerp(themeCol, new Color(90, 110, 130), 0.55f);
			DrawHighTechBorder(spriteBatch, rect, borderColor);

			int cx = rect.X + rect.Width / 2;

			// Upper Section: Support Shield Graphic
			DrawSupportStanceGraphic(spriteBatch, cx, rect.Y + 15, isActive);

			// Subtle separator rail between icon and count
			spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(rect.X + 4, rect.Y + 26, rect.Width - 8, 1), themeCol * (isActive ? 0.35f : 0.15f));

			// Lower Section: Clean integrated count bar
			Rectangle countArea = new Rectangle(rect.X + 2, rect.Y + 27, rect.Width - 4, rect.Height - 29);
			spriteBatch.Draw(TextureAssets.MagicPixel.Value, countArea, new Color(4, 7, 14) * 0.85f);

			string countText = isActive ? (count >= 5 ? "5/5" : $"{count}/5") : $"{count}/2";
			Vector2 numScale = new Vector2(0.55f);
			Vector2 numSize = ChatManager.GetStringSize(font, countText, numScale);
			float numX = cx - numSize.X * 0.5f;
			float numY = countArea.Y + (countArea.Height - numSize.Y) * 0.5f + 8f;
			Color countColor = isActive ? AugmentTextColors.Healing : new Color(175, 195, 220);
			ChatManager.DrawColorCodedStringWithShadow(spriteBatch, font, countText, new Vector2(numX, numY), countColor, 0f, Vector2.Zero, numScale);
		}

		private static void DrawSupportStanceGraphic(SpriteBatch spriteBatch, int cx, int cy, bool isActive)
		{
			Color iconCol = isActive
				? Color.Lerp(new Color(74, 222, 128), Color.White, 0.25f)
				: Color.Lerp(new Color(74, 222, 128), new Color(170, 190, 210), 0.40f);

			Color outline = new Color(4, 8, 14) * 0.90f;

			// Outer Shield Silhouette (Width 16, Height 18)
			// Dark contrast outline
			spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 7, cy - 8, 14, 8), outline);
			spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 6, cy, 12, 4), outline);
			spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 4, cy + 4, 8, 3), outline);
			spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 2, cy + 7, 4, 2), outline);
			spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 1, cy + 9, 2, 1), outline);

			// Shield Body Fill
			spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 6, cy - 7, 12, 7), iconCol * 0.85f);
			spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 5, cy, 10, 4), iconCol * 0.85f);
			spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 3, cy + 4, 6, 3), iconCol * 0.85f);
			spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 1, cy + 7, 2, 2), iconCol * 0.85f);

			// Center Support Plus/Cross (Width 10, Height 10, thickness 2)
			// Cross Outline
			spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 5, cy - 3, 10, 4), outline);
			spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 2, cy - 6, 4, 10), outline);

			// Cross Fill
			Color crossCol = isActive ? Color.White : new Color(200, 235, 215);
			spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 4, cy - 2, 8, 2), crossCol);
			spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 1, cy - 5, 2, 8), crossCol);

			if (isActive)
			{
				// Brilliant center micro-sparkle
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cx - 1, cy - 2, 2, 2), Color.White);
			}
		}

		private static void DrawSupportStanceSlideoutPanel(SpriteBatch spriteBatch, DynamicSpriteFont font, AugmentPlayer ap, Rectangle iconRect, float progress)
		{
			const float pad = 14f;
			const float lineSpacing = 3f;

			int count = ap.SupportAugmentCount;
			bool isActive = count >= 2;
			Color themeCol = new Color(74, 222, 128); // Emerald Green

			Vector2 titleScale = new Vector2(0.82f);
			Vector2 statusScale = new Vector2(0.72f);
			Vector2 sectionScale = new Vector2(0.68f);
			Vector2 itemScale = new Vector2(0.76f);
			Vector2 perkScale = new Vector2(0.72f);

			string titleText = "SUPPORT CLASS";
			string statusText = isActive
				? (count >= 5
					? "CLASS ACTIVE (MAX TIER)  •  +60 DEFENSE, -5% DAMAGE"
					: (count == 4
						? $"CLASS ACTIVE  •  +40 DEFENSE, -16% DAMAGE ({count}/5 PLUGINS)"
						: (count == 3
							? $"CLASS ACTIVE  •  +30 DEFENSE, -23% DAMAGE ({count}/5 PLUGINS)"
							: $"CLASS ACTIVE  •  +20 DEFENSE, -30% DAMAGE ({count}/5 PLUGINS)")))
				: $"INACTIVE  •  {count}/2 REQUIRED FOR CLASS BONUSES";

			string descText = "Sacrifices direct offensive output to amplify armor and personal defense.";

			float curHeight = pad;
			float maxContentWidth = 330f;

			void Measure(string text, Vector2 scale, float extraPad = 36f)
			{
				float w = ChatManager.GetStringSize(font, text, scale).X + extraPad;
				if (w > maxContentWidth)
					maxContentWidth = w;
			}

			// Header block
			Vector2 tSz = ChatManager.GetStringSize(font, titleText, titleScale);
			curHeight += tSz.Y + 2f;
			Measure(titleText, titleScale, 36f);

			Vector2 sSz = ChatManager.GetStringSize(font, statusText, statusScale);
			curHeight += sSz.Y + 2f;
			Measure(statusText, statusScale, 36f);

			Vector2 dSz = ChatManager.GetStringSize(font, descText, statusScale);
			curHeight += dSz.Y + 6f;
			Measure(descText, statusScale, 36f);

			// Divider 1
			curHeight += 6f;

			// Threshold Matrix Section
			string bSectionHeader = "SUPPORT LEVEL BONUSES";
			curHeight += ChatManager.GetStringSize(font, bSectionHeader, sectionScale).Y + 6f;
			Measure(bSectionHeader, sectionScale, 36f);

			var thresholds = new (int Threshold, string Label, int Defense, int Damage, string Note)[]
			{
				(2, "2 Plugins", 20, -30, "Initial Support Protocol"),
				(3, "3 Plugins", 30, -23, "Reinforced Armor"),
				(4, "4 Plugins", 40, -16, "High Output Shield Lattice"),
				(5, "5+ Plugins", 60, -5, "Peak Operational Efficiency")
			};

			int activeTier = 0;
			if (count >= 5) activeTier = 5;
			else if (count >= 4) activeTier = 4;
			else if (count >= 3) activeTier = 3;
			else if (count >= 2) activeTier = 2;

			foreach (var (req, label, def, dmg, note) in thresholds)
			{
				bool unlocked = count >= req;
				bool isCurrent = (req == 5 && activeTier >= 5) || (req == activeTier);
				string tag = isCurrent ? "Active [CURRENT]" : (unlocked ? "Active" : "Locked");
				string lineHeader = $"✓ ({req}) {note}  •  {tag}";
				curHeight += ChatManager.GetStringSize(font, lineHeader, itemScale).Y + lineSpacing;
				Measure(lineHeader, itemScale, 36f);

				string perkLine = $"• +{def} Defense, {dmg}% Damage Output";
				curHeight += ChatManager.GetStringSize(font, perkLine, perkScale).Y + lineSpacing;
				Measure(perkLine, perkScale, 48f);
				curHeight += 4f;
			}

			// Divider 2
			curHeight += 8f;

			// Assigned Support Plugins Section
			string mSectionHeader = "ASSIGNED SUPPORT PLUGINS";
			curHeight += ChatManager.GetStringSize(font, mSectionHeader, sectionScale).Y + 6f;
			Measure(mSectionHeader, sectionScale, 36f);

			var supportAugs = new List<Augment>();
			foreach (var a in ap.Owned)
			{
				if (a.Class == AugmentClass.Support)
					supportAugs.Add(a);
			}

			if (supportAugs.Count == 0)
			{
				string emptyLine = "• No Support Plugins Installed";
				curHeight += ChatManager.GetStringSize(font, emptyLine, itemScale).Y + lineSpacing;
				Measure(emptyLine, itemScale, 36f);
			}
			else
			{
				foreach (var a in supportAugs)
				{
					string aLine = $"✓ {a.DisplayName}  •  INSTALLED";
					curHeight += ChatManager.GetStringSize(font, aLine, itemScale).Y + lineSpacing;
					Measure(aLine, itemScale, 36f);
				}
			}

			string footerNote = "• Autonomous calibration: Class adapts dynamically as plugins change.";
			curHeight += 4f + ChatManager.GetStringSize(font, footerNote, perkScale).Y + lineSpacing;
			Measure(footerNote, perkScale, 48f);

			curHeight += pad;
			float panelHeight = curHeight;
			float panelWidth = Math.Max(MinPanelWidth, (float)Math.Ceiling(maxContentWidth));

			// Slide animation calculation - flush against icon edge (no gap), slides smoothly in from the left
			float finalX = iconRect.X - panelWidth + 1f;
			float currentX = finalX - (1f - progress) * 16f;
			float panelY = Math.Clamp(iconRect.Y - 16f, 10f, Math.Max(10f, Main.screenHeight - panelHeight - 10f));

			Rectangle panelRect = new Rectangle((int)currentX, (int)panelY, (int)panelWidth, (int)panelHeight);
			panelBounds[SupportStanceId] = panelRect;

			// Ambient drop shadow (2px expansion)
			spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(panelRect.X - 2, panelRect.Y - 2, panelRect.Width + 4, panelRect.Height + 4), new Color(0, 0, 0, (int)(160 * progress)));

			// Cybernetic chassis background
			spriteBatch.Draw(TextureAssets.MagicPixel.Value, panelRect, new Color(10, 16, 28) * progress);
			spriteBatch.Draw(TextureAssets.MagicPixel.Value, panelRect, themeCol * (0.04f * progress));

			// High-tech panel border
			DrawHighTechBorder(spriteBatch, panelRect, themeCol * (0.85f * progress));

			// Render content
			float drawY = panelRect.Y + pad;
			float leftX = panelRect.X + 16f;

			// 1. Title (Centered)
			Vector2 titleSz = ChatManager.GetStringSize(font, titleText, titleScale);
			Vector2 titlePos = new Vector2(panelRect.X + (panelRect.Width - titleSz.X) * 0.5f, drawY);
			ChatManager.DrawColorCodedStringWithShadow(spriteBatch, font, titleText, titlePos, themeCol * progress, 0f, Vector2.Zero, titleScale);
			drawY += titleSz.Y + 2f;

			// 2. Status Subtitle (Centered)
			Vector2 statusSz = ChatManager.GetStringSize(font, statusText, statusScale);
			Vector2 statusPos = new Vector2(panelRect.X + (panelRect.Width - statusSz.X) * 0.5f, drawY);
			Color statusCol = isActive ? AugmentTextColors.Healing : new Color(148, 163, 184);
			ChatManager.DrawColorCodedStringWithShadow(spriteBatch, font, statusText, statusPos, statusCol * progress, 0f, Vector2.Zero, statusScale);
			drawY += statusSz.Y + 2f;

			// 3. Description Note (Centered)
			Vector2 descSz = ChatManager.GetStringSize(font, descText, statusScale);
			Vector2 descPos = new Vector2(panelRect.X + (panelRect.Width - descSz.X) * 0.5f, drawY);
			ChatManager.DrawColorCodedStringWithShadow(spriteBatch, font, descText, descPos, new Color(175, 190, 215) * progress, 0f, Vector2.Zero, statusScale);
			drawY += descSz.Y + 6f;

			// Divider 1
			DrawDivider(spriteBatch, panelRect, drawY, themeCol, progress);
			drawY += 8f;

			// 4. Threshold Matrix Section
			ChatManager.DrawColorCodedStringWithShadow(spriteBatch, font, bSectionHeader, new Vector2(leftX, drawY), new Color(250, 204, 21) * progress, 0f, Vector2.Zero, sectionScale);
			drawY += ChatManager.GetStringSize(font, bSectionHeader, sectionScale).Y + 6f;

			foreach (var (req, label, def, dmg, note) in thresholds)
			{
				bool unlocked = count >= req;
				bool isCurrent = (req == 5 && activeTier >= 5) || (req == activeTier);

				string sym = unlocked ? "✓" : "•";
				Color symCol = unlocked ? AugmentTextColors.Healing : new Color(100, 116, 139);
				string threshStr = $" ({req}) ";
				Color threshCol = unlocked ? new Color(56, 189, 248) : new Color(100, 116, 139);
				string title = note;
				Color titleCol = unlocked ? Color.White : new Color(148, 163, 184);
				string tag = isCurrent ? "Active [CURRENT]" : (unlocked ? "Active" : "Locked");
				Color tagCol = isCurrent ? new Color(251, 191, 36) : (unlocked ? AugmentTextColors.Healing : new Color(100, 116, 139));

				float curX = leftX;

				// Symbol
				ChatManager.DrawColorCodedStringWithShadow(spriteBatch, font, sym, new Vector2(curX, drawY), symCol * progress, 0f, Vector2.Zero, itemScale);
				curX += ChatManager.GetStringSize(font, sym, itemScale).X;

				// Threshold
				ChatManager.DrawColorCodedStringWithShadow(spriteBatch, font, threshStr, new Vector2(curX, drawY), threshCol * progress, 0f, Vector2.Zero, itemScale);
				curX += ChatManager.GetStringSize(font, threshStr, itemScale).X;

				// Title
				ChatManager.DrawColorCodedStringWithShadow(spriteBatch, font, title, new Vector2(curX, drawY), titleCol * progress, 0f, Vector2.Zero, itemScale);
				curX += ChatManager.GetStringSize(font, title, itemScale).X;

				// Separator
				ChatManager.DrawColorCodedStringWithShadow(spriteBatch, font, "  •  ", new Vector2(curX, drawY), new Color(71, 85, 105) * progress, 0f, Vector2.Zero, itemScale);
				curX += ChatManager.GetStringSize(font, "  •  ", itemScale).X;

				// Status Tag
				ChatManager.DrawColorCodedStringWithShadow(spriteBatch, font, tag, new Vector2(curX, drawY), tagCol * progress, 0f, Vector2.Zero, itemScale);

				drawY += ChatManager.GetStringSize(font, title, itemScale).Y + lineSpacing;

				// Indented perk line
				float perkIndent = leftX + 16f;
				string perkLine = $"• +{def} Defense, {dmg}% Damage Output";
				Color perkCol = unlocked ? new Color(210, 238, 225) : new Color(125, 138, 158);
				ChatManager.DrawColorCodedStringWithShadow(spriteBatch, font, perkLine, new Vector2(perkIndent, drawY), perkCol * progress, 0f, Vector2.Zero, perkScale);
				drawY += ChatManager.GetStringSize(font, perkLine, perkScale).Y + lineSpacing;

				drawY += 4f;
			}

			drawY += 2f;
			// Divider 2
			DrawDivider(spriteBatch, panelRect, drawY, themeCol, progress);
			drawY += 8f;

			// 5. Assigned Support Plugins Section
			ChatManager.DrawColorCodedStringWithShadow(spriteBatch, font, mSectionHeader, new Vector2(leftX, drawY), new Color(250, 204, 21) * progress, 0f, Vector2.Zero, sectionScale);
			drawY += ChatManager.GetStringSize(font, mSectionHeader, sectionScale).Y + 6f;

			if (supportAugs.Count == 0)
			{
				string emptyLine = "• No Support Plugins Installed";
				ChatManager.DrawColorCodedStringWithShadow(spriteBatch, font, emptyLine, new Vector2(leftX, drawY), new Color(148, 163, 184) * progress, 0f, Vector2.Zero, itemScale);
				drawY += ChatManager.GetStringSize(font, emptyLine, itemScale).Y + lineSpacing;
			}
			else
			{
				foreach (var a in supportAugs)
				{
					string mSymbol = "✓";
					Color mSymbolCol = AugmentTextColors.Healing;
					string mName = a.DisplayName;
					Color mNameCol = Color.White;

					Vector2 symSz = ChatManager.GetStringSize(font, mSymbol, itemScale);
					Vector2 nameSz = ChatManager.GetStringSize(font, mName, itemScale);

					ChatManager.DrawColorCodedStringWithShadow(spriteBatch, font, mSymbol, new Vector2(leftX, drawY), mSymbolCol * progress, 0f, Vector2.Zero, itemScale);
					ChatManager.DrawColorCodedStringWithShadow(spriteBatch, font, mName, new Vector2(leftX + 16f, drawY), mNameCol * progress, 0f, Vector2.Zero, itemScale);

					string mStatus = "  •  INSTALLED";
					Color mStatusCol = AugmentTextColors.Healing;
					ChatManager.DrawColorCodedStringWithShadow(spriteBatch, font, mStatus, new Vector2(leftX + 16f + nameSz.X, drawY), mStatusCol * progress, 0f, Vector2.Zero, itemScale);

					drawY += symSz.Y + lineSpacing;
				}
			}

			drawY += 4f;
			ChatManager.DrawColorCodedStringWithShadow(spriteBatch, font, footerNote, new Vector2(leftX, drawY), new Color(148, 163, 184) * progress, 0f, Vector2.Zero, perkScale);
		}

		private static void DrawDivider(SpriteBatch spriteBatch, Rectangle panelRect, float y, Color themeCol, float progress)
		{
			int lineLeft = panelRect.X + 16;
			int lineW = panelRect.Width - 32;

			// Subtle full-width hairline rail matching tooltip styling
			spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(lineLeft, (int)y, lineW, 1), themeCol * (0.35f * progress));
		}

		private static void DrawHighTechBorder(SpriteBatch spriteBatch, Rectangle rect, Color color)
		{
			// Thin border lines
			spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(rect.X, rect.Y, rect.Width, 1), color * 0.55f);
			spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), color * 0.55f);
			spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(rect.X, rect.Y, 1, rect.Height), color * 0.55f);
			spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), color * 0.55f);

			// 1px inner hairline accent
			Color innerHairline = Color.White * 0.06f;
			spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(rect.X + 1, rect.Y + 1, rect.Width - 2, 1), innerHairline);
			spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(rect.X + 1, rect.Bottom - 2, rect.Width - 2, 1), innerHairline);
			spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(rect.X + 1, rect.Y + 1, 1, rect.Height - 2), innerHairline);
			spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(rect.Right - 2, rect.Y + 1, 1, rect.Height - 2), innerHairline);

			// Corner brackets
			const int cornerLen = 5;
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
