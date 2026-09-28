using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace Augments
{
	internal static class SupportEffects
	{
		public const float AuraRadius = 600f;

		public static bool AreAllies(Player owner, Player target)
		{
			if (owner == null || target == null || !owner.active || !target.active)
				return false;
			return owner.team == 0 || target.team == 0 || owner.team == target.team;
		}

		/// <summary>
		/// Checks line-of-sight for the MediGun beam. Only fully solid blocks obstruct the beam.
		/// Ropes, platforms, and non-solid blocks do NOT interrupt the beam.
		/// </summary>
		public static bool CanBeamPassLine(Vector2 start, Vector2 end)
		{
			int x0 = (int)(start.X / 16f);
			int y0 = (int)(start.Y / 16f);
			int x1 = (int)(end.X / 16f);
			int y1 = (int)(end.Y / 16f);

			int dx = Math.Abs(x1 - x0);
			int dy = Math.Abs(y1 - y0);
			int sx = x0 < x1 ? 1 : -1;
			int sy = y0 < y1 ? 1 : -1;
			int err = dx - dy;

			int curX = x0;
			int curY = y0;

			while (true)
			{
				// Ignore start and end tile so entity bounding box clipping at endpoints never blocks the beam
				if ((curX != x0 || curY != y0) && (curX != x1 || curY != y1))
				{
					if (curX >= 0 && curX < Main.maxTilesX && curY >= 0 && curY < Main.maxTilesY)
					{
						Tile tile = Main.tile[curX, curY];
						if (tile.HasUnactuatedTile)
						{
							ushort type = tile.TileType;
							bool isPlatform = Main.tileSolidTop[type] || TileID.Sets.Platforms[type];
							bool isRope = Main.tileRope[type];
							bool isSolid = Main.tileSolid[type];

							// Only true solid blocks (not platforms and not ropes) interrupt the beam
							if (isSolid && !isPlatform && !isRope)
							{
								return false;
							}
						}
					}
				}

				if (curX == x1 && curY == y1)
					break;

				int e2 = 2 * err;
				if (e2 > -dy)
				{
					err -= dy;
					curX += sx;
				}
				if (e2 < dx)
				{
					err += dx;
					curY += sy;
				}
			}

			return true;
		}

		public static bool IsAllyInRange(Player owner, Player target, float radius, bool includeOwner = false)
		{
			if (!AreAllies(owner, target) || target.dead)
				return false;
			if (!includeOwner && owner.whoAmI == target.whoAmI)
				return false;
			return Vector2.DistanceSquared(owner.Center, target.Center) <= radius * radius;
		}

		public static bool TryFindSupportOwner(Player target, string augmentId, float radius, out Player owner, bool includeSelf = false)
		{
			owner = null;
			float bestDistanceSquared = radius < 0f ? float.MaxValue : radius * radius;
			for (int i = 0; i < Main.maxPlayers; i++)
			{
				Player candidate = Main.player[i];
				if (!candidate.active || candidate.dead)
					continue;
				if (!includeSelf && candidate.whoAmI == target.whoAmI)
					continue;
				if (!AreAllies(candidate, target) || !candidate.GetModPlayer<AugmentPlayer>().HasAugment(augmentId))
					continue;

				float distanceSquared = Vector2.DistanceSquared(candidate.Center, target.Center);
				if (distanceSquared > bestDistanceSquared)
					continue;

				bestDistanceSquared = distanceSquared;
				owner = candidate;
			}

			return owner != null;
		}

		public static int ServerHealPlayer(Player target, int healAmount)
		{
			if (Main.netMode == NetmodeID.MultiplayerClient || target == null || !target.active || target.dead || healAmount <= 0)
				return 0;

			int maxLife = Math.Max(target.statLifeMax, target.statLifeMax2);
			if (maxLife <= 0) maxLife = 500;

			int oldLife = target.statLife;
			target.statLife = Math.Min(maxLife, target.statLife + healAmount);
			int actualHeal = Math.Max(healAmount, target.statLife - oldLife);
			if (actualHeal <= 0)
				return 0;

			if (Main.netMode == NetmodeID.Server)
			{
				NetMessage.SendData(MessageID.PlayerLifeMana, -1, -1, null, target.whoAmI);
				ModPacket packet = ModContent.GetInstance<Augments>().GetPacket();
				packet.Write((byte)AugmentPacketType.SupportHealVisual);
				packet.Write((byte)target.whoAmI);
				packet.Write(actualHeal);
				packet.Send();
			}
			else
			{
				target.HealEffect(actualHeal, false);
			}

			ModContent.GetInstance<Augments>().Logger.Info($"Server healed target={target.name} amount={actualHeal}");
			return actualHeal;
		}

		public static bool IsCleansableDebuff(int buffType)
		{
			if (buffType <= 0 || !Main.debuff[buffType])
				return false;
			if (buffType == BuffID.PotionSickness || buffType == BuffID.ManaSickness || buffType == BuffID.ChaosState)
				return false;
			if (buffType == ModContent.BuffType<LifelineCooldownBuff>() || buffType == ModContent.BuffType<LastRitesCooldownBuff>())
				return false;
			return true;
		}

		public static void ServerClearDebuffs(Player target)
		{
			if (Main.netMode == NetmodeID.MultiplayerClient || target == null || !target.active)
				return;

			for (int i = target.buffType.Length - 1; i >= 0; i--)
			{
				if (IsCleansableDebuff(target.buffType[i]))
					target.DelBuff(i);
			}

			if (Main.netMode == NetmodeID.Server)
			{
				ModPacket packet = ModContent.GetInstance<Augments>().GetPacket();
				packet.Write((byte)AugmentPacketType.CleanseClearDebuffs);
				packet.Send(target.whoAmI);
			}
		}

		public static void HandleCleanseClearDebuffs()
		{
			if (Main.netMode != NetmodeID.MultiplayerClient)
				return;

			Player player = Main.LocalPlayer;
			if (!player.active || player.dead)
				return;

			bool removedAny = false;
			for (int i = player.buffType.Length - 1; i >= 0; i--)
			{
				if (IsCleansableDebuff(player.buffType[i]))
				{
					player.DelBuff(i);
					removedAny = true;
				}
			}

			SoundEngine.PlaySound(SoundID.Item4, player.Center);
			for (int i = 0; i < 25; i++)
			{
				Vector2 speed = Main.rand.NextVector2Circular(5f, 5f);
				Dust d = Dust.NewDustDirect(player.position, player.width, player.height, DustID.PurificationPowder, speed.X, speed.Y);
				d.noGravity = true;
			}

			if (removedAny)
			{
				CombatText.NewText(player.getRect(), Color.LightCyan, "Cleansed!");
			}

			NetMessage.SendData(MessageID.PlayerBuffs, -1, -1, null, player.whoAmI);
		}

		public static void HandleSoulLinkRequest(int whoAmI, int requestedHeal)
		{
			if (!TryGetRequestOwner(whoAmI, "soul_link", out Player owner))
				return;
			if (!owner.GetModPlayer<AugmentPlayer>().TryAuthorizeSoulLinkRequest())
				return;

			int healAmount = Math.Clamp(requestedHeal, 1, 200);
			foreach (Player target in Main.player)
			{
				if (IsAllyInRange(owner, target, AuraRadius))
					ServerHealPlayer(target, healAmount);
			}
		}

		public static void HandleCleanseRequest(int whoAmI)
		{
			if (!TryGetRequestOwner(whoAmI, "cleanse", out Player owner))
				return;
			owner.GetModPlayer<AugmentPlayer>().TryTriggerCleanseServer();
		}

		public static void HandleMediGunHealRequest(int senderWhoAmI, byte targetPlayerIndex, int healAmount)
		{
			if (Main.netMode != NetmodeID.Server || senderWhoAmI < 0 || senderWhoAmI >= Main.maxPlayers || targetPlayerIndex >= Main.maxPlayers)
				return;

			Player sender = Main.player[senderWhoAmI];
			Player target = Main.player[targetPlayerIndex];

			if (!sender.active || sender.dead || !target.active || target.dead)
				return;

			if (!AreAllies(sender, target))
				return;

			// Range check (~1400f safety buffer against latency across MK I, MK II, MK III, and MK IV tiers)
			if (Vector2.DistanceSquared(sender.Center, target.Center) > 1400f * 1400f)
				return;

			int clampedHeal = Math.Clamp(healAmount, 1, 50);
			ServerHealPlayer(target, clampedHeal);
		}

		public static void ProcessBandOfRegenFeedback(Player medic, int healAmount)
		{
			if (medic == null || !medic.active || medic.dead || healAmount <= 0)
				return;

			int selfHeal = Math.Max(1, healAmount / 2);
			int medicMaxLife = Math.Max(medic.statLifeMax, medic.statLifeMax2);
			if (medic.statLife < medicMaxLife)
			{
				if (Main.netMode == NetmodeID.SinglePlayer)
				{
					ServerHealPlayer(medic, selfHeal);
				}
				else if (Main.netMode == NetmodeID.MultiplayerClient)
				{
					ModPacket packet = ModContent.GetInstance<Augments>().GetPacket();
					packet.Write((byte)AugmentPacketType.MediGunHealRequest);
					packet.Write((byte)medic.whoAmI);
					packet.Write(selfHeal);
					packet.Send();
				}
				Dust d = Dust.NewDustDirect(medic.position, medic.width, medic.height, DustID.GreenFairy, 0f, -1f, 100, default, 0.9f);
				d.noGravity = true;
			}
		}

		public static void ApplyTetherSocketEffects(Player medic, int targetPlayerWhoAmI, int targetNPCWhoAmI, int socketed)
		{
			if (socketed <= 0)
				return;

			if (targetPlayerWhoAmI >= 0 && targetPlayerWhoAmI < Main.maxPlayers)
			{
				Player target = Main.player[targetPlayerWhoAmI];
				if (target.active && !target.dead)
				{
					if (socketed == ItemID.BandofRegeneration)
					{
						target.AddBuff(ModContent.BuffType<BandOfRegenBuff>(), 10);
					}
					else if (socketed == ItemID.BandofStarpower)
					{
						target.AddBuff(ModContent.BuffType<BandOfStarpowerBuff>(), 10);
					}
					else if (socketed == ItemID.AnkletoftheWind)
					{
						target.AddBuff(ModContent.BuffType<AnkletOfTheWindBuff>(), 10);
					}
					else if (socketed == ItemID.CobaltShield)
					{
						target.AddBuff(ModContent.BuffType<CobaltShieldBuff>(), 10);
					}
					else if (socketed == ItemID.Bezoar)
					{
						target.AddBuff(ModContent.BuffType<BezoarWardBuff>(), 10);
						target.buffImmune[BuffID.Poisoned] = true;
						target.buffImmune[BuffID.Venom] = true;
						target.ClearBuff(BuffID.Poisoned);
						target.ClearBuff(BuffID.Venom);
					}
					else if (socketed == ItemID.SharkToothNecklace)
					{
						target.AddBuff(ModContent.BuffType<SharkToothNecklaceBuff>(), 10);
					}
					else if (socketed == ItemID.PhilosophersStone)
					{
						target.AddBuff(ModContent.BuffType<PhilosophersStoneBuff>(), 10);
						if (medic != null && medic.active && !medic.dead)
						{
							var mgPlayer = medic.GetModPlayer<MediGunPlayer>();
							if (target.potionDelay > 38 * 60 && target.potionDelay <= 40 * 60 && mgPlayer.PhilosopherHealCooldown <= 0)
							{
								mgPlayer.PhilosopherHealCooldown = 30 * 60;
								ServerHealPlayer(medic, 25);
								CombatText.NewText(medic.getRect(), new Color(130, 240, 180), "+25 HP Potion Echo!");
								SoundEngine.PlaySound(SoundID.Item4, medic.Center);
							}
						}
					}
					else if (socketed == ItemID.Aglet)
					{
						target.AddBuff(ModContent.BuffType<AgletBuff>(), 10);
					}
					else if (socketed == ItemID.HandWarmer)
					{
						target.AddBuff(ModContent.BuffType<HandWarmerBuff>(), 10);
						target.buffImmune[BuffID.Chilled] = true;
						target.buffImmune[BuffID.Frozen] = true;
						target.ClearBuff(BuffID.Chilled);
						target.ClearBuff(BuffID.Frozen);
						Lighting.AddLight(target.Center, 0.7f, 0.35f, 0.15f);
					}
					else if (socketed == ItemID.FeralClaws)
					{
						target.AddBuff(ModContent.BuffType<FeralClawsBuff>(), 10);
					}
					else if (socketed == ItemID.Shackle)
					{
						target.AddBuff(ModContent.BuffType<ShackleBuff>(), 10);
					}
				}
			}
			else if (targetNPCWhoAmI >= 0 && targetNPCWhoAmI < Main.maxNPCs)
			{
				NPC npc = Main.npc[targetNPCWhoAmI];
				if (npc.active)
				{
					if (socketed == ItemID.BandofRegeneration)
					{
						npc.lifeRegen += 3;
					}
					else if (socketed == ItemID.Shackle)
					{
						npc.defense += 3;
					}
				}
			}
		}

		public static void ProcessSocketHealPulse(Player medic, Player targetPlayer, NPC targetNPC, int healAmount, int socketed)
		{
			if (socketed <= 0 || healAmount <= 0)
				return;

			if (socketed == ItemID.BandofRegeneration)
			{
				ProcessBandOfRegenFeedback(medic, healAmount);
			}
			else if (socketed == ItemID.BandofStarpower)
			{
				if (targetPlayer != null && targetPlayer.active && !targetPlayer.dead)
				{
					if (targetPlayer.statMana < targetPlayer.statManaMax2)
					{
						targetPlayer.statMana = Math.Min(targetPlayer.statManaMax2, targetPlayer.statMana + 3);
						targetPlayer.ManaEffect(3);
					}
				}
			}
			else if (socketed == ItemID.Bezoar)
			{
				int currentLife = targetPlayer != null ? targetPlayer.statLife : (targetNPC != null ? targetNPC.life : 100);
				int maxLife = targetPlayer != null ? Math.Max(targetPlayer.statLifeMax, targetPlayer.statLifeMax2) : (targetNPC != null ? targetNPC.lifeMax : 100);
				if (currentLife < maxLife / 2)
				{
					int bonusHeal = Math.Max(1, (int)(healAmount * 0.10f));
					if (targetPlayer != null)
					{
						ServerHealPlayer(targetPlayer, bonusHeal);
					}
					else if (targetNPC != null)
					{
						targetNPC.life = Math.Min(targetNPC.lifeMax, targetNPC.life + bonusHeal);
					}
				}
			}
			else if (socketed == ItemID.SharkToothNecklace)
			{
				int medicMaxLife = Math.Max(medic.statLifeMax, medic.statLifeMax2);
				if (medic != null && medic.active && !medic.dead && medic.statLife < medicMaxLife)
				{
					medic.statLife = Math.Min(medicMaxLife, medic.statLife + 2);
				}
			}
		}

		public static void HandleMediGunOverclockActivate(int senderWhoAmI, byte tier, byte targetPlayerIndex)
		{
			if (Main.netMode != NetmodeID.Server || senderWhoAmI < 0 || senderWhoAmI >= Main.maxPlayers || targetPlayerIndex >= Main.maxPlayers)
				return;

			Player sender = Main.player[senderWhoAmI];
			Player target = Main.player[targetPlayerIndex];

			if (!sender.active || sender.dead || !target.active || target.dead || !AreAllies(sender, target))
				return;

			int healAmount = tier == 4 ? 100 : (tier == 3 ? 75 : (tier == 2 ? 50 : 30));
			int buffDuration = 360; // 6s

			ServerHealPlayer(target, healAmount);
			if (tier == 4)
			{
				target.AddBuff(ModContent.BuffType<OverclockMK4Buff>(), buffDuration);
			}
			else if (tier == 3)
			{
				target.AddBuff(ModContent.BuffType<OverclockMK3Buff>(), buffDuration);
			}
			else if (tier == 2)
			{
				target.AddBuff(ModContent.BuffType<OverclockMK2Buff>(), buffDuration);
			}
			else
			{
				target.AddBuff(ModContent.BuffType<OverclockBuff>(), buffDuration);
			}
		}

		public static void HandleLifelineRequest(int whoAmI)
		{
			if (Main.netMode != NetmodeID.Server || whoAmI < 0 || whoAmI >= Main.maxPlayers)
				return;

			Player target = Main.player[whoAmI];
			if (target.active)
				target.GetModPlayer<AugmentPlayer>().TryConsumeLifelineServer();
		}

		public static void HandleSoulMartyrTrigger(int sourcePlayerIndex, byte martyrIndex, int damage)
		{
			if (Main.netMode != NetmodeID.Server || martyrIndex >= Main.maxPlayers)
				return;

			Player martyr = Main.player[martyrIndex];
			if (!martyr.active || martyr.dead || !martyr.GetModPlayer<AugmentPlayer>().HasAugment("soul_martyr"))
				return;

			// Server applies damage to martyr, clamped so they never drop below 1 HP
			martyr.statLife = Math.Max(1, martyr.statLife - damage);
			NetMessage.SendData(MessageID.PlayerLifeMana, -1, -1, null, martyr.whoAmI);

			// Notify martyr's client so it updates local statLife and triggers invulnerability
			ModPacket packet = ModContent.GetInstance<Augments>().GetPacket();
			packet.Write((byte)AugmentPacketType.SoulMartyrDamage);
			packet.Write(damage);
			packet.Send(martyr.whoAmI);
		}

		public static void HandleSoulMartyrDamage(int damage)
		{
			if (Main.netMode != NetmodeID.MultiplayerClient)
				return;

			Player player = Main.LocalPlayer;
			player.statLife = Math.Max(1, player.statLife - damage);
			player.immune = true;
			player.immuneTime = 40;
			SoundEngine.PlaySound(SoundID.Item29, player.Center);
			NetMessage.SendData(MessageID.PlayerLifeMana, -1, -1, null, player.whoAmI);
		}

		public static void HandleUndyingBondRequest(int whoAmI)
		{
			if (Main.netMode != NetmodeID.Server || whoAmI < 0 || whoAmI >= Main.maxPlayers)
				return;

			Player target = Main.player[whoAmI];
			if (target.active && target.dead)
				target.GetModPlayer<AugmentPlayer>().TryApplyUndyingBondRedirectServer();
		}

		public static void BroadcastRevitalizingWaveVisual(Player owner)
		{
			if (Main.netMode != NetmodeID.Server)
				return;

			ModPacket packet = ModContent.GetInstance<Augments>().GetPacket();
			packet.Write((byte)AugmentPacketType.RevitalizingWaveVisual);
			packet.Write((byte)owner.whoAmI);
			packet.Send();
		}

		public static void SendUndyingBondRedirect(Player target, int spawnX, int spawnY)
		{
			if (Main.netMode != NetmodeID.Server)
				return;

			ModPacket packet = ModContent.GetInstance<Augments>().GetPacket();
			packet.Write((byte)AugmentPacketType.UndyingBondRedirect);
			packet.Write(spawnX);
			packet.Write(spawnY);
			packet.Send(target.whoAmI);
		}

		public static void HandleRevitalizingWaveRequest(int whoAmI)
		{
			if (!TryGetRequestOwner(whoAmI, "revitalizing_wave", out Player owner))
				return;

			BroadcastRevitalizingWaveVisual(owner);

			foreach (Player target in Main.player)
			{
				if (IsAllyInRange(owner, target, AuraRadius))
					ServerHealPlayer(target, 25);
			}
		}

		public static void HandleRevitalizingWaveVisual(int ownerIndex)
		{
			if (Main.netMode == NetmodeID.MultiplayerClient && ownerIndex >= 0 && ownerIndex < Main.maxPlayers)
				RevitalizingWaveAugment.SpawnBurst(Main.player[ownerIndex]);
		}

		public static void HandleHealVisual(int targetIndex, int amount)
		{
			if (Main.netMode != NetmodeID.MultiplayerClient || targetIndex < 0 || targetIndex >= Main.maxPlayers || amount <= 0)
				return;

			Player player = Main.player[targetIndex];
			if (!player.active || player.dead)
				return;

			// Floating combat text for all nearby clients
			player.HealEffect(amount, false);

			int maxHp = Math.Max(player.statLifeMax, player.statLifeMax2);
			if (maxHp <= 0) maxHp = 500;
			player.statLife = Math.Min(maxHp, player.statLife + amount);

			// If this client is the target being healed, we MUST sync our own local statLife!
			// In vanilla Terraria, a client's own statLife is client-authoritative and ignores
			// incoming PlayerLifeMana (packet 16) from the server when whoAmI == Main.myPlayer.
			// Without this local update and sync, the client would report their old HP on next sync,
			// causing the heal to rubberband backwards.
			if (targetIndex == Main.myPlayer)
			{
				NetMessage.SendData(MessageID.PlayerLifeMana, -1, -1, null, Main.myPlayer);
			}
		}

		public static void HandleUndyingBondRedirect(int spawnX, int spawnY)
		{
			if (Main.netMode != NetmodeID.MultiplayerClient)
				return;

			Main.LocalPlayer.GetModPlayer<AugmentPlayer>().ApplyUndyingBondRedirectClient(spawnX, spawnY);
		}

		private static bool TryGetRequestOwner(int whoAmI, string augmentId, out Player owner)
		{
			owner = null;
			if (Main.netMode != NetmodeID.Server || whoAmI < 0 || whoAmI >= Main.maxPlayers)
				return false;

			owner = Main.player[whoAmI];
			return owner.active && !owner.dead && owner.GetModPlayer<AugmentPlayer>().HasAugment(augmentId);
		}
	}
}
