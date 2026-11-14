// Automata: Plug-in Chips anonymous stats server (a Cloudflare Worker with a D1 database).
//
//   POST /              the mod sends a batch here (no login, anonymous)
//   GET  /report        your private report page   (needs ?token=YOUR_REPORT_TOKEN)
//   GET  /report.json   the same numbers as data   (needs ?token=YOUR_REPORT_TOKEN)
//
// PRIVACY: this code never reads or stores the sender's IP address, name or anything that identifies them. A batch is only
// numbers about plugins and weapons, and it is ADDED into running totals, so single players cannot be picked out.

const MAX_BODY_BYTES = 256 * 1024;
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
    if (!r || typeof r.p !== 'string' || !PLUGIN_ID.test(r.p) || typeof r.w !== 'string' || !WEAPON_KEY.test(r.w) || r.w.includes('..')) continue;

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

const PAGE = `<!doctype html><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Plug-in Chips balance report</title>
<style>
 :root{--bg:#14161c;--card:#1c202a;--line:#2c3243;--text:#ece8d6;--dim:#99a0b3;--gold:#d6c36a;--red:#ff6b6b;--amber:#ffc857;--blue:#6bb8ff;--green:#7ed99a}
 *{box-sizing:border-box} body{font:15px/1.45 system-ui,Segoe UI,sans-serif;background:var(--bg);color:var(--text);margin:0;padding:24px 28px 60px}
 h1{font-size:22px;margin:0 0 4px} h2{font-size:15px;margin:0 0 8px;color:var(--gold);letter-spacing:.04em}
 .sub{color:var(--dim);margin:0 0 18px;max-width:1100px}
 .cards{display:grid;grid-template-columns:repeat(auto-fit,minmax(190px,1fr));gap:12px;margin:0 0 18px}
 .card{background:var(--card);border:1px solid var(--line);border-radius:10px;padding:12px 14px}
 .card b{display:block;font-size:26px;line-height:1.1;margin-top:2px} .card span{color:var(--dim);font-size:13px}
 .legend{background:var(--card);border:1px solid var(--line);border-radius:10px;padding:12px 16px;margin:0 0 18px;display:grid;grid-template-columns:repeat(auto-fit,minmax(260px,1fr));gap:8px 24px;font-size:14px}
 .legend div b{color:var(--gold)}
 .bar{display:flex;flex-wrap:wrap;gap:10px 16px;align-items:center;margin:0 0 14px}
 .bar label{color:var(--dim);font-size:13px;display:flex;flex-direction:column;gap:3px}
 select,input[type=text],input[type=number]{background:#10131a;color:var(--text);border:1px solid var(--line);padding:7px 9px;border-radius:6px;font:inherit;font-size:14px}
 input[type=number]{width:84px} input[type=checkbox]{margin-right:6px}
 .tabs{display:flex;gap:0;margin:0 0 12px} .tabs button{background:var(--card);color:var(--dim);border:1px solid var(--line);padding:9px 18px;font:inherit;cursor:pointer}
 .tabs button:first-child{border-radius:8px 0 0 8px} .tabs button:last-child{border-radius:0 8px 8px 0;border-left:0}
 .tabs button.on{background:var(--gold);color:#1b1a12;font-weight:600}
 .attn{background:#2a1a1c;border:1px solid #5a2a2e;border-radius:10px;padding:10px 14px;margin:0 0 14px;display:none}
 .attn b{color:var(--red)}
 table{border-collapse:collapse;width:100%;background:var(--card);border:1px solid var(--line);border-radius:10px;overflow:hidden}
 th,td{padding:9px 12px;border-bottom:1px solid var(--line);text-align:right;white-space:nowrap}
 th{position:sticky;top:0;background:#222736;color:var(--gold);cursor:pointer;font-size:13px;user-select:none}
 th:hover{background:#2a3044} td:first-child,th:first-child,td.l,th.l{text-align:left}
 tbody tr:hover{background:#232838} tr.few td{opacity:.55}
 tr.v-strong td:first-child{box-shadow:inset 4px 0 var(--amber)} tr.v-toostrong td:first-child{box-shadow:inset 4px 0 var(--red)}
 tr.v-weak td:first-child{box-shadow:inset 4px 0 var(--blue)}
 .pl{font-weight:600} .dim{color:var(--dim)} .mini{font-size:12px;color:var(--dim)}
 .meter{display:inline-block;width:90px;height:9px;background:#10131a;border-radius:5px;vertical-align:middle;margin-right:8px;overflow:hidden}
 .meter i{display:block;height:100%} .chip{display:inline-block;padding:2px 9px;border-radius:12px;font-size:12px;font-weight:600}
 .c-toostrong{background:#5a2a2e;color:#ffb0b0} .c-strong{background:#4d3d18;color:var(--amber)} .c-normal{background:#1f3a2a;color:var(--green)}
 .c-weak{background:#1c3550;color:var(--blue)} .c-none{background:#2a2e3a;color:var(--dim)} .c-na{background:#2a2e3a;color:var(--dim)}
 .q-good{color:var(--green)} .q-some{color:var(--amber)} .q-few{color:var(--red)}
 #empty{padding:30px;text-align:center;color:var(--dim);display:none}
</style>
<h1>Plug-in Chips balance report</h1>
<p class="sub">Anonymous numbers from players who chose to share. Use it to spot plugins that do far more than the others, and see which weapons make them strong.</p>

<div class="cards">
 <div class="card"><span>Plugins with data</span><b id="k-plugins">-</b></div>
 <div class="card"><span>Weapons seen</span><b id="k-weapons">-</b></div>
 <div class="card"><span>Too strong now</span><b id="k-toostrong" style="color:var(--red)">-</b></div>
 <div class="card"><span>Strong</span><b id="k-strong" style="color:var(--amber)">-</b></div>
</div>

<div class="legend">
 <div><b>Boost</b>: the extra damage a plugin adds on top of the weapon. +25% means the weapon does 100 and the plugin adds 25 more.</div>
 <div><b>Share</b>: the plugin's part of all the damage (plugin plus weapon). 20% means one fifth of the damage came from the plugin.</div>
 <div><b>Plugin DPS</b>: plugin damage per second of fighting. <b>Weapon DPS</b>: the weapon's own damage per second while that plugin was equipped.</div>
 <div><b>Strength</b>: Plugin DPS compared with the typical plugin at the same progress stage and weapon class. 1.0x is typical, 2.0x is double. It needs at least 3 plugins with data to compare.</div>
 <div><b>Data</b>: <span class="q-good">good</span> (10+ uploads and 2+ minutes), <span class="q-some">some</span>, <span class="q-few">few</span>. Trust a row only when it says good.</div>
</div>

<div class="attn" id="attn"></div>

<div class="tabs"><button id="t-plugin" class="on">By plugin (all weapons together)</button><button id="t-weapon">By plugin and weapon</button></div>

<div class="bar">
 <label>Progress stage<select id="f-stage"></select></label>
 <label>Weapon class<select id="f-class"></select></label>
 <label>Plugin<select id="f-plugin"></select></label>
 <label>Weapon search<input type="text" id="f-q" placeholder="e.g. Uzi"></label>
 <label>Min uploads<input type="number" id="f-b" value="3" min="1"></label>
 <label>Min seconds<input type="number" id="f-s" value="20" min="0"></label>
 <label style="flex-direction:row;align-items:center;margin-top:16px"><input type="checkbox" id="f-low" checked>Show rows with little data (greyed)</label>
 <label style="flex-direction:row;align-items:center;margin-top:16px"><input type="checkbox" id="f-cal">Calamity players only</label>
</div>

<table><thead id="head"></thead><tbody id="body"></tbody></table>
<div id="empty">No rows match. Lower "Min uploads" and "Min seconds", or tick "Show rows with little data".</div>

<script>
var STAGES = ['Start (before Skeletron)','Skeletron','Wall of Flesh','All Mechs','Plantera','Golem','Moon Lord','Providence','Devourer of Gods','Yharon','Exo Mechs / Supreme Calamitas'];
var CLASSES = ['Universal','Melee','Ranged','Magic','Summon','Support'];
var token = new URLSearchParams(location.search).get('token') || '';
var rows = [], view = 'plugin', sortKey = 'power', sortDir = -1;
var $ = function (id) { return document.getElementById(id); };

function esc(s) { return String(s).replace(/[&<>"]/g, function (c) { return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]; }); }
function nice(id) { return id.split('_').map(function (w) { return w.charAt(0).toUpperCase() + w.slice(1); }).join(' '); }
function weaponName(key) {
  if (key === 'none') return 'No weapon (damage over time)';
  var parts = key.split('/'), name = parts[parts.length - 1].replace(/([a-z0-9])([A-Z])/g, '$1 $2');
  return parts[0] === 'Terraria' || parts.length < 2 ? name : name + ' (' + parts[0] + ')';
}
function fmt(n) { return n >= 1e6 ? (n / 1e6).toFixed(2) + 'M' : n >= 1e4 ? (n / 1e3).toFixed(1) + 'k' : n >= 100 ? n.toFixed(0) : n.toFixed(1); }
function pct(n) { return n >= 10 ? n.toFixed(0) + '%' : n.toFixed(1) + '%'; }
function mmss(s) { s = Math.round(s); var m = Math.floor(s / 60), r = s % 60; return m + ':' + (r < 10 ? '0' : '') + r; }
function median(a) { a = a.slice().sort(function (x, y) { return x - y; }); var m = a.length >> 1; return a.length % 2 ? a[m] : (a[m - 1] + a[m]) / 2; }

function build() {
  var byWeapon = view === 'weapon', calOnly = $('f-cal').checked, map = {}, list = [];
  rows.forEach(function (r) {
    if (calOnly && !r.cal) return;
    var k = [r.plugin, byWeapon ? r.weapon : '*', r.class, r.stage].join('|'), x = map[k];
    if (!x) { x = map[k] = { plugin: r.plugin, weapon: byWeapon ? r.weapon : '', cls: r.class, stage: r.stage, batches: 0, pd: 0, ph: 0, wd: 0, sec: 0 }; list.push(x); }
    x.batches += r.batches; x.pd += r.pdmg; x.ph += r.phits; x.wd += r.wdmg; x.sec += r.seconds;
  });
  var minB = +$('f-b').value || 1, minS = +$('f-s').value || 0;
  list.forEach(function (x) {
    x.dps = x.sec > 0 ? x.pd / x.sec : 0; x.wdps = x.sec > 0 ? x.wd / x.sec : 0;
    x.boost = x.wd > 0 ? x.pd / x.wd * 100 : 0; x.share = x.pd + x.wd > 0 ? x.pd / (x.pd + x.wd) * 100 : 0; x.avg = x.ph > 0 ? x.pd / x.ph : 0;
    x.enough = x.batches >= minB && x.sec >= minS;
    x.quality = x.batches >= 10 && x.sec >= 120 ? 2 : x.enough ? 1 : 0;
  });
  var groups = {};
  list.forEach(function (x) { if (x.enough && x.dps > 0) { var g = x.stage + '|' + x.cls; (groups[g] = groups[g] || []).push(x.dps); } });
  list.forEach(function (x) {
    var g = groups[x.stage + '|' + x.cls];
    x.typical = g && g.length >= 3 ? median(g) : 0;
    x.power = x.typical > 0 ? x.dps / x.typical : -1;
    x.verdict = x.pd === 0 ? 'none' : x.power < 0 ? 'na' : x.power >= 2 ? 'toostrong' : x.power >= 1.4 ? 'strong' : x.power <= 0.6 ? 'weak' : 'normal';
  });
  return list;
}

var VERDICT = { toostrong: 'Too strong', strong: 'Strong', normal: 'Normal', weak: 'Weak', none: 'No damage', na: 'Not enough to compare' };
var COLOR = { toostrong: 'var(--red)', strong: 'var(--amber)', normal: 'var(--green)', weak: 'var(--blue)' };
var QUALITY = ['few', 'some', 'good'];

function render() {
  var all = build(), stage = $('f-stage').value, cls = $('f-class').value, plug = $('f-plugin').value, q = $('f-q').value.toLowerCase(), low = $('f-low').checked;
  var list = all.filter(function (x) {
    return (stage === 'all' || x.stage == stage) && (cls === 'all' || x.cls == cls) && (plug === 'all' || x.plugin === plug) &&
      (!q || (x.plugin + ' ' + weaponName(x.weapon)).toLowerCase().indexOf(q) >= 0) && (low || x.enough);
  });
  list.sort(function (a, b) {
    var va = a[sortKey], vb = b[sortKey];
    if (sortKey === 'plugin') { va = nice(a.plugin); vb = nice(b.plugin); }
    if (sortKey === 'weapon') { va = weaponName(a.weapon); vb = weaponName(b.weapon); }
    return (typeof va === 'string' ? va.localeCompare(vb) : va - vb) * sortDir;
  });

  var cols = [['plugin', 'Plugin', 1]];
  if (view === 'weapon') cols.push(['weapon', 'Weapon', 1]);
  cols.push(['stage', 'Stage', 1], ['cls', 'Class', 1], ['quality', 'Data', 1], ['sec', 'Time', 0], ['wdps', 'Weapon DPS', 0], ['dps', 'Plugin DPS', 0], ['boost', 'Boost', 0], ['share', 'Share', 0], ['avg', 'Avg plugin hit', 0], ['power', 'Strength', 0]);
  $('head').innerHTML = '<tr>' + cols.map(function (c) { return '<th class="' + (c[2] ? 'l' : '') + '" data-k="' + c[0] + '">' + c[1] + (sortKey === c[0] ? (sortDir < 0 ? ' ▼' : ' ▲') : '') + '</th>'; }).join('') + '</tr>';

  $('body').innerHTML = list.map(function (x) {
    var strength = x.power < 0 || !x.pd ? '<span class="dim">-</span> ' :
      '<span class="meter"><i style="width:' + Math.min(100, x.power / 3 * 100).toFixed(0) + '%;background:' + (COLOR[x.verdict] || 'var(--dim)') + '"></i></span>' + x.power.toFixed(2) + 'x ';
    var cells = ['<td class="pl">' + esc(nice(x.plugin)) + '</td>'];
    if (view === 'weapon') cells.push('<td class="l">' + esc(weaponName(x.weapon)) + '</td>');
    cells.push('<td class="l">' + esc(STAGES[x.stage] || ('Stage ' + x.stage)) + '</td>', '<td class="l">' + (CLASSES[x.cls] || x.cls) + '</td>',
      '<td class="l q-' + QUALITY[x.quality] + '">' + QUALITY[x.quality] + ' <span class="mini">(' + x.batches + ' uploads)</span></td>',
      '<td>' + mmss(x.sec) + '</td>', '<td>' + fmt(x.wdps) + '</td>', '<td>' + (x.pd ? fmt(x.dps) : '<span class="dim">0</span>') + '</td>',
      '<td>' + (x.pd && x.wd ? '+' + pct(x.boost) : '<span class="dim">-</span>') + '</td>', '<td>' + (x.pd ? pct(x.share) : '<span class="dim">-</span>') + '</td>',
      '<td>' + (x.ph ? fmt(x.avg) : '<span class="dim">-</span>') + '</td>',
      '<td>' + strength + '<span class="chip c-' + x.verdict + '">' + VERDICT[x.verdict] + '</span></td>');
    return '<tr class="v-' + x.verdict + (x.enough ? '' : ' few') + '">' + cells.join('') + '</tr>';
  }).join('');
  $('empty').style.display = list.length ? 'none' : 'block';

  // headline numbers use every row that has enough data, not only what the filters show
  var ok = all.filter(function (x) { return x.enough; });
  var plugins = {}, weapons = {};
  rows.forEach(function (r) { if (!calOnlyRow(r)) return; plugins[r.plugin] = 1; if (r.weapon !== 'none') weapons[r.weapon] = 1; });
  $('k-plugins').textContent = Object.keys(plugins).length; $('k-weapons').textContent = Object.keys(weapons).length;
  var ts = ok.filter(function (x) { return x.verdict === 'toostrong'; }), st = ok.filter(function (x) { return x.verdict === 'strong'; });
  $('k-toostrong').textContent = ts.length; $('k-strong').textContent = st.length;
  var attn = $('attn');
  if (ts.length) {
    ts.sort(function (a, b) { return b.power - a.power; });
    attn.style.display = 'block';
    attn.innerHTML = '<b>Needs attention:</b> ' + ts.slice(0, 6).map(function (x) { return esc(nice(x.plugin)) + (view === 'weapon' ? ' on ' + esc(weaponName(x.weapon)) : '') + ' (' + x.power.toFixed(1) + 'x, ' + esc(STAGES[x.stage] || x.stage) + ')'; }).join(', ');
  } else attn.style.display = 'none';
}
function calOnlyRow(r) { return !$('f-cal').checked || r.cal; }

function fillFilters() {
  function opts(sel, items, label) { $(sel).innerHTML = '<option value="all">' + label + '</option>' + items.map(function (i) { return '<option value="' + i[0] + '">' + esc(i[1]) + '</option>'; }).join(''); }
  var stages = {}, classes = {}, plugins = {};
  rows.forEach(function (r) { stages[r.stage] = 1; classes[r.class] = 1; plugins[r.plugin] = 1; });
  opts('f-stage', Object.keys(stages).map(Number).sort(function (a, b) { return a - b; }).map(function (s) { return [s, STAGES[s] || 'Stage ' + s]; }), 'All stages');
  opts('f-class', Object.keys(classes).map(Number).sort(function (a, b) { return a - b; }).map(function (c) { return [c, CLASSES[c] || 'Class ' + c]; }), 'All classes');
  opts('f-plugin', Object.keys(plugins).sort().map(function (p) { return [p, nice(p)]; }), 'All plugins');
}

$('head').addEventListener('click', function (e) { var th = e.target.closest('th'); if (!th) return; var k = th.getAttribute('data-k'); sortDir = sortKey === k ? -sortDir : (k === 'plugin' || k === 'weapon' || k === 'stage' || k === 'cls' ? 1 : -1); sortKey = k; render(); });
['f-stage', 'f-class', 'f-plugin', 'f-q', 'f-b', 'f-s', 'f-low', 'f-cal'].forEach(function (id) { $(id).addEventListener('input', render); });
$('t-plugin').onclick = function () { view = 'plugin'; $('t-plugin').className = 'on'; $('t-weapon').className = ''; render(); };
$('t-weapon').onclick = function () { view = 'weapon'; $('t-weapon').className = 'on'; $('t-plugin').className = ''; render(); };

fetch('/report.json?token=' + encodeURIComponent(token)).then(function (r) { return r.ok ? r.json() : Promise.reject(r.status); }).then(function (data) {
  rows = data; fillFilters(); render();
}).catch(function (e) { document.body.insertAdjacentHTML('beforeend', '<p style="color:var(--red)">Could not load the numbers (' + e + '). Is the password in the address right?</p>'); });
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
