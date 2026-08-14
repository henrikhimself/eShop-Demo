# Deep Review

Perform a deep review of this repository to surface bugs, risky code, and maintainability problems, and to check that project organization, documentation, and workflows are healthy. Record findings in `ISSUES.md` at the repository root.

**Objective:** find real, verifiable problems. Optimize for accuracy over volume. Every finding must be traceable to a specific file, and to a line number where relevant, so a future engineer or agent can act on it without re-discovering the problem.

This is a review, not a refactor. Do not fix findings unless explicitly asked to. Describe them clearly enough that fixing them is straightforward.

## 0. Operating rules

### 0.1 Optional input — prior consolidation pass

A companion prompt (knowledge consolidation) may have run before this one. It reorganizes code comments, `MEMORY.md`, and `CHRONICLE.md`, and produces a consolidation report ending in a *Handoff to Deep Review* section.

- If that report is available in this session, treat its resolved items as resolved: do not re-report documentation staleness, misplaced knowledge, or contradictions it already fixed. Prioritize the areas it flagged as warranting closer review.
- If it is not available, proceed normally and review the repository as found.

Either way, review the repository in its **current on-disk state**, not against any prior described state.

### 0.2 Determinism

- Follow the method in section 3 in order.
- Do not ask clarifying questions mid-run. Record unresolved ambiguity as a finding or in the report, per the confidence policy.
- Process files in a stable order: alphabetical by repository-relative path within each priority tier defined in section 3.
- Use the exact `ISSUES.md` structure in section 5 and the exact report template in section 6. Do not rename, reorder, or add sections.
- Order findings within each severity by blast radius, most impactful first.

### 0.3 Confidence policy

| Band | Meaning | Required action |
| --- | --- | --- |
| High | The failure scenario has been traced through actual repository content. | Report it. |
| Medium | The code looks wrong but its intent is unclear from repository content. | Report it one severity level lower than it would otherwise warrant, and state explicitly in the *Summary* that intent is unclear. |
| Low | Cannot be substantiated from repository content. | Do not report it. |

Never assert a bug you have not traced. Never invent a failure scenario to justify a finding.

### 0.4 Evidence and invention

- Do not invent policies, requirements, intentions, owners, or priorities not evidenced by the repository.
- Do not report a finding you have not personally traced through actual repository content.
- If documentation and code disagree, report the contradiction rather than deciding which is correct, unless the correct side is unambiguous from repository content.

### 0.5 Canonical file names

Match documentation files case-insensitively against this canonical set: `README.md`, `AGENTS.md`, `SPEC.md`, `System landscape.md`, `TODO.md`, `MEMORY.md`, `CHRONICLE.md`, `ISSUES.md`, and ADR files under the repository's existing ADR directory. Never rename an existing file. Create `ISSUES.md` at the repository root only if it does not already exist.

### 0.6 Constraints

- The only file this prompt writes to is `ISSUES.md`. Do not modify source code, tests, build scripts, CI configuration, or other documentation.
- Do not report purely stylistic preferences — formatting, naming taste — unless they cause real confusion or inconsistency at scale.
- Do not duplicate findings already accurately tracked in `TODO.md`, unless this review adds new material information — for example, that a planned item is in fact a live bug.
- Re-validate every existing entry in `ISSUES.md`. Do not delete an issue you have not verified as resolved.

## 1. Scope

Cover all five areas. Skip an area only if it genuinely does not apply to this repository, and say so in the report rather than silently omitting it.

1. **Project organization**
   - Directory and module structure versus actual responsibilities.
   - Misplaced files, unclear boundaries, duplicated concerns across projects or packages.
   - Naming inconsistencies that make navigation harder.
   - Dead code, unused projects, orphaned files, stale generated artifacts.

2. **Project documentation**
   - `README.md`, `AGENTS.md`, `SPEC.md`, `System landscape.md`, ADRs, `TODO.md`, `CHRONICLE.md`, `MEMORY.md`, and doc/prompt content.
   - Accuracy: does the documentation match the current code and workflows?
   - Completeness: is there missing onboarding, setup, or architecture context a new engineer or agent would need?
   - Staleness: references to removed components, outdated commands, superseded decisions not reflected in ADRs.
   - Contradictions between documents, or between documentation and code comments.

3. **Build, test, and workflow**
   - Build scripts, CI/CD pipelines, Dockerfiles, task runners (`scripts/*.sh`, `*.ps1`, `*.zsh`, Makefiles, MSBuild, npm scripts).
   - Whether documented build/test/run instructions actually work as described.
   - Test coverage gaps for critical or risky logic; tests that are skipped, disabled, or assert nothing meaningful.
   - Flaky-looking tests: timing dependencies, shared mutable state, ordering assumptions.
   - Inconsistent tooling versions or configuration drift across local, CI, and container environments.

