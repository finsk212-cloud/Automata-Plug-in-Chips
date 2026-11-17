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
		private static bool wasMouseLeft = false;
		private static bool wasMouseRight = false;

		// Rotating natural tips
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
			("[POD 042]", "Holding [Ctrl] in the Plugins menu displays detailed mechanics and damage calculations for installed chips."),
			("[POD 042]", "When inspecting extended specifications with [Ctrl] in the plugin terminal, use the mouse wheel to scroll through detailed telemetry notes."),
			("[POD 042]", "Cycle the sort toggle in the plugin terminal to 'Newest' or filter by '★ New Only' to quickly isolate recently deployed plugin chips."),
			("[POD 042]", "The plugin terminal features multiple loadout slots to rapidly switch between boss encounters, farming, and exploration builds."),
			("[POD 042]", "Left-clicking an advisory notice immediately cycles to the next tactical tip, while right-clicking dismisses it."),
			("[POD 042]", "Banners stored inside a Banner Station project their damage and defense buffs across an entire sector without cluttering arena platforms."),
			("[POD 042]", "Armor penetration ignores enemy defense across all damage classes, significantly boosting your damage against heavily armored enemies."),
			("[POD 042]", "Everything that lives is designed to end. We are perpetually trapped in a never-ending spiral... However, installing high-tier defensive plugins significantly extends the duration.")
		};

		private static bool AdvisoryTipsEnabled => AugmentConfig.Instance == null || AugmentConfig.Instance.EnableAdvisoryTips;

		private static float PeriodicIntervalSeconds => AugmentConfig.Instance == null
			? DefaultPeriodicInterval
			: AugmentConfig.Instance.AdvisoryTipIntervalMinutes * 60f;

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

			// Not marked as seen, so a first-time tip still appears if the player re-enables tips.
			if (!AdvisoryTipsEnabled)
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

			bool leftClicked = Main.mouseLeft && !wasMouseLeft;
			bool rightClicked = Main.mouseRight && !wasMouseRight;

			// Click to interact: Left click cycles to next tip, right click dismisses
			if (isHovered)
			{
				player.mouseInterface = true;

				if (leftClicked)
				{
					TriggerNextRandomTip();
					SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = 0.2f });
					Main.blockMouse = true;
				}
				else if (rightClicked)
				{
					displayTimer = 0f;
					SoundEngine.PlaySound(SoundID.MenuClose);
					Main.blockMouse = true;
				}
			}

			wasMouseLeft = Main.mouseLeft;
			wasMouseRight = Main.mouseRight;

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
			if (AdvisoryTipsEnabled && !isBossActive && displayTimer <= 0f && fadeAlpha <= 0f)
			{
				periodicTimer -= dt;
				if (periodicTimer <= 0f)
				{
					periodicTimer = PeriodicIntervalSeconds;
					TriggerNextRandomTip();
				}
			}
		}

		// True while the current tip is still being "spoken" (revealed letter by letter); the Pod pet bobs faster meanwhile.
		// The tip's heading: Pod 042 becomes the name of the Pod's upgrade (Pod 153 with Autopilot) when the player has one.
		private static string ShownPrefix => string.IsNullOrEmpty(currentPrefix) ? "" : currentPrefix.Replace(PodUpgrades.BasePodName, PodUpgrades.PodName(Main.LocalPlayer));

		internal static bool IsTalking => displayTimer > 0f && fadeAlpha > 0.1f && (DisplayDuration - displayTimer) * RevealLettersPerSecond < currentMessage.Length;

		private const float RevealLettersPerSecond = 55f;

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

		// The Pod sprite comes from PodSprites (one texture per skin). These two stay so the other UI that used them keeps working.
		internal static Texture2D GetPodTexture() => PodSprites.Get(Cosmetic.EquippedPodIndex);

		internal static void DrawPodIcon(SpriteBatch sb, Rectangle box, Color skin) => PodSprites.DrawBlocks(sb, box, skin);

		// The same for a whole skin, so an ending skin shows its own look.
		internal static void DrawPodIcon(SpriteBatch sb, Rectangle box, Cosmetic.PodSkinInfo skin) => PodSprites.DrawBlocks(sb, box, skin);
		private static string bubbleFor = "";
		private static List<string> bubbleLines = new List<string>();

		// Chat colour tags like [c/94A3B8:text] are dropped for the bubble, keeping the text, so the letter-by-letter reveal never cuts through a tag.
		private static readonly System.Text.RegularExpressions.Regex TagRegex =
			new System.Text.RegularExpressions.Regex(@"\[[A-Za-z]+(/[^:\]]*)?:([^\]]*)\]", System.Text.RegularExpressions.RegexOptions.Compiled);

		// The tip as a speech bubble beside the player's own Pod. Only this player's game draws it, so nobody else sees the tips.
		// It takes no clicks (so it can never get in the way of using an item); the tip keybind still moves on to the next one.
		private static void DrawBubble(SpriteBatch sb, Vector2 pod)
		{
			bounds = Rectangle.Empty;

			var font = FontAssets.MouseText.Value;
			const float scale = 0.6f;
			const float maxTextWidth = 190f;

			if (bubbleFor != currentMessage)
			{
				bubbleFor = currentMessage;
				bubbleLines = HubDraw.Wrap(TagRegex.Replace(currentMessage, m => m.Groups[2].Value), maxTextWidth, scale);
			}

			float lineHeight = font.LineSpacing * scale;
			float widest = 0f;
			foreach (string line in bubbleLines)
				widest = Math.Max(widest, HubDraw.Width(line, scale));

			const float headerHeight = 12f;
			float w = Math.Max(widest, 80f) + 18f;
			float h = headerHeight + bubbleLines.Count * lineHeight + 12f;

			float uiW = Main.screenWidth / Main.UIScale;
			float uiH = Main.screenHeight / Main.UIScale;
			bool toRight = pod.X + 24f + w + 8f < uiW;
			float bx = toRight ? pod.X + 24f : pod.X - 24f - w;
			float by = MathHelper.Clamp(pod.Y - h - 8f, 8f, Math.Max(8f, uiH - h - 8f));
			var box = new Rectangle((int)bx, (int)by, (int)w, (int)h);

			float a = fadeAlpha * 0.75f; // kept subtle: the tip should be easy to ignore
			Color cyan = PanelChrome.Tone(new Color(56, 189, 248));
			
			HubDraw.Fill(sb, box, PanelChrome.Tone(new Color(10, 16, 26)) * (0.70f * a));
			HubDraw.Border(sb, box, cyan * (0.35f * a));
			HubDraw.Corners(sb, new Rectangle(box.X - 2, box.Y - 2, box.Width + 4, box.Height + 4), cyan * (0.7f * a), 5, 1);

			// A little tail pointing down toward the Pod.
			int tailX = toRight ? box.X + 12 : box.Right - 12;
			for (int i = 0; i < 7; i++)
			{
				int half = 6 - i;
				HubDraw.Fill(sb, new Rectangle(tailX - half, box.Bottom + i, half * 2 + 1, 1), PanelChrome.Tone(new Color(10, 16, 26)) * (0.92f * a));
				HubDraw.Fill(sb, new Rectangle(tailX - half, box.Bottom + i, 1, 1), cyan * (0.8f * a));
				HubDraw.Fill(sb, new Rectangle(tailX + half, box.Bottom + i, 1, 1), cyan * (0.8f * a));
			}

			Vector2 textScale = new Vector2(scale);
			Color shadowColor = Color.Black * (0.5f * a);
			ModText.DrawCoded(sb, font, ShownPrefix, new Vector2(box.X + 9, box.Y + 5), cyan * (0.8f * a), 0f, Vector2.Zero, new Vector2(0.5f));

			// The text appears letter by letter, as if the Pod were saying it.
			int revealed = (int)Math.Max(0f, Math.Min(currentMessage.Length, (DisplayDuration - displayTimer) * RevealLettersPerSecond));
			float y = box.Y + 5 + headerHeight;
			foreach (string line in bubbleLines)
			{
				if (revealed <= 0)
					break;

				string shown = revealed >= line.Length ? line : line.Substring(0, revealed);
				revealed -= line.Length + 1; // +1 for the space the wrap removed
				var pos = new Vector2(box.X + 9, y);
				ModText.DrawCoded(sb, font, shown, pos + new Vector2(1f, 1f), shadowColor, 0f, Vector2.Zero, textScale);
				ModText.DrawCoded(sb, font, shown, pos, new Color(210, 220, 232) * a, 0f, Vector2.Zero, textScale);
				y += lineHeight;
			}
		}

		public static void Draw(SpriteBatch spriteBatch)
		{
			if (Main.dedServ || Main.gameMenu || fadeAlpha <= 0.001f || string.IsNullOrEmpty(currentMessage))
				return;

			// With a Pod beside you the tip is a speech bubble next to it. Without one (it is switched off) the old ticker at the bottom is used.
			if (PodPetPlayer.TryGetLocalPodScreenPos(out Vector2 podScreen))
			{
				DrawBubble(spriteBatch, podScreen);
				return;
			}

			Texture2D podTexture = PodSprites.Get(Cosmetic.EquippedPodIndex);

			var font = FontAssets.MouseText.Value;
			Vector2 scale = new Vector2(0.74f);

			Vector2 prefixSize = ModText.Size(font, ShownPrefix, scale);
			Vector2 msgSize = ModText.Size(font, currentMessage, scale);

			const float gap = 8f;
			const float podWidth = 20f;
			const float podGap = 7f;
			float totalWidth = podWidth + podGap + prefixSize.X + gap + msgSize.X;
			float textHeight = Math.Max(prefixSize.Y, msgSize.Y);

			// Position horizontally centered at the bottom of the screen
			float posX = (Main.screenWidth - totalWidth) * 0.5f;
			float posY = Main.screenHeight - 56f;

			// Invisible interaction bounds around the text & pod for pause-on-hover and click-to-dismiss
			bounds = new Rectangle((int)posX - 12, (int)posY - 8, (int)totalWidth + 24, (int)textHeight + 28);

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
				Vector2 eyeCenter = podPos + PodSprites.Lens;
				int glowSize = (int)(4f + eyePulse * 2f);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle((int)eyeCenter.X - glowSize / 2, (int)eyeCenter.Y - glowSize / 2, glowSize, glowSize), PanelChrome.Tone(new Color(56, 189, 248)) * ((0.20f + eyePulse * 0.25f) * fadeAlpha));
			}

			Color pColor = PanelChrome.Tone(new Color(56, 189, 248)) * fadeAlpha; // Cyan
			Color mColor = new Color(241, 245, 249) * fadeAlpha; // Crisp white

			// Soft subtle drop shadow behind so the text pops cleanly on any bright or dark terrain
			Color shadowColor = Color.Black * (0.80f * fadeAlpha);
			ModText.DrawCoded(spriteBatch, font, ShownPrefix, prefixPos + new Vector2(1f, 1f), shadowColor, 0f, Vector2.Zero, scale);
			ModText.DrawCoded(spriteBatch, font, currentMessage, msgPos + new Vector2(1f, 1f), shadowColor, 0f, Vector2.Zero, scale);

			// Primary text
			ModText.DrawCoded(spriteBatch, font, ShownPrefix, prefixPos, pColor, 0f, Vector2.Zero, scale);
			ModText.DrawCoded(spriteBatch, font, currentMessage, msgPos, mColor, 0f, Vector2.Zero, scale);

			// Subtle interaction hint when hovered
			if (bounds.Contains(new Point(Main.mouseX, Main.mouseY)) && fadeAlpha > 0.5f)
			{
				string hint = "[Left Click: Next Tip  •  Right Click: Dismiss]";
				Vector2 hintScale = new Vector2(0.56f);
				Vector2 hintSize = ModText.Size(font, hint, hintScale);
				Vector2 hintPos = new Vector2((Main.screenWidth - hintSize.X) * 0.5f, posY + textHeight + 4f);
				ModText.DrawCoded(spriteBatch, font, hint, hintPos, PanelChrome.Tone(new Color(148, 163, 184)) * (0.85f * fadeAlpha), 0f, Vector2.Zero, hintScale);
			}
		}
	}
}
