using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace Augments
{
	// The panel a Fallen Unit opens: its 1 to 3 plugins, each card buried under old dust. Hold the left mouse button and
	// wipe; once a card is about 70% clean the rest of the dust falls away and the card can be picked. The rarity colour
	// peeks out along the top of the dust as a hint. Closing the panel keeps the same cards for next time (the server
	// remembers them), and how much dust you wiped off each card is kept on your character (FallenUnitPlayer.DustMemory).
	public sealed class FallenUnitUISystem : ModSystem
	{
		private static UserInterface ui;
		private static FallenUnitPanelState state;
		private static GameTime lastTime;

		public static bool IsOpen => ui?.CurrentState != null;

		public override void Load()
		{
			if (!Main.dedServ)
				ui = new UserInterface();
		}

		public override void Unload()
		{
			ui = null;
			state = null;
		}

		public static void Open(FallenUnit unit, List<string> ids)
		{
			if (ui == null || unit == null)
				return;

			var augments = new List<Augment>();
			foreach (string id in ids)
			{
				Augment augment = AugmentDatabase.GetById(id);
				if (augment != null)
					augments.Add(augment);
			}
			if (augments.Count == 0)
				return;

			state = new FallenUnitPanelState(unit, augments);
			ui.SetState(state);
			SoundEngine.PlaySound(SoundID.MenuOpen);
		}

		public static void Close()
		{
			state?.RememberDust();
			ui?.SetState(null);
			state = null;
		}

		public override void UpdateUI(GameTime gameTime)
		{
			lastTime = gameTime;
			if (ui?.CurrentState == null)
				return;

			// Walking away, dying or leaving the world closes it.
			Player me = Main.LocalPlayer;
			if (me == null || !me.active || me.dead || state == null
				|| Vector2.Distance(me.Center, state.Unit.Center) > FallenUnitSystem.RecoverRange + 200f
				|| state.Unit.IsRecoveredBy(me))
			{
				Close();
				return;
			}

			ui.Update(gameTime);
		}

		public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
		{
			int index = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Mouse Text"));
			if (index == -1)
				return;

			layers.Insert(index, new LegacyGameInterfaceLayer(
				"Augments: Fallen Unit",
				delegate
				{
					if (lastTime != null && ui?.CurrentState != null)
					{
						ui.Draw(Main.spriteBatch, lastTime);
						AugmentChoiceCard.DrawActiveTooltip(Main.spriteBatch);
						Main.LocalPlayer.mouseInterface = true;
					}
					return true;
				},
				InterfaceScaleType.UI));
		}
	}

	internal sealed class FallenUnitPanelState : UIState
	{
		public readonly FallenUnit Unit;

		private const float CardWidth = 268f;
		private const float CardHeight = AugmentChoiceCard.MinCardHeight;
		private const float Gap = 16f;
		private const float Pad = 20f;
		private const float HeaderHeight = 58f;
		private const float FooterHeight = 46f;

		private static readonly SoundStyle InstallSound = new SoundStyle("Augments/Sounds/ChipInstall") { Volume = 0.9f, PitchVariance = 0.05f };

		private readonly PanelFrame frame;
		private readonly List<DustCover> covers = new List<DustCover>();
		private readonly List<DustSpeck> specks = new List<DustSpeck>();
		private bool picked;

		public FallenUnitPanelState(FallenUnit unit, List<Augment> augments)
		{
			Unit = unit;
			int n = augments.Count;
			float width = Pad * 2f + n * CardWidth + (n - 1) * Gap;
			float height = HeaderHeight + CardHeight + FooterHeight;

			frame = new PanelFrame(this);
			frame.Width.Set(width, 0f);
			frame.Height.Set(height, 0f);
			frame.HAlign = 0.5f;
			frame.VAlign = 0.5f;
			Append(frame);

			for (int i = 0; i < n; i++)
			{
				float x = Pad + i * (CardWidth + Gap);

				var card = new AugmentChoiceCard(augments[i], CardWidth, CardHeight, i);
				card.Left.Set(x, 0f);
				card.Top.Set(HeaderHeight, 0f);
				card.OnAugmentChosen += Choose;
				frame.Append(card);

				// Appended after its card, so it sits on top and catches the mouse until it is wiped clean.
				// A card you already wiped clean last time stays clean.
				string key = FallenUnitPlayer.DustKey(unit, augments[i].Id);
				byte[] saved = FallenUnitPlayer.Local?.RecallDust(key);
				if (saved != null && DustCover.IsCleanEnough(saved))
					continue;

				var cover = new DustCover(this, augments[i].Rarity, i, key, saved);
				cover.Left.Set(x, 0f);
				cover.Top.Set(HeaderHeight, 0f);
				cover.Width.Set(CardWidth, 0f);
				cover.Height.Set(CardHeight, 0f);
				frame.Append(cover);
				covers.Add(cover);
			}

			var leave = new LeaveButton();
			leave.Width.Set(180f, 0f);
			leave.Height.Set(28f, 0f);
			leave.HAlign = 0.5f;
			leave.Top.Set(height - FooterHeight + 10f, 0f);
			leave.OnLeftClick += (_, _) =>
			{
				SoundEngine.PlaySound(SoundID.MenuClose);
				FallenUnitUISystem.Close();
			};
			frame.Append(leave);
		}

		public bool AnyDusty
		{
			get
			{
				foreach (DustCover cover in covers)
					if (!cover.Popped)
						return true;
				return false;
			}
		}

		// Writes every card's dust into the character's memory, so the next visit starts where you left off.
		internal void RememberDust()
		{
			if (picked)
				return;
			foreach (DustCover cover in covers)
				cover.Remember();
		}

		private void Choose(Augment augment)
		{
			if (picked)
				return;
			picked = true;
			FallenUnitPlayer.Local?.ForgetUnit(Unit);
			SoundEngine.PlaySound(InstallSound);
			FallenUnitSystem.RequestPick(Main.LocalPlayer, Unit, augment.Id);
			FallenUnitUISystem.Close();
		}

		internal void AddSpeck(Vector2 at, Color color)
		{
			if (specks.Count > 220)
				return;
			specks.Add(new DustSpeck
			{
				Pos = at,
				Vel = new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), Main.rand.NextFloat(-0.8f, 0.3f)),
				Life = Main.rand.Next(28, 55),
				Size = Main.rand.Next(2, 4),
				Color = color
			});
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);

			// Covers that finished fading are taken away here, not while the frame is still walking its children.
			for (int i = covers.Count - 1; i >= 0; i--)
			{
				if (covers[i].Gone)
				{
					frame.RemoveChild(covers[i]);
					covers.RemoveAt(i);
				}
			}

			for (int i = specks.Count - 1; i >= 0; i--)
			{
				DustSpeck s = specks[i];
				s.Vel.Y += 0.12f;
				s.Pos += s.Vel;
				s.Life--;
				if (s.Life <= 0)
					specks.RemoveAt(i);
				else
					specks[i] = s;
			}
		}

		public override void Draw(SpriteBatch spriteBatch)
		{
			base.Draw(spriteBatch);

			Texture2D px = TextureAssets.MagicPixel.Value;
			foreach (DustSpeck s in specks)
			{
				float a = Math.Min(1f, s.Life / 20f);
				spriteBatch.Draw(px, new Rectangle((int)s.Pos.X, (int)s.Pos.Y, s.Size, s.Size), s.Color * a);
			}
		}

		private struct DustSpeck
		{
			public Vector2 Pos, Vel;
			public int Life, Size;
			public Color Color;
		}

		// The dark window behind the cards, with the unit's name.
		private sealed class PanelFrame : UIElement
		{
			private readonly FallenUnitPanelState owner;

			public PanelFrame(FallenUnitPanelState owner) => this.owner = owner;

			protected override void DrawSelf(SpriteBatch sb)
			{
				CalculatedStyle d = GetDimensions();
				var rect = new Rectangle((int)d.X, (int)d.Y, (int)d.Width, (int)d.Height);

				HubDraw.Fill(sb, new Rectangle(rect.X - 2, rect.Y - 2, rect.Width + 4, rect.Height + 4), new Color(0, 0, 0, 150));
				HubDraw.Fill(sb, rect, new Color(20, 19, 16, 244));
				HubDraw.Border(sb, rect, new Color(222, 214, 186), 1);
				HubDraw.Brackets(sb, rect, new Color(222, 214, 186), 16, 2);

				Color beige = new Color(222, 214, 186);
				Color dim = new Color(160, 152, 128);
				HubDraw.Text(sb, "UNIT " + owner.Unit.Name.ToUpperInvariant() + "  //  RECOVERED DATA", new Vector2(rect.X + Pad, rect.Y + 12), beige, 0.8f, 1.5f);
				string hint = owner.AnyDusty
					? "Hold the left mouse button and wipe away the dust."
					: "Choose one plugin to recover.";
				HubDraw.Text(sb, hint, new Vector2(rect.X + Pad, rect.Y + 34), dim, 0.72f);
			}
		}

		private sealed class LeaveButton : UIElement
		{
			protected override void DrawSelf(SpriteBatch sb)
			{
				CalculatedStyle d = GetDimensions();
				var rect = new Rectangle((int)d.X, (int)d.Y, (int)d.Width, (int)d.Height);
				bool hot = IsMouseHovering;
				Color line = hot ? new Color(255, 190, 90) : new Color(160, 152, 128);
				HubDraw.Fill(sb, rect, hot ? new Color(48, 40, 26, 230) : new Color(28, 27, 23, 230));
				HubDraw.Border(sb, rect, line, 1);
				const string label = "Leave it for now";
				const float scale = 0.74f;
				float w = HubDraw.Width(label, scale);
				HubDraw.Text(sb, label, new Vector2(rect.X + (rect.Width - w) / 2f, rect.Y + (rect.Height - HubDraw.LineHeight(scale)) / 2f), line, scale);
			}

			public override void MouseOver(UIMouseEvent evt)
			{
				base.MouseOver(evt);
				SoundEngine.PlaySound(SoundID.MenuTick);
			}
		}
	}

	// Old dust over one card. It is a grid of small cells, each with its own amount of dust; the mouse brush lowers the
	// cells it passes over. Each cell has a fixed shade so the layer looks grainy and old.
	internal sealed class DustCover : UIElement
	{
		private const int Cell = 6;
		private const float BrushRadius = 32f;
		private const float CleanEnough = 0.70f;
		private const int FadeTicks = 18;

		private readonly FallenUnitPanelState owner;
		private readonly AugmentRarity rarity;
		private readonly int seed;
		private readonly string key;
		private byte[] saved;

		private int cols, rows;
		private float[] dust;
		private Vector2? lastMouse;
		private int soundCooldown;
		private int fade;
		private int age;

		public bool Popped { get; private set; }
		public bool Gone => Popped && fade >= FadeTicks;

		public DustCover(FallenUnitPanelState owner, AugmentRarity rarity, int index, string key, byte[] saved)
		{
			this.owner = owner;
			this.rarity = rarity;
			this.key = key;
			this.saved = saved;
			seed = Main.rand.Next(100000) + index * 7919;
		}

		// Once popped it lets the mouse through to the card underneath.
		public override bool ContainsPoint(Vector2 point) => !Popped && base.ContainsPoint(point);

		private void EnsureGrid()
		{
			if (dust != null)
				return;
			CalculatedStyle d = GetDimensions();
			cols = Math.Max(1, (int)Math.Ceiling(d.Width / Cell));
			rows = Math.Max(1, (int)Math.Ceiling(d.Height / Cell));
			dust = new float[cols * rows];
			bool restore = saved != null && saved.Length == dust.Length;
			for (int i = 0; i < dust.Length; i++)
				dust[i] = restore ? saved[i] / 255f : 1f;
			saved = null;
		}

		// The dust as bytes (0 clean, 255 untouched), kept on the character between visits.
		internal void Remember()
		{
			if (dust == null || string.IsNullOrEmpty(key))
				return;
			var bytes = new byte[dust.Length];
			for (int i = 0; i < dust.Length; i++)
				bytes[i] = Popped ? (byte)0 : (byte)Math.Round(MathHelper.Clamp(dust[i], 0f, 1f) * 255f);
			FallenUnitPlayer.Local?.StoreDust(key, bytes);
		}

		internal static bool IsCleanEnough(byte[] bytes)
		{
			if (bytes.Length == 0)
				return false;
			int clean = 0;
			foreach (byte b in bytes)
				if (b < 64)
					clean++;
			return clean / (float)bytes.Length >= CleanEnough;
		}

		private int Hash(int x, int y)
		{
			unchecked
			{
				int h = seed;
				h = h * 31 + x * 73856093;
				h = h * 31 + y * 19349663;
				h ^= h >> 13;
				h *= 1274126177;
				return h & 0x7fffffff;
			}
		}

		private Color ShadeOf(int x, int y)
		{
			int h = Hash(x, y);
			float t = (h % 1000) / 1000f;
			Color c = Color.Lerp(new Color(86, 80, 68), new Color(132, 124, 106), t);
			if (h % 9 == 0)
				c = new Color(58, 54, 46);          // dark grit
			else if (h % 13 == 0)
				c = new Color(158, 150, 130);       // pale fluff
			return c;
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			EnsureGrid();
			age++;

			if (Popped)
			{
				fade++;
				return;
			}

			if (soundCooldown > 0)
				soundCooldown--;

			CalculatedStyle d = GetDimensions();
			Vector2 mouse = Main.MouseScreen;
			bool brushing = Main.mouseLeft && base.ContainsPoint(mouse);
			if (!brushing)
			{
				lastMouse = null;
				return;
			}

			Vector2 local = mouse - new Vector2(d.X, d.Y);
			Vector2 from = lastMouse ?? local;
			lastMouse = local;

			// Brush along the whole stroke since last tick, so a fast swipe leaves no gaps.
			float length = Vector2.Distance(from, local);
			int steps = Math.Max(1, (int)(length / 5f));
			float removed = 0f;
			for (int s = 0; s <= steps; s++)
				removed += Brush(Vector2.Lerp(from, local, s / (float)steps), d);

			if (removed > 0.4f && soundCooldown <= 0)
			{
				SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.35f, Pitch = 0.45f, PitchVariance = 0.2f });
				soundCooldown = 8;
			}

			if (CleanedFraction() >= CleanEnough)
			{
				Popped = true;
				Remember();
				SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.45f, Pitch = 0.3f });
				// The rest of the dust drops off in a little puff.
				for (int i = 0; i < 40; i++)
					owner.AddSpeck(new Vector2(d.X + Main.rand.NextFloat(d.Width), d.Y + Main.rand.NextFloat(d.Height)), ShadeOf(i, age));
			}
		}

		private float Brush(Vector2 at, CalculatedStyle d)
		{
			float removed = 0f;
			int cx0 = Math.Max(0, (int)((at.X - BrushRadius) / Cell));
			int cx1 = Math.Min(cols - 1, (int)((at.X + BrushRadius) / Cell));
			int cy0 = Math.Max(0, (int)((at.Y - BrushRadius) / Cell));
			int cy1 = Math.Min(rows - 1, (int)((at.Y + BrushRadius) / Cell));
			for (int y = cy0; y <= cy1; y++)
			{
				for (int x = cx0; x <= cx1; x++)
				{
					Vector2 center = new Vector2(x * Cell + Cell / 2f, y * Cell + Cell / 2f);
					float dist = Vector2.Distance(center, at);
					if (dist > BrushRadius)
						continue;
					int i = y * cols + x;
					if (dust[i] <= 0f)
						continue;
					float take = Math.Min(dust[i], 0.12f * (1f - dist / BrushRadius) + 0.03f);
					dust[i] -= take;
					removed += take;

					// Now and then a speck of the dust falls off where you wipe.
					if (take > 0.06f && Main.rand.NextBool(14))
						owner.AddSpeck(new Vector2(d.X, d.Y) + center, ShadeOf(x, y));
				}
			}
			return removed;
		}

		private float CleanedFraction()
		{
			int clean = 0;
			foreach (float v in dust)
				if (v < 0.25f)
					clean++;
			return clean / (float)dust.Length;
		}

		protected override void DrawSelf(SpriteBatch sb)
		{
			EnsureGrid();
			CalculatedStyle d = GetDimensions();
			int left = (int)d.X, top = (int)d.Y;
			int right = (int)(d.X + d.Width), bottom = (int)(d.Y + d.Height);
			float fadeMul = Popped ? 1f - fade / (float)FadeTicks : 1f;
			if (fadeMul <= 0f)
				return;

			Texture2D px = TextureAssets.MagicPixel.Value;
			for (int y = 0; y < rows; y++)
			{
				for (int x = 0; x < cols; x++)
				{
					float a = dust[y * cols + x] * fadeMul;
					if (a <= 0.02f)
						continue;
					int x0 = left + x * Cell, y0 = top + y * Cell;
					var r = new Rectangle(x0, y0, Math.Min(Cell, right - x0), Math.Min(Cell, bottom - y0));
					// Dust stays mostly opaque until it is nearly gone, so text underneath only shows once it is wiped.
					sb.Draw(px, r, ShadeOf(x, y) * Math.Min(1f, a * 1.25f));
				}
			}

			// The rarity colour peeks out along the top edge, pulsing gently, as a hint of what is underneath.
			Color tier = RarityLook.Of(rarity);
			float pulse = 0.35f + 0.2f * (float)Math.Sin(age * 0.06f + seed);
			for (int x = 0; x < cols; x++)
			{
				float a = dust[x] * fadeMul;
				if (a <= 0.02f)
					continue;
				int x0 = left + x * Cell;
				sb.Draw(px, new Rectangle(x0, top, Math.Min(Cell, right - x0), 3), tier * (pulse * a));
			}
		}
	}
}
