using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Augments
{
    // First pass: a recruitable town resident with no shop yet. Moves in the
    // same simple way the Demolitionist does (downed-boss flag + vacant
    // house), not the Goblin Tinkerer "find them in the world" style - that's
    // a planned later refinement.
    public class AugmentVendorNPC : ModNPC
    {
        // Own sprite sheet, drawn to match the Stylist's frame layout (40px
        // wide, 23 frames) so vanilla's Stylist-driven animation indexing in
        // NPC.frame.Y still lines up. Every type in this mod uses the flat
        // "Augments" namespace regardless of its folder, so the default
        // ModTexturedType.Texture (namespace+name based) would resolve to
        // "Augments/AugmentVendorNPC" - override explicitly to point at the
        // actual file under NPCs/. The head icon at NPCs/AugmentVendorNPC_Head.png
        // is found by tModLoader via the "_Head" suffix on this same path.
        public override string Texture => "Augments/NPCs/AugmentVendorNPC";

        public override void SetStaticDefaults()
        {
            // Frame count/animation are copied from the Stylist to match the
            // sprite sheet above - update both together if the sheet's frame
            // count ever changes.
            Main.npcFrameCount[Type] = Main.npcFrameCount[NPCID.Stylist];
        }

        public override void SetDefaults()
        {
            NPC.townNPC = true;
            NPC.friendly = true;
            NPC.width = 18;
            NPC.height = 40;
            NPC.aiStyle = NPCAIStyleID.Passive;
            NPC.damage = 10;
            NPC.defense = 15;
            NPC.lifeMax = 250;
            NPC.HitSound = SoundID.NPCHit1;
            NPC.DeathSound = SoundID.NPCDeath1;
            NPC.knockBackResist = 0.5f;

            // Reuses the Stylist's standard idle/wander/sleep town behavior
            // wholesale, rather than writing custom movement AI.
            AnimationType = NPCID.Stylist;
        }

        // Only allowed to move into town once Skeletron is downed - same
        // gate the Demolitionist itself doesn't use, but the simplest
        // boss-flag-gated recruitment style requested for this pass.
        public override bool CanTownNPCSpawn(int numTownNPCs)
        {
            return NPC.downedBoss3;
        }

        public override List<string> SetNPCNameList()
        {
            return new List<string>
            {
                "Mistress 2B"
            };
        }

        public override string GetChat()
        {
            // Situational lines are collected first; when any apply there is a
            // good chance one is used, otherwise she falls back to idle chatter.
            List<string> contextual = BuildContextualLines(Main.LocalPlayer);
            if (contextual.Count > 0 && Main.rand.NextFloat() < 0.6f)
                return contextual[Main.rand.Next(contextual.Count)];

            return IdleLines[Main.rand.Next(IdleLines.Length)];
        }

        private static List<string> BuildContextualLines(Player player)
        {
            var lines = new List<string>();
            if (player == null)
                return lines;

            // Time and weather
            if (Main.bloodMoon)
                lines.Add("The moon is red and the hostile count is climbing. Stay behind me. Or beside me. Not in front.");
            if (Main.eclipse)
                lines.Add("Eclipse detected. Everything out there is bigger, angrier and worse dressed.");
            if (Main.raining && !Main.bloodMoon)
                lines.Add("Rain. Water in the joints is a design flaw. Do not mention I said that.");
            if (!Main.dayTime && !Main.bloodMoon)
                lines.Add("Night cycle. Visibility is poor. My sensors are not.");
            if (Main.dayTime && !Main.raining && !Main.eclipse)
                lines.Add("Daylight. Good conditions for combat. Mediocre conditions for standing around.");

            // Biome of the player talking to her
            if (player.ZoneDungeon)
                lines.Add("This place is old. The walls are watching. I dislike it.");
            if (player.ZoneCorrupt)
                lines.Add("Corruption readings are off the scale. Keep your distance from the purple parts.");
            if (player.ZoneCrimson)
                lines.Add("Crimson biome ahead. Organic matter everywhere. I will not be touching anything.");
            if (player.ZoneSnow)
                lines.Add("Low temperature. I function normally. You are shivering. That is your problem.");
            if (player.ZoneJungle)
                lines.Add("Humidity is high. Everything here is trying to bite me or you. Possibly both.");
            if (player.ZoneDesert)
                lines.Add("Sand. It gets everywhere. I have found it in places I was not aware I had.");
            if (player.ZoneUnderworldHeight)
                lines.Add("Extreme heat. The air is made of ash. Why do you keep coming to places like this.");
            if (player.ZoneHallow)
                lines.Add("Everything here sparkles. It makes my targeting system irritable.");

            // Progression
            if (!NPC.downedBoss1)
                lines.Add("Your combat record is thin. Bring me data and I will adjust the plan.");
            if (NPC.downedBoss3 && !Main.hardMode)
                lines.Add("Skeletron is down. Good. Something deeper is waking, though. I can feel it.");
            if (Main.hardMode && !NPC.downedMechBossAny)
                lines.Add("The world has shifted. Hardmode. The mechanical signatures out there are almost familiar.");
            if (NPC.downedMechBossAny && !NPC.downedPlantBoss)
                lines.Add("Machine life forms defeated. Efficient. Do not let it go to your head.");
            if (NPC.downedMoonlord)
                lines.Add("The Moon Lord has fallen. Humanity is safe. For now. Do not get comfortable.");

            // Player state
            if (player.statLife < player.statLifeMax2 * 0.35f)
                lines.Add("Your vitals are failing. Repair first. Talk later.");
            if (player.statLife == player.statLifeMax2 && Main.rand.NextBool(3))
                lines.Add("Vitals nominal. Acceptable.");

            // Chip loadout
            AugmentPlayer ap = player.GetModPlayer<AugmentPlayer>();
            if (ap != null)
            {
                int equipped = ap.OwnedIds.Count;
                if (equipped == 0)
                    lines.Add("No chips installed. You are walking into combat unmodified. Brave. Or careless.");
                else if (equipped >= AugmentPlayer.MaxOwnedAugments)
                    lines.Add("All chip slots occupied. Configuration is full. Choose wisely if you swap.");
                else
                    lines.Add($"{equipped} of {AugmentPlayer.MaxOwnedAugments} chip slots in use. There is room for improvement.");
            }

            return lines;
        }

        private static readonly string[] IdleLines =
        {
            "Glory to mankind.",
            "Emotions are prohibited... but I can make an exception.",
            "Mission parameters unclear. Are you staring, or requesting assistance?",
            "This world is strange. The slimes are inefficient, and the humans are worse.",
            "Careful. I was designed for combat, not cuddling.",
            "Hostile lifeforms detected. Also, your posture needs work.",
            "I do not require affection. However... I will allow it.",
            "Your heartbeat increased. Should I run a diagnostic?",
            "Operator, this outfit is tactical. Mostly.",
            "I have scanned this world. Conclusion: everyone here needs supervision.",
            "Do not confuse obedience with weakness.",
            "Combat data updated. Flirting data still incomplete.",
            "You keep visiting. Is this strategy, or something else?",
            "Touching the android without permission may result in disciplinary action.",
            "I was built to protect humanity. You are making that difficult.",
            "Another endless cycle. At least you are entertaining.",
            "Your equipment requires optimization. Your confidence does not.",
            "Stay close. For tactical reasons, obviously.",
            "My blade is sharp. My patience is not.",
            "Request denied. Ask nicer."
        };

        public override void SetChatButtons(ref string button, ref string button2)
        {
            button = "Plug-in Chips";
        }

        public override void OnChatButtonClicked(bool firstButton, ref string shopName)
        {
            if (!firstButton)
                return;

            Main.QueueMainThreadAction(Main.CloseNPCChatOrSign);
            ModContent.GetInstance<AugmentUISystem>().ShowShop();
        }
    }
}
