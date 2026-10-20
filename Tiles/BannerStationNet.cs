using System;
using System.Collections.Generic;
using System.IO;
using Augments.Items;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace Augments.Tiles
{
    // Everything that changes a placed station goes through here, so singleplayer and multiplayer share one set of rules.
    // The server owns the contents. Clients ask, and are handed back anything the server refuses.
    internal static class BannerStationNet
    {
        private const float ReachPixels = 16f * 30f;

        private const byte ReplyRefund = 0;
        private const byte ReplyGrant = 1;
        private const byte ReplyKitRefund = 2;

        // ---------- Client entry points ----------

        public static void ToggleUI(int left, int top)
        {
            if (Main.netMode == NetmodeID.Server)
                return;

            if (BannerStationEntity.TryGetAt(left, top, out BannerStationEntity station) && !station.Initialized)
            {
                // A station nobody has filled in yet (placed by someone else, or the claim was missed) starts empty.
                if (Main.netMode == NetmodeID.SinglePlayer)
                    station.ApplyInit(station.GetTier(), new List<int>());
                else
                    SendInit(left, top, station.GetTier(), "");
            }

            ModContent.GetInstance<AugmentUISystem>().ShowBannerStation(left, top);
        }

        // Puts one banner into the station. The caller removes it from the cursor or inventory when this returns true.
        public static bool Deposit(int left, int top, int itemType)
        {
            if (!BannerStationEntity.TryGetAt(left, top, out BannerStationEntity station) || !station.Initialized || !station.CanAdd(itemType))
                return false;

            if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                // Shown right away; the server's copy replaces it on the next sync, and sends the banner back if it disagrees.
                station.Banners.Add(itemType);

                ModPacket packet = NewPacket(AugmentPacketType.BannerStationDeposit, left, top);
                packet.Write(itemType);
                packet.Send();
            }
            else
            {
                station.Banners.Add(itemType);
            }

            return true;
        }

        public static void Withdraw(int left, int top, int itemType)
        {
            if (!BannerStationEntity.TryGetAt(left, top, out BannerStationEntity station) || !station.Initialized)
                return;

            if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                // The server removes it and hands the banner back, so it can never be taken twice.
                station.Banners.Remove(itemType);

                ModPacket packet = NewPacket(AugmentPacketType.BannerStationWithdraw, left, top);
                packet.Write(itemType);
                packet.Send();
            }
            else if (station.Banners.Remove(itemType))
            {
                Give(Main.LocalPlayer, itemType);
            }
        }

        public static void RequestUpgrade(BannerStationEntity station, BannerStationKitItem kit, Player player)
        {
            if (!station.Initialized)
            {
                Main.NewText("This station is still setting up.", 255, 140, 90);
                return;
            }

            int tier = station.GetTier();
            if (kit.TargetTier != tier + 1)
            {
                string text = tier >= BannerStationData.TierCount - 1
                    ? "This station is already at its highest tier."
                    : $"This kit upgrades a {BannerStationData.TierNames[kit.TargetTier - 1]} station. Use the {BannerStationData.TierNames[tier + 1]} kit on this one.";
                Main.NewText(text, 255, 140, 90);
                return;
            }

            int left = station.Position.X;
            int top = station.Position.Y;
            Item held = player.HeldItem;
            held.stack--;
            if (held.stack <= 0)
                held.TurnToAir();

            if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                ModPacket packet = NewPacket(AugmentPacketType.BannerStationUpgrade, left, top);
                packet.Write((byte)kit.TargetTier);
                packet.Send();
            }
            else
            {
                ApplyUpgrade(station, kit.TargetTier);
            }

            SoundEngine.PlaySound(SoundID.Item37, station.Center);
        }

        // Used by the placing player's client: fills a fresh station with what the item carried.
        public static void SendInit(int left, int top, int tier, string data)
        {
            ModPacket packet = NewPacket(AugmentPacketType.BannerStationInit, left, top);
            packet.Write((byte)tier);
            packet.Write(data ?? "");
            packet.Send();
        }

        // ---------- Packet handling ----------

        public static void Handle(AugmentPacketType type, BinaryReader reader, int whoAmI)
        {
            if (type == AugmentPacketType.BannerStationReply)
            {
                HandleReply(reader);
                return;
            }

            int left = reader.ReadInt16();
            int top = reader.ReadInt16();

            if (Main.netMode != NetmodeID.Server)
                return;

            Player sender = Main.player[whoAmI];
            if (!sender.active || !BannerStationEntity.TryGetAt(left, top, out BannerStationEntity station))
                return;

            bool inReach = Vector2.Distance(sender.Center, station.Center) <= ReachPixels;

            switch (type)
            {
                case AugmentPacketType.BannerStationInit:
                    HandleInit(reader, station);
                    break;
                case AugmentPacketType.BannerStationDeposit:
                    HandleDeposit(reader, station, whoAmI, inReach);
                    break;
                case AugmentPacketType.BannerStationWithdraw:
                    HandleWithdraw(reader, station, whoAmI, inReach);
                    break;
                case AugmentPacketType.BannerStationUpgrade:
                    HandleUpgrade(reader, station, whoAmI, inReach);
                    break;
            }
        }

        private static void HandleInit(BinaryReader reader, BannerStationEntity station)
        {
            int tier = reader.ReadByte();
            string data = reader.ReadString();

            // Only the first claim counts, and only for the tier the placed tile really is.
            if (station.Initialized || tier != station.GetTier())
                return;

            station.ApplyInit(tier, BannerStationData.Decode(data));
            Sync(station);
        }

        private static void HandleDeposit(BinaryReader reader, BannerStationEntity station, int whoAmI, bool inReach)
        {
            int itemType = reader.ReadInt32();

            if (inReach && station.Initialized && station.CanAdd(itemType))
            {
                station.Banners.Add(itemType);
            }
            else
            {
                SendReply(whoAmI, ReplyRefund, itemType);
            }

            // Always sent, so a client that guessed wrong snaps back to the real contents.
            Sync(station);
        }

        private static void HandleWithdraw(BinaryReader reader, BannerStationEntity station, int whoAmI, bool inReach)
        {
            int itemType = reader.ReadInt32();

            if (inReach && station.Initialized && station.Banners.Remove(itemType))
                SendReply(whoAmI, ReplyGrant, itemType);

            Sync(station);
        }

        private static void HandleUpgrade(BinaryReader reader, BannerStationEntity station, int whoAmI, bool inReach)
        {
            int targetTier = reader.ReadByte();

            if (inReach && station.Initialized && targetTier == station.GetTier() + 1 && targetTier < BannerStationData.TierCount)
                ApplyUpgrade(station, targetTier);
            else
                SendReply(whoAmI, ReplyKitRefund, KitType(targetTier));

            Sync(station);
        }

        private static void HandleReply(BinaryReader reader)
        {
            byte kind = reader.ReadByte();
            int itemType = reader.ReadInt32();

            if (Main.netMode != NetmodeID.MultiplayerClient || itemType <= 0 || itemType >= ItemLoader.ItemCount)
                return;

            Give(Main.LocalPlayer, itemType);
            if (kind != ReplyGrant)
                Main.NewText("The station could not take that.", 255, 140, 90);
        }

        // ---------- Shared rules ----------

        // Raises the station one tier: the entity and all six tiles of its sprite. Runs in singleplayer and on the server.
        private static void ApplyUpgrade(BannerStationEntity station, int newTier)
        {
            station.Tier = newTier;
            station.Initialized = true;

            int left = station.Position.X;
            int top = station.Position.Y;
            for (int dx = 0; dx < 2; dx++)
            {
                for (int dy = 0; dy < 3; dy++)
                {
                    Tile tile = Main.tile[left + dx, top + dy];
                    if (tile.HasTile && tile.TileType == ModContent.TileType<BannerStationTile>())
                        tile.TileFrameX = (short)(newTier * 36 + dx * 18);
                }
            }

            if (Main.netMode == NetmodeID.Server)
                NetMessage.SendTileSquare(-1, left, top, 2, 3);
        }

        private static int KitType(int targetTier)
        {
            switch (targetTier)
            {
                case 2: return ModContent.ItemType<BannerStationKitMk3Item>();
                case 3: return ModContent.ItemType<BannerStationKitMk4Item>();
                default: return ModContent.ItemType<BannerStationKitMk2Item>();
            }
        }

        // Pushes the station's current contents to every client.
        public static void Sync(BannerStationEntity station)
        {
            if (Main.netMode == NetmodeID.Server)
                NetMessage.SendData(MessageID.TileEntitySharing, -1, -1, null, station.ID, station.Position.X, station.Position.Y);
        }

        private static void SendReply(int toClient, byte kind, int itemType)
        {
            ModPacket packet = ModContent.GetInstance<Augments>().GetPacket();
            packet.Write((byte)AugmentPacketType.BannerStationReply);
            packet.Write(kind);
            packet.Write(itemType);
            packet.Send(toClient);
        }

        private static ModPacket NewPacket(AugmentPacketType type, int left, int top)
        {
            ModPacket packet = ModContent.GetInstance<Augments>().GetPacket();
            packet.Write((byte)type);
            packet.Write((short)left);
            packet.Write((short)top);
            return packet;
        }

        private static void Give(Player player, int itemType)
        {
            player.QuickSpawnItem(player.GetSource_FromThis(), itemType);
        }
    }
}
