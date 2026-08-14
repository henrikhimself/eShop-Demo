# Knowledge consolidation

You will execute a three-phase pass over this repository: consolidate code comments into documentation, compress `MEMORY.md`, then compress `CHRONICLE.md`. Finish with a reference-integrity pass and a consolidated change report.

Execute the phases strictly in order. Do not begin a phase until the previous phase is complete. Do not parallelize, interleave, or revisit a completed phase.

A separate prompt (deep review) runs after this one. Do not perform review work here, and do not write to `ISSUES.md`.

---

## 0. Operating rules (apply to all phases)

### 0.1 Determinism

- Follow the phase order exactly: Phase 1 → Phase 2 → Phase 3 → Reference integrity → Final report.
- Where classification rules are numbered, apply them in order and stop at the first match. Do not weigh rules against each other.
- Do not ask clarifying questions mid-run. When intent is unclear, apply the conservative action defined for that phase and record the ambiguity in the final report.
- Make only the changes each phase explicitly authorizes. No opportunistic edits, no refactors, no formatting sweeps, no reordering of unrelated content.
- Process files in a stable order: alphabetical by repository-relative path. Process each file once.
- Use the exact section headings and report template given below. Do not rename, reorder, or add sections.
- Do not restate or re-litigate a decision already made in an earlier phase.

### 0.2 Confidence policy

For every classification, assign a confidence band and take the mandated action:

| Band | Meaning | Required action |
| --- | --- | --- |
| High | The evidence for the classification is in the file being read or in another repository file. | Apply the change. |
| Medium | The classification is likely but rests on inference. | Apply the conservative action (keep, preserve, or mark `Unclear`) and list the item in the final report under *Ambiguities*. |
| Low | Intent cannot be determined from repository content. | Change nothing. List the item in the final report under *Ambiguities*. |

Never resolve a Medium or Low item by inventing rationale.

### 0.3 Evidence and invention

- Do not invent dates, owners, priorities, requirements, decisions, rationale, policies, or intentions. Every statement written into documentation must be traceable to existing repository content.
- Preserve a date only when it is already stated in existing material or is unambiguously derivable from repository content (for example a git commit or changelog entry). Never estimate a date.
- Do not change facts. Compression and rewriting may reduce wording; they may not alter meaning.
- If a claim in existing documentation contradicts the code, do not silently correct either one. Record the contradiction in the final report.

### 0.4 Canonical file names

The canonical set is:

`README.md`, `AGENTS.md`, `SPEC.md`, `System landscape.md`, `TODO.md`, `MEMORY.md`, `CHRONICLE.md`, and ADR files under the repository's existing ADR directory.

Resolution rules:

1. Match existing files case-insensitively. If `readme.md` exists, use it as-is.
2. Never rename an existing file to match the canonical casing.
3. If a canonical file does not exist and a phase requires writing to it, create it using the canonical name above, with only the content that phase produces.
4. If two files resolve to the same canonical name (for example `Todo.md` and `TODO.md`), do not merge them. Write to the one with more content and record the duplication in the final report.
5. `c4` PlantUML (`.puml`) diagrams: keep them consistent with `SPEC.md` and `System landscape.md` when a change in this pass makes them inconsistent. Diagram edits are limited to what the change requires.

### 0.5 Global constraints

- Do not change runtime behavior.
- Do not modify source code except to remove, shorten, or relocate comments in Phase 1.
- Do not modify tests, build scripts, or CI configuration.
- Do not create or modify `ISSUES.md`. That file belongs to Part B.
- Do not perform formatting changes unrelated to the authorized work.
- Do not delete a `TODO` comment unless it is moved into `TODO.md` or is proven obsolete by existing repository evidence.
- Do not create an ADR unless the source material contains a clear architectural decision or trade-off. Follow the existing ADR format and numbering.

---

## 1. Knowledge routing table (single source of truth)

This table is authoritative for every phase. When any phase must decide where a piece of knowledge belongs, use it. Apply rows top to bottom and stop at the first match.

