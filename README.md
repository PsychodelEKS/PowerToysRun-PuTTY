# PowerToys Run PuTTY

PowerToys Run plugin for opening saved PuTTY and KiTTY sessions.

Current version: `0.1.3`.

## Features

- Reads PuTTY sessions from `HKCU\Software\SimonTatham\PuTTY\Sessions`.
- Reads KiTTY sessions from `HKCU\Software\9bis.com\KiTTY\Sessions`.
- Reads file-backed KiTTY/PuTTY-style sessions from a configured folder, such as `Sessions` next to `kitty.exe`.
- Shows a session-specific icon when the saved session defines one; otherwise uses the configured PuTTY or KiTTY executable icon.
- Uses `putty <query>` to search and launch saved sessions.
- Launches PuTTY or KiTTY with `-load <session>`.
- Supports separate executable paths for `putty.exe` and `kitty.exe`.
- Supports `putty settings` and `putty rescan` commands.
- Optionally exposes sessions in global PowerToys Run results.

## Install with ptr

Install [`ptr`](https://github.com/8LWXpg/ptr), then run:

```powershell
ptr add PuTTY PsychodelEKS/PowerToysRun-PuTTY
```

To update an existing install:

```powershell
ptr update PuTTY
```

## Manual install

1. Download the latest `PowerToysRun-PuTTY-*-x64.zip` or `PowerToysRun-PuTTY-*-arm64.zip` from the [releases page](https://github.com/PsychodelEKS/PowerToysRun-PuTTY/releases).
2. Exit PowerToys completely.
3. Create this folder if it does not exist:

```powershell
$env:LOCALAPPDATA\Microsoft\PowerToys\PowerToys Run\Plugins\PuTTY
```

4. Extract the zip contents directly into that folder. `plugin.json` should be at:

```powershell
$env:LOCALAPPDATA\Microsoft\PowerToys\PowerToys Run\Plugins\PuTTY\plugin.json
```

5. Start PowerToys again.

## Usage

Use the activation keyword:

```text
putty <query>
```

Control commands:

```text
putty settings
putty rescan
```

`putty settings` opens a small editor for executable paths, registry source toggles, and the file sessions folder. `putty rescan` refreshes the session index in the background and shows a notification when it finishes.

The context menu supports:

- Open
- Copy session name
- Copy host
- Rescan sessions

## Settings

Open PowerToys Settings, go to PowerToys Run plugins, then open `PuTTY`.

Available settings:

- `PuTTY executable path`: path to `putty.exe`, or `putty.exe` if available in `PATH`.
- `KiTTY executable path`: path to `kitty.exe`, or `kitty.exe` if available in `PATH`.
- `Enable PuTTY sessions`: read PuTTY registry sessions.
- `Enable KiTTY sessions`: read KiTTY registry sessions.
- `Enable file sessions`: read session files from a configured folder.
- `File sessions directory`: absolute path to a sessions folder, or a relative path such as `Sessions`. Relative paths are resolved from the KiTTY executable directory first, then the PuTTY executable directory.
- `Include in global result`: let sessions appear outside the `putty` keyword.

The plugin stores its own settings and cache outside the installed plugin folder:

```powershell
$env:LOCALAPPDATA\Microsoft\PowerToys\PowerToys Run\Settings\Plugins\Community.PowerToys.Run.Plugin.PuTTY\settings.json
$env:LOCALAPPDATA\Microsoft\PowerToys\PowerToys Run\Settings\Plugins\Community.PowerToys.Run.Plugin.PuTTY\index.json
```

## Build

```powershell
dotnet build .\PowerToysRun-PuTTY.sln -c Release -p:Platform=x64
```

The project targets `net10.0-windows` and is built against `Community.PowerToys.Run.Plugin.Dependencies` `0.97.0`.

## Release

Create and push a version tag:

```powershell
git tag -a vX.Y.Z -m "vX.Y.Z"
git push origin main vX.Y.Z
```

GitHub Actions will publish `x64` and `arm64` zip assets compatible with `ptr`.
