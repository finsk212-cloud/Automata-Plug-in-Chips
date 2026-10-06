using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;
using Augments.Items;
using Augments.Core;

namespace Augments
{
	public class MediGunPlayer : ModPlayer
	{
		public float OverclockCharge { get; set; } = 0f;
		public int CurrentPatientWhoAmI { get; set; } = -1;
		public int CurrentTargetNPCWhoAmI { get; set; } = -1;
		public int CurrentEnemySiphonWhoAmI { get; set; } = -1;
		public int SiphonBurstCooldown { get; set; } = 0;
		public int SiphonContinuousCooldown { get; set; } = 0;
		public int PhilosopherHealCooldown { get; set; } = 0;

		public float OverclockChargeRateMultiplier
		{
			get
			{
				float mult = 1.0f;
				if (Player.HeldItem?.TryGetGlobalItem<MediGunGlobalItem>(out var mg) == true)
				{
					if (mg.SocketedAccessoryType == ItemID.BandofStarpower)
						mult += 0.25f;
					if (mg.SocketedAccessoryType == ItemID.FeralClaws)
						mult += 0.15f;
				}

				if (Player.GetModPlayer<AugmentPlayer>().HasAugment("supercharger"))
				{
					mult += 0.15f;
				}

				return mult;
			}
		}

		private bool rightClickReleased = true;
		private bool leftClickReleased = true;
		private bool playedReadySound = false;

		public bool IsHoldingMediGun(out int tier)
		{
			tier = 1;
			if (Player.HeldItem == null || Player.HeldItem.IsAir)
				return false;

			int type = Player.HeldItem.type;
			if (type == ModContent.ItemType<Items.MediGunItem>())
			{
				tier = 1;
				return true;
			}
			if (type == ModContent.ItemType<Items.MediGunMK2Item>())
			{
				tier = 2;
				return true;
			}
			if (type == ModContent.ItemType<Items.MediGunMK3Item>())
			{
				tier = 3;
				return true;
			}
			if (type == ModContent.ItemType<Items.MediGunMK4Item>())
			{
				tier = 4;
				return true;
			}

			return false;
		}

		public override void PostUpdateEquips()
		{
			if (IsHoldingMediGun(out _) && Player.HeldItem?.TryGetGlobalItem<MediGunGlobalItem>(out var mg) == true)
			{
				bool isTethered = CurrentPatientWhoAmI >= 0 || CurrentTargetNPCWhoAmI >= 0 || CurrentEnemySiphonWhoAmI >= 0;

				if (mg.SocketedAccessoryType == ItemID.AnkletoftheWind && isTethered)
				{
					Player.moveSpeed += 0.12f;
				}
				else if (mg.SocketedAccessoryType == ItemID.Aglet && isTethered)
				{
					Player.moveSpeed += 0.06f;
				}
				else if (mg.SocketedAccessoryType == ItemID.CobaltShield && isTethered)
				{
					Player.noKnockback = true;
				}
				else if (mg.SocketedAccessoryType == ItemID.Bezoar)
				{
					Player.buffImmune[BuffID.Poisoned] = true;
				}
				else if (mg.SocketedAccessoryType == ItemID.HandWarmer)
				{
					Player.buffImmune[BuffID.Chilled] = true;
					Player.buffImmune[BuffID.Frozen] = true;
					Player.resistCold = true;
				}
				else if (mg.SocketedAccessoryType == ItemID.SharkToothNecklace)
				{
					Player.GetArmorPenetration(DamageClass.Generic) += 3;
				}
				else if (mg.SocketedAccessoryType == ItemID.Shackle && isTethered)
				{
					Player.statDefense += 2;
				}
			}
		}

