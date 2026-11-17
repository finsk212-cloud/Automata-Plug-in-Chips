using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace Augments
{
	// The little bolt the Pea Shooter's Pod fires. It is drawn from plain squares (no texture file): a bright core with a soft glow, the
	// colour of Pod 218's LED.
	public class PodBoltProjectile : ModProjectile
	{
		public override string Texture => "Terraria/Images/MagicPixel";

		private static readonly Color Glow = new Color(90, 220, 255);

		public override void SetDefaults()
		{
			// Marked as plugin damage, like the other plugin projectiles, so a bolt every second never sets off other plugins' on-hit effects.
			Projectile.GetGlobalProjectile<AugmentProjectileTag>().IsAugmentProcDamage = true;
			Projectile.width = 8;
			Projectile.height = 8;
			Projectile.friendly = true;
			Projectile.hostile = false;
			Projectile.DamageType = DamageClass.Generic;
			Projectile.penetrate = 1;
			Projectile.timeLeft = 70;
			Projectile.aiStyle = -1;
			Projectile.tileCollide = true;
			Projectile.ignoreWater = true;
			Projectile.alpha = 0;
		}

		public override void AI()
		{
			Projectile.rotation = Projectile.velocity.ToRotation();
			Lighting.AddLight(Projectile.Center, 0.1f, 0.35f, 0.45f);

			if (Main.rand.NextBool(3))
			{
				Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Electric, Vector2.Zero, 100, Glow, 0.6f);
				d.noGravity = true;
				d.velocity *= 0.1f;
			}
		}

		public override void OnKill(int timeLeft)
		{
			for (int i = 0; i < 6; i++)
			{
				Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Electric, Main.rand.NextVector2Circular(2f, 2f), 100, Glow, 0.8f);
				d.noGravity = true;
			}
		}

		public override bool PreDraw(ref Color lightColor)
		{
			Texture2D pixel = TextureAssets.MagicPixel.Value;
			Vector2 pos = Projectile.Center - Main.screenPosition;
			var src = new Rectangle(0, 0, 1, 1);
			var origin = new Vector2(0.5f, 0.5f);

			Main.EntitySpriteDraw(pixel, pos, src, Glow * 0.35f, Projectile.rotation, origin, new Vector2(16f, 8f), SpriteEffects.None, 0);
			Main.EntitySpriteDraw(pixel, pos, src, Glow, Projectile.rotation, origin, new Vector2(12f, 4f), SpriteEffects.None, 0);
			Main.EntitySpriteDraw(pixel, pos, src, Color.White, Projectile.rotation, origin, new Vector2(8f, 2f), SpriteEffects.None, 0);
			return false;
		}
	}
}