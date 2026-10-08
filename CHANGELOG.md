# Automata: Plug-in Chips — Upcoming Update Changelog

## Version 1.3.0

### Highlights
- **Automata Music**: [TODO: describe the Automata Music mod and add its Workshop link here]
- **Full UI Visual Overhaul**: Every menu and HUD now shares one clean "cybernetic chassis" look: Plugins menu and inspector, Shop, reward choice cards, tooltips, Combat Analytics, pinned stat HUD, Protocol sidebar, cooldown/charge tiles, and the new Pod 042 advisory ticker.
- **Diagnostics Drawer**: A slide-out panel on the edge of the screen (click or hover to open, dockable left or right, can be turned off in the config) showing hidden stats such as Luck, fishing multipliers and combat mechanics.
- **Automata Protocols**: 12 chip-set bonuses (see section 1).
- **Core Overrides**: build-defining one-per-character chips (see section 2).
- **Combat Analytics & Live DPS Monitor** (see section 6).

### 1. New Feature: Automata Protocols (Chip Synergies)
- **Assigned Plug-in Chip Synergies**: Equipping matching sets of chips now unlocks powerful, passive protocol specifications.
- **Bloodhunter Protocol** (2-Piece Offense Duo: Bloodletter + Festering Wounds):
  - **Exsanguination**: Bleed damage over time increased by +50% (3 DPS -> 5 DPS).
  - Attacks against bleeding targets gain +8% Critical Strike Chance.
- **Field Medic Protocol** (2-Piece Support Duo: Second Wind + Combat Medic):
  - **Triage Efficiency**: Potion sickness duration reduced by -20%.
  - Heart pickups restore +20% more health (+4 HP).
- **Kinetic Protocol** (2-Piece Offense Duo: Momentum Swing + Momentum Crash):
  - **Kinetic Momentum**: Dashing or sprinting at full speed releases a kinetic shockwave on melee impact dealing 75% base weapon damage with radial knockback.
  - Melee attack speed scales up by +1% per 2 mph of current movement speed (up to +15%).
- **Cryo Protocol** (2-Piece Control Duo: Frost Touch + Frostbound):
  - **Absolute Zero**: Enemies afflicted with Frostburn are slowed by 20% (using smooth non-compounding displacement so enemies stay mobile). Slaying a chilled or Frostburned enemy shatters them into 3 independent miniature ice crystal shards (dealing 40 damage each) that scatter outwards, weave gracefully through the air with afterimage trails, and home separately into nearby enemies. Includes network authority guards, self-proc immunity, and internal cooldown to prevent cascades or multi-segment spam.
  - Custom procedural pixel-art Frost Crystal / Snowflake HUD icon in Glacial Ice Cyan (`#38BDF8`).
- **Volt Protocol** (2-Piece Melee/Shock Duo: Chain Lightning + Stormcaller):
  - **Stormcaller Alignment**: Converted `Stormcaller` from Magic to Melee (`Class => AugmentClass.Melee`), triggering lightning strikes on melee kills so both chips harmonize cleanly under a pure Melee build.
  - **Superconductor**: Lightning and electrical procs inflict Electrified (dealing continuous shock damage). Attacks against Electrified enemies gain +8% Critical Strike Chance and have a 25% chance to arc static electricity to another nearby target dealing 25 damage with electric spark trails and deflection audio.
  - Custom procedural pixel-art Double Lightning Bolt HUD icon in High-Voltage Electric Violet (`#8B5CF6`).
- **Hivemind Protocol** (2-Piece Summoner Duo: Queen's Swarm + Necromancer's Court):
  - **Swarm Coordination**: Minion and sentry attacks inject Micro-Nanites into enemies (4-second duration, rendered with crawling toxic neon-lime nanite particles and acid-green lighting).
  - For every active minion attacking the same target, that enemy takes **+3 flat bonus damage per hit from all sources**, allowing rapid summoner swarms to melt high-durability enemies and bosses. Includes multiplayer client-server synchronization, automatic target tagging, and active minion proximity/recent hit tracking.
  - Custom procedural pixel-art Micro-Drone / Nanite Cell Cluster HUD icon in Toxic Neon Lime / Acid Cyber-Green (`#10B981`).
