using MatrixScreensaver.Core;
using MatrixScreensaver.Forms;

namespace MatrixScreensaver;

/// <summary>
/// Application entry point.
/// Dispatches to the correct mode based on standard Windows screensaver
/// command-line arguments:
/// <list type="bullet">
///   <item><c>/s</c> - start the screensaver (full-screen).</item>
///   <item><c>/c</c> - show the configuration dialog.</item>
///   <item><c>/p &lt;hwnd&gt;</c> - preview inside the Display Properties thumbnail.</item>
///   <item>(no args) - treat as /s for easy debugging in the IDE.</item>
/// </list>
/// </summary>
internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        // Parse the first argument (case-insensitive, strip leading - or /).
        string mode = args.Length > 0
            ? args[0].TrimStart('/', '-').ToUpperInvariant()
            : "S";

        // Strip a colon suffix, e.g. "/c:12345" -> "C"
        int colonIdx = mode.IndexOf(':');
        if (colonIdx >= 0)
            mode = mode[..colonIdx];

        ScreenSaverSettings settings = ScreenSaverSettings.Load();

        switch (mode)
        {
            case "S":
                MatrixScreenSaverForm.RunOnAllScreens(settings);
                break;

            case "C":
                using (var dlg = new SettingsDialog(settings))
                    dlg.ShowDialog();
                break;

            case "P":
                if (args.Length > 1 && long.TryParse(args[1], out long hwnd))
                    ShowPreview(hwnd, settings);
                else
                    ShowPreview(0, settings);
                break;

            default:
                MatrixScreenSaverForm.RunOnAllScreens(settings);
                break;
        }
    }

    /// <summary>
    /// Runs a preview-mode form. For a full implementation the form would be
    /// re-parented to the Display Properties preview window via SetParent.
    /// </summary>
    private static void ShowPreview(long previewHwnd, ScreenSaverSettings settings)
    {
        Screen primary = Screen.PrimaryScreen ?? Screen.AllScreens[0];
        using var form = new MatrixScreenSaverForm(primary, settings, isPrimary: true);
        form.FormBorderStyle = FormBorderStyle.None;
        form.Size            = new Size(320, 240);
        form.StartPosition   = FormStartPosition.CenterScreen;
        Application.Run(form);
    }
}
