# Nullable warning cleanup prompt

Keep iterating on nullable warning fixes until `/root/dotnet/dotnet build /p:LibraryRestore=false /clp:Summary` reports **zero** nullable-related warnings. Do not pause after a handful of fixes—alternate building and repairing in a tight loop until clean. Priorities:

1. Run the build, copy the current nullable warnings, and focus on the highest-noise files first (often FW, Utils, EntityBuilder).
2. For each warning, decide whether null is truly possible; prefer non-null defaults (`toStr()`, `toInt()`, empty collections) unless null is semantically required.
3. Avoid adding nullable annotations where the runtime should guarantee a value; instead, initialize fields and coalesce option lookups.
4. After each fix batch, immediately rebuild and continue without waiting for intermediate requests; log progress in `docs/agents/tasks/summary-2025-12-08-002.md`.
5. When clean, run a final full build and document the zero-warning result in the task summary.
