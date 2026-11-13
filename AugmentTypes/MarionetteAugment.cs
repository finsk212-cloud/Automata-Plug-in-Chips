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

        // Minions whose summon slots sit on a different projectile than the one that actually moves.
        //  - Stardust Dragon: the slot is on the tail, but the head leads and the body follows it.
        //    Steer the head; the body and tail are never steered, they follow the head by themselves.
        //  - Abigail: the slot is on her flower, her ghost carries none. Steer the ghost, not the flower.
        // Abigail is looked up by name so a missing entry can't break the build.
        private static HashSet<int> alwaysSteered;
        private static HashSet<int> neverSteered;

        private static void EnsureTypeSets()
        {
            if (alwaysSteered != null)
                return;

            alwaysSteered = new HashSet<int> { ProjectileID.StardustDragon1 };
            neverSteered = new HashSet<int>
            {
                ProjectileID.StardustDragon2,
                ProjectileID.StardustDragon3,
                ProjectileID.StardustDragon4
            };

            if (ProjectileID.Search.TryGetId("AbigailMinion", out int abigail))
                alwaysSteered.Add(abigail);
            if (ProjectileID.Search.TryGetId("AbigailCounter", out int flower))
                neverSteered.Add(flower);
        }

        internal static void ClearTypeSets()
        {
            alwaysSteered = null;
            neverSteered = null;
        }

        // Whole minions only: body segments (e.g. worm-style minions) take 0 slots and follow their head.
        internal static bool IsSteerable(Projectile p, int owner)
        {
            if (!p.active || p.owner != owner || p.sentry)
                return false;

            EnsureTypeSets();
            if (neverSteered.Contains(p.type))
                return false;
            if (alwaysSteered.Contains(p.type))
                return true;

            return p.minion && p.minionSlots > 0f;
        }

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
        internal static Vector2 SlotPosition(Vector2 anchor, int index, int count, float ringRadius, bool bobbing = true)
        {
            count = Math.Max(1, count);
            float radius = count == 1 ? 0f : ringRadius;
            float angle = (float)Main.GlobalTimeWrappedHourly * 0.9f + index * (MathHelper.TwoPi / count);
            float bob = bobbing ? (float)Math.Sin(Main.GlobalTimeWrappedHourly * 3f + index * 1.3f) * 4f : 0f;
            return anchor + new Vector2((float)Math.Cos(angle) * radius, (float)Math.Sin(angle) * radius + bob);
        }
    }

    public class MarionetteGlobalProjectile : GlobalProjectile
    {
        public override bool InstancePerEntity => false;

        // Most a minion can be moved in one tick (pixels); anything closer arrives exactly.
        private const float MaxStep = 60f;

        // Smoothed facing of the Stardust Dragon's head (local player only).
        private static int dragonHeadId = -1;
        private static float dragonHeadRotation = float.NaN;

        // Ticks between contact hits on the same enemy (20 = 3 hits per second).
        private const int ContactHitCooldown = 20;

        // Melee minions that only switch their body damage on during their own attack move, which the
        // lock never lets them start. Shooting minions are deliberately NOT in here. Looked up by
        // name / ProjectileID so a missing entry can't break the build; add more names to extend it.
        private static readonly string[] ContactMinionNames = {
            "OneEyedPirate", "SoulscourgePirate", "PirateCaptain", "Pirate", "AbigailMinion",
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

        // ---- Other players' game: following the owner's steered minions without rubber-banding ----
        //
        // On every other client a minion still runs its normal AI, which thinks it should follow the owner
        // or chase an enemy, not sit on a cursor only the owner can see. Between position updates it drifts
        // the wrong way, then each update snaps it back: that looks like the minion going back in time.
        // So on those clients the minion simply keeps moving with the last velocity the owner sent, and
        // its own AI's movement is thrown away.
        private static Vector2[] remoteVelocity;
        private static int[] remoteIdentity;
        private static int[] remoteIdle;

        // Where each steered minion stood before its own AI ran this tick. Some minions' AI moves them by
        // setting their position directly (the "teleport back to the owner" some of them do when far away),
        // which no velocity can undo; this lets PostAI put them back.
        private static Vector2[] prePosition;
        private static uint[] prePositionTick;

        // Ticks without a new update before the remembered velocity starts fading out.
        private const int RemoteIdleTicks = 3;

        private static bool IsRemoteSteered(Projectile projectile)
        {
            if (Main.dedServ || Main.netMode != NetmodeID.MultiplayerClient || !projectile.active
                || projectile.owner == Main.myPlayer || projectile.owner < 0 || projectile.owner >= Main.maxPlayers)
                return false;

            Player owner = Main.player[projectile.owner];
            if (!owner.active || owner.dead)
                return false;

            return owner.GetModPlayer<AugmentPlayer>().HasAugment("marionette")
                && MarionetteAugment.IsSteerable(projectile, projectile.owner);
        }

        private static void EnsureRemoteArrays()
        {
            if (remoteVelocity != null)
                return;

            remoteVelocity = new Vector2[Main.maxProjectiles + 1];
            remoteIdentity = new int[Main.maxProjectiles + 1];
            remoteIdle = new int[Main.maxProjectiles + 1];
            prePosition = new Vector2[Main.maxProjectiles + 1];
            prePositionTick = new uint[Main.maxProjectiles + 1];
        }

        private static void RememberPosition(Projectile projectile)
        {
            prePosition[projectile.whoAmI] = projectile.position;
            prePositionTick[projectile.whoAmI] = Main.GameUpdateCount;
        }

        private static void RestorePosition(Projectile projectile)
        {
            if (prePositionTick != null && prePositionTick[projectile.whoAmI] == Main.GameUpdateCount)
                projectile.position = prePosition[projectile.whoAmI];
        }

        public override bool PreAI(Projectile projectile)
        {
            if (!projectile.active || Main.dedServ)
                return true;

            // Owner's own game: remember where a steered minion stands before its AI can teleport it.
            if (projectile.owner == Main.myPlayer)
            {
                if (MarionetteAugment.TryGetSlot(projectile, out _, out _))
                {
                    EnsureRemoteArrays();
                    RememberPosition(projectile);
                }
                return true;
            }

            if (!IsRemoteSteered(projectile))
                return true;

            EnsureRemoteArrays();
            int i = projectile.whoAmI;
            RememberPosition(projectile);

            // Nothing but our own PostAI changes the velocity between ticks, so a different value
            // means an update from the owner has arrived (or this is a brand new projectile).
            if (remoteIdentity[i] != projectile.identity || projectile.velocity != remoteVelocity[i])
            {
                remoteIdentity[i] = projectile.identity;
                remoteVelocity[i] = projectile.velocity;
                remoteIdle[i] = 0;
            }
            else
            {
                remoteIdle[i]++;
            }

            return true;
        }

        private static void FollowOwnersVelocity(Projectile projectile)
        {
            if (remoteVelocity == null)
                return;

            int i = projectile.whoAmI;
            if (remoteIdentity[i] != projectile.identity)
                return;

            if (remoteIdle[i] > RemoteIdleTicks)
                remoteVelocity[i] *= 0.6f;

            projectile.tileCollide = false;
            RestorePosition(projectile);
            projectile.velocity = remoteVelocity[i];
        }

        public override void Unload()
        {
            contactTypes = null;
            MarionetteAugment.ClearTypeSets();
            remoteVelocity = null;
            remoteIdentity = null;
            remoteIdle = null;
            prePosition = null;
            prePositionTick = null;
        }

        public override void PostAI(Projectile projectile)
        {
            if (Main.dedServ || !projectile.active)
                return;

            if (projectile.owner != Main.myPlayer)
            {
                if (IsRemoteSteered(projectile))
                    FollowOwnersVelocity(projectile);
                return;
            }

            // Only projectiles the augment picked a ring slot for are steered (see MarionetteAugment.IsSteerable).

            Player player = Main.player[projectile.owner];
            if (!player.active || player.dead || !player.GetModPlayer<AugmentPlayer>().HasAugment("marionette"))
                return;

            if (!MarionetteAugment.TryGetSlot(projectile, out int index, out int count))
                return;

            // If its AI teleported it (e.g. back to the owner), undo that before steering.
            RestorePosition(projectile);

            // Hard lock: the slot is always on the cursor, never on an enemy. The velocity is set to the
            // whole step to the slot, so after this tick's movement the minion is exactly there and
            // whatever the minion's own AI decided about moving (chasing, dashing, returning) is thrown away.
            float ring = Math.Min(20f + count * 4f, 70f);
            // The Stardust Dragon's head is the pivot of the whole chain: it sits exactly on its slot with
            // no bobbing, and a tiny distance is ignored so it doesn't jitter around the cursor.
            bool dragonHead = projectile.type == ProjectileID.StardustDragon1;
            Vector2 slot = MarionetteAugment.SlotPosition(Main.MouseWorld, index, count, ring, !dragonHead);
            Vector2 toSlot = slot - projectile.Center;
            float dist = toSlot.Length();

            projectile.tileCollide = false;
            projectile.velocity = dist > MaxStep ? toSlot / dist * MaxStep : toSlot;

            if (dragonHead)
            {
                if (dist < 3f)
                    projectile.velocity = Vector2.Zero;

                // The dragon's own AI turns the head toward whatever IT wanted to chase, which fights the lock
                // and makes the head twitch. Turn it smoothly toward where it is actually moving instead,
                // and keep the last facing while it is nearly still.
                if (dragonHeadId != projectile.whoAmI || float.IsNaN(dragonHeadRotation))
                {
                    dragonHeadId = projectile.whoAmI;
                    dragonHeadRotation = projectile.rotation;
                }
                if (projectile.velocity.Length() > 1.5f)
                {
                    float target = projectile.velocity.ToRotation() + MathHelper.PiOver2;
                    dragonHeadRotation = Utils.AngleLerp(dragonHeadRotation, target, 0.2f);
                }
                projectile.rotation = dragonHeadRotation;
            }

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
            // Every other tick keeps their copy close without flooding the connection.
            if (Main.netMode == NetmodeID.MultiplayerClient && Main.GameUpdateCount % 2 == 0)
                projectile.netUpdate = true;
        }
    }
}
