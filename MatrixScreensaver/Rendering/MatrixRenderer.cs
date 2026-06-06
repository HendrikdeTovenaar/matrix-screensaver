using MatrixScreensaver.Core;
using SkiaSharp;

namespace MatrixScreensaver.Rendering;

/// <summary>
/// Owns and drives all <see cref="RainColumn"/> instances and renders them
/// to an <see cref="SKCanvas"/> each frame using SkiaSharp.
/// </summary>
/// <remarks>
/// Rendering model:
/// Characters occupy <b>fixed grid rows</b> — they do not move.
/// The bright leading character (gold/white) appears at the growing bottom
/// of each stream; the trail fades green toward the top.
/// Zero per-frame heap allocations: paints and columns are reused.
/// </remarks>
public sealed class MatrixRenderer : IDisposable
{
    // ── Settings ──────────────────────────────────────────────────────────────

    private readonly ScreenSaverSettings _settings;

    // ── Columns ───────────────────────────────────────────────────────────────

    private RainColumn[] _columns = [];
    private readonly Random _rng = new();

    // ── Grid metrics ──────────────────────────────────────────────────────────

    private float _cellWidth;
    private float _cellHeight;
    private int   _totalRows;
    private int   _canvasWidth;
    private int   _canvasHeight;

    // ── Reusable SkiaSharp paints ─────────────────────────────────────────────

    /// <summary>Paint for green trail glyphs.</summary>
    private readonly SKPaint _trailPaint = new() { IsAntialias = true };

    /// <summary>Paint for the bright leading (head) glyph.</summary>
    private readonly SKPaint _headPaint  = new() { IsAntialias = true };

    /// <summary>Paint for the optional glow halo behind the head.</summary>
    private readonly SKPaint _glowPaint  = new() { IsAntialias = true };

    // ── Fonts (hold typeface + size; reused each frame) ───────────────────────

    private SKFont _trailFont = new();
    private SKFont _headFont  = new();
    private SKFont _glowFont  = new();

    /// <summary>Solid black background fill.</summary>
    private readonly SKPaint _bgPaint    = new() { Color = SKColors.Black, Style = SKPaintStyle.Fill };

    // ── Typeface ──────────────────────────────────────────────────────────────

    private SKTypeface? _typeface;
    private bool        _fontReady;

    // ── Constructor ───────────────────────────────────────────────────────────

    public MatrixRenderer(ScreenSaverSettings settings) => _settings = settings;

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// (Re)initialises the grid and columns for the given canvas size.
    /// Must be called before the first <see cref="UpdateAndRender"/> and again on resize.
    /// </summary>
    public void Initialise(int width, int height)
    {
        _canvasWidth  = width;
        _canvasHeight = height;

        EnsureFont();
        MeasureGrid();
        CreateColumns();
    }

