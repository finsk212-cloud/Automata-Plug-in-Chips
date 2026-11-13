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
    // Active: teleport to the spot under your cursor (within 25 tiles), leaving an afterimage behind that explodes
    // a second later. A budget Rod of Discord, minus the Chaos State.
    //
    // Owner-authoritative like Blade Dance: the owner's game moves the player (and tells the others where they
    // are) and spawns the afterimage, which Terraria syncs like any other projectile. No custom packet.
    public class DiscountRodAugment : Augment
    {
        // Tuning
        public const float MaxRangePixels = 25f * 16f;   // 25 tiles
        public const int BaseBlastDamage = 80;           // run through AugmentScaling.Scale
        public const int CooldownTicks = 480;            // 8 seconds
        private const float MinMovePixels = 48f;

        public override string Id => "discount_rod";
        public override string DisplayName => "Rod of Discount";
        public override string Description =>
            $"{AugmentText.Active("Active:")} Press [{AugmentText.Key(Augments.DiscountRodKeybind)}] to {AugmentText.Trigger("teleport to your cursor")} (up to 25 tiles away), " +
            $"leaving an afterimage that explodes after {AugmentText.Duration("1 second")}. {AugmentText.Cooldown("8 seconds cooldown")}.";

        public override string GetDetailedDescription(Player player) =>
            AugmentScaling.ApplyToText(Description + "\n" +
            AugmentText.Note($"(The explosion deals {BaseBlastDamage} damage to enemies near the spot you left, scales with game progression, and uses your magic crit chance.\n" +
            "If your cursor is too far or inside blocks, you land on the closest free spot toward it. Nothing happens, and no cooldown is spent, if there is no room to land or you are frozen or webbed. " +
            "Like the real Rod of Discord, it can't take you into the Jungle Temple before Plantera is defeated. Unlike the real one, there is no Chaos State.)"),
            GetScalingEntries());

        public override (string label, int baseValue)[] GetScalingEntries()
        {
            return new[] { ("Explosion damage", BaseBlastDamage) };
        }

        public override AugmentRarity Rarity => AugmentRarity.Rare;
        public override AugmentClass Class => AugmentClass.Magic;
        public override ModKeybind ActiveModKeybind => Augments.DiscountRodKeybind;
        public override int CooldownRemaining => GetCooldown(LocalPlayerState.Player);
        public override Texture2D Icon => TextureAssets.Item[ItemID.RodofDiscord].Value;

        private static readonly Dictionary<int, int> cooldownTimer = new();

        public static int GetCooldown(Player player) =>
            player != null && cooldownTimer.TryGetValue(player.whoAmI, out int cd) ? cd : 0;

        public static void ResetCooldown(Player player)
        {
            if (player != null)
                cooldownTimer[player.whoAmI] = 0;
        }

        public override void OnAcquire(Player player)
        {
            cooldownTimer[player.whoAmI] = 0;
        }

        public override void OnUpdate(Player player)
        {
            if (cooldownTimer.TryGetValue(player.whoAmI, out int cd) && cd > 0)
                cooldownTimer[player.whoAmI] = cd - 1;
        }

        // Called from AugmentPlayer.ProcessTriggers when the keybind is pressed.
        public static void TryActivate(Player player)
        {
            if (player.whoAmI != Main.myPlayer || player.dead || player.CCed || GetCooldown(player) > 0 || BladeDanceAugment.IsDancing(player))
                return;

            // Aim at the cursor, but no farther than the range allows.
            Vector2 target = Main.MouseWorld;
            Vector2 toTarget = target - player.Center;
            if (toTarget.Length() > MaxRangePixels)
                target = player.Center + toTarget.SafeNormalize(Vector2.UnitX) * MaxRangePixels;

            // Walk back from the target toward the player until the first spot that has room.
            Vector2 half = new Vector2(player.width / 2f, player.height / 2f);
            Vector2 landing = Vector2.Zero;
            bool found = false;
            const int Steps = 30;
            for (int i = 0; i <= Steps; i++)
            {
                Vector2 center = Vector2.Lerp(target, player.Center, i / (float)Steps);
                if (!CanStandAt(center, half, player))
                    continue;

                landing = center;
                found = true;
                break;
            }

            if (!found || Vector2.Distance(landing, player.Center) < MinMovePixels)
            {
                Main.NewText("Rod of Discount: no room to land there.", 255, 100, 100);
                return;
            }

            Vector2 from = player.Center;

            // The afterimage stays behind where the player was.
            int crit = (int)player.GetTotalCritChance(DamageClass.Magic);
            // ai[1] and ai[2] carry the landing point, so every player's game can play the teleport sounds and the trail
            // (the afterimage plays them itself on its first tick). ai[0] is the explosion flag.
            int afterimage = Projectile.NewProjectile(
                player.GetSource_FromThis(),
                from,
                Vector2.Zero,
                ModContent.ProjectileType<DiscountRodAfterimageProjectile>(),
                AugmentScaling.Scale(BaseBlastDamage),
                7f,
                player.whoAmI,
                0f,
                landing.X,
                landing.Y);

            if (afterimage >= 0 && afterimage < Main.maxProjectiles)
                Main.projectile[afterimage].CritChance = crit;

            player.position = landing - half;
            player.velocity = Vector2.Zero;
            player.RemoveAllGrapplingHooks();
            player.fallStart = (int)(player.position.Y / 16f);

            // A player's position is client-authoritative: without this the others would not see the jump.
            if (Main.netMode == NetmodeID.MultiplayerClient)
                NetMessage.SendData(MessageID.PlayerControls, -1, -1, null, player.whoAmI);

            cooldownTimer[player.whoAmI] = CooldownTicks;
            // The sounds and the dust trail come from the afterimage projectile, on every game.
        }

        private static bool CanStandAt(Vector2 center, Vector2 half, Player player)
        {
            Vector2 topLeft = center - half;
            int tileX = (int)(center.X / 16f);
            int tileY = (int)(center.Y / 16f);
            if (!WorldGen.InWorld(tileX, tileY, 10))
                return false;

            if (Collision.SolidCollision(topLeft, player.width, player.height))
                return false;

            // Same rule as the real Rod of Discord: no Jungle Temple until Plantera has fallen.
            if (Main.tile[tileX, tileY].WallType == WallID.LihzahrdBrickUnsafe && !NPC.downedPlantBoss)
                return false;

            return true;
        }
    }
}
