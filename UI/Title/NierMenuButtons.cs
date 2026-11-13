using System;
using System.Diagnostics;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using ReLogic.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace Augments
{
	public static class NierMenuButtons
	{
		private static bool hooksRegistered = false;
		private static string currentHoveredText = "";
		private static float hoverTimer = 0f;
		private static readonly Stopwatch clickClock = Stopwatch.StartNew();
		private static double clickStamp = -10.0;
		private const double ClickGlitchTime = 0.35;

		public static void Load()
		{
			// The title screen only exists on a client.
			if (hooksRegistered || Main.dedServ)
				return;

			Terraria.IL_Main.DrawMenu += Hook_IL_DrawMenu;
			hooksRegistered = true;
		}

		public static void Unload()
		{
			if (!hooksRegistered)
				return;

			Terraria.IL_Main.DrawMenu -= Hook_IL_DrawMenu;
			hooksRegistered = false;
			currentHoveredText = "";
			hoverTimer = 0f;
		}

		private static void Hook_IL_DrawMenu(ILContext il)
		{
			try
			{
				var targetMethod = typeof(DynamicSpriteFontExtensionMethods).GetMethod(
					"DrawString",
					new Type[] {
						typeof(SpriteBatch),
						typeof(DynamicSpriteFont),
						typeof(string),
						typeof(Vector2),
						typeof(Color),
						typeof(float),
						typeof(Vector2),
						typeof(float),
						typeof(SpriteEffects),
						typeof(float)
					});

				var replacementMethod = typeof(NierMenuButtons).GetMethod(
					nameof(CustomDrawString),
					BindingFlags.Public | BindingFlags.Static);

				if (targetMethod == null || replacementMethod == null)
					return;

				var cursor = new ILCursor(il);
				int redirected = 0;

				while (cursor.TryGotoNext(MoveType.Before, i => i.MatchCall(targetMethod)))
				{
					cursor.Next.OpCode = OpCodes.Call;
					cursor.Next.Operand = il.Import(replacementMethod);
					redirected++;
					cursor.Index++;
				}

				ModContent.GetInstance<Augments>()?.Logger.Info($"[NierMenuButtons] Successfully redirected {redirected} DrawString calls in DrawMenu!");
			}
			catch (Exception ex)
			{
				ModContent.GetInstance<Augments>()?.Logger.Warn("[NierMenuButtons] IL hook error: " + ex);
			}
		}

		public static bool IsThemeActive() => AutomataTitleMenu.IsActive;

		public static void CustomDrawString(
			SpriteBatch sb,
			DynamicSpriteFont font,
			string text,
			Vector2 position,
			Color color,
			float rotation,
			Vector2 origin,
			float scale,
			SpriteEffects effects,
			float layerDepth)
		{
			// If NieR theme is not active, or this is not the main menu button font, render normally
			if (!IsThemeActive() || string.IsNullOrEmpty(text) || font != FontAssets.DeathText.Value)
			{
				DynamicSpriteFontExtensionMethods.DrawString(sb, font, text, position, color, rotation, origin, scale, effects, layerDepth);
				return;
			}

			// In vanilla DrawMenu, each button is drawn in a 5-pass loop (k = 0..4).
			// Passes 0..3 are border outline passes (R, G, B are low: 0 for idle, ~51 for hovered).
			// Pass 4 is the front text pass (R >= 75 for idle, R >= 200 for hovered).
			// Suppress all dark border passes so only the front pass renders cleanly without ghosting.
			if (color.R <= 60 && color.G <= 60 && color.B <= 60)
			{
				return;
			}

			Texture2D pixel = TextureAssets.MagicPixel.Value;
			if (pixel == null)
			{
				DynamicSpriteFontExtensionMethods.DrawString(sb, font, text, position, color, rotation, origin, scale, effects, layerDepth);
				return;
			}

			// Clean uppercase formatting for authentic NieR YoRHa military UI
			string upperText = text.ToUpperInvariant();

			float centerX = Main.screenWidth / 2f;
			float centerY = position.Y;

			// Check if the button is hovered:
			// In vanilla Terraria, hovered buttons use bright yellow/gold (high R & G, low B)
			bool isHovered = (color.R > 180 && color.G > 140 && color.B < 125);

			// Compact, sleek scale for refined military terminal typography (smaller & cleaner)
			float drawScale = 0.60f;
			Vector2 textSize = font.MeasureString(upperText) * drawScale;

			// Vertical text alignment centered with the row
			Vector2 textPos = new Vector2(
				(float)Math.Round(centerX - textSize.X / 2f),
				(float)Math.Round(centerY - textSize.Y / 2f + 5f)
			);

			if (isHovered)
			{
				// Track hover duration per button to trigger authentic digital decode glitch on hover
				if (currentHoveredText != upperText)
				{
					currentHoveredText = upperText;
					hoverTimer = 0f;
				}
				else
				{
					hoverTimer += 0.01667f;
				}

				// Click: restart the glitch burst on the frame the mouse goes down over a hovered button
				if (Main.mouseLeft && Main.mouseLeftRelease)
					clickStamp = clickClock.Elapsed.TotalSeconds;
				double clickAge = clickClock.Elapsed.TotalSeconds - clickStamp;
				bool clicking = clickAge < ClickGlitchTime;
				float clickK = clicking ? 1f - (float)(clickAge / ClickGlitchTime) : 0f;
				bool invert = clicking && (clickAge < 0.06 || (clickAge > 0.13 && clickAge < 0.17));
				float shake = 0f;
				if (clicking)
				{
					Random sr = new Random((int)(clickAge * 40.0) * 7717 + 3);
					shake = ((float)sr.NextDouble() * 2f - 1f) * 4f * clickK;
				}
				textPos.X += shake;

				// Spacious, comfortable YoRHa selection banner (34px tall, generous breathing room)
				float bannerW = Math.Max(280f, textSize.X + 68f);
				float bannerH = 34f;
				float bannerX = (float)Math.Round(centerX - bannerW / 2f + shake);
				float bannerY = (float)Math.Round(centerY - bannerH / 2f);
				Rectangle bannerRect = new Rectangle((int)bannerX, (int)bannerY, (int)bannerW, (int)bannerH);

				// 1. Solid YoRHa bone-ivory highlight banner (#EAE5D4)
				Color bannerBg = invert ? new Color(22, 24, 28) : MenuLook.Tint(new Color(234, 229, 212));
				sb.Draw(pixel, bannerRect, bannerBg);

				// 2. Dark charcoal top & bottom borders (#202226)
				Color borderCol = new Color(32, 34, 38);
				sb.Draw(pixel, new Rectangle(bannerRect.X, bannerRect.Y, bannerRect.Width, 1), borderCol * 0.9f);
				sb.Draw(pixel, new Rectangle(bannerRect.X, bannerRect.Bottom - 1, bannerRect.Width, 1), borderCol * 0.9f);

				// 3. Left and right 2px vertical end-caps
				sb.Draw(pixel, new Rectangle(bannerRect.X, bannerRect.Y, 2, bannerRect.Height), borderCol * 0.9f);
				sb.Draw(pixel, new Rectangle(bannerRect.Right - 2, bannerRect.Y, 2, bannerRect.Height), borderCol * 0.9f);

				// 4. Tactical corner brackets (matching NieR YoRHa UI)
				int arm = 6;
				sb.Draw(pixel, new Rectangle(bannerRect.X, bannerRect.Y, arm, 2), borderCol);
				sb.Draw(pixel, new Rectangle(bannerRect.Right - arm, bannerRect.Y, arm, 2), borderCol);
				sb.Draw(pixel, new Rectangle(bannerRect.X, bannerRect.Bottom - 2, arm, 2), borderCol);
				sb.Draw(pixel, new Rectangle(bannerRect.Right - arm, bannerRect.Bottom - 2, arm, 2), borderCol);

				// 5. Signature NieR square cursor pip (■) to the left of the text
				int pipSize = 6;
				int pipX = (int)(textPos.X - 16f);
				int pipY = (int)Math.Round(centerY - pipSize / 2f);
				sb.Draw(pixel, new Rectangle(pipX, pipY, pipSize, pipSize), new Color(24, 26, 30));

				// 6. NieR Digital Decode Glitch Effect on Hover
				// Initial 0.55s burst upon hover, and a periodic subtle 0.35s twitch every 3.5s
				float cycle = hoverTimer % 3.5f;
				bool isGlitching = cycle < 0.55f || clicking;

				Color textDark = invert ? MenuLook.Tint(new Color(234, 229, 212)) : new Color(22, 24, 28);
				int len = upperText.Length;

				if (isGlitching && len > 0)
				{
					int seed = (int)(Main.timeForVisualEffects * 0.6f) + len * 7 + (clicking ? (int)(clickAge * 40.0) * 131 : 0);
					Random rand = new Random(seed);

					// Pre-calculate per-character X offsets
					float[] charX = new float[len + 1];
					for (int i = 0; i <= len; i++)
					{
						charX[i] = font.MeasureString(upperText.Substring(0, i)).X * drawScale;
					}

					// Ghost duplicate underneath (NieR CRT scan artifact)
					DynamicSpriteFontExtensionMethods.DrawString(sb, font, upperText, textPos + new Vector2(0f, 3f), textDark * 0.25f, 0f, Vector2.Zero, drawScale, SpriteEffects.None, 0f);

					// Click: chromatic split of the whole label
					if (clicking)
					{
						float split = 1.5f + 4f * clickK;
						DynamicSpriteFontExtensionMethods.DrawString(sb, font, upperText, textPos + new Vector2(-split, 0f), new Color(215, 85, 75) * 0.55f, 0f, Vector2.Zero, drawScale, SpriteEffects.None, 0f);
						DynamicSpriteFontExtensionMethods.DrawString(sb, font, upperText, textPos + new Vector2(split, 0f), new Color(60, 170, 185) * 0.55f, 0f, Vector2.Zero, drawScale, SpriteEffects.None, 0f);
					}

					// Pick 1-2 slots to glitch into decode glyphs
					int glitchSlot1 = rand.Next(len);
					int glitchSlot2 = rand.Next(len);
					char[] glitchGlyphs = { '0', '1', 'X', 'M', '_', '/', '-', '9', 'T', 'A' };

					// Draw each character with occasional jitter & glyph replacement
					for (int i = 0; i < len; i++)
					{
						string ch = upperText[i].ToString();
						float jx = 0f;
						float jy = 0f;

						if (i == glitchSlot1 || i == glitchSlot2 || (clicking && rand.NextDouble() < 0.35))
						{
							ch = glitchGlyphs[rand.Next(glitchGlyphs.Length)].ToString();
							jx = (float)(rand.NextDouble() * 3.0 - 1.5);
							jy = (float)(rand.NextDouble() * 2.0 - 1.0);
						}

						Vector2 cPos = new Vector2(textPos.X + charX[i] + jx, textPos.Y + jy);
						DynamicSpriteFontExtensionMethods.DrawString(sb, font, ch, cPos, textDark, 0f, Vector2.Zero, drawScale, SpriteEffects.None, 0f);
					}

					// Rectangular data blocks on letters (the authentic boxes from title card / video)
					int numBlocks = clicking ? rand.Next(6, 11) : rand.Next(3, 7);
					for (int b = 0; b < numBlocks; b++)
					{
						int targetSlot = rand.Next(len);
						float slotX = textPos.X + charX[targetSlot];
						int bw = rand.Next(6, 16);
						int bh = rand.Next(3, 7);
						int bx = (int)(slotX + (rand.NextDouble() * 10.0 - 2.0));
						int by = (int)(textPos.Y + (rand.NextDouble() * 14.0));

						// Alternate between charcoal accent blocks and ivory mask cutouts
						Color blockCol = (b % 2 == 0) ? textDark : bannerBg;
						sb.Draw(pixel, new Rectangle(bx, by, bw, bh), blockCol * 0.95f);
					}

					// Horizontal razor slice line across text
					int sliceY = (int)(centerY + (rand.NextDouble() * 12.0 - 6.0));
					sb.Draw(pixel, new Rectangle((int)textPos.X - 8, sliceY, (int)textSize.X + 16, 1), textDark * 0.60f);
				}
				else
				{
					// Stable, clean high-contrast deep charcoal text
					DynamicSpriteFontExtensionMethods.DrawString(sb, font, upperText, textPos, textDark, 0f, Vector2.Zero, drawScale, SpriteEffects.None, 0f);
				}
			}
			else
			{
				// Unselected buttons: pure, clean, minimalist text with no background boxes or borders
				// 1. Subtle 1px drop shadow for crisp legibility against orbital background
				DynamicSpriteFontExtensionMethods.DrawString(sb, font, upperText, textPos + new Vector2(1f, 1f), Color.Black * 0.85f, 0f, Vector2.Zero, drawScale, SpriteEffects.None, 0f);

				// 2. Muted YoRHa bone-silver / platinum text (#D4CEBF)
				Color idleText = MenuLook.Tint(new Color(212, 206, 191));
				DynamicSpriteFontExtensionMethods.DrawString(sb, font, upperText, textPos, idleText, 0f, Vector2.Zero, drawScale, SpriteEffects.None, 0f);
			}
		}
	}
}
