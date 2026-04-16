using Pico2WH.Pi5.IIoT.Application.Common.Models;

namespace Pico2WH.Pi5.IIoT.Application.Common.Interfaces;

public interface IStatusLogIngestRepository
{
    Task AddAsync(StatusLogIngestItem item, CancellationToken cancellationToken = default);
}
