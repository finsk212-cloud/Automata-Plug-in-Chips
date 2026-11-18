# Rebuilds server/src/plugins.json from the mod's plugin files (id, display name, rarity, class), so the stats report can show
# names and compare plugins within their rarity. Run it from the repository root after adding or renaming a plugin:
#   powershell -File server\tools\make-plugins.ps1
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$map = [ordered]@{}
foreach ($file in Get-ChildItem (Join-Path $root 'AugmentTypes') -Filter '*.cs' | Sort-Object Name) {
  $text = Get-Content $file.FullName -Raw
  if ($text -notmatch 'class\s+\w+\s*:\s*Augment\b') { continue }
  $id    = [regex]::Match($text, 'string\s+Id\s*=>\s*"([^"]+)"').Groups[1].Value
  if (-not $id) { $id = [regex]::Match($text, 'const\s+string\s+ChipId\s*=\s*"([^"]+)"').Groups[1].Value } # Pod upgrades and a few others keep the id in a constant
  $name  = [regex]::Match($text, 'string\s+DisplayName\s*=>\s*"([^"]+)"').Groups[1].Value
  $rar   = [regex]::Match($text, 'AugmentRarity\s+Rarity\s*=>\s*AugmentRarity\.(\w+)').Groups[1].Value
  $class = [regex]::Match($text, 'AugmentClass\s+Class\s*=>\s*AugmentClass\.(\w+)').Groups[1].Value
  if (-not $id) { Write-Warning "no literal Id in $($file.Name), skipped"; continue }
  $map[$id] = [ordered]@{ n = $name; r = $rar; c = $class }
}
$json = $map | ConvertTo-Json -Depth 4
[IO.File]::WriteAllText((Join-Path $root 'server\src\plugins.json'), $json, (New-Object Text.UTF8Encoding($false)))
Write-Host ("Wrote " + $map.Count + " plugins.")