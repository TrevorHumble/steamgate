---
name: research-prior-art
description: >
  How to run a quick prior-art and topic check before building something new. Use when
  asked to "check prior art", "research whether X exists", "look up how X is done",
  or "before I write this, what already exists" — and when the output is a short
  findings doc the caller acts on.
---

# Running a prior-art check

Delegate to `agents/researcher.md`. Do not improvise the research inline.

## What to pass to the researcher

- The topic or question (one sentence; do not pad).
- Any known local paths to check (skills, agents, standards, issues directories).
- Whether Blender-specific RAG (`search_blender_docs`) is relevant.
- The output path for the findings doc.

## Search order (researcher must follow this)

1. Local repo (`skills/`, `agents/`, `standards/`, `issues/`, `references/`) — glob and grep first.
2. Blender RAG at `C:\Users\thumb\BlenderRag` via `search_blender_docs` (if Blender-relevant; requires a local Blender RAG/MCP setup, skip if not using Blender — path is the original author's machine, adapt to your own install).
3. Web search — only after local + RAG are exhausted or clearly insufficient.

## Time box

The researcher runs a bounded check (≤5 minutes of real wall-clock). It is not a deep research session. If the topic requires depth, escalate to the `deep-research` skill.

## Output contract

The researcher returns a findings doc at the specified path containing:
- What already exists (file paths, links, or URLs).
- Whether each existing artifact is adaptable or must be built fresh.
- The relevant standard or pattern learned.

## After receiving findings

Read the findings doc. Do not build anything the findings show already exists and is adaptable. If the researcher found nothing, proceed with authoring using the appropriate writer skill.
