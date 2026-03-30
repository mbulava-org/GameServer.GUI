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

Suggested fields:
- `Id`
- `Key`
- `DisplayName`
- `Description`
- `ImageReference`
- `ThumbnailUrl`
- `DocumentationUrl`
- `IsActive`
- `CurrentRevisionId` nullable
- `CreatedAt`
- `UpdatedAt`

Notes:
- `Key` is the stable logical identifier
- `ImageReference` is the fixed Docker image for this game type
- if a different Docker image is needed, a new `GameType` should be created rather than changing the image on an existing one

---

## B. Revisioned Deployable Definition Layer

All deployable definition tables should attach to `GameTypeRevisionId`, not directly to `GameTypeId`.

### `GameTypeRevisions`
One row per published version of a game type definition.

Suggested fields:
- `Id`
- `GameTypeId`
- `VersionTag`
- `ImageDigest` nullable
- `EnableTTY`
- `Notes` nullable
- `IsPublished`
- `CreatedAt`

Purpose:
- freeze a deployable template at a point in time
- represent an updated version tag for the fixed image owned by the parent `GameType`

Notes:
- for Docker-container game types, a new revision corresponds to a new or updated image tag
- changing the Docker image itself should create a new `GameType`, not a new revision

### `GameTypePorts`
Curated container port definitions for a revision.

Suggested fields:
- `Id`
- `GameTypeRevisionId`
- `ContainerPort`
- `Protocol`
- `AdvertisedPort`
- `Description`
- `DisplayOrder`

Notes:
- `AdvertisedPort` is a boolean flag
- each `GameTypeRevision` should have exactly one advertised port
- this marks the primary connection port users should connect to

### `GameTypeVolumes`
Curated volume definitions for a revision.

Suggested fields:
- `Id`
- `GameTypeRevisionId`
- `Source`
- `Description`
- `DisplayOrder`
- `Usage`

`Usage` examples:
- `config`
- `world`
- `mods`
- `gamefiles`

Notes:
- 'Usage' value will be handled within the Primary Service to determine how each volume binding is resolved and deployed

### `GameTypeSettingDefinitions`
Curated setting definitions for a revision.

Suggested fields:
- `Id`
- `GameTypeRevisionId`
- `SettingKey`
- `DefaultValue`
- `Description`
- `DisplayOrder`

### `GameTypeSettingMetadata`
UI and validation metadata for one setting definition.

Suggested fields:
- `Id`
- `GameTypeSettingDefinitionId`
- `DataType`
- `Category`
- `IsRequired`
- `CannotBeEmpty`
- `Placeholder`
- `ValidationPattern`
- `ValidationMessage`
- `AutoAllocatePort`
- `ValidateRelatedPortsAvailability`
- `AllowedValuesJson`
- `ValueMappingsJson`

Notes:
- port relationship/link data should not be split between metadata and a separate relationship model
- metadata should describe the setting itself, while port mapping rules should live in their own child table

### `GameTypeSettingPortMappings`
Port mapping rules attached to setting metadata.

Suggested fields:
- `Id`
- `GameTypeSettingMetadataId`
- `MappingRole`
- `RelationType`
- `TargetContainerPort`
- `TargetProtocol`
- `CalculationValue`
- `Description`
- `IsRequired`
- `DisplayOrder`

Notes:
- `MappingRole` identifies whether the row is the primary mapping or a related mapping
- `RelationType` determines how `CalculationValue` is interpreted
- `CalculationValue` replaces separate `OffsetValue`, `FixedValue`, and future multiplier-specific columns
- a direct primary mapping can use a null or zero `CalculationValue`

### `GameTypeWebHosts`
Revisioned Web Host definitions used to generate load balancer labels.

Suggested fields:
- `Id`
- `GameTypeRevisionId`
- `Name`
- `Description`
- `PathSegment`
- `ContainerPort` nullable
- `ContainerPortVariable` nullable
- `EnabledWhen` nullable
- `DisplayOrder`

Purpose:
- define revisioned reverse proxy exposure rules for the game type
- support path-based routing such as `/game-{serverId}` and subpaths
- support conditional enablement through server settings or environment variables

Notes:
- `ContainerPortVariable` supports dynamic port resolution from settings
- `EnabledWhen` captures the condition expression currently used by the resolver
- `PathSegment` can be used for static path segments or as a template with variables for dynamic paths
- `ContainerPort` is used when the exposed port is static and does not depend on server-specific settings

---

## C. Server Instance Layer

### `GameServers`
The main persisted server instance.

Suggested fields:
- `Id`
- `ServerId`
- `Name`
- `Description`
- `GameTypeRevisionId`
- `ServiceName`
- `Status`
- `CreatedAt`
- `UpdatedAt`
- `LastDeployedAt` nullable
- `LastSeenAt` nullable
- `IsDeleted` optional

Purpose:
- define how the Docker service should be deployed or updated
- store only server-specific deployment state that cannot be derived from `GameTypeRevision`

Notes:
- `GameTypeRevisionId` is the single schema reference for deployment shape
- `GameType`, image reference, version tag, and image digest should be derived through the selected revision
- `GameServers` should not duplicate data already owned by `GameType` and `GameTypeRevision`

### `GameServerSettings`
Desired per-server setting values.

Suggested fields:
- `Id`
- `GameServerId`
- `SettingKey`
- `Value`

Notes:
- this aligns with `Server.Settings`
- list-like settings can continue to be stored as newline-separated strings where required

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

