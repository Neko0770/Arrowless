namespace Arrowless;

internal static class Program
{
    internal const string HideArg = "--hide";
    internal const string ShowArg = "--show";

    [STAThread]
    private static int Main(string[] args)
    {
        // Helper mode: launched elevated only to change the registry, then exits.
        if (args.Length > 0 && (args[0] == HideArg || args[0] == ShowArg))
        {
            try
            {
                if (args[0] == HideArg) ArrowService.Remove();
                else ArrowService.Restore();
                return 0;
            }
            catch
            {
                return 1;
            }
        }

        // Permanent tray icon. On startup it checks the registry and recreates the value if missing.
        using var mutex = new Mutex(true, @"Local\Arrowless.SingleInstance", out bool isFirst);
        if (!isFirst) return 0;

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayContext());
        return 0;
    }
}
