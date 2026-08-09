# V2 GameType Editor Components

## Purpose
This guide documents how the V2 GameType editor is assembled in `GameServer.Web`, which component owns each editing surface, and which state remains coordinated by `GameTypeDetailsV2.razor`.

Use this document as the functional reference for validating behavior before manual testing and for driving future code updates from documentation changes.

## Main coordinator
`src/GameServer.Web/Components/Pages/GameTypes/GameTypeDetailsV2.razor`

### Responsibilities
- Loads the V2 GameType detail payload from `GameTypeV2ApiService`
- Owns page-level save operations for the GameType and selected revision draft
- Owns revision draft state shared across tabs
- Owns validation, warnings, detection results, and review summaries
- Composes the tab-level child components

### State owned here
- basic GameType fields such as `keyValue`, `displayName`, and `gameTypeType`
- selected/current revision ids
- the in-progress revision draft fields, including `revisionImageReference`
- detection and comparison results
- save and loading flags
- page-level save validation displayed above the tab set

### Why it remains the coordinator
The page still owns cross-tab state because revision validation, detection application, save orchestration, and review all depend on the same in-memory draft, even though every tab now renders through a dedicated child component.

## Child components

### `GameTypeBasicInfoV2Editor`
`src/GameServer.Web/Components/Pages/GameTypes/Components/V2/GameTypeBasicInfoV2Editor.razor`

#### Purpose
Edits the fixed GameType catalog fields that are not revision-specific.

#### Inputs
- `IsNew`
- two-way bindings for:
  - `KeyValue`
  - `DisplayName`
  - `Type`
  - `ThumbnailUrl`
  - `DocumentationUrl`
  - `Description`
  - `IsActive`

#### Behavior
- disables the key when editing an existing GameType
- keeps field editing local to the basic tab surface
- delegates all persistence back to the page

### `GameTypeRevisionEditor`
`src/GameServer.Web/Components/Pages/GameTypes/Components/V2/GameTypeRevisionEditor.razor`

#### Purpose
Renders the revision draft metadata form and revision list.

#### Inputs
- `RevisionRows`
- `ValidationIssues`
- `Warnings`
- two-way bindings for:
  - `ImageReference`
  - `VersionTag`
  - `ImageDigest`
  - `EnableTTY`
  - `Notes`
  - `IsPublished`
- action callbacks for:
  - set current
  - publish + current
  - edit revision
  - clone revision

#### Behavior
- shows the active unsaved draft row when the page provides one
- keeps revision publish/current actions grouped with the draft metadata form
- does not save directly; it raises callbacks back to the page

### Active revision card in `GameTypeDetailsV2.razor`

#### Purpose
Hosts the selected revision dropdown plus the primary draft actions that affect the whole multi-tab editing flow.

#### Behavior
- shows `New Draft` and `Save Revision` beside the `Active Revision` selector
- inserts and selects an unsaved draft option immediately when `New Draft` is clicked
- keeps page-level save validation visible outside the tab content

### `GameTypeRevisionPortsEditor`
`src/GameServer.Web/Components/Pages/GameTypes/Components/V2/GameTypeRevisionPortsEditor.razor`

#### Purpose
Edits revision-owned port definitions.

#### Inputs
- `Ports`
- `ProtocolOptions`

#### Behavior
- adds new port draft rows
- allows port reordering and deletion
- enforces a single advertised port within the in-memory list
- mutates the shared draft list directly so the main page validation and save logic sees the current values immediately

### `GameTypeRevisionVolumesEditor`
`src/GameServer.Web/Components/Pages/GameTypes/Components/V2/GameTypeRevisionVolumesEditor.razor`

#### Purpose
Edits revision-owned volume definitions.

#### Inputs
- `Volumes`
- `VolumeUsageOptions`

#### Behavior
- adds, removes, and reorders volumes
- edits source, usage, and description in place
- mutates the shared draft list directly

### `GameTypeRevisionSettingsEditor`
`src/GameServer.Web/Components/Pages/GameTypes/Components/V2/GameTypeRevisionSettingsEditor.razor`

#### Purpose
Edits revision-owned setting definitions using the master-detail interaction pattern.

#### Inputs
- `Settings`
- `DataTypeOptions`
- `ProtocolOptions`
- `PortMappingRoleOptions`
- `PortRelationTypeOptions`

