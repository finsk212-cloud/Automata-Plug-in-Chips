using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace Augments
{
    public class EchoChamberAugment : Augment
    {
        public override string Id => "echo_chamber";
        public override string DisplayName => "Echo Chamber";
        public override string Description =>
            $"Firing a ranged projectile also fires an echo shot for {AugmentText.BonusDamage("20% damage")}. Echo shots trigger {AugmentText.OnHit("on-hit")} effects at 50% effectiveness.";

        public override AugmentRarity Rarity => AugmentRarity.Legendary;
        public override AugmentClass Class => AugmentClass.Ranged;

        private const float EchoDamageMultiplier = 0.2f;
        private const float EchoEffectiveness = 0.5f;
        private const int EchoDelayTicks = 12;
        private static int _lastSoundTick = -1;

        public override void OnShootProjectile(Player player, Item item, Projectile projectile)
        {
            if (projectile.owner != Main.myPlayer || !projectile.CountsAsClass(DamageClass.Ranged))
                return;

            AugmentProjectileTag originalTag = projectile.GetGlobalProjectile<AugmentProjectileTag>();
            if (originalTag.PreventEchoChamberCopy)
                return;

            if (originalTag.IsAugmentProcDamage && !originalTag.CanTriggerOnHitAugments)
                return;

            player.GetModPlayer<AugmentPlayer>().EchoChamberPendingEchoes.Add(new PendingEcho(projectile));
        }

        public override void OnUpdate(Player player)
        {
            if (player.whoAmI != Main.myPlayer)
                return;

            List<PendingEcho> pendingEchoes = player.GetModPlayer<AugmentPlayer>().EchoChamberPendingEchoes;
            for (int i = pendingEchoes.Count - 1; i >= 0; i--)
            {
                PendingEcho pending = pendingEchoes[i];
                pending.TicksRemaining--;

                if (pending.TicksRemaining > 0)
                    continue;

                SpawnEcho(player, pending);
                pendingEchoes.RemoveAt(i);
            }
        }

        private static void SpawnEcho(Player player, PendingEcho pending)
        {
            int echoDamage = Math.Max(1, (int)(pending.Damage * EchoDamageMultiplier));

            int echoIndex = Projectile.NewProjectile(
                player.GetSource_FromThis(),
                pending.Position,
                pending.Velocity,
                pending.Type,
                echoDamage,
                pending.KnockBack,
                player.whoAmI,
                pending.AI0,
                pending.AI1,
                pending.AI2
            );

            if (echoIndex < 0 || echoIndex >= Main.maxProjectiles)
                return;

            Projectile echo = Main.projectile[echoIndex];
            echo.originalDamage = echoDamage;
            echo.DamageType = pending.DamageType;
            echo.CritChance = pending.CritChance;
            echo.ArmorPenetration = pending.ArmorPenetration;
            echo.noDropItem = true;
            echo.usesLocalNPCImmunity = true;
            if (echo.localNPCHitCooldown < 10 && echo.localNPCHitCooldown != -1)
                echo.localNPCHitCooldown = 10;

            AugmentProjectileTag echoTag = echo.GetGlobalProjectile<AugmentProjectileTag>();
            echoTag.IsAugmentProcDamage = true;
            echoTag.CanTriggerOnHitAugments = true;
            echoTag.OnHitEffectiveness = EchoEffectiveness;
            echoTag.PreventEchoChamberCopy = true;
            echoTag.IsEchoChamberEcho = true;
            echoTag.FromRocketLauncher = pending.FromRocketLauncher;
            echo.alpha = Math.Max(echo.alpha, 110);
            echo.netUpdate = true;

            if (player.whoAmI == Main.myPlayer && (int)Main.GameUpdateCount != _lastSoundTick)
            {
                _lastSoundTick = (int)Main.GameUpdateCount;
                SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.35f, Pitch = 0.25f }, player.Center);
            }
        }

        // Queued echoes live on AugmentPlayer (per-player), since this augment
        // instance is a singleton shared by every player.
        internal class PendingEcho
        {
            public int TicksRemaining = EchoDelayTicks;
            public readonly Vector2 Position;
            public readonly Vector2 Velocity;
            public readonly int Type;
            public readonly int Damage;
            public readonly float KnockBack;
            public readonly float AI0;
            public readonly float AI1;
            public readonly float AI2;
            public readonly DamageClass DamageType;
            public readonly int CritChance;
            public readonly int ArmorPenetration;
            public readonly bool FromRocketLauncher;

            public PendingEcho(Projectile projectile)
            {
                FromRocketLauncher = projectile.GetGlobalProjectile<AugmentProjectileTag>().FromRocketLauncher;
                Position = projectile.Center;
                Velocity = projectile.velocity;
                Type = projectile.type;
                Damage = projectile.damage;
                KnockBack = projectile.knockBack;
                AI0 = projectile.ai[0];
                AI1 = projectile.ai[1];
                AI2 = projectile.ai[2];
                DamageType = projectile.DamageType;
                CritChance = projectile.CritChance;
                ArmorPenetration = projectile.ArmorPenetration;
            }
        }
    }
}
