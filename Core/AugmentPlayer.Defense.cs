using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.Chat;
using Terraria.DataStructures;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Augments.Core;

namespace Augments
{
	// Defensive hooks: dodging, damage reduction, death, respawn, Undying Bond and hurt handling.
	public partial class AugmentPlayer
	{
		public override bool FreeDodge(Player.HurtInfo info)
		{
			foreach (var a in Owned)
			{
				if (a.FreeDodge(Player, info))
				{
					AugmentDamageTracker.RecordDamageBlocked(a.Id, a.DisplayName, Math.Max(1, info.Damage), a.Rarity, a.Class);
					return true;
				}
			}
			return false;
		}

		public override void ModifyHurt(ref Player.HurtModifiers modifiers)
		{
			foreach (var a in Owned)
				a.ModifyHurt(Player, ref modifiers);

			// Pull-based Martyr's Resolve: check nearby Support players for the augment
			// and apply 15% incoming damage reduction to the local player.
			// The Support player's corresponding self-penalty (+15% damage taken) is
			// applied inside MartyrsResolveAugment.ModifyHurt as a normal augment hook.
			for (int i = 0; i < Main.maxPlayers; i++)
			{
				Player other = Main.player[i];
				if (!SupportEffects.IsAllyInRange(other, Player, AuraRadius))
					continue;
				var otherAP = other.GetModPlayer<AugmentPlayer>();
				if (!otherAP.HasAugment("martyrs_resolve"))
					continue;
				modifiers.FinalDamage *= 0.85f;
				break;
			}
		}

		// Soul Martyr & Lifeline: fires on the dying player's own client the moment HP hits 0.
		// Returning false prevents death; returning true allows it.
		public override bool PreKill(double damage, int hitDirection, bool pvp, ref bool playSound, ref bool genGore, ref PlayerDeathReason damageSource)
		{
			// Type-D Bastion Protocol: Energy Barrier active completely prevents death
			if (BastionBarrierTicks > 0)
			{
				int blocked = Math.Max(1, (int)damage);
				AugmentDamageTracker.RecordDamageBlocked("type_d_dreadnought_protocol", "Type-D: Dreadnought Protocol", blocked, AugmentRarity.Epic, AugmentClass.Universal);
				Player.statLife = 1;
				Player.immune = true;
				Player.immuneTime = Math.Max(Player.immuneTime, BastionBarrierTicks);
				return false;
			}

			// Type-D Dreadnought Protocol: If the lethal blow accumulates >= 120 cumulative damage,
			// activate the energy barrier now to save the player's life at 1 HP.
			if (HasAugment("type_d_dreadnought_protocol") || HasAugment("type_d_bastion_protocol") || HasAugment("avatar_of_the_wall"))
			{
				if (BastionStoredDamage + (int)damage >= 120)
				{
					int blocked = Math.Max(1, (int)damage);
					AugmentDamageTracker.RecordDamageBlocked("type_d_dreadnought_protocol", "Type-D: Dreadnought Protocol", blocked, AugmentRarity.Epic, AugmentClass.Universal);
					BastionStoredDamage = 0;
					BastionBarrierTicks = 90;
					Player.statLife = 1;
					Player.immune = true;
					Player.immuneTime = 90;
					AvatarOfTheWallAugment.TriggerKineticShockwave(Player);
					return false;
				}
			}

			// Soul Martyr: Teammates inside your aura cannot die. Any lethal damage they take is absorbed and transferred to you instead (cannot drop you below 1 HP).
			if (SupportEffects.TryFindSupportOwner(Player, "soul_martyr", AuraRadius, out Player martyrOwner))
			{
				int dmg = (int)damage;
				if (dmg <= 0)
					dmg = 1;

				AugmentDamageTracker.RecordDamageBlocked("soul_martyr", "Soul Martyr", dmg, AugmentRarity.Rare, AugmentClass.Support);

				Player.statLife = 1;
				Player.immune = true;
				Player.immuneTime = 60;
				SoundEngine.PlaySound(SoundID.Item29, Player.Center);

				if (Main.netMode == NetmodeID.SinglePlayer)
				{
					martyrOwner.statLife = Math.Max(1, martyrOwner.statLife - dmg);
					martyrOwner.immune = true;
					martyrOwner.immuneTime = 40;
					SoundEngine.PlaySound(SoundID.Item29, martyrOwner.Center);
				}
				else if (Main.netMode == NetmodeID.MultiplayerClient)
				{
					ModPacket packet = ModContent.GetInstance<Augments>().GetPacket();
					packet.Write((byte)AugmentPacketType.SoulMartyrTrigger);
					packet.Write((byte)martyrOwner.whoAmI);
					packet.Write(dmg);
					packet.Send();
				}
				return false;
			}

			if (LifelineCooldown > 0 || !lifelineProtectionAuthorized)
				return true;

			if (SupportEffects.TryFindSupportOwner(Player, "lifeline", AuraRadius, out Player lifelineOwner, includeSelf: true))
			{
				lifelineOwner.GetModPlayer<AugmentPlayer>().LifelineCooldown = 5400;
				lifelineOwner.AddBuff(ModContent.BuffType<LifelineCooldownBuff>(), 5400);
			}

			int lethalBlocked = Math.Max(1, (int)damage);
			AugmentDamageTracker.RecordDamageBlocked("lifeline", "Lifeline", lethalBlocked, AugmentRarity.Epic, AugmentClass.Support);

			if (Main.netMode == NetmodeID.MultiplayerClient)
			{
				ModPacket packet = ModContent.GetInstance<Augments>().GetPacket();
				packet.Write((byte)AugmentPacketType.LifelineTrigger);
				packet.Send();
				lifelineProtectionAuthorized = false;
				LifelineCooldown = 5400;
				lifelineInvulnTicks = 120;
				Player.statLife = 1;
				Player.immune = true;
				Player.immuneTime = 120;
				Player.AddBuff(ModContent.BuffType<LifelineCooldownBuff>(), 5400);
				SoundEngine.PlaySound(SoundID.Item29, Player.Center);
				return false;
			}

			return !TryConsumeLifelineServer();
		}

