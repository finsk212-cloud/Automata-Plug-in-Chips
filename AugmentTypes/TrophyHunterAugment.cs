using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace Augments
{
	public class TrophyHunterAugment : Augment
	{
		public override string Id => "trophy_hunter";
		public override bool RunsKillHookOnKillerClient => true;
		public override string DisplayName => "Trophy Hunter";
		public override string Description =>
			$"The first melee kill against each unique enemy type permanently grants {AugmentText.BonusDamage("+0.25% melee damage")}.\n" +
			AugmentText.Note("(Permanent bonus with no stack cap.)");

		public override AugmentRarity Rarity => AugmentRarity.Epic;
		public override AugmentClass Class => AugmentClass.Melee;

		private const float BonusPerType = 0.0025f;

		public override int? StatusValue
		{
			get
			{
				int count = Main.LocalPlayer.GetModPlayer<AugmentPlayer>().TrophyHunterKilledTypes.Count;
				return count > 0 ? (int)System.Math.Round(count * BonusPerType * 100f) : (int?)null;
			}
		}
		public override string StatusValueSuffix => "%";
		public override Microsoft.Xna.Framework.Color StatusValueColor => AugmentTextColors.BonusDamage;

		public override void UpdateEquips(Player player)
		{
			int count = player.GetModPlayer<AugmentPlayer>().TrophyHunterKilledTypes.Count;
			if (count > 0)
				player.GetDamage(DamageClass.Melee) += count * BonusPerType;
		}

		public override void OnHitNPCWithItem(Player player, Item item, NPC target, NPC.HitInfo hit)
		{
			if (item.CountsAsClass(DamageClass.Melee))
				target.GetGlobalNPC<AugmentTrophyHunterNPC>().TagMeleeHit(player.whoAmI);
		}

		public override void OnHitNPCWithProj(Player player, Projectile proj, NPC target, NPC.HitInfo hit)
		{
			if (proj.CountsAsClass(DamageClass.Melee))
				target.GetGlobalNPC<AugmentTrophyHunterNPC>().TagMeleeHit(player.whoAmI);
		}

		public override void OnKillNPC(Player player, NPC npc)
		{
			if (!IsHostileEnemy(npc))
				return;

			var marker = npc.GetGlobalNPC<AugmentTrophyHunterNPC>();
			if (!marker.IsTaggedBy(player.whoAmI))
				return;

			marker.ClearTag();

			if (player.GetModPlayer<AugmentPlayer>().TrophyHunterKilledTypes.Add(npc.type))
				SoundEngine.PlaySound(SoundID.AchievementComplete, player.Center);
		}

		private static bool IsHostileEnemy(NPC npc) => !npc.friendly && npc.lifeMax > 5;
	}
}
