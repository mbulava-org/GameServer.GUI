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
            // Enable SQLite performance optimizations
            Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");      // Write-Ahead Logging for better concurrency
            Database.ExecuteSqlRaw("PRAGMA synchronous=NORMAL;");    // Faster writes (still safe)
            Database.ExecuteSqlRaw("PRAGMA cache_size=-64000;");     // 64MB cache
            Database.ExecuteSqlRaw("PRAGMA temp_store=MEMORY;");     // Use memory for temp tables
            Database.ExecuteSqlRaw("PRAGMA mmap_size=268435456;");   // 256MB memory-mapped I/O
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
                entity.ToTable("Ports", t =>
                {
                    t.HasCheckConstraint("CK_Ports_Protocol", "Protocol IN ('tcp', 'udp')");
                    t.HasCheckConstraint("CK_Ports_Range", "Port >= 1 AND Port <= 65535");
                });
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.GameTypeId);
                entity.HasIndex(e => e.IsDefaultPort);

                entity.Property(e => e.Protocol).IsRequired().HasMaxLength(10);
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
                entity.ToTable("PortValidation", t =>
                {
                    t.HasCheckConstraint("CK_PortValidation_Range",
                        "MinPort >= 1 AND MinPort <= MaxPort AND MaxPort <= 65535");
                });
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.SettingMetadataId).IsUnique();

                entity.Property(e => e.MinPort).HasDefaultValue(1024);
                entity.Property(e => e.MaxPort).HasDefaultValue(65535);
                entity.Property(e => e.CheckAvailability).HasDefaultValue(true);
                entity.Property(e => e.IsUserEditable).HasDefaultValue(true);
            });

            // PortRelationship configuration
            modelBuilder.Entity<PortRelationshipEntity>(entity =>
            {
                entity.ToTable("PortRelationships", t =>
                {
                    t.HasCheckConstraint("CK_PortRelationships_Type",
                        "RelationType IN (0, 1, 2)");
                    t.HasCheckConstraint("CK_PortRelationships_Protocol",
                        "TargetProtocol IN ('tcp', 'udp')");
                });
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.SettingMetadataId);

                entity.Property(e => e.TargetProtocol).HasDefaultValue("udp");
                entity.Property(e => e.IsRequired).HasDefaultValue(true);
            });

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
