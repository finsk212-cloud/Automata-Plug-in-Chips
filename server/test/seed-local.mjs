// Fills a LOCAL test server (wrangler dev) with made-up uploads shaped like the mod's real ones, so the report page can be tried.
//   1. In server/:  wrangler d1 execute automata-stats --local --file=schema.sql
//   2. In server/:  wrangler dev           (the local address is printed, usually http://localhost:8787)
//   3. In server/:  node test/seed-local.mjs http://localhost:8787
//   4. Open  http://localhost:8787/report?token=localtest   (the password is in server/.dev.vars)
// It refuses any address that is not this computer, so it can never put fake numbers on the real server.
//
// The data holds three kinds of players, so the Gear check can be tried:
//   - normal players: stage-appropriate weapons at every stage (the "peers" the check compares against)
//   - a tester with endgame gear (Zenith and friends) in early worlds, a small part of the data at most stages
//   - the same tester making up MOST of the Start stage (so the Start median is overgeared too)

const base = (process.argv[2] || 'http://localhost:8787').replace(/\/$/, '');
const host = new URL(base).hostname;
if (!['localhost', '127.0.0.1', '[::1]'].includes(host)) {
  console.error('Refusing to send test data to ' + host + '. Only localhost is allowed.');
  process.exit(1);
}

let seed = 12345;
const rand = () => ((seed = (seed * 1664525 + 1013904223) >>> 0) / 4294967296);
const CLASS = { Universal: 0, Melee: 1, Ranged: 2, Magic: 3, Summon: 4, Support: 5 };
const STAGE = { Start: 0, Skeletron: 1, WoF: 2, Mechs: 3, Plantera: 4, Golem: 5, MoonLord: 6 };
const STAGE_NAMES = Object.keys(STAGE);

// The mod records a hit's damage capped at the enemy's remaining life, so a huge weapon does not hit for DPS/2.5 forever:
// its hits flatten out at roughly what the enemies of that stage can lose in one hit.
const HIT_CAP = [250, 400, 700, 1500, 2000, 3000, 5000];
const hitSize = (dps, stage) => Math.max(5, Math.min(dps / 2.5, HIT_CAP[STAGE[stage]]));

// Pass --tester-only to leave out the normal players: then no stage has other weapons to compare with, and the Gear check should say it
// cannot judge instead of calling everything fine.
const testerOnly = process.argv.includes('--tester-only');

// [plugin, weapon, class, stage, version, uploads, seconds per upload, weapon DPS, Boost in %, how much the Boost varies between uploads (0 to 1), optional fixed damage per hit]
const specs = [];

// ---- normal players: three weapons per class at every stage ----
// Only weapons whose rarity and base damage were checked on the Terraria wiki are used (see INFO below); the stage each one is paired with is
// roughly when a normal player could have it. Everything here is made up except those weapon facts: the DPS, the Boost and the upload counts.
const NORMAL = {
  Melee: [['Katana', 'LightsBane', 'Muramasa'], ['NightsEdge', 'Sunfury', 'Muramasa'], ['Excalibur', 'NightsEdge', 'Sunfury'], ['Excalibur', 'ChlorophyteSaber', 'NightsEdge'],
    ['ChlorophyteSaber', 'TerraBlade', 'Excalibur'], ['TerraBlade', 'ChlorophyteSaber', 'Excalibur'], ['TerraBlade', 'ChlorophyteSaber', 'Excalibur']],
  Ranged: [['Boomstick', 'Minishark', 'PhoenixBlaster'], ['Boomstick', 'Minishark', 'PhoenixBlaster'], ['Megashark', 'Uzi', 'PhoenixBlaster'], ['Megashark', 'Uzi', 'Minishark'],
    ['Uzi', 'SniperRifle', 'Megashark'], ['SniperRifle', 'Tsunami', 'Uzi'], ['Tsunami', 'SniperRifle', 'Megashark']]
};
const BASE_DPS = [40, 90, 150, 320, 420, 560, 800];
if (!testerOnly) STAGE_NAMES.forEach((stage, si) => {
  for (const cls of ['Melee', 'Ranged']) {
    NORMAL[cls][si].forEach((w, wi) => {
      const dps = BASE_DPS[si] * [0.85, 1, 1.2][wi] * (cls === 'Ranged' ? 1.1 : 1);
      specs.push(['steady_heart', 'Terraria/' + w, cls, stage, '1.4.2', 3, 25, dps, 6, 0.3]);
      specs.push(['momentum_swing', 'Terraria/' + w, cls, stage, '1.4.2', 3, 25, dps, 4, 0.3]);
    });
  }
});