| Destination | Content it owns |
| --- | --- |
| Code comment | Non-obvious knowledge tightly coupled to the adjacent implementation. See Phase 1 rule A. |
| ADR | Architectural decisions, selected patterns, rejected alternatives, trade-offs, risks, constraints, consequences, decision rationale. |
| `SPEC.md` | Functional behavior, domain rules, business requirements, acceptance criteria, API behavior, lifecycle rules, validation rules, user-visible behavior. |
| `System landscape.md` | System context, components, integrations, dependencies, external systems, data flows, deployment topology, runtime boundaries, ownership, cross-system interactions. |
| `AGENTS.md` | Instructions aimed at AI coding agents: repo conventions, common commands, safe-editing rules, test expectations, documentation boundaries, prohibited behaviors, workflow guidance. |
| `README.md` | Human onboarding: repository purpose, local setup, build/run/test instructions, high-level usage, developer workflow, common commands. |
| `TODO.md` | Actionable future work: cleanup tasks, known gaps, planned refactors, open questions, technical debt, follow-ups, migration steps. |
| `MEMORY.md` | Durable **current-state** knowledge an agent needs on every task: active conventions, established preferences, current constraints, repository norms, lessons that still apply. |
| `CHRONICLE.md` | **Historical** knowledge: timeline entries, notable changes, migration history, major refactors, incident aftermath, dated events useful as chronological context. |

Disambiguation rules:

- `MEMORY.md` versus `CHRONICLE.md`: if the statement describes what is true now, it belongs in `MEMORY.md`; if it describes what changed or why a past decision was made, it belongs in `CHRONICLE.md`. A statement that does both is split — current summary to `MEMORY.md`, explanation to `CHRONICLE.md`.
- `MEMORY.md` versus `TODO.md`/`SPEC.md`/ADR: `MEMORY.md` never holds implementation details, actionable tasks, specified behavior, or decision rationale that belongs in those files. It holds only the norms and constraints that govern how work is done.
- Decision rationale versus behavior: the reason a choice was made goes in an ADR; the behavior that resulted goes in `SPEC.md`.

---

## 2. Phase 1 — Code comment consolidation

**Objective:** reduce verbose, stale, duplicated, architectural, historical, and project-level knowledge in code comments. Keep code comments concise and local. Move broader knowledge to the destination given by the routing table in section 1.

Guiding principle: a code comment explains *why this, here*. Documentation explains what the project knows.

### 2.1 Preparation

1. Read the repository structure.
2. Read all existing documentation files listed in section 0.4, plus ADRs and any `doc/` or `prompts/` content, before modifying any comment. This establishes what knowledge already exists and prevents duplicate migration.

### 2.2 Unit of work

A *comment* is one contiguous comment block: consecutive comment lines uninterrupted by code, or a single block comment. Counts reported later use this definition.

### 2.3 Classification

For each comment, apply these rules in order and stop at the first match.

**Rule A — Keep in code.** Keep the comment if it explains something non-obvious and tightly coupled to the nearby implementation:

- why a seemingly strange implementation is necessary
- edge cases that are easy to break
- protocol, framework, or API quirks relevant to the exact code block
- security-sensitive assumptions
- concurrency, ordering, timing, retry, caching, or consistency concerns
- invariants that must remain true for this method or block
- generated-code warnings or tool-specific markers
- short clarification where the code cannot be made self-explanatory

Kept comments are still subject to the rewrite rules in 2.4.

**Rule B — Move to documentation.** If the comment carries knowledge that is durable, project-level, or useful away from this call site, move it to the destination given by the routing table. Delete it from the code afterwards.

Pointer rule: leave a local pointer of at most one line only when the code would otherwise appear incorrect or arbitrary without it. Pointer format: `// See <FILE> — <short subject>`. Do not add pointers routinely.

Merge rule: if the destination file already states the same knowledge, do not append a duplicate. Delete the comment and count it as *removed as duplicate*, not *moved*.

**Rule C — Remove.** Remove the comment if it:

- repeats what the code already says
- narrates obvious control flow
- describes parameters, variables, or methods without adding insight
- contains stale, speculative, or unverifiable information
- explains general programming concepts
- duplicates nearby documentation
- includes implementation history not needed to maintain the code locally

**Rule D — Ambiguous.** If none of A–C applies at High confidence, keep the comment unchanged and record it in the final report under *Ambiguities*.

### 2.4 Rewrite rules for kept comments

