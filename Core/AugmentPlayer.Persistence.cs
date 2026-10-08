using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.Chat;
using Terraria.DataStructures;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Augments.Core;

namespace Augments
{
	// Client/server sync, initialization and save/load (SaveData/LoadData).
	public partial class AugmentPlayer
	{
		// tModLoader calls CopyClientState each tick on the LOCAL player, copies
		// current state into a throw-away clone, then immediately calls
		// SendClientChanges with that clone as the "before" picture. If anything
		// changed, SendClientChanges fires a packet so the server (and from there
		// all other clients) can update their ghost copy of this player.
		public override void CopyClientState(ModPlayer targetCopy)
		{
			var snapshot = (AugmentPlayer)targetCopy;
			snapshot.syncedOwnedIds.Clear();
			snapshot.syncedOwnedIds.UnionWith(ownedIds);
		}

		public override void SendClientChanges(ModPlayer clientPlayer)
		{
			var prev = (AugmentPlayer)clientPlayer;
			if (ownedIds.SetEquals(prev.syncedOwnedIds))
				return;

			SendSyncOwnedAugments();
		}

		// Writes a SyncOwnedAugments packet from the local client to the server.
		internal void SendSyncOwnedAugments()
		{
			if (Main.netMode != NetmodeID.MultiplayerClient)
				return;

			ModPacket packet = ModContent.GetInstance<Augments>().GetPacket();
			packet.Write((byte)AugmentPacketType.SyncOwnedAugments);
			packet.Write((byte)Player.whoAmI);
			packet.Write(ownedIds.Count);
			foreach (string id in ownedIds)
				packet.Write(id);
			packet.Send();
		}

		// Applied on the server and on remote clients when a SyncOwnedAugments packet
		// arrives. Rebuilds ownedIds + owned WITHOUT touching cooldown/timer fields,
		// since those are synced separately via WriteAugmentState.
		public void ApplySyncedOwnedIds(IEnumerable<string> ids)
		{
			ownedIds.Clear();
			foreach (string id in ids)
			{
				if (ownedIds.Count >= MaxOwnedAugments)
					break;
				if (AugmentDatabase.GetById(id) != null)
					ownedIds.Add(id);
			}
			owned.Clear();
			foreach (string id in ownedIds)
			{
				Augment augment = AugmentDatabase.GetById(id);
				if (augment != null)
					owned.Add(augment);
			}
		}

