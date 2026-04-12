using AutoMapper;
using Pico2WH.Pi5.IIoT.Application.Common.Models;
using Pico2WH.Pi5.IIoT.Domain.Entities;

namespace Pico2WH.Pi5.IIoT.Application.Mappings;

public sealed class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<TelemetryReading, TelemetryListItemDto>()
            .ForMember(d => d.DeviceId, o => o.MapFrom(s => s.DeviceId.Value));

        CreateMap<StructuredLogEntry, LogListItemDto>();
    }
}
