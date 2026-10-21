---
name: visual-confirm
description: >
  The human visual-confirmation gate. Use after a committed change to an add-on
  alters its visible geometry — it renders the add-on's output to an image so
  the owner can SEE the result and confirm it is what they meant. Triggers: "render a
  preview", "let the owner see it", "visual gate", "does this look right", any
  add-on change where the math passed but the look is unconfirmed.
---

# Visual-confirmation gate

This is the sanctioned final-eye checkpoint. The deterministic geometry gates
(`tests/run_headless.py`, the evals) prove the math is correct. They cannot
prove the result is what the owner *meant* — they are the final eye on the visual
result, and they cannot read code. This gate closes that gap: it renders the
add-on's output to a picture they can look at and say yes / no to.

It is the **human** gate, distinct from the deterministic gates. It never
blocks a merge on its own — a render hiccup is not a correctness failure. It
produces a picture and a plain-language question; the owner's answer is the signal.

## When it fires

A committed change to an add-on that alters visible geometry. That means:

- a change under `addons/<name>.py` that changes what the output looks like
  (new/changed operator math, default parameters, the shape it builds), and
- the deterministic gates already passed (this gate runs *after* them, on the
  thing they certified as mathematically correct).

It does NOT fire for changes that cannot move a pixel — comments, docstrings,
renames, test-only or skill-only edits.

## What it does

Run the preview renderer for the affected add-on:

```powershell
blender --background --python tools/render_preview.py -- <addon_name>
```

`<addon_name>` is `bevel_bezier_corners` or `phyllotaxis`. The script builds a
representative example of that add-on's output in a clean scene, auto-frames a
top-down orthographic camera over it, lights it, and renders an 800x800 PNG to
`renders/<addon_name>.png`. Success prints `RENDER_OK <path>`; any failure
prints `RENDER_FAIL: <reason>` and exits non-zero.

In CI the same two renders run after the eval step and upload as a workflow
artifact (`addon-previews`), so the image is downloadable from any run. The CI
render step is `continue-on-error` — it is a preview artifact, not a
correctness gate, and a render hiccup must not red the build.

## How the result is surfaced to the owner

Show them the image with a plain-language, non-developer question. No jargon, no
code. Use this shape:

> Here's what **<add-on>** now produces — does this look right? Is it what you
> meant?
> [the image]
> **[confirm]** / **[it's wrong]**

If they say **confirm**, the visible result is accepted; note it and move on.
If they say **it's wrong**, that is a real defect even though every math gate
passed — capture it as an issue and route it through the normal pipeline. Their
eye is the authority here; do not argue the geometry is "correct" at them.

## Scope (v1)

This is the render + surface mechanism. Auto-wiring it into the full pipeline
(firing it automatically on the right commits, posting the image to the right
place without being asked) is a deferred follow-up — for now, run it when the
firing condition above is met and surface the image by hand.
