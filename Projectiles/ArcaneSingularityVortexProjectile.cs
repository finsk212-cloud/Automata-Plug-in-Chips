using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace Augments
{
    public class ArcaneSingularityVortexProjectile : ModProjectile
    {
        private const float PullRadius = 300f;
        private const float DamageRadius = 90f;
        private const int DamageIntervalTicks = 30;

        public override string Texture => "Terraria/Images/MagicPixel";

        public override void SetDefaults()
        {
            AugmentProjectileTag tag = Projectile.GetGlobalProjectile<AugmentProjectileTag>();
            tag.IsAugmentProcDamage = true;
            tag.SourceAugmentId = "arcane_singularity";
            Projectile.width = (int)(DamageRadius * 2f);
            Projectile.height = (int)(DamageRadius * 2f);
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 180;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = DamageIntervalTicks;
        }

        public override bool PreDraw(ref Color lightColor) => false;

        public override bool? CanDamage() => Projectile.localAI[1] == 1f;

        public override void AI()
        {
            Projectile.velocity = Vector2.Zero;
            Projectile.localAI[0]++;

            PullNearbyEnemies();
            SpawnVortexDust();
            Lighting.AddLight(Projectile.Center, 0.18f, 0.04f, 0.28f);

            if ((int)Projectile.localAI[0] % DamageIntervalTicks == 0)
            {
                Projectile.localAI[1] = 1f;
                Projectile.Damage();
                Projectile.localAI[1] = 0f;
                SoundEngine.PlaySound(SoundID.Item103 with { Volume = 0.25f, Pitch = -0.6f }, Projectile.Center);
            }
        }

        // Directly mutates npc.velocity, so this must only run on whichever
        // side is authoritative for NPCs (singleplayer or the dedicated
        // server) - same guard AugmentPlayer/SupportEffects use everywhere
        // else. Running this on a MultiplayerClient would only touch that
        // client's local prediction copy of the NPC, which the next NPC sync
        // packet from the server immediately overwrites - the pull would
        // never actually take effect for anyone, same bug ShockwaveAugment
        // had before it switched to SimpleStrikeNPC.
        private void PullNearbyEnemies()
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            foreach (NPC npc in Main.npc)
            {
                if (!npc.active || npc.friendly || npc.townNPC || npc.dontTakeDamage)
                    continue;

                Vector2 toCore = Projectile.Center - npc.Center;
                float distance = toCore.Length();
                if (distance <= DamageRadius || distance > PullRadius || distance == 0f)
                    continue;

                float pullStrength = MathHelper.Lerp(0.05f, 0.14f, 1f - distance / PullRadius);
                if (npc.boss)
                    pullStrength *= 0.3f;

                Vector2 desiredVelocity = toCore / distance * 9f;
                npc.velocity = Vector2.Lerp(npc.velocity, desiredVelocity, pullStrength);
            }
        }

        private void SpawnVortexDust()
        {
            for (int i = 0; i < 2; i++)
            {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float radius = Main.rand.NextFloat(35f, PullRadius);
                Vector2 offset = angle.ToRotationVector2() * radius;
                Vector2 inwardVelocity = -offset.SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(2f, 5f);
                Vector2 tangentVelocity = (angle + MathHelper.PiOver2).ToRotationVector2() * 1.5f;

                Dust dust = Dust.NewDustPerfect(
                    Projectile.Center + offset,
                    Main.rand.NextBool(3) ? DustID.GemAmethyst : DustID.Shadowflame,
                    inwardVelocity + tangentVelocity,
                    90,
                    default,
                    Main.rand.NextFloat(0.8f, 1.25f)
                );
                dust.noGravity = true;
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            float nearestX = MathHelper.Clamp(Projectile.Center.X, targetHitbox.Left, targetHitbox.Right);
            float nearestY = MathHelper.Clamp(Projectile.Center.Y, targetHitbox.Top, targetHitbox.Bottom);
            Vector2 nearestPoint = new Vector2(nearestX, nearestY);
            return Vector2.DistanceSquared(Projectile.Center, nearestPoint) <= DamageRadius * DamageRadius;
        }

        public override void OnKill(int timeLeft)
        {
            for (int i = 0; i < 24; i++)
            {
                float angle = MathHelper.TwoPi * i / 24f;
                Dust dust = Dust.NewDustPerfect(
                    Projectile.Center,
                    i % 3 == 0 ? DustID.GemDiamond : DustID.Shadowflame,
                    angle.ToRotationVector2() * Main.rand.NextFloat(2f, 6f),
                    70,
                    default,
                    1.15f
                );
                dust.noGravity = true;
            }
        }
    }
}
