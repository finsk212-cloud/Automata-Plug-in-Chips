using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace Augments
{
    public class TauntAugment : Augment
    {
        public override string Id => "taunt";
        public override string DisplayName => "Taunt";
        public override string Description =>
            $"Nearby enemies preferentially {AugmentText.Trigger("target you")} instead of teammates.";

        public override string GetDetailedDescription(Player player) =>
            Description + "\n" +
            AugmentText.Note("(Draws enemy aggro within 800 pixels.)");
        public override AugmentRarity Rarity => AugmentRarity.Rare;
        public override AugmentClass Class => AugmentClass.Support;
        public override bool HasAuraEffect => true;
        // NPC targeting is redirected in AugmentGlobalNPC.PreAI (800px radius).

        public override void OnUpdate(Player player)
        {
            if ((Main.GameUpdateCount + (ulong)player.whoAmI) % 12 != 0)
                return;

            for (int i = 0; i < 2; i++)
            {
                Vector2 spawnPos = player.Top + new Vector2(Main.rand.NextFloatDirection() * 6f, -4f);
                Dust d = Dust.NewDustDirect(spawnPos, 0, 0, DustID.Blood);
                d.noGravity = true;
                d.velocity = new Vector2(Main.rand.NextFloatDirection() * 0.6f, -1.8f - Main.rand.NextFloat(0.8f));
                d.scale = 0.55f + Main.rand.NextFloat(0.3f);
                d.alpha = 80;
            }
        }
    }
}
