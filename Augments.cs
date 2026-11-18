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
		SyncPlayerLifeMax, // client -> server -> all: player effective max life sync
		LoadoutRequest,    // client -> server: stash/unstash/save/buy/apply loadout
		SyncBossKills,     // client -> server: saved boss reward kill counts (merged by max)
		DebugGivePlugin,  // client -> server: dev command, give a chip to a target player
		ClientFullState,   // client -> server: whole disk-loaded state (stash, loadouts, sold list...) on world join
		CalloutBonusDamage, // client -> server -> marker's client: extra damage a Callout mark made a teammate deal
		ChronobreakRequest, // client -> server: the owner pressed Chronobreak
		ChronobreakApply,   // server -> client: rewind yourself (payload: owner index)
		GetOverHereRequest, // client -> server: the owner pressed Get over here!
		GetOverHereApply,   // server -> pulled client: haul yourself to the owner (payload: owner index)
		GetOverHereEffect,  // server -> all clients: hook and chain visual (payload: owner index, pulled player index)
		BannerStationInit,     // client -> server: fill a freshly placed station with the item's tier and banners
		BannerStationDeposit,  // client -> server: put one banner into a station
		BannerStationWithdraw, // client -> server: take one banner out of a station
		BannerStationUpgrade,  // client -> server: upgrade a station with a kit
		BannerStationReply,    // server -> one client: a banner or kit to hand back (payload: kind, item type)
		// New values are appended at the end so existing packet ids never shift.
		KillCredit,          // server -> killer's client: run client-side kill hooks (and Cryo shards) for this NPC
		SyncPersistentState, // client -> server on join: sold/ever-owned/locked families/boss kill counts
		GachaRollRequest,    // client -> server: spend 3 Machine Cores and roll a chip
		GachaResult,         // server -> client: the chip rolled (empty id = nothing available, cores not spent)
		TakeMachineCores,    // server -> owner's client: remove N Machine Cores the server just charged
		ChamberPrimed,       // client -> server -> others: One in the Chamber is loaded / fired (payload: player index, primed)
		ScapegoatTransfer,   // client -> server: a teammate removed a debuff, hand it to the Scapegoat owner (payload: owner index, buff type, ticks)
		ScapegoatApply,      // server -> Scapegoat owner's client: take this debuff (payload: buff type, ticks)
		PluginEffect,        // client -> server -> others: play a plugin's sound/visual effect on the sender (payload: sender, effect id, int)
		PodPetSync,          // client -> server -> others: the sender's Pod pet is on/off and which skin it wears (payload: player, on, skin)
		FallenUnitRecover,   // client -> server: open this Fallen Unit's offer (payload: unit id)
		FallenUnitRecovered, // server -> all: a player recovered a Fallen Unit (payload: unit id, player name)
		FallenUnitOffer,     // server -> one client: the plugins in this unit (payload: unit id, count, ids)
		FallenUnitPick       // client -> server: take this plugin from the unit (payload: unit id, plugin id)
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
		public static ModKeybind ChronobreakKeybind;
		public static ModKeybind GetOverHereKeybind;
		public static ModKeybind BladeDanceKeybind;
		public static ModKeybind DangerCloseKeybind;
		public static ModKeybind DiscountRodKeybind;
		public static ModKeybind LongDistanceKeybind;
		public static ModKeybind AdminKeybind;
		public static ModKeybind[] LoadoutKeybinds = new ModKeybind[3];
		public static ModKeybind UndoReforgeKeybind;
		public static ModKeybind ResetCooldownsKeybind;
		public static ModKeybind ToggleCombatAnalyticsKeybind;
		public static ModKeybind TriggerTipKeybind;
		public static ModKeybind TogglePodKeybind;
		public static ModKeybind SniffOreKeybind;

		public override void Load()
		{
			AugmentDatabase.Load();

			// Inline marker icon used by the Callout description.
			if (!Main.dedServ)
			{
				try { Terraria.UI.Chat.ChatManager.Register<CalloutMarkTagHandler>(CalloutMarkTagHandler.TagName); }
				catch (System.Exception e) { Logger.Warn("Could not register the Callout marker tag.", e); }
			}
			OpenAugmentListKeybind = KeybindLoader.RegisterKeybind(this, "OpenAugmentList", "P");
			DebugTriggerPopupKeybind = KeybindLoader.RegisterKeybind(this, "DebugTriggerPopup", "OemCloseBrackets");
			DebugSpawnVendorKeybind = KeybindLoader.RegisterKeybind(this, "DebugSpawnVendor", "OemSemicolon");
			DebugToggleShopKeybind = KeybindLoader.RegisterKeybind(this, "DebugToggleShop", "OemQuotes");
			CleanseKeybind = KeybindLoader.RegisterKeybind(this, "Cleanse", "None");
			ChronobreakKeybind = KeybindLoader.RegisterKeybind(this, "Chronobreak", "None");
			GetOverHereKeybind = KeybindLoader.RegisterKeybind(this, "GetOverHere", "None");
			BladeDanceKeybind = KeybindLoader.RegisterKeybind(this, "BladeDance", "None");
			DangerCloseKeybind = KeybindLoader.RegisterKeybind(this, "DangerClose", "None");
			DiscountRodKeybind = KeybindLoader.RegisterKeybind(this, "DiscountRod", "None");
			LongDistanceKeybind = KeybindLoader.RegisterKeybind(this, "LongDistance", "None");
			AdminKeybind = KeybindLoader.RegisterKeybind(this, "RunAsAdmin", "None");
			for (int i = 0; i < LoadoutKeybinds.Length; i++)
				LoadoutKeybinds[i] = KeybindLoader.RegisterKeybind(this, "ApplyLoadout" + (i + 1), "None");
			UndoReforgeKeybind = KeybindLoader.RegisterKeybind(this, "UndoReforge", "None");
			ResetCooldownsKeybind = KeybindLoader.RegisterKeybind(this, "ResetCooldowns", "K");
			ToggleCombatAnalyticsKeybind = KeybindLoader.RegisterKeybind(this, "ToggleCombatAnalytics", "L");
			TriggerTipKeybind = KeybindLoader.RegisterKeybind(this, "TriggerTacticalTip", "None");
			TogglePodKeybind = KeybindLoader.RegisterKeybind(this, "TogglePod", "None");
			SniffOreKeybind = KeybindLoader.RegisterKeybind(this, "SniffOre", "None");

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
			// ChatManager keeps handlers for the whole game session, so drop ours to let a reload register it again.
			try
			{
				var handlers = typeof(Terraria.UI.Chat.ChatManager).GetField("_handlers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)?.GetValue(null)
					as System.Collections.Generic.Dictionary<string, Terraria.UI.Chat.ITagHandler>;
				handlers?.Remove(CalloutMarkTagHandler.TagName);
			}
			catch { }

			OpenAugmentListKeybind = null;
			DebugTriggerPopupKeybind = null;
			DebugSpawnVendorKeybind = null;
			DebugToggleShopKeybind = null;
			CleanseKeybind = null;
			ChronobreakKeybind = null;
			GetOverHereKeybind = null;
			BladeDanceKeybind = null;
			DangerCloseKeybind = null;
			DiscountRodKeybind = null;
			LongDistanceKeybind = null;
			AdminKeybind = null;
			GetOverHereAugment.UnloadSounds();
			for (int i = 0; i < LoadoutKeybinds.Length; i++)
				LoadoutKeybinds[i] = null;
			UndoReforgeKeybind = null;
			ResetCooldownsKeybind = null;
			ToggleCombatAnalyticsKeybind = null;
			TriggerTipKeybind = null;
			TogglePodKeybind = null;
			SniffOreKeybind = null;
			PodSprites.Unload();
			ClassIcons.Unload();
		}

		public override void HandlePacket(BinaryReader reader, int whoAmI)
		{
			try
			{
				HandlePacketInternal(reader, whoAmI);
			}
			catch (System.Exception e)
			{
				// A malformed or unexpected packet must not crash the server or kick the player.
				Logger.Error($"Failed to handle mod packet from player {whoAmI}.", e);
			}
		}

		private void HandlePacketInternal(BinaryReader reader, int whoAmI)
		{
			var type = (AugmentPacketType)reader.ReadByte();
			if (AugmentNet.HandlePacket(type, reader, whoAmI))
				return;

			switch (type)
			{
				case AugmentPacketType.PodPetSync:
					PodPetPlayer.HandleSync(reader, whoAmI);
					break;

				case AugmentPacketType.FallenUnitRecover:
					FallenUnitSystem.HandleOpenRequest(whoAmI, reader.ReadInt32());
					break;

				case AugmentPacketType.FallenUnitOffer:
				{
					int unitId = reader.ReadInt32();
					int count = reader.ReadByte();
					var ids = new System.Collections.Generic.List<string>(count);
					for (int i = 0; i < count; i++)
						ids.Add(reader.ReadString());
					FallenUnitSystem.HandleOffer(unitId, ids);
					break;
				}

				case AugmentPacketType.FallenUnitPick:
				{
					int unitId = reader.ReadInt32();
					FallenUnitSystem.HandlePickRequest(whoAmI, unitId, reader.ReadString());
					break;
				}

				case AugmentPacketType.FallenUnitRecovered:
				{
					int unitId = reader.ReadInt32();
					FallenUnitSystem.HandleRecovered(unitId, reader.ReadString());
					break;
				}

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
					SupportEffects.HandleCleanseRequest(whoAmI, reader);
					break;

				case AugmentPacketType.CleanseClearDebuffs:
					SupportEffects.HandleCleanseClearDebuffs();
					break;

				case AugmentPacketType.ChronobreakRequest:
					SupportEffects.HandleChronobreakRequest(whoAmI);
					break;

				case AugmentPacketType.ChronobreakApply:
					SupportEffects.HandleChronobreakApply(reader.ReadByte());
					break;

				case AugmentPacketType.GetOverHereRequest:
					SupportEffects.HandleGetOverHereRequest(whoAmI);
					break;

				case AugmentPacketType.GetOverHereApply:
					SupportEffects.HandleGetOverHereApply(reader.ReadByte());
					break;

				case AugmentPacketType.GetOverHereEffect:
					SupportEffects.HandleGetOverHereEffect(reader.ReadByte(), reader.ReadByte());
					break;

				case AugmentPacketType.MediGunHealRequest:
					SupportEffects.HandleMediGunHealRequest(whoAmI, reader.ReadByte(), reader.ReadInt32());
					break;

				case AugmentPacketType.MediGunOverclockActivate:
					SupportEffects.HandleMediGunOverclockActivate(whoAmI, reader.ReadByte(), reader.ReadByte());
					break;

				case AugmentPacketType.SupportHealVisual:
					SupportEffects.HandleHealVisual(reader.ReadByte(), reader.ReadInt32(), reader.ReadInt32());
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

				case AugmentPacketType.SyncBossKills:
				{
					reader.ReadByte();
					if (Main.netMode == NetmodeID.Server && whoAmI >= 0 && whoAmI < Main.maxPlayers && Main.player[whoAmI].active)
						Main.player[whoAmI].GetModPlayer<AugmentPlayer>().ReadBossKillsMerge(reader);
					break;
				}

				case AugmentPacketType.ClientFullState:
				{
					reader.ReadByte();
					if (Main.netMode == NetmodeID.Server && whoAmI >= 0 && whoAmI < Main.maxPlayers && Main.player[whoAmI].active)
					{
						Main.player[whoAmI].GetModPlayer<AugmentPlayer>().ReadAugmentState(reader);
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
