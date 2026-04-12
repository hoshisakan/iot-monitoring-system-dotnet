using Pico2WH.Pi5.IIoT.Domain.Common;

namespace Pico2WH.Pi5.IIoT.Domain.Entities;

/// <summary>使用者帳號（對齊 v5 §A.2 <c>users</c>）。</summary>
public sealed class User : EntityBase
{
    private User()
    {
    }

    public User(string username, string passwordHash, UserRole role, string tenantScope, bool isActive = true)
    {
        if (string.IsNullOrWhiteSpace(username))
            throw new DomainException("Username 不可為空。");

        if (username.Length > 64)
            throw new DomainException("Username 長度不可超過 64。");

        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new DomainException("PasswordHash 不可為空。");

        if (string.IsNullOrWhiteSpace(tenantScope))
            throw new DomainException("TenantScope 不可為空。");

        if (tenantScope.Length > 64)
            throw new DomainException("TenantScope 長度不可超過 64。");

        Id = Guid.NewGuid();
        Username = username.Trim();
        PasswordHash = passwordHash;
        Role = role;
        TenantScope = tenantScope.Trim();
        IsActive = isActive;
        var now = DateTime.UtcNow;
        CreatedAtUtc = now;
        UpdatedAtUtc = now;
    }

    public string Username { get; private set; } = string.Empty;

    public string PasswordHash { get; private set; } = string.Empty;

    public UserRole Role { get; private set; }

    /// <summary>租戶／站臺範圍（對齊 <c>site_id</c>）。</summary>
    public string TenantScope { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }

    public void SetPasswordHash(string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new DomainException("PasswordHash 不可為空。");

        PasswordHash = passwordHash;
        Touch();
    }

    public void SetActive(bool active)
    {
        IsActive = active;
        Touch();
    }

    private void Touch()
    {
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
