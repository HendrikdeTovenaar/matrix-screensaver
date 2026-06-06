using MatrixScreensaver.Core;

namespace MatrixScreensaver.Forms;

/// <summary>
/// Simple WinForms dialog that lets the user adjust screensaver settings.
/// Invoked when Windows passes the <c>/c</c> command-line argument.
/// </summary>
public sealed class SettingsDialog : Form
{
    // ── Fields ────────────────────────────────────────────────────────────────

    private readonly ScreenSaverSettings _settings;

    // Controls
    private readonly TrackBar   _fontSizeBar      = new();
    private readonly TrackBar   _minSpeedBar      = new();
    private readonly TrackBar   _maxSpeedBar      = new();
    private readonly TrackBar   _trailBar         = new();
    private readonly TrackBar   _fpsBar           = new();
    private readonly TrackBar   _flickerBar       = new();
    private readonly TrackBar   _dropDensityBar   = new();
    private readonly TrackBar   _charWidthBar     = new();
    private readonly CheckBox   _glowCheck        = new();
    private readonly Label      _fontSizeLabel    = new();
    private readonly Label      _minSpeedLabel    = new();
    private readonly Label      _maxSpeedLabel    = new();
    private readonly Label      _trailLabel       = new();
    private readonly Label      _fpsLabel         = new();
    private readonly Label      _flickerLabel     = new();
    private readonly Label      _dropDensityLabel = new();
    private readonly Label      _charWidthLabel   = new();
    private readonly Label      _trailColorLabel  = new();
    private readonly Button     _trailColorButton = new();
    private readonly Button     _okButton         = new();
    private readonly Button     _cancelButton     = new();

    // Value display labels
    private readonly Label      _fontSizeValueLabel    = new();
    private readonly Label      _minSpeedValueLabel    = new();
    private readonly Label      _maxSpeedValueLabel    = new();
    private readonly Label      _trailValueLabel       = new();
    private readonly Label      _fpsValueLabel         = new();
    private readonly Label      _flickerValueLabel     = new();
    private readonly Label      _dropDensityValueLabel = new();
    private readonly Label      _charWidthValueLabel   = new();

    private Color               _trailColor       = Color.FromArgb(255, 0, 255, 65);
    private readonly ToolTip    _toolTip          = new();

    // ── Constructor ───────────────────────────────────────────────────────────

    public SettingsDialog(ScreenSaverSettings settings)
    {
        _settings = settings;
        BuildUi();
        LoadValues();
    }

    // ── UI construction ───────────────────────────────────────────────────────

