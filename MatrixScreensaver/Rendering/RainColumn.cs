using MatrixScreensaver.Core;

namespace MatrixScreensaver.Rendering;

/// <summary>
/// Models a single vertical stream of Matrix rain characters.
/// </summary>
/// <remarks>
/// Design: characters occupy <b>fixed grid rows</b> — they do not scroll.
/// A fractional "head row" cursor advances downward at a set speed (rows/second).
/// Each time the cursor crosses a new integer row it stamps a fresh character into
/// that row.  The renderer draws the trail from the current head row upward.
/// <para>
/// Trail slot 0 = head (bottom of the active trail, drawn in bright gold/white).
/// Trail slot Length-1 = topmost/faintest character.
/// </para>
/// </remarks>
public sealed class RainColumn
{
    // ── Dependencies ─────────────────────────────────────────────────────────

    private readonly Random              _rng;
    private readonly ScreenSaverSettings _settings;

    // ── Grid geometry ─────────────────────────────────────────────────────────

    /// <summary>Horizontal pixel centre of this column.</summary>
    public float X { get; set; }

    /// <summary>Height of one glyph cell in pixels.</summary>
    public float CellHeight { get; set; }

    /// <summary>Total number of rows that fit on screen.</summary>
    public int TotalRows { get; set; }

    // ── Stream state ──────────────────────────────────────────────────────────

    /// <summary>Number of lit characters in this stream (5–30).</summary>
    public int TrailLength { get; private set; }

    /// <summary>Fractional head row; the integer part is the current head row index.</summary>
    private float _headRowF;

    /// <summary>Last integer row the head occupied (used to detect row advances).</summary>
    private int _prevHeadRow;

    /// <summary>Head row at the start of the current frame (set before advancing).</summary>
    public int PreviousHeadRow { get; private set; }

    /// <summary>Integer head row at which this drop spawned. The kill check is suppressed until the drop advances past this row.</summary>
    public int SpawnRow { get; private set; }

    /// <summary>Current integer head row.</summary>
    public int HeadRow => (int)_headRowF;

    /// <summary>Fall speed in rows per second.</summary>
    public float Speed { get; private set; }

    // ── Character ring buffer ─────────────────────────────────────────────────
    // Indexed modulo MaxTrail.  _ringHead is the index for slot 0 (the head).
    // Slot i maps to _charRing[(_ringHead + i) % MaxTrail].

    private const int MaxTrail = 30;
    private readonly char[]  _charRing   = new char[MaxTrail];
    private readonly float[] _brightness = new float[MaxTrail];
    private int _ringHead;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    /// <summary>True while a sequence is actively falling.</summary>
    private bool  _active;

    /// <summary>
    /// Rows at or below this value are hidden because a more-advanced drop on the
    /// same X column has overtaken them.  Set by the renderer each frame.
    /// -1 means no occlusion.
    /// </summary>
    public int OccludedBelowRow { get; set; } = -1;

    /// <summary>Seconds remaining before the next sequence begins.</summary>
    private float _cooldown;

    // ── Mutation timer ────────────────────────────────────────────────────────

    private float _mutateAccum;

    // ── Public glyph output (written each Update, read by renderer) ───────────

    /// <summary>
    /// Pre-built glyph data for the current frame.
    /// Valid indices: 0 … <see cref="Length"/>-1.
    /// Index 0 is the head (bright gold); higher indices are the fading trail above.
    /// </summary>
    public MatrixGlyph[] Glyphs { get; } = new MatrixGlyph[MaxTrail];

    /// <summary>
    /// Number of glyphs to render this frame.
    /// Zero while the column is on cooldown between sequences.
    /// </summary>
    public int Length => _active ? TrailLength : 0;

    // ── Character pool ────────────────────────────────────────────────────────

    private static readonly char[] CharacterPool = BuildPool();

    private static char[] BuildPool()
    {
        const string latin    = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        const string digits   = "0123456789";
        const string katakana = "アイウエオカキクケコサシスセソタチツテトナニヌネノハヒフヘホマミムメモヤユヨラリルレロワヲン";
        return (latin + digits + katakana).ToCharArray();
    }

    // ── Constructor ───────────────────────────────────────────────────────────

