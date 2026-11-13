using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Augments
{
	// Pull-based effects for Soft Landing, Keen Eye, Emergency Aid and Scapegoat.
	// Every client runs this for every player it can see (same as the other support auras in
	// AugmentPlayer), so each player works out what nearby Support players give them.
	public class SupportAuraPlayer : ModPlayer
	{
		// Scapegoat: ticks to wait before the same debuff type is moved again, so a debuff that is
		// re-applied every tick (lava, a tongue, an enemy aura) does not flood the network.
		private const int ScapegoatRepeatTicks = 30;
		private readonly Dictionary<int, uint> scapegoatLastSent = new Dictionary<int, uint>();

		public override void UpdateEquips()
		{
			float radius = SupportEffects.AuraRadius;

			// Soft Landing: no fall damage for the owner and allies in range.
			if (SupportEffects.TryFindSupportOwner(Player, "soft_landing", radius, out _, includeSelf: true))
				Player.noFallDmg = true;

			// Keen Eye: crit for teammates only. The owner never gets it from their own plugin
			// (another Keen Eye owner nearby still counts as a teammate). Sources don't stack.
			if (SupportEffects.TryFindSupportOwner(Player, "keen_eye", radius, out _, includeSelf: false))
				Player.GetCritChance(DamageClass.Generic) += KeenEyeAugment.CritBonus;

			// Emergency Aid: extra regeneration while below the HP threshold.
			int maxLife = Math.Max(Player.statLifeMax, Player.statLifeMax2);
			if (maxLife > 0 && Player.statLife < maxLife * EmergencyAidAugment.HealthThreshold
				&& SupportEffects.TryFindSupportOwner(Player, "emergency_aid", radius, out _, includeSelf: true))
			{
				Player.lifeRegen += EmergencyAidAugment.LifeRegenBonus;
			}
		}

		// Scapegoat. A player's debuffs belong to their own client, so the affected teammate's
		// client removes the debuff and asks the server to hand it to the Scapegoat owner's client.
		public override void PostUpdate()
		{
			if (Main.netMode != NetmodeID.MultiplayerClient || Player.whoAmI != Main.myPlayer || Player.dead)
				return;

			// Owners never redirect their own debuffs. That also stops two Scapegoat owners from
			// passing the same debuff back and forth.
			if (Player.GetModPlayer<AugmentPlayer>().HasAugment("scapegoat"))
				return;

			if (!SupportEffects.TryFindSupportOwner(Player, "scapegoat", SupportEffects.AuraRadius, out Player owner))
				return;

			for (int i = Player.buffType.Length - 1; i >= 0; i--)
			{
				int type = Player.buffType[i];
				int time = Player.buffTime[i];
				if (type <= 0 || time <= 0 || !SupportEffects.IsCleansableDebuff(type) || type == BuffID.TheTongue)
					continue;

				if (scapegoatLastSent.TryGetValue(type, out uint lastTick) && Main.GameUpdateCount - lastTick < ScapegoatRepeatTicks)
					continue;

				scapegoatLastSent[type] = Main.GameUpdateCount;
				Player.DelBuff(i);
				AugmentNet.SendScapegoatTransfer(owner.whoAmI, type, time);
			}
		}
	}
}
