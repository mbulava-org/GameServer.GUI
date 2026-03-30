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
