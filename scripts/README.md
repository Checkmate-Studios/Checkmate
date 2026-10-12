# Build scripts

These scripts turn Checkmate's source code into a program you can double-click.
The result is **self-contained**: whoever runs it does not need .NET installed.

You only need the **.NET 8 SDK** (and **Git**, if the script has to download the code) on the
computer that *builds* it. The scripts offer to install what is missing on Windows (via winget)
or tell you the exact command for your system.

Each script works two ways:
- **Inside the repo** (this `scripts/` folder): it builds your working copy.
- **On its own** (copied anywhere): it first downloads Checkmate from GitHub into a
  `Checkmate-source` folder next to the script, then builds that.

| I use...                         | Run                                   | Result                                   |
|----------------------------------|---------------------------------------|------------------------------------------|
| **Windows**                      | double-click `build-windows.bat`      | `dist\win-x64\Checkmate.exe`             |
| **macOS**                        | double-click `build-macos.command`    | `dist/osx-*/Checkmate.app`               |
| **Linux**                        | `./build-linux.sh`                    | `dist/linux-*/Checkmate`                 |
| **All three at once (Windows)**  | double-click `build-all.bat`          | `dist\release\Checkmate-*.zip`           |
| **All three at once (Mac/Linux)**| `./build-all.sh`                      | `dist/release/Checkmate-*`               |

## Options

**Windows** (`build-windows.ps1`, or pass them to the `.bat`): `-Shortcut` (Desktop shortcut), `-Run`, `-NoPrompt`, `-Rid win-arm64`

**Linux** (`build-linux.sh`): `--install` (adds Checkmate to your app menu, no sudo), `--run`, `--rid linux-arm64`

**macOS** (`build-macos.sh`): `--install` (copies the app to `~/Applications`), `--run`, `--rid osx-x64`

**All** (`build-all.ps1` / `build-all.sh`): `-Only windows,linux,macos` / `--only linux,macos`

## Good to know

- **macOS:** build the Mac app *on a Mac* if you can. The script then signs it (a free "ad-hoc" signature,
  needed on Apple Silicon) and gives it an icon. An app you built yourself does not trigger the
  "unidentified developer" warning.
- **Cross-building:** `build-all` can produce the Mac and Linux builds from Windows, but Windows cannot mark files
  "executable" or sign Mac apps. Those zips contain a `README-FIRST.txt` with the one or two commands to run
  after unzipping. Building on Mac/Linux with `build-all.sh` avoids this.
- **Linux libraries:** if Checkmate does not start, install the common desktop libraries:
  `sudo apt install libfontconfig1 libx11-6 libice6 libsm6`
- **MP4 export** still needs FFmpeg on the machine that *runs* Checkmate (see the main README).
- The first build downloads packages and takes a few minutes; later builds are much faster.
