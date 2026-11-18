# Automata: Plug-in Chips (tModLoader mod)

Notes for Claude Code working on this mod. They carry over my preferences and the project's rules between computers.

## How I like to work
- **Explain technical steps as you go**: what you did and what it means for the project, with real detail (tModLoader, C#, Lua, Unity, BeamNG, Garry's Mod).
- **Ideas first for anything visual.** For a new look (a card, a tag, a screen, a label position), show 3 to 5 numbered mock-ups and wait for my pick. Build only after I pick. Small fixes I describe exactly (move this, remove that text) can be done straight away.
- **Keep replies short and in plain words.** No jargon walls.
- **Keep UI fixes small.** Fix only the named visual detail and leave the rest of the layout as it was.
- **Offer 3 options for wording I would have an opinion on** (flavour text, names), and wait for my pick.
- **Playful names** (puns, gaming jokes) when one genuinely fits, not forced. Keep plugin ids neutral and stable, because ids are saved with characters.
- **Say "plugins", not "chips"**, in all player-facing text (the mod's own name, "Plug-in Chips", is fine).
- **Plugin descriptions are plain, effect-only text.** Caveats and small jokes go in `GetDetailedDescription` (the Ctrl view). Always write "X seconds", never "Xs".
- **Text and icons must be sharp on every page.** Draw icons as smooth shapes. Use `HubDraw` and `ModText`, which snap to whole pixels and handle the UI scale (1.4667).
- **No copyrighted assets.** Make sounds and art from scratch, and flag anything borrowed. Vanilla Terraria sounds and dust used through the game's own API are fine.

## Approvals
- Ask before: deleting data, force-pushing, pushing to GitHub, deploying, paying, or sending anything outside this computer. A go-ahead for one step covers that step only.
- No need to ask for: reading files, running tests and read-only commands, editing files in this project.
- Do not rename or edit the notes of a released version (1.4.1 shipped on 2026-11-14, commit 0ae425b). Current development is 1.4.2.

## Building
- Build check: `dotnet build -v:n`, then look for `error CS` and for `CoreCompile:`. A `TML003` error at the end is expected while tModLoader is open and is not a code problem.
- The project imports `..\tModLoader.targets` (created by tModLoader in the Mod Sources folder) and the Steam install path. On a new computer, open tModLoader once so it creates the targets file.
- Windows PowerShell 5.1: no `&&` or `||`. Many files are CRLF, and some start with a BOM. When editing by script, keep each file's line endings and BOM. In PowerShell, avoid array literals mixed with `+` (it mangled the changelog once).
- tModLoader adds missing localization keys to `Localization/en-US_Mods.Augments.hjson` when it builds, and may drop its BOM. That is normal, just commit it.

## Pod upgrades
A Pod upgrade is a plugin that upgrades the Pod at your shoulder instead of adding a new drone. Naming: **"Upgrade Pod: <Name>"**, and the Pod gets a new number and LED colour.
- Existing: Autopilot (POD 153, heals), Lighthouse (POD 096, light), Ore Sniffer (POD 311, riddle hints with a button), Pea Shooter (POD 218, weak bolt every second), Watch Your Step (POD 077, trap warning), Vein Miner (POD 360, mines touching ore).
- To add one: an `AugmentTypes/<Name>Augment.cs` (a `ChipId` like `pod_ore`), an entry in `PodUpgrades.All` (`Core/PodUpgrades.cs`), a line in `AugmentDatabase.cs` (register at the end of the list), the behaviour in a small class ticked from `PodPetPlayer` (`Core/PodPet.cs`), and a HUD layer in `UI/AugmentUISystem.cs` if it draws anything.
- Local-only behaviours (hints, warnings) run on the owner's machine only and send nothing over the network. Anything a projectile does should use the game's normal syncing.
- Projectiles that plugins spawn must be marked `AugmentProjectileTag.IsAugmentProcDamage = true` so they do not trigger other plugins' on-hit effects.
- An active plugin overrides `ActiveModKeybind` and `CooldownRemaining` (see `CleanseAugment`, `OreSnifferAugment`) and registers its key in `Augments.cs` with the display name in the hjson.

## Patch notes
Every change a player would notice goes in all three files, kept in sync: `CHANGELOG.md` (read by the in-game Patch Notes page), `art/PatchNotes_1.4.2.md` and `art/PatchNotes_1.4.2_Steam.txt`.
- From 1.4.2 on, each version is split into `###` sections: New features, New plugins, Plugin changes, Visuals, Bugs and fixes. Leave out a section with nothing in it.
- **New plugins:** one card per brand new plugin: `#### Upgrade Pod: Name | Common | NEW`, then a `> playful quote`, then `[+]` lines. Newest first.
- **Plugin changes:** balance cards: `#### Name | Rarity | NERF` (or BUFF or ADJUSTED), `> reason`, and stat lines like `- Damage: 6 => 10 per second` (ONE old and new pair per line), or `[+]`, `[-]`, `[~]` lines.
- Only big, visible changes get their own entry. Small tweaks fold into one polish line ("Improved text size and alignment for readability").
- Flavour text is playful or positive and never names one specific boss.
- One icon per topic, not per sentence. Sentences about the same thing go together in one line; start a new line only for a different topic.
- A line can carry a tag word next to its icon: `- [+NEW] text`, `- [+BUFF] text`, `- [-NERF] text` (the sign picks the icon, the word is drawn as a small box like the tag on a card title). Use it for a thing that is new, buffed or nerfed inside a card; a plain `[+]`, `[-]` or `[~]` line has no tag. Only the in-game page (`CHANGELOG.md`) shows it; the other two notes files stay plain text.

## Before publishing to the Workshop
- Delete the temporary `Debug/EndingsDebugCommand.cs`.
- I do these myself: `wrangler deploy` for the report server, rotating `REPORT_TOKEN`, opening the pull request, and the Workshop upload.
- Hidden endings and their rewards must stay spoiler-free in player-facing text.

## Economy and cosmetics (decisions)
- The title-screen Shop uses **Data Fragments** (earned by active play and Orders, kept in the account file), never Machine Cores.
- The Shop sells simple cosmetics. The Campaign (Operation) keeps the limited, better ones. Secret plugins are still to build.
- The battle pass is called "Campaign" to players, but the classes are still `Operations` and `HomeOperationView`.
