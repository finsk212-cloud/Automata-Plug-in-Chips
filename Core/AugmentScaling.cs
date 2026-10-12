using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Augments.Core;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace Augments
{
	public enum ScalingStage
	{
		Start,
		Skeletron,
		WallOfFlesh,
		Mechs,
		Plantera,
		Golem,
		MoonLord,
		Providence,
		DevourerOfGods,
		Yharon,
		ExoMechsOrSupremeCalamitas
	}

	// Chips with a fixed damage number multiply it by the stage the world has reached.
	// The highest stage reached applies (a stage is never lost), and the multipliers
	// come from the server config so a host can retune them without rebuilding.
	public static class AugmentScaling
	{
		public static bool IsCalamityStage(ScalingStage stage) => stage >= ScalingStage.Providence;

		public static ScalingStage CurrentStage
		{
			get
			{
				if (AugmentScalingSystem.HasCalamity)
				{
					if (AugmentScalingSystem.IsDown(CalamityBoss.ExoMechsOrSupremeCalamitas))
						return ScalingStage.ExoMechsOrSupremeCalamitas;
					if (AugmentScalingSystem.IsDown(CalamityBoss.Yharon))
						return ScalingStage.Yharon;
					if (AugmentScalingSystem.IsDown(CalamityBoss.DevourerOfGods))
						return ScalingStage.DevourerOfGods;
					if (AugmentScalingSystem.IsDown(CalamityBoss.Providence))
						return ScalingStage.Providence;
				}

				if (NPC.downedMoonlord)
					return ScalingStage.MoonLord;
				if (NPC.downedGolemBoss)
					return ScalingStage.Golem;
				if (NPC.downedPlantBoss)
					return ScalingStage.Plantera;
				if (NPC.downedMechBoss1 && NPC.downedMechBoss2 && NPC.downedMechBoss3)
					return ScalingStage.Mechs;
				if (Main.hardMode)
					return ScalingStage.WallOfFlesh;
				if (NPC.downedBoss3)
					return ScalingStage.Skeletron;
				return ScalingStage.Start;
			}
		}

		public static float GetMultiplier(ScalingStage stage)
		{
			AugmentServerConfig config = AugmentServerConfig.Instance;
			if (config == null)
				return DefaultMultiplier(stage);

			return stage switch
			{
				ScalingStage.Start => config.ScaleStart,
				ScalingStage.Skeletron => config.ScaleSkeletron,
				ScalingStage.WallOfFlesh => config.ScaleWallOfFlesh,
				ScalingStage.Mechs => config.ScaleMechs,
				ScalingStage.Plantera => config.ScalePlantera,
				ScalingStage.Golem => config.ScaleGolem,
				ScalingStage.MoonLord => config.ScaleMoonLord,
				ScalingStage.Providence => config.ScaleProvidence,
				ScalingStage.DevourerOfGods => config.ScaleDevourerOfGods,
				ScalingStage.Yharon => config.ScaleYharon,
				ScalingStage.ExoMechsOrSupremeCalamitas => config.ScaleExoMechsOrSupremeCalamitas,
				_ => 1f
			};
		}

		public static float Multiplier => GetMultiplier(CurrentStage);

		// Applies the current multiplier to a chip's base damage. Never returns less than 1
		// for a positive base, matching how ScaleHitEffect treats tiny numbers.
		public static int Scale(int baseDamage)
		{
			if (baseDamage <= 0)
				return 0;

			return Math.Max(1, (int)Math.Round(baseDamage * Multiplier));
		}

		public static string StageName(ScalingStage stage)
		{
			return stage switch
			{
				ScalingStage.Start => "Start",
				ScalingStage.Skeletron => "Skeletron",
				ScalingStage.WallOfFlesh => "Wall of Flesh",
				ScalingStage.Mechs => "All Mechanical Bosses",
				ScalingStage.Plantera => "Plantera",
				ScalingStage.Golem => "Golem",
				ScalingStage.MoonLord => "Moon Lord",
				ScalingStage.Providence => "Providence",
				ScalingStage.DevourerOfGods => "Devourer of Gods",
				ScalingStage.Yharon => "Yharon",
				ScalingStage.ExoMechsOrSupremeCalamitas => "Exo Mechs / Supreme Calamitas",
				_ => stage.ToString()
			};
		}

		private static float DefaultMultiplier(ScalingStage stage)
		{
			return stage switch
			{
				ScalingStage.Start => 1f,
				ScalingStage.Skeletron => 1.25f,
				ScalingStage.WallOfFlesh => 2f,
				ScalingStage.Mechs => 3f,
				ScalingStage.Plantera => 3.5f,
				ScalingStage.Golem => 4f,
				ScalingStage.MoonLord => 5f,
				ScalingStage.Providence => 6f,
				ScalingStage.DevourerOfGods => 7.5f,
				ScalingStage.Yharon => 9f,
				ScalingStage.ExoMechsOrSupremeCalamitas => 10f,
				_ => 1f
			};
		}

		// Builds the "Base 15 x 3.5 (Plantera) = 53" lines shown while holding Ctrl in the
		// plugin list. Each entry is a (label, base damage) pair supplied by the chip.
		public static List<string> BuildBreakdownLines((string label, int baseValue)[] entries)
		{
			var lines = new List<string>();
			if (entries == null || entries.Length == 0)
				return lines;

			ScalingStage stage = CurrentStage;
			float multiplier = GetMultiplier(stage);

			lines.Add($"Damage scaling: x{FormatMultiplier(multiplier)} ({StageName(stage)})");
			foreach (var (label, baseValue) in entries)
			{
				int scaled = baseValue <= 0 ? 0 : Math.Max(1, (int)Math.Round(baseValue * multiplier));
				lines.Add($"  {label}: {baseValue} x {FormatMultiplier(multiplier)} = {scaled}");
			}

			return lines;
		}

		// Fixed-damage bonuses that belong to a protocol (chip family) rather than one chip.
		public static (string label, int baseValue)[] GetProtocolEntries(string familyId)
		{
			if (familyId == null)
				return null;

			if (familyId == AugmentFamilyRegistry.FortuneId)
				return new[] { ("Fortune Protocol: House Edge", 50) };
			if (familyId == AugmentFamilyRegistry.VoltId)
				return new[] { ("Volt Protocol: Superconductor", 25) };
			if (familyId == AugmentFamilyRegistry.ArcaneSurgeId)
				return new[] { ("Arcane Surge: Nova", 65) };
			if (familyId == AugmentFamilyRegistry.MarksmanId)
				return new[] { ("Marksman Protocol: Kinetic Shrapnel", 35) };

			if (familyId == AugmentFamilyRegistry.CryoId)
				return new[] { ("Cryo Protocol: Ice Shard (each)", 40) };

			return null;
		}

		public static string FormatMultiplier(float multiplier)
		{
			return multiplier.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
		}
	}

	public enum CalamityBoss
	{
		Providence = 0,
		DevourerOfGods = 1,
		Yharon = 2,
		ExoMechsOrSupremeCalamitas = 3
	}

	// Remembers which late Calamity bosses have fallen in this world. It is a safety net
	// next to Calamity's own downed-boss flags (read through reflection when available),
	// so the stage still advances if those flags cannot be read.
	public class AugmentScalingSystem : ModSystem
	{
		private static int trackedFlags;

		public static bool HasCalamity => ModLoader.HasMod("CalamityMod");

		private static readonly Dictionary<CalamityBoss, string> CalamityFlagNames = new()
		{
			{ CalamityBoss.Providence, "downedProvidence" },
			{ CalamityBoss.DevourerOfGods, "downedDoG" },
			{ CalamityBoss.Yharon, "downedYharon" },
			{ CalamityBoss.ExoMechsOrSupremeCalamitas, "downedExoMechs" }
		};

		private static readonly Dictionary<CalamityBoss, FieldInfo> reflectedFlags = new();
		private static FieldInfo supremeCalamitasFlag;
		private static bool reflectionResolved;

		public static bool IsDown(CalamityBoss boss)
		{
			if ((trackedFlags & (1 << (int)boss)) != 0)
				return true;

			ResolveReflection();

			try
			{
				if (reflectedFlags.TryGetValue(boss, out FieldInfo field) && field.GetValue(null) is bool down && down)
					return true;

				if (boss == CalamityBoss.ExoMechsOrSupremeCalamitas && supremeCalamitasFlag != null
					&& supremeCalamitasFlag.GetValue(null) is bool scalDown && scalDown)
					return true;
			}
			catch (Exception)
			{
			}

			return false;
		}

		public static void SetDown(CalamityBoss boss, bool value)
		{
			int bit = 1 << (int)boss;
			bool had = (trackedFlags & bit) != 0;
			if (value)
				trackedFlags |= bit;
			else
				trackedFlags &= ~bit;

			if (had != value && Main.netMode == NetmodeID.Server)
				NetMessage.SendData(MessageID.WorldData);
		}

		public static void ClearAll()
		{
			trackedFlags = 0;
			if (Main.netMode == NetmodeID.Server)
				NetMessage.SendData(MessageID.WorldData);
		}

		// Calamity keeps its downed flags as static bools; the class holding them has moved
		// between versions, so try the known names and silently give up if none resolve.
		private static void ResolveReflection()
		{
			if (reflectionResolved)
				return;

			reflectionResolved = true;

			try
			{
				if (!ModLoader.TryGetMod("CalamityMod", out Mod calamity) || calamity.Code == null)
					return;

				Type holder = calamity.Code.GetType("CalamityMod.DownedBossSystem");
				if (holder == null)
					return;

				const BindingFlags flags = BindingFlags.Public | BindingFlags.Static;
				foreach (var pair in CalamityFlagNames)
				{
					FieldInfo field = holder.GetField(pair.Value, flags);
					if (field != null && field.FieldType == typeof(bool))
						reflectedFlags[pair.Key] = field;
				}

				FieldInfo scal = holder.GetField("downedCalamitas", flags);
				if (scal != null && scal.FieldType == typeof(bool))
					supremeCalamitasFlag = scal;
			}
			catch (Exception)
			{
			}
		}

		public override void OnWorldLoad()
		{
			trackedFlags = 0;
		}

		public override void OnWorldUnload()
		{
			trackedFlags = 0;
		}

		public override void SaveWorldData(TagCompound tag)
		{
			tag["calamityStages"] = trackedFlags;
		}

		public override void LoadWorldData(TagCompound tag)
		{
			trackedFlags = tag.ContainsKey("calamityStages") ? tag.GetInt("calamityStages") : 0;
		}

		public override void NetSend(BinaryWriter writer)
		{
			writer.Write((byte)trackedFlags);
		}

		public override void NetReceive(BinaryReader reader)
		{
			trackedFlags = reader.ReadByte();
		}
	}

	// Marks the late Calamity stages when the matching boss dies. Server / singleplayer only.
	public class AugmentScalingProgress : GlobalNPC
	{
		private static readonly string[] ExoMechNames = { "AresBody", "ThanatosHead", "Apollo", "Artemis" };

		public override void OnKill(NPC npc)
		{
			if (Main.netMode == NetmodeID.MultiplayerClient)
				return;
			if (npc.ModNPC?.Mod?.Name != "CalamityMod")
				return;

			switch (npc.ModNPC.Name)
			{
				case "Providence":
					AugmentScalingSystem.SetDown(CalamityBoss.Providence, true);
					break;
				case "DevourerofGodsHead":
				case "DevourerofGodsHeadS":
					AugmentScalingSystem.SetDown(CalamityBoss.DevourerOfGods, true);
					break;
				case "Yharon":
					AugmentScalingSystem.SetDown(CalamityBoss.Yharon, true);
					break;
				case "SupremeCalamitas":
					AugmentScalingSystem.SetDown(CalamityBoss.ExoMechsOrSupremeCalamitas, true);
					break;
				case "AresBody":
				case "ThanatosHead":
				case "Apollo":
				case "Artemis":
					if (!AnotherExoMechAlive(npc))
						AugmentScalingSystem.SetDown(CalamityBoss.ExoMechsOrSupremeCalamitas, true);
					break;
			}
		}

		// The Exo Mechs only count as defeated once the last one falls.
		private static bool AnotherExoMechAlive(NPC dying)
		{
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC other = Main.npc[i];
				if (other == null || !other.active || other.whoAmI == dying.whoAmI)
					continue;
				if (other.ModNPC?.Mod?.Name != "CalamityMod")
					continue;

				if (Array.IndexOf(ExoMechNames, other.ModNPC.Name) >= 0)
					return true;
			}

			return false;
		}
	}
}
