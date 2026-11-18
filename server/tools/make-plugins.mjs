// Rebuilds server/src/plugins.json from the mod's plugin files: id, display name, rarity, class and the plain description
// (colour tags removed), so the stats report can show names, compare plugins within their rarity and explain each plugin on hover.
// Run from the repository root after adding or changing a plugin:   node server/tools/make-plugins.mjs
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
const read = (p) => fs.readFileSync(p, 'utf8');

// Every number constant in the mod (const int Foo = 3; static readonly float Bar = 0.5f;), so {Foo} in a description can be filled in.
const consts = {};
function scan(dir) {
  for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
    const p = path.join(dir, e.name);
    if (e.isDirectory()) { if (!['bin', 'obj', 'server', '.git', '.claude'].includes(e.name)) scan(p); continue; }
    if (!e.name.endsWith('.cs')) continue;
    for (const m of read(p).matchAll(/(?:const|static\s+readonly)\s+(?:int|float|double|long)\s+(\w+)\s*=\s*(-?[0-9.]+)[fFdDmM]?\s*;/g)) (consts[m[1]] ??= []).push(+m[2]);
  }
}
scan(root);

const HELPER_DEFAULT = { OnHit: 'on-hit', Electrified: 'Electrified' };

// Reads a C# string expression such as  $"a {AugmentText.Defense("+1")} b" + "c" + Foo.Bar  and returns plain text and what it could not resolve.
function evaluate(expr, unresolved) {
  let i = 0, out = '';
  const skipWs = () => { while (i < expr.length && /\s/.test(expr[i])) i++; };

  function readEscape() { // at a backslash inside a normal string
    const c = expr[i + 1]; i += 2;
    return { n: '\n', t: '\t', '"': '"', '\\': '\\', "'": "'" }[c] ?? c;
  }
  function readString() { // at the opening quote of a plain string
    i++; let s = '';
    while (i < expr.length && expr[i] !== '"') s += expr[i] === '\\' ? readEscape() : expr[i++];
    i++; return s;
  }
  function holeText(code) {
    code = code.trim();
    const num = (name) => consts[name]?.length ? consts[name][0] : null;
    const call = /^((?:\w+\.)*\w+)\(([\s\S]*)\)$/.exec(code); // Name(args), as long as the last ) closes the first (
    if (call) {
      let depth = 0, balanced = true;
      for (let k = call[1].length; k < code.length - 1; k++) { if (code[k] === '(') depth++; else if (code[k] === ')') depth--; if (depth === 0) { balanced = false; break; } }
      if (balanced) {
        const fn = call[1].split('.').pop(), args = call[2].trim();
        if (fn === 'Key') return 'its hotkey';                                   // AugmentText.Key(Augments.XKeybind)
        if (fn === 'FormatCurrentChance') {                                      // a chance such as 0.2 shows as "20% chance"
          const v = num(args.split('.').pop());
          if (v != null) return (Math.round(v * 1000) / 10) + '% chance';
        } else if (fn === 'Scale') {                                             // the starting value; it grows with progress in the game
          const v = num(args.split('.').pop());
          if (v != null) return String(v);
        } else if (call[1].startsWith('AugmentText.')) {                         // a colour helper: keep only its text
          if (args === '') return HELPER_DEFAULT[fn] ?? (unresolved.push(code), '?');
          if (fn === 'Color') { const first = /^("(?:[^"\\]|\\.)*")/.exec(args); if (first) return evaluate(first[1], unresolved); } // Color("text", "hex")
          return evaluate(args, unresolved);
        }
      }
    }
    const m = /^(?:\w+\.)*(\w+)$/.exec(code);
    if (m && num(m[1]) != null) return String(num(m[1]));
    if (m && /Note$/.test(m[1])) return '';                                      // an optional extra note the game adds while playing
    unresolved.push(code); return '?';
  }
  function readInterpolated() { // at $"
    i += 2; let s = '';
    while (i < expr.length && expr[i] !== '"') {
      if (expr[i] === '\\') s += readEscape();
      else if (expr[i] === '{' && expr[i + 1] === '{') { s += '{'; i += 2; }
      else if (expr[i] === '}' && expr[i + 1] === '}') { s += '}'; i += 2; }
      else if (expr[i] === '{') {
        let depth = 1, code = ''; i++;
        while (i < expr.length && depth > 0) {
          if (expr[i] === '"') { const start = i; i++; while (i < expr.length && expr[i] !== '"') i += expr[i] === '\\' ? 2 : 1; i++; code += expr.slice(start, i); continue; }
          if (expr[i] === '{') depth++; if (expr[i] === '}') { depth--; if (depth === 0) break; }
          code += expr[i++];
        }
        i++; // the closing }
        s += holeText(code.replace(/:[^:"]*$/, ''));
      } else s += expr[i++];
    }
    i++; return s;
  }

  while (i < expr.length) {
    skipWs();
    if (expr[i] === '$' && expr[i + 1] === '"') out += readInterpolated();
    else if (expr[i] === '"') out += readString();
    else if (expr[i] === '+') i++;
    else { // a name or a call between the strings
      let code = '', depth = 0;
      while (i < expr.length && !(depth === 0 && /[+\s]/.test(expr[i]) && code.trim())) {
        if (expr[i] === '(') depth++; if (expr[i] === ')') depth--;
        if (expr[i] === '"') { const start = i; i++; while (i < expr.length && expr[i] !== '"') i += expr[i] === '\\' ? 2 : 1; i++; code += expr.slice(start, i); continue; }
        code += expr[i++];
      }
      if (code.trim()) out += holeText(code);
    }
  }
  return out;
}

const map = {}, problems = [];
for (const file of fs.readdirSync(path.join(root, 'AugmentTypes')).filter((f) => f.endsWith('.cs')).sort()) {
  const text = read(path.join(root, 'AugmentTypes', file));
  if (!/class\s+\w+\s*:\s*Augment\b/.test(text)) continue;
  const id = /string\s+Id\s*=>\s*"([^"]+)"/.exec(text)?.[1] ?? /const\s+string\s+ChipId\s*=\s*"([^"]+)"/.exec(text)?.[1];
  const name = /string\s+DisplayName\s*=>\s*"([^"]+)"/.exec(text)?.[1];
  const rarity = /AugmentRarity\s+Rarity\s*=>\s*AugmentRarity\.(\w+)/.exec(text)?.[1];
  const cls = /AugmentClass\s+Class\s*=>\s*AugmentClass\.(\w+)/.exec(text)?.[1];
  if (!id || !name) { problems.push(file + ': no id or name, skipped'); continue; }

  let d = '';
  // Either  Description => ...;   or a property with a getter that ends in  return ...;
  const m = /override\s+string\s+Description\s*=>\s*([^;]*?);/s.exec(text) ?? /override\s+string\s+Description\s*\{[\s\S]*?\breturn\s+([\s\S]*?);\s*\}/.exec(text);
  if (m) {
    const unresolved = [];
    d = evaluate(m[1], unresolved).replace(/[ \t]+\n/g, '\n').replace(/\[its hotkey\]/g, 'its hotkey').replace(/\s+/g, ' ').trim();
    if (unresolved.length) problems.push(file + ': could not fill in ' + unresolved.map((u) => '{' + u + '}').join(', '));
  } else problems.push(file + ': no Description found');
  map[id] = { n: name, r: rarity ?? '', c: cls ?? '', d };
}

fs.writeFileSync(path.join(root, 'server', 'src', 'plugins.json'), JSON.stringify(map, null, 1) + '\n');
console.log('Wrote ' + Object.keys(map).length + ' plugins, ' + Object.values(map).filter((p) => p.d).length + ' with a description.');
if (problems.length) { console.log('\nThings to look at:'); problems.forEach((p) => console.log(' - ' + p)); }