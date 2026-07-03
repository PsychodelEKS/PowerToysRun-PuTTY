using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Community.PowerToys.Run.Plugin.PuTTY.Models;
using Community.PowerToys.Run.Plugin.PuTTY.Services;
using Community.PowerToys.Run.Plugin.PuTTY.UI;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Wox.Infrastructure;
using Wox.Plugin;

namespace Community.PowerToys.Run.Plugin.PuTTY;

public sealed class Main : IPlugin, IPluginI18n, IContextMenu, ISettingProvider, IReloadable, IDisposable, IDelayedExecutionPlugin
{
    private const string DefaultActionKeyword = "putty";
    private const string RescanCommand = "rescan";
    private const string SettingsCommand = "settings";
    private const int ExactCommandScore = 1100;
    private const int UnmatchedCommandScore = 1;

    public const string PuTTYExecutablePathOptionKey = "puttyExecutablePath";
    public const string KiTTYExecutablePathOptionKey = "kittyExecutablePath";
    public const string EnablePuTTYSessionsOptionKey = "enablePuTTYSessions";
    public const string EnableKiTTYSessionsOptionKey = "enableKiTTYSessions";
    public const string EnableFileSessionsOptionKey = "enableFileSessions";
    public const string FileSessionsDirectoryOptionKey = "fileSessionsDirectory";

    private PluginInitContext? _context;
    private SettingsService? _settingsService;
    private SessionIndexService? _indexService;
    private PuTTYSettings _settings = PuTTYSettings.CreateDefault();
    private string _iconPath = "Images\\PuTTY.light.png";
    private Window? _settingsWindow;
    private bool _disposed;

    public string Name => "PuTTY";

    public string Description => "Open PuTTY and KiTTY saved sessions.";

    public static string PluginID => "1e79e695edd147bfb36f103fe5f9faef";

    public IEnumerable<PluginAdditionalOption> AdditionalOptions
    {
        get
        {
            return
            [
                new()
                {
                    PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Textbox,
                    Key = PuTTYExecutablePathOptionKey,
                    DisplayLabel = "PuTTY executable path",
                    DisplayDescription = "Path to putty.exe. You can use putty.exe if it is available in PATH.",
                    TextValue = _settings.PuTTYExecutablePath,
                    PlaceholderText = @"C:\Program Files\PuTTY\putty.exe",
                },
                new()
                {
                    PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Textbox,
                    Key = KiTTYExecutablePathOptionKey,
                    DisplayLabel = "KiTTY executable path",
                    DisplayDescription = "Path to kitty.exe. You can use kitty.exe if it is available in PATH.",
                    TextValue = _settings.KiTTYExecutablePath,
                    PlaceholderText = @"T:\Apps\KiTTY\kitty.exe",
                },
                new()
                {
                    PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Checkbox,
                    Key = EnablePuTTYSessionsOptionKey,
                    DisplayLabel = "Enable PuTTY sessions",
                    DisplayDescription = @"Read sessions from HKCU\Software\SimonTatham\PuTTY\Sessions.",
                    Value = _settings.EnablePuTTYSessions,
                },
                new()
                {
                    PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Checkbox,
                    Key = EnableKiTTYSessionsOptionKey,
                    DisplayLabel = "Enable KiTTY sessions",
                    DisplayDescription = @"Read sessions from HKCU\Software\9bis.com\KiTTY\Sessions.",
                    Value = _settings.EnableKiTTYSessions,
                },
                new()
                {
                    PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Checkbox,
                    Key = EnableFileSessionsOptionKey,
                    DisplayLabel = "Enable file sessions",
                    DisplayDescription = "Read session files from a configured directory. Relative paths are resolved from the KiTTY executable directory.",
                    Value = _settings.EnableFileSessions,
                },
                new()
                {
                    PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Textbox,
                    Key = FileSessionsDirectoryOptionKey,
                    DisplayLabel = "File sessions directory",
                    DisplayDescription = @"Folder with session files, for example T:\Apps\KiTTY\Sessions or Sessions relative to kitty.exe.",
                    TextValue = _settings.FileSessionsDirectory,
                    PlaceholderText = "Sessions",
                },
            ];
        }
    }

