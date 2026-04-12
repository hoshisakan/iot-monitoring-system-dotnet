using System.Linq;
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

    public async Task<IReadOnlyList<ContainerStatusDto>> ListContainersAsync(
        bool includeStopped,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            _logger.LogWarning("Docker 用戶端已停用，回傳空清單。");
            return Array.Empty<ContainerStatusDto>();
        }

        using var cfg = new DockerClientConfiguration(new Uri(_options.Uri));
        using var client = cfg.CreateClient();

        var list = await client.Containers.ListContainersAsync(
            new ContainersListParameters { All = includeStopped },
            cancellationToken).ConfigureAwait(false);

        return list.Select(c => new ContainerStatusDto(
            ContainerId: c.ID,
            Name: c.Names?.FirstOrDefault()?.TrimStart('/') ?? c.ID,
            Status: c.State ?? string.Empty,
            UptimeSec: null,
            Ip: null,
            HealthStatus: c.Status)).ToList();
    }
}
