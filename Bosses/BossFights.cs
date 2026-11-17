using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Augments
{
	// A boss fight is one reward even when the boss is built from several NPCs. This works out which
	// fight an NPC belongs to (the key players are tracked under) and which death pays it out.
	//
	// Without it, hits on parts that aren't boss-flagged never counted (Slime God's paladins - its Core
	// can't be damaged at all - and Aquatic Scourge's body), and parts that ARE boss-flagged were tracked
	// under their own type, so they never matched the type that finally dies (worm bodies, Exo Mech arms).
	public static class BossFights
	{
		private const string CalamityName = "CalamityMod";

		// Vanilla part -> the NPC type whose death ends the fight. A leader maps to itself where its boss
		// flag can't be relied on to register hits.
		private static readonly Dictionary<int, int> VanillaLeaders = new Dictionary<int, int>
		{
			{ NPCID.EaterofWorldsHead, NPCID.EaterofWorldsHead },
			{ NPCID.EaterofWorldsBody, NPCID.EaterofWorldsHead },
			{ NPCID.EaterofWorldsTail, NPCID.EaterofWorldsHead },
			{ NPCID.SkeletronHand, NPCID.SkeletronHead },
			{ NPCID.WallofFleshEye, NPCID.WallofFlesh },
			{ NPCID.TheDestroyer, NPCID.TheDestroyer },
			{ NPCID.TheDestroyerBody, NPCID.TheDestroyer },
			{ NPCID.TheDestroyerTail, NPCID.TheDestroyer },
			{ NPCID.PrimeCannon, NPCID.SkeletronPrime },
			{ NPCID.PrimeSaw, NPCID.SkeletronPrime },
			{ NPCID.PrimeVice, NPCID.SkeletronPrime },
			{ NPCID.PrimeLaser, NPCID.SkeletronPrime },
			{ NPCID.GolemHead, NPCID.Golem },
			{ NPCID.GolemHeadFree, NPCID.Golem },
			{ NPCID.GolemFistLeft, NPCID.Golem },
			{ NPCID.GolemFistRight, NPCID.Golem },
			{ NPCID.CultistBossClone, NPCID.CultistBoss },
			{ NPCID.MoonLordHead, NPCID.MoonLordCore },
			{ NPCID.MoonLordHand, NPCID.MoonLordCore },
			{ NPCID.MoonLordFreeEye, NPCID.MoonLordCore },
		};

		// Calamity part -> the Calamity NPC (by internal name) whose death ends the fight.
		private static readonly Dictionary<string, string> CalamityLeaders = new Dictionary<string, string>
		{
			// Slime God: the Core takes no damage, the paladins (and the halves they split into) do.
			{ "EbonianPaladin", "SlimeGodCore" },
			{ "CrimulanPaladin", "SlimeGodCore" },
			{ "SplitEbonianPaladin", "SlimeGodCore" },
			{ "SplitCrimulanPaladin", "SlimeGodCore" },
			// Older Calamity versions called them slime gods.
			{ "EbonianSlimeGod", "SlimeGodCore" },
			{ "CrimulanSlimeGod", "SlimeGodCore" },
			{ "SplitEbonianSlimeGod", "SlimeGodCore" },
			{ "SplitCrimulanSlimeGod", "SlimeGodCore" },
			{ "SlimeGod", "SlimeGodCore" },
			{ "SlimeGodRun", "SlimeGodCore" },
			{ "SlimeGodSplit", "SlimeGodCore" },
			{ "SlimeGodRunSplit", "SlimeGodCore" },
			// Worms: the head is the one that dies for the fight, the body and tail soak the hits.
			{ "DesertScourgeBody", "DesertScourgeHead" },
			{ "DesertScourgeTail", "DesertScourgeHead" },
			{ "AquaticScourgeBody", "AquaticScourgeHead" },
			{ "AquaticScourgeBodyAlt", "AquaticScourgeHead" },
			{ "AquaticScourgeTail", "AquaticScourgeHead" },
			{ "AstrumDeusBody", "AstrumDeusHead" },
			{ "AstrumDeusTail", "AstrumDeusHead" },
			{ "StormWeaverBody", "StormWeaverHead" },
			{ "StormWeaverTail", "StormWeaverHead" },
			{ "DevourerofGodsBody", "DevourerofGodsHead" },
			{ "DevourerofGodsTail", "DevourerofGodsHead" },
			{ "ThanatosBody1", "ThanatosHead" },
			{ "ThanatosBody2", "ThanatosHead" },
			{ "ThanatosTail", "ThanatosHead" },
			// Ares and its arms.
			{ "AresLaserCannon", "AresBody" },
			{ "AresPlasmaFlamethrower", "AresBody" },
			{ "AresTeslaCannon", "AresBody" },
			{ "AresGaussNuke", "AresBody" },
			// Profaned Guardians: the Commander is the one that counts.
			{ "ProfanedGuardianDefender", "ProfanedGuardianCommander" },
			{ "ProfanedGuardianHealer", "ProfanedGuardianCommander" },
			// Ravager and its limbs.
			{ "RavagerClawLeft", "RavagerBody" },
			{ "RavagerClawRight", "RavagerBody" },
			{ "RavagerLegLeft", "RavagerBody" },
			{ "RavagerLegRight", "RavagerBody" },
			{ "RavagerHead", "RavagerBody" },
			{ "RavagerHead2", "RavagerBody" },
			// Perforator Hive and its cysts and worms.
			{ "PerforatorCyst", "PerforatorHive" },
			{ "PerforatorHeadSmall", "PerforatorHive" },
			{ "PerforatorBodySmall", "PerforatorHive" },
			{ "PerforatorTailSmall", "PerforatorHive" },
			{ "PerforatorHeadMedium", "PerforatorHive" },
			{ "PerforatorBodyMedium", "PerforatorHive" },
			{ "PerforatorTailMedium", "PerforatorHive" },
			{ "PerforatorHeadLarge", "PerforatorHive" },
			{ "PerforatorBodyLarge", "PerforatorHive" },
			{ "PerforatorTailLarge", "PerforatorHive" },
			// Primordial Wyrm: the head is the one that dies, the body and tail soak the hits.
			{ "PrimordialWyrmBody", "PrimordialWyrmHead" },
			{ "PrimordialWyrmBodyAlt", "PrimordialWyrmHead" },
			{ "PrimordialWyrmTail", "PrimordialWyrmHead" },
			// Polterghast's hooks.
			{ "PolterghastHook", "Polterghast" },
		};

		// Calamity parts that belong to a vanilla boss.
		private static readonly Dictionary<string, int> CalamityVanillaLeaders = new Dictionary<string, int>
		{
			{ "BrainIllusion", NPCID.BrainofCthulhu },
			{ "FalseBrain", NPCID.BrainofCthulhu },
		};

		// Calamity NPC type by internal name; -1 when it can't be found in the installed version.
		private static readonly Dictionary<string, int> calamityTypes = new Dictionary<string, int>();

		// The key a hit on this NPC counts towards, or false when it isn't part of a boss fight.
		// The key is the NPC type of the fight's leader (Twins share Retinazer's).
		public static bool TryGetFightKey(NPC npc, out int key)
		{
			if (VanillaLeaders.TryGetValue(npc.type, out key))
				return true;

			ModNPC modNpc = npc.ModNPC;
			if (modNpc != null && modNpc.Mod.Name == CalamityName)
			{
				if (CalamityLeaders.TryGetValue(modNpc.Name, out string leaderName) && TryGetCalamityType(leaderName, out key))
					return true;

				if (CalamityVanillaLeaders.TryGetValue(modNpc.Name, out key))
					return true;
			}

			if (npc.boss)
			{
				key = npc.type == NPCID.Spazmatism ? NPCID.Retinazer : npc.type;
				return true;
			}

			key = -1;
			return false;
		}

		// Whether a hit on this NPC is a hit on a boss: the boss itself or any part of its fight (Golem's head and fists, worm bodies,
		// Calamity limbs...). Plugins that react to "hits against a boss" use this, because many bosses are several NPCs and only one of
		// them has the boss flag.
		public static bool IsBossTarget(NPC npc)
		{
			if (npc.boss || TryGetFightKey(npc, out _))
				return true;

			// Segments that share the health of a boss-flagged head.
			return npc.realLife >= 0 && npc.realLife < Main.maxNPCs
				&& Main.npc[npc.realLife].active && Main.npc[npc.realLife].boss;
		}

		// The max health of the whole boss that this NPC belongs to (not of the part that was hit: a Golem fist or a worm segment has far
		// less than the boss).
		public static int BossMaxLife(NPC npc)
		{
			int max = npc.lifeMax;

			if (npc.realLife >= 0 && npc.realLife < Main.maxNPCs)
				max = System.Math.Max(max, Main.npc[npc.realLife].lifeMax);

			if (TryGetFightKey(npc, out int key))
			{
				for (int i = 0; i < Main.maxNPCs; i++)
				{
					NPC other = Main.npc[i];
					if (other.active && other.type == key)
						max = System.Math.Max(max, other.lifeMax);
				}
			}

			return max;
		}

		// Whether this NPC dying should pay the fight out. Parts of a bigger fight never do, only its leader.
		public static bool IsRewardTrigger(NPC npc, int key)
		{
			// Eater of Worlds has no leader: the fight ends with its last segment, whichever that is.
			if (IsEaterOfWorlds(npc.type))
				return !OtherEaterOfWorldsSegmentAlive(npc);

			// The Twins end together, so whoever dies last pays out and one fight is one reward.
			if (npc.type == NPCID.Retinazer)
				return !NPC.AnyNPCs(NPCID.Spazmatism);
			if (npc.type == NPCID.Spazmatism)
				return !NPC.AnyNPCs(NPCID.Retinazer);

			return npc.type == key;
		}

		private static bool IsEaterOfWorlds(int type)
			=> type == NPCID.EaterofWorldsHead || type == NPCID.EaterofWorldsBody || type == NPCID.EaterofWorldsTail;

		private static bool OtherEaterOfWorldsSegmentAlive(NPC dying)
		{
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC other = Main.npc[i];
				if (other.active && other.whoAmI != dying.whoAmI && IsEaterOfWorlds(other.type))
					return true;
			}

			return false;
		}

		private static bool TryGetCalamityType(string name, out int type)
		{
			if (!calamityTypes.TryGetValue(name, out type))
			{
				type = ModContent.TryFind<ModNPC>(CalamityName, name, out ModNPC found) ? found.Type : -1;
				calamityTypes[name] = type;
			}

			return type >= 0;
		}
	}
}
