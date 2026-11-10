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
    public class WildMagicAugment : Augment
    {
        public override string Id => "wild_magic";
        public override string DisplayName => "Wild Magic";
        public override string Description =>
            $"Magic attacks have a {FormatCurrentChance(ProcChance)} to trigger a random {AugmentText.Trigger("Wild Surge")} ({AugmentText.Crit("guaranteed on Critical Strike")}):\n" +
            $"• {AugmentText.Frostburn("Frost Nova")}: Chills and deals {AugmentText.SpecialDamage("22 magic damage")}\n" +
            $"• {AugmentText.BonusDamage("Supernova")}: AOE blast dealing {AugmentText.SpecialDamage("32 magic damage")}\n" +
            $"• {AugmentText.Electrified("Chain Zap")}: Arc lightning jumping to 3 enemies for {AugmentText.SpecialDamage("26 magic damage")}\n" +
            $"• {AugmentText.Mana("Mana Cascade")}: Restores 40 Mana\n" +
            $"• {AugmentText.Active("★ JACKPOT ★")}: 5% chance to unleash ALL surges at once!";

        public override string GetDetailedDescription(Player player) =>
            AugmentScaling.ApplyToText(Description, GetScalingEntries()) + "\n" +
            AugmentText.Note("(Critical strikes guarantee a Wild Surge with double the chance to hit the Jackpot. Internal cooldown: 0.16s.)");

        public override AugmentRarity Rarity => AugmentRarity.Epic;
        public override AugmentClass Class => AugmentClass.Magic;

        public const float ProcChance = 0.20f;
        public const int FrostDamage = 22;
        public const int SupernovaDamage = 32;
        public const int ZapDamage = 26;
        public const int ManaRestore = 40;

        public override (string label, int baseValue)[] GetScalingEntries()
        {
            return new[]
            {
                ("Frost Nova damage", FrostDamage),
                ("Supernova damage", SupernovaDamage),
                ("Chain Zap damage", ZapDamage)
            };
        }

        private static readonly Dictionary<int, int> cooldowns = new();

        public override void OnHitNPCWithProj(Player player, Projectile proj, NPC target, NPC.HitInfo hit, AugmentHitSource source, float effectiveness)
        {
            if (source == AugmentHitSource.AugmentProc)
                return;

            bool isMagic = proj.CountsAsClass(DamageClass.Magic) || proj.DamageType == DamageClass.Magic;
            if (!isMagic)
                return;

            TryTriggerWildSurge(player, target, hit);
        }

        public override void OnHitNPCWithItem(Player player, Item item, NPC target, NPC.HitInfo hit, AugmentHitSource source, float effectiveness)
        {
            if (source == AugmentHitSource.AugmentProc)
                return;

            bool isMagic = item.CountsAsClass(DamageClass.Magic) || item.DamageType == DamageClass.Magic;
            if (!isMagic)
                return;

            TryTriggerWildSurge(player, target, hit);
        }

        private void TryTriggerWildSurge(Player player, NPC target, NPC.HitInfo hit)
        {
            if (target == null || !target.active || target.friendly || target.dontTakeDamage || target.lifeMax <= 5)
                return;

            int currentTick = (int)Main.GameUpdateCount;
            int who = player.whoAmI;
            if (cooldowns.TryGetValue(who, out int lastTick) && currentTick - lastTick < 10)
                return;

            bool shouldProc = hit.Crit || RollFortuneChance(player, ProcChance);
            if (!shouldProc)
                return;

            cooldowns[who] = currentTick;

            if (who != Main.myPlayer)
                return;

            bool isJackpot = Main.rand.NextFloat() < (hit.Crit ? 0.10f : 0.05f);
            if (isJackpot)
            {
                TriggerJackpot(player, target);
            }
            else
            {
                int surge = Main.rand.Next(4);
                switch (surge)
                {
                    case 0: TriggerFrostNova(player, target); break;
                    case 1: TriggerSupernova(player, target); break;
                    case 2: TriggerChainZap(player, target); break;
                    case 3: TriggerManaCascade(player, target); break;
                }
            }
        }

        private void TriggerFrostNova(Player player, NPC target)
        {
            SoundEngine.PlaySound(SoundID.Item30 with { Volume = 0.55f, Pitch = 0.2f }, target.Center);

            int dmg = Math.Max(1, (int)player.GetTotalDamage(DamageClass.Magic).ApplyTo(AugmentScaling.Scale(FrostDamage)));
            const float radius = 100f;

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC n = Main.npc[i];
                if (!n.active || n.friendly || n.dontTakeDamage || n.lifeMax <= 5)
                    continue;

                if (Vector2.DistanceSquared(target.Center, n.Center) <= radius * radius)
                {
                    var hitInfo = new NPC.HitInfo
                    {
                        Damage = dmg,
                        SourceDamage = dmg,
                        HitDirection = n.Center.X >= target.Center.X ? 1 : -1,
                        DamageType = DamageClass.Magic,
                        HideCombatText = true
                    };
                    n.StrikeNPC(hitInfo);
                    if (Main.netMode != NetmodeID.SinglePlayer)
                        NetMessage.SendStrikeNPC(n, in hitInfo);

                    n.AddBuff(BuffID.Frostburn, 240);
                    n.AddBuff(BuffID.Chilled, 120);
                }
            }

            CombatText.NewText(target.Hitbox, AugmentTextColors.Frostburn, dmg);
            AugmentDamageTracker.RecordChipHit(Id, dmg, false);

            for (int i = 0; i < 20; i++)
            {
                Vector2 vel = Main.rand.NextVector2Circular(5f, 5f);
                Dust d = Dust.NewDustPerfect(target.Center, DustID.IceTorch, vel, 60, default, Main.rand.NextFloat(1.2f, 1.8f));
                d.noGravity = true;
            }
        }

        private void TriggerSupernova(Player player, NPC target)
        {
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.60f, Pitch = 0.35f }, target.Center);

            int dmg = Math.Max(1, (int)player.GetTotalDamage(DamageClass.Magic).ApplyTo(AugmentScaling.Scale(SupernovaDamage)));
            const float radius = 110f;

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC n = Main.npc[i];
                if (!n.active || n.friendly || n.dontTakeDamage || n.lifeMax <= 5)
                    continue;

                if (Vector2.DistanceSquared(target.Center, n.Center) <= radius * radius)
                {
                    var hitInfo = new NPC.HitInfo
                    {
                        Damage = dmg,
                        SourceDamage = dmg,
                        HitDirection = n.Center.X >= target.Center.X ? 1 : -1,
                        DamageType = DamageClass.Magic,
                        HideCombatText = true
                    };
                    n.StrikeNPC(hitInfo);
                    if (Main.netMode != NetmodeID.SinglePlayer)
                        NetMessage.SendStrikeNPC(n, in hitInfo);

                    n.AddBuff(BuffID.OnFire3, 240); // Hellfire
                }
            }

            CombatText.NewText(target.Hitbox, AugmentTextColors.SpecialDamage, dmg);
            AugmentDamageTracker.RecordChipHit(Id, dmg, false);

            for (int i = 0; i < 24; i++)
            {
                Vector2 vel = Main.rand.NextVector2Circular(6.5f, 6.5f);
                Dust d = Dust.NewDustPerfect(target.Center, DustID.SolarFlare, vel, 60, default, Main.rand.NextFloat(1.4f, 2.1f));
                d.noGravity = true;
            }
        }

        private void TriggerChainZap(Player player, NPC target)
        {
            SoundEngine.PlaySound(SoundID.Item93 with { Volume = 0.50f, Pitch = 0.25f }, target.Center);

            int dmg = Math.Max(1, (int)player.GetTotalDamage(DamageClass.Magic).ApplyTo(AugmentScaling.Scale(ZapDamage)));
            const float maxRange = 320f;
            int chained = 0;

            for (int i = 0; i < Main.maxNPCs && chained < 3; i++)
            {
                NPC n = Main.npc[i];
                if (!n.active || n.whoAmI == target.whoAmI || n.friendly || n.dontTakeDamage || n.lifeMax <= 5)
                    continue;

                float dist = Vector2.Distance(target.Center, n.Center);
                if (dist <= maxRange)
                {
                    chained++;
                    var hitInfo = new NPC.HitInfo
                    {
                        Damage = dmg,
                        SourceDamage = dmg,
                        HitDirection = n.Center.X >= target.Center.X ? 1 : -1,
                        DamageType = DamageClass.Magic,
                        HideCombatText = true
                    };
                    n.StrikeNPC(hitInfo);
                    if (Main.netMode != NetmodeID.SinglePlayer)
                        NetMessage.SendStrikeNPC(n, in hitInfo);

                    n.AddBuff(BuffID.Electrified, 180);

                    // Lightning trail between target and chained enemy
                    int steps = Math.Max(4, (int)(dist / 20f));
                    for (int s = 0; s < steps; s++)
                    {
                        Vector2 pos = Vector2.Lerp(target.Center, n.Center, s / (float)steps);
                        Dust d = Dust.NewDustPerfect(pos + Main.rand.NextVector2Circular(4f, 4f), DustID.Electric, Vector2.Zero, 80, default, 0.85f);
                        d.noGravity = true;
                    }
                }
            }

            CombatText.NewText(target.Hitbox, AugmentTextColors.Electrified, dmg);
            AugmentDamageTracker.RecordChipHit(Id, dmg, false);
        }

        private void TriggerManaCascade(Player player, NPC target)
        {
            SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.55f, Pitch = 0.35f }, player.Center);

            player.statMana = Math.Min(player.statManaMax2, player.statMana + ManaRestore);
            player.ManaEffect(ManaRestore);

            for (int i = 0; i < 14; i++)
            {
                Vector2 vel = (player.Center - target.Center).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(3f, 7f)
                    + Main.rand.NextVector2Circular(2f, 2f);
                Dust d = Dust.NewDustPerfect(target.Center, DustID.MagicMirror, vel, 80, new Color(56, 189, 248), Main.rand.NextFloat(1.1f, 1.6f));
                d.noGravity = true;
            }
        }

        private void TriggerJackpot(Player player, NPC target)
        {
            SoundEngine.PlaySound(SoundID.CoinPickup with { Pitch = 0.2f, Volume = 0.7f }, target.Center);
            SoundEngine.PlaySound(SoundID.Item29 with { Pitch = 0.3f, Volume = 0.6f }, target.Center);

            CombatText.NewText(target.Hitbox, AugmentTextColors.Active, "★ JACKPOT! ★", true);

            TriggerFrostNova(player, target);
            TriggerSupernova(player, target);
            TriggerChainZap(player, target);
            TriggerManaCascade(player, target);

            // Kaleidoscopic rainbow fireworks
            for (int i = 0; i < 30; i++)
            {
                Vector2 vel = Main.rand.NextVector2Circular(8f, 8f);
                Dust d = Dust.NewDustPerfect(target.Center, DustID.RainbowRod, vel, 60, default, Main.rand.NextFloat(1.3f, 2.0f));
                d.noGravity = true;
            }
        }
    }
}