- **Marksman Protocol** (2-Piece Ranged Duo: Sharpshooter + Deadeye):
  - **Pinpoint Ballistics**:
    - Ranged weapon projectile flight velocity increased by **+25%**, giving arrows and bullets flatter, high-velocity trajectories.
    - Ranged hits landing from beyond 350 pixels mark the target with a **Kinetic Mark** for 4 seconds, displaying laser ruby tracking particles and a glowing scope reticle.
    - Attacks against Kinetic Marked targets gain **+15 Armor Penetration**.
    - Critical strikes on marked targets detonate the mark, releasing **3 high-velocity kinetic shrapnel flechettes** (dealing 35 piercing damage with ricochet audio and spark trails).
  - Custom procedural pixel-art Sniper Crosshairs / Reticle Targeting Matrix HUD icon in Laser Ruby / Scope Crimson (`#F43F5E`).
- **Arcane Surge Protocol** (2-Piece Magic Duo: Overcharge + Spell Echo):
  - **Astral Discharge**:
    - Consuming mana charges an **Astral Matrix** at 1 stack per 20 mana spent (up to 5 stacks at 100 mana spent).
    - Features animated orbiting cosmic motes circling the player that indicate current matrix stacks (1-5).
    - At 5 stacks, the next magic hit discharges an **Arcane Nova** shockwave dealing **65 flat magic damage** to all enemies in a 140px radius with cosmic sound and radial particle rings.
    - Instantly refunds **+30 Mana** back to the player with vanilla blue combat text feedback upon detonation.
  - Custom procedural pixel-art Arcane Singularity / Pulsating Mana Core HUD icon in Cosmic Indigo / Astral Violet (`#818CF8`).
- **Bastion Protocol** (2-Piece Defense Duo: Bulwark + Adaptive Armor):
  - **Kinetic Hardening & Concussive Deflection**:
    - **Stack Preservation**: Bulwark's kinetic barrier absorbs incoming damage before armor plating is compromised, preventing Adaptive Armor defense stacks from being lost when dodging an attack.
    - **Concussive Deflection**: Blocking a hit with Bulwark triggers a concussive shockwave in a 160px radius that violently pushes enemies away, dealing **50 flat kinetic damage + 5 bonus damage per current Adaptive Armor stack** (up to 100 damage at max 10 stacks) with heavy electric and titanium shockwave bursts.
    - **Barrier Acceleration**: While Adaptive Armor is at maximum stacks (+10 defense), Bulwark's shield recharge time is accelerated by **25%** (7.5 seconds instead of 10 seconds).
  - Custom procedural pixel-art Heavy Kinetic Aegis / Fortified Barrier Matrix HUD icon in Bastion Cyan / Heavy Cobalt (`#38BDF8`).
- **Gunslinger Protocol** (2-Piece Ranged Duo: Quickfire + Rapid Fire):
  - **Rotary Acceleration & Lead Storm**:
    - **Rotary Acceleration**: Continuously firing any ranged weapon ramps up attack speed by **+2.5% per second** of sustained fire, up to a maximum of **+10% bonus attack speed** after 4 seconds (reaching +20% total attack speed between Rapid Fire and the protocol).
    - **Linger Window**: Ceasing fire maintains the spin-up for a 1-second grace window before resetting to 0.
    - **Lead Storm (Peak Spin-Up)**: While maintaining full spin-up (4+ seconds of continuous fire), ranged attacks gain **+8% Critical Strike Chance**, accompanied by golden muzzle heat sparkles and spin-up ignition audio.
  - Custom procedural pixel-art 6-Chamber Revolver Cylinder / Gatling Rotary Cluster HUD icon in High-Caliber Brass / Rotary Gold (`#EAB308`).
- **Lasher Protocol** (2-Piece Summoner Duo: Whip Master + Whip Cracker):
  - **Sonic Crack & Predatory Command**:
    - **Sonic Crack**: Striking an enemy who is already at maximum (5) Whip Cracker stacks detonates a concussive **Sonic Crack** shockwave, dealing **45 flat summon damage** to all enemies in a 130-pixel radius and resetting the target's debuff duration back to 4 seconds. Accompanied by sonic boom audio (`SoundID.Item105` + `SoundID.Item38`) and radiating amber shockwave particles.
    - **Predatory Command**: Friendly minions attacking an enemy at maximum (5) Whip Cracker stacks gain **+12% Critical Strike Chance** against that target (enabling full double-damage critical strikes for minion attacks).
  - Custom procedural pixel-art Coiled Barbed Bullwhip / Resonant Kinetic Lash HUD icon in Neural Amber / Whip Lash Orange (`#F97316`).
