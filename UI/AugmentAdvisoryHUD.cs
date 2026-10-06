using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.UI.Chat;

namespace Augments
{
	public static class AugmentAdvisoryHUD
	{
		private const float DisplayDuration = 10.0f;
		private const float FadeDuration = 0.4f;
		private const float DefaultPeriodicInterval = 420f; // 7 minutes

		private static string currentPrefix = "";
		private static string currentMessage = "";
		private static float displayTimer = 0f;
		private static float fadeAlpha = 0f;
		private static float periodicTimer = 180f; // Start first periodic tip after 3 mins
		private static int lastTipIndex = -1;
		private static Rectangle bounds = Rectangle.Empty;

		// 12 Rotating natural tips
		private static readonly (string Prefix, string Text)[] RotatingTips = new (string, string)[]
		{
			("[POD 042]", "Hold Left Alt to freely drag and move your pinned DPS and damage tracker cards anywhere on your screen."),
			("[POD 042]", "Protocols unlock powerful team perks when you equip two or four matching plugins. Combining different protocols can create unique builds."),
			("[POD 042]", "Keystone plugins permanently install game changing powers into your build, but each character can only equip one Keystone."),
			("[POD 042]", "Fortune plugins do more than just drop extra Machine Cores. They also directly boost your character's world luck stat."),
			("[POD 042]", "Equipping two Field Medic plugins cuts your Potion Sickness cooldown by 20%, allowing you to heal much more often."),
			("[POD 042]", "Kinetic Protocol turns your movement speed into bonus Melee attack speed. The faster you run or fly, the faster your weapons swing."),
			("[POD 042]", "Press [P] whenever you want to search through your plugins by class, rarity, or keywords."),
			("[POD 042]", "Hover your mouse over the docked icons on the right edge of the screen to slide out full bonus specifications."),
			("[POD 042]", "Universal plugins can be used by any class with zero restrictions, making them versatile choices for any build."),
			("[POD 042]", "You can socket compatible accessories directly into your Medi Gun to share accessory buffs with your tethered patient."),
			("[POD 042]", "Sealed Plugin Caches dropped by monsters can be decrypted with Machine Cores to extract random plugins."),
			("[POD 042]", "Defeating any boss triggers a three card reward screen offering plugins scaled to your current world progression tier."),
			("[POD 042]", "Opening the Combat Analytics menu shows a breakdown of your current DPS alongside total incoming damage absorbed by shields."),
			("[POD 042]", "Right clicking any pinned card on your screen instantly removes it to keep your combat view clear."),
			("[POD 042]", "If none of the offered reward plugins fit your character, you can use the Skip button to decline without penalty."),
			("[POD 042]", "Equipping multiple support class plugins reduces your attack penalty while granting bonus defense and team utility."),
			("[POD 042]", "The Plugin Vendor moves into an empty town room once Skeletron has been defeated."),
			("[POD 042]", "Machine Cores drop reliably from bosses and rare mechanical units, serving as the primary currency for plugin trading and upgrades."),
			("[POD 042]", "Holding a CTRL button in Plugin List menu let's you see a detailed view of plugin.")
		};