		// Undying Bond: when the player respawns, flag for teleport to the living Support ally.
		// Vanilla Player.Spawn() runs PlayerLoader.OnRespawn() BEFORE resetting position to world spawn,
		// so we defer the actual teleport to the first PostUpdate() tick to ensure it sticks.
		public override void OnRespawn()
		{
			if (SupportEffects.TryFindSupportOwner(Player, "undying_bond", -1f, out _))
			{
				undyingBondNeedsTeleport = true;
			}
		}

		// Undying Bond: fires every tick while the local player is dead.
		// Overrides SpawnX/SpawnY to redirect respawn to the nearest Support player
		// who owns "undying_bond". No range limit — works regardless of distance.
		// MULTIPLAYER FLAG: SpawnX/SpawnY are modified on the dead player's client.
		// If the server uses its own copy for the respawn calculation, this will only
		// work in singleplayer and a ModPacket sync will be needed for multiplayer.
		public override void UpdateDead()
		{
			if (undyingBondRedirectSent)
				return;
			if (undyingBondRequestTimer > 0)
			{
				undyingBondRequestTimer--;
				return;
			}

			if (Main.netMode == NetmodeID.MultiplayerClient)
			{
				ModPacket packet = ModContent.GetInstance<Augments>().GetPacket();
				packet.Write((byte)AugmentPacketType.UndyingBondRequest);
				packet.Send();
				undyingBondRequestTimer = 60;
				return;
			}

			if (!TryApplyUndyingBondRedirectServer())
				undyingBondRequestTimer = 60;
		}

