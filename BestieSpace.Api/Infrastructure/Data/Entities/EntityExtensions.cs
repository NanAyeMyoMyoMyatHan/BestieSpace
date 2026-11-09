using System;
using System.Collections.Generic;

namespace BestieSpace.Api.Infrastructure.Data.Entities;

public partial class RefreshToken
{
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    public bool IsRevoked => RevokedAt != null;
    public bool IsActive => !IsRevoked && !IsExpired;
}

public partial class ChatMessage
{
    public virtual ICollection<ChatMessageRead> ReadReceipts => ChatMessageReads;
    public virtual ICollection<ChatMessageDelivery> DeliveryReceipts => ChatMessageDeliveries;

    public double? DurationSeconds
    {
        get => VoiceDurationSeconds;
        set => VoiceDurationSeconds = value;
    }
}

public partial class BestieCircle
{
    public virtual ICollection<BestieCircleMember> Members => BestieCircleMembers;
    public virtual User Owner => OwnerUser;
}
