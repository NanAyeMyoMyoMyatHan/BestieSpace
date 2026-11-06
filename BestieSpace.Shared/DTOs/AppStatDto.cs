namespace BestieSpace.Shared.DTOs;

public class AppStatDto
{
    public int Id { get; set; }
    public int MissYouCount { get; set; }
    public string MainLetterTitle { get; set; } = string.Empty;
    public string MainLetterContent { get; set; } = string.Empty;
    public string SenderNickname { get; set; } = string.Empty;
    public string RecipientNickname { get; set; } = string.Empty;
    public DateTime LastUpdated { get; set; }
}
