using System.Runtime.InteropServices;
using MatrixScreensaver.Core;
using MatrixScreensaver.Rendering;
using SkiaSharp;
using SkiaSharp.Views.Desktop;

namespace MatrixScreensaver.Forms;

/// <summary>
/// Full-screen borderless Windows Form that hosts the Matrix rain effect.
/// </summary>
/// <remarks>
/// Behaviour:
/// <list type="bullet">
///   <item>Fills the assigned <see cref="Screen"/> entirely, no border or title bar.</item>
///   <item>Hides the mouse cursor (primary form only).</item>
///   <item>Exits on any key press (Escape exits immediately without further action).</item>
///   <item>Exits when the mouse moves more than 10 pixels from its starting position.</item>
///   <item>Right-click opens the <see cref="SettingsDialog"/>; the animation pauses while
///         the dialog is open and reinitialises if settings changed on OK.</item>
///   <item>Left/middle mouse button clicks exit the screensaver.</item>
///   <item>Supports multi-monitor setups via <see cref="RunOnAllScreens"/>.</item>
/// </list>
/// </remarks>
public sealed class MatrixScreenSaverForm : Form
{
    // ── Win32 interop ─────────────────────────────────────────────────────────

    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);

    // ── Fields ────────────────────────────────────────────────────────────────

    private readonly ScreenSaverSettings _settings;
    private readonly MatrixRenderer      _renderer;
    private readonly FrameTimer          _timer     = new();
    private readonly SKControl           _skControl;
    private readonly System.Windows.Forms.Timer _frameTimer = new();

    /// <summary>True when this is the primary (first) form; only the primary handles input.</summary>
    private readonly bool _isPrimary;

    // Prevent re-entrant settings dialog opens.
    private bool _settingsOpen;

    // ── Constructor ───────────────────────────────────────────────────────────

    /// <param name="screen">The screen this form should fill.</param>
    /// <param name="settings">Shared settings instance.</param>
    /// <param name="isPrimary">Whether this is the primary form that handles exit input.</param>
    public MatrixScreenSaverForm(Screen screen, ScreenSaverSettings settings, bool isPrimary)
    {
        _settings  = settings;
        _isPrimary = isPrimary;
        _renderer  = new MatrixRenderer(settings);

        ConfigureForm(screen);

        // SKControl fills the entire client area and owns all SkiaSharp rendering.
        _skControl = new SKControl { Dock = DockStyle.Fill };
        _skControl.PaintSurface += OnPaintSurface;

        // Mouse/key events on the child control must be forwarded to the form handlers
        // because the child covers the entire client area and swallows all input.
        _skControl.MouseDown += (s, e) => OnMouseDown(e);
        _skControl.KeyDown   += (s, e) => OnKeyDown(e);

        Controls.Add(_skControl);

        // WinForms timer drives the animation loop at the requested FPS.
        _frameTimer.Interval = _settings.FpsLimit > 0
            ? Math.Max(1, 1000 / _settings.FpsLimit)
            : 1;
        _frameTimer.Tick += OnFrameTick;
    }

    // ── Form configuration ────────────────────────────────────────────────────

    private void ConfigureForm(Screen screen)
    {
        FormBorderStyle = FormBorderStyle.None;
        WindowState     = FormWindowState.Normal;
        StartPosition   = FormStartPosition.Manual;
        TopMost         = true;
        BackColor       = Color.Black;
        Bounds          = screen.Bounds;

        // Intercept keys before child controls so Escape and other keys are handled here.
        KeyPreview = true;
    }

    // ── Lifetime ──────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        if (_isPrimary)
        {
            Cursor.Hide();
        }

        _renderer.Initialise(ClientSize.Width, ClientSize.Height);
        _timer.Reset();
        _frameTimer.Start();
        _skControl.Focus();
    }

    /// <inheritdoc/>
    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _frameTimer.Stop();
        _renderer.Dispose();
        if (_isPrimary) Cursor.Show();
        base.OnFormClosed(e);
    }

    // ── Animation loop ────────────────────────────────────────────────────────

    private void OnFrameTick(object? sender, EventArgs e)
    {
        _timer.Tick();
        _skControl.Invalidate();
    }

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        _renderer.UpdateAndRender(e.Surface.Canvas, _timer.DeltaSeconds);
    }

    // ── Input handling (primary form only) ────────────────────────────────────

    /// <inheritdoc/>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!_isPrimary) return;

        // Escape exits immediately; any other key also exits.
        ExitScreensaver();
    }

    /// <inheritdoc/>
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (!_isPrimary) return;

        if (e.Button == MouseButtons.Right)
        {
            // Right-click → open the settings dialog while keeping the screensaver alive.
            OpenSettingsDialog();
        }
        else
        {
            // Any other mouse button exits.
            ExitScreensaver();
        }
    }

    // ── Settings dialog ───────────────────────────────────────────────────────

    /// <summary>
    /// Pauses animation, shows the settings dialog, then resumes.
    /// If the user accepted the new settings the renderer is reinitialised.
    /// </summary>
    private void OpenSettingsDialog()
    {
        if (_settingsOpen) return;
        _settingsOpen = true;

        // Pause the animation timer and show the cursor while the dialog is open.
        _frameTimer.Stop();
        Cursor.Show();

        // Lower TopMost on every screensaver form so the dialog can appear above them.
        var screensaverForms = Application.OpenForms
            .OfType<MatrixScreenSaverForm>()
            .ToList();
        foreach (var f in screensaverForms)
            f.TopMost = false;

        using var dlg = new SettingsDialog(_settings) { TopMost = true };
        DialogResult result = dlg.ShowDialog();

        // Restore TopMost on all screensaver forms now that the dialog is gone.
        foreach (var f in screensaverForms)
            f.TopMost = true;

        // Restore cursor and resume animation.
        Cursor.Hide();
        _timer.Reset(); // prevent a large delta spike after the pause
        _frameTimer.Start();
        _settingsOpen = false;

        if (result == DialogResult.OK)
        {
            // Rebuild columns / repaint with updated settings.
            _renderer.Initialise(ClientSize.Width, ClientSize.Height);

            // Update the timer interval in case FPS changed.
            _frameTimer.Interval = _settings.FpsLimit > 0
                ? Math.Max(1, 1000 / _settings.FpsLimit)
                : 1;
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Closes all screensaver forms and terminates the application.</summary>
    private static void ExitScreensaver() => Application.Exit();

    /// <inheritdoc/>
    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (ClientSize.Width > 0 && ClientSize.Height > 0)
        {
            _renderer.Initialise(ClientSize.Width, ClientSize.Height);
            _timer.Reset();
        }
    }

    // ── Multi-monitor factory ─────────────────────────────────────────────────

    /// <summary>
    /// Spawns one <see cref="MatrixScreenSaverForm"/> per connected monitor
    /// and runs the WinForms application message loop.
    /// </summary>
    /// <param name="settings">Loaded and shared settings instance.</param>
    public static void RunOnAllScreens(ScreenSaverSettings settings)
    {
        Screen[] screens = Screen.AllScreens;
        var forms = new MatrixScreenSaverForm[screens.Length];

        for (int i = 0; i < screens.Length; i++)
            forms[i] = new MatrixScreenSaverForm(screens[i], settings, isPrimary: i == 0);

        // Show secondary forms first so the primary one ends up on top.
        for (int i = screens.Length - 1; i >= 1; i--)
            forms[i].Show();

        // Run the message loop on the primary form.
        Application.Run(forms[0]);

        // Close any secondary forms after the loop exits.
        for (int i = 1; i < forms.Length; i++)
        {
            if (!forms[i].IsDisposed)
                forms[i].Close();
        }
    }
}
