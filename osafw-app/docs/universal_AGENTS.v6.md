<!-- AGENTS.md - Universal, Auto-Bootstrapping (v6.2) -->

# Process/Workflow for each user request
0. Bootstrap (IMPORTANT!) - if you see this item, perform steps in <bootstrap> even if it takes additional efforts - it will save you time for user's tasks
<bootstrap>
- Discover Project: scan structure, languages, coding style, find helpful docs, common tasks, build/test/pack/CI tooling, etc. Identify entrypoints and common scripts.
- Initialize knowledge artifacts (create if missing):
  - `docs/agents/heuristics.md` - terse, timestamped heuristics. Mark with today’s date (YYYY-MM-DD).
  - `docs/agents/domain.md` - business knowledge, domain purpose, bounded context, invariants, core entities.
  - `docs/agents/glossary.md` - short definitions of domain and technical terms.
- Update sections (fill placeholders) below in this AGENTS.md
- Once done - delete bootstrap item and block from AGENTS.md
</bootstrap>

1. Create task summary file `docs/agents/tasks/summary-<YYYY-MM-DD>-<TASK-ID>.md` per template in
<task-summary-template>
## What changed
## Commands that worked (build/test/run)
## Pitfalls - fixes
## Decisions - why
## Heuristics (keep terse)
## Reflection
</task-summary-template>

2. Work on user's query/task, update task summary file as needed

3. Post-process - after user's query is completely resolved, perform self-reflection, self-improvement, and optimization of the entire process/workflow, see steps in <post-process>
<post-process>
Goal: accumulate reusable project knowledge while staying concise.
- Review task summary file, make sure it's complete
- Classify discoveries:
  - STABLE FACT - add to domain.md or glossary.md or relevant sections below in AGENTS.md
  - HEURISTIC - add to heuristics.md, timestamp new heuristics and expire or revise anything older than 90 days
  - ONE-OFF - keep in task summary file only
- If a substantial business decision changed, add an ADR under `docs/agents/adr/`
- AGENTS.md upkeep:
  - If patterns recur, add a new section here only if it reduces future token/step cost
  - Do not change more than 20% of AGENTS.md content in one go
- Keep all knowledge concise and informative
- Replace "YYYY-MM-DD" with today's date.
- Don't store secrets or large logs.
</post-process>

Whenever AGENTS.md updated - make copy of it to .github/copilot-instructions.md

## Project Overview
<!-- TODO fill by bootstrap -->

## Folder Structure
<!-- TODO fill by bootstrap -->

## Coding Style
<!-- TODO fill by bootstrap -->

## Helpful Docs
- `docs/agents/heuristics.md` - heuristics for this project
- `docs/agents/domain.md` - domain/business knowledge
- `docs/agents/glossary.md` - glossary of terms
- `README.md` - osafw framework documentation
<!-- TODO fill by bootstrap -->

## Common Tasks
<!-- TODO fill by bootstrap -->

## Command Palette
Working commands per OS

## Heuristics
Updated in each post-process run