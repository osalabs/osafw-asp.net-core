# Framework Naming Conventions

Use this guide when adding new framework code, reviewing method names, or touching nearby model/controller helpers. It describes preferred conventions, not a forced migration rule for application projects built on the framework.

## Use This When

- You are naming or renaming model, controller, service, or helper methods.
- You are adding model methods that read from or write to the database.
- You are reviewing generated code or agent-authored code for project fit.
- You are deciding whether an existing name is worth changing while you are already touching the same code.

Do not rename unrelated working code only to satisfy this guide. Preserve public compatibility when callers may already depend on a name.

## Why This Matters

The framework has many model methods. Generic `Get*` and `Set*` names quickly become hard to scan because they hide the return shape and the real side effect. Framework names should answer the first question a reader has:

- Does this return a list, one row, a scalar, or a composed object?
- Does this write to the database?
- Which non-default filter or qualifier matters?

## Core Shape

Prefer method names shaped as:

```text
[result kind or side effect][qualifier][ByFilter]
```

Examples:

- `listByParticipant`
- `listActiveByParticipant`
- `oneWithModelBrand`
- `updateEmail`
- `updateTermsAgreeDate`

Use the shape as guidance, not as a parser. Some good names are shorter because the containing class or parameter list already carries the missing context.

## Result And Side-Effect Prefixes

- Use `list*` for collections of rows or entities.
- Use `one*` for a single row or entity.
- Use `count*` for counts.
- Use `exists*` for boolean existence checks when the boolean is the main result.
- For scalar values, name the returned value directly, such as `testClassCode`, `adjacentId`, `tableSchema`, or `junctionFieldStatus`.
- Use `listSelectOptions*` for lightweight option lists where callers usually need only id/name style values.
- Use `add*` only for create-only operations.
- Use `update*` only for persisted updates of existing records.
- Use `save*` when the method may create or update and that mixed behavior is important to the caller.
- Use `delete*` for deletion or soft-deletion operations.
- Use `build*` for methods that assemble DTO, view, config, or page-state structures without owning persistence.
- Use `load*` for methods that fetch data and populate controller or model state.
- Use `apply*` for methods that mutate query, filter, search, or request state.
- Use `attach*` for methods that enrich an existing response, row, DTO, or view model.
- Use `validate*` for validation helpers.
- Use `sync*` for methods that reconcile submitted state with stored state.

## Filters And Qualifiers

Use `ByFilter` only when the filter is not already obvious from the default model id.

Prefer:

- `oneByIcode`
- `listByParticipant`
- `listActiveByParticipant`
- `listLinkedByMainId`
- `listSelectOptionsYears`

Avoid:

- `oneUserByIcode` inside the `Users` model.
- `listById` when the method reads by the model's main id.
- `listByParticipantId` when framework convention already implies entity references are passed by id.

Include `Id` when it prevents real ambiguity. For example, `listLinkedByMainId` can be clearer than `listLinkedByMain` when both main and linked sides exist in a junction model.

## Casing

Casing matters for scanability, but it should not become a churn project.

- Use `PascalCase` for C# classes, controller classes, DTO classes, and framework route action methods such as `IndexAction`, `ShowAction`, `ShowFormAction`, `SaveAction`, `SaveMultiAction`, `ShowDeleteAction`, and `DeleteAction`.
- Use the established local style for ordinary helper methods. Framework model helpers commonly use lower camel case, such as `listByParticipant`, `oneByIcode`, and `updateEmail`.
- Use `ALL_CAPS` for constants when the surrounding framework code already follows that style, such as access-level constants.
- Use `camelCase` for local variables by default.
- Use `snake_case` for local variables only when mirroring database columns, template keys, request keys, JSON config keys, or existing framework fields makes the code easier to verify.
- Avoid using two casings for the same concept in one method or class.

For local variables, clarity in the immediate scope matters more than global uniformity. A short method with consistent names is usually better than a broad rename that obscures the actual change.

## Framework Names That Stay As-Is

Keep standard route hooks and framework contracts unchanged:

- `IndexAction`
- `ShowAction`
- `ShowFormAction`
- `SaveAction`
- `SaveMultiAction`
- `ShowDeleteAction`
- `DeleteAction`

Keep established framework type and data-shape names when they are part of existing contracts, such as `FwDict`, `FwList`, `DBRow`, `DBList`, `ps`, `field_id`, `field_iname`, and `field_status`.

## Return-Shape Contracts

Naming should match the return contract:

- `list*()` methods return empty `FwList`/`DBList` or typed lists when no rows are found.
- Dictionary-backed `one*()` methods return empty `FwDict`/`DBRow` when no row is found.
- Typed single-row methods such as `DB.row<T>`, `DB.rowp<T>`, and `oneT*` return `null` when no row is found, unless using an `*OrFail` method.
- `*OrFail` names should throw or otherwise fail clearly when the record is missing.

Do not use a `one*` name for a method that sometimes returns many rows, and do not use a `list*` name for a method that returns one row.

## Avoid

Avoid these when a more specific name is available:

- `Get*` / `Set*`
- `Process*`
- `Handle*`
- `Data`
- `Info`
- `Full`
- `PS`
- Repeating the model class name inside model methods.
- Leaking table, join, or storage details into the name unless that detail is meaningful to the caller.

These names are not banned. They are fallback names for cases where a more specific result or side-effect name would be misleading.

## Examples

Prefer:

- `listByParticipant`
- `listSelectOptionsYears`
- `oneByIcode`
- `updateEmail`
- `saveModel`
- `buildPageState`
- `applyListSearch`
- `loadListRows`
- `saveSubtableRow`

Avoid:

- `GetLeasesByParticipantIdList`
- `GetLeasesYearsSelectOptions`
- `SetParticipantEmail`
- `GetListRows`
- `SetPS`

## Review Checklist

Before accepting a new method name, check:

- The first word tells the reader the result kind or side effect.
- The qualifier describes the meaningful state or shape, not generic words like `Data`.
- The `By*` suffix names a real non-default filter.
- The method does not repeat the model class name unless the repetition removes ambiguity.
- The casing matches the surrounding contract and does not introduce two spellings for one concept.
- Existing callers are protected when renaming a public or widely used method.

## Adoption

Use these conventions for new framework work and for focused cleanup when a touched name already slows review. Existing names that do not follow this guide can remain until a local refactor or compatibility window makes the rename worthwhile.
