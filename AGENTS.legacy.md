# Guidelines for AI contributors

This repository contains the **OSA Framework** – a lightweight MVC-inspired web framework built on ASP.NET Core.  The project root hosts a Visual Studio solution `osafw-asp.net-core.sln` which includes:

- `osafw-app` – the main framework/web application
- `osafw-tests` – MSTest project with unit tests

## Repository Layout

```
/osafw-app            main web project
  /App_Code           C# sources
    /controllers      controller classes
    /fw               framework core libraries
    /models           model classes
  /App_Data           non‑public data
    /sql              initial DB and updates
    /template         ParsePage HTML templates
  /upload             private uploads
  /wwwroot            public web root
/osafw-tests          MSTest tests
```

## Building & Running

The framework targets .NET 8.0. Build and run using the standard SDK:

```bash
# build the solution
 dotnet build osafw-asp.net-core.sln

# run the web project
 dotnet run --project osafw-app/osafw-app.csproj
```

Unit tests are located in `osafw-tests`. Run them with:

```bash
 dotnet test osafw-tests/osafw-tests.csproj
```

> Note: in offline environments NuGet restore may fail. Ensure packages are pre‑restored or available locally.

## Coding Style

- Controllers inherit from `FwController` (or `FwAdminController`, `FwApiController` etc.) and reside under `osafw-app/App_Code/controllers`.
- Models inherit from `FwModel` and live under `osafw-app/App_Code/models`.
- Use UpperCamelCase for class names (`UserLists`, `UserListsController`) and snake_case for table names (`user_lists`).
- Templates mirror controller names under `osafw-app/App_Data/template/<controller lowercase>/<action>`.
- Place validation logic inside `Validate()` methods.
- Standard layout files are defined in `appsettings.json` (`PAGE_LAYOUT`, `PAGE_LAYOUT_PUBLIC` and others).
- use SiteUtils.cs for application-specific helpers not related to a particular model

## Helpful Docs

- `README.md` – framework overview, development & deployment notes, naming conventions and best practices.
- `docs/db.md` – description of `DB.cs` helper and SQL utilities.
- `docs/dynamic.md` – dynamic and Vue controllers reference.
- `docs/parsepage.md` – ParsePage template engine reference.
- `App_Data/template/dev/manage/entitybuilder/README.md` – entity definition format used by the scaffolding tools.

## Common Tasks

- **Database updates**: modify `App_Data/sql/database.sql` then add incremental scripts under `App_Data/sql/updates/` (`updYYYY-MM-DD.sql`).
- **Self tests**: run controller and configuration checks via the `DevSelfTest` controller (`/Dev/SelfTest`).
- **Background jobs**: `FwCronService` can be enabled in `Program.cs` for scheduled tasks.

Follow the conventions above when enhancing the framework or creating applications based on it. All pull requests should ensure `dotnet build` and `dotnet test` succeed.