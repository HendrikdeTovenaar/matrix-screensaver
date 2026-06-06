# Matrix Screensaver

A high-performance Windows Matrix digital-rain screensaver built with **C# / .NET 10**, **Windows Forms**, and **SkiaSharp** for GPU-accelerated rendering.

> **Disclaimer:** This project was created entirely through AI and prompt engineering, with only minor manual edits. It was developed as a personal experiment to evaluate the capabilities of modern AI-assisted software development.

---

## Features

- Classic Matrix digital-rain effect with falling streams of Katakana, Latin letters, and digits.
- Characters occupy **fixed grid rows** — they do not scroll. A bright leading character grows the stream downward one row at a time.
- Each stream has a **randomised trail length** (5–30 characters) and **randomised speed**.
- Bright gold / near-white **leading character** with an optional soft **glow halo**.
- Fading **trail** with per-glyph brightness flicker — trail color is fully customisable.
- Adjustable **character boldness** via stroke-width overlay (5 levels).
- Configurable **drop density** — from one drop per column up to eight overlapping drops.
- Full **multi-monitor** support — one full-screen window per display.
- **Right-click** to open the settings dialog while the screensaver is running.
- All settings are persisted to JSON in `%AppData%\MatrixScreensaver\settings.json`.

---

## Project Structure

```
MatrixScreensaver/
├── Program.cs                    Entry point; CLI argument dispatcher
├── Core/
│   ├── ScreenSaverSettings.cs    Settings model and JSON persistence
│   └── FrameTimer.cs             High-resolution delta-time and FPS counter
├── Rendering/
│   ├── MatrixGlyph.cs            Single character-cell data struct
│   ├── RainColumn.cs             One falling stream (fixed-row model)
│   └── MatrixRenderer.cs         SkiaSharp rendering engine
└── Forms/
    ├── MatrixScreenSaverForm.cs  Full-screen WinForms host
    └── SettingsDialog.cs         Configuration dialog
```

---

## Command-Line Arguments

Windows screensavers communicate with the OS via standard command-line flags.

| Argument | Meaning |
|---|---|
| `/s` | **Start** the screensaver in full-screen mode. |
| `/c` or `/c:<hwnd>` | Open the **configuration** (settings) dialog. |
| `/p <hwnd>` | Show a small **preview** inside the Display Properties thumbnail. |
| *(none)* | Treated as `/s` — useful for running directly during development. |

### Examples

```powershell
# Run full-screen (normal screensaver mode)
.\MatrixScreensaver.scr /s

# Open the settings dialog
.\MatrixScreensaver.scr /c

# Run directly from Visual Studio (no args = /s)
dotnet run
```

---

## Runtime Controls

| Action | Effect |
|---|---|
| **Any key** | Exit the screensaver |
| **Escape** | Exit the screensaver |
| **Mouse move** (> 10 px) | Exit the screensaver |
| **Left / middle click** | Exit the screensaver |
| **Right-click** | Open the Settings dialog (screensaver stays open) |

---

## Settings

All settings are available through the Settings dialog (right-click while running, or `/c` argument) and are saved automatically on **OK**.

| Setting | Default | Range | Description |
|---|---|---|---|
| **Font size** | `16` | 8 – 48 pt | Point size of the glyph typeface. Larger values = fewer columns, bigger characters. |
| **Min speed** | `4` | 1 – 30 rows/s | Slowest a stream can fall (rows per second). |
| **Max speed** | `16` | 1 – 60 rows/s | Fastest a stream can fall. Each stream picks a random speed between min and max. |
| **Trail length** | `35 %` | 5 – 80 % | Maximum trail length as a percentage of visible screen height. |
| **FPS limit** | `60` | 10 – 144 | Target frames per second. Lower values reduce CPU/GPU load. |
| **Flicker amount** | `15 %` | 0 – 50 % | Per-glyph random brightness variation each frame. 0 = steady; higher = more visible flicker. |
| **Drop density** | `100 %` | 100 – 800 % | Number of active drops relative to available columns. 100 % = one drop per column; 800 % = eight overlapping drops per column. |
| **Character boldness** | `1` | 1 – 5 | Stroke width overlaid on every glyph. 1 = normal weight (no extra stroke); 5 = heaviest. |
| **Trail color** | matrix green | color picker | Color of all trail glyphs. Defaults to `#FF00FF41`. Click the swatch button to open the color picker. |
| **Enable glow** | `on` | on / off | Draw a soft blur halo behind the leading (head) character. |

### Settings file location

```
%AppData%\MatrixScreensaver\settings.json
```

Example file:

```json
{
  "FontSize": 16,
  "MinSpeed": 4,
  "MaxSpeed": 16,
  "MaxTrailFraction": 0.35,
  "CharChangeRate": 0.04,
  "FpsLimit": 60,
  "GlowEnabled": true,
  "FlickerAmount": 0.15,
  "DropDensity": 1.0,
  "TrailColorHex": "#FF00FF41",
  "CharacterBoldness": 1
}
```

> Delete the file to reset all settings to their defaults.

---

## Building and Publishing

### Build (debug)

```powershell
cd MatrixScreensaver
dotnet build
```

### Publish as a single self-contained executable

```powershell
dotnet publish -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -o ./publish
```

---

## Installing as a Windows Screensaver

### 1 — Rename the output

```powershell
Rename-Item .\publish\MatrixScreensaver.exe MatrixScreensaver.scr
```

### 2a — Right-click install (simplest)

1. Copy `MatrixScreensaver.scr` to `C:\Windows\System32\`.
2. Right-click the `.scr` file → **Install**.

### 2b — Set via Display Settings

1. Copy `MatrixScreensaver.scr` to `C:\Windows\System32\`.
2. Right-click the Desktop → **Personalize** → **Lock screen** → **Screen saver settings**.
3. Select **MatrixScreensaver** from the drop-down and click **OK**.

### 2c — Run without installing (testing)

```powershell
# Full-screen test
.\MatrixScreensaver.scr /s

# Settings dialog test
.\MatrixScreensaver.scr /c
```

---

## NuGet Dependencies

| Package | Version | Purpose |
|---|---|---|
| `SkiaSharp` | 3.119.4 | 2D rendering engine (GPU-accelerated) |
| `SkiaSharp.Views.WindowsForms` | 3.119.4 | `SKControl` WinForms host |
| `SkiaSharp.NativeAssets.Win32` | 3.119.4 | Native Win32 binaries for self-contained publish |

---

## License

MIT — free to use, modify, and distribute.
