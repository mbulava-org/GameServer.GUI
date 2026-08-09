# Database Reorganization Proposal for Docker-Based Game Servers

## Purpose

This document defines the proposed data layout for configuring Docker-based game servers.

The design separates four concerns:

1. **Game type catalog** - logical game identity and curated metadata
2. **Game type revisions** - versioned deployable definitions
3. **Game server instances** - desired deployment and update configuration for a specific server
4. **On-demand runtime correlation** - lightweight linkage between database state and Docker state

The core rule is:

- `GameType` owns the fixed Docker image and revision decisions
- `GameTypeRevision` owns the tagged deployable template, including Web Hosts
- `GameServer` defines server-specific deployment intent for the Docker service
- the Primary Service owns load balancer provider configuration
- Docker remains the source for detailed live runtime information

---

## Core Design Principles

### 1. `GameType` is the curated catalog entry
`GameType` represents a logical game family such as `minecraft` or `valheim`.

It should own:
- logical identity
- user-facing metadata
- a fixed Docker image reference
- the relationship to published revisions

It should not directly represent a running service.

### 2. `GameTypeRevision` is the frozen deployable template
A revision captures the exact deployable schema for a game type at a point in time.

It should own:
- version tag and image digest for the fixed parent image
- ports
- volumes
- setting definitions and metadata
- setting port mappings
- Web Host definitions

This prevents later edits from silently changing already-configured servers.

### 3. `GameServer` is deployment intent
`GameServer` should store the desired server-specific deployment configuration.

It should own:
- the selected `GameTypeRevision`
- desired server-specific settings
- service identity fields

It should **not** store a full copy of image inspection data.

It should also avoid storing deployment details that are fully computable from `GameTypeRevision`.

### 4. Image scan data belongs near `GameType`
When editing a `GameType`, the application should scan image/tag metadata.

This supports:
- detecting when a tag SHA changed
- detecting when a new tag was added
- deciding whether a new `GameTypeRevision` is needed

This does **not** require storing all Docker image inspection data per `GameServer` or persisting a separate image-tag table in V2.

### 5. Web Hosts belong to `GameTypeRevision`
Web Hosts are part of the revisioned template.

They are usually enabled or disabled per `GameServer` instance based on one or more environment variables.

For this design:
- `WebHost` and `Redirect` are the same domain concept for now
- load balancer provider configuration stays in the Primary Service
- the current provider is Traefik using label-driven routing on a Swarm manager

### 6. Docker labels are for identity and correlation
Labels should be used to reliably match Docker services and containers back to the database.

Labels should **not** be used as the primary store for full server configuration.

---

## Recommended Logical Model

## A. Catalog Layer

### `GameTypes`
One row per logical game type.

| Column | Data Type | Nullability | Key / Constraint | Description |
|---|---|---|---|---|
| `Id` | `int` | Not Null | Primary Key | Internal identifier for the catalog entry. |
| `Key` | `string` | Not Null | Unique | Stable logical identifier such as `minecraft` or `valheim`. |
| `DisplayName` | `string` | Not Null |  | User-facing name shown in editors and selection UIs. |
| `Description` | `string` | Nullable |  | Human-readable summary of the game type. |
| `ImageReference` | `string` | Not Null |  | Fixed Docker image reference for this game type; changing this should create a new `GameType` instead of mutating an existing one. |
| `ThumbnailUrl` | `string` | Nullable |  | Optional image used for catalog display in the UI. |
| `DocumentationUrl` | `string` | Nullable |  | Optional reference to image or game setup documentation. |
| `IsActive` | `bool` | Not Null |  | Indicates whether the game type should be available for new server creation. |
| `CurrentRevisionId` | `int` | Nullable | Foreign Key -> `GameTypeRevisions.Id` | Points to the currently recommended published revision for this game type. |
| `CreatedAt` | `datetime` | Not Null |  | Audit timestamp for when the catalog entry was created. |
| `UpdatedAt` | `datetime` | Not Null |  | Audit timestamp for the last metadata change to the catalog entry. |

---

## B. Revisioned Deployable Definition Layer

