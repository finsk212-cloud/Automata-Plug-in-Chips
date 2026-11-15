using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace Augments
{
	// A small summary of each character, saved to one file so the title screen can show it before any world is loaded.
	//
	//   * ProfileSnapshotPlayer writes the summary whenever a character is saved (leaving a world, autosave).
	//   * ProfileStore.Load reads the file once while the mod loads, and the title screen shows the most recently
	//     played character: Machine Cores, rank, plugins collected, bosses defeated.
	//
	// The file lives in the Terraria save folder (AugmentsProfile.json). It is a plain local file that you can edit, so it
	// is for display and cosmetic use only: nothing in the game's rules should ever trust it.
	public static class ProfileStore
	{
		// Bumped when the Archive collection has to be reset for everyone. A profile file saved before this reads as 0, so the first
		// launch of the update clears the collected plugins once (update 1.4.0 is reset 1).
		public const int CurrentResetVersion = 1;

		public sealed class CharacterRecord
		{
			public string Name { get; set; } = "";
			public long LastPlayedUtcTicks { get; set; }
			public int MachineCores { get; set; }
			public int EquippedPlugins { get; set; }
			public int BossesDefeated { get; set; }
			public int TotalBossKills { get; set; }
			// Loadout slots unlocked (1 to 3). Files saved before this was recorded read as 1, the free slot.
			public int LoadoutSlots { get; set; } = 1;
			public List<string> EverOwned { get; set; } = new List<string>();
			// The highest 3 second damage rate this character has reached. 0 = not recorded (older saves).
			public int BestDps { get; set; }
			// Plugin ids pinned on the Profile page (up to 3, "" = empty slot). Chosen on the title screen, so a save must keep them.
			public List<string> Favourites { get; set; } = new List<string>();

			public int Collected => EverOwned?.Count ?? 0;

			// Rank is worked out from progress: each different boss counts 10, each plugin collected 2, each boss kill 1.
			// Every 50 points is one rank.
			public const int PointsPerRank = 50;

			public int Points => BossesDefeated * 10 + Collected * 2 + TotalBossKills;
			public int Rank => 1 + Points / PointsPerRank;
			public float RankProgress => (Points % PointsPerRank) / (float)PointsPerRank;
			// How far into the current rank (0 to 49), for the "24 / 50" shown beside the rank bar.
			public int PointsIntoRank => Points % PointsPerRank;
		}

		private sealed class ProfileFile
		{
			public int ResetVersion { get; set; }
			public List<CharacterRecord> Characters { get; set; } = new List<CharacterRecord>();
		}

		private static ProfileFile file = new ProfileFile();

		private static string FilePath => Path.Combine(Main.SavePath, "AugmentsProfile.json");

		// Every saved character, most recently played first. Cached, because the title screen asks every frame.
		private static List<CharacterRecord> ordered;

		public static IReadOnlyList<CharacterRecord> All
		{
			get
			{
				if (ordered == null)
					ordered = file.Characters.OrderByDescending(c => c.LastPlayedUtcTicks).ToList();
				return ordered;
			}
		}

		// The most recently played character, or null if there is no profile yet.
		public static CharacterRecord Latest => All.Count > 0 ? All[0] : null;

		// The character the title screen is showing. It starts as the latest one; the arrows on the Shop and Profile pages
		// change it. In a world there is no choice to make, so it is always the latest save.
		private static string selectedName;

		public static CharacterRecord Selected
		{
			get
			{
				if (Main.gameMenu && selectedName != null)
				{
					foreach (CharacterRecord c in All)
					{
						if (string.Equals(c.Name, selectedName, StringComparison.Ordinal))
							return c;
					}
				}

				return Latest;
			}
		}

		// Moves the selection one character forward (1) or back (-1), wrapping around.
		public static void CycleSelected(int direction)
		{
			if (All.Count < 2)
				return;

			CharacterRecord current = Selected;
			int index = 0;
			for (int i = 0; i < All.Count; i++)
			{
				if (ReferenceEquals(All[i], current))
				{
					index = i;
					break;
				}
			}

			index = ((index + direction) % All.Count + All.Count) % All.Count;
			selectedName = All[index].Name;
		}

		// Shows this character (picked from the list on the Profile page).
		public static void Select(CharacterRecord rec)
		{
			if (rec != null)
				selectedName = rec.Name;
		}

		public static void Load()
		{
			ordered = null;
			file = new ProfileFile();
			try
			{
				if (File.Exists(FilePath))
				{
					ProfileFile loaded = JsonSerializer.Deserialize<ProfileFile>(File.ReadAllText(FilePath));
					if (loaded?.Characters != null)
					{
						file = loaded;

						// One time reset of the Archive collection: every character in the file starts again from zero.
						if (file.ResetVersion < CurrentResetVersion)
						{
							foreach (CharacterRecord c in file.Characters)
								c.EverOwned = new List<string>();
							file.ResetVersion = CurrentResetVersion;
							WriteFile();
						}
					}
				}
				else
				{
					file.ResetVersion = CurrentResetVersion;
				}
			}			catch (Exception e)
			{
				// A damaged file just means "no profile yet": it is rewritten the next time a character saves.
				ModContent.GetInstance<Augments>().Logger.Warn("The profile file could not be read and will be replaced: " + e.Message);
				ordered = null;
				file = new ProfileFile();
			}
		}

		public static void Unload()
		{
			ordered = null;
			file = new ProfileFile();
		}

		// Writes the profile file. A temporary file is written first so a crash halfway through never leaves a half-written profile.
		private static void WriteFile()
		{
			Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
			string temp = FilePath + ".tmp";
			File.WriteAllText(temp, JsonSerializer.Serialize(file, new JsonSerializerOptions { WriteIndented = true }));
			File.Copy(temp, FilePath, true);
			File.Delete(temp);
		}

		// Pins a plugin in one of the three favourite slots of a character (id "" clears the slot) and saves the file. A plugin can only be
		// pinned once, so pinning it again moves it. Only the title screen calls this.
		public static void SetFavourite(CharacterRecord rec, int slot, string id)
		{
			if (rec == null || slot < 0 || slot > 2)
				return;

			id = id ?? "";
			if (rec.Favourites == null)
				rec.Favourites = new List<string>();
			while (rec.Favourites.Count < 3)
				rec.Favourites.Add("");

			if (id.Length > 0)
			{
				for (int i = 0; i < 3; i++)
				{
					if (rec.Favourites[i] == id)
						rec.Favourites[i] = "";
				}
			}

			rec.Favourites[slot] = id;

			try
			{
				WriteFile();
			}
			catch (Exception e)
			{
				ModContent.GetInstance<Augments>().Logger.Warn("The pinned plugins could not be saved: " + e.Message);
			}
		}

		// Updates the summary for this character and writes the file.
		public static void Record(Player player)
		{
			try
			{
				if (player == null || Main.dedServ || player.whoAmI != Main.myPlayer || string.IsNullOrEmpty(player.name))
					return;

				AugmentPlayer ap = player.GetModPlayer<AugmentPlayer>();
				var record = new CharacterRecord
				{
					Name = player.name,
					LastPlayedUtcTicks = DateTime.UtcNow.Ticks,
					MachineCores = player.CountItem(ModContent.ItemType<AugmentEssenceItem>(), int.MaxValue),
					EquippedPlugins = ap.Owned.Count,
					BossesDefeated = ap.BossAugmentKills.Count,
					TotalBossKills = ap.BossAugmentKills.Values.Sum(),
					LoadoutSlots = ap.LoadoutSlotsUnlocked,
					EverOwned = ap.ArchiveCollectedIds.OrderBy(id => id, StringComparer.Ordinal).ToList(),
						BestDps = (int)Math.Min(int.MaxValue, Math.Round(ap.BestDps))
				};

				// The pinned plugins are set from the title screen and live only in this file, so carry them over.
				CharacterRecord previous = file.Characters.Find(c => string.Equals(c.Name, record.Name, StringComparison.Ordinal));
				if (previous?.Favourites != null)
					record.Favourites = new List<string>(previous.Favourites);

				file.Characters.RemoveAll(c => string.Equals(c.Name, record.Name, StringComparison.Ordinal));
				file.Characters.Add(record);
				ordered = null;

				file.ResetVersion = CurrentResetVersion;
				WriteFile();
			}
			catch (Exception e)
			{
				ModContent.GetInstance<Augments>().Logger.Warn("The profile summary could not be saved: " + e.Message);
			}
		}
	}

	// Writes the profile summary each time the character is saved.
	public class ProfileSnapshotPlayer : ModPlayer
	{
		public override void SaveData(TagCompound tag)
		{
			ProfileStore.Record(Player);
		}

		// tModLoader refuses to load a player class that overrides only one of SaveData / LoadData, so this exists
		// to pair with SaveData. There is nothing to load: the summary lives in its own file, not in the character.
		public override void LoadData(TagCompound tag)
		{
		}
	}
}
