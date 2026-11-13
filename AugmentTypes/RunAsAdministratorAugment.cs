using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace Augments
{
    // Active: for a few seconds your spells cost no mana and you get a big attack-speed boost. Then the system locks
    // you out of casting for a moment. Run as Administrator, then "Access denied".
    //
    // Everything is local to the caster: mana, attack speed and weapon use are all decided by the player's own game.
    public class RunAsAdministratorAugment : Augment
    {
        // Tuning
        public const int ActiveTicks = 360;      // 6 seconds
        public const int LockoutTicks = 180;     // 3 seconds
        public const int CooldownTicks = 1500;   // 25 seconds, counted from the moment you press the key
        public const float AttackSpeedBonus = 0.5f;

        public override string Id => "run_as_administrator";
        public override string DisplayName => "Run as Administrator";
        public override string Description =>
            $"{AugmentText.Active("Active:")} Press [{AugmentText.Key(Augments.AdminKeybind)}] for {AugmentText.Duration("6 seconds")} of " +
            $"{AugmentText.Mana("no mana costs")} and {AugmentText.AttackSpeed("+50% magic attack speed")}. Then you can't cast for {AugmentText.Duration("3 seconds")}. {AugmentText.Cooldown("25 seconds cooldown")}.";

        public override string GetDetailedDescription(Player player) =>
            Description + "\n" +
            AugmentText.Note("(The cooldown starts the moment you press the key, so it already includes the 6 seconds of admin rights and the 3 second lockout. " +
            "The lockout only blocks magic weapons: you can still swing, shoot, drink and run. Dying ends both at once. " +
            "Yes, you will see \"ACCESS DENIED\". No, you can't blame the antivirus.)");

        public override AugmentRarity Rarity => AugmentRarity.Legendary;
        public override AugmentClass Class => AugmentClass.Magic;
        public override ModKeybind ActiveModKeybind => Augments.AdminKeybind;
        public override int CooldownRemaining => GetCooldown(LocalPlayerState.Player);
        public override Texture2D Icon => TextureAssets.Item[ItemID.CellPhone].Value;

        private static readonly Dictionary<int, int> cooldownTimer = new();
        private static readonly Dictionary<int, int> activeTimer = new();
        private static readonly Dictionary<int, int> lockoutTimer = new();

        public static int GetCooldown(Player player) =>
            player != null && cooldownTimer.TryGetValue(player.whoAmI, out int cd) ? cd : 0;

        private static bool IsActive(Player player) =>
            player != null && activeTimer.TryGetValue(player.whoAmI, out int t) && t > 0;

        private static bool IsLockedOut(Player player) =>
            player != null && lockoutTimer.TryGetValue(player.whoAmI, out int t) && t > 0;

        public static void ResetCooldown(Player player)
        {
            if (player != null)
                cooldownTimer[player.whoAmI] = 0;
        }

        public override void OnAcquire(Player player)
        {
            ClearState(player.whoAmI);
        }

        // Dying ends the admin session and the lockout, but not the cooldown: dying must never be a way to reset it.
        public override void OnKill(Player player)
        {
            activeTimer[player.whoAmI] = 0;
            lockoutTimer[player.whoAmI] = 0;
        }

        private static void ClearState(int who)
        {
            cooldownTimer[who] = 0;
            activeTimer[who] = 0;
            lockoutTimer[who] = 0;
        }

        public override void OnUpdate(Player player)
        {
            int who = player.whoAmI;

            if (cooldownTimer.TryGetValue(who, out int cd) && cd > 0)
                cooldownTimer[who] = cd - 1;
            if (lockoutTimer.TryGetValue(who, out int lockout) && lockout > 0)
                lockoutTimer[who] = lockout - 1;

            if (!activeTimer.TryGetValue(who, out int active) || active <= 0)
                return;

            activeTimer[who] = active - 1;

            // No mana refill here any more: while active, spells simply cost no mana (see ModifyManaCost),
            // so your mana bar stays where it is instead of being topped up.

            if (active - 1 <= 0)
                BeginLockout(player);
        }

        // The lockout's "ACCESS DENIED" show is part of the aura projectile (see AdminAuraProjectile), which runs its own
        // matching clock on every player's game. This only starts the real lockout.
        private static void BeginLockout(Player player)
        {
            lockoutTimer[player.whoAmI] = LockoutTicks;
        }

        public override void UpdateEquips(Player player)
        {
            if (IsActive(player))
                player.GetAttackSpeed(DamageClass.Magic) += AttackSpeedBonus;
        }

        public override void ModifyManaCost(Player player, Item item, ref float reduce, ref float mult)
        {
            if (IsActive(player))
                mult = 0f;
        }

        public override bool CanUseItem(Player player, Item item)
        {
            return !(IsLockedOut(player) && item != null && item.damage > 0 && item.CountsAsClass(DamageClass.Magic));
        }

        // Called from AugmentPlayer.ProcessTriggers when the keybind is pressed.
        public static void TryActivate(Player player)
        {
            if (player.whoAmI != Main.myPlayer || player.dead || GetCooldown(player) > 0 || IsActive(player) || IsLockedOut(player))
                return;

            int who = player.whoAmI;
            activeTimer[who] = ActiveTicks;
            cooldownTimer[who] = CooldownTicks;

            // The whole show (terminal window, shield, "ACCESS DENIED" and the sounds) is one aura projectile that follows
            // the caster. It exists on every player's game, so everyone sees and hears it.
            Projectile.NewProjectile(
                player.GetSource_FromThis(),
                player.Center,
                Vector2.Zero,
                ModContent.ProjectileType<AdminAuraProjectile>(),
                0,
                0f,
                who);
        }
    }
}
