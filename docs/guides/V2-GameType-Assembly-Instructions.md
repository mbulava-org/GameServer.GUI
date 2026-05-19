# V2 GameType Assembly Instructions

## Purpose
This guide explains how to assemble a V2 GameType from scratch and how to validate each step against the current editor behavior.

Use this as the documentation-first workflow for reviewing functionality and proposing future adjustments.

## Core model split
A V2 GameType is assembled from two layers:

### GameType
Owns the fixed catalog information:
- key
- display name
- description
- type
- thumbnail URL
- documentation URL
- active flag

### GameTypeRevision
Owns the deployable template:
- Docker image reference
- version tag
- image digest
- TTY flag
- revision notes
- published flag
- ports
- volumes
- setting definitions
- Web Host rules

## Assembly workflow

### 1. Create the GameType shell
In the `Basic` tab, define the catalog identity:
- choose a stable `Key`
- set `Display Name`
- set `Type` to `docker`
- optionally add thumbnail/docs/description
- save the GameType before attempting revision work

#### Validation checklist
- key, display name, and type are required
- existing GameTypes should keep the key read-only
- the page should navigate to `/gametypes-v2/{key}` after creating a new GameType

### 2. Create a revision draft
In the `Revisions` tab:
- use `New Draft` beside the `Active Revision` selector
- supply a `Docker Image Reference`
- supply a `Version Tag`
- optionally set `Image Digest`
- decide whether `Enable TTY` should be on
- optionally add revision notes
- leave `Published` off until the draft is ready

#### Validation checklist
- a new unsaved draft item should appear in the `Active Revision` selector and become selected immediately
- a new unsaved draft row should appear in the revision list
- the draft row should update as the version tag changes
- creating a draft should not be blocked by missing `Version Tag` or missing ports because those inputs are completed across different tabs
- save and publish actions should remain blocked until required revision data such as `Docker Image Reference`, `Version Tag`, and at least one port has been provided

### 3. Define ports
In the `Ports` tab:
- add the exposed container ports needed by the revision
- choose `tcp` or `udp`
- mark exactly one port as the advertised connection port
- add descriptions for clarity where useful

#### Validation checklist
- at least one port is required
- exactly one port must be advertised
- duplicate `containerPort/protocol` pairs should be rejected by validation

## 4. Define volumes
In the `Volumes` tab:
- add each persisted path required by the image
- set the `Usage` classification
- use the description to describe the container path or purpose

#### Validation checklist
- empty volume lists are allowed but should be reviewed intentionally
- order should reflect the intended presentation of persisted paths

## 5. Define settings
In the `Settings` tab:
- add each revision-owned environment variable definition
- set default values
- categorize settings for grouping
- choose the correct data type
- mark required or non-empty settings as needed
- define port mapping rules when a setting drives port behavior

### Port mapping rule
- every setting port mapping must target a `port/protocol` that already exists in the `Ports` tab
- the editor should not allow creating a mapping until at least one draft port exists
- if a draft port is removed later, validation should flag any orphaned mapping targets

### Recommended editing pattern
- use categories to keep the left list grouped logically
- use the left list to navigate between settings
- use the detail pane to edit one setting completely before moving on

#### Validation checklist
- setting keys must be unique
- required + non-empty settings should not have empty defaults
- port mappings must target ports that exist in the draft
- mapping targets should be selected from the known draft `port/protocol` list rather than typed free-form

## 6. Define Web Host rules
In the `Web Hosts` tab:
- add each Web UI or redirectable endpoint
- define either a static container port or a port variable
- never define both at the same time
- use `Enabled When` for conditional exposure

#### Validation checklist
- each Web Host must specify exactly one of:
  - `ContainerPort`
  - `ContainerPortVariable`
- name and path segment should remain human-readable and predictable

## 7. Use detection when available
In the `Detection` tab:
- this tab appears before `Basic` and is only enabled when the GameType `Type` is `docker`
- provide a Docker image reference
- optionally provide a version tag to scan
- run detection against the provided image identity
- review detected ports, settings, and volumes
- review inferred setting-to-port mapping counts in detected settings
- detection should automatically seed the current revision draft
- if the detected image/tag does not already exist as a revision, detection should switch to a new draft for it

### Recommended order
1. enter the image reference and optional tag
2. run `Detect Settings`
3. review the comparison against the selected revision when one exists
4. fine-tune the imported ports/settings/volumes if needed
5. re-review the draft before save

### Detection expectations for port mappings
- if a detected setting looks like a port setting and its default value matches an exposed port, detection should infer a direct mapping candidate
- if a primary port setting matches multiple exposed ports, detection should infer one primary mapping plus related mappings for the additional exposed ports
- inferred mappings are suggestions and should still be reviewed before save or publish

#### Validation checklist
- detection should populate the revision draft with the detected image identity and inferred metadata
- applying settings should preserve existing metadata where possible
- comparison guidance should explain whether ports, settings, or volumes changed
- the detection view should expose inferred mapping suggestions clearly enough to review before applying settings

## 8. Review before save or publish
In the `Review` tab:
- inspect draft summary values
- inspect counts of ports, volumes, settings, and Web Hosts
- read warnings and validation issues
- inspect the draft diff against the selected revision
- use the review summary as the final documentation-backed checkpoint before save or publish

#### Validation checklist
- review content should reflect the live in-memory draft
- draft differences should change as edits are made in other tabs

## 9. Save the revision
Back in `Revisions`:
- click `Save Revision`
- confirm the revision becomes a persisted row
- verify the unsaved draft row disappears after reload

#### Validation checklist
- saved revision should appear in the revision grid
- the page should reselect the saved revision after reload

## 10. Publish or set current
When the draft is ready:
- use `Set Current` to change the active revision
- use `Publish + Current` when the revision is production-ready

#### Validation checklist
- only persisted revisions can be made current
- publishing should leave the revision list and summary consistent after reload

## Documentation-first change workflow
When validating or proposing changes:
1. update this guide or the component guide first
2. identify which tab or component behavior should change
3. apply the code change
4. update the related test coverage
5. re-read the docs to ensure they still describe the actual behavior

## Recommended future validation passes
- extract and document the `Detection` tab as its own component
- extract and document the `Review` tab as its own component
- add targeted component tests for ports, volumes, and Web Host editors
