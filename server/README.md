# Anonymous stats server (optional)

This folder is **not** part of the mod (it is in `buildIgnore` in `build.txt`). It is the small online service that receives the
anonymous plugin numbers from players who chose "Share anonymous stats", and shows you a private balance report.

Nothing is collected or sent by the mod until you do **step 5** below. While `StatsUploader.Endpoint` is empty, the mod collects
nothing, sends nothing and never shows the prompt.

## What it costs

Nothing at your size. It runs on a free Cloudflare account (Workers and D1 both have free plans).

## Set it up (about 15 minutes, you do this part yourself)

1. Make a free account at cloudflare.com and install Node.js (nodejs.org).
2. In a terminal in this `server` folder, run:
   ```
   npm install -g wrangler
   wrangler login
   wrangler d1 create automata-stats
   ```
   The last command prints a `database_id`. Paste it into `wrangler.toml` where it says `PASTE-THE-ID-HERE`.
3. Create the tables:
   ```
   wrangler d1 execute automata-stats --remote --file=schema.sql
   ```
4. Choose a long random password for your report page and store it as a secret, then deploy:
   ```
   wrangler secret put REPORT_TOKEN
   wrangler deploy
   ```
   `wrangler deploy` prints your address, like `https://automata-stats.YOURNAME.workers.dev`.
5. Put that address in the mod: open `Core/StatsCollector.cs`, set `public const string Endpoint = "https://automata-stats.YOURNAME.workers.dev/";`
   then Build + Reload. Now the prompt appears on the title screen for players who have not answered.

Your report is at `https://automata-stats.YOURNAME.workers.dev/report?token=YOUR_REPORT_TOKEN`. Keep that link private.

## Upgrading an existing server to the new report

The new report keeps the mod version with every number and a "consistency" measure, so it needs one new table. Do these in this order,
because if the new worker goes live before the table exists, uploads fail and the mod drops them after three tries:

1. `wrangler d1 execute automata-stats --remote --file=schema.sql`  (safe to repeat; it only adds what is missing and never touches your numbers)
2. `wrangler deploy`

Your old numbers stay. The report shows them as the version "earlier". New uploads are filed under the mod version that sent them.
The mod itself does not need to change: it already sends its version.

## How to read the report

The page starts on the **newest mod version**, because balance is about the current build. Pick "All versions" to pool everything
(it warns you that data from before and after a patch gets mixed).

- **Boost**: the extra damage a plugin adds on top of its weapon (+25% means the weapon does 100 and the plugin adds 25). The verdict is
  based on Boost because it stays fair across weak and strong weapons. Plugin DPS compared with other plugins ("vs peers") is shown only as
  context, since it says nothing about how big a plugin is.
- **Verdict**: Too strong and Strong are Boost above the lines for the plugin's rarity. Low is under 30% of the Strong line. Plugins that
  deal no damage, or that have no weapon to compare against (damage over time), are "not judged".
- **Balance targets** (a panel at the bottom): the lines per rarity. **The defaults are placeholders I picked, not rules.** Set them to
  the Boost you think each rarity should have; they are remembered in your browser only.
- **Data**: good is 10+ uploads and 2+ minutes. Only good rows reach "What to look at first" (nerf candidates, watch list, buff
  candidates). Rows with thin data are hidden from the table until you tick "Show rows with thin data".
- **Uploads**: a batch is counted once for each weapon it holds a row for, so the number can run a little high for plugins used with several weapons.
- **Consistency**: how much a plugin's Boost changes between uploads (steady, varies, swingy). Short or low-hit uploads (under 10 seconds or 10 weapon hits) are left out. It needs 3 or more counted uploads from the new
  version on, so older rows show a dash. Uploads from one player are alike, so this is a warning sign, not a statistical guarantee.
- **Click any row or list item** for the plugin detail: Boost by stage, the weapons it was seen with (and whether one weapon dominates the
  data), a version-by-version history with the change between versions, a size suggestion ("cut about 35% to reach the Strong line"),
  and a patch note card to copy as a starting point.
- **Heat map**: plugins down, stages across, coloured by verdict. **Rarity ladder** (top): the median Boost per rarity, which should climb
  from Common to Legendary.
- **Gaps** (bottom): plugins nobody has uploaded yet, and plugins that never dealt damage.

Numbers pool every player who opted in, so a few heavy players can pull them. It shows *where to look*, not an exact verdict.

## Changing the report

- `src/report.html` is the whole page (no outside scripts or fonts, because your password is in the address).
- `src/plugins.json` gives each plugin's name, rarity and plain description (shown when you hover a plugin name). Rebuild it after adding or renaming a plugin, from the repository root:
  `node server/tools/make-plugins.mjs`. A plugin missing from it still shows, just without a rarity (it then uses the Rare lines).

## Trying the report on your own computer (never touches the real server)

```
wrangler d1 execute automata-stats --local --file=schema.sql
wrangler dev
node test/seed-local.mjs http://localhost:8787
```
Then open `http://localhost:8787/report?token=localtest` (the test password is in `.dev.vars`, which is not committed). `seed-local.mjs`
refuses any address that is not this computer, so it can never add fake numbers to the real database.

## Privacy, in plain words

The mod sends only: plugin id, an internal weapon key (like `Terraria/Uzi`), the weapon's damage class, the progress stage,
damage totals, hit counts, combat seconds, the mod version and whether Calamity is installed. No name, Steam account, world name,
install id or chat. The server code never reads the IP address, and the database has no column that could hold one or a time.
For the consistency measure the server also keeps, per row, how many uploads counted and two running sums of their Boost (the Boost and
its square). These are plain totals that mix every player together, so no single upload can be read back out.
The `observability` setting in `wrangler.toml` is off so Cloudflare keeps no request logs for this worker.

Tell players what is collected (the prompt, the config tooltip and your Workshop page all say so), and ask the tModLoader Discord
Workshop moderators before publishing, because the Workshop rules ask that network access be "sensible".

## Turning it off for good

Set `Endpoint` back to `""` in `Core/StatsCollector.cs`, or delete the worker in the Cloudflare dashboard.
