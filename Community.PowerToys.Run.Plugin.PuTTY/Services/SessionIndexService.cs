using System.Text;
using Community.PowerToys.Run.Plugin.PuTTY.Models;
using Wox.Infrastructure;

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
            var match = Score(entry, query);
            if (match.Score <= 0)
            {
                continue;
            }

            matches.Add(new SearchMatch
            {
                Entry = entry,
                Score = match.Score,
                TitleHighlightData = match.TitleHighlightData,
            });
        }

        return matches
            .OrderByDescending(match => match.Score)
            .ThenBy(match => match.Entry.ClientLabel, StringComparer.OrdinalIgnoreCase)
            .ThenBy(match => match.Entry.Name.Length)
            .ThenBy(match => match.Entry.Name, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .ToList();
    }

    private static SearchScore Score(SessionEntry entry, string query)
    {
        var nameMatch = FuzzySearch(query, entry.Name);
        var bestScore = new SearchScore(nameMatch.Score, nameMatch.MatchData);

        bestScore = Max(bestScore, FuzzySearch(query, entry.HostName), weight: 2);
        bestScore = Max(bestScore, FuzzySearch(query, entry.UserName), weight: 2);
        bestScore = Max(bestScore, FuzzySearch(query, entry.HostLabel), weight: 2);
        bestScore = Max(bestScore, FuzzySearch(query, entry.Protocol), weight: 3);
        bestScore = Max(bestScore, FuzzySearch(query, entry.ClientLabel), weight: 3);

        return bestScore;
    }

    private static SearchScore Max(SearchScore current, MatchResult match, int weight)
    {
        var weightedScore = match.Score / weight;
        if (weightedScore <= current.Score)
        {
            return current;
        }

        return new SearchScore(weightedScore, TitleHighlightData: null);
    }

    private static MatchResult FuzzySearch(string query, string text)
    {
        var match = FuzzyMatch(query, text);
        if (match.Score > 0 || string.IsNullOrWhiteSpace(text))
        {
            return match;
        }

        var compactText = CompactSearchText(text);
        if (compactText.Text.Length == text.Length)
        {
            return match;
        }

        var compactMatch = FuzzyMatch(query, compactText.Text);
        if (compactMatch.Score <= 0 || compactMatch.MatchData.Count == 0)
        {
            return match;
        }

        return new MatchResult(
            compactMatch.Success,
            compactMatch.SearchPrecision,
            compactMatch.MatchData.Select(index => compactText.IndexMap[index]).ToList(),
            compactMatch.RawScore);
    }

    private static MatchResult FuzzyMatch(string query, string text)
    {
        if (StringMatcher.Instance is not null)
        {
            return StringMatcher.Instance.FuzzyMatch(query, text);
        }

        return new StringMatcher { UserSettingSearchPrecision = StringMatcher.SearchPrecisionScore.Regular }
            .FuzzyMatch(query, text);
    }

    private static CompactSearchTextResult CompactSearchText(string text)
    {
        var builder = new StringBuilder(text.Length);
        var indexMap = new List<int>(text.Length);

        for (var i = 0; i < text.Length; i++)
        {
            if (!char.IsLetterOrDigit(text[i]))
            {
                continue;
            }

            builder.Append(text[i]);
            indexMap.Add(i);
        }

        return new CompactSearchTextResult(builder.ToString(), indexMap);
    }

    private sealed record SearchScore(int Score, IList<int>? TitleHighlightData);

    private sealed record CompactSearchTextResult(string Text, List<int> IndexMap);
}
