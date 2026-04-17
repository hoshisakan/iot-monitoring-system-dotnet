namespace Pico2WH.Pi5.IIoT.Infrastructure.Docker;

public sealed class DockerOptions
{
    public const string SectionName = "Docker";

    /// <summary>例如 <c>unix:///var/run/docker.sock</c>（Linux）或 Windows named pipe。</summary>
    public string Uri { get; set; } = "unix:///var/run/docker.sock";

    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 是否僅回傳與目前 compose project 相同的容器（避免混入其他專案容器）。
    /// </summary>
    public bool LimitToComposeProject { get; set; } = true;

    /// <summary>
    /// 指定 compose project 名稱；未指定時會嘗試從目前容器（HOSTNAME）推導。
    /// </summary>
    public string? ComposeProjectName { get; set; }
}
