using Terraria;
using Terraria.ModLoader;

namespace Augments
{
    public class AugmentFocusDirectiveNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        private int tagTimer;

        public bool IsTagged => tagTimer > 0;

        public void ApplyTag(int durationTicks)
        {
            if (durationTicks > tagTimer)
                tagTimer = durationTicks;
        }

        public override void PostAI(NPC npc)
        {
            if (tagTimer > 0)
                tagTimer--;
        }

        public override void ModifyIncomingHit(NPC npc, ref NPC.HitModifiers modifiers)
        {
            if (IsTagged && (modifiers.DamageType == DamageClass.Summon || modifiers.DamageType == DamageClass.SummonMeleeSpeed))
            {
                modifiers.FlatBonusDamage += FocusDirectiveAugment.TagDamage;
            }
        }
    }
}
