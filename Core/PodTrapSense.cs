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
			if (type == TileID.Explosives)
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
			if (!PodPetPlayer.TryGetLocalPodScreenPos(out anchor))
				anchor = Vector2.Transform(p.Center - Main.screenPosition + new Vector2(0f, -56f), Main.GameViewMatrix.ZoomMatrix) / Main.UIScale;

			// The words flash a little with the Pod's light.
			float flash = 0.82f + 0.18f * (float)Math.Sin(pet.AnimClock * 0.25f);
			string where = PodOreSniffer.Where(s.delta);
			string name = NameFor(s.trapType);
			string text = s.distanceTiles <= 3f ? name + " right here!" : name + " nearby" + (where.Length > 0 ? ", " + where : "");
			PodHintChip.Draw(sb, anchor.X, anchor.Y - 44f, text, s.delta, Red, Ink, s.fade * flash);
		}
	}
}