// Automata: Plug-in Chips anonymous stats server (a Cloudflare Worker with a D1 database).
//
//   POST /              the mod sends a batch here (no login, anonymous)
//   GET  /report        your private report page   (needs ?token=YOUR_REPORT_TOKEN)
//   GET  /report.json   the same numbers as data   (needs ?token=YOUR_REPORT_TOKEN)
//
// PRIVACY: this code never reads or stores the sender's IP address, name or anything that identifies them. A batch is only
// numbers about plugins and weapons, and it is ADDED into running totals, so single players cannot be picked out.

const MAX_BODY_BYTES = 64 * 1024;
const MAX_ROWS = 400;
const PLUGIN_ID = /^[a-z0-9_]{2,48}$/;          // plugin ids are lower case letters, digits and _
const WEAPON_KEY = /^[A-Za-z0-9_./-]{1,80}$/;   // "Terraria/Uzi", "CalamityMod/SomeGun", or "none"
const MOD_VERSION = /^[0-9.]{1,20}$/;

// Sanity limits for ONE batch row (a batch is at most about 10 minutes of play), so a forged request cannot ruin the totals.
const LIMITS = { pd: 2e9, ph: 1e6, wd: 2e9, wh: 1e6, t: 3600, c: 10, g: 30 };

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
    if (!r || typeof r.p !== 'string' || !PLUGIN_ID.test(r.p) || typeof r.w !== 'string' || !WEAPON_KEY.test(r.w)) continue;

    const c = int(r.c, LIMITS.c), g = int(r.g, LIMITS.g);
    const pd = int(r.pd, LIMITS.pd), ph = int(r.ph, LIMITS.ph), wd = int(r.wd, LIMITS.wd), wh = int(r.wh, LIMITS.wh);
    const t = num(r.t, LIMITS.t);
    if ([c, g, pd, ph, wd, wh, t].includes(null)) continue;

    statements.push(env.DB.prepare(
      `INSERT INTO stats (plugin, weapon, class, stage, cal, batches, pdmg, phits, wdmg, whits, seconds)
       VALUES (?1, ?2, ?3, ?4, ?5, 1, ?6, ?7, ?8, ?9, ?10)
       ON CONFLICT(plugin, weapon, class, stage, cal) DO UPDATE SET
         batches = batches + 1, pdmg = pdmg + ?6, phits = phits + ?7, wdmg = wdmg + ?8, whits = whits + ?9, seconds = seconds + ?10`
    ).bind(r.p, r.w, c, g, cal, pd, ph, wd, wh, t));
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

async function reportJson(env) {
  const { results } = await env.DB.prepare(
    `SELECT plugin, weapon, class, stage, cal, batches, pdmg, phits, wdmg, whits, seconds FROM stats`
  ).all();
  return new Response(JSON.stringify(results), { headers: { 'content-type': 'application/json', 'cache-control': 'no-store' } });
}

