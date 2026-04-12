namespace Pico2WH.Pi5.IIoT.Domain.Common;

/// <summary>領域層可預期的規則違反或業務錯誤。</summary>
public sealed class DomainException : Exception
{
    public DomainException()
    {
    }

    public DomainException(string message)
        : base(message)
    {
    }

    public DomainException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
