# AmqTool

Desktop tool for making AMQ (Anime Music Quiz) uploads: cut OPs/EDs/inserts out of episodes, render them to webm/mp3, check and fix the volume, and upload to litterbox, all from one window.

## Install

1. Download **AmqTool-Setup-vX.Y.Z.exe** from the [latest release](https://github.com/Kikimanox/AmqTool/releases/latest) and run it. No admin rights needed, it installs to `%LOCALAPPDATA%\Programs\AmqTool`.
   - Windows may say *"Windows protected your PC"* because the exe isn't signed: click **More info → Run anyway**.
   - No installer wanted? Download **AmqTool-vX.Y.Z.zip** instead, extract it anywhere you can write to (not Program Files) and run `AmqTool.exe`.
2. You need **ffmpeg**. Don't have it? Open a terminal and run `winget install Gyan.FFmpeg`, AmqTool finds it by itself.
3. On the first start fill in the setup window (the blue **?** buttons explain each folder).
   - ⚠️ The orange **tmp** folder gets wiped every time AmqTool starts and closes. Keep the default or use an empty folder.

**Coming from the old installer** (the one that installed into `C:\Program Files (x86)\AmqTool` and needed the permissions fix): install the new version and on its first start AmqTool offers to import your settings and job lists from the old install. Then uninstall the old one (Settings → Apps → AmqTool, version 0.1).

## Updating

**Help → Check for updates.** AmqTool downloads the new version, restarts itself and keeps your settings and jobs. When an update is out, the Help menu says so. Running a newer setup exe over the old install also works.

## How to use (tl;dr)

1. **Volume**: check the volume and bitrate of files or links, fix the volume with the slider + orange button, re-upload.
2. **Get clips**: cut a clean OP/ED/insert out of an episode. Press **F1** / **F2** while it plays to mark the start / end, then fine tune them with the blue (start) and orange (end) one-frame buttons. Add the job, click its yellow **bulb** to set the volume for AMQ automatically (it aims for -18 dB mean and keeps the max at -1 dB or lower), tick 720/480/mp3 and **Start jobs**. Once rendered, the upload button sends it to litterbox; click it again to copy a Discord-ready block with all links (the 7/4/3 buttons copy single links). Forgot the volume? Move the slider and click the orange button after rendering.
3. **Process**: put audio you edited in Vegas onto the video (only needed for audio edits).
4. **Mkv to Mp4**: convert episodes for Vegas.
5. **Extract audio**: anything → flac.

Uploads go to litterbox and its links only last 72 hours. When you copy a link that has expired, AmqTool tells you and removes it, so you can just upload again.

Drag and drop files into the window: in Volume mode they get analyzed, otherwise they load into the player (PageUp / PageDown switch between dropped files). Almost everything explains itself when you hover over it. Keybinds are in Edit → Keybindings.

## Making a release

1. Bump the version in `AmqTool/Properties/AssemblyInfo.cs` (all three lines).
2. Run `powershell -ExecutionPolicy Bypass -File build\build-release.ps1`. It builds and creates in `dist\`:
   - `AmqTool-vX.Y.Z.zip`: the in-app updater downloads this one, so it must be attached to every release
   - `AmqTool-Setup-vX.Y.Z.exe`: needs Inno Setup 6 (`winget install JRSoftware.InnoSetup`)
3. Create a GitHub release with the tag `vX.Y.Z` and attach both files, e.g.
   `gh release create vX.Y.Z dist\AmqTool-vX.Y.Z.zip dist\AmqTool-Setup-vX.Y.Z.exe --title "AmqTool vX.Y.Z" --notes "what changed"`

Building needs Visual Studio 2022+ (or the VS Build Tools) with .NET Framework 4.7.2. Only use **Release | Any CPU**: `lib\mpv-1.dll` is 32-bit, so the app has to run as a 32-bit process.

To test without touching the internet, set these before starting AmqTool: `AMQTOOL_UPDATE_URL` (a local copy of a GitHub `releases/latest` JSON) for the updater, and `AMQTOOL_LITTERBOX_URL` (a fake `api.php`) for uploads.