		public static void ShowAdvisory(string prefix, string message, bool playSound = true)
		{
			currentPrefix = prefix;
			currentMessage = message;
			displayTimer = DisplayDuration;
			if (playSound && !Main.dedServ)
			{
				SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = 0.2f, Volume = 0.6f });
			}
		}

		public static void TriggerSmartAdvisory(AugmentPlayer ap, string triggerId, string prefix, string message)
		{
			if (ap == null || ap.SeenAdvisoryTriggers.Contains(triggerId))
				return;

			ap.SeenAdvisoryTriggers.Add(triggerId);
			ShowAdvisory(prefix, message, true);
		}

		public static void Update(GameTime gameTime)
		{
			if (Main.dedServ || Main.gameMenu)
				return;

			float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

			var player = Main.LocalPlayer;
			if (player == null || !player.active)
				return;

			var ap = player.GetModPlayer<AugmentPlayer>();
			if (ap == null)
				return;

			// Suppress tips during active boss encounters
			bool isBossActive = false;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (n.active && (n.boss || n.type == NPCID.EaterofWorldsHead || n.type == NPCID.EaterofWorldsBody || n.type == NPCID.EaterofWorldsTail))
				{
					isBossActive = true;
					break;
				}
			}

			Point mouse = new Point(Main.mouseX, Main.mouseY);
			bool isHovered = bounds != Rectangle.Empty && bounds.Contains(mouse) && fadeAlpha > 0.1f;

			// Click to interact: Left click cycles to next tip, right click dismisses
			if (isHovered)
			{
				if (Main.mouseLeft && Main.mouseLeftRelease)
				{
					TriggerNextRandomTip();
					Main.mouseLeftRelease = false;
				}
				else if (Main.mouseRight && Main.mouseRightRelease)
				{
					displayTimer = 0f;
					Main.mouseRightRelease = false;
				}
			}

			// Active toast countdown (pause countdown if player is actively hovering to read)
			if (displayTimer > 0f)
			{
				if (isBossActive)
				{
					displayTimer = 0f; // Instantly dismiss on boss summon
				}
				else if (!isHovered)
				{
					displayTimer -= dt;
				}

				if (fadeAlpha < 1f)
					fadeAlpha = Math.Min(1f, fadeAlpha + dt / FadeDuration);
			}
			else
			{
				if (fadeAlpha > 0f)
					fadeAlpha = Math.Max(0f, fadeAlpha - dt / FadeDuration);
				else
					bounds = Rectangle.Empty;
			}

			// Periodic random tip timer (paused during boss fights)
			if (!isBossActive && displayTimer <= 0f && fadeAlpha <= 0f)
			{
				periodicTimer -= dt;
				if (periodicTimer <= 0f)
				{
					periodicTimer = DefaultPeriodicInterval;
					TriggerNextRandomTip();
				}
			}
		}

		public static void TriggerNextRandomTip()
		{
			if (RotatingTips.Length == 0)
				return;

			int index;
			do
			{
				index = Main.rand.Next(RotatingTips.Length);
			} while (index == lastTipIndex && RotatingTips.Length > 1);

			lastTipIndex = index;
			var (prefix, text) = RotatingTips[index];
			ShowAdvisory(prefix, text, true);
		}

		private static Texture2D podTexture;

		// Option A: Tactical Pod 042 (NieR: Automata authentic)
		private static readonly string[] PodMask = new string[]
		{
			"        AA        ",
			"        AA        ",
			"        ..        ",
			"    ..........    ",
			"    .BBBBBBBS.    ",
			"    .BMMMMMMS.    ",
			"..  .BMMMMMMS.  ..",
			".LL..BMMMMMMS..LL.",
			".LL..BMMEEMMS..LL.",
			".LL..BMMeWMMS..LL.",
			".LL..BMMeEMMS..LL.",
			".LL..BMMMMMMS..LL.",
			".ll..BMMMMMMS..ll.",
			"..  .BMMMMMMS.  ..",
			"    .BMMMMMMS.    ",
			"    .SSSSSSSS.    ",
			"    ....TT....    ",
			"        TT        ",
		};

		private static void EnsurePodTexture()
		{
			if (podTexture != null && !podTexture.IsDisposed)
				return;

			if (Main.graphics?.GraphicsDevice == null)
				return;

			int width = 18;
			int height = 18;
			podTexture = new Texture2D(Main.graphics.GraphicsDevice, width, height);
			Color[] data = new Color[width * height];

			Color outline = new Color(14, 20, 30, 255);
			Color antenna = new Color(160, 175, 195, 255);
			Color bevel = new Color(155, 170, 190, 255);
			Color body = new Color(74, 88, 108, 255);
			Color shadow = new Color(48, 58, 74, 255);
			Color arm = new Color(66, 80, 100, 255);
			Color armJoint = new Color(38, 48, 62, 255);
			Color eyeOuter = new Color(2, 132, 199, 255);
			Color eyeBright = new Color(56, 189, 248, 255);
			Color eyeGlint = new Color(245, 252, 255, 255);
			Color thruster = new Color(14, 165, 233, 235);

			for (int y = 0; y < height; y++)
			{
				string row = PodMask[y];
				for (int x = 0; x < width; x++)
				{
					char c = row[x];
					Color col = Color.Transparent;
					if (c == '.') col = outline;
					else if (c == 'A') col = antenna;
					else if (c == 'B') col = bevel;
					else if (c == 'M') col = body;
					else if (c == 'S') col = shadow;
					else if (c == 'L') col = arm;
					else if (c == 'l') col = armJoint;
					else if (c == 'E') col = eyeOuter;
					else if (c == 'e') col = eyeBright;
					else if (c == 'W') col = eyeGlint;
					else if (c == 'T') col = thruster;

					data[y * width + x] = col;
				}
			}

			podTexture.SetData(data);
		}

		public static void Draw(SpriteBatch spriteBatch)
		{
			if (Main.dedServ || Main.gameMenu || fadeAlpha <= 0.001f || string.IsNullOrEmpty(currentMessage))
				return;

			EnsurePodTexture();

			var font = FontAssets.MouseText.Value;
			Vector2 scale = new Vector2(0.74f);

			Vector2 prefixSize = ChatManager.GetStringSize(font, currentPrefix, scale);
			Vector2 msgSize = ChatManager.GetStringSize(font, currentMessage, scale);

			const float gap = 8f;
			const float podWidth = 18f;
			const float podGap = 7f;
			float totalWidth = podWidth + podGap + prefixSize.X + gap + msgSize.X;
			float textHeight = Math.Max(prefixSize.Y, msgSize.Y);

			// Position horizontally centered at the bottom of the screen
			float posX = (Main.screenWidth - totalWidth) * 0.5f;
			float posY = Main.screenHeight - 56f;

			// Invisible interaction bounds around the text & pod for pause-on-hover and click-to-dismiss
			bounds = new Rectangle((int)posX - 8, (int)posY - 8, (int)totalWidth + 16, (int)textHeight + 16);

			// Smooth floating hover bobbing for Pod 042 - optically leveled with text midline
			float podHover = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 3.5f) * 2.0f;
			Vector2 podPos = new Vector2(posX, posY + (textHeight - 18f) * 0.5f - 5f + podHover);
			podPos = new Vector2((float)Math.Round(podPos.X), (float)Math.Round(podPos.Y));

			Vector2 prefixPos = new Vector2(posX + podWidth + podGap, posY);
			Vector2 msgPos = new Vector2(prefixPos.X + prefixSize.X + gap, posY);

			// 1. Draw Pod 042
			if (podTexture != null)
			{
				// Subtle shadow behind pod
				spriteBatch.Draw(podTexture, podPos + new Vector2(0f, 1f), null, Color.Black * (0.65f * fadeAlpha), 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);

				// Pod sprite
				spriteBatch.Draw(podTexture, podPos, null, Color.White * fadeAlpha, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);

				// Breathing optical lens cyan eye glow
				float eyePulse = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 4.5f) * 0.5f + 0.5f;
				Vector2 eyeCenter = podPos + new Vector2(9f, 9.5f);
				int glowSize = (int)(4f + eyePulse * 2f);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle((int)eyeCenter.X - glowSize / 2, (int)eyeCenter.Y - glowSize / 2, glowSize, glowSize), new Color(56, 189, 248) * ((0.20f + eyePulse * 0.25f) * fadeAlpha));
			}

			Color pColor = new Color(56, 189, 248) * fadeAlpha; // Cyan
			Color mColor = new Color(241, 245, 249) * fadeAlpha; // Crisp white

			// Soft subtle drop shadow behind so the text pops cleanly on any bright or dark terrain
			Color shadowColor = Color.Black * (0.80f * fadeAlpha);
			ChatManager.DrawColorCodedStringWithShadow(spriteBatch, font, currentPrefix, prefixPos + new Vector2(1f, 1f), shadowColor, 0f, Vector2.Zero, scale);
			ChatManager.DrawColorCodedStringWithShadow(spriteBatch, font, currentMessage, msgPos + new Vector2(1f, 1f), shadowColor, 0f, Vector2.Zero, scale);

			// Primary text
			ChatManager.DrawColorCodedStringWithShadow(spriteBatch, font, currentPrefix, prefixPos, pColor, 0f, Vector2.Zero, scale);
			ChatManager.DrawColorCodedStringWithShadow(spriteBatch, font, currentMessage, msgPos, mColor, 0f, Vector2.Zero, scale);

			// Subtle interaction hint when hovered
			if (bounds.Contains(new Point(Main.mouseX, Main.mouseY)) && fadeAlpha > 0.5f)
			{
				string hint = "[Left Click: Next Tip  •  Right Click: Dismiss]";
				Vector2 hintScale = new Vector2(0.56f);
				Vector2 hintSize = ChatManager.GetStringSize(font, hint, hintScale);
				Vector2 hintPos = new Vector2((Main.screenWidth - hintSize.X) * 0.5f, posY + textHeight + 4f);
				ChatManager.DrawColorCodedStringWithShadow(spriteBatch, font, hint, hintPos, new Color(148, 163, 184) * (0.85f * fadeAlpha), 0f, Vector2.Zero, hintScale);
			}
		}
	}
}
