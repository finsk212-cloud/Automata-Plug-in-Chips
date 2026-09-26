using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Augments
{
	public class BossAugmentDrop : GlobalNPC
	{
		// Runs server-side / singleplayer whenever any NPC dies.
		public override void OnKill(NPC npc)
		{
			if (Main.netMode == NetmodeID.MultiplayerClient) return;
			if (!npc.boss) return;

			// Twins are two separate NPCs but one fight — normalize both segments
			// to Retinazer's type so participation and kill count treat them as one boss.
			// BossTierMap still uses npc.type directly (it maps both to the same bracket).
			int bossKey = (npc.type == NPCID.Spazmatism) ? NPCID.Retinazer : npc.type;

			RarityBracket bracket = BossTierMap.GetBracket(npc.type);
			if (!BossTierMap.Brackets.ContainsKey(npc.type))
			{
				bracket = GetModdedBossBracket(npc);
			}

			foreach (Player player in Main.player)
			{
				if (!player.active) continue;

				AugmentPlayer ap = player.GetModPlayer<AugmentPlayer>();

				// Participation check — works in both singleplayer and multiplayer.
				// In multiplayer, the server's copy of DamagedBossesThisFight is populated
				// via BossDamageParticipation packets sent by each client on first hit.
				// See AugmentPlayer.TryRegisterBossDamage.
				if (!ap.DamagedBossesThisFight.Contains(bossKey))
					continue;

				// KILL COUNT GATING (keyed by bossKey so Twins share one counter):
				ap.BossAugmentKills.TryGetValue(bossKey, out int killCount);

				float chance = killCount switch
				{
					0 => 1.00f,  // 1st kill: guaranteed
					1 => 0.30f,  // 2nd kill: 30%
					2 => 0.10f,  // 3rd kill: 10%
					_ => 0.00f   // 4th+: never
				};

				// Increment BEFORE rolling — farming to win the random roll still
				// advances the counter, preventing indefinite retry loops.
				ap.BossAugmentKills[bossKey] = killCount + 1;
				AugmentAdvisoryHUD.TriggerSmartAdvisory(ap, "first_boss", "[POD 042]", "Combat analytics are online. Press [L] or click the DPS button on the right edge of your screen to review live damage stats and damage blocked.");

				if (chance <= 0f) continue;
				if (Main.rand.NextFloat() > chance) continue;

				AugmentRewardLogic.GrantReward(player, bracket);
			}

			// Clear server-side participation for this boss key across all players
			// so the next fight starts fresh (regardless of whether they got a reward).
			foreach (Player player in Main.player)
			{
				if (!player.active) continue;
				player.GetModPlayer<AugmentPlayer>().DamagedBossesThisFight.Remove(bossKey);
			}
		}

		private static RarityBracket GetModdedBossBracket(NPC npc)
		{
			if (npc.ModNPC?.Mod?.Name == "CalamityMod")
			{
				switch (npc.ModNPC.Name)
				{
					case "SupremeCalamitas":
					case "AresBody":
					case "ThanatosHead":
					case "Apollo":
					case "Artemis":
						return RarityBracket.FinalCalamity;
					case "DevourerofGodsHead":
					case "Yharon":
						return RarityBracket.LatePostMoonLord;
				}
			}

			if (!Main.hardMode)
				return RarityBracket.PreHardmode;
			if (!NPC.downedMechBossAny)
				return RarityBracket.EarlyHardmode;
			if (!NPC.downedPlantBoss)
				return RarityBracket.PostMechs;
			if (!NPC.downedMoonlord)
				return RarityBracket.PostPlantera;
			return RarityBracket.EarlyPostMoonLord;
		}
	}
}
