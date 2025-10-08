<!-- AGENTS.md - Universal, Auto-Bootstrapping (v6) -->

# Process/Workflow for each user query
0. Bootstrap (IMPORTANT!) - if you see this item, perform steps in <bootstrap> even if it takes additional efforts - it will save you time for user's tasks
<bootstrap>
- review/learn about whole project, its structure, coding style, find helpful docs, common tasks
- update this AGENTS.md with all the information you found
- create `docs/agents/heuristics.md` with initial heuristics for this project (mark with Today's date)
- create `docs/agents/domain.md` with discovered domain/business knowledge (Purpose & Bounded Context) for this project
- create `docs/agents/glossary.md` with discovered glossary of terms for this project
- once done - delete bootstrap item and block from AGENTS.md
</bootstrap>

1. Create task summary file `docs/agents/tasks/summary-<YYYY-MM-DD>-<TASK-ID>.md` per template in
<task-summary-template>
## What changed
## Commands that worked (build/test/run)
## Pitfalls - fixes
## Decisions - why
## Heuristics (keep terse)
</task-summary-template>

2. Work on user's query/task, update task summary file as needed

3. Post-process - after user's query is completely resolved, perform self-reflection, self-improvement, and optimization of the entire process/workflow, see steps in <post-process>
<post-process>
- review task summary file, make sure it's complete
- classify discoveries:
  - STABLE FACT - add to domain.md or glossary.md or AGENTS.md relevant section
  - HEURISTIC - add to heuristics.md, timestamp new heuristics and expire or revise anything older than 90 days.
  - ONE-OFF - keep in task summary file only
- if a substantial business decision changed, add an ADR under `docs/agents/adr/`.
- keep all knowledge concise and informative
- replace "YYYY-MM-DD" with today's date.
- don't store secrets or large logs.
</post-process>

Whenever AGENTS.md updated - make copy of it to .github/copilot-instructions.md

## Project Overview
<!-- TODO fill by bootstrap -->

## Folder Structure
<!-- TODO fill by bootstrap -->

## Coding Style
<!-- TODO fill by bootstrap -->

## Helpful Docs
<!-- TODO fill by bootstrap -->

## Common Tasks
<!-- TODO fill by bootstrap -->

## Command Palette
Working commands per OS

## Heuristics
Updated in each post-process run