    public void Init(PluginInitContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _context = context;
        _context.API.ThemeChanged += OnThemeChanged;
        UpdateIconPath(_context.API.GetCurrentTheme());

        _settingsService = new SettingsService();
        _settings = _settingsService.LoadSettings();
        _indexService = new SessionIndexService(_settingsService);
        _settingsService.SavePowerToysPluginOptions(_settings);
        _ = RescanInBackgroundAsync(showNotification: false);
    }

    public List<Result> Query(Query query)
    {
        return QueryInternal(query);
    }

    public List<Result> Query(Query query, bool delayedExecution)
    {
        return QueryInternal(query);
    }

    public List<ContextMenuResult> LoadContextMenus(Result selectedResult)
    {
        if (selectedResult.ContextData is not SessionEntry entry)
        {
            return [];
        }

        return
        [
            new ContextMenuResult
            {
                Title = "Open",
                Glyph = "\xE8A7",
                FontFamily = "Segoe Fluent Icons,Segoe MDL2 Assets",
                AcceleratorKey = Key.Enter,
                Action = _ => LaunchSession(entry),
            },
            new ContextMenuResult
            {
                Title = "Copy session name",
                Glyph = "\xE8C8",
                FontFamily = "Segoe Fluent Icons,Segoe MDL2 Assets",
                AcceleratorKey = Key.C,
                AcceleratorModifiers = ModifierKeys.Control,
                Action = _ =>
                {
                    System.Windows.Clipboard.SetText(entry.Name);
                    return true;
                },
            },
            new ContextMenuResult
            {
                Title = "Copy host",
                Glyph = "\xE8C8",
                FontFamily = "Segoe Fluent Icons,Segoe MDL2 Assets",
                AcceleratorKey = Key.H,
                AcceleratorModifiers = ModifierKeys.Control,
                Action = _ =>
                {
                    if (string.IsNullOrWhiteSpace(entry.HostName))
                    {
                        return false;
                    }

                    System.Windows.Clipboard.SetText(entry.HostName);
                    return true;
                },
            },
            new ContextMenuResult
            {
                Title = "Rescan sessions",
                Glyph = "\xE72C",
                FontFamily = "Segoe Fluent Icons,Segoe MDL2 Assets",
                AcceleratorKey = Key.R,
                AcceleratorModifiers = ModifierKeys.Control,
                Action = context =>
                {
                    _ = RescanInBackgroundAsync(showNotification: true);
                    return true;
                },
            },
        ];
    }

    public System.Windows.Controls.Control CreateSettingPanel()
    {
        return new SettingsPanel(
            _settings.Clone(),
            SaveSettingsFromGui,
            () => RescanInBackgroundAsync(showNotification: true));
    }

    public void UpdateSettings(PowerLauncherPluginSettings settings)
    {
        if (_settingsService is null)
        {
            return;
        }

        var updatedSettings = _settings.Clone();
        updatedSettings.EnableGlobalResults = settings.IsGlobal;
        updatedSettings.PuTTYExecutablePath = GetTextOption(settings, PuTTYExecutablePathOptionKey, updatedSettings.PuTTYExecutablePath);
        updatedSettings.KiTTYExecutablePath = GetTextOption(settings, KiTTYExecutablePathOptionKey, updatedSettings.KiTTYExecutablePath);
        updatedSettings.EnablePuTTYSessions = GetBoolOption(settings, EnablePuTTYSessionsOptionKey, updatedSettings.EnablePuTTYSessions);
        updatedSettings.EnableKiTTYSessions = GetBoolOption(settings, EnableKiTTYSessionsOptionKey, updatedSettings.EnableKiTTYSessions);
        updatedSettings.EnableFileSessions = GetBoolOption(settings, EnableFileSessionsOptionKey, updatedSettings.EnableFileSessions);
        updatedSettings.FileSessionsDirectory = GetTextOption(settings, FileSessionsDirectoryOptionKey, updatedSettings.FileSessionsDirectory);

        SaveSettings(updatedSettings);
        _settingsService.SavePowerToysPluginOptions(_settings);
        _ = RescanInBackgroundAsync(showNotification: false);
    }

    public void ReloadData()
    {
        if (_settingsService is null)
        {
            return;
        }

        _settings = _settingsService.LoadSettings();
        _ = RescanInBackgroundAsync(showNotification: false);
    }

    public string GetTranslatedPluginTitle()
    {
        return Name;
    }

