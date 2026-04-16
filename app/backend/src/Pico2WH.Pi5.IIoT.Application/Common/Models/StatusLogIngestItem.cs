namespace Pico2WH.Pi5.IIoT.Application.Common.Models;

public sealed record StatusLogIngestItem(
    string DeviceId,
    string Channel,
    string Level,
    string Message,
    string PayloadJson,
    DateTime DeviceTimeUtc,
    DateTime CreatedAtUtc);
