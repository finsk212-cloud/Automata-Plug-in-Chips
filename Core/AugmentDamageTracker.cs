using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;

namespace Augments.Core
{
	public enum AnalyticsViewMode
	{
		Last10Minutes,
		TotalSession
	}

	public class DamageSourceRecord
	{
		public string Id { get; set; }
		public string DisplayName { get; set; }
		public Color Color { get; set; }
		public long TotalDamage { get; set; }
		public int HitCount { get; set; }
		public int CritCount { get; set; }
		public int MaxHit { get; set; }
		public long DamageBlocked { get; set; }
		public int BlockCount { get; set; }
		public long HealingDone { get; set; }
		public int HealCount { get; set; }
		public AugmentRarity? Rarity { get; set; }
		public AugmentClass? SourceClass { get; set; }
		public bool IsProtocol { get; set; }
		public bool IsWeapon { get; set; }
	}

	public struct TimedHit
	{
		public float Timestamp;
		public string SourceId;
		public int Damage;
		public bool IsCrit;
	}

	public struct TimedBlockedHit
	{
		public float Timestamp;
		public string SourceId;
		public int DamageBlocked;
	}

	public struct TimedHeal
	{
		public float Timestamp;
		public string SourceId;
		public int Amount;
	}

	public static class AugmentDamageTracker
	{
		private const float InactivityTimeout = 4.0f;
		private const float RollingDpsWindow = 3.0f;
		public const float TenMinutesSeconds = 600.0f;

		public static bool IsPaused { get; set; } = false;
		public static float SessionDuration { get; private set; } = 0f;
		public static long TotalSessionDamage { get; private set; } = 0;
		public static long TotalSessionDamageBlocked { get; private set; } = 0;
		public static long TotalSessionHealing { get; private set; } = 0;
		public static AnalyticsViewMode ViewMode { get; set; } = AnalyticsViewMode.Last10Minutes;

		private static float timeSinceLastHit = 999f;
		private static readonly Dictionary<string, DamageSourceRecord> totalRecords = new();
		private static readonly Queue<TimedHit> historyQueue = new();
		private static readonly Queue<TimedBlockedHit> historyBlockedQueue = new();
		private static readonly Queue<TimedHeal> historyHealQueue = new();
		private static float globalTime = 0f;

		public static IReadOnlyDictionary<string, DamageSourceRecord> TotalRecords => totalRecords;

		public static void Update(float dt)
		{
			if (IsPaused)
				return;

			globalTime += dt;

			// Active combat session duration
			if (timeSinceLastHit < InactivityTimeout)
			{
				SessionDuration += dt;
				timeSinceLastHit += dt;
			}

			// Prune hits older than 10 minutes (600 seconds)
			float pruneCutoff = globalTime - TenMinutesSeconds;
			while (historyQueue.Count > 0 && historyQueue.Peek().Timestamp < pruneCutoff)
			{
				historyQueue.Dequeue();
			}

			// Prune blocked hits older than 10 minutes (600 seconds)
			while (historyBlockedQueue.Count > 0 && historyBlockedQueue.Peek().Timestamp < pruneCutoff)
			{
				historyBlockedQueue.Dequeue();
			}

			while (historyHealQueue.Count > 0 && historyHealQueue.Peek().Timestamp < pruneCutoff)
			{
				historyHealQueue.Dequeue();
			}
		}