const PAGE = `<!doctype html><meta charset="utf-8"><title>Plug-in Chips stats</title>
<style>
 body{font:14px system-ui,sans-serif;background:#14161c;color:#e6e2d0;margin:24px}
 h1{font-size:20px} .row{display:flex;gap:12px;flex-wrap:wrap;margin:12px 0}
 select,input{background:#1f2330;color:#e6e2d0;border:1px solid #3a4054;padding:6px 8px;border-radius:4px}
 table{border-collapse:collapse;width:100%} th,td{padding:6px 10px;border-bottom:1px solid #2a3042;text-align:right}
 th{cursor:pointer;color:#c9b86c;position:sticky;top:0;background:#14161c} td:first-child,th:first-child,td:nth-child(2),th:nth-child(2){text-align:left}
 .op{color:#ff7a7a;font-weight:600} .hi{color:#ffc857} .dim{color:#8a90a2}
</style>
<h1>Plug-in Chips: plugin damage by weapon</h1>
<p class="dim">Each row is one plugin on one weapon. <b>Plugin DPS</b> = the plugin's own damage per combat second. <b>Share</b> = plugin damage as a part of
(plugin + weapon damage). <b>OP score</b> = Plugin DPS compared with the median of other plugins at the same stage and class (2.0 = twice the typical plugin).
Numbers pool every player who opted in, so a few heavy players can pull an average. Check "Batches" before trusting a row.</p>
<div class="row">
 <label>Stage <select id="stage"></select></label>
 <label>Min batches <input id="minb" type="number" value="5" min="1" style="width:70px"></label>
 <label>Min combat seconds <input id="mins" type="number" value="60" min="0" style="width:90px"></label>
 <label>Search <input id="q" placeholder="plugin or weapon"></label>
 <label><input id="calonly" type="checkbox"> Calamity players only</label>
</div>
<table id="t"><thead><tr>
 <th data-k="plugin">Plugin</th><th data-k="weapon">Weapon</th><th data-k="stage">Stage</th><th data-k="batches">Batches</th>
 <th data-k="seconds">Combat s</th><th data-k="dps">Plugin DPS</th><th data-k="share">Share</th><th data-k="avgHit">Avg hit</th><th data-k="op">OP score</th>
</tr></thead><tbody></tbody></table>
<script>
const token = new URLSearchParams(location.search).get('token') || '';
let rows = [], sortKey = 'op', sortDir = -1;
const fmt = n => n >= 1e6 ? (n/1e6).toFixed(2)+'M' : n >= 1e3 ? (n/1e3).toFixed(1)+'k' : n.toFixed(n < 10 ? 2 : 0);

function median(a){ if(!a.length) return 0; a = [...a].sort((x,y)=>x-y); const m = a.length>>1; return a.length%2 ? a[m] : (a[m-1]+a[m])/2; }

function build(raw, calOnly){
  const merged = new Map();
  for (const r of raw) {
    if (calOnly && !r.cal) continue;
    const k = [r.plugin, r.weapon, r.class, r.stage].join('|');
    const m = merged.get(k) || { plugin:r.plugin, weapon:r.weapon, class:r.class, stage:r.stage, batches:0, pdmg:0, phits:0, wdmg:0, seconds:0 };
    m.batches += r.batches; m.pdmg += r.pdmg; m.phits += r.phits; m.wdmg += r.wdmg; m.seconds += r.seconds;
    merged.set(k, m);
  }
  const list = [...merged.values()].map(m => ({ ...m,
    dps: m.seconds > 0 ? m.pdmg / m.seconds : 0,
    share: m.pdmg + m.wdmg > 0 ? m.pdmg / (m.pdmg + m.wdmg) : 0,
    avgHit: m.phits > 0 ? m.pdmg / m.phits : 0 }));
  const groups = new Map();
  for (const m of list) { const g = m.stage+'|'+m.class; (groups.get(g) || groups.set(g, []).get(g)).push(m.dps); }
  const med = new Map([...groups].map(([g, v]) => [g, median(v.filter(x => x > 0))]));
  for (const m of list) { const d = med.get(m.stage+'|'+m.class) || 0; m.op = d > 0 ? m.dps / d : 0; }
  return list;
}

function render(){
  const stage = document.getElementById('stage').value, minb = +document.getElementById('minb').value, mins = +document.getElementById('mins').value;
  const q = document.getElementById('q').value.toLowerCase();
  const list = build(rows, document.getElementById('calonly').checked).filter(m =>
    (stage === 'all' || m.stage == stage) && m.batches >= minb && m.seconds >= mins && (m.plugin+' '+m.weapon).toLowerCase().includes(q));
  list.sort((a,b) => (typeof a[sortKey] === 'string' ? a[sortKey].localeCompare(b[sortKey]) : a[sortKey]-b[sortKey]) * sortDir);
  document.querySelector('#t tbody').innerHTML = list.slice(0, 500).map(m =>
    '<tr><td>'+m.plugin+'</td><td>'+m.weapon+'</td><td>'+m.stage+'</td><td>'+m.batches+'</td><td>'+fmt(m.seconds)+'</td><td>'+fmt(m.dps)+
    '</td><td>'+(m.share*100).toFixed(0)+'%</td><td>'+fmt(m.avgHit)+'</td><td class="'+(m.op>=2?'op':m.op>=1.4?'hi':'')+'">'+m.op.toFixed(2)+'</td></tr>').join('');
}

document.querySelectorAll('th').forEach(th => th.onclick = () => { const k = th.dataset.k; sortDir = sortKey === k ? -sortDir : -1; sortKey = k; render(); });
['stage','minb','mins','q','calonly'].forEach(id => document.getElementById(id).oninput = render);

fetch('/report.json?token=' + encodeURIComponent(token)).then(r => r.ok ? r.json() : Promise.reject(r.status)).then(data => {
  rows = data;
  const stages = [...new Set(rows.map(r => r.stage))].sort((a,b)=>a-b);
  document.getElementById('stage').innerHTML = '<option value="all">All</option>' + stages.map(s => '<option>'+s+'</option>').join('');
  render();
}).catch(e => { document.body.insertAdjacentHTML('beforeend', '<p class="op">Could not load the numbers (' + e + '). Is the token in the address right?</p>'); });
</script>`;

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