- **Fortune Protocol Rework** (5-Piece Utility Protocol: Lucky Strike + Fortune's Favor + Lucky Find + Scavenger's Luck + Wild Card):
  - Consolidated all 5 lucky plug-in chips into a multi-tiered Automata protocol.
  - **Tiered Protocol Specifications**:
    - **(2) Probability Matrix**: Increases World Luck by +0.15 and boosts Fortune scaling and lucky procs by +20% effectiveness.
    - **(4) Jackpot Calibration**: Increases World Luck by +0.35 total and boosts Fortune scaling and lucky procs by +45% effectiveness.
    - **(5) House Edge (Full Set)**: Increases World Luck by +0.50 total. Critical strikes release a radial shower of gold coins dealing 50 damage to nearby enemies.
  - **Dynamic Live Stat Clarity**: Replaced ambiguous "(scales with Fortune)" text with live calculated trigger odds (e.g. "18% chance (15% base + 3% Fortune)") and explicit stat lines explaining World Luck and trigger boosts.
  - Integrated into the choice card UI as clean, unbordered text displaying the protocol (`FORTUNE PROTOCOL`) with interactive hover inspection tooltips.
- **Protocol Completion Bias (+20%)**:
  - Owning any incomplete protocol set now grants a +20% bias across boss reward tier rolls, gacha rolls, and card draft picks toward offering missing protocol chips.
- **Docked HUD Protocol Monitor**:
  - In-game HUD widget docked on the right side of the screen below the minimap.
  - Automatically tracks and displays active protocols with custom pixel-art icons (Field Medic cross, Bloodhunter broadsword, Kinetic forward chevron / battering ram, and Fortune 5-pip golden die).
  - Hovering slides out an inspection panel showing current installation progress, active/locked specifications, and assigned chips.

---

### 2. Complete Rework: Automata Core Overrides (Keystones)
- **Rebranding & Limit**: Rebranded Keystones into **Core Overrides** (limit 1 per character, permanent installation with massive build-defining power and distinct operational trade-offs).
- **Type-B: Berserker Protocol**:
  - Permanent 50% HP cap removed.
  - **Berserk Overclock**: While below 50% HP, gain +40% damage, +15% movement speed, and +10% attack speed.
  - **Trade-off**: Potion sickness duration increased by +15 seconds.
- **Type-D: Dreadnought Protocol** (Renamed from Type-D: Bastion Protocol to resolve naming conflict with the 2-piece Bastion Protocol):
  - Passive: +20 defense, 15% incoming damage reduction.
  - **Operational Trade-off**: Outgoing weapon damage reduced by -15%.
  - **Kinetic Shockwave & Energy Barrier**: Accumulating 120 cumulative damage detonates a kinetic shockwave dealing **60 flat kinetic damage** with violent radial knockback, activating a **1.5s Energy Barrier** that negates all incoming damage.
  - **Invulnerability & Safety Overhaul**: Full lethal blow protection prevents dying on or during barrier activation.
  - **Audio & Visual Polish**: Stripped bold floating combat text ("ENERGY BARRIER!"), replacing it with crisp barrier ignition audio (`SoundID.Item93`) and expanding electric shockwave particles.
