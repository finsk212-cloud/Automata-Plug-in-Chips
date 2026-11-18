// Automata: Plug-in Chips anonymous stats server (a Cloudflare Worker with a D1 database).
//
//   POST /              the mod sends a batch here (no login, anonymous)
//   GET  /report        your private report page   (needs ?token=YOUR_REPORT_TOKEN)
//   GET  /report.json   the same numbers as data   (needs ?token=YOUR_REPORT_TOKEN)
//
// The page itself lives in report.html. plugins.json (made by tools/make-plugins.mjs) gives the report each plugin's name and rarity.
//
// PRIVACY: this code never reads or stores the sender's IP address, name or anything that identifies them. A batch is only
// numbers about plugins and weapons, and it is ADDED into running totals, so single players cannot be picked out.

import PAGE from './report.html';
import PLUGINS from './plugins.json';

const MAX_BODY_BYTES = 256 * 1024;
const MAX_ROWS = 400;
const PLUGIN_ID = /^[a-z0-9_]{2,48}$/;          // plugin ids are lower case letters, digits and _
const WEAPON_KEY = /^[A-Za-z0-9_./-]{1,80}$/;   // "Terraria/Uzi", "CalamityMod/SomeGun", or "none"
const MOD_VERSION = /^[0-9.]{1,20}$/;

// Sanity limits for ONE batch row (a batch is at most about 10 minutes of play), so a forged request cannot ruin the totals.
const LIMITS = { pd: 2e9, ph: 1e6, wd: 2e9, wh: 1e6, t: 3600, c: 10, g: 30, rare: 11, bd: 100000 };

function int(value, max) {
  return Number.isInteger(value) && value >= 0 && value <= max ? value : null;
}

function num(value, max) {
  return typeof value === 'number' && Number.isFinite(value) && value >= 0 && value <= max ? value : null;
}

async function ingest(request, env) {
  const length = Number(request.headers.get('content-length') || 0);
  if (length > MAX_BODY_BYTES) return new Response('too large', { status: 413 });

  const text = await request.text();
  if (text.length > MAX_BODY_BYTES) return new Response('too large', { status: 413 });

  let body;
  try { body = JSON.parse(text); } catch { return new Response('bad json', { status: 400 }); }

  if (!body || body.v !== 1 || typeof body.mod !== 'string' || !MOD_VERSION.test(body.mod) || !Array.isArray(body.rows)) {
    return new Response('bad batch', { status: 400 });
  }

  const cal = body.cal === true ? 1 : 0;
  const statements = [];

  for (const r of body.rows.slice(0, MAX_ROWS)) {
    if (!r || typeof r.p !== 'string' || !PLUGIN_ID.test(r.p) || typeof r.w !== 'string' || !WEAPON_KEY.test(r.w) || r.w.includes('..')) continue;

    const c = int(r.c, LIMITS.c), g = int(r.g, LIMITS.g);
    const pd = int(r.pd, LIMITS.pd), ph = int(r.ph, LIMITS.ph), wd = int(r.wd, LIMITS.wd), wh = int(r.wh, LIMITS.wh);
    const t = num(r.t, LIMITS.t);
    if ([c, g, pd, ph, wd, wh, t].includes(null)) continue;

    // Consistency: this batch's Boost (plugin damage / weapon damage). It only counts when the batch is long enough and the weapon hit
    // enough times to mean something, because a few lucky hits give a wild Boost and these sums cannot be corrected later.
    const counted = t >= 10 && wh >= 10 && wd > 0 ? 1 : 0;
    const boost = counted ? pd / wd : 0;

    statements.push(env.DB.prepare(
      `INSERT INTO stats2 (plugin, weapon, class, stage, cal, ver, batches, pdmg, phits, wdmg, whits, seconds, nb, sb, sb2)
       VALUES (?1, ?2, ?3, ?4, ?5, ?11, 1, ?6, ?7, ?8, ?9, ?10, ?12, ?13, ?14)
       ON CONFLICT(plugin, weapon, class, stage, cal, ver) DO UPDATE SET
         batches = batches + 1, pdmg = pdmg + ?6, phits = phits + ?7, wdmg = wdmg + ?8, whits = whits + ?9, seconds = seconds + ?10,
         nb = nb + ?12, sb = sb + ?13, sb2 = sb2 + ?14`
    ).bind(r.p, r.w, c, g, cal, pd, ph, wd, wh, t, body.mod, counted, boost, boost * boost));

    // What the weapon is (rarity and base damage). Older versions of the mod do not send these, so they are optional.
    if (r.w !== 'none' && Number.isInteger(r.r) && r.r >= -1 && r.r <= LIMITS.rare && Number.isInteger(r.bd) && r.bd >= 0 && r.bd <= LIMITS.bd) {
      statements.push(env.DB.prepare(
        `INSERT INTO weapons (weapon, rare, bd) VALUES (?1, ?2, ?3) ON CONFLICT(weapon) DO UPDATE SET rare = ?2, bd = ?3`
      ).bind(r.w, r.r, r.bd));
    }
  }

  if (statements.length > 0) await env.DB.batch(statements);
  return new Response('ok');
}

function authorised(url, env) {
  const given = url.searchParams.get('token') || '';
  const real = env.REPORT_TOKEN || '';
  if (!real || given.length !== real.length) return false;
  let diff = 0; // constant time compare
  for (let i = 0; i < real.length; i++) diff |= given.charCodeAt(i) ^ real.charCodeAt(i);
  return diff === 0;
}

// Every row: the current table, plus the old table (from before versions were kept) shown as the version "earlier".
async function reportJson(env) {
  const { results } = await env.DB.prepare(
    `SELECT plugin, weapon, class, stage, cal, ver, batches, pdmg, phits, wdmg, whits, seconds, nb, sb, sb2 FROM stats2
     UNION ALL
     SELECT plugin, weapon, class, stage, cal, 'earlier', batches, pdmg, phits, wdmg, whits, seconds, 0, 0, 0 FROM stats`
  ).all();
  // weapon -> [rarity, base damage]. Weapons that were never uploaded by a version that sends these are simply missing.
  // (If the weapons table does not exist yet, because the worker went live before schema.sql was run, the report still loads without it.)
  const weapons = {};
  try {
    const w = await env.DB.prepare('SELECT weapon, rare, bd FROM weapons').all();
    for (const x of w.results) weapons[x.weapon] = [x.rare, x.bd];
  } catch { /* no weapons table yet */ }
  return new Response(JSON.stringify({ plugins: PLUGINS, rows: results, weapons }), { headers: { 'content-type': 'application/json', 'cache-control': 'no-store' } });
}

export default {
  async fetch(request, env) {
    const url = new URL(request.url);

    if (request.method === 'POST' && url.pathname === '/') return ingest(request, env);

    if (request.method === 'GET' && (url.pathname === '/report' || url.pathname === '/report.json')) {
      if (!authorised(url, env)) return new Response('Not found', { status: 404 });
      return url.pathname === '/report.json'
        ? reportJson(env)
        : new Response(PAGE, { headers: { 'content-type': 'text/html; charset=utf-8', 'cache-control': 'no-store' } });
    }

    return new Response('Not found', { status: 404 });
  }
};