		public static void RecordHit(string sourceId, string displayName, int damage, bool isCrit, Color color, AugmentRarity? rarity = null, AugmentClass? sourceClass = null, bool isProtocol = false, bool isWeapon = false)
		{
			if (IsPaused || damage <= 0)
				return;

			timeSinceLastHit = 0f;
			TotalSessionDamage += damage;

			historyQueue.Enqueue(new TimedHit
			{
				Timestamp = globalTime,
				SourceId = sourceId,
				Damage = damage,
				IsCrit = isCrit
			});

			if (!totalRecords.TryGetValue(sourceId, out var rec))
			{
				rec = new DamageSourceRecord
				{
					Id = sourceId,
					DisplayName = displayName,
					Color = color,
					TotalDamage = 0,
					HitCount = 0,
					CritCount = 0,
					MaxHit = 0,
					Rarity = rarity,
					SourceClass = sourceClass,
					IsProtocol = isProtocol,
					IsWeapon = isWeapon
				};
				totalRecords[sourceId] = rec;
			}
			else
			{
				if (sourceClass.HasValue && !rec.SourceClass.HasValue)
					rec.SourceClass = sourceClass;
				if (rarity.HasValue && !rec.Rarity.HasValue)
					rec.Rarity = rarity;
			}

			rec.TotalDamage += damage;
			rec.HitCount++;
			if (isCrit)
				rec.CritCount++;
			if (damage > rec.MaxHit)
				rec.MaxHit = damage;
		}

		public static void RecordWeaponHit(string weaponName, int damage, bool isCrit, AugmentClass weaponClass = AugmentClass.Universal)
		{
			string cleanName = string.IsNullOrEmpty(weaponName) ? "Held Weapon" : weaponName;
			string id = "weapon_" + cleanName.ToLowerInvariant().Replace(' ', '_');
			RecordHit(id, cleanName, damage, isCrit, new Color(240, 240, 245), null, weaponClass, isProtocol: false, isWeapon: true);
		}

		public static void RecordChipHit(Augment augment, int damage, bool isCrit)
		{
			if (augment == null)
				return;

			Color rarityColor = augment.Rarity switch
			{
				AugmentRarity.Legendary => new Color(255, 200, 50),
				AugmentRarity.Epic => new Color(185, 115, 255),
				AugmentRarity.Rare => new Color(60, 195, 255),
				_ => new Color(210, 215, 225)
			};

			RecordHit(augment.Id, augment.DisplayName, damage, isCrit, rarityColor, augment.Rarity, augment.Class, isProtocol: false, isWeapon: false);
		}

		public static void RecordChipHit(string sourceId, int damage, bool isCrit)
		{
			if (string.IsNullOrEmpty(sourceId) || damage <= 0)
				return;

			Augment aug = AugmentDatabase.GetById(sourceId);
			if (aug != null)
			{
				RecordChipHit(aug, damage, isCrit);
			}
			else
			{
				RecordHit(sourceId, sourceId, damage, isCrit, Color.White, null, null, isProtocol: false, isWeapon: false);
			}
		}

		public static void RecordProtocolHit(string protocolId, string protocolName, int damage, bool isCrit)
		{
			Color protocolColor = new Color(255, 62, 165); // Vivid Electric Fuchsia
			AugmentClass protoClass = protocolId switch
			{
				AugmentFamilyRegistry.BloodhunterId or AugmentFamilyRegistry.KineticId => AugmentClass.Melee,
				AugmentFamilyRegistry.MarksmanId or AugmentFamilyRegistry.GunslingerId => AugmentClass.Ranged,
				AugmentFamilyRegistry.ArcaneSurgeId or AugmentFamilyRegistry.CryoId => AugmentClass.Magic,
				AugmentFamilyRegistry.HivemindId or AugmentFamilyRegistry.LasherId => AugmentClass.Summon,
				AugmentFamilyRegistry.FieldMedicId => AugmentClass.Support,
				_ => AugmentClass.Universal
			};

			if (AugmentFamilyRegistry.Families.TryGetValue(protocolId, out var fam))
			{
				protocolColor = fam.ThemeColor;
			}

			RecordHit($"proto_{protocolId}", protocolName, damage, isCrit, protocolColor, null, protoClass, isProtocol: true, isWeapon: false);
		}