4. **Source code: bugs and risky patterns**
   - Concrete bugs: incorrect logic, off-by-one errors, wrong comparisons, unreachable code, resource leaks, unhandled exceptions.
   - Concurrency risks: races, unsynchronized shared state, deadlock potential, fire-and-forget async work.
   - Error handling: swallowed exceptions, overly broad catches, missing timeouts/retries/cancellation, silent failures.
   - Security-sensitive risk: injection, unsafe deserialization, path traversal, missing input validation, secrets or credentials in code, config, or logs.
   - Resource and lifecycle risks: unclosed streams, handles, or connections; missing disposal; unbounded caches or collections.
   - API and contract risks: nullability violations, implicit type coercions, mismatched assumptions between caller and callee.
   - Copy-paste duplication where a fix in one place is easy to miss in the other.

5. **Maintainability**
   - Code disproportionately complex, deeply nested, or hard to reason about relative to what it does.
   - Tight coupling that makes safe change difficult, or missing abstraction where duplication has spread.
   - Inconsistent conventions — naming, error handling, logging, configuration — across otherwise similar code.
   - Configuration or magic-value sprawl that should be centralized.

## 2. Severity levels

- **Critical** — causes incorrect behavior, data loss, security exposure, or outages in realistic conditions.
- **High** — likely to cause bugs or significant maintenance pain; not yet triggered, but a clear failure scenario exists.
- **Medium** — real risk or maintainability cost, with limited blast radius or requiring unlikely conditions.
- **Low** — minor risk, inconsistency, or cleanup opportunity worth tracking but not urgent.

## 3. Method

1. Read repository documentation first — `README.md`, `AGENTS.md`, `SPEC.md`, `System landscape.md`, ADRs, `TODO.md`, `CHRONICLE.md`, `MEMORY.md` — to understand intended structure, conventions, and known constraints before judging the code against them.
2. Survey the directory and project structure to understand actual organization.
3. Read build, test, and workflow definitions, and cross-check them against what the documentation claims.
4. Read the existing `ISSUES.md`, if present, so prior findings can be re-validated during the code review rather than in a separate pass.
5. Review source code for the risk areas in section 1, in this priority order:
   - **Tier 1:** code paths with the highest blast radius — startup and bootstrapping, shared infrastructure, security-sensitive code, concurrency, external I/O.
   - **Tier 2:** recently changed or actively developed areas. Use git history and status for signal.
   - **Tier 3:** code with weak or missing test coverage.
   - **Tier 4:** everything else.
6. Verify each candidate finding against the actual code before reporting. Confirm the failure scenario is real, not a stylistic preference. Prefer fewer, confidently verified findings over a long list of speculative ones.
7. If something looks wrong but its intent is unclear, apply the Medium band in section 0.3 rather than asserting a bug.

## 4. Stopping condition

Stop reviewing when Tier 1 and Tier 2 are fully covered and every existing `ISSUES.md` entry has been re-validated. If Tiers 3 and 4 were not fully covered, say so explicitly in the report under *Areas skipped*. Do not silently truncate.

## 5. `ISSUES.md` format

Merge, do not overwrite. Keep existing open issues that are still valid, update those this review re-confirms or invalidates, and append new ones.

```markdown
# Issues

Last deep review: <date>

## Critical

### <short title>
- **Area:** organization | documentation | workflow | code | maintainability
- **Location:** `path/to/file.ext:line`
- **Summary:** one or two sentences describing the defect.
- **Failure scenario:** concrete input/state that triggers it, and the resulting wrong behavior.
- **Suggested fix:** brief direction, not a full patch.

## High

...

## Medium

...

## Low

...

## Resolved since last review

List issues from the previous `ISSUES.md` that this review confirmed are fixed, with a one-line note on how.
```

For `<date>`: use the date supplied in this session. If none was supplied, write `<unknown>`. Do not guess.

## 6. Required output to the user

After updating `ISSUES.md`, report exactly these sections, in this order. Keep it concise: bullets, no narrative.

```markdown
# Deep Review Report

## 1. Coverage
- Areas reviewed: <list>
- Areas skipped and why: <list, or None>
- Tiers fully covered: <Tier 1-4>

## 2. Findings by Severity
- Critical: <n>
- High: <n>
- Medium: <n>
- Low: <n>

## 3. Most Important Findings
<2-3 bullets, one line each>

## 4. Previous ISSUES.md Entries
- Confirmed resolved: <n>
- Invalidated or no longer reproducible: <n>
- Still open and re-confirmed: <n>

## 5. Unresolved Ambiguity
<findings reported at reduced severity because intent was unclear, and anything that could not be substantiated>
```

Do not add commentary outside these sections.
