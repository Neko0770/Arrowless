using System.ComponentModel;
using System.Diagnostics;

namespace Arrowless;

/// <summary>System tray icon with a context menu. There is no main window.</summary>
internal sealed class TrayContext : ApplicationContext
{
    private enum HelperResult { Success, Cancelled, Failed }

    private readonly NotifyIcon _tray;
    private readonly ToolStripMenuItem _statusItem = new() { Enabled = false };
    private readonly ToolStripMenuItem _showArrowsItem = new("Show arrows") { CheckOnClick = false };
    private readonly ToolStripMenuItem _startupItem = new("Start with Windows") { CheckOnClick = false };
    private readonly Icon _icon = LoadIcon();
    private bool _busy;

    public TrayContext()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add(_statusItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_showArrowsItem);
        menu.Items.Add(_startupItem);
        menu.Items.Add(new ToolStripSeparator());
        var exit = new ToolStripMenuItem("Exit");
        exit.Click += (_, _) => ExitThread();
        menu.Items.Add(exit);

        _showArrowsItem.Click += async (_, _) => await ToggleArrowsAsync();
        _startupItem.Click += (_, _) => ToggleStartup();

        _tray = new NotifyIcon
        {
            Icon = _icon,
            ContextMenuStrip = menu,
            Visible = true
        };

        if (Settings.IsFirstRun)
        {
            TrySetStartup(true); // typical tray-app behaviour
            Settings.MarkInitialized();
        }

        RefreshUi();
        _ = EnsureDesiredStateAsync();
    }

    /// <summary>Applies the desired state (default: no arrows), e.g. after a Windows update reverted it.</summary>
    private async Task EnsureDesiredStateAsync()
    {
        bool wantHidden = Settings.HideArrows;
        if (wantHidden == ArrowService.IsRemoved()) return;
        await ApplyAsync(wantHidden);
    }

    private async Task ToggleArrowsAsync()
    {
        bool newHide = !Settings.HideArrows; // arrows hidden now -> the user wants to see them, and vice versa
        if (await ApplyAsync(newHide))
            Settings.HideArrows = newHide;
        RefreshUi();
    }

    private void ToggleStartup()
    {
        TrySetStartup(!Settings.StartWithWindows);
        RefreshUi();
    }

    private void TrySetStartup(bool enable)
    {
        try
        {
            Settings.StartWithWindows = enable;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            Notify("Could not change the \"Start with Windows\" setting.");
        }
    }

    private async Task<bool> ApplyAsync(bool hide)
    {
        if (_busy) return false;
        _busy = true;
        _showArrowsItem.Enabled = false;
        try
        {
            var result = await Task.Run(() => RunElevatedHelper(hide ? Program.HideArg : Program.ShowArg));
            if (result == HelperResult.Failed)
                Notify("Could not change the shortcut arrows. Please try again.");
            return result == HelperResult.Success;
        }
        finally
        {
            _busy = false;
            _showArrowsItem.Enabled = true;
            RefreshUi();
        }
    }

    /// <summary>Relaunches this same exe with UAC just to write to the registry (HKLM).</summary>
    private static HelperResult RunElevatedHelper(string arg)
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo(Environment.ProcessPath!, arg)
            {
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden
            });
            p?.WaitForExit();
            return p?.ExitCode == 0 ? HelperResult.Success : HelperResult.Failed;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223) // ERROR_CANCELLED: the user declined UAC
        {
            return HelperResult.Cancelled;
        }
        catch
        {
            return HelperResult.Failed;
        }
    }

    private void Notify(string message) =>
        _tray.ShowBalloonTip(5000, "Arrowless", message, ToolTipIcon.Warning);

    private void RefreshUi()
    {
        bool hidden = ArrowService.IsRemoved();
        _statusItem.Text = hidden ? "Arrows: hidden" : "Arrows: visible";
        _showArrowsItem.Checked = !hidden;
        _startupItem.Checked = Settings.StartWithWindows;
        _tray.Text = hidden ? "Arrowless - arrows hidden" : "Arrowless - arrows visible";
    }

    /// <summary>Loads the embedded icon, picking the size that matches the tray (DPI aware).</summary>
    private static Icon LoadIcon()
    {
        using var stream = typeof(TrayContext).Assembly.GetManifestResourceStream("app.ico")
            ?? throw new InvalidOperationException("The embedded icon is missing.");
        return new Icon(stream, SystemInformation.SmallIconSize);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _tray.Visible = false;
            _tray.Dispose();
            _icon.Dispose();
        }
        base.Dispose(disposing);
    }
}
