using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Augments
{
    public class OverwhelmAugment : Augment
    {
        public override string Id => "overwhelm";
        public override string DisplayName => "Overwhelm";
        public override string Description =>
            $"Hitting the same enemy {AugmentText.Trigger("3 times")} within {AugmentText.Duration("1.5 seconds")} applies " +
            $"{AugmentText.Immobilize("Confused")} for {AugmentText.Duration("1 second")}. Switching targets or waiting too long resets the count.";

        public override AugmentRarity Rarity => AugmentRarity.Rare;
        public override AugmentClass Class => AugmentClass.Melee;

        private const int ResetWindowTicks = 90;
        private const int ConfusedDurationTicks = 60;
        private const int HitsToTrigger = 3;

        public override void OnUpdate(Player player)
        {
            var ap = player.GetModPlayer<AugmentPlayer>();
            if (ap.OverwhelmResetTimer <= 0)
                return;

            ap.OverwhelmResetTimer--;
            if (ap.OverwhelmResetTimer == 0)
            {
                ap.OverwhelmHitCounter = 0;
                ap.OverwhelmLastTargetWhoAmI = -1;
            }
        }

        public override void OnHitNPCWithItem(Player player, Item item, NPC target, NPC.HitInfo hit)
        {
            if (item.CountsAsClass(DamageClass.Melee))
                RegisterHit(player, target);
        }

        public override void OnHitNPCWithProj(Player player, Projectile proj, NPC target, NPC.HitInfo hit)
        {
            if (proj.CountsAsClass(DamageClass.Melee))
                RegisterHit(player, target);
        }

        private static void RegisterHit(Player player, NPC target)
        {
            var ap = player.GetModPlayer<AugmentPlayer>();
            if (target.whoAmI == ap.OverwhelmLastTargetWhoAmI && ap.OverwhelmResetTimer > 0)
                ap.OverwhelmHitCounter++;
            else
            {
                ap.OverwhelmHitCounter = 1;
                ap.OverwhelmLastTargetWhoAmI = target.whoAmI;
            }

            ap.OverwhelmResetTimer = ResetWindowTicks;

            if (ap.OverwhelmHitCounter % HitsToTrigger == 0)
            {
                target.AddBuff(BuffID.Confused, ConfusedDurationTicks);
                if (Main.netMode != NetmodeID.SinglePlayer)
                    NetMessage.SendData(MessageID.NPCBuffs, number: target.whoAmI);
            }
        }
    }
}
