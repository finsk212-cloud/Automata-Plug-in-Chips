using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace Augments
{
    // The lightning bolt of Grand Entrance. It sits where the new minion appeared, hurts everything around it for a few ticks,
    // then stays a little longer so the effect can play out. Every game draws it (a white-and-cyan bolt from the sky, then a
    // YoRHa-style dashed shockwave and flying squares), so other players see the entrance too.
    public class GrandEntranceBoltProjectile : ModProjectile
    {
        private const int HitTicks = 5;       // the bolt only hurts for its first moments
        private const int TotalTicks = 34;
        private const int BoltTicks = 9;      // how long the bolt itself is visible
        private const float BlastSize = 220f; // about 7 tiles either way
        private const float SkyHeight = 760f;

        public override string Texture => "Terraria/Images/MagicPixel";

        // Per-game tick counter (not synced: every game counts its own, which is all the visuals need).
        private int age;

        public override void SetDefaults()
        {
            AugmentProjectileTag tag = Projectile.GetGlobalProjectile<AugmentProjectileTag>();
            tag.IsAugmentProcDamage = true;
            tag.SourceAugmentId = "grand_entrance";
            tag.CanTriggerOnHitAugments = false;
            tag.PreventEchoChamberCopy = true;
            tag.PreventRicochetEngineCopy = true;

            Projectile.width = (int)BlastSize;
            Projectile.height = (int)BlastSize;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = TotalTicks;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 60;
        }

        // Walls stop the bolt: an enemy is only hit if there is a clear line between it and the point of impact,
        // so the blast no longer reaches enemies on the other side of blocks. (Returning null leaves the normal rules alone.)
        public override bool? CanHitNPC(NPC target)
        {
            return Collision.CanHitLine(Projectile.Center, 1, 1, target.position, target.width, target.height) ? null : false;
        }

        // Knock enemies away from the point of impact, whichever side of it they are on.
        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
        {
            modifiers.HitDirectionOverride = target.Center.X >= Projectile.Center.X ? 1 : -1;
        }

        public override void AI()
        {
            if (age == 0 && !Main.dedServ)
            {
                SoundEngine.PlaySound(SoundID.Item94 with { Volume = 0.8f, Pitch = 0.1f }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.45f, Pitch = 0.4f }, Projectile.Center);

                for (int i = 0; i < 26; i++)
                {
                    Vector2 velocity = Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(3f, 9f);
                    Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Electric, velocity, 100, default, 1.2f);
                    d.noGravity = true;
                }
            }

            age++;
            if (age > HitTicks)
                Projectile.friendly = false;

            if (age < BoltTicks)
                Lighting.AddLight(Projectile.Center, 0.9f, 1.0f, 1.2f);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Vector2 center = Projectile.Center;
            int seed = Projectile.identity;
            float t = age / (float)TotalTicks;
            float ease = 1f - (1f - t) * (1f - t);

            // A white flash of squares right at the moment of impact.
            if (age < 5)
            {
                float flash = 1f - age / 5f;
                AutomataFx.Rect(center, 70f * (1f + age * 0.4f), 70f * (1f + age * 0.4f), MathHelper.PiOver4, AutomataFx.White * (0.55f * flash));
            }

            // The bolt: a jagged line from the sky, glowing cyan with a white core, flickering as it fades.
            if (age < BoltTicks)
            {
                float fade = 1f - age / (float)BoltTicks;
                float flicker = age % 2 == 0 ? 1f : 0.65f;
                const int segments = 9;

                Vector2 previous = center + new Vector2((AutomataFx.Rand(seed, 99, 1) - 0.5f) * 60f, -SkyHeight);
                for (int k = 1; k <= segments; k++)
                {
                    float f = k / (float)segments;
                    Vector2 point = k == segments
                        ? center
                        : new Vector2(
                            center.X + (AutomataFx.Rand(seed, k, 2) - 0.5f) * 90f * (1f - f),
                            MathHelper.Lerp(center.Y - SkyHeight, center.Y, f));

                    DangerCloseVisuals.Line(previous, point, 15f * fade + 3f, AutomataFx.Cyan * (0.45f * fade * flicker));
                    DangerCloseVisuals.Line(previous, point, 5f * fade + 1.5f, AutomataFx.White * (fade * flicker));
                    previous = point;
                }
            }

            // The shockwave: two dashed rings spinning against each other, and squares flung outwards.
            float ringFade = 1f - t;
            AutomataFx.DashedRing(center, 16f + (BlastSize * 0.5f) * ease, 14, 0.6f, age * 0.07f, 3f, AutomataFx.Cyan * ringFade);
            AutomataFx.DashedRing(center, 10f + (BlastSize * 0.34f) * ease, 9, 0.5f, -age * 0.09f, 2f, AutomataFx.Pink * (0.8f * ringFade));

            for (int i = 0; i < 10; i++)
            {
                float angle = i * MathHelper.TwoPi / 10f + AutomataFx.Rand(seed, i, 3) * 0.5f;
                float distance = (30f + 100f * AutomataFx.Rand(seed, i, 4)) * ease + 8f;
                float size = 4f + 5f * AutomataFx.Rand(seed, i, 5);
                AutomataFx.Rect(center + angle.ToRotationVector2() * distance, size, size, angle + age * 0.1f, AutomataFx.White * ringFade);
            }

            // A short terminal-style tag over the spot, as if the unit just logged in.
            if (age < 26)
            {
                float textFade = age < 18 ? 1f : 1f - (age - 18) / 8f;
                AutomataFx.TextLeft("UNIT ONLINE", center + new Vector2(-46f, -BlastSize * 0.5f - 6f), AutomataFx.Green * textFade, 0.8f);
            }

            return false;
        }
    }
}
