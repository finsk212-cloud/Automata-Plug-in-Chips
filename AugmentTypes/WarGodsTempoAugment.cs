using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace Augments
{
    // Legendary Melee. Two ideas in one:
    //  - Drumbeat: melee hits build Tempo, but only when they land on the beat (0.3s to 1.0s after your last
    //    counted hit). Hitting too fast adds nothing; missing the beat halves your Tempo.
    //  - Conquest: melee kills add extra Tempo. At full Tempo you raise a War Banner: you and your allies
    //    nearby deal more damage and take less, and your kills release shockwaves.
    //
    // Multiplayer: Tempo, the beat timing and the kill tags all live on the owner's own game (hits are
    // dealt there; kills reach it through the KillCredit packet, see RunsKillHookOnKillerClient). The banner
    // is carried by a player buff, which vanilla syncs to everyone, and allies pull the aura onto themselves.
    // Works with other mods' melee weapons (Calamity's included) because it goes by damage class, not item id.
    public class WarGodsTempoAugment : Augment
    {
        public override string Id => "war_gods_tempo";
        public override string DisplayName => "War God's Tempo";
        public override string Description =>
            $"{AugmentText.Trigger("Drumbeat:")} melee hits landing {AugmentText.Duration("0.3-1.0 seconds")} after your last counted hit build {AugmentText.Trigger("Tempo")} (max {MaxTempo}). " +
            $"Each stack gives {AugmentText.BonusDamage("+2% melee damage")}. Missing the beat halves your Tempo.\n" +
            $"{AugmentText.Trigger("Conquest:")} melee kills add {KillStacks} Tempo. At full Tempo you raise a {AugmentText.Trigger("War Banner")} for {AugmentText.Duration("10 seconds")}: " +
            $"you and allies within {BannerRadiusTiles} tiles deal {AugmentText.BonusDamage("+15% damage")} and take {AugmentText.Defense("10% less damage")}, " +
            $"and your melee kills release a {AugmentText.BonusDamage($"{AugmentScaling.Scale(ShockwaveDamage)} damage")} shockwave. {AugmentText.Cooldown("15 seconds")} before the next banner.";

        public override string GetDetailedDescription(Player player) =>
            AugmentScaling.ApplyToText(Description, GetScalingEntries()) + "\n" +
            AugmentText.Note("(Hits faster than the beat are ignored, never punished, so fast weapons are fine. Tempo fades after 5 seconds without a melee hit and cannot grow while the banner is up. Works with other mods' melee weapons, including Calamity's.)");

        public override AugmentRarity Rarity => AugmentRarity.Legendary;
        public override AugmentClass Class => AugmentClass.Melee;

        // Kills are only seen by the server; this chip needs the killer's own game (its hit tags and Tempo live there).
        public override bool RunsKillHookOnKillerClient => true;

        private const int MaxTempo = 15;
        private const float DamagePerStack = 0.02f;
        private const int KillStacks = 2;

        // Drumbeat timing, in ticks.
        private const int MinBeatTicks = 18;      // 0.3s
        private const int MaxBeatTicks = 60;      // 1.0s
        private const int ResetDelayTicks = 300;  // Tempo fades after 5s without a melee hit

        // War Banner.
        private const int BannerTicks = 600;          // 10s
        private const int BannerCooldownTicks = 900;  // 15s after it ends
        private const float BannerRadius = 600f;
        private const int BannerRadiusTiles = 37;
        private const int ShockwaveDamage = 90;
        private const int ShockwaveCooldownTicks = 30;

        // Owner's own game only.
        private static readonly Dictionary<int, uint> lastBeatTick = new();
        private static readonly Dictionary<int, int> bannerCooldown = new();
        private static readonly Dictionary<int, int> shockwaveCooldown = new();

        public override (string label, int baseValue)[] GetScalingEntries()
        {
            return new[] { ("Shockwave damage", ShockwaveDamage) };
        }

        public override void OnAcquire(Player player)
        {
            int who = player.whoAmI;
            lastBeatTick.Remove(who);
            bannerCooldown[who] = 0;
            shockwaveCooldown[who] = 0;
            var ap = player.GetModPlayer<AugmentPlayer>();
            ap.WarGodsTempoStacks = 0;
            ap.WarGodsTempoResetTimer = 0;
        }

        private static bool BannerUp(Player player) =>
            player.active && !player.dead && player.HasBuff(ModContent.BuffType<WarBannerBearerBuff>());

        public override void UpdateEquips(Player player)
        {
            int stacks = player.GetModPlayer<AugmentPlayer>().WarGodsTempoStacks;
            if (stacks > 0)
                player.GetDamage(DamageClass.Melee) += stacks * DamagePerStack;
        }

        public override void OnHitNPCWithItem(Player player, Item item, NPC target, NPC.HitInfo hit, AugmentHitSource source, float effectiveness)
        {
            if (source == AugmentHitSource.NormalAttack && item.CountsAsClass(DamageClass.Melee))
                RegisterMeleeHit(player, target);
        }

        public override void OnHitNPCWithProj(Player player, Projectile proj, NPC target, NPC.HitInfo hit, AugmentHitSource source, float effectiveness)
        {
            if (source == AugmentHitSource.NormalAttack && proj.CountsAsClass(DamageClass.Melee))
                RegisterMeleeHit(player, target);
        }

        public override void OnUpdate(Player player)
        {
            int who = player.whoAmI;

            // Every player's game: draw the banner and let the local player pick up its aura.
            if (BannerUp(player))
                RunBanner(player);

            if (who != Main.myPlayer)
                return;

            if (bannerCooldown.TryGetValue(who, out int cd) && cd > 0)
                bannerCooldown[who] = cd - 1;
            if (shockwaveCooldown.TryGetValue(who, out int sc) && sc > 0)
                shockwaveCooldown[who] = sc - 1;

            var ap = player.GetModPlayer<AugmentPlayer>();
            if (ap.WarGodsTempoResetTimer > 0)
            {
                ap.WarGodsTempoResetTimer--;
                if (ap.WarGodsTempoResetTimer == 0)
                {
                    ap.WarGodsTempoStacks = 0;
                    lastBeatTick.Remove(who);
                }
            }

            // Full Tempo that had to wait for the cooldown raises the banner as soon as it can.
            if (ap.WarGodsTempoStacks >= MaxTempo && !BannerUp(player) && bannerCooldown.GetValueOrDefault(who) <= 0)
                StartBanner(player, ap);
        }

        // ---- Drumbeat ----

        private static void RegisterMeleeHit(Player player, NPC target)
        {
            if (player.whoAmI != Main.myPlayer || Main.dedServ)
                return;

            // Mark the enemy so a later kill counts as this player's melee kill (Conquest).
            target.GetGlobalNPC<AugmentWarGodTagNPC>().TagHit(player.whoAmI);

            var ap = player.GetModPlayer<AugmentPlayer>();
            ap.WarGodsTempoResetTimer = ResetDelayTicks;

            // No Tempo while the banner is up; it is spent.
            if (BannerUp(player))
                return;

            int who = player.whoAmI;
            uint now = Main.GameUpdateCount;
            bool hadBeat = lastBeatTick.TryGetValue(who, out uint last);
            long gap = hadBeat ? (long)now - last : long.MaxValue;

            if (!hadBeat || gap > ResetDelayTicks)
            {
                ap.WarGodsTempoStacks = 0;
                GainTempo(player, ap, 1);
                lastBeatTick[who] = now;
            }
            else if (gap < MinBeatTicks)
            {
                // Same beat (a multi-hit swing, a fast weapon): neither a gain nor a miss.
            }
            else if (gap <= MaxBeatTicks)
            {
                GainTempo(player, ap, 1);
                lastBeatTick[who] = now;
            }
            else
            {
                // Missed the beat: the groove is broken, half the Tempo is lost, and this hit starts a new one.
                ap.WarGodsTempoStacks /= 2;
                GainTempo(player, ap, 1);
                lastBeatTick[who] = now;
            }
        }

        private static void GainTempo(Player player, AugmentPlayer ap, int amount)
        {
            ap.WarGodsTempoStacks = Math.Min(MaxTempo, ap.WarGodsTempoStacks + amount);
            ap.WarGodsTempoResetTimer = ResetDelayTicks;

            float pitch = -0.7f + ap.WarGodsTempoStacks * 0.05f;
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.30f, Pitch = pitch }, player.Center);
            for (int i = 0; i < 6; i++)
            {
                Vector2 dir = Main.rand.NextVector2CircularEdge(1f, 1f);
                Dust d = Dust.NewDustPerfect(player.Center + dir * 18f, DustID.GoldFlame, dir * 2.2f, 80, default, 1.1f);
                d.noGravity = true;
            }

            if (ap.WarGodsTempoStacks >= MaxTempo && bannerCooldown.GetValueOrDefault(player.whoAmI) <= 0)
                StartBanner(player, ap);
        }

        // ---- Conquest ----

        public override void OnKillNPC(Player player, NPC npc)
        {
            if (player.whoAmI != Main.myPlayer || Main.dedServ)
                return;

            AugmentWarGodTagNPC tag = npc.GetGlobalNPC<AugmentWarGodTagNPC>();
            if (!tag.IsTaggedBy(player.whoAmI))
                return;
            tag.ClearTag();

            int who = player.whoAmI;
            if (BannerUp(player))
            {
                if (shockwaveCooldown.GetValueOrDefault(who) > 0)
                    return;
                shockwaveCooldown[who] = ShockwaveCooldownTicks;

                Projectile.NewProjectile(
                    player.GetSource_FromThis(),
                    npc.Center,
                    Vector2.Zero,
                    ModContent.ProjectileType<WarGodShockwaveProjectile>(),
                    AugmentScaling.Scale(ShockwaveDamage),
                    0f,
                    who);
                return;
            }

            var ap = player.GetModPlayer<AugmentPlayer>();
            GainTempo(player, ap, KillStacks);
        }

        // ---- War Banner ----

        private static void StartBanner(Player player, AugmentPlayer ap)
        {
            int who = player.whoAmI;
            ap.WarGodsTempoStacks = 0;
            ap.WarGodsTempoResetTimer = 0;
            lastBeatTick.Remove(who);
            bannerCooldown[who] = BannerTicks + BannerCooldownTicks;

            player.AddBuff(ModContent.BuffType<WarBannerBearerBuff>(), BannerTicks);

            // Buffs are synced by vanilla, but the other players need this one right now: it is how their
            // games learn the banner is up.
            if (Main.netMode == NetmodeID.MultiplayerClient)
                NetMessage.SendData(MessageID.PlayerBuffs, -1, -1, null, who);

            SoundEngine.PlaySound(SoundID.Item119 with { Volume = 0.7f, Pitch = -0.2f }, player.Center);
            for (int i = 0; i < 40; i++)
            {
                Vector2 dir = Main.rand.NextVector2CircularEdge(1f, 1f);
                Dust d = Dust.NewDustPerfect(player.Center + dir * 20f, DustID.GoldFlame, dir * Main.rand.NextFloat(3f, 8f), 60, default, 1.5f);
                d.noGravity = true;
            }
        }

        // Runs on every game for a player whose banner is up.
        private static void RunBanner(Player owner)
        {
            Lighting.AddLight(owner.Center, 0.55f, 0.40f, 0.08f);

            // Ring of embers marking the edge of the aura.
            if (Main.GameUpdateCount % 3 == 0)
            {
                for (int i = 0; i < 3; i++)
                {
                    Vector2 dir = Main.rand.NextVector2CircularEdge(1f, 1f);
                    Dust d = Dust.NewDustPerfect(owner.Center + dir * BannerRadius, DustID.GoldFlame, new Vector2(0f, -0.8f), 90, default, 1.0f);
                    d.noGravity = true;
                }
            }

            // Each game gives only its own player the aura; the bearer already has the bonuses from their buff.
            if (Main.dedServ)
                return;

            Player local = Main.LocalPlayer;
            if (local == null || !local.active || local.dead || local.whoAmI == owner.whoAmI)
                return;
            if (!SupportEffects.AreAllies(owner, local) || Vector2.DistanceSquared(owner.Center, local.Center) > BannerRadius * BannerRadius)
                return;

            // 10 ticks, refreshed every tick while inside: it falls off right after leaving the aura.
            local.AddBuff(ModContent.BuffType<WarBannerBuff>(), 10);
        }
    }

    // Marks an enemy as "last hit by this player's melee damage", so a kill that lands later (for example
    // from a damage-over-time tick) still counts as that player's melee kill. Lives on the player's own game,
    // like the other chips' markers.
    public class AugmentWarGodTagNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        private int taggedPlayerIndex = -1;

        public void TagHit(int playerIndex) => taggedPlayerIndex = playerIndex;
        public bool IsTaggedBy(int playerIndex) => taggedPlayerIndex == playerIndex;
        public void ClearTag() => taggedPlayerIndex = -1;
    }
}
