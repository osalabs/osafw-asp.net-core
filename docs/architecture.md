# Framework architecture and application conventions

osafw is an opinionated framework for business applications. Its purpose is to let developers spend their effort on business rules and useful screens. Recurring decisions about routes, CRUD, forms, feedback, persistence and layout belong in the framework where applications can inherit them.

This guide explains that division of work and how to choose an extension point. It describes the current framework and its intended direction; it does not make every legacy implementation a recommended pattern. The linked topic guides own detailed contracts. Applications copy the framework source and may customize it, so changes to inherited behavior still require consumer and upgrade checks.

## Follow a request through the framework

1. `FW.getRoute()` maps the request to a controller and action using the standard REST mappings, configured prefixes and the `/(Action)` convention for custom actions. A normal module needs no route table of its own.
2. FW applies route authentication, initializes the controller, checks controller access and invokes its action. Controller configuration can supply the model, fields, validation rules, list behavior and UI definitions. Controller access and row ownership are separate checks.
3. An inherited CRUD action handles the normal list, show, edit, save or delete flow. A custom action handles its specific request and delegates business operations to models. Custom mutations explicitly enforce the permitted method/XSS token and read-only restrictions; dispatching a custom action does not supply those guards automatically.
4. Model methods perform data access and entity-related business logic. Standard model writes maintain configured audit fields, activity history and request caches; creation timestamps normally come from database defaults. `modelAddOrUpdate()` also converts user input and sets standard saved-record feedback.
5. Actions normally return `FwDict` page state. FW selects ParsePage templates from the route; shared layouts and fragments provide the surrounding UI. `afterSave()` handles the ordinary redirect or JSON response, and `actionError()` provides the validation/error path. Direct-response actions are supported when the output needs them.

The consequence is practical: before replacing an inherited action, read its whole path, including the helpers it calls. Reproducing the database write alone can lose input conversion, error redisplay, feedback, cache invalidation or JSON behavior.

See [REST mappings](../README.md#rest-mappings), [FW](../osafw-app/App_Code/fw/FW.cs), [FwController](../osafw-app/App_Code/fw/FwController.cs) and [CRUD workflows](crud.md).

## Put each decision with its owner

| Concern | Existing owner | What an application supplies |
| --- | --- | --- |
| URL-to-action mapping | FW routing conventions | Controller/action names; route configuration only for an actual exception |
| Standard administration | `FwDynamicController` / `FwVueController`, `config.json`, common templates | Model, editable/list fields, labels, rules, options and custom fields |
| Request/response coordination | Controller actions and inherited hooks | Request-specific checks, model calls and additional page state |
| Entity data and business operations | `FwModel` or `FwModel<Row>` subclass, obtained through `fw.model<T>()` | Domain queries, calculations, invariants, ownership rules and transitions |
| Parameters, provider handling and database values | `DB` | Parameter values and any necessary custom query; model `qTable()` for its table identifier |
| User-facing text and navigation | ParsePage templates, selectors, module `url.html`, configuration labels | Business wording and module-specific UI; use language markers and escaping |
| Screen layout and interaction | Bootstrap 5, common list/form fragments, framework JavaScript | The fields and actions that differ from the standard screen |

A table model is also the home for business logic related to that entity. It need not be paired with a repository and service class for every feature. Separate a class when it has a real independent responsibility or lifecycle, rather than because a generic layering pattern calls for one.

## Start with the smallest application customization

Use a Dynamic or Vue controller when its configuration can express the screen. Override the narrow hook that differs: validation, a query condition, row enrichment, a field fragment or an additional action. Use a plain controller for flows that do not fit standard CRUD. `AdminDemosDynamic` demonstrates the configurable path; `AdminDemos` shows more explicit customization and examples, not a requirement to copy all of its code.

Keep standard data and UI declarations together in configuration when supported. Shared field renderers already implement labels, required state, input naming, validation feedback and layout. Custom controls remain appropriate for specialized inputs, but their author then owns the parts of that contract the renderer used to provide.

Use the existing `Validate`, `validateRequired`, `addFormError` and `validateCheckResult` flow for form validation. Standard create/update/restore feedback should use existing flash keys and templates. Unexpected database exceptions should reach the framework error path. A conditional update that affects no row is different: the model can return `false` because a business precondition no longer holds, and the controller must represent that outcome without claiming success.

Compose screens with the common header, list, form and delete-confirmation templates. Prefer Bootstrap utilities and existing framework components before adding CSS or another UI abstraction. Reuse the module URL template for links and form actions. See [dynamic controllers](dynamic.md), [templates](templates.md), [layout](layout.md), [design system](design_system.html) and [naming](naming.md).

## Know what the defaults do not guarantee

- `one()` / `oneT()` are request-cached lookups. Standard writes invalidate the relevant model caches. Raw DB writes bypass that behavior; invalidate explicitly where they change a cached record. Use a fresh read where external changes actually matter, such as claiming background work.
- Model reads/writes do not automatically authorize every row. Controller access does not establish ownership of a requested record or its attachments. Use the appropriate row/parent authorization at that boundary.
- An ordinary `update(id, fields)` is not a conditional state transition. A task claim, one-use token or similar operation can need an atomic update with additional predicates. Keep the special condition in its domain model. The protected `update(id, item, conditions)` overload retains standard audit/cache handling for administrative changes; quiet worker writes through `DB` still need explicit cache invalidation.
- Offline work has no browser request or logged-in user unless the caller establishes the necessary context. A report running in the background must deliberately establish its identity, input context and access checks.
- Existing source contains older conventions and specialized exceptions. Consult the relevant base implementation and canonical docs before copying an example or removing code that appears redundant.

Typed request-cache reads use the database property mapper to copy the saved row and return detached copies. Standard updates invalidate both typed and dictionary lookups. Audit comparison includes time for submitted `DateTime`/`DateTimeOffset` values and timestamp text containing a colon; date-only form text and unspecified-midnight DateTime values still compare calendar dates. This lets conditional task-definition updates reuse the standard model audit path.

## Evaluate a framework change by the work it removes

A useful core change lets applications stop repeating a real responsibility while preserving behavior. Name the existing callers it simplifies and the obligations it takes over, such as method guards, audit fields or response handling. Prefer a small extension to the existing owner over a parallel framework within a feature.

Fewer lines alone are not enough. Moving several necessary checks into a vaguely named wrapper can make developers learn another convention without removing a decision. Conversely, a shared CRUD renderer or model operation can remove an entire recurring concern. Check both the ordinary path and the deliberate exceptions before generalizing.

For review, trace one actual workflow from request to response and persistence. Ask what the application author still has to know, what is already handled by the framework, and whether the proposed change preserves that complete flow. Keep required domain rules explicit; avoid speculative machinery for unsupported deployments.

## Basis and scope

This guide was checked against FW routing/dispatch, the controller bases and demo controllers, `FwModel` and its typed subclass, and the common CRUD templates. It is an application-development guide, not proof that every module follows the conventions or that a particular PR is correct.

The description approach uses bounded guidance from the [FPF Library](https://github.com/ailev/FPF/tree/a5ac87ac15e78517d12bc66155b9da3ca254b643): `C.30.AD:0` keeps the purpose and limits of an architecture description explicit; `SYSE.5:4.1.1` helps reason from the intended developer outcome to the components that should provide it. The framework choices above come from osafw's code, documentation and maintainer direction. They are not requirements imposed by FPF.
