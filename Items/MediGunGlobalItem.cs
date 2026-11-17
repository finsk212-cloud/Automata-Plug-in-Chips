using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Microsoft.Xna.Framework.Input;

namespace Augments.Items
{
	public class MediGunGlobalItem : GlobalItem
	{
		public override bool InstancePerEntity => true;

		public int SocketedAccessoryType { get; set; } = 0;
		public int SocketedAccessoryPrefix { get; set; } = 0;

		public override bool AppliesToEntity(Item item, bool lateInstantiation)
		{
			return item.type == ModContent.ItemType<MediGunItem>()
				|| item.type == ModContent.ItemType<MediGunMK2Item>()
				|| item.type == ModContent.ItemType<MediGunMK3Item>()
				|| item.type == ModContent.ItemType<MediGunMK4Item>();
		}

		public static bool IsSupportedAccessory(int itemType)
		{
			return itemType == ItemID.BandofRegeneration
				|| itemType == ItemID.BandofStarpower
				|| itemType == ItemID.AnkletoftheWind
				|| itemType == ItemID.CobaltShield
				|| itemType == ItemID.Bezoar
				|| itemType == ItemID.SharkToothNecklace
				|| itemType == ItemID.PhilosophersStone
				|| itemType == ItemID.Aglet
				|| itemType == ItemID.HandWarmer
				|| itemType == ItemID.FeralClaws
				|| itemType == ItemID.Shackle;
		}

		public override void SaveData(Item item, TagCompound tag)
		{
			if (SocketedAccessoryType > 0)
			{
				tag["SocketedAccessoryType"] = SocketedAccessoryType;
				if (SocketedAccessoryPrefix > 0)
				{
					tag["SocketedAccessoryPrefix"] = SocketedAccessoryPrefix;
				}
			}
		}

		public override void LoadData(Item item, TagCompound tag)
		{
			SocketedAccessoryType = tag.GetInt("SocketedAccessoryType");
			SocketedAccessoryPrefix = tag.GetInt("SocketedAccessoryPrefix");
		}

		public override void NetSend(Item item, System.IO.BinaryWriter writer)
		{
			writer.Write(SocketedAccessoryType);
			writer.Write(SocketedAccessoryPrefix);
		}

		public override void NetReceive(Item item, System.IO.BinaryReader reader)
		{
			SocketedAccessoryType = reader.ReadInt32();
			SocketedAccessoryPrefix = reader.ReadInt32();
		}

		public override void OnCreated(Item item, ItemCreationContext context)
		{
			if (context is RecipeItemCreationContext recipeContext)
			{
				foreach (Item consumed in recipeContext.ConsumedItems)
				{
					if (consumed.TryGetGlobalItem<MediGunGlobalItem>(out var consumedMedi) && consumedMedi.SocketedAccessoryType > 0)
					{
						SocketedAccessoryType = consumedMedi.SocketedAccessoryType;
						SocketedAccessoryPrefix = consumedMedi.SocketedAccessoryPrefix;
						break;
					}
				}
			}
		}

		public override bool CanRightClick(Item item)
		{
			return SocketedAccessoryType > 0;
		}

		public override bool ConsumeItem(Item item, Player player)
		{
			return false;
		}

		public override void RightClick(Item item, Player player)
		{
			if (SocketedAccessoryType > 0)
			{
				// Detach existing accessory directly into hand
				int detachedType = SocketedAccessoryType;
				int detachedPrefix = SocketedAccessoryPrefix;
				SocketedAccessoryType = 0;
				SocketedAccessoryPrefix = 0;

				if (Main.mouseItem == null || Main.mouseItem.IsAir)
				{
					Main.mouseItem = new Item();
					Main.mouseItem.SetDefaults(detachedType);
					if (detachedPrefix > 0)
					{
						Main.mouseItem.Prefix(detachedPrefix);
					}
				}
				else
				{
					Item dropped = player.QuickSpawnItemDirect(player.GetSource_ItemUse(item), detachedType, 1);
					if (detachedPrefix > 0)
					{
						dropped.Prefix(detachedPrefix);
					}
				}
				SoundEngine.PlaySound(SoundID.Grab, player.Center);
				CombatText.NewText(player.getRect(), new Color(255, 200, 80), $"Detached: {Lang.GetItemNameValue(detachedType)}");
			}
		}

		private static bool ShiftHeld => Main.keyState.IsKeyDown(Keys.LeftShift) || Main.keyState.IsKeyDown(Keys.RightShift);

