using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using Terraria;
using Terraria.GameContent;

namespace Augments
{
	public static class BunkerMenuTheme
	{
		// FX
		private static float titleTimer = 0f;

		public static void Unload()
		{
			OrbitalBackdrop.Unload();
			titleTimer = 0f;
		}

		// Right edge of the title card as last drawn (0 when it is not being drawn). The home page lays itself out around it.
		public static int CardRight;

		private static bool? isCjkSupported;

		// Whether the game font can draw Japanese; if not the title card falls back to English text.
		private static bool CheckCjkSupport(DynamicSpriteFont font)
		{
			if (isCjkSupported.HasValue)
				return isCjkSupported.Value;

			isCjkSupported = font != null && font.IsCharacterSupported('壊') && font.IsCharacterSupported('世');
			return isCjkSupported.Value;
		}

		public static void Draw(SpriteBatch sb, Vector2 logoDrawCenter)
		{
			Texture2D pixel = TextureAssets.MagicPixel.Value;
			if (pixel == null)
				return;

			int screenW = Main.screenWidth;
			int screenH = Main.screenHeight;
			float time = (float)Main.timeForVisualEffects * 0.02f;

			// Deterministic title transition timer (6.5s total loop)
			titleTimer += 0.01667f;
			if (titleTimer >= 6.5f)
				titleTimer -= 6.5f;

			// 1. Fully procedural orbital scene (rotating Earth, sunrise, nebula, stars, moon)
			OrbitalBackdrop.Draw(sb, pixel, screenW, screenH);

			// 2. Tactical YoRHa Military Corner Brackets
			DrawTacticalFrame(sb, pixel, screenW, screenH, time);

			// 3. NieR:Automata Stylized Title Card / Logo
			DrawNierTitleCard(sb, pixel, screenW, logoDrawCenter, time);
		}

		private static void DrawScanlines(SpriteBatch sb, Texture2D pixel, int screenW, int screenH)
		{
			Color scanlineColor = new Color(0, 0, 0, 14);
			for (int y = 0; y < screenH; y += 4)
			{
				sb.Draw(pixel, new Rectangle(0, y, screenW, 1), scanlineColor);
			}
		}

