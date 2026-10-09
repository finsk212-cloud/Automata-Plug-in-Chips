using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Augments.Core;

namespace Augments
{
	public enum LoadoutOp : byte
	{
		Stash,
		Unstash,
		SaveLoadout,
		BuyLoadoutSlot,
		ApplyLoadout
	}

	// Stash (free uninstall/reinstall of owned chips) and the 3 loadout presets built on it.
	public partial class AugmentPlayer
	{
		public const int MaxLoadouts = 3;
		public const int ApplyCooldownTicks = 180;
		public const int RecentlyHurtTicks = 300;

		private readonly HashSet<string> stashedIds = new HashSet<string>();
		private readonly List<string>[] loadouts = new List<string>[MaxLoadouts]
		{
			new List<string>(), new List<string>(), new List<string>()
		};
		private int loadoutSlotsUnlocked = 1;
		private int activeLoadout = -1;
		private long lastHurtTick = -100000;
		private long lastLoadoutApplyTick = -100000;

		public IReadOnlySet<string> StashedIds => stashedIds;
		public int LoadoutSlotsUnlocked => loadoutSlotsUnlocked;
		public int ActiveLoadout => activeLoadout;
		public IReadOnlyList<string> GetLoadout(int slot) => slot >= 0 && slot < MaxLoadouts ? loadouts[slot] : new List<string>();

		public static int GetLoadoutSlotCost(int slot) => slot switch { 1 => 5, 2 => 10, _ => 0 };

		internal void MarkHurtForLoadoutLock() => lastHurtTick = (long)Main.GameUpdateCount;

		internal void ResetLoadoutState()
		{
			stashedIds.Clear();
			foreach (var l in loadouts)
				l.Clear();
			loadoutSlotsUnlocked = 1;
			activeLoadout = -1;
		}

		// Null when swapping is allowed, otherwise a short player-facing reason.
		public string GetLoadoutLockReason()
		{
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (n.active && n.boss)
					return "Locked during a boss fight";
			}
			long now = (long)Main.GameUpdateCount;
			if (now - lastHurtTick < RecentlyHurtTicks)
				return "Locked: recently took damage";
			if (now - lastLoadoutApplyTick < ApplyCooldownTicks)
				return "Loadout swap on cooldown";
			return null;
		}

		// Routes to the server in multiplayer, runs directly otherwise.
		public void RequestLoadoutOp(LoadoutOp op, int slot = 0, string id = "")
		{
			if (Main.netMode == NetmodeID.MultiplayerClient)
			{
				AugmentNet.SendLoadoutRequest(op, slot, id);
				return;
			}
			ExecuteLoadoutOp(op, slot, id);
		}

		internal bool ExecuteLoadoutOp(LoadoutOp op, int slot, string id)
		{
			if (Main.netMode == NetmodeID.MultiplayerClient)
				return false;

			bool changed;
			switch (op)
			{
				case LoadoutOp.Stash: changed = StashAugment(id); break;
				case LoadoutOp.Unstash: changed = UnstashAugment(id); break;
				case LoadoutOp.SaveLoadout: changed = SaveLoadout(slot); break;
				case LoadoutOp.BuyLoadoutSlot: changed = BuyLoadoutSlot(slot); break;
				case LoadoutOp.ApplyLoadout: changed = ApplyLoadout(slot); break;
				default: return false;
			}

			if (changed && Main.netMode == NetmodeID.Server)
				AugmentNet.SendSyncPlayer(Player);
			return changed;
		}

		private bool StashAugment(string id)
		{
			Augment a = AugmentDatabase.GetById(id);
			if (a == null || a.IsPermanent || !ownedIds.Contains(id))
				return false;
			string lockReason = GetLoadoutLockReason();
			if (lockReason != null && lockReason != "Loadout swap on cooldown")
			{
				NotifyPlayer(lockReason, new Color(255, 140, 140));
				return false;
			}
			ownedIds.Remove(id);
			stashedIds.Add(id);
			activeLoadout = -1;
			RebuildOwnedCacheFromOwnedIdsOnly();
			return true;
		}

