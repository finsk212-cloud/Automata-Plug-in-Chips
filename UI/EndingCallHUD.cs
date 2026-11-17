using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace Augments
{
	// The "Commander is calling" screen shown when an ending is earned: a small incoming call bar, then a panel that glitches open at the
	// bottom middle of the screen, types out the Commander's words, and glitches away again.
	//
	// There is no render target here. The glitch is made from plain rectangles: the panel's background is drawn in horizontal bands that
	// slide sideways, the title is drawn again in two off-colour copies (the colour split), a few bars of colour flash across it, and
	// thin scanlines lie on top. All of it is re-rolled every other game tick, so it flickers instead of smearing.
	public static class EndingCallHUD
	{
		// A real win waits this long before the call comes in, so it does not land on top of the boss reward cards and the fight's loot.
		public const float DefaultDelay = 30f;

		private const float CallTime = 1.5f;      // the small "incoming" bar
		private const float OpenTime = 0.6f;      // the panel glitching open
		private const float HoldTail = 4.5f;      // how long the panel stays after the last letter has appeared
		private const float CloseTime = 0.55f;    // the panel glitching away
		private const float LettersPerSecond = 42f;
		private const float TypeDelay = 0.2f;     // a short pause after the panel opens before the first letter

		private struct Entry
		{
			public int Index;
			public int Mask;
		}

		private static readonly Queue<Entry> waiting = new Queue<Entry>();
		private static bool active;
		private static Entry current;
		private static float t;
		private static int beeps;
		private static int chimes;
		private static bool openedSound;
		private static bool closedSound;
		private static int lastTickedAt;
		private static bool loggedSize;
		private static float pendingDelay;

		// Called when an ending is earned. Several at once (the /end all command) are shown one after another.
		public static void Show(int index, int earnedMaskAfter, float delaySeconds)
		{
			if (Main.dedServ || index < 0 || index >= Endings.All.Length)
				return;

			// The wait counts down once for the whole group: endings earned together follow each other without another wait.
			if (!active && waiting.Count == 0)
				pendingDelay = delaySeconds;

			waiting.Enqueue(new Entry { Index = index, Mask = earnedMaskAfter });
		}

		// Forgets any call that is waiting or playing (a new world was entered).
		public static void Clear()
		{
			waiting.Clear();
			active = false;
			pendingDelay = 0f;
		}

		private static float TypeTime(Endings.Def def) => def.Call.Length / LettersPerSecond;

		private static float Total(Endings.Def def) => CallTime + OpenTime + TypeDelay + TypeTime(def) + HoldTail + CloseTime;

		public static void Update(GameTime gameTime)
		{
			if (Main.dedServ || Main.gameMenu)
				return;

			float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

			if (!active)
			{
				if (waiting.Count == 0)
					return;

				// A real win waits before its call comes in (see DefaultDelay); /end previews have no wait.
				if (pendingDelay > 0f)
				{
					pendingDelay -= dt;
					return;
				}

				current = waiting.Dequeue();
				active = true;
				t = 0f;
				beeps = 0;
				chimes = 0;
				openedSound = false;
				closedSound = false;
				lastTickedAt = 0;
			}

			t += dt;
			Endings.Def def = Endings.All[current.Index];

			// Two soft call beeps while the incoming bar is up.
			float[] beepAt = { 0.05f, 0.55f, 1.05f };
			while (beeps < beepAt.Length && t >= beepAt[beeps])
			{
				SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = 0.55f, Volume = 0.7f });
				beeps++;
			}

			if (!openedSound && t >= CallTime)
			{
				openedSound = true;
				SoundEngine.PlaySound(SoundID.MenuOpen with { Pitch = -0.35f, Volume = 0.8f });
				if (!Gold) // ending E plays its own chime below instead
					SoundEngine.PlaySound(SoundID.Item35 with { Volume = 0.7f });
			}

			// Ending E only: a bright three-note chime climbing up just after the panel opens (the game's own bell sound at three pitches).
			float[] chimeAt = { CallTime, CallTime + 0.22f, CallTime + 0.44f };
			float[] chimePitch = { 0f, 0.35f, 0.7f };
			while (Gold && chimes < chimeAt.Length && t >= chimeAt[chimes])
			{
				SoundEngine.PlaySound(SoundID.Item35 with { Pitch = chimePitch[chimes], Volume = 0.85f, MaxInstances = 4 }); // several at once, so a note rings on under the next
				chimes++;
			}

			// A quiet tick while the letters appear.
			int shown = RevealedLetters(def);
			if (shown >= lastTickedAt + 3)
			{
				lastTickedAt = shown;
				SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = 0.7f, Volume = 0.18f });
			}

			float end = Total(def);
			if (!closedSound && t >= end - CloseTime)
			{
				closedSound = true;
				SoundEngine.PlaySound(SoundID.MenuClose with { Pitch = -0.3f, Volume = 0.7f });
			}

			if (t >= end)
				active = false;
		}

		private static int RevealedLetters(Endings.Def def)
		{
			float since = t - (CallTime + OpenTime + TypeDelay);
			return (int)MathHelper.Clamp(since * LettersPerSecond, 0f, def.Call.Length);
		}

		// A repeatable random number from 0 to 1, so the same frame always glitches the same way.
		private static float Rnd(int a, int b = 0, int c = 0)
		{
			unchecked
			{
				uint x = (uint)(a * 73856093) ^ (uint)(b * 19349663) ^ (uint)(c * 83492791);
				x ^= x >> 13;
				x *= 0x5bd1e995;
				x ^= x >> 15;
				return (x & 0xFFFF) / 65535f;
			}
		}

		// The call's colours. Ending E (the true ending) gets a gold screen instead of the usual rust-and-steel one.
		private static bool Gold => current.Index == Endings.All.Length - 1;

		private static class Pal
		{
			public static Color Panel => Gold ? new Color(34, 26, 8) : HubTheme.Panel;
			public static Color Line => Gold ? new Color(196, 152, 52) : HubTheme.Line;
			public static Color LineSoft => Gold ? new Color(110, 86, 34) : HubTheme.LineSoft;
			public static Color Accent => Gold ? new Color(240, 196, 80) : HubTheme.Accent;
			public static Color Warn => Gold ? new Color(255, 228, 150) : HubTheme.Warn;
			public static Color Text => Gold ? new Color(255, 244, 216) : HubTheme.Text;
			public static Color OnAccent => Gold ? new Color(48, 32, 4) : HubTheme.OnAccent;
			public static Color Dim => Gold ? new Color(184, 154, 92) : HubTheme.Dim;
		}
		public static void Draw(SpriteBatch sb)
		{
			if (!active || Main.dedServ || Main.gameMenu)
				return;

			Endings.Def def = Endings.All[current.Index];
			// The screen is Main.screenWidth by Main.screenHeight units, the same as the mod's other HUDs (the docked icons, the pinned cards).
			// Dividing by Main.UIScale put the panel left of centre and too high on a screen whose UI scale is above 1.
			float m = Main.UIScaleMatrix.M11;
			float uiW = Main.screenWidth;
			float uiH = Main.screenHeight;
			int frame = (int)(Main.GameUpdateCount / 2);

			if (!loggedSize)
			{
				loggedSize = true;
				ModContent.GetInstance<Augments>().Logger.Info("ENDING CALL screen=" + Main.screenWidth + "x" + Main.screenHeight + " UIScale=" + Main.UIScale
					+ " matrix=" + m + " viewport=" + Main.instance.GraphicsDevice.Viewport.Width + "x" + Main.instance.GraphicsDevice.Viewport.Height);
			}

			int w = (int)Math.Min(440f, uiW - 40f);
			const int h = 122; // grew by 14 so two lines of the larger text still clear the footer
			var full = new Rectangle((int)((uiW - w) / 2f), (int)(uiH - h - 30f), w, h);

			if (t < CallTime)
			{
				DrawCall(sb, full, frame);
				return;
			}

			float end = Total(def);
			float open = MathHelper.Clamp((t - CallTime) / OpenTime, 0f, 1f);
			float close = MathHelper.Clamp((t - (end - CloseTime)) / CloseTime, 0f, 1f);

			float glitch = 1f - open * 0.85f;
			if (open >= 1f && close <= 0f)
			{
				// Now and then a short burst while the panel is just sitting there.
				float scaled = t * 2.2f;
				int bucket = (int)scaled;
				glitch = (Rnd(bucket, current.Index, 7) < 0.2f && scaled - bucket < 0.3f) ? 0.5f : 0f;
			}
			if (close > 0f)
				glitch = 0.3f + 0.7f * close;

			float alpha = 1f;
			if (glitch > 0f && Rnd(frame, 11) < glitch * 0.45f)
				alpha = 0.35f;
			if (close > 0f)
				alpha *= 1f - close * close;

			// The panel turns on like a screen: a thin line that grows into the full panel, and shrinks back when it leaves.
			float grow = open < 1f ? MathHelper.Clamp(open * 1.35f + (Rnd(frame, 3) - 0.5f) * 0.4f * (1f - open), 0.04f, 1f) : 1f;
			if (close > 0f)
				grow = MathHelper.Lerp(1f, 0.06f, close * close);

			int panelH = Math.Max(4, (int)(h * grow));
			var rect = new Rectangle(full.X, full.Bottom - panelH, full.Width, panelH);

			DrawGlitchBackground(sb, rect, Pal.Panel, Pal.Line, glitch, frame, alpha);

			if (grow >= 0.92f)
			{
				// Colour split: the title and letter are drawn twice more in off colours, pushed apart.
				if (glitch > 0.25f)
				{
					int split = (int)Math.Round(2f + glitch * 5f);
					DrawHeadline(sb, rect, def, new Color(172, 78, 48), alpha * 0.55f, new Point(-split, 0));
					DrawHeadline(sb, rect, def, new Color(70, 150, 150), alpha * 0.55f, new Point(split, 0));
				}

				int jitter = (glitch > 0.3f && Rnd(frame, 5) < glitch) ? (int)((Rnd(frame, 6) - 0.5f) * 14f * glitch) : 0;
				DrawHeader(sb, rect, def, alpha, jitter);
				DrawHeadline(sb, rect, def, null, alpha, new Point(jitter, 0));
				DrawBody(sb, rect, def, alpha, jitter);
			}

			DrawGlitchOverlay(sb, rect, glitch, frame, alpha);
			if (Gold && grow >= 0.92f)
				DrawGoldShimmer(sb, rect, alpha);
		}

		// Ending E only: a soft band of light sweeps across the gold panel, and its edge breathes.
		private static void DrawGoldShimmer(SpriteBatch sb, Rectangle r, float alpha)
		{
			float sweep = (t * 0.45f) % 2f - 0.5f;
			int bandW = 46;
			int bx = r.X + (int)(sweep * r.Width);
			for (int i = 0; i < bandW; i += 2)
			{
				float edge = 1f - Math.Abs(i - bandW / 2f) / (bandW / 2f);
				int x0 = Math.Max(bx + i, r.X + 2);
				int x1 = Math.Min(bx + i + 2, r.Right - 2);
				if (x1 > x0)
					HubDraw.Fill(sb, new Rectangle(x0, r.Y + 18, x1 - x0, r.Height - 20), new Color(255, 236, 170) * (0.10f * edge * alpha));
			}

			float breath = 0.5f + 0.5f * (float)Math.Sin(t * 3f);
			HubDraw.Border(sb, new Rectangle(r.X - 1, r.Y - 1, r.Width + 2, r.Height + 2), new Color(255, 214, 100) * ((0.25f + 0.3f * breath) * alpha));
		}

		// The small "incoming" bar before the panel.
		private static void DrawCall(SpriteBatch sb, Rectangle full, int frame)
		{
			float slide = MathHelper.Clamp(t / 0.25f, 0f, 1f);
			int w = (int)(240f * slide);
			const int h = 26;
			var bar = new Rectangle(full.Center.X - w / 2, full.Bottom - h, w, h);
			float glitch = t < 0.3f ? 0.9f - t * 2f : (Rnd((int)(t * 6f), 21) < 0.12f ? 0.5f : 0f);
			float alpha = (glitch > 0f && Rnd(frame, 12) < glitch * 0.4f) ? 0.4f : 1f;

			DrawGlitchBackground(sb, bar, Pal.Panel, Pal.Line, glitch, frame, alpha);

			if (slide >= 1f)
			{
				// Signal bars that rise and fall, then the text, blinking.
				for (int i = 0; i < 4; i++)
				{
					float level = 0.3f + 0.7f * (0.5f + 0.5f * (float)Math.Sin(t * 9f + i * 1.3f));
					int bh = (int)((4 + i * 3) * level) + 2;
					HubDraw.Fill(sb, new Rectangle(bar.X + 10 + i * 6, bar.Bottom - 6 - bh, 4, bh), Pal.Accent * alpha);
				}

				bool lit = ((int)(t * 4f)) % 2 == 0;
				HubDraw.Text(sb, "INCOMING TRANSMISSION", new Vector2(bar.X + 40, bar.Y + 6), Pal.Text * alpha, 0.54f, 2f);
				HubDraw.TextRight(sb, "CMDR", bar.Right - 10, bar.Y + 6, (lit ? Pal.Warn : Pal.Dim) * alpha, 0.54f, 2f);
			}

			DrawGlitchOverlay(sb, bar, glitch, frame, alpha);
		}

		// The panel background drawn in bands that slide sideways while it glitches.
		private static void DrawGlitchBackground(SpriteBatch sb, Rectangle r, Color fill, Color line, float glitch, int frame, float alpha)
		{
			const int band = 6;
			int bands = (r.Height + band - 1) / band;
			int firstDx = 0;
			int lastDx = 0;

			for (int b = 0; b < bands; b++)
			{
				int y = r.Y + b * band;
				int bh = Math.Min(band, r.Bottom - y);
				int dx = 0;
				if (glitch > 0f && Rnd(frame, b, 1) < glitch * 0.5f)
					dx = (int)((Rnd(frame, b, 2) - 0.5f) * 70f * glitch);

				if (b == 0)
					firstDx = dx;
				if (b == bands - 1)
					lastDx = dx;

				HubDraw.Fill(sb, new Rectangle(r.X + dx, y, r.Width, bh), fill * alpha);
				HubDraw.Fill(sb, new Rectangle(r.X + dx, y, 2, bh), line * alpha);
				HubDraw.Fill(sb, new Rectangle(r.Right - 2 + dx, y, 2, bh), line * alpha);
			}

			HubDraw.Fill(sb, new Rectangle(r.X + firstDx, r.Y, r.Width, 2), line * alpha);
			HubDraw.Fill(sb, new Rectangle(r.X + lastDx, r.Bottom - 2, r.Width, 2), line * alpha);

			if (r.Height > 40)
				HubDraw.Corners(sb, new Rectangle(r.X - 3, r.Y - 3, r.Width + 6, r.Height + 6), Pal.Accent * alpha, 12, 2);
		}

		// Flashing colour bars across the panel, then fine scanlines on top.
		private static void DrawGlitchOverlay(SpriteBatch sb, Rectangle r, float glitch, int frame, float alpha)
		{
			if (glitch > 0.2f)
			{
				for (int i = 0; i < 4; i++)
				{
					if (Rnd(frame, i, 9) >= glitch)
						continue;

					int bw = 30 + (int)(Rnd(frame, i, 10) * 170f);
					int bh = 2 + (int)(Rnd(frame, i, 13) * 7f);
					int bx = r.X + (int)(Rnd(frame, i, 14) * Math.Max(1, r.Width - bw));
					int by = r.Y + (int)(Rnd(frame, i, 15) * Math.Max(1, r.Height - bh));
					Color c = Rnd(frame, i, 16) > 0.5f ? Pal.Warn : Pal.Accent;
					HubDraw.Fill(sb, new Rectangle(bx, by, bw, bh), c * (0.6f * alpha));
				}
			}

			for (int y = r.Y + 1; y < r.Bottom; y += 3)
				HubDraw.Fill(sb, new Rectangle(r.X + 2, y, Math.Max(0, r.Width - 4), 1), Color.Black * (0.07f * alpha));
		}

		private static void DrawHeader(SpriteBatch sb, Rectangle r, Endings.Def def, float alpha, int dx)
		{
			var bar = new Rectangle(r.X + 2 + dx, r.Y + 2, r.Width - 4, 16);
			HubDraw.Fill(sb, bar, Pal.Accent * alpha);
			HubDraw.Text(sb, "INCOMING TRANSMISSION", new Vector2(bar.X + 8, bar.Y + 2), Pal.OnAccent * alpha, 0.5f, 2f);

			int count = 0;
			for (int i = 0; i < Endings.All.Length; i++)
			{
				if (Endings.Has(current.Mask, i))
					count++;
			}

			HubDraw.TextRight(sb, "ENDING " + count + " OF " + Endings.All.Length, bar.Right - 8, bar.Y + 2, Pal.OnAccent * alpha, 0.5f, 2f);
		}

		// The big letter tile, the CHALLENGE COMPLETE line and the name. Drawn on its own so the colour split can repeat it.
		private static void DrawHeadline(SpriteBatch sb, Rectangle r, Endings.Def def, Color? tint, float alpha, Point off)
		{
			int x = r.X + 14 + off.X;
			int y = r.Y + 25 + off.Y;

			var tile = new Rectangle(x, y, 48, 48);
			HubDraw.Fill(sb, tile, (tint ?? Pal.Accent) * alpha);

			string letter = def.Letter.ToString();
			float letterScale = HubDraw.BigScale(28f);
			float lw = HubDraw.BigWidth(letter, letterScale);
			HubDraw.BigText(sb, letter, new Vector2(tile.Center.X - lw / 2f, tile.Y + 14), (tint.HasValue ? Color.White : Pal.OnAccent) * alpha, letterScale);

			int tx = tile.Right + 12;
			HubDraw.Text(sb, "CHALLENGE COMPLETE", new Vector2(tx, y - 1), (tint ?? Pal.Warn) * alpha, 0.46f, 2f);
			HubDraw.BigText(sb, def.Name, new Vector2(tx, y + 11), (tint ?? Pal.Text) * alpha, HubDraw.BigScale(20f), 2f);
		}

		// The Commander's words (typed out) and the row of five ending squares.
		private static void DrawBody(SpriteBatch sb, Rectangle r, Endings.Def def, float alpha, int dx)
		{
			int tx = r.X + 14 + 48 + 12 + dx;
			int maxW = r.Right - 14 - tx;
			float scale = 0.62f; // the Commander's words: about 3 pixels taller than the first version (0.5)
			float lineStep = HubDraw.LineHeight(scale) + 2f;

			HubDraw.HLine(sb, tx, r.Y + 52, maxW, Pal.LineSoft * alpha);

			List<string> lines = HubDraw.Wrap(def.Call, maxW, scale);
			int remaining = RevealedLetters(def);
			float ly = r.Y + 57;
			foreach (string line in lines)
			{
				if (remaining <= 0)
					break;

				string shown = remaining >= line.Length ? line : line.Substring(0, remaining);
				remaining -= line.Length + 1;
				HubDraw.Text(sb, shown, new Vector2(tx, ly), Pal.Text * alpha, scale);
				ly += lineStep;
			}

			// A blinking cursor while the line is still being typed.
			if (RevealedLetters(def) < def.Call.Length && ((int)(t * 6f)) % 2 == 0)
				HubDraw.Fill(sb, new Rectangle(tx + maxW - 5, r.Y + 57, 5, 7), Pal.Accent * alpha);

			// The five endings: earned ones are filled, this one is rust coloured.
			int px = r.X + 14 + dx;
			int py = r.Bottom - 17;
			HubDraw.Text(sb, "ENDINGS", new Vector2(px, py), Pal.Dim * alpha, 0.44f, 2f);
			int sx = px + 52;
			for (int i = 0; i < Endings.All.Length; i++)
			{
				var sq = new Rectangle(sx + i * 13, py + 1, 8, 8);
				if (i == current.Index)
					HubDraw.Fill(sb, sq, Pal.Warn * alpha);
				else if (Endings.Has(current.Mask, i))
					HubDraw.Fill(sb, sq, Pal.Accent * alpha);
				else
					HubDraw.Border(sb, sq, Pal.Line * alpha);
			}

			HubDraw.TextRight(sb, "- COMMANDER", r.Right - 14, py, Pal.Dim * alpha, 0.44f, 2f);
		}
	}
}
