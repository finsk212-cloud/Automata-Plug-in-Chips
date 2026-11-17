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
	// The Ore Sniffer plugin's Pod upgrade ("Pod 311"). It is not a separate drone: it is a behaviour of the Pod on the owner's shoulder.
	// Everything here is local to the owner: the world's tiles are already on their machine, and the hint is only drawn on their own screen,
	// so nothing is sent over the network.
	//
	// It is an active plugin: it does nothing until the owner presses its button (see Augments.SniffOreKeybind). Then the Pod sniffs for
	// 2 seconds (a pulsing ring), says a riddle about the nearest ore and points an arrow at it, and cools down for 6 seconds before the
	// button works again. The riddle and arrow stay up for the first 5 seconds of the cooldown.
	internal sealed class PodOreSniffer
	{
		private const int RangeTiles = 60;
		private const int ScanTicks = 120;      // 2 seconds of sniffing
		private const int CooldownTicks = 360;  // 6 seconds before the button works again
		private const int ShowTicks = 300;      // the riddle stays up for the first 5 seconds of the cooldown

		private enum Phase { Ready, Sniffing, Cooldown }

		private Phase phase = Phase.Ready;
		private int phaseTicks;
		private bool found;
		private bool revealed;                 // the riddle (or "nothing here") is being shown this cycle
		private Point tile;
		private string riddle = "";
		private float distanceTiles;
		private Vector2 delta;                 // in tiles, from the player to the ore
		private float fade;                    // 0..1, how visible the riddle and arrow are
		private float scanFade;                // 0..1, how visible the sniffing ring is

		private static PodOreSniffer Local
		{
			get
			{
				Player p = Main.LocalPlayer;
				return p != null && p.active ? p.GetModPlayer<PodPetPlayer>().Sniffer : null;
			}
		}

		internal void Reset()
		{
			phase = Phase.Ready;
			phaseTicks = 0;
			found = false;
			revealed = false;
			fade = 0f;
			scanFade = 0f;
		}

		// Ticks until the button works again (0 when it is ready); the plugin list shows this as its cooldown.
		internal int CooldownRemaining
		{
			get
			{
				if (phase == Phase.Sniffing)
					return ScanTicks - phaseTicks + CooldownTicks;
				return phase == Phase.Cooldown ? CooldownTicks - phaseTicks : 0;
			}
		}

		// The owner pressed the button. Does nothing while it is already sniffing or cooling down.
		internal void Request()
		{
			if (phase != Phase.Ready)
				return;

			phase = Phase.Sniffing;
			phaseTicks = 0;
			found = false;
			revealed = false;
			SoundEngine.PlaySound(SoundID.Item35 with { Volume = 0.25f, Pitch = -0.2f, MaxInstances = 2 }, Main.LocalPlayer.Center);
		}

		// Once per game tick, on the owner's own machine, while they have the Ore Sniffer plugin.
		internal void Update(Player owner)
		{
			if (phase != Phase.Ready)
				phaseTicks++;

			if (phase == Phase.Sniffing)
			{
				if (phaseTicks >= ScanTicks)
				{
					Reveal(owner);
					phase = Phase.Cooldown;
					phaseTicks = 0;
				}
			}
			else if (phase == Phase.Cooldown)
			{
				if (phaseTicks >= CooldownTicks)
				{
					phase = Phase.Ready;
					phaseTicks = 0;
					found = false;
					revealed = false;
				}
				else if (found)
				{
					Track(owner); // the arrow keeps pointing at the ore while you move
				}
			}

			float wantFade = phase == Phase.Cooldown && revealed && phaseTicks < ShowTicks ? 1f : 0f;
			fade = MoveToward(fade, wantFade, 0.08f);
			scanFade = MoveToward(scanFade, phase == Phase.Sniffing ? 1f : 0f, 0.1f);
		}

		private static float MoveToward(float value, float target, float step)
		{
			return value < target ? Math.Min(target, value + step) : Math.Max(target, value - step);
		}

		// The end of the sniffing: find the nearest ore now, and say something about it.
		private void Reveal(Player owner)
		{
			Scan(owner);
			revealed = true;
			if (found)
				SoundEngine.PlaySound(SoundID.Item35 with { Volume = 0.4f, Pitch = 0.5f, MaxInstances = 2 }, owner.Center);
		}

		// Ore, but not chests, life crystals and the like. Mod ores usually set the same flags as the vanilla ones.
		private static bool IsOre(int type)
		{
			if (type < 0 || type >= TileLoader.TileCount)
				return false;
			if (Main.tileContainer[type] || type == TileID.Heart)
				return false;
			if (type == TileID.LifeFruit)
				return false;
			if (Riddles.ContainsKey(type) || TileID.Sets.Ore[type])
				return true;
			// The metal detector's list is broader than ore (it can hold pots and fruit), so only solid blocks from it count.
			return Main.tileOreFinderPriority[type] > 0 && Main.tileSolid[type];
		}

		private void Scan(Player owner)
		{
			Point centre = owner.Center.ToTileCoordinates();
			int best = int.MaxValue;
			Point bestTile = default;
			int bestType = -1;

			for (int dx = -RangeTiles; dx <= RangeTiles; dx++)
			{
				for (int dy = -RangeTiles; dy <= RangeTiles; dy++)
				{
					int d2 = dx * dx + dy * dy;
					if (d2 >= best || d2 > RangeTiles * RangeTiles)
						continue;

					int x = centre.X + dx;
					int y = centre.Y + dy;
					if (!WorldGen.InWorld(x, y, 2))
						continue;

					Tile t = Main.tile[x, y];
					if (!t.HasTile || !IsOre(t.TileType))
						continue;

					best = d2;
					bestTile = new Point(x, y);
					bestType = t.TileType;
				}
			}

			if (bestType < 0)
			{
				found = false;
				riddle = NothingRiddle;
				return;
			}

			found = true;
			tile = bestTile;
			riddle = RiddleFor(bestType);
			Track(owner);
		}

		private void Track(Player owner)
		{
			Point centre = owner.Center.ToTileCoordinates();
			delta = new Vector2(tile.X - centre.X, tile.Y - centre.Y);
			distanceTiles = delta.Length();
		}

		// The Pod never says the ore's name: it gives a riddle instead. Every vanilla ore and gem has its own; anything else (a modded ore)
		// gets the fallback. These tile types are always counted as ore, so a gem is found even if the game does not flag it as one.
		private const string FallbackRiddle = "Something unknown is hiding. Even I'm curious.";
		private const string NothingRiddle = "Nothing but dirt and doubt around here.";

		private static readonly Dictionary<int, string> Riddles = new Dictionary<int, string>
		{
			{ TileID.Copper, "Something reddish is humming nearby." },
			{ TileID.Tin, "I smell a tin can that isn't a can." },
			{ TileID.Iron, "Something sturdy and rusty is hiding." },
			{ TileID.Lead, "Something heavy is sulking in the dark." },
			{ TileID.Silver, "Something is whispering with a silver tongue." },
			{ TileID.Tungsten, "Something tough is hiding down there. Bring a good pickaxe." },
			{ TileID.Gold, "I sense something shiny nearby." },
			{ TileID.Platinum, "Something fancy is hiding. Very fancy." },
			{ TileID.Demonite, "Something purple and grumpy is lurking." },
			{ TileID.Crimtane, "Something red and a bit meaty is nearby." },
			{ TileID.Meteorite, "Something fell from the sky and it's still warm." },
			{ TileID.Hellstone, "Something is glowing hot. Watch your toes." },
			{ TileID.Cobalt, "Something blue is humming a tune." },
			{ TileID.Palladium, "Something orange is humming a tune." },
			{ TileID.Mythril, "Something green and legendary is glowing nearby." },
			{ TileID.Orichalcum, "Something pink is blooming underground." },
			{ TileID.Adamantite, "Something red and unbreakable is stirring." },
			{ TileID.Titanium, "Something silver and stubborn is waiting." },
			{ TileID.Chlorophyte, "The jungle is hiding something that grows." },
			{ TileID.LunarOre, "Something not from this world is glowing." },
			{ TileID.Amethyst, "A purple sparkle winked at me." },
			{ TileID.Topaz, "A yellow sparkle winked at me." },
			{ TileID.Sapphire, "A blue sparkle winked at me." },
			{ TileID.Emerald, "A green sparkle winked at me." },
			{ TileID.Ruby, "A red sparkle winked at me." },
			{ TileID.Diamond, "Something is sparkling like it paid for it." }
		};

		private static string RiddleFor(int type)
		{
			return Riddles.TryGetValue(type, out string r) ? r : FallbackRiddle;
		}

		// How close, in plain words, so the riddle does not need a number.
		private static string Closeness(float tiles)
		{
			if (tiles <= 4f)
				return "right here!";
			if (tiles <= 12f)
				return "very close";
			return tiles <= 30f ? "closer" : "far";
		}

		// "below and left", for a hint a person can follow without reading the arrow.
		private static string Where(Vector2 d)
		{
			string vertical = Math.Abs(d.Y) >= 6f ? (d.Y < 0f ? "above" : "below") : "";
			string horizontal = Math.Abs(d.X) >= 6f ? (d.X < 0f ? "left" : "right") : "";
			if (vertical.Length > 0 && horizontal.Length > 0)
				return vertical + " and " + horizontal;
			return vertical.Length > 0 ? vertical : horizontal;
		}

		// --- Drawing (client only), in the units the HUD draws in ---

		private static readonly Color Orange = new Color(255, 160, 70);
		private static readonly Color Dark = new Color(60, 28, 6);

		internal static void Draw(SpriteBatch sb)
		{
			PodOreSniffer s = Local;
			if (s == null || (s.fade <= 0.01f && s.scanFade <= 0.01f) || Main.gameMenu || Main.mapFullscreen)
				return;

			Player p = Main.LocalPlayer;
			PodPetPlayer pet = p.GetModPlayer<PodPetPlayer>();
			if (!pet.SnifferOn || p.dead)
				return;

			// Next to the Pod, or above the player while the Pod is hidden.
			Vector2 anchor;
			if (!PodPetPlayer.TryGetLocalPodScreenPos(out anchor))
				anchor = Vector2.Transform(p.Center - Main.screenPosition + new Vector2(0f, -56f), Main.GameViewMatrix.ZoomMatrix) / Main.UIScale;

			if (s.scanFade > 0.01f)
				s.DrawSniffing(sb, anchor);
			if (s.fade > 0.01f)
				s.DrawResult(sb, anchor);
		}

		// While it sniffs: rings spreading out from the Pod, and "Sniffing..." with dots that count up.
		private void DrawSniffing(SpriteBatch sb, Vector2 anchor)
		{
			float a = scanFade;
			for (int i = 0; i < 2; i++)
			{
				float t = ((phaseTicks / 60f) + i * 0.5f) % 1f;      // each ring takes a second to spread out
				float radius = 10f + 52f * t;
				DrawRing(sb, anchor, radius, 1.6f, Orange * (a * (1f - t) * 0.8f));
			}

			if (AugmentAdvisoryHUD.IsTalking)
				return;

			string full = "Sniffing...";
			int dots = 1 + (phaseTicks / 20) % 3;
			string shown = "Sniffing" + new string('.', dots);
			float scale = 0.62f;
			float w = HubDraw.Width(full, scale);
			DrawOutlined(sb, shown, new Vector2(anchor.X - w / 2f, anchor.Y + 40f), Orange * (a * 0.95f), scale, a);
		}

		// A thin circle, drawn as short straight pieces.
		private static void DrawRing(SpriteBatch sb, Vector2 centre, float radius, float thickness, Color c)
		{
			const int Segments = 28;
			float outer = radius + thickness * 0.5f;
			float inner = radius - thickness * 0.5f;
			for (int i = 0; i < Segments; i++)
			{
				float a0 = MathHelper.TwoPi * i / Segments;
				float a1 = MathHelper.TwoPi * (i + 1) / Segments;
				Vector2 d0 = new Vector2((float)Math.Cos(a0), (float)Math.Sin(a0));
				Vector2 d1 = new Vector2((float)Math.Cos(a1), (float)Math.Sin(a1));
				Span<Vector2> quad = stackalloc Vector2[] { centre + d0 * outer, centre + d1 * outer, centre + d1 * inner, centre + d0 * inner };
				HubDraw.FillPolygon(sb, quad, c);
			}
		}

		// After it sniffs: an arrow circling the Pod toward the ore, the riddle, and how close and which way.
		private void DrawResult(SpriteBatch sb, Vector2 anchor)
		{
			float a = fade;
			float ring = 30f;

			if (found)
			{
				Vector2 dir = delta.LengthSquared() > 0.01f ? Vector2.Normalize(delta) : new Vector2(0f, 1f);
				Vector2 perp = new Vector2(-dir.Y, dir.X);
				Vector2 tip = anchor + dir * (ring + 10f);
				Vector2 baseMid = anchor + dir * ring;
				Span<Vector2> outer = stackalloc Vector2[] { tip + dir * 1.5f, baseMid + perp * 6.5f - dir * 1.5f, baseMid - perp * 6.5f - dir * 1.5f };
				Span<Vector2> inner = stackalloc Vector2[] { tip, baseMid + perp * 5f, baseMid - perp * 5f };
				HubDraw.FillPolygon(sb, outer, Dark * (0.85f * a));
				HubDraw.FillPolygon(sb, inner, Orange * a);
			}

			// The words wait while Pod 042 is speaking a tip next to it, so the two never overlap. The arrow stays.
			if (AugmentAdvisoryHUD.IsTalking)
				return;

			string line1 = "\"" + riddle + "\"";
			string line2 = null;
			if (found)
			{
				string where = Where(delta);
				string close = Closeness(distanceTiles);
				line2 = distanceTiles <= 4f || where.Length == 0 ? close : close + ", " + where;
			}

			// The riddle in cream with a black outline, so it reads over grass, dirt and sky alike.
			float scale = 0.66f;
			float y = anchor.Y + ring + 22f;
			DrawOutlined(sb, line1, new Vector2(anchor.X - HubDraw.Width(line1, scale) / 2f, y), Cream * a, scale, a);

			if (line2 == null)
				return;

			// Under it, an orange chip with a small arrow pointing toward the ore and how close and which way.
			const float chipScale = 0.56f;
			const float arrowSize = 9f;
			float textW = HubDraw.Width(line2, chipScale, 0.5f);
			int boxH = (int)Math.Round(HubDraw.LineHeight(chipScale) * 0.5f) + 10;
			int boxW = (int)Math.Round(textW) + (int)arrowSize + 5 + 16;
			var chip = new Rectangle((int)Math.Round(anchor.X - boxW / 2f), (int)Math.Round(y + HubDraw.LineHeight(scale) + 5f), boxW, boxH);
			HubDraw.Fill(sb, new Rectangle(chip.X - 1, chip.Y - 1, chip.Width + 2, chip.Height + 2), Color.Black * (0.75f * a));
			HubDraw.Fill(sb, chip, Orange * a);

			Vector2 d = delta.LengthSquared() > 0.01f ? Vector2.Normalize(delta) : new Vector2(0f, 1f);
			Vector2 p = new Vector2(-d.Y, d.X);
			Vector2 c = new Vector2(chip.X + 8f + arrowSize / 2f, chip.Y + boxH / 2f);
			Span<Vector2> arrow = stackalloc Vector2[] { c + d * (arrowSize / 2f), c - d * (arrowSize / 2f) + p * (arrowSize / 2.4f), c - d * (arrowSize / 2f) - p * (arrowSize / 2.4f) };
			HubDraw.FillPolygon(sb, arrow, Dark * a);
			HubDraw.Text(sb, line2, new Vector2(chip.X + 8f + arrowSize + 5f, chip.Y + boxH / 2f - HubDraw.CapMid(chipScale)), Dark * a, chipScale, 0.5f);
		}

		private static readonly Color Cream = new Color(255, 243, 220);

		// Text with a one-pixel black outline all the way round it.
		private static void DrawOutlined(SpriteBatch sb, string text, Vector2 pos, Color c, float scale, float alpha)
		{
			float o = HubDraw.OnePx;
			Color black = Color.Black * (0.95f * alpha);
			for (int dx = -1; dx <= 1; dx++)
			{
				for (int dy = -1; dy <= 1; dy++)
				{
					if (dx != 0 || dy != 0)
						HubDraw.Text(sb, text, pos + new Vector2(dx * o, dy * o), black, scale);
				}
			}
			HubDraw.Text(sb, text, pos, c, scale);
		}
	}
}