## 1. `GameTypes`
| Column | Type | Notes |
|---|---|---|
| `Id` | int PK | Internal key |
| `Key` | string unique | Stable logical key |
| `DisplayName` | string | User-facing name |
| `Description` | string nullable | Summary |
| `ImageReference` | string | Fixed Docker image for this game type |
| `ThumbnailUrl` | string nullable | Optional |
| `DocumentationUrl` | string nullable | Optional |
| `IsActive` | bool | Active or inactive |
| `CurrentRevisionId` | int nullable FK | Latest published revision |
| `CreatedAt` | datetime | Audit |
| `UpdatedAt` | datetime | Audit |

## 2. `GameTypeRevisions`
| Column | Type | Notes |
|---|---|---|
| `Id` | int PK | Revision key |
| `GameTypeId` | int FK | Parent game type |
| `VersionTag` | string | Docker tag for this revision |
| `ImageDigest` | string nullable | Known digest for the tagged image |
| `EnableTTY` | bool | Runtime behavior |
| `Notes` | string nullable | Release notes |
| `IsPublished` | bool | Publish state |
| `CreatedAt` | datetime | Audit |

## 3. `GameTypePorts`
| Column | Type | Notes |
|---|---|---|
| `Id` | int PK | Row key |
| `GameTypeRevisionId` | int FK | Parent revision |
| `ContainerPort` | int | Container port |
| `Protocol` | string | `tcp` or `udp` |
| `AdvertisedPort` | bool | True for the single user-facing connection port |
| `Description` | string nullable | Meaning |
| `DisplayOrder` | int | UI ordering |

## 4. `GameTypeVolumes`
| Column | Type | Notes |
|---|---|---|
| `Id` | int PK | Row key |
| `GameTypeRevisionId` | int FK | Parent revision |
| `Source` | string | Logical or default source |
| `Description` | string nullable | Meaning |
| `DisplayOrder` | int | UI ordering |
| `Usage` | string | `config`, `world`, `mods`, `gamefiles` |

## 5. `GameTypeSettingDefinitions`
| Column | Type | Notes |
|---|---|---|
| `Id` | int PK | Row key |
| `GameTypeRevisionId` | int FK | Parent revision |
| `SettingKey` | string | Env or setting key |
| `DefaultValue` | string nullable | Default value |
| `Description` | string nullable | Summary |
| `DisplayOrder` | int | UI ordering |

## 6. `GameTypeSettingMetadata`
| Column | Type | Notes |
|---|---|---|
| `Id` | int PK | Row key |
| `GameTypeSettingDefinitionId` | int FK | Parent setting |
| `DataType` | string | `string`, `number`, `boolean`, `enum`, `port` |
| `Category` | string nullable | UI grouping |
| `IsRequired` | bool | Required flag |
| `CannotBeEmpty` | bool | Validation flag |
| `Placeholder` | string nullable | UI hint |
| `ValidationPattern` | string nullable | Regex or pattern |
| `ValidationMessage` | string nullable | UI message |
| `AutoAllocatePort` | bool | Allocation flag |
| `ValidateRelatedPortsAvailability` | bool | Validation flag |
| `AllowedValuesJson` | string nullable | Enum options |
| `ValueMappingsJson` | string nullable | Enum labels |

## 7. `GameTypeSettingPortMappings`
| Column | Type | Notes |
|---|---|---|
| `Id` | int PK | Row key |
| `GameTypeSettingMetadataId` | int FK | Parent metadata |
| `MappingRole` | int or string | Primary or related mapping |
| `RelationType` | int or string | Direct, offset, fixed, multiplier |
| `TargetContainerPort` | int | Target port |
| `TargetProtocol` | string | `tcp` or `udp` |
| `CalculationValue` | int nullable | Value interpreted by relation type |
| `Description` | string nullable | Summary |
| `IsRequired` | bool | Requirement flag |
| `DisplayOrder` | int | UI ordering |

## 8. `GameTypeWebHosts`
| Column | Type | Notes |
|---|---|---|
| `Id` | int PK | Row key |
| `GameTypeRevisionId` | int FK | Parent revision |
| `Name` | string | Host name |
| `Description` | string nullable | Summary |
| `PathSegment` | string nullable | URL segment |
| `ContainerPort` | int nullable | Static port when not dynamically resolved |
| `ContainerPortVariable` | string nullable | Dynamic port source |
| `EnabledWhen` | string nullable | Condition expression |
| `DisplayOrder` | int | UI and routing priority |

## 9. `GameServers`
| Column | Type | Notes |
|---|---|---|
| `Id` | int PK | Internal key |
| `ServerId` | string unique | External stable ID |
| `Name` | string | Display name |
| `Description` | string nullable | Summary |
| `GameTypeRevisionId` | int FK | Frozen deployable revision |
| `ServiceName` | string | Docker service name |
| `Status` | string | Desired or current status |
| `CreatedAt` | datetime | Audit |
| `UpdatedAt` | datetime | Audit |
| `LastDeployedAt` | datetime nullable | Last deploy |
| `LastSeenAt` | datetime nullable | Last observed |
| `IsDeleted` | bool optional | Soft delete |

## 10. `GameServerSettings`
| Column | Type | Notes |
|---|---|---|
| `Id` | int PK | Row key |
| `GameServerId` | int FK | Parent server |
| `SettingKey` | string | Server setting |
| `Value` | string nullable | Desired value |

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
