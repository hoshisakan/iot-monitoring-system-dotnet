namespace Pico2WH.Pi5.IIoT.Application.Common.Interfaces;

/// <summary>密碼雜湊與驗證（由 Infrastructure <c>Identity/Security</c> 實作）。</summary>
public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string password, string passwordHash);
}
