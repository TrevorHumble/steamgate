# run_tests.ps1 — local test runner for the Blender add-ons.
# Runs the three bpy-free pure tests with plain Python (run_pure,
# run_phyllotaxis_pure, mutation_harness), then the Blender background-mode
# gates (run_headless, run_phyllotaxis_headless, and the eval suite).
# Mirrors the CI gate order. Exits 1 if any process fails.

$blender = if ($env:BLENDER_EXE) { $env:BLENDER_EXE } else { 'C:\Program Files\Blender Foundation\Blender 5.1\blender.exe' }
$root = (Resolve-Path "$PSScriptRoot\..")

if (-not (Test-Path $blender)) {
    Write-Host "TESTS_FAILED: blender not found at $blender"
    exit 1
}

$python = (Get-Command python -ErrorAction SilentlyContinue).Source
if (-not $python) {
    Write-Host "TESTS_FAILED: python not found"
    exit 1
}

Write-Host "=== Lint + security (ruff/bandit) ==="
# Pure-Python, no Blender — fast, so it runs first. Mirrors the CI 'lint' job:
# ruff F,B (Pyflakes + bugbear) and bandit, both scoped to addons/.
# Guard: if ruff/bandit aren't installed, SKIP with a notice (lintcode stays 0)
# rather than fail the whole run over a missing dev tool — matches how the
# runner treats optional tooling. CI is the authoritative lint gate.
$lintcode = 0
& $python -c "import ruff, bandit" 2>$null
if ($LASTEXITCODE -ne 0) {
    Write-Host "LINT_SKIPPED: ruff/bandit not installed (pip install ruff bandit) — CI is the authoritative gate"
} else {
    try {
        & $python -m ruff check --select F,B "$root\addons"
        $ruffcode = $LASTEXITCODE
    } catch {
        Write-Host "TESTS_FAILED: ruff threw: $_"
        $ruffcode = 1
    }
    try {
        & $python -m bandit -r "$root\addons"
        $banditcode = $LASTEXITCODE
    } catch {
        Write-Host "TESTS_FAILED: bandit threw: $_"
        $banditcode = 1
    }
    if ($ruffcode -ne 0 -or $banditcode -ne 0) { $lintcode = 1 }
}

Write-Host "=== Pure geometry test ==="
try {
    & $python "$root\tests\run_pure.py"
    $purecode = $LASTEXITCODE
} catch {
    Write-Host "TESTS_FAILED: run_pure.py threw: $_"
    $purecode = 1
}

Write-Host "=== Pure phyllotaxis test ==="
try {
    & $python "$root\tests\run_phyllotaxis_pure.py"
    $phyllocode = $LASTEXITCODE
} catch {
    Write-Host "TESTS_FAILED: run_phyllotaxis_pure.py threw: $_"
    $phyllocode = 1
}

Write-Host "=== Mutation/tamper gate ==="
try {
    & $python "$root\tests\mutation_harness.py"
    $mutationcode = $LASTEXITCODE
} catch {
    Write-Host "TESTS_FAILED: mutation_harness.py threw: $_"
    $mutationcode = 1
}

Write-Host "=== Headless operator test ==="
# --python-exit-code 1: Blender background mode exits 0 even on an unhandled
# Python exception by default; this forces exit 1 if run_headless.py raises
# (e.g. the add-on fails to load or register), so a broken test cannot false-pass.
try {
    & $blender --background --python-exit-code 1 --python "$root\tests\run_headless.py"
    $headlesscode = $LASTEXITCODE
} catch {
    Write-Host "TESTS_FAILED: run_headless.py threw: $_"
    $headlesscode = 1
}

Write-Host "=== Phyllotaxis headless operator test ==="
# --python-exit-code 1: forces exit 1 if run_phyllotaxis_headless.py raises
# (e.g. the add-on fails to load or register), so a broken test cannot false-pass.
try {
    & $blender --background --python-exit-code 1 --python "$root\tests\run_phyllotaxis_headless.py"
    $phylloheadlesscode = $LASTEXITCODE
} catch {
    Write-Host "TESTS_FAILED: run_phyllotaxis_headless.py threw: $_"
    $phylloheadlesscode = 1
}

Write-Host "=== Eval suite ==="
# --python-exit-code 1: forces exit 1 if run_evals.py raises (e.g. an add-on
# fails to load or register), so a broken eval cannot false-pass.
try {
    & $blender --background --python-exit-code 1 --python "$root\evals\run_evals.py"
    $evalcode = $LASTEXITCODE
} catch {
    Write-Host "TESTS_FAILED: run_evals.py threw: $_"
    $evalcode = 1
}

if ($lintcode -ne 0 -or $purecode -ne 0 -or $phyllocode -ne 0 -or $mutationcode -ne 0 -or $headlesscode -ne 0 -or $phylloheadlesscode -ne 0 -or $evalcode -ne 0) {
    Write-Host "TESTS_FAILED (lint=$lintcode, run_pure.py=$purecode, run_phyllotaxis_pure.py=$phyllocode, mutation_harness.py=$mutationcode, run_headless.py=$headlesscode, run_phyllotaxis_headless.py=$phylloheadlesscode, run_evals.py=$evalcode)"
    exit 1
}

Write-Host "ALL_TESTS_PASS"
exit 0
