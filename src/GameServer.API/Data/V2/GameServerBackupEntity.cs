using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GameServer.API.Data.V2;

[Table("GameServerBackups")]
public class GameServerBackupEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    [MaxLength(64)]
    public string BackupId { get; set; } = Guid.NewGuid().ToString("N");

    [Required]
    [MaxLength(128)]
    public string ServerId { get; set; } = string.Empty;

    [Required]
    [MaxLength(256)]
    public string ServerName { get; set; } = string.Empty;

    [Required]
    [MaxLength(512)]
    public string VolumePath { get; set; } = string.Empty;

    [MaxLength(1024)]
    public string? SourceSubPath { get; set; }

    public bool IsDirectory { get; set; } = true;

    [Required]
    [MaxLength(256)]
    public string FileName { get; set; } = string.Empty;

    [Required]
    [MaxLength(1024)]
    public string FilePath { get; set; } = string.Empty;

    public long FileSizeBytes { get; set; }

    public int CreatedByUserId { get; set; }

    [Required]
    [MaxLength(128)]
    public string CreatedByUsername { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddDays(30);

    public int ExtensionDaysAdded { get; set; } = 0;

    public bool IsSharedWithGroups { get; set; } = false;

    public bool IsDeleted { get; set; } = false;

    public DateTime? DeletedAt { get; set; }
}