    /// <param name="x">Horizontal pixel centre of this column.</param>
    /// <param name="cellHeight">Height of one character cell in pixels.</param>
    /// <param name="totalRows">Number of character rows that fit the screen height.</param>
    /// <param name="settings">Shared settings (not owned).</param>
    /// <param name="rng">Shared random instance.</param>
    public RainColumn(float x, float cellHeight, int totalRows,
                      ScreenSaverSettings settings, Random rng)
    {
        X          = x;
        CellHeight = cellHeight;
        TotalRows  = totalRows;
        _settings  = settings;
        _rng       = rng;

        Initialise(stagger: true);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the absolute screen row for trail slot <paramref name="slot"/>.
    /// Slot 0 = head row (lowest visible), slot 1 = one row above, etc.
    /// </summary>
    public int GetRow(int slot) => (int)_headRowF - slot;

    /// <summary>
    /// Immediately ends the active sequence and starts a short cooldown.
    /// Called when another drop at the same X position has overtaken this one.
    /// </summary>
    public void ForceKill()
    {
        _active   = false;
        float density = Math.Clamp(_settings.DropDensity, 1.0f, 8.0f);
        _cooldown = (float)_rng.NextDouble() * (3.0f / density);
    }

    /// <summary>Advances the simulation
    public void Update(float deltaSeconds)
    {
        if (!_active)
        {
            // Wait out the inter-sequence cooldown.
            _cooldown -= deltaSeconds;
            if (_cooldown <= 0f)
                Initialise(stagger: false);
            return;
        }

        // ── Advance head ──────────────────────────────────────────────────────

        PreviousHeadRow = (int)_headRowF;
        _headRowF += Speed * deltaSeconds;
        int headRow = (int)_headRowF;

        // Stamp a new character for every integer row the head has crossed.
        while (_prevHeadRow < headRow)
        {
            _prevHeadRow++;
            AdvanceRing();
        }

        // ── Character mutations ───────────────────────────────────────────────

        _mutateAccum += deltaSeconds;
        bool doMutate = _mutateAccum >= 1f / 20f; // ~20 mutation ticks per second
        if (doMutate)
            _mutateAccum = 0f;

        // ── Update per-slot brightness and optional mutations ─────────────────

        for (int i = 0; i < TrailLength; i++)
        {
            // Flicker: slight random brightness variation each frame.
            float flicker    = 1f - _settings.FlickerAmount * (float)_rng.NextDouble();
            _brightness[i]   = Math.Clamp(flicker, 0f, 1f);

            // Mutate non-head characters at a low random rate.
            if (doMutate && i > 0 && (float)_rng.NextDouble() < _settings.CharChangeRate)
                SetRingChar(i, RandomChar());
        }

        // ── Build public Glyphs array ─────────────────────────────────────────

        for (int i = 0; i < TrailLength; i++)
        {
            Glyphs[i] = new MatrixGlyph
            {
                Character  = GetRingChar(i),
                Alpha      = ComputeAlpha(i),
                Brightness = _brightness[i],
                IsHead     = i == 0,
            };
        }

        // ── Recycle check ─────────────────────────────────────────────────────

        // The sequence is finished when the topmost trail cell has left the screen.
        int tailRow = headRow - TrailLength + 1;
        if (tailRow >= TotalRows)
        {
            _active   = false;
            // Higher density → shorter cooldown so extra drops respawn more aggressively.
            float density = Math.Clamp(_settings.DropDensity, 1.0f, 8.0f);
            _cooldown = (float)_rng.NextDouble() * (3.0f / density);
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Resets this column ready for a new falling sequence.
    /// </summary>
    /// <param name="stagger">
    /// When <c>true</c> the head starts at a random negative row so that
    /// columns on different screens enter the viewport at different times.
    /// When <c>false</c> the head starts just one row above the top.
    /// </param>
    private void Initialise(bool stagger)
    {
        // Random trail length between 5 and 30 characters.
        TrailLength = _rng.Next(5, MaxTrail + 1);

        // Random speed in rows per second.
        Speed = _settings.MinSpeed
              + (float)_rng.NextDouble() * (_settings.MaxSpeed - _settings.MinSpeed);

        // Starting position.
        _headRowF       = stagger ? -(float)_rng.NextDouble() * TotalRows : -1f;
        _prevHeadRow    = (int)_headRowF - 1;
        PreviousHeadRow = (int)_headRowF;
        SpawnRow        = (int)_headRowF;

        // Fill the ring buffer with random characters.
        for (int i = 0; i < MaxTrail; i++)
        {
            _charRing[i]   = RandomChar();
            _brightness[i] = 1f;
        }

        _ringHead    = 0;
        _mutateAccum = 0f;
        _active      = true;
    }

    /// <summary>
    /// Advances the ring so that slot 0 (the head) receives a brand-new character.
    /// All existing characters shift up one slot automatically via the ring offset.
    /// </summary>
    private void AdvanceRing()
    {
        // Move ring head one step backward (head slot = lowest index in the ring).
        _ringHead = (_ringHead - 1 + MaxTrail) % MaxTrail;
        _charRing[_ringHead] = RandomChar();
    }

    private char GetRingChar(int slot) =>
        _charRing[(_ringHead + slot) % MaxTrail];

    private void SetRingChar(int slot, char c) =>
        _charRing[(_ringHead + slot) % MaxTrail] = c;

    /// <summary>
    /// Returns the alpha for trail slot <paramref name="slot"/>.
    /// Slot 0 (head) = 1.0; fades quadratically to near-zero at the tail.
    /// </summary>
    private float ComputeAlpha(int slot)
    {
        if (slot == 0) return 1f;
        float t = (float)slot / (TrailLength - 1);
        return Math.Max(0.02f, (1f - t) * (1f - t));
    }

    private char RandomChar() => CharacterPool[_rng.Next(CharacterPool.Length)];
}