		private static int TierOf(Item item)
		{
			if (item.type == ModContent.ItemType<MediGunMK4Item>()) return 4;
			if (item.type == ModContent.ItemType<MediGunMK3Item>()) return 3;
			if (item.type == ModContent.ItemType<MediGunMK2Item>()) return 2;
			return 1;
		}

		// The extra lines shown while Shift is held (the short tooltip is in the localization file).
		private static string[] DetailLines(int tier)
		{
			int[] seconds = { 35, 30, 25, 20 };
			int[] ovHeal = { 30, 50, 75, 100 };
			int[] ovDamage = { 40, 100, 250, 500 };
			int[] range = { 650, 750, 850, 1000 };
			string ally = tier switch
			{
				1 => "[c/2DD4BF:+20% movement speed]",
				2 => "[c/2DD4BF:+20% movement speed] and [c/FB923C:+10% attack speed]",
				3 => "[c/2DD4BF:+25% movement speed], [c/FB923C:+15% attack speed] and [c/FFC83B:+8% critical strike chance]",
				_ => "[c/2DD4BF:+30% movement speed], [c/FB923C:+20% attack speed], [c/FFC83B:+12% critical strike chance] and [c/FF5C5C:+15% all damage]"
			};
			string enemy = tier switch
			{
				1 => "[c/FFA500:Corrosion] (-5 defense for 5 seconds)",
				2 => "[c/EAB308:Ichor] (-15 defense for 6 seconds)",
				3 => "[c/EAB308:Ichor] (-15 defense for 8 seconds)",
				_ => "[c/EAB308:Ichor] (-15 defense) and [c/D946EF:Betsy's Curse] (-40 defense) for 10 seconds"
			};
			int i = tier - 1;
			return new[]
			{
				$"Healing or damaging builds Overclock over [c/A5C8E8:{seconds[i]} seconds]",
				$"Right-Click at 100% while healing: restores [c/4ADE80:{ovHeal[i]} HP] to your ally and grants {ally} for [c/A5C8E8:6 seconds]",
				$"Left-Click at 100% while damaging: deals [c/FF5C5C:{ovDamage[i]} damage] and inflicts {enemy}",
				"A new target, or letting go of the beam, restarts the warm-up",
				$"The beam locks on within {range[i]} pixels and breaks if the line of sight is blocked or the target gets too far away",
				"Right-Click with an empty hand to detach an attached accessory"
			};
		}
		public override void ModifyTooltips(Item item, List<TooltipLine> tooltips)
		{
			// Holding Shift swaps the short tooltip for the full Overclock details.
			if (ShiftHeld)
			{
				int at = tooltips.FindIndex(t => t.Mod == "Terraria" && t.Name.StartsWith("Tooltip"));
				tooltips.RemoveAll(t => t.Mod == "Terraria" && t.Name.StartsWith("Tooltip"));
				if (at < 0)
					at = Math.Min(1, tooltips.Count);
				string[] lines = DetailLines(TierOf(item));
				for (int i = 0; i < lines.Length; i++)
					tooltips.Insert(at + i, new TooltipLine(Mod, "MediDetail" + i, lines[i]));
			}

			if (SocketedAccessoryType > 0)
			{
				tooltips.Add(new TooltipLine(Mod, "SocketHeader", $"[c/FFC83B:Attached Accessory: {Lang.GetItemNameValue(SocketedAccessoryType)}]")
				{
					OverrideColor = new Color(255, 200, 59)
				});

				if (SocketedAccessoryType == ItemID.BandofRegeneration)
				{
					tooltips.Add(new TooltipLine(Mod, "SocketEffect1", "• 50% of beam healing is returned to the medic")
					{
						OverrideColor = new Color(74, 222, 128)
					});
					tooltips.Add(new TooltipLine(Mod, "SocketEffect2", "• Tethered ally gains amplified Rapid Healing (+3 life regen)")
					{
						OverrideColor = new Color(74, 222, 128)
					});
				}
				else if (SocketedAccessoryType == ItemID.BandofStarpower)
				{
					tooltips.Add(new TooltipLine(Mod, "SocketEffect1", "• Overclock charges 25% faster while tethered")
					{
						OverrideColor = new Color(74, 222, 128)
					});
					tooltips.Add(new TooltipLine(Mod, "SocketEffect2", "• Tethered ally gains +40 maximum mana and rapid mana recovery")
					{
						OverrideColor = new Color(74, 222, 128)
					});
				}
				else if (SocketedAccessoryType == ItemID.AnkletoftheWind)
				{
					tooltips.Add(new TooltipLine(Mod, "SocketEffect1", "• Grants the medic +12% movement speed while tethered")
					{
						OverrideColor = new Color(74, 222, 128)
					});
					tooltips.Add(new TooltipLine(Mod, "SocketEffect2", "• Tethered ally gains +15% movement speed and acceleration")
					{
						OverrideColor = new Color(74, 222, 128)
					});
				}
				else if (SocketedAccessoryType == ItemID.CobaltShield)
				{
					tooltips.Add(new TooltipLine(Mod, "SocketEffect1", "• Grants the medic knockback immunity while tethered")
					{
						OverrideColor = new Color(74, 222, 128)
					});
					tooltips.Add(new TooltipLine(Mod, "SocketEffect2", "• Tethered ally gains knockback immunity and +4 defense")
					{
						OverrideColor = new Color(74, 222, 128)
					});
				}
				else if (SocketedAccessoryType == ItemID.Bezoar)
				{
					tooltips.Add(new TooltipLine(Mod, "SocketEffect1", "• Grants the medic immunity to Poison and +10% healing to allies under 50% HP")
					{
						OverrideColor = new Color(74, 222, 128)
					});
					tooltips.Add(new TooltipLine(Mod, "SocketEffect2", "• Tethered ally becomes immune to Poison and Venom, instantly purging active toxins")
					{
						OverrideColor = new Color(74, 222, 128)
					});
				}
				else if (SocketedAccessoryType == ItemID.SharkToothNecklace)
				{
					tooltips.Add(new TooltipLine(Mod, "SocketEffect1", "• Grants the medic +3 armor penetration and restorative life sparks on beam pulses")
					{
						OverrideColor = new Color(74, 222, 128)
					});
					tooltips.Add(new TooltipLine(Mod, "SocketEffect2", "• Tethered ally gains +8 armor penetration")
					{
						OverrideColor = new Color(74, 222, 128)
					});
				}
				else if (SocketedAccessoryType == ItemID.PhilosophersStone)
				{
					tooltips.Add(new TooltipLine(Mod, "SocketEffect1", "• When the tethered ally drinks a potion, the medic restores 25 HP")
					{
						OverrideColor = new Color(74, 222, 128)
					});
					tooltips.Add(new TooltipLine(Mod, "SocketEffect2", "• Tethered ally potion sickness is reduced by 20 seconds (down to 40 seconds)")
					{
						OverrideColor = new Color(74, 222, 128)
					});
				}
				else if (SocketedAccessoryType == ItemID.Aglet)
				{
					tooltips.Add(new TooltipLine(Mod, "SocketEffect1", "• Grants the medic +6% movement speed while tethered")
					{
						OverrideColor = new Color(74, 222, 128)
					});
					tooltips.Add(new TooltipLine(Mod, "SocketEffect2", "• Tethered ally gains +8% movement speed and acceleration")
					{
						OverrideColor = new Color(74, 222, 128)
					});
				}
				else if (SocketedAccessoryType == ItemID.HandWarmer)
				{
					tooltips.Add(new TooltipLine(Mod, "SocketEffect1", "• Grants the medic immunity to Chilled and Frozen")
					{
						OverrideColor = new Color(74, 222, 128)
					});
					tooltips.Add(new TooltipLine(Mod, "SocketEffect2", "• Tethered ally becomes immune to Chilled and Frozen, purging active frost")
					{
						OverrideColor = new Color(74, 222, 128)
					});
				}
				else if (SocketedAccessoryType == ItemID.FeralClaws)
				{
					tooltips.Add(new TooltipLine(Mod, "SocketEffect1", "• Overclock charges 15% faster while tethered")
					{
						OverrideColor = new Color(74, 222, 128)
					});
					tooltips.Add(new TooltipLine(Mod, "SocketEffect2", "• Tethered ally gains +15% melee and whip attack speed with auto-swing")
					{
						OverrideColor = new Color(74, 222, 128)
					});
				}
				else if (SocketedAccessoryType == ItemID.Shackle)
				{
					tooltips.Add(new TooltipLine(Mod, "SocketEffect1", "• Grants the medic +2 defense while tethered")
					{
						OverrideColor = new Color(74, 222, 128)
					});
					tooltips.Add(new TooltipLine(Mod, "SocketEffect2", "• Tethered ally gains +3 defense")
					{
						OverrideColor = new Color(74, 222, 128)
					});
				}

			}
			else
			{
				tooltips.Add(new TooltipLine(Mod, "EmptySocket", "[c/94A3B8:Empty socket:] [c/8CE6A0:right-click an accessory to attach it]"));
			}
		}
	}
}
