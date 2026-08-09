# V2 GameType Settings & Metadata

How settings are defined on a `GameTypeRevision`, what each data type does, and how values are resolved at deployment time.

> This replaces the retired V1 `GameType-Metadata-Complete-Guide.md` and `GameType-Editor-Complete-Functionality-Guide.md`. The V1 `DefaultSettings`, `SettingsMetadata`, `ExtendedMetadata`, `PortValidation`, and `PortRelationships` models no longer exist.

---

## Model Shape

Settings live on the revision, not the game type:

```
GameType
└── GameTypeRevision
	└── GameTypeSettingDefinition        (SettingKey, DefaultValue, Description, DisplayOrder)
		└── GameTypeSettingMetadata      (DataType, Category, IsRequired, validation, enum values)
			└── GameTypeSettingPortMapping[]
```

`GameTypeSettingDefinition` describes *what* the setting is. `GameTypeSettingMetadata` describes *how it is edited and validated*. A setting becomes a container environment variable named after its `SettingKey`.

### Metadata fields

| Field | Purpose |
|---|---|
| `DataType` | Controls the editor and validation. See table below. |
| `Category` | Groups settings in the editor UI. |
| `IsRequired` | The setting must be present on the server. |
| `CannotBeEmpty` | The value may not be blank. |
| `Placeholder` | Editor hint text. |
| `ValidationPattern` / `ValidationMessage` | Regex validation and its failure message. |
| `AutoAllocatePort` | For `port` settings — allocate a free published port automatically. |
| `ValidateRelatedPortsAvailability` | Also check offset/multiplier-derived ports for availability. |
| `AllowedValuesJson` | Generated JSON array of allowed enum values. |
| `ValueMappingsJson` | Generated JSON object mapping value → display label. |
| `PortMappings` | Links the setting to revision ports (see [V2 Ports & Web Hosts](V2-Ports-And-WebHosts.md)). |

---

## Data Types

| `DataType` | Editor | Notes |
|---|---|---|
| `string` | Text box | Default. |
| `number` | Numeric input | |
| `boolean` | Switch | Emits `true` / `false`. |
| `yesno` | Switch | Emits literal `yes` / `no`. |
| `enum` | Dropdown | Backed by `AllowedValuesJson` + `ValueMappingsJson`. |
| `port` | Numeric input with live availability check | Drives port mappings and save gating. |
| `servervariable` | Text box **plus an expansion toggle** | Supports `{Token}` substitution. |

---

## Enum Settings

Enum settings are edited as a **structured list of values**, not as raw JSON. Each entry has:

- **Value** — what is actually passed to the container
- **Display** — what the user sees in the dropdown

An **underlying type** (`string` or `numeric`) is chosen for the setting. The editor infers it automatically: if every supplied value parses as a number, `EnumUnderlyingType` is set to `numeric`, otherwise `string`. This drives numeric-aware validation and ordering.

On save, the editor serializes the list into the two persisted columns:

```json
// AllowedValuesJson
["LATEST", "1.21", "1.20"]

// ValueMappingsJson
{ "LATEST": "Latest release", "1.21": "1.21.x", "1.20": "1.20.x" }
```

When the setting is loaded, both columns are parsed back into the value/display pairs, so the JSON is never hand-edited. `GameServerSettingFieldV2` renders the dropdown using the mapping when present, falling back to the raw value as its own label.

Backend validation (`GameServerValidationService`) rejects any submitted value that is not contained in `AllowedValuesJson`.

---

## Server Variable Settings (`servervariable`)

A `servervariable` setting can embed properties of the target `GameServer` using `{Token}` syntax. Each such setting has an **on/off switch** in the Create/Edit Server UI:

- **Off** — the value is treated as a **literal string**; braces are not interpreted.
- **On** — `{Token}` occurrences are replaced at deployment time.

### Supported tokens

`{ServerId}` · `{Name}` · `{ServiceName}` · `{Description}` · `{Status}` · `{GameTypeKey}` · `{RevisionVersionTag}` · `{RevisionImageReference}`

### Storage encoding

The toggle state is encoded into the stored value by `ServerVariableExpander`:

| Stored value | Meaning |
|---|---|
| `@vars:Welcome to {Name}` | Expansion **enabled**; raw text is `Welcome to {Name}` |
| `@literal:@vars:...` | Expansion **disabled**, escaping a value that literally begins with a marker |
| `Welcome to {Name}` | Expansion **disabled**; treated as a literal |

`Decode()` splits the stored value into `(ExpandVariables, RawValue)`; `Encode()` recombines them. This keeps the toggle in a single string column with no schema change and no ambiguity for values that genuinely start with `@vars:`.

`GameServerDeploymentService` and `GameServerSpecBuilder` both call `ServerVariableExpander.Resolve(...)` so the **preview and the actual deployment produce identical values**.

---

## Where Settings Are Edited

| Surface | Component |
|---|---|
| GameType revision settings (definition) | `GameTypeRevisionSettingsEditor.razor` |
| Per-server setting values (override) | `GameServerSettingFieldV2.razor` inside `GameServerEditorV2.razor` |

The revision editor uses a master-detail layout: a settings list on the left, the selected setting's details on the right.

---

## Related Documentation

- **[V2 GameType Assembly Instructions](V2-GameType-Assembly-Instructions.md)** - Building a GameType end to end
- **[V2 GameType Editor Components](V2-GameType-Editor-Components.md)** - Component breakdown
- **[V2 Ports & Web Hosts](V2-Ports-And-WebHosts.md)** - Port settings and mappings
- **[V2 GameServer Lifecycle](V2-GameServer-Lifecycle.md)** - Applying settings to a server