    private void BuildUi()
    {
        Text            = "Matrix Screensaver – Settings";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition   = FormStartPosition.CenterScreen;
        MaximizeBox     = false;
        MinimizeBox     = false;
        ClientSize      = new Size(540, 680);
        BackColor       = Color.FromArgb(245, 245, 245);
        Font            = new Font("Segoe UI", 9F);

        int y = 16;
        const int margin = 20;
        const int groupPaddingTop = 24;
        const int groupPaddingBottom = 12;

        // ═══════════════════════════════════════════════════════════════════════
        // Display Settings Group
        // ═══════════════════════════════════════════════════════════════════════
        var displayGroup = CreateGroupBox("Display", margin, y, ClientSize.Width - 2 * margin, 80);
        int localY = groupPaddingTop;

        AddLabeledTrackBarWithValue(displayGroup, "Font Size:", _fontSizeLabel, _fontSizeBar, _fontSizeValueLabel,
            ref localY, 120, 8, 48, 1, "pt", "Adjust the size of the matrix characters");

        displayGroup.Height = localY + groupPaddingBottom;
        Controls.Add(displayGroup);
        y += displayGroup.Height + 10;

        // ═══════════════════════════════════════════════════════════════════════
        // Animation Settings Group
        // ═══════════════════════════════════════════════════════════════════════
        var animationGroup = CreateGroupBox("Animation", margin, y, ClientSize.Width - 2 * margin, 200);
        localY = groupPaddingTop;

        AddLabeledTrackBarWithValue(animationGroup, "Minimum Speed:", _minSpeedLabel, _minSpeedBar, _minSpeedValueLabel,
            ref localY, 120, 1, 30, 1, "cells/s", "Slowest speed for falling characters");

        AddLabeledTrackBarWithValue(animationGroup, "Maximum Speed:", _maxSpeedLabel, _maxSpeedBar, _maxSpeedValueLabel,
            ref localY, 120, 1, 60, 1, "cells/s", "Fastest speed for falling characters");

        AddLabeledTrackBarWithValue(animationGroup, "Trail Length:", _trailLabel, _trailBar, _trailValueLabel,
            ref localY, 120, 5, 80, 1, "%", "Length of the character trail relative to screen height");

        AddLabeledTrackBarWithValue(animationGroup, "Drop Density:", _dropDensityLabel, _dropDensityBar, _dropDensityValueLabel,
            ref localY, 120, 100, 800, 100, "%", "Number of falling character streams");

        animationGroup.Height = localY + groupPaddingBottom;
        Controls.Add(animationGroup);
        y += animationGroup.Height + 10;

        // ═══════════════════════════════════════════════════════════════════════
        // Visual Effects Group
        // ═══════════════════════════════════════════════════════════════════════
        var visualGroup = CreateGroupBox("Visual Effects", margin, y, ClientSize.Width - 2 * margin, 230);
        localY = groupPaddingTop;

        AddLabeledTrackBarWithValue(visualGroup, "Flicker Amount:", _flickerLabel, _flickerBar, _flickerValueLabel,
            ref localY, 120, 0, 50, 1, "%", "Random brightness variation of characters");

        AddLabeledTrackBarWithValue(visualGroup, "Character Boldness:", _charWidthLabel, _charWidthBar, _charWidthValueLabel,
            ref localY, 120, 1, 5, 1, "", "Thickness/weight of the characters");

        // Add extra spacing before color picker
        localY += 6;

        // Trail color picker
        _trailColorLabel.Text     = "Trail Color:";
        _trailColorLabel.AutoSize = false;
        _trailColorLabel.Size     = new Size(120, 20);
        _trailColorLabel.Location = new Point(12, localY);
        _trailColorLabel.Font     = new Font("Segoe UI", 9F);
        visualGroup.Controls.Add(_trailColorLabel);

        _trailColorButton.Size      = new Size(80, 28);
        _trailColorButton.Location  = new Point(135, localY - 2);
        _trailColorButton.FlatStyle = FlatStyle.Flat;
        _trailColorButton.FlatAppearance.BorderColor = Color.FromArgb(180, 180, 180);
        _trailColorButton.Click    += OnTrailColorClicked;
        _trailColorButton.Cursor    = Cursors.Hand;
        _toolTip.SetToolTip(_trailColorButton, "Click to choose the color of falling characters");
        UpdateColorButtonSwatch();
        visualGroup.Controls.Add(_trailColorButton);
        localY += 38;

        // Glow checkbox
        _glowCheck.Text     = "Enable glow on leading character";
        _glowCheck.AutoSize = true;
        _glowCheck.Location = new Point(12, localY);
        _glowCheck.Font     = new Font("Segoe UI", 9F);
        _glowCheck.FlatStyle = FlatStyle.Flat;
        _glowCheck.Cursor   = Cursors.Hand;
        _toolTip.SetToolTip(_glowCheck, "Add a bright glow effect to the first character in each stream");
        visualGroup.Controls.Add(_glowCheck);
        localY += 28;

        visualGroup.Height = localY + groupPaddingBottom;
        Controls.Add(visualGroup);
        y += visualGroup.Height + 10;

        // ═══════════════════════════════════════════════════════════════════════
        // Performance Settings Group
        // ═══════════════════════════════════════════════════════════════════════
        var perfGroup = CreateGroupBox("Performance", margin, y, ClientSize.Width - 2 * margin, 80);
        localY = groupPaddingTop;

        AddLabeledTrackBarWithValue(perfGroup, "FPS Limit:", _fpsLabel, _fpsBar, _fpsValueLabel,
            ref localY, 120, 10, 144, 1, "fps", "Maximum frames per second (higher uses more CPU)");

        perfGroup.Height = localY + groupPaddingBottom;
        Controls.Add(perfGroup);
        y += perfGroup.Height + 16;

        // ═══════════════════════════════════════════════════════════════════════
        // Action Buttons
        // ═══════════════════════════════════════════════════════════════════════
        _okButton.Text          = "OK";
        _okButton.DialogResult  = DialogResult.OK;
        _okButton.Size          = new Size(100, 34);
        _okButton.Location      = new Point(ClientSize.Width - 220, y);
        _okButton.FlatStyle     = FlatStyle.Flat;
        _okButton.BackColor     = Color.FromArgb(0, 120, 215);
        _okButton.ForeColor     = Color.White;
        _okButton.FlatAppearance.BorderSize = 0;
        _okButton.Font          = new Font("Segoe UI", 9.5F, FontStyle.Bold);
        _okButton.Cursor        = Cursors.Hand;
        _okButton.Click        += OnOkClicked;
        Controls.Add(_okButton);

        _cancelButton.Text         = "Cancel";
        _cancelButton.DialogResult = DialogResult.Cancel;
        _cancelButton.Size         = new Size(100, 34);
        _cancelButton.Location     = new Point(ClientSize.Width - 110, y);
        _cancelButton.FlatStyle    = FlatStyle.Flat;
        _cancelButton.BackColor    = Color.FromArgb(225, 225, 225);
        _cancelButton.ForeColor    = Color.FromArgb(50, 50, 50);
        _cancelButton.FlatAppearance.BorderColor = Color.FromArgb(180, 180, 180);
        _cancelButton.Font         = new Font("Segoe UI", 9.5F);
        _cancelButton.Cursor       = Cursors.Hand;
        Controls.Add(_cancelButton);

        AcceptButton = _okButton;
        CancelButton = _cancelButton;
    }

