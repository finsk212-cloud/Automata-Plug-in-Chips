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
    public class GetOverHereAugment : Augment
    {
        // 60s at 60 ticks/sec.
        public const int CooldownTicks = 3600;
        public const float RangePixels = 1500f;
        public const float DamageReduction = 0.5f;
        // 2s at 60 ticks/sec.
        public const int ReductionTicks = 120;

        public override string Id => "get_over_here";
        public override string DisplayName => "Get over here!";
        public override string Description =>
            $"{AugmentText.Active("Active:")} Press [Get over here!] to hook the nearest teammate within {AugmentText.Duration("1500 pixels")} " +
            $"and yank them to your side. They take {AugmentText.Defense("50% less damage")} for {AugmentText.Duration("2s")} after landing. " +
            $"{AugmentText.Cooldown("60s cooldown")}. {AugmentText.Note("(Nothing happens, and no cooldown is spent, if no teammate is in range.)")}";
        public override AugmentRarity Rarity => AugmentRarity.Epic;
        public override AugmentClass Class => AugmentClass.Support;
        public override ModKeybind ActiveModKeybind => Augments.GetOverHereKeybind;
        public override int CooldownRemaining => LocalPlayerState.GetOverHereCooldown;
        public override Texture2D Icon => TextureAssets.Item[ItemID.GrapplingHook].Value;
        // No hooks - the owner's press is validated on the server, the pulled player's own client
        // moves itself and takes the damage reduction (see AugmentPlayer / SupportEffects).

        // Custom voice lines, Sounds/GetOverHere/GetOverHere1 .. GetOverHere5 (.wav/.ogg/.mp3).
        private const int SfxVariants = 5;
        private static SoundStyle[] customSounds;

        private static SoundStyle[] CustomSounds
        {
            get
            {
                if (customSounds != null)
                    return customSounds;

                // Only the files that actually exist are used, so a missing or renamed file can never
                // throw while playing - it just drops out of the random pick.
                var found = new System.Collections.Generic.List<SoundStyle>();
                for (int i = 1; i <= SfxVariants; i++)
                {
                    string path = "Augments/Sounds/GetOverHere/GetOverHere" + i;
                    if (ModContent.HasAsset(path))
                        found.Add(new SoundStyle(path) { Volume = 0.9f });
                }

                customSounds = found.ToArray();
                return customSounds;
            }
        }

        internal static void UnloadSounds() => customSounds = null;

        private static void PlayPullSound(Vector2 position)
        {
            bool custom = AugmentConfig.Instance == null || AugmentConfig.Instance.EnableGetOverHereSfx;
            SoundStyle[] sounds = custom ? CustomSounds : null;

            if (sounds != null && sounds.Length > 0)
                SoundEngine.PlaySound(sounds[Main.rand.Next(sounds.Length)], position);
            else
                SoundEngine.PlaySound(SoundID.Item17 with { Pitch = -0.2f, Volume = 0.8f }, position);
        }

        // Shown on every client: a thin chain of metal motes from where the teammate stood to the
        // owner, and a small puff where they land.
        internal static void SpawnPullEffect(Vector2 from, Vector2 to)
        {
            if (Main.dedServ)
                return;

            PlayPullSound(to);

            int steps = Math.Clamp((int)(Vector2.Distance(from, to) / 14f), 4, 110);
            for (int i = 0; i <= steps; i++)
            {
                Vector2 position = Vector2.Lerp(from, to, i / (float)steps);
                Dust link = Dust.NewDustPerfect(position, DustID.Silver, Vector2.Zero, 0, default, 0.9f);
                link.noGravity = true;
            }

            for (int i = 0; i < 12; i++)
            {
                Dust puff = Dust.NewDustPerfect(to, DustID.Smoke, Main.rand.NextVector2Circular(2.5f, 2.5f), 100, default, 1.2f);
                puff.noGravity = true;
            }
        }
    }
}
