# Anonymous stats server (optional)

This folder is **not** part of the mod (it is in `buildIgnore` in `build.txt`). It is the small online service that receives the
anonymous plugin numbers from players who chose "Share anonymous stats", and shows you a private report.

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
3. Create the table:
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

## How to read the report

- **Plugin DPS**: the plugin's own damage per combat second, on that weapon.
- **OP score**: that Plugin DPS compared with the median plugin at the same stage and class. 2.0 means twice as strong as a typical plugin.
- **Batches**: how many uploads fed the row. Under about 5, ignore it.
- Numbers pool every player who opted in, so a few heavy players can pull them. It shows *where to look*, not an exact verdict.

## Privacy, in plain words

The mod sends only: plugin id, an internal weapon key (like `Terraria/Uzi`), the weapon's damage class, the progress stage,
damage totals, hit counts, combat seconds, the mod version and whether Calamity is installed. No name, Steam account, world name,
install id or chat. The server code never reads the IP address, and the database has no column that could hold one.
The `observability` setting in `wrangler.toml` is off so Cloudflare keeps no request logs for this worker.

Tell players what is collected (the prompt, the config tooltip and your Workshop page all say so), and ask the tModLoader Discord
Workshop moderators before publishing, because the Workshop rules ask that network access be "sensible".

## Turning it off for good

Set `Endpoint` back to `""` in `Core/StatsCollector.cs`, or delete the worker in the Cloudflare dashboard.
