using Pico2WH.Pi5.IIoT.Application.Common.Models;

namespace Pico2WH.Pi5.IIoT.Application.Common.Interfaces;

public interface ITelemetryIngestRepository
{
    Task AddAsync(TelemetryIngestItem item, CancellationToken cancellationToken = default);
}
