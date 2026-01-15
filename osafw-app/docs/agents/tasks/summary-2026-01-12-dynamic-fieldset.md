## What changed
- added fieldset structural support for dynamic show/showform configs with templates, JS toggle behavior, and styling
- updated DemosDynamic edit config to demonstrate fieldset usage
- refactored fieldset rendering to avoid nested fieldset markup inside existing form fieldsets

## Commands that worked (build/test/run)
- 

## Pitfalls - fixes
- dotnet is not available in the environment (dotnet run failed)

## Decisions - why
- switched the fieldset structure template to a div wrapper so dynamic fieldsets can nest inside existing form fieldsets without invalid HTML

## Heuristics (keep terse)
- 
