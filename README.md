# Automata: Plug-in Chips

A [tModLoader](https://github.com/tModLoader/tModLoader) mod for Terraria. Every boss you defeat lets you pick one of three **plug-in chips**: permanent powerups bound to your character. Stack them into builds, complete chip sets for **protocol** bonuses, and heal your team with the **Medi Gun**.

- Author: Finsk
- Discord: <https://discord.gg/cE92pA3fdu>
- Internal mod name: `Augments` (the display name is "Automata: Plug-in Chips")

## Features

- **Boss reward chips**: defeat a boss, choose 1 of 3. Chips never roll as duplicates.
- **100+ chips** across four rarities (Common, Rare, Epic, Legendary) and six classes (Melee, Ranged, Magic, Summon, Support, Universal).
- **Protocols**: owning a matching set of chips unlocks bonus effects (Bloodhunter, Field Medic, Kinetic, Cryo, Volt, Hivemind, Marksman, Arcane Surge, Bastion, Gunslinger, Lasher, Fortune). See [CHANGELOG.md](CHANGELOG.md) for details.
- **Core Overrides**: one build-defining chip per character with trade-offs (Type-B, Type-D, Type-S).
- **Medi Gun (MK I to MK IV)**: a support weapon that tethers to teammates and heals them.
- **Mistress 2B**: a vendor NPC where you can sell and buy back chips.
- **In-game tools**: chip list, combat analytics and live DPS monitor, pinnable stat HUD, hidden-stats drawer, and the Pod 042 advisory tips.

## Install and build

You need tModLoader (Steam).

1. Clone this repo into your `ModSources` folder, **in a folder named `Augments`**. The folder name is the mod's internal name and the code loads assets by it, so don't rename it:
   ```
   cd "<Documents>\My Games\Terraria\tModLoader\ModSources"
   git clone https://github.com/finsk212-cloud/Automata-Plug-in-Chips.git Augments
   ```
2. In tModLoader open **Workshop → Develop Mods** and click **Build + Reload** next to the mod.

To update later, run `git pull` in that folder and Build + Reload again.

## Developer tools

The `/augment` command, the dev-mode chip editor and the dev keybinds are off by default. To use them, enable **Settings → Mod Configuration → Automata Server Configuration → Enable Developer Tools**. Leave it off on public servers, because it lets any player grant themselves chips.

| Command | Does |
|---|---|
| `/augment list [page]` | List chips |
| `/augment add <id or number>` | Add a chip |
| `/augment remove <id or number>` | Remove a chip |
| `/augment sell` / `buyback <id>` | Test the vendor flow |
| `/augment addall` / `clear` | Add or remove every chip |

## Project layout

| Folder | Contents |
|---|---|
| `AugmentTypes/` | One class per chip (`*Augment.cs`) |
| `Core/` | Chip base class, database, player logic (`AugmentPlayer`), networking (`AugmentNet`), reward and rarity rolling |
| `Effects/`, `Buffs/`, `Projectiles/` | NPC debuffs, buffs and projectiles used by chips |
| `Items/`, `NPCs/`, `Bosses/` | Medi Gun, chip items, vendor NPC, boss drop hooks |
| `UI/` | Reward cards, chip list, shop, analytics, HUD widgets |
| `Localization/` | `en-US` strings and config labels |
| `art/` | Icon drafts, not packed into the mod (`buildIgnore` in `build.txt`) |

## Adding a chip

1. Create `AugmentTypes/YourChipAugment.cs` deriving from `Augment`. Set `Id`, `DisplayName`, `Description`, `Rarity` and `Class`, then override the hooks you need (for example `ModifyWeaponCrit`).
2. Register it in `Core/AugmentDatabase.cs` with `Register(new YourChipAugment());`.
3. Build and test in game with `/augment add <id>` (Developer Tools on).

## License

No license has been chosen yet, so all rights are reserved by the author by default.
