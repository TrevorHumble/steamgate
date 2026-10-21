# retire-example: remove the bundled worked example (the two Blender add-ons + their
# tests/evals/preview) when you're ready to build your OWN project. CI auto-skips the
# example steps once addons/*.py is gone (see .github/workflows/ci.yml's hashFiles
# guards), so the build stays green. This only deletes the KNOWN example files; your
# own code is never touched.
#
# Safe by default: it shows what it WOULD remove and changes nothing unless you pass
# -Yes. The deletion is staged but NOT committed -- commit it through the normal
# review/commit flow (the orchestrator records the verdict) so even removing the
# example goes through the gate.
#
#   powershell -File tools/retire-example.ps1          # preview only
#   powershell -File tools/retire-example.ps1 -Yes     # actually remove + stage
param([switch]$Yes)

$top = (& git rev-parse --show-toplevel 2>$null)
if (-not $top) { Write-Host "retire-example: run inside the cloned git repo." -ForegroundColor Red; exit 1 }
Set-Location $top

# Safety guard: only act when the EXAMPLE is actually present (its signature file).
# This prevents deleting your own work if you've reused the dir names addons/tests/evals.
if (-not (Test-Path 'addons/bevel_bezier_corners.py')) {
  Write-Host "The bundled example isn't here (already retired, or you've replaced addons/ with your own code)." -ForegroundColor Green
  Write-Host "Not deleting anything. retire-example only removes the original worked example." -ForegroundColor Green
  exit 0
}

$targets = @('addons', 'tests', 'evals', 'tools/render_preview.py') | Where-Object { Test-Path $_ }
if (-not $targets) { Write-Host "Nothing to retire -- the example is already gone." -ForegroundColor Green; exit 0 }

Write-Host "The bundled example consists of:" -ForegroundColor Cyan
$targets | ForEach-Object { Write-Host "  - $_" }

if (-not $Yes) {
  Write-Host ""
  Write-Host "Preview only -- nothing changed. To remove and stage them, re-run with -Yes:" -ForegroundColor Yellow
  Write-Host "  powershell -File tools/retire-example.ps1 -Yes"
  exit 0
}

foreach ($t in $targets) { & git rm -r --quiet $t 2>$null; if (Test-Path $t) { Remove-Item $t -Recurse -Force } }
Write-Host ""
Write-Host "Example removed and staged. CI will auto-skip the example steps (it stays green)." -ForegroundColor Green
Write-Host "Next: commit it through the normal flow so the removal is reviewed and recorded, then"
Write-Host "build your own first feature ( /build <what you want> ) and add its own tests."
