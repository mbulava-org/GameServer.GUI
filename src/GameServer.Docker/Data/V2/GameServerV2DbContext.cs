using Microsoft.EntityFrameworkCore;

namespace GameServer.Docker.Data.V2;

/// <summary>
/// Dedicated DbContext for the V2 persistence model.
/// </summary>
public class GameServerV2DbContext : DbContext
{
    public GameServerV2DbContext(DbContextOptions<GameServerV2DbContext> options)
        : base(options)
    {
        if (Database.IsSqlite())
        {
            // Enable SQLite performance optimizations
            Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");
            Database.ExecuteSqlRaw("PRAGMA synchronous=NORMAL;");
            Database.ExecuteSqlRaw("PRAGMA cache_size=-64000;");
            Database.ExecuteSqlRaw("PRAGMA temp_store=MEMORY;");
            Database.ExecuteSqlRaw("PRAGMA mmap_size=268435456;");
        }
    }

    public DbSet<GameTypeEntity> GameTypes { get; set; }

    public DbSet<GameTypeRevisionEntity> GameTypeRevisions { get; set; }

    public DbSet<GameTypePortEntity> GameTypePorts { get; set; }

    public DbSet<GameTypeVolumeEntity> GameTypeVolumes { get; set; }

    public DbSet<GameTypeSettingDefinitionEntity> GameTypeSettingDefinitions { get; set; }

    public DbSet<GameTypeSettingMetadataEntity> GameTypeSettingMetadata { get; set; }

    public DbSet<GameTypeSettingPortMappingEntity> GameTypeSettingPortMappings { get; set; }

    public DbSet<GameTypeWebHostEntity> GameTypeWebHosts { get; set; }

    public DbSet<GameServerEntity> GameServers { get; set; }

    public DbSet<GameServerVolumeEntity> GameServerVolumes { get; set; }

    public DbSet<GameServerSettingEntity> GameServerSettings { get; set; }

    public DbSet<MountTypeConfigEntity> MountTypeConfigs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        var isMySql = Database.ProviderName?.Contains("MySql", StringComparison.OrdinalIgnoreCase) == true;
        var isPostgreSql = Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true;

        if (isPostgreSql)
        {
            modelBuilder.HasDefaultSchema("core");
        }

