using System.ComponentModel.DataAnnotations;

namespace Bot.Models;

public class Sound
{
    public int Id { get; set; }

    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [Required] [StringLength(200)] public string FilePath { get; set; } = string.Empty;

    public long FileSizeBytes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}