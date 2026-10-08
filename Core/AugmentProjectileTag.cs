using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace Augments
{
    // Per-projectile provenance used by AugmentPlayer's hit dispatchers.
    // Proc projectiles set this in SetDefaults so the tag is reconstructed
    // consistently on the server and every client.
    public class AugmentProjectileTag : GlobalProjectile
    {
        public override bool InstancePerEntity => true;

        public bool IsAugmentProcDamage;
        public bool CanTriggerOnHitAugments;
        public float OnHitEffectiveness = 1f;
        public bool PreventEchoChamberCopy;
        public bool PreventRicochetEngineCopy;
        public bool IsEchoChamberEcho;
        public bool NecromancersCourtGhost;
        public int NecromancersCourtGhostTimeLeft;
        public int SourceMinionProjectileType = -1;
        public int SourceMinionProjectileIdentity = -1;

        public string SourceAugmentId;
        // Name of the weapon that fired this projectile (local only, used by the DPS panel).
        public string SourceItemName;
        public string SourceProtocolId;

        public override void SendExtraAI(Projectile projectile, BitWriter bitWriter, BinaryWriter binaryWriter)
        {
            bitWriter.WriteBit(IsAugmentProcDamage);
            bool hasMinionSource = SourceMinionProjectileType >= 0;
            bitWriter.WriteBit(hasMinionSource);

            bool hasAugmentId = !string.IsNullOrEmpty(SourceAugmentId);
            bitWriter.WriteBit(hasAugmentId);
            if (hasAugmentId)
                binaryWriter.Write(SourceAugmentId);

            bool hasProtocolId = !string.IsNullOrEmpty(SourceProtocolId);
            bitWriter.WriteBit(hasProtocolId);
            if (hasProtocolId)
                binaryWriter.Write(SourceProtocolId);

            if (IsAugmentProcDamage)
            {
                bitWriter.WriteBit(CanTriggerOnHitAugments);
                bitWriter.WriteBit(PreventEchoChamberCopy);
                bitWriter.WriteBit(PreventRicochetEngineCopy);
                bitWriter.WriteBit(IsEchoChamberEcho);
                bitWriter.WriteBit(NecromancersCourtGhost);
                binaryWriter.Write(OnHitEffectiveness);

                if (NecromancersCourtGhost)
                    binaryWriter.Write(NecromancersCourtGhostTimeLeft);
            }

            if (hasMinionSource)
            {
                binaryWriter.Write(SourceMinionProjectileType);
                binaryWriter.Write(SourceMinionProjectileIdentity);
            }
        }

        public override void ReceiveExtraAI(Projectile projectile, BitReader bitReader, BinaryReader binaryReader)
        {
            IsAugmentProcDamage = bitReader.ReadBit();
            bool hasMinionSource = bitReader.ReadBit();

            bool hasAugmentId = bitReader.ReadBit();
            SourceAugmentId = hasAugmentId ? binaryReader.ReadString() : null;

            bool hasProtocolId = bitReader.ReadBit();
            SourceProtocolId = hasProtocolId ? binaryReader.ReadString() : null;

            if (!IsAugmentProcDamage)
            {
                CanTriggerOnHitAugments = false;
                PreventEchoChamberCopy = false;
                PreventRicochetEngineCopy = false;
                IsEchoChamberEcho = false;
                NecromancersCourtGhost = false;
                NecromancersCourtGhostTimeLeft = 0;
                OnHitEffectiveness = 1f;
            }
            else
            {
                CanTriggerOnHitAugments = bitReader.ReadBit();
                PreventEchoChamberCopy = bitReader.ReadBit();
                PreventRicochetEngineCopy = bitReader.ReadBit();
                IsEchoChamberEcho = bitReader.ReadBit();
                NecromancersCourtGhost = bitReader.ReadBit();
                OnHitEffectiveness = binaryReader.ReadSingle();

                NecromancersCourtGhostTimeLeft = NecromancersCourtGhost ? binaryReader.ReadInt32() : 0;
            }

            if (hasMinionSource)
            {
                SourceMinionProjectileType = binaryReader.ReadInt32();
                SourceMinionProjectileIdentity = binaryReader.ReadInt32();
            }
            else
            {
                SourceMinionProjectileType = -1;
                SourceMinionProjectileIdentity = -1;
            }
        }

        public override void PostAI(Projectile projectile)
        {
            if (IsEchoChamberEcho)
            {
                Lighting.AddLight(projectile.Center, 0.1f, 0.22f, 0.35f);
                if (Main.rand.NextBool(2))
                {
                    Dust d = Dust.NewDustDirect(projectile.position, projectile.width, projectile.height, Terraria.ID.DustID.DungeonSpirit, projectile.velocity.X * 0.1f, projectile.velocity.Y * 0.1f, 150, default, 0.85f);
                    d.noGravity = true;
                    d.velocity *= 0.5f;
                }
            }

            if (!NecromancersCourtGhost)
                return;

            projectile.damage = 5;
            projectile.originalDamage = 5;
            projectile.minionSlots = 0f;
            projectile.friendly = true;
            projectile.hostile = false;
            projectile.DamageType = DamageClass.Summon;
            projectile.usesLocalNPCImmunity = true;
            projectile.localNPCHitCooldown = 60;
            projectile.alpha = System.Math.Max(projectile.alpha, 110);
            Lighting.AddLight(projectile.Center, 0.04f, 0.08f, 0.15f);

            NecromancersCourtGhostTimeLeft--;
            if (NecromancersCourtGhostTimeLeft <= 0)
                projectile.Kill();
        }

        public override Color? GetAlpha(Projectile projectile, Color lightColor)
        {
            if (IsEchoChamberEcho)
                return new Color(130, 210, 255, 175);

            return NecromancersCourtGhost ? new Color(145, 185, 255, 175) : null;
        }

        public override void OnKill(Projectile projectile, int timeLeft)
        {
            if (!NecromancersCourtGhost || !projectile.minion)
                return;

            for (int i = 0; i < 8; i++)
            {
                Dust dust = Dust.NewDustPerfect(
                    projectile.Center + Main.rand.NextVector2Circular(10f, 16f),
                    i % 2 == 0 ? Terraria.ID.DustID.GemSapphire : Terraria.ID.DustID.GemAmethyst,
                    Main.rand.NextVector2Circular(1.2f, 1.2f),
                    170,
                    default,
                    Main.rand.NextFloat(0.45f, 0.7f)
                );
                dust.noGravity = true;
                dust.fadeIn = 0.4f;
            }
        }
    }
}