- **Type-S: Synchronizer Protocol**:
  - **Dynamic Polarity Trade-off**: Cycles polarity every 10 seconds between two extreme combat modes:
    - **Offensive Overclock (Combat Pulse, 10s)**: +15% weapon damage, +10% critical strike chance, and +10 armor penetration.
      - **Operational Trade-off**: Halts all natural life regeneration completely while active.
    - **Restorative Nano-Repair (Repair Pulse, 10s)**: +15 defense, +12 life regeneration, and +15% movement speed.
      - **Operational Trade-off**: Outgoing weapon damage reduced by -15% while active.
  - **Twin Orbiting Polarity Resonators**: Two dual-phase energy motes orbit the player in a 2.5D elliptical plane, visually displaying current system state:
    - Glowing amber/orange motes with warm dynamic lighting and trailing sparks during Offensive Overclock.
    - Glowing emerald motes with cybernetic green dynamic lighting and restorative particle trails during Restorative Nano-Repair.
  - **Pre-Shift Telegraphing (Final 2 Seconds)**:
    - At 2 seconds remaining, the motes accelerate to 2.4x rotational speed and emit energetic warning flickers.
    - High-tech acoustic warning ticks cue at 2.0s and 1.0s remaining to alert the player of the impending polarity shift.
  - **Buff Bar Integration**: Dedicated status buffs (`Type-S: Offensive Overclock` and `Type-S: Restorative Nano-Repair`) track remaining phase duration on the vanilla buff bar with a live countdown timer (10s to 0s) and full tooltip explanations.
  - **Audio & Visual Polish**: Stripped bold floating combat text ("COMBAT PULSE" / "REPAIR PULSE"), replacing them with distinct harmonic audio chimes (`SoundID.Item29` laser charge on overclock, `SoundID.Item4` harmonic chime on repair) and expanding particle rings.
- **Save Migration**: Existing characters with old Avatar chips or legacy IDs automatically migrate upon loading.

---

### 3. Balance Changes
- **Apex Hunter**:
  - Threshold increased from **10 hits** to **15 hits**.
  - Burst damage reduced from **15% max HP** to **5% max HP**.
  - Added an **8-second internal cooldown** between procs to prevent rapid-fire weapons from repeatedly melting bosses.
- **Combat Medic**:
  - Reduced baseline life regeneration aura from 3 HP/sec to 1.5 HP/sec.
- **Echo Chamber**:
  - Echo shot damage reduced from **30%** to **20%**.
  - Overhauled for ranged volleys (such as Daedalus Stormbow): echo projectiles now use independent local NPC hit immunity, ensuring echo shots deal damage even during global enemy invulnerability frames.
- **Chain Lightning**:
  - Overhauled from strictly requiring critical strikes to triggering on all melee hits.
  - Range increased from 200 to 380 pixels (~24 tiles).
  - **15 On-Hit Damage & Chaining**: Melee hits now directly deal 15 on-hit damage to the target and chain electricity to up to 2 nearby enemies dealing 50% of your total on-hit damage (synergizing with Twin Strike, Iron Rhythm, and Critical Surge).
  - Added electric zap sounds and spark arcs connecting chained enemies, with guaranteed hit registration on Target Dummies and in multiplayer.

### 4. UI & Visual Polish
- **Dedicated "On-Hit" Proc Color**:
  - Replaced the previous amber/orange trigger color on `on-hit` text with a dedicated **Vivid Electric Fuchsia** (`#FF3EA5`).
  - Standardized all chip descriptions to use `on-hit` (with hyphen) consistently (fixing `Chain Lightning`).
  - Ensures on-hit combat triggers instantly pop out and catch the player's attention without visually clashing or blending into crits (gold), damage (crimson), attack speed (orange), or protocol titles.
- **Ricochet Engine Bullet Alignment & Visuals**:
  - Fixed bullet sprite orientation from rendering perpendicular (vertical bar) to horizontal, pointing directly forward along the flight velocity.
  - Added glowing neon purple tracer trails (`PreDraw` afterimages), luminous in-flight particles (`DustID.PurpleTorch` + `DustID.GemAmethyst`), and metallic deflection sounds/spark bursts when ricocheting off enemies.
- **Pod 042 Advisory Tip Settings**: New client config options (`Settings > Mod Configuration > Automata Configuration`) to turn automatic Pod 042 tips off and to choose how often they appear (1-30 minutes, default 7). The TIP button and hotkey still work when automatic tips are off.

---

### 5. Multiplayer & Networking Fixes
- **TCP Stream Desync Protection**: Fixed an issue where incoming effect/damage packets with invalid NPC targets returned early without draining payload bytes, permanently corrupting the packet stream for connected clients.
- **Remote Max HP Synchronization**: Fixed vanilla Terraria bug where remote players' max HP defaulted to 100 on connected clients; added live synchronization across multiplayer.
- **Medi Gun Tether Reliability**: Fixed ally health checks so tether healing no longer locks out on unsynced clients; added instant client-side HP prediction on heal.

