using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Augments.Core;

namespace Augments
{
    // Half of every hit is paid from mana instead of life.
    //
    // Two steps, because the game finishes a hit before it knows whether the hit lands:
    //  1. The hit is lowered while it is being finalised (Player.HurtModifiers.ModifyHurtInfo), so this also works on a
    //     hit that would have been lethal. The amount is only noted down here.
    //  2. The mana is taken in OnHurt, which only runs for hits that actually land. A hit that gets dodged (Mirror Image,
    //     Bulwark, Void Step, vanilla dodges) costs nothing, shows nothing and is not counted as blocked damage.
    // It only runs on the hurt player's own game, which is the one that owns their mana.
    public class PayToWinAugment : Augment
    {
        // The id is deliberately neutral: ids are saved with characters and must never change, but the name may.
        public override string Id => "mana_buffer";
        public override string DisplayName => "Pay to Win";
        public override string Description =>
            $"Half of every hit you take is paid from your {AugmentText.Mana("mana")} instead of your life, while you have mana.";

        public override string GetDetailedDescription(Player player) =>
            Description + "\n" +
            AugmentText.Note("(1 mana pays for 1 damage. If you don't have enough mana, it pays what it can, and the rest hurts as usual. A hit is never reduced below 1 damage. " +
            "Hits you dodge cost no mana. It stands aside for a killing blow that Last Stand or Phoenix Heart is about to save you from, so they still do their job. " +
            "Mana Reflux gives back 35% of the damage you take, so the two make a loop: your wallet takes the hit and refunds a third of it.)");

        public override AugmentRarity Rarity => AugmentRarity.Epic;
        public override AugmentClass Class => AugmentClass.Magic;
        public override Texture2D Icon => TextureAssets.Item[ItemID.ManaCrystal].Value;

        // The payment worked out for the hit currently being processed, per player. Only the local player uses it.
        private static readonly Dictionary<int, int> pendingPayment = new();

        public override void ModifyHurt(Player player, ref Player.HurtModifiers modifiers)
        {
            if (player.whoAmI != Main.myPlayer)
                return;

            // A new hit: forget any payment from a previous hit that never landed.
            pendingPayment[player.whoAmI] = 0;
            modifiers.ModifyHurtInfo += (ref Player.HurtInfo info) => Reduce(player, ref info);
        }

        private void Reduce(Player player, ref Player.HurtInfo info)
        {
            var ap = player.GetModPlayer<AugmentPlayer>();

            // Last Stand and Phoenix Heart cap a killing blow at "life - 1" and only spend themselves if the hit still
            // matches that cap. Lowering it would let the hit through for mana and leave the save unspent.
            bool deathSaveArmed = ap.LastStandArmedThisHit || ap.PhoenixHeartArmedThisHit;
            if (deathSaveArmed && info.Damage == player.statLife - 1)
                return;

            int payment = Math.Min(info.Damage / 2, player.statMana);
            if (payment <= 0)
                return;

            info.Damage -= payment;
            pendingPayment[player.whoAmI] = payment;
        }

        public override void OnHurt(Player player, Player.HurtInfo info)
        {
            if (player.whoAmI != Main.myPlayer)
                return;

            if (!pendingPayment.TryGetValue(player.whoAmI, out int payment) || payment <= 0)
                return;

            pendingPayment[player.whoAmI] = 0;
            player.statMana = Math.Max(0, player.statMana - payment);

            AugmentDamageTracker.RecordDamageBlocked(Id, DisplayName, payment, Rarity, Class);

            // The blue "-N" and sparks: shown here and relayed so other players see you pay too.
            PluginEffects.Show(PluginEffectId.PayToWinPaid, player, payment);
        }
    }
}
