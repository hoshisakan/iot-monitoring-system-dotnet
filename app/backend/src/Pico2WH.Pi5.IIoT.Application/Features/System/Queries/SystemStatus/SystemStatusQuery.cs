using MediatR;
using Pico2WH.Pi5.IIoT.Application.Common.Models;

namespace Pico2WH.Pi5.IIoT.Application.Features.System.Queries.SystemStatus;

public sealed record SystemStatusQuery(bool IncludeStopped) : IRequest<IReadOnlyList<ContainerStatusDto>>;
