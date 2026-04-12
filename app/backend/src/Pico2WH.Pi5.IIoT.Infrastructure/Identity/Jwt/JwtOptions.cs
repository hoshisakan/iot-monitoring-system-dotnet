namespace Pico2WH.Pi5.IIoT.Infrastructure.Identity.Jwt;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "Pico2WH.Pi5.IIoT";

    public string Audience { get; set; } = "Pico2WH.Pi5.IIoT.Api";

    /// <summary>HMAC-SHA256 金鑰（建議 ≥ 32 位元組 UTF-8）。</summary>
    public string SigningKey { get; set; } = "CHANGE_ME_DEV_ONLY_MIN_32_CHARS_LONG!!";

    public int AccessTokenMinutes { get; set; } = 60;

    public int RefreshTokenDays { get; set; } = 7;
}
