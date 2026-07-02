namespace Community.PowerToys.Run.Plugin.PuTTY.Models;

public sealed class SessionEntry
{
    public required string Name { get; init; }

    public required ClientKind ClientKind { get; init; }

    public string HostName { get; init; } = string.Empty;

    public string UserName { get; init; } = string.Empty;

    public string Protocol { get; init; } = string.Empty;

    public int? PortNumber { get; init; }

    public required DateTimeOffset IndexedAt { get; init; }

    public string ClientLabel => ClientKind == ClientKind.KiTTY ? "KiTTY" : "PuTTY";

    public string HostLabel
    {
        get
        {
            var host = string.IsNullOrWhiteSpace(HostName) ? "host not set" : HostName;
            var userPrefix = string.IsNullOrWhiteSpace(UserName) ? string.Empty : $"{UserName}@";
            var portSuffix = PortNumber is null ? string.Empty : $":{PortNumber.Value}";
            return $"{userPrefix}{host}{portSuffix}";
        }
    }
}
