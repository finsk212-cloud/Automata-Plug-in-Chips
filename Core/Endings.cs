using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace Augments
{
	// Endings A to E: five hidden, one time challenges for each character. They are not missions: they have no progress bar and pay
	// no Data Fragments. The Profile page shows only a riddle until one is earned, and then the "transmission" text.
	//
	// All five are about HOW you beat a boss, so they are worked out from one boss fight at a time (see EndingsPlayer).
	// Endings only work in singleplayer for now: in multiplayer a teammate can finish the boss while you stand back, which would
	// hand out the "no hit" ending for free.
	public static class Endings
	{
		public sealed class Def
		{
			public char Letter;
			public string Name;
			public string Riddle;
			public string Transmission;
			// What the Commander says on the call screen when the ending is earned (see EndingCallHUD).
			public string Call;
			// The weakest boss that counts (see BossCounts). Keeps an early boss from giving a hard ending by accident.
			public RarityBracket MinBracket;
		}

		public static readonly Def[] All =
		{
			new Def { Letter = 'A', Call = "Unit 042, I watched the whole fight. Not one hit landed. Whatever you are doing out there, keep doing it.", Name = "UNTOUCHED", Riddle = "Not a scratch.", Transmission = "Unit 042 faced it alone and came back whole. Not one hit landed.", MinBracket = RarityBracket.EarlyHardmode },
			new Def { Letter = 'B', Call = "Only the plainest plugins, and still you won. Command checked the records twice. No unit has ever done it this plainly.", Name = "PLAINEST", Riddle = "The plainest plugins win the proudest.", Transmission = "No rare parts and no shine. Command had never seen a win this plain.", MinBracket = RarityBracket.EarlyHardmode },
			new Def { Letter = 'C', Call = "Your frame was empty. No plugins, nothing to lean on. And you won anyway. I will not forget it.", Name = "EMPTY CHASSIS", Riddle = "Win with nothing installed.", Transmission = "No plugins at all. Just the frame, and it was enough.", MinBracket = RarityBracket.EarlyHardmode },
			new Def { Letter = 'D', Call = "No repairs. No remedy. You took all of it and you are still standing. That kind of resolve is rare.", Name = "NO SAFETY NET", Riddle = "Pain is the only medicine.", Transmission = "No repairs and no remedy. The unit endured to the end.", MinBracket = RarityBracket.EarlyHardmode },
			new Def { Letter = 'E', Call = "Untouched, plain and unhealed, all in one fight. Unit 042, this is what Command hoped for. Glory to mankind.", Name = "TRUE ENDING", Riddle = "Every rule at once, in a single fight.", Transmission = "Untouched, plain and unhealed. This is the one Command will remember.", MinBracket = RarityBracket.PostMechs },
		};

		// The rules in one place. met says which endings the fight's conditions satisfy (A to E); the return value is the new bits to award:
		// only endings not already earned, and only when the boss is old enough to count. The test project in the scratchpad checks this.
		public static int Evaluate(bool tookHit, bool usedPotion, bool anyPlugin, bool nonCommonPlugin, RarityBracket boss, RarityBracket worldAtStart, int alreadyEarned, out bool[] met)
		{
			bool a = !tookHit;
			bool b = anyPlugin && !nonCommonPlugin;
			bool c = !anyPlugin;
			bool d = !usedPotion;
			bool e = a && b && d;
			met = new[] { a, b, c, d, e };

			int gained = 0;
			for (int i = 0; i < All.Length; i++)
			{
				if (met[i] && !Has(alreadyEarned, i) && BossCounts(boss, worldAtStart, All[i].MinBracket))
					gained |= 1 << i;
			}

			return gained;
		}

		public static bool Has(int mask, int index) => index >= 0 && index < All.Length && (mask & (1 << index)) != 0;

		// The one place that decides which bosses count. The boss must be at least the ending's own minimum, and at least as new as
		// the world was when the fight started, so an old boss in a late world does not count.
		public static bool BossCounts(RarityBracket boss, RarityBracket worldAtStart, RarityBracket min)
			=> boss >= min && boss >= worldAtStart;
	}

	// Follows each boss fight for the local player and awards endings when the boss dies.
	public class EndingsPlayer : ModPlayer
	{
		// One boss fight. It starts when a boss is alive near the player (not on the first hit, or a hit taken before you attacked
		// would be missed) and the flags only ever turn on, so swapping a loadout or drinking mid fight cannot be undone.
		private sealed class Fight
		{
			public RarityBracket Bracket;
			public RarityBracket WorldAtStart;
			public bool TookHit;
			public bool UsedPotion;
			public bool AnyPlugin;
			public bool NonCommonPlugin;
			public uint LastSeen;
		}

		private const float StartRange = 3000f;
		private const uint ForgetAfterTicks = 300; // 5 seconds with no part of the boss alive ends the fight

		// Bit 0 = A ... bit 4 = E.
		public int EarnedMask;

		private readonly Dictionary<int, Fight> fights = new Dictionary<int, Fight>();
		private uint tick;
		private int lastSicknessTime;
		private int lastLife = -1;
		private int lastLifeMax = -1;

		private bool Active => Main.netMode == NetmodeID.SinglePlayer && Player.whoAmI == Main.myPlayer;

		public override void SaveData(TagCompound tag)
		{
			tag["endings"] = EarnedMask;
		}

		public override void LoadData(TagCompound tag)
		{
			EarnedMask = tag.ContainsKey("endings") ? tag.GetInt("endings") : 0;
		}

		public override void OnEnterWorld()
		{
			fights.Clear();
			EndingCallHUD.Clear();
			lastSicknessTime = 0;
			lastLife = -1;
			lastLifeMax = -1;
		}

		public override void OnHurt(Player.HurtInfo info)
		{
			if (!Active || info.Damage <= 0)
				return;

			foreach (Fight f in fights.Values)
				f.TookHit = true;
		}

		public override void PostUpdate()
		{
			if (!Active)
				return;

			tick++;

			// A healing potion starts Potion Sickness, so its time going UP means one was drunk this tick.
			int index = Player.FindBuffIndex(BuffID.PotionSickness);
			int sickness = index >= 0 ? Player.buffTime[index] : 0;
			bool drank = sickness > lastSicknessTime;
			lastSicknessTime = sickness;

			// Life that went down without a hit (poison, fire, a plugin that costs life) still counts as a scratch. A change in maximum life
			// (a plugin swap, a potion wearing off) is not a loss, so those ticks are skipped.
			bool lostLife = !Player.dead && Player.statLifeMax2 == lastLifeMax && Player.statLife < lastLife;
			lastLife = Player.statLife;
			lastLifeMax = Player.statLifeMax2;

			if (fights.Count > 0)
			{
				bool anyPlugin = false;
				bool nonCommon = false;
				IReadOnlyList<Augment> owned = Player.GetModPlayer<AugmentPlayer>().Owned;
				for (int i = 0; i < owned.Count; i++)
				{
					anyPlugin = true;
					if (owned[i].Rarity != AugmentRarity.Common)
						nonCommon = true;
				}

				foreach (Fight f in fights.Values)
				{
					f.AnyPlugin |= anyPlugin;
					f.NonCommonPlugin |= nonCommon;
					f.UsedPotion |= drank;
					f.TookHit |= lostLife;
				}
			}

			if (tick % 2 == 0)
				ScanForBosses();
		}

		private void ScanForBosses()
		{
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC npc = Main.npc[i];
				if (!npc.active || !BossFights.TryGetFightKey(npc, out int key))
					continue;

				if (fights.TryGetValue(key, out Fight existing))
				{
					existing.LastSeen = tick;
					continue;
				}

				if (Player.Distance(npc.Center) > StartRange)
					continue;

				RarityBracket world = BossTierMap.GetCurrentWorldBracket();
				bool listed = BossTierMap.Brackets.TryGetValue(key, out RarityBracket bracket);
				if (!listed)
				{
					// A boss that is not in the tier map (some modded ones) is guessed with a slightly different ladder than the
					// world's, so never let it count as older than the world: it counts as a boss of the world's current stage.
					bracket = BossAugmentDrop.GetModdedBossBracket(npc);
					if (bracket < world)
						bracket = world;
				}

				var fight = new Fight { Bracket = bracket, WorldAtStart = world, LastSeen = tick };

				// Start from what is true right now: the plugins equipped at this moment count from the first tick.
				IReadOnlyList<Augment> owned = Player.GetModPlayer<AugmentPlayer>().Owned;
				for (int p = 0; p < owned.Count; p++)
				{
					fight.AnyPlugin = true;
					if (owned[p].Rarity != AugmentRarity.Common)
						fight.NonCommonPlugin = true;
				}

				fights[key] = fight;
			}

			List<int> stale = null;
			foreach (KeyValuePair<int, Fight> kv in fights)
			{
				if (tick - kv.Value.LastSeen > ForgetAfterTicks)
					(stale ??= new List<int>()).Add(kv.Key);
			}

			if (stale != null)
			{
				foreach (int key in stale)
					fights.Remove(key);
			}
		}

		// Gives one ending (0 = A ... 4 = E) and queues its call screen. Used by real boss kills (30 second wait) and by the debug command (none).
		public void Grant(int index, float delaySeconds = EndingCallHUD.DefaultDelay)
		{
			if (index < 0 || index >= Endings.All.Length)
				return;

			EarnedMask |= 1 << index;
			// The call screen shows the ending and plays its own sounds.
			EndingCallHUD.Show(index, EarnedMask, delaySeconds);
		}

		// Debug only (see EndingsDebugCommand): forgets every earned ending.
		public void DebugClear()
		{
			EarnedMask = 0;
		}

		// Any healing item used (see EndingsHealWatcher) counts as using a potion, even one that gives no Potion Sickness.
		public void NoteHealing()
		{
			if (!Active)
				return;

			foreach (Fight f in fights.Values)
				f.UsedPotion = true;
		}

		// Debug only (see EndingsDebugCommand): one line per boss fight being followed, so you can watch the flags change.
		public List<string> DebugStatus()
		{
			var lines = new List<string>();
			foreach (KeyValuePair<int, Fight> kv in fights)
			{
				Fight f = kv.Value;
				lines.Add("fight key " + kv.Key + " (" + f.Bracket + ", world was " + f.WorldAtStart + "): hit=" + f.TookHit + " potion=" + f.UsedPotion
					+ " plugins=" + f.AnyPlugin + " nonCommon=" + f.NonCommonPlugin);
			}

			return lines;
		}

		// Called by BossAugmentDrop when the boss this player fought dies (singleplayer only).
		public void OnBossDefeated(int bossKey)
		{
			if (!Active)
				return;

			if (!fights.TryGetValue(bossKey, out Fight f))
			{
				ModContent.GetInstance<Augments>().Logger.Info("ENDINGS boss key " + bossKey + " died but no fight was being followed, so nothing was checked");
				return;
			}

			fights.Remove(bossKey);

			int gained = Endings.Evaluate(f.TookHit, f.UsedPotion, f.AnyPlugin, f.NonCommonPlugin, f.Bracket, f.WorldAtStart, EarnedMask, out bool[] met);
			bool a = met[0], b = met[1], c = met[2], d = met[3], e = met[4];

			// One line per boss kill, so a single easy kill shows whether every condition was read correctly.
			ModContent.GetInstance<Augments>().Logger.Info("ENDINGS kill key=" + bossKey + " bracket=" + f.Bracket + " worldAtStart=" + f.WorldAtStart
				+ " tookHit=" + f.TookHit + " usedPotion=" + f.UsedPotion + " anyPlugin=" + f.AnyPlugin + " nonCommon=" + f.NonCommonPlugin
				+ " met=" + (a ? "A" : "") + (b ? "B" : "") + (c ? "C" : "") + (d ? "D" : "") + (e ? "E" : "") + " newly earned mask=" + gained);

			for (int i = 0; i < Endings.All.Length; i++)
			{
				if ((gained & (1 << i)) == 0)
					continue;

				Grant(i);
			}
		}
	}

	// Tells the endings when a healing item is used, so ending D cannot be earned by a potion that gives no Potion Sickness.
	public class EndingsHealWatcher : GlobalItem
	{
		public override void OnConsumeItem(Item item, Player player)
		{
			if (item.healLife > 0 || item.potion)
				player.GetModPlayer<EndingsPlayer>().NoteHealing();
		}
	}
}
