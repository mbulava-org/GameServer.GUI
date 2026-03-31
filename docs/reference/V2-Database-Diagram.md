# V2 Database Diagram

This diagram reflects the intended V2 database layout described in `docs/DATABASE-REORGANIZATION-PROPOSAL.md`.

## Entity Relationship Diagram

```mermaid
erDiagram
    GameTypes {
        int Id PK
        string Key UK
        string DisplayName
        string Description
        string ImageReference
        string ThumbnailUrl
        string DocumentationUrl
        bool IsActive
        int CurrentRevisionId FK
        datetime CreatedAt
        datetime UpdatedAt
    }

    GameTypeRevisions {
        int Id PK
        int GameTypeId FK
        string VersionTag
        string ImageDigest
        bool EnableTTY
        string Notes
        bool IsPublished
        datetime CreatedAt
    }

    GameTypePorts {
        int Id PK
        int GameTypeRevisionId FK
        int ContainerPort
        string Protocol
        bool AdvertisedPort
        string Description
        int DisplayOrder
    }

    GameTypeVolumes {
        int Id PK
        int GameTypeRevisionId FK
        string Source
        string Description
        int DisplayOrder
        string Usage
    }

    GameTypeSettingDefinitions {
        int Id PK
        int GameTypeRevisionId FK
        string SettingKey
        string DefaultValue
        string Description
        int DisplayOrder
    }

    GameTypeSettingMetadata {
        int Id PK
        int GameTypeSettingDefinitionId FK
        string DataType
        string Category
        bool IsRequired
        bool CannotBeEmpty
        string Placeholder
        string ValidationPattern
        string ValidationMessage
        bool AutoAllocatePort
        bool ValidateRelatedPortsAvailability
        string AllowedValuesJson
        string ValueMappingsJson
    }

    GameTypeSettingPortMappings {
        int Id PK
        int GameTypeSettingMetadataId FK
        string MappingRole
        int RelationType
        int TargetContainerPort
        string TargetProtocol
        int CalculationValue
        string Description
        bool IsRequired
        int DisplayOrder
    }

    GameTypeWebHosts {
        int Id PK
        int GameTypeRevisionId FK
        string Name
        string Description
        string PathSegment
        int ContainerPort
        string ContainerPortVariable
        string EnabledWhen
        int DisplayOrder
    }

    GameServers {
        int Id PK
        string ServerId UK
        string Name
        string Description
        int GameTypeRevisionId FK
        string ServiceName
        string Status
        datetime CreatedAt
        datetime UpdatedAt
        datetime LastDeployedAt
        datetime LastSeenAt
        bool IsDeleted
    }

    GameServerSettings {
        int Id PK
        int GameServerId FK
        string SettingKey
        string Value
    }

    GameTypes ||--o{ GameTypeRevisions : has
    GameTypeRevisions ||--o{ GameTypePorts : defines
    GameTypeRevisions ||--o{ GameTypeVolumes : defines
    GameTypeRevisions ||--o{ GameTypeSettingDefinitions : defines
    GameTypeRevisions ||--o{ GameTypeWebHosts : defines
    GameTypeRevisions ||--o{ GameServers : selected_by

    GameTypeSettingDefinitions ||--o| GameTypeSettingMetadata : describes
    GameTypeSettingMetadata ||--o{ GameTypeSettingPortMappings : maps

    GameServers ||--o{ GameServerSettings : configures
    
```

## Table Definitions

### `GameTypes`

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

### `GameTypeRevisions`

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

| Column | Data Type | Nullability | Key / Constraint | Description |
|---|---|---|---|---|
| `Id` | `int` | Not Null | Primary Key | Internal identifier for the revisioned volume definition. |
| `GameTypeRevisionId` | `int` | Not Null | Foreign Key -> `GameTypeRevisions.Id` | Associates the volume with the revision that defines it. |
| `Source` | `string` | Not Null |  | Logical or authored source key used by the Primary Service to resolve storage bindings. |
| `Description` | `string` | Nullable |  | Human-readable purpose of the volume such as config, world, or mods. |
| `DisplayOrder` | `int` | Not Null |  | UI ordering for volume presentation. |
| `Usage` | `string` | Not Null |  | Semantic classification used by deployment logic to determine how the binding should be generated. |

### `GameTypeSettingDefinitions`

| Column | Data Type | Nullability | Key / Constraint | Description |
|---|---|---|---|---|
| `Id` | `int` | Not Null | Primary Key | Internal identifier for the setting definition. |
| `GameTypeRevisionId` | `int` | Not Null | Foreign Key -> `GameTypeRevisions.Id` | Associates the setting definition with the revision that owns it. |
| `SettingKey` | `string` | Not Null | Unique with `GameTypeRevisionId` | Environment variable or authored setting key consumed during deployment. |
| `DefaultValue` | `string` | Nullable |  | Default value used when a server does not provide an override. |
| `Description` | `string` | Nullable |  | Human-readable explanation of what the setting controls. |
| `DisplayOrder` | `int` | Not Null |  | UI ordering for editors and review screens. |

### `GameTypeSettingMetadata`

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

### `GameServers`

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

| Column | Data Type | Nullability | Key / Constraint | Description |
|---|---|---|---|---|
| `Id` | `int` | Not Null | Primary Key | Internal identifier for the server-specific setting row. |
| `GameServerId` | `int` | Not Null | Foreign Key -> `GameServers.Id` | Associates the setting override with the server instance that owns it. |
| `SettingKey` | `string` | Not Null | Unique with `GameServerId` | Setting or environment variable key being overridden for this server. |
| `Value` | `string` | Nullable |  | Desired value supplied for deployment; list-like values may remain newline-separated when required by existing behavior. |

## Notes

- `GameTypes` is the logical catalog root.
- `GameTypes` owns the fixed Docker image reference for the game type.
- `GameTypeRevisions` is the frozen deployable template for a specific image tag of that fixed Docker image.
- `GameServers` stores deployment intent, not full runtime state.
- `GameServers` references `GameTypeRevisionId` and should derive game type and image details through that revision instead of duplicating them.
- Web Host state should be derived from `GameTypeWebHosts` plus `GameServerSettings`, not stored separately in V2.
- `GameTypeSettingPortMappings` stores both primary and related port rules for a setting.
- `CalculationValue` is interpreted according to `RelationType`, instead of using separate offset/fixed/multiplier columns.
- `GameServerPorts`, `GameServerVolumes`, and runtime snapshot tables are intentionally excluded from V2.
- if a different Docker image is needed for the same conceptual game, a new `GameType` should be created instead of changing the image on an existing one.

## Key constraints

- `GameTypes.Key` is unique.
- `GameServers.ServerId` is unique.
- Each `GameTypeRevision` should have exactly one `GameTypePorts` row where `AdvertisedPort = true`.
- `GameTypeSettingDefinitions` should be unique per `GameTypeRevisionId + SettingKey`.
- `GameServerSettings` should be unique per `GameServerId + SettingKey`.
