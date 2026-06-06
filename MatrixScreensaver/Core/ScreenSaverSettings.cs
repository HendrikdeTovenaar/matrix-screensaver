using System.Text.Json;

namespace MatrixScreensaver.Core;

/// <summary>
/// Holds all user-configurable settings for the Matrix screensaver.
/// Settings are persisted to JSON in the user's AppData folder.
/// </summary>
public sealed class ScreenSaverSettings
{
    // ── Serialization ───────────────────────────────────────────────────────

    /// <summary>Path to the settings file on disk.</summary>
    public static string SettingsFilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MatrixScreensaver",
        "settings.json");

    private static readonly JsonSerializerOptions s_writeOptions =
        new() { WriteIndented = true };

    // ── Display ─────────────────────────────────────────────────────────────

    /// <summary>Font size in points used for matrix glyphs.</summary>
    public float FontSize { get; set; } = 18f;

    // ── Animation ───────────────────────────────────────────────────────────

    /// <summary>Minimum fall speed in cells per second.</summary>
    public float MinSpeed { get; set; } = 4f;

    /// <summary>Maximum fall speed in cells per second.</summary>
    public float MaxSpeed { get; set; } = 17f;

    /// <summary>
    /// Fraction (0–1) of the screen height used as the maximum trail length.
    /// A value of 0.25 means trails can be up to 25 % of the screen height.
    /// </summary>
    public float MaxTrailFraction { get; set; } = 0.49f;

    /// <summary>Probability per frame per glyph that its character changes.</summary>
    public float CharChangeRate { get; set; } = 0.04f;

    /// <summary>Target frames per second cap (0 = unlimited).</summary>
    public int FpsLimit { get; set; } = 60;

    // ── Visual ──────────────────────────────────────────────────────────────

    /// <summary>Enable a soft glow halo around the bright leading character.</summary>
    public bool GlowEnabled { get; set; } = true;

    /// <summary>Intensity of random per-glyph brightness flicker (0 = none).</summary>
    public float FlickerAmount { get; set; } = 0.15f;

    /// <summary>
    /// Fraction of available columns populated with active drops.
    /// 1.0 = one drop per column slot (default).
    /// Values range from 1.0 (minimum) to 8.0 (maximum, 8× density).
    /// </summary>
    public float DropDensity { get; set; } = 2.6f;

    /// <summary>
    /// ARGB hex string for the trail glyph color (e.g. "#FF00FF41" for matrix green).
    /// </summary>
    public string TrailColorHex { get; set; } = "#FF00FF40";

    /// <summary>
    /// Character boldness: 1 = normal (no extra stroke), 5 = heaviest stroke.
    /// Controls the stroke width overlaid on each glyph.
    /// </summary>
    public int CharacterBoldness { get; set; } = 2;

    // ── I/O ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Loads settings from disk. Returns defaults when the file is absent or corrupt.
    /// </summary>
    public static ScreenSaverSettings Load()
    {
        try
        {
            if (File.Exists(SettingsFilePath))
            {
                string json = File.ReadAllText(SettingsFilePath);
                return JsonSerializer.Deserialize<ScreenSaverSettings>(json)
                       ?? new ScreenSaverSettings();
            }
        }
        catch
        {
            // Swallow I/O or deserialization errors – fall back to defaults.
        }

        return new ScreenSaverSettings();
    }

    /// <summary>
    /// Persists the current settings to disk, creating the directory if needed.
    /// </summary>
    public void Save()
    {
        try
        {
            string? dir = Path.GetDirectoryName(SettingsFilePath);
            if (dir is not null)
                Directory.CreateDirectory(dir);

            string json = JsonSerializer.Serialize(this, s_writeOptions);
            File.WriteAllText(SettingsFilePath, json);
        }
        catch
        {
            // Silently ignore save failures (e.g., restricted environments).
        }
    }
}
