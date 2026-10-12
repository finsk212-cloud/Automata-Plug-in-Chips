using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Augments.Core;

namespace Augments
{
	public class WhipCrackerAugment : Augment
	{
		public override string Id => "whip_cracker";
		public override string DisplayName => "Whip Cracker";
		public override string Description =>
			$"Whip hits apply a stacking debuff, increasing damage taken by {AugmentText.BonusDamage("2%")} per stack (max 5 stacks).";

		public override AugmentRarity Rarity => AugmentRarity.Rare;
		public override AugmentClass Class => AugmentClass.Summon;
		public override string FamilyId => AugmentFamilyRegistry.LasherId;

		private const int StackDurationTicks = 240;

		public override (string label, int baseValue)[] GetScalingEntries()
		{
			return new[] { ("Lasher Protocol: Sonic Crack", 45) };
		}

		public override void OnHitNPCWithProj(Player player, Projectile proj, NPC target, NPC.HitInfo hit)
		{
			if (proj.DamageType == DamageClass.SummonMeleeSpeed)
			{
				var cracked = target.GetGlobalNPC<AugmentCrackedNPC>();
				bool wasAtMax = cracked.Stacks >= AugmentCrackedNPC.MaxStacks;

				cracked.ApplyStack(StackDurationTicks);
				if (Main.netMode == NetmodeID.MultiplayerClient)
					AugmentNet.SendApplyNPCEffectCracked(target.whoAmI, StackDurationTicks);

				if (wasAtMax && cracked.SonicCrackCooldown <= 0 && AugmentFamilyRegistry.GetOwnedCount(player.GetModPlayer<AugmentPlayer>(), AugmentFamilyRegistry.LasherId) >= 2)
				{
					cracked.SonicCrackCooldown = 90; // 1.5s internal cooldown per target
					TriggerSonicCrack(player, target);
				}
			}
		}

		public static void TriggerSonicCrack(Player player, NPC target)
		{
			int shockwaveDamage = AugmentScaling.Scale(45);
			const float blastRadius = 130f;

			// Sonic whip crack audio
			SoundEngine.PlaySound(SoundID.Item105 with { Pitch = 0.35f, Volume = 0.85f }, target.Center);
			SoundEngine.PlaySound(SoundID.Item38 with { Pitch = 0.5f, Volume = 0.6f }, target.Center);

			// Shockwave particle burst in Neural Amber
			for (int i = 0; i < 28; i++)
			{
				float angle = MathHelper.TwoPi * (i / 28f);
				Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(4.5f, 8f);
				Dust d = Dust.NewDustPerfect(target.Center, DustID.CopperCoin, vel, 0, default, 1.25f);
				d.noGravity = true;

				Dust d2 = Dust.NewDustPerfect(target.Center, DustID.GemAmber, vel * 0.7f, 0, default, 1.35f);
				d2.noGravity = true;
			}

			if (player.whoAmI == Main.myPlayer)
			{
				foreach (NPC npc in Main.npc)
				{
					if (!npc.active || npc.friendly || npc.townNPC || npc.dontTakeDamage)
						continue;

					if (npc.Distance(target.Center) <= blastRadius)
					{
						Vector2 dir = npc.Center - target.Center;
						if (dir == Vector2.Zero)
							dir = new Vector2(player.direction, 0f);
						dir.Normalize();

						int hitDirection = dir.X >= 0f ? 1 : -1;
						npc.SimpleStrikeNPC(shockwaveDamage, hitDirection, false, 6.5f, DamageClass.Summon, false);
						AugmentDamageTracker.RecordProtocolHit(AugmentFamilyRegistry.LasherId, "Lasher Protocol: Sonic Crack", shockwaveDamage, false);
					}
				}
			}
		}
	}
}
