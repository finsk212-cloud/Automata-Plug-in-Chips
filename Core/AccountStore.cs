using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Terraria;
using Terraria.ModLoader;

namespace Augments
{
	// The account: things that belong to YOU rather than to one character, kept in one signed file (AugmentsAccount.json).
	//
	//   * Data Fragments, the title-screen currency. Earned ONLY by active play with the mod (see AccountEarningPlayer) and by
	//     completing Orders. It is never read from items or Machine Cores, so item-spawning and cheat tools cannot reach it.
	//   * What you have bought in the Shop (colour schemes, panel frames, later secret plugins) and what is equipped.
	//   * Totals that the Orders are measured against: active play time, distinct bosses defeated, finished orders.
	//
	// About the signature. The file holds a payload and an HMAC of that exact text. A casual edit of the numbers, or a copy
	// from a friend, no longer verifies and the file is set aside. Everything runs on the player's own computer though, so
	// someone who reads this code can still forge a valid file. It stops casual cheating, not a determined one.
	//
	// About saving. Two copies of the game can be open at once, so the store never overwrites the file from memory. It keeps
	// what THIS copy has earned or spent since the last save as "pending" changes, and each save re-reads the file, adds the
	// pending changes to what is there, and writes the result.
	public static class AccountStore
	{
		public sealed class AccountData
		{
			public int Version { get; set; } = 1;
			// Which Data Fragments reset this account has been through. A file saved before the reset reads as 0.
			public int ResetVersion { get; set; }
			public int Balance { get; set; }
			public long LifetimeEarned { get; set; }
			public double PlaySeconds { get; set; }
			public double CoopSeconds { get; set; }
			public HashSet<string> Unlocked { get; set; } = new HashSet<string>();
			public HashSet<string> BossIds { get; set; } = new HashSet<string>();
			public HashSet<string> MissionsDone { get; set; } = new HashSet<string>();
			public Dictionary<string, string> Equipped { get; set; } = new Dictionary<string, string>();
			public Dictionary<string, long> OperationXp { get; set; } = new Dictionary<string, long>();
			public HashSet<string> ClaimedRewards { get; set; } = new HashSet<string>();
		}

		private sealed class Envelope
		{
			public string Payload { get; set; } = "";
			public string Sig { get; set; } = "";
		}

		// Bumped when everyone's Data Fragments have to go back to zero (update 1.4.0 is reset 1). The first launch of the update does it once,
		// then records it in the file, so it never happens again.
		public const int CurrentResetVersion = 1;

		private static AccountData committed = new AccountData { ResetVersion = CurrentResetVersion };

		// Changes made by this copy of the game that are not in the file yet.
		private static int pendingBalance;
		private static long pendingLifetime;
		private static double pendingPlay;
		private static double pendingCoop;
		private static readonly HashSet<string> pendingUnlocks = new HashSet<string>();
		private static readonly HashSet<string> pendingBosses = new HashSet<string>();
		private static readonly Dictionary<string, int> pendingMissions = new Dictionary<string, int>(); // id -> reward already added
		private static readonly Dictionary<string, string> pendingEquipped = new Dictionary<string, string>();
		private static readonly Dictionary<string, long> pendingXp = new Dictionary<string, long>();
		private static readonly Dictionary<string, int> pendingClaims = new Dictionary<string, int>(); // reward key -> fragments already paid

		private static string FilePath => Path.Combine(Main.SavePath, "AugmentsAccount.json");

		private static bool Dirty => pendingBalance != 0 || pendingLifetime != 0 || pendingPlay > 0 || pendingCoop > 0
			|| pendingUnlocks.Count > 0 || pendingBosses.Count > 0 || pendingMissions.Count > 0 || pendingEquipped.Count > 0
			|| pendingXp.Count > 0 || pendingClaims.Count > 0;

		// --- What the rest of the mod reads (the saved file plus this copy's unsaved changes) ---

		public static int Balance => Math.Max(0, committed.Balance + pendingBalance);
		public static long LifetimeEarned => committed.LifetimeEarned + pendingLifetime;
		public static double PlaySeconds => committed.PlaySeconds + pendingPlay;
		public static double CoopSeconds => committed.CoopSeconds + pendingCoop;
		public static int BossCount => CountUnion(committed.BossIds, pendingBosses);

		public static bool IsUnlocked(string id) => committed.Unlocked.Contains(id) || pendingUnlocks.Contains(id);
		public static bool IsMissionDone(string id) => committed.MissionsDone.Contains(id) || pendingMissions.ContainsKey(id);
		public static bool IsRewardClaimed(string key) => committed.ClaimedRewards.Contains(key) || pendingClaims.ContainsKey(key);

