// Fills a LOCAL test server (wrangler dev) with made-up uploads shaped like the mod's real ones, so the report page can be tried.
//   1. In server/:  wrangler d1 execute automata-stats --local --file=schema.sql
//   2. In server/:  wrangler dev           (the local address is printed, usually http://localhost:8787)
//   3. In server/:  node test/seed-local.mjs http://localhost:8787
//   4. Open  http://localhost:8787/report?token=localtest   (the password is in server/.dev.vars)
// It refuses any address that is not this computer, so it can never put fake numbers on the real server.

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

// [plugin, weapon, class, stage, version, uploads, seconds per upload, weapon DPS, Boost in %, how much the Boost varies between uploads (0 to 1)]
const specs = [
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
];

let sent = 0;
for (const [plugin, weapon, cls, stage, ver, uploads, secs, wdps, boost, spread] of specs) {
  for (let i = 0; i < uploads; i++) {
    const b = Math.max(0, boost * (1 + (rand() * 2 - 1) * spread));
    const t = Math.max(1, Math.round(secs * (0.7 + rand() * 0.6) * 10) / 10);
    const wd = Math.round(wdps * t), pd = boost === 0 ? 0 : Math.round(wd * b / 100);
    const row = { p: plugin, w: weapon, c: CLASS[cls], g: STAGE[stage], pd, ph: Math.max(1, Math.round(pd / 40)), wd, wh: Math.max(1, Math.round(wd / 60)), t };
    const res = await fetch(base + '/', { method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify({ v: 1, mod: ver, cal: false, rows: [row] }) });
    if (!res.ok) { console.error('POST failed', res.status, await res.text()); process.exit(1); }
    sent++;
  }
}
console.log('Sent ' + sent + ' test uploads to ' + base);