    public string GetTranslatedPluginDescription()
    {
        return Description;
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    private List<Result> QueryInternal(Query query)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (_indexService is null)
        {
            return [];
        }

        var isKeywordQuery = IsKeywordQuery(query);
        var search = query.Search?.Trim() ?? string.Empty;

        if (!isKeywordQuery && !_settings.EnableGlobalResults)
        {
            return [];
        }

        if (!isKeywordQuery && string.IsNullOrWhiteSpace(search))
        {
            return [];
        }

        var results = new List<Result>();
        if (isKeywordQuery || !string.IsNullOrWhiteSpace(search))
        {
            results.AddRange(_indexService.Search(search)
                .Select(match => CreateSessionResult(match, search)));
        }

        if (isKeywordQuery)
        {
            results.AddRange(CreateCommandResults(search));
        }

        return results
            .OrderByDescending(result => result.Score)
            .ToList();
    }

    private Result CreateSessionResult(SearchMatch match, string query)
    {
        var entry = match.Entry;
        return new Result
        {
            Title = entry.Name,
            SubTitle = GetSessionSubtitle(entry),
            QueryTextDisplay = query,
            IcoPath = GetResultIconPath(entry),
            Score = match.Score,
            TitleHighlightData = match.TitleHighlightData,
            ContextData = entry,
            Action = _ => LaunchSession(entry),
        };
    }

    private IEnumerable<Result> CreateCommandResults(string query)
    {
        if (ShouldShowCommand(query, RescanCommand))
        {
            yield return CreateRescanResult(query, GetCommandScore(query, RescanCommand));
        }

        if (ShouldShowCommand(query, SettingsCommand))
        {
            yield return CreateSettingsResult(query, GetCommandScore(query, SettingsCommand));
        }
    }

    private Result CreateRescanResult(string query, int score)
    {
        var subtitle = _indexService?.IsRescanRunning == true
            ? "Session rescan is already running."
            : "Refresh registry and file-backed saved sessions in the background.";

        return new Result
        {
            Title = "Rescan PuTTY sessions",
            SubTitle = subtitle,
            QueryTextDisplay = query,
            IcoPath = _iconPath,
            Score = score,
            Action = context =>
            {
                _ = RescanInBackgroundAsync(showNotification: true);
                return true;
            },
        };
    }

    private Result CreateSettingsResult(string query, int score)
    {
        return new Result
        {
            Title = "Open PuTTY settings",
            SubTitle = "Edit executable paths, registry sources, and file sessions.",
            QueryTextDisplay = query,
            IcoPath = _iconPath,
            Score = score,
            Action = _ =>
            {
                OpenSettingsWindow();
                return true;
            },
        };
    }

    private async Task<int?> RescanInBackgroundAsync(bool showNotification)
    {
        if (_indexService is null)
        {
            return null;
        }

        try
        {
            var count = await _indexService.TryRescanAsync(_settings).ConfigureAwait(false);
            if (showNotification)
            {
                if (count is null)
                {
                    ShowNotification("PuTTY sessions", "Session rescan is already running.");
                }
                else
                {
                    ShowNotification("PuTTY sessions", $"Indexed {count.Value} session(s).");
                }
            }

            return count;
        }
        catch (Exception ex)
        {
            if (showNotification)
            {
                ShowNotification("PuTTY sessions", $"Rescan failed: {ex.Message}");
            }

            return null;
        }
    }

    private void SaveSettings(PuTTYSettings settings)
    {
        _settings = settings.Clone();
        _settingsService?.SaveSettings(_settings);
    }

    private void SaveSettingsFromGui(PuTTYSettings settings)
    {
        SaveSettings(settings);
        _settingsService?.SavePowerToysPluginOptions(_settings);
    }

