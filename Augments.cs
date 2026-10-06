using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Augments
{
	internal enum AugmentPacketType : byte
	{
		SoulLinkRequest,
		RevitalizingWaveVisual,
		RevitalizingWaveRequest,
		CleanseRequest,
		CleanseClearDebuffs,
		MediGunHealRequest,
		MediGunOverclockActivate,
		BossDamageParticipation,  // client → server: "I damaged boss type X this fight"
		SupportHealVisual,
		LifelineTrigger,
		SoulMartyrTrigger,
		SoulMartyrDamage,
		UndyingBondRequest,
		UndyingBondRedirect,
		OpenRewardChoices,
		RerollRequest,
		LuckyFindDropRequest,
		ChooseReward,
		SyncAugmentState,
		RequestAugmentSync,
		DebugRequestReward,
		DebugCommandRequest,
		VendorSellRequest,
		VendorBuyBackRequest,
		RequestVendorSpawn,
		SyncOwnedAugments, // client -> server -> all: list of owned augment IDs
		ApplyNPCEffect,    // client -> server: custom GlobalNPC effect (Bleed, Slow, Cracked)
		SyncPlayerLifeMax  // client -> server -> all: player effective max life sync
	}

	internal enum DebugAugmentCommandType : byte
	{
		Add,
		Remove,
		AddAll,
		Clear,
		Sell,
		BuyBack
	}

	public class Augments : Mod
	{
		public static ModKeybind OpenAugmentListKeybind;
		public static ModKeybind DebugTriggerPopupKeybind;
		public static ModKeybind DebugSpawnVendorKeybind;
		public static ModKeybind DebugToggleShopKeybind;
		public static ModKeybind CleanseKeybind;
		public static ModKeybind UndoReforgeKeybind;
		public static ModKeybind ResetCooldownsKeybind;
		public static ModKeybind ToggleCombatAnalyticsKeybind;
		public static ModKeybind TriggerTipKeybind;

		public override void Load()
		{
			AugmentDatabase.Load();
			OpenAugmentListKeybind = KeybindLoader.RegisterKeybind(this, "OpenAugmentList", "P");
			DebugTriggerPopupKeybind = KeybindLoader.RegisterKeybind(this, "DebugTriggerPopup", "OemCloseBrackets");
			DebugSpawnVendorKeybind = KeybindLoader.RegisterKeybind(this, "DebugSpawnVendor", "OemSemicolon");
			DebugToggleShopKeybind = KeybindLoader.RegisterKeybind(this, "DebugToggleShop", "OemQuotes");
			CleanseKeybind = KeybindLoader.RegisterKeybind(this, "Cleanse", "None");
			UndoReforgeKeybind = KeybindLoader.RegisterKeybind(this, "UndoReforge", "None");
			ResetCooldownsKeybind = KeybindLoader.RegisterKeybind(this, "ResetCooldowns", "K");
			ToggleCombatAnalyticsKeybind = KeybindLoader.RegisterKeybind(this, "ToggleCombatAnalytics", "L");
			TriggerTipKeybind = KeybindLoader.RegisterKeybind(this, "TriggerTacticalTip", "None");

			// Registered manually, in this exact order, instead of relying on
			// autoload - AugmentFesteringWoundsNPC's UpdateLifeRegen must run
			// AFTER AugmentBleedNPC's so it amplifies a bleed contribution
			// that already landed this same tick. See the comments on both
			// classes for why autoload order can't be trusted for this.
			AddContent(new AugmentBleedNPC());
			AddContent(new AugmentFesteringWoundsNPC());
		}

		public override void Unload()
		{
			OpenAugmentListKeybind = null;
			DebugTriggerPopupKeybind = null;
			DebugSpawnVendorKeybind = null;
			DebugToggleShopKeybind = null;
			CleanseKeybind = null;
			UndoReforgeKeybind = null;
			ResetCooldownsKeybind = null;
			ToggleCombatAnalyticsKeybind = null;
			TriggerTipKeybind = null;
		}

		public override void HandlePacket(BinaryReader reader, int whoAmI)
		{
			var type = (AugmentPacketType)reader.ReadByte();
			if (AugmentNet.HandlePacket(type, reader, whoAmI))
				return;

			switch (type)
			{
				case AugmentPacketType.SoulLinkRequest:
					SupportEffects.HandleSoulLinkRequest(whoAmI, reader.ReadInt32());
					break;

				case AugmentPacketType.RevitalizingWaveVisual:
					SupportEffects.HandleRevitalizingWaveVisual(reader.ReadByte());
					break;

				case AugmentPacketType.RevitalizingWaveRequest:
					SupportEffects.HandleRevitalizingWaveRequest(whoAmI);
					break;

				case AugmentPacketType.CleanseRequest:
					SupportEffects.HandleCleanseRequest(whoAmI);
					break;

				case AugmentPacketType.CleanseClearDebuffs:
					SupportEffects.HandleCleanseClearDebuffs();
					break;

				case AugmentPacketType.MediGunHealRequest:
					SupportEffects.HandleMediGunHealRequest(whoAmI, reader.ReadByte(), reader.ReadInt32());
					break;

				case AugmentPacketType.MediGunOverclockActivate:
					SupportEffects.HandleMediGunOverclockActivate(whoAmI, reader.ReadByte(), reader.ReadByte());
					break;

				case AugmentPacketType.SupportHealVisual:
					SupportEffects.HandleHealVisual(reader.ReadByte(), reader.ReadInt32());
					break;

				case AugmentPacketType.LifelineTrigger:
					SupportEffects.HandleLifelineRequest(whoAmI);
					break;

				case AugmentPacketType.SoulMartyrTrigger:
					SupportEffects.HandleSoulMartyrTrigger(whoAmI, reader.ReadByte(), reader.ReadInt32());
					break;

				case AugmentPacketType.SoulMartyrDamage:
					SupportEffects.HandleSoulMartyrDamage(reader.ReadInt32());
					break;

				case AugmentPacketType.UndyingBondRequest:
					SupportEffects.HandleUndyingBondRequest(whoAmI);
					break;

				case AugmentPacketType.UndyingBondRedirect:
					SupportEffects.HandleUndyingBondRedirect(reader.ReadInt32(), reader.ReadInt32());
					break;

				case AugmentPacketType.BossDamageParticipation:
				{
					reader.ReadByte();
					int bossNpcType = reader.ReadInt32();
					if (Main.netMode == NetmodeID.Server && whoAmI >= 0 && whoAmI < Main.maxPlayers)
					{
						Player p = Main.player[whoAmI];
						if (p.active)
							p.GetModPlayer<AugmentPlayer>().DamagedBossesThisFight.Add(bossNpcType);
					}
					break;
				}

				case AugmentPacketType.SyncPlayerLifeMax:
				{
					byte playerIndex = reader.ReadByte();
					int maxLife2 = reader.ReadInt32();

					if (playerIndex < Main.maxPlayers)
					{
						Player target = Main.player[playerIndex];
						if (target.active)
							target.statLifeMax2 = maxLife2;
					}

					if (Main.netMode == NetmodeID.Server)
					{
						ModPacket relay = ModContent.GetInstance<Augments>().GetPacket();
						relay.Write((byte)AugmentPacketType.SyncPlayerLifeMax);
						relay.Write(playerIndex);
						relay.Write(maxLife2);
						relay.Send(-1, whoAmI);
					}
					break;
				}

				case AugmentPacketType.SyncOwnedAugments:
				{
					byte playerIndex = reader.ReadByte();
					int count = reader.ReadInt32();
					var ids = new System.Collections.Generic.List<string>(count);
					for (int i = 0; i < count; i++)
						ids.Add(reader.ReadString());

					if (playerIndex >= 0 && playerIndex < Main.maxPlayers)
					{
						Player target = Main.player[playerIndex];
						if (target.active)
							target.GetModPlayer<AugmentPlayer>().ApplySyncedOwnedIds(ids);
					}

					// Server relays to all other clients so they can see this player's augments.
					if (Main.netMode == NetmodeID.Server)
					{
						ModPacket relay = ModContent.GetInstance<Augments>().GetPacket();
						relay.Write((byte)AugmentPacketType.SyncOwnedAugments);
						relay.Write(playerIndex);
						relay.Write(count);
						foreach (string id in ids)
							relay.Write(id);
						relay.Send(-1, whoAmI); // all except original sender
					}
					break;
				}
			}
		}
	}
}
