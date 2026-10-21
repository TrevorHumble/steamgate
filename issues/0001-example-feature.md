# Issue #0001 — Example: <describe your first feature>

**Type:** ready. **Category:** <product feature or system change>.
**Depends on:** none.
**Blocks:** none.
**Touches:** `<path/to/file>` (new).

> This is a placeholder EXAMPLE issue showing the required shape from `standards/issue-standards.md`:
> a consumer user story, Given/When/Then acceptance criteria that resolve to literal/structural checks,
> a numbered implementation plan, and the dependency map above. Replace every angle-bracket placeholder
> with real content, or delete this file and let the orchestrator file your first issue.

## User story
As a <consumer — the person or system that uses what this builds>, I need <what they need> so that
<the outcome it produces>.

## Acceptance criteria
Each criterion is a literal string or structural check a separate agent can verify by reading only the
produced artifact — no semantic interpretation.

1. **Given** the new file `<path/to/file>`, **When** a reader greps it, **Then** it contains the literal
   string `<expected token>`.
2. **Given** the file, **When** a reader greps it, **Then** it contains `<another literal or structural
   property>`.
3. **Given** the input `<example input>`, **When** `<the function/action>` runs, **Then** it produces the exact output `<expected output value>`. *(Keep at least one criterion in this input→output form — an issue whose criteria are all "file contains string" checks can't catch a wrong implementation.)*

## Implementation plan
1. Create `<path/to/file>` with <the core deliverable named in criterion 1>.
2. <Second concrete step naming a file path or deliverable>.
3. <Third step — e.g. verify the result and record it>.

## Out of scope
- <What this issue deliberately does not cover, deferred to a later issue.>
