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
    // Active: open a portal at your cursor. While it is open, your magic spells leave the portal (and fly toward
    // your cursor) instead of leaving you, so you can cast around corners and over walls.
    //
    // Everything runs on the caster's own game: the portal is a synced visual projectile, and moving a spell as it
    // is created is done by the owner before Terraria sends the new projectile to everyone else.
    public class LongDistanceRelationshipAugment : Augment
    {
        // Tuning
        public const int PortalTicks = 480;       // 8 seconds
        public const int DisconnectTicks = 60;    // 1 second of "DISCONNECTED" after the portal has stopped working
        public const int CooldownTicks = 1200;    // 20 seconds, from the moment the portal opens
        // Portal spells deal +20% damage with the portal right next to you, rising to +50% at 40 tiles or more.
        private const float MinDamageBonus = 0.20f;
        private const float MaxDamageBonus = 0.50f;
        private const float FullBonusDistancePixels = 40f * 16f;
        private const float ManaCostMultiplier = 0.5f;
        // A spell counts as "fired from the player" only if it appears this close to them. That leaves out spells that
        // appear at the cursor or fall from the sky, which have nothing to move.
        private const float MaxSpawnDistance = 160f;

        public override string Id => "long_distance_relationship";
        public override string DisplayName => "Long Distance Relationship";
        public override string Description =>
            $"{AugmentText.Active("Active:")} Press [{AugmentText.Key(Augments.LongDistanceKeybind)}] to open a {AugmentText.Trigger("portal")} at your cursor for {AugmentText.Duration("8 seconds")}.\n" +
            $"Your magic spells launch from the portal toward your cursor instead of from you.\n" +
            $"Portal spells deal {AugmentText.BonusDamage("up to +50% damage")}, more the farther the portal is from you, and cost {AugmentText.Mana("half the mana")}.\n" +
            $"{AugmentText.Cooldown("20 seconds cooldown")}.";

        public override string GetDetailedDescription(Player player) =>
            Description + "\n" +
            AugmentText.Note("(The damage bonus is +20% with the portal right next to you, rising to +50% when it is 40 tiles or more away. " +
            "The half mana cost applies to every magic weapon while the portal is open, even for spells that aren't moved. " +
            "The portal can't open inside blocks. Spells keep their spread, and anything you cast aims from the portal to your cursor. " +
            "Only spells that appear next to you are moved: beams you hold down, and spells that appear at the cursor or fall from the sky, are left alone. " +
            "Extra spells from other augments, like Double Tip's twin, may or may not come out of the portal depending on which augment you got first. " +
            "The cooldown starts when the portal opens. When the time is up it hangs up on you: \"DISCONNECTED\", a sad face and a hang-up sound, for 1 more second. Distance makes the cast grow fonder.)");

        public override AugmentRarity Rarity => AugmentRarity.Legendary;
        public override AugmentClass Class => AugmentClass.Magic;
        public override ModKeybind ActiveModKeybind => Augments.LongDistanceKeybind;
        public override int CooldownRemaining => GetCooldown(LocalPlayerState.Player);
        public override Texture2D Icon => TextureAssets.Item[ItemID.PortalGun].Value;

        private static readonly Dictionary<int, int> cooldownTimer = new();
        private static readonly Dictionary<int, int> portalTimer = new();
        private static readonly Dictionary<int, Vector2> portalPosition = new();
        // The portal projectile each player opened, so a launched spell can make it flash.
        private static readonly Dictionary<int, int> portalProjectile = new();
        private static uint lastPulseTick;

        public static int GetCooldown(Player player) =>
            player != null && cooldownTimer.TryGetValue(player.whoAmI, out int cd) ? cd : 0;

        public static void ResetCooldown(Player player)
        {
            if (player != null)
                cooldownTimer[player.whoAmI] = 0;
        }

        private static bool PortalOpen(Player player, out Vector2 position)
        {
            position = Vector2.Zero;
            return player != null
                && portalTimer.TryGetValue(player.whoAmI, out int ticks) && ticks > 0
                && portalPosition.TryGetValue(player.whoAmI, out position);
        }

        public override void OnAcquire(Player player)
        {
            cooldownTimer[player.whoAmI] = 0;
            portalTimer[player.whoAmI] = 0;
        }

        public override void OnKill(Player player)
        {
            portalTimer[player.whoAmI] = 0;
        }

        public override void OnUpdate(Player player)
        {
            int who = player.whoAmI;
            if (cooldownTimer.TryGetValue(who, out int cd) && cd > 0)
                cooldownTimer[who] = cd - 1;
            if (portalTimer.TryGetValue(who, out int pt) && pt > 0)
                portalTimer[who] = pt - 1;
        }

        // Called from AugmentPlayer.ProcessTriggers when the keybind is pressed.
        public static void TryActivate(Player player)
        {
            if (player.whoAmI != Main.myPlayer || player.dead || GetCooldown(player) > 0)
                return;

            Vector2 spot = Main.MouseWorld;
            int tileX = (int)(spot.X / 16f);
            int tileY = (int)(spot.Y / 16f);
            if (!WorldGen.InWorld(tileX, tileY, 10) || Collision.SolidCollision(spot - new Vector2(12f, 12f), 24, 24))
            {
                Main.NewText("Long Distance Relationship: no room for a portal there.", 255, 100, 100);
                return;
            }

            int who = player.whoAmI;
            portalPosition[who] = spot;
            portalTimer[who] = PortalTicks;
            cooldownTimer[who] = CooldownTicks;

            // The opening sound is played by the portal projectile on its first tick, on every game.
            portalProjectile[who] = Projectile.NewProjectile(
                player.GetSource_FromThis(),
                spot,
                Vector2.Zero,
                ModContent.ProjectileType<LongDistancePortalProjectile>(),
                0,
                0f,
                who);
        }

        // Makes the portal flash on every player's game by bumping the launch counter in its ai[0].
        // The counter is changed from outside the projectile's own update, so it is sent right away, and no more than
        // every few ticks so a fast weapon can't flood the network.
        private static void PulsePortal(Player player)
        {
            if (Main.GameUpdateCount - lastPulseTick < 6)
                return;

            if (!portalProjectile.TryGetValue(player.whoAmI, out int index) || index < 0 || index >= Main.maxProjectiles)
                return;

            Projectile portal = Main.projectile[index];
            if (!portal.active || portal.owner != player.whoAmI || portal.type != ModContent.ProjectileType<LongDistancePortalProjectile>())
                return;

            lastPulseTick = Main.GameUpdateCount;
            portal.ai[0] += 1f;
            portal.netUpdate = true;
            if (Main.netMode == NetmodeID.MultiplayerClient)
                NetMessage.SendData(MessageID.SyncProjectile, -1, -1, null, index);
        }

        // While the portal is open, magic weapons cost half as much mana.
        public override void ModifyManaCost(Player player, Item item, ref float reduce, ref float mult)
        {
            if (item != null && item.CountsAsClass(DamageClass.Magic) && PortalOpen(player, out _))
                mult *= ManaCostMultiplier;
        }

        // Runs for a projectile spawned by using a weapon, before Terraria sends it to the other players.
        public override void OnShootProjectile(Player player, Item item, Projectile projectile)
        {
            if (player.whoAmI != Main.myPlayer || !PortalOpen(player, out Vector2 portal))
                return;

            if (item == null || item.channel || !item.CountsAsClass(DamageClass.Magic) || item.damage <= 0)
                return;

            // Held-down beams are channelled weapons (already skipped above), so these checks only need to leave out summons.
            if (!projectile.friendly || projectile.minion || projectile.sentry)
                return;

            if (Vector2.Distance(projectile.Center, player.Center) > MaxSpawnDistance)
                return;

            // Turn the whole shot (all pellets/spread included) from "player toward cursor" to "portal toward cursor".
            Vector2 aim = Main.MouseWorld;
            float turn = 0f;
            if (Vector2.DistanceSquared(aim, portal) > 24f * 24f)
                turn = (aim - portal).ToRotation() - (aim - player.Center).ToRotation();

            Vector2 offset = (projectile.Center - player.Center).RotatedBy(turn);
            projectile.velocity = projectile.velocity.RotatedBy(turn);
            projectile.Center = portal + offset;

            // Distance makes the cast grow fonder: more damage the farther the portal is from the caster.
            float closeness = MathHelper.Clamp(Vector2.Distance(portal, player.Center) / FullBonusDistancePixels, 0f, 1f);
            float damageMultiplier = 1f + MinDamageBonus + (MaxDamageBonus - MinDamageBonus) * closeness;
            projectile.damage = (int)System.Math.Round(projectile.damage * damageMultiplier);
            projectile.originalDamage = (int)System.Math.Round(projectile.originalDamage * damageMultiplier);

            projectile.netUpdate = true;
            // The spell was changed from outside its own update, so send it right away as well: other players must see
            // it start at the portal, not at the caster. A second sync is harmless.
            if (Main.netMode == NetmodeID.MultiplayerClient)
                NetMessage.SendData(MessageID.SyncProjectile, -1, -1, null, projectile.whoAmI);

            PulsePortal(player);
        }
    }
}
