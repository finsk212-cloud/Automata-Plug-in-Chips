using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace Augments
{
    // The invisible, player-sized hitbox that makes a star-powered player hurt whatever they touch, and the
    // source of the rainbow trail. ai[0] = ticks of Star Power left (synced when refreshed).
    //
    // Because this projectile exists on every player's game, it also plays the pickup sound for everyone: once when
    // it appears, and again whenever the timer jumps back up because the owner picked up another star.
    public class StarPowerAuraProjectile : ModProjectile
    {
        public override string Texture => "Terraria/Images/MagicPixel";

        // The timer value this game saw on the previous tick. -1 until the first tick.
        private float previousTimer = -1f;

        public override void SetDefaults()
        {
            AugmentProjectileTag tag = Projectile.GetGlobalProjectile<AugmentProjectileTag>();
            tag.IsAugmentProcDamage = true;
            tag.SourceAugmentId = "star_power";
            tag.CanTriggerOnHitAugments = false;
            tag.PreventEchoChamberCopy = true;
            tag.PreventRicochetEngineCopy = true;

            Projectile.width = 34;
            Projectile.height = 54;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 1000;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = StarPowerAugment.TouchCooldownTicks;
        }

        public override void AI()
        {
            Player owner = Main.player[Projectile.owner];
            if (!owner.active || owner.dead)
            {
                Projectile.Kill();
                return;
            }

            Projectile.Center = owner.Center;
            StarPowerAugment.MarkActive(Projectile.owner);

            // The start sound (and a refresh sound when the timer jumps up), on every game.
            if (!Main.dedServ && (previousTimer < 0f || Projectile.ai[0] > previousTimer + 0.5f))
            {
                bool refresh = previousTimer >= 0f;
                SoundEngine.PlaySound(SoundID.Item4 with { Volume = refresh ? 0.5f : 0.7f, Pitch = 0.6f }, owner.Center);
            }

            Projectile.ai[0] -= 1f;
            previousTimer = Projectile.ai[0];
            if (Projectile.ai[0] <= 0f)
            {
                Projectile.Kill();
                return;
            }

            // Rainbow trail, thicker the faster the player moves. Flickers out over the last second.
            bool fading = Projectile.ai[0] < 60f && (int)Projectile.ai[0] % 8 < 4;
            if (!fading)
            {
                int count = owner.velocity.Length() > 3f ? 3 : 1;
                for (int i = 0; i < count; i++)
                {
                    float hue = (Main.GlobalTimeWrappedHourly * 1.8f + Main.rand.NextFloat(0.25f)) % 1f;
                    Vector2 spot = owner.Center + new Vector2(Main.rand.NextFloat(-14f, 14f), Main.rand.NextFloat(-24f, 24f));
                    Dust d = Dust.NewDustPerfect(spot, DustID.RainbowMk2, -owner.velocity * 0.15f, 0, Main.hslToRgb(hue, 1f, 0.6f), 1.2f);
                    d.noGravity = true;
                }
            }

            Lighting.AddLight(owner.Center, 0.7f, 0.6f, 0.9f);
        }

        // Drawn entirely with dust.
        public override bool PreDraw(ref Color lightColor) => false;
    }
}
