using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Augments
{
	// The Vein Miner plugin's Pod upgrade ("Pod 360"). It is a behaviour of the Pod on the owner's shoulder, but there is no drone to move:
	// when the owner mines an ore with a pickaxe, the ore tiles of the same kind touching it are queued, and PodVeinMinerSystem breaks them
	// a few ticks apart (so it looks like the Pod chewing through the vein, and a big vein does not freeze the game).
	//
	// Only the owner's own machine does this. It breaks tiles the same way mining does (WorldGen.KillTile, then a tile update to the
	// server), so in multiplayer every other player and the server see the tiles go like any tiles the owner had mined by hand.
	internal static class PodVeinMiner
	{
		public const int MaxExtraTiles = 8;

		internal static readonly Queue<Point> Pending = new Queue<Point>();
		internal static bool Working; // true while this system breaks a tile, so those breaks never start another vein

		// Which tiles count, shared with the Ore Sniffer so "ore" means the same thing in both.
		internal static bool IsOre(int type) => PodOreSniffer.IsOre(type);

		// The tiles of the same kind touching (x, y), nearest first, up to the limit. Corners count as touching.
		internal static List<Point> Collect(int x, int y, int type)
		{
			var found = new List<Point>();
			var seen = new HashSet<Point> { new Point(x, y) };
			var frontier = new Queue<Point>();
			frontier.Enqueue(new Point(x, y));

			while (frontier.Count > 0 && found.Count < MaxExtraTiles)
			{
				Point at = frontier.Dequeue();
				for (int dx = -1; dx <= 1; dx++)
				{
					for (int dy = -1; dy <= 1; dy++)
					{
						if (dx == 0 && dy == 0)
							continue;

						var next = new Point(at.X + dx, at.Y + dy);
						if (!seen.Add(next) || !WorldGen.InWorld(next.X, next.Y, 2))
							continue;

						Tile t = Main.tile[next.X, next.Y];
						if (!t.HasTile || t.TileType != type)
							continue;

						found.Add(next);
						frontier.Enqueue(next);
						if (found.Count >= MaxExtraTiles)
							return found;
					}
				}
			}
			return found;
		}
	}

	// Notices when the local player mines an ore with a pickaxe.
	internal sealed class PodVeinMinerTile : GlobalTile
	{
		public override void KillTile(int i, int j, int type, ref bool fail, ref bool effectOnly, ref bool noItem)
		{
			if (fail || effectOnly || noItem || PodVeinMiner.Working || Main.netMode == NetmodeID.Server)
				return;
			if (!PodVeinMiner.IsOre(type))
				return;

			Player p = Main.LocalPlayer;
			if (p == null || !p.active || p.dead || !p.GetModPlayer<AugmentPlayer>().HasAugment(VeinMinerAugment.ChipId))
				return;

			// It has to be this player's own pickaxe swing at this very tile (not a bomb, another player, or something else breaking it).
			if (p.HeldItem.pick <= 0 || p.itemAnimation <= 0 || Player.tileTargetX != i || Player.tileTargetY != j)
				return;

			foreach (Point extra in PodVeinMiner.Collect(i, j, type))
				PodVeinMiner.Pending.Enqueue(extra);
		}
	}

	// Breaks the queued vein tiles a few ticks apart.
	internal sealed class PodVeinMinerSystem : ModSystem
	{
		private const int TicksBetweenTiles = 2;
		private int clock;

		public override void OnWorldUnload()
		{
			PodVeinMiner.Pending.Clear();
			PodVeinMiner.Working = false;
			clock = 0;
		}

		public override void PostUpdateEverything()
		{
			if (Main.dedServ || PodVeinMiner.Pending.Count == 0)
				return;

			Player p = Main.LocalPlayer;
			if (p == null || !p.active || p.dead || !p.GetModPlayer<AugmentPlayer>().HasAugment(VeinMinerAugment.ChipId))
			{
				PodVeinMiner.Pending.Clear();
				return;
			}

			if (++clock < TicksBetweenTiles)
				return;
			clock = 0;

			Point at = PodVeinMiner.Pending.Dequeue();
			if (!WorldGen.InWorld(at.X, at.Y, 2))
				return;

			Tile t = Main.tile[at.X, at.Y];
			if (!t.HasTile || !PodVeinMiner.IsOre(t.TileType) || !p.HasEnoughPickPowerToHurtTile(at.X, at.Y))
				return;

			PodVeinMiner.Working = true;
			try
			{
				WorldGen.KillTile(at.X, at.Y, false, false, false);
			}
			finally
			{
				PodVeinMiner.Working = false;
			}

			if (!Main.tile[at.X, at.Y].HasTile && Main.netMode == NetmodeID.MultiplayerClient)
				NetMessage.SendData(MessageID.TileManipulation, -1, -1, null, 0, at.X, at.Y, 0f);
		}
	}
}