# Changelog

## Unreleased
- Moved class-only template partials from `App_Data/template/common/` into `App_Data/template/common/cl/` and updated references to the new locations (for example: `common/active` -> `common/cl/active`, `common/disabledcl` -> `common/cl/disabled`, `common/hide` -> `common/cl/hide`, `common/text_end` -> `common/cl/text_end`).
- Renamed class partials to match class names within the new `common/cl/` folder (for example: `disabledcl.html` -> `disabled.html`, `selected0.html` -> `selected.html`).
- Moved attribute-related template partials into `App_Data/template/common/attr/` and updated template references (for example: `common/checked` -> `common/attr/checked`, `common/disabled` -> `common/attr/disabled`, `common/display_none` -> `common/attr/display_none`).
