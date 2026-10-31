### What's new in 0.14.0
- **Upload all to litterbox**: new button under *Clear jobs list*. Uploads every rendered job that isn't uploaded yet, one after the other. Jobs that are already uploaded or uploading right now are skipped, and their upload arrows turn yellow while they wait. If an upload fails (e.g. litterbox is down), it stops there and a popup tells you why instead of trying every job
- **Failed uploads now tell you why**: "litterbox answered HTTP 502", "could not reach litterbox: unable to connect", "timeout"... instead of just a Windows sound. Litterbox error pages are turned into readable text, and the reason is also shown at the bottom of the window
- An answer from litterbox that isn't a link is now counted as a failed upload (it used to be saved as the "link")
- Uploading jobs stay yellow when you scroll them out of view and back

### Install / update
Already have AmqTool: **Help → Check for updates**.

New: download `AmqTool-Setup-v0.14.0.exe` below and run it (no admin needed; on *"Windows protected your PC"* click *More info → Run anyway*), or extract the zip anywhere you can write to. You also need ffmpeg: `winget install Gyan.FFmpeg`.
