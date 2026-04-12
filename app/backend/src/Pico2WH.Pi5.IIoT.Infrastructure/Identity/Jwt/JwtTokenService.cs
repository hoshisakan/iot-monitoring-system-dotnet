using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Pico2WH.Pi5.IIoT.Application.Common.Interfaces;
using Pico2WH.Pi5.IIoT.Domain.Entities;

namespace Pico2WH.Pi5.IIoT.Infrastructure.Identity.Jwt;

public sealed class JwtTokenService : IJwtService
{
    private readonly JwtOptions _options;

    public JwtTokenService(IOptions<JwtOptions> options)
    {
        _options = options.Value;
        if (string.IsNullOrWhiteSpace(_options.SigningKey) || Encoding.UTF8.GetByteCount(_options.SigningKey) < 32)
            throw new InvalidOperationException("Jwt:SigningKey 長度不足（至少 32 bytes UTF-8）。");
    }

    public TimeSpan AccessTokenLifetime => TimeSpan.FromMinutes(_options.AccessTokenMinutes);

    public TimeSpan RefreshTokenLifetime => TimeSpan.FromDays(_options.RefreshTokenDays);

    public string CreateAccessToken(User user)
    {
        var role = user.Role == UserRole.Admin ? "admin" : "customer";
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.UniqueName, user.Username),
            new Claim("role", role),
            new Claim("tenant_scope", user.TenantScope)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: DateTime.UtcNow.Add(AccessTokenLifetime),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshTokenPlainText()
    {
        var bytes = RandomNumberGenerator.GetBytes(48);
        return Convert.ToBase64String(bytes);
    }

    public string HashRefreshToken(string plainRefreshToken)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(plainRefreshToken));
        return Convert.ToHexString(hash);
    }
}
