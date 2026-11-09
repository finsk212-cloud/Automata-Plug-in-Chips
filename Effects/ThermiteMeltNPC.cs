using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Augments
{
    // Tracks the "Thermite Melt" (Molten Slag) debuff inflicted by Floor is Lava's napalm fire.
    // While active, ignores 20 enemy defense and amplifies incoming ranged damage by +15% from all sources.
    public class ThermiteMeltNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        public const float DefensePierce = 20f;
        public const float RangedBonusDamageMultiplier = 1.15f; // +15%

        public int MeltTimer;

        public bool IsMelted => MeltTimer > 0;

        public void ApplyMelt(int durationTicks = 180)
        {
            MeltTimer = Math.Max(MeltTimer, durationTicks);
        }

        public override void ResetEffects(NPC npc)
        {
            if (MeltTimer > 0)
                MeltTimer--;
        }

        public override void ModifyIncomingHit(NPC npc, ref NPC.HitModifiers modifiers)
        {
            if (IsMelted)
            {
                modifiers.ArmorPenetration += DefensePierce;

                if (modifiers.DamageType == DamageClass.Ranged || modifiers.DamageType.CountsAsClass(DamageClass.Ranged))
                {
                    modifiers.FinalDamage *= RangedBonusDamageMultiplier;
                }
            }
        }

        public override void DrawEffects(NPC npc, ref Color drawColor)
        {
            if (!IsMelted || npc.friendly || npc.townNPC)
                return;

            // Sizzling molten orange dripping slag particles falling off the melting enemy
            if (Main.rand.NextBool(3))
            {
                Vector2 pos = npc.position + new Vector2(Main.rand.NextFloat(npc.width), Main.rand.NextFloat(npc.height));
                Dust d = Dust.NewDustPerfect(
                    pos,
                    DustID.OrangeTorch,
                    new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), Main.rand.NextFloat(1.2f, 2.8f)),
                    80,
                    default,
                    Main.rand.NextFloat(1.1f, 1.5f)
                );
                d.noGravity = false;
            }

            Lighting.AddLight(npc.Center, 0.45f, 0.20f, 0.04f);
        }
    }
}