    private GroupBox CreateGroupBox(string title, int x, int y, int width, int height)
    {
        var groupBox = new GroupBox
        {
            Text      = title,
            Location  = new Point(x, y),
            Size      = new Size(width, height),
            Font      = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(60, 60, 60),
            FlatStyle = FlatStyle.Flat,
        };
        return groupBox;
    }

    private void AddLabeledTrackBarWithValue(Control parent, string labelText, Label label, 
        TrackBar bar, Label valueLabel, ref int y, int labelW, int min, int max, 
        int tickFreq, string unit, string tooltip)
    {
        const int leftMargin = 12;
        const int barX = 140;
        const int barW = 260;
        const int valueX = 410;
        const int valueW = 80;

        label.Text      = labelText;
        label.AutoSize  = false;
        label.Size      = new Size(labelW, 20);
        label.Location  = new Point(leftMargin, y + 4);
        label.Font      = new Font("Segoe UI", 9F);
        label.ForeColor = Color.FromArgb(80, 80, 80);
        parent.Controls.Add(label);

        bar.Minimum       = min;
        bar.Maximum       = max;
        bar.TickFrequency = tickFreq;
        bar.Size          = new Size(barW, 30);
        bar.Location      = new Point(barX, y);
        bar.Cursor        = Cursors.Hand;
        bar.ValueChanged += (s, e) => UpdateValueLabel(bar, valueLabel, unit);
        _toolTip.SetToolTip(bar, tooltip);
        parent.Controls.Add(bar);

        valueLabel.AutoSize  = false;
        valueLabel.Size      = new Size(valueW, 20);
        valueLabel.Location  = new Point(valueX, y + 4);
        valueLabel.Font      = new Font("Segoe UI", 9F, FontStyle.Bold);
        valueLabel.ForeColor = Color.FromArgb(0, 100, 180);
        valueLabel.TextAlign = ContentAlignment.MiddleLeft;
        parent.Controls.Add(valueLabel);

        y += 42;
    }

    private void UpdateValueLabel(TrackBar bar, Label valueLabel, string unit)
    {
        valueLabel.Text = $"{bar.Value}{unit}";
    }

    private void AddLabeledTrackBar(string labelText, Label label, TrackBar bar,
        ref int y, int labelW, int barX, int barW, int min, int max, int tickFreq)
    {
        label.Text      = labelText;
        label.AutoSize  = false;
        label.Size      = new Size(labelW, 24);
        label.Location  = new Point(16, y + 8);
        Controls.Add(label);

        bar.Minimum      = min;
        bar.Maximum      = max;
        bar.TickFrequency = tickFreq;
        bar.Size         = new Size(barW, 30);
        bar.Location     = new Point(barX, y);
        Controls.Add(bar);

        y += 44;
    }

    // ── Value load / save ─────────────────────────────────────────────────────

