using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace Augments
{
    public class ProlongedSurgeAugment : Augment
    {
        public const int DefaultBuffTicks = 360;
        public const int BuffTicks = 720;

        public override string Id => "prolonged_surge";
        public override string DisplayName => "Prolonged Surge";
        public override string Description =>
            $"Medi Gun Overclock buffs on your ally last {AugmentText.Duration("12 seconds")} instead of {AugmentText.Duration("6 seconds")}.";
        public override AugmentRarity Rarity => AugmentRarity.Common;
        public override AugmentClass Class => AugmentClass.Support;
        public override Texture2D Icon => TextureAssets.Item[ItemID.Stopwatch].Value;

        // Used by the single-player path (MediGunPlayer) and the server handler (SupportEffects).
        public static int GetBuffTicks(Player medic) =>
            medic != null && medic.GetModPlayer<AugmentPlayer>().HasAugment("prolonged_surge") ? BuffTicks : DefaultBuffTicks;
    }
}