		public override void PostUpdate()
		{
			if (PhilosopherHealCooldown > 0)
				PhilosopherHealCooldown--;

			if (SiphonBurstCooldown > 0)
				SiphonBurstCooldown--;

			if (SiphonContinuousCooldown > 0)
				SiphonContinuousCooldown--;

			// Only local player processes input
			if (Player.whoAmI != Main.myPlayer)
				return;

			if (IsHoldingMediGun(out int tier))
			{
				if (OverclockCharge >= 100f)
				{
					OverclockCharge = 100f;
					if (!playedReadySound)
					{
						playedReadySound = true;
						SoundEngine.PlaySound(SoundID.MaxMana, Player.Center);
					}

					// Right-click activates Overclock on current tethered ally target (only when inventory is closed)
					if (Main.mouseRight && rightClickReleased && !Main.playerInventory)
					{
						rightClickReleased = false;

						if (CurrentPatientWhoAmI >= 0 || CurrentTargetNPCWhoAmI >= 0)
						{
							ActivateOverclock(tier);
						}
					}

					// Left-click activates Overclock on current siphoned enemy target (while channeling with right-click)
					if (Main.mouseLeft && leftClickReleased && !Main.playerInventory)
					{
						leftClickReleased = false;

						if (CurrentEnemySiphonWhoAmI >= 0 && CurrentEnemySiphonWhoAmI < Main.maxNPCs)
						{
							ActivateEnemyOverclock(tier, CurrentEnemySiphonWhoAmI);
						}
					}
				}
				else
				{
					playedReadySound = false;
				}

				if (!Main.mouseRight)
				{
					rightClickReleased = true;
				}

				if (!Main.mouseLeft)
				{
					leftClickReleased = true;
				}
			}
			else
			{
				playedReadySound = false;
			}
		}

