using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;

namespace Augments.Core
{
	// Anonymous balance statistics, so the author can see which plugins are too strong on which weapons.
	//
	// WHAT IS COLLECTED (only after the player says yes, see StatsConsent):
	//   for each plugin the player has equipped, and each weapon they fought with, and each progress stage:
	//     - the damage that plugin dealt and how many hits (PluginDamage / PluginHits)
	//     - the damage the weapon dealt while that plugin was equipped, and its hits (WeaponDamage / WeaponHits)
	//     - the seconds spent in combat with that pair (Seconds)
	//   and nothing else: the plugin id, an internal weapon key (like "Terraria/Uzi"), the weapon's damage class and the
	//   progress stage. There is NO player name, Steam id, world name, install id, chat text or IP address in what is sent.
	//
	// HOW IT IS NUMBERED. The numbers are plain additions, so the server can add batches from many players together and
	// work out for each plugin and weapon: plugin damage per combat second, and the plugin's share of the total damage.
	//
	// SAFETY. Nothing is collected or sent unless (1) the player turned it on and (2) a server address has been filled
	// in (StatsUploader.Endpoint). Sending runs in the background, never blocks the game and swallows every failure.
	public sealed class StatsBucket
	{
		public string Plugin;
		public string Weapon;
		public int Class;
		public int Stage;
		public long PluginDamage;
		public int PluginHits;
		public long WeaponDamage;
		public int WeaponHits;
		public double Seconds;
		public int Attempts; // failed sends so far; a batch is dropped after a few

		public void Add(StatsBucket other)
		{
			PluginDamage += other.PluginDamage;
			PluginHits += other.PluginHits;
			WeaponDamage += other.WeaponDamage;
			WeaponHits += other.WeaponHits;
			Seconds += other.Seconds;
			Attempts = Math.Max(Attempts, other.Attempts);
		}
	}

	public static class StatsCollector
	{
		private const int MaxBuckets = 400;           // a batch never holds more rows than this
		private const int WeaponWindowTicks = 360;    // a plugin hit is credited to the weapon used in the last 6 seconds
		private const int CombatWindowTicks = 240;    // combat seconds count for 4 seconds after the last hit
		private const int FlushEveryTicks = 60 * 60 * 10; // send what has been gathered every 10 minutes
		private const int MaxAttempts = 3;

		private static readonly object gate = new object();
		private static readonly Dictionary<string, StatsBucket> buckets = new Dictionary<string, StatsBucket>();

		private static string lastWeapon = "none";
		private static AugmentClass lastClass = AugmentClass.Universal;
		private static uint lastWeaponTick;
		private static int flushCounter;

		// On only when the player agreed and there is somewhere to send it.
		public static bool Enabled => !Main.dedServ && StatsUploader.Configured && (AugmentConfig.Instance?.ShareAnonymousStats ?? false);

		// A short, stable, language independent name for a weapon. Item.Name changes with the game language, so it is not used.
		public static string WeaponKey(Item item)
		{
			if (item == null || item.IsAir)
				return "none";

			if (item.ModItem != null)
				return Clean(item.ModItem.FullName);

			return "Terraria/" + (ItemID.Search.TryGetName(item.type, out string name) ? Clean(name) : item.type.ToString());
		}

		// Only letters, digits and a few separators survive, so no free text can ever leave the computer.
		private static string Clean(string text)
		{
			if (string.IsNullOrEmpty(text))
				return "none";

			var sb = new StringBuilder(Math.Min(text.Length, 80));
			foreach (char c in text)
			{
				if (sb.Length >= 80)
					break;
				if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_' || c == '.' || c == '/' || c == '-')
					sb.Append(c);
			}
			return sb.Length == 0 ? "none" : sb.ToString();
		}

		// Called when the local player lands a weapon hit (or a proc that knows its weapon), before the damage is recorded.
		public static void BeginHit(string weaponKey, AugmentClass? weaponClass)
		{
			if (!Enabled)
				return;

			if (!string.IsNullOrEmpty(weaponKey))
				lastWeapon = weaponKey;
			if (weaponClass.HasValue)
				lastClass = weaponClass.Value;
			lastWeaponTick = Main.GameUpdateCount;
		}

