using MediatR;
using Pico2WH.Pi5.IIoT.Application.Common.Interfaces;
using Pico2WH.Pi5.IIoT.Application.Common.Models;

namespace Pico2WH.Pi5.IIoT.Application.Features.System.Queries.SystemStatus;

public sealed class SystemStatusQueryHandler : IRequestHandler<SystemStatusQuery, SystemStatusResultDto>
{
    private readonly IDockerSystemClient _docker;

    public SystemStatusQueryHandler(IDockerSystemClient docker)
    {
        _docker = docker;
    }

    public Task<SystemStatusResultDto> Handle(SystemStatusQuery request, CancellationToken cancellationToken) =>
        _docker.ListContainersAsync(request.IncludeStopped, cancellationToken);
}