All deployable definition tables should attach to `GameTypeRevisionId`, not directly to `GameTypeId`.

### `GameTypeRevisions`
One row per published version of a game type definition.

| Column | Data Type | Nullability | Key / Constraint | Description |
|---|---|---|---|---|
| `Id` | `int` | Not Null | Primary Key | Internal identifier for the frozen deployable revision. |
| `GameTypeId` | `int` | Not Null | Foreign Key -> `GameTypes.Id` | Associates the revision to the fixed-image game type it belongs to. |
| `VersionTag` | `string` | Not Null | Unique with `GameTypeId` | Docker image tag represented by this revision for the parent `GameType.ImageReference`. |
| `ImageDigest` | `string` | Nullable |  | Optional digest captured for the tagged image when the revision was created or published. |
| `EnableTTY` | `bool` | Not Null |  | Indicates whether the deployed service should enable TTY for this revision. |
| `Notes` | `string` | Nullable |  | Optional release or authoring notes describing what changed in this revision. |
| `IsPublished` | `bool` | Not Null |  | Indicates whether this revision is available for use by server instances. |
| `CreatedAt` | `datetime` | Not Null |  | Audit timestamp for when the revision was created. |

### `GameTypePorts`
Curated container port definitions for a revision.

| Column | Data Type | Nullability | Key / Constraint | Description |
|---|---|---|---|---|
| `Id` | `int` | Not Null | Primary Key | Internal identifier for the revisioned port definition. |
| `GameTypeRevisionId` | `int` | Not Null | Foreign Key -> `GameTypeRevisions.Id` | Associates the port definition with the frozen revision that owns it. |
| `ContainerPort` | `int` | Not Null |  | Container port exposed by the image or expected by the deployable template. |
| `Protocol` | `string` | Not Null |  | Transport protocol for this port, typically `tcp` or `udp`. |
| `AdvertisedPort` | `bool` | Not Null |  | Marks the single user-facing primary connection port for the revision. |
| `Description` | `string` | Nullable |  | Human-readable purpose of the port such as game, query, or admin access. |
| `DisplayOrder` | `int` | Not Null |  | UI ordering for port presentation and generated summaries. |

### `GameTypeVolumes`
Curated volume definitions for a revision.

| Column | Data Type | Nullability | Key / Constraint | Description |
|---|---|---|---|---|
| `Id` | `int` | Not Null | Primary Key | Internal identifier for the revisioned volume definition. |
| `GameTypeRevisionId` | `int` | Not Null | Foreign Key -> `GameTypeRevisions.Id` | Associates the volume with the revision that defines it. |
| `Source` | `string` | Not Null |  | Logical or authored source key used by the Primary Service to resolve storage bindings. |
| `Description` | `string` | Nullable |  | Human-readable purpose of the volume such as config, world, or mods. |
| `DisplayOrder` | `int` | Not Null |  | UI ordering for volume presentation. |
| `Usage` | `string` | Not Null |  | Semantic classification used by deployment logic to determine how the binding should be generated. |

### `GameTypeSettingDefinitions`
Curated setting definitions for a revision.

| Column | Data Type | Nullability | Key / Constraint | Description |
|---|---|---|---|---|
| `Id` | `int` | Not Null | Primary Key | Internal identifier for the setting definition. |
| `GameTypeRevisionId` | `int` | Not Null | Foreign Key -> `GameTypeRevisions.Id` | Associates the setting definition with the revision that owns it. |
| `SettingKey` | `string` | Not Null | Unique with `GameTypeRevisionId` | Environment variable or authored setting key consumed during deployment. |
| `DefaultValue` | `string` | Nullable |  | Default value used when a server does not provide an override. |
| `Description` | `string` | Nullable |  | Human-readable explanation of what the setting controls. |
| `DisplayOrder` | `int` | Not Null |  | UI ordering for editors and review screens. |

### `GameTypeSettingMetadata`
UI and validation metadata for one setting definition.

