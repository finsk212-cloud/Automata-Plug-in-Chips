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

        // The pull is real movement, not a teleport: the hook flies out (a few ticks), then the
        // pulled player is hauled toward the owner, starting slow and speeding up. Pixels per tick.
        public const float PullStartSpeed = 10f;
        public const float PullAcceleration = 1.5f;
        public const float PullMaxSpeed = 36f;
        // Safety cap so a pull can never hold someone forever (1.5s).
        public const int PullMaxTicks = 90;

        // How many ticks the hook takes to fly out; the visual and the pulled client both use this
        // so the drag starts exactly when the hook lands.
        public static int ThrowTicks(float distance) => Math.Clamp((int)(distance / 100f), 3, 12);

        public override string Id => "get_over_here";
        public override string DisplayName => "Get over here!";
        public override string Description =>
            $"{AugmentText.Active("Active:")} Press [{AugmentText.Key(Augments.GetOverHereKeybind)}] to hook the nearest teammate within {AugmentText.Duration("1500 pixels")} " +
            $"and yank them to your side. They take {AugmentText.Defense("50% less damage")} for {AugmentText.Duration("2s")} after landing. " +
            $"{AugmentText.Cooldown("60s cooldown")}. {AugmentText.Note("(Nothing happens, and no cooldown is spent, if no teammate is in range.)")}";
        public override AugmentRarity Rarity => AugmentRarity.Epic;
        public override AugmentClass Class => AugmentClass.Support;
        public override ModKeybind ActiveModKeybind => Augments.GetOverHereKeybind;
        public override int CooldownRemaining => LocalPlayerState.GetOverHereCooldown;
        public override Texture2D Icon => TextureAssets.Item[ItemID.GrapplingHook].Value;
        // No hooks - the owner's press is validated on the server, the pulled player's own client
        // hauls itself in and takes the damage reduction (see AugmentPlayer / SupportEffects).

        // Custom voice lines: every .wav/.ogg/.mp3 in the mod's Sounds/GetOverHere/ folder, whatever
        // it is called, is part of the random pick.
        private static readonly string[] SoundExtensions = { ".wav", ".ogg", ".mp3" };
        private const string SoundFolder = "Sounds/GetOverHere/";
        private static SoundStyle[] customSounds;

        private static SoundStyle[] CustomSounds
        {
            get
            {
                if (customSounds != null)
                    return customSounds;

                // Only files that actually load are used, so a bad file can never throw while
                // playing - it just drops out of the random pick.
                var found = new System.Collections.Generic.List<SoundStyle>();
                var seen = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var mod = ModContent.GetInstance<Augments>();
                var files = mod.GetFileNames();
                if (files != null)
                {
                    foreach (string file in files)
                    {
                        string normalized = file.Replace('\\', '/');
                        if (!normalized.StartsWith(SoundFolder, StringComparison.OrdinalIgnoreCase))
                            continue;

                        string extension = System.IO.Path.GetExtension(normalized);
                        if (Array.IndexOf(SoundExtensions, extension.ToLowerInvariant()) < 0)
                            continue;

                        string path = "Augments/" + normalized.Substring(0, normalized.Length - extension.Length);
                        if (seen.Add(path) && ModContent.HasAsset(path))
                            found.Add(new SoundStyle(path) { Volume = 0.9f });
                    }
                }

                customSounds = found.ToArray();
                mod.Logger.Info($"Get over here! found {customSounds.Length} custom sound file(s) in {SoundFolder}");
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

        // Shown on every client when the owner throws: the voice line at the owner, and the hook
        // and chain that fly out to the teammate and stay on them while they are hauled in.
        internal static void BeginPullEffect(Player owner, Player target)
        {
            if (Main.dedServ)
                return;

            PlayPullSound(owner.Center);
            GetOverHereHooks.Add(owner.whoAmI, target.whoAmI);
        }
    }
}
