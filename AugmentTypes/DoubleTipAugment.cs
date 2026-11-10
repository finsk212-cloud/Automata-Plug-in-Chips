using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Augments.Core;

namespace Augments
{
    public class DoubleTipAugment : Augment
    {
        public override string Id => "double_tip";
        public override string DisplayName => "Double Tip";
        public override string Description =>
            $"Magic casts fire an additional {AugmentText.SpecialDamage("Twin Harmonic Spell")} dealing {AugmentText.BonusDamage("35% damage")}.\n" +
            $"Alternating between different magic weapons triggers {AugmentText.Trigger("Hyper-Weave")} for {AugmentText.Duration("5 seconds")}, " +
            $"boosting twin damage to {AugmentText.BonusDamage("60%")}, granting {AugmentText.BonusDamage("+15% magic damage")}, and reducing mana cost by {AugmentText.Mana("-50%")}.";

        public override string GetDetailedDescription(Player player) =>
            Description + "\n" +
            AugmentText.Note("(Every magic projectile cast is duplicated as a staggered twin. Swapping magic weapons supercharges the twin spell and slashes mana costs.)");

        public override AugmentRarity Rarity => AugmentRarity.Epic;
        public override AugmentClass Class => AugmentClass.Magic;

        private const int BuffDurationTicks = 300; // 5 seconds
        private const int TwinDelayTicks = 4;      // ~0.06s staggered firing delay
        private static int _lastSoundTick = -1;

        private static readonly Dictionary<int, int> lastItemType = new();
        private static readonly Dictionary<int, int> activeTimer = new();
        private static readonly Dictionary<int, List<PendingTwin>> pendingTwins = new();
        private static readonly Dictionary<int, int> shotCooldown = new();

        public static bool IsHyperWeaveActive(Player player)
        {
            return activeTimer.TryGetValue(player.whoAmI, out int t) && t > 0;
        }

        public static int GetTimer(Player player)
        {
            return activeTimer.TryGetValue(player.whoAmI, out int t) ? t : 0;
        }

        public override bool IsCharging => IsHyperWeaveActive(LocalPlayerState.Player);
        public override int ChargeIndicatorPercent => (int)(GetTimer(LocalPlayerState.Player) / (float)BuffDurationTicks * 100f);
        public override Color StatusValueColor => AugmentTextColors.Trigger;

        public override void OnUpdate(Player player)
        {
            if (player.dead || !player.active)
                return;

            int who = player.whoAmI;

            // 1. Decrement Hyper-Weave active buff timer
            if (activeTimer.TryGetValue(who, out int t) && t > 0)
            {
                activeTimer[who] = t - 1;

                if (who == Main.myPlayer && Main.rand.NextBool(6))
                {
                    int dType = Main.rand.NextBool() ? DustID.MagicMirror : DustID.PinkTorch;
                    Dust d = Dust.NewDustDirect(player.position, player.width, player.height, dType, 0f, 0f, 120, default, 0.9f);
                    d.noGravity = true;
                    d.velocity *= 0.3f;
                }
            }

            if (shotCooldown.TryGetValue(who, out int cd) && cd > 0)
                shotCooldown[who] = cd - 1;

            // 2. Spell-weaving weapon swap detection
            Item held = player.HeldItem;
            if (held != null && !held.IsAir && (held.CountsAsClass(DamageClass.Magic) || held.DamageType == DamageClass.Magic))
            {
                lastItemType.TryGetValue(who, out int last);
                if (last != 0 && last != held.type)
                {
                    activeTimer[who] = BuffDurationTicks;

                    if (who == Main.myPlayer)
                    {
                        SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.50f, Pitch = 0.35f }, player.Center);
                        for (int i = 0; i < 14; i++)
                        {
                            Vector2 vel = Main.rand.NextVector2Circular(3.5f, 3.5f);
                            int dType = Main.rand.NextBool() ? DustID.MagicMirror : DustID.PinkTorch;
                            Dust d = Dust.NewDustPerfect(player.Center, dType, vel, 80, default, 1.2f);
                            d.noGravity = true;
                        }
                    }
                }
                lastItemType[who] = held.type;
            }