| Column | Data Type | Nullability | Key / Constraint | Description |
|---|---|---|---|---|
| `Id` | `int` | Not Null | Primary Key | Internal identifier for the setting metadata row. |
| `GameTypeSettingDefinitionId` | `int` | Not Null | Foreign Key -> `GameTypeSettingDefinitions.Id`; Unique | One-to-one metadata row describing how a setting should be rendered and interpreted. |
| `DataType` | `string` | Not Null |  | Semantic type used by the UI and backend interpretation, such as `string`, `number`, `boolean`, `enum`, `port`, `list`, or `timezone`. |
| `Category` | `string` | Nullable |  | Optional UI grouping label used to organize settings in editors. |
| `IsRequired` | `bool` | Not Null |  | Indicates whether the setting must be provided before deployment. |
| `CannotBeEmpty` | `bool` | Not Null |  | Indicates whether the setting may be present but blank. |
| `Placeholder` | `string` | Nullable |  | UI hint shown when the setting value is empty. |
| `ValidationPattern` | `string` | Nullable |  | Optional regex or pattern used to validate the setting value. |
| `ValidationMessage` | `string` | Nullable |  | Error text presented when the validation pattern fails. |
| `AutoAllocatePort` | `bool` | Not Null |  | Indicates whether backend services may automatically assign published ports for this setting. |
| `ValidateRelatedPortsAvailability` | `bool` | Not Null |  | Indicates whether related port mappings should be checked together for availability before deployment or update. |
| `AllowedValuesJson` | `string` | Nullable |  | Serialized list of allowed values used to render enum-like pickers. |
| `ValueMappingsJson` | `string` | Nullable |  | Serialized key-to-label mappings used to display friendly values in the UI. |

### `GameTypeSettingPortMappings`
Port mapping rules attached to setting metadata.

| Column | Data Type | Nullability | Key / Constraint | Description |
|---|---|---|---|---|
| `Id` | `int` | Not Null | Primary Key | Internal identifier for the port mapping rule. |
| `GameTypeSettingMetadataId` | `int` | Not Null | Foreign Key -> `GameTypeSettingMetadata.Id` | Associates the mapping rule with the port-type setting metadata that owns it. |
| `MappingRole` | `string` or `int` | Not Null |  | Distinguishes the primary mapping from related mappings generated from the same setting. |
| `RelationType` | `string` or `int` | Not Null |  | Defines how the target port is derived, such as direct, offset, fixed, or multiplier. |
| `TargetContainerPort` | `int` | Not Null |  | Container port definition that this mapping rule controls or derives. |
| `TargetProtocol` | `string` | Not Null |  | Protocol associated with the target container port. |
| `CalculationValue` | `int` | Nullable |  | Single calculation operand interpreted according to `RelationType` and `MappingRole`. |
| `Description` | `string` | Nullable |  | Human-readable summary of what the derived or primary mapping is intended to represent. |
| `IsRequired` | `bool` | Not Null |  | Indicates whether this derived mapping must exist for the setting to be considered valid. |
| `DisplayOrder` | `int` | Not Null |  | UI ordering for displaying related mappings. |

### `GameTypeWebHosts`
Revisioned Web Host definitions used to generate load balancer labels.

| Column | Data Type | Nullability | Key / Constraint | Description |
|---|---|---|---|---|
| `Id` | `int` | Not Null | Primary Key | Internal identifier for the authored Web Host definition. |
| `GameTypeRevisionId` | `int` | Not Null | Foreign Key -> `GameTypeRevisions.Id` | Associates the Web Host definition with the revision that owns it. |
| `Name` | `string` | Not Null |  | Human-readable name of the web-accessible endpoint such as admin UI or map view. |
| `Description` | `string` | Nullable |  | Summary of the endpoint and what it exposes. |
| `PathSegment` | `string` | Nullable |  | Static or templated path segment used when generating route paths. |
| `ContainerPort` | `int` | Nullable |  | Static container port used when the endpoint does not resolve its port from a setting. |
| `ContainerPortVariable` | `string` | Nullable |  | Setting key used to resolve the effective container port dynamically from `GameServerSettings`. |
| `EnabledWhen` | `string` | Nullable |  | Conditional expression used to determine whether the endpoint should be generated for a server. |
| `DisplayOrder` | `int` | Not Null |  | UI and generation ordering for Web Host processing. |