    private void OpenSettingsWindow()
    {
        if (_context is null)
        {
            return;
        }

        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            if (_settingsWindow is not null)
            {
                _settingsWindow.Activate();
                return;
            }

            _settingsWindow = new Window
            {
                Title = "PuTTY settings",
                Width = 760,
                Height = 430,
                MinWidth = 640,
                MinHeight = 360,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Content = new SettingsPanel(
                    _settings.Clone(),
                    SaveSettingsFromGui,
                    () => RescanInBackgroundAsync(showNotification: true)),
            };
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
            _settingsWindow.Show();
            _settingsWindow.Activate();
        });
    }

    private string GetSessionSubtitle(SessionEntry entry)
    {
        var executableProblem = GetExecutableProblem(entry.ClientKind);
        if (!string.IsNullOrWhiteSpace(executableProblem))
        {
            return $"{executableProblem} {entry.SourceLabel} session: {entry.HostLabel}";
        }

        var protocol = string.IsNullOrWhiteSpace(entry.Protocol) ? string.Empty : $" ({entry.Protocol})";
        return $"{entry.ClientLabel} {entry.SourceLabel}: {entry.HostLabel}{protocol}";
    }

    private bool LaunchSession(SessionEntry entry)
    {
        var executablePath = NormalizeExecutablePath(GetExecutablePath(entry.ClientKind));
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            ShowNotification(entry.ClientLabel, $"{entry.ClientLabel} executable path is not configured.");
            return false;
        }

        if (IsRootedMissingFile(executablePath))
        {
            ShowNotification(entry.ClientLabel, $"Executable not found: {executablePath}");
            return false;
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                UseShellExecute = false,
            };
            startInfo.ArgumentList.Add("-load");
            startInfo.ArgumentList.Add(entry.Name);

            Process.Start(startInfo);
            return true;
        }
        catch (Exception ex)
        {
            ShowNotification(entry.ClientLabel, $"Failed to launch {entry.Name}: {ex.Message}");
            return false;
        }
    }

    private string GetExecutableProblem(ClientKind clientKind)
    {
        var executablePath = NormalizeExecutablePath(GetExecutablePath(clientKind));
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return $"{GetClientLabel(clientKind)} executable path is not configured.";
        }

        return IsRootedMissingFile(executablePath)
            ? $"{GetClientLabel(clientKind)} executable not found: {executablePath}."
            : string.Empty;
    }

    private string GetExecutablePath(ClientKind clientKind)
    {
        return clientKind == ClientKind.KiTTY
            ? _settings.KiTTYExecutablePath
            : _settings.PuTTYExecutablePath;
    }

    private string GetResultIconPath(SessionEntry entry)
    {
        var executablePath = NormalizeExecutablePath(GetExecutablePath(entry.ClientKind));
        var sessionIconPath = ResolveIconPath(
            entry.IconPath,
            GetDirectoryName(entry.SourcePath),
            GetDirectoryName(executablePath));
        if (!string.IsNullOrWhiteSpace(sessionIconPath))
        {
            return sessionIconPath;
        }

        var executableIconPath = ResolveExecutablePath(executablePath);
        return string.IsNullOrWhiteSpace(executableIconPath) ? _iconPath : executableIconPath;
    }

    private static string ResolveIconPath(string iconPath, params string[] baseDirectories)
    {
        iconPath = NormalizeExecutablePath(TryDecode(iconPath.Trim()));
        if (string.IsNullOrWhiteSpace(iconPath))
        {
            return string.Empty;
        }

        iconPath = Environment.ExpandEnvironmentVariables(iconPath);
        var withoutIconIndex = StripIconIndex(iconPath);

        foreach (var candidate in EnumerateIconCandidates(iconPath, withoutIconIndex, baseDirectories))
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return string.Empty;
    }

    private static IEnumerable<string> EnumerateIconCandidates(
        string iconPath,
        string withoutIconIndex,
        IEnumerable<string> baseDirectories)
    {
        yield return iconPath;
        if (!string.Equals(withoutIconIndex, iconPath, StringComparison.Ordinal))
        {
            yield return withoutIconIndex;
        }

        var iconFileName = Path.GetFileName(withoutIconIndex);

        foreach (var baseDirectory in baseDirectories.Where(directory => !string.IsNullOrWhiteSpace(directory)))
        {
            if (!Path.IsPathRooted(iconPath))
            {
                yield return Path.Combine(baseDirectory, iconPath);
                yield return Path.Combine(baseDirectory, withoutIconIndex);
            }

            if (!string.IsNullOrWhiteSpace(iconFileName))
            {
                yield return Path.Combine(baseDirectory, iconFileName);
                yield return Path.Combine(baseDirectory, "Ico", iconFileName);
            }
        }
    }

    private static string ResolveExecutablePath(string executablePath)
    {
        executablePath = NormalizeExecutablePath(executablePath);
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return string.Empty;
        }

        if (Path.IsPathRooted(executablePath))
        {
            return File.Exists(executablePath) ? executablePath : string.Empty;
        }

        var pathEnvironment = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var directory in pathEnvironment.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var candidate = Path.Combine(directory.Trim(), executablePath);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
            catch
            {
            }
        }

        return string.Empty;
    }

    private static string StripIconIndex(string iconPath)
    {
        var commaIndex = iconPath.LastIndexOf(",", StringComparison.Ordinal);
        if (commaIndex < 0 || commaIndex == iconPath.Length - 1)
        {
            return iconPath;
        }

        var suffix = iconPath[(commaIndex + 1)..];
        return int.TryParse(suffix, out _) ? iconPath[..commaIndex] : iconPath;
    }

    private static string TryDecode(string value)
    {
        try
        {
            return Uri.UnescapeDataString(value);
        }
        catch
        {
            return value;
        }
    }

    private static string GetDirectoryName(string path)
    {
        path = NormalizeExecutablePath(path);
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path))
        {
            return string.Empty;
        }

        try
        {
            return Path.GetDirectoryName(path) ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string GetClientLabel(ClientKind clientKind)
    {
        return clientKind == ClientKind.KiTTY ? "KiTTY" : "PuTTY";
    }

    private static bool IsRootedMissingFile(string path)
    {
        return Path.IsPathRooted(path) && !File.Exists(path);
    }

    private static string NormalizeExecutablePath(string path)
    {
        path = path.Trim();
        if (path.Length >= 2 &&
            ((path[0] == '"' && path[^1] == '"') || (path[0] == '\'' && path[^1] == '\'')))
        {
            return path[1..^1].Trim();
        }

        return path;
    }

    private void ShowNotification(string title, string message)
    {
        try
        {
            _context?.API.ShowNotification(title, message);
        }
        catch
        {
        }
    }

    private static string GetTextOption(PowerLauncherPluginSettings settings, string key, string defaultValue)
    {
        var option = settings.AdditionalOptions?
            .FirstOrDefault(item => string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase));
        return option is null ? defaultValue : option.TextValue ?? string.Empty;
    }

    private static bool GetBoolOption(PowerLauncherPluginSettings settings, string key, bool defaultValue)
    {
        var option = settings.AdditionalOptions?
            .FirstOrDefault(item => string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase));
        return option?.Value ?? defaultValue;
    }

    private static bool IsKeywordQuery(Query query)
    {
        if (!string.IsNullOrWhiteSpace(query.ActionKeyword) &&
            !string.Equals(query.ActionKeyword, "*", StringComparison.Ordinal))
        {
            return true;
        }

        var rawQuery = query.RawQuery ?? string.Empty;
        return rawQuery.Equals(DefaultActionKeyword, StringComparison.OrdinalIgnoreCase)
            || rawQuery.StartsWith($"{DefaultActionKeyword} ", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ShouldShowCommand(string search, string command)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return true;
        }

        return command.StartsWith(search, StringComparison.OrdinalIgnoreCase)
            || search.StartsWith(command, StringComparison.OrdinalIgnoreCase)
            || !search.Contains(' ', StringComparison.Ordinal);
    }

    private static int GetCommandScore(string search, string command)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return UnmatchedCommandScore;
        }

        if (search.Equals(command, StringComparison.OrdinalIgnoreCase))
        {
            return ExactCommandScore;
        }

        var match = FuzzySearch(search, command);
        return match.Score > 0 ? match.Score : UnmatchedCommandScore;
    }

    private static MatchResult FuzzySearch(string query, string text)
    {
        if (StringMatcher.Instance is not null)
        {
            return StringMatcher.Instance.FuzzyMatch(query, text);
        }

        return new StringMatcher { UserSettingSearchPrecision = StringMatcher.SearchPrecisionScore.Regular }
            .FuzzyMatch(query, text);
    }

    private void OnThemeChanged(Theme oldTheme, Theme newTheme)
    {
        UpdateIconPath(newTheme);
    }

    private void UpdateIconPath(Theme theme)
    {
        _iconPath = theme is Theme.Light or Theme.HighContrastWhite
            ? "Images\\PuTTY.light.png"
            : "Images\\PuTTY.dark.png";
    }

    private void Dispose(bool disposing)
    {
        if (_disposed || !disposing)
        {
            return;
        }

        if (_context is not null)
        {
            _context.API.ThemeChanged -= OnThemeChanged;
        }

        _settingsWindow?.Close();
        _settingsWindow = null;

        _disposed = true;
    }
}