		private static void DrawTacticalFrame(SpriteBatch sb, Texture2D pixel, int screenW, int screenH, float time)
		{
			var font = FontAssets.MouseText.Value;
			int inset = 22;
			int bracketArm = 48;
			int thick = 2;

			Color frameBeige = MenuLook.Tint(new Color(230, 220, 190)) * 0.90f;

			// Corner brackets, in the FRAME style picked on the COSMETICS page: thin (default), long and heavy, a bracket inside
			// a bracket, or brackets with circuit nodes.
			int style = MenuLook.FrameIndex;
			int arm = style == 1 ? 72 : bracketArm;
			int th = style == 1 ? 4 : thick;

			void Corner(int x, int y, int sx, int sy, int armLen, int thk, Color col, bool node)
			{
				int hx = sx > 0 ? x : x - armLen;
				int hy = sy > 0 ? y : y - thk;
				int vx = sx > 0 ? x : x - thk;
				int vy = sy > 0 ? y : y - armLen;
				sb.Draw(pixel, new Rectangle(hx, hy, armLen, thk), col);
				sb.Draw(pixel, new Rectangle(vx, vy, thk, armLen), col);
				if (node)
					sb.Draw(pixel, new Rectangle(sx > 0 ? x + armLen : x - armLen - 4, hy - 1, 4, 4), col);
			}

			int[] cx = { inset, screenW - inset, inset, screenW - inset };
			int[] cy = { inset, inset, screenH - inset, screenH - inset };
			int[] dx = { 1, -1, 1, -1 };
			int[] dy = { 1, 1, -1, -1 };
			for (int k = 0; k < 4; k++)
			{
				Corner(cx[k], cy[k], dx[k], dy[k], arm, th, frameBeige, true);
				if (style == 2)
					Corner(cx[k] + dx[k] * 8, cy[k] + dy[k] * 8, dx[k], dy[k], 26, 1, frameBeige * 0.8f, false);
				else if (style == 3)
					sb.Draw(pixel, new Rectangle(cx[k] + dx[k] * 9 - 2, cy[k] + dy[k] * 9 - 2, 5, 5), frameBeige);
			}
			if (font == null)
				return;

			// Top-Left Corner: Snug within the ┌ bracket
			float textScale = 0.68f;
			float subScale = 0.58f;
			int padX = 14;
			int padY = 8;

			string tlHeader = "[ YoRHa ORBITAL PATROL // BUNKER PERIMETER ]";
			string tlSub = "TARGET: THE BUNKER [IN VIEW] // COMM: STABLE // OPERATOR: 6O";

			Utils.DrawBorderString(sb, tlHeader, new Vector2(inset + padX, inset + padY), MenuLook.Tint(new Color(245, 236, 212)), textScale);
			Utils.DrawBorderString(sb, tlSub, new Vector2(inset + padX, inset + padY + 18), MenuLook.Tint(new Color(180, 175, 160)) * 0.90f, subScale);

			// Top-Right Corner: Snug within the ┐ bracket (Right-Aligned)
			DateTime now = DateTime.Now;
			string trHeader = $"SYS TIME: {now:HH:mm:ss} // CYCLE: 11945.03.10";
			string trSub = "FFCS: ONLINE // LINK QUALITY: 99.8% // STATUS: NOMINAL";
			Vector2 trHeaderSize = font.MeasureString(trHeader) * textScale;
			Vector2 trSubSize = font.MeasureString(trSub) * subScale;

			Utils.DrawBorderString(sb, trHeader, new Vector2(screenW - inset - padX - trHeaderSize.X, inset + padY), MenuLook.Tint(new Color(240, 232, 210)), textScale);
			Utils.DrawBorderString(sb, trSub, new Vector2(screenW - inset - padX - trSubSize.X, inset + padY + 18), MenuLook.Tint(new Color(180, 175, 160)) * 0.90f, subScale);

			// Bottom-Left Corner: Snug within the └ bracket
			string blHeader = "POD 042: STANDBY [NORMAL] // FFCS: ENGAGED";
			string blSub = "PROGRAMS: LASER [ONLINE] // GATLING [ARMED] // SHIELD [READY]";

			Utils.DrawBorderString(sb, blHeader, new Vector2(inset + padX, screenH - inset - padY - 30), MenuLook.Tint(new Color(242, 234, 212)), textScale);
			Utils.DrawBorderString(sb, blSub, new Vector2(inset + padX, screenH - inset - padY - 12), MenuLook.Tint(new Color(175, 170, 155)) * 0.90f, subScale);

			// Bottom-Right Corner: Audio visualizer
			DrawAudioVisualizer(sb, pixel, screenW - inset - padX, screenH - inset - padY, time);
		}

		private static void DrawAudioVisualizer(SpriteBatch sb, Texture2D pixel, int rightX, int bottomY, float time)
		{
			var font = FontAssets.MouseText.Value;
			int barCount = 14;
			int barW = 3;
			int barGap = 2;
			int totalW = barCount * (barW + barGap);
			int startX = rightX - totalW;

			string label = "AUDIO FREQUENCY // CH-01 [ACTIVE]";

			if (font != null)
			{
				Vector2 lSize = font.MeasureString(label) * 0.58f;
				Utils.DrawBorderString(sb, label, new Vector2(rightX - lSize.X, bottomY - 26), MenuLook.Tint(new Color(188, 178, 155)) * 0.90f, 0.58f);
			}

			Color barColor = MenuLook.Tint(new Color(240, 226, 192)) * 0.92f;
			for (int b = 0; b < barCount; b++)
			{
				float wave = (float)Math.Sin(time * 1.5f + b * 0.45f) * 0.5f + 0.5f;
				float wave2 = (float)Math.Cos(time * 0.9f - b * 0.30f) * 0.35f + 0.35f;
				float hRatio = MathHelper.Clamp(wave * 0.65f + wave2 * 0.35f, 0.15f, 1f);
				int barH = (int)(hRatio * 15f);

				int bx = startX + b * (barW + barGap);
				int by = bottomY - 2 - barH;
				sb.Draw(pixel, new Rectangle(bx, by, barW, barH), barColor);
			}
		}

