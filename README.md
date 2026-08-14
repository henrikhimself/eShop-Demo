# eShop Demo

A reference implementation of an e-commerce platform built with .NET 10, Aspire, Optimizely CMS 13, and Optimizely Commerce Connect 15.

The purpose of the demo is to:

- Demonstrate how a traditional digital commerce platform can be integrated with cloud-native architectural patterns.
- Provide a large, feature-rich codebase with sufficient real-world functionality to stress-test agentic coding workflows.
- Serve as a proving ground for evaluating new technologies, tools, and architectural patterns.

## Overview

This demo combines:

- Local developer support with no cloud dependencies.
- Support for multiple AI coding harnesses.
- Optimizely CMS 13 for content management and editorial experiences.
- Optimizely Commerce Connect 15 for catalog, pricing, inventory, promotions, cart, and order management.
- .NET Aspire for orchestration, service discovery, and observability.
- .NET 10 as the application platform.
- Microservices deployed as independently deployable units.
- Asynchronous messaging.
- API-based communication.
- Resiliency and fault tolerance.

## Prerequisites

- x86-64 (amd64) CPU
- Docker

## Developer CLI

The `./scripts/eshop.sh` script installs and exposes commands for building, testing, and managing the development environment. Run `./scripts/eshop.sh -h` for usage instructions.

The eShop CLI formalizes development workflows, automates routine tasks, and enforces strict quality gates to ensure consistency and reliability.

It provides an AI-friendly developer experience with terminal output designed to be easily understood by AI coding agents. It also acts as a guardrail against common AI coding failure modes, helping detect when agents take shortcuts, skip validation, or otherwise drift from the project's required quality standards.

## AI Development Knowledge System

The solution is designed to support multiple AI coding harnesses. To avoid vendor lock-in and ensure knowledge remains portable, project context is captured in dedicated artifacts with clearly defined responsibilities.

- CHRONICLE preserves reasoning chains, implementation history, lessons learned, and discoveries. This prevents critical context from becoming scattered throughout source code, commit messages, or tool-specific conversations.
- MEMORY stores current project knowledge in a harness-agnostic format that can be consumed by different AI coding tools, including Claude, Copilot, and future assistants.
- TODO stores open and deferred questions.
- ADRs (Architecture Decision Records) capture architectural and technical decisions, together with their rationale, alternatives considered, and trade-offs.
- SPECs define requirements, expected behavior, constraints, and acceptance criteria.
- C4 Model diagrams provide visual representations of the system architecture.

See the `doc/prompts/knowledge-consolidation.md` for a ready to use prompt.

## ISSUES.md and Human Review

The `ISSUES.md` file serves as the persistent output of the project's deep code review process. It captures bugs, architectural concerns, quality issues, and other findings identified through structured reviews of the codebase.

AI coding agents can produce code that compiles and satisfies static analysis while still containing defects, flawed assumptions, or design issues. Likewise, AI-generated code reviews can report false positives, make unsupported assumptions, or recommend changes that deviate from the intended architecture and design.

The `ISSUES.md` file provides a human-in-the-loop quality control mechanism. Findings identified during review are recorded in `ISSUES.md` and must be evaluated, addressed, rejected, or explicitly accepted by a human reviewer before being promoted to actionable work.

See the `doc/prompts/deep-review.md` for a ready to use prompt.

## Documentation Clarity

Some documentation is written using ASD-STE100 (Simplified Technical English) to improve clarity. ASD-STE100 is a controlled language standard that promotes simple sentence structures, consistent terminology, and unambiguous wording.

The goal is not to oversimplify technical concepts, but to communicate them in a way that reduces the likelihood of misinterpretation. Technical documentation serves as input to both human developers and AI coding agents, making precision more important than stylistic flexibility.

In AI-assisted development, ambiguity can be expensive. A coding agent that misinterprets a requirement can generate large amounts of plausible but incorrect code that may compile and appear reasonable during review. Identifying the misunderstanding, tracing its impact across the codebase, and discarding or reworking the resulting implementation wastes both time and tokens. Clear and unambiguous documentation reduces this risk by making requirements, constraints, and architectural intent easier to interpret correctly from the outset.

Using ASD-STE100 helps to:

- Reduce ambiguity in requirements, specifications, and architecture documentation.
- Promote consistent terminology throughout the project.
- Improve the reliability of AI-assisted development by providing clearer instructions and context.
- Reduce wasted effort caused by misunderstood requirements and incorrect assumptions.
- Minimize token consumption spent generating, reviewing, and correcting code based on faulty interpretations.
- Reduce the risk of implementation errors caused by ambiguity in requirements or design intent.

ASD-STE100 is applied selectively where clarity, precision, and consistency are more important than narrative style.

## Code Style: Optimized for AI Coding Agents

This repository's `.editorconfig` is deliberately stricter than a typical human-oriented C# style guide. Its primary design goal is to make code safe and predictable for AI coding agents to read, generate, and modify - not just pleasant for people to write.

Concretely, the rules are chosen to:

- Reduce inference requirements: explicit types, accessibility modifiers, braces, and naming patterns mean an agent (or a reviewer) never has to guess intent from context.
- Eliminate ambiguity: one idiomatic way to express a given construct is enforced wherever reasonably possible, instead of allowing several equivalent styles to coexist.
- Improve the safety of isolated edits: most rules are file-local and structural (naming, ordering, nullability, explicit modifiers) so a change to one file or method can be validated without needing broader project context.

### Why this trade-off is necessary

AI coding agents typically edit code in small, isolated diffs and have limited ability to infer conventions that aren't explicitly enforced by tooling. A style that is merely "readable to an experienced human" can still be ambiguous enough to cause an agent to introduce subtle regressions (e.g. silently widened nullability, an unintended access modifier, or an inconsistent naming pattern). Enforcing stricter, more explicit rules at `error` severity via `.editorconfig` - and failing the build on any violation - closes that gap by making the compiler itself the safety net, rather than relying on human review to catch every ambiguity.

This does mean some rules will feel more rigid or verbose than what many human developers are used to. That is an intentional and accepted trade-off: the cost of extra explicitness is paid once, consistently, by tooling; the alternative cost - an ambiguity-driven bug introduced by an automated edit - is far more expensive to find later.

### Idiomatic and performant C# is still the goal

Stricter does not mean unconventional. The configuration is built on top of, not instead of, idiomatic C#: standard .NET naming and formatting conventions, modern language features (pattern matching, `nameof`, file-scoped namespaces, primary constructors, etc.), and the framework's own recommended analyzer rules. Where a rule would conflict with idiomatic usage of a core framework in this stack (e.g. Optimizely CMS, Entity Framework Core, or `System.Text.Json`), it has been deliberately relaxed rather than forced.

Runtime performance and allocation efficiency are also explicit priorities: the ruleset enables analyzers that favor concrete types over interfaces where safe, span-based and allocation-free APIs, and avoidance of unnecessary allocations in hot paths. In short: prefer the idiomatic, performant C# convention - and where more than one idiomatic option exists, prefer whichever option is more explicit and unambiguous.

## Agent Coding Harness

The `.agents` and `.claude` directories, as well as `.mcp.json`, are intentionally not committed. This lets each developer choose their own agent coding tools and configuration without imposing them on others.

The `AGENTS.md` contains common instructions. You can add `AGENTS.local.md` for your own instructions and to override common agent instructions.

## Disclaimer

This project is an independent educational and reference implementation. It does not contain code, assets, or confidential information from any client engagement. Any similarity to commercial systems is incidental and reflects commonly used software engineering practices and architectural patterns.
