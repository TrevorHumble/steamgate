"""
run_evals.py — deterministic geometry eval harness for bevel_bezier_corners.

Run under: blender --background --python evals/run_evals.py

Registers the add-on, iterates EVAL_CASES, runs each case's build callable,
applies the operator, runs each check, and prints a per-case PASS/FAIL line
plus a final EVAL SCORE: line.  Exits 1 if any case fails, 0 otherwise.
"""

import sys
import os
import runpy
import math

import bpy

# Make cases.py importable without installing a package.
sys.path.insert(0, os.path.dirname(__file__))

from cases import EVAL_CASES

# ---------------------------------------------------------------------------
# Register the add-on.
# ---------------------------------------------------------------------------

_ADDON_PATH = os.path.join(os.path.dirname(__file__), '..', 'addons', 'bevel_bezier_corners.py')
_addon_globals = runpy.run_path(_ADDON_PATH)
_addon_globals['register']()


# ---------------------------------------------------------------------------
# Eval loop
# ---------------------------------------------------------------------------

def _select_activate(obj):
    """Deselect all, then select and activate obj so the operator's poll passes."""
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def _run_case(case):
    """
    Build, operate, check one case.  Returns (all_passed: bool, detail: str).
    """
    radius = case.get('radius', 0.4)

    obj = case['build']()
    _select_activate(obj)

    result = bpy.ops.curve.bevel_bezier_corners(radius=radius)
    if 'FINISHED' not in result:
        return False, f"operator returned {result}, expected FINISHED"

    failures = []
    for check in case['checks']:
        passed, msg = check(obj)
        if not passed:
            failures.append(msg)

    if failures:
        return False, "; ".join(failures)
    return True, "all checks passed"


def main():
    passed_count = 0
    total = len(EVAL_CASES)

    for case in EVAL_CASES:
        name = case['name']
        try:
            ok, detail = _run_case(case)
        except Exception as exc:
            ok = False
            detail = f"exception: {exc}"

        tag = "[PASS]" if ok else "[FAIL]"
        print(f"{tag} {name}: {detail}")
        if ok:
            passed_count += 1

    print(f"EVAL SCORE: {passed_count}/{total}")

    if passed_count < total:
        sys.exit(1)
    sys.exit(0)


if __name__ == "__main__":
    main()
