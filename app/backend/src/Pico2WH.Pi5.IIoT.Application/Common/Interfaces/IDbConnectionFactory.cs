using System.Data.Common;

namespace Pico2WH.Pi5.IIoT.Application.Common.Interfaces;

/// <summary>
/// Provides opened database connections for read-side query services.
/// </summary>
public interface IDbConnectionFactory
{
    Task<DbConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default);
}
