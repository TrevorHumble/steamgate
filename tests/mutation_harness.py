"""
mutation_harness.py — tamper gate for the bpy-free pure tests.

Deliberately breaks a known behavior in an add-on (a "mutant"), points the
matching pure test at the mutated copy, and asserts the test FAILS. A mutant
the test still passes is a SURVIVED mutant — a real hole where the gate is
blind to that break. A mutant whose pattern can't be applied is BROKEN — it
proves nothing. Each mutant also declares the SPECIFIC assertion that must catch
it; a failure from a DIFFERENT assertion is MIS-CAUGHT and does not count, so a
"catch" means the right guard fired. Exit 0 only if every mutant was caught by
its intended guard and none was survived, errored, mis-caught, or broken.

Pure Python, no Blender. Run from the repo root: python tests/mutation_harness.py
"""

import os
import subprocess
import sys
import tempfile

_HERE = os.path.dirname(os.path.abspath(__file__))
_ROOT = os.path.join(_HERE, '..')
_ADDONS = os.path.join(_ROOT, 'addons')

BEVEL = os.path.join(_ADDONS, 'bevel_bezier_corners.py')
PHYLLO = os.path.join(_ADDONS, 'phyllotaxis.py')
RUN_PURE = os.path.join(_HERE, 'run_pure.py')
RUN_PHYLLO = os.path.join(_HERE, 'run_phyllotaxis_pure.py')

# (name, test_script, addon_path, env_var, old_substring, new_substring,
#  pass_sentinel, expect_fail)
# Each old->new breaks a real behavior the named test must catch.
# pass_sentinel is the success line the test prints when it PASSES; its presence
# means the break went undetected (survived), regardless of return code.
# expect_fail is a distinctive substring of the SPECIFIC FAIL line this mutant
# must trip. A catch is credited only when that substring appears, so "caught"
# means the RIGHT guard fired — not merely that something failed. Each substring
# below was confirmed by running the mutant and reading the FAIL line it emits.
MUTANTS = [
    # h=0 collapses the arc handles to zero length -> handle position check trips.
    ("bevel-chamfer", RUN_PURE, BEVEL, 'BEVEL_ADDON_PATH',
     'h = (4.0/3.0) * math.tan(alpha / 4.0) * r_eff',
     'h = 0.0', 'PURE_PASS', 'handle_t1'),
    # Wrong arc constant -> handles wrong length/position -> handle check trips.
    ("bevel-wrong-arc-constant", RUN_PURE, BEVEL, 'BEVEL_ADDON_PATH',
     '(4.0/3.0) * math.tan(alpha / 4.0)',
     '(1.0) * math.tan(alpha / 4.0)', 'PURE_PASS', 'handle_t1'),
    # Setback sign/operator wrong -> tangent points land off -> t1 check trips.
    ("bevel-wrong-setback", RUN_PURE, BEVEL, 'BEVEL_ADDON_PATH',
     'd = radius / math.tan(theta / 2.0)',
     'd = radius * math.tan(theta / 2.0)', 'PURE_PASS', ': t1 expected'),
    # Hardcoded angle drifts from the exact golden angle -> step invariant trips.
    ("phyllotaxis-golden-angle-hardcode", RUN_PHYLLO, PHYLLO, 'PHYLLO_ADDON_PATH',
     'ga = math.radians(360.0 * (2.0 - (1.0 + math.sqrt(5.0)) / 2.0))',
     'ga = math.radians(137.5)', 'PHYLLOTAXIS_PURE_PASS', 'golden-angle step'),
    # Linear radius instead of sqrt(i) -> radius law trips.
    ("phyllotaxis-radius-linear", RUN_PHYLLO, PHYLLO, 'PHYLLO_ADDON_PATH',
     'r = scale * math.sqrt(i)',
     'r = scale * i', 'PHYLLOTAXIS_PURE_PASS', 'radius at'),
    # Flipped dome falloff sign -> outer rim rises instead of flattening.
    ("phyllotaxis-dome-sign", RUN_PHYLLO, PHYLLO, 'PHYLLO_ADDON_PATH',
     'dome * (1.0 - (r / r_max) ** 2)',
     'dome * (1.0 + (r / r_max) ** 2)', 'PHYLLOTAXIS_PURE_PASS', 'dome outermost'),
]


def _apply_mutant(name, test_script, addon_path, env_var, old, new, sentinel,
                  expect_fail):
    """Run one mutant. Return 'caught', 'mis-caught', 'survived', 'errored', or 'broken'.

    A catch is credited ONLY when the SPECIFIC assertion fired: non-zero exit,
    success sentinel absent, and a FAIL line containing this mutant's expect_fail
    substring present. A non-zero exit with a FAIL line but NOT the expected one
    is 'mis-caught' — the suite failed, but a different assertion caught it, so we
    can't claim the intended guard works. A non-zero exit with no FAIL line at all
    (crash, traceback, import error, bad path) is 'errored' — the test failed for
    the WRONG reason and proves nothing.
    """
    src = open(addon_path, encoding='utf-8').read()
    n = src.count(old)
    if n != 1:
        print(f"MUTANT_BROKEN: {name} (pattern matched {n} times)")
        return 'broken'

    mutated = src.replace(old, new)
    fd, path = tempfile.mkstemp(suffix='.py', prefix='mutant_')
    try:
        with os.fdopen(fd, 'w', encoding='utf-8') as f:
            f.write(mutated)
        env = dict(os.environ)
        env[env_var] = path
        proc = subprocess.run([sys.executable, test_script], env=env,
                              capture_output=True, text=True)
        out = proc.stdout or ''
        passed = sentinel in out
        fail_lines = [line for line in out.splitlines() if line.startswith('FAIL')]
        if proc.returncode == 0 or passed:
            return 'survived'
        if fail_lines:
            if any(expect_fail in line for line in fail_lines):
                return 'caught'
            print(f'MIS-CAUGHT: {name} (expected FAIL containing "{expect_fail}", '
                  f'got a different FAIL)')
            print(fail_lines[0])
            return 'mis-caught'
        tail = ((proc.stderr or '') + out).strip()[:300]
        print(f"ERRORED: {name} (test failed without an assertion — wrong reason)")
        print(tail)
        return 'errored'
    finally:
        os.remove(path)


def main():
    if not MUTANTS:
        print("HARNESS_ERROR: no mutants defined")
        sys.exit(1)

    caught = survived = errored = mis_caught = broken = 0
    total = len(MUTANTS)
    for mutant in MUTANTS:
        name = mutant[0]
        result = _apply_mutant(*mutant)
        if result == 'caught':
            caught += 1
            print(f"caught: {name}")
        elif result == 'survived':
            survived += 1
            print(f"SURVIVED: {name}")
        elif result == 'mis-caught':
            mis_caught += 1
        elif result == 'errored':
            errored += 1
        else:  # 'broken'
            broken += 1

    # Keep this exact phrase verbatim — DESIGN.md and docs reference it.
    print(f"guards caught {caught}/{total}")
    print(f"MUTATION_SCORE: caught={caught} survived={survived} "
          f"errored={errored} mis-caught={mis_caught} broken={broken} "
          f"score={caught}/{total}")
    # Exit 0 only if every mutant was legitimately caught by its intended guard.
    failures = survived + errored + mis_caught + broken
    sys.exit(0 if (caught == total and failures == 0) else 1)


if __name__ == "__main__":
    main()
