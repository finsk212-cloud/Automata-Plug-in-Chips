using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace Augments
{
	// Fallen Units: broken androids hidden underground, each with a dead Pod beside it. Right-click one and it gives the
	// usual "pick 1 of 3" plugin offer, once per character. They are a way to find plugins without fighting a boss.
	//
	// They are not tiles: the world keeps a list of spots (saved with the world) and every game draws them on top of the
	// tiles. The list is made the first time a world runs with this feature, so new and old worlds both get them, placed
	// in caves away from spawn. Who has recovered which unit is kept in the world too, by character name, so in
	// multiplayer every player can take one offer from each body and nobody can take it away from the others.
	//
	// The offer opens a dusty panel (FallenUnitUISystem) where you wipe each card clean before you can pick it. Rarities roll
	// one bracket below what a boss would give at this point of the game, so bosses stay the best source.
	public sealed class FallenUnit
	{
		public int Id;
		public int X, Y;     // the tile just above the floor, in the middle of the body
		public byte Look;    // which of the four scenes (see FallenUnitArt)
		public byte Spot;    // where it rests: 0 a cave, 1 under water, 2 inside a living tree
		public bool Flip;    // facing left instead of right
		public string Name = "";
		public readonly HashSet<string> RecoveredBy = new HashSet<string>();
		// The cards waiting for each character who opened this body but has not picked yet (server and singleplayer only).
		public readonly Dictionary<string, List<string>> Offers = new Dictionary<string, List<string>>();

		public Vector2 Center => new Vector2(X * 16 + 8, Y * 16);
		public bool IsRecoveredBy(Player player) => player != null && RecoveredBy.Contains(player.name);
	}

	public sealed class FallenUnitSystem : ModSystem
	{
		public static readonly List<FallenUnit> Units = new List<FallenUnit>();
		private static bool generated;

		// World generation's own random numbers while a world is being made, the normal ones otherwise (old worlds, debug).
		private static Terraria.Utilities.UnifiedRandom Rand => WorldGen.generatingWorld ? WorldGen.genRand : Main.rand;
		private static int nextId = 1;

		// How close (in pixels) a player must be to right-click a unit, and to see its Pod message.
		public const float RecoverRange = 160f;

		public static FallenUnit Find(int id)
		{
			foreach (FallenUnit unit in Units)
			{
				if (unit.Id == id)
					return unit;
			}
			return null;
		}

		public static FallenUnit Nearest(Vector2 position, Player notRecoveredBy = null)
		{
			FallenUnit best = null;
			float bestDist = float.MaxValue;
			foreach (FallenUnit unit in Units)
			{
				if (notRecoveredBy != null && unit.IsRecoveredBy(notRecoveredBy))
					continue;
				float d = Vector2.DistanceSquared(position, unit.Center);
				if (d < bestDist)
				{
					bestDist = d;
					best = unit;
				}
			}
			return best;
		}

		// The rarity bracket of a Fallen Unit offer: one step below a boss at this point of the game.
		public static RarityBracket OfferBracket()
		{
			int bracket = (int)BossTierMap.GetCurrentWorldBracket() - 1;
			return (RarityBracket)Math.Max(0, bracket);
		}

		// --- Saving and syncing ---

		public override void ClearWorld()
		{
			Units.Clear();
			generated = false;
			nextId = 1;
		}

		public override void SaveWorldData(TagCompound tag)
		{
			tag["fallenGenerated"] = generated;
			tag["fallenNextId"] = nextId;
			var list = new List<TagCompound>();
			foreach (FallenUnit unit in Units)
			{
				list.Add(new TagCompound
				{
					["id"] = unit.Id,
					["x"] = unit.X,
					["y"] = unit.Y,
					["look"] = unit.Look,
						["spot"] = unit.Spot,
					["flip"] = unit.Flip,
					["name"] = unit.Name,
					["by"] = new List<string>(unit.RecoveredBy),
					["offers"] = SaveOffers(unit)
				});
			}
			tag["fallenUnits"] = list;
		}

		public override void LoadWorldData(TagCompound tag)
		{
			Units.Clear();
			generated = tag.GetBool("fallenGenerated");
			nextId = Math.Max(1, tag.GetInt("fallenNextId"));
			foreach (TagCompound t in tag.GetList<TagCompound>("fallenUnits"))
			{
				var unit = new FallenUnit
				{
					Id = t.GetInt("id"),
					X = t.GetInt("x"),
					Y = t.GetInt("y"),
					Look = (byte)(t.GetByte("look") % FallenUnitArt.LookCount),
						Spot = (byte)Math.Min(2, (int)t.GetByte("spot")),
					Flip = t.GetBool("flip"),
					Name = t.GetString("name")
				};
				foreach (string name in t.GetList<string>("by"))
					unit.RecoveredBy.Add(name);
				foreach (TagCompound o in t.GetList<TagCompound>("offers"))
					unit.Offers[o.GetString("name")] = new List<string>(o.GetList<string>("ids"));
				Units.Add(unit);
				nextId = Math.Max(nextId, unit.Id + 1);
			}
		}

		private static List<TagCompound> SaveOffers(FallenUnit unit)
		{
			var list = new List<TagCompound>();
			foreach (KeyValuePair<string, List<string>> kv in unit.Offers)
				list.Add(new TagCompound { ["name"] = kv.Key, ["ids"] = new List<string>(kv.Value) });
			return list;
		}

		public override void NetSend(BinaryWriter writer)
		{
			writer.Write(Units.Count);
			foreach (FallenUnit unit in Units)
			{
				writer.Write(unit.Id);
				writer.Write(unit.X);
				writer.Write(unit.Y);
				writer.Write(unit.Look);
				writer.Write(unit.Flip);
				writer.Write(unit.Spot);
				writer.Write(unit.Name ?? "");
				writer.Write(unit.RecoveredBy.Count);
				foreach (string name in unit.RecoveredBy)
					writer.Write(name);
			}
		}

		public override void NetReceive(BinaryReader reader)
		{
			Units.Clear();
			int count = reader.ReadInt32();
			for (int i = 0; i < count; i++)
			{
				var unit = new FallenUnit
				{
					Id = reader.ReadInt32(),
					X = reader.ReadInt32(),
					Y = reader.ReadInt32(),
					Look = (byte)(reader.ReadByte() % FallenUnitArt.LookCount),
					Flip = reader.ReadBoolean(),
					Spot = (byte)Math.Min(2, (int)reader.ReadByte()),
					Name = reader.ReadString()
				};
				int by = reader.ReadInt32();
				for (int j = 0; j < by; j++)
					unit.RecoveredBy.Add(reader.ReadString());
				Units.Add(unit);
			}
		}

		// --- Placing them in the world ---

		public override void PostWorldGen()
		{
			Generate();
		}

		public override void PostUpdateWorld()
		{
			// Old worlds (made before this feature) get their units the first time they run.
			if (!generated)
			{
				Generate();
				if (Main.netMode == NetmodeID.Server)
					NetMessage.SendData(MessageID.WorldData);
			}
		}

		public static int TargetCount()
		{
			if (Main.maxTilesX < 5000)
				return 6;
			if (Main.maxTilesX < 7500)
				return 10;
			return 14;
		}

		internal static void Generate()
		{
			Units.Clear();
			generated = true;

			int count = TargetCount();

			// One always waits in the dungeon.
			TryPlaceInDungeon();

			// One may rest inside a living tree, if this world has one with a room big enough.
			TryPlaceInTree();

			// The rest are spread across the world in vertical strips, so they reach the snow, the jungle, the desert and so on.
			int strips = Math.Max(1, count - Units.Count);
			int left = 120;
			int stripWidth = Math.Max(1, (Main.maxTilesX - 2 * left) / strips);
			for (int i = 0; i < strips; i++)
			{
				// Every third strip tries to put its unit on the floor of an underground lake first.				for (int attempt = 0; attempt < 400; attempt++)
				{
					int x = left + i * stripWidth + Rand.Next(stripWidth);
					int top = (int)Main.worldSurface + 20;
					int bottom = Main.UnderworldLayer - 40;
					if (bottom <= top)
						break;
					int y = FindFloor(x, Rand.Next(top, bottom), 60);
					if (y < 0 || !IsGoodSpot(x, y, false))
						continue;
					Add(x, y);
					break;
				}
			}
		}

		private static void TryPlaceInDungeon()
		{
			for (int attempt = 0; attempt < 800; attempt++)
			{
				int x = Main.dungeonX + Rand.Next(-150, 151);
				int y = FindFloor(x, Main.dungeonY + Rand.Next(10, 260), 40);
				if (y < 0 || !WorldGen.InWorld(x, y, 20) || !Main.wallDungeon[Main.tile[x, y].WallType])
					continue;
				if (!IsGoodSpot(x, y, true))
					continue;
				Add(x, y);
				return;
			}
		}

		// The floor of an underground lake: seven solid tiles with four tiles of water above them.
		private static bool TryPlaceUnderwater(int stripLeft, int stripWidth)
		{
			for (int attempt = 0; attempt < 60; attempt++)
			{
				int x = stripLeft + Rand.Next(stripWidth);
				int y = FindWetFloor(x, (int)Main.worldSurface + 20, Main.UnderworldLayer - 40);
				if (y < 0 || !IsGoodSpot(x, y, false, 1))
					continue;
				Add(x, y, -1, 1);
				return true;
			}
			return false;
		}

		// A room inside a living tree (its walls are living wood). Looks for open living-wood tiles all over the world, then
		// for a floor under one of them. Nothing is placed if this world has no tree room big enough.
		private static void TryPlaceInTree()
		{
			var spots = new List<Point>();
			int top = (int)(Main.worldSurface * 0.3);
			int bottom = Math.Min(Main.maxTilesY - 200, (int)Main.rockLayer + 100);
			for (int x = 100; x < Main.maxTilesX - 100; x += 2)
			{
				for (int y = top; y < bottom; y += 2)
				{
					Tile t = Main.tile[x, y];
					if (IsTreeWall(t.WallType) && !t.HasTile)
						spots.Add(new Point(x, y));
				}
			}

			for (int attempt = 0; attempt < 120 && spots.Count > 0; attempt++)
			{
				Point p = spots[Rand.Next(spots.Count)];
				for (int y = p.Y; y < p.Y + 10; y++)
				{
					if (!HasTreeRoomAt(p.X, y))
						continue;
					if (IsGoodSpot(p.X, y, false, 2))
					{
						Add(p.X, y, -1, 2);
						return;
					}
					break;
				}
			}
		}

		internal static FallenUnit Add(int x, int y, int look = -1, int spot = 0)
		{
			var unit = new FallenUnit
			{
				Id = nextId++,
				X = x,
				Y = y,
				Look = (byte)(look >= 0 ? look % FallenUnitArt.LookCount : spot == 2 ? (Rand.NextBool() ? 0 : 2) : Rand.Next(FallenUnitArt.LookCount)),
				Flip = Rand.NextBool(),
				Spot = (byte)spot,
				Name = MakeName()
			};
			Units.Add(unit);
			return unit;
		}

		// Unit numbers in the YoRHa style, but never one of the named units from the games.
		private static readonly HashSet<string> TakenNames = new HashSet<string> { "2B", "9S", "A2", "21O", "6O", "11B", "4S", "3B", "4B", "7E", "1D", "2D", "64B", "12H", "16D" };

		private static string MakeName()
		{
			const string letters = "BSHDEG";
			for (int i = 0; i < 20; i++)
			{
				string name = Rand.NextBool()
					? Rand.Next(1000, 10000).ToString()
					: Rand.Next(3, 100) + letters[Rand.Next(letters.Length)].ToString();
				if (!TakenNames.Contains(name))
					return name;
			}
			return Rand.Next(1000, 10000).ToString();
		}

		// The first tile at or below startY that has open space and a floor under it, or -1.
		internal static int FindFloor(int x, int startY, int depth)
		{
			for (int y = startY; y < startY + depth; y++)
			{
				if (!WorldGen.InWorld(x, y, 12))
					return -1;
				if (HasRoomAt(x, y))
					return y;
			}
			return -1;
		}

		private static bool IsSolid(int x, int y)
		{
			Tile t = Main.tile[x, y];
			return t.HasTile && !t.IsActuated && Main.tileSolid[t.TileType] && !Main.tileSolidTop[t.TileType];
		}

		private static bool IsOpen(int x, int y)
		{
			Tile t = Main.tile[x, y];
			if (t.LiquidAmount > 0)
				return false;
			return !t.HasTile || (!Main.tileSolid[t.TileType] && Main.tileCut[t.TileType]);
		}

		// A flat floor seven tiles wide with four tiles of dry, open space above it.
		internal static bool HasRoomAt(int x, int y)
		{
			for (int dx = -3; dx <= 3; dx++)
			{
				if (!WorldGen.InWorld(x + dx, y - 4, 12) || !IsSolid(x + dx, y + 1))
					return false;
				for (int dy = 0; dy <= 3; dy++)
				{
					if (!IsOpen(x + dx, y - dy))
						return false;
				}
			}
			return true;
		}

		// Water that is deep enough to swim in (and not lava, honey or shimmer), with nothing solid in it.
		private static bool IsWater(int x, int y)
		{
			Tile t = Main.tile[x, y];
			if (t.LiquidAmount < 128 || t.LiquidType != LiquidID.Water)
				return false;
			return !t.HasTile || (!Main.tileSolid[t.TileType] && Main.tileCut[t.TileType]);
		}

		// A flat floor seven tiles wide with four tiles of water above it.
		internal static bool HasWetRoomAt(int x, int y)
		{
			for (int dx = -3; dx <= 3; dx++)
			{
				if (!WorldGen.InWorld(x + dx, y - 4, 12) || !IsSolid(x + dx, y + 1))
					return false;
				for (int dy = 0; dy <= 3; dy++)
				{
					if (!IsWater(x + dx, y - dy))
						return false;
				}
			}
			return true;
		}

		// The first tile from top down to bottom that has a wet room (see HasWetRoomAt), or -1.
		internal static int FindWetFloor(int x, int top, int bottom)
		{
			for (int y = top; y < bottom; y++)
			{
				if (!WorldGen.InWorld(x, y, 12))
					return -1;
				if (HasWetRoomAt(x, y))
					return y;
			}
			return -1;
		}

		// The wall of a living tree's rooms: the placed living wood wall, or the one world generation uses.
		private static bool IsTreeWall(ushort wall) => wall == WallID.LivingWood || wall == WallID.LivingWoodUnsafe;

		// A smaller room than a cave needs (the tree's rooms are tight): five tiles of floor, three of dry air, and living wood behind.
		internal static bool HasTreeRoomAt(int x, int y)
		{
			if (!WorldGen.InWorld(x, y, 12) || !IsTreeWall(Main.tile[x, y].WallType))
				return false;
			for (int dx = -2; dx <= 2; dx++)
			{
				if (!IsSolid(x + dx, y + 1))
					return false;
				for (int dy = 0; dy <= 2; dy++)
				{
					if (!IsOpen(x + dx, y - dy))
						return false;
				}
			}
			return true;
		}

		private static bool IsGoodSpot(int x, int y, bool dungeon, int spot = 0)
		{
			if (!(spot == 1 ? HasWetRoomAt(x, y) : spot == 2 ? HasTreeRoomAt(x, y) : HasRoomAt(x, y)))
				return false;

			ushort wall = Main.tile[x, y].WallType;
			if (wall == WallID.LihzahrdBrickUnsafe)
				return false;
			if (!dungeon && Main.wallDungeon[wall])
				return false;

			return IsFarEnough(x, y);
		}

		// Away from spawn (where an old world's base usually is) and from each other.
		private static bool IsFarEnough(int x, int y)
		{
			if (Math.Abs(x - Main.spawnTileX) < 140 && Math.Abs(y - Main.spawnTileY) < 120)
				return false;
			foreach (FallenUnit other in Units)
			{
				if (Math.Abs(other.X - x) < 90 && Math.Abs(other.Y - y) < 90)
					return false;
			}
			return true;
		}

		// --- Recovering ---

		// Each body offers 1, 2 or 3 plugins (30%, 40%, 30%), and each one rolls its own rarity, so a body can hold a
		// Common next to a Legendary, three of a kind, or a single plugin. The offer is rolled once per character and kept
		// (saved with the world) until a plugin is picked, so closing the panel and opening it again shows the same cards.
		private static List<string> RollOffer(Player player)
		{
			AugmentPlayer ap = player.GetModPlayer<AugmentPlayer>();
			RarityBracket bracket = OfferBracket();

			int roll = Main.rand.Next(100);
			int count = roll < 30 ? 1 : roll < 70 ? 2 : 3;

			var ids = new HashSet<string>();
			var order = new List<string>();
			for (int i = 0; i < count; i++)
			{
				AugmentRarity rarity = BossRarityRoller.Roll(bracket);
				foreach (AugmentRarity tier in AugmentRewardLogic.GetRarityFallbackOrder(rarity))
				{
					List<Augment> pick = ap.RollChoices(1, tier, ids);
					if (pick.Count > 0)
					{
						ids.Add(pick[0].Id);
						order.Add(pick[0].Id);
						break;
					}
				}
			}
			return order;
		}

		// A saved offer loses any plugin the character has gotten some other way since; an empty one is rolled again.
		private static List<string> OfferFor(FallenUnit unit, Player player)
		{
			AugmentPlayer ap = player.GetModPlayer<AugmentPlayer>();
			if (unit.Offers.TryGetValue(player.name, out List<string> saved))
			{
				saved.RemoveAll(id => AugmentDatabase.GetById(id) == null || ap.HasAugment(id) || ap.SoldAugmentIds.Contains(id) || ap.StashedIds.Contains(id));
				if (saved.Count > 0)
					return saved;
			}

			List<string> fresh = RollOffer(player);
			unit.Offers[player.name] = fresh;
			return fresh;
		}

		private static bool CanUse(Player player, FallenUnit unit)
		{
			return player != null && player.active && !player.dead && unit != null && !unit.IsRecoveredBy(player)
				&& Vector2.Distance(player.Center, unit.Center) <= RecoverRange + 200f;
		}

		private static void Tell(Player player, string message)
		{
			if (Main.netMode == NetmodeID.Server)
				Terraria.Chat.ChatHelper.SendChatMessageToClient(Terraria.Localization.NetworkText.FromLiteral(message), new Color(255, 200, 90), player.whoAmI);
			else
				Main.NewText(message, 255, 200, 90);
		}

		// Server (or singleplayer): the player right-clicked a body. Send them its offer, which opens the dusty panel.
		internal static void ServerOpen(Player player, int unitId)
		{
			if (Main.netMode == NetmodeID.MultiplayerClient)
				return;

			FallenUnit unit = Find(unitId);
			if (!CanUse(player, unit))
				return;

			List<string> offer = OfferFor(unit, player);
			if (offer.Count == 0)
			{
				Tell(player, "Nothing in this unit can be recovered.");
				return;
			}

			if (Main.netMode == NetmodeID.Server)
			{
				ModPacket packet = ModContent.GetInstance<Augments>().GetPacket();
				packet.Write((byte)AugmentPacketType.FallenUnitOffer);
				packet.Write(unit.Id);
				packet.Write((byte)offer.Count);
				foreach (string id in offer)
					packet.Write(id);
				packet.Send(player.whoAmI);
			}
			else
			{
				FallenUnitUISystem.Open(unit, offer);
			}
		}

		// Server (or singleplayer): the player picked one plugin from the panel.
		internal static void ServerPick(Player player, int unitId, string augmentId)
		{
			if (Main.netMode == NetmodeID.MultiplayerClient)
				return;

			FallenUnit unit = Find(unitId);
			if (!CanUse(player, unit))
				return;
			if (!unit.Offers.TryGetValue(player.name, out List<string> offer) || !offer.Contains(augmentId))
				return;

			Augment augment = AugmentDatabase.GetById(augmentId);
			AugmentPlayer ap = player.GetModPlayer<AugmentPlayer>();
			if (augment == null || ap.HasAugment(augmentId))
				return;

			bool server = Main.netMode == NetmodeID.Server;
			if (!ap.ApplyRewardAugment(augment, !server))
				return;
			if (server)
				AugmentNet.SendSyncPlayer(player);

			unit.Offers.Remove(player.name);
			unit.RecoveredBy.Add(player.name);

			if (server)
			{
				ModPacket packet = ModContent.GetInstance<Augments>().GetPacket();
				packet.Write((byte)AugmentPacketType.FallenUnitRecovered);
				packet.Write(unit.Id);
				packet.Write(player.name);
				packet.Send();
			}
		}

		// Client side: right-click on a body.
		internal static void RequestOpen(Player player, FallenUnit unit)
		{
			if (Main.netMode == NetmodeID.SinglePlayer)
			{
				ServerOpen(player, unit.Id);
				return;
			}

			ModPacket packet = ModContent.GetInstance<Augments>().GetPacket();
			packet.Write((byte)AugmentPacketType.FallenUnitRecover);
			packet.Write(unit.Id);
			packet.Send();
		}

		// Client side: a plugin was picked in the panel.
		internal static void RequestPick(Player player, FallenUnit unit, string augmentId)
		{
			if (Main.netMode == NetmodeID.SinglePlayer)
			{
				ServerPick(player, unit.Id, augmentId);
				return;
			}

			ModPacket packet = ModContent.GetInstance<Augments>().GetPacket();
			packet.Write((byte)AugmentPacketType.FallenUnitPick);
			packet.Write(unit.Id);
			packet.Write(augmentId);
			packet.Send();
		}

		internal static void HandleOpenRequest(int whoAmI, int unitId)
		{
			if (Main.netMode == NetmodeID.Server && whoAmI >= 0 && whoAmI < Main.maxPlayers)
				ServerOpen(Main.player[whoAmI], unitId);
		}

		internal static void HandlePickRequest(int whoAmI, int unitId, string augmentId)
		{
			if (Main.netMode == NetmodeID.Server && whoAmI >= 0 && whoAmI < Main.maxPlayers)
				ServerPick(Main.player[whoAmI], unitId, augmentId);
		}

		internal static void HandleOffer(int unitId, List<string> ids)
		{
			FallenUnit unit = Find(unitId);
			if (unit != null && Main.netMode == NetmodeID.MultiplayerClient)
				FallenUnitUISystem.Open(unit, ids);
		}

		internal static void HandleRecovered(int unitId, string playerName)
		{
			FallenUnit unit = Find(unitId);
			if (unit == null)
				return;
			unit.RecoveredBy.Add(playerName);
			if (!Main.dedServ && Main.LocalPlayer != null && Main.LocalPlayer.name == playerName)
				FallenUnitPlayer.Local?.ForgetUnit(unit);
		}

		// --- Drawing ---

		public override void PostDrawTiles()
		{
			if (Main.dedServ || Units.Count == 0)
				return;

			Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
			try
			{
				FallenUnitArt.DrawAll();
			}
			finally
			{
				Main.spriteBatch.End();
			}
		}

		public override void Unload()
		{
			FallenUnitArt.Unload();
		}
	}

	// The local player's side: the Pod's beep while one is near, and the right-click that recovers it.
	public sealed class FallenUnitPlayer : ModPlayer
	{
		private int beepTimer;

		// The Pod's beep: a short soft retro blip, like an old handheld game, made for this mod.
		private static readonly SoundStyle PodBeep = new SoundStyle("Augments/Sounds/PodBeep") { MaxInstances = 2 };
		private int requestCooldown;

		// The unit the mouse is over and close enough to recover this tick, or null.
		internal static FallenUnit Hovered;

		// Your own Pod's antenna flashes red for this many ticks with each beep (drawn in PodPet).
		internal const int PodPingLength = 14;
		internal static int PodPingTicks;

		// How much dust you have wiped off each card, so a card does not get dirty again when you come back.
		// Key: world, unit and plugin (see DustKey). Value: one byte per dust cell, 0 clean to 255 untouched.
		// Saved with the character; a unit's entries are dropped once you pick a plugin from it.
		private readonly Dictionary<string, byte[]> dustMemory = new Dictionary<string, byte[]>();

		internal static FallenUnitPlayer Local => Main.gameMenu || Main.LocalPlayer == null ? null : Main.LocalPlayer.GetModPlayer<FallenUnitPlayer>();

		private static string WorldKey => Main.ActiveWorldFileData?.UniqueId.ToString() ?? Main.worldID.ToString();

		internal static string DustKey(FallenUnit unit, string augmentId) => WorldKey + ":" + unit.Id + ":" + augmentId;

		internal byte[] RecallDust(string key) => dustMemory.TryGetValue(key, out byte[] bytes) ? bytes : null;

		internal void StoreDust(string key, byte[] bytes) => dustMemory[key] = bytes;

		internal void ForgetUnit(FallenUnit unit)
		{
			string prefix = WorldKey + ":" + unit.Id + ":";
			var old = new List<string>();
			foreach (string key in dustMemory.Keys)
				if (key.StartsWith(prefix, StringComparison.Ordinal))
					old.Add(key);
			foreach (string key in old)
				dustMemory.Remove(key);
		}

		public override void SaveData(TagCompound tag)
		{
			if (dustMemory.Count == 0)
				return;
			tag["fallenDustKeys"] = new List<string>(dustMemory.Keys);
			tag["fallenDustData"] = new List<byte[]>(dustMemory.Values);
		}

		public override void LoadData(TagCompound tag)
		{
			dustMemory.Clear();
			IList<string> keys = tag.GetList<string>("fallenDustKeys");
			IList<byte[]> data = tag.GetList<byte[]>("fallenDustData");
			for (int i = 0; i < Math.Min(keys.Count, data.Count); i++)
				dustMemory[keys[i]] = data[i];
		}

		public override void SetControls()
		{
			if (Player.whoAmI != Main.myPlayer || Main.dedServ)
				return;

			Hovered = null;
			if (requestCooldown > 0)
				requestCooldown--;

			if (Player.dead || Player.mouseInterface || Main.gameMenu)
				return;

			Vector2 mouse = Main.MouseWorld;
			foreach (FallenUnit unit in FallenUnitSystem.Units)
			{
				if (unit.IsRecoveredBy(Player))
					continue;
				if (!FallenUnitArt.Bounds(unit).Contains(mouse.ToPoint()))
					continue;
				if (Vector2.Distance(Player.Center, unit.Center) > FallenUnitSystem.RecoverRange)
					continue;
				Hovered = unit;
				break;
			}

			if (Hovered == null || !Player.controlUseTile || !Player.releaseUseTile)
				return;

			// The right-click is ours: do not let it also use an item or open something behind the body.
			Player.controlUseTile = false;
			Player.releaseUseTile = false;
			Player.tileInteractionHappened = true;
			Main.mouseRightRelease = false;

			if (requestCooldown > 0)
				return;
			requestCooldown = 30;

			if (FallenUnitUISystem.IsOpen)
				return;
			if (ModContent.GetInstance<AugmentUISystem>().IsOpen)
			{
				Main.NewText("Finish your current plugin choice first.", 255, 200, 90);
				return;
			}

			FallenUnitSystem.RequestOpen(Player, Hovered);
		}

		public override void PostUpdate()
		{
			if (Player.whoAmI != Main.myPlayer || Main.dedServ)
				return;
			if (PodPingTicks > 0)
				PodPingTicks--;
			if (Player.dead)
				return;

			// Standing near a unit you have recovered saves its memory log, and may finish a reward (see FallenUnitLogs).
			FallenUnitLogs.Update(Player);

			// The Pod beeps when an unrecovered unit is within 50 tiles, faster and a little higher the closer it is,
			// right up to the body. It goes quiet while the unit's panel is open.
			FallenUnit nearest = FallenUnitSystem.Nearest(Player.Center, Player);
			if (nearest == null || FallenUnitUISystem.IsOpen)
				return;

			float tiles = Vector2.Distance(Player.Center, nearest.Center) / 16f;
			if (tiles > 50f)
			{
				beepTimer = Math.Min(beepTimer, 20);
				return;
			}

			if (--beepTimer > 0)
				return;

			float closeness = MathHelper.Clamp(1f - (tiles - 3f) / 47f, 0f, 1f);
			beepTimer = (int)MathHelper.Lerp(120f, 22f, closeness);
			SoundEngine.PlaySound(PodBeep with { Volume = MathHelper.Lerp(0.35f, 0.6f, closeness), Pitch = 0.25f * closeness });
			PodPingTicks = PodPingLength;
		}
	}

	// The three looks, drawn as pixel art built in code (one art pixel is two screen pixels, like the game's own sprites),
	// plus the dead Pod (the mod's own Pod sprite in grey, lens cracked), a soft shaft of light with falling petals, and
	// the Pod's message box. Small animations: the blindfold tied to the sword flutters, the overgrown unit's broken arm
	// sparks now and then, and the core and the Pod's light pulse until it is recovered. Nothing glows in the dark: everything
	// is lit by the cave's own light, except the Pod's tiny red light.
	internal static class FallenUnitArt
	{
		public const int LookCount = 3;
		private const int Px = 2;

		private sealed class Scene
		{
			public int W, H;
			public char[][,] Frames;      // one grid per animation frame (only the sword grave has two)
			public Texture2D[] Tex;
			public Vector2 PodAt;         // in art pixels from the top left
			public float PodRot;
			public Point? Spark;          // where the broken arm sparks, if it does
			public readonly List<Point> Core = new List<Point>();
			public readonly List<Point> Flowers = new List<Point>();
			public readonly List<Point> Mushrooms = new List<Point>();
		}

		// --- The parts the scenes are built from. Letters are colours (see ColorOf); spaces are see-through. ---

		private static readonly string[] Sitting =
		{
			"     ....",
			"    .hhhh..",
			"   .hiihhhh.",
			"   .hihhhhhH.",
			"  ..hhhhhkkH.",
			"bb.bHhbbbbbbb.",
			" .b.HhhkkkkK.",
			"    .HHhkkK.",
			"    .eddddd..",
			"   .eDdwwdDdd.",
			"   .dDdwcdDdDd.",
			"   .dDzdwdDdGg.",
			"  .ddwwwwwddgg.",
			"  .dDDDDDDDdd.......",
			" .wWwWwWwWwddddddd.o.",
			"  ....dDDDDDDDDDDd.Oo.",
			"      .ddddddddddd.oO.",
			"       ............oo."
		};

		private static readonly string[] Lying =
		{
			"      ..",
			"     .kK.",
			"   ..bbbb.........",
			"  .hhbbbbkKdDdwwdD........",
			" .hihhhhkkKdDDwcwDDddddddd.o.",
			".hhhhhhhhhHdDDdwwDzdddddddd.Oo.",
			".HHhhhhhhH.wWwWwWwWddddddddd.oo.",
			" .......... ........ddddddd..oo.",
			"                    .......  ..."
		};

		private static readonly string[] Sword =
		{
			"  .MM.", "  .mM.", "  .Mm.", "  .mM.", " .rmmr.", ".MMMMMM.", " ..sS..", "  .xS.", "  .sS.",
			"  .sS.", "  .xS.", "  .sS.", "  .sS.", "  .sS.", "  .xS.", "  .sS.", "  .sS."
		};

		private static readonly string[] SwordFlat =
		{
			".MmMmM.r.",
			".mMmMmMM.xxxxxxxxxxxxxx.",
			".MmMmM.M.SSSSSSSSSSSSSSS.",
			"        ................"
		};

		private static readonly string[] ClothA = { "bbbb", "bBbbbb", " bbbbbbb", "    bBbbbb", "       bbb" };
		private static readonly string[] ClothB = { "bbbb", "bBbbbb", "  bbbbbb", "   bbBbb", "      bbbbb", "         bb" };
		private static readonly string[] Flower = { " f ", "fyf", " l ", "Ll " };
		private static readonly string[] Flower2 = { "f  ", "yf f", " fyf", " l l", "Ll L" };
		private static readonly string[] Rocks = { "  qQ ", "qQqqQq" };
		private static readonly string[] Mushroom = { " u ", "uuu", " U " };

		private static readonly string[] Vines =
		{
			"", "     V", "    vV", "   v n", "", "", "", "", " V", "vVv", "  v  V", "   vvV", " V    v", "vnv    v",
			"   vV   vV", " vv  vvn  vV", "vVvVvvVvvVvvvVv"
		};

		private static Color ColorOf(char c)
		{
			switch (c)
			{
				case '.': return new Color(20, 18, 16);
				case 'h': return new Color(232, 228, 218);
				case 'H': return new Color(175, 169, 155);
				case 'i': return new Color(255, 255, 255);
				case 'k': return new Color(238, 211, 192);
				case 'K': return new Color(201, 168, 147);
				case 'b': return new Color(18, 18, 20);
				case 'B': return new Color(58, 58, 68);
				case 'd': return new Color(36, 36, 42);
				case 'D': return new Color(58, 58, 68);
				case 'e': return new Color(85, 85, 106);
				case 'w': return new Color(218, 214, 202);
				case 'W': return new Color(158, 154, 142);
				case 'g': return new Color(26, 26, 30);
				case 'G': return new Color(64, 64, 74);
				case 'o': return new Color(22, 22, 26);
				case 'O': return new Color(60, 60, 72);
				case 's': return new Color(210, 214, 222);
				case 'S': return new Color(134, 138, 148);
				case 'x': return new Color(255, 255, 255);
				case 'm': return new Color(58, 56, 52);
				case 'M': return new Color(108, 104, 96);
				case 'r': return new Color(150, 44, 36);
				case 'f': return new Color(244, 244, 236);
				case 'y': return new Color(230, 202, 102);
				case 'l': return new Color(74, 116, 40);
				case 'L': return new Color(118, 164, 60);
				case 'v': return new Color(54, 96, 31);
				case 'V': return new Color(94, 144, 48);
				case 'n': return new Color(143, 190, 74);
				case 'c': return new Color(60, 78, 96); // the core when it is dark; it glows on top while the unit is unrecovered
				case 'q': return new Color(86, 82, 74);
				case 'Q': return new Color(122, 117, 106);
				case 'z': return new Color(10, 10, 12);
				case 'u': return new Color(127, 200, 240);
				case 'U': return new Color(62, 110, 138);
				default: return Color.Transparent;
			}
		}

		private static Scene[] scenes;

		private static Scene[] Scenes => scenes ??= BuildScenes();

		private static char[,] NewGrid(int w, int h)
		{
			var grid = new char[w, h];
			for (int x = 0; x < w; x++)
				for (int y = 0; y < h; y++)
					grid[x, y] = ' ';
			return grid;
		}

		private static void Stamp(char[,] grid, string[] part, int ox, int oy)
		{
			int w = grid.GetLength(0), h = grid.GetLength(1);
			for (int y = 0; y < part.Length; y++)
			{
				for (int x = 0; x < part[y].Length; x++)
				{
					char c = part[y][x];
					int gx = ox + x, gy = oy + y;
					if (c != ' ' && gx >= 0 && gx < w && gy >= 0 && gy < h)
						grid[gx, gy] = c;
				}
			}
		}

		private static Scene BuildGrave(int frames)
		{
			var scene = new Scene { W = 34, H = 19, Frames = new char[frames][,], PodAt = new Vector2(29f, 16f), PodRot = 0.25f };
			for (int f = 0; f < frames; f++)
			{
				char[,] g = NewGrid(scene.W, scene.H);
				Stamp(g, Sword, 11, 2);
				Stamp(g, f == 0 ? ClothA : ClothB, 14, 8);
				Stamp(g, Rocks, 10, 17);
				Stamp(g, Flower, 3, 15);
				Stamp(g, Flower2, 6, 14);
				Stamp(g, Flower, 17, 15);
				Stamp(g, Flower2, 19, 14);
				scene.Frames[f] = g;
			}
			return scene;
		}

		private static Scene[] BuildScenes()
		{
			// 1. Sword grave: only the sword with the blindfold tied on it (fluttering), stones and flowers.
			Scene grave = BuildGrave(2);

			// 2. Lying in flowers, the sword beside her.
			var lying = new Scene { W = 42, H = 11, Frames = new char[1][,], PodAt = new Vector2(38f, 8f), PodRot = -0.25f };
			char[,] lg = NewGrid(lying.W, lying.H);
			Stamp(lg, SwordFlat, 9, 7);
			Stamp(lg, Lying, 2, 2);
			Stamp(lg, Flower, 0, 7);
			Stamp(lg, Flower2, 9, 6);
			Stamp(lg, Flower, 34, 7);
			Stamp(lg, Flower2, 31, 6);
			lying.Frames[0] = lg;

			// 3. Overgrown: sitting against the wall under vines, a flower in the hair, glowing mushrooms, a sparking arm.
			var overgrown = new Scene { W = 35, H = 18, Frames = new char[1][,], PodAt = new Vector2(31f, 16f), PodRot = 0.6f, Spark = new Point(14, 11) };
			char[,] og = NewGrid(overgrown.W, overgrown.H);
			Stamp(og, Sitting, 1, 0);
			Stamp(og, Vines, 1, 1);
			Stamp(og, Flower, 5, 0);
			Stamp(og, Mushroom, 23, 15);
			Stamp(og, Mushroom, 26, 15);
			overgrown.Frames[0] = og;

			var all = new[] { grave, lying, overgrown };
			foreach (Scene scene in all)
			{
				scene.Tex = new Texture2D[scene.Frames.Length];
				char[,] g = scene.Frames[0];
				for (int x = 0; x < scene.W; x++)
				{
					for (int y = 0; y < scene.H; y++)
					{
						switch (g[x, y])
						{
							case 'c': scene.Core.Add(new Point(x, y)); break;
							case 'f': scene.Flowers.Add(new Point(x, y)); break;
							case 'u': scene.Mushrooms.Add(new Point(x, y)); break;
						}
					}
				}
			}
			return all;
		}

		private static Texture2D TextureOf(Scene scene, int frame)
		{
			frame %= scene.Frames.Length;
			Texture2D tex = scene.Tex[frame];
			if (tex != null && !tex.IsDisposed)
				return tex;
			if (Main.graphics?.GraphicsDevice == null)
				return null;

			char[,] g = scene.Frames[frame];
			var data = new Color[scene.W * scene.H];
			for (int y = 0; y < scene.H; y++)
				for (int x = 0; x < scene.W; x++)
					data[y * scene.W + x] = ColorOf(g[x, y]);
			tex = new Texture2D(Main.graphics.GraphicsDevice, scene.W, scene.H);
			tex.SetData(data);
			scene.Tex[frame] = tex;
			return tex;
		}

		// The dead Pod: the mod's Pod sprite, all in dull greys with a dark, cracked lens.
		private static Texture2D deadPod;

		private static Texture2D DeadPod()
		{
			if (deadPod != null && !deadPod.IsDisposed)
				return deadPod;
			if (Main.graphics?.GraphicsDevice == null)
				return null;

			var data = new Color[PodSprites.Width * PodSprites.Height];
			for (int y = 0; y < PodSprites.Height; y++)
			{
				string row = PodSprites.Mask[y];
				for (int x = 0; x < PodSprites.Width; x++)
				{
					Color c;
					switch (row[x])
					{
						case '.': c = new Color(18, 18, 18); break;
						case 'H': c = new Color(120, 118, 112); break;
						case 'B': case 'L': c = new Color(90, 88, 84); break;
						case 'M': c = new Color(76, 74, 70); break;
						case 'S': case 'l': case 'G': c = new Color(58, 56, 54); break;
						case 'D': case 'T': case 'A': c = new Color(44, 42, 40); break;
						case 'E': case 'e': case 'W': c = new Color(28, 28, 30); break;
						case 'R': c = new Color(60, 32, 30); break;
						default: c = Color.Transparent; break;
					}
					data[y * PodSprites.Width + x] = c;
				}
			}

			// A crack across the lens.
			data[9 * PodSprites.Width + 10] = new Color(138, 138, 138);
			data[11 * PodSprites.Width + 9] = new Color(138, 138, 138);

			deadPod = new Texture2D(Main.graphics.GraphicsDevice, PodSprites.Width, PodSprites.Height);
			deadPod.SetData(data);
			return deadPod;
		}

		internal static void Unload()
		{
			var old = new List<Texture2D> { deadPod };
			if (scenes != null)
				foreach (Scene scene in scenes)
					old.AddRange(scene.Tex);
			deadPod = null;
			scenes = null;
			Main.QueueMainThreadAction(() =>
			{
				foreach (Texture2D tex in old)
					tex?.Dispose();
			});
		}

		// Where the scene's top left corner is in the world. Its bottom edge sits on the floor, centred on the unit.
		private static Vector2 TopLeft(FallenUnit unit, Scene scene)
		{
			float floorY = (unit.Y + 1) * 16f;
			return new Vector2(unit.X * 16 + 8 - scene.W * Px / 2f, floorY - scene.H * Px);
		}

		// An art position in the world, taking the facing into account.
		private static Vector2 ArtToWorld(FallenUnit unit, Scene scene, Vector2 art)
		{
			float x = unit.Flip ? scene.W - art.X : art.X;
			return TopLeft(unit, scene) + new Vector2(x * Px, art.Y * Px);
		}

		// The top left corner of one art pixel in the world (mirrored correctly when the scene faces left).
		private static Vector2 CellToWorld(FallenUnit unit, Scene scene, Point p) =>
			ArtToWorld(unit, scene, new Vector2(p.X + (unit.Flip ? 1f : 0f), p.Y));

		// The area the mouse must be over to right-click the unit, in world pixels.
		internal static Rectangle Bounds(FallenUnit unit)
		{
			Scene scene = Scenes[unit.Look % LookCount];
			Vector2 tl = TopLeft(unit, scene);
			return new Rectangle((int)tl.X - 8, (int)tl.Y - 8, scene.W * Px + 16, scene.H * Px + 16);
		}

		internal static void DrawAll()
		{
			Player me = Main.LocalPlayer;
			var view = new Rectangle((int)Main.screenPosition.X - 200, (int)Main.screenPosition.Y - 200, Main.screenWidth + 400, Main.screenHeight + 400);

			FallenUnit boxUnit = null;
			float boxDist = FallenUnitSystem.RecoverRange;

			foreach (FallenUnit unit in FallenUnitSystem.Units)
			{
				if (!view.Contains(unit.Center.ToPoint()))
					continue;

				DrawUnit(unit, unit.IsRecoveredBy(me));

				float d = Vector2.Distance(me.Center, unit.Center);
				if (!me.dead && d <= boxDist)
				{
					boxDist = d;
					boxUnit = unit;
				}
			}

			if (boxUnit != null)
			{
				if (boxUnit.Id != focusId)
				{
					focusId = boxUnit.Id;
					focusTicks = 0;
				}
				focusTicks++;
				DrawMessage(boxUnit, boxUnit.IsRecoveredBy(me), FallenUnitPlayer.Hovered == boxUnit);
			}
			else
			{
				focusId = -1;
			}
		}

		private static int focusId = -1;
		private static int focusTicks;

		// Additive colours (alpha 0) brighten what is underneath, like light.
		private static Color Glow(int r, int g, int b) => new Color(r, g, b, 0);

		private static void DrawUnit(FallenUnit unit, bool recovered)
		{
			Scene scene = Scenes[unit.Look % LookCount];
			float time = (float)Main.timeForVisualEffects;
			Texture2D tex = TextureOf(scene, (int)(time / 16f));
			if (tex == null)
				return;

			// Lit exactly like the cave around it: in the dark it is dark, so you need a light to see it. Nothing here glows on
			// its own: the light shaft, the petals and the sparks all fade with the cave's light too.
			Color light = Lighting.GetColor(unit.X, unit.Y - 1);
			if (unit.Spot == 1)
			{
				// Under water: a light blue tint (the game draws real water over it too), and bubbles rise from the body instead of petals falling.
				light = light.MultiplyRGB(new Color(215, 232, 255));
				DrawBubbles(unit, scene, recovered, time, light);
			}
			else
			{
				DrawLightShaft(unit, scene, recovered, time, light);
			}

			Vector2 tl = TopLeft(unit, scene);
			SpriteEffects flip = unit.Flip ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
			Main.spriteBatch.Draw(tex, tl - Main.screenPosition, null, light, 0f, Vector2.Zero, Px, flip, 0f);

			float pulse = 0.55f + 0.45f * (float)Math.Sin(time * 0.05f + unit.Id);

			// The broken arm throws a little spark now and then.
			if (scene.Spark is Point spark)
			{
				int k = (int)(time + unit.Id * 37) % 90;
				if (k < 8)
				{
					for (int i = 0; i < 3; i++)
					{
						var art = new Vector2(spark.X + 1 + k * 0.4f + i, spark.Y - k * 0.3f - i * 0.6f);
						Vector2 at = ArtToWorld(unit, scene, art);
						Fill(at, 2, 2, (i == 0 ? new Color(255, 244, 200) : new Color(255, 210, 122)).MultiplyRGB(light));
					}
				}
			}

			// The dead Pod beside it.
			Texture2D pod = DeadPod();
			Vector2 podPos = ArtToWorld(unit, scene, scene.PodAt);
			float podRot = unit.Flip ? -scene.PodRot : scene.PodRot;
			if (pod != null)
				Main.spriteBatch.Draw(pod, podPos - Main.screenPosition, null, light, podRot, PodSprites.Origin, 1f, flip, 0f);

			if (recovered)
				return; // its lights are out: nothing left to take

			// The core shows a faint blue, pulsing slowly, but only as bright as the light around it.
			foreach (Point p in scene.Core)
				Fill(CellToWorld(unit, scene, p), Px, Px, new Color(150, 210, 255).MultiplyRGB(light) * (0.6f + 0.4f * pulse));

			// The Pod's antenna light blinks red now and then.
			if ((int)(time / 20f + unit.Id * 7) % 6 == 0)
			{
				// The antenna is straight above the Pod's centre, so flipping the sprite does not move it; only the tilt does.
				Vector2 antenna = podPos + (PodSprites.Antenna - PodSprites.Origin).RotatedBy(podRot);
				Fill(antenna - new Vector2(1f, 1f), 2, 2, new Color(240, 60, 45));
			}
		}

		// A soft beam of light falling on the body from above, brighter towards the floor, with petals drifting
		// down through it. Drawn as thin horizontal bands so it needs no texture. Dimmer once you recovered it, and only as
		// bright as the cave's own light.
		private static void DrawLightShaft(FallenUnit unit, Scene scene, bool recovered, float time, Color light)
		{
			// The beam is only as bright as the cave: in darkness it is gone.
			float lit = (light.R + light.G + light.B) / (3f * 255f);
			if (lit < 0.02f)
				return;
			const int Bands = 28;
			const float Rise = 22f;                     // how far above the scene the beam starts, in art pixels
			float strength = recovered ? 0.05f : 0.09f;

			float floorY = (unit.Y + 1) * 16f;
			float topY = floorY - (scene.H + Rise) * Px;
			float centerX = unit.X * 16 + 8 + (unit.Flip ? 1f : -1f) * scene.W * Px * 0.05f;
			float topHalf = 4f * Px;
			float bottomHalf = scene.W * Px * 0.5f;
			float bandH = (floorY - topY) / Bands;

			for (int i = 0; i < Bands; i++)
			{
				float t = (i + 0.5f) / Bands;
				float half = MathHelper.Lerp(topHalf, bottomHalf, t);
				float y = topY + i * bandH;
				var rect = new Rectangle((int)(centerX - half), (int)y, (int)(half * 2f), (int)Math.Ceiling(bandH));
				Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(rect.X - (int)Main.screenPosition.X, rect.Y - (int)Main.screenPosition.Y, rect.Width, rect.Height),
					new Rectangle(0, 0, 1, 1), Glow(255, 244, 210) * (strength * t * lit));
			}

			// Pink and white petals drifting down through the light, swaying and turning as they fall. Worked out from the
			// time alone, so nothing has to be stored.
			for (int i = 0; i < 7; i++)
			{
				float seedA = Frac((unit.Id * 0.6180339f) + i * 0.3819660f);
				float seedB = Frac((unit.Id * 0.7548776f) + i * 0.5698403f);
				float speed = 0.4f + 0.6f * seedB;
				float down = Frac(seedA + time * 0.0016f * speed);
				float sway = (float)Math.Sin(time * 0.04f + seedA * 6.283f);
				float y = topY + down * (floorY - topY);
				float half = MathHelper.Lerp(topHalf, bottomHalf, down) * 0.75f;
				float x = centerX - half + seedB * half * 2f + sway * 8f;

				// Fade in at the top and out just before the floor.
				float alpha = Math.Min(1f, down * 6f) * Math.Min(1f, (1f - down) * 8f) * (recovered ? 0.6f : 0.9f);
				Color petal = i % 3 == 0 ? new Color(244, 244, 236) : new Color(248, 178, 200);
				bool flat = sway > 0f; // a petal turning over shows as wide, then tall
				Fill(new Vector2((int)x, (int)y), flat ? 4 : 2, flat ? 2 : 4, petal.MultiplyRGB(light) * alpha);
			}
		}
		// Small pale bubbles rising from the body, wobbling a little, fading in at the bottom and out at the top. Worked out from the
		// time alone, so nothing has to be stored.
		private static void DrawBubbles(FallenUnit unit, Scene scene, bool recovered, float time, Color light)
		{
			float lit = (light.R + light.G + light.B) / (3f * 255f);
			if (lit < 0.02f)
				return;

			float floorY = (unit.Y + 1) * 16f;
			float startY = floorY - scene.H * Px * 0.6f;
			const float Rise = 80f;
			for (int i = 0; i < 6; i++)
			{
				float seedA = Frac((unit.Id * 0.6180339f) + i * 0.3819660f);
				float seedB = Frac((unit.Id * 0.7548776f) + i * 0.5698403f);
				float up = Frac(seedA + time * 0.0014f * (0.6f + 0.6f * seedB));
				float x = unit.X * 16 + 8 + (seedB - 0.5f) * scene.W * Px * 0.8f + (float)Math.Sin(time * 0.05f + seedA * 6.283f) * 3f;
				float y = startY - up * Rise;
				float alpha = Math.Min(1f, up * 6f) * Math.Min(1f, (1f - up) * 5f) * (recovered ? 0.5f : 0.8f);
				int size = i % 3 == 0 ? 3 : 2;
				Fill(new Vector2((int)x, (int)y), size, size, new Color(200, 235, 255).MultiplyRGB(light) * alpha);
			}
		}

		private static float Frac(float v) => v - (float)Math.Floor(v);

		private static void Fill(Vector2 worldTopLeft, int w, int h, Color color)
		{
			Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, worldTopLeft - Main.screenPosition, new Rectangle(0, 0, 1, 1), color, 0f, Vector2.Zero, new Vector2(w, h), SpriteEffects.None, 0f);
		}

		// The Pod's message above the body, typed out like the Last Gasp box.
		private static void DrawMessage(FallenUnit unit, bool recovered, bool hovered)
		{
			Scene scene = Scenes[unit.Look % LookCount];
			Vector2 tl = TopLeft(unit, scene);

			const float boxW = 250f;
			string[] log = recovered ? FallenUnitLogs.LinesOf(unit) : null; // a recovered unit's memory log, read from the Pod
			float boxH = recovered ? 42f + 16f * (log.Length + 1) : 74f;
			var center = new Vector2(unit.X * 16 + 8, tl.Y - 26f - boxH / 2f);
			float fade = Math.Min(1f, focusTicks / 10f);

			Color beige = DangerCloseVisuals.Beige;
			Color frame = recovered ? beige * 0.45f : (hovered ? DangerCloseVisuals.Amber : beige);
			Vector2 topLeft = center + new Vector2(-boxW / 2f, -boxH / 2f);
			Vector2 topRight = center + new Vector2(boxW / 2f, -boxH / 2f);
			Vector2 bottomLeft = center + new Vector2(-boxW / 2f, boxH / 2f);
			Vector2 bottomRight = center + new Vector2(boxW / 2f, boxH / 2f);

			DangerCloseVisuals.Line(center + new Vector2(-boxW / 2f, 0f), center + new Vector2(boxW / 2f, 0f), boxH, new Color(20, 19, 16) * (0.88f * fade));
			DangerCloseVisuals.Line(topLeft, topRight, 2f, frame * fade);
			DangerCloseVisuals.Line(bottomLeft, bottomRight, 2f, frame * fade);
			DangerCloseVisuals.Line(topLeft, bottomLeft, 2f, frame * fade);
			DangerCloseVisuals.Line(topRight, bottomRight, 2f, frame * fade);
			DangerCloseVisuals.Polygon(center + new Vector2(0f, boxH / 2f + 6f), 7f, 3, MathHelper.PiOver2, 2f, frame * fade);

			Vector2 left = center + new Vector2(-boxW / 2f + 10f, -boxH / 2f + 12f);
			Color dim = new Color(185, 178, 154) * fade;
			Color text = new Color(232, 226, 204) * fade;
			Color amber = new Color(255, 190, 90) * fade;

			int shown = focusTicks * 2;
			string line1 = "Unit " + unit.Name + " has stopped.";
			Write("POD 042", left, dim);
			Write(Typed(line1, shown), left + new Vector2(0f, 16f), recovered ? dim : text);
			if (recovered)
			{
				// The header, then the log itself, one line after the other.
				string header = "LOG " + (FallenUnitLogs.IndexOf(unit) + 1) + " OF " + FallenUnitLogs.Logs.Length;
				int typed = shown - line1.Length;
				Write(Typed(header, typed), left + new Vector2(0f, 32f), amber);
				typed -= header.Length;
				for (int i = 0; i < log.Length; i++)
				{
					Write(Typed(log[i], typed), left + new Vector2(0f, 48f + 16f * i), text);
					typed -= log[i].Length;
				}
				return;
			}

			string line2 = "Its plugin can still be recovered.";
			Write(Typed(line2, shown - line1.Length), left + new Vector2(0f, 32f), text);
			if (shown > line1.Length + line2.Length)
				Write(hovered ? "Right-click to recover." : "Point at it and right-click to recover.", left + new Vector2(0f, 50f), hovered ? amber : dim);
		}

		private static string Typed(string line, int shown)
		{
			shown = Math.Clamp(shown, 0, line.Length);
			string visible = line.Substring(0, shown);
			return shown < line.Length && shown > 0 && (focusTicks / 4) % 2 == 0 ? visible + "_" : visible;
		}

		private static void Write(string line, Vector2 worldPos, Color color)
		{
			if (line.Length > 0)
				ModText.DrawBorder(Main.spriteBatch, line, worldPos - Main.screenPosition, color, 0.72f, 0f, 0.5f);
		}
	}
}
