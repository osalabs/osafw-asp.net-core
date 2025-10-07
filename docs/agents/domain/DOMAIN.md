# Domain Overview — OSAF ASP.NET Core Framework

## Purpose
OSA Framework (OSAFW) is a lightweight ASP.NET Core MVC-style framework optimized for building line-of-business web applications. It combines convention-based controllers and models with ParsePage templating and SQL Server persistence.

## Architecture
- **Solution layout**: `osafw-app` hosts the web application/framework code, while `osafw-tests` contains MSTest-based unit tests.
- **Controllers**: Inherit from `FwController` derivatives (`FwAdminController`, `FwApiController`, etc.) and reside in `App_Code/controllers`. Routing is convention-based; URL paths map directly to controller actions suffixed with `Action`.
- **Models**: Inherit from `FwModel` under `App_Code/models`, encapsulating data access and business logic.
- **Framework core**: Shared helpers live in `App_Code/fw`, including utilities like `FwCache`, `FwUpdates`, and `FwSelfTest`.
- **Templating**: Views leverage the ParsePage engine with templates stored under `App_Data/template/<controller>/<view>`.
- **Static assets**: Served from `wwwroot`, with LibMan-managed libraries in `wwwroot/lib` and custom assets under `wwwroot/assets`.

## Data & Persistence
- Defaults to SQL Server via the `db.net` library; schema scripts live in `App_Data/sql`.
- Incremental database updates are tracked through scripts in `App_Data/sql/updates/` and applied by `FwUpdates`.

## Key Workflows
1. **Development loop**: Modify controllers/models, update templates, run `dotnet build` and `dotnet test`, then run the app via `dotnet run --project osafw-app/osafw-app.csproj`.
2. **Request handling**: `FW.run()` dispatches to controller actions, applies authentication checks, and renders ParsePage templates.
3. **Deployment**: Publish with `dotnet publish --configuration Release`, deploy to IIS with .NET Hosting Bundle, and execute SQL scripts to provision the database.

## Integration Points
- Built-in RESTful conventions with optional custom actions.
- Vue.js support through dynamic controllers and JSON configuration files.
- Optional S3-backed file storage and cron service toggled in `Program.cs`.
