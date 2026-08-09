# Enum Value Mappings

`enum` settings use two metadata fields:

- `AllowedValuesJson` stores the canonical list of saved values, for example `['survival','creative']`.
- `ValueMappingsJson` stores optional display labels for those values, for example `{'survival':'Survival Mode'}`.

The UI should always save one of the allowed values. If a mapping exists, the dropdown shows the mapped label while keeping the stored value unchanged. If `ValueMappingsJson` is missing, incomplete, or invalid, the UI should fall back to the raw allowed value. If the enum metadata cannot be parsed safely, the editor should fall back to a plain text field rather than rendering an empty selector.

Boolean settings are separate from enums. They should use a checkbox or toggle and store `true` or `false` rather than enum-style labels.