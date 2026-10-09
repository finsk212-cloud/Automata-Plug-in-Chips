using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Augments
{
	internal static class AugmentNet
	{
		// Debug commands, dev keybinds and the dev-mode chip editor are always available
		// in singleplayer (only the local player is affected). In multiplayer they stay
		// off unless the (server-side) EnableDeveloperTools config option is ticked.
		public static bool EnableDebugCommandsInMultiplayer =>
			Main.netMode == NetmodeID.SinglePlayer || AugmentServerConfig.Instance?.EnableDeveloperTools == true;

		private sealed class PendingReward
		{
			public HashSet<string> Choices;
			public AugmentRarity Rarity;
			public RarityBracket Bracket;
			public int RerollCount;
		}

		private static readonly Dictionary<int, PendingReward> PendingRewardChoicesByPlayer = new Dictionary<int, PendingReward>();

		public static bool HandlePacket(AugmentPacketType type, BinaryReader reader, int whoAmI)
		{
			switch (type)
			{
				case AugmentPacketType.OpenRewardChoices:
					HandleOpenRewardChoices(reader);
					return true;
				case AugmentPacketType.RerollRequest:
					HandleRerollRequest(reader, whoAmI);
					return true;
				case AugmentPacketType.LuckyFindDropRequest:
					HandleLuckyFindDropRequest(reader, whoAmI);
					return true;
				case AugmentPacketType.ChooseReward:
					HandleChooseReward(reader, whoAmI);
					return true;
				case AugmentPacketType.SyncAugmentState:
					HandleSyncAugmentState(reader);
					return true;
				case AugmentPacketType.RequestAugmentSync:
					HandleRequestAugmentSync(whoAmI);
					return true;
				case AugmentPacketType.DebugRequestReward:
					HandleDebugRewardRequest(whoAmI);
					return true;
				case AugmentPacketType.DebugCommandRequest:
					HandleDebugCommandRequest(reader, whoAmI);
					return true;
				case AugmentPacketType.VendorSellRequest:
					HandleVendorSellRequest(reader, whoAmI);
					return true;
				case AugmentPacketType.VendorBuyBackRequest:
					HandleVendorBuyBackRequest(reader, whoAmI);
					return true;
				case AugmentPacketType.RequestVendorSpawn:
					HandleVendorSpawnRequest(whoAmI);
					return true;
				case AugmentPacketType.LoadoutRequest:
					HandleLoadoutRequest(reader, whoAmI);
					return true;
				case AugmentPacketType.ApplyNPCEffect:
					HandleApplyNPCEffect(reader, whoAmI);
					return true;
			}

			return false;
		}

		public static void SendRewardChoices(int toClient, List<Augment> choices, AugmentRarity rarity, RarityBracket bracket, int rerollCount = 0)
		{
			if (Main.netMode != NetmodeID.Server)
				return;

			var pendingIds = new HashSet<string>();
			foreach (var augment in choices)
			{
				if (augment != null)
					pendingIds.Add(augment.Id);
			}

			PendingRewardChoicesByPlayer[toClient] = new PendingReward { Choices = pendingIds, Rarity = rarity, Bracket = bracket, RerollCount = rerollCount };

			ModPacket packet = ModContent.GetInstance<Augments>().GetPacket();
			packet.Write((byte)AugmentPacketType.OpenRewardChoices);
			packet.Write((byte)rarity);
			packet.Write((byte)bracket);
			packet.Write((byte)rerollCount);
			packet.Write((byte)choices.Count);
			foreach (var augment in choices)
				packet.Write(augment.Id);
			packet.Send(toClient);
		}

		public static void SendRerollRequest(Player player)
		{
			if (Main.netMode != NetmodeID.MultiplayerClient || player == null)
				return;

			ModPacket packet = ModContent.GetInstance<Augments>().GetPacket();
			packet.Write((byte)AugmentPacketType.RerollRequest);
			packet.Write((byte)player.whoAmI);
			packet.Send();
		}

		public static void SendLuckyFindDropRequest(Player player, Vector2 position, float effectiveness)
		{
			if (Main.netMode != NetmodeID.MultiplayerClient || player == null)
				return;

			ModPacket packet = ModContent.GetInstance<Augments>().GetPacket();
			packet.Write((byte)AugmentPacketType.LuckyFindDropRequest);
			packet.Write((byte)player.whoAmI);
			packet.Write(position.X);
			packet.Write(position.Y);
			packet.Write(effectiveness);
			packet.Send();
		}

		public static void SendChooseReward(string augmentId)
		{
			if (Main.netMode != NetmodeID.MultiplayerClient)
				return;

			ModPacket packet = ModContent.GetInstance<Augments>().GetPacket();
			packet.Write((byte)AugmentPacketType.ChooseReward);
			packet.Write(augmentId);
			packet.Send();
		}

		public static void SendSyncPlayer(Player player, int toClient = -1)
		{
			if (Main.netMode != NetmodeID.Server || player == null || !player.active)
				return;

			ModPacket packet = ModContent.GetInstance<Augments>().GetPacket();
			packet.Write((byte)AugmentPacketType.SyncAugmentState);
			packet.Write((byte)player.whoAmI);
			player.GetModPlayer<AugmentPlayer>().WriteAugmentState(packet);
			packet.Send(toClient);
		}

		public static void SendRequestAugmentSync()
		{
			if (Main.netMode != NetmodeID.MultiplayerClient)
				return;

			ModPacket packet = ModContent.GetInstance<Augments>().GetPacket();
			packet.Write((byte)AugmentPacketType.RequestAugmentSync);
			packet.Send();
		}

		public static void SendDebugRewardRequest()
		{
			if (Main.netMode != NetmodeID.MultiplayerClient || !EnableDebugCommandsInMultiplayer)
				return;

			ModPacket packet = ModContent.GetInstance<Augments>().GetPacket();
			packet.Write((byte)AugmentPacketType.DebugRequestReward);
			packet.Send();
		}

		public static void SendDebugCommandRequest(DebugAugmentCommandType command, string augmentId = "")
		{
			if (Main.netMode != NetmodeID.MultiplayerClient || !EnableDebugCommandsInMultiplayer)
				return;

			ModPacket packet = ModContent.GetInstance<Augments>().GetPacket();
			packet.Write((byte)AugmentPacketType.DebugCommandRequest);
			packet.Write((byte)command);
			packet.Write(augmentId ?? "");
			packet.Send();
		}

		public static void SendVendorSellRequest(string augmentId)
		{
			SendVendorRequest(AugmentPacketType.VendorSellRequest, augmentId);
		}

		public static void SendVendorBuyBackRequest(string augmentId)
		{
			SendVendorRequest(AugmentPacketType.VendorBuyBackRequest, augmentId);
		}

		private static void SendVendorRequest(AugmentPacketType type, string augmentId)
		{
			if (Main.netMode != NetmodeID.MultiplayerClient || string.IsNullOrEmpty(augmentId))
				return;

			ModPacket packet = ModContent.GetInstance<Augments>().GetPacket();
			packet.Write((byte)type);
			packet.Write(augmentId);
			packet.Send();
		}

		public static void RequestVendorSpawn(Player player)
		{
			if (player == null || !player.active)
				return;

			if (Main.netMode == NetmodeID.MultiplayerClient)
			{
				ModPacket packet = ModContent.GetInstance<Augments>().GetPacket();
				packet.Write((byte)AugmentPacketType.RequestVendorSpawn);
				packet.Send();
				return;
			}

			SpawnVendor(player);
		}

		private static void HandleOpenRewardChoices(BinaryReader reader)
		{
			AugmentRarity rarity = (AugmentRarity)reader.ReadByte();
			RarityBracket bracket = (RarityBracket)reader.ReadByte();
			int rerollCount = reader.ReadByte();
			int count = reader.ReadByte();
			var choices = new List<Augment>();
			for (int i = 0; i < count; i++)
			{
				string id = reader.ReadString();
				Augment augment = AugmentDatabase.GetById(id);
				if (augment != null)
					choices.Add(augment);
			}

			if (Main.netMode != NetmodeID.MultiplayerClient)
				return;

			if (choices.Count > 0)
				ModContent.GetInstance<AugmentUISystem>().ShowChoices(choices, rarity, bracket, true, rerollCount);
		}

		private static void HandleRerollRequest(BinaryReader reader, int whoAmI)
		{
			int playerId = reader.ReadByte();

			if (Main.netMode != NetmodeID.Server || playerId != whoAmI || playerId < 0 || playerId >= Main.maxPlayers)
				return;
			if (!PendingRewardChoicesByPlayer.TryGetValue(playerId, out var pending) || pending.Choices.Count == 0)
				return;

			if (pending.RerollCount >= 2)
				return;

			Player player = Main.player[playerId];
			if (!player.active)
				return;

			int essenceType = ModContent.ItemType<AugmentEssenceItem>();
			if (pending.RerollCount == 1)
			{
				if (player.CountItem(essenceType) < 4)
					return;
			}

			AugmentPlayer augmentPlayer = player.GetModPlayer<AugmentPlayer>();
			if (!AugmentRewardLogic.TryRollRewardChoices(augmentPlayer, pending.Bracket, pending.Choices, out List<Augment> choices, out AugmentRarity finalRarity))
				return;

			if (pending.RerollCount == 1)
			{
				for (int i = 0; i < 4; i++)
					player.ConsumeItem(essenceType);
				augmentPlayer.SyncInventory();
			}

			pending.RerollCount++;
			SendRewardChoices(playerId, choices, finalRarity, pending.Bracket, pending.RerollCount);
		}

		private static void HandleLuckyFindDropRequest(BinaryReader reader, int whoAmI)
		{
			int playerId = reader.ReadByte();
			var position = new Vector2(reader.ReadSingle(), reader.ReadSingle());
			float effectiveness = MathHelper.Clamp(reader.ReadSingle(), 0f, 1f);

			if (Main.netMode != NetmodeID.Server || playerId != whoAmI || playerId < 0 || playerId >= Main.maxPlayers)
				return;

			Player player = Main.player[playerId];
			if (!player.active || !player.GetModPlayer<AugmentPlayer>().HasAugment("lucky_find"))
				return;

			LuckyFindAugment.TryDropCoinsServer(player, position, effectiveness);
		}

		private static void HandleChooseReward(BinaryReader reader, int whoAmI)
		{
			string augmentId = reader.ReadString();

			if (Main.netMode != NetmodeID.Server)
				return;
			if (whoAmI < 0 || whoAmI >= Main.maxPlayers)
				return;
			if (!PendingRewardChoicesByPlayer.TryGetValue(whoAmI, out var pendingChoices))
				return;
			if (!pendingChoices.Choices.Contains(augmentId))
				return;

			Player player = Main.player[whoAmI];
			if (!player.active)
				return;

			var augmentPlayer = player.GetModPlayer<AugmentPlayer>();
			Augment augment = AugmentDatabase.GetById(augmentId);
			if (augment == null || augmentPlayer.HasAugment(augmentId))
				return;

			if (!augmentPlayer.ApplyRewardAugment(augment, false))
				return;

			PendingRewardChoicesByPlayer.Remove(whoAmI);
			SendSyncPlayer(player);
		}

		private static void HandleSyncAugmentState(BinaryReader reader)
		{
			int playerIndex = reader.ReadByte();
			if (playerIndex >= 0 && playerIndex < Main.maxPlayers)
			{
				Main.player[playerIndex].GetModPlayer<AugmentPlayer>().ReadAugmentState(reader);
				if (Main.netMode == NetmodeID.MultiplayerClient)
				{
					ModContent.GetInstance<Augments>().Logger.Info($"Synced support state for player={Main.player[playerIndex].name}");
					if (playerIndex == Main.myPlayer)
						ModContent.GetInstance<AugmentUISystem>().RefreshOpenPlayerPanels();
				}
			}
			else
			{
				new AugmentPlayer().ReadAugmentState(reader);
			}
		}

		private static void HandleRequestAugmentSync(int whoAmI)
		{
			if (Main.netMode != NetmodeID.Server)
				return;
			if (whoAmI < 0 || whoAmI >= Main.maxPlayers)
				return;

			PendingRewardChoicesByPlayer.Remove(whoAmI);

			for (int i = 0; i < Main.maxPlayers; i++)
			{
				Player player = Main.player[i];
				if (player.active)
					SendSyncPlayer(player, whoAmI);
			}
		}

		private static void HandleVendorSpawnRequest(int whoAmI)
		{
			if (Main.netMode != NetmodeID.Server || whoAmI < 0 || whoAmI >= Main.maxPlayers)
				return;

			Player player = Main.player[whoAmI];
			if (player.active)
				SpawnVendor(player);
		}

		private static void HandleDebugRewardRequest(int whoAmI)
		{
			if (Main.netMode != NetmodeID.Server || !EnableDebugCommandsInMultiplayer || whoAmI < 0 || whoAmI >= Main.maxPlayers)
				return;

			Player player = Main.player[whoAmI];
			if (player.active)
				AugmentRewardLogic.GrantReward(player, RarityBracket.FinalCalamity);
		}

		private static void HandleDebugCommandRequest(BinaryReader reader, int whoAmI)
		{
			DebugAugmentCommandType command = (DebugAugmentCommandType)reader.ReadByte();
			string augmentId = reader.ReadString();

			if (Main.netMode != NetmodeID.Server || !EnableDebugCommandsInMultiplayer)
				return;
			if (whoAmI < 0 || whoAmI >= Main.maxPlayers)
				return;

			Player player = Main.player[whoAmI];
			if (!player.active || !ApplyDebugCommand(player, command, augmentId))
				return;

			SendSyncPlayer(player);
		}

		private static void HandleVendorSellRequest(BinaryReader reader, int whoAmI)
		{
			string augmentId = reader.ReadString();
			if (!TryGetActiveSender(whoAmI, out Player player))
				return;

			AugmentPlayer augmentPlayer = player.GetModPlayer<AugmentPlayer>();
			if (augmentPlayer.SellAugmentByIdServerAuthoritative(augmentId, false))
				SendSyncPlayer(player);
		}

		private static void HandleVendorBuyBackRequest(BinaryReader reader, int whoAmI)
		{
			string augmentId = reader.ReadString();
			if (!TryGetActiveSender(whoAmI, out Player player))
				return;

			AugmentPlayer augmentPlayer = player.GetModPlayer<AugmentPlayer>();
			if (augmentPlayer.BuyBackSoldAugmentByIdServerAuthoritative(augmentId, false))
				SendSyncPlayer(player);
		}

		public static void SendLoadoutRequest(LoadoutOp op, int slot, string id)
		{
			if (Main.netMode != NetmodeID.MultiplayerClient)
				return;

			ModPacket packet = ModContent.GetInstance<Augments>().GetPacket();
			packet.Write((byte)AugmentPacketType.LoadoutRequest);
			packet.Write((byte)op);
			packet.Write((byte)slot);
			packet.Write(id ?? "");
			packet.Send();
		}

		private static void HandleLoadoutRequest(BinaryReader reader, int whoAmI)
		{
			LoadoutOp op = (LoadoutOp)reader.ReadByte();
			int slot = reader.ReadByte();
			string id = reader.ReadString();
			if (!TryGetActiveSender(whoAmI, out Player player))
				return;

			player.GetModPlayer<AugmentPlayer>().ExecuteLoadoutOp(op, slot, id);
		}

		private static bool TryGetActiveSender(int whoAmI, out Player player)
		{
			player = null;
			if (Main.netMode != NetmodeID.Server || whoAmI < 0 || whoAmI >= Main.maxPlayers)
				return false;

			player = Main.player[whoAmI];
			return player.active;
		}

		public static bool ApplyDebugCommand(Player player, DebugAugmentCommandType command, string augmentId = "")
		{
			if (Main.netMode == NetmodeID.MultiplayerClient || player == null || !player.active || !EnableDebugCommandsInMultiplayer)
				return false;

			AugmentPlayer augmentPlayer = player.GetModPlayer<AugmentPlayer>();
			switch (command)
			{
			case DebugAugmentCommandType.Add:
					return augmentPlayer.GrantAugmentByIdServerAuthoritative(augmentId, false, true);
				case DebugAugmentCommandType.Remove:
					return augmentPlayer.RemoveAugmentByIdServerAuthoritative(augmentId, false);
				case DebugAugmentCommandType.AddAll:
				{
					bool changed = false;
					foreach (Augment augment in AugmentDatabase.All)
					{
						if (augmentPlayer.OwnedIds.Count >= AugmentPlayer.MaxOwnedAugments)
							break;
						if (augmentPlayer.GrantAugmentByIdServerAuthoritative(augment.Id, false))
							changed = true;
					}
					return changed;
				}
				case DebugAugmentCommandType.Clear:
				{
					if (augmentPlayer.OwnedIds.Count == 0)
						return false;

					foreach (string id in new List<string>(augmentPlayer.OwnedIds))
						augmentPlayer.RemoveAugmentByIdServerAuthoritative(id, false);
					return true;
				}
				case DebugAugmentCommandType.Sell:
					return augmentPlayer.SellAugmentByIdServerAuthoritative(augmentId, false);
				case DebugAugmentCommandType.BuyBack:
					return augmentPlayer.BuyBackSoldAugmentByIdServerAuthoritative(augmentId, false);
				default:
					return false;
			}
		}

		private static void SpawnVendor(Player player)
		{
			if (player == null || !player.active)
				return;

			int vendorType = ModContent.NPCType<AugmentVendorNPC>();
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC existingNpc = Main.npc[i];
				if (existingNpc.active && existingNpc.type == vendorType)
				{
					// Teleport existing vendor directly to the player!
					existingNpc.position.X = player.Center.X - existingNpc.width / 2f;
					existingNpc.position.Y = player.Bottom.Y - existingNpc.height;
					existingNpc.velocity = Vector2.Zero;
					existingNpc.direction = player.direction;
					existingNpc.netUpdate = true;

					if (Main.netMode == NetmodeID.Server)
					{
						NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, i);
					}

					for (int d = 0; d < 25; d++)
					{
						Dust.NewDust(existingNpc.position, existingNpc.width, existingNpc.height, DustID.Electric, 0f, 0f, 100, Color.Cyan, 1.2f);
					}
					Terraria.Audio.SoundEngine.PlaySound(SoundID.Item6, existingNpc.Center);
					Main.NewText("✦ Mistress 2B teleported to your position! ✦", new Color(100, 220, 255));
					return;
				}
			}

			int spawnX = (int)player.Center.X;
			int spawnY = (int)(player.Bottom.Y - 40);
			int npcIndex = NPC.NewNPC(
				player.GetSource_FromThis(),
				spawnX,
				spawnY,
				vendorType);

			if (npcIndex >= 0 && npcIndex < Main.maxNPCs)
			{
				Main.npc[npcIndex].netUpdate = true;
				if (Main.netMode == NetmodeID.Server)
					NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, npcIndex);
			}

			for (int d = 0; d < 25; d++)
			{
				Dust.NewDust(player.position, player.width, player.height, DustID.Electric, 0f, 0f, 100, Color.Cyan, 1.2f);
			}
			Terraria.Audio.SoundEngine.PlaySound(SoundID.Item6, player.Center);
			Main.NewText("✦ Mistress 2B summoned! ✦", new Color(100, 220, 255));
		}

		public enum NPCEffectType : byte
		{
			Bleed,
			Slow,
			Cracked,
			Nanites,
			KineticMark
		}

		public static void SendApplyNPCEffectBleed(int npcIndex, int durationTicks, int dps)
		{
			if (Main.netMode != NetmodeID.MultiplayerClient || npcIndex < 0 || npcIndex >= Main.maxNPCs)
				return;

			ModPacket packet = ModContent.GetInstance<Augments>().GetPacket();
			packet.Write((byte)AugmentPacketType.ApplyNPCEffect);
			packet.Write((byte)NPCEffectType.Bleed);
			packet.Write((short)npcIndex);
			packet.Write(durationTicks);
			packet.Write(dps);
			packet.Send();
		}

		public static void SendApplyNPCEffectSlow(int npcIndex, int durationTicks, float slowPercent)
		{
			if (Main.netMode != NetmodeID.MultiplayerClient || npcIndex < 0 || npcIndex >= Main.maxNPCs)
				return;

			ModPacket packet = ModContent.GetInstance<Augments>().GetPacket();
			packet.Write((byte)AugmentPacketType.ApplyNPCEffect);
			packet.Write((byte)NPCEffectType.Slow);
			packet.Write((short)npcIndex);
			packet.Write(durationTicks);
			packet.Write(slowPercent);
			packet.Send();
		}

		public static void SendApplyNPCEffectCracked(int npcIndex, int durationTicks)
		{
			if (Main.netMode != NetmodeID.MultiplayerClient || npcIndex < 0 || npcIndex >= Main.maxNPCs)
				return;

			ModPacket packet = ModContent.GetInstance<Augments>().GetPacket();
			packet.Write((byte)AugmentPacketType.ApplyNPCEffect);
			packet.Write((byte)NPCEffectType.Cracked);
			packet.Write((short)npcIndex);
			packet.Write(durationTicks);
			packet.Send();
		}

		public static void SendApplyNPCEffectNanites(int npcIndex, int durationTicks, int minionIdentity)
		{
			if (Main.netMode != NetmodeID.MultiplayerClient || npcIndex < 0 || npcIndex >= Main.maxNPCs)
				return;

			ModPacket packet = ModContent.GetInstance<Augments>().GetPacket();
			packet.Write((byte)AugmentPacketType.ApplyNPCEffect);
			packet.Write((byte)NPCEffectType.Nanites);
			packet.Write((short)npcIndex);
			packet.Write(durationTicks);
			packet.Write(minionIdentity);
			packet.Send();
		}

		public static void SendApplyNPCEffectKineticMark(int npcIndex, int durationTicks)
		{
			if (Main.netMode != NetmodeID.MultiplayerClient || npcIndex < 0 || npcIndex >= Main.maxNPCs)
				return;

			ModPacket packet = ModContent.GetInstance<Augments>().GetPacket();
			packet.Write((byte)AugmentPacketType.ApplyNPCEffect);
			packet.Write((byte)NPCEffectType.KineticMark);
			packet.Write((short)npcIndex);
			packet.Write(durationTicks);
			packet.Send();
		}

		private static void HandleApplyNPCEffect(BinaryReader reader, int whoAmI)
		{
			NPCEffectType effectType = (NPCEffectType)reader.ReadByte();
			short npcIndex = reader.ReadInt16();

			int durationTicks = 0;
			int dps = 0;
			float slowPercent = 0f;
			int minionIdentity = -1;

			switch (effectType)
			{
				case NPCEffectType.Bleed:
					durationTicks = reader.ReadInt32();
					dps = reader.ReadInt32();
					break;
				case NPCEffectType.Slow:
					durationTicks = reader.ReadInt32();
					slowPercent = reader.ReadSingle();
					break;
				case NPCEffectType.Cracked:
				case NPCEffectType.KineticMark:
					durationTicks = reader.ReadInt32();
					break;
				case NPCEffectType.Nanites:
					durationTicks = reader.ReadInt32();
					minionIdentity = reader.ReadInt32();
					break;
			}

			if (npcIndex < 0 || npcIndex >= Main.maxNPCs)
				return;

			NPC npc = Main.npc[npcIndex];
			if (!npc.active)
				return;

			switch (effectType)
			{
				case NPCEffectType.Bleed:
					npc.GetGlobalNPC<AugmentBleedNPC>().ApplyBleed(durationTicks, dps);
					break;
				case NPCEffectType.Slow:
					npc.GetGlobalNPC<AugmentSlowNPC>().ApplySlow(durationTicks, slowPercent);
					break;
				case NPCEffectType.Cracked:
					npc.GetGlobalNPC<AugmentCrackedNPC>().ApplyStack(durationTicks);

					// If on server, relay to all other clients so their local damage multipliers are up to date.
					if (Main.netMode == NetmodeID.Server)
					{
						ModPacket relay = ModContent.GetInstance<Augments>().GetPacket();
						relay.Write((byte)AugmentPacketType.ApplyNPCEffect);
						relay.Write((byte)NPCEffectType.Cracked);
						relay.Write(npcIndex);
						relay.Write(durationTicks);
						relay.Send(-1, whoAmI);
					}
					break;
				case NPCEffectType.Nanites:
					npc.GetGlobalNPC<AugmentNaniteNPC>().ApplyNanites(whoAmI, minionIdentity, durationTicks);

					if (Main.netMode == NetmodeID.Server)
					{
						ModPacket relay = ModContent.GetInstance<Augments>().GetPacket();
						relay.Write((byte)AugmentPacketType.ApplyNPCEffect);
						relay.Write((byte)NPCEffectType.Nanites);
						relay.Write(npcIndex);
						relay.Write(durationTicks);
						relay.Write(minionIdentity);
						relay.Send(-1, whoAmI);
					}
					break;
				case NPCEffectType.KineticMark:
					npc.GetGlobalNPC<AugmentMarksmanNPC>().ApplyMark(whoAmI, durationTicks);

					if (Main.netMode == NetmodeID.Server)
					{
						ModPacket relay = ModContent.GetInstance<Augments>().GetPacket();
						relay.Write((byte)AugmentPacketType.ApplyNPCEffect);
						relay.Write((byte)NPCEffectType.KineticMark);
						relay.Write(npcIndex);
						relay.Write(durationTicks);
						relay.Send(-1, whoAmI);
					}
					break;
			}
		}
	}
}
