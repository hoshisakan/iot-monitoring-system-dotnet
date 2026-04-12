namespace Pico2WH.Pi5.IIoT.Application.Common.Models;

/// <summary>分頁查詢共用模型。</summary>
public sealed class PagedResult<T>
{
    public required IReadOnlyList<T> Items { get; init; }

    public int Page { get; init; }

    public int PageSize { get; init; }

    public int TotalCount { get; init; }
}
