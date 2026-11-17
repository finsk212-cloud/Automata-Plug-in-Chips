using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Map;
using Terraria.ModLoader;

namespace Augments
{
	// The Ore Sniffer plugin's Pod upgrade ("Pod 311"). It is not a separate drone: it is a behaviour of the Pod on the owner's shoulder.
	// Everything here is local to the owner: the world's tiles are already on their machine, and the hint is only drawn on their own screen,
	// so nothing is sent over the network.
	internal sealed class PodOreSniffer
	{
		private const int RangeTiles = 60;
		private const int ScanIntervalTicks = 30;
		private const int NearTiles = 12;      // inside this the Pod gives a little ping once

		private int clock;
		private bool found;
		private Point tile;
		private string oreName = "";
		private float distanceTiles;
		private Vector2 delta;                 // in tiles, from the player to the ore
		private float fade;                    // 0..1, how visible the hint is
		private bool wasNear;
		private int lastPingClock = -100000;
		private const int PingCooldownTicks = 600; // at least 10 seconds apart

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
			found = false;
			fade = 0f;
			clock = 0;
		}

		// Once per game tick, on the owner's own machine, while they have the Ore Sniffer plugin.
		internal void Update(Player owner)
		{
			if (clock++ % ScanIntervalTicks == 0)
				Scan(owner);

			fade = found ? Math.Min(1f, fade + 0.08f) : Math.Max(0f, fade - 0.08f);
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
			if (TileID.Sets.Ore[type])
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
				wasNear = false;
				return;
			}

			found = true;
			tile = bestTile;
			oreName = NameOf(bestType);
			delta = new Vector2(bestTile.X - centre.X, bestTile.Y - centre.Y);
			distanceTiles = (float)Math.Sqrt(best);

			// One soft ping when you come close to ore, not one for every block of a vein you walk along or mine.
			bool near = distanceTiles <= NearTiles;
			if (near && !wasNear && clock - lastPingClock >= PingCooldownTicks)
			{
				lastPingClock = clock;
				SoundEngine.PlaySound(SoundID.Item35 with { Volume = 0.35f, Pitch = 0.5f, MaxInstances = 2 }, owner.Center);
			}
			wasNear = near;
		}

		private static string NameOf(int type)
		{
			string name = Lang.GetMapObjectName(MapHelper.TileToLookup(type, 0));
			return string.IsNullOrWhiteSpace(name) ? "Ore" : name;
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

		internal static void Draw(SpriteBatch sb)
		{
			PodOreSniffer s = Local;
			if (s == null || s.fade <= 0.01f || Main.gameMenu || Main.mapFullscreen)
				return;

			Player p = Main.LocalPlayer;
			PodPetPlayer pet = p.GetModPlayer<PodPetPlayer>();
			if (!pet.SnifferOn || p.dead)
				return;

			// Next to the Pod, or above the player while the Pod is hidden.
			Vector2 anchor;
			if (!PodPetPlayer.TryGetLocalPodScreenPos(out anchor))
				anchor = Vector2.Transform(p.Center - Main.screenPosition + new Vector2(0f, -56f), Main.GameViewMatrix.ZoomMatrix) / Main.UIScale;

			float a = s.fade;
			var orange = new Color(255, 160, 70);
			var dark = new Color(60, 28, 6);

			// The arrow circles the Pod and points at the ore.
			Vector2 dir = s.delta.LengthSquared() > 0.01f ? Vector2.Normalize(s.delta) : new Vector2(0f, 1f);
			Vector2 perp = new Vector2(-dir.Y, dir.X);
			float ring = 30f;
			Vector2 tip = anchor + dir * (ring + 10f);
			Vector2 baseMid = anchor + dir * ring;
			Span<Vector2> outer = stackalloc Vector2[] { tip + dir * 1.5f, baseMid + perp * 6.5f - dir * 1.5f, baseMid - perp * 6.5f - dir * 1.5f };
			Span<Vector2> inner = stackalloc Vector2[] { tip, baseMid + perp * 5f, baseMid - perp * 5f };
			HubDraw.FillPolygon(sb, outer, dark * (0.85f * a));
			HubDraw.FillPolygon(sb, inner, orange * a);

			// The words wait while Pod 042 is speaking a tip next to it, so the two never overlap. The arrow stays.
			if (AugmentAdvisoryHUD.IsTalking)
				return;

			// What it is, how far, and which way.
			string where = Where(s.delta);
			string line1 = s.oreName;
			string line2 = s.distanceTiles <= 4f ? "right here!" : (int)Math.Round(s.distanceTiles) + " tiles" + (where.Length > 0 ? ", " + where : "");
			float scale = 0.62f;
			float w1 = HubDraw.Width(line1, scale);
			float w2 = HubDraw.Width(line2, scale);
			float y = anchor.Y + ring + 22f;
			DrawShadowed(sb, line1, new Vector2(anchor.X - w1 / 2f, y), orange * a, scale);
			DrawShadowed(sb, line2, new Vector2(anchor.X - w2 / 2f, y + HubDraw.LineHeight(scale) + 1f), Color.White * (0.9f * a), scale);
		}

		private static void DrawShadowed(SpriteBatch sb, string text, Vector2 pos, Color c, float scale)
		{
			HubDraw.Text(sb, text, pos + new Vector2(1f, 1f), Color.Black * (c.A / 255f * 0.8f), scale);
			HubDraw.Text(sb, text, pos, c, scale);
		}
	}
}