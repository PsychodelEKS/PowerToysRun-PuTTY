using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Community.PowerToys.Run.Plugin.PuTTY.Models;

namespace Community.PowerToys.Run.Plugin.PuTTY.Services;

public sealed class SettingsService
{
    private const string PluginName = "PuTTY";
    private const string PluginId = "1e79e695edd147bfb36f103fe5f9faef";
    private const string ActionKeyword = "putty";
    private const string Website = "https://github.com/PsychodelEKS/PowerToysRun-PuTTY";
    private const int CheckboxOptionType = 0;
    private const int TextboxOptionType = 2;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    public string SettingsDirectory { get; }

    public string SettingsPath => Path.Combine(SettingsDirectory, "settings.json");

    public string IndexPath => Path.Combine(SettingsDirectory, "index.json");

    public string PowerToysRunSettingsPath { get; }

    public SettingsService()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var powerToysRunDirectory = Path.Combine(localAppData, "Microsoft", "PowerToys", "PowerToys Run");
        SettingsDirectory = Path.Combine(
            powerToysRunDirectory,
            "Settings",
            "Plugins",
            "Community.PowerToys.Run.Plugin.PuTTY");
        PowerToysRunSettingsPath = Path.Combine(powerToysRunDirectory, "settings.json");
    }

    public PuTTYSettings LoadSettings()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                return PuTTYSettings.CreateDefault();
            }

            var json = File.ReadAllText(SettingsPath);
            return JsonSerializer.Deserialize<PuTTYSettings>(json, JsonOptions) ?? PuTTYSettings.CreateDefault();
        }
        catch
        {
            return PuTTYSettings.CreateDefault();
        }
    }

    public void SaveSettings(PuTTYSettings settings)
    {
        Directory.CreateDirectory(SettingsDirectory);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, JsonOptions));
    }

    public IReadOnlyList<SessionEntry> LoadIndex()
    {
        try
        {
            if (!File.Exists(IndexPath))
            {
                return [];
            }

            var json = File.ReadAllText(IndexPath);
            return JsonSerializer.Deserialize<List<SessionEntry>>(json, JsonOptions) ?? [];
        }
        catch
        {
            return [];
        }
    }

    public void SaveIndex(IReadOnlyList<SessionEntry> entries)
    {
        Directory.CreateDirectory(SettingsDirectory);
        File.WriteAllText(IndexPath, JsonSerializer.Serialize(entries, JsonOptions));
    }

    public void SavePowerToysPluginOptions(PuTTYSettings settings)
    {
        if (!File.Exists(PowerToysRunSettingsPath))
        {
            return;
        }

        try
        {
            var root = JsonNode.Parse(File.ReadAllText(PowerToysRunSettingsPath)) as JsonObject;
            var plugins = root?["plugins"] as JsonArray;
            if (root is null || plugins is null)
            {
                return;
            }

            var plugin = FindPlugin(plugins);
            if (plugin is null)
            {
                return;
            }

            plugin["IsGlobal"] = settings.EnableGlobalResults;
            plugin["Id"] = PluginId;
            plugin["Name"] = PluginName;
            plugin["ActionKeyword"] = ActionKeyword;
            plugin["Author"] = "PsychodelEKS";
            plugin["Website"] = Website;

            if (plugin["AdditionalOptions"] is not JsonArray additionalOptions)
            {
                additionalOptions = [];
                plugin["AdditionalOptions"] = additionalOptions;
            }

            SetTextboxOption(
                additionalOptions,
                Main.PuTTYExecutablePathOptionKey,
                "PuTTY executable path",
                "Path to putty.exe. You can use putty.exe if it is available in PATH.",
                settings.PuTTYExecutablePath,
                @"C:\Program Files\PuTTY\putty.exe");
            SetTextboxOption(
                additionalOptions,
                Main.KiTTYExecutablePathOptionKey,
                "KiTTY executable path",
                "Path to kitty.exe. You can use kitty.exe if it is available in PATH.",
                settings.KiTTYExecutablePath,
                @"T:\Apps\KiTTY\kitty.exe");
            SetCheckboxOption(
                additionalOptions,
                Main.EnablePuTTYSessionsOptionKey,
                "Enable PuTTY sessions",
                "Read sessions from HKCU\\Software\\SimonTatham\\PuTTY\\Sessions.",
                settings.EnablePuTTYSessions);
            SetCheckboxOption(
                additionalOptions,
                Main.EnableKiTTYSessionsOptionKey,
                "Enable KiTTY sessions",
                "Read sessions from HKCU\\Software\\9bis.com\\KiTTY\\Sessions.",
                settings.EnableKiTTYSessions);
            SetCheckboxOption(
                additionalOptions,
                Main.EnableFileSessionsOptionKey,
                "Enable file sessions",
                "Read session files from a configured directory. Relative paths are resolved from the KiTTY executable directory.",
                settings.EnableFileSessions);
            SetTextboxOption(
                additionalOptions,
                Main.FileSessionsDirectoryOptionKey,
                "File sessions directory",
                "Folder with session files, for example T:\\Apps\\KiTTY\\Sessions or Sessions relative to kitty.exe.",
                settings.FileSessionsDirectory,
                "Sessions");

            File.WriteAllText(PowerToysRunSettingsPath, root.ToJsonString(JsonOptions));
        }
        catch
        {
        }
    }

    private static JsonObject? FindPlugin(JsonArray plugins)
    {
        var exact = plugins
            .OfType<JsonObject>()
            .FirstOrDefault(plugin =>
                string.Equals(plugin["Id"]?.GetValue<string>(), PluginId, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
        {
            return exact;
        }

        return plugins
            .OfType<JsonObject>()
            .FirstOrDefault(plugin =>
                string.Equals(plugin["Name"]?.GetValue<string>(), PluginName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(plugin["Website"]?.GetValue<string>(), Website, StringComparison.OrdinalIgnoreCase));
    }

    private static void SetTextboxOption(
        JsonArray options,
        string key,
        string label,
        string description,
        string value,
        string placeholder)
    {
        var option = FindOrCreateOption(options, key, TextboxOptionType, label, description);
        option["TextValue"] = value;
        option["PlaceholderText"] = placeholder;
    }

    private static void SetCheckboxOption(
        JsonArray options,
        string key,
        string label,
        string description,
        bool value)
    {
        var option = FindOrCreateOption(options, key, CheckboxOptionType, label, description);
        option["Value"] = value;
    }

    private static JsonObject FindOrCreateOption(
        JsonArray options,
        string key,
        int optionType,
        string label,
        string description)
    {
        var option = options
            .OfType<JsonObject>()
            .FirstOrDefault(item => string.Equals(item["Key"]?.GetValue<string>(), key, StringComparison.OrdinalIgnoreCase));

        if (option is null)
        {
            option = new JsonObject
            {
                ["PluginOptionType"] = optionType,
                ["Key"] = key,
                ["DisplayLabel"] = label,
                ["DisplayDescription"] = description,
                ["Value"] = false,
                ["ComboBoxValue"] = 0,
                ["NumberValue"] = 0,
            };
            options.Add(option);
        }

        option["PluginOptionType"] = optionType;
        option["DisplayLabel"] = label;
        option["DisplayDescription"] = description;
        return option;
    }
}
