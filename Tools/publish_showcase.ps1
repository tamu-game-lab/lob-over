# WO-S1: run explicitly after the public Pages repository has been created.
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$showcaseRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot 'Showcase'))
$publishRoot = [IO.Path]::GetFullPath((Join-Path $showcaseRoot '.publish'))
$distRoot = [IO.Path]::GetFullPath((Join-Path $showcaseRoot 'dist'))
$remote = 'https://github.com/tamu-game-lab/lob-over-showcase.git'
function Assert-Exit([string] $message) { if ($LASTEXITCODE -ne 0) { throw $message } }

& npm.cmd --prefix (Join-Path $repoRoot 'Monitor') run showcase
Assert-Exit 'Showcase build failed.'
& git ls-remote $remote | Out-Null
Assert-Exit "The public repository $remote does not exist or cannot be reached. Create it before publishing."
if (-not (Test-Path -LiteralPath (Join-Path $publishRoot '.git'))) {
    if (Test-Path -LiteralPath $publishRoot) { throw 'Showcase/.publish exists without a Git checkout; move it aside before publishing.' }
    & git clone $remote $publishRoot
    Assert-Exit 'Could not clone the showcase repository.'
} else {
    $actualRemote = & git -C $publishRoot remote get-url origin
    Assert-Exit 'Could not read the publish remote.'
    if ($actualRemote -ne $remote) { throw 'Showcase/.publish points to a different repository.' }
    $dirty = & git -C $publishRoot status --porcelain
    Assert-Exit 'Could not inspect the publish checkout.'
    if ($dirty) { throw 'Showcase/.publish has uncommitted changes; preserve them before publishing.' }
    & git -C $publishRoot fetch origin
    Assert-Exit 'Could not update the showcase repository.'
}
& git -C $publishRoot show-ref --verify --quiet refs/remotes/origin/main
if ($LASTEXITCODE -eq 0) {
    & git -C $publishRoot checkout main
    Assert-Exit 'Could not select the publish main branch.'
    & git -C $publishRoot merge --ff-only origin/main
    Assert-Exit 'The publish checkout cannot fast-forward to origin/main.'
} else {
    & git -C $publishRoot checkout -B main
    Assert-Exit 'Could not initialize the publish main branch.'
}
# Verify every absolute deletion target before replacing generated publish contents.
if ($publishRoot -ne [IO.Path]::GetFullPath((Join-Path $repoRoot 'Showcase/.publish'))) { throw 'Unexpected publish directory.' }
foreach ($entry in Get-ChildItem -LiteralPath $publishRoot -Force) {
    if ($entry.Name -eq '.git') { continue }
    $target = [IO.Path]::GetFullPath($entry.FullName)
    if (-not $target.StartsWith($publishRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw "Unsafe publish target: $target" }
    if ($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Publish checkout contains a link: $target" }
    Remove-Item -LiteralPath $target -Recurse -Force
}
foreach ($entry in Get-ChildItem -LiteralPath $distRoot -Force) { Copy-Item -LiteralPath $entry.FullName -Destination $publishRoot -Recurse -Force }
$sourceHash = & git -C $repoRoot rev-parse --short HEAD
Assert-Exit 'Could not read the source revision.'
& git -C $publishRoot add --all
Assert-Exit 'Could not stage the built showcase.'
& git -C $publishRoot diff --cached --quiet
if ($LASTEXITCODE -eq 1) {
    & git -C $publishRoot commit -m "Showcase $(Get-Date -Format yyyy-MM-dd) $sourceHash"
    Assert-Exit 'Could not commit the built showcase.'
} elseif ($LASTEXITCODE -ne 0) { throw 'Could not inspect the staged showcase.' }
& git -C $publishRoot push origin main
Assert-Exit 'Could not push the showcase main branch.'
Write-Output 'https://tamu-game-lab.github.io/lob-over-showcase/'