		public override void Initialize()
		{
			ownedIds.Clear();
			everOwnedIds.Clear();
			soldAugmentIds.Clear();
			lockedKeystoneFamilies.Clear();
			owned.Clear();
			BossAugmentKills = new Dictionary<int, int>();
			DamagedBossesThisFight = new HashSet<int>();
			TrophyHunterKilledTypes = new HashSet<int>();
			LuckyFindCopperGained = 0;

			AdaptiveArmorUndamagedTimer = 0;
			AdaptiveArmorDefenseBonus = 0;
			AmbushStillTicks = 0;
			AmbushReady = false;
			ApexHunterMarkStacks = 0f;
			ApexHunterCooldown = 0;
			ArcaneSingularityCharge = 0f;
			AvatarOfRageLastLife = -1;
			BastionStoredDamage = 0;
			BastionBarrierTicks = 0;
			SynchronizerTimer = 0;
			BulwarkNoDamageTicks = 0;
			DeadeyeLastTargetWhoAmI = -1;
			DeadeyeHitStacks = 0f;
			EchoChamberPendingEchoes.Clear();
			EldritchCovenantCorruption = 0;
			EldritchCovenantManaProgress = 0;
			EternalFlameBoostTicks = 0;
			FeatherfallSpeedBurstTicks = 0;
			FinalStandCooldown = 0;
			FinalStandActiveTicks = 0;
			FortunesFavorRegenTimer = 0;
			FrenziedAssaultStacks = 0;
			FrenziedAssaultResetTimer = 0;
			GetExcitedTimer = 0;
			GetExcitedStacks = 0;
			GodslayerBladeCooldown = 0;
			GrappleMasterWasGrappling = false;
			GrappleMasterSpeedBurstTicks = 0;
			HuntersPaceSpeedTimer = 0;
			HuntersPaceCurrentBonus = 0f;
			InfernosHeartChargeStacks = 0;
			InfernosHeartResetTimer = 0;
			IronRhythmHitCounter = 0f;
			IronRhythmPendingSpecialDamage = 0;
			IronWillDurationRemaining = 0;
			IronWillCooldown = 0;
			LastStandCooldown = 0;
			LastStandArmedThisHit = false;
			MinionMomentumHitStacks = 0;
			MinionMomentumActiveMinionProjType = -1;
			MirrorImageInvulnTicks = 0;
			MomentumCrashPreviousSpeed = 0f;
			MomentumCrashDashWindowTimer = 0;
			MomentumCrashPendingConfuse = false;
			MomentumSwingLastTargetWhoAmI = -1;
			MomentumSwingStacks = 0;
			MomentumSwingResetTimer = 0;
			OverchargeRoundHitStacks = 0f;
			OverchargeRoundResetTimer = 0;
			OverwhelmLastTargetWhoAmI = -1;
			OverwhelmHitCounter = 0;
			OverwhelmResetTimer = 0;
			PhoenixHeartArmedThisHit = false;
			PhoenixHeartCooldown = 0;
			PhoenixHeartInvulnTicks = 0;
			PiedPiperCooldown = 0;
			PiedPiperDurationRemaining = 0;
			PotionRushTimer = 0;
			QuickRecoveryRegenTimer = 0;
			RavenousSwarmSlotsGranted = 0;
			LastReforgedItem = null;
			LastReforgePrefix = -1;
			LastReforgeCost = 0;
			LastReforgePendingCost = 0;
			RiposteWindowRemaining = 0;
			ScavengersLuckBuffTicks = 0;
			SecondWindCooldown = 0;
			SteadyHandsCurrentCritBonus = 0f;
			SteadyHandsRampTicks = 0;
			TwinStrikeItemProcPending = false;
			TwinStrikeProjProcPending = false;
			VoidStepKillStacks = 0;
			VoidStepResetTimer = 0;
			VoidStepInvulnTicks = 0;
			WarGodsTempoStacks = 0;
			WarGodsTempoResetTimer = 0;
			WildCardSpeedTicks = 0;
			WildCardInvulnTicks = 0;
		}

		public override void OnEnterWorld()
		{
			DamagedBossesThisFight.Clear();
			// A Pause clicked in an earlier session would otherwise silently drop every hit.
			AugmentDamageTracker.IsPaused = false;
			// Push our disk-loaded owned list to the server FIRST so it has the
			// correct state before it replies to RequestAugmentSync. Packets on the
			// same connection are processed in order, so this arrives before the
			// request is handled.
			SendSyncOwnedAugments();
			AugmentNet.SendRequestAugmentSync();
		}

		public override void SyncPlayer(int toWho, int fromWho, bool newPlayer)
		{
			if (Main.netMode == NetmodeID.Server)
				AugmentNet.SendSyncPlayer(Player, toWho);
		}

		public void WriteAugmentState(BinaryWriter writer)
		{
			writer.Write((ushort)ownedIds.Count);
			foreach (string id in ownedIds)
				writer.Write(id);

			writer.Write((ushort)everOwnedIds.Count);
			foreach (string id in everOwnedIds)
				writer.Write(id);

			writer.Write((ushort)soldAugmentIds.Count);
			foreach (string id in soldAugmentIds)
				writer.Write(id);

			writer.Write((ushort)lockedKeystoneFamilies.Count);
			foreach (string family in lockedKeystoneFamilies)
				writer.Write(family);

			writer.Write(RevitalizingWaveTimer);
			writer.Write(CleanseCooldown);
			writer.Write(LastRitesCooldown);
			writer.Write(lastRitesInvulnTicks);
			writer.Write(LifelineCooldown);
			writer.Write(lifelineInvulnTicks);
			writer.Write(lifelineProtectionAuthorized);
		}

