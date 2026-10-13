using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Augments.Core;

namespace Augments
{
    public class ChaosRoundsAugment : Augment
    {
        public override string Id          => "chaos_rounds";
        public override string DisplayName => "Chaos Rounds";
        public override string Description =>
            $"Ranged hits apply a random debuff for {AugmentText.Duration("3s")}: " +
            $"{AugmentText.Color("On Fire", "FB923C")}, {AugmentText.Frostburn("Frostburn")}, " +
            $"{AugmentText.Color("Poisoned", "84CC16")}, {AugmentText.Color("Venom", "A855F7")} or {AugmentText.Ichor("Ichor")}.";

        public override string GetDetailedDescription(Player player) =>
            Description + "\n" +
            $"{AugmentText.Ichor("Ichor")} lowers the enemy's defense. Enemies that are immune to the rolled debuff simply don't get it.\n" +
            $"[c/94A3B8:(The burning, freezing and poison debuffs count as damage over time, so chips that look for one, like Volatile Rounds, will trigger.)]";

        public override AugmentRarity Rarity => AugmentRarity.Epic;
        public override AugmentClass  Class  => AugmentClass.Ranged;

        private const int DebuffDurationTicks = 180;

        private static readonly int[] DebuffPool =
        {
            BuffID.OnFire,
            BuffID.Frostburn,
            BuffID.Poisoned,
            BuffID.Venom,
            BuffID.Ichor,
        };

        public override void OnHitNPCWithProj(Player player, Projectile proj, NPC target, NPC.HitInfo hit, AugmentHitSource source, float effectiveness)
        {
            if (source == AugmentHitSource.AugmentProc)
                return;

            if (!proj.CountsAsClass(DamageClass.Ranged))
                return;

            ApplyRandomDebuff(player, target);
        }

        public override void OnHitNPCWithItem(Player player, Item item, NPC target, NPC.HitInfo hit, AugmentHitSource source, float effectiveness)
        {
            if (source == AugmentHitSource.AugmentProc)
                return;

            if (!item.CountsAsClass(DamageClass.Ranged))
                return;

            ApplyRandomDebuff(player, target);
        }

        private static void ApplyRandomDebuff(Player player, NPC target)
        {
            if (player.whoAmI != Main.myPlayer)
                return;

            if (target == null || !target.active || target.friendly || target.townNPC)
                return;

            int buffType = DebuffPool[Main.rand.Next(DebuffPool.Length)];

            // Bosses that are immune to the rolled debuff just don't take it.
            if (target.buffImmune[buffType])
                return;

            target.AddBuff(buffType, DebuffDurationTicks);
            if (Main.netMode == NetmodeID.MultiplayerClient)
                NetMessage.SendData(MessageID.AddNPCBuff, -1, -1, null, target.whoAmI, buffType, DebuffDurationTicks);

            SpawnHitDust(target, buffType);
        }

        // A small burst in the rolled debuff's colour so you can see what the roll was.
        private static void SpawnHitDust(NPC target, int buffType)
        {
            int dustType = buffType switch
            {
                BuffID.OnFire    => DustID.Torch,
                BuffID.Frostburn => DustID.IceTorch,
                BuffID.Poisoned  => DustID.GreenTorch,
                BuffID.Venom     => DustID.PurpleTorch,
                _                => DustID.Ichor,
            };

            for (int i = 0; i < 6; i++)
            {
                Dust d = Dust.NewDustDirect(target.position, target.width, target.height, dustType, 0f, 0f, 100, default, 1.2f);
                d.noGravity = true;
                d.velocity *= 0.6f;
            }
        }
    }
}