---

## C. Server Instance Layer

### `GameServers`
The main persisted server instance.

| Column | Data Type | Nullability | Key / Constraint | Description |
|---|---|---|---|---|
| `Id` | `int` | Not Null | Primary Key | Internal identifier for the persisted server instance. |
| `ServerId` | `string` | Not Null | Unique | Stable external identifier used for API access, service labels, and runtime correlation. |
| `Name` | `string` | Not Null |  | User-facing server name. |
| `Description` | `string` | Nullable |  | Optional description of the server instance. |
| `GameTypeRevisionId` | `int` | Not Null | Foreign Key -> `GameTypeRevisions.Id` | Single schema reference describing the frozen deployable template for the server. |
| `ServiceName` | `string` | Not Null |  | Docker Swarm service name or generated identity used during deployment and updates. |
| `Status` | `string` | Not Null |  | Desired or observed deployment status for the server instance. |
| `CreatedAt` | `datetime` | Not Null |  | Audit timestamp for when the server record was created. |
| `UpdatedAt` | `datetime` | Not Null |  | Audit timestamp for the last server metadata or settings update. |
| `LastDeployedAt` | `datetime` | Nullable |  | Timestamp for the last deploy or update operation. |
| `LastSeenAt` | `datetime` | Nullable |  | Timestamp for the last observed runtime correlation or heartbeat-derived state. |
| `IsDeleted` | `bool` | Not Null |  | Soft-delete marker used to hide or retire servers without immediately removing audit history. |

### `GameServerSettings`
Desired per-server setting values.

| Column | Data Type | Nullability | Key / Constraint | Description |
|---|---|---|---|---|
| `Id` | `int` | Not Null | Primary Key | Internal identifier for the server-specific setting row. |
| `GameServerId` | `int` | Not Null | Foreign Key -> `GameServers.Id` | Associates the setting override with the server instance that owns it. |
| `SettingKey` | `string` | Not Null | Unique with `GameServerId` | Setting or environment variable key being overridden for this server. |
| `Value` | `string` | Nullable |  | Desired value supplied for deployment; list-like values may remain newline-separated when required by existing behavior. |

### Derived Web Host State
Resolved Web Host state should be derived from:

- `GameTypeWebHosts` on the selected `GameTypeRevision`
- `GameServerSettings`

Purpose:
- keep Web Host behavior deterministic without storing duplicate per-server resolution data

Recommendation:
- resolve enabled hosts, ports, and paths on demand from revision definitions plus server settings
- do not persist `GameServerWebHosts` in V2

---

## D. Primary Service Configuration Boundary

Load balancer provider configuration belongs in the Primary Service, not in `GameServer` rows.

That includes:
- provider type such as `Traefik`
- load balancer network
- base domain or URL pattern
- middleware and auth defaults
- provider-specific label generation behavior

Current expectation:
- Traefik is attached to a Swarm manager
- Traefik detects service label changes automatically
- the Primary Service generates labels during deploy or update

Recommendation:
- keep this configuration in application settings first
- add a database table only if provider settings must become user-editable later

---

## What Should Be Detected from Docker Images

Image scans should capture technical facts useful for authoring and revision detection.

### Detect and optionally cache
- tag
- digest
- exposed ports
- declared volumes
- default environment variables

### Detect but usually do not persist per server
- entrypoint
- cmd
- labels
- working directory
- user
- os
- architecture

These may be read on demand from Docker when needed.

---

## What Should Stay Curated in the Database

These should remain authored in the DB, not inferred from image metadata alone:

- logical game type key
- display name
- description
- settings schema
- setting metadata
- validation rules
- port semantics
- volume semantics
- TTY behavior
- Web Host definitions
- revision boundaries

Reason:
Different Docker images for the same game often use different environment variable names, labels, ports, and conventions.

---

## What `GameServer` Should and Should Not Store

## `GameServer` should store
- the selected game type revision
- desired settings
- service name
- deployment status fields

## `GameServer` should not store
- full image inspection payloads
- full image labels payloads
- full entrypoint or cmd metadata cache
- OS or architecture metadata unless specifically needed for deployment logic

