using Microsoft.Win32;
using Community.PowerToys.Run.Plugin.PuTTY.Models;

namespace Community.PowerToys.Run.Plugin.PuTTY.Services;

public sealed class SessionRegistryReader
{
    private const string PuTTYSessionsKey = @"Software\SimonTatham\PuTTY\Sessions";
    private const string KiTTYSessionsKey = @"Software\9bis.com\KiTTY\Sessions";
    private const string DefaultSettingsName = "Default Settings";

    public IReadOnlyList<SessionEntry> ReadSessions(PuTTYSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var now = DateTimeOffset.UtcNow;
        var sessions = new List<SessionEntry>();

        if (settings.EnablePuTTYSessions)
        {
            sessions.AddRange(ReadClientSessions(ClientKind.PuTTY, PuTTYSessionsKey, now));
        }

        if (settings.EnableKiTTYSessions)
        {
            sessions.AddRange(ReadClientSessions(ClientKind.KiTTY, KiTTYSessionsKey, now));
        }

        return sessions
            .OrderBy(session => session.ClientLabel, StringComparer.OrdinalIgnoreCase)
            .ThenBy(session => session.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IEnumerable<SessionEntry> ReadClientSessions(
        ClientKind clientKind,
        string registryPath,
        DateTimeOffset indexedAt)
    {
        using var sessionsKey = Registry.CurrentUser.OpenSubKey(registryPath);
        if (sessionsKey is null)
        {
            yield break;
        }

        foreach (var rawName in sessionsKey.GetSubKeyNames())
        {
            var sessionName = DecodeSessionName(rawName);
            if (string.IsNullOrWhiteSpace(sessionName) ||
                string.Equals(sessionName, DefaultSettingsName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            using var sessionKey = sessionsKey.OpenSubKey(rawName);
            if (sessionKey is null)
            {
                continue;
            }

            yield return new SessionEntry
            {
                Name = sessionName,
                ClientKind = clientKind,
                HostName = ReadString(sessionKey, "HostName"),
                UserName = ReadString(sessionKey, "UserName"),
                Protocol = ReadString(sessionKey, "Protocol"),
                PortNumber = ReadInt(sessionKey, "PortNumber"),
                IndexedAt = indexedAt,
            };
        }
    }

    private static string DecodeSessionName(string rawName)
    {
        try
        {
            return Uri.UnescapeDataString(rawName);
        }
        catch
        {
            return rawName;
        }
    }

    private static string ReadString(RegistryKey key, string valueName)
    {
        try
        {
            return key.GetValue(valueName)?.ToString() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static int? ReadInt(RegistryKey key, string valueName)
    {
        try
        {
            return key.GetValue(valueName) switch
            {
                int intValue => intValue,
                string stringValue when int.TryParse(stringValue, out var parsed) => parsed,
                _ => null,
            };
        }
        catch
        {
            return null;
        }
    }
}