            // 3. Process and spawn pending Twin Harmonic Projectiles
            if (who == Main.myPlayer && pendingTwins.TryGetValue(who, out var list))
            {
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    var pending = list[i];
                    pending.TicksRemaining--;

                    if (pending.TicksRemaining <= 0)
                    {
                        SpawnTwin(player, pending);
                        list.RemoveAt(i);
                    }
                }
            }
        }

        public override void UpdateEquips(Player player)
        {
            if (IsHyperWeaveActive(player))
            {
                player.GetDamage(DamageClass.Magic) += 0.15f;
            }
        }

        public override void ModifyManaCost(Player player, Item item, ref float reduce, ref float mult)
        {
            if (item != null && (item.CountsAsClass(DamageClass.Magic) || item.DamageType == DamageClass.Magic))
            {
                if (IsHyperWeaveActive(player))
                    mult *= 0.5f;
                else
                    mult *= 0.85f; // Permanent 15% mana discount baseline
            }
        }

        public override void OnShootProjectile(Player player, Item item, Projectile projectile)
        {
            if (player.whoAmI != Main.myPlayer)
                return;

            AugmentProjectileTag tag = projectile.GetGlobalProjectile<AugmentProjectileTag>();
            if (tag.IsAugmentProcDamage || tag.PreventEchoChamberCopy || projectile.minion || projectile.sentry)
                return;

            bool isMagic = (item != null && (item.CountsAsClass(DamageClass.Magic) || item.DamageType == DamageClass.Magic))
                || projectile.CountsAsClass(DamageClass.Magic)
                || projectile.DamageType == DamageClass.Magic;
            if (!isMagic)
                return;

            int who = player.whoAmI;
            if (shotCooldown.TryGetValue(who, out int cd) && cd > 0)
                return;

            // Small cadence cooldown (4 ticks) to avoid beam / shotgun flood
            shotCooldown[who] = 4;

            if (!pendingTwins.TryGetValue(who, out var list))
            {
                list = new List<PendingTwin>();
                pendingTwins[who] = list;
            }

            if (list.Count < 6)
            {
                list.Add(new PendingTwin(projectile));
            }
        }

        private static void SpawnTwin(Player player, PendingTwin pending)
        {
            float mult = IsHyperWeaveActive(player) ? 0.60f : 0.35f;
            int twinDamage = Math.Max(1, (int)(pending.Damage * mult));

            Vector2 spreadVel = pending.Velocity.RotatedBy(Main.rand.NextFloat(-0.07f, 0.07f)) * 0.96f;

            int twinIndex = Projectile.NewProjectile(
                player.GetSource_FromThis(),
                pending.Position,
                spreadVel,
                pending.Type,
                twinDamage,
                pending.KnockBack * 0.5f,
                player.whoAmI,
                pending.AI0,
                pending.AI1,
                pending.AI2
            );

            if (twinIndex < 0 || twinIndex >= Main.maxProjectiles)
                return;

            Projectile twin = Main.projectile[twinIndex];
            twin.originalDamage = twinDamage;
            twin.DamageType = DamageClass.Magic;
            twin.CritChance = pending.CritChance;
            twin.ArmorPenetration = pending.ArmorPenetration;
            twin.noDropItem = true;
            twin.usesLocalNPCImmunity = true;
            if (twin.localNPCHitCooldown < 10 && twin.localNPCHitCooldown != -1)
                twin.localNPCHitCooldown = 10;

            AugmentProjectileTag twinTag = twin.GetGlobalProjectile<AugmentProjectileTag>();
            twinTag.IsAugmentProcDamage = true;
            twinTag.SourceAugmentId = "double_tip";
            twinTag.CanTriggerOnHitAugments = false;
            twinTag.PreventEchoChamberCopy = true;
            twinTag.PreventRicochetEngineCopy = true;
            twin.alpha = Math.Max(twin.alpha, 70);
            twin.netUpdate = true;

            // Audio & muzzle dust
            if ((int)Main.GameUpdateCount != _lastSoundTick)
            {
                _lastSoundTick = (int)Main.GameUpdateCount;
                SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.35f, Pitch = 0.3f }, player.Center);
            }

            for (int i = 0; i < 4; i++)
            {
                int dType = Main.rand.NextBool() ? DustID.PinkTorch : DustID.MagicMirror;
                Dust d = Dust.NewDustPerfect(twin.Center, dType, spreadVel * 0.2f + Main.rand.NextVector2Circular(1f, 1f), 100, default, 1.1f);
                d.noGravity = true;
            }
        }

        internal class PendingTwin
        {
            public int TicksRemaining = TwinDelayTicks;
            public readonly Vector2 Position;
            public readonly Vector2 Velocity;
            public readonly int Type;
            public readonly int Damage;
            public readonly float KnockBack;
            public readonly float AI0;
            public readonly float AI1;
            public readonly float AI2;
            public readonly int CritChance;
            public readonly int ArmorPenetration;

            public PendingTwin(Projectile projectile)
            {
                Position = projectile.Center;
                Velocity = projectile.velocity;
                Type = projectile.type;
                Damage = projectile.damage;
                KnockBack = projectile.knockBack;
                AI0 = projectile.ai[0];
                AI1 = projectile.ai[1];
                AI2 = projectile.ai[2];
                CritChance = projectile.CritChance;
                ArmorPenetration = projectile.ArmorPenetration;
            }
        }
    }
}
