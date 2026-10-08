using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace Augments
{
	public class AugmentGlobalProjectile : GlobalProjectile
	{
		public override void OnSpawn(Projectile projectile, IEntitySource source)
		{
			AugmentProjectileTag projectileTag = projectile.GetGlobalProjectile<AugmentProjectileTag>();

			// Remember which weapon fired this so the DPS panel credits the real weapon
			// instead of whatever the player is holding when the hit lands.
			if (source is EntitySource_ItemUse weaponSource && weaponSource.Item != null && !weaponSource.Item.IsAir)
				projectileTag.SourceItemName = weaponSource.Item.Name;
			if (projectile.minion)
			{
				projectileTag.SourceMinionProjectileType = projectile.type;
				projectileTag.SourceMinionProjectileIdentity = projectile.identity;
			}

			// Proc projectiles sometimes spawn secondary projectiles of their own.
			// Carry provenance through that chain so child hits cannot silently
			// become normal attacks or lose Echo Chamber's effectiveness rules.
			if (source is EntitySource_Parent parentSource && parentSource.Entity is Projectile parentProjectile)
			{
				AugmentProjectileTag parentTag = parentProjectile.GetGlobalProjectile<AugmentProjectileTag>();
				if (projectileTag.SourceItemName == null)
					projectileTag.SourceItemName = parentTag.SourceItemName;
				if (parentTag.IsAugmentProcDamage)
				{
					projectileTag.SourceAugmentId ??= parentTag.SourceAugmentId;
					projectileTag.SourceProtocolId ??= parentTag.SourceProtocolId;
				}
				if (parentTag.SourceMinionProjectileType >= 0)
				{
					projectileTag.SourceMinionProjectileType = parentTag.SourceMinionProjectileType;
					projectileTag.SourceMinionProjectileIdentity = parentTag.SourceMinionProjectileIdentity;
				}

				if (parentTag.IsAugmentProcDamage)
				{
					AugmentProjectileTag childTag = projectileTag;
					childTag.IsAugmentProcDamage = true;
					childTag.CanTriggerOnHitAugments = parentTag.CanTriggerOnHitAugments;
					childTag.OnHitEffectiveness = parentTag.OnHitEffectiveness;
					childTag.PreventEchoChamberCopy = parentTag.PreventEchoChamberCopy;
					childTag.PreventRicochetEngineCopy = parentTag.PreventRicochetEngineCopy;
					childTag.IsEchoChamberEcho = parentTag.IsEchoChamberEcho;
					childTag.NecromancersCourtGhost = parentTag.NecromancersCourtGhost;
					childTag.NecromancersCourtGhostTimeLeft = parentTag.NecromancersCourtGhostTimeLeft;
				}
			}

			if (projectile.owner < 0 || projectile.owner >= Main.maxPlayers)
				return;

			Player player = Main.player[projectile.owner];

			if (!player.active)
				return;

			foreach (var augment in player.GetModPlayer<AugmentPlayer>().Owned)
			{
				augment.OnProjectileSpawn(player, projectile);

				if (source is EntitySource_ItemUse itemUse && itemUse.Entity == player)
					augment.OnShootProjectile(player, itemUse.Item, projectile);
			}
		}
	}
}
