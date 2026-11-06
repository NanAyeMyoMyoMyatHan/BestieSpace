using System;

namespace BestieSpace.Api.Infrastructure.Data.Entities;

/// <summary>
/// Entity representing global application statistics and the heartfelt welcome letter.
/// Scaffolded to match AppStats SQL table.
/// </summary>
public partial class AppStat
{
    public int Id { get; set; }

    public int MissYouCount { get; set; }

    public string MainLetterTitle { get; set; } = null!;

    public string MainLetterContent { get; set; } = null!;

    public string SenderNickname { get; set; } = null!;

    public string RecipientNickname { get; set; } = null!;

    public DateTime LastUpdated { get; set; }
}
