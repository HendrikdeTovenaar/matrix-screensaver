namespace MatrixScreensaver.Rendering;

/// <summary>
/// Represents a single character cell within a <see cref="RainColumn"/>.
/// </summary>
/// <remarks>
/// Kept as a struct to allow cheap, allocation-free storage in arrays.
/// The renderer reads these values every frame, so they are kept simple.
/// </remarks>
public struct MatrixGlyph
{
    // ── State ───────────────────────────────────────────────────────────────

    /// <summary>The Unicode character displayed in this cell.</summary>
    public char Character;

    /// <summary>
    /// Normalised alpha value in [0, 1] used when rendering this glyph.
    /// 0 = fully transparent (tail end), 1 = fully opaque (near head).
    /// </summary>
    public float Alpha;

    /// <summary>
    /// Additional per-glyph brightness multiplier driven by the flicker system.
    /// Range [0, 1]; multiplied with the base colour before painting.
    /// </summary>
    public float Brightness;

    /// <summary>
    /// True when this glyph is the leading (head) character of the column.
    /// The renderer paints the head in bright gold/white with an optional glow.
    /// </summary>
    public bool IsHead;

    // ── Factory ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a new glyph with full opacity and brightness.
    /// </summary>
    public static MatrixGlyph Create(char character, float alpha, bool isHead = false) =>
        new()
        {
            Character  = character,
            Alpha      = alpha,
            Brightness = 1f,
            IsHead     = isHead,
        };
}