		public static void RecordDamageBlocked(string sourceId, string displayName, int damageBlocked, AugmentRarity? rarity = null, AugmentClass? sourceClass = null, bool isProtocol = false)
		{
			if (IsPaused || damageBlocked <= 0)
				return;

			timeSinceLastHit = 0f;
			TotalSessionDamageBlocked += damageBlocked;

			historyBlockedQueue.Enqueue(new TimedBlockedHit
			{
				Timestamp = globalTime,
				SourceId = sourceId,
				DamageBlocked = damageBlocked
			});

			Color sourceColor = isProtocol ? new Color(255, 62, 165) : (rarity switch
			{
				AugmentRarity.Legendary => new Color(255, 200, 50),
				AugmentRarity.Epic => new Color(185, 115, 255),
				AugmentRarity.Rare => new Color(60, 195, 255),
				_ => new Color(52, 211, 153)
			});

			if (!totalRecords.TryGetValue(sourceId, out var rec))
			{
				rec = new DamageSourceRecord
				{
					Id = sourceId,
					DisplayName = displayName,
					Color = sourceColor,
					TotalDamage = 0,
					HitCount = 0,
					CritCount = 0,
					MaxHit = 0,
					DamageBlocked = 0,
					BlockCount = 0,
					Rarity = rarity,
					SourceClass = sourceClass,
					IsProtocol = isProtocol,
					IsWeapon = false
				};
				totalRecords[sourceId] = rec;
			}
			else
			{
				if (sourceClass.HasValue && !rec.SourceClass.HasValue)
					rec.SourceClass = sourceClass;
				if (rarity.HasValue && !rec.Rarity.HasValue)
					rec.Rarity = rarity;
			}

			rec.DamageBlocked += damageBlocked;
			rec.BlockCount++;
		}

		public static void RecordDamageBlocked(Augment augment, int damageBlocked)
		{
			if (augment == null || damageBlocked <= 0)
				return;

			RecordDamageBlocked(augment.Id, augment.DisplayName, damageBlocked, augment.Rarity, augment.Class, isProtocol: false);
		}

		public static void RecordDamageBlocked(string sourceId, int damageBlocked)
		{
			if (string.IsNullOrEmpty(sourceId) || damageBlocked <= 0)
				return;

			Augment aug = AugmentDatabase.GetById(sourceId);
			if (aug != null)
			{
				RecordDamageBlocked(aug, damageBlocked);
			}
			else
			{
				RecordDamageBlocked(sourceId, sourceId, damageBlocked, null, null, isProtocol: false);
			}
		}

		// Records healing a plugin did for an ally (or the player). Healing shows up
		// in the analytics panel beside damage and blocked damage.
		public static void RecordHealing(string sourceId, int amount)
		{
			if (IsPaused || amount <= 0 || string.IsNullOrEmpty(sourceId))
				return;

			Augment aug = AugmentDatabase.GetById(sourceId);
			string displayName = aug != null ? aug.DisplayName : sourceId;
			Color sourceColor = Color.White;
			if (aug != null)
			{
				sourceColor = aug.Rarity switch
				{
					AugmentRarity.Legendary => new Color(255, 200, 50),
					AugmentRarity.Epic => new Color(185, 115, 255),
					AugmentRarity.Rare => new Color(60, 195, 255),
					_ => new Color(210, 215, 225)
				};
			}

			timeSinceLastHit = 0f;
			TotalSessionHealing += amount;

			historyHealQueue.Enqueue(new TimedHeal
			{
				Timestamp = globalTime,
				SourceId = sourceId,
				Amount = amount
			});

			if (!totalRecords.TryGetValue(sourceId, out var rec))
			{
				rec = new DamageSourceRecord
				{
					Id = sourceId,
					DisplayName = displayName,
					Color = sourceColor,
					Rarity = aug?.Rarity,
					SourceClass = aug?.Class,
					IsProtocol = false,
					IsWeapon = false
				};
				totalRecords[sourceId] = rec;
			}

			rec.HealingDone += amount;
			rec.HealCount++;
		}