		private static readonly string[] TerrariaLetters = { "T", "E", "R", "R", "A", "R", "I", "A" };
		private static readonly string[] AutomataLetters = { "A", "U", "T", "O", "M", "A", "T", "A" };
		private static readonly int[] SwitchOrder = { 2, 5, 1, 6, 3, 0, 4, 7 };
		private static readonly char[] GlitchGlyphs = { '0', '1', 'u', 'x', 'm', 't', '_', '-', '/' };

		private struct GlitchBlock
		{
			public int Slot;
			public float RelX;
			public float RelY;
			public int Width;
			public int Height;
			public float Alpha;
		}

		private struct TearLine
		{
			public float RelY, XFrac, WFrac, Alpha;
			public int H;
		}

		private static float glitchChroma, glitchShake;
		private static readonly TearLine[] tearLines = new TearLine[4];
		private static int tearCount;

		// Wraps the letter-decoding state with NieR-style signal instability:
		// chromatic split, whole-title shake, horizontal tear lines, stray glyph flicker and data blocks.
		private static void GetTitleGlitchState(
			float timer, float time,
			out string[] displayLetters,
			out string katakanaRaw,
			out float ghostAlpha,
			out float sliceLineAlpha,
			out List<GlitchBlock> glitchBlocks,
			out float[] slotJitterX,
			out float[] slotJitterY)
		{
			GetTitleGlitchStateCore(timer, time, out displayLetters, out katakanaRaw, out ghostAlpha, out sliceLineAlpha, out glitchBlocks, out slotJitterX, out slotJitterY);

			float inten;
			bool macro = timer >= 3.05f && timer < 5.3f;
			if (timer < 3.05f) inten = 0f;
			else if (timer < 3.2f) inten = 0.5f;        // signal starts to destabilise
			else if (timer < 3.65f) inten = 1f;
			else if (timer < 4.65f) inten = 0.12f;
			else if (timer < 5.0f) inten = 1f;
			else if (timer < 5.3f) inten = 0.4f * (1f - (timer - 5.0f) / 0.3f);
			else inten = 0f;

			int frame = (int)(time * 30f);
			Random r = new Random(frame * 7919 + 13);

			// Short random blips while the title is otherwise stable
			if (inten < 0.2f && new Random((frame / 3) * 104729 + 7).Next(40) == 0)
				inten = Math.Max(inten, 0.4f);

			tearCount = 0;
			glitchChroma = 0f;
			glitchShake = 0f;
			if (inten <= 0f)
				return;

			glitchChroma = inten * (3f + 3.5f * (float)Math.Abs(Math.Sin(time * 38f)));
			glitchShake = (float)Math.Sin(time * 61f) * inten * 3.5f;

			ghostAlpha = Math.Max(ghostAlpha, 0.22f * inten);
			sliceLineAlpha = Math.Max(sliceLineAlpha, r.NextDouble() < 0.5 ? 0.5f * inten : 0f);

			tearCount = 1 + (int)(inten * 2.5f * r.NextDouble());
			for (int i = 0; i < tearCount; i++)
			{
				tearLines[i] = new TearLine
				{
					RelY = (float)r.NextDouble() * 38f,
					XFrac = (float)r.NextDouble() * 0.6f,
					WFrac = 0.15f + (float)r.NextDouble() * 0.5f,
					Alpha = (0.35f + (float)r.NextDouble() * 0.5f) * Math.Min(1f, inten + 0.3f),
					H = r.NextDouble() < 0.7 ? 1 : 2
				};
			}

			// Extra data blocks
			int extra = 2 + (int)(inten * 4f);
			for (int b = 0; b < extra; b++)
			{
				glitchBlocks.Add(new GlitchBlock
				{
					Slot = r.Next(8),
					RelX = (float)(r.NextDouble() * 26.0 - 4.0),
					RelY = (float)(r.NextDouble() * 28.0),
					Width = r.Next(6, 18),
					Height = r.Next(3, 8),
					Alpha = 0.7f + (float)r.NextDouble() * 0.3f
				});
			}

			if (macro)
			{
				// Unstable signal: letters randomly flick to glyphs and jump sideways
				for (int i = 0; i < 8; i++)
				{
					if (r.NextDouble() < 0.14 * inten)
					{
						displayLetters[i] = GlitchGlyphs[r.Next(GlitchGlyphs.Length)].ToString();
						slotJitterX[i] += (float)(r.NextDouble() * 4.0 - 2.0);
					}
					else if (r.NextDouble() < 0.24 * inten)
					{
						slotJitterX[i] += (float)(r.NextDouble() * 6.0 - 3.0) * inten;
					}
					if (r.NextDouble() < 0.08 * inten)
						slotJitterY[i] += (float)(r.NextDouble() * 4.0 - 2.0);
				}
			}
		}

