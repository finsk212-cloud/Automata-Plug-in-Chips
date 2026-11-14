using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.UI.Chat;
using Augments.Core;

namespace Augments
{
	public enum PinnedStatType
	{
		LiveDPS,
		HitsAndCrits,
		TotalDamage,
		CombatTime,
		DamageBlocked
	}

	public class PinnedWidgetData
	{
		public PinnedStatType Type;
		public bool IsPinned;
		public Vector2 Position;
		public bool IsDragging;
		public Vector2 DragOffset;
		public float Width = 160f;
		public float Height = 32f;
	}

	// In-game HUD overlay for pinned combat analytics telemetry cards.
	// Completely accident-proof: in normal gameplay, widgets are locked and mouse clicks pass through
	// without interfering with combat or weapon swings.
	// Hold Left Alt (or Left Shift) to unlock dragging and reposition widgets anywhere on screen.
	public static class AugmentPinnedHUD
	{
		private static readonly Dictionary<PinnedStatType, PinnedWidgetData> widgets = new()
		{
			[PinnedStatType.LiveDPS] = new PinnedWidgetData
			{
				Type = PinnedStatType.LiveDPS,
				IsPinned = false,
				Position = new Vector2(20f, 110f),
				Width = 160f,
				Height = 32f
			},
			[PinnedStatType.HitsAndCrits] = new PinnedWidgetData
			{
				Type = PinnedStatType.HitsAndCrits,
				IsPinned = false,
				Position = new Vector2(20f, 148f),
				Width = 160f,
				Height = 32f
			},
			[PinnedStatType.TotalDamage] = new PinnedWidgetData
			{
				Type = PinnedStatType.TotalDamage,
				IsPinned = false,
				Position = new Vector2(20f, 186f),
				Width = 160f,
				Height = 32f
			},
			[PinnedStatType.CombatTime] = new PinnedWidgetData
			{
				Type = PinnedStatType.CombatTime,
				IsPinned = false,
				Position = new Vector2(20f, 224f),
				Width = 160f,
				Height = 32f
			},
			[PinnedStatType.DamageBlocked] = new PinnedWidgetData
			{
				Type = PinnedStatType.DamageBlocked,
				IsPinned = false,
				Position = new Vector2(20f, 262f),
				Width = 160f,
				Height = 32f
			}
		};

		private static bool wasMouseLeft = false;
		private static bool wasMouseRight = false;
		private static float pulseTimer = 0f;

		public static bool IsPinned(PinnedStatType type) => widgets.TryGetValue(type, out var w) && w.IsPinned;

		public static void SetPinned(PinnedStatType type, bool pinned)
		{
			if (widgets.TryGetValue(type, out var w))
			{
				w.IsPinned = pinned;
			}
		}

		public static void TogglePin(PinnedStatType type)
		{
			if (widgets.TryGetValue(type, out var w))
			{
				w.IsPinned = !w.IsPinned;
			}
		}

		public static bool AnyPinned
		{
			get
			{
				foreach (var w in widgets.Values)
				{
					if (w.IsPinned) return true;
				}
				return false;
			}
		}

		public static bool IsInteractingAny
		{
			get
			{
				if (Main.dedServ || Main.gameMenu)
					return false;

				bool isAltDown = Main.keyState.IsKeyDown(Keys.LeftAlt)
				              || Main.keyState.IsKeyDown(Keys.RightAlt)
				              || Main.keyState.IsKeyDown(Keys.LeftShift);
				if (!isAltDown)
					return false;

				Vector2 mouse = new Vector2(Main.mouseX, Main.mouseY);
				foreach (var w in widgets.Values)
				{
					if (!w.IsPinned)
						continue;
					if (w.IsDragging)
						return true;
					Rectangle rect = new Rectangle((int)w.Position.X, (int)w.Position.Y, (int)w.Width, (int)w.Height);
					if (rect.Contains((int)mouse.X, (int)mouse.Y))
						return true;
				}
				return false;
			}
		}

