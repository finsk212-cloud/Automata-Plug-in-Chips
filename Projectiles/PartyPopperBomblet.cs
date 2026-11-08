using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Augments.Core;

namespace Augments
{
    public class PartyPopperBomblet : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.PartyGirlGrenade;

        public override void SetDefaults()
        {
            AugmentProjectileTag tag = Projectile.GetGlobalProjectile<AugmentProjectileTag>();
            tag.IsAugmentProcDamage = true;
            tag.SourceProtocolId = AugmentFamilyRegistry.MarksmanId;
            tag.CanTriggerOnHitAugments = false;
            tag.PreventEchoChamberCopy = true;
            tag.PreventRicochetEngineCopy = true;

            Projectile.width = 12;
            Projectile.height = 12;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 90; // 1.5s
            Projectile.tileCollide = true;
            Projectile.ignoreWater = false;
        }

        public override void AI()
        {
            Projectile.velocity.Y += 0.28f; // Gravity
            Projectile.rotation += Projectile.velocity.X * 0.08f;

            // Festive confetti trail
            if (Main.rand.NextBool(3))
            {
                Dust d = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.Confetti,
                    Projectile.velocity * -0.2f + Main.rand.NextVector2Circular(1f, 1f),
                    0,
                    default,
                    Main.rand.NextFloat(0.7f, 1.1f)
                );
                d.noGravity = true;
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity)
        {
            // Bounce up to 2 times
            if (Projectile.ai[0] < 2)
            {
                Projectile.ai[0]++;
                SoundEngine.PlaySound(SoundID.Dig with { Pitch = 0.4f, Volume = 0.5f }, Projectile.position);
                if (Math.Abs(Projectile.velocity.X - oldVelocity.X) > float.Epsilon)
                    Projectile.velocity.X = -oldVelocity.X * 0.65f;
                if (Math.Abs(Projectile.velocity.Y - oldVelocity.Y) > float.Epsilon)
                    Projectile.velocity.Y = -oldVelocity.Y * 0.65f;
                return false;
            }

            return true; // Explode on 3rd bounce
        }

        public override void OnKill(int timeLeft)
        {
            SoundEngine.PlaySound(SoundID.Item14 with { Pitch = 0.4f, Volume = 0.7f }, Projectile.position);

            // Blast radius damage
            if (Projectile.owner == Main.myPlayer)
            {
                const float BlastRadius = 60f;
                for (int i = 0; i < Main.maxNPCs; i++)
                {
                    NPC npc = Main.npc[i];
                    if (npc.active && !npc.friendly && !npc.dontTakeDamage && npc.lifeMax > 5)
                    {
                        if (Vector2.Distance(npc.Center, Projectile.Center) <= BlastRadius)
                        {
                            var hitInfo = new NPC.HitInfo
                            {
                                Damage = Projectile.damage,
                                SourceDamage = Projectile.damage,
                                HitDirection = npc.Center.X >= Projectile.Center.X ? 1 : -1,
                                DamageType = DamageClass.Ranged,
                                Knockback = 3f,
                                HideCombatText = false
                            };
                            npc.StrikeNPC(hitInfo);
                            if (Main.netMode != NetmodeID.SinglePlayer)
                                NetMessage.SendStrikeNPC(npc, in hitInfo);
                        }
                    }
                }
            }

            // Confetti blast visual
            for (int i = 0; i < 20; i++)
            {
                Vector2 speed = Main.rand.NextVector2Circular(5f, 5f);
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Confetti, speed, 0, default, Main.rand.NextFloat(0.8f, 1.3f));
                d.noGravity = true;
            }
            for (int i = 0; i < 8; i++)
            {
                Dust smoke = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Smoke, 0f, 0f, 100, default, 1f);
                smoke.velocity *= 1.4f;
            }
        }
    }
}
