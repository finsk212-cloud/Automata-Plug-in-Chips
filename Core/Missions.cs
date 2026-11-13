using System;
using System.Collections.Generic;

namespace Augments
{
	public enum MissionKind
	{
		Playtime, // active seconds playing with the mod
		Bosses,   // different bosses defeated while playing
		Survival, // active seconds in one stretch without dying
		Coop      // active seconds with another player in the world
	}

	// The Orders: goals that pay Data Fragments. Every one is measured from things that happen while you play (see
	// AccountEarningPlayer), never from what a character already owns, so a loaded save or an item-spawning tool cannot finish them.
	// Collecting plugins is deliberately NOT an order: the /augment command can hand plugins out.
	public sealed class Mission
	{
		public string Id;
		public string Title;
		public string Description;
		public MissionKind Kind;
		public double Target; // seconds for the time-based kinds, a count for Bosses
		public int Reward;
		public string Group;

		public bool IsTime => Kind != MissionKind.Bosses;
	}

	public static class Missions
	{
		private const double Minute = 60.0;
		private const double Hour = 3600.0;

		public static readonly Mission[] All =
		{
			new Mission { Id = "play_30m", Group = "ON DUTY", Title = "FIRST SHIFT", Description = "Play 30 minutes with the mod.", Kind = MissionKind.Playtime, Target = 30 * Minute, Reward = 40 },
			new Mission { Id = "play_3h", Group = "ON DUTY", Title = "ON THE CLOCK", Description = "Play 3 hours with the mod.", Kind = MissionKind.Playtime, Target = 3 * Hour, Reward = 120 },
			new Mission { Id = "play_10h", Group = "ON DUTY", Title = "LONG HAUL", Description = "Play 10 hours with the mod.", Kind = MissionKind.Playtime, Target = 10 * Hour, Reward = 350 },
			new Mission { Id = "play_25h", Group = "ON DUTY", Title = "UNIT 042", Description = "Play 25 hours with the mod.", Kind = MissionKind.Playtime, Target = 25 * Hour, Reward = 800 },

			new Mission { Id = "boss_1", Group = "COMBAT", Title = "TARGET ACQUIRED", Description = "Defeat your first boss.", Kind = MissionKind.Bosses, Target = 1, Reward = 60 },
			new Mission { Id = "boss_5", Group = "COMBAT", Title = "EXTERMINATOR", Description = "Defeat 5 different bosses.", Kind = MissionKind.Bosses, Target = 5, Reward = 200 },
			new Mission { Id = "boss_12", Group = "COMBAT", Title = "SCRAP METAL", Description = "Defeat 12 different bosses.", Kind = MissionKind.Bosses, Target = 12, Reward = 500 },

			new Mission { Id = "alive_20m", Group = "FIELD", Title = "STAY ALIVE", Description = "Play 20 minutes in a row without dying.", Kind = MissionKind.Survival, Target = 20 * Minute, Reward = 80 },
			new Mission { Id = "coop_30m", Group = "FIELD", Title = "COMRADES", Description = "Play 30 minutes with another player in your world.", Kind = MissionKind.Coop, Target = 30 * Minute, Reward = 150 }
		};

		// How far along a mission is, in the same unit as its Target. `aliveStretch` is the current no-death stretch in seconds
		// (only known in a world; on the title screen a finished survival order shows full and an open one shows empty).
		public static double Progress(Mission m, double aliveStretch)
		{
			if (AccountStore.IsMissionDone(m.Id))
				return m.Target;

			switch (m.Kind)
			{
				case MissionKind.Playtime: return Math.Min(m.Target, AccountStore.PlaySeconds);
				case MissionKind.Bosses: return Math.Min(m.Target, AccountStore.BossCount);
				case MissionKind.Survival: return Math.Min(m.Target, aliveStretch);
				default: return Math.Min(m.Target, AccountStore.CoopSeconds);
			}
		}

		// Finishes every order that has reached its target and returns the ones that just finished, so the caller can announce them.
		public static List<Mission> CompleteReached(double aliveStretch)
		{
			List<Mission> finished = null;
			foreach (Mission m in All)
			{
				if (AccountStore.IsMissionDone(m.Id) || Progress(m, aliveStretch) < m.Target)
					continue;

				if (AccountStore.CompleteMission(m.Id, m.Reward))
				{
					if (finished == null)
						finished = new List<Mission>();
					finished.Add(m);
				}
			}
			return finished;
		}

		// "1 h 30 min" style text for time targets, plain numbers for counts.
		public static string Format(Mission m, double value)
		{
			if (!m.IsTime)
				return ((int)value).ToString();

			int totalMinutes = (int)(value / 60.0);
			if (totalMinutes >= 60)
				return (totalMinutes / 60) + " h " + (totalMinutes % 60).ToString("00") + " min";
			return totalMinutes + " min";
		}
	}
}
