using Community.PowerToys.Run.Plugin.PuTTY.Models;

namespace Community.PowerToys.Run.Plugin.PuTTY.Services;

public sealed class SessionIndexService
{
    private readonly SettingsService _settingsService;
    private readonly SessionRegistryReader _sessionReader = new();
    private readonly SemaphoreSlim _rescanLock = new(1, 1);
    private IReadOnlyList<SessionEntry> _entries;

    public SessionIndexService(SettingsService settingsService)
    {
        _settingsService = settingsService;
        _entries = settingsService.LoadIndex();
    }

    public bool IsRescanRunning => _rescanLock.CurrentCount == 0;

    public IReadOnlyList<SessionEntry> Entries => _entries;

    public async Task<int?> TryRescanAsync(PuTTYSettings settings, CancellationToken cancellationToken = default)
    {
        if (!await _rescanLock.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        try
        {
            var entries = await Task.Run(() => _sessionReader.ReadSessions(settings), cancellationToken)
                .ConfigureAwait(false);
            _entries = entries;
            _settingsService.SaveIndex(entries);
            return entries.Count;
        }
        finally
        {
            _rescanLock.Release();
        }
    }

    public IReadOnlyList<SearchMatch> Search(string query, int limit = 50)
    {
        query = query.Trim();
        if (string.IsNullOrEmpty(query))
        {
            return _entries
                .Take(limit)
                .Select(entry => new SearchMatch { Entry = entry, Score = 500 })
                .ToList();
        }

        var matches = new List<SearchMatch>();
        foreach (var entry in _entries)
        {
            var score = Score(entry, query);
            if (score <= 0)
            {
                continue;
            }

            matches.Add(new SearchMatch { Entry = entry, Score = score });
        }

        return matches
            .OrderByDescending(match => match.Score)
            .ThenBy(match => match.Entry.ClientLabel, StringComparer.OrdinalIgnoreCase)
            .ThenBy(match => match.Entry.Name.Length)
            .ThenBy(match => match.Entry.Name, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .ToList();
    }

    private static int Score(SessionEntry entry, string query)
    {
        if (entry.Name.Equals(query, StringComparison.OrdinalIgnoreCase))
        {
            return 1000;
        }

        if (entry.Name.StartsWith(query, StringComparison.OrdinalIgnoreCase))
        {
            return 900;
        }

        if (entry.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            return 750;
        }

        if (entry.HostName.StartsWith(query, StringComparison.OrdinalIgnoreCase) ||
            entry.UserName.StartsWith(query, StringComparison.OrdinalIgnoreCase))
        {
            return 700;
        }

        if (entry.HostLabel.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            entry.Protocol.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            entry.ClientLabel.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            return 550;
        }

        return 0;
    }
}
