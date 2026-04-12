using Pico2WH.Pi5.IIoT.Domain.Common;

namespace Pico2WH.Pi5.IIoT.Domain.Entities;

/// <summary>Refresh Token 儲存（對齊 v5 §A.2；持久層存雜湊而非明文）。</summary>
public sealed class RefreshToken : EntityBase
{
    private RefreshToken()
    {
    }

    public RefreshToken(Guid userId, string tokenHash, DateTime expiresAtUtc, DateTime issuedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(tokenHash))
            throw new DomainException("TokenHash 不可為空。");

        Id = Guid.NewGuid();
        UserId = userId;
        TokenHash = tokenHash;
        ExpiresAtUtc = expiresAtUtc;
        IssuedAtUtc = issuedAtUtc;
        IsRevoked = false;
        CreatedAtUtc = issuedAtUtc;
        UpdatedAtUtc = issuedAtUtc;
    }

    public Guid UserId { get; private set; }

    public string TokenHash { get; private set; } = string.Empty;

    public DateTime ExpiresAtUtc { get; private set; }

    public DateTime IssuedAtUtc { get; private set; }

    public bool IsRevoked { get; private set; }

    public DateTime? RevokedAtUtc { get; private set; }

    public string? RevokedReason { get; private set; }

    public void Revoke(DateTime revokedAtUtc, string? reason = null)
    {
        if (IsRevoked)
            return;

        IsRevoked = true;
        RevokedAtUtc = revokedAtUtc;
        RevokedReason = reason;
        UpdatedAtUtc = revokedAtUtc;
    }
}