		private static string CurrentWeapon() =>
			lastWeaponTick != 0 && Main.GameUpdateCount - lastWeaponTick <= WeaponWindowTicks ? lastWeapon : "none";

		private static StatsBucket Get(string plugin, string weapon)
		{
			string key = plugin + "|" + weapon + "|" + (int)AugmentScaling.CurrentStage;
			if (!buckets.TryGetValue(key, out StatsBucket b))
			{
				if (buckets.Count >= MaxBuckets)
					return null;

				b = new StatsBucket { Plugin = plugin, Weapon = weapon, Class = (int)lastClass, Stage = (int)AugmentScaling.CurrentStage };
				buckets[key] = b;
			}
			return b;
		}

		// The weapon itself hit something: that damage counts for every plugin the player has equipped.
		public static void NoteWeaponHit(int damage)
		{
			if (!Enabled || damage <= 0 || Main.LocalPlayer == null)
				return;

			string weapon = CurrentWeapon();
			lock (gate)
			{
				foreach (Augment a in Main.LocalPlayer.GetModPlayer<AugmentPlayer>().Owned)
				{
					StatsBucket b = Get(a.Id, weapon);
					if (b == null)
						continue;
					b.WeaponDamage += damage;
					b.WeaponHits++;
				}
			}
		}

		// A plugin dealt damage (a proc, or its share of a boosted hit).
		public static void NotePluginDamage(string pluginId, int damage)
		{
			if (!Enabled || damage <= 0 || string.IsNullOrEmpty(pluginId) || AugmentDatabase.GetById(pluginId) == null)
				return;

			lock (gate)
			{
				StatsBucket b = Get(pluginId, CurrentWeapon());
				if (b == null)
					return;
				b.PluginDamage += damage;
				b.PluginHits++;
			}
		}

		// Once per game tick for the local player: counts combat seconds and sends a batch now and then.
		public static void Tick(Player player)
		{
			if (!Enabled || Main.gamePaused)
				return;

			if (++flushCounter >= FlushEveryTicks)
			{
				flushCounter = 0;
				Flush();
			}

			if (lastWeaponTick == 0 || Main.GameUpdateCount - lastWeaponTick > CombatWindowTicks)
				return;

			lock (gate)
			{
				foreach (Augment a in player.GetModPlayer<AugmentPlayer>().Owned)
				{
					StatsBucket b = Get(a.Id, lastWeapon);
					if (b != null)
						b.Seconds += 1.0 / 60.0;
				}
			}
		}

		// Sends what has been gathered and starts again from zero. Safe to call at any time.
		public static void Flush()
		{
			if (!Enabled)
				return;

			List<StatsBucket> rows;
			lock (gate)
			{
				rows = buckets.Values.Where(b => b.PluginHits > 0 || b.WeaponHits > 0 || b.Seconds >= 0.5).ToList();
				buckets.Clear();
			}

			if (rows.Count > 0)
				StatsUploader.Send(rows);
		}

		// A send failed: put the numbers back so the next send carries them, unless it has failed too often.
		internal static void Restore(List<StatsBucket> rows)
		{
			lock (gate)
			{
				foreach (StatsBucket r in rows)
				{
					r.Attempts++;
					if (r.Attempts >= MaxAttempts)
						continue;

					string key = r.Plugin + "|" + r.Weapon + "|" + r.Stage;
					if (buckets.TryGetValue(key, out StatsBucket existing))
						existing.Add(r);
					else if (buckets.Count < MaxBuckets)
						buckets[key] = r;
				}
			}
		}

		public static void Clear()
		{
			lock (gate)
				buckets.Clear();
			lastWeapon = "none";
			lastWeaponTick = 0;
			flushCounter = 0;
		}
	}

