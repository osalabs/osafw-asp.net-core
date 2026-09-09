# Select-template label caching

## Objective / acceptance

Avoid repeated parsing of select labels while preserving first-match lookup behavior and detecting changed or deleted files.

## What changed

Added a bounded, synchronized cache keyed by canonical path, UTC modification time and length. Invalid lines are ignored. Read failures retain the last successful parse; deletion invalidates it. Path containment now uses a directory boundary. The shared language-marker regex also serves option rendering.

## Changed contracts

No public signature changes. Malformed label lines no longer throw. Template replacement must change timestamp or length; cache capacity is 128 files. Canonical template documentation describes this behavior. No changelog entry is needed for this internal optimization and bug fix.

## Commands used / verification

- `dotnet test osafw-tests/osafw-tests.csproj --no-restore --filter 'FullyQualifiedName~SelectTemplateCacheTests|FullyQualifiedName~FromUtilsTests' --verbosity quiet`: 26 passed, covering duplicates, malformed lines, relative paths, replacement, deletion/recreation, concurrent reads, read failure recovery, sibling-prefix rejection and lookups beyond the configured capacity.
- Public helper entry points are exercised with temporary files and isolated settings; no shared database or application was used.

## Risks / follow-ups

Timestamp and length checks cannot detect a rewrite that deliberately preserves both. Filesystem link policy remains a deployment concern, as for other template loaders.
