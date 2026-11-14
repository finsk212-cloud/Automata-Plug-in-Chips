-- One row per (plugin, weapon, damage class, progress stage, Calamity yes/no). Every batch a player sends is ADDED to the matching row.
-- There is no player, install, world or IP column on purpose: nothing in here can be traced back to anyone.
CREATE TABLE IF NOT EXISTS stats (
  plugin   TEXT    NOT NULL,
  weapon   TEXT    NOT NULL,
  class    INTEGER NOT NULL,
  stage    INTEGER NOT NULL,
  cal      INTEGER NOT NULL,
  batches  INTEGER NOT NULL DEFAULT 0, -- how many batches contributed (more batches = more trustworthy numbers)
  pdmg     INTEGER NOT NULL DEFAULT 0, -- damage the plugin dealt
  phits    INTEGER NOT NULL DEFAULT 0,
  wdmg     INTEGER NOT NULL DEFAULT 0, -- damage the weapon dealt while the plugin was equipped
  whits    INTEGER NOT NULL DEFAULT 0,
  seconds  REAL    NOT NULL DEFAULT 0, -- combat seconds with this plugin and weapon
  PRIMARY KEY (plugin, weapon, class, stage, cal)
);
