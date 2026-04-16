namespace Pico2WH.Pi5.IIoT.Application.Common.Models;

public sealed record UiEventIngestItem(
    string DeviceId,
    string SiteId,
    DateTime DeviceTimeUtc,
    string EventType,
    string EventValue,
    string Channel,
    string PayloadJson,
    DateTime IngestedAtUtc);
