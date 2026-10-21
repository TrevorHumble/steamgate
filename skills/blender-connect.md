---
name: blender-connect
description: >-
  Thin wrapper — reach for this when you need to control a live Blender session:
  run `bpy` code, inspect the scene, take a viewport screenshot, or keyframe
  objects. Triggers: `execute_blender_code`, "modify the scene", "connect to
  Blender", "run this in Blender", any task that writes to the user's open
  `.blend` file.
---

# blender-connect (orchestrator wrapper)

Full skill: `C:\Users\thumb\.claude\skills\blender-mcp\SKILL.md` — read it. (Requires a local Blender RAG/MCP setup; skip if not using Blender. Path is the original author's machine — adapt to your own install.)

That skill is required reading before any live Blender control — load it.

## Before running any `execute_blender_code`

Confirm the API with blender-rag first (`skills/blender-rag.md`).
Then execute here. Confirm, then execute — that order is the whole point.
