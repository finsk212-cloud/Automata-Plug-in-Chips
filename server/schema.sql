-- Run this whole file with:  wrangler d1 execute automata-stats --remote --file=schema.sql
-- It is safe to run again: it only creates what is missing, and never changes or deletes numbers you already have.
--
-- There is no player, install, world, IP or time column on purpose: nothing in here can be traced back to anyone.

-- THE OLD TABLE (before version 1.4.2 of the report). New uploads no longer go here. The report still reads it and shows those
-- numbers as the version "earlier".
CREATE TABLE IF NOT EXISTS stats (
  plugin   TEXT    NOT NULL,
  weapon   TEXT    NOT NULL,
  class    INTEGER NOT NULL,
  stage    INTEGER NOT NULL,
  cal      INTEGER NOT NULL,
  batches  INTEGER NOT NULL DEFAULT 0,
  pdmg     INTEGER NOT NULL DEFAULT 0,
  phits    INTEGER NOT NULL DEFAULT 0,
  wdmg     INTEGER NOT NULL DEFAULT 0,
  whits    INTEGER NOT NULL DEFAULT 0,
  seconds  REAL    NOT NULL DEFAULT 0,
  PRIMARY KEY (plugin, weapon, class, stage, cal)
);

-- THE CURRENT TABLE. One row per (plugin, weapon, damage class, progress stage, Calamity yes/no, mod version). Every batch a
-- player sends is ADDED to the matching row, so single players cannot be picked out.
CREATE TABLE IF NOT EXISTS stats2 (
  plugin   TEXT    NOT NULL,
  weapon   TEXT    NOT NULL,
  class    INTEGER NOT NULL,
  stage    INTEGER NOT NULL,
  cal      INTEGER NOT NULL,
  ver      TEXT    NOT NULL,            -- the mod version that sent the batch, so you can compare before and after a patch
  batches  INTEGER NOT NULL DEFAULT 0,  -- how many batches contributed (more batches = more trustworthy numbers)
  pdmg     INTEGER NOT NULL DEFAULT 0,  -- damage the plugin dealt
  phits    INTEGER NOT NULL DEFAULT 0,
  wdmg     INTEGER NOT NULL DEFAULT 0,  -- damage the weapon dealt while the plugin was equipped
  whits    INTEGER NOT NULL DEFAULT 0,
  seconds  REAL    NOT NULL DEFAULT 0,  -- combat seconds with this plugin and weapon
  -- "Consistency": how much the plugin's Boost (plugin damage divided by weapon damage) differs from one batch to the next.
  -- Only batches with at least 5 combat seconds and some weapon damage count. nb = how many, sb = their Boost values added up,
  -- sb2 = those values squared and added up. Two plain sums per row, no per-player data.
  nb       INTEGER NOT NULL DEFAULT 0,
  sb       REAL    NOT NULL DEFAULT 0,
  sb2      REAL    NOT NULL DEFAULT 0,
  PRIMARY KEY (plugin, weapon, class, stage, cal, ver)
);