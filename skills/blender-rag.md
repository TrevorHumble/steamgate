---
name: blender-rag
description: >-
  Thin wrapper — reach for this when you need to confirm a Blender 5.1 API
  signature, operator name, node socket, or version-specific behavior before
  writing or running code. Triggers: `search_blender_docs`, "confirm the API",
  "check the bpy docs", "is this right in 5.1", any Blender Python task where
  you are about to assume a 4.x API still applies.
---

# blender-rag (orchestrator wrapper)

Full skill: `C:\Users\thumb\.claude\skills\blender-rag\SKILL.md` — read it.
RAG corpus: `C:\Users\thumb\BlenderRag`
MCP tool: `search_blender_docs`

> Requires a local Blender RAG/MCP setup; skip if not using Blender. The paths above are the original author's machine — adapt them to your own install.

## The one rule that matters here

**Confirm the API via the RAG, then execute via blender-connect.**

Querying and `source_type` guidance: see the full skill at the path above.
