namespace Pico2WH.Pi5.IIoT.Infrastructure.Docker;

public sealed class DockerOptions
{
    public const string SectionName = "Docker";

    /// <summary>例如 <c>unix:///var/run/docker.sock</c>（Linux）或 Windows named pipe。</summary>
    public string Uri { get; set; } = "unix:///var/run/docker.sock";

    public bool Enabled { get; set; } = true;
}