    /// <summary>
    /// Advances the simulation by <paramref name="deltaSeconds"/> and renders
    /// the current frame onto <paramref name="canvas"/>.
    /// </summary>
    public void UpdateAndRender(SKCanvas canvas, float deltaSeconds)
    {
        // Update all columns first.
        foreach (var col in _columns)
            col.Update(deltaSeconds);

        // ── Occlusion pass ────────────────────────────────────────────────────
        // Coordinate system: larger head-row number = lower on screen = more advanced.
        // A "leader" is a drop that is further down the screen.
        // A "chaser" is a drop on the same column that started later from the top.
        //
        // Rules applied per leader drop:
        //  • If any chaser's head has reached the leader's head → the leader has
        //    been overtaken: kill the leader.
        //  • If any chaser's head has entered the leader's trail region (chaser is
        //    approaching from behind) → occlude the leader's trail rows at and
        //    below the chaser's head so the chaser cleanly overwrites them.
        //
        // Chasers are never touched here; they keep falling until they catch up.

        // Group column indices by shared X position.
        var groups = new Dictionary<float, List<int>>(_columns.Length);
        for (int i = 0; i < _columns.Length; i++)
        {
            float x = _columns[i].X;
            if (!groups.TryGetValue(x, out var list))
                groups[x] = list = [];
            list.Add(i);
        }

        foreach (var (_, indices) in groups)
        {
            // Reset occlusion state for every drop in this column slot.
            foreach (int idx in indices)
                _columns[idx].OccludedBelowRow = -1;

            if (indices.Count <= 1) continue;

            // Treat each active drop as a potential leader.
            foreach (int leaderIdx in indices)
            {
                var leader = _columns[leaderIdx];
                if (leader.Length == 0) continue;

                int leaderHead = leader.GetRow(0);
                int leaderTail = leaderHead - leader.TrailLength + 1;
                int occlusionRow = -1;

                // Look for chasers: drops on the same column that are BEHIND this
                // leader (smaller head row = higher on screen = not yet as far down).
                foreach (int chaserIdx in indices)
                {
                    if (chaserIdx == leaderIdx) continue;
                    var chaser = _columns[chaserIdx];
                    if (chaser.Length == 0) continue;

                    int chaserHead = chaser.GetRow(0);

                    // Only consider drops that are behind (above) this leader.
                    if (chaserHead >= leaderHead)
                        continue; // this drop is ahead of or level with us; handled when it becomes leader

                    if (chaserHead >= leaderTail)
                    {
                        // Chaser's head is inside the leader's trail.
                        // Hide the leader's trail at and below the chaser's head.
                        occlusionRow = Math.Max(occlusionRow, chaserHead);
                    }
                }

                if (leader.Length > 0)
                    leader.OccludedBelowRow = occlusionRow;
            }

            // Kill pass: a crossing event occurs when a chaser was behind the leader
            // last frame but is now at or past the leader's head this frame.
            // Only the leader is killed (it has been overtaken).
            foreach (int chaserIdx in indices)
            {
                var chaser = _columns[chaserIdx];
                if (chaser.Length == 0) continue;
                int chaserNow  = chaser.HeadRow;
                int chaserPrev = chaser.PreviousHeadRow;

                foreach (int leaderIdx in indices)
                {
                    if (leaderIdx == chaserIdx) continue;
                    var leader = _columns[leaderIdx];
                    if (leader.Length == 0) continue;
                    int leaderHead = leader.HeadRow;

                    // Only check for overtaking once the chaser has advanced past
                    // the row it spawned at — avoids false kills on the first frame.
                    if (chaserNow <= chaser.SpawnRow) continue;

                    // Crossing: chaser was behind last frame, is now at or past leader.
                    if (chaserPrev < leaderHead && chaserNow >= leaderHead)
                    {
                        leader.ForceKill();
                    }
                }
            }
        }

        // Clear to solid black every frame (no blur fade — characters are static).
        canvas.DrawRect(0, 0, _canvasWidth, _canvasHeight, _bgPaint);

        // Draw each active column — no occlusion math needed here.
        foreach (var col in _columns)
            if (col.Length > 0)
                DrawColumn(canvas, col);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private void EnsureFont()
    {
        if (_fontReady) return;
        _fontReady = true;

        // Prefer fonts with full Katakana coverage.
        string[] candidates = ["MS Gothic", "Yu Gothic", "Noto Sans JP", "Courier New", "Consolas"];
        foreach (string name in candidates)
        {
            var tf = SKTypeface.FromFamilyName(name, SKFontStyle.Normal);
            if (tf is not null && tf.FamilyName != SKTypeface.Default.FamilyName)
            {
                _typeface = tf;
                break;
            }
        }
        _typeface ??= SKTypeface.Default;

        _trailFont.Typeface = _typeface;
        _headFont.Typeface  = _typeface;
        _glowFont.Typeface  = _typeface;
    }

    private void MeasureGrid()
    {
        using var probe = new SKFont(_typeface, _settings.FontSize);

        // Use a full-width Katakana glyph as the column-width reference.
        float charW  = probe.MeasureText("ア");
        _cellWidth   = charW + 2f;
        _cellHeight  = _settings.FontSize * 1.35f;
        _totalRows   = Math.Max(1, (int)Math.Ceiling(_canvasHeight / _cellHeight));

        _trailFont.Size = _settings.FontSize;
        _headFont.Size  = _settings.FontSize;
        _glowFont.Size  = _settings.FontSize + 4f;
    }

    private void CreateColumns()
    {
        int totalColumns = Math.Max(1, (int)(_canvasWidth / _cellWidth));
        float density    = Math.Clamp(_settings.DropDensity, 1.0f, 8.0f);
        int activeCount  = Math.Max(1, (int)Math.Round(totalColumns * density));

        _columns = new RainColumn[activeCount];

        for (int i = 0; i < activeCount; i++)
        {
            // Wrap column index so extra drops reuse existing X positions.
            int colIndex = i % totalColumns;
            float cx = colIndex * _cellWidth + _cellWidth * 0.5f;
            _columns[i] = new RainColumn(cx, _cellHeight, _totalRows, _settings, _rng);
        }
    }

    /// <summary>
    /// Draws all visible glyphs for <paramref name="col"/> at their fixed grid rows.
    /// Slot 0 = head (lowest row, bright gold).
    /// Slot i > 0 = trail (rows above, fading green).
    /// </summary>
    /// <summary>
    /// Draws all visible glyphs for <paramref name="col"/> at their fixed grid rows.
    /// Slot 0 = head (lowest row, bright gold).
    /// Slot i > 0 = trail (rows above, fading green).
    /// </summary>
    private void DrawColumn(SKCanvas canvas, RainColumn col)
    {
        // Approximate ascent so text sits centred in the cell.
        float ascent = _settings.FontSize * 0.82f;

        for (int slot = 0; slot < col.Length; slot++)
        {
            int row = col.GetRow(slot);

            // Skip rows outside the visible canvas.
            if (row < 0 || row >= _totalRows) continue;

            // Skip rows occluded by a more-advanced drop on the same X column.
            if (col.OccludedBelowRow >= 0 && row <= col.OccludedBelowRow) continue;

            ref MatrixGlyph g = ref col.Glyphs[slot];

            float baselineY = row * col.CellHeight + ascent;
            string ch       = g.Character.ToString();

            if (g.IsHead)
            {
                // ── Glow halo (drawn first, behind the character) ──────────────
                if (_settings.GlowEnabled)
                {
                    _glowPaint.Color      = new SKColor(255, 220, 80, 55);
                    _glowPaint.MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 9f);
                    canvas.DrawText(ch, col.X, baselineY, SKTextAlign.Center, _glowFont, _glowPaint);
                    _glowPaint.MaskFilter = null;
                }

                // ── Head character: bright gold / near-white ───────────────────
                byte ha = (byte)(255f * g.Brightness);
                float headStrokeW = (_settings.CharacterBoldness - 1) * 0.4f;
                _headPaint.Style       = headStrokeW > 0f ? SKPaintStyle.StrokeAndFill : SKPaintStyle.Fill;
                _headPaint.StrokeWidth = headStrokeW;
                _headPaint.Color = new SKColor(240, 255, 180, ha);
                canvas.DrawText(ch, col.X, baselineY, SKTextAlign.Center, _headFont, _headPaint);
            }
            else
            {
                // ── Trail character: trail color, fading with alpha ─────────────
                byte a = (byte)(255f * g.Alpha * g.Brightness);

                // The first few glyphs just behind the head are brighter.
                float brightScale = slot switch
                {
                    1 => 1.00f,
                    2 => 0.90f,
                    3 => 0.84f,
                    _ => 0.70f,
                };

                SKColor baseTrail = ParseTrailColor();
                float strokeW = (_settings.CharacterBoldness - 1) * 0.4f; // 1→0, 5→1.6
                _trailPaint.Style       = strokeW > 0f ? SKPaintStyle.StrokeAndFill : SKPaintStyle.Fill;
                _trailPaint.StrokeWidth = strokeW;
                _trailPaint.Color = new SKColor(
                    (byte)(baseTrail.Red   * brightScale),
                    (byte)(baseTrail.Green * brightScale),
                    (byte)(baseTrail.Blue  * brightScale),
                    a);
                canvas.DrawText(ch, col.X, baselineY, SKTextAlign.Center, _trailFont, _trailPaint);
            }
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private SKColor? _cachedTrailColor;
    private string?  _cachedTrailHex;

    private SKColor ParseTrailColor()
    {
        string hex = _settings.TrailColorHex ?? "#FF00FF41";
        if (hex == _cachedTrailHex && _cachedTrailColor.HasValue)
            return _cachedTrailColor.Value;

        _cachedTrailHex = hex;
        // Parse #AARRGGBB or #RRGGBB
        string h = hex.TrimStart('#');
        SKColor color = h.Length == 8
            ? new SKColor(
                Convert.ToByte(h[2..4], 16),
                Convert.ToByte(h[4..6], 16),
                Convert.ToByte(h[6..8], 16),
                Convert.ToByte(h[0..2], 16))
            : h.Length == 6
                ? new SKColor(
                    Convert.ToByte(h[0..2], 16),
                    Convert.ToByte(h[2..4], 16),
                    Convert.ToByte(h[4..6], 16))
                : new SKColor(0, 255, 65); // fallback matrix green
        _cachedTrailColor = color;
        return color;
    }

    // ── IDisposable ───────────────────────────────────────────────────────────

    private bool _disposed;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _trailPaint.Dispose();
        _headPaint.Dispose();
        _glowPaint.Dispose();
        _bgPaint.Dispose();
        _trailFont.Dispose();
        _headFont.Dispose();
        _glowFont.Dispose();
        _typeface?.Dispose();
    }
}