		public static void Update(GameTime gameTime)
		{
			if (Main.dedServ || Main.gameMenu)
				return;

			float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
			pulseTimer += dt;

			var player = Main.LocalPlayer;
			if (player == null || !player.active)
				return;

			bool isAltDown = Main.keyState.IsKeyDown(Keys.LeftAlt)
			              || Main.keyState.IsKeyDown(Keys.RightAlt)
			              || Main.keyState.IsKeyDown(Keys.LeftShift);

			Vector2 mouse = new Vector2(Main.mouseX, Main.mouseY);
			bool leftPressed = Main.mouseLeft && !wasMouseLeft;
			bool rightPressed = Main.mouseRight && !wasMouseRight;

			foreach (var w in widgets.Values)
			{
				if (!w.IsPinned)
				{
					w.IsDragging = false;
					continue;
				}

				Rectangle rect = new Rectangle((int)w.Position.X, (int)w.Position.Y, (int)w.Width, (int)w.Height);
				bool hovered = rect.Contains((int)mouse.X, (int)mouse.Y);

				if (isAltDown)
				{
					// Close button [✕] on right side
					Rectangle closeRect = new Rectangle(rect.Right - 18, rect.Y + 4, 14, 14);
					bool closeHover = closeRect.Contains((int)mouse.X, (int)mouse.Y);

					if (hovered && rightPressed)
					{
						w.IsPinned = false;
						w.IsDragging = false;
						SoundEngine.PlaySound(SoundID.MenuClose);
						Main.blockMouse = true;
						continue;
					}

					if (closeHover && leftPressed)
					{
						w.IsPinned = false;
						w.IsDragging = false;
						SoundEngine.PlaySound(SoundID.MenuClose);
						Main.blockMouse = true;
						continue;
					}

					if (hovered && leftPressed && !closeHover)
					{
						w.IsDragging = true;
						w.DragOffset = mouse - w.Position;
						SoundEngine.PlaySound(SoundID.MenuTick);
						Main.blockMouse = true;
					}

					if (w.IsDragging)
					{
						if (Main.mouseLeft)
						{
							Vector2 rawPos = mouse - w.DragOffset;
							Vector2 snapPos = rawPos;

							const float snapDist = 14f;
							const float screenMarginX = 20f;
							const float screenMarginY = 20f;
							const float widgetGap = 6f;

							// 1. Sibling snapping (align with other pinned widgets in columns or rows)
							foreach (var other in widgets.Values)
							{
								if (other == w || !other.IsPinned)
									continue;

								// Snap X to other's X (column alignment)
								if (Math.Abs(rawPos.X - other.Position.X) < snapDist)
								{
									snapPos.X = other.Position.X;
								}
								// Snap X to other's right side (horizontal row alignment)
								else if (Math.Abs(rawPos.X - (other.Position.X + other.Width + widgetGap)) < snapDist)
								{
									snapPos.X = other.Position.X + other.Width + widgetGap;
								}
								// Snap X to other's left side
								else if (Math.Abs((rawPos.X + w.Width + widgetGap) - other.Position.X) < snapDist)
								{
									snapPos.X = other.Position.X - w.Width - widgetGap;
								}

								// Snap Y to other's Y (horizontal row alignment)
								if (Math.Abs(rawPos.Y - other.Position.Y) < snapDist)
								{
									snapPos.Y = other.Position.Y;
								}
								// Snap Y below other (vertical column stack)
								else if (Math.Abs(rawPos.Y - (other.Position.Y + other.Height + widgetGap)) < snapDist)
								{
									snapPos.Y = other.Position.Y + other.Height + widgetGap;
								}
								// Snap Y above other
								else if (Math.Abs((rawPos.Y + w.Height + widgetGap) - other.Position.Y) < snapDist)
								{
									snapPos.Y = other.Position.Y - w.Height - widgetGap;
								}
							}

							// 2. Screen boundary snapping
							if (Math.Abs(snapPos.X - screenMarginX) < snapDist)
								snapPos.X = screenMarginX;
							else if (Math.Abs(snapPos.X - (Main.screenWidth - w.Width - screenMarginX)) < snapDist)
								snapPos.X = Main.screenWidth - w.Width - screenMarginX;

							if (Math.Abs(snapPos.Y - screenMarginY) < snapDist)
								snapPos.Y = screenMarginY;
							else if (Math.Abs(snapPos.Y - (Main.screenHeight - w.Height - screenMarginY)) < snapDist)
								snapPos.Y = Main.screenHeight - w.Height - screenMarginY;

							// 3. Screen boundary clamping
							snapPos.X = MathHelper.Clamp(snapPos.X, 4f, Main.screenWidth - w.Width - 4f);
							snapPos.Y = MathHelper.Clamp(snapPos.Y, 4f, Main.screenHeight - w.Height - 4f);

							w.Position = snapPos;
							Main.blockMouse = true;
						}
						else
						{
							w.IsDragging = false;
						}
					}
				}
				else
				{
					// When Alt is NOT pressed, dragging is completely locked to prevent accidental movement in combat
					w.IsDragging = false;
				}
			}

			wasMouseLeft = Main.mouseLeft;
			wasMouseRight = Main.mouseRight;
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

			bool isAltDown = Main.keyState.IsKeyDown(Keys.LeftAlt)
			              || Main.keyState.IsKeyDown(Keys.RightAlt)
			              || Main.keyState.IsKeyDown(Keys.LeftShift);

			var font = FontAssets.MouseText.Value;
			Vector2 mouse = new Vector2(Main.mouseX, Main.mouseY);

			// Pre-fetch telemetry data
			float currentDps = AugmentDamageTracker.GetCurrentDPS();
			var (viewDamage, viewBlocked, sortedRecords) = AugmentDamageTracker.GetCurrentViewData(ap);
			int totalHits = 0;
			int totalCrits = 0;
			foreach (var r in sortedRecords)
			{
				totalHits += r.HitCount;
				totalCrits += r.CritCount;
			}
			float critRate = totalHits > 0 ? ((float)totalCrits / totalHits) * 100f : 0f;
			int combatSec = (int)AugmentDamageTracker.SessionDuration;

			foreach (var w in widgets.Values)
			{
				if (!w.IsPinned)
					continue;

				// Configure stat-specific branding and clean inline text
				Color accentColor;
				string labelText;
				string valueText;

				switch (w.Type)
				{
					case PinnedStatType.LiveDPS:
						accentColor = PanelChrome.StatAccent(w.Type);
						labelText = "DPS";
						valueText = $"{Math.Round(currentDps):N0} /s";
						break;

					case PinnedStatType.HitsAndCrits:
						accentColor = PanelChrome.StatAccent(w.Type);
						labelText = "CRIT";
						valueText = $"{critRate:0.0}% ({totalHits:N0})";
						break;

					case PinnedStatType.TotalDamage:
						accentColor = PanelChrome.StatAccent(w.Type);
						labelText = "DMG";
						valueText = $"{viewDamage:N0}";
						break;

					case PinnedStatType.DamageBlocked:
						accentColor = PanelChrome.StatAccent(w.Type);
						labelText = "BLOCKED";
						valueText = $"{viewBlocked:N0}";
						break;

					case PinnedStatType.CombatTime:
					default:
						accentColor = PanelChrome.StatAccent(w.Type);
						labelText = "TIME";
						valueText = $"{combatSec / 60:D2}:{combatSec % 60:D2}";
						break;
				}

				Vector2 textScale = new Vector2(0.62f);
				string dotSep = "  •  ";
				float labelW = ModText.Size(font, labelText, textScale).X;
				float dotW = ModText.Size(font, dotSep, textScale).X;
				float valW = ModText.Size(font, valueText, textScale).X;
				float totalTextW = labelW + dotW + valW;

				// Dynamic width adaptation: ensures zero text clipping even with huge damage numbers
				float minWidth = isAltDown ? 168f : 156f;
				w.Width = Math.Max(minWidth, (float)Math.Ceiling(totalTextW + (isAltDown ? 52f : 38f)));

				Rectangle rect = new Rectangle((int)w.Position.X, (int)w.Position.Y, (int)w.Width, (int)w.Height);
				bool hovered = rect.Contains((int)mouse.X, (int)mouse.Y);

				// 1. Ambient Underglow
				float underglowPulse = (float)Math.Sin(pulseTimer * 3.5f) * 0.5f + 0.5f;
				float underglowAlpha = hovered ? (0.07f + underglowPulse * 0.04f) : 0.035f;
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, rect, accentColor * underglowAlpha);

				// 2. Solid Cyber Chassis Background
				Color bgColor = PanelChrome.Tone(new Color(10, 16, 28)) * 0.94f;
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, rect, bgColor);

				// 3. Cybernetic Chassis Border
				Color borderColor = isAltDown && w.IsDragging
					? Color.Lerp(new Color(255, 215, 75), Color.White, underglowPulse * 0.45f)
					: (hovered ? Color.Lerp(accentColor, Color.White, 0.30f) : accentColor * 0.60f);

				DrawCyberBorder(spriteBatch, rect, borderColor);

				// The title screen's glitch: now and then the card tears sideways for a moment (each card at its own time).
				float burst = PanelGlitch.IdleStrength(Main.GameUpdateCount, (int)w.Type + 11);
				if (burst > 0f)
				{
					int glitchSeed = (int)(Main.GameUpdateCount / 2) + (int)w.Type * 7;
					PanelGlitch.Bands(spriteBatch, rect, glitchSeed, 1, burst);
					PanelGlitch.Ghost(spriteBatch, rect, glitchSeed, burst * 0.8f);
				}

				// 4. Floating Procedural Stat Icon (zero box outline)
				int icx = rect.X + 15;
				int icy = rect.Y + rect.Height / 2;
				DrawStatIcon(spriteBatch, w.Type, icx, icy, accentColor, currentDps);

				// 5. Clean Inline Metrics
				float textX = rect.X + 27f;
				float textY = rect.Y + (rect.Height - 12.2f) * 0.5f - 1.5f;

				// Label
				ModText.DrawCoded(spriteBatch, font, labelText, new Vector2(textX, textY), accentColor, 0f, Vector2.Zero, textScale);

				// Separator dot
				ModText.DrawCoded(spriteBatch, font, dotSep, new Vector2(textX + labelW, textY), PanelChrome.Tone(new Color(100, 125, 155)) * 0.80f, 0f, Vector2.Zero, textScale);

				// Value
				ModText.DrawCoded(spriteBatch, font, valueText, new Vector2(textX + labelW + dotW, textY), Color.White, 0f, Vector2.Zero, textScale);

				// 6. Alt Drag / Unpin Controls Overlay
				if (isAltDown)
				{
					Rectangle closeRect = new Rectangle(rect.Right - 18, rect.Y + 4, 14, 14);
					bool closeHover = closeRect.Contains((int)mouse.X, (int)mouse.Y);
					Color closeColor = closeHover ? new Color(248, 113, 113) : PanelChrome.Tone(new Color(148, 163, 184)) * 0.8f;

					Vector2 xSz = ModText.Size(font, "✕", new Vector2(0.55f));
					Vector2 xPos = new Vector2(closeRect.X + (closeRect.Width - xSz.X) * 0.5f, closeRect.Y + (closeRect.Height - xSz.Y) * 0.5f);
					ModText.DrawCoded(spriteBatch, font, "✕", xPos, closeColor, 0f, Vector2.Zero, new Vector2(0.55f));

					if (hovered)
					{
						Main.instance.MouseText($"Pinned {labelText}\nDrag to reposition (snaps to screen edges and cards)\nClick ✕ or Right-click to unpin");
					}
				}
				else if (hovered)
				{
					Main.instance.MouseText($"Pinned {labelText}\nHold Alt to drag & reposition");
				}
			}
		}

		private static void DrawStatIcon(SpriteBatch spriteBatch, PinnedStatType type, int cx, int cy, Color accentColor, float currentDps)
		{
			Texture2D pixel = TextureAssets.MagicPixel.Value;

			switch (type)
			{
				case PinnedStatType.LiveDPS:
					// Animated 4-bar waveform (matches sidebar HUD)
					float animPhase = pulseTimer * (currentDps > 0f ? 8f : 2.5f);
					int[] barHeights = {
						2 + (int)(Math.Sin(animPhase) * 1.5f + 1.5f),
						4 + (int)(Math.Sin(animPhase + 1.2f) * 2f + 2f),
						6 + (int)(Math.Sin(animPhase + 2.4f) * 2.5f + 2.5f),
						3 + (int)(Math.Sin(animPhase + 3.6f) * 1.5f + 1.5f)
					};
					int[] barX = { cx - 5, cx - 2, cx + 2, cx + 5 };
					for (int b = 0; b < 4; b++)
					{
						int bh = Math.Clamp(barHeights[b], 2, 8);
						spriteBatch.Draw(pixel, new Rectangle(barX[b], cy + 4 - bh, 2, bh), accentColor);
					}
					break;

				case PinnedStatType.HitsAndCrits:
					// Precision Crosshair / Critical reticle
					spriteBatch.Draw(pixel, new Rectangle(cx - 1, cy - 5, 2, 3), accentColor);
					spriteBatch.Draw(pixel, new Rectangle(cx - 1, cy + 3, 2, 3), accentColor);
					spriteBatch.Draw(pixel, new Rectangle(cx - 5, cy - 1, 3, 2), accentColor);
					spriteBatch.Draw(pixel, new Rectangle(cx + 3, cy - 1, 3, 2), accentColor);
					spriteBatch.Draw(pixel, new Rectangle(cx - 1, cy - 1, 2, 2), Color.White);
					break;

				case PinnedStatType.TotalDamage:
					// Upright combat broadsword
					spriteBatch.Draw(pixel, new Rectangle(cx - 1, cy - 6, 2, 8), accentColor);
					spriteBatch.Draw(pixel, new Rectangle(cx, cy - 7, 1, 1), Color.White);
					spriteBatch.Draw(pixel, new Rectangle(cx - 4, cy + 2, 8, 2), accentColor);
					spriteBatch.Draw(pixel, new Rectangle(cx - 1, cy + 4, 2, 3), Color.Lerp(accentColor, Color.Black, 0.35f));
					spriteBatch.Draw(pixel, new Rectangle(cx - 2, cy + 7, 4, 1), Color.White);
					break;

				case PinnedStatType.DamageBlocked:
					// Bastion energy shield
					spriteBatch.Draw(pixel, new Rectangle(cx - 4, cy - 5, 8, 2), accentColor);
					spriteBatch.Draw(pixel, new Rectangle(cx - 5, cy - 3, 10, 5), accentColor);
					spriteBatch.Draw(pixel, new Rectangle(cx - 4, cy + 2, 8, 2), accentColor);
					spriteBatch.Draw(pixel, new Rectangle(cx - 2, cy + 4, 4, 2), accentColor);
					spriteBatch.Draw(pixel, new Rectangle(cx - 1, cy + 6, 2, 1), Color.White);
					spriteBatch.Draw(pixel, new Rectangle(cx - 1, cy - 2, 2, 3), Color.White);
					break;

				case PinnedStatType.CombatTime:
				default:
					// Chronometer / stopwatch
					spriteBatch.Draw(pixel, new Rectangle(cx - 2, cy - 7, 4, 2), accentColor);
					spriteBatch.Draw(pixel, new Rectangle(cx - 5, cy - 4, 10, 2), accentColor);
					spriteBatch.Draw(pixel, new Rectangle(cx - 5, cy + 3, 10, 2), accentColor);
					spriteBatch.Draw(pixel, new Rectangle(cx - 6, cy - 3, 2, 7), accentColor);
					spriteBatch.Draw(pixel, new Rectangle(cx + 4, cy - 3, 2, 7), accentColor);
					// Dial hands
					spriteBatch.Draw(pixel, new Rectangle(cx - 1, cy, 2, 2), Color.White);
					spriteBatch.Draw(pixel, new Rectangle(cx, cy - 3, 1, 3), Color.White);
					spriteBatch.Draw(pixel, new Rectangle(cx + 1, cy, 3, 1), accentColor);
					break;
			}
		}

		private static void DrawCyberBorder(SpriteBatch spriteBatch, Rectangle rect, Color color)
		{
			Texture2D pixel = TextureAssets.MagicPixel.Value;

			// Outer 1px frame
			spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 1), color * 0.55f);
			spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), color * 0.55f);
			spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, 1, rect.Height), color * 0.55f);
			spriteBatch.Draw(pixel, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), color * 0.55f);

			// Inner 1px hairline accent
			Color innerHairline = Color.White * 0.06f;
			spriteBatch.Draw(pixel, new Rectangle(rect.X + 1, rect.Y + 1, rect.Width - 2, 1), innerHairline);
			spriteBatch.Draw(pixel, new Rectangle(rect.X + 1, rect.Bottom - 2, rect.Width - 2, 1), innerHairline);
			spriteBatch.Draw(pixel, new Rectangle(rect.X + 1, rect.Y + 1, 1, rect.Height - 2), innerHairline);
			spriteBatch.Draw(pixel, new Rectangle(rect.Right - 2, rect.Y + 1, 1, rect.Height - 2), innerHairline);

			// Corner marks, in the frame style chosen on the COSMETICS page
			Color cornerCol = Color.Lerp(color, Color.White, 0.40f);
			PanelChrome.DrawCorners(spriteBatch, rect, cornerCol, 0.7f);
		}
	}
}
