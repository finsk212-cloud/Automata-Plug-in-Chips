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
	// Protocol procs: on-hit effects fired by completed chip sets (Fortune, Kinetic, Volt, Hivemind, Marksman, Arcane Surge helpers).
	public partial class AugmentPlayer
	{
		public void TryTriggerFortuneCoinBurst(NPC target)
		{
			if (FortuneCoinBurstCooldown > 0)
				return;

			if (AugmentFamilyRegistry.GetOwnedCount(this, AugmentFamilyRegistry.FortuneId) < 5)
				return;

			FortuneCoinBurstCooldown = 20;

			SoundEngine.PlaySound(SoundID.CoinPickup with { Volume = 0.85f, Pitch = 0.35f }, target.Center);

			for (int i = 0; i < 18; i++)
			{
				Vector2 vel = Main.rand.NextVector2Circular(5.5f, 5.5f);
				Dust d = Dust.NewDustPerfect(target.Center, DustID.GoldCoin, vel, 100, Color.Gold, 1.4f);
				d.noGravity = true;
			}

			if (Main.myPlayer != Player.whoAmI)
				return;

			const int coinBurstDamage = 50;
			const float radius = 130f;

			foreach (NPC npc in Main.npc)
			{
				if (!npc.active || npc.friendly || npc.townNPC || npc.dontTakeDamage)
					continue;

				if (npc.Distance(target.Center) > radius)
					continue;

				Vector2 dir = npc.Center - target.Center;
				int hitDir = dir.X >= 0f ? 1 : -1;
				npc.SimpleStrikeNPC(coinBurstDamage, hitDir, false, 4f, DamageClass.Generic, false);
				AugmentDamageTracker.RecordProtocolHit(AugmentFamilyRegistry.FortuneId, "Fortune Protocol: House Edge", coinBurstDamage, false);
			}
		}

		public void TryTriggerKineticShockwave(NPC target, int baseDamage)
		{
			if (KineticShockwaveCooldown > 0)
				return;

			if (AugmentFamilyRegistry.GetOwnedCount(this, AugmentFamilyRegistry.KineticId) < 2)
				return;

			bool isDashing = KineticDashWindowMemoryTimer > 0 || MomentumCrashDashWindowTimer > 0 || Player.dashDelay < 0;
			float speed = Player.velocity.Length();
			float maxRun = Math.Max(Player.maxRunSpeed, Player.accRunSpeed);
			bool isSprintingAtFullSpeed = (maxRun > 0f && speed >= maxRun * 0.90f) || (speed * 5.1f >= 22f);

			if (!isDashing && !isSprintingAtFullSpeed)
				return;

			KineticShockwaveCooldown = 25;

			SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.65f, Pitch = 0.25f }, target.Center);

			for (int i = 0; i < 24; i++)
			{
				float angle = i * (MathHelper.TwoPi / 24f);
				Vector2 vel = angle.ToRotationVector2() * 6.0f;
				Dust d = Dust.NewDustPerfect(target.Center, DustID.Torch, vel, 100, new Color(249, 115, 22), 1.6f);
				d.noGravity = true;
			}
			for (int i = 0; i < 12; i++)
			{
				Dust d = Dust.NewDustPerfect(target.Center, DustID.Smoke, Main.rand.NextVector2Circular(4f, 4f), 100, Color.DarkGray, 1.0f);
				d.noGravity = true;
			}

			if (Main.myPlayer != Player.whoAmI)
				return;

			int shockwaveDamage = Math.Max(1, (int)(baseDamage * 0.75f));
			const float radius = 140f;

			foreach (NPC npc in Main.npc)
			{
				if (!npc.active || npc.friendly || npc.townNPC || npc.dontTakeDamage)
					continue;

				if (npc.Distance(target.Center) > radius)
					continue;

				Vector2 knockbackDir = npc.Center - Player.Center;
				if (knockbackDir == Vector2.Zero)
					knockbackDir = new Vector2(Player.direction, 0f);
				knockbackDir.Normalize();

				int hitDirection = knockbackDir.X >= 0f ? 1 : -1;
				npc.SimpleStrikeNPC(shockwaveDamage, hitDirection, false, 5f, DamageClass.Melee, false);
				AugmentDamageTracker.RecordProtocolHit(AugmentFamilyRegistry.KineticId, "Kinetic Protocol: Shockwave", shockwaveDamage, false);
			}
		}

		public void TryTriggerVoltStaticArc(NPC target)
		{
			if (AugmentFamilyRegistry.GetOwnedCount(this, AugmentFamilyRegistry.VoltId) < 2)
				return;

			if (!target.HasBuff(BuffID.Electrified))
				return;

			if (Main.rand.NextFloat() >= 0.25f)
				return;

			const float arcRange = 250f;
			const int arcDamage = 25;

			NPC secondary = null;
			float nearestDistSq = arcRange * arcRange;
			foreach (NPC npc in Main.npc)
			{
				if (npc == target || !npc.active || npc.friendly || npc.townNPC || npc.dontTakeDamage)
					continue;

				float dSq = Vector2.DistanceSquared(target.Center, npc.Center);
				if (dSq < nearestDistSq)
				{
					nearestDistSq = dSq;
					secondary = npc;
				}
			}

			if (secondary == null)
				return;

			SoundEngine.PlaySound(SoundID.Item93 with { Volume = 0.45f, Pitch = 0.35f }, target.Center);

			// Visual electric arc between target and secondary
			Vector2 diff = secondary.Center - target.Center;
			float dist = diff.Length();
			int dustCount = Math.Max(2, (int)(dist / 14f));
			for (int i = 0; i <= dustCount; i++)
			{
				Vector2 pos = Vector2.Lerp(target.Center, secondary.Center, i / (float)dustCount);
				Dust d = Dust.NewDustPerfect(pos + Main.rand.NextVector2Circular(3f, 3f), DustID.Electric, Vector2.Zero, 80, default, 0.9f);
				d.noGravity = true;
			}

			if (Main.myPlayer != Player.whoAmI)
				return;

			int hitDir = secondary.Center.X >= target.Center.X ? 1 : -1;
			var hitInfo = new NPC.HitInfo
			{
				Damage = arcDamage,
				SourceDamage = arcDamage,
				HitDirection = hitDir,
				DamageType = DamageClass.Generic,
				HideCombatText = false
			};
			secondary.StrikeNPC(hitInfo);
			AugmentDamageTracker.RecordProtocolHit(AugmentFamilyRegistry.VoltId, "Volt Protocol: Superconductor", arcDamage, false);
			if (Main.netMode != NetmodeID.SinglePlayer)
				NetMessage.SendStrikeNPC(secondary, in hitInfo);
		}

		public void TryTriggerHivemindNanites(Projectile proj, NPC target)
		{
			if (AugmentFamilyRegistry.GetOwnedCount(this, AugmentFamilyRegistry.HivemindId) < 2)
				return;

			if (target == null || !target.active || target.friendly || target.dontTakeDamage)
				return;

			bool isMinionOrSentry = proj.minion || proj.sentry
				|| ProjectileID.Sets.MinionShot[proj.type] || ProjectileID.Sets.SentryShot[proj.type]
				|| (proj.CountsAsClass(DamageClass.Summon) && !ProjectileID.Sets.IsAWhip[proj.type]);

			if (!isMinionOrSentry)
				return;

			target.GetGlobalNPC<AugmentNaniteNPC>().ApplyNanites(Player.whoAmI, proj.identity);
			if (Main.netMode == NetmodeID.MultiplayerClient)
				AugmentNet.SendApplyNPCEffectNanites(target.whoAmI, 240, proj.identity);
		}

		public void ApplyHivemindFlatDamage(NPC target, ref NPC.HitModifiers modifiers)
		{
			var naniteNPC = target.GetGlobalNPC<AugmentNaniteNPC>();
			if (!naniteNPC.IsInfested)
				return;

			Player infestingPlayer = (naniteNPC.InfestedByPlayer >= 0 && naniteNPC.InfestedByPlayer < Main.maxPlayers)
				? Main.player[naniteNPC.InfestedByPlayer]
				: Player;

			bool hivemindActive = AugmentFamilyRegistry.GetOwnedCount(this, AugmentFamilyRegistry.HivemindId) >= 2
				|| (infestingPlayer != null && infestingPlayer.active && AugmentFamilyRegistry.GetOwnedCount(infestingPlayer.GetModPlayer<AugmentPlayer>(), AugmentFamilyRegistry.HivemindId) >= 2);

			if (hivemindActive)
			{
				int minionCount = naniteNPC.GetActiveMinionCount(infestingPlayer ?? Player, target);
				if (minionCount > 0)
					modifiers.FlatBonusDamage += minionCount * 3;
			}
		}

		public void ApplyMarksmanArmorPenetration(NPC target, bool isRanged, ref NPC.HitModifiers modifiers)
		{
			if (!isRanged)
				return;

			var marksmanNPC = target.GetGlobalNPC<AugmentMarksmanNPC>();
			if (!marksmanNPC.IsMarked)
				return;

			Player markingPlayer = (marksmanNPC.MarkedByPlayer >= 0 && marksmanNPC.MarkedByPlayer < Main.maxPlayers)
				? Main.player[marksmanNPC.MarkedByPlayer]
				: Player;

			bool marksmanActive = AugmentFamilyRegistry.GetOwnedCount(this, AugmentFamilyRegistry.MarksmanId) >= 2
				|| (markingPlayer != null && markingPlayer.active && AugmentFamilyRegistry.GetOwnedCount(markingPlayer.GetModPlayer<AugmentPlayer>(), AugmentFamilyRegistry.MarksmanId) >= 2);

			if (marksmanActive)
			{
				modifiers.ArmorPenetration += 15f;
			}
		}

		public void TryTriggerChainLightning(NPC target, NPC.HitInfo hit, int onHitDamage)
		{
			if (!Owned.Any(a => a is ChainLightningAugment))
				return;

			ChainLightningAugment.ChainToNearbyTargets(Player, target, hit, onHitDamage);
		}
	}
}