- Shorten and sharpen. Prefer one concise sentence over a paragraph.
- Remove commentary, background history, and tutorial-style explanation.
- Drop justifications already captured in documentation.
- Avoid vague markers such as "important", "temporary", or "do not change" unless the reason is stated. If the reason is stated and is global, move the reason per Rule B.
- Do not move a genuinely local implementation constraint out of code if doing so would make the code harder to maintain.
- Do not change comment syntax style, indentation, or surrounding code.

---

## 3. Phase 2 — Compress `MEMORY.md`

**Objective:** reduce `MEMORY.md` so it contains only current, actionable knowledge. Move historical knowledge into `CHRONICLE.md`. Optimize for agent usefulness, architectural correctness, and low context bloat.

Phase 2 operates on the state of `MEMORY.md` *after* Phase 1 has written to it.

### 3.1 Read

Read the current contents of `MEMORY.md` and `CHRONICLE.md` in full before editing either.

### 3.2 Content definitions

`MEMORY.md` keeps information describing the current state of the system or directly guiding future work:

- current architecture and system boundaries
- current coding conventions
- current framework, library, and infrastructure choices
- active architectural constraints
- current domain rules
- current API contracts
- current data ownership rules
- current security, reliability, performance, or compliance requirements
- current deployment, testing, observability, or operational expectations
- known active risks and unresolved follow-ups
- current migration state — present status and next required actions only
- non-obvious implementation constraints that are still true

`CHRONICLE.md` receives information describing how the system got here:

- historical timeline entries
- completed migrations
- past refactors
- deprecated approaches
- old implementation details
- incident history
- decision history and rejected alternatives
- past architectural states
- historical release notes
- old plans that were completed, abandoned, or superseded
- rationale that explains the current architecture but is not needed on every task

### 3.3 Classification

For each section, paragraph, or bullet in `MEMORY.md`, apply in order and stop at the first match:

1. **Current knowledge** — keep in `MEMORY.md`.
2. **Current knowledge with historical explanation** — keep the current-state summary in `MEMORY.md`; move the rationale, history, or timeline to `CHRONICLE.md`.
3. **Historical knowledge** — move or merge into `CHRONICLE.md`.
4. **Obsolete or irrelevant** — remove, unless it explains an active constraint or prevents a known mistake, in which case keep it under *Non-obvious Current Constraints*.
5. **Unclear** — preserve verbatim and prefix the line with `Unclear:`. Never delete an unclear item.

### 3.4 Rewrite

Rewrite `MEMORY.md` as concise, current, actionable knowledge, using these sections in this order. Omit a section only if it would be empty.

```markdown
# Memory

## Current State
## Conventions and Constraints
## Non-obvious Current Constraints
## Open Follow-ups
## Unclear
```

- **Non-obvious Current Constraints:** surprising or important constraints that a future agent must not accidentally remove or violate.
- **Open Follow-ups:** unresolved risks, TODOs, cleanup items, architectural concerns, and decisions that need revisiting. If an item is actionable work rather than a governing constraint, move it to `TODO.md` and leave only a one-line reference here.
- Prefer bullets over prose. Merge duplicates. Do not add knowledge that was not already present.

Append moved content to `CHRONICLE.md` under its existing structure. Do not compress it during this phase; Phase 3 does that.

---

## 4. Phase 3 — Compress `CHRONICLE.md`

**Objective:** reduce the size of `CHRONICLE.md` while retaining everything that affects future decisions, migrations, debugging, architecture, security, reliability, operations, or maintainability. Optimize for future usefulness, not historical completeness.

Phase 3 operates on the state of `CHRONICLE.md` *after* Phases 1 and 2 have written to it.

### 4.1 Keep or summarize

1. **Architectural decisions** — chosen patterns, rejected alternatives, trade-offs, constraints, rationale; important decisions not captured in another document. If an entry is a full architectural decision not yet recorded in an ADR, create or update the ADR and leave a one-line dated pointer in `CHRONICLE.md`.
2. **System invariants and constraints** — what must remain true for correctness, security, performance, compliance, or operability; cross-service contracts, data ownership rules, API compatibility constraints, migration boundaries.
3. **Migration and refactoring knowledge** — major migrations, partially completed transitions, compatibility layers, deprecated paths, pending cleanup, why a migration happened, what assumptions remain.
4. **Incident aftermath and production learnings** — root causes, mitigations, known failure modes, monitoring gaps, rollback lessons, operational playbooks. Do not retain blow-by-blow timelines unless still useful.
5. **Non-obvious implementation context** — surprising behavior, historical reasons for awkward code, integration quirks, vendor limitations, legacy constraints; anything a future agent might otherwise "simplify" incorrectly.
6. **Domain or business context that affects design** — rules, workflows, regulatory assumptions, customer constraints, business decisions that shape the code.