		public bool TryApplyUndyingBondRedirectServer()
		{
			if (Main.netMode == NetmodeID.MultiplayerClient || undyingBondRedirectSent)
				return false;
			ModContent.GetInstance<Augments>().Logger.Info($"Undying Bond check target={Player.name}");
			if (!SupportEffects.TryFindSupportOwner(Player, "undying_bond", -1f, out Player owner))
				return false;

			Player.SpawnX = (int)(owner.Center.X / 16f);
			Player.SpawnY = (int)(owner.Center.Y / 16f);
			undyingBondRedirectSent = true;
			ModContent.GetInstance<Augments>().Logger.Info($"Undying Bond found support owner={owner.name}");
			if (Main.netMode == NetmodeID.Server)
				SupportEffects.SendUndyingBondRedirect(Player, Player.SpawnX, Player.SpawnY);
			return true;
		}

		public void ApplyUndyingBondRedirectClient(int spawnX, int spawnY)
		{
			if (Main.netMode != NetmodeID.MultiplayerClient)
				return;

			Player.SpawnX = spawnX;
			Player.SpawnY = spawnY;
			undyingBondRedirectSent = true;
		}

		public override void Kill(double damage, int hitDirection, bool pvp, PlayerDeathReason damageSource)
		{
			undyingBondNeedsTeleport = false;
			undyingBondRedirectSent = false;
			undyingBondRequestTimer = 0;
			mendingAuraHealTimer = 0;
			vitalEchoLastLife = -1;
			vitalEchoDefenseTicks = 0;
			soulLinkRequestCooldown = 0;
			BastionStoredDamage = 0;
			BastionBarrierTicks = 0;
			SynchronizerTimer = 0;
			ApexHunterCooldown = 0;
			KineticShockwaveCooldown = 0;
			KineticDashWindowMemoryTimer = 0;
			FortuneCoinBurstCooldown = 0;
			GunslingerContinuousFireTicks = 0;
			GunslingerLingerTimer = 0;
			GunslingerWasAtPeak = false;
			foreach (var a in Owned)
				a.OnKill(Player);
		}

		public override void OnHurt(Player.HurtInfo info)
		{
			foreach (var a in Owned)
				a.OnHurt(Player, info);

			// Track mitigated damage from Type-D Dreadnought Protocol (15% incoming damage reduction)
			if (HasAugment("type_d_dreadnought_protocol") || HasAugment("type_d_bastion_protocol") || HasAugment("avatar_of_the_wall"))
			{
				if (BastionBarrierTicks <= 0 && info.Damage > 0)
				{
					int mitigated = (int)Math.Round(info.Damage * (0.15f / 0.85f));
					if (mitigated > 0)
					{
						AugmentDamageTracker.RecordDamageBlocked("type_d_dreadnought_protocol", "Type-D: Dreadnought Protocol", mitigated, AugmentRarity.Epic, AugmentClass.Universal);
					}
				}
			}

			// Track mitigated damage from nearby Martyr's Resolve Support aura (15% incoming damage reduction)
			for (int i = 0; i < Main.maxPlayers; i++)
			{
				Player other = Main.player[i];
				if (!SupportEffects.IsAllyInRange(other, Player, AuraRadius))
					continue;
				var otherAP = other.GetModPlayer<AugmentPlayer>();
				if (!otherAP.HasAugment("martyrs_resolve"))
					continue;
				if (info.Damage > 0)
				{
					int mitigated = (int)Math.Round(info.Damage * (0.15f / 0.85f));
					if (mitigated > 0)
					{
						AugmentDamageTracker.RecordDamageBlocked("martyrs_resolve", "Martyr's Resolve", mitigated, AugmentRarity.Rare, AugmentClass.Support);
					}
				}
				break;
			}
		}

		public override void OnHitByNPC(NPC npc, Player.HurtInfo hurtInfo)
		{
			foreach (var a in Owned)
				a.OnHitByNPC(Player, npc, hurtInfo);
		}
	}
}
