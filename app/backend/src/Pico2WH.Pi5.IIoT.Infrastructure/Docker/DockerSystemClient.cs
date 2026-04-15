using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pico2WH.Pi5.IIoT.Application.Common.Interfaces;
using Pico2WH.Pi5.IIoT.Application.Common.Models;

namespace Pico2WH.Pi5.IIoT.Infrastructure.Docker;

public sealed class DockerSystemClient : IDockerSystemClient
{
    private readonly DockerOptions _options;
    private readonly ILogger<DockerSystemClient> _logger;

    public DockerSystemClient(IOptions<DockerOptions> options, ILogger<DockerSystemClient> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<SystemStatusResultDto> ListContainersAsync(
        bool includeStopped,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            _logger.LogWarning("Docker 用戶端已停用，回傳空清單。");
            return new SystemStatusResultDto(
                Items: Array.Empty<ContainerStatusDto>(),
                WarningCode: "DOCKER_DISABLED",
                WarningMessage: "Docker 用戶端已停用，無法讀取容器狀態。");
        }

        try
        {
            using var cfg = new DockerClientConfiguration(new Uri(_options.Uri));
            using var client = cfg.CreateClient();

            var list = await client.Containers.ListContainersAsync(
                new ContainersListParameters { All = includeStopped },
                cancellationToken).ConfigureAwait(false);

            var items = list.Select(c => new ContainerStatusDto(
                ContainerId: c.ID,
                Name: c.Names?.FirstOrDefault()?.TrimStart('/') ?? c.ID,
                Status: c.State ?? string.Empty,
                UptimeSec: null,
                Ip: null,
                HealthStatus: c.Status)).ToList();
            return new SystemStatusResultDto(
                Items: items,
                WarningCode: null,
                WarningMessage: null);
        }
        catch (Exception ex) when (ex is HttpRequestException or SocketException or DockerApiException)
        {
            _logger.LogWarning(ex, "無法讀取 Docker 容器狀態（Uri={DockerUri}），回傳空清單。", _options.Uri);
            var warning = BuildUnavailableWarning(ex, _options.Uri);
            return new SystemStatusResultDto(
                Items: Array.Empty<ContainerStatusDto>(),
                WarningCode: warning.Code,
                WarningMessage: warning.Message);
        }
    }

    private static (string Code, string Message) BuildUnavailableWarning(Exception ex, string dockerUri)
    {
        var socketEx = ex as SocketException ?? ex.InnerException as SocketException;
        if (socketEx?.SocketErrorCode == SocketError.AccessDenied ||
            ex.Message.Contains("permission denied", StringComparison.OrdinalIgnoreCase))
        {
            return (
                "DOCKER_PERMISSION_DENIED",
                $"無法讀取 Docker（{dockerUri}）：目前程序沒有 socket 權限。請確認使用者已加入 docker 群組，或調整 /var/run/docker.sock 權限。");
        }

        if (ex.Message.Contains("no such file or directory", StringComparison.OrdinalIgnoreCase))
        {
            return (
                "DOCKER_SOCKET_NOT_FOUND",
                $"找不到 Docker socket（{dockerUri}）。請確認 Docker daemon 已啟動，且 Uri 設定正確。");
        }

        if (socketEx?.SocketErrorCode == SocketError.ConnectionRefused ||
            ex.Message.Contains("connection refused", StringComparison.OrdinalIgnoreCase))
        {
            return (
                "DOCKER_DAEMON_DOWN",
                $"無法連線 Docker（{dockerUri}）：連線被拒絕。請確認 Docker daemon 目前正在執行。");
        }

        return (
            "DOCKER_UNAVAILABLE",
            $"無法連線 Docker（{dockerUri}）。常見原因：權限不足或 Docker 未啟動。");
    }
}
