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
    public class BladeDanceAugment : Augment
    {
        public override string Id => "blade_dance";
        public override string DisplayName => "Blade Dance";
        public override string Description =>
            $"Landing {AugmentText.Trigger("6 melee hits")} primes {AugmentText.Crit("Blade Dance")}.\n" +
            $"Your next strike unleashes a flurry of {AugmentText.SpecialDamage("5 Phantom Blade Slashes")} across surrounding targets over 0.35s (dealing {AugmentText.BonusDamage("70% damage")} each).\n" +
            $"Grants {AugmentText.Defense("invulnerability")} during the dance flurry. {AugmentText.Cooldown("8s cooldown")}.";

        public override string GetDetailedDescription(Player player) =>
            Description + "\n" +
            AugmentText.Note("(Melee strikes build combo stacks. At 6 stacks, you enter an untouchable phantom flurry cutting down nearby enemies.)");

        public override AugmentRarity Rarity => AugmentRarity.Legendary;
        public override AugmentClass Class => AugmentClass.Melee;

        private const int MaxComboHits = 6;
        private const int ComboResetTicks = 300;  // 5 seconds
        private const int CooldownTicks = 480;    // 8 seconds
        private const int SlashCount = 5;
        private const int SlashCadenceTicks = 4;
        private const float SearchRadius = 380f;

        private static readonly Dictionary<int, int> comboStacks = new();
        private static readonly Dictionary<int, int> comboResetTimer = new();
        private static readonly Dictionary<int, int> cooldownTimer = new();
        private static readonly Dictionary<int, bool> danceActive = new();
        private static readonly Dictionary<int, int> danceTimer = new();
        private static readonly Dictionary<int, int> slashesRemaining = new();
        private static readonly Dictionary<int, Vector2> lastTargetPos = new();
        private static readonly Dictionary<int, int> slashDamage = new();
        private static readonly Dictionary<int, int> slashCrit = new();

        public static int GetCooldown(Player player) =>
            cooldownTimer.TryGetValue(player.whoAmI, out int cd) ? cd : 0;

        public static int GetCombo(Player player) =>
            comboStacks.TryGetValue(player.whoAmI, out int c) ? c : 0;

        public static bool IsDancing(Player player) =>
            danceActive.TryGetValue(player.whoAmI, out bool a) && a;

        public override bool IsCharging
        {
            get
            {
                Player p = LocalPlayerState.Player;
                return IsDancing(p) || GetCombo(p) > 0 || GetCooldown(p) > 0;
            }
        }

        public override int ChargeIndicatorPercent
        {
            get
            {
                Player p = LocalPlayerState.Player;
                int cd = GetCooldown(p);
                if (cd > 0)
                    return (int)((1f - (cd / (float)CooldownTicks)) * 100f);

                return (int)(GetCombo(p) / (float)MaxComboHits * 100f);
            }
        }

        public override Color StatusValueColor
        {
            get
            {
                Player p = LocalPlayerState.Player;
                if (GetCooldown(p) > 0)
                    return AugmentTextColors.Cooldown;
                return AugmentTextColors.Crit;
            }
        }

        public override void OnHitNPCWithItem(
            Player player,
            Item item,
            NPC target,
            NPC.HitInfo hit,
            AugmentHitSource source,
            float effectiveness)
        {
            if (player.whoAmI != Main.myPlayer)
                return;

            if (source == AugmentHitSource.NormalAttack && item.CountsAsClass(DamageClass.Melee))
            {
                RegisterMeleeHit(player, target, item.damage, (int)player.GetTotalCritChance(DamageClass.Melee));
            }
        }

        public override void OnHitNPCWithProj(
            Player player,
            Projectile proj,
            NPC target,
            NPC.HitInfo hit,
            AugmentHitSource source,
            float effectiveness)
        {
            if (player.whoAmI != Main.myPlayer)
                return;

            var tag = proj.GetGlobalProjectile<AugmentProjectileTag>();
            if (tag.IsAugmentProcDamage || proj.minion || proj.sentry)
                return;

            if (source == AugmentHitSource.NormalAttack && proj.CountsAsClass(DamageClass.Melee))
            {
                RegisterMeleeHit(player, target, proj.damage, proj.CritChance);
            }
        }

        private static void RegisterMeleeHit(Player player, NPC target, int baseDamage, int critChance)
        {
            int who = player.whoAmI;

            if (cooldownTimer.TryGetValue(who, out int cd) && cd > 0)
                return;

            if (danceActive.TryGetValue(who, out bool active) && active)
                return;

            comboResetTimer[who] = ComboResetTicks;
            int current = comboStacks.TryGetValue(who, out int c) ? c : 0;
            current++;

            if (current >= MaxComboHits)
            {
                // Trigger Blade Dance!
                comboStacks[who] = 0;
                danceActive[who] = true;
                slashesRemaining[who] = SlashCount;
                danceTimer[who] = 0;
                slashDamage[who] = Math.Max(1, (int)(baseDamage * 0.70f));
                slashCrit[who] = critChance;
                lastTargetPos[who] = target.Center;

                SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.65f, Pitch = 0.4f }, player.Center);
                SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.85f, Pitch = -0.2f }, target.Center);

                for (int i = 0; i < 18; i++)
                {
                    Vector2 vel = Main.rand.NextVector2Circular(4.5f, 4.5f);
                    int dType = Main.rand.NextBool() ? DustID.CrimsonTorch : DustID.SilverCoin;
                    Dust d = Dust.NewDustPerfect(player.Center, dType, vel, 80, default, 1.3f);
                    d.noGravity = true;
                }
            }
            else
            {
                comboStacks[who] = current;
                SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.35f, Pitch = 0.1f * current }, player.Center);

                for (int i = 0; i < 3; i++)
                {
                    Dust d = Dust.NewDustPerfect(target.Center, DustID.CrimsonTorch, Main.rand.NextVector2Circular(2f, 2f), 100, default, 1.1f);
                    d.noGravity = true;
                }
            }
        }

        public override void OnUpdate(Player player)
        {
            int who = player.whoAmI;

            if (player.dead || !player.active)
            {
                if (player.dead)
                {
                    comboStacks[who] = 0;
                    comboResetTimer[who] = 0;
                    cooldownTimer[who] = 0;
                    danceActive[who] = false;
                    slashesRemaining[who] = 0;
                }
                return;
            }

            // Decrement cooldown
            if (cooldownTimer.TryGetValue(who, out int cd) && cd > 0)
                cooldownTimer[who] = cd - 1;

            // Decrement combo reset timer
            if (comboResetTimer.TryGetValue(who, out int cr) && cr > 0)
            {
                comboResetTimer[who] = cr - 1;
                if (comboResetTimer[who] <= 0)
                    comboStacks[who] = 0;
            }

            // Process active Blade Dance flurry
            if (danceActive.TryGetValue(who, out bool active) && active)
            {
                // Grant invulnerability during dance flurry
                player.immune = true;
                player.immuneTime = Math.Max(player.immuneTime, 30);
                player.immuneNoBlink = true;

                // Speed afterimage particles
                if (who == Main.myPlayer && Main.rand.NextBool(2))
                {
                    int dType = Main.rand.NextBool() ? DustID.CrimsonTorch : DustID.SilverCoin;
                    Dust d = Dust.NewDustDirect(player.position, player.width, player.height, dType, 0f, 0f, 100, default, 1.2f);
                    d.noGravity = true;
                    d.velocity *= 0.2f;
                }

                danceTimer.TryGetValue(who, out int timer);

                if (who == Main.myPlayer && timer % SlashCadenceTicks == 0 && slashesRemaining.TryGetValue(who, out int slashes) && slashes > 0)
                {
                    SpawnPhantomSlash(player);
                    slashesRemaining[who] = slashes - 1;

                    if (slashesRemaining[who] <= 0)
                    {
                        danceActive[who] = false;
                        cooldownTimer[who] = CooldownTicks;
                        SoundEngine.PlaySound(SoundID.Item105 with { Volume = 0.6f, Pitch = 0.2f }, player.Center);
                    }
                }

                danceTimer[who] = timer + 1;
            }
        }

        private static void SpawnPhantomSlash(Player player)
        {
            int who = player.whoAmI;
            Vector2 anchor = lastTargetPos.TryGetValue(who, out Vector2 pos) ? pos : player.Center;

            // Find closest living hostile enemy near anchor or player
            NPC bestTarget = null;
            float bestDistSq = SearchRadius * SearchRadius;

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.friendly || npc.dontTakeDamage || npc.life <= 0)
                    continue;

                float distSq = Vector2.DistanceSquared(anchor, npc.Center);
                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    bestTarget = npc;
                }
            }

            Vector2 slashCenter = bestTarget != null ? bestTarget.Center : anchor;
            lastTargetPos[who] = slashCenter;

            float slashAngle = Main.rand.NextFloat(MathHelper.TwoPi);
            Vector2 spawnPos = slashCenter - slashAngle.ToRotationVector2() * 45f;
            Vector2 slashVel = slashAngle.ToRotationVector2() * 18f;

            int dmg = slashDamage.TryGetValue(who, out int d) ? d : 50;
            int crit = slashCrit.TryGetValue(who, out int c) ? c : 4;

            int projId = Projectile.NewProjectile(
                player.GetSource_FromThis(),
                spawnPos,
                slashVel,
                ModContent.ProjectileType<BladeDanceSlashProjectile>(),
                dmg,
                5.5f,
                player.whoAmI
            );

            if (projId >= 0 && projId < Main.maxProjectiles)
            {
                Projectile p = Main.projectile[projId];
                p.CritChance = crit;
            }

            SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.70f, Pitch = Main.rand.NextFloat(-0.1f, 0.3f) }, slashCenter);
        }
    }
}
