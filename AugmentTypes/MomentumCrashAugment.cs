using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Augments.Core;

namespace Augments
{
	public class MomentumCrashAugment : Augment
	{
		public override string Id => "momentum_crash";
		public override string DisplayName => "Momentum Crash";
		public override string Description =>
			$"Dashing grants 75% reduced damage taken for {AugmentText.Duration("0.25 seconds")}. Landing a melee hit during this " +
			$"window deals {AugmentText.BonusDamage("+30% bonus damage")} and applies {AugmentText.Immobilize("Confused")} for {AugmentText.Duration("1.5 seconds")}.";

		public override AugmentRarity Rarity => AugmentRarity.Epic;
		public override AugmentClass Class => AugmentClass.Melee;
		public override string FamilyId => AugmentFamilyRegistry.KineticId;

		private const float VelocitySpikeThreshold = 8f;
		private const int DashWindowTicks = 15;
		private const float BonusDamagePercent = 0.30f;
		private const float DamageReductionPercent = 0.75f;
		private const int ConfusedDurationTicks = 90;

		// Generic velocity-spike dash detection - vanilla has no single shared
		// "is dashing" flag across Shield of Cthulhu/Tabi/Master Ninja Gear (or
		// modded equivalents), so we watch for a sudden jump in speed instead.
		// Excludes the immunity window after getting hit, since knockback from
		// taking damage produces the same kind of spike as an actual dash.
		// Only opens a new window when one isn't already active, so sustained
		// high speed (falling, minecarts, etc.) can't keep re-triggering every tick.
		public override void OnUpdate(Player player)
		{
			var ap = player.GetModPlayer<AugmentPlayer>();
			float currentSpeed = player.velocity.Length();

			if (ap.MomentumCrashDashWindowTimer > 0)
				ap.MomentumCrashDashWindowTimer--;
			else if (!player.immune && currentSpeed - ap.MomentumCrashPreviousSpeed > VelocitySpikeThreshold)
				ap.MomentumCrashDashWindowTimer = DashWindowTicks;

			ap.MomentumCrashPreviousSpeed = currentSpeed;
		}

		// Shield of Cthulhu-style dash damage is dealt through a separate
		// vanilla system (NPC.StrikeNPC called directly from the dash code)
		// that bypasses item/proj hit hooks entirely, so this can't react to
		// the dash impact itself - only to a melee weapon swing landing
		// while the window from OnUpdate is still open.
		public override void ModifyHurt(Player player, ref Player.HurtModifiers modifiers)
		{
			if (player.GetModPlayer<AugmentPlayer>().MomentumCrashDashWindowTimer > 0)
				modifiers.FinalDamage *= 1f - DamageReductionPercent;
		}

		public override void ModifyHitNPCWithItem(Player player, Item item, NPC target, ref NPC.HitModifiers modifiers)
		{
			if (item.CountsAsClass(DamageClass.Melee))
				TryTriggerDashImpact(player, item.damage, ref modifiers);
		}

		public override void ModifyHitNPCWithProj(Player player, Projectile proj, NPC target, ref NPC.HitModifiers modifiers)
		{
			if (proj.CountsAsClass(DamageClass.Melee))
				TryTriggerDashImpact(player, proj.damage, ref modifiers);
		}

		private static void TryTriggerDashImpact(Player player, int baseDamage, ref NPC.HitModifiers modifiers)
		{
			var ap = player.GetModPlayer<AugmentPlayer>();
			if (ap.MomentumCrashDashWindowTimer <= 0)
				return;

			modifiers.FlatBonusDamage += (int)(baseDamage * BonusDamagePercent);
			ap.MomentumCrashPendingConfuse = true;
			ap.MomentumCrashDashWindowTimer = 0;
		}

		public override void OnHitNPCWithItem(Player player, Item item, NPC target, NPC.HitInfo hit)
		{
			ApplyPendingConfuse(player, target);
		}

		public override void OnHitNPCWithProj(Player player, Projectile proj, NPC target, NPC.HitInfo hit)
		{
			ApplyPendingConfuse(player, target);
		}

		private static void ApplyPendingConfuse(Player player, NPC target)
		{
			var ap = player.GetModPlayer<AugmentPlayer>();
			if (!ap.MomentumCrashPendingConfuse)
				return;

			ap.MomentumCrashPendingConfuse = false;
			target.AddBuff(BuffID.Confused, ConfusedDurationTicks);
			if (Main.netMode != NetmodeID.SinglePlayer)
				NetMessage.SendData(MessageID.NPCBuffs, number: target.whoAmI);
		}
	}
}