		// Total healing in the current view (session total or last 10 minutes).
		public static long GetViewHealing()
		{
			if (ViewMode == AnalyticsViewMode.TotalSession)
				return TotalSessionHealing;

			float cutoff = globalTime - TenMinutesSeconds;
			long total = 0;
			foreach (var h in historyHealQueue)
			{
				if (h.Timestamp >= cutoff)
					total += h.Amount;
			}
			return total;
		}

		public static float GetCurrentDPS()
		{
			if (historyQueue.Count == 0 || timeSinceLastHit >= InactivityTimeout)
				return 0f;

			float cutoff = globalTime - RollingDpsWindow;
			long windowDamage = 0;

			foreach (var hit in historyQueue)
			{
				if (hit.Timestamp >= cutoff)
					windowDamage += hit.Damage;
			}

			return (float)windowDamage / RollingDpsWindow;
		}

		public static float GetSessionDPS()
		{
			if (SessionDuration <= 0.05f)
				return 0f;

			return (float)TotalSessionDamage / SessionDuration;
		}

		public static long GetTenMinuteDamageBlocked()
		{
			float cutoff = globalTime - TenMinutesSeconds;
			long total = 0;
			foreach (var b in historyBlockedQueue)
			{
				if (b.Timestamp >= cutoff)
					total += b.DamageBlocked;
			}
			return total;
		}