		// Operation XP for one Operation (the saved amount plus this copy's unsaved XP).
		public static long OperationXp(string operationId)
		{
			committed.OperationXp.TryGetValue(operationId, out long saved);
			pendingXp.TryGetValue(operationId, out long unsaved);
			return saved + unsaved;
		}

		public static string Equipped(string slot, string fallback)
		{
			if (pendingEquipped.TryGetValue(slot, out string now))
				return now;
			return committed.Equipped.TryGetValue(slot, out string saved) ? saved : fallback;
		}

		private static int CountUnion(HashSet<string> a, HashSet<string> b)
		{
			int n = a.Count;
			foreach (string s in b)
			{
				if (!a.Contains(s))
					n++;
			}
			return n;
		}

		// --- Changes ---

		public static void AddPlay(double seconds, bool withOthers)
		{
			pendingPlay += seconds;
			if (withOthers)
				pendingCoop += seconds;
		}

		public static void Earn(int amount)
		{
			if (amount <= 0)
				return;
			pendingBalance += amount;
			pendingLifetime += amount;
		}

		// A boss the player defeated while playing with the mod. Counts once per boss for the whole account.
		public static void NoteBoss(string id)
		{
			if (!committed.BossIds.Contains(id))
				pendingBosses.Add(id);
		}

		public static void AddOperationXp(string operationId, long amount)
		{
			if (amount <= 0)
				return;
			pendingXp.TryGetValue(operationId, out long now);
			pendingXp[operationId] = now + amount;
		}

		// Claims one Operation reward, once: pays its fragments (if any) and unlocks its item (if any). Returns false if it was
		// already claimed. The caller checks that the tier has been reached.
		public static bool ClaimReward(string key, int fragments, string unlockId)
		{
			if (IsRewardClaimed(key))
				return false;

			pendingClaims[key] = fragments;
			Earn(fragments);
			if (!string.IsNullOrEmpty(unlockId))
				pendingUnlocks.Add(unlockId);
			Flush();
			return true;
		}

		// Marks an order finished and pays its reward, once. Returns false if it was already finished.
		public static bool CompleteMission(string id, int reward)
		{
			if (IsMissionDone(id))
				return false;

			pendingMissions[id] = reward;
			Earn(reward);
			return true;
		}

		// Pays `cost` and unlocks `unlockId`. Returns false (and changes nothing) if you cannot afford it or already own it.
		public static bool TrySpend(int cost, string unlockId)
		{
			if (cost < 0 || IsUnlocked(unlockId) || Balance < cost)
				return false;

			pendingBalance -= cost;
			pendingUnlocks.Add(unlockId);
			Flush(); // a purchase is written at once
			return true;
		}

		public static void Equip(string slot, string id)
		{
			pendingEquipped[slot] = id;
			Flush();
		}

		// --- Load and save ---

		public static void Load()
		{
			try
			{
				AccountData read = ReadFile();
				if (read == null)
				{
					// No account yet (or an unreadable one set aside): it starts empty, and there is nothing to reset.
					committed = new AccountData { ResetVersion = CurrentResetVersion };
				}
				else
				{
					committed = read;
					if (committed.ResetVersion < CurrentResetVersion)
					{
						// One time reset of the Data Fragments (update 1.4.0). Shop unlocks and finished orders are kept.
						committed.Balance = 0;
						committed.LifetimeEarned = 0;
						committed.ResetVersion = CurrentResetVersion;
						WriteFile(committed);
					}
				}
			}
			catch (IOException e)
			{
				// Could not be read right now. Start empty; the next save re-reads the file and merges, so nothing is lost.
				committed = new AccountData();
				ModContent.GetInstance<Augments>().Logger.Warn("The account file could not be read yet: " + e.Message);
			}
			ClearPending();
		}

		public static void Unload()
		{
			Flush();
			committed = new AccountData();
			ClearPending();
		}

		private static void ClearPending()
		{
			pendingBalance = 0;
			pendingLifetime = 0;
			pendingPlay = 0;
			pendingCoop = 0;
			pendingUnlocks.Clear();
			pendingBosses.Clear();
			pendingMissions.Clear();
			pendingEquipped.Clear();
			pendingXp.Clear();
			pendingClaims.Clear();
		}

