# Dashboard Panels

The main dashboard renders a collection of *panes*. Each pane is described by a hashtable in the controller and rendered via templates under `App_Data/template/main/index`.

## Built-in Panel Templates

The framework ships with several panel types:

- `bignum` – large number with optional badge and icon
- `barchart` – bar chart using Chart.js
- `piechart` – doughnut chart using Chart.js
- `linechart` – line chart using Chart.js
- `table` – simple data table
- `html` – raw HTML block
- `area` – area chart (line with filled region)
- `progress` – Bootstrap progress bar

## Creating a Custom Panel Type

1. **Create a template** named `type_<name>.html` in `App_Data/template/main/index`.
2. **Register the template** in `std_pane.html` by adding a line:
   ```html
   <~type_<name> ifeq="type" value="<name>">
   ```
3. **Provide data** in the controller. Add a hashtable to the `panes` collection in `MainController.IndexAction()` (or another controller) with at least a `type` field:
   ```csharp
   var one = [];
   one["type"] = "progress";
   one["title"] = "Active Users";
   one["percent"] = 75;
   one["progress_class"] = "bg-success";
   panes["progress"] = one;
   ```

Panel data is available to the template via its keys. See the existing templates for examples.