		public static (long totalDamage, long totalDamageBlocked, List<DamageSourceRecord> records) GetCurrentViewData(AugmentPlayer ap)
		{
			// Gather baseline records
			var resultDict = new Dictionary<string, DamageSourceRecord>();

			// Always populate all owned augments even if 0 damage
			if (ap != null)
			{
				foreach (var a in ap.Owned)
				{
					Color rarityColor = a.Rarity switch
					{
						AugmentRarity.Legendary => new Color(255, 200, 50),
						AugmentRarity.Epic => new Color(185, 115, 255),
						AugmentRarity.Rare => new Color(60, 195, 255),
						_ => new Color(210, 215, 225)
					};

					resultDict[a.Id] = new DamageSourceRecord
					{
						Id = a.Id,
						DisplayName = a.DisplayName,
						Color = rarityColor,
						TotalDamage = 0,
						HitCount = 0,
						CritCount = 0,
						MaxHit = 0,
						DamageBlocked = 0,
						BlockCount = 0,
						Rarity = a.Rarity,
						SourceClass = a.Class,
						IsProtocol = false,
						IsWeapon = false
					};
				}
			}

			long totalViewDamage = 0;
			long totalViewBlocked = 0;

			if (ViewMode == AnalyticsViewMode.TotalSession)
			{
				totalViewDamage = TotalSessionDamage;
				totalViewBlocked = TotalSessionDamageBlocked;
				foreach (var kvp in totalRecords)
				{
					if (!resultDict.TryGetValue(kvp.Key, out var existing))
					{
						existing = new DamageSourceRecord
						{
							Id = kvp.Value.Id,
							DisplayName = kvp.Value.DisplayName,
							Color = kvp.Value.Color,
							Rarity = kvp.Value.Rarity,
							SourceClass = kvp.Value.SourceClass,
							IsProtocol = kvp.Value.IsProtocol,
							IsWeapon = kvp.Value.IsWeapon
						};
						resultDict[kvp.Key] = existing;
					}

					existing.TotalDamage = kvp.Value.TotalDamage;
					existing.HitCount = kvp.Value.HitCount;
					existing.CritCount = kvp.Value.CritCount;
					existing.MaxHit = kvp.Value.MaxHit;
					existing.DamageBlocked = kvp.Value.DamageBlocked;
					existing.BlockCount = kvp.Value.BlockCount;
					existing.HealingDone = kvp.Value.HealingDone;
					existing.HealCount = kvp.Value.HealCount;
				}
			}
			else
			{
				// 10-Minute window
				float cutoff = globalTime - TenMinutesSeconds;
				foreach (var hit in historyQueue)
				{
					if (hit.Timestamp < cutoff)
						continue;

					totalViewDamage += hit.Damage;

					if (!resultDict.TryGetValue(hit.SourceId, out var rec))
					{
						// Check totalRecords for meta
						if (totalRecords.TryGetValue(hit.SourceId, out var meta))
						{
							rec = new DamageSourceRecord
							{
								Id = meta.Id,
								DisplayName = meta.DisplayName,
								Color = meta.Color,
								Rarity = meta.Rarity,
								SourceClass = meta.SourceClass,
								IsProtocol = meta.IsProtocol,
								IsWeapon = meta.IsWeapon
							};
						}
						else
						{
							rec = new DamageSourceRecord
							{
								Id = hit.SourceId,
								DisplayName = hit.SourceId,
								Color = Color.White
							};
						}
						resultDict[hit.SourceId] = rec;
					}

					rec.TotalDamage += hit.Damage;
					rec.HitCount++;
					if (hit.IsCrit)
						rec.CritCount++;
					if (hit.Damage > rec.MaxHit)
						rec.MaxHit = hit.Damage;
				}

				foreach (var block in historyBlockedQueue)
				{
					if (block.Timestamp < cutoff)
						continue;

					totalViewBlocked += block.DamageBlocked;

					if (!resultDict.TryGetValue(block.SourceId, out var rec))
					{
						if (totalRecords.TryGetValue(block.SourceId, out var meta))
						{
							rec = new DamageSourceRecord
							{
								Id = meta.Id,
								DisplayName = meta.DisplayName,
								Color = meta.Color,
								Rarity = meta.Rarity,
								SourceClass = meta.SourceClass,
								IsProtocol = meta.IsProtocol,
								IsWeapon = meta.IsWeapon
							};
						}
						else
						{
							rec = new DamageSourceRecord
							{
								Id = block.SourceId,
								DisplayName = block.SourceId,
								Color = Color.White
							};
						}
						resultDict[block.SourceId] = rec;
					}

					rec.DamageBlocked += block.DamageBlocked;
					rec.BlockCount++;
				}

				// Healing in the 10-minute window
				foreach (var heal in historyHealQueue)
				{
					if (heal.Timestamp < cutoff)
						continue;

					if (!resultDict.TryGetValue(heal.SourceId, out var rec))
					{
						if (totalRecords.TryGetValue(heal.SourceId, out var meta))
						{
							rec = new DamageSourceRecord
							{
								Id = meta.Id,
								DisplayName = meta.DisplayName,
								Color = meta.Color,
								Rarity = meta.Rarity,
								SourceClass = meta.SourceClass,
								IsProtocol = meta.IsProtocol,
								IsWeapon = meta.IsWeapon
							};
						}
						else
						{
							rec = new DamageSourceRecord { Id = heal.SourceId, DisplayName = heal.SourceId, Color = Color.White };
						}
						resultDict[heal.SourceId] = rec;
					}

					rec.HealingDone += heal.Amount;
					rec.HealCount++;
				}
			}

			var sortedList = resultDict.Values
				.OrderByDescending(r => r.TotalDamage + r.DamageBlocked + r.HealingDone)
				.ThenBy(r => r.DisplayName)
				.ToList();

			return (totalViewDamage, totalViewBlocked, sortedList);
		}

		public static void Reset()
		{
			totalRecords.Clear();
			historyQueue.Clear();
			historyBlockedQueue.Clear();
			historyHealQueue.Clear();
			TotalSessionHealing = 0;
			TotalSessionDamage = 0;
			TotalSessionDamageBlocked = 0;
			SessionDuration = 0f;
			timeSinceLastHit = 999f;
			globalTime = 0f;
		}

		public static void TogglePause()
		{
			IsPaused = !IsPaused;
		}
	}
}
