using Pico2WH.Pi5.IIoT.Domain.Entities;

namespace Pico2WH.Pi5.IIoT.Application.Common.Interfaces;

/// <summary>JWT 簽發與 Refresh Token 雜湊（由 Infrastructure <c>Identity/Jwt</c> 實作）。</summary>
public interface IJwtService
{
    TimeSpan AccessTokenLifetime { get; }

    TimeSpan RefreshTokenLifetime { get; }

    string CreateAccessToken(User user);

    /// <summary>產生隨機 Refresh Token 明文（僅回傳給客戶端一次；持久化存雜湊）。</summary>
    string GenerateRefreshTokenPlainText();

    /// <summary>將 Refresh Token 明文轉為儲存／查詢用雜湊。</summary>
    string HashRefreshToken(string plainRefreshToken);
}
