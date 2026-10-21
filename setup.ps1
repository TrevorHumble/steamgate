# setup: run this ONCE after cloning, to turn on the enforcement that can't ride
# along in a clone. One command:
#   powershell -ExecutionPolicy Bypass -File setup.ps1
#
# It (1) activates the commit gate (a clone doesn't carry core.hooksPath, so
# without this the gate is silently OFF), (2) makes sure you have a git identity
# so your first commit doesn't error, and (3) shows you exactly which gates are on.
$ErrorActionPreference = 'Stop'

$top = (& git rev-parse --show-toplevel 2>$null)
if (-not $top) {
  Write-Host "setup: run this from inside the cloned git repo (could not find a git root)." -ForegroundColor Red
  exit 1
}
Set-Location $top

# 1. Activate the commit gate.
& git config core.hooksPath .githooks
# Re-assert the hook's execute bit. Matters only for POSIX checkouts/CI (Git on
# Windows ignores the bit and runs the hook via its bundled sh); harmless here.
& git update-index --chmod=+x .githooks/pre-commit 2>$null

# 2. Ensure a git identity (a fresh clone may have none -> the first commit errors
#    with "Author identity unknown"). Auto-fill from your GitHub account if we can.
$haveEmail = "$(& git config user.email)".Trim()
if (-not $haveEmail) {
  $ghExe = (Get-Command gh -ErrorAction SilentlyContinue).Source
  if (-not $ghExe -and (Test-Path 'C:\Program Files\GitHub CLI\gh.exe')) { $ghExe = 'C:\Program Files\GitHub CLI\gh.exe' }
  $set = $false
  if ($ghExe) {
    $login = "$(& $ghExe api user --jq '.login' 2>$null)".Trim()
    $uid   = "$(& $ghExe api user --jq '.id' 2>$null)".Trim()
    if ($login -and $uid) {
      & git config user.name $login
      & git config user.email "$uid+$login@users.noreply.github.com"
      Write-Host "Set your git identity from GitHub ($login)."
      $set = $true
    }
  }
  if (-not $set) {
    Write-Host "Heads-up: set your git identity once before your first commit:" -ForegroundColor Yellow
    Write-Host '  git config --global user.name "Your Name"'
    Write-Host '  git config --global user.email "you@example.com"'
  }
}

# 3. Show the honest gate status (commit gate active now; goal/loop gates wired,
#    they load when you open the folder in Claude Code).
Write-Host ""
& powershell -NoProfile -File tools/check-enforcement.ps1
$gateStatus = $LASTEXITCODE

Write-Host ""
Write-Host "Next:"
Write-Host "  1. Open CLAUDE.md and fill in your North Star (one sentence: who it's for + what it builds)."
Write-Host "     Then tell the orchestrator to carry that North Star into DESIGN.md and PLAN.md as its first task."
Write-Host "  2. Open this folder in Claude Code and accept the trust prompt (this loads the goal & loop gates)."
Write-Host "     To start a build, type:  /build <what you want, in plain words>"
Write-Host "  3. The orchestrator drives issue -> review -> implement -> review -> commit; you direct and confirm."
Write-Host ""
if ($gateStatus -ne 0) {
  Write-Host "One or more gates are not active yet (see above)." -ForegroundColor Yellow
  exit 1
}
