# Publishes the built showcase (Showcase/dist) to the gh-pages branch of THIS repository's origin
# (https://github.com/tamu-game-lab/lob-over). GitHub Pages serves that branch at
# https://tamu-game-lab.github.io/lob-over/ once Pages is set to "Deploy from a branch: gh-pages, / (root)".
# One repository: source on main, built site on gh-pages. Re-run after any change; safe to run repeatedly.
# Run from anywhere: powershell -ExecutionPolicy Bypass -File Tools\publish_showcase.ps1
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$distRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot 'Showcase\dist'))
$wt = [IO.Path]::GetFullPath((Join-Path $repoRoot 'Showcase\.publish'))
$pagesUrl = 'https://tamu-game-lab.github.io/lob-over/'
function Assert-Exit([string] $message) { if ($LASTEXITCODE -ne 0) { throw $message } }

# 1. Build the static site.
& node (Join-Path $repoRoot 'Showcase/build.mjs')
Assert-Exit 'Showcase build failed.'
if (-not (Test-Path -LiteralPath (Join-Path $distRoot 'index.html'))) { throw "No build at $distRoot" }

# 2. The remote is this repository's origin.
$remote = & git -C $repoRoot remote get-url origin
Assert-Exit 'This repository has no origin. Run: git remote add origin https://github.com/tamu-game-lab/lob-over.git'
& git -C $repoRoot fetch origin --prune
Assert-Exit "Could not fetch $remote"

# 3. A worktree for the gh-pages branch (created as an orphan the first time).
& git -C $repoRoot show-ref --verify --quiet refs/remotes/origin/gh-pages
$remoteHasBranch = ($LASTEXITCODE -eq 0)
if (Test-Path -LiteralPath (Join-Path $wt '.git')) {
    $branch = & git -C $wt branch --show-current
    if ($branch -ne 'gh-pages') { throw 'Showcase/.publish is a worktree but not on gh-pages; remove it with: git worktree remove Showcase/.publish --force' }
    if ($remoteHasBranch) { & git -C $wt merge --ff-only origin/gh-pages; Assert-Exit 'Showcase/.publish cannot fast-forward to origin/gh-pages; remove it and re-run.' }
} elseif (Test-Path -LiteralPath $wt) {
    throw 'Showcase/.publish exists and is not a git worktree; move it aside and re-run.'
} elseif ($remoteHasBranch) {
    & git -C $repoRoot worktree add -B gh-pages $wt origin/gh-pages
    Assert-Exit 'Could not create the gh-pages worktree.'
} else {
    & git -C $repoRoot worktree add --detach $wt
    Assert-Exit 'Could not create the publish worktree.'
    & git -C $wt checkout --orphan gh-pages
    Assert-Exit 'Could not create the orphan gh-pages branch.'
    & git -C $wt rm -r -f -q . 2>$null
}

# 4. Replace the worktree's contents with the build (keep .git).
if ($wt -ne [IO.Path]::GetFullPath((Join-Path $repoRoot 'Showcase\.publish'))) { throw 'Unexpected publish directory.' }
foreach ($entry in Get-ChildItem -LiteralPath $wt -Force) {
    if ($entry.Name -eq '.git') { continue }
    $target = [IO.Path]::GetFullPath($entry.FullName)
    if (-not $target.StartsWith($wt + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw "Unsafe publish target: $target" }
    if ($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Publish checkout contains a link: $target" }
    Remove-Item -LiteralPath $target -Recurse -Force
}
foreach ($entry in Get-ChildItem -LiteralPath $distRoot -Force) { Copy-Item -LiteralPath $entry.FullName -Destination $wt -Recurse -Force }
if (-not (Test-Path -LiteralPath (Join-Path $wt '.nojekyll'))) { New-Item -ItemType File -Path (Join-Path $wt '.nojekyll') | Out-Null }

# 5. Commit and push.
$sourceHash = & git -C $repoRoot rev-parse --short HEAD
Assert-Exit 'Could not read the source revision.'
& git -C $wt add --all
Assert-Exit 'Could not stage the built showcase.'
& git -C $wt diff --cached --quiet
if ($LASTEXITCODE -eq 1) {
    & git -C $wt commit -q -m "Showcase $(Get-Date -Format yyyy-MM-dd) from $sourceHash"
    Assert-Exit 'Could not commit the built showcase.'
} elseif ($LASTEXITCODE -ne 0) { throw 'Could not inspect the staged showcase.' }
& git -C $wt push -u origin gh-pages
Assert-Exit 'Could not push gh-pages.'
Write-Output "Published. Pages serves it at $pagesUrl (first time: Settings > Pages > Deploy from a branch > gh-pages > / (root); allow a minute)."
