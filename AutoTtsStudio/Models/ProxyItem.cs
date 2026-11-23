namespace AutoTtsStudio.Models;

public enum ProxyStatus
{
    Unknown,
    Live,
    Dead
}

public class ProxyItem
{
    public string Raw { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public ProxyStatus Status { get; set; } = ProxyStatus.Unknown;
    public int FailCount { get; set; } = 0;

    /// <summary>
    /// Parse from "host:port" or "host:port:user:pass"
    /// </summary>
    public static ProxyItem? TryParse(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var parts = raw.Trim().Split(':');
        if (parts.Length < 2) return null;
        if (!int.TryParse(parts[1], out var port)) return null;
        return new ProxyItem
        {
            Raw = raw.Trim(),
            Host = parts[0],
            Port = port,
            Username = parts.Length > 2 ? parts[2] : string.Empty,
            Password = parts.Length > 3 ? parts[3] : string.Empty,
            Status = ProxyStatus.Live
        };
    }

    public string ToUriString()
    {
        if (!string.IsNullOrEmpty(Username))
            return $"http://{Uri.EscapeDataString(Username)}:{Uri.EscapeDataString(Password)}@{Host}:{Port}";
        return $"http://{Host}:{Port}";
    }

    public override string ToString() => Raw;
}
