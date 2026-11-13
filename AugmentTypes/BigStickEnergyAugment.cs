using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace Augments
{
    public class BigStickEnergyAugment : Augment
    {
        // +25% size. Scale is multiplicative on the weapon's own scale, so a Zenith-sized weapon
        // simply gets 25% bigger too.
        public const float ScaleBonus = 0.25f;

        public override string Id => "big_stick_energy";
        public override string DisplayName => "Big Stick Energy";
        public override string Description =>
            $"Melee weapons are {AugmentText.BonusDamage("25% bigger")}.";

        public override string GetDetailedDescription(Player player) =>
            Description + "\n" +
            AugmentText.Note("(Both the sprite and the swing hitbox grow. Only affects damaging melee weapons, never pickaxes, axes or hammers. Weapons that fire a projectile instead of swinging may not grow. Yes, size matters.)");

        public override AugmentRarity Rarity => AugmentRarity.Common;
        public override AugmentClass Class => AugmentClass.Melee;
        public override Texture2D Icon => TextureAssets.Item[ItemID.WoodenSword].Value;

        // ModifyItemScale is read by the swing hitbox and by the held-item drawing, on every client,
        // so the bigger weapon is seen by other players too and no packet is needed.
        public override void ModifyItemScale(Player player, Item item, ref float scale)
        {
            if (item == null || item.IsAir || item.damage <= 0 || !item.CountsAsClass(DamageClass.Melee))
                return;
            if (item.pick > 0 || item.axe > 0 || item.hammer > 0)
                return;

            scale *= 1f + ScaleBonus;
        }
    }
}
