using Microsoft.EntityFrameworkCore;
using GameServer.Docker.Models;

namespace GameServer.Docker.Data
{
    /// <summary>
    /// Database context for GameType management using SQLite
    /// </summary>
    public class GameServerDbContext : DbContext
    {
        public GameServerDbContext(DbContextOptions<GameServerDbContext> options)
            : base(options)
        {
        }

        // Main tables
        public DbSet<GameTypeEntity> GameTypes { get; set; }
        public DbSet<PortEntity> Ports { get; set; }
        public DbSet<VolumeEntity> Volumes { get; set; }
        public DbSet<DefaultSettingEntity> DefaultSettings { get; set; }
        public DbSet<ExtendedMetadataEntity> ExtendedMetadata { get; set; }
        public DbSet<SettingMetadataEntity> SettingsMetadata { get; set; }
        public DbSet<PortValidationEntity> PortValidations { get; set; }
        public DbSet<PortRelationshipEntity> PortRelationships { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // GameType configuration
            modelBuilder.Entity<GameTypeEntity>(entity =>
            {
                entity.ToTable("GameTypes");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Key).IsUnique();
                entity.HasIndex(e => e.IsActive);

                entity.Property(e => e.Key).IsRequired().HasMaxLength(100);
                entity.Property(e => e.DisplayName).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Image).IsRequired().HasMaxLength(500);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

                // Relationships
                entity.HasMany(e => e.Ports)
                    .WithOne(e => e.GameType)
                    .HasForeignKey(e => e.GameTypeId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(e => e.Volumes)
                    .WithOne(e => e.GameType)
                    .HasForeignKey(e => e.GameTypeId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(e => e.DefaultSettings)
                    .WithOne(e => e.GameType)
                    .HasForeignKey(e => e.GameTypeId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.ExtendedMetadata)
                    .WithOne(e => e.GameType)
                    .HasForeignKey<ExtendedMetadataEntity>(e => e.GameTypeId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Port configuration
            modelBuilder.Entity<PortEntity>(entity =>
            {
                entity.ToTable("Ports");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.GameTypeId);
                entity.HasIndex(e => e.IsDefaultPort);

                entity.Property(e => e.Protocol).IsRequired().HasMaxLength(10);
                entity.HasCheckConstraint("CK_Ports_Protocol", 
                    "Protocol IN ('tcp', 'udp')");
                entity.HasCheckConstraint("CK_Ports_Range", 
                    "Port >= 1 AND Port <= 65535");
            });

            // Volume configuration
            modelBuilder.Entity<VolumeEntity>(entity =>
            {
                entity.ToTable("Volumes");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.GameTypeId);

                entity.Property(e => e.Source).IsRequired().HasMaxLength(500);
                entity.Property(e => e.Target).IsRequired().HasMaxLength(500);
            });

            // DefaultSetting configuration
            modelBuilder.Entity<DefaultSettingEntity>(entity =>
            {
                entity.ToTable("DefaultSettings");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.GameTypeId, e.SettingKey }).IsUnique();
                entity.HasIndex(e => e.SettingKey);

                entity.Property(e => e.SettingKey).IsRequired().HasMaxLength(200);

                // 1:1 optional relationship with SettingsMetadata
                entity.HasOne(e => e.SettingsMetadata)
                    .WithOne(e => e.DefaultSetting)
                    .HasForeignKey<SettingMetadataEntity>(e => e.DefaultSettingId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // ExtendedMetadata configuration
            modelBuilder.Entity<ExtendedMetadataEntity>(entity =>
            {
                entity.ToTable("ExtendedMetadata");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.GameTypeId).IsUnique();

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

                // Note: SettingsMetadata now belongs to DefaultSettings, not ExtendedMetadata
            });

            // SettingMetadata configuration
            modelBuilder.Entity<SettingMetadataEntity>(entity =>
            {
                entity.ToTable("SettingsMetadata");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.DefaultSettingId).IsUnique();
                entity.HasIndex(e => e.Category);
                entity.HasIndex(e => e.MapsToContainerPort);

                entity.Property(e => e.DataType).HasMaxLength(50);
                entity.Property(e => e.PortProtocol).HasDefaultValue("tcp");
                entity.Property(e => e.ListDelimiter).HasDefaultValue(",");
                entity.Property(e => e.ValidateRelatedPortsAvailability).HasDefaultValue(true);

                entity.HasCheckConstraint("CK_SettingsMetadata_DataType",
                    "DataType IS NULL OR DataType IN ('string', 'number', 'boolean', 'enum', 'list', 'port')");

                // Relationships
                entity.HasOne(e => e.PortValidation)
                    .WithOne(e => e.SettingMetadata)
                    .HasForeignKey<PortValidationEntity>(e => e.SettingMetadataId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(e => e.PortRelationships)
                    .WithOne(e => e.SettingMetadata)
                    .HasForeignKey(e => e.SettingMetadataId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // PortValidation configuration
            modelBuilder.Entity<PortValidationEntity>(entity =>
            {
                entity.ToTable("PortValidation");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.SettingMetadataId).IsUnique();

                entity.Property(e => e.MinPort).HasDefaultValue(1024);
                entity.Property(e => e.MaxPort).HasDefaultValue(65535);
                entity.Property(e => e.CheckAvailability).HasDefaultValue(true);
                entity.Property(e => e.IsUserEditable).HasDefaultValue(true);

                entity.HasCheckConstraint("CK_PortValidation_Range",
                    "MinPort >= 1 AND MinPort <= MaxPort AND MaxPort <= 65535");
            });

            // PortRelationship configuration
            modelBuilder.Entity<PortRelationshipEntity>(entity =>
            {
                entity.ToTable("PortRelationships");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.SettingMetadataId);

                entity.Property(e => e.TargetProtocol).HasDefaultValue("udp");
                entity.Property(e => e.IsRequired).HasDefaultValue(true);

                entity.HasCheckConstraint("CK_PortRelationships_Type",
                    "RelationType IN (0, 1, 2)");
                entity.HasCheckConstraint("CK_PortRelationships_Protocol",
                    "TargetProtocol IN ('tcp', 'udp')");
            });

            // Seed initial data (optional)
            SeedData(modelBuilder);
        }

        private void SeedData(ModelBuilder modelBuilder)
        {
            // Seed example: Minecraft game type
            modelBuilder.Entity<GameTypeEntity>().HasData(
                new GameTypeEntity
                {
                    Id = 1,
                    Key = "minecraft",
                    DisplayName = "Minecraft Server",
                    Description = "Java Edition Minecraft Server",
                    Image = "itzg/minecraft-server:latest",
                    ThumbnailUrl = "https://static.wikia.nocookie.net/minecraft_gamepedia/images/2/2d/Plains_Banner.png",
                    DocumentationUrl = "https://hub.docker.com/r/itzg/minecraft-server",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }
            );

            // Seed example ports
            modelBuilder.Entity<PortEntity>().HasData(
                new PortEntity { Id = 1, GameTypeId = 1, Port = 25565, Protocol = "tcp", IsDefaultPort = true, Description = "Game Port" },
                new PortEntity { Id = 2, GameTypeId = 1, Port = 25565, Protocol = "udp", IsDefaultPort = false, Description = "Query Port" }
            );

            // Seed example default settings
            modelBuilder.Entity<DefaultSettingEntity>().HasData(
                new DefaultSettingEntity { Id = 1, GameTypeId = 1, SettingKey = "EULA", SettingValue = "TRUE" },
                new DefaultSettingEntity { Id = 2, GameTypeId = 1, SettingKey = "VERSION", SettingValue = "LATEST" }
            );
        }

        /// <summary>
        /// Update timestamps on save
        /// </summary>
        public override int SaveChanges()
        {
            UpdateTimestamps();
            return base.SaveChanges();
        }

        /// <summary>
        /// Update timestamps on save async
        /// </summary>
        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            UpdateTimestamps();
            return await base.SaveChangesAsync(cancellationToken);
        }

        private void UpdateTimestamps()
        {
            var entries = ChangeTracker.Entries()
                .Where(e => e.State == EntityState.Modified);

            foreach (var entry in entries)
            {
                if (entry.Entity is GameTypeEntity gameType)
                {
                    gameType.UpdatedAt = DateTime.UtcNow;
                }
                else if (entry.Entity is ExtendedMetadataEntity metadata)
                {
                    metadata.UpdatedAt = DateTime.UtcNow;
                }
            }
        }
    }
}
