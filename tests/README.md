# Tests

Dependency-free headless test harness for the Blender add-ons. No pytest, no pip install required. In-license: runs inside the free Blender 5.x build used by CI.

## Gates

Six entry points run in CI, in this order. The first three are bpy-free pure Python; the rest need Blender.

| Entry point | Runtime | What it verifies |
|---|---|---|
| `run_pure.py` | Plain Python (no Blender) | Bevel geometry: tangent points, handle placement, circular-arc midpoint |
| `run_phyllotaxis_pure.py` | Plain Python (no Blender) | Phyllotaxis math: golden angle, radius law, dome profile |
| `mutation_harness.py` | Plain Python (no Blender) | The tamper gate. Breaks both add-ons and proves the pure tests catch each break; emits a `guards caught N/total` and `MUTATION_SCORE:` line |
| `run_headless.py` | `blender --background --python` | Bevel operator produces the expected vertex positions under Blender |
| `run_phyllotaxis_headless.py` | `blender --background --python` | Phyllotaxis operator builds the expected mesh under Blender |
| `../evals/run_evals.py` | `blender --background --python` | Geometry eval suite |

`run_tests.ps1` (PowerShell) orchestrates all six and exits 1 if any sub-process exits non-zero. When Blender is not installed locally it stops at the pure-Python gates; CI always runs all six.

## Running

```powershell
# Run all gates (PowerShell)
tests/run_tests.ps1

# Override Blender path
$env:BLENDER_EXE = "C:\path\to\blender.exe"; tests/run_tests.ps1
```

```
# Run a Blender gate directly
blender --background --python tests/run_headless.py
```

## License

All tests are in-license: no external service, no non-Anthropic key, no hosted account required. Blender is free and open-source.