---

### 6. New Feature: Plugin Combat Analytics & Live DPS Monitor
- **Real-Time Combat Telemetry**:
  - Monitors all outgoing damage dealt by the player, breaking down contributions between primary weapon hits, individual plugins, and active protocol procs.
  - **Live DPS & Sliding Window**: Real-time sliding 3-second window calculating continuous live DPS, updated dynamically on every frame.
  - **Dual View Modes**:
    - **[LAST 10M]**: Rolling 10-minute queue tracking recent performance during boss encounters or mob clearing.
    - **[TOTAL]**: Full session accumulation tracking overall plugin impact across the entire play session.
  - **Owned Plugin Breakdown**: Automatically lists every currently installed plugin (even with 0 damage dealt), making it easy to spot underperforming or inactive plugins.
  - **Damage Blocked Tracking**:
    - Full telemetry tracking for defensive plugins, including kinetic barrier absorptions (Bulwark, Type-D Dreadnought Protocol), dodge negations (Mirror Image, Void Step), lethal damage protections (PreKill death saves on Type-D barrier, Soul Martyr, Lifeline), and damage reduction percentages (Type-D 15% damage reduction, Martyr's Resolve aura mitigation).
    - **Dedicated Metric Card**: Added a 5th header card ("DAMAGE BLOCKED") styled in shield emerald (`#34D399`).
    - **Pinnable HUD Widget**: Players can pin the "DMG BLOCKED" stat to their live in-game HUD overlay alongside Live DPS, Total Damage, Combat Time, and Hits/Crits.
    - **Telemetry List Integration**: Plugins display `◈ [Amount] Blocked` in their subtag, attacks blocked count in the hits column, and blocked share percentage in the damage column for zero-damage defensive plugins. Sorted by combined combat impact (`TotalDamage + DamageBlocked`).
  - **Interactive Controls & Draggable UI**:
    - Toggleable via hotkey (default: **L**, configurable in Terraria Controls) or via the docked **[DPS]** button directly above the Protocol HUD.
    - **Draggable Window**: High-tech cybernetic header allows moving the analytics window freely across the screen.
    - **[PAUSE]**: Freezes the display for post-combat review without losing accumulated data.
    - **[RESET]**: Clears the active damage queue and resets metrics to zero with a single click.
    - **Scrollable List**: Smooth mouse wheel scrolling with dynamic scroll thumb indicator.
    - **Visual Percentage Bars**: Color-coded progress bars reflect each plugin's percentage of total damage output or damage blocked using plugin rarity palette colors.
    - **Detailed Tooltips**: Hovering over any record displays total hits, critical strike count, crit rate percentage, max hit, and total damage blocked with block count.
  - **Tracking Fixes**: Vengeance, Inferno's Heart, Volatile Rounds, Twin Strike, Wild Card, Bulwark and Type-D Dreadnought shockwave damage is now counted under the right chip. Arcane Singularity, Eldritch Covenant and War God's Tempo damage is no longer misattributed to your held weapon. A paused feed is automatically resumed when you enter a world. Minion, sentry and projectile hits are now credited to the weapon that fired them instead of whatever you were holding (no more Torch, Silver Bullet or Target Dummy listed as weapons), and stat-boost chips now show the bonus damage they add to your hits, which is taken out of the weapon's row.

---

### 7. Security & Stability
- **Developer Tools Locked in Multiplayer**:
  - The `/augment` command, the secret dev-mode chip editor, the dev keybinds (trigger reward, spawn vendor, open shop, reset cooldowns) and the debug network packets now require the new server-side **Enable Developer Tools** option (`Settings > Mod Configuration > Automata Server Configuration`), which is **off** by default. Singleplayer is unaffected, so the dev tools always work there.
  - Fixes a hole where any player on a multiplayer server could grant themselves plug-in chips.
- **Save & Load Hardening**:
  - A failing chip can no longer abort a character save or load; the error is logged and that chip's extra data falls back to defaults.
  - If loading fails partway, the character keeps the data that did load instead of losing its chips.
  - Boss kill counts from older saves load safely instead of risking a crash.
- **Network Packet Safety**: Malformed or unexpected mod packets are logged and ignored instead of crashing the server or dropping the player.