		private bool UnstashAugment(string id)
		{
			Augment a = AugmentDatabase.GetById(id);
			if (a == null || !stashedIds.Contains(id) || ownedIds.Count >= MaxOwnedAugments)
				return false;
			string lockReason = GetLoadoutLockReason();
			if (lockReason != null && lockReason != "Loadout swap on cooldown")
			{
				NotifyPlayer(lockReason, new Color(255, 140, 140));
				return false;
			}
			stashedIds.Remove(id);
			ownedIds.Add(id);
			activeLoadout = -1;
			RebuildOwnedCacheFromOwnedIdsOnly();
			return true;
		}

		private bool SaveLoadout(int slot)
		{
			if (slot < 0 || slot >= loadoutSlotsUnlocked)
				return false;
			loadouts[slot].Clear();
			foreach (string id in ownedIds)
			{
				Augment a = AugmentDatabase.GetById(id);
				if (a != null && !a.IsPermanent)
					loadouts[slot].Add(id);
			}
			activeLoadout = slot;
			NotifyPlayer($"Loadout {slot + 1} saved.", new Color(120, 220, 255));
			return true;
		}

		private bool BuyLoadoutSlot(int slot)
		{
			// Slots unlock strictly in order.
			if (slot != loadoutSlotsUnlocked || slot < 1 || slot >= MaxLoadouts)
				return false;

			int cost = GetLoadoutSlotCost(slot);
			int essenceType = ModContent.ItemType<AugmentEssenceItem>();
			if (Player.CountItem(essenceType, cost) < cost)
			{
				NotifyPlayer($"Need {cost} Machine Cores.", new Color(255, 140, 140));
				return false;
			}
			for (int i = 0; i < cost; i++)
				Player.ConsumeItem(essenceType);

			loadoutSlotsUnlocked++;
			if (Main.netMode == NetmodeID.Server)
				SyncInventory();
			NotifyPlayer($"Loadout {slot + 1} unlocked.", new Color(255, 215, 0));
			return true;
		}

		private bool ApplyLoadout(int slot)
		{
			if (slot < 0 || slot >= loadoutSlotsUnlocked || loadouts[slot].Count == 0)
				return false;

			string lockReason = GetLoadoutLockReason();
			if (lockReason != null)
			{
				NotifyPlayer(lockReason, new Color(255, 140, 140));
				return false;
			}

			// Permanent chips can never leave; they keep their place.
			var result = new List<string>();
			foreach (string id in ownedIds)
			{
				Augment a = AugmentDatabase.GetById(id);
				if (a != null && a.IsPermanent)
					result.Add(id);
			}

			var skipped = new List<string>();
			foreach (string id in loadouts[slot])
			{
				Augment a = AugmentDatabase.GetById(id);
				bool available = a != null && (ownedIds.Contains(id) || stashedIds.Contains(id));
				if (!available || result.Contains(id) || result.Count >= MaxOwnedAugments)
				{
					if (a != null && !result.Contains(id))
						skipped.Add(a.DisplayName);
					continue;
				}
				result.Add(id);
			}

			// Everything not in the result goes to the stash; cooldown timers are untouched.
			foreach (string id in ownedIds.ToList())
			{
				if (!result.Contains(id))
					stashedIds.Add(id);
			}
			foreach (string id in result)
				stashedIds.Remove(id);

			ownedIds.Clear();
			foreach (string id in result)
				ownedIds.Add(id);

			activeLoadout = slot;
			lastLoadoutApplyTick = (long)Main.GameUpdateCount;
			RebuildOwnedCacheFromOwnedIdsOnly();

			NotifyPlayer($"Loadout {slot + 1} applied.", new Color(120, 220, 255));
			if (skipped.Count > 0)
				NotifyPlayer("Unavailable (archived): " + string.Join(", ", skipped), new Color(255, 200, 120));
			return true;
		}
	}
}
