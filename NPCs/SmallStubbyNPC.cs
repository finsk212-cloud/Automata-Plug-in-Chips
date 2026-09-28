using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent.Bestiary;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

namespace Augments
{
    public class SmallStubbyNPC : ModNPC
    {
        public override string Texture => "Augments/NPCs/SmallStubbyNPC";

        public override void SetStaticDefaults()
        {
            Main.npcFrameCount[Type] = 4;
        }

        public override void SetDefaults()
        {
            NPC.width = 24;
            NPC.height = 32;
            NPC.damage = 12;
            NPC.defense = 6;
            NPC.lifeMax = 42;
            NPC.HitSound = SoundID.NPCHit4;     // Metallic clink
            NPC.DeathSound = SoundID.NPCDeath14; // Mechanical explosion / pop
            NPC.value = 150f;
            NPC.knockBackResist = 0.55f;
            NPC.aiStyle = NPCAIStyleID.Fighter;
            AIType = NPCID.Zombie;
            Banner = Item.NPCtoBanner(NPCID.Zombie);
            BannerItem = Item.BannerToItem(Banner);
        }

        public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry)
        {
            bestiaryEntry.Info.AddRange(new IBestiaryInfoElement[]
            {
                BestiaryDatabaseNPCsPopulator.CommonTags.SpawnConditions.Biomes.Surface,
                BestiaryDatabaseNPCsPopulator.CommonTags.SpawnConditions.Biomes.Caverns,
                new FlavorTextBestiaryInfoElement("A primitive, bucket-headed machine lifeform driven by an ancient network loop. Salvaged units yield intact Machine Cores.")
            });
        }

        public override void ModifyNPCLoot(NPCLoot npcLoot)
        {
            int essenceType = ModContent.ItemType<AugmentEssenceItem>();

            // Guaranteed 1 Machine Core
            npcLoot.Add(ItemDropRule.Common(essenceType, 1, 1, 1));

            // Scrap drops
            npcLoot.Add(ItemDropRule.Common(ItemID.Wire, 2, 2, 5));
            npcLoot.Add(ItemDropRule.Common(ItemID.IronOre, 3, 1, 3));
        }

        public override float SpawnChance(NPCSpawnInfo spawnInfo)
        {
            if (spawnInfo.PlayerSafe || spawnInfo.Invasion || Main.invasionType != 0 || spawnInfo.Player.ZoneDungeon)
                return 0f;

            // Surface or underground Pre-Hardmode encounter (1% spawn rate)
            if (!Main.hardMode && (spawnInfo.SpawnTileY < Main.rockLayer || spawnInfo.SpawnTileY > Main.worldSurface))
            {
                return 0.01f;
            }

            // Occasional stray machine scout in Hardmode
            if (Main.hardMode && spawnInfo.SpawnTileY < Main.rockLayer)
            {
                return 0.005f;
            }

            return 0f;
        }

        public override void FindFrame(int frameHeight)
        {
            NPC.spriteDirection = NPC.direction;

            if (NPC.velocity.Y != 0f)
            {
                // In air: walking frame 2
                NPC.frame.Y = 2 * frameHeight;
            }
            else if (NPC.velocity.X == 0f)
            {
                // Idle standing
                NPC.frame.Y = 0;
                NPC.frameCounter = 0.0;
            }
            else
            {
                // Walking cycle
                NPC.frameCounter += Math.Abs(NPC.velocity.X) * 0.22f;
                int frame = ((int)NPC.frameCounter) % 4;
                NPC.frame.Y = frame * frameHeight;
            }
        }

        public override void HitEffect(NPC.HitInfo hit)
        {
            // Metallic spark dust on hit
            for (int i = 0; i < 4; i++)
            {
                Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.Torch, hit.HitDirection * 1.5f, -1f, 0, default, 1.1f);
                Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.Iron, hit.HitDirection, -1f, 0, default, 0.9f);
            }

            // Destruction blast on death
            if (NPC.life <= 0)
            {
                for (int i = 0; i < 15; i++)
                {
                    Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.Smoke, Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-2f, 2f), 0, default, 1.2f);
                    Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.Torch, Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-3f, 3f), 0, default, 1.3f);
                    Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.Iron, Main.rand.NextFloat(-2.5f, 2.5f), Main.rand.NextFloat(-2.5f, 2.5f), 0, default, 1.1f);
                }
            }
        }
    }
}
