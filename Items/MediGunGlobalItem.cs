using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

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

		public override void ModifyTooltips(Item item, List<TooltipLine> tooltips)
		{
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

				tooltips.Add(new TooltipLine(Mod, "SocketPrompt", "Right-Click with empty hand to detach into hand")
				{
					OverrideColor = new Color(148, 163, 184)
				});
			}
			else
			{
				tooltips.Add(new TooltipLine(Mod, "EmptySocket", "[c/94A3B8:Empty Accessory Socket]")
				{
					OverrideColor = new Color(148, 163, 184)
				});
				tooltips.Add(new TooltipLine(Mod, "EmptySocketHint", "Pick up an accessory and right-click to socket")
				{
					OverrideColor = new Color(140, 230, 160)
				});
			}
		}
	}
}
