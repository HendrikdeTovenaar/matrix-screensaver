using System.Diagnostics;

namespace MatrixScreensaver.Core;

/// <summary>
/// High-resolution frame timer that provides an accurate delta-time value
/// and enforces an optional FPS cap.
/// </summary>
/// <remarks>
/// Uses <see cref="Stopwatch"/> for sub-millisecond accuracy, avoiding the
/// drift that accumulates with <c>Environment.TickCount</c> or <see cref="DateTime"/>.
/// </remarks>
public sealed class FrameTimer
{
    // ── State ────────────────────────────────────────────────────────────────

    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private long _lastTicks;

    // ── FPS tracking ─────────────────────────────────────────────────────────

    private int   _frameCount;
    private long  _fpsAccumTicks;
    private float _currentFps;

    // ── Properties ────────────────────────────────────────────────────────────

    /// <summary>
    /// Seconds elapsed since the last call to <see cref="Tick"/>.
    /// Clamped to a maximum of 0.1 s to prevent simulation explosions after
    /// the app is paused or the machine goes to sleep.
    /// </summary>
    public float DeltaSeconds { get; private set; }

    /// <summary>Smoothed frames-per-second measured over the last ~60 frames.</summary>
    public float CurrentFps => _currentFps;

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Must be called once at the very start of each frame (before updating or rendering).
    /// Updates <see cref="DeltaSeconds"/> and internal FPS statistics.
    /// </summary>
    public void Tick()
    {
        long now   = _stopwatch.ElapsedTicks;
        long delta = now - _lastTicks;
        _lastTicks = now;

        DeltaSeconds = Math.Min((float)delta / Stopwatch.Frequency, 0.1f);

        // Accumulate for FPS counter.
        _fpsAccumTicks += delta;
        _frameCount++;

        if (_frameCount >= 60)
        {
            float elapsed = (float)_fpsAccumTicks / Stopwatch.Frequency;
            _currentFps   = elapsed > 0f ? _frameCount / elapsed : 0f;
            _frameCount   = 0;
            _fpsAccumTicks = 0;
        }
    }

    /// <summary>
    /// Resets the timer so that the next call to <see cref="Tick"/> reports
    /// a delta of zero. Call this after a long pause (e.g., form resize).
    /// </summary>
    public void Reset()
    {
        _lastTicks = _stopwatch.ElapsedTicks;
        DeltaSeconds = 0f;
    }
}
