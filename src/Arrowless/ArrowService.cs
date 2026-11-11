using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Arrowless;

/// <summary>
/// Hides/restores the shortcut arrow overlay using value "29" of the "Shell Icons" key
/// (pointing to the transparent icon -50 of shell32.dll).
/// It only touches that value, and only removes it if it is the one this app wrote.
/// </summary>
internal static class ArrowService
{
    private const string KeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Shell Icons";
    private const string ValueName = "29";
    // Transparent icon that ships with Windows (no file needs to be created).
    private const string IconValue = @"%windir%\System32\shell32.dll,-50";

    /// <summary>True if the arrows are currently hidden by this app.</summary>
    public static bool IsRemoved()
    {
        using var key = Registry.LocalMachine.OpenSubKey(KeyPath);
        return string.Equals(key?.GetValue(ValueName) as string, IconValue, StringComparison.OrdinalIgnoreCase);
    }

    public static void Remove()
    {
        using var key = Registry.LocalMachine.CreateSubKey(KeyPath, writable: true)
            ?? throw new InvalidOperationException("Could not open the registry key.");
        key.SetValue(ValueName, IconValue, RegistryValueKind.String);

        RefreshShell();
    }

    public static void Restore()
    {
        using (var key = Registry.LocalMachine.OpenSubKey(KeyPath, writable: true))
        {
            // Only delete the value if this app wrote it.
            if (string.Equals(key?.GetValue(ValueName) as string, IconValue, StringComparison.OrdinalIgnoreCase))
                key!.DeleteValue(ValueName, throwOnMissingValue: false);
        }

        RefreshShell();
    }

    private static void RefreshShell()
    {
        // Rebuild the icon cache.
        try
        {
            using var p = Process.Start(new ProcessStartInfo("ie4uinit.exe", "-show")
            { CreateNoWindow = true, UseShellExecute = false });
            p?.WaitForExit(5000);
        }
        catch { /* not critical */ }

        SHChangeNotify(0x08000000 /* SHCNE_ASSOCCHANGED */, 0, IntPtr.Zero, IntPtr.Zero);

        // Windows restarts the shell automatically when it is closed.
        foreach (var p in Process.GetProcessesByName("explorer"))
        {
            try { p.Kill(); } catch { /* ignore */ }
        }
    }

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(int eventId, uint flags, IntPtr item1, IntPtr item2);
}
