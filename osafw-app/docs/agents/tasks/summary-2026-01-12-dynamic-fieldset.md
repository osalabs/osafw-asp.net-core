## What changed
- added fieldset structural support for dynamic show/showform configs with templates, JS toggle behavior, and styling
- updated DemosDynamic view/edit configs to demonstrate fieldset usage
- refactored fieldset rendering to avoid nested fieldset markup inside existing form fieldsets
- added fieldset legend class support, animated icon rotation, and legend overlap styling
- adjusted fieldset padding and legend offset for spacing tweaks
- wrapped Demos view/edit date sections in fieldset styling and added Vue fieldset support
- added fieldset usage in DemosVue config for date groups
- added Vue fieldset toggle handlers for click/keyboard collapse
- introduced themeable fieldset legend background token with theme overrides

## Commands that worked (build/test/run)
- 

## Pitfalls - fixes
- dotnet is not available in the environment (dotnet run failed)

## Decisions - why
- switched the fieldset structure template to a div wrapper so dynamic fieldsets can nest inside existing form fieldsets without invalid HTML

## Heuristics (keep terse)
- 