		private static void GetTitleGlitchStateCore(
			float timer, float time,
			out string[] displayLetters,
			out string katakanaRaw,
			out float ghostAlpha,
			out float sliceLineAlpha,
			out List<GlitchBlock> glitchBlocks,
			out float[] slotJitterX,
			out float[] slotJitterY)
		{
			displayLetters = new string[8];
			slotJitterX = new float[8];
			slotJitterY = new float[8];
			glitchBlocks = new List<GlitchBlock>();
			ghostAlpha = 0f;
			sliceLineAlpha = 0f;

			// Phase 0: Pure, stable "T E R R A R I A" (0.0s -> 3.2s)
			if (timer < 3.2f)
			{
				for (int i = 0; i < 8; i++)
					displayLetters[i] = TerrariaLetters[i];

				katakanaRaw = " / テラリア";
				return;
			}

			// Phase 1: Progressive NieR digital decode transition into Automata (3.2s -> 4.2s, 1.0s duration)
			if (timer < 3.65f)
			{
				float prog = (timer - 3.2f) / 0.45f; // 0.0 -> 1.0
				int switchedCount = (int)(prog * 8f);
				ghostAlpha = 0.45f * ((float)Math.Sin(time * 45f) * 0.3f + 0.7f);
				sliceLineAlpha = 0.65f * ((float)Math.Sin(time * 65f) * 0.4f + 0.6f);

				int seed = (int)(time * 24f);
				Random rand = new Random(seed);

				// Default all to Terraria
				for (int i = 0; i < 8; i++)
					displayLetters[i] = TerrariaLetters[i];

				// Switch letters according to NieR decoding sequence
				for (int s = 0; s < switchedCount && s < 8; s++)
				{
					int slot = SwitchOrder[s];
					displayLetters[slot] = AutomataLetters[slot];
				}

				// The currently decoding slot flickers with glyphs
				if (switchedCount < 8)
				{
					int activeSlot = SwitchOrder[switchedCount];
					displayLetters[activeSlot] = GlitchGlyphs[rand.Next(GlitchGlyphs.Length)].ToString();
					slotJitterX[activeSlot] = (float)(rand.NextDouble() * 3.0 - 1.5);
					slotJitterY[activeSlot] = (float)(rand.NextDouble() * 2.0 - 1.0);
				}

				// Multiple white data block artifacts across corrupted and active letters
				if (switchedCount > 0)
				{
					int numBlocks = rand.Next(4, 9);
					for (int b = 0; b < numBlocks; b++)
					{
						int targetSlot = SwitchOrder[rand.Next(Math.Min(switchedCount + 1, 8))];
						glitchBlocks.Add(new GlitchBlock
						{
							Slot = targetSlot,
							RelX = (float)(rand.NextDouble() * 24.0 - 4.0),
							RelY = (float)(rand.NextDouble() * 26.0),
							Width = rand.Next(6, 22),
							Height = rand.Next(3, 9),
							Alpha = 0.80f + (float)rand.NextDouble() * 0.20f
						});
					}
				}

				katakanaRaw = switchedCount >= 4 ? " / オートマタ" : " / テラリア";
				return;
			}

			// Phase 2: Full Hold on "A U T O M A T A" for 1.0 full second (4.2s -> 5.2s)
			if (timer < 4.65f)
			{
				for (int i = 0; i < 8; i++)
					displayLetters[i] = AutomataLetters[i];

				katakanaRaw = " / オートマタ";

				// Vertical ghost duplicate faintly visible underneath like title screen in video
				ghostAlpha = 0.36f + (float)Math.Sin(time * 8f) * 0.08f;

				// Authentic NieR white data block accents on letter strokes (Slide 7 in video)
				int pulse = (int)(timer * 6f) % 4;
				glitchBlocks.Add(new GlitchBlock
				{
					Slot = 4, // 'M'
					RelX = 5f,
					RelY = 4f,
					Width = 14,
					Height = 5,
					Alpha = 0.95f
				});
				glitchBlocks.Add(new GlitchBlock
				{
					Slot = 6, // 'T'
					RelX = 2f,
					RelY = 12f,
					Width = 10,
					Height = 4,
					Alpha = 0.88f
				});
				glitchBlocks.Add(new GlitchBlock
				{
					Slot = 1, // 'U'
					RelX = 8f,
					RelY = 2f,
					Width = 8,
					Height = 6,
					Alpha = 0.90f
				});
				if (pulse >= 2)
				{
					glitchBlocks.Add(new GlitchBlock
					{
						Slot = 7, // 'A'
						RelX = 4f,
						RelY = 16f,
						Width = 12,
						Height = 4,
						Alpha = 0.85f
					});
				}

				return;
			}

			// Phase 3: Glitch / digital decode back to "T E R R A R I A" (5.2s -> 5.7s, 0.5s duration)
			if (timer < 5.0f)
			{
				float prog = (timer - 4.65f) / 0.35f; // 0.0 -> 1.0
				int revertedCount = (int)(prog * 8f);
				ghostAlpha = 0.40f * ((float)Math.Sin(time * 50f) * 0.3f + 0.7f);
				sliceLineAlpha = 0.70f * ((float)Math.Sin(time * 70f) * 0.4f + 0.6f);

				int seed = (int)(time * 24f) + 42;
				Random rand = new Random(seed);

				// Start with Automata
				for (int i = 0; i < 8; i++)
					displayLetters[i] = AutomataLetters[i];

				// Revert letters back to Terraria in reverse sequence
				for (int s = 0; s < revertedCount && s < 8; s++)
				{
					int slot = SwitchOrder[7 - s];
					displayLetters[slot] = TerrariaLetters[slot];
				}

				if (revertedCount < 8)
				{
					int activeSlot = SwitchOrder[7 - revertedCount];
					displayLetters[activeSlot] = GlitchGlyphs[rand.Next(GlitchGlyphs.Length)].ToString();
					slotJitterX[activeSlot] = (float)(rand.NextDouble() * 3.0 - 1.5);
					slotJitterY[activeSlot] = (float)(rand.NextDouble() * 2.0 - 1.0);
				}

				// Multiple white data blocks during glitch back
				int numBlocks = rand.Next(4, 8);
				for (int b = 0; b < numBlocks; b++)
				{
					int targetSlot = SwitchOrder[rand.Next(8)];
					glitchBlocks.Add(new GlitchBlock
					{
						Slot = targetSlot,
						RelX = (float)(rand.NextDouble() * 24.0 - 4.0),
						RelY = (float)(rand.NextDouble() * 26.0),
						Width = rand.Next(6, 20),
						Height = rand.Next(3, 8),
						Alpha = 0.75f + (float)rand.NextDouble() * 0.25f
					});
				}

				katakanaRaw = revertedCount >= 4 ? " / テラリア" : " / オートマタ";
				return;
			}

			// Phase 4: Settle back to stable "T E R R A R I A" (5.7s -> 6.5s)
			for (int i = 0; i < 8; i++)
				displayLetters[i] = TerrariaLetters[i];

			katakanaRaw = " / テラリア";
		}

