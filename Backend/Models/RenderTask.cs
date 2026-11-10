using System.Text.Json.Serialization;

namespace Backend.Models;

public class RenderTask
{
    public Guid Id { get; set; }
    public string FigureType { get; set; } = string.Empty;
    public int CanvasWidth { get; set; }
    public int CanvasHeight { get; set; }
    public int Iterations { get; set; }

    public string Status { get; set; } = "InQueue";

    public int Progress { get; set; } = 0;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    public string? ResultImageUrl { get; set; }

    public int? UserId { get; set; }

    [JsonIgnore]
    public User? User { get; set; }
    public string ServerId { get; set; } = string.Empty;
}