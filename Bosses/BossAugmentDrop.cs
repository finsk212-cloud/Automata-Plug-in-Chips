using System.Collections.Generic;
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

			// A boss fight can be several NPCs (Twins, worm segments, Slime God's paladins, Exo Mech arms...).
			// BossFights works out which fight this NPC belongs to and whether its death should pay out;
			// the key is the fight leader's NPC type, which is also what participation and kill counts use.
			if (!BossFights.TryGetFightKey(npc, out int bossKey)) return;
			if (!BossFights.IsRewardTrigger(npc, bossKey)) return;

			RarityBracket bracket = BossTierMap.Brackets.ContainsKey(bossKey)
				? BossTierMap.GetBracket(bossKey)
				: GetModdedBossBracket(npc);

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
				if (Main.netMode == NetmodeID.Server)
					AugmentNet.SendSyncPlayer(player, player.whoAmI);
				AugmentAdvisoryHUD.TriggerSmartAdvisory(ap, "first_boss", "[POD 042]", "Combat analytics are online. Press [L] or click the DPS button on the right edge of your screen to review live damage stats and damage blocked.");

				ModContent.GetInstance<Augments>().Logger.Info($"Boss kill: {npc.TypeName} (key {bossKey}) kill #{killCount + 1} for {player.name}, {chance:P0} reward chance");

				if (chance <= 0f) continue;
				if (Main.rand.NextFloat() > chance) continue;

				ModContent.GetInstance<Augments>().Logger.Info($"Boss reward granted to {player.name} for {npc.TypeName}");
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

		// Calamity bosses by internal NPC name. Anything not listed falls back to the world-progress guess below.
		private static readonly Dictionary<string, RarityBracket> CalamityBrackets = new Dictionary<string, RarityBracket>
		{
			// Pre-Hardmode
			{ "DesertScourgeHead", RarityBracket.PreHardmode },
			{ "Crabulon", RarityBracket.PreHardmode },
			{ "HiveMind", RarityBracket.PreHardmode },
			{ "PerforatorHive", RarityBracket.PreHardmode },
			{ "SlimeGodCore", RarityBracket.PreHardmode },
			// Early Hardmode
			{ "Cryogen", RarityBracket.EarlyHardmode },
			{ "AquaticScourgeHead", RarityBracket.EarlyHardmode },
			// Post-Mechanical
			{ "BrimstoneElemental", RarityBracket.PostMechs },
			{ "CalamitasClone", RarityBracket.PostMechs },
			// Post-Plantera
			{ "Leviathan", RarityBracket.PostPlantera },
			{ "Anahita", RarityBracket.PostPlantera },
			{ "AstrumAureus", RarityBracket.PostPlantera },
			{ "PlaguebringerGoliath", RarityBracket.PostPlantera },
			{ "RavagerBody", RarityBracket.PostPlantera },
			// Post-Moon Lord
			{ "AstrumDeusHead", RarityBracket.EarlyPostMoonLord },
			{ "ProfanedGuardianCommander", RarityBracket.EarlyPostMoonLord },
			{ "Dragonfolly", RarityBracket.EarlyPostMoonLord },
			{ "Bumblebirb", RarityBracket.EarlyPostMoonLord }, // pre-rename class name
			// Late Endgame
			{ "Providence", RarityBracket.LatePostMoonLord },
			{ "StormWeaverHead", RarityBracket.LatePostMoonLord },
			{ "CeaselessVoid", RarityBracket.LatePostMoonLord },
			{ "Signus", RarityBracket.LatePostMoonLord },
			{ "Polterghast", RarityBracket.LatePostMoonLord },
			{ "OldDuke", RarityBracket.LatePostMoonLord },
			{ "DevourerofGodsHead", RarityBracket.LatePostMoonLord },
			{ "Yharon", RarityBracket.LatePostMoonLord },
			// Final Protocol
			{ "SupremeCalamitas", RarityBracket.FinalCalamity },
			{ "AresBody", RarityBracket.FinalCalamity },
			{ "ThanatosHead", RarityBracket.FinalCalamity },
			{ "Apollo", RarityBracket.FinalCalamity },
			{ "Artemis", RarityBracket.FinalCalamity },
		};

		private static RarityBracket GetModdedBossBracket(NPC npc)
		{
			if (npc.ModNPC?.Mod?.Name == "CalamityMod")
			{
				if (CalamityBrackets.TryGetValue(npc.ModNPC.Name, out RarityBracket calamityBracket))
					return calamityBracket;
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
