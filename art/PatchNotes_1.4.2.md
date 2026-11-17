# Automata: Plug-in Chips 1.4.2

A bigger update: hidden endings, a better Profile page, an old computer screen effect, a new Patch Notes page sorted into categories, balance changes to Apex Hunter, Meteor Strike and the Medi Gun, and a fix for bosses made of several parts.

## New features

**Hidden endings**
Five secret challenge endings, A to E, are now tracked for each character and shown on the Profile page as a riddle until you earn them. They are about how you beat a boss, not just that you beat it. Every ending only counts from the Wall of Flesh onward, and the last one needs a Mechanical Boss or later. A boss older than your world's current stage never counts. Endings only work in singleplayer for now.

**Ending rewards**
Each hidden ending now gives a Profile title and a Pod skin, yours for the whole account. They stay secret on the Shop's Pods and Titles tabs, with only the ending's riddle as a hint, until you earn them.

**A better Profile page**
The Profile page on the title screen now shows more about your character.
- Your real character is drawn in the portrait box.
- Best DPS is the highest damage per second you have reached, measured over 3 seconds. It is saved for each character and shows -- until you have one.
- Your Data Fragments and your rank points, like 25 / 50 toward the next rank, are shown. The Machine Cores row is gone from this page.
- You can pin up to 3 favourite plugins. Click a slot and pick one of the plugins that character has collected. Right click a slot to clear it.
- With more than one character, click the name box at the top right to pick one from a list, or use the arrows.

**A new Patch Notes page**
Balance changes now show as cards, with a NERF, BUFF or ADJUSTED tag and the old and new numbers side by side. The notes are split into categories, the index on the left is grouped by version, and a glowing marker shows until you have opened the newest notes.

## Plugin changes

**Apex Hunter** (Legendary, adjusted)
- Now counts hits on every part of a boss, such as Golem's head and fists and the segments of Calamity worm bosses.
- Burst damage: 5% of the boss's max HP => 100% of the damage you dealt while marking. The burst is still capped at 5% of the boss's max HP.

**Meteor Strike** (Epic, buff)
- Now triggers on crits against every part of a boss fight, such as Golem's head and fists.
- Strike size: 2.5% of the hit part's max HP => 2.5% of the whole boss's max HP.

**Medi Gun** (Epic, buff)
- The damage tether warms up: up to +50% damage after 5 seconds on the same enemy.
- The heal tether warms up too: up to +50% healing after 5 seconds on the same ally, and at once for an ally under half health.
- Damage tether: MK I 6 => 10 per second, MK II 20 => 40 per second, MK III 3x20 => 3x40, MK IV 3x50 => 3x100.
- Overclock hit: 25 => 40, 60 => 100, 150 => 250, 300 => 500.
- Heal tether per second: 3 => 4, 6 => 8, 10 => 12, 16 => 20.
- Switching target, or letting go of the beam, starts the warm-up again.

## Visuals

**Old screen effect**
The Plugins menu, the Shop and Combat Analytics now turn off like an old computer screen, collapsing to a bright line and then a dot, and turn on the same way backwards.

**Readability and polish**
Improved text size and alignment for readability on the Orders and Profile pages, sharper page titles, and cleaner badge tiles and emblem icons.

**A shorter Medi Gun tooltip**
The Medi Gun's tooltip is much shorter now: one line for each beam, the warm-up, and what Overclock does. Hold Shift to see the full Overclock details.

**Sharper text and icons**
Text and icons are sharper on every page and screen, with cleaner edges on diamonds and arrows, especially if your UI scale is above 100%.

**NEW marks that go away**
Plugins marked NEW in the Plugins menu now lose their mark for good once you have opened one and read it for a moment. It stays gone on every character.

## Bugs and fixes

**Bosses made of several parts**
Apex Hunter and Meteor Strike now work on every part of a boss, such as Golem's head and fists and the segments of Calamity worm bosses. Before, hits on those parts did not count.
