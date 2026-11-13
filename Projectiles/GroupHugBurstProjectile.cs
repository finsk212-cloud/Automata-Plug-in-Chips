using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace Augments
{
    // The burst of Group Hug. Four brackets close in on the enemy like a hug, then it goes off. Hurts for its first few ticks,
    // then lingers so the effect can play out. Every game draws it, so other players see it too.
    public class GroupHugBurstProjectile : ModProjectile
    {
        private const int HitTicks = 3;
        private const int TotalTicks = 22;
        private const int CloseInTicks = 5;
        private const float BlastSize = 130f;   // about 4 tiles

        public override string Texture => "Terraria/Images/MagicPixel";

        private int age;

        public override void SetDefaults()
        {
            AugmentProjectileTag tag = Projectile.GetGlobalProjectile<AugmentProjectileTag>();
            tag.IsAugmentProcDamage = true;
            tag.SourceAugmentId = "pellet_burst";
            tag.CanTriggerOnHitAugments = false;
            tag.PreventEchoChamberCopy = true;
            tag.PreventRicochetEngineCopy = true;

            Projectile.width = (int)BlastSize;
            Projectile.height = (int)BlastSize;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = TotalTicks;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 30;
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
        {
            modifiers.HitDirectionOverride = target.Center.X >= Projectile.Center.X ? 1 : -1;
        }

        public override void AI()
        {
            if (age == 0 && !Main.dedServ)
            {
                SoundEngine.PlaySound(SoundID.Item62 with { Volume = 0.55f, Pitch = 0.35f }, Projectile.Center);
                for (int i = 0; i < 10; i++)
                {
                    Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.WhiteTorch, Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(2f, 5f), 100, default, 1.1f);
                    d.noGravity = true;
                }
            }

            age++;
            if (age > HitTicks)
                Projectile.friendly = false;

            Lighting.AddLight(Projectile.Center, 0.6f, 0.7f, 0.8f);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Vector2 center = Projectile.Center;
            int seed = Projectile.identity;
            float t = age / (float)TotalTicks;
            float ease = 1f - (1f - t) * (1f - t);
            float fade = 1f - t;

            // The hug: four brackets close in from the corners, then flash and fade.
            float closing = MathHelper.Clamp(age / (float)CloseInTicks, 0f, 1f);
            float corner = MathHelper.Lerp(54f, 16f, closing * closing);
            float bracketFade = age < CloseInTicks ? 1f : 1f - (age - CloseInTicks) / (float)(TotalTicks - CloseInTicks);
            Color bracket = AutomataFx.White * bracketFade;
            DangerCloseVisuals.Bracket(center + new Vector2(-corner, -corner), 1f, 1f, 9f, 2f, bracket);
            DangerCloseVisuals.Bracket(center + new Vector2(corner, -corner), -1f, 1f, 9f, 2f, bracket);
            DangerCloseVisuals.Bracket(center + new Vector2(-corner, corner), 1f, -1f, 9f, 2f, bracket);
            DangerCloseVisuals.Bracket(center + new Vector2(corner, corner), -1f, -1f, 9f, 2f, bracket);

            // A flash of squares at the moment of the squeeze.
            if (age >= CloseInTicks - 1 && age < CloseInTicks + 4)
                AutomataFx.Rect(center, 40f, 40f, MathHelper.PiOver4, AutomataFx.Cyan * 0.5f);

            // A dashed ring spreading out, and squares flying off.
            AutomataFx.DashedRing(center, 8f + (BlastSize * 0.5f) * ease, 10, 0.55f, age * 0.12f, 2.5f, AutomataFx.Cyan * fade);
            for (int i = 0; i < 6; i++)
            {
                float angle = i * MathHelper.TwoPi / 6f + AutomataFx.Rand(seed, i, 3) * 0.6f;
                float distance = (16f + 40f * AutomataFx.Rand(seed, i, 4)) * ease + 6f;
                float size = 3f + 3f * AutomataFx.Rand(seed, i, 5);
                AutomataFx.Rect(center + angle.ToRotationVector2() * distance, size, size, angle + age * 0.15f, AutomataFx.Pink * fade);
            }

            return false;
        }
    }
}
