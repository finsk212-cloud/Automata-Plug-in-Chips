using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;
using Augments.Core;

namespace Augments
{
    public class TurretSyndromeAugment : Augment
    {
        public override string Id => "turret_syndrome";
        public override string DisplayName => "Turret Syndrome";
        public override string Description =>
            $"Permanently grants {AugmentText.Defense("+1 max sentry slot")}. Active sentries become {AugmentText.Trigger("mobile hovering drones")} " +
            $"that follow you in formation, projecting a lethal {AugmentText.SpecialDamage("28 summon damage laser grid")} between them.";

        public override string GetDetailedDescription(Player player) =>
            AugmentScaling.ApplyToText(Description, GetScalingEntries()) + "\n" +
            AugmentText.Note("(Sentries hover in orbit around you. Enemies passing through the connecting laser matrix take continuous tick damage and are slowed by 30%.)") + "\n" +
            AugmentText.Note("(Damage ticks 4x/sec, scaling across world stages and with your Summon damage bonuses.)");

        public override AugmentRarity Rarity => AugmentRarity.Legendary;
        public override AugmentClass Class => AugmentClass.Summon;

        public const int BaseDamage = 28;
        public const int HitCooldownTicks = 15; // 4 hits per second max per enemy
        public const float LineCollisionWidth = 16f;

        public override (string label, int baseValue)[] GetScalingEntries()
        {
            return new[] { ("Laser grid damage", BaseDamage) };
        }

        private static readonly Dictionary<int, int> npcHitCooldown = new();
        private static int soundCooldown = 0;

        public override void UpdateEquips(Player player)
        {
            player.maxTurrets += 1;
        }

        public override void OnUpdate(Player player)
        {
            if (player.dead || !player.active)
                return;

            if (soundCooldown > 0)
                soundCooldown--;

            // Periodically clean up inactive NPCs from the cooldown dictionary
            if (Main.GameUpdateCount % 180 == 0 && npcHitCooldown.Count > 0)
            {
                var dead = new List<int>();
                foreach (var id in npcHitCooldown.Keys)
                {
                    if (id < 0 || id >= Main.maxNPCs || !Main.npc[id].active)
                        dead.Add(id);
                }
                foreach (var id in dead)
                    npcHitCooldown.Remove(id);
            }

            List<Projectile> sentries = GetActiveSentries(player);
            if (sentries.Count == 0)
                return;

            int count = sentries.Count;

            // --- 1. Position and Repulsor Logic for Hovering Sentries ---
            for (int i = 0; i < count; i++)
            {
                Projectile sentry = sentries[i];
                Vector2 targetPos = CalculateSentrySlot(player, i, count);

                sentry.tileCollide = false;
                float dist = Vector2.Distance(sentry.Center, targetPos);
                if (dist > 1200f)
                {
                    sentry.Center = targetPos;
                    sentry.velocity = Vector2.Zero;
                }
                else if (dist > 2f)
                {
                    sentry.Center = Vector2.Lerp(sentry.Center, targetPos, 0.14f);
                    sentry.velocity = Vector2.Zero;
                }

                // Repulsor drone exhaust particles & light
                Vector2 exhaustPos = sentry.Bottom;
                if (Main.rand.NextBool(3))
                {
                    Dust d = Dust.NewDustPerfect(
                        exhaustPos + new Vector2(Main.rand.NextFloat(-8f, 8f), 2f),
                        DustID.OrangeTorch,
                        new Vector2(0f, Main.rand.NextFloat(1.5f, 3f)),
                        100,
                        default,
                        Main.rand.NextFloat(0.8f, 1.2f)
                    );
                    d.noGravity = true;
                }
                Lighting.AddLight(exhaustPos, 0.35f, 0.20f, 0.05f);
            }

            // --- 2. Laser Grid Line Segments & Ambient Light ---
            List<(Vector2 start, Vector2 end)> segments = GetLaserSegments(player, sentries);
            if (segments.Count == 0)
                return;

            foreach (var seg in segments)
            {
                Lighting.AddLight((seg.start + seg.end) * 0.5f, 0.45f, 0.22f, 0.05f);
            }

            // --- 3. Collision & Damage Execution (Local player authority only) ---
            if (player.whoAmI != Main.myPlayer)
                return;

            int currentTick = (int)Main.GameUpdateCount;
            int baseDmg = AugmentScaling.Scale(BaseDamage);
            int finalDmg = Math.Max(1, (int)player.GetTotalDamage(DamageClass.Summon).ApplyTo(baseDmg));

            for (int n = 0; n < Main.maxNPCs; n++)
            {
                NPC npc = Main.npc[n];
                if (!npc.active || npc.friendly || npc.dontTakeDamage || npc.lifeMax <= 5)
                    continue;

                if (npcHitCooldown.TryGetValue(n, out int lastHit) && currentTick - lastHit < HitCooldownTicks)
                    continue;

                float colPoint = 0f;
                Vector2 npcPos = npc.position;
                Vector2 npcSize = npc.Size;

                bool hit = false;
                for (int s = 0; s < segments.Count; s++)
                {
                    if (Collision.CheckAABBvLineCollision(npcPos, npcSize, segments[s].start, segments[s].end, LineCollisionWidth, ref colPoint))
                    {
                        hit = true;
                        break;
                    }
                }

                if (hit)
                {
                    npcHitCooldown[n] = currentTick;

                    var hitInfo = new NPC.HitInfo
                    {
                        Damage = finalDmg,
                        SourceDamage = finalDmg,
                        HitDirection = npc.Center.X >= player.Center.X ? 1 : -1,
                        DamageType = DamageClass.Summon,
                        Knockback = 0.5f,
                        HideCombatText = true
                    };

                    npc.StrikeNPC(hitInfo);
                    if (Main.netMode != NetmodeID.SinglePlayer)
                        NetMessage.SendStrikeNPC(npc, in hitInfo);

                    CombatText.NewText(npc.Hitbox, AugmentTextColors.SpecialDamage, finalDmg);
                    AugmentDamageTracker.RecordChipHit(Id, finalDmg, false);

                    npc.AddBuff(BuffID.Slow, 120);    // 30% slow for 2s
                    npc.AddBuff(BuffID.OnFire3, 180); // Hellfire for 3s
                    if (Main.netMode != NetmodeID.SinglePlayer)
                        NetMessage.SendData(MessageID.NPCBuffs, number: npc.whoAmI);

                    // Impact sparks
                    Dust d = Dust.NewDustDirect(npc.position, npc.width, npc.height, DustID.OrangeTorch, 0f, 0f, 100, default, 1.1f);
                    d.noGravity = true;
                    d.velocity *= 0.4f;

                    if (soundCooldown <= 0)
                    {
                        SoundEngine.PlaySound(SoundID.Item93 with { Volume = 0.35f, Pitch = 0.2f }, npc.Center);
                        soundCooldown = 15;
                    }
                }
            }
        }

