# Copies the LOB-Game art, animation and audio this project reuses into Assets\LOBGame\, with their .meta files
# (GUIDs stay valid, so prefab references keep working). Read-only on the source. About 1.7 GB. Re-run any time.
# Assets\LOBGame\ is gitignored; this script is the source of truth. Run from the repo root in PowerShell.
$ErrorActionPreference = 'Stop'
$src = Join-Path (Split-Path $PSScriptRoot -Parent) '..\LOB-Game\Assets' | Resolve-Path
$dst = Join-Path (Split-Path $PSScriptRoot -Parent) 'Assets\LOBGame'
$folders = @(
  '_Project\Art', '_Project\Animation',
  '3D_Models\Execavator', '3D_Models\Mobile Crane', '3D_Models\Full_Set', '3D_Models\pipe',
  '3D_Models\constructionWorkers_Anims_01', '3D_Models\constructionWorkers_Anims_02', '3D_Models\constructionWorkers_Anims_03',
  '3D_Models\Workers', '3D_Models\a-pile-of-sand', '3D_Models\Construction Assets',
  '3D_Models\trafic-cone', '3D_Models\water-barriers', '3D_Models\road-barrier-collection',
  '3D_Models\Tree', '3D_Models\sophiaImports', '3D_Models\traffic-props', '3D_Models\construction-site-barrier',
  'Audio\Construction', 'PM'
)
$files = @('Resources\SiteAudioBank.asset', 'Resources\SiteAudioBank.asset.meta')
New-Item -ItemType Directory -Force -Path $dst | Out-Null
foreach ($f in $folders) {
  $from = Join-Path $src $f; $to = Join-Path $dst $f
  if (-not (Test-Path $from)) { Write-Warning "missing in LOB-Game: $f"; continue }
  robocopy $from $to /E /NFL /NDL /NJH /NJS /NP /XF *.zip | Out-Null
  $meta = "$from.meta"; if (Test-Path $meta) { Copy-Item $meta "$to.meta" -Force }
  Write-Host "copied $f"
}
foreach ($f in $files) { $from = Join-Path $src $f; if (Test-Path $from) { $to = Join-Path $dst $f; New-Item -ItemType Directory -Force -Path (Split-Path $to) | Out-Null; Copy-Item $from $to -Force } }
# Parent folder .meta files so Unity keeps the same folder GUIDs
foreach ($p in @('_Project', '3D_Models', 'Audio', 'Resources')) { $m = Join-Path $src "$p.meta"; if (Test-Path $m) { Copy-Item $m (Join-Path $dst "$p.meta") -Force } }
Write-Host "done: $dst"
