# PawSip: notes for coding agents

Shared project memory for AI coding assistants (Codex, Claude Code, Gemini CLI, …). `CLAUDE.md` and `GEMINI.md` load this file. Keep it up to date when something important changes.

## What this is
PawSip is a free Windows water reminder with an animated vector cat companion and a desktop widget, built in C#/WPF.
- Owner: Muhammad Noman (https://github.com/MuhammadNoman76/PawSip).
- Website: https://www.nomanshafiq.com/pawsip/ (download page) and https://www.nomanshafiq.com/blog/pawsip/ (article). The site lives in a separate repo: `D:\Work\nomanshafiq-portfolio`.
- Behaviour the owner asked for:
  - The cat comes back every 15 minutes until the user drinks.
  - Reminders follow presence (screen on, PC unlocked, keyboard or mouse input in the last 5 minutes), not active hours, and wait while a fullscreen app runs.
  - The meow sound and the small cat size are the defaults.
  - Undo, a choice of cat size, and "remind me later" (5/10/15/30/60 min) are available.
  - Data stays on the PC in `%APPDATA%\PawSip\pawsip.json`.

## Layout
- `src/PawSip/` holds the app.
  - `Core/`: DataStore, Hydration, ReminderScheduler, Updater, Messages, Sounds (meow/drop/chime synthesized in code), StartupManager, and the compat helpers `Compat.cs`, `LegacyPolyfills.cs` and `MathEx.cs`.
  - `Controls/`: CatView.xaml, the vector cat with its moods, plus WaterRing, BarChart and MonthHeatmap.
  - `Views/`: dashboard, statistics, settings, the cat widget and reminder windows, and the tray icon.
  - `Interop/`: Native and Presence.
- `installer/PawSip.iss`: one Inno Setup script for every edition (`/DEdition=x64|arm64|x86|legacy`). It installs per user, with no admin prompt.
- `build.ps1`: builds every edition and writes `SHA256SUMS.txt`.
- `release/`: the published installers plus `SHA256SUMS.txt`. Download links point here.

## Editions (one codebase)
| Edition | Target | Installer |
|---|---|---|
| x64 | net10.0-windows, win-x64, self-contained | `PawSip-Setup-<ver>-x64.exe` |
| arm64 | net10.0-windows, win-arm64, self-contained | `-arm64.exe` |
| x86 | net10.0-windows, win-x86, self-contained | `-x86.exe` |
| legacy | net48 | `-win7-8.exe` (Windows 7 SP1 / 8.1; needs .NET Framework 4.8; Windows 8 needs the 8.1 update) |

## Compatibility rules (don't break)
- The code must compile for both net10.0-windows and net48. Use `Compat`, `MathEx.Clamp` and the polyfills.
- No `Math.Clamp`, `Environment.ProcessPath` or `File.Move(…, true)` directly.
- 32-bit: use the `IntPtr.Size` wrappers in `Interop/Native.cs`; never import `GetWindowLongPtrW` directly.

## Build and release
1. Bump `<Version>` in `src/PawSip/PawSip.csproj` (and the defaults in `build.ps1` and `installer/PawSip.iss`).
2. Build: `powershell -ExecutionPolicy Bypass -File build.ps1 -Version X.Y.Z`. The .NET SDK is at `%LOCALAPPDATA%\Microsoft\dotnet\dotnet.exe`; Inno Setup 6 is at `%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe`. build.ps1 embeds each build's edition (`-p:PawSipEdition=x64|arm64|x86|win7-8` → AssemblyMetadata "Edition"), which the updater uses to pick its installer.
3. Copy the installers and `SHA256SUMS.txt` to `release/`, then commit and push. Push this repo first: the update feed links to these files.
4. In the website repo, update `src/data/pawsip.ts` (version, released, editions with exact bytes and SHA-256) and `public/updates/pawsip.json` (the update feed; a test keeps it in step with pawsip.ts), run `npm run verify`, then commit and push. Every installed PawSip 1.0.2+ sees the new version within a day.

## Automatic updates (1.0.2+)
- `Core/Updater.cs`: reads `https://www.nomanshafiq.com/updates/pawsip.json` (`PAWSIP_UPDATE_URL` overrides it for testing; only then may installer links be plain HTTP), compares versions, downloads this edition's installer to `%TEMP%\PawSip-Update`, checks size and SHA-256 (a mismatch is deleted, never run), then runs it with `/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CURRENTUSER /RELAUNCH=1 /DIR="<app folder>"`. TLS 1.2 is switched on for net48.
- `App.Updates.cs`: first check 30 s after start, then at most every 24 h (`AppData.LastUpdateCheck`; a failed check is logged and retried after an hour). Offers go to the dashboard card (`Views/DashboardPage.Updates.cs`) or, with the window closed, a tray balloon that waits while `Native.IsUserBusy()` (fullscreen) or the user is away. Update now / Later / Skip this version (`Settings.SkippedVersion`); Settings › Updates and the tray menu have Check for updates; `Settings.CheckForUpdates` turns the daily check off.
- `installer/PawSip.iss`: with `/RELAUNCH=1` it skips the `--set-startup` runs (the app's own startup choice stays) and starts `PawSip.exe --background --updated` (tray balloon "is ready"); `[InstallDelete]` removes the old program files first, only in a folder named PawSip that holds PawSip.exe; `StopPawSip` closes only a PawSip.exe running from `{app}` (WMI query + `taskkill /PID`), so other copies are left alone. The app empties `%TEMP%\PawSip-Update` on its next start.
- Only an installed copy (HKCU uninstall entry for this folder, or `unins000.exe` beside it) updates itself; a build-folder copy gets "Download page" instead.

## Testing on the owner's PC (be careful)
- The owner runs PawSip all day. Don't launch a second instance: that shows the main window of the running one. The single-instance mutex is per user (`Core/SingleInstance.cs`), so a test copy can't run beside it; a test copy started with `--background` just exits quietly.
- Never open windows over a full-screen app (the owner watches full-screen video).
- Back up `%APPDATA%\PawSip` before any test that could write to it.
- Installer tests: never run a real PawSip installer on this PC (it shares the owner's AppId, shortcuts and uninstall entry). Compile a side-by-side copy instead, e.g. `ISCC /DAppIdOverride={{<new GUID>} "/DAppName=PawSip Update Test" /O<scratch> installer\PawSip.iss`, install it with `/RELAUNCH=1 /DIR=<scratch>\PawSip` (that also skips `--set-startup`, which would write the owner's data and Run key), and uninstall it afterwards.

## Git and writing rules
- Commit as `Muhammad Noman <muhammadnomanshafiq76@gmail.com>`.
- Never add AI attribution: no `Co-Authored-By` trailer and no "Generated with …" line.
- Don't mention AI in commit messages, README or repo descriptions.
- Ask before pushing unless the owner has asked for it in the current task.
- Read and write text files as UTF-8. PowerShell `Get-Content`/`Set-Content` without `-Encoding` corrupts characters such as · – ’.
- Public claims must match the code. Don't invent features or numbers, and say what is untested.