		public void ReadAugmentState(BinaryReader reader)
		{
			ownedIds.Clear();
			ushort ownedCount = reader.ReadUInt16();
			for (int i = 0; i < ownedCount; i++)
				ownedIds.Add(reader.ReadString());

			everOwnedIds.Clear();
			ushort everOwnedCount = reader.ReadUInt16();
			for (int i = 0; i < everOwnedCount; i++)
				everOwnedIds.Add(reader.ReadString());

			soldAugmentIds.Clear();
			ushort soldCount = reader.ReadUInt16();
			for (int i = 0; i < soldCount; i++)
				soldAugmentIds.Add(reader.ReadString());

			lockedKeystoneFamilies.Clear();
			ushort lockedFamilyCount = reader.ReadUInt16();
			for (int i = 0; i < lockedFamilyCount; i++)
				lockedKeystoneFamilies.Add(reader.ReadString());

			RevitalizingWaveTimer = reader.ReadInt32();
			CleanseCooldown = reader.ReadInt32();
			LastRitesCooldown = reader.ReadInt32();
			lastRitesInvulnTicks = reader.ReadInt32();
			LifelineCooldown = reader.ReadInt32();
			lifelineInvulnTicks = reader.ReadInt32();
			lifelineProtectionAuthorized = reader.ReadBoolean();

			RebuildOwnedCacheFromOwnedIdsOnly();
		}

		// --- Persistence: augments survive between play sessions ---

		public override void SaveData(TagCompound tag)
		{
			var customData = new TagCompound();
			foreach (var a in owned)
			{
				// One chip's bug must not abort the save, or the whole character loses its chips.
				try
				{
					var augmentTag = new TagCompound();
					a.SaveCustomData(augmentTag);
					if (augmentTag.Count > 0)
						customData[a.Id] = augmentTag;
				}
				catch (System.Exception e)
				{
					Mod.Logger.Error($"Failed to save custom data for chip '{a.Id}'; skipping it.", e);
				}
			}
			tag["ownedAugmentIds"] = new List<string>(ownedIds);
			tag["augmentCustomData"] = customData;
			tag["everOwnedIds"] = new List<string>(everOwnedIds);
			tag["soldAugmentIds"] = new List<string>(soldAugmentIds);
			tag["lockedKeystoneFamilies"] = new List<string>(lockedKeystoneFamilies);
			tag["revitalizingWaveTimer"] = RevitalizingWaveTimer;
			tag["cleanseCooldown"] = CleanseCooldown;
			tag["lastRitesCooldown"] = LastRitesCooldown;
			tag["lastRitesInvulnTicks"] = lastRitesInvulnTicks;
			tag["lifelineCooldown"] = LifelineCooldown;
			tag["lifelineInvulnTicks"] = lifelineInvulnTicks;

			tag["trophyHunterKilledTypes"] = new List<int>(TrophyHunterKilledTypes);
			tag["luckyFindCopperGained"] = LuckyFindCopperGained;

			// TagCompound requires string keys — store NPC type as string.
			var bossKills = new TagCompound();
			foreach (var kv in BossAugmentKills)
				bossKills[kv.Key.ToString()] = kv.Value;
			tag["bossAugmentKills"] = bossKills;
			tag["seenAdvisoryTriggers"] = new List<string>(SeenAdvisoryTriggers);
		}

		private static string NormalizeLegacyId(string id) => id switch
		{
			"avatar_of_rage" => "type_b_berserker_protocol",
			"avatar_of_the_wall" => "type_d_dreadnought_protocol",
			"type_d_bastion_protocol" => "type_d_dreadnought_protocol",
			"avatar_of_balance" => "type_s_synchronizer_protocol",
			_ => id
		};

		public override void LoadData(TagCompound tag)
		{
			try
			{
				LoadDataInternal(tag);
			}
			catch (System.Exception e)
			{
				// Keep whatever loaded before the failure so the character stays playable.
				Mod.Logger.Error("Failed to fully load plug-in chip data; continuing with partial data.", e);
				RebuildOwnedCacheFromOwnedIdsOnly();
			}
		}

