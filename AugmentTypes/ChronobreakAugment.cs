using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace Augments
{
    public class ChronobreakAugment : Augment
    {
        // 120s at 60 ticks/sec.
        public const int CooldownTicks = 7200;

        public override string Id => "chronobreak";
        public override string DisplayName => "Chronobreak";
        public override string Description =>
            $"{AugmentText.Active("Active:")} Press [Chronobreak] to rewind you and nearby teammates to where they stood " +
            $"{AugmentText.Duration("4 seconds")} ago, restoring {AugmentText.HP("HP")} up to what they had then. " +
            $"It never lowers anyone's {AugmentText.HP("HP")}. Rewound players are invulnerable for {AugmentText.Duration("0.5s")}. " +
            $"{AugmentText.Cooldown("120s cooldown")}.";
        public override AugmentRarity Rarity => AugmentRarity.Legendary;
        public override AugmentClass Class => AugmentClass.Support;
        public override bool HasAuraEffect => true;
        public override ModKeybind ActiveModKeybind => Augments.ChronobreakKeybind;
        public override int CooldownRemaining => LocalPlayerState.ChronobreakCooldown;
        public override Texture2D Icon => TextureAssets.Item[ItemID.Stopwatch].Value;
        // No hooks - the position/HP history is recorded in AugmentPlayer.PostUpdate and the
        // rewind itself runs on each affected player's own client (see SupportEffects).

        // Quiet visuals for one rewound player: a soft chime, a thin trail of fading motes from
        // where they stood to where they landed, and a small puff at the arrival point.
        internal static void SpawnRewindEffect(Vector2 from, Vector2 to, bool moved)
        {
            if (Main.dedServ)
                return;

            SoundEngine.PlaySound(SoundID.Item6 with { Pitch = -0.4f, Volume = 0.7f }, to);

            if (moved)
            {
                int steps = Math.Clamp((int)(Vector2.Distance(from, to) / 28f), 2, 48);
                for (int i = 0; i <= steps; i++)
                {
                    Vector2 position = Vector2.Lerp(from, to, i / (float)steps);
                    Dust trail = Dust.NewDustPerfect(position, Main.rand.NextBool(3) ? DustID.PurpleTorch : DustID.MagicMirror,
                        Main.rand.NextVector2Circular(0.6f, 0.6f), 100, default, 1f);
                    trail.noGravity = true;
                }
            }

            for (int i = 0; i < 10; i++)
            {
                Dust puff = Dust.NewDustPerfect(to, DustID.MagicMirror, Main.rand.NextVector2Circular(2.5f, 2.5f), 100, default, 1.1f);
                puff.noGravity = true;
            }
        }
    }
}
