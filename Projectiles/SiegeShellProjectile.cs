using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace Augments
{
    // Shell fired by Siege Battery. It flies at the target, then turns into a short-lived explosion hitbox so the
    // splash goes through the normal hit path (damage numbers, Combat Analytics, multiplayer) like a vanilla bomb.
    public class SiegeShellProjectile : ModProjectile
    {
        private const int ExplosionSize = 110;
        private const int ExplosionTicks = 5;
        private const float HomingStrength = 0.08f;
        private const float SearchRadius = 400f;

        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.Bomb;

        // ai[0] is 1 once the shell has exploded. It is synced so every client draws the blast at the same moment.
        private bool Exploded => Projectile.ai[0] == 1f;
        private bool blastShown;

        public override void SetDefaults()
        {
            var tag = Projectile.GetGlobalProjectile<AugmentProjectileTag>();
            tag.IsAugmentProcDamage = true;
            tag.SourceAugmentId = "siege_battery";
            tag.CanTriggerOnHitAugments = false;

            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = -1;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 90;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI()
        {
            if (Exploded)
            {
                ShowBlast();
                Projectile.velocity = Vector2.Zero;
                return;
            }

            NPC target = GetTarget();
            if (target != null)
            {
                float speed = Projectile.velocity.Length();
                Vector2 desired = Projectile.DirectionTo(target.Center) * speed;
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, HomingStrength);
            }

            Projectile.rotation += 0.25f;

            Lighting.AddLight(Projectile.Center, 0.5f, 0.3f, 0.05f);
            Dust trail = Dust.NewDustPerfect(Projectile.Center, DustID.Torch, -Projectile.velocity * 0.15f, 100, default, 1.2f);
            trail.noGravity = true;

            // Ran out of flight time without touching anything: go off where it is.
            if (Projectile.timeLeft <= 2)
                Detonate();
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            Detonate();
        }

        public override bool OnTileCollide(Vector2 oldVelocity)
        {
            Detonate();
            return false;
        }

        private void Detonate()
        {
            if (Exploded)
                return;

            Projectile.ai[0] = 1f;
            Projectile.velocity = Vector2.Zero;
            Projectile.tileCollide = false;
            Projectile.timeLeft = ExplosionTicks;
            GrowToBlastSize();
            Projectile.netUpdate = true;
            ShowBlast();
        }

        // Blast sound and dust, once per client. Non-owners reach it through the synced ai[0].
        private void ShowBlast()
        {
            if (blastShown)
                return;

            blastShown = true;

            if (Projectile.width != ExplosionSize)
                GrowToBlastSize();

            if (Main.dedServ)
                return;

            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.8f, Pitch = -0.1f }, Projectile.Center);

            for (int i = 0; i < 20; i++)
            {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Torch, Main.rand.NextVector2Circular(7f, 7f), 100, default, 1.9f);
                d.noGravity = true;
            }

            for (int i = 0; i < 10; i++)
            {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Smoke, Main.rand.NextVector2Circular(4f, 4f), 100, Color.DarkGray, 1.5f);
                d.noGravity = true;
            }

            Lighting.AddLight(Projectile.Center, 1f, 0.6f, 0.1f);
        }

        private void GrowToBlastSize()
        {
            Vector2 center = Projectile.Center;
            Projectile.width = ExplosionSize;
            Projectile.height = ExplosionSize;
            Projectile.Center = center;
        }

        // ai[1] holds the NPC the sentry aimed at. If that one is gone, the shell homes on the biggest enemy nearby instead.
        private NPC GetTarget()
        {
            int index = (int)Projectile.ai[1];
            if (index >= 0 && index < Main.maxNPCs)
            {
                NPC aimed = Main.npc[index];
                if (aimed.CanBeChasedBy(Projectile))
                    return aimed;
            }

            return FindFallbackTarget();
        }

        private NPC FindFallbackTarget()
        {
            NPC best = null;
            float bestLife = -1f;

            foreach (NPC npc in Main.npc)
            {
                if (!npc.CanBeChasedBy(Projectile))
                    continue;

                if (Vector2.DistanceSquared(Projectile.Center, npc.Center) > SearchRadius * SearchRadius)
                    continue;

                if (npc.life > bestLife)
                {
                    best = npc;
                    bestLife = npc.life;
                }
            }

            return best;
        }
    }
}