		// Writes this copy's unsaved changes into the file. Safe to call often; does nothing when there is nothing to save.
		public static void Flush()
		{
			if (Main.dedServ || !Dirty)
				return;

			try
			{
				// Start from whatever is in the file right now, so a second copy of the game is never overwritten.
				AccountData fresh = ReadFile() ?? committed;
				var merged = new AccountData
				{
					Balance = fresh.Balance + pendingBalance,
					LifetimeEarned = fresh.LifetimeEarned + pendingLifetime,
					PlaySeconds = fresh.PlaySeconds + pendingPlay,
					CoopSeconds = fresh.CoopSeconds + pendingCoop,
					ResetVersion = Math.Max(fresh.ResetVersion, committed.ResetVersion),
						Unlocked = new HashSet<string>(fresh.Unlocked),
					BossIds = new HashSet<string>(fresh.BossIds),
					MissionsDone = new HashSet<string>(fresh.MissionsDone),
					Equipped = new Dictionary<string, string>(fresh.Equipped),
					OperationXp = new Dictionary<string, long>(fresh.OperationXp),
					ClaimedRewards = new HashSet<string>(fresh.ClaimedRewards)
				};

				merged.Unlocked.UnionWith(pendingUnlocks);
				merged.BossIds.UnionWith(pendingBosses);

				// An order the other copy already finished must not pay twice: take its reward back out.
				foreach (KeyValuePair<string, int> m in pendingMissions)
				{
					if (!merged.MissionsDone.Add(m.Key))
					{
						merged.Balance -= m.Value;
						merged.LifetimeEarned -= m.Value;
					}
				}

				foreach (KeyValuePair<string, long> x in pendingXp)
				{
					merged.OperationXp.TryGetValue(x.Key, out long have);
					merged.OperationXp[x.Key] = have + x.Value;
				}

				// A reward the other copy already claimed must not pay twice either.
				foreach (KeyValuePair<string, int> c in pendingClaims)
				{
					if (!merged.ClaimedRewards.Add(c.Key))
					{
						merged.Balance -= c.Value;
						merged.LifetimeEarned -= c.Value;
					}
				}

				foreach (KeyValuePair<string, string> e in pendingEquipped)
					merged.Equipped[e.Key] = e.Value;

				merged.Balance = Math.Max(0, merged.Balance);
				merged.LifetimeEarned = Math.Max(0, merged.LifetimeEarned);

				WriteFile(merged);
				committed = merged;
				ClearPending();
			}
			catch (Exception e)
			{
				// Keep the pending changes; the next flush tries again.
				ModContent.GetInstance<Augments>().Logger.Warn("The account file could not be saved: " + e.Message);
			}
		}

		// --- File format ---

		private static byte[] Key()
		{
			using (var sha = SHA256.Create())
				return sha.ComputeHash(Encoding.UTF8.GetBytes("Augments.Account.v1|plug-in-chips|data-fragments"));
		}

		private static string Sign(string payload)
		{
			using (var hmac = new HMACSHA256(Key()))
				return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)));
		}

		private static void WriteFile(AccountData data)
		{
			string payload = JsonSerializer.Serialize(data);
			string text = JsonSerializer.Serialize(new Envelope { Payload = payload, Sig = Sign(payload) }, new JsonSerializerOptions { WriteIndented = true });

			Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
			string temp = FilePath + "." + Environment.ProcessId + ".tmp"; // per copy of the game, so two never share one
			File.WriteAllText(temp, text);
			File.Copy(temp, FilePath, true);
			File.Delete(temp);
		}

		// Returns the saved account, or null if there is no file or it does not verify (a failed one is set aside, not deleted).
		// A file that cannot be READ right now (the other copy of the game is writing it) is not a bad file: that throws an
		// IOException, so the caller keeps its pending changes and tries again later instead of setting a good file aside.
		private static AccountData ReadFile()
		{
			if (!File.Exists(FilePath))
				return null;

			string text = File.ReadAllText(FilePath); // may throw IOException on purpose

			try
			{
				Envelope env = JsonSerializer.Deserialize<Envelope>(text);
				if (env == null || string.IsNullOrEmpty(env.Payload) || !CryptographicOperations.FixedTimeEquals(
						Encoding.UTF8.GetBytes(Sign(env.Payload)), Encoding.UTF8.GetBytes(env.Sig ?? "")))
				{
					SetAside("the signature did not match");
					return null;
				}

				return JsonSerializer.Deserialize<AccountData>(env.Payload);
			}
			catch (JsonException e)
			{
				SetAside(e.Message);
				return null;
			}
		}
		private static void SetAside(string why)
		{
			try
			{
				string aside = FilePath + ".invalid-" + DateTime.UtcNow.Ticks;
				File.Move(FilePath, aside);
				ModContent.GetInstance<Augments>().Logger.Warn("The account file was not accepted (" + why + ") and was set aside as " + Path.GetFileName(aside) + ". A new account starts empty.");
			}
			catch
			{
			}
		}
	}
}
