namespace Community.PowerToys.Run.Plugin.PuTTY.Models;

public sealed class PuTTYSettings
{
    public bool EnableGlobalResults { get; set; }

    public string PuTTYExecutablePath { get; set; } = "putty.exe";

    public string KiTTYExecutablePath { get; set; } = "kitty.exe";

    public bool EnablePuTTYSessions { get; set; } = true;

    public bool EnableKiTTYSessions { get; set; } = true;

    public bool EnableFileSessions { get; set; } = true;

    public string FileSessionsDirectory { get; set; } = "Sessions";

    public static PuTTYSettings CreateDefault()
    {
        return new PuTTYSettings();
    }

    public PuTTYSettings Clone()
    {
        return new PuTTYSettings
        {
            EnableGlobalResults = EnableGlobalResults,
            PuTTYExecutablePath = PuTTYExecutablePath,
            KiTTYExecutablePath = KiTTYExecutablePath,
            EnablePuTTYSessions = EnablePuTTYSessions,
            EnableKiTTYSessions = EnableKiTTYSessions,
            EnableFileSessions = EnableFileSessions,
            FileSessionsDirectory = FileSessionsDirectory,
        };
    }
}
