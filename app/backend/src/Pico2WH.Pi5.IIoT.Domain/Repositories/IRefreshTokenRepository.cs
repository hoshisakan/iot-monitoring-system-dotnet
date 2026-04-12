using Pico2WH.Pi5.IIoT.Domain.Entities;

namespace Pico2WH.Pi5.IIoT.Domain.Repositories;

/// <summary>Refresh Token 持久化埠。</summary>
public interface IRefreshTokenRepository
{
    Task AddAsync(RefreshToken token, CancellationToken cancellationToken = default);

    Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    Task UpdateAsync(RefreshToken token, CancellationToken cancellationToken = default);
}