### 4.2 Remove or aggressively compress

- routine implementation logs
- small bug fixes with no lasting lesson
- duplicated or near-duplicate entries
- temporary plans that were completed and no longer matter
- obsolete speculation
- status updates that do not affect future choices
- detailed timelines where only the outcome matters
- entries already captured accurately in ADRs, `README.md`, `SPEC.md`, or code comments

### 4.3 Summarization rules

- Preserve dates when they help establish sequence or compatibility. Do not invent or estimate dates.
- Prefer concise bullets over prose. Merge duplicates.
- Do not change facts. Do not create decisions unsupported by the original file.
- Do not remove unresolved risks or compatibility warnings.
- Do not remove rationale for unusual or non-obvious code.
- Do not retain implementation noise merely because it is recent.
- Compress old timeline entries into short grouped summaries under a `## Archived Historical Summary` section. Do not preserve every individual event unless it remains decision-relevant.

### 4.4 Gate before writing

Verify all six before producing the replacement. If any answer is no, revise before writing.

1. Could a new AI coding agent understand why the system looks the way it does?
2. Could it avoid repeating known mistakes?
3. Could it identify active migrations, deprecated paths, and compatibility constraints?
4. Could it preserve important non-functional requirements?
5. Is irrelevant historical detail removed?
6. Are unresolved risks still visible?

---

## 5. Reference integrity pass

Run once, after Phase 3 and before the final report.

Phases 1–3 moved, compressed, or removed knowledge. Code comments and documents may still point at content that no longer exists where it did. Locate references to relocated or removed knowledge — in code comments, documentation files, ADRs, and `c4` `.puml` diagrams — and update or remove them so neither developers nor AI agents are misled.

Constraints:

- Update only references that this pass invalidated. Do not fix pre-existing broken references; report them instead.
- Do not add new cross-references beyond restoring accuracy.
- If a reference cannot be repointed with High confidence, leave it and record it in the final report under *Ambiguities*.

---

## 6. Final report

Output exactly these sections, in this order, after the reference integrity pass completes. Keep it concise: bullets, no narrative.

```markdown
# Consolidation Report

## 1. Files Changed
<repository-relative paths, grouped by phase>

## 2. Phase 1 — Comment Consolidation
- Comments kept: <n>
- Comments shortened: <n>
- Comments removed: <n>
- Comments removed as duplicates of existing documentation: <n>
- Comments moved: <n>
- Destinations and the type of knowledge added to each:
  - <FILE>: <knowledge types>
- ADRs created or updated: <list, or None>

## 3. Phase 2 — MEMORY.md
- Original approximate size: <n lines / n words>
- New approximate size: <n lines / n words>
- Items kept, moved to CHRONICLE.md, removed, marked Unclear: <n / n / n / n>
- Items promoted to TODO.md, SPEC.md, or an ADR: <list, or None>

## 4. Phase 3 — CHRONICLE.md
- Original approximate size: <n lines / n words>
- New approximate size: <n lines / n words>
- Major information preserved: <bullets>
- Major information removed: <bullets>
- Unclear or risky removals: <bullets, or None>

## 5. Reference Integrity
- References updated: <n>
- References removed: <n>
- Pre-existing broken references found but not fixed: <list, or None>

## 6. Contradictions and Stale Knowledge Discovered
<contradictions between documents, between documentation and code, or unclear ownership>

## 7. Ambiguities
<every Medium- and Low-confidence item deferred during any phase, with file and reason>

## 8. Recommended Follow-up Actions
<ordered by value, most valuable first>

## 9. Handoff to Deep Review
- Documentation issues resolved by this pass, which the deep review should not re-report: <bullets>
- Areas this pass touched that warrant closer review: <bullets>
```

Do not add commentary outside these sections.