        modelBuilder.Entity<GameTypeEntity>(entity =>
        {
            entity.ToTable("GameTypes");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Key).IsUnique();
            entity.HasIndex(e => e.IsActive);

            entity.Property(e => e.Key).IsRequired().HasMaxLength(100);
            entity.Property(e => e.DisplayName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Type).IsRequired().HasMaxLength(50);
            ConfigureTimestampProperty(entity.Property(e => e.CreatedAt), isMySql);
            ConfigureTimestampProperty(entity.Property(e => e.UpdatedAt), isMySql);

            entity.HasMany(e => e.Revisions)
                .WithOne(e => e.GameType)
                .HasForeignKey(e => e.GameTypeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<GameTypeRevisionEntity>(entity =>
        {
            entity.ToTable("GameTypeRevisions");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.GameTypeId, e.ImageReference, e.VersionTag }).IsUnique();
            entity.Property(e => e.VersionTag).IsRequired().HasMaxLength(100);
            entity.Property(e => e.ImageReference).IsRequired().HasMaxLength(500);
            entity.Property(e => e.ImageDigest).HasMaxLength(250);
            ConfigureTimestampProperty(entity.Property(e => e.CreatedAt), isMySql);

            entity.HasMany(e => e.Ports)
                .WithOne(e => e.GameTypeRevision)
                .HasForeignKey(e => e.GameTypeRevisionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.Volumes)
                .WithOne(e => e.GameTypeRevision)
                .HasForeignKey(e => e.GameTypeRevisionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.SettingDefinitions)
                .WithOne(e => e.GameTypeRevision)
                .HasForeignKey(e => e.GameTypeRevisionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.WebHosts)
                .WithOne(e => e.GameTypeRevision)
                .HasForeignKey(e => e.GameTypeRevisionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.Servers)
                .WithOne(e => e.GameTypeRevision)
                .HasForeignKey(e => e.GameTypeRevisionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<GameTypePortEntity>(entity =>
        {
            entity.ToTable("GameTypePorts", t =>
            {
                t.HasCheckConstraint("CK_GameTypePorts_Protocol", "Protocol IN ('tcp', 'udp')");
                t.HasCheckConstraint("CK_GameTypePorts_Range", "ContainerPort >= 1 AND ContainerPort <= 65535");
            });
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.GameTypeRevisionId);
            entity.HasIndex(e => new { e.GameTypeRevisionId, e.AdvertisedPort });
            entity.Property(e => e.Protocol).IsRequired().HasMaxLength(10);
        });

        modelBuilder.Entity<GameTypeVolumeEntity>(entity =>
        {
            entity.ToTable("GameTypeVolumes");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.GameTypeRevisionId);
            entity.Property(e => e.Source).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Usage).IsRequired().HasMaxLength(100);
            entity.Property(e => e.MountType).HasMaxLength(50);
            entity.Property(e => e.Permissions).HasMaxLength(10);

            // Soft FK to MountTypeConfigs; keep optional so existing data loads.
            entity.HasOne<MountTypeConfigEntity>()
                .WithMany()
                .HasForeignKey(e => e.MountType)
                .HasConstraintName("FK_GameTypeVolumes_MountTypeConfigs_MountType")
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<GameServerVolumeEntity>(entity =>
        {
            entity.ToTable("GameServerVolumes");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.GameServerId);
            entity.HasIndex(e => new { e.GameServerId, e.ContainerPath }).IsUnique();
            entity.Property(e => e.Usage).IsRequired().HasMaxLength(100);
            entity.Property(e => e.ContainerPath).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Source).IsRequired().HasMaxLength(500);
            entity.Property(e => e.MountType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Driver).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Permissions).HasMaxLength(10);
            entity.Property(e => e.InitMode).IsRequired().HasMaxLength(50);
            entity.Property(e => e.SeedSourcePath).HasMaxLength(500);

            entity.HasOne(e => e.GameServer)
                .WithMany(e => e.Volumes)
                .HasForeignKey(e => e.GameServerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<GameTypeSettingDefinitionEntity>(entity =>
        {
            entity.ToTable("GameTypeSettingDefinitions");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.GameTypeRevisionId, e.SettingKey }).IsUnique();
            entity.Property(e => e.SettingKey).IsRequired().HasMaxLength(200);

            entity.HasOne(e => e.Metadata)
                .WithOne(e => e.GameTypeSettingDefinition)
                .HasForeignKey<GameTypeSettingMetadataEntity>(e => e.GameTypeSettingDefinitionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<GameTypeSettingMetadataEntity>(entity =>
        {
            entity.ToTable("GameTypeSettingMetadata");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.GameTypeSettingDefinitionId).IsUnique();
            entity.Property(e => e.DataType).HasMaxLength(50);
            entity.Property(e => e.Category).HasMaxLength(100);

            entity.HasMany(e => e.PortMappings)
                .WithOne(e => e.GameTypeSettingMetadata)
                .HasForeignKey(e => e.GameTypeSettingMetadataId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<GameTypeSettingPortMappingEntity>(entity =>
        {
            entity.ToTable("GameTypeSettingPortMappings", t =>
            {
                t.HasCheckConstraint("CK_GameTypeSettingPortMappings_Role", "MappingRole IN (0, 1)");
                t.HasCheckConstraint("CK_GameTypeSettingPortMappings_Type", "RelationType IN (0, 1, 2, 3)");
                t.HasCheckConstraint("CK_GameTypeSettingPortMappings_Protocol", "TargetProtocol IN ('tcp', 'udp')");
            });
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.GameTypeSettingMetadataId);
            entity.Property(e => e.TargetProtocol).HasDefaultValue("udp");
        });

        modelBuilder.Entity<GameTypeWebHostEntity>(entity =>
        {
            entity.ToTable("GameTypeWebHosts");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.GameTypeRevisionId);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.PathSegment).HasMaxLength(200);
            entity.Property(e => e.ContainerPortVariable).HasMaxLength(200);
            entity.Property(e => e.EnabledWhen).HasMaxLength(500);
        });

        modelBuilder.Entity<GameServerEntity>(entity =>
        {
            entity.ToTable("GameServers");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ServerId).IsUnique();
            entity.HasIndex(e => e.GameTypeRevisionId);
            entity.HasIndex(e => e.IsDeleted);
            entity.Property(e => e.ServerId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.ServiceName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(50);
            ConfigureTimestampProperty(entity.Property(e => e.CreatedAt), isMySql);
            ConfigureTimestampProperty(entity.Property(e => e.UpdatedAt), isMySql);

            entity.HasMany(e => e.Settings)
                .WithOne(e => e.GameServer)
                .HasForeignKey(e => e.GameServerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<GameServerSettingEntity>(entity =>
        {
            entity.ToTable("GameServerSettings");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.GameServerId, e.SettingKey }).IsUnique();
            entity.Property(e => e.SettingKey).IsRequired().HasMaxLength(200);
        });

        modelBuilder.Entity<MountTypeConfigEntity>(entity =>
        {
            entity.ToTable("MountTypeConfigs");
            entity.HasKey(e => e.Key);
            entity.Property(e => e.Key).IsRequired().HasMaxLength(50);
            entity.Property(e => e.DisplayName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Driver).IsRequired().HasMaxLength(200);
            entity.Property(e => e.SourcePathTemplate).IsRequired().HasMaxLength(500);
            entity.Property(e => e.ContainerPathTemplate).IsRequired().HasMaxLength(500);
            entity.Property(e => e.DefaultInitMode).IsRequired().HasMaxLength(50);
            entity.Property(e => e.DefaultPermissions).HasMaxLength(10);
            entity.Property(e => e.DefaultOwnerUid);
            entity.Property(e => e.DefaultOwnerGid);
            ConfigureTimestampProperty(entity.Property(e => e.CreatedAt), isMySql);
            ConfigureTimestampProperty(entity.Property(e => e.UpdatedAt), isMySql);

            entity.HasData(
                new MountTypeConfigEntity
                {
                    Key = "volume",
                    DisplayName = "Docker volume",
                    Driver = "local",
                    DriverOptionsJson = null,
                    SourcePathTemplate = "{gameTypeKey}_{serverId}_{Source}",
                    ContainerPathTemplate = "{Source}",
                    DefaultReadOnly = false,
                    DefaultInitMode = "none",
                    IsActive = true
                },
                new MountTypeConfigEntity
                {
                    Key = "bind",
                    DisplayName = "Bind mount",
                    Driver = "local",
                    DriverOptionsJson = null,
                    SourcePathTemplate = "/host/gameservers/{gameTypeKey}/{serverId}/{Source}",
                    ContainerPathTemplate = "{Source}",
                    DefaultReadOnly = false,
                    DefaultInitMode = "none",
                    IsActive = true
                },
                new MountTypeConfigEntity
                {
                    Key = "tmpfs",
                    DisplayName = "tmpfs",
                    Driver = "local",
                    DriverOptionsJson = null,
                    SourcePathTemplate = string.Empty,
                    ContainerPathTemplate = "{Source}",
                    DefaultReadOnly = false,
                    DefaultInitMode = "none",
                    IsActive = true
                },
                new MountTypeConfigEntity
                {
                    Key = "nfs",
                    DisplayName = "NFS volume",
                    Driver = "vieux/sshfs",
                    DriverOptionsJson = "{\"type\":\"nfs\",\"device\":\":/exported/path\",\"o\":\"addr=host.docker.internal,rw\"}",
                    SourcePathTemplate = "{gameTypeKey}_{serverId}_{Source}",
                    ContainerPathTemplate = "{Source}",
                    DefaultReadOnly = false,
                    DefaultInitMode = "none",
                    IsActive = true
                });
        });
    }

    public override int SaveChanges()
    {
        UpdateTimestamps();
        return base.SaveChanges();
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateTimestamps();
        return await base.SaveChangesAsync(cancellationToken);
    }

    private void UpdateTimestamps()
    {
        foreach (var entry in ChangeTracker.Entries().Where(e => e.State == EntityState.Modified))
        {
            if (entry.Entity is GameTypeEntity gameType)
            {
                gameType.UpdatedAt = DateTime.UtcNow;
            }
            else if (entry.Entity is GameServerEntity gameServer)
            {
                gameServer.UpdatedAt = DateTime.UtcNow;
            }
            else if (entry.Entity is MountTypeConfigEntity mountTypeConfig)
            {
                mountTypeConfig.UpdatedAt = DateTime.UtcNow;
            }
        }
    }

    private static void ConfigureTimestampProperty<T>(Microsoft.EntityFrameworkCore.Metadata.Builders.PropertyBuilder<T> propertyBuilder, bool isMySql)
    {
        ArgumentNullException.ThrowIfNull(propertyBuilder);

        if (isMySql)
        {
            propertyBuilder
                .HasColumnType("datetime(6)");

            return;
        }

        propertyBuilder.HasDefaultValueSql("CURRENT_TIMESTAMP");
    }
}
