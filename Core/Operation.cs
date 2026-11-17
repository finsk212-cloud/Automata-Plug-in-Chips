using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Augments
{
	public enum RewardKind
	{
		Fragments, // a number of Data Fragments
		Scheme,    // a title-screen colour scheme that cannot be bought in the Shop
		Frame,     // a panel frame that cannot be bought in the Shop
		PodSkin,   // a colour for the Pod beside you in game
		Title,     // a title line shown on the Profile page
		Emblem,    // a small emblem shown on the Profile page
		TierDesign // how the plugin card borders are built (Shop only, never an Operation reward)
	}

	public sealed class OperationReward
	{
		public RewardKind Kind;
		public int Amount;  // Fragments only
		public string Id;   // the item's id for every other kind

		// The id kept in the account's unlock list ("" for fragments, which are not an item).
		public string UnlockId
		{
			get
			{
				switch (Kind)
				{
					case RewardKind.Scheme: return "scheme:" + Id;
					case RewardKind.Frame: return "frame:" + Id;
					case RewardKind.PodSkin: return "podskin:" + Id;
					case RewardKind.Title: return "title:" + Id;
					case RewardKind.Emblem: return "emblem:" + Id;
					default: return "";
				}
			}
		}

		// The slot the item is equipped into (see AccountStore.Equip), or null if it cannot be equipped.
		public string EquipSlot
		{
			get
			{
				switch (Kind)
				{
					case RewardKind.Scheme: return "scheme";
					case RewardKind.Frame: return "frame";
					case RewardKind.PodSkin: return "podskin";
					case RewardKind.Title: return "title";
					case RewardKind.Emblem: return "emblem";
					default: return null;
				}
			}
		}

		// One short sentence about what the reward does.
		public string Description
		{
			get
			{
				switch (Kind)
				{
					case RewardKind.Fragments: return "Added to your Data Fragments when claimed. Spend them in the Shop.";
					case RewardKind.Scheme: return "Recolours the whole title screen. Not sold in the Shop.";
					case RewardKind.Frame: return "New corner brackets around every panel. Not sold in the Shop.";
					case RewardKind.PodSkin: return "Recolours the Pod that floats beside you in game.";
					case RewardKind.Title: return "A title line shown under your name on the Profile page.";
					default: return "A badge shown on your Profile page.";
				}
			}
		}

		public string KindLabel
		{
			get
			{
				switch (Kind)
				{
					case RewardKind.Fragments: return "DATA FRAGMENTS";
					case RewardKind.Scheme: return "COLOUR SCHEME";
					case RewardKind.Frame: return "PANEL FRAME";
					case RewardKind.PodSkin: return "POD SKIN";
					case RewardKind.Title: return "PROFILE TITLE";
					default: return "PROFILE EMBLEM";
				}
			}
		}

		public string Name
		{
			get
			{
				switch (Kind)
				{
					case RewardKind.Fragments: return Amount + " FRAGMENTS";
					case RewardKind.Scheme: return Cosmetic.SchemeName(Id);
					case RewardKind.Frame: return Cosmetic.FrameName(Id);
					case RewardKind.PodSkin: return Cosmetic.PodSkinName(Id);
					case RewardKind.Title: return Cosmetic.TitleName(Id);
					default: return Cosmetic.EmblemName(Id);
				}
			}
		}
	}

	// The cosmetics that Operations hand out and the places that draw them. Ids are saved in the account file, so never rename one.
	public static class Cosmetic
	{
		public sealed class PodSkinInfo
		{
			public string Id;
			public string Name;
			public Color Tint;
			// A skin earned from a hidden ending has its own look (see PodSprites.StylePalette) instead of just a tint; this names it.
			// Empty for the ordinary tinted skins. `Tint` is still the colour used where only one colour fits.
			public string Style = "";
			}

		public static readonly PodSkinInfo[] PodSkins =
		{
			new PodSkinInfo { Id = "default", Name = "STANDARD", Tint = Color.White },
			new PodSkinInfo { Id = "amber", Name = "AMBER", Tint = new Color(255, 205, 120) },
			new PodSkinInfo { Id = "frost", Name = "FROST", Tint = new Color(150, 210, 255) },
			new PodSkinInfo { Id = "verdant", Name = "VERDANT", Tint = new Color(150, 255, 175) },
			new PodSkinInfo { Id = "violet", Name = "VIOLET", Tint = new Color(205, 155, 255) },
			new PodSkinInfo { Id = "gold", Name = "GOLD", Tint = new Color(255, 226, 110) },
			// Ending rewards (see Endings.Rewards). Never sold, never an Operation reward.
			new PodSkinInfo { Id = "untouched", Name = "UNTOUCHED", Tint = new Color(235, 245, 255), Style = "pearl" },
			new PodSkinInfo { Id = "smiley", Name = "SMILEY", Tint = new Color(251, 191, 36), Style = "sticker" },
			new PodSkinInfo { Id = "hologram", Name = "HOLOGRAM", Tint = new Color(120, 230, 225), Style = "hologram" },
			new PodSkinInfo { Id = "scarred", Name = "PAIN TOLERANCE", Tint = new Color(255, 90, 60), Style = "scarred" },
			new PodSkinInfo { Id = "glory", Name = "GLORY TO MANKIND", Tint = new Color(255, 214, 90), Style = "glory" }
		};

		public static readonly string[][] Titles =
		{
			new[] { "recruit", "RECRUIT" },
			new[] { "operator", "OPERATOR" },
			new[] { "vanguard", "VANGUARD" },
			new[] { "veteran", "VETERAN" },
			new[] { "commander", "COMMANDER" },
			new[] { "legend", "LEGEND" },
			// Ending rewards (see Endings.Rewards): one title for each hidden ending, A to E.
			new[] { "untouched", "UNTOUCHED" },
			new[] { "basicinstinct", "BASIC INSTINCT" },
			new[] { "emptychassis", "EMPTY CHASSIS" },
			new[] { "paintolerance", "PAIN TOLERANCE" },
			new[] { "glory", "GLORY TO MANKIND" }
		};

		public static readonly string[][] Emblems =
		{
			new[] { "diamond", "DIAMOND" },
			new[] { "cross", "CROSS" },
			new[] { "hexagon", "HEXAGON" },
			new[] { "star", "STAR" },
			new[] { "crown", "CROWN" }
		};

		public static PodSkinInfo FindPodSkin(string id)
		{
			foreach (PodSkinInfo s in PodSkins)
			{
				if (s.Id == id)
					return s;
			}
			return PodSkins[0];
		}

		public static string PodSkinName(string id)
		{
			foreach (PodSkinInfo s in PodSkins)
			{
				if (s.Id == id)
					return s.Name + " POD";
			}
			return id.ToUpperInvariant();
		}

		// What the Shop charges, in Data Fragments. -1 means it is not for sale: it is a Campaign reward only. The simple ones are sold,
		// and the better ones (Verdant, Violet and Gold Pods, the Vanguard to Legend titles, the Hexagon, Star and Crown emblems) are earned.
		public static int PodSkinPrice(string id)
		{
			switch (id)
			{
				case "amber": return 150;
				case "frost": return 200;
				default: return -1;
			}
		}

		public static int TitlePrice(string id)
		{
			switch (id)
			{
				case "recruit": return 100;
				case "operator": return 150;
				default: return -1;
			}
		}

		public static int EmblemPrice(string id)
		{
			switch (id)
			{
				case "diamond": return 120;
				case "cross": return 180;
				default: return -1;
			}
		}

		public static string TitleName(string id) => Lookup(Titles, id);
		public static string EmblemName(string id) => Lookup(Emblems, id);

		public static string SchemeName(string id)
		{
			foreach (MenuLook.Scheme s in MenuLook.All)
			{
				if (s.Id == id)
					return s.Name;
			}
			return id.ToUpperInvariant();
		}

		public static string FrameName(string id)
		{
			foreach (MenuLook.FrameStyle f in MenuLook.Frames)
			{
				if (f.Id == id)
					return f.Name;
			}
			return id.ToUpperInvariant();
		}

		private static string Lookup(string[][] table, string id)
		{
			foreach (string[] row in table)
			{
				if (row[0] == id)
					return row[1];
			}
			return id.ToUpperInvariant();
		}

		// The Pod colour you have equipped (white if none, or if the file names one you do not own).
		public static Color EquippedPodTint
		{
			get
			{
				string id = AccountStore.Equipped("podskin", "default");
				if (id == "default" || !AccountStore.IsUnlocked("podskin:" + id))
					return Color.White;

				foreach (PodSkinInfo s in PodSkins)
				{
					if (s.Id == id)
						return s.Tint;
				}
				return Color.White;
			}
		}

		// The position of the equipped Pod skin in PodSkins (0 = standard).
		public static int EquippedPodIndex
		{
			get
			{
				string id = EquippedPodId;
				for (int i = 0; i < PodSkins.Length; i++)
				{
					if (PodSkins[i].Id == id)
						return i;
				}
				return 0;
			}
		}

		// The id of the equipped Pod skin ("default" if none, or if the file names one you do not own).
		public static string EquippedPodId
		{
			get
			{
				string id = AccountStore.Equipped("podskin", "default");
				return id != "default" && AccountStore.IsUnlocked("podskin:" + id) ? id : "default";
			}
		}

		// The equipped title, or "" if none or not owned.
		public static string EquippedTitle
		{
			get
			{
				string id = AccountStore.Equipped("title", "none");
				return id != "none" && AccountStore.IsUnlocked("title:" + id) ? TitleName(id) : "";
			}
		}

		// The equipped emblem's id, or "" if none or not owned.
		public static string EquippedEmblem
		{
			get
			{
				string id = AccountStore.Equipped("emblem", "none");
				return id != "none" && AccountStore.IsUnlocked("emblem:" + id) ? id : "";
			}
		}
	}

	// An Operation: a track of tiers filled by Operation XP. One ships with each update and none ever expires on a date, so an
	// older one can still be finished after a newer one arrives. XP comes from the same things that pay Data Fragments (active
	// play and Orders), never from items, and goes to the current Operation only.
	public sealed class Operation
	{
		public string Id;
		public string Number;     // "01"
		public string Name;
		public int XpPerTier;
		public OperationReward[] Rewards; // Rewards[0] is tier 1

		public int TierCount => Rewards.Length;

		// The claim key of each tier, built once (the page asks for them many times a frame).
		internal string[] KeyCache;
	}

	public static class Operations
	{
		public const int XpPerActiveMinute = 5;

		private static OperationReward F(int amount) => new OperationReward { Kind = RewardKind.Fragments, Amount = amount };
		private static OperationReward R(RewardKind kind, string id) => new OperationReward { Kind = kind, Id = id };

		public static readonly Operation Current = new Operation
		{
			Id = "op01",
			Number = "01",
			Name = "BUNKER PERIMETER",
			XpPerTier = 300,
			Rewards = new[]
			{
				F(50),                              // 1
				F(50),                              // 2 (the Recruit title is sold in the Shop now)
				F(75),                              // 3
				F(75),                              // 4 (the Diamond emblem is sold in the Shop now)
				F(75),                              // 5 (the Amber Pod is sold in the Shop now)
				F(75),                              // 6
				F(100),                             // 7 (the Operator title is sold in the Shop now)
				F(100),                             // 8
				F(100),                             // 9 (the Cross emblem is sold in the Shop now)
				R(RewardKind.Scheme, "void"),       // 10
				F(100),                             // 11
				F(100),                             // 12 (the Frost Pod is sold in the Shop now)
				R(RewardKind.Title, "vanguard"),    // 13
				F(100),                             // 14
				R(RewardKind.Frame, "circuit"),     // 15
				F(125),                             // 16
				R(RewardKind.Emblem, "hexagon"),    // 17
				R(RewardKind.PodSkin, "verdant"),   // 18
				F(125),                             // 19
				R(RewardKind.Scheme, "ember"),      // 20
				R(RewardKind.Title, "veteran"),     // 21
				F(150),                             // 22
				R(RewardKind.Emblem, "star"),       // 23
				R(RewardKind.PodSkin, "violet"),    // 24
				F(150),                             // 25
				R(RewardKind.Title, "commander"),   // 26
				F(175),                             // 27
				R(RewardKind.Emblem, "crown"),      // 28
				R(RewardKind.PodSkin, "gold"),      // 29
				R(RewardKind.Title, "legend")       // 30
			}
		};

		public static long Xp(Operation op) => AccountStore.OperationXp(op.Id);

		// How many tiers are reached (0 to TierCount).
		public static int TierReached(Operation op) => (int)Math.Min(op.TierCount, Xp(op) / op.XpPerTier);

		// How far through the next tier you are (0 to 1; 1 when the whole Operation is done).
		public static float ProgressToNext(Operation op)
		{
			if (TierReached(op) >= op.TierCount)
				return 1f;
			return (Xp(op) % op.XpPerTier) / (float)op.XpPerTier;
		}

		// The key names the reward as well as the tier, so if a tier's reward is ever swapped for another item, the new one can be claimed.
		// (Changing only the number of fragments keeps the key, so it can never pay twice.)
		public static string RewardKey(Operation op, int tier)
		{
			if (op.KeyCache == null)
			{
				var keys = new string[op.TierCount];
				for (int i = 0; i < keys.Length; i++)
				{
					OperationReward reward = op.Rewards[i];
					keys[i] = op.Id + ":" + (i + 1) + ":" + (reward.Kind == RewardKind.Fragments ? "fragments" : reward.UnlockId);
				}
				op.KeyCache = keys;
			}
			return op.KeyCache[tier - 1];
		}

		public static bool IsClaimed(Operation op, int tier) => AccountStore.IsRewardClaimed(RewardKey(op, tier));
		public static bool IsClaimable(Operation op, int tier) => tier >= 1 && tier <= TierReached(op) && !IsClaimed(op, tier);

		public static int ClaimableCount(Operation op)
		{
			int n = 0;
			for (int tier = 1; tier <= TierReached(op); tier++)
			{
				if (!IsClaimed(op, tier))
					n++;
			}
			return n;
		}

		// The next reward you have not reached yet, as a tier number (or 0 when every tier is reached).
		public static int NextTier(Operation op)
		{
			int reached = TierReached(op);
			return reached >= op.TierCount ? 0 : reached + 1;
		}

		// Claims the reward of one tier if it is reached and not yet claimed.
		public static bool TryClaim(Operation op, int tier)
		{
			if (!IsClaimable(op, tier))
				return false;

			OperationReward reward = op.Rewards[tier - 1];
			return AccountStore.ClaimReward(RewardKey(op, tier), reward.Kind == RewardKind.Fragments ? reward.Amount : 0, reward.UnlockId);
		}

		// Whether an item reward is the one currently equipped.
		public static bool IsEquipped(OperationReward reward)
		{
			switch (reward.Kind)
			{
				case RewardKind.Scheme: return MenuLook.All[MenuLook.SelectedIndex].Id == reward.Id;
				case RewardKind.Frame: return MenuLook.Frames[MenuLook.FrameIndex].Id == reward.Id;
				case RewardKind.PodSkin: return AccountStore.Equipped("podskin", "default") == reward.Id;
				case RewardKind.Title: return AccountStore.Equipped("title", "none") == reward.Id;
				case RewardKind.Emblem: return AccountStore.Equipped("emblem", "none") == reward.Id;
				default: return false;
			}
		}

		// Equips an item reward you own. Equipping the one already on takes it off (Pod skin, title and emblem only).
		public static void Equip(OperationReward reward)
		{
			switch (reward.Kind)
			{
				case RewardKind.Scheme:
					for (int i = 0; i < MenuLook.All.Length; i++)
					{
						if (MenuLook.All[i].Id == reward.Id)
							MenuLook.SelectScheme(i);
					}
					break;
				case RewardKind.Frame:
					for (int i = 0; i < MenuLook.Frames.Length; i++)
					{
						if (MenuLook.Frames[i].Id == reward.Id)
							MenuLook.SelectFrame(i);
					}
					break;
				case RewardKind.PodSkin:
					AccountStore.Equip("podskin", IsEquipped(reward) ? "default" : reward.Id);
					break;
				case RewardKind.Title:
					AccountStore.Equip("title", IsEquipped(reward) ? "none" : reward.Id);
					break;
				case RewardKind.Emblem:
					AccountStore.Equip("emblem", IsEquipped(reward) ? "none" : reward.Id);
					break;
			}
		}

		public static bool IsOwned(OperationReward reward) => !string.IsNullOrEmpty(reward.UnlockId) && AccountStore.IsUnlocked(reward.UnlockId);
	}
}