    private void LoadValues()
    {
        _fontSizeBar.Value = Math.Clamp((int)_settings.FontSize,     _fontSizeBar.Minimum, _fontSizeBar.Maximum);
        _minSpeedBar.Value = Math.Clamp((int)_settings.MinSpeed,     _minSpeedBar.Minimum, _minSpeedBar.Maximum);
        _maxSpeedBar.Value = Math.Clamp((int)_settings.MaxSpeed,     _maxSpeedBar.Minimum, _maxSpeedBar.Maximum);
        _trailBar.Value    = Math.Clamp((int)(_settings.MaxTrailFraction * 100), _trailBar.Minimum, _trailBar.Maximum);
        _fpsBar.Value      = Math.Clamp(_settings.FpsLimit,          _fpsBar.Minimum,      _fpsBar.Maximum);
        _flickerBar.Value  = Math.Clamp((int)(_settings.FlickerAmount * 100),   _flickerBar.Minimum, _flickerBar.Maximum);
        _dropDensityBar.Value = Math.Clamp((int)(_settings.DropDensity * 100), _dropDensityBar.Minimum, _dropDensityBar.Maximum);
        _charWidthBar.Value = Math.Clamp(_settings.CharacterBoldness, _charWidthBar.Minimum, _charWidthBar.Maximum);
        _glowCheck.Checked = _settings.GlowEnabled;

        // Update all value labels
        UpdateValueLabel(_fontSizeBar, _fontSizeValueLabel, "pt");
        UpdateValueLabel(_minSpeedBar, _minSpeedValueLabel, "cells/s");
        UpdateValueLabel(_maxSpeedBar, _maxSpeedValueLabel, "cells/s");
        UpdateValueLabel(_trailBar, _trailValueLabel, "%");
        UpdateValueLabel(_fpsBar, _fpsValueLabel, "fps");
        UpdateValueLabel(_flickerBar, _flickerValueLabel, "%");
        UpdateValueLabel(_dropDensityBar, _dropDensityValueLabel, "%");
        UpdateValueLabel(_charWidthBar, _charWidthValueLabel, "");

        // Load saved trail color
        try
        {
            string h = (_settings.TrailColorHex ?? "#FF00FF41").TrimStart('#');
            if (h.Length == 8)
                _trailColor = Color.FromArgb(
                    Convert.ToInt32(h[0..2], 16),
                    Convert.ToInt32(h[2..4], 16),
                    Convert.ToInt32(h[4..6], 16),
                    Convert.ToInt32(h[6..8], 16));
            else if (h.Length == 6)
                _trailColor = Color.FromArgb(
                    Convert.ToInt32(h[0..2], 16),
                    Convert.ToInt32(h[2..4], 16),
                    Convert.ToInt32(h[4..6], 16));
        }
        catch { /* keep default */ }
        UpdateColorButtonSwatch();
    }

    private void ApplyValues()
    {
        _settings.FontSize          = _fontSizeBar.Value;
        _settings.MinSpeed          = _minSpeedBar.Value;
        _settings.MaxSpeed          = Math.Max(_maxSpeedBar.Value, _minSpeedBar.Value);
        _settings.MaxTrailFraction  = _trailBar.Value / 100f;
        _settings.FpsLimit          = _fpsBar.Value;
        _settings.FlickerAmount     = _flickerBar.Value / 100f;
        _settings.DropDensity       = _dropDensityBar.Value / 100f;
        _settings.CharacterBoldness = _charWidthBar.Value;
        _settings.GlowEnabled       = _glowCheck.Checked;
        _settings.TrailColorHex     = $"#{_trailColor.A:X2}{_trailColor.R:X2}{_trailColor.G:X2}{_trailColor.B:X2}";
    }

    // ── Event handlers ────────────────────────────────────────────────────────

    private void OnTrailColorClicked(object? sender, EventArgs e)
    {
        using var dlg = new ColorDialog
        {
            Color    = _trailColor,
            FullOpen = true,
            AnyColor = true,
        };
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            _trailColor = dlg.Color;
            UpdateColorButtonSwatch();
        }
    }

    private void UpdateColorButtonSwatch()
    {
        _trailColorButton.BackColor = _trailColor;
        float lum = 0.299f * _trailColor.R + 0.587f * _trailColor.G + 0.114f * _trailColor.B;
        _trailColorButton.ForeColor = lum > 128 ? Color.Black : Color.White;
        _trailColorButton.Text      = string.Empty;
    }

    private void OnOkClicked(object? sender, EventArgs e)
    {
        ApplyValues();
        _settings.Save();
    }
}
