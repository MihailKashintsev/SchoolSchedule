# ============================================================
#  release.ps1  --  Skript sozdaniya reliza SchoolSchedule
#  Ispolzovanie: powershell -ExecutionPolicy Bypass -File release.ps1
# ============================================================

param()

$Host.UI.RawUI.WindowTitle = "SchoolSchedule - Release Builder"

function Write-Header($text) {
    Write-Host "`n==========================================" -ForegroundColor Cyan
    Write-Host "  $text" -ForegroundColor Cyan
    Write-Host "==========================================`n" -ForegroundColor Cyan
}
function Write-Step($text)    { Write-Host "  >> $text" -ForegroundColor Yellow }
function Write-Ok($text)      { Write-Host "  OK $text" -ForegroundColor Green }
function Write-Err($text)     { Write-Host "  !! $text" -ForegroundColor Red }

Write-Header "SchoolSchedule Release Builder"

# -- 1. Check repo root --
if (-not (Test-Path "SchoolSchedule.sln")) {
    Write-Err "Run this script from the repository root (next to SchoolSchedule.sln)"
    Read-Host "Press Enter to exit"
    exit 1
}

# -- 2. Ask version --
Write-Host "  Enter release version (example: 1.0.0 or 1.2.3):" -ForegroundColor White
$version = Read-Host "  Version"
$version = $version.Trim().TrimStart('v')

if ($version -notmatch '^\d+\.\d+\.\d+') {
    Write-Err "Invalid version format. Use X.Y.Z"
    Read-Host "Press Enter to exit"
    exit 1
}

$tag = "v$version"
Write-Ok "Tag: $tag"

# -- 3. Git status check --
Write-Step "Checking git status..."
$gitStatus = git status --porcelain 2>&1
if ($gitStatus) {
    Write-Host ""
    Write-Host "  Uncommitted changes found:" -ForegroundColor Yellow
    $gitStatus | ForEach-Object { Write-Host "    $_" -ForegroundColor DarkYellow }
    Write-Host ""
    $commit = Read-Host "  Commit all changes with message 'Release $tag'? (y/n)"
    if ($commit -eq 'y') {
        git add -A
        git commit -m "Release $tag"
        Write-Ok "Changes committed"
    } else {
        Write-Err "Please commit your changes manually and re-run the script"
        Read-Host "Press Enter to exit"
        exit 1
    }
}

# -- 4. Check if tag exists --
$existingTag = git tag -l $tag 2>&1
if ($existingTag -eq $tag) {
    Write-Host "  Tag $tag already exists. Delete it? (y/n)" -ForegroundColor Yellow
    $del = Read-Host "  "
    if ($del -eq 'y') {
        git tag -d $tag
        git push origin --delete $tag 2>$null
        Write-Ok "Tag deleted"
    } else {
        exit 1
    }
}

# -- 5. Create tag --
Write-Step "Creating tag $tag..."
git tag -a $tag -m "Release $tag"
if ($LASTEXITCODE -ne 0) { Write-Err "Failed to create tag"; exit 1 }
Write-Ok "Tag created"

# -- 6. Push --
Write-Step "Pushing to GitHub..."
git push origin master 2>&1 | Out-Null
git push origin $tag
if ($LASTEXITCODE -ne 0) {
    Write-Err "Push failed. Check your GitHub connection"
    Read-Host "Press Enter to exit"
    exit 1
}
Write-Ok "Tag and commits pushed to GitHub"

# -- 7. Done --
Write-Header "Done!"
Write-Host "  Tag: $tag" -ForegroundColor White
Write-Host "  GitHub Actions will build and publish the release automatically." -ForegroundColor White
Write-Host ""
Write-Host "  Track build progress:" -ForegroundColor DarkGray
Write-Host "  https://github.com/MihailKashintsev/SchoolSchedule/actions" -ForegroundColor Cyan
Write-Host ""
Write-Host "  Release will appear at:" -ForegroundColor DarkGray
Write-Host "  https://github.com/MihailKashintsev/SchoolSchedule/releases/tag/$tag" -ForegroundColor Cyan
Write-Host ""
Read-Host "  Press Enter to close"
