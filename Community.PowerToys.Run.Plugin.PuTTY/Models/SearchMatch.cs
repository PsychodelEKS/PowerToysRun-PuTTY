namespace Community.PowerToys.Run.Plugin.PuTTY.Models;

public sealed class SearchMatch
{
    public required SessionEntry Entry { get; init; }

    public required int Score { get; init; }

    public IList<int>? TitleHighlightData { get; init; }
}
