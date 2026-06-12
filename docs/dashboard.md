# Dashboard Panels

The main dashboard renders a collection of *panes*. Each pane is described by a hashtable in the controller and rendered via templates under `osafw-app/App_Data/template/main/index`.

For dashboard card visual rules, theme behavior, icon treatment, and spacing examples, see [design_system.html](design_system.html#components).

The built-in `MainController` panes are sample framework dashboard data. Some sample aggregates use conventional current-user filters for lower-access sessions, while panes such as `Users by Type` remain framework samples. Production apps should replace or scope dashboard panes with app-specific authorization predicates instead of treating the sample aggregates as domain-ready access control.

When `ASSISTANT_ENABLED=true`, `/Main` also renders a compact AI Assistant pane that posts the prompt to `/Assistant`. The pane remains visible if the OpenAI key is missing; submission then returns the standard administrator-configuration message without creating assistant or RAG records.

## Built-in Panel Templates

The framework ships with several panel types:

- `bignum` – large number with optional badge and icon
- `barchart` – bar chart using Apache ECharts
- `piechart` – doughnut chart using Apache ECharts
- `linechart` – line chart using Apache ECharts
- `areachart` – area chart (line with filled region)
- `table` – simple data table
- `html` – raw HTML block
- `progress` – Bootstrap progress bar

- `assistant` - compact prompt form that continues in `/Assistant`

## Creating a Custom Panel Type

1. **Create a template** named `type_NAME.html` in `osafw-app/App_Data/template/main/index`.
2. **Register the template** in `std_pane.html` by adding a line:
   ```html
   <~type_NAME ifeq="type" value="NAME">
   ```
3. **Provide data** in the controller. Add a hashtable to the `panes` collection in `MainController.IndexAction()` with at least a `type` field:
   ```csharp
   one = [];
   one["type"] = "progress";
   one["title"] = "Active Users";
   one["percent"] = 75;
   one["progress_class"] = "bg-success";
   panes["progress"] = one;
   ```

Panel data is available to the template via its keys. See the existing templates for examples.
