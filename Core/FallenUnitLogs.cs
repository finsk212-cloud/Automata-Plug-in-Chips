using System;
using Terraria;

namespace Augments
{
	// The story of the Fallen Units: six short memory logs. Each unit holds one, and the Pod shows it in its message box once you have
	// recovered that unit. Reading logs is remembered by the account, so the six add up across every world and character.
	//
	// Nothing about the logs is saved in the world or sent over the network: which log a unit holds comes from its number and the world's
	// id, so every game works out the same answer. A rotation (not a shuffle) keeps all six logs present in any world with six or more units.
	//
	// Two rewards come from this (each a Profile title and a Pod skin, like the hidden endings, owned by the whole account):
	//   - THE NEXT UNIT (Patchwork Pod): the first time you have recovered every unit in one world.
	//   - HUM ALONG (Music Box Pod): once you have read all six logs.
	// The checks run while you stand near a unit you have recovered, so they also work for units recovered before this update.
	internal static class FallenUnitLogs
	{
		// One log per entry, already split into lines that fit the Pod's message box.
		public static readonly string[][] Logs =
		{
			new[] { "We were told to wait here.", "Nobody said for what." },
			new[] { "Something stirs far below.", "It hums, very softly." },
			new[] { "Unit 4 went quiet.", "We think it went to listen." },
			new[] { "It means no harm.", "We think it is lonely too." },
			new[] { "We hid our plugins in our", "chests, for whoever came." },
			new[] { "If you are reading this,", "we waited well.", "Be kind to what hums below." }
		};

		// A world needs at least this many units before "every unit" counts, so an empty or half-made list can never finish it.
		public const int MinUnitsForCompletion = 6;

		// The ids of the two rewards. They are saved in the account file, so they are permanent.
		public const string NextUnitId = "nextunit";
		public const string HumAlongId = "humalong";

		// What the Shop shows for each locked reward, in order: THE NEXT UNIT, then HUM ALONG.
		public static readonly string[] RewardIds = { NextUnitId, HumAlongId };
		public static readonly string[] RewardHints =
		{
			"Leave no body behind.",
			"Six voices left a song. Hear every line."
		};

		public static int IndexOf(FallenUnit unit)
		{
			return (((unit.Id - 1) + (Main.worldID & 0x7fffffff)) % Logs.Length + Logs.Length) % Logs.Length;
		}

		public static string[] LinesOf(FallenUnit unit) => Logs[IndexOf(unit)];

		private static string LogKey(int index) => "fulog:" + index;

		public static int LogsRead()
		{
			int count = 0;
			for (int i = 0; i < Logs.Length; i++)
			{
				if (AccountStore.IsUnlocked(LogKey(i)))
					count++;
			}
			return count;
		}

		// The mark drawn on a reward's title icon, or '\0' if the title is not one of these.
		public static char MarkOfTitle(string id)
		{
			if (id == NextUnitId)
				return 'U';
			return id == HumAlongId ? 'H' : '\0';
		}

		private static void Unlock(string rewardId)
		{
			AccountStore.Unlock("title:" + rewardId);
			AccountStore.Unlock("podskin:" + rewardId);
		}

		private static bool Owns(string rewardId) => AccountStore.IsUnlocked("title:" + rewardId) && AccountStore.IsUnlocked("podskin:" + rewardId);

		// Called every tick for the local player. Does nothing unless a recovered unit is within reach.
		internal static void Update(Player player)
		{
			if (Main.dedServ || Main.gameMenu || player == null)
				return;

			bool nearRecovered = false;
			foreach (FallenUnit unit in FallenUnitSystem.Units)
			{
				if (!unit.IsRecoveredBy(player))
					continue;
				if (Microsoft.Xna.Framework.Vector2.Distance(player.Center, unit.Center) > FallenUnitSystem.RecoverRange)
					continue;

				nearRecovered = true;
				int index = IndexOf(unit);
				if (!AccountStore.IsUnlocked(LogKey(index)))
				{
					AccountStore.Unlock(LogKey(index));
					Main.NewText("Pod 042: memory log saved. " + LogsRead() + " of " + Logs.Length + " read.", 255, 200, 90);
				}
			}

			if (!nearRecovered)
				return;

			if (LogsRead() >= Logs.Length && !Owns(HumAlongId))
			{
				Unlock(HumAlongId);
				Main.NewText("Pod 042: all the logs are read. A new title and Pod skin are waiting in the Shop.", 255, 214, 90);
			}

			if (!Owns(NextUnitId) && EveryUnitRecovered(player))
			{
				Unlock(NextUnitId);
				Main.NewText("Pod 042: every unit is recovered. A new title and Pod skin are waiting in the Shop.", 255, 214, 90);
			}
		}

		// True when the world has enough units and this character has recovered all of them.
		private static bool EveryUnitRecovered(Player player)
		{
			if (FallenUnitSystem.Units.Count < MinUnitsForCompletion)
				return false;

			foreach (FallenUnit unit in FallenUnitSystem.Units)
			{
				if (!unit.IsRecoveredBy(player))
					return false;
			}
			return true;
		}
	}
}