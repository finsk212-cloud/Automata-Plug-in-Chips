using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace Augments
{
	// The Pea Shooter plugin's Pod upgrade ("Pod 218"). It is the behaviour of the Pod on the owner's shoulder, not a separate drone.
	// Only the owner's machine runs it: it spawns an ordinary friendly projectile owned by the player, and the game already syncs those to
	// everyone else, so nothing extra is sent over the network.
	internal sealed class PodShooter
	{
		public const int BaseDamage = 5;
		private const int ShotTicks = 60;          // one shot per second
		private const float RangePixels = 560f;    // about 35 tiles
		private const float BoltSpeed = 11f;
		private const float DamageBonusShare = 0.5f; // only half of the owner's damage bonuses carry over, so it stays a helper

		private int timer;

		internal void Reset()
		{
			timer = 0;
		}

		// Once per game tick, on the owner's own machine, while they have the Pea Shooter plugin. `from` is where the Pod is.
		internal void Update(Player owner, Vector2 from)
		{
			if (timer < ShotTicks)
			{
				timer++;
				return;
			}

			// Ready: it waits here until an enemy shows up, then fires at once and starts counting again.
			NPC target = FindTarget(from);
			if (target == null)
				return;

			timer = 0;
			Vector2 aim = target.Center - from;
			if (aim.LengthSquared() < 0.01f)
				aim = new Vector2(owner.direction, 0f);
			aim.Normalize();

			float bonus = owner.GetTotalDamage(DamageClass.Generic).ApplyTo(1f);
			int damage = Math.Max(1, (int)Math.Round(BaseDamage * (1f + (bonus - 1f) * DamageBonusShare)));

			Projectile.NewProjectile(owner.GetSource_Misc("PodPeaShooter"), from, aim * BoltSpeed,
				ModContent.ProjectileType<PodBoltProjectile>(), damage, 0.5f, owner.whoAmI);
			SoundEngine.PlaySound(SoundID.Item12 with { Volume = 0.25f, Pitch = 0.6f, MaxInstances = 3 }, from);
		}

		// The closest enemy in range that the Pod has a clear line to.
		private static NPC FindTarget(Vector2 from)
		{
			NPC best = null;
			float bestDist = RangePixels * RangePixels;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (!n.active || !n.CanBeChasedBy())
					continue;

				float d = Vector2.DistanceSquared(from, n.Center);
				if (d >= bestDist)
					continue;
				if (!Collision.CanHitLine(from, 1, 1, n.position, n.width, n.height))
					continue;

				bestDist = d;
				best = n;
			}
			return best;
		}
	}
}