// ---- a tester with endgame gear: a few rows at most stages ----
specs.push(
  ['armor_piercer',   'Terraria/Zenith',      'Melee',  'Start',     '1.4.2',  1, 19, 791, 11, 0.1],
  ['tombstone_boost', 'Terraria/Zenith',      'Melee',  'Start',     '1.4.2',  1, 14, 265, 27, 0.1],
  ['death_blast',     'Terraria/Zenith',      'Melee',  'Start',     '1.4.2',  4,  7, 191, 21, 0.4],
  ['chain_lightning', 'Terraria/Zenith',      'Melee',  'Skeletron', '1.4.1', 15, 40, 887, 34, 0.3],
  ['chain_lightning', 'Terraria/Zenith',      'Melee',  'Skeletron', '1.4.2', 14, 40, 887,  6, 0.3],
  ['chain_lightning', 'Terraria/Zenith',      'Melee',  'WoF',       '1.4.2', 21, 25, 1771, 3.3, 0.3],
  ['stormcaller',     'Terraria/Zenith',      'Melee',  'Skeletron', '1.4.2', 14, 40, 887, 2.9, 0.3],
  ['momentum_swing',  'Terraria/Zenith',      'Melee',  'Skeletron', '1.4.2', 28, 60, 407, 2.7, 0.2],
  ['momentum_swing',  'Terraria/Zenith',      'Melee',  'WoF',       '1.4.2', 50, 100, 852, 3.7, 0.2],
  ['riposte',         'Terraria/Zenith',      'Melee',  'Skeletron', '1.4.2', 15, 50, 300, 38, 0.3],
  ['riposte',         'Terraria/TerraBlade',  'Melee',  'Skeletron', '1.4.2', 12, 30, 420, 41, 0.3],
  ['riposte',         'Terraria/Zenith',      'Melee',  'WoF',       '1.4.2', 14, 50, 500, 18, 3.0],
  ['iron_rhythm',     'Terraria/Uzi',         'Ranged', 'Golem',     '1.4.2',  9, 20, 811, 19, 0.2],
  ['iron_rhythm',     'Terraria/Uzi',         'Ranged', 'Start',     '1.4.2', 13, 80, 640, 4.7, 0.4],
  ['apex_hunter',     'Terraria/Uzi',         'Ranged', 'Golem',     '1.4.2',  9, 20, 830, 7.3, 0.2],
  ['apex_hunter',     'Terraria/Megashark',   'Ranged', 'Golem',     '1.4.2', 12, 40, 900, 12, 0.2],
  ['steady_heart',    'Terraria/Zenith',      'Melee',  'Skeletron', '1.4.2', 16, 60, 125, 3.2, 0.3],
  ['pod_shoot',       'none',                 'Universal','Start',   '1.4.2', 14, 30, 0, 0, 0],
  ['stormcaller',     'Terraria/Zenith',      'Melee',  'Skeletron', '1.4.1', 12, 40, 887, 5, 0.3]
);

// ---- the same tester making up most of the Start stage: four more endgame weapons at the very start ----
// How an overgeared hit changes Boost: a plugin that adds a percentage of the hit (riposte) stays the same, a plugin with flat damage that
// grows with the world stage (stormcaller) looks weak next to a huge weapon, and a plugin that hits other enemies (death_blast) looks strong,
// because the weapon's own hit is capped at the enemy's life while the plugin's hits on the others are not.
for (const w of ['StarWrath', 'Meowmere', 'SolarEruption', 'TerraBlade']) {
  const dps = { StarWrath: 1300, Meowmere: 1100, SolarEruption: 950, TerraBlade: 900 }[w];
  specs.push(['riposte',     'Terraria/' + w, 'Melee', 'Start', '1.4.2', 4, 25, dps, 30, 0.2]);
  specs.push(['stormcaller', 'Terraria/' + w, 'Melee', 'Start', '1.4.2', 4, 25, dps, 1.5, 0.2]);
  specs.push(['death_blast', 'Terraria/' + w, 'Melee', 'Start', '1.4.2', 4, 25, dps, 70, 0.2]);
}
// Normal players at the Start stage use the same plugins with ordinary gear, for comparison.
if (!testerOnly) for (const w of ['Katana', 'LightsBane', 'Muramasa']) {
  specs.push(['riposte',     'Terraria/' + w, 'Melee', 'Start', '1.4.2', 4, 25, 45, 30, 0.2]);
  specs.push(['stormcaller', 'Terraria/' + w, 'Melee', 'Start', '1.4.2', 4, 25, 45, 14, 0.2]);
  specs.push(['death_blast', 'Terraria/' + w, 'Melee', 'Start', '1.4.2', 4, 25, 45, 22, 0.2]);
}
// A slow heavy hitter with ordinary DPS: huge hits (bigger than what weapons 3 stages ahead do per hit) but nothing special per second.
// The Gear check must leave it in, because only one of its two numbers is high.
if (!testerOnly) specs.push(['riposte', 'Terraria/SlowHeavyHammer', 'Melee', 'Start', '1.4.2', 5, 40, 45, 30, 0.2, 400]);

// What each weapon IS, as newer versions of the mod send it: [rarity 0 to 11 (-1 = modded or unknown), base damage]. Weapons not listed here send
// nothing (like an older mod version), so the page has to cope with missing rarity too. The normal weapons get rarity by stage.
// Rarity and base damage of the REAL weapons below were read from the Terraria wiki (terraria.wiki.gg), not guessed. The two fake weapons are marked.
const INFO = {
  'Terraria/Katana': [1, 18], 'Terraria/LightsBane': [1, 16], 'Terraria/Muramasa': [2, 24], 'Terraria/NightsEdge': [3, 40], 'Terraria/Sunfury': [3, 64],
  'Terraria/Excalibur': [5, 72], 'Terraria/ChlorophyteSaber': [7, 80], 'Terraria/TerraBlade': [8, 85],
  'Terraria/Boomstick': [2, 14], 'Terraria/Minishark': [2, 6], 'Terraria/PhoenixBlaster': [3, 30], 'Terraria/Megashark': [5, 25], 'Terraria/Uzi': [7, 30],
  'Terraria/SniperRifle': [8, 200], 'Terraria/Tsunami': [8, 53],
  'Terraria/Zenith': [10, 190], 'Terraria/Meowmere': [10, 200], 'Terraria/StarWrath': [10, 170], 'Terraria/SolarEruption': [10, 105],
  'Terraria/SlowHeavyHammer': [2, 30],          // FAKE weapon, for the slow-heavy-hitter case
  'CalamityMod/FakeModdedBlade': [-1, 400]      // FAKE modded weapon with an unknown rarity
};
if (!process.argv.includes('--no-rarity')) {
  // a modded weapon with unknown rarity used at Start, to see the page fall back to the other signs
  specs.push(['riposte', 'CalamityMod/FakeModdedBlade', 'Melee', 'Start', '1.4.2', 4, 25, 900, 30, 0.2]);
}

let sent = 0;
for (const [plugin, weapon, cls, stage, ver, uploads, secs, wdps, boost, spread, fixedHit] of specs) {
  for (let i = 0; i < uploads; i++) {
    const b = Math.max(0, boost * (1 + (rand() * 2 - 1) * spread));
    const t = Math.max(1, Math.round(secs * (0.7 + rand() * 0.6) * 10) / 10);
    const wd = Math.round(wdps * t), pd = boost === 0 ? 0 : Math.round(wd * b / 100);
    const info = process.argv.includes('--no-rarity') ? null : INFO[weapon];
    const row = { p: plugin, w: weapon, c: CLASS[cls], ...(info ? { r: info[0], bd: info[1] } : {}), g: STAGE[stage], pd, ph: Math.max(1, Math.round(pd / 40)), wd, wh: wd > 0 ? Math.max(1, Math.round(wd / (fixedHit || hitSize(wdps, stage)))) : 0, t };
    const res = await fetch(base + '/', { method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify({ v: 1, mod: ver, cal: false, rows: [row] }) });
    if (!res.ok) { console.error('POST failed', res.status, await res.text()); process.exit(1); }
    sent++;
  }
}
console.log('Sent ' + sent + ' test uploads to ' + base);