Reason:
That data belongs either:
- in `GameType` / `GameTypeRevision`, or
- in Docker itself for live inspection

---

## Full Proposed Physical Schema

The table definitions above are the proposed physical schema. Each table is shown with:

- column name
- data type
- nullability
- primary, unique, and foreign key intent
- a description of what the data defines or is intended to generate

## Relationship Summary

- `GameTypes` 1 -> many `GameTypeRevisions`
- `GameTypeRevisions` 1 -> many `GameTypePorts`
- `GameTypeRevisions` 1 -> many `GameTypeVolumes`
- `GameTypeRevisions` 1 -> many `GameTypeSettingDefinitions`
- `GameTypeRevisions` 1 -> many `GameTypeWebHosts`
- `GameTypeSettingDefinitions` 1 -> 0..1 `GameTypeSettingMetadata`
- `GameTypeSettingMetadata` 1 -> many `GameTypeSettingPortMappings`
- `GameTypeRevisions` 1 -> many `GameServers`
- `GameServers` 1 -> many `GameServerSettings`

---

## Label Strategy

Labels should remain minimal and stable.

### Required labels
- `ServiceLabels.Managed`
- `ServiceLabels.ServerId`
- `ServiceLabels.GameType`

### Recommended additional labels
- `gameserver.docker.gametypeRevision`
- `gameserver.docker.image`
- `gameserver.docker.imageDigest`
- `gameserver.docker.schemaVersion`

### Why these labels matter
- `Managed` confirms ownership
- `ServerId` links Docker objects back to `GameServers`
- `GameType` enables fast correlation
- `GameTypeRevision` links runtime state to the frozen deployable schema
- `Image` and `ImageDigest` help validate deployed identity

### What not to store in labels
- full settings payloads
- full port mapping payloads
- full volume mapping payloads
- large runtime snapshots
- UI metadata

---

## Runtime Correlation Flow

Recommended correlation process:

1. verify `ServiceLabels.Managed == ServiceLabels.ManagedValue`
2. read `ServiceLabels.ServerId`
3. load the `GameServer` row
4. compare revision label vs `GameServer.GameTypeRevisionId`
5. compare runtime image ref and digest vs the image reference on `GameType` plus the tag and digest on `GameTypeRevision`
6. if mismatched, mark drift instead of guessing

---

## Revision Decision Flow for `GameType`

When editing a `GameType`:

1. read configured image and tag choices
2. scan Docker for the current digest
3. compare the scanned digest against the published revisions for that `GameType`
4. if digest changed or a new tag appears, compare relevant detected fields
5. if deploy-shape changed, recommend creating a new `GameTypeRevision`

Examples of deploy-shape changes:
- exposed ports changed
- declared volumes changed
- default environment variables changed in a meaningful way
- digest changed for a selected tag
- Web Host resolution assumptions changed because referenced port variables changed meaningfully

---

## Suggested Implementation Order

1. add persisted `GameServers` and `GameServerSettings`
2. add `GameTypeRevisions`
3. move ports, volumes, settings, and Web Hosts under revisions
4. keep image scan and tag comparison logic near `GameType` editing and revision publishing
5. extend labels with revision and image identity fields

---

## Recommendation Summary

### Detect near `GameType`
- image digest for candidate tags
- tag presence
- exposed ports
- declared volumes
- default env values

### Keep in `GameTypeRevision`
- ports
- volumes
- setting definitions
- setting metadata
- setting port mappings
- Web Host definitions

### Keep in `GameServer`
- deployment intent
- resolved settings
- service identity and status

### Keep in Primary Service config
- load balancer provider selection
- Traefik and future provider settings
- label generation strategy

### Keep in Docker or on-demand runtime inspection
- detailed live runtime metadata
- full image inspect payload
- container-level live state

This keeps the system aligned with the intended model:

- `GameType` owns the fixed Docker image and revision decisions
- `GameTypeRevision` owns the tagged deployable template including Web Hosts
- `GameServer` owns deployment intent
- the Primary Service owns load balancer provider configuration
- Docker owns detailed live runtime state
