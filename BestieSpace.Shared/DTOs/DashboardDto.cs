namespace BestieSpace.Shared.DTOs;

public class DashboardDto
{
    public AppStatDto AppStats { get; set; } = new();
    public List<MemoryDto> Memories { get; set; } = new();
    public List<SecretNoteDto> SecretNotes { get; set; } = new();
}

public class MissYouResponse
{
    public int MissYouCount { get; set; }
    public DateTime LastUpdated { get; set; }
}
