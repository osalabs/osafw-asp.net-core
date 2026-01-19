## What changed
- swapped bootstrap-select assets and template usage for Select2, including new common template and updated demo config
- cleaned selectpicker references in docs and styles, updated autosave blur selector for Select2 search inputs

## Commands that worked (build/test/run)
- 

## Pitfalls - fixes
- dotnet CLI not available in the environment, so the app could not be run for screenshots

## Decisions - why
- used Select2 default theme with light Bootstrap-aligned overrides in the common template for immediate styling without extra dependencies

## Heuristics (keep terse)
- 
