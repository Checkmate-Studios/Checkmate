# Checkmate

Free, open-source, professional-grade 2D animation for everyone who wants to make their own anime.
Easy like FlipaClip, powerful like Toon Boom, free forever.

Built with C# / .NET 8, Avalonia 11 and SkiaSharp. Runs on Windows, macOS and Linux.

## Requirements

.NET SDK matching `TargetFramework` in `Directory.Build.props` (default `net8.0`; change it to `net9.0`/`net10.0` if that is what you have installed). Desktop only: Windows, Linux, macOS.

## X-sheet

The panel on the right shows the exposure sheet: rows are frames, columns are layers (top layer on the left).
A number marks where a drawing starts, the line under it is the hold, a short tick marks where it ends.
Click a cell to jump there, drag down the sheet to scrub. **Active layer on 1s / 2s / 3s** re-times every
drawing on the layer in one click (anime is usually drawn on 2s). **Hold +/-** changes how long one drawing
is shown and everything after it on that layer moves along. **+ Frame** / **Delete frame** work on all layers.
Everything is undoable.

## Colour palettes

The palette drop-down on the left holds ten themed sets (Classic, Anime skin/hair/eyes, Pastel, Vivid pop, Neon cyber, Sunset, Ocean & sky, Forest & earth, Sakura & spring, Night city, Retro 8-bit, Grayscale) and **My colors**: pick any colour, press **+ Add current**, and it is saved between sessions. Hover a swatch to see its hex code.

## Text and export range

The **Text** tool (`T` or the `T Text` button) opens a dialog for text, font, size, bold and italic; the text appears where you click, in the current colour and opacity. It is a normal undoable item on the layer, so the eraser, onion skin and exports treat it like any other drawing.

**Export MP4** and **Export PNGs** ask which frames to export (From / To, with shortcuts for the whole animation or up to the current frame).

## MP4 export

Checkmate pipes its frames into [FFmpeg](https://ffmpeg.org), which is not bundled. Install it once
(`winget install Gyan.FFmpeg`, `brew install ffmpeg` or `sudo apt install ffmpeg`); Checkmate finds it
automatically, or asks you to locate it and remembers the choice.

## Run

```
dotnet run --project src/Checkmate.Desktop
dotnet test
```

## Controls (Milestone 1)

| Action | Input |
|---|---|
| Draw | Left mouse / pen (pressure supported) |
| Pen / G-Pen / Pencil / Marker / Airbrush | `B` / `G` / `P` / `M` / `A` |
| Eraser / Soft eraser | `E` / `Shift+E` (pen eraser end also works) |
| Text tool (click the canvas, then type) | `T` |
| Eyedropper | `I`, or hold `Alt` and click |
| Brush size - / + | `[` / `]` |
| Zoom / Rotate / Pan | Wheel / Shift+Wheel / Right or middle drag |
| Onion skin on/off | `O` |
| Add frame / hold frame after current | `Insert` / `Shift+Insert` |
| Delete current frame (all layers) | `Delete` |
| Hold this drawing longer / shorter | `=` / `-` |
| Copy held drawing into a new drawing | `D` |
| Undo / Redo | `Ctrl+Z` / `Ctrl+Y` |
| Play / Pause | `Space` |
| Prev / Next frame | `,` / `.` |
| New drawing on a held frame | `N` |

## Architecture

- `Checkmate.Core`: model (Project, Layer, Cel, Stroke), `Editor` (all operations, undo/redo), save/load. No UI code.
- `Checkmate.Desktop`: Avalonia UI, MVVM, Skia compositor.
- `Checkmate.Core.Tests`: xUnit tests for the core.

**Exposure model:** a layer stores drawings keyed by the frame where each starts, and a drawing is held until the next one.
This is how anime is actually made (drawing on 2s, 3s...) and it is the foundation for the X-sheet in Milestone 2.

## Roadmap

1. **Milestone 1:** canvas, pressure brush, eraser, zoom/pan/rotate, layers, undo/redo, timeline, playback, save/open.
2. **Milestone 2:** ~~cached rendering~~, ~~onion skin~~, ~~PNG sequence export~~ done; X-sheet view, lasso + transform, copy/paste frames still to do.
3. **Milestone 3:** gap-closing bucket fill, camera layer, audio + scrubbing, MP4 export.
4. **Milestone 4:** tweening, brush editor, shortcut editor, plugins.
