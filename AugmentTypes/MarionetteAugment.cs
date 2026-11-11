using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Augments.Core;

namespace Augments
{
    // Legendary Summoner: the player's minions are locked to the mouse cursor instead of following
    // the player. Everything here runs on the owner's own game only: the cursor only exists there,
    // and the minions' AI is owner-authoritative (positions reach other clients through normal
    // projectile sync).
    //
    // Only the minions' MOVEMENT is taken over. What they shoot is still their own business: they
    // keep choosing their own targets, the way they normally do.
    public class MarionetteAugment : Augment
    {
        public override string Id => "marionette";
        public override string DisplayName => "Marionette";
        public override string Description =>
            $"Your {AugmentText.Trigger("minions")} are locked to your {AugmentText.Trigger("mouse cursor")} instead of following you. " +
            $"Minions deal {AugmentText.BonusDamage("+15% damage")}.";

        public override string GetDetailedDescription(Player player) =>
            Description + "\n" +
            AugmentText.Note("(Minions stay in a tight ring on the cursor and never move toward enemies on their own: put the cursor on an enemy to hit it. Minions that shoot still pick and shoot their own targets. Sentries and minion shots are unaffected.)");

        public override AugmentRarity Rarity => AugmentRarity.Legendary;
        public override AugmentClass Class => AugmentClass.Summon;

        public const float MinionDamageBonus = 0.15f;

        // Rebuilt every tick on the local player: which minion sits in which ring slot.
        private static readonly Dictionary<int, int> slotByProjectile = new();
        private static int slotCount;

        internal static bool TryGetSlot(Projectile projectile, out int index, out int count)
        {
            count = slotCount;
            return slotByProjectile.TryGetValue(projectile.whoAmI, out index);
        }

        // Whole minions only: body segments (e.g. worm-style minions) take 0 slots and follow their head.
        internal static bool IsSteerable(Projectile p, int owner) =>
            p.active && p.owner == owner && p.minion && !p.sentry && p.minionSlots > 0f;

        public override void OnAcquire(Player player)
        {
            slotByProjectile.Clear();
            slotCount = 0;
        }

        public override void OnUpdate(Player player)
        {
            if (Main.dedServ || player.whoAmI != Main.myPlayer)
                return;

            slotByProjectile.Clear();
            slotCount = 0;
            if (player.dead)
                return;

            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                if (IsSteerable(Main.projectile[i], player.whoAmI))
                    slotByProjectile[i] = slotCount++;
            }
        }

        public override void ModifyHitNPCWithProj(Player player, Projectile proj, NPC target, ref NPC.HitModifiers modifiers, AugmentHitSource source, float effectiveness)
        {
            if (proj.minion && !proj.sentry)
                modifiers.SourceDamage += MinionDamageBonus;
        }

        // Where a minion should sit: a slowly turning ring around the cursor.
        internal static Vector2 SlotPosition(Vector2 anchor, int index, int count, float ringRadius)
        {
            count = Math.Max(1, count);
            float radius = count == 1 ? 0f : ringRadius;
            float angle = (float)Main.GlobalTimeWrappedHourly * 0.9f + index * (MathHelper.TwoPi / count);
            float bob = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 3f + index * 1.3f) * 4f;
            return anchor + new Vector2((float)Math.Cos(angle) * radius, (float)Math.Sin(angle) * radius + bob);
        }
    }

    public class MarionetteGlobalProjectile : GlobalProjectile
    {
        public override bool InstancePerEntity => false;

        // Most a minion can be moved in one tick (pixels); anything closer arrives exactly.
        private const float MaxStep = 60f;

        // Ticks between contact hits on the same enemy (20 = 3 hits per second).
        private const int ContactHitCooldown = 20;

        // Melee minions that only switch their body damage on during their own attack move, which the
        // lock never lets them start. Shooting minions are deliberately NOT in here. Looked up by
        // name / ProjectileID so a missing entry can't break the build; add more names to extend it.
        private static readonly string[] ContactMinionNames = {
            "OneEyedPirate", "SoulscourgePirate", "PirateCaptain", "Pirate",
            "Pygmy", "Pygmy2", "Pygmy3", "Pygmy4"
        };
        private static HashSet<int> contactTypes;

        private static bool IsContactMinion(Projectile projectile)
        {
            if (projectile.aiStyle == ProjAIStyleID.CommonFollow)
                return true;

            if (contactTypes == null)
            {
                contactTypes = new HashSet<int>
                {
                    ProjectileID.OneEyedPirate,
                    ProjectileID.SoulscourgePirate,
                    ProjectileID.PirateCaptain,
                    ProjectileID.Pygmy,
                    ProjectileID.Pygmy2,
                    ProjectileID.Pygmy3,
                    ProjectileID.Pygmy4,
                    ProjectileID.BabySlime,
                    ProjectileID.BabyBird,
                    ProjectileID.FlinxMinion,
                    ProjectileID.VampireFrog,
                    ProjectileID.VenomSpider,
                    ProjectileID.JumperSpider,
                    ProjectileID.DangerousSpider,
                    ProjectileID.Raven,
                    ProjectileID.DeadlySphere,
                    ProjectileID.StormTigerTier1,
                    ProjectileID.StormTigerTier2,
                    ProjectileID.StormTigerTier3,
                    ProjectileID.Smolstar,
                    ProjectileID.EmpressBlade,
                    ProjectileID.Spazmamini
                };

                foreach (string name in ContactMinionNames)
                {
                    if (ProjectileID.Search.TryGetId(name, out int id))
                        contactTypes.Add(id);
                }
            }

            return contactTypes.Contains(projectile.type);
        }

        public override void Unload() => contactTypes = null;

        public override void PostAI(Projectile projectile)
        {
            if (Main.dedServ || !projectile.active || !projectile.minion || projectile.owner != Main.myPlayer)
                return;

            Player player = Main.player[projectile.owner];
            if (!player.active || player.dead || !player.GetModPlayer<AugmentPlayer>().HasAugment("marionette"))
                return;

            if (!MarionetteAugment.TryGetSlot(projectile, out int index, out int count))
                return;

            // Hard lock: the slot is always on the cursor, never on an enemy. The velocity is set to the
            // whole step to the slot, so after this tick's movement the minion is exactly there and
            // whatever the minion's own AI decided about moving (chasing, dashing, returning) is thrown away.
            float ring = Math.Min(20f + count * 4f, 70f);
            Vector2 slot = MarionetteAugment.SlotPosition(Main.MouseWorld, index, count, ring);
            Vector2 toSlot = slot - projectile.Center;
            float dist = toSlot.Length();

            projectile.tileCollide = false;
            projectile.velocity = dist > MaxStep ? toSlot / dist * MaxStep : toSlot;

            if (projectile.damage > 0 && IsContactMinion(projectile))
            {
                projectile.friendly = true;
                if (!projectile.usesLocalNPCImmunity)
                {
                    projectile.usesIDStaticNPCImmunity = false;
                    projectile.usesLocalNPCImmunity = true;
                    projectile.localNPCHitCooldown = ContactHitCooldown;
                }
            }

            if (Math.Abs(projectile.velocity.X) > 0.5f)
                projectile.spriteDirection = projectile.direction = projectile.velocity.X > 0f ? 1 : -1;

            // Other clients only see the minion through projectile sync.
            if (Main.netMode == NetmodeID.MultiplayerClient && Main.GameUpdateCount % 5 == 0)
                projectile.netUpdate = true;
        }
    }
}
