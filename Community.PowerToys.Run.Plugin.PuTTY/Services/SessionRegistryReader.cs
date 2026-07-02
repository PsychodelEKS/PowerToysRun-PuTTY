using System.IO;
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

        if (settings.EnableFileSessions)
        {
            sessions.AddRange(ReadFileSessions(settings, now));
        }

        return Deduplicate(sessions)
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
                SourceKind = SessionSourceKind.Registry,
                HostName = ReadString(sessionKey, "HostName"),
                UserName = ReadString(sessionKey, "UserName"),
                Protocol = ReadString(sessionKey, "Protocol"),
                PortNumber = ReadInt(sessionKey, "PortNumber"),
                IndexedAt = indexedAt,
            };
        }
    }

    private static IEnumerable<SessionEntry> ReadFileSessions(PuTTYSettings settings, DateTimeOffset indexedAt)
    {
        var (directory, clientKind) = ResolveFileSessionsDirectory(settings);
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            yield break;
        }

        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(directory);
        }
        catch
        {
            yield break;
        }

        foreach (var filePath in files)
        {
            var sessionName = DecodeSessionName(Path.GetFileName(filePath));
            if (string.IsNullOrWhiteSpace(sessionName) ||
                string.Equals(sessionName, DefaultSettingsName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            IReadOnlyDictionary<string, string> values;
            try
            {
                values = ReadFileSessionValues(filePath);
            }
            catch
            {
                continue;
            }

            yield return new SessionEntry
            {
                Name = sessionName,
                ClientKind = clientKind,
                SourceKind = SessionSourceKind.File,
                SourcePath = filePath,
                HostName = GetValue(values, "HostName"),
                UserName = GetValue(values, "UserName"),
                Protocol = GetValue(values, "Protocol"),
                PortNumber = ReadInt(GetValue(values, "PortNumber")),
                IndexedAt = indexedAt,
            };
        }
    }

    private static IReadOnlyList<SessionEntry> Deduplicate(IEnumerable<SessionEntry> sessions)
    {
        var byClientAndName = new Dictionary<string, SessionEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var session in sessions)
        {
            var key = $"{session.ClientKind}\0{session.Name}";
            if (!byClientAndName.TryGetValue(key, out var existing) ||
                existing.SourceKind == SessionSourceKind.Registry && session.SourceKind == SessionSourceKind.File)
            {
                byClientAndName[key] = session;
            }
        }

        return byClientAndName.Values.ToList();
    }

    private static (string Directory, ClientKind ClientKind) ResolveFileSessionsDirectory(PuTTYSettings settings)
    {
        var configuredDirectory = NormalizePath(settings.FileSessionsDirectory);
        if (string.IsNullOrWhiteSpace(configuredDirectory))
        {
            return (string.Empty, ClientKind.KiTTY);
        }

        if (Path.IsPathRooted(configuredDirectory))
        {
            return (configuredDirectory, ClientKind.KiTTY);
        }

        var kittyDirectory = GetExecutableDirectory(settings.KiTTYExecutablePath);
        if (!string.IsNullOrWhiteSpace(kittyDirectory))
        {
            return (Path.Combine(kittyDirectory, configuredDirectory), ClientKind.KiTTY);
        }

        var puttyDirectory = GetExecutableDirectory(settings.PuTTYExecutablePath);
        if (!string.IsNullOrWhiteSpace(puttyDirectory))
        {
            return (Path.Combine(puttyDirectory, configuredDirectory), ClientKind.PuTTY);
        }

        return (configuredDirectory, ClientKind.KiTTY);
    }

    private static string GetExecutableDirectory(string executablePath)
    {
        var normalizedPath = NormalizePath(executablePath);
        if (string.IsNullOrWhiteSpace(normalizedPath) || !Path.IsPathRooted(normalizedPath))
        {
            return string.Empty;
        }

        try
        {
            return Path.GetDirectoryName(normalizedPath) ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string NormalizePath(string path)
    {
        path = path.Trim();
        if (path.Length >= 2 &&
            ((path[0] == '"' && path[^1] == '"') || (path[0] == '\'' && path[^1] == '\'')))
        {
            return path[1..^1].Trim();
        }

        return path;
    }

    private static IReadOnlyDictionary<string, string> ReadFileSessionValues(string filePath)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in File.ReadLines(filePath))
        {
            var separator = line.IndexOf('\\', StringComparison.Ordinal);
            if (separator <= 0)
            {
                continue;
            }

            var key = line[..separator];
            var value = line[(separator + 1)..];
            if (value.EndsWith('\\'))
            {
                value = value[..^1];
            }

            values[key] = DecodeSessionName(value);
        }

        return values;
    }

    private static string GetValue(IReadOnlyDictionary<string, string> values, string key)
    {
        return values.TryGetValue(key, out var value) ? value : string.Empty;
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

    private static int? ReadInt(string value)
    {
        return int.TryParse(value, out var parsed) ? parsed : null;
    }
}
