namespace Emby.ExternalPlayer.Domain;

public sealed class CustomPlayerConfiguration
{
    public string Id { get; set; } = string.Empty;

    public bool Enabled { get; set; }

    public string ApplicationName { get; set; } = string.Empty;

    public string Platform { get; set; } = string.Empty;

    public string UrlTemplate { get; set; } = string.Empty;
}
