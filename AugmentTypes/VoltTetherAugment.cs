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
    public class VoltTetherAugment : Augment
    {
        public override string Id => "volt_tether";
        public override string DisplayName => "Volt Tether";
        public override string Description =>
            $"High-voltage arcs {AugmentText.Trigger("tether you to your 2 nearest minions")}. " +
            $"Enemies caught in the electric arcs take continuous {AugmentText.SpecialDamage("8 summon damage")} and are {AugmentText.Electrified("Electrified")}.";

        public override string GetDetailedDescription(Player player) =>
            AugmentScaling.ApplyToText(Description, GetScalingEntries()) + "\n" +
            AugmentText.Note("(Tether range: 550px. If 2+ minions are active, links them together too. Ticks 4x/sec, scaling with world and summon damage.)") + "\n" +
            AugmentText.Note("(Electrified: Deals electric shock damage over time, quadrupled while the enemy is moving.)");

        public override AugmentRarity Rarity => AugmentRarity.Epic;
        public override AugmentClass Class => AugmentClass.Summon;

        public const int BaseDamage = 8;
        public const float MaxTetherDistance = 550f;
        public const int HitCooldownTicks = 15; // 4 hits per second max per enemy
        public const float LineCollisionWidth = 14f;

        public override (string label, int baseValue)[] GetScalingEntries()
        {
            return new[] { ("Tether damage", BaseDamage) };
        }

        private static readonly Dictionary<int, int> npcHitCooldown = new();
        private static int soundCooldown = 0;

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

            // Locate up to 2 closest active minions/sentries within range
            GetNearestMinions(player, MaxTetherDistance, out Projectile m1, out Projectile m2);
            if (m1 == null)
                return;

            Vector2 playerCenter = player.Center;
            Vector2 m1Center = m1.Center;

            // Subtle electric ambient lighting along tethers (no dust particles)
            Lighting.AddLight((playerCenter + m1Center) * 0.5f, 0.08f, 0.22f, 0.38f);
            bool hasTwo = m2 != null;
            Vector2 m2Center = hasTwo ? m2.Center : Vector2.Zero;

            if (hasTwo)
            {
                Lighting.AddLight((playerCenter + m2Center) * 0.5f, 0.08f, 0.22f, 0.38f);
                Lighting.AddLight((m1Center + m2Center) * 0.5f, 0.08f, 0.22f, 0.38f);
            }

            // Damage and debuff application: only local player computes collisions to avoid multiplayer duplicates
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

                bool hit = Collision.CheckAABBvLineCollision(npcPos, npcSize, playerCenter, m1Center, LineCollisionWidth, ref colPoint)
                    || (hasTwo && (Collision.CheckAABBvLineCollision(npcPos, npcSize, playerCenter, m2Center, LineCollisionWidth, ref colPoint)
                                || Collision.CheckAABBvLineCollision(npcPos, npcSize, m1Center, m2Center, LineCollisionWidth, ref colPoint)));

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

                    // Electrified debuff
                    npc.AddBuff(BuffID.Electrified, 120);
                    if (Main.netMode != NetmodeID.SinglePlayer)
                        NetMessage.SendData(MessageID.NPCBuffs, number: npc.whoAmI);

                    // Minimal local impact sparks on the target hit only
                    Dust d = Dust.NewDustDirect(npc.position, npc.width, npc.height, DustID.Electric, 0f, 0f, 100, default, 0.75f);
                    d.noGravity = true;
                    d.velocity *= 0.3f;

                    if (soundCooldown <= 0)
                    {
                        SoundEngine.PlaySound(SoundID.Item93 with { Volume = 0.35f, Pitch = 0.3f }, npc.Center);
                        soundCooldown = 15;
                    }
                }
            }
        }

        public static void GetNearestMinions(Player player, float maxDist, out Projectile m1, out Projectile m2)
        {
            m1 = null;
            m2 = null;
            float dist1Sq = maxDist * maxDist;
            float dist2Sq = maxDist * maxDist;

            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile p = Main.projectile[i];
                if (!p.active || p.owner != player.whoAmI)
                    continue;

                bool isMinion = p.minion || p.sentry || ProjectileID.Sets.MinionSacrificable[p.type] || p.minionSlots > 0f;
                if (!isMinion)
                    continue;

                float dSq = Vector2.DistanceSquared(player.Center, p.Center);
                if (dSq < dist1Sq)
                {
                    dist2Sq = dist1Sq;
                    m2 = m1;

                    dist1Sq = dSq;
                    m1 = p;
                }
                else if (dSq < dist2Sq)
                {
                    dist2Sq = dSq;
                    m2 = p;
                }
            }
        }

        public static void DrawAllTethers(SpriteBatch spriteBatch)
        {
            for (int p = 0; p < Main.maxPlayers; p++)
            {
                Player player = Main.player[p];
                if (!player.active || player.dead)
                    continue;

                var ap = player.GetModPlayer<AugmentPlayer>();
                if (!ap.HasAugment("volt_tether"))
                    continue;

                GetNearestMinions(player, MaxTetherDistance, out Projectile m1, out Projectile m2);
                if (m1 == null)
                    continue;

                DrawStaticBeam(spriteBatch, player.Center, m1.Center);

                if (m2 != null)
                {
                    DrawStaticBeam(spriteBatch, player.Center, m2.Center);
                    DrawStaticBeam(spriteBatch, m1.Center, m2.Center);
                }
            }
        }

        private static void DrawStaticBeam(SpriteBatch spriteBatch, Vector2 start, Vector2 end)
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

            float pulse = 0.5f + 0.5f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 18f);

            // 1. Outer Electric Aura (Luminous electric cyan/indigo glow, soft and clean)
            spriteBatch.Draw(
                pixel,
                drawPos,
                pixelSource,
                new Color(99, 102, 241) * 0.35f,
                rotation,
                origin,
                new Vector2(length, 5f + pulse * 1.5f),
                SpriteEffects.None,
                0f
            );

            // 2. Inner High-Energy Core Beam (Bright electric cyan)
            spriteBatch.Draw(
                pixel,
                drawPos,
                pixelSource,
                new Color(56, 189, 248) * 0.80f,
                rotation,
                origin,
                new Vector2(length, 2f + pulse * 0.8f),
                SpriteEffects.None,
                0f
            );

            // 3. Central Razor Core (White-hot static filament)
            spriteBatch.Draw(
                pixel,
                drawPos,
                pixelSource,
                Color.White * 0.95f,
                rotation,
                origin,
                new Vector2(length, 1f),
                SpriteEffects.None,
                0f
            );

            // Anchor node points at start and end
            DrawAnchorNode(spriteBatch, pixel, drawPos);
            DrawAnchorNode(spriteBatch, pixel, end - Main.screenPosition);
        }

        private static void DrawAnchorNode(SpriteBatch spriteBatch, Texture2D pixel, Vector2 screenPos)
        {
            Rectangle nodeGlow = new Rectangle((int)screenPos.X - 3, (int)screenPos.Y - 3, 6, 6);
            spriteBatch.Draw(pixel, nodeGlow, new Color(56, 189, 248) * 0.70f);

            Rectangle nodeCore = new Rectangle((int)screenPos.X - 1, (int)screenPos.Y - 1, 2, 2);
            spriteBatch.Draw(pixel, nodeCore, Color.White * 0.95f);
        }
    }

    public class VoltTetherSystem : ModSystem
    {
        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
        {
            if (Main.dedServ)
                return;

            // Insert into the game-world interface layer so it renders in world coordinates behind UI
            layers.Insert(0, new LegacyGameInterfaceLayer(
                "Augments: Volt Tether",
                delegate
                {
                    VoltTetherAugment.DrawAllTethers(Main.spriteBatch);
                    return true;
                },
                InterfaceScaleType.Game));
        }
    }
}
