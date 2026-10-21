# pipeline-audit: a plain-language health report on the DISCRETIONARY review pipeline
# -- the parts the commit gate does NOT mechanically force (how many reviewers ran,
# whether the doc-currency gate is wired, the architectural-audit cadence). The
# mechanical gates have their own signal (tools/check-enforcement.ps1); THIS is the
# honest picture of what's still left to the orchestrator's discipline, so the owner
# isn't trusting a green light that only proves the mechanical half. Read-only.
$top = (& git rev-parse --show-toplevel 2>$null)
if (-not $top) { Write-Error 'pipeline-audit: not inside a git repo'; exit 1 }
Set-Location $top

Write-Host "Pipeline audit -- the discretionary layer the commit gate does not force:" -ForegroundColor Cyan

# 1. Reviewer counts over time (from review_verdict.ps1's history log).
$hist = '.review_state/verdict-history.jsonl'
if (Test-Path $hist) {
  $rows  = Get-Content $hist | ForEach-Object { try { $_ | ConvertFrom-Json } catch {} } | Where-Object { $_ }
  $total = @($rows).Count
  $thin  = @($rows | Where-Object { @($_.reviewers).Count -lt 2 }).Count
  $c = if ($thin -gt 0) { 'Yellow' } else { 'Green' }
  Write-Host ("  Verdicts recorded: {0}; with fewer than 2 reviewers: {1}" -f $total, $thin) -ForegroundColor $c
} else {
  Write-Host "  Verdicts recorded: none yet (history builds as you commit reviewed work)."
}

# 2. Architectural-audit cadence (orchestrator runs one every 5th committed entry).
if (Test-Path 'BUILDLOG.md') {
  $entries = Get-Content 'BUILDLOG.md' | Where-Object { $_ -match '^\s*-\s' }
  $since = 0
  foreach ($l in ($entries | Select-Object -Last 25)) { if ($l -match '\[AUDIT\]') { break }; $since++ }
  $c = if ($since -ge 5) { 'Yellow' } else { 'Green' }
  $due = if ($since -ge 5) { ' -- an architectural audit is DUE' } else { '' }
  Write-Host ("  Committed entries since last [AUDIT]: {0}{1}" -f $since, $due) -ForegroundColor $c
}

# 3. Doc-currency gate wiring (the agent exists but may not be spawned).
$buildCmd = if (Test-Path '.claude/commands/build.md') { Get-Content '.claude/commands/build.md' -Raw } else { '' }
if ($buildCmd -match 'reviewer-doc-currency') {
  Write-Host "  Doc-currency gate wired into /build: yes" -ForegroundColor Green
} else {
  Write-Host "  Doc-currency gate wired into /build: NO (agent exists, not yet spawned -- issue #13)" -ForegroundColor Yellow
}

# 4. Commit gate active (mechanical -- cross-check the floor is on).
$hp = "$(& git config --get core.hooksPath)".Trim()
if ($hp -eq '.githooks') { Write-Host "  Commit gate active: yes" -ForegroundColor Green }
else { Write-Host "  Commit gate active: NO -- run setup.ps1" -ForegroundColor Red }

Write-Host ""
Write-Host "Honest picture: the commit gate forces a recorded verdict for every committed change,"
Write-Host "but the DEPTH of review (reviewer count, the gates above) is the orchestrator's discipline"
Write-Host "until the program-driven loop (issue #11) makes it mechanical too."
