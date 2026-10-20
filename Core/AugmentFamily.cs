using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Augments.Core
{
	public class FamilyThresholdBonus
	{
		public string Title { get; set; }
		public string[] Descriptions { get; set; }

		public FamilyThresholdBonus(string title, string[] descriptions)
		{
			Title = title;
			Descriptions = descriptions;
		}
	}

	public enum FamilyType
	{
		Support,
		Offense,
		Defense,
		Utility
	}

	public class AugmentFamily
	{
		public string Id { get; set; }
		public string DisplayName { get; set; }
		public string Description { get; set; }
		public Color ThemeColor { get; set; }
		public FamilyType Type { get; set; } = FamilyType.Support;
		public List<string> MemberIds { get; set; } = new();
		public Dictionary<int, FamilyThresholdBonus> ThresholdBonuses { get; set; } = new();

		public int MaxMembers => MemberIds.Count;
	}

	public static class AugmentFamilyRegistry
	{
		public const string FieldMedicId = "field_medic";
		public const string BloodhunterId = "bloodhunter";
		public const string KineticId = "kinetic";
		public const string FortuneId = "fortune";
		public const string CryoId = "cryo";
		public const string VoltId = "volt";
		public const string HivemindId = "hivemind";
		public const string MarksmanId = "marksman";
		public const string ArcaneSurgeId = "arcane_surge";
		public const string BastionId = "bastion";
		public const string GunslingerId = "gunslinger";
		public const string LasherId = "lasher";

		public static readonly Dictionary<string, AugmentFamily> Families = new();

		static AugmentFamilyRegistry()
		{
			// Field Medic: 2-Piece Common Duo
			Families[FieldMedicId] = new AugmentFamily
			{
				Id = FieldMedicId,
				DisplayName = "Field Medic",
				Description = "Combat first-aid and rapid trauma recovery.",
				ThemeColor = new Color(70, 240, 160),
				Type = FamilyType.Support,
				MemberIds = new List<string>
				{
					"second_wind",
					"combat_medic"
				},
				ThresholdBonuses = new Dictionary<int, FamilyThresholdBonus>
				{
					[2] = new FamilyThresholdBonus(
						"Triage Efficiency",
						new string[]
						{
							$"Potion sickness duration is reduced by {AugmentText.Duration("-20%")}",
							$"Heart pickups restore {AugmentText.Healing("+20% more health (+4 HP)")}"
						}
					)
				}
			};

			// Bloodhunter: 2-Piece Offense Duo
			Families[BloodhunterId] = new AugmentFamily
			{
				Id = BloodhunterId,
				DisplayName = "Bloodhunter",
				Description = "Tracking, wounding, and finishing off afflicted prey.",
				ThemeColor = new Color(235, 55, 65),
				Type = FamilyType.Offense,
				MemberIds = new List<string>
				{
					"bloodletter",
					"festering_wounds"
				},
				ThresholdBonuses = new Dictionary<int, FamilyThresholdBonus>
				{
					[2] = new FamilyThresholdBonus(
						"Exsanguination",
						new string[]
						{
							$"{AugmentText.Bleed("Bleed")} damage over time is increased by {AugmentText.BonusDamage("+50%")}",
							$"Attacks against {AugmentText.Bleed("bleeding")} targets gain {AugmentText.Crit("+8% Critical Strike Chance")}"
						}
					)
				}
			};

			// Kinetic: 2-Piece Offense Duo
			Families[KineticId] = new AugmentFamily
			{
				Id = KineticId,
				DisplayName = "Kinetic",
				Description = "High-speed momentum conversion and kinetic shockwaves.",
				ThemeColor = new Color(249, 115, 22),
				Type = FamilyType.Offense,
				MemberIds = new List<string>
				{
					"momentum_swing",
					"momentum_crash"
				},
				ThresholdBonuses = new Dictionary<int, FamilyThresholdBonus>
				{
					[2] = new FamilyThresholdBonus(
						"Kinetic Momentum",
						new string[]
						{
							$"Dashing or sprinting at full speed releases a kinetic shockwave on impact dealing {AugmentText.SpecialDamage("75% base weapon damage")}",
							$"Melee {AugmentText.AttackSpeed("attack speed")} scales up by {AugmentText.AttackSpeed("+1%")} per 2 mph of current movement speed (up to {AugmentText.AttackSpeed("+15%")})"
						}
					)
				}
			};

			// Fortune: 5-Piece Utility Protocol
			Families[FortuneId] = new AugmentFamily
			{
				Id = FortuneId,
				DisplayName = "Fortune",
				Description = "Probability manipulation, critical procs, and lucrative windfalls.",
				ThemeColor = new Color(255, 200, 59),
				Type = FamilyType.Utility,
				MemberIds = new List<string>
				{
					"lucky_strike",
					"fortunes_favor",
					"lucky_find",
					"scavengers_luck",
					"wild_card"
				},
				ThresholdBonuses = new Dictionary<int, FamilyThresholdBonus>
				{
					[2] = new FamilyThresholdBonus(
						"Lucky Streak",
						new string[]
						{
							$"Increases character's World Luck by {AugmentText.Crit("+0.15")}",
							$"Fortune scaling and lucky procs gain {AugmentText.Crit("+20% effectiveness")}"
						}
					),
					[4] = new FamilyThresholdBonus(
						"Jackpot Calibration",
						new string[]
						{
							$"Increases character's World Luck by an additional {AugmentText.Crit("+0.20")} (+0.35 total)",
							$"Fortune scaling and lucky procs gain an additional {AugmentText.Crit("+25% effectiveness")} (+45% total)"
						}
					),
					[5] = new FamilyThresholdBonus(
						"House Edge",
						new string[]
						{
							$"Increases character's World Luck by an additional {AugmentText.Crit("+0.15")} (+0.50 total)",
							$"{AugmentText.Crit("Critical strikes")} release a radial shower of gold coins dealing {AugmentText.SpecialDamage("50 damage")} to nearby enemies"
						}
					)
				}
			};

			// Cryo: 2-Piece Offense/Control Duo
			Families[CryoId] = new AugmentFamily
			{
				Id = CryoId,
				DisplayName = "Cryo",
				Description = "Deep-freeze crowd control and glacial shattering.",
				ThemeColor = new Color(56, 189, 248), // Glacial Ice Cyan (#38BDF8)
				Type = FamilyType.Offense,
				MemberIds = new List<string>
				{
					"frost_touch",
					"frostbound"
				},
				ThresholdBonuses = new Dictionary<int, FamilyThresholdBonus>
				{
					[2] = new FamilyThresholdBonus(
						"Absolute Zero",
						new string[]
						{
							$"Enemies afflicted with {AugmentText.Frostburn("Frostburn")} are {AugmentText.Immobilize("slowed by 20%")}",
							$"Slaying a chilled or {AugmentText.Frostburn("Frostburned")} enemy shatters them into 3 homing ice shards ({AugmentText.SpecialDamage("40 damage")})"
						}
					)
				}
			};

			// Volt: 2-Piece Elemental/Shock Duo
			Families[VoltId] = new AugmentFamily
			{
				Id = VoltId,
				DisplayName = "Volt",
				Description = "High-voltage conductivity, electrification, and static arcs.",
				ThemeColor = new Color(139, 92, 246), // High-Voltage Electric Violet (#8B5CF6)
				Type = FamilyType.Offense,
				MemberIds = new List<string>
				{
					"chain_lightning",
					"stormcaller"
				},
				ThresholdBonuses = new Dictionary<int, FamilyThresholdBonus>
				{
					[2] = new FamilyThresholdBonus(
						"Superconductor",
						new string[]
						{
							$"Lightning and electrical procs inflict {AugmentText.SpecialDamage("Electrified")}",
							$"Attacks against {AugmentText.SpecialDamage("Electrified")} enemies gain {AugmentText.Crit("+8% Critical Strike Chance")}",
							$"Attacks against {AugmentText.SpecialDamage("Electrified")} enemies have a 25% chance to arc static electricity dealing {AugmentText.SpecialDamage("25 damage")}"
						}
					)
				}
			};

			// Hivemind: 2-Piece Summoner Duo
			Families[HivemindId] = new AugmentFamily
			{
				Id = HivemindId,
				DisplayName = "Hivemind",
				Description = "Nanite swarm coordination and focused minion firepower.",
				ThemeColor = new Color(16, 185, 129), // Toxic Neon Lime / Acid Cyber-Green (#10B981)
				Type = FamilyType.Offense,
				MemberIds = new List<string>
				{
					"queens_swarm",
					"necromancers_court"
				},
				ThresholdBonuses = new Dictionary<int, FamilyThresholdBonus>
				{
					[2] = new FamilyThresholdBonus(
						"Swarm Coordination",
						new string[]
						{
							$"Minion and sentry attacks inject Micro-Nanites into enemies ({AugmentText.Duration("4s duration")})",
							$"Target takes {AugmentText.BonusDamage("+3 flat bonus damage")} per hit from all sources for each active minion attacking it"
						}
					)
				}
			};

			// Marksman: 2-Piece Ranged Duo
			Families[MarksmanId] = new AugmentFamily
			{
				Id = MarksmanId,
				DisplayName = "Marksman",
				Description = "High-precision ballistics, target tracking, and armor-piercing kinetic detonations.",
				ThemeColor = new Color(244, 63, 94), // Laser Ruby / Scope Crimson (#F43F5E)
				Type = FamilyType.Offense,
				MemberIds = new List<string>
				{
					"sharpshooter",
					"deadeye"
				},
				ThresholdBonuses = new Dictionary<int, FamilyThresholdBonus>
				{
					[2] = new FamilyThresholdBonus(
						"Pinpoint Ballistics",
						new string[]
						{
							$"Ranged projectile velocity is increased by {AugmentText.AttackSpeed("+25%")}",
							$"Ranged hits from beyond 350 pixels mark the target with a Kinetic Mark for {AugmentText.Duration("4s")}",
							$"{AugmentText.Crit("Critical strikes")} against marked targets gain {AugmentText.Defense("+15 Armor Penetration")} and detonate 3 kinetic shrapnel flechettes ({AugmentText.SpecialDamage("35 damage")}, at most once per second)"
						}
					)
				}
			};

			// Arcane Surge: 2-Piece Magic Duo
			Families[ArcaneSurgeId] = new AugmentFamily
			{
				Id = ArcaneSurgeId,
				DisplayName = "Arcane Surge",
				Description = "High-output mana resonance, astral charging, and arcane nova discharges.",
				ThemeColor = new Color(129, 140, 248), // Cosmic Indigo / Astral Violet (#818CF8)
				Type = FamilyType.Offense,
				MemberIds = new List<string>
				{
					"overcharge",
					"spell_echo"
				},
				ThresholdBonuses = new Dictionary<int, FamilyThresholdBonus>
				{
					[2] = new FamilyThresholdBonus(
						"Astral Discharge",
						new string[]
						{
							$"Spending {AugmentText.Mana("mana")} builds Astral Charge (1 stack per {AugmentText.Mana("20 mana")} spent, max {AugmentText.Trigger("5 stacks")})",
							$"At {AugmentText.Trigger("5 stacks")} (100 mana spent), your next magic hit releases an Arcane Nova shockwave dealing {AugmentText.SpecialDamage("65 damage")} and refunds {AugmentText.Mana("30 mana")}"
						}
					)
				}
			};

			// Bastion: 2-Piece Defense Duo
			Families[BastionId] = new AugmentFamily
			{
				Id = BastionId,
				DisplayName = "Bastion",
				Description = "Heavy barrier projection, kinetic hardening, and concussive deflection.",
				ThemeColor = new Color(56, 189, 248), // Bastion Cyan / Heavy Cobalt (#38BDF8)
				Type = FamilyType.Defense,
				MemberIds = new List<string>
				{
					"bulwark",
					"adaptive_armor"
				},
				ThresholdBonuses = new Dictionary<int, FamilyThresholdBonus>
				{
					[2] = new FamilyThresholdBonus(
						"Kinetic Hardening & Concussive Deflection",
						new string[]
						{
							$"Bulwark absorbs damage before armor plating is compromised, preserving all Adaptive Armor {AugmentText.Defense("defense stacks")}",
							$"Blocking a hit with Bulwark detonates a Concussive Deflection shockwave (160px radius) dealing {AugmentText.SpecialDamage("50 flat kinetic damage")} + 5 per Adaptive Armor stack (up to 100) with violent knockback",
							$"While Adaptive Armor is at maximum stacks ({AugmentText.Defense("+10 defense")}), Bulwark's shield recharge time is accelerated by {AugmentText.Cooldown("25% (7.5s instead of 10s)")}"
						}
					)
				}
			};

			// Gunslinger: 2-Piece Ranged Duo
			Families[GunslingerId] = new AugmentFamily
			{
				Id = GunslingerId,
				DisplayName = "Gunslinger",
				Description = "Rapid-fire ballistic cadence, rotary spin-up, and sustained lead storms.",
				ThemeColor = new Color(234, 179, 8), // High-Caliber Brass / Rotary Gold (#EAB308)
				Type = FamilyType.Offense,
				MemberIds = new List<string>
				{
					"quickfire",
					"rapid_fire"
				},
				ThresholdBonuses = new Dictionary<int, FamilyThresholdBonus>
				{
					[2] = new FamilyThresholdBonus(
						"Rotary Acceleration & Lead Storm",
						new string[]
						{
							$"Continuously firing any ranged weapon ramps up {AugmentText.AttackSpeed("attack speed")} by {AugmentText.AttackSpeed("+2.5%")} per second (up to {AugmentText.AttackSpeed("+10% bonus attack speed")} after {AugmentText.Duration("4s")})",
							$"Ceasing fire for {AugmentText.Duration("1 second")} resets the rotary spin-up",
							$"While maintaining maximum spin-up (Lead Storm), ranged attacks gain {AugmentText.Crit("+8% Critical Strike Chance")}"
						}
					)
				}
			};

			// Lasher: 2-Piece Summoner Duo
			Families[LasherId] = new AugmentFamily
			{
				Id = LasherId,
				DisplayName = "Lasher",
				Description = "High-velocity whip coordination, concussive sonic cracks, and minion predatory focus.",
				ThemeColor = new Color(249, 115, 22), // Neural Amber / Whip Lash Orange (#F97316)
				Type = FamilyType.Offense,
				MemberIds = new List<string>
				{
					"whip_master",
					"whip_cracker"
				},
				ThresholdBonuses = new Dictionary<int, FamilyThresholdBonus>
				{
					[2] = new FamilyThresholdBonus(
						"Sonic Crack & Predatory Command",
						new string[]
						{
							$"Striking an enemy at maximum ({AugmentText.Trigger("5")}) Whip Cracker stacks detonates a Sonic Crack shockwave (130px radius) dealing {AugmentText.SpecialDamage("45 flat summon damage")} and refreshes the debuff duration back to {AugmentText.Duration("4s")}",
							$"Friendly minions attacking an enemy at maximum ({AugmentText.Trigger("5")}) Whip Cracker stacks gain {AugmentText.Crit("+12% Critical Strike Chance")} against that target"
						}
					)
				}
			};
		}

		public static AugmentFamily Get(string familyId)
		{
			if (familyId != null && Families.TryGetValue(familyId, out var family))
				return family;
			return null;
		}

		public static int GetOwnedCount(AugmentPlayer ap, string familyId)
		{
			var family = Get(familyId);
			if (family == null || ap == null)
				return 0;

			int count = 0;
			foreach (var memberId in family.MemberIds)
			{
				if (ap.HasAugment(memberId))
					count++;
			}
			return count;
		}
	}
}