        public static List<Projectile> GetActiveSentries(Player player)
        {
            var list = new List<Projectile>();
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile p = Main.projectile[i];
                if (p.active && p.owner == player.whoAmI && p.sentry)
                {
                    list.Add(p);
                }
            }
            return list;
        }

        public static Vector2 CalculateSentrySlot(Player player, int index, int count)
        {
            if (count == 1)
            {
                return player.Center + new Vector2(-55f * player.direction, -65f);
            }
            else if (count == 2)
            {
                float xOffset = (index == 0) ? -75f : 75f;
                return player.Center + new Vector2(xOffset, -55f);
            }
            else
            {
                float angleStep = MathHelper.TwoPi / count;
                float orbitAngle = angleStep * index + (float)Main.GameUpdateCount * 0.015f;
                float rx = 95f + count * 5f;
                float ry = 65f + count * 3f;
                return player.Center + new Vector2((float)Math.Cos(orbitAngle) * rx, (float)Math.Sin(orbitAngle) * ry - 20f);
            }
        }

        public static List<(Vector2 start, Vector2 end)> GetLaserSegments(Player player, List<Projectile> sentries)
        {
            var segments = new List<(Vector2 start, Vector2 end)>();
            int count = sentries.Count;
            if (count == 0)
                return segments;

            if (count == 1)
            {
                segments.Add((player.Center, sentries[0].Center));
            }
            else if (count == 2)
            {
                segments.Add((sentries[0].Center, sentries[1].Center));
                segments.Add((player.Center, sentries[0].Center));
                segments.Add((player.Center, sentries[1].Center));
            }
            else
            {
                for (int i = 0; i < count; i++)
                {
                    segments.Add((sentries[i].Center, sentries[(i + 1) % count].Center));
                    segments.Add((player.Center, sentries[i].Center));
                }
            }
            return segments;
        }

        public static void DrawAllLaserGrids(SpriteBatch spriteBatch)
        {
            for (int p = 0; p < Main.maxPlayers; p++)
            {
                Player player = Main.player[p];
                if (!player.active || player.dead)
                    continue;

                var ap = player.GetModPlayer<AugmentPlayer>();
                if (!ap.HasAugment("turret_syndrome"))
                    continue;

                List<Projectile> sentries = GetActiveSentries(player);
                if (sentries.Count == 0)
                    continue;

                List<(Vector2 start, Vector2 end)> segments = GetLaserSegments(player, sentries);
                foreach (var seg in segments)
                {
                    DrawLaserBeam(spriteBatch, seg.start, seg.end);
                }
            }
        }

        private static void DrawLaserBeam(SpriteBatch spriteBatch, Vector2 start, Vector2 end)
        {
            Vector2 diff = end - start;
            float length = diff.Length();
            if (length < 2f)
                return;

            float rotation = diff.ToRotation();
            Vector2 origin = new Vector2(0f, 0.5f);
            Rectangle pixelSource = new Rectangle(0, 0, 1, 1);
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Vector2 drawPos = start - Main.screenPosition;

            float pulse = 0.5f + 0.5f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 20f);

            // 1. Outer Laser Bloom (Amber-orange radiation glow)
            spriteBatch.Draw(
                pixel,
                drawPos,
                pixelSource,
                new Color(251, 146, 60) * 0.40f,
                rotation,
                origin,
                new Vector2(length, 6f + pulse * 1.5f),
                SpriteEffects.None,
                0f
            );

            // 2. Inner High-Energy Plasma Beam (Vivid fiery orange)
            spriteBatch.Draw(
                pixel,
                drawPos,
                pixelSource,
                new Color(255, 90, 20) * 0.85f,
                rotation,
                origin,
                new Vector2(length, 2.5f + pulse * 0.8f),
                SpriteEffects.None,
                0f
            );

            // 3. Central Razor Core (Brilliant sun-white core filament)
            spriteBatch.Draw(
                pixel,
                drawPos,
                pixelSource,
                new Color(255, 250, 220) * 0.95f,
                rotation,
                origin,
                new Vector2(length, 1.2f),
                SpriteEffects.None,
                0f
            );
        }
    }

    public class TurretSyndromeSystem : ModSystem
    {
        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
        {
            if (Main.dedServ)
                return;

            layers.Insert(0, new LegacyGameInterfaceLayer(
                "Augments: Turret Syndrome",
                delegate
                {
                    TurretSyndromeAugment.DrawAllLaserGrids(Main.spriteBatch);
                    return true;
                },
                InterfaceScaleType.Game));
        }
    }
}
