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
	public partial class AugmentPlayer : ModPlayer
	{
		public const int MaxOwnedAugments = 5;

		private readonly HashSet<string> ownedIds = new HashSet<string>();
		private readonly HashSet<string> everOwnedIds = new HashSet<string>();
		private readonly HashSet<string> soldAugmentIds = new HashSet<string>();
		private readonly HashSet<string> lockedKeystoneFamilies = new HashSet<string>();
		private readonly List<Augment> owned = new List<Augment>();
		public readonly HashSet<string> SeenAdvisoryTriggers = new HashSet<string>();
		public float spawnTipDelay = 2.5f;

		public IReadOnlySet<string> OwnedIds => ownedIds;
		public IReadOnlySet<string> EverOwnedIds => everOwnedIds;
		public IReadOnlySet<string> SoldAugmentIds => soldAugmentIds;
		public IReadOnlySet<string> LockedKeystoneFamilies => lockedKeystoneFamilies;
		public IReadOnlyList<Augment> Owned => owned;

		// How many times each boss type has granted (or attempted to grant)
		// augment selection to this player. Persisted across sessions.
		// Key = NPC.type, value = attempt count (increments even on failed rolls).
		public Dictionary<int, int> BossAugmentKills = new Dictionary<int, int>();

		// Boss types the player has dealt damage to during the current play session.
		// Session-only — resets on world enter. Used for multiplayer participation
		// checks (skipped in singleplayer; see TODO in BossAugmentDrop).
		public HashSet<int> DamagedBossesThisFight = new HashSet<int>();

		private int lastSyncedLifeMax2 = -1;

		// Keystone families with one member already chosen permanently exclude
		// every sibling in that family from RollChoices once set.

		public bool HasAugment(string id)
		{
			return id != null && ownedIds.Contains(id);
		}

		// Main.NewText broadcasts to every connected client when netMode==Server
		// (a genuine dedicated server, or a "Host & Play" host) - only correct
		// for singleplayer. Player-specific notifications need the targeted
		// ChatHelper path instead, same pattern already established in
		// AugmentRewardLogic. Every caller below has already ruled out
		// MultiplayerClient by this point, so only SinglePlayer/Server remain.
		private void NotifyPlayer(string message, Color color)
		{
			if (Main.netMode == NetmodeID.Server)
				ChatHelper.SendChatMessageToClient(NetworkText.FromLiteral(message), color, Player.whoAmI);
			else
				Main.NewText(message, color);
		}

		public bool GrantAugmentByIdServerAuthoritative(string id, bool sync = true, bool ignoreCaps = false)
		{
			if (Main.netMode == NetmodeID.MultiplayerClient)
				return false;

			Augment augment = AugmentDatabase.GetById(id);
			if (augment == null || ownedIds.Contains(id))
				return false;
			if (!ignoreCaps && (soldAugmentIds.Contains(id) || ownedIds.Count >= MaxOwnedAugments))
				return false;

			soldAugmentIds.Remove(id);
			ownedIds.Add(id);
			everOwnedIds.Add(id);
			if (augment.KeystoneFamily != null)
				lockedKeystoneFamilies.Add(augment.KeystoneFamily);
			if (id == "revitalizing_wave")
				RevitalizingWaveTimer = 1200;
			else if (id == "cleanse")
				CleanseCooldown = 0;

			RebuildOwnedCacheFromOwnedIdsOnly();
			augment.OnAcquire(Player);

			NotifyPlayer($"Plug-in Chip installed: {augment.DisplayName}", new Color(255, 215, 0));

			if (sync && Main.netMode == NetmodeID.Server)
				AugmentNet.SendSyncPlayer(Player);
			return true;
		}

		public bool RemoveAugmentByIdServerAuthoritative(string id, bool sync = true)
		{
			if (Main.netMode == NetmodeID.MultiplayerClient || !ownedIds.Remove(id))
				return false;

			Augment augment = AugmentDatabase.GetById(id);
			RebuildOwnedCacheFromOwnedIdsOnly();

			if (augment != null)
				NotifyPlayer($"Plug-in Chip uninstalled: {augment.DisplayName}", new Color(255, 140, 140));

			if (sync && Main.netMode == NetmodeID.Server)
				AugmentNet.SendSyncPlayer(Player);
			return true;
		}

		public bool SellAugmentByIdServerAuthoritative(string id, bool sync = true)
		{
			if (Main.netMode == NetmodeID.MultiplayerClient)
				return false;

			// Permanent augments (e.g. Avatar of Rage/Balance/the Wall) can
			// never be sold/removed once acquired - checked before removing
			// from ownedIds so a permanent augment is never even briefly
			// pulled out of the owned set.
			Augment preCheck = AugmentDatabase.GetById(id);
			if (preCheck != null && preCheck.IsPermanent)
				return false;

			if (!ownedIds.Remove(id))
				return false;

			Augment augment = AugmentDatabase.GetById(id);
			if (augment == null)
			{
				ownedIds.Add(id);
				return false;
			}

			soldAugmentIds.Add(id);
			everOwnedIds.Add(id);
			RebuildOwnedCacheFromOwnedIdsOnly();

			int refund = GetRemoveRefund(augment.Rarity);
			if (refund > 0)
				SpawnEssenceRefund(refund);

			if (sync && Main.netMode == NetmodeID.Server)
				AugmentNet.SendSyncPlayer(Player);
			return true;
		}

		public bool BuyBackSoldAugmentByIdServerAuthoritative(string id, bool sync = true)
		{
			if (Main.netMode == NetmodeID.MultiplayerClient || !soldAugmentIds.Contains(id) || ownedIds.Count >= MaxOwnedAugments)
				return false;

			Augment augment = AugmentDatabase.GetById(id);
			if (augment == null)
				return false;

			int cost = GetBuyBackCost(augment.Rarity);
			int essenceType = ModContent.ItemType<AugmentEssenceItem>();
			if (Player.CountItem(essenceType, cost) < cost)
				return false;

			for (int i = 0; i < cost; i++)
				Player.ConsumeItem(essenceType);

			soldAugmentIds.Remove(id);
			ownedIds.Add(id);
			everOwnedIds.Add(id);
			RebuildOwnedCacheFromOwnedIdsOnly();
			augment.OnAcquire(Player);

			if (Main.netMode == NetmodeID.Server)
				SyncInventory();

			if (sync && Main.netMode == NetmodeID.Server)
				AugmentNet.SendSyncPlayer(Player);
			return true;
		}

		public void ChooseReward(Augment augment)
		{
			if (augment == null)
				return;

			if (Main.netMode == NetmodeID.MultiplayerClient)
			{
				AugmentNet.SendChooseReward(augment.Id);
				return;
			}

			ApplyRewardAugment(augment);
		}

		public bool ApplyRewardAugment(Augment augment, bool sync = true)
		{
			if (Main.netMode == NetmodeID.MultiplayerClient || augment == null || HasAugment(augment.Id) || soldAugmentIds.Contains(augment.Id))
				return false;

			if (ownedIds.Count >= MaxOwnedAugments)
			{
				everOwnedIds.Add(augment.Id);
				soldAugmentIds.Add(augment.Id);

				int refund = GetRewardRefund(augment.Rarity);
				if (refund > 0)
					SpawnEssenceRefund(refund);

				string msg = $"✦ [Slots Full] {augment.DisplayName} transferred to Mistress 2B's archive! Received {refund} Machine Core{(refund > 1 ? "s" : "")}. ✦";
				if (Main.netMode == NetmodeID.Server)
				{
					ChatHelper.SendChatMessageToClient(NetworkText.FromLiteral(msg), new Color(255, 140, 90), Player.whoAmI);
					if (sync)
						AugmentNet.SendSyncPlayer(Player);
				}
				else if (Main.netMode == NetmodeID.SinglePlayer)
				{
					Main.NewText(msg, 255, 140, 90);
				}

				return true;
			}

			return GrantAugmentByIdServerAuthoritative(augment.Id, sync);
		}

		public static int GetRewardRefund(AugmentRarity rarity)
		{
			switch (rarity)
			{
				case AugmentRarity.Common:
					return 1;
				case AugmentRarity.Rare:
					return 2;
				case AugmentRarity.Epic:
					return 3;
				case AugmentRarity.Legendary:
					return 4;
				default:
					return 1;
			}
		}

		private void SpawnEssenceRefund(int amount)
		{
			if (amount <= 0 || Main.netMode == NetmodeID.MultiplayerClient)
				return;

			Player.QuickSpawnItem(Player.GetSource_FromThis(), ModContent.ItemType<AugmentEssenceItem>(), amount);
		}

		public static int GetRemoveRefund(AugmentRarity rarity)
		{
			return rarity switch
			{
				AugmentRarity.Epic => 1,
				AugmentRarity.Legendary => 2,
				_ => 0
			};
		}

		public static int GetBuyBackCost(AugmentRarity rarity)
		{
			return rarity switch
			{
				AugmentRarity.Epic => 2,
				AugmentRarity.Legendary => 4,
				_ => 1
			};
		}

		// Live-summed, not separately saved - every owned augment's
		// FortuneBonus stacks additively into one shared Luck stat.
		public float TotalFortune
		{
			get
			{
				float total = Owned.Sum(a => a.FortuneBonus);
				int fortuneOwned = AugmentFamilyRegistry.GetOwnedCount(this, AugmentFamilyRegistry.FortuneId);
				if (fortuneOwned >= 4)
					total = (total + 0.10f) * 1.45f;
				else if (fortuneOwned >= 2)
					total = (total + 0.05f) * 1.20f;
				return total;
			}
		}

		// Live count of Support-class augments owned. Player is "in Support
		// stance" when this is >= 2, which applies a damage penalty and defense
		// bonus that both scale with the count (see UpdateEquips below).
		public int SupportAugmentCount => Owned.Count(a => a.Class == AugmentClass.Support);

		// Shared pixel radius for all pull-based Support auras.
		private const float AuraRadius = 600f;

		// Set true each UpdateEquips tick when the Ironclad Aura defense bonus
		// was applied from a nearby Support player. Read by AugmentCooldownDrawer
		// to show the status icon. Reset to false at the top of each UpdateEquips.
		public bool ReceivedIroncladAura;

		// Per-frame tracking for other pull-based aura effects.
		// Reset and re-evaluated each frame; used to prevent double-application
		// when two Support players both own the same aura augment.
		public bool ReceivedSwiftnessAura;
		public bool ReceivedManaWell;
		public bool ReceivedCombatMedic;
		public int RevitalizingWaveTimer = 1200;
		public int CleanseCooldown;

		// Per-player cooldowns for triggered Support auras (Last Rites, Lifeline).
		// Volatile — not saved to disk. 90s = 5400 ticks at 60 ticks/sec.
		public int LastRitesCooldown;
		private int lastRitesInvulnTicks;
		public int LifelineCooldown;
		private int lifelineInvulnTicks;
		private bool lifelineProtectionAuthorized;
		private bool undyingBondRedirectSent;
		private int undyingBondRequestTimer;
		private bool undyingBondNeedsTeleport;
		private int mendingAuraHealTimer;
		private int vitalEchoLastLife = -1;
		private int vitalEchoDefenseTicks;
		private int soulLinkRequestCooldown;

		// Per-player state for augments that previously stored data on the singleton.
		// Moved here so multiple players in multiplayer don't share the same counter.
		public HashSet<int> TrophyHunterKilledTypes = new HashSet<int>();
		public long LuckyFindCopperGained;

		// Per-player combat/timer state for the remaining augments that previously
		// kept these on their singleton instances in AugmentDatabase.All (shared by
		// every player — the same multiplayer state-corruption bug as TrophyHunter/
		// LuckyFind above). All volatile session state: intentionally NOT persisted,
		// matching the old singleton behavior which reset on every mod reload
		// (RavenousSwarm's slot bonus is documented session-only by design).
		public int AdaptiveArmorUndamagedTimer;
		public int AdaptiveArmorDefenseBonus;
		public int AmbushStillTicks;
		public bool AmbushReady;
		public float ApexHunterMarkStacks;
		public int ApexHunterCooldown;
		public float ArcaneSingularityCharge;
		public int AvatarOfRageLastLife = -1;
		public int BastionStoredDamage;
		public int BastionBarrierTicks;
		public int SynchronizerTimer;
		public int BulwarkNoDamageTicks;
		public int DeadeyeLastTargetWhoAmI = -1;
		public float DeadeyeHitStacks;
		internal readonly List<EchoChamberAugment.PendingEcho> EchoChamberPendingEchoes = new List<EchoChamberAugment.PendingEcho>();
		public int EldritchCovenantCorruption;
		public int EldritchCovenantManaProgress;
		public int EternalFlameBoostTicks;
		public int FeatherfallSpeedBurstTicks;
		public int FinalStandCooldown;
		public int FinalStandActiveTicks;
		public int FortunesFavorRegenTimer;
		public int FrenziedAssaultStacks;
		public int FrenziedAssaultResetTimer;
		public int GetExcitedTimer;
		public int GetExcitedStacks;
		public int GodslayerBladeCooldown;
		public bool GrappleMasterWasGrappling;
		public int GrappleMasterSpeedBurstTicks;
		public int HuntersPaceSpeedTimer;
		public float HuntersPaceCurrentBonus;
		public int InfernosHeartChargeStacks;
		public int InfernosHeartResetTimer;
		public float IronRhythmHitCounter;
		public int IronRhythmPendingSpecialDamage;
		public int IronWillDurationRemaining;
		public int IronWillCooldown;
		public int LastStandCooldown;
		public bool LastStandArmedThisHit;
		public int MinionMomentumHitStacks;
		public int MinionMomentumActiveMinionProjType = -1;
		public int MirrorImageInvulnTicks;
		public int KineticShockwaveCooldown;
		public int KineticDashWindowMemoryTimer;
		public int CryoShatterCooldown;
		public int FortuneCoinBurstCooldown;
		public int ChainLightningCooldown;
		public int KineticShrapnelCooldown;
		public int AstralMatrixManaSpent;
		public int AstralMatrixStacks;
		public int GunslingerContinuousFireTicks;
		public int GunslingerLingerTimer;
		public bool GunslingerWasAtPeak;
		public int CurrentHitOnHitDamage;

		public void RecordOnHitDamage(int damage)
		{
			CurrentHitOnHitDamage += damage;
		}
		public float MomentumCrashPreviousSpeed;
		public int MomentumCrashDashWindowTimer;
		public bool MomentumCrashPendingConfuse;
		public int MomentumSwingLastTargetWhoAmI = -1;
		public int MomentumSwingStacks;
		public int MomentumSwingResetTimer;
		public float OverchargeRoundHitStacks;
		public int OverchargeRoundResetTimer;
		public int OverwhelmLastTargetWhoAmI = -1;
		public int OverwhelmHitCounter;
		public int OverwhelmResetTimer;
		public bool PhoenixHeartArmedThisHit;
		public int PhoenixHeartCooldown;
		public int PhoenixHeartInvulnTicks;
		public int PiedPiperCooldown;
		public int PiedPiperDurationRemaining;
		public int PotionRushTimer;
		public int QuickRecoveryRegenTimer;
		public int RavenousSwarmSlotsGranted;
		// One-slot "undo my last reforge" record for Reforger's Patience.
		// Item is a live reference into the player's own inventory - not
		// meaningful across a save/reload, so intentionally left out of
		// SaveData/LoadData, same as every other volatile field on this list.
		// LastReforgePendingCost is a scratch value bridging ReforgePrice
		// (where the discounted price is computed) to PreReforge (where the
		// old prefix is captured) within the same synchronous reforge call.
		public Item LastReforgedItem;
		public int LastReforgePrefix = -1;
		public int LastReforgeCost;
		public int LastReforgePendingCost;
		public int RiposteWindowRemaining;
		public int ScavengersLuckBuffTicks;
		public int SecondWindCooldown;
		public float SteadyHandsCurrentCritBonus;
		public int SteadyHandsRampTicks;
		public bool TwinStrikeItemProcPending;
		public bool TwinStrikeProjProcPending;
		public int VoidStepKillStacks;
		public int VoidStepResetTimer;
		public int VoidStepInvulnTicks;
		public int WarGodsTempoStacks;
		public int WarGodsTempoResetTimer;
		public int WildCardSpeedTicks;
		public int WildCardInvulnTicks;

		// Snapshot taken by CopyClientState each tick for change detection.
		// Only used by SendClientChanges; not persisted and never read by game logic.
		private HashSet<string> syncedOwnedIds = new HashSet<string>();

		public bool LifelineProtectionAuthorized => lifelineProtectionAuthorized;
		public bool HasVitalEchoDefense => vitalEchoDefenseTicks > 0;

		// Picks up to `count` random augments of the given rarity that the
		// player doesn't already own, no repeats. With any TotalFortune,
		// each slot independently gets a 15% chance to specifically try for
		// a lucky-themed pick instead of a fully random one, falling back to
		// the normal random pick if no eligible lucky-themed augment exists.
		private const float LuckyThemedBiasChance = 0.15f;
		private const float ProtocolPartnerBiasChance = 0.20f;

		public List<Augment> GetMissingProtocolPartners()
		{
			var list = new List<Augment>();
			foreach (var fam in AugmentFamilyRegistry.Families.Values)
			{
				int owned = AugmentFamilyRegistry.GetOwnedCount(this, fam.Id);
				if (owned > 0 && owned < fam.MaxMembers)
				{
					foreach (var memberId in fam.MemberIds)
					{
						if (!HasAugment(memberId) && !soldAugmentIds.Contains(memberId))
						{
							var aug = AugmentDatabase.GetById(memberId);
							if (aug != null && aug.Class != AugmentClass.Support)
								list.Add(aug);
						}
					}
				}
			}
			return list;
		}

		public List<Augment> RollChoices(int count, AugmentRarity rarity, IReadOnlySet<string> excludedIds = null)
		{
			var available = new List<Augment>();
			foreach (var augment in AugmentDatabase.All)
			{
				if (augment.Rarity != rarity || augment.IsDebugOnly || HasAugment(augment.Id) || (excludedIds != null && excludedIds.Contains(augment.Id)))
					continue;
				if (soldAugmentIds.Contains(augment.Id))
					continue;

				// Support class temporarily disabled from the reward pool - the
				// cross-player packet flow (Soul Link, Lifeline, Undying Bond,
				// Revitalizing Wave) is not working correctly in multiplayer yet.
				// Remove this check once the root cause is found and fixed.
				if (augment.Class == AugmentClass.Support)
					continue;

				// Keystones never appear through this normal per-slot roll at
				// all, locked or not - the only way one is ever offered is the
				// separate whole-family check in AugmentRewardLogic.GrantReward,
				// which presents all of a family's members together as a set.
				if (augment.KeystoneFamily != null)
					continue;

				available.Add(augment);
			}

			var missingPartners = GetMissingProtocolPartners();
			var missingPartnerIds = new HashSet<string>();
			foreach (var p in missingPartners)
				missingPartnerIds.Add(p.Id);

			bool hasFortune = TotalFortune > 0f;

			var picks = new List<Augment>();
			while (picks.Count < count && available.Count > 0)
			{
				int index = -1;

				// Protocol Completion Bias: 20% chance to prioritize offering a missing partner chip
				// to complete an active 1/2 protocol duo
				if (missingPartnerIds.Count > 0 && Main.rand.NextFloat() < ProtocolPartnerBiasChance)
				{
					var partnerIndices = new List<int>();
					for (int i = 0; i < available.Count; i++)
					{
						if (missingPartnerIds.Contains(available[i].Id))
							partnerIndices.Add(i);
					}

					if (partnerIndices.Count > 0)
						index = partnerIndices[Main.rand.Next(partnerIndices.Count)];
				}

				if (index < 0 && hasFortune && Main.rand.NextFloat() < LuckyThemedBiasChance)
				{
					var luckyIndices = new List<int>();
					for (int i = 0; i < available.Count; i++)
					{
						if (available[i].IsLuckyThemed)
							luckyIndices.Add(i);
					}

					if (luckyIndices.Count > 0)
						index = luckyIndices[Main.rand.Next(luckyIndices.Count)];
				}

				if (index < 0)
					index = Main.rand.Next(available.Count);

				picks.Add(available[index]);
				available.RemoveAt(index);
			}
			return picks;
		}

		public override void PostUpdate()
		{
			// Enforce statLifeMax2 >= statLifeMax for all active players on clients so health bars and checks never read stale defaults
			if (Main.netMode != NetmodeID.Server)
			{
				for (int i = 0; i < Main.maxPlayers; i++)
				{
					Player p = Main.player[i];
					if (p.active && p.statLifeMax2 < p.statLifeMax)
					{
						p.statLifeMax2 = p.statLifeMax;
					}
				}
			}

			// Broadcast local statLifeMax2 to server/other clients when it changes
			if (Player.whoAmI == Main.myPlayer && Main.netMode == NetmodeID.MultiplayerClient)
			{
				if (Player.statLifeMax2 != lastSyncedLifeMax2)
				{
					lastSyncedLifeMax2 = Player.statLifeMax2;
					ModPacket packet = ModContent.GetInstance<Augments>().GetPacket();
					packet.Write((byte)AugmentPacketType.SyncPlayerLifeMax);
					packet.Write((byte)Player.whoAmI);
					packet.Write(Player.statLifeMax2);
					packet.Send();
				}
			}

			if (Player.whoAmI == Main.myPlayer)
			{
				AugmentDamageTracker.Update(1f / 60f);

				if (spawnTipDelay > 0f)
				{
					spawnTipDelay -= 1f / 60f;
					if (spawnTipDelay <= 0f)
					{
						AugmentAdvisoryHUD.TriggerSmartAdvisory(this, "first_spawn", "[POD 042]", "Welcome. You can press [P] anytime to open your Plugins Menu and configure your installed chips.");
					}
				}

				if (Player.CountItem(ModContent.ItemType<AugmentEssenceItem>()) > 0)
				{
					AugmentAdvisoryHUD.TriggerSmartAdvisory(this, "first_core", "[POD 042]", "Machine Core acquired. You can use these cores to purchase, upgrade, and reroll combat plugins with Vendor 2B.");
				}

				if (!SeenAdvisoryTriggers.Contains("first_protocol"))
				{
					foreach (var fid in AugmentFamilyRegistry.Families.Keys)
					{
						if (AugmentFamilyRegistry.GetOwnedCount(this, fid) >= 2)
						{
							AugmentAdvisoryHUD.TriggerSmartAdvisory(this, "first_protocol", "[POD 042]", "Protocol synergy activated. Hover over the icons docked on the right side of your screen to inspect your unlocked bonus perks.");
							break;
						}
					}
				}

				if (SupportAugmentCount >= 2)
				{
					AugmentAdvisoryHUD.TriggerSmartAdvisory(this, "first_support", "[POD 042]", "Support Class activated. Your armor has surged with bonus defense, but your personal weapon damage is reduced. Check your Support icon on the right for tier details.");
				}
			}

			foreach (var a in Owned)
				a.OnUpdate(Player);

			if (KineticShockwaveCooldown > 0)
				KineticShockwaveCooldown--;
			if (KineticDashWindowMemoryTimer > 0)
				KineticDashWindowMemoryTimer--;
			if (CryoShatterCooldown > 0)
				CryoShatterCooldown--;
			if (FortuneCoinBurstCooldown > 0)
				FortuneCoinBurstCooldown--;
			if (ChainLightningCooldown > 0)
				ChainLightningCooldown--;
			if (KineticShrapnelCooldown > 0)
				KineticShrapnelCooldown--;

			if (AstralMatrixStacks > 0 && AugmentFamilyRegistry.GetOwnedCount(this, AugmentFamilyRegistry.ArcaneSurgeId) >= 2)
			{
				float baseAngle = (float)Main.timeForVisualEffects * 0.05f;
				for (int i = 0; i < AstralMatrixStacks; i++)
				{
					float angle = baseAngle + (i * MathHelper.TwoPi / AstralMatrixStacks);
					Vector2 offset = angle.ToRotationVector2() * 30f;
					if (Main.rand.NextBool(3))
					{
						Dust d = Dust.NewDustPerfect(
							Player.Center + offset,
							DustID.DungeonSpirit,
							Vector2.Zero,
							120,
							new Color(129, 140, 248),
							0.85f
						);
						d.noGravity = true;
					}
				}
			}

			// Gunslinger Protocol (2-Piece Synergy): Track continuous ranged firing
			if (AugmentFamilyRegistry.GetOwnedCount(this, AugmentFamilyRegistry.GunslingerId) >= 2)
			{
				bool isFiringRanged = (Player.itemAnimation > 0 || (Player.channel && Player.controlUseItem)) &&
				                      Player.HeldItem != null && !Player.HeldItem.IsAir &&
				                      Player.HeldItem.CountsAsClass(DamageClass.Ranged) &&
				                      Player.HeldItem.damage > 0 &&
				                      Player.HeldItem.useStyle != ItemUseStyleID.None;

				if (isFiringRanged)
				{
					if (GunslingerContinuousFireTicks < 240)
					{
						GunslingerContinuousFireTicks++;
						if (GunslingerContinuousFireTicks >= 240)
						{
							// Reached peak spin-up!
							if (!GunslingerWasAtPeak)
							{
								GunslingerWasAtPeak = true;
								if (Player.whoAmI == Main.myPlayer)
								{
									SoundEngine.PlaySound(SoundID.Item149 with { Pitch = 0.2f, Volume = 0.65f }, Player.Center);
								}

								for (int d = 0; d < 16; d++)
								{
									Vector2 sparkVel = Main.rand.NextVector2Circular(4f, 4f);
									Dust dust = Dust.NewDustPerfect(Player.Center, DustID.GoldFlame, sparkVel, 0, default, 1.2f);
									dust.noGravity = true;
								}
							}
						}
					}

					GunslingerLingerTimer = 60; // 1s grace window

					// Ambient muzzle heat sparks while at peak
					if (GunslingerContinuousFireTicks >= 240 && Main.rand.NextBool(3))
					{
						Vector2 muzzlePos = Player.MountedCenter + new Vector2(Player.direction * 14f, -4f);
						Dust d = Dust.NewDustPerfect(muzzlePos, DustID.GoldFlame, new Vector2(Player.direction * Main.rand.NextFloat(1f, 3f), Main.rand.NextFloat(-1f, 1f)), 0, default, 0.9f);
						d.noGravity = true;
					}
				}
				else
				{
					if (GunslingerLingerTimer > 0)
					{
						GunslingerLingerTimer--;
						if (GunslingerLingerTimer == 0)
						{
							GunslingerContinuousFireTicks = 0;
							GunslingerWasAtPeak = false;
						}
					}
				}
			}
			else
			{
				GunslingerContinuousFireTicks = 0;
				GunslingerLingerTimer = 0;
				GunslingerWasAtPeak = false;
			}

			if (MomentumCrashDashWindowTimer > 0 || Player.dashDelay < 0)
				KineticDashWindowMemoryTimer = 15;

			UpdateSupportAuthorityState();

			if (undyingBondNeedsTeleport && Player.whoAmI == Main.myPlayer)
			{
				undyingBondNeedsTeleport = false;
				if (SupportEffects.TryFindSupportOwner(Player, "undying_bond", -1f, out Player owner))
				{
					Vector2 targetPos = new Vector2(owner.Center.X - Player.width / 2f, owner.Center.Y - Player.height / 2f);
					Player.Teleport(targetPos, 1);
					Player.velocity = Vector2.Zero;
					SoundEngine.PlaySound(SoundID.Item6, Player.Center);

					if (Main.netMode == NetmodeID.MultiplayerClient)
					{
						NetMessage.SendData(MessageID.TeleportEntity, -1, -1, null, 0, Player.whoAmI, targetPos.X, targetPos.Y, 1);
					}
				}
			}

			// Keep the Support Class buff active while any Support augment is owned.
			// Short duration refreshed every tick — expires within 3 frames if removed.
			if (SupportAugmentCount >= 1)
				Player.AddBuff(ModContent.BuffType<SupportClassBuff>(), 3);

			// Pull-based Warcry aura: each player checks nearby Support players
			// for the "warcry" augment and self-applies the buff. This is
			// multiplayer-safe because every client only modifies their own player.
			for (int i = 0; i < Main.maxPlayers; i++)
			{
				Player other = Main.player[i];
				if (!SupportEffects.IsAllyInRange(other, Player, AuraRadius))
					continue;
				var otherAP = other.GetModPlayer<AugmentPlayer>();
				if (!otherAP.HasAugment("warcry"))
					continue;
				// 10-tick duration = refreshed each tick while in range, falls off
				// within 10 ticks (~0.17s) of the Support player leaving range.
				Player.AddBuff(ModContent.BuffType<WarCryBuff>(), 10);
				break;
			}

			// Owner also benefits from their own Warcry - the pull loop skips
			// self, so this handles the Support player's own application.
			if (HasAugment("warcry"))
				Player.AddBuff(ModContent.BuffType<WarCryBuff>(), 10);

			// Tick down triggered-aura cooldowns and re-assert invulnerability
			// each frame while it's active. Re-assertion is required because vanilla
			// overwrites player.immuneTime with its own short post-hit window every
			// tick — a one-shot set doesn't survive. See PhoenixHeartAugment for detail.
			if (lastRitesInvulnTicks > 0)
			{
				Player.immune = true;
				Player.immuneTime = lastRitesInvulnTicks;
				lastRitesInvulnTicks--;
			}

			if (lifelineInvulnTicks > 0)
			{
				Player.immune = true;
				Player.immuneTime = lifelineInvulnTicks;
				lifelineInvulnTicks--;
			}
		}

		public override void ModifyLuck(ref float luck)
		{
			if (TotalFortune > 0f)
				luck += TotalFortune * 0.5f;

			// Fortune Protocol Tiered Bonuses (2, 4, 5 pieces)
			int fortuneOwned = AugmentFamilyRegistry.GetOwnedCount(this, AugmentFamilyRegistry.FortuneId);
			if (fortuneOwned >= 5)
				luck += 0.50f;
			else if (fortuneOwned >= 4)
				luck += 0.35f;
			else if (fortuneOwned >= 2)
				luck += 0.15f;
		}

		private void UpdateSupportAuthorityState()
		{
			if (CleanseCooldown > 0)
				CleanseCooldown--;
			if (LastRitesCooldown > 0)
				LastRitesCooldown--;
			if (LifelineCooldown > 0)
				LifelineCooldown--;
			if (soulLinkRequestCooldown > 0)
				soulLinkRequestCooldown--;

			if (HasAugment("revitalizing_wave"))
			{
				if (RevitalizingWaveTimer > 0)
					RevitalizingWaveTimer--;

				if (RevitalizingWaveTimer == 0)
				{
					if (Player.whoAmI == Main.myPlayer)
					{
						RevitalizingWaveTimer = 1200;
						RevitalizingWaveAugment.SpawnBurst(Player);

						if (Main.netMode == NetmodeID.SinglePlayer)
						{
							foreach (Player target in Main.player)
							{
								if (SupportEffects.IsAllyInRange(Player, target, SupportEffects.AuraRadius))
									SupportEffects.ServerHealPlayer(target, 25);
							}
						}
						else if (Main.netMode == NetmodeID.MultiplayerClient)
						{
							ModPacket packet = ModContent.GetInstance<Augments>().GetPacket();
							packet.Write((byte)AugmentPacketType.RevitalizingWaveRequest);
							packet.Send();
						}
					}
					else if (Main.netMode == NetmodeID.Server)
					{
						RevitalizingWaveTimer = 1200;
					}
				}
			}

			// Self-only detection (own HP threshold, own nearby-owner scan feeding
			// lifelineProtectionAuthorized below) - no netmode guard needed, same
			// reasoning as Mending Aura. The internal Server checks further down
			// still correctly gate only the network broadcast to OTHER clients;
			// the server independently re-validates authorization from scratch
			// before ever touching Player.dead/statLife (see
			// HandleLifelineRequest/TryConsumeLifelineServer), so a client
			// locally computing this flag early carries no exploit risk.
			if (Player.statLife <= (int)(Player.statLifeMax2 * 0.20f) && LastRitesCooldown == 0 &&
				SupportEffects.TryFindSupportOwner(Player, "last_rites", AuraRadius, out Player lrOwner, includeSelf: true))
			{
				LastRitesCooldown = 5400;
				lastRitesInvulnTicks = 180;
				Player.AddBuff(ModContent.BuffType<LastRitesCooldownBuff>(), 5400);

				var lrOwnerAP = lrOwner.GetModPlayer<AugmentPlayer>();
				lrOwnerAP.LastRitesCooldown = 5400;
				lrOwnerAP.Player.AddBuff(ModContent.BuffType<LastRitesCooldownBuff>(), 5400);

				if (Main.netMode == NetmodeID.Server)
				{
					NetMessage.SendData(MessageID.PlayerBuffs, -1, -1, null, Player.whoAmI);
					AugmentNet.SendSyncPlayer(Player);

					if (lrOwner.whoAmI != Player.whoAmI)
					{
						NetMessage.SendData(MessageID.PlayerBuffs, -1, -1, null, lrOwner.whoAmI);
						AugmentNet.SendSyncPlayer(lrOwner);
					}
				}
			}

			bool protection = LifelineCooldown == 0 && SupportEffects.TryFindSupportOwner(Player, "lifeline", AuraRadius, out _, includeSelf: true);
			if (protection != lifelineProtectionAuthorized)
			{
				lifelineProtectionAuthorized = protection;
				if (Main.netMode == NetmodeID.Server)
					AugmentNet.SendSyncPlayer(Player);
			}
		}

		// True while a valid, still-undoable reforge record exists - read by
		// AugmentShopUIState to decide whether to show/enable the Undo
		// Reforge row. Runs the same staleness check TryUndoLastReforge
		// itself relies on, but without consuming or clearing the record.
		public bool HasPendingReforgeUndo => LastReforgedItem != null && LastReforgePrefix >= 0 && IsStillInInventory(LastReforgedItem);

		private bool IsStillInInventory(Item item)
		{
			foreach (Item invItem in Player.inventory)
			{
				if (invItem == item)
					return true;
			}

			return false;
		}

		// Reforging itself is entirely client-local in vanilla (there's no
		// request/response packet for it, and PreReforge/PostReforge just run
		// straight off the local DrawInventory click) - undo mirrors that same
		// shape, touching only the local player's own inventory item and
		// their own coins, no server round-trip needed.
		public bool TryUndoLastReforge()
		{
			if (LastReforgedItem == null || LastReforgePrefix < 0)
				return false;

			Item item = LastReforgedItem;

			// Guard against undoing a stale record: the item must still be
			// sitting in this player's own inventory, unchanged since the
			// reforge that produced this record (a different reforge, a sold/
			// dropped item, or a swapped-in different item all invalidate it).
			if (!IsStillInInventory(item))
			{
				ClearLastReforgeRecord();
				return false;
			}

			// Item.Prefix(int) doesn't just overwrite the prefix field, it
			// also reapplies that prefix's stat multipliers (damage, crit,
			// etc.) - a raw `item.prefix = x` assignment would leave the
			// current (new) prefix's stat bonuses baked into the item while
			// only the displayed prefix number changed. ResetPrefix() first
			// strips those before Prefix() reapplies the restored one,
			// exactly mirroring vanilla's own reforge call sequence.
			item.ResetPrefix();
			item.Prefix(LastReforgePrefix);

			int cost = LastReforgeCost;
			if (cost > 0)
			{
				int[] coins = Utils.CoinsSplit(cost);
				if (coins[0] > 0)
					Player.QuickSpawnItem(Player.GetSource_FromThis(), ItemID.CopperCoin, coins[0]);
				if (coins[1] > 0)
					Player.QuickSpawnItem(Player.GetSource_FromThis(), ItemID.SilverCoin, coins[1]);
				if (coins[2] > 0)
					Player.QuickSpawnItem(Player.GetSource_FromThis(), ItemID.GoldCoin, coins[2]);
				if (coins[3] > 0)
					Player.QuickSpawnItem(Player.GetSource_FromThis(), ItemID.PlatinumCoin, coins[3]);
			}

			NotifyPlayer("Reforge undone - previous prefix and cost restored.", new Color(180, 220, 255));
			ClearLastReforgeRecord();
			return true;
		}

		private void ClearLastReforgeRecord()
		{
			LastReforgedItem = null;
			LastReforgePrefix = -1;
			LastReforgeCost = 0;
		}

		public bool TryTriggerCleanseServer()
		{
			if (Main.netMode == NetmodeID.MultiplayerClient || !HasAugment("cleanse") || CleanseCooldown > 0 || Player.dead)
				return false;

			CleanseCooldown = 1800;
			foreach (Player target in Main.player)
			{
				if (SupportEffects.IsAllyInRange(Player, target, SupportEffects.AuraRadius, includeOwner: true))
					SupportEffects.ServerClearDebuffs(target);
			}

			if (Main.netMode == NetmodeID.Server)
				AugmentNet.SendSyncPlayer(Player);
			return true;
		}

		public bool TryAuthorizeSoulLinkRequest()
		{
			if (Main.netMode != NetmodeID.Server || !HasAugment("soul_link") || Player.dead || soulLinkRequestCooldown > 0)
				return false;

			soulLinkRequestCooldown = 30;
			return true;
		}

		public bool TryConsumeLifelineServer()
		{
			// Note: PreKill already diverts MultiplayerClient callers to the
			// packet-request branch above and never reaches this method in that
			// case, so this guard was already inert for that call path - kept
			// removed anyway for consistency with the rest of the "self-only
			// effects don't need this guard" cleanup.
			if (LifelineCooldown > 0 ||
				!SupportEffects.TryFindSupportOwner(Player, "lifeline", AuraRadius, out Player owner, includeSelf: true))
				return false;

			ModContent.GetInstance<Augments>().Logger.Info($"Lifeline found support owner={owner.name}");
			LifelineCooldown = 5400;
			lifelineInvulnTicks = 120;
			lifelineProtectionAuthorized = false;
			Player.dead = false;
			Player.statLife = 1;
			Player.immune = true;
			Player.immuneTime = 120;
			Player.AddBuff(ModContent.BuffType<LifelineCooldownBuff>(), 5400);

			var ownerAP = owner.GetModPlayer<AugmentPlayer>();
			ownerAP.LifelineCooldown = 5400;
			ownerAP.Player.AddBuff(ModContent.BuffType<LifelineCooldownBuff>(), 5400);

			if (Main.netMode == NetmodeID.Server)
			{
				NetMessage.SendData(MessageID.PlayerLifeMana, -1, -1, null, Player.whoAmI);
				NetMessage.SendData(MessageID.PlayerBuffs, -1, -1, null, Player.whoAmI);
				AugmentNet.SendSyncPlayer(Player);

				if (owner.whoAmI != Player.whoAmI)
				{
					NetMessage.SendData(MessageID.PlayerBuffs, -1, -1, null, owner.whoAmI);
					AugmentNet.SendSyncPlayer(owner);
				}
			}

			ModContent.GetInstance<Augments>().Logger.Info($"Lifeline consumed and saved target={Player.name}");
			return true;
		}

		public void TickMendingAura()
		{
			// Self-only heal, same category as TickVitalEcho below - no netmode guard
			// needed here, same reasoning as the direct statLife heal at the bottom
			// of this method.
			if (Player.velocity.LengthSquared() >= 0.25f)
			{
				mendingAuraHealTimer = 0;
				return;
			}

			mendingAuraHealTimer++;
			if (mendingAuraHealTimer < 60)
				return;

			mendingAuraHealTimer = 0;

			// Self-only heal - apply directly rather than routing through
			// ServerHealPlayer, which exists for CROSS-PLAYER heals (Revitalizing
			// Wave, Soul Link) that need server authority. Same unguarded
			// statLife+HealEffect pattern every other personal heal in this mod
			// already uses (Quick Recovery, Second Wind, Swarm Tactics, etc).
			Player.statLife = System.Math.Min(Player.statLifeMax2, Player.statLife + 5);
			Player.HealEffect(5, true);
		}

		public void TickVitalEcho()
		{
			if (vitalEchoLastLife != -1 && Player.statLife > vitalEchoLastLife)
				vitalEchoDefenseTicks = 180;

			vitalEchoLastLife = Player.statLife;
			if (vitalEchoDefenseTicks > 0)
				vitalEchoDefenseTicks--;
		}

		public override void PostUpdateRunSpeeds()
		{
			ReceivedSwiftnessAura = false;

			foreach (var a in Owned)
				a.PostUpdateRunSpeeds(Player);

			// Pull-based Swiftness Aura: each player checks nearby Support players
			// for the "swiftness_aura" augment and self-applies the movement boost.
			for (int i = 0; i < Main.maxPlayers; i++)
			{
				Player other = Main.player[i];
				if (!SupportEffects.IsAllyInRange(other, Player, AuraRadius))
					continue;
				var otherAP = other.GetModPlayer<AugmentPlayer>();
				if (!otherAP.HasAugment("swiftness_aura"))
					continue;
				Player.maxRunSpeed *= 1.15f;
				Player.accRunSpeed *= 1.15f;
				Player.runAcceleration *= 1.15f;
				ReceivedSwiftnessAura = true;
				break;
			}

			// Owner also benefits from their own Swiftness Aura.
			if (!ReceivedSwiftnessAura && HasAugment("swiftness_aura"))
			{
				Player.maxRunSpeed *= 1.15f;
				Player.accRunSpeed *= 1.15f;
				Player.runAcceleration *= 1.15f;
				ReceivedSwiftnessAura = true;
			}
		}

		public override void UpdateEquips()
		{
			ReceivedIroncladAura = false;
			ReceivedManaWell = false;
			ReceivedCombatMedic = false;
			if (AugmentListUIState.IsDevCritMode)
			{
				Player.GetCritChance(DamageClass.Generic) += 1000f;
			}

			foreach (var a in Owned)
				a.UpdateEquips(Player);

			int supportCount = SupportAugmentCount;
			if (supportCount >= 2)
			{
				float damagePenalty;
				int defenseBonus;

				if      (supportCount == 2) { damagePenalty = -0.30f; defenseBonus = 20; }
				else if (supportCount == 3) { damagePenalty = -0.23f; defenseBonus = 30; }
				else if (supportCount == 4) { damagePenalty = -0.16f; defenseBonus = 40; }
				else                        { damagePenalty = -0.05f; defenseBonus = 60; }

				Player.GetDamage(DamageClass.Generic) += damagePenalty;
				Player.statDefense += defenseBonus;
			}

			// Field Medic (2-Piece Synergy): -20% Potion Sickness cooldown
			if (AugmentFamilyRegistry.GetOwnedCount(this, AugmentFamilyRegistry.FieldMedicId) >= 2)
			{
				Player.potionDelayTime = (int)(Player.potionDelayTime * 0.80f);
			}

			// Kinetic Protocol (2-Piece Synergy): Melee attack speed scales up by +1% per 2 mph of current movement speed (up to +15%)
			if (AugmentFamilyRegistry.GetOwnedCount(this, AugmentFamilyRegistry.KineticId) >= 2)
			{
				float speedMph = Player.velocity.Length() * 5.1f;
				float kineticBonus = Math.Clamp((speedMph / 2f) * 0.01f, 0f, 0.15f);
				if (kineticBonus > 0f)
				{
					Player.GetAttackSpeed(DamageClass.Melee) += kineticBonus;
				}
			}

			// Gunslinger Protocol (2-Piece Synergy): Rotary Acceleration & Lead Storm
			if (AugmentFamilyRegistry.GetOwnedCount(this, AugmentFamilyRegistry.GunslingerId) >= 2 && GunslingerContinuousFireTicks > 0)
			{
				float rampBonus = Math.Min(0.10f, GunslingerContinuousFireTicks * (0.10f / 240f));
				Player.GetAttackSpeed(DamageClass.Ranged) += rampBonus;

				// Lead Storm: +8% Crit at full spin-up
				if (GunslingerContinuousFireTicks >= 240)
				{
					Player.GetCritChance(DamageClass.Ranged) += 8f;
				}
			}

			// Type-B: Berserker Protocol: +15s Potion Sickness duration
			if (HasAugment("type_b_berserker_protocol") || HasAugment("avatar_of_rage"))
			{
				Player.potionDelayTime += 900;
			}

			// Pull-based Ironclad Aura: each player checks nearby Support players
			// for the "ironclad_aura" augment and adds the defense bonus to themselves.
			// DESIGN FLAG: if two Support players both own this augment and both stand
			// near a teammate, that teammate receives +16 defense instead of +8.
			// Current behavior (break on first match) caps it at one source.
			// Remove the break to allow stacking from multiple Support players.
			for (int i = 0; i < Main.maxPlayers; i++)
			{
				Player other = Main.player[i];
				if (!SupportEffects.IsAllyInRange(other, Player, AuraRadius))
					continue;
				var otherAP = other.GetModPlayer<AugmentPlayer>();
				if (!otherAP.HasAugment("ironclad_aura"))
					continue;
				Player.statDefense += 8;
				ReceivedIroncladAura = true;
				break;
			}

			// Owner also benefits from their own Ironclad Aura. The
			// !ReceivedIroncladAura guard prevents double-application if a
			// second Support player nearby already triggered the pull loop above.
			if (!ReceivedIroncladAura && HasAugment("ironclad_aura"))
			{
				Player.statDefense += 8;
				ReceivedIroncladAura = true;
			}

			// Pull-based Mana Well: advance the mana regen timer by 30% each frame
			// when a nearby Support player owns the augment, making mana regenerate
			// approximately 30% faster. Player.manaRegen is a timer that counts up
			// to Player.manaRegenDelay; advancing it extra each tick shortens the cycle.
			for (int i = 0; i < Main.maxPlayers; i++)
			{
				Player other = Main.player[i];
				if (!SupportEffects.IsAllyInRange(other, Player, AuraRadius))
					continue;
				var otherAP = other.GetModPlayer<AugmentPlayer>();
				if (!otherAP.HasAugment("mana_well"))
					continue;
				Player.manaRegen += (int)(Player.manaRegen * 0.30f);
				ReceivedManaWell = true;
				break;
			}

			// Owner also benefits from their own Mana Well.
			if (!ReceivedManaWell && HasAugment("mana_well"))
			{
				Player.manaRegen += (int)(Player.manaRegen * 0.30f);
				ReceivedManaWell = true;
			}

			// Pull-based Combat Medic: +3 lifeRegen = 1.5 HP/sec (lifeRegen applies at
			// half its value per second — same field used by vanilla regen buffs/potions).
			for (int i = 0; i < Main.maxPlayers; i++)
			{
				Player other = Main.player[i];
				if (!SupportEffects.IsAllyInRange(other, Player, AuraRadius))
					continue;
				var otherAP = other.GetModPlayer<AugmentPlayer>();
				if (!otherAP.HasAugment("combat_medic"))
					continue;
				Player.lifeRegen += 3;
				ReceivedCombatMedic = true;
				break;
			}

			// Owner also benefits from their own Combat Medic.
			if (!ReceivedCombatMedic && HasAugment("combat_medic"))
			{
				Player.lifeRegen += 3;
				ReceivedCombatMedic = true;
			}
		}

		public override void UpdateLifeRegen()
		{
			foreach (var a in Owned)
				a.UpdateLifeRegen(Player);
		}

		public override void UpdateBadLifeRegen()
		{
			foreach (var a in Owned)
				a.UpdateBadLifeRegen(Player);
		}

		public override void ProcessTriggers(TriggersSet triggersSet)
		{
			if (Main.myPlayer != Player.whoAmI)
				return;

			var ui = ModContent.GetInstance<AugmentUISystem>();
			bool isTyping = ui?.IsTypingAnywhere() ?? false;

			bool listPressed = Augments.OpenAugmentListKeybind?.JustPressed == true;
			if (!listPressed && (Augments.OpenAugmentListKeybind == null || Augments.OpenAugmentListKeybind.GetAssignedKeys().Count == 0))
			{
				if (!isTyping)
				{
					if (Main.keyState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.P) && !Main.oldKeyState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.P))
					{
						listPressed = true;
					}
				}
			}

			if (listPressed && !isTyping)
				ui?.ToggleList();

			bool analyticsPressed = Augments.ToggleCombatAnalyticsKeybind?.JustPressed == true;
			if (!analyticsPressed && (Augments.ToggleCombatAnalyticsKeybind == null || Augments.ToggleCombatAnalyticsKeybind.GetAssignedKeys().Count == 0))
			{
				if (!isTyping)
				{
					if (Main.keyState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.L) && !Main.oldKeyState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.L))
					{
						analyticsPressed = true;
					}
				}
			}

			if (analyticsPressed && !isTyping)
				ui?.ToggleAnalytics();

			if (Augments.TriggerTipKeybind?.JustPressed == true && !isTyping)
			{
				AugmentAdvisoryHUD.TriggerNextRandomTip();
			}

			if (HasAugment("cleanse") && CleanseCooldown == 0 && Augments.CleanseKeybind?.JustPressed == true && !isTyping)
			{
				CleanseCooldown = 1800;
				SoundEngine.PlaySound(SoundID.Item4, Player.Center);
				for (int i = 0; i < 25; i++)
				{
					Vector2 speed = Main.rand.NextVector2Circular(5f, 5f);
					Dust d = Dust.NewDustDirect(Player.position, Player.width, Player.height, DustID.PurificationPowder, speed.X, speed.Y);
					d.noGravity = true;
				}

				bool removedAny = false;
				for (int i = Player.buffType.Length - 1; i >= 0; i--)
				{
					if (SupportEffects.IsCleansableDebuff(Player.buffType[i]))
					{
						Player.DelBuff(i);
						removedAny = true;
					}
				}
				if (removedAny)
				{
					CombatText.NewText(Player.getRect(), Color.LightCyan, "Cleansed!");
				}

				if (Main.netMode == NetmodeID.SinglePlayer)
				{
					TryTriggerCleanseServer();
				}
				else if (Main.netMode == NetmodeID.MultiplayerClient)
				{
					NetMessage.SendData(MessageID.PlayerBuffs, -1, -1, null, Player.whoAmI);
					ModPacket packet = ModContent.GetInstance<Augments>().GetPacket();
					packet.Write((byte)AugmentPacketType.CleanseRequest);
					packet.Send();
				}
			}

			if (HasAugment("reforgers_patience") && Augments.UndoReforgeKeybind?.JustPressed == true)
			{
				if (TryUndoLastReforge())
					SoundEngine.PlaySound(SoundID.Item4, Player.Center);
				else
					Main.NewText("No reforge to undo.", 255, 80, 80);
			}

			// Debug: pop the reward choice UI without killing a boss. Endgame
			// is hardcoded since it's the bracket that can roll all 4
			// rarities via fallback, which is most useful for testing.
			bool devTools = AugmentNet.EnableDebugCommandsInMultiplayer;
			if (devTools && Augments.DebugTriggerPopupKeybind.JustPressed)
			{
				if (Main.netMode == NetmodeID.SinglePlayer)
				AugmentRewardLogic.GrantReward(Player, RarityBracket.FinalCalamity);
				else if (Main.netMode == NetmodeID.MultiplayerClient)
					AugmentNet.SendDebugRewardRequest();
			}

			// Debug: force-spawn the vendor NPC at the player, bypassing
			// CanTownNPCSpawn entirely (that check only gates the automatic
			// town-relocation system, not a direct NewNPC call) - lets us
			// test the NPC without needing a house or Skeletron downed.
			if (devTools && Augments.DebugSpawnVendorKeybind.JustPressed)
			{
				AugmentNet.RequestVendorSpawn(Player);
			}

			// Debug: open the vendor shop panel directly, ahead of it being
			// wired to the NPC's chat button - lets the panel be tested on
			// its own before that wiring happens.
			if (devTools && Augments.DebugToggleShopKeybind.JustPressed)
				ModContent.GetInstance<AugmentUISystem>().ToggleShop();

			if (devTools && Augments.ResetCooldownsKeybind?.JustPressed == true)
			{
				ResetAllCooldowns();
				SoundEngine.PlaySound(SoundID.MaxMana, Player.Center);
				Main.NewText("✦ [DEV] All plug-in chip cooldowns have been reset to 0! ✦", Color.Cyan);
			}
		}

		public void ResetAllCooldowns()
		{
			CleanseCooldown = 0;
			LastRitesCooldown = 0;
			LifelineCooldown = 0;
			soulLinkRequestCooldown = 0;
			FinalStandCooldown = 0;
			FinalStandActiveTicks = 0;
			GodslayerBladeCooldown = 0;
			IronWillCooldown = 0;
			IronWillDurationRemaining = 0;
			LastStandCooldown = 0;
			LastStandArmedThisHit = false;
			PhoenixHeartCooldown = 0;
			PhoenixHeartArmedThisHit = false;
			PiedPiperCooldown = 0;
			SecondWindCooldown = 0;

			Player.ClearBuff(ModContent.BuffType<LastRitesCooldownBuff>());
			Player.ClearBuff(ModContent.BuffType<LifelineCooldownBuff>());
		}

		// Fires on every melee/weapon hit - dispatches to whichever owned
		// augments care about it (most won't override this and do nothing).
		// Normalized boss key — Twins are two NPCs but one fight, so both segments
		// map to Retinazer's type for participation and kill-count tracking.
		private static int BossKey(int npcType)
			=> npcType == NPCID.Spazmatism ? NPCID.Retinazer : npcType;

		// Records that the local player dealt damage to a boss this fight.
		// Sends a packet on the first hit so the server can populate its copy.
		private void TryRegisterBossDamage(NPC target)
		{
			if (!target.boss) return;

			int key = BossKey(target.type);
			if (DamagedBossesThisFight.Contains(key)) return; // already registered

			DamagedBossesThisFight.Add(key);

			if (Main.netMode != NetmodeID.MultiplayerClient) return;

			ModPacket packet = ModContent.GetInstance<Augments>().GetPacket();
			packet.Write((byte)AugmentPacketType.BossDamageParticipation);
			packet.Write((byte)Player.whoAmI);
			packet.Write(key);
			packet.Send();
		}

		public override void OnHitNPCWithItem(Item item, NPC target, NPC.HitInfo hit, int damageDone)
		{
			TryRegisterBossDamage(target);

			if (Player.whoAmI == Main.myPlayer)
			{
				AugmentClass wClass = AugmentClass.Melee;
				if (item.CountsAsClass(DamageClass.Ranged) || item.DamageType == DamageClass.Ranged)
					wClass = AugmentClass.Ranged;
				else if (item.CountsAsClass(DamageClass.Magic) || item.DamageType == DamageClass.Magic)
					wClass = AugmentClass.Magic;
				else if (item.CountsAsClass(DamageClass.Summon) || item.DamageType == DamageClass.Summon || item.CountsAsClass(DamageClass.SummonMeleeSpeed))
					wClass = AugmentClass.Summon;
				else if (item.CountsAsClass(DamageClass.Generic) || item.DamageType == DamageClass.Generic)
					wClass = AugmentClass.Universal;

				AugmentDamageTracker.RecordWeaponHit(item.Name, ApplyChipContributions(damageDone, hit.Crit), hit.Crit, wClass);
			}

			if (item.CountsAsClass(DamageClass.Melee))
			{
				TryTriggerKineticShockwave(target, item.damage);
			}

			if (hit.Crit)
			{
				TryTriggerFortuneCoinBurst(target);
			}

			TryTriggerVoltStaticArc(target);

			foreach (var a in Owned)
				a.OnHitNPCWithItem(Player, item, target, hit, AugmentHitSource.NormalAttack);

			// Twin Strike (DuplicatesOnHitEffects) - a crit re-fires every
			// owned augment's on-hit reaction a second time. Deliberately
			// scoped to this dispatcher only; ModifyHitNPCWithItem's flat
			// damage-bonus modifiers below are untouched, single pass always.
			if (hit.Crit && Owned.Any(a => a.DuplicatesOnHitEffects))
			{
				foreach (var a in Owned)
					a.OnHitNPCWithItem(Player, item, target, hit, AugmentHitSource.NormalAttack);
			}

			if (item.CountsAsClass(DamageClass.Melee))
			{
				TryTriggerChainLightning(target, hit, CurrentHitOnHitDamage);
			}

			TryTriggerArcaneNova(target, item.CountsAsClass(DamageClass.Magic) || item.DamageType == DamageClass.Magic);
		}

		// Covers projectile hits, which includes thrust-style melee weapons
		// like short swords and spears - they register hits this way instead
		// of through OnHitNPCWithItem.
		public override void OnHitNPCWithProj(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone)
		{
			TryRegisterBossDamage(target);

			AugmentProjectileTag tag = proj.GetGlobalProjectile<AugmentProjectileTag>();

			if (Player.whoAmI == Main.myPlayer)
			{
				if (tag.IsAugmentProcDamage)
				{
					if (!string.IsNullOrEmpty(tag.SourceProtocolId))
					{
						string protoName = AugmentFamilyRegistry.Families.TryGetValue(tag.SourceProtocolId, out var f) ? f.DisplayName : tag.SourceProtocolId;
						AugmentDamageTracker.RecordProtocolHit(tag.SourceProtocolId, protoName, damageDone, hit.Crit);
					}
					else if (!string.IsNullOrEmpty(tag.SourceAugmentId))
					{
						Augment aug = AugmentDatabase.GetById(tag.SourceAugmentId);
						if (aug != null)
							AugmentDamageTracker.RecordChipHit(aug, damageDone, hit.Crit);
						else
							AugmentDamageTracker.RecordHit(tag.SourceAugmentId, tag.SourceAugmentId, damageDone, hit.Crit, Color.White);
					}
					else
					{
						AugmentDamageTracker.RecordHit("augment_proc", proj.Name, damageDone, hit.Crit, new Color(255, 62, 165));
					}
				}
				else
				{
					// Credit the weapon that fired the projectile. Only fall back to the held
					// item when it is a real weapon (not ammo, a torch or a placeable like a dummy).
					Item held = Player.HeldItem;
					bool heldIsWeapon = held != null && !held.IsAir && held.damage > 0 && held.ammo == 0 && !held.consumable;
					string weaponName = !string.IsNullOrEmpty(tag.SourceItemName) ? tag.SourceItemName
						: heldIsWeapon ? held.Name
						: proj.Name;
					AugmentClass wClass = AugmentClass.Universal;
					if (proj.CountsAsClass(DamageClass.Melee) || proj.DamageType == DamageClass.Melee)
						wClass = AugmentClass.Melee;
					else if (proj.CountsAsClass(DamageClass.Ranged) || proj.DamageType == DamageClass.Ranged)
						wClass = AugmentClass.Ranged;
					else if (proj.CountsAsClass(DamageClass.Magic) || proj.DamageType == DamageClass.Magic)
						wClass = AugmentClass.Magic;
					else if (proj.CountsAsClass(DamageClass.Summon) || proj.DamageType == DamageClass.Summon || proj.CountsAsClass(DamageClass.SummonMeleeSpeed))
						wClass = AugmentClass.Summon;

					AugmentDamageTracker.RecordWeaponHit(weaponName, ApplyChipContributions(damageDone, hit.Crit), hit.Crit, wClass);
				}
			}

			if (tag.IsAugmentProcDamage && !tag.CanTriggerOnHitAugments)
				return;

			AugmentHitSource source = tag.IsAugmentProcDamage ? AugmentHitSource.AugmentProc : AugmentHitSource.NormalAttack;
			float effectiveness = tag.IsAugmentProcDamage ? MathHelper.Clamp(tag.OnHitEffectiveness, 0f, 1f) : 1f;

			if (source == AugmentHitSource.NormalAttack && proj.CountsAsClass(DamageClass.Melee))
			{
				TryTriggerKineticShockwave(target, proj.damage);
			}

			if (source == AugmentHitSource.NormalAttack && hit.Crit)
			{
				TryTriggerFortuneCoinBurst(target);
			}

			if (source == AugmentHitSource.NormalAttack)
			{
				TryTriggerVoltStaticArc(target);
			}

			TryTriggerHivemindNanites(proj, target);
			TryTriggerMarksmanEffects(proj, target, hit, source);
			TryTriggerArcaneNova(target, proj.CountsAsClass(DamageClass.Magic) || proj.DamageType == DamageClass.Magic);

			foreach (var a in Owned)
				a.OnHitNPCWithProj(Player, proj, target, hit, source, effectiveness);

			// Mirrors the OnHitNPCWithItem second pass above.
			if (source == AugmentHitSource.NormalAttack && hit.Crit && Owned.Any(a => a.DuplicatesOnHitEffects))
			{
				foreach (var a in Owned)
					a.OnHitNPCWithProj(Player, proj, target, hit, source, effectiveness);
			}

			if (source == AugmentHitSource.NormalAttack && proj.CountsAsClass(DamageClass.Melee))
			{
				TryTriggerChainLightning(target, hit, CurrentHitOnHitDamage);
			}
		}


		// Per-hit record of how much extra damage each chip added in its Modify hook,
		// so the DPS panel can credit stat-boost chips instead of only the weapon.
		private readonly List<(Augment aug, int delta)> pendingChipContributions = new();
		private int pendingContributionBaseline;

		// Returns the damage left for the weapon after crediting each chip's share.
		private int ApplyChipContributions(int damageDone, bool crit)
		{
			int remaining = damageDone;
			int total = 0;
			foreach (var c in pendingChipContributions)
				total += c.delta;

			if (total > 0 && damageDone > 0)
			{
				int denominator = Math.Max(pendingContributionBaseline, total);
				foreach (var c in pendingChipContributions)
				{
					int share = (int)((long)damageDone * c.delta / denominator);
					if (share <= 0)
						continue;
					AugmentDamageTracker.RecordChipHit(c.aug, share, crit);
					remaining -= share;
				}
			}

			pendingChipContributions.Clear();
			return Math.Max(0, remaining);
		}

		// Fires BEFORE a melee hit is finalized - lets augments boost the
		// actual damage of the hit via modifiers.FlatBonusDamage.
		public override void ModifyHitNPCWithItem(Item item, NPC target, ref NPC.HitModifiers modifiers)
		{
			CurrentHitOnHitDamage = 0;
			pendingChipContributions.Clear();

			if (AugmentListUIState.IsDevCritMode)
				modifiers.SetCrit();

			if (AugmentFamilyRegistry.GetOwnedCount(this, AugmentFamilyRegistry.BloodhunterId) >= 2)
			{
				bool isBleeding = target.GetGlobalNPC<AugmentBleedNPC>().IsActive || target.HasBuff(BuffID.Bleeding);
				if (isBleeding && Main.rand.NextFloat() < 0.08f)
					modifiers.SetCrit();
			}

			if (AugmentFamilyRegistry.GetOwnedCount(this, AugmentFamilyRegistry.VoltId) >= 2)
			{
				if (target.HasBuff(BuffID.Electrified) && Main.rand.NextFloat() < 0.08f)
					modifiers.SetCrit();
			}

			ApplyHivemindFlatDamage(target, ref modifiers);
			ApplyMarksmanArmorPenetration(target, item.CountsAsClass(DamageClass.Ranged) || item.DamageType == DamageClass.Ranged, ref modifiers);

			bool trackContrib = Player.whoAmI == Main.myPlayer;
			float baseDmg = Math.Max(1, item.damage);
			foreach (var a in Owned)
			{
				int before = trackContrib ? modifiers.GetDamage(baseDmg, false) : 0;
				a.ModifyHitNPCWithItem(Player, item, target, ref modifiers, AugmentHitSource.NormalAttack);
				if (trackContrib)
				{
					int delta = modifiers.GetDamage(baseDmg, false) - before;
					if (delta > 0)
						pendingChipContributions.Add((a, delta));
				}
			}
			pendingContributionBaseline = trackContrib ? modifiers.GetDamage(baseDmg, false) : 0;
		}

		public override void ModifyHitNPCWithProj(Projectile proj, NPC target, ref NPC.HitModifiers modifiers)
		{
			CurrentHitOnHitDamage = 0;
			pendingChipContributions.Clear();

			if (AugmentListUIState.IsDevCritMode)
				modifiers.SetCrit();

			if (AugmentFamilyRegistry.GetOwnedCount(this, AugmentFamilyRegistry.BloodhunterId) >= 2)
			{
				bool isBleeding = target.GetGlobalNPC<AugmentBleedNPC>().IsActive || target.HasBuff(BuffID.Bleeding);
				if (isBleeding && Main.rand.NextFloat() < 0.08f)
					modifiers.SetCrit();
			}

			if (AugmentFamilyRegistry.GetOwnedCount(this, AugmentFamilyRegistry.VoltId) >= 2)
			{
				if (target.HasBuff(BuffID.Electrified) && Main.rand.NextFloat() < 0.08f)
					modifiers.SetCrit();
			}

			// Lasher Protocol (2-Piece Synergy): Predatory Command (+12% minion crit against 5-stack targets)
			if (AugmentFamilyRegistry.GetOwnedCount(this, AugmentFamilyRegistry.LasherId) >= 2)
			{
				bool isSummonMinion = proj.minion || proj.sentry || proj.DamageType == DamageClass.Summon;
				if (isSummonMinion && target.GetGlobalNPC<AugmentCrackedNPC>().Stacks >= AugmentCrackedNPC.MaxStacks)
				{
					if (Main.rand.NextFloat() < 0.12f)
						modifiers.SetCrit();
				}
			}

			ApplyHivemindFlatDamage(target, ref modifiers);
			ApplyMarksmanArmorPenetration(target, proj.CountsAsClass(DamageClass.Ranged) || proj.DamageType == DamageClass.Ranged, ref modifiers);

			AugmentProjectileTag tag = proj.GetGlobalProjectile<AugmentProjectileTag>();
			if (tag.IsAugmentProcDamage && !tag.CanTriggerOnHitAugments)
				return;

			AugmentHitSource source = tag.IsAugmentProcDamage ? AugmentHitSource.AugmentProc : AugmentHitSource.NormalAttack;
			float effectiveness = tag.IsAugmentProcDamage ? MathHelper.Clamp(tag.OnHitEffectiveness, 0f, 1f) : 1f;

			bool trackContrib = Player.whoAmI == Main.myPlayer;
			float baseDmg = Math.Max(1, proj.damage);
			foreach (var a in Owned)
			{
				int before = trackContrib ? modifiers.GetDamage(baseDmg, false) : 0;
				a.ModifyHitNPCWithProj(Player, proj, target, ref modifiers, source, effectiveness);
				if (trackContrib)
				{
					int delta = modifiers.GetDamage(baseDmg, false) - before;
					if (delta > 0)
						pendingChipContributions.Add((a, delta));
				}
			}
			pendingContributionBaseline = trackContrib ? modifiers.GetDamage(baseDmg, false) : 0;
		}

		public override void ModifyShootStats(Item item, ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback)
		{
			if (AugmentFamilyRegistry.GetOwnedCount(this, AugmentFamilyRegistry.MarksmanId) >= 2)
			{
				if (item.CountsAsClass(DamageClass.Ranged) || item.DamageType == DamageClass.Ranged)
				{
					velocity *= 1.25f;
				}
			}
		}

		public void TryTriggerMarksmanEffects(Projectile proj, NPC target, NPC.HitInfo hit, AugmentHitSource source)
		{
			if (AugmentFamilyRegistry.GetOwnedCount(this, AugmentFamilyRegistry.MarksmanId) < 2)
				return;

			if (target == null || !target.active || target.friendly || target.dontTakeDamage)
				return;

			if (!proj.CountsAsClass(DamageClass.Ranged) && proj.DamageType != DamageClass.Ranged)
				return;

			var marksmanNPC = target.GetGlobalNPC<AugmentMarksmanNPC>();

			// 1. Kinetic Mark Application: Ranged hits from beyond 350 pixels
			float distSq = Vector2.DistanceSquared(Player.Center, target.Center);
			if (distSq >= 350f * 350f)
			{
				marksmanNPC.ApplyMark(Player.whoAmI, 240);
				if (Main.netMode == NetmodeID.MultiplayerClient)
					AugmentNet.SendApplyNPCEffectKineticMark(target.whoAmI, 240);
			}

			// 2. Kinetic Shrapnel Detonation: Critical strike against a marked target
			if (hit.Crit && marksmanNPC.IsMarked && KineticShrapnelCooldown <= 0 && Main.myPlayer == Player.whoAmI)
			{
				KineticShrapnelCooldown = 15;

				SoundEngine.PlaySound(SoundID.Item89 with { Volume = 0.55f, Pitch = 0.4f }, target.Center);

				const int shrapnelCount = 3;
				const int shrapnelDamage = 35;
				float baseAngle = Main.rand.NextFloat(MathHelper.TwoPi);

				for (int i = 0; i < shrapnelCount; i++)
				{
					float angle = baseAngle + (i * MathHelper.TwoPi / shrapnelCount) + Main.rand.NextFloat(-0.25f, 0.25f);
					Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(10f, 13f);

					Projectile.NewProjectile(
						Player.GetSource_OnHit(target),
						target.Center,
						velocity,
						ModContent.ProjectileType<KineticShrapnelProjectile>(),
						shrapnelDamage,
						3f,
						Player.whoAmI
					);
				}
			}
		}

		public void TryTriggerArcaneNova(NPC target, bool isMagic)
		{
			if (!isMagic)
				return;

			if (AugmentFamilyRegistry.GetOwnedCount(this, AugmentFamilyRegistry.ArcaneSurgeId) < 2)
				return;

			if (AstralMatrixStacks < 5)
				return;

			if (target == null || !target.active || target.friendly || target.dontTakeDamage)
				return;

			AstralMatrixStacks = 0;
			AstralMatrixManaSpent = 0;

			// Refund +30 mana to the player
			int oldMana = Player.statMana;
			Player.statMana = Math.Min(Player.statManaMax2, Player.statMana + 30);
			int restored = Player.statMana - oldMana;
			if (restored > 0)
			{
				Player.ManaEffect(restored);
			}

			SoundEngine.PlaySound(SoundID.Item94 with { Volume = 0.75f, Pitch = 0.25f }, target.Center);

			// Radial shockwave of astral indigo particles
			for (int i = 0; i < 28; i++)
			{
				float angle = i * (MathHelper.TwoPi / 28f);
				Vector2 vel = angle.ToRotationVector2() * 6.5f;
				Dust d = Dust.NewDustPerfect(target.Center, DustID.DungeonSpirit, vel, 100, new Color(129, 140, 248), 1.5f);
				d.noGravity = true;
			}
			for (int i = 0; i < 14; i++)
			{
				Dust d = Dust.NewDustPerfect(target.Center, DustID.PurpleTorch, Main.rand.NextVector2Circular(4.5f, 4.5f), 100, Color.White, 1.2f);
				d.noGravity = true;
			}

			if (Main.myPlayer != Player.whoAmI)
				return;

			const int novaDamage = 65;
			const float radius = 140f;

			foreach (NPC npc in Main.npc)
			{
				if (!npc.active || npc.friendly || npc.townNPC || npc.dontTakeDamage)
					continue;

				if (npc.Distance(target.Center) > radius)
					continue;

				Vector2 knockbackDir = npc.Center - target.Center;
				if (knockbackDir == Vector2.Zero)
					knockbackDir = new Vector2(Player.direction, 0f);
				knockbackDir.Normalize();

				int hitDirection = knockbackDir.X >= 0f ? 1 : -1;
				npc.SimpleStrikeNPC(novaDamage, hitDirection, false, 4.5f, DamageClass.Magic, false);
				AugmentDamageTracker.RecordProtocolHit(AugmentFamilyRegistry.ArcaneSurgeId, "Arcane Surge: Nova", novaDamage, false);
			}
		}


		public override void ModifyManaCost(Item item, ref float reduce, ref float mult)
		{
			foreach (var a in Owned)
				a.ModifyManaCost(Player, item, ref reduce, ref mult);
		}

		public override void OnConsumeMana(Item item, int manaConsumed)
		{
			if (AugmentFamilyRegistry.GetOwnedCount(this, AugmentFamilyRegistry.ArcaneSurgeId) >= 2)
			{
				AstralMatrixManaSpent += manaConsumed;
				AstralMatrixStacks = Math.Clamp(AstralMatrixManaSpent / 20, 0, 5);
			}

			foreach (var a in Owned)
				a.OnConsumeMana(Player, item, manaConsumed);
		}

		public override bool CanConsumeAmmo(Item weapon, Item ammo)
		{
			foreach (var a in Owned)
			{
				if (!a.CanConsumeAmmo(Player, weapon, ammo))
					return false;
			}
			return true;
		}

		public override void ModifyWeaponCrit(Item item, ref float crit)
		{
			foreach (var a in Owned)
				a.ModifyWeaponCrit(Player, item, ref crit);
		}

		public override void GetHealLife(Item item, bool quickHeal, ref int healValue)
		{
			foreach (var a in Owned)
				a.GetHealLife(Player, item, quickHeal, ref healValue);
		}

		public override void ModifyFishingAttempt(ref FishingAttempt attempt)
		{
			foreach (var a in Owned)
				a.ModifyFishingAttempt(Player, ref attempt);
		}

		public override void GetFishingLevel(Item fishingRod, Item bait, ref float fishingLevel)
		{
			foreach (var a in Owned)
					a.GetFishingLevel(Player, fishingRod, bait, ref fishingLevel);
		}

		private void RebuildOwnedCacheFromOwnedIdsOnly()
		{
			owned.Clear();
			RevitalizingWaveTimer = 1200;
			CleanseCooldown = 0;
			LastRitesCooldown = 0;
			lastRitesInvulnTicks = 0;
			LifelineCooldown = 0;
			lifelineInvulnTicks = 0;
			lifelineProtectionAuthorized = false;
			undyingBondRedirectSent = false;
			undyingBondRequestTimer = 0;
			foreach (string id in ownedIds)
			{
				Augment augment = AugmentDatabase.GetById(id);
				if (augment != null)
					owned.Add(augment);
			}

		}

		internal void SyncInventory()
		{
			if (Main.netMode != NetmodeID.Server)
				return;

			for (int slot = 0; slot < Player.inventory.Length; slot++)
				NetMessage.SendData(MessageID.SyncEquipment, -1, -1, null, Player.whoAmI, slot, Player.inventory[slot].prefix);
		}

		public override void SetControls()
		{
			if (Main.netMode != NetmodeID.Server && Player.whoAmI == Main.myPlayer)
			{
				if (ModContent.GetInstance<AugmentUISystem>()?.IsPlayerInputBlocked() == true)
				{
					Player.controlUseItem = false;
					Player.controlUseTile = false;
					Player.mouseInterface = true;
				}
			}
		}

		public override bool PreItemCheck()
		{
			if (Main.netMode != NetmodeID.Server && Player.whoAmI == Main.myPlayer)
			{
				if (ModContent.GetInstance<AugmentUISystem>()?.IsPlayerInputBlocked() == true)
				{
					Player.mouseInterface = true;
					return false;
				}
			}

			return base.PreItemCheck();
		}

		public override bool CanUseItem(Item item)
		{
			if (Main.netMode != NetmodeID.Server && Player.whoAmI == Main.myPlayer)
			{
				if (ModContent.GetInstance<AugmentUISystem>()?.IsPlayerInputBlocked() == true)
					return false;
			}

			return base.CanUseItem(item);
		}


		public override bool OnPickup(Item item)
		{
			if (item.type == ItemID.Heart || item.type == ItemID.CandyApple || item.type == ItemID.CandyCane)
			{
				if (AugmentFamilyRegistry.GetOwnedCount(this, AugmentFamilyRegistry.FieldMedicId) >= 2)
				{
					int bonus = 4;
					Player.statLife = Math.Min(Player.statLifeMax2, Player.statLife + bonus);
					Player.HealEffect(bonus);
				}
			}
			return true;
		}
	}
}
