using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace Augments
{
    public class ContactChargeAugment : Augment
    {
        public const float ChargePerHit = 20f;
        public const int HitCooldownTicks = 120;

        public override string Id => "contact_charge";
        public override string DisplayName => "Contact Charge";
        public override string Description =>
            $"True melee hits grant {AugmentText.AttackSpeed("+20% Overclock charge")} to your Medi Gun, at most once every {AugmentText.Cooldown("2 seconds")}.";

        public override string GetDetailedDescription(Player player) =>
            Description + "\n" +
            AugmentText.Note("Direct weapon swings only, not projectiles.");
        public override AugmentRarity Rarity => AugmentRarity.Common;
        public override AugmentClass Class => AugmentClass.Support;
        public override Texture2D Icon => TextureAssets.Item[ItemID.CopperBroadsword].Value;

        // Hit hooks run on the attacking player's own client, which is also the
        // client that owns the Overclock charge, so no packet is needed.
        public override void OnHitNPCWithItem(Player player, Item item, NPC target, NPC.HitInfo hit)
        {
            if (player.whoAmI != Main.myPlayer || item == null || item.damage <= 0 || !item.CountsAsClass(DamageClass.Melee))
                return;
            // Town NPCs and critters (catchable) don't count, so bunnies can't be farmed for charge.
            if (target == null || target.friendly || target.catchItem > 0)
                return;

            player.GetModPlayer<MediGunPlayer>().TryAddContactCharge(ChargePerHit, HitCooldownTicks);
        }
    }
}
