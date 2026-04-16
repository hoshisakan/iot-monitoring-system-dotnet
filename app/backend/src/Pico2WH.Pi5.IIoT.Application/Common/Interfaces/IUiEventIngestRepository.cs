using Pico2WH.Pi5.IIoT.Application.Common.Models;

namespace Pico2WH.Pi5.IIoT.Application.Common.Interfaces;

public interface IUiEventIngestRepository
{
    Task AddAsync(UiEventIngestItem item, CancellationToken cancellationToken = default);
}