		public void ActivateOverclock(int tier)
		{
			bool hasSupercharger = Player.GetModPlayer<AugmentPlayer>().HasAugment("supercharger");
			OverclockCharge = hasSupercharger ? 15f : 0f;
			playedReadySound = false;

			int healAmount = tier == 4 ? 100 : (tier == 3 ? 75 : (tier == 2 ? 50 : 30));
			int buffDuration = 360; // 6 seconds

			// Target player heal & buff (NO BUFFS TO MEDIC)
			if (CurrentPatientWhoAmI >= 0 && CurrentPatientWhoAmI < Main.maxPlayers)
			{
				Player patient = Main.player[CurrentPatientWhoAmI];
				if (patient.active && !patient.dead)
				{
					if (Main.netMode == NetmodeID.SinglePlayer)
					{
						SupportEffects.ServerHealPlayer(patient, healAmount);
						if (tier == 4)
						{
							patient.AddBuff(ModContent.BuffType<OverclockMK4Buff>(), buffDuration);
						}
						else if (tier == 3)
						{
							patient.AddBuff(ModContent.BuffType<OverclockMK3Buff>(), buffDuration);
						}
						else if (tier == 2)
						{
							patient.AddBuff(ModContent.BuffType<OverclockMK2Buff>(), buffDuration);
						}
						else
						{
							patient.AddBuff(ModContent.BuffType<OverclockBuff>(), buffDuration);
						}
					}
					else if (Main.netMode == NetmodeID.MultiplayerClient)
					{
						ModPacket packet = ModContent.GetInstance<Augments>().GetPacket();
						packet.Write((byte)AugmentPacketType.MediGunOverclockActivate);
						packet.Write((byte)tier);
						packet.Write((byte)CurrentPatientWhoAmI);
						packet.Send();
					}

					// Audio & visual burst on ally
					SoundEngine.PlaySound(SoundID.Item93 with { Volume = 0.85f, Pitch = 0.2f }, patient.Center);
					SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.95f, Pitch = 0.4f }, patient.Center);

					// Kinetic burst dusts around the ally
					for (int i = 0; i < 20; i++)
					{
						Dust d = Dust.NewDustDirect(patient.position, patient.width, patient.height, DustID.Electric, Main.rand.NextFloat(-3.5f, 3.5f), Main.rand.NextFloat(-3.5f, 3.5f), 100, default, 1.2f);
						d.noGravity = true;
					}
				}
			}
			else if (CurrentTargetNPCWhoAmI >= 0 && CurrentTargetNPCWhoAmI < Main.maxNPCs)
			{
				NPC npc = Main.npc[CurrentTargetNPCWhoAmI];
				if (npc.active)
				{
					npc.life = Math.Min(npc.lifeMax, npc.life + healAmount);
					npc.HealEffect(healAmount);
					if (Main.netMode == NetmodeID.Server)
					{
						NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, npc.whoAmI);
					}
					SoundEngine.PlaySound(SoundID.Item93 with { Volume = 0.85f, Pitch = 0.2f }, npc.Center);
				}
			}
		}

		public void ActivateEnemyOverclock(int tier, int npcWhoAmI)
		{
			if (npcWhoAmI < 0 || npcWhoAmI >= Main.maxNPCs)
				return;

			NPC target = Main.npc[npcWhoAmI];
			if (!target.active || target.life <= 0)
				return;

			bool hasSupercharger = Player.GetModPlayer<AugmentPlayer>().HasAugment("supercharger");
			OverclockCharge = hasSupercharger ? 15f : 0f;
			playedReadySound = false;

			int burstDamage = tier switch
			{
				4 => 300,
				3 => 150,
				2 => 60,
				_ => 25
			};

			int hitDir = Player.direction;
			if (target.Center.X != Player.Center.X)
				hitDir = target.Center.X > Player.Center.X ? 1 : -1;

			target.SimpleStrikeNPC(burstDamage, hitDir, false, 2f, DamageClass.Generic, false);

			if (Player.whoAmI == Main.myPlayer)
			{
				string weaponName = Player.HeldItem?.Name ?? "Medi Gun";
				AugmentDamageTracker.RecordWeaponHit(weaponName, burstDamage, false, AugmentClass.Support);
			}

			// Apply debuffs based on tier
			if (tier == 1)
			{
				target.AddBuff(ModContent.BuffType<Buffs.MediGunCorrosionBuff>(), 300); // -5 def, 5 seconds
				if (Main.netMode == NetmodeID.MultiplayerClient)
				{
					NetMessage.SendData(MessageID.AddNPCBuff, -1, -1, null, target.whoAmI, ModContent.BuffType<Buffs.MediGunCorrosionBuff>(), 300);
				}
			}
			else if (tier == 2)
			{
				target.AddBuff(BuffID.Ichor, 360); // -15 def, 6 seconds
				if (Main.netMode == NetmodeID.MultiplayerClient)
				{
					NetMessage.SendData(MessageID.AddNPCBuff, -1, -1, null, target.whoAmI, BuffID.Ichor, 360);
				}
			}
			else if (tier == 3)
			{
				target.AddBuff(BuffID.Ichor, 480); // -15 def, 8 seconds
				if (Main.netMode == NetmodeID.MultiplayerClient)
				{
					NetMessage.SendData(MessageID.AddNPCBuff, -1, -1, null, target.whoAmI, BuffID.Ichor, 480);
				}
			}
			else if (tier >= 4)
			{
				target.AddBuff(BuffID.Ichor, 600); // -15 def, 10 seconds
				target.AddBuff(BuffID.BetsysCurse, 600); // -40 def, 10 seconds
				if (Main.netMode == NetmodeID.MultiplayerClient)
				{
					NetMessage.SendData(MessageID.AddNPCBuff, -1, -1, null, target.whoAmI, BuffID.Ichor, 600);
					NetMessage.SendData(MessageID.AddNPCBuff, -1, -1, null, target.whoAmI, BuffID.BetsysCurse, 600);
				}
			}

			// Audio: Explosive discharge and electric crackle
			SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.85f, Pitch = 0.15f }, target.Center);
			SoundEngine.PlaySound(SoundID.Item94 with { Volume = 0.9f, Pitch = -0.1f }, target.Center);

			// Visual Explosion: High-energy kinetic crimson & status particles
			int particleCount = 20 + tier * 6;
			for (int i = 0; i < particleCount; i++)
			{
				Vector2 speed = Main.rand.NextVector2Circular(5.5f + tier * 0.8f, 5.5f + tier * 0.8f);
				int dustType = DustID.CrimsonTorch;
				if (tier >= 2 && Main.rand.NextBool(3))
				{
					dustType = DustID.Ichor;
				}
				else if (tier >= 4 && Main.rand.NextBool(3))
				{
					dustType = DustID.Shadowflame;
				}
				else if (Main.rand.NextBool(2))
				{
					dustType = DustID.LifeDrain;
				}

				Dust d = Dust.NewDustDirect(target.position, target.width, target.height, dustType, speed.X, speed.Y, 100, default, 1.35f + tier * 0.15f);
				d.noGravity = true;
			}
		}

		public override void Kill(double damage, int hitDirection, bool pvp, PlayerDeathReason damageSource)
		{
			OverclockCharge = 0f;
			CurrentPatientWhoAmI = -1;
			CurrentTargetNPCWhoAmI = -1;
			CurrentEnemySiphonWhoAmI = -1;
			SiphonBurstCooldown = 0;
			SiphonContinuousCooldown = 0;
			playedReadySound = false;
			leftClickReleased = true;
			rightClickReleased = true;
			PhilosopherHealCooldown = 0;
		}

		public override bool HoverSlot(Item[] inventory, int context, int slot)
		{
			if (inventory == null || slot < 0 || slot >= inventory.Length)
				return false;

			Item targetItem = inventory[slot];
			if (targetItem == null || targetItem.IsAir || !targetItem.TryGetGlobalItem<MediGunGlobalItem>(out var mediGun))
				return false;

			if (context != ItemSlot.Context.InventoryItem &&
				context != ItemSlot.Context.ChestItem &&
				context != ItemSlot.Context.BankItem &&
				context != ItemSlot.Context.VoidItem)
			{
				return false;
			}

			if (Main.mouseRight && Main.mouseRightRelease)
			{
				// 1. Holding a supported accessory on cursor -> socket/swap it
				if (Main.mouseItem != null && !Main.mouseItem.IsAir && MediGunGlobalItem.IsSupportedAccessory(Main.mouseItem.type))
				{
					int oldType = mediGun.SocketedAccessoryType;
					int oldPrefix = mediGun.SocketedAccessoryPrefix;

					mediGun.SocketedAccessoryType = Main.mouseItem.type;
					mediGun.SocketedAccessoryPrefix = Main.mouseItem.prefix;

					if (oldType > 0)
					{
						Main.mouseItem.SetDefaults(oldType);
						if (oldPrefix > 0)
						{
							Main.mouseItem.Prefix(oldPrefix);
						}
					}
					else
					{
						Main.mouseItem.stack--;
						if (Main.mouseItem.stack <= 0)
							Main.mouseItem.TurnToAir();
					}

					SoundEngine.PlaySound(SoundID.Item37, Player.Center);
					string prefixName = mediGun.SocketedAccessoryPrefix > 0 && mediGun.SocketedAccessoryPrefix < Lang.prefix.Length ? Lang.prefix[mediGun.SocketedAccessoryPrefix].Value : "";
					string fullTitle = string.IsNullOrEmpty(prefixName) ? Lang.GetItemNameValue(mediGun.SocketedAccessoryType) : $"{prefixName} {Lang.GetItemNameValue(mediGun.SocketedAccessoryType)}";
					CombatText.NewText(Player.getRect(), new Color(74, 222, 128), $"Socketed: {fullTitle}");

					if (Main.netMode == NetmodeID.MultiplayerClient && context == ItemSlot.Context.ChestItem && Player.chest >= 0)
					{
						NetMessage.SendData(MessageID.SyncChestItem, -1, -1, null, Player.chest, slot);
					}

					Main.mouseRightRelease = false;
					Recipe.FindRecipes();
					return true;
				}

				// 2. Empty cursor hand -> detach attached accessory directly into hand
				if ((Main.mouseItem == null || Main.mouseItem.IsAir) && mediGun.SocketedAccessoryType > 0)
				{
					int detachedType = mediGun.SocketedAccessoryType;
					int detachedPrefix = mediGun.SocketedAccessoryPrefix;

					mediGun.SocketedAccessoryType = 0;
					mediGun.SocketedAccessoryPrefix = 0;

					Main.mouseItem = new Item();
					Main.mouseItem.SetDefaults(detachedType);
					if (detachedPrefix > 0)
					{
						Main.mouseItem.Prefix(detachedPrefix);
					}

					SoundEngine.PlaySound(SoundID.Grab, Player.Center);
					string prefixName = detachedPrefix > 0 && detachedPrefix < Lang.prefix.Length ? Lang.prefix[detachedPrefix].Value : "";
					string fullTitle = string.IsNullOrEmpty(prefixName) ? Lang.GetItemNameValue(detachedType) : $"{prefixName} {Lang.GetItemNameValue(detachedType)}";
					CombatText.NewText(Player.getRect(), new Color(255, 200, 80), $"Detached: {fullTitle}");

					if (Main.netMode == NetmodeID.MultiplayerClient && context == ItemSlot.Context.ChestItem && Player.chest >= 0)
					{
						NetMessage.SendData(MessageID.SyncChestItem, -1, -1, null, Player.chest, slot);
					}

					Main.mouseRightRelease = false;
					Recipe.FindRecipes();
					return true;
				}
			}

			return false;
		}
	}
}
