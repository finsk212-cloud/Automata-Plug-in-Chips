using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace Augments
{
	// The Watch Your Step plugin's Pod upgrade ("Pod 077"). It is a behaviour of the Pod on the owner's shoulder. Everything here is local to
	// the owner: the world's tiles are already on their machine and the warning is only drawn on their own screen, so nothing is sent over
	// the network. While a trap is within range the Pod flashes red and a small red chip above it points the way.
	internal sealed class PodTrapSense
	{
		private const int RangeTiles = 15;          // for most traps
		private const int ExplosiveRangeTiles = 25;  // explosives can be seen from further away
		private const int BoulderRangeTiles = 30;    // and so can boulders, which are the biggest danger
		private const int MaxRangeTiles = BoulderRangeTiles;
		private const int ScanIntervalTicks = 15;
		private const int BeepCooldownTicks = 300; // the warning sound at most every 5 seconds

		private int clock;
		private int lastBeepClock = -100000;
		private bool found;
		private bool wasFound;
		private Point tile;
		private int trapType;
		private Vector2 delta;                 // in tiles, from the player to the trap
		private float distanceTiles;
		private float fade;                    // 0..1, how visible the warning is

		// 0..1: how strongly the warning is showing right now, for the Pod's red flash.
		internal float Strength => fade;

		internal void Reset()
		{
			found = false;
			wasFound = false;
			fade = 0f;
			clock = 0;
		}

		// Once per game tick, on the owner's own machine, while they have the Watch Your Step plugin.
		internal void Update(Player owner)
		{
			if (clock++ % ScanIntervalTicks == 0)
				Scan(owner);
			else if (found)
				Track(owner);

			fade = found ? Math.Min(1f, fade + 0.12f) : Math.Max(0f, fade - 0.08f);
		}

		// How far away this tile is sensed, in tiles, or 0 if it is not a trap (or something that sets one off).
		private static int RangeFor(int type)
		{
			if (type == TileID.Boulder)
				return BoulderRangeTiles;
			if (type == TileID.Explosives || type == TileID.LandMine)
				return ExplosiveRangeTiles;

			bool trap = type == TileID.Traps
				|| type == TileID.PressurePlates
				|| type == TileID.GeyserTrap
				|| type == TileID.Spikes
				|| type == TileID.WoodenSpikes;
			return trap ? RangeTiles : 0;
		}

		private static string NameFor(int type)
		{
			if (type == TileID.Boulder)
				return "Boulder";
			if (type == TileID.LandMine)
				return "Landmine";
			return type == TileID.Explosives ? "Explosives" : "Trap";
		}

		private void Scan(Player owner)
		{
			Point centre = owner.Center.ToTileCoordinates();
			int best = int.MaxValue;
			Point bestTile = default;
			bool any = false;
			int bestType = 0;

			for (int dx = -MaxRangeTiles; dx <= MaxRangeTiles; dx++)
			{
				for (int dy = -MaxRangeTiles; dy <= MaxRangeTiles; dy++)
				{
					int d2 = dx * dx + dy * dy;
					if (d2 >= best || d2 > MaxRangeTiles * MaxRangeTiles)
						continue;

					int x = centre.X + dx;
					int y = centre.Y + dy;
					if (!WorldGen.InWorld(x, y, 2))
						continue;

					Tile t = Main.tile[x, y];
					if (!t.HasTile)
						continue;
					int range = RangeFor(t.TileType);
					if (range == 0 || d2 > range * range)
						continue;

					best = d2;
					bestTile = new Point(x, y);
					bestType = t.TileType;
					any = true;
				}
			}

			found = any;
			if (!any)
			{
				wasFound = false;
				return;
			}

			tile = bestTile;
			trapType = bestType;
			Track(owner);

			// A short beep when a trap first comes into range, not while you stay near it.
			if (!wasFound && clock - lastBeepClock >= BeepCooldownTicks)
			{
				lastBeepClock = clock;
				SoundEngine.PlaySound(SoundID.Item35 with { Volume = 0.3f, Pitch = -0.4f, MaxInstances = 2 }, owner.Center);
			}
			wasFound = true;
		}

		private void Track(Player owner)
		{
			Point centre = owner.Center.ToTileCoordinates();
			delta = new Vector2(tile.X - centre.X, tile.Y - centre.Y);
			distanceTiles = delta.Length();
		}

		// --- Drawing (client only), in the units the HUD draws in ---

		private static readonly Color Red = new Color(255, 84, 72);
		private static readonly Color Ink = new Color(52, 8, 6);

		internal static void Draw(SpriteBatch sb)
		{
			Player p = Main.LocalPlayer;
			if (p == null || !p.active || p.dead || Main.gameMenu || Main.mapFullscreen)
				return;

			PodPetPlayer pet = p.GetModPlayer<PodPetPlayer>();
			PodTrapSense s = pet.TrapSense;
			if (!pet.TrapOn || s.fade <= 0.01f || AugmentAdvisoryHUD.IsTalking)
				return;

			// Above the Pod (the Ore Sniffer writes under it), or above the player while the Pod is hidden.
			Vector2 anchor;
			bool podShown = PodPetPlayer.TryGetLocalPodScreenPos(out anchor);
			if (!podShown)
				anchor = Vector2.Transform(p.Center - Main.screenPosition + new Vector2(0f, -56f), Main.GameViewMatrix.ZoomMatrix) / Main.UIScale;

			string where = PodOreSniffer.Where(s.delta);
			string name = NameFor(s.trapType);
			string text = s.distanceTiles <= 3f ? name + " right here!" : name + " nearby" + (where.Length > 0 ? ", " + where : "");

			// A red chip with a round "!" badge, a small arrow toward the trap, and a soft red glow that pulses with the Pod's light. A thin
			// line joins it to the Pod, so it reads as the Pod's own warning.
			float pulse = 0.5f + 0.5f * (float)Math.Sin(pet.AnimClock * 0.25f);
			float a = s.fade;
			const float textScale = 0.6f;
			const float badge = 16f;
			const float arrowSize = 9f;
			float textW = HubDraw.Width(text, textScale, 0.5f);
			int boxH = Math.Max(22, (int)Math.Round(HubDraw.LineHeight(textScale) * 0.5f) + 12);
			int boxW = 3 + (int)badge + 7 + (int)Math.Round(textW) + 7 + (int)arrowSize + 8;
			int boxY = (int)Math.Round(anchor.Y - 20f - boxH);
			var chip = new Rectangle((int)Math.Round(anchor.X - boxW / 2f), boxY, boxW, boxH);

			// The line down to the Pod, then the glow, then the chip itself.
			if (podShown)
				HubDraw.Fill(sb, new Rectangle((int)Math.Round(anchor.X), chip.Bottom, 1, Math.Max(2, (int)Math.Round(anchor.Y - 9f) - chip.Bottom)), Red * (0.8f * a));
			for (int i = 3; i >= 1; i--)
				HubDraw.Fill(sb, new Rectangle(chip.X - i * 2, chip.Y - i * 2, chip.Width + i * 4, chip.Height + i * 4), Red * (a * (0.05f + 0.07f * pulse) * (4 - i) / 3f));
			HubDraw.Fill(sb, new Rectangle(chip.X - 1, chip.Y - 1, chip.Width + 2, chip.Height + 2), Color.Black * (0.9f * a));
			HubDraw.Fill(sb, chip, Red * a);

			// The round badge with its "!".
			Vector2 bc = new Vector2(chip.X + 3f + badge / 2f, chip.Y + boxH / 2f);
			Span<Vector2> disc = stackalloc Vector2[16];
			for (int i = 0; i < disc.Length; i++)
			{
				float ang = MathHelper.TwoPi * i / disc.Length;
				disc[i] = bc + new Vector2((float)Math.Cos(ang), (float)Math.Sin(ang)) * (badge / 2f);
			}
			HubDraw.FillPolygon(sb, disc, Ink * a);
			float bangW = HubDraw.Width("!", textScale, 0.5f);
			HubDraw.Text(sb, "!", new Vector2(bc.X - bangW / 2f, bc.Y - HubDraw.CapMid(textScale)), new Color(255, 155, 147) * a, textScale, 0.5f);

			// The words, then a small arrow pointing toward the trap.
			float tx = chip.X + 3f + badge + 7f;
			HubDraw.Text(sb, text, new Vector2(tx, bc.Y - HubDraw.CapMid(textScale)), Ink * a, textScale, 0.5f);

			Vector2 d = s.delta.LengthSquared() > 0.01f ? Vector2.Normalize(s.delta) : new Vector2(0f, 1f);
			Vector2 perp = new Vector2(-d.Y, d.X);
			Vector2 ac = new Vector2(tx + textW + 7f + arrowSize / 2f, bc.Y);
			Span<Vector2> arrow = stackalloc Vector2[] { ac + d * (arrowSize / 2f), ac - d * (arrowSize / 2f) + perp * (arrowSize / 2.4f), ac - d * (arrowSize / 2f) - perp * (arrowSize / 2.4f) };
			HubDraw.FillPolygon(sb, arrow, Ink * a);
		}

	}
}