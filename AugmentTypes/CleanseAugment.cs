using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace Augments
{
	public class CleanseAugment : Augment
	{
		public override string Id => "cleanse";
		public override string DisplayName => "Cleanse";
		public override string Description =>
			$"{AugmentText.Active("Active:")} Press [{AugmentText.Key(Augments.CleanseKeybind)}] to remove all debuffs from nearby teammates. {AugmentText.Cooldown("30s cooldown")}.";
		public override AugmentRarity Rarity => AugmentRarity.Rare;
		public override AugmentClass Class => AugmentClass.Support;
		public override bool HasAuraEffect => true;
		public override ModKeybind ActiveModKeybind => Augments.CleanseKeybind;
		public override int CooldownRemaining => Main.LocalPlayer.GetModPlayer<AugmentPlayer>().CleanseCooldown;
		public override Texture2D Icon => TextureAssets.Item[ItemID.PurificationPowder].Value;
	}
}