		private void LoadDataInternal(TagCompound tag)
		{
			ownedIds.Clear();
			everOwnedIds.Clear();
			soldAugmentIds.Clear();
			lockedKeystoneFamilies.Clear();
			SeenAdvisoryTriggers.Clear();
			if (tag.ContainsKey("seenAdvisoryTriggers"))
			{
				foreach (string trigger in tag.GetList<string>("seenAdvisoryTriggers"))
					SeenAdvisoryTriggers.Add(trigger);
			}

			string ownedKey = tag.ContainsKey("ownedAugmentIds") ? "ownedAugmentIds" : "augmentIds";
			if (tag.ContainsKey(ownedKey))
			{
				foreach (string rawId in tag.GetList<string>(ownedKey))
				{
					string id = NormalizeLegacyId(rawId);
					if (ownedIds.Count >= MaxOwnedAugments)
						break;
					if (AugmentDatabase.GetById(id) != null)
						ownedIds.Add(id);
				}
			}

			if (tag.ContainsKey("everOwnedIds"))
			{
				foreach (string rawId in tag.GetList<string>("everOwnedIds"))
					everOwnedIds.Add(NormalizeLegacyId(rawId));
			}
			everOwnedIds.UnionWith(ownedIds);

			if (tag.ContainsKey("soldAugmentIds"))
			{
				foreach (string rawId in tag.GetList<string>("soldAugmentIds"))
					soldAugmentIds.Add(NormalizeLegacyId(rawId));
			}
			else
			{
				// Legacy saves inferred buyback history as EverOwned minus Owned.
				foreach (string id in everOwnedIds)
				{
					if (!ownedIds.Contains(id))
						soldAugmentIds.Add(id);
				}
			}
			everOwnedIds.UnionWith(soldAugmentIds);

			if (tag.ContainsKey("lockedKeystoneFamilies"))
				lockedKeystoneFamilies.UnionWith(tag.GetList<string>("lockedKeystoneFamilies"));

			RevitalizingWaveTimer = tag.ContainsKey("revitalizingWaveTimer") ? tag.GetInt("revitalizingWaveTimer") : 1200;
			CleanseCooldown = tag.ContainsKey("cleanseCooldown") ? tag.GetInt("cleanseCooldown") : 0;
			LastRitesCooldown = tag.ContainsKey("lastRitesCooldown") ? tag.GetInt("lastRitesCooldown") : 0;
			lastRitesInvulnTicks = tag.ContainsKey("lastRitesInvulnTicks") ? tag.GetInt("lastRitesInvulnTicks") : 0;
			LifelineCooldown = tag.ContainsKey("lifelineCooldown") ? tag.GetInt("lifelineCooldown") : 0;
			lifelineInvulnTicks = tag.ContainsKey("lifelineInvulnTicks") ? tag.GetInt("lifelineInvulnTicks") : 0;

			RebuildOwnedCacheFromOwnedIdsOnly();

			if (tag.GetCompound("augmentCustomData") is TagCompound customData)
			{
				foreach (var a in owned)
				{
					try
					{
						if (customData.GetCompound(a.Id) is TagCompound augmentTag)
							a.LoadCustomData(augmentTag);
					}
					catch (System.Exception e)
					{
						Mod.Logger.Error($"Failed to load custom data for chip '{a.Id}'; using defaults.", e);
					}
				}
			}

			TrophyHunterKilledTypes.Clear();
			if (tag.ContainsKey("trophyHunterKilledTypes"))
				TrophyHunterKilledTypes.UnionWith(tag.GetList<int>("trophyHunterKilledTypes"));
			// Convert handles both the old Int32 save value and the current Int64 value.
			LuckyFindCopperGained = tag.ContainsKey("luckyFindCopperGained")
				? System.Convert.ToInt64(tag["luckyFindCopperGained"])
				: 0;

			// Migration: old saves stored these inside augment custom data.
			if (tag.ContainsKey("augmentCustomData"))
			{
				var legacy = tag.GetCompound("augmentCustomData");
				if (TrophyHunterKilledTypes.Count == 0 && legacy.ContainsKey("trophy_hunter"))
				{
					var t = legacy.GetCompound("trophy_hunter");
					if (t.ContainsKey("killedTypes"))
						TrophyHunterKilledTypes.UnionWith(t.GetList<int>("killedTypes"));
				}
				if (LuckyFindCopperGained == 0 && legacy.ContainsKey("lucky_find"))
				{
					var t = legacy.GetCompound("lucky_find");
					if (t.ContainsKey("copperGained"))
						LuckyFindCopperGained = t.GetInt("copperGained");
				}
			}

			BossAugmentKills.Clear();
			if (tag.ContainsKey("bossAugmentKills"))
			{
				TagCompound bossKills = tag.GetCompound("bossAugmentKills");
				foreach (var kv in bossKills)
				{
					if (int.TryParse(kv.Key, out int npcType))
						BossAugmentKills[npcType] = System.Convert.ToInt32(kv.Value);
				}
			}

			// Saves predate Keystone tracking too - backfill from whatever
			// Keystone is currently owned so a save made before this feature
			// existed still correctly locks out that Keystone's siblings.
			foreach (var a in owned)
			{
				if (a.KeystoneFamily != null)
					lockedKeystoneFamilies.Add(a.KeystoneFamily);
			}

		}
	}
}