#### Behavior
- groups settings by category
- filters by search text
- keeps a selected setting in a details pane
- edits setting metadata and port mappings in place
- adds, removes, and reorders settings
- only allows port mapping targets that already exist in the draft `Ports` list
- disables `Add Port Mapping` until at least one draft `port/protocol` exists
- mutates the shared draft list directly

### `GameTypeRevisionWebHostsEditor`
`src/GameServer.Web/Components/Pages/GameTypes/Components/V2/GameTypeRevisionWebHostsEditor.razor`

#### Purpose
Edits revision-owned Web Host rules.

#### Inputs
- `WebHosts`

#### Behavior
- adds, removes, and reorders Web Host rules
- edits name, path segment, static port, port variable, description, and enabled condition
- mutates the shared draft list directly

### `GameTypeRevisionDetectionEditor`
`src/GameServer.Web/Components/Pages/GameTypes/Components/V2/GameTypeRevisionDetectionEditor.razor`

#### Purpose
Renders the detection scan workflow, detected image metadata, inferred setting mapping counts, and revision comparison guidance.

#### Inputs
- `IsNew`
- `IsDetecting`
- `IsDockerType`
- two-way binding for `DetectionImageReference`
- two-way binding for `DetectionVersionTag`
- `DetectionResult`
- `DetectionComparison`
- callbacks for:
  - detect settings
  - apply all
  - apply identity
  - apply ports
  - apply settings
  - apply volumes

#### Behavior
- lives to the left of `Basic` in the tab order and is only enabled for docker GameTypes
- disables scanning while a scan is in progress, when the GameType has not been created yet, or when no Docker image reference has been supplied
- detects from an explicit image reference plus optional version tag rather than from a fixed shell image
- reuses an existing matching revision when the detected image/tag already exists; otherwise it seeds a new draft revision
- shows detected port, setting, and volume counts
- shows inferred setting mapping counts from detection results
- shows comparison guidance per section when comparison data exists

### `GameTypeRevisionReviewEditor`
`src/GameServer.Web/Components/Pages/GameTypes/Components/V2/GameTypeRevisionReviewEditor.razor`

#### Purpose
Renders the consolidated draft summary, detection status summary, validation output, warnings, and draft diff review.

#### Inputs
- revision summary values
- draft content counts
- `DetectionResult`
- `DetectionComparison`
- `ValidationIssues`
- `Warnings`
- `DraftDifferences`
- `DetailedDraftDifferences`

#### Behavior
- shows a compact summary of the current draft state
- shows detection status and comparison summary when detection has been used
- shows validation issues, warnings, and differences using the live in-memory draft data supplied by the page

## Shared editor models
`src/GameServer.Web/Components/Pages/GameTypes/Components/V2/GameTypeDetailsV2EditorModels.cs`

### Purpose
Provides shared draft models for the V2 editor components so the coordinator page and tab-level components work with the same in-memory types.

### Models currently defined
- `GameTypeRevisionListRow`
- `GameTypeRevisionPortDraft`
- `GameTypeRevisionVolumeDraft`
- `GameTypeRevisionSettingDraft`
- `GameTypeRevisionSettingMetadataDraft`
- `GameTypeRevisionPortMappingDraft`
- `GameTypeRevisionWebHostDraft`

## Remaining page-owned orchestration
The page still owns orchestration-only behavior rather than tab rendering:
- loading and reloading the V2 GameType
- saving and publishing revisions
- validation and warning generation
- applying detection results into the draft
- diff generation against the selected revision

## Composition overview
1. The page loads a V2 GameType.
2. The page materializes a revision draft into shared draft models.
3. Tab components edit the same shared lists/values in memory.
4. The page validates and transforms the draft into API request models.
5. The page saves or publishes through `GameTypeV2ApiService`.
6. The page reloads the persisted GameType and rebuilds the draft state.

## Testing coverage
Current V2 GUI tests live in `tests/GameServer.Web.Tests/Components/GameTypes/V2/` and cover:
- unsaved revision draft visibility on the V2 page
- settings master-detail interaction
- settings add/select behavior
- port editor add/advertised behavior
- detection component rendering and comparison guidance
- review component rendering for summaries and differences

Current V2 detection service tests in `tests/GameServer.Docker.Tests/Services/V2/Detection/` cover:
- direct setting-to-port mapping inference when a detected setting value matches an exposed port
- related multi-port mapping inference when a primary port setting corresponds to multiple exposed ports

Add new component tests alongside this folder when a component gains behavior that should be locked down before manual validation.
