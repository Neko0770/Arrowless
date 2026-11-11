using Microsoft.Win32;

namespace Arrowless;

/// <summary>User preferences (HKCU, no administrator rights needed).</summary>
internal static class Settings
{
    private const string AppKey = @"Software\Arrowless";
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunName = "Arrowless";

    /// <summary>Arrows are hidden by default.</summary>
    public static bool HideArrows
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(AppKey);
            return (key?.GetValue("HideArrows") as int? ?? 1) == 1;
        }
        set
        {
            using var key = Registry.CurrentUser.CreateSubKey(AppKey);
            key.SetValue("HideArrows", value ? 1 : 0, RegistryValueKind.DWord);
        }
    }

    public static bool IsFirstRun
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(AppKey);
            return key?.GetValue("Initialized") is null;
        }
    }

    public static void MarkInitialized()
    {
        using var key = Registry.CurrentUser.CreateSubKey(AppKey);
        key.SetValue("Initialized", 1, RegistryValueKind.DWord);
    }

    public static bool StartWithWindows
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(RunName) is string;
        }
        set
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey);
            if (value)
                key.SetValue(RunName, $"\"{Environment.ProcessPath}\"", RegistryValueKind.String);
            else
                key.DeleteValue(RunName, throwOnMissingValue: false);
        }
    }
}