	// Sends a batch to the author's server in the background.
	public static class StatsUploader
	{
		// THE ADDRESS OF THE AUTHOR'S STATS SERVER. Left empty on purpose: while it is empty nothing is collected, nothing is
		// sent and the "Share anonymous stats" prompt is not shown. Fill it in with the https address of the deployed server
		// (see server/README.md) when it is ready.
		public const string Endpoint = "";

		public static bool Configured => Endpoint.StartsWith("https://", StringComparison.Ordinal);

		private static readonly HttpClient http = CreateClient();

		private static HttpClient CreateClient()
		{
			var client = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
			client.DefaultRequestHeaders.UserAgent.ParseAdd("AutomataStats/1");
			return client;
		}

		public static void Send(List<StatsBucket> rows)
		{
			if (!Configured || rows == null || rows.Count == 0)
				return;

			string json;
			try
			{
				var payload = new
				{
					v = 1,
					mod = ModContent.GetInstance<Augments>().Version.ToString(),
					cal = ModLoader.HasMod("CalamityMod"),
					rows = rows.Select(r => new
					{
						p = r.Plugin,
						w = r.Weapon,
						c = r.Class,
						g = r.Stage,
						pd = r.PluginDamage,
						ph = r.PluginHits,
						wd = r.WeaponDamage,
						wh = r.WeaponHits,
						t = Math.Round(r.Seconds, 1)
					}).ToList()
				};
				json = JsonSerializer.Serialize(payload);
			}
			catch
			{
				return;
			}

			// Runs on a worker thread so the game never waits for the network. Every failure is swallowed.
			Task.Run(async () =>
			{
				try
				{
					using (var content = new StringContent(json, Encoding.UTF8, "application/json"))
					using (HttpResponseMessage response = await http.PostAsync(Endpoint, content).ConfigureAwait(false))
					{
						if (!response.IsSuccessStatusCode)
							StatsCollector.Restore(rows);
					}
				}
				catch
				{
					StatsCollector.Restore(rows);
				}
			});
		}
	}

	// Whether the player has been asked, and the yes or no they gave. The yes/no itself is the "Share anonymous stats"
	// switch in the mod's config; this only remembers that the question was answered, so it is asked once.
	public static class StatsConsent
	{
		private static bool? answered;

		private static string FilePath => Path.Combine(Main.SavePath, "AugmentsStatsChoice.txt");

		public static bool Answered
		{
			get
			{
				if (answered == null)
					answered = File.Exists(FilePath);
				return answered.Value;
			}
		}

		public static bool Sharing => AugmentConfig.Instance?.ShareAnonymousStats ?? false;

		// The first-launch prompt shows only when there is a server to send to and the player has not chosen yet.
		public static bool NeedsPrompt => !Main.dedServ && StatsUploader.Configured && !Answered && !Sharing;

		public static void Answer(bool share)
		{
			AugmentConfig config = AugmentConfig.Instance;
			if (config != null)
			{
				config.ShareAnonymousStats = share;
				try
				{
					config.SaveChanges(silent: true, broadcast: false); // writes the config file like pressing Save in the config screen
				}
				catch (Exception e)
				{
					ModContent.GetInstance<Augments>().Logger.Warn("The stats choice could not be saved to the config: " + e.Message);
				}
			}

			try
			{
				Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
				File.WriteAllText(FilePath, share ? "yes" : "no");
			}
			catch (IOException)
			{
			}
			answered = true;

			if (!share)
				StatsCollector.Clear();
		}
	}

	// Counts combat seconds and sends a batch every few minutes; sends the rest when you leave a world.
	public class StatsPlayer : ModPlayer
	{
		public override void PostUpdate()
		{
			if (Player.whoAmI == Main.myPlayer)
				StatsCollector.Tick(Player);
		}
	}

	public class StatsSystem : ModSystem
	{
		public override void OnWorldUnload()
		{
			StatsCollector.Flush();
		}

		public override void Unload()
		{
			StatsCollector.Clear();
		}
	}
}
