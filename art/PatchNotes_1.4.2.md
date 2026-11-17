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
Balance changes now show as cards, with a NERF, BUFF, ADJUSTED or NEW tag and the old and new numbers side by side. The notes are split into categories, the index on the left is grouped by version, and a glowing marker shows until you have opened the newest notes.

## New plugins

**Upgrade Pod: Vein Miner** (Rare, new)
- Hit one rock, and the whole family tumbles out.
- Your Pod becomes Pod 360, and it works even while the Pod is hidden.
- When you mine an ore, your Pod also mines up to 8 more tiles of the same ore touching it.
- You still need a pickaxe strong enough for that ore, and the extra tiles break one after another, nearest first.
**Upgrade Pod: Watch Your Step** (Common, new)
- Your Pod gets a funny feeling about that floor tile.
- Your Pod becomes Pod 077, and it works even while the Pod is hidden.
- Senses traps within about 15 tiles: dart, spear, flame and spiky ball traps, pressure plates, spikes and geyser traps. It sees explosives from about 25 tiles and boulders from about 30 tiles.
- The Pod flashes red and a small chip points toward the closest one, like "Trap nearby, below and left".
**Upgrade Pod: Pea Shooter** (Common, new)
- Tiny bolts, big attitude. It really tries.
- Your Pod becomes Pod 218, and it works even while the Pod is hidden.
- Shoots a small bolt at the nearest enemy every 1 second.
- Each bolt deals 5 damage, plus a small part of your damage bonuses, so it is a helper and never a weapon.
**Upgrade Pod: Ore Sniffer** (Common, new)
- Your Pod learned to smell treasure. It won't tell you what, only hints.
- Your Pod becomes Pod 311, and it works even while the Pod is hidden.
- Press its button (set the key in the Controls menu) and the Pod sniffs for 2 seconds.
- It gives a riddle about the nearest ore within about 60 tiles, how close you are and which way to dig, with an arrow by the Pod.
- The riddle and arrow stay up for 5 seconds, then it rests for 6 seconds before you can sniff again.

**Upgrade Pod: Lighthouse** (Common, new)
- Who turned the lights on? Your Pod did.
- Your Pod becomes Pod 096 and lights up the area around you with a warm glow.
- Everyone near you sees by it too, and it works even while the Pod is hidden.
- Autopilot is now called Upgrade Pod: Autopilot, so every Pod upgrade is easy to spot.

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
- Overclock hit: MK I 25 => 40, MK II 60 => 100, MK III 150 => 250, MK IV 300 => 500.
- Heal tether: MK I 3 => 4 per second, MK II 6 => 8, MK III 10 => 12, MK IV 16 => 20.
- Switching target, or letting go of the beam, starts the warm up again.

## Visuals

**Old screen effect**
The Plugins menu, the Shop and Combat Analytics now turn off like an old computer screen, collapsing to a bright line and then a dot, and turn on the same way backwards.

**Readability and polish**
Improved text size and alignment for readability on the Orders and Profile pages, sharper page titles, and cleaner badge tiles and emblem icons.

**A shorter Medi Gun tooltip**
The Medi Gun's tooltip is much shorter now: one line for each beam, the warm up, and what Overclock does. Hold Shift to see the full Overclock details.

**Sharper text and icons**
Text and icons are sharper on every page and screen, with cleaner edges on diamonds and arrows, especially if your UI scale is above 100%.

**NEW marks that go away**
Plugins marked NEW in the Plugins menu now lose their mark for good once you have opened one and read it for a moment. It stays gone on every character.

## Bugs and fixes

**Bosses made of several parts**
Apex Hunter and Meteor Strike now work on every part of a boss, such as Golem's head and fists and the segments of Calamity worm bosses. Before, hits on those parts did not count.
