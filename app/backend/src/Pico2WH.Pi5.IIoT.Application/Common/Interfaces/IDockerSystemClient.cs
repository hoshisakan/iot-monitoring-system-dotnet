using Pico2WH.Pi5.IIoT.Application.Common.Models;

namespace Pico2WH.Pi5.IIoT.Application.Common.Interfaces;

/// <summary>讀取 Docker 引擎容器狀態（由 Infrastructure <c>Docker</c> 實作）。</summary>
public interface IDockerSystemClient
{
    Task<SystemStatusResultDto> ListContainersAsync(
        bool includeStopped,
        CancellationToken cancellationToken = default);
}