		private static void DrawNierTitleCard(SpriteBatch sb, Texture2D pixel, int screenW, Vector2 logoDrawCenter, float time)
		{
			var fontDeath = FontAssets.DeathText.Value;
			var fontMouse = FontAssets.MouseText.Value;
			if (fontDeath == null || fontMouse == null)
				return;

			// The card sits at the left, above the menu buttons, so the top of the screen is free for the navigation bar and the
			// preview. Its position is set below, once its width is known.
			float centerX = 0f;
			float titleY = 0f;

			string subTitle = CheckCjkSupport(fontMouse)
				? "— YoRHa OS v4.02 // 人類に栄光あれ —"
				: "— YoRHa OS v4.02 // For the Glory of Mankind —";

			float titleScale = 0.90f;
			float katakanaScale = 0.78f;
			float subScale = 0.68f;

			// NieR:Automata Title Glitch State
			GetTitleGlitchState(
				titleTimer, time,
				out string[] displayLetters,
				out string katakanaRaw,
				out float ghostAlpha,
				out float sliceLineAlpha,
				out List<GlitchBlock> glitchBlocks,
				out float[] jitterX,
				out float[] jitterY);

			string displayKatakana = CheckCjkSupport(fontMouse)
				? katakanaRaw
				: (katakanaRaw.Contains("オートマタ") ? " / Automata" : " / Terraria");

			// Fixed slot layout so typography stays rock-solid
			float slotWidth = 33f;
			float slotsTotalW = slotWidth * 8f;
			// Panel size must not depend on the current text: measure the widest of both variants
			bool cjk = CheckCjkSupport(fontMouse);
			float kataW = Math.Max(
				fontMouse.MeasureString(cjk ? " / オートマタ" : " / Automata").X,
				fontMouse.MeasureString(cjk ? " / テラリア" : " / Terraria").X) * katakanaScale;
			Vector2 subSize = fontMouse.MeasureString(subTitle) * subScale;

			float totalTitleW = slotsTotalW + kataW + 12f;
			float cardW = Math.Max(totalTitleW, subSize.X) + 84f;
			float cardH = 88f;

			// On the HOME section of the Automata page the card sits at the left, above the menu buttons. On every other page (the other
			// Automata sections, the character list, Multiplayer, Settings, a world list...) it stays where it always was, at the top in the middle.
			bool onHome = Main.MenuUI?.CurrentState is AutomataHomeUIState homePage && homePage.IsHome;
			if (onHome)
			{
				centerX = AutomataHomeUIState.Margin + cardW / 2f;
				titleY = AutomataHomeUIState.HomeTop + 14f;
			}
			else
			{
				centerX = screenW / 2f;
				titleY = Math.Max(70f, logoDrawCenter.Y - 60f);
			}

			Rectangle cardRect = new Rectangle((int)(centerX - cardW / 2f), (int)(titleY - 14f), (int)cardW, (int)cardH);
			CardRight = onHome ? cardRect.Right : 0; // the home page reads this to leave room for the card

			// 1. Clean dark translucent backing banner
			sb.Draw(pixel, cardRect, new Color(12, 13, 17) * 0.92f);

			// 2. Faint border outline
			Color cardBorder = MenuLook.Tint(new Color(185, 175, 145)) * 0.40f;
			sb.Draw(pixel, new Rectangle(cardRect.X, cardRect.Y, cardRect.Width, 1), cardBorder);
			sb.Draw(pixel, new Rectangle(cardRect.X, cardRect.Bottom - 1, cardRect.Width, 1), cardBorder);
			sb.Draw(pixel, new Rectangle(cardRect.X, cardRect.Y, 1, cardRect.Height), cardBorder);
			sb.Draw(pixel, new Rectangle(cardRect.Right - 1, cardRect.Y, 1, cardRect.Height), cardBorder);

			// 3. YoRHa Corner Brackets for Title Card
			Color bracketColor = MenuLook.Tint(new Color(240, 230, 200)) * 0.92f;
			int bArm = 14;
			int bThick = 2;

			HubDraw.Corners(sb, cardRect, bracketColor, bArm, bThick);


			int midY = cardRect.Y + cardRect.Height / 2;
			sb.Draw(pixel, new Rectangle(cardRect.X - 1, midY - 2, 3, 5), bracketColor * 0.7f);
			sb.Draw(pixel, new Rectangle(cardRect.Right - 2, midY - 2, 3, 5), bracketColor * 0.7f);

			// 4. Top Classification Bar inside the card
			float hdrScale = 0.52f;
			string headerText = "[ YoRHa FFCS // PROJECT: TERRARIA // VER. 1.1945 ]";
			Vector2 hdrPos = new Vector2(cardRect.X + 16, cardRect.Y + 7);
			Utils.DrawBorderString(sb, headerText, hdrPos, MenuLook.Tint(new Color(175, 168, 150)) * 0.85f, hdrScale);

			// 5. Main Title Letters & Katakana Sub-Logo
			float textStartX = centerX - totalTitleW / 2f + glitchShake;
			Color mainTitleCol = MenuLook.Tint(new Color(248, 242, 222));
			Color ghostCol = MenuLook.Tint(new Color(176, 168, 148));

			// Draw 8 letter slots individually
			for (int i = 0; i < 8; i++)
			{
				string letter = displayLetters[i];
				Vector2 lSize = fontDeath.MeasureString(letter) * titleScale;
				float slotX = textStartX + i * slotWidth;
				float drawX = slotX + (slotWidth - lSize.X) / 2f + jitterX[i];
				float drawY = titleY + 6f + jitterY[i];
				Vector2 charPos = new Vector2(drawX, drawY);

				// NieR vertical ghost duplicate underneath (Slide 7 in reference video)
				if (ghostAlpha > 0f)
				{
					Vector2 ghostPos = charPos + new Vector2(0f, 11f);
					sb.DrawString(fontDeath, letter, ghostPos, ghostCol * ghostAlpha, 0f, Vector2.Zero, titleScale, SpriteEffects.None, 0f);
				}

				// Clean drop shadow
				sb.DrawString(fontDeath, letter, charPos + new Vector2(2, 2), Color.Black * 0.85f, 0f, Vector2.Zero, titleScale, SpriteEffects.None, 0f);

				// Main letter glyph
				sb.DrawString(fontDeath, letter, charPos, mainTitleCol, 0f, Vector2.Zero, titleScale, SpriteEffects.None, 0f);
			}

			// Button-style data blocks: alternating bright blocks and card-colour cutouts, clamped to the letter bodies
			for (int b = 0; b < glitchBlocks.Count; b++)
			{
				var gb = glitchBlocks[b];
				if (gb.Slot < 0 || gb.Slot >= 8)
					continue;

				float slotX = textStartX + gb.Slot * slotWidth + jitterX[gb.Slot];
				int x0 = (int)Math.Max(slotX + gb.RelX, slotX + 4f);
				int x1 = (int)Math.Min(slotX + gb.RelX + gb.Width, slotX + slotWidth - 4f);
				int y0 = (int)Math.Max(titleY + 6f + gb.RelY, titleY + 11f);
				int y1 = (int)Math.Min(titleY + 6f + gb.RelY + gb.Height, titleY + 37f);
				if (x1 <= x0 || y1 <= y0)
					continue;

				Color blockCol = (b % 2 == 0) ? Color.White * gb.Alpha : new Color(12, 13, 17) * 0.95f;
				sb.Draw(pixel, new Rectangle(x0, y0, x1 - x0, y1 - y0), blockCol);
			}

			// Katakana subtitle (drawn to the right of the 8 letters)
			Vector2 kataPos = new Vector2(textStartX + slotsTotalW + 10f, titleY + 18f);

			if (ghostAlpha > 0f)
			{
				Vector2 kataGhostPos = kataPos + new Vector2(0f, 9f);
				sb.DrawString(fontMouse, displayKatakana, kataGhostPos, ghostCol * (ghostAlpha * 0.70f), 0f, Vector2.Zero, katakanaScale, SpriteEffects.None, 0f);
			}

			if (glitchChroma > 0.4f)
			{
				sb.DrawString(fontMouse, displayKatakana, kataPos + new Vector2(-glitchChroma * 0.6f, 0f), new Color(235, 95, 85) * 0.5f, 0f, Vector2.Zero, katakanaScale, SpriteEffects.None, 0f);
				sb.DrawString(fontMouse, displayKatakana, kataPos + new Vector2(glitchChroma * 0.6f, 0f), new Color(110, 225, 235) * 0.5f, 0f, Vector2.Zero, katakanaScale, SpriteEffects.None, 0f);
			}

			sb.DrawString(fontMouse, displayKatakana, kataPos + new Vector2(1, 1), Color.Black * 0.70f, 0f, Vector2.Zero, katakanaScale, SpriteEffects.None, 0f);
			sb.DrawString(fontMouse, displayKatakana, kataPos, MenuLook.Tint(new Color(212, 198, 168)), 0f, Vector2.Zero, katakanaScale, SpriteEffects.None, 0f);

			// Horizontal tear lines, kept inside the title card
			for (int i = 0; i < tearCount; i++)
			{
				TearLine tl = tearLines[i];
				int tw = (int)(totalTitleW * tl.WFrac);
				int tx = (int)(textStartX + totalTitleW * tl.XFrac);
				int ty = (int)(titleY + 6f + tl.RelY);
				sb.Draw(pixel, new Rectangle(tx, ty, tw, tl.H), MenuLook.Tint(new Color(245, 240, 225)) * tl.Alpha);
				sb.Draw(pixel, new Rectangle(tx + tw / 3, ty + tl.H, tw / 2, 1), new Color(12, 13, 17) * (tl.Alpha * 0.9f));
			}

			// Center razor slice line across the title (Slide 0 & 3 in reference video)
			if (sliceLineAlpha > 0f)
			{
				int lineY = (int)(titleY + 24f);
				int lineX = (int)(textStartX - 8f);
				int lineW = (int)(totalTitleW + 16f);
				sb.Draw(pixel, new Rectangle(lineX, lineY, lineW, 1), MenuLook.Tint(new Color(245, 240, 225)) * sliceLineAlpha);
			}

			// 6. Tactical horizontal divider below the main title
			int divY = (int)(titleY + 48f);
			int divW = cardRect.Width - 32;
			int divX = cardRect.X + 16;

			// 7. Subtitle line (For the Glory of Mankind)
			Vector2 subPos = new Vector2(centerX - subSize.X / 2f, divY + 6f);
			Color subColor = MenuLook.Tint(new Color(195, 186, 165)) * 0.92f;
			Utils.DrawBorderString(sb, subTitle, subPos, subColor, subScale);
		}
	}
}
