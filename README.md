<div align="center">

<!-- Put your logo here: export Checkmate.ico as a PNG, save it as assets/logo.png, then uncomment -->
<!-- <img src="assets/logo.png" width="140" alt="Checkmate logo"> -->

# Checkmate

### Make your own anime. Free. Forever.

**A friendly, professional-minded 2D animation studio for desktop.**
Easy enough for your first flipbook. Built around a real X-sheet so it can grow with you.

![Status](https://img.shields.io/badge/status-early%20development-ff4d9d)
![.NET](https://img.shields.io/badge/.NET-8-6c5ce7)
![UI](https://img.shields.io/badge/UI-Avalonia%2011-8a7dff)
![Platforms](https://img.shields.io/badge/Windows%20%7C%20macOS%20%7C%20Linux-desktop-444)
![License](https://img.shields.io/badge/license-TBD-lightgrey)

<!-- A screenshot or short GIF here does more than any paragraph. Try: docs/screenshot.png -->
<!-- <img src="docs/screenshot.png" alt="Checkmate main window" width="900"> -->

</div>

---

## Why this exists

I'm an anime fan. I wanted to animate something of my own, so I went looking for a tool.

- **FlipaClip** is lovely and easy, but it runs out of road quickly.
- **OpenToonz** is genuinely professional, and genuinely hard to get started with.
- **Adobe Animate** does everything, but it costs money every month.

There was a gap in the middle: something simple on day one that doesn't hit a wall on day thirty. Checkmate is my attempt to fill it, and I'm building it in the open so that **every anime fan can make their own mini-anime without paying or fighting the software.**

The idea is simple: **start like a flipbook, grow like a studio.** You get a clean canvas and a few tools. When you're ready for it, the same project already contains the real thing, because it's built on the same exposure model professional anime studios use.

---

## What you can do today

Checkmate is early, and I'd rather show you what works than promise the moon. Here's what's in the box right now.

### Draw
- **Seven brushes**, each with its own feel:
  **Pen**, **G-Pen** (anime ink with tapered, pointed line ends), **Pencil** (grainy), **Marker** (translucent, never darkens where lines cross), **Airbrush**, plus a hard **Eraser** and a **Soft eraser**.
- **Pressure-sensitive** with a pen tablet. The mouse works too.
- **Size from 1 to 300 px** on a slider that is precise for tiny lines, and `[` / `]` to nudge it.
- **Opacity** and a **smoothing** slider (stabilizer) for steadier lines.
- Every tool **remembers its own size and opacity**, so switching from a thin pen to a big eraser and back doesn't reset anything.
- A **brush-size ring** under your cursor shows the true size at your current zoom.
- **Zoom, rotate and pan** the canvas the way you would turn a sheet of paper.
- A **Text tool**: click, type, pick a font from the ones installed on your computer, done.

### Colour
- A full colour picker, plus an **eyedropper** (`I`, or hold `Alt` and click) that picks exactly what you see on screen.
- **13 palettes** to choose from: Classic, Anime (skin, hair and eyes), Pastel, Vivid pop, Neon cyber, Sunset, Ocean & sky, Forest & earth, Sakura & spring, Night city, Retro 8-bit, Grayscale, and **My colors**, which is yours and is remembered between sessions.
- **Recent colours** and hover-for-hex on every swatch.

### Animate
- **Layers** you can show and hide.
- **Onion skin**: ghosts of earlier drawings in red and later ones in green, 0 to 4 frames each way, hidden automatically during playback.
- A **frame timeline** with playback, a **1–60 FPS** control, and buttons to **add a frame**, add a **hold** frame, **delete a frame**, start a new drawing, or **copy** the held drawing into a new one.
- **Undo / redo** for everything, including layer and timeline edits.

### The X-sheet (the part I'm proudest of)
Anime isn't usually drawn on every frame. A drawing is **held** for two or three frames ("animating on 2s"), and studios plan that on an **exposure sheet**: rows are frames, columns are layers. Checkmate has one, built in:

- A **number** marks where a drawing starts, a **line** is how long it's held, a **tick** is where it ends.
- Click a cell to jump there, drag down the sheet to scrub.
- **One-click retiming**: set the active layer **on 1s, 2s or 3s**.
- **Hold + / Hold −** to make one drawing last longer or shorter; later drawings on that layer slide along with it.
- Insert and delete frames across **all** layers at once.

### Share
- **Export MP4** (H.264, plays everywhere) via FFmpeg. Choose **which frames** to export, from any start to any end.
- **Export PNG sequence** at full resolution, with the same frame-range picker.
- Save and open your work as `.checkmate` project files.


---

## Your first animation in five minutes

1. **Draw** something simple on frame 1: a ball on the left side of the canvas.
2. Press **`Insert`** (or click **+ Frame**). You're now on a new blank frame, and **onion skin** shows the previous ball in red.
3. Draw the ball a little further to the right, guided by its ghost.
4. Repeat a few times.
5. Press **`Space`** to play it back. Drag the **FPS** slider until it feels right (12 is classic).
6. Open the **X-sheet** panel, make sure your layer is active, and click **2s**. Your animation is now "on twos", the way real anime is timed.
7. Click **Export MP4**, pick the frames you want, and you've got a video.

---

## Getting started

### What you need
- The **[.NET 8 SDK](https://dotnet.microsoft.com/download)**
- *(Optional, for MP4 export)* **[FFmpeg](https://ffmpeg.org/download.html)**:
  `winget install Gyan.FFmpeg` on Windows, `brew install ffmpeg` on macOS, `sudo apt install ffmpeg` on Linux.
  Checkmate finds it on its own, or asks you to point to it once and remembers.

### Run it

```bash
git clone https://github.com/Checkmate-Studios/Checkmate.git
cd Checkmate
dotnet run --project src/Checkmate.Desktop
```

Run the tests with:

```bash
dotnet test
```

> **Using a different .NET version?** Change `net8.0` in `Directory.Build.props` to the one you have installed.
>
> **Platforms:** Checkmate is desktop-only and built on Avalonia, so it's designed to run on Windows, macOS and Linux. It's developed and tested on **Windows** so far, so reports from Mac and Linux are very welcome.

---

## Keyboard & mouse

| Action | How |
|---|---|
| Draw | Left mouse button, or pen |
| Zoom / rotate / pan | Mouse wheel / `Shift` + wheel / right or middle drag |
| Pen · G-Pen · Pencil · Marker · Airbrush | `B` · `G` · `P` · `M` · `A` |
| Eraser · Soft eraser | `E` · `Shift+E` (a pen's eraser end works too) |
| Eyedropper | `I`, or hold `Alt` and click |
| Text tool | `T` |
| Brush smaller / bigger | `[` / `]` |
| Undo / redo | `Ctrl+Z` / `Ctrl+Y` (or `Ctrl+Shift+Z`) |
| Play / pause | `Space` |
| Previous / next frame | `,` / `.` |
| Add frame / add hold frame | `Insert` / `Shift+Insert` |
| Delete current frame | `Delete` |
| New drawing here / copy held drawing | `N` / `D` |
| Hold longer / shorter | `=` / `-` |
| Onion skin on / off | `O` |

Everything is undoable, so don't be afraid to press things.

---

## How it's built

```
Checkmate/
├─ src/
│  ├─ Checkmate.Core/       the brain: project model, editing, undo/redo, save/load (no UI code)
│  └─ Checkmate.Desktop/    the face: Avalonia UI, MVVM, Skia rendering, export
└─ tests/
   └─ Checkmate.Core.Tests/ xUnit tests for the core
```

A few decisions worth knowing about:

- **Vector strokes, not pixels.** Every line is stored as points + pressure + brush, then rendered with [SkiaSharp](https://github.com/mono/SkiaSharp). That keeps line art clean, makes undo cheap, and means exports are rendered fresh at full resolution instead of being scaled-up screenshots.
- **The exposure model.** A layer stores drawings keyed by the frame where each one *starts*, and a drawing is held until the next one. Holds are free, "animate on 2s" is just re-spacing the keys, and the X-sheet is simply a window onto that data.
- **A tested core.** All editing goes through one `Editor` class with no UI dependencies, covered by 18 unit tests: undo/redo, exposure edits, frame insert/delete, text, save/load.
- **Fast while you draw.** Finished drawings are cached as bitmaps; only the stroke you're currently drawing is rendered live on top. Translucent brushes and erasers still come out right.
- **Video export without temp files.** Frames are rendered and streamed straight into FFmpeg's input. Low frame rates are written at 24 fps with repeated frames, so every player and social platform accepts the file with identical timing.

Built with: **C# / .NET 8**, **[Avalonia UI](https://avaloniaui.net)**, **[SkiaSharp](https://github.com/mono/SkiaSharp)**, **[CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet)**, **[xUnit](https://xunit.net)**, and **[FFmpeg](https://ffmpeg.org)** for video.

---

## Honest limitations (for now)

I'd rather you hear it from me:

- **No lasso / move / transform yet.** Once something is drawn or typed, you can erase or undo it, but you can't pick it up and move it. Text can't be edited after placing either.
- **No paint bucket yet.** Colouring is by brush for now. A gap-closing bucket is the next big feature.
- **No audio, camera moves or GIF export yet.**
- **Project files are plain JSON.** Great for debugging and version control, but big for long animations. A compact container format is planned.
- **One scene per project.**
- **Tested mainly on Windows.** It should work on macOS and Linux, but I haven't verified it there.

---

## Roadmap

- [x] Pressure brushes, eraser, zoom / pan / rotate, layers, undo / redo
- [x] Timeline, playback, onion skin
- [x] Seven brush types, size / opacity / smoothing, text tool
- [x] Palettes, custom colours, eyedropper
- [x] **X-sheet** with exposure editing and frame insert / delete
- [x] PNG sequence and **MP4 export** with frame ranges
- [ ] Lasso select, move, scale, rotate (and moving text)
- [ ] Copy and paste frames
- [ ] **Gap-closing paint bucket** for colouring line art
- [ ] Camera layer with pan / zoom / rotate keyframes
- [ ] Audio import with scrubbing, for lip sync
- [ ] GIF export
- [ ] Tweening for cut-out animation
- [ ] Brush editor, shortcut editor
- [ ] Compact project format (zip container), plugin system

Have an idea that isn't on the list? Open an issue. This tool is for animators, and animators should shape it.

---

## Contributing

Checkmate is better with more hands, and you don't need to be an expert.

- **Use it and tell me what felt wrong.** Confusing buttons, slow moments and missing features are all useful reports.
- **Report bugs** with what you did, what you expected, and your operating system.
- **Test on macOS and Linux**, because that's where I need the most help.
- **Send code.** Please open an issue first for anything big so we can agree on the direction. `Checkmate.Core` is the friendliest place to start: it's small, has no UI, and is covered by tests.

Before opening a pull request, run `dotnet test` and make sure it passes.

---

## License

*Not chosen yet.* A license will be added to the repository before the first release. Until then, all rights are reserved by the author.

---

## Thank you

To the teams behind Avalonia, SkiaSharp, FFmpeg and the .NET community, whose free tools made this possible. And to everyone who has ever wanted to bring a drawing to life and didn't know where to start.

<div align="center">

**Now go make something that moves.** 

</div>
