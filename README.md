<p align="center">
  <img src="docs/icon.png" width="120" alt="PawSip icon" />
</p>

<h1 align="center">PawSip</h1>

<p align="center">
  <b>A cute desktop cat that keeps you hydrated.</b><br/>
  A tiny water-reminder app for Windows: an animated cat lives on your desktop, tracks your daily water
  and asks you to drink at the right moments, without ever getting in your way.
</p>

<p align="center">
  <a href="release/PawSip-Setup-1.0.1.exe"><b>⬇ Download PawSip for Windows</b></a>
  &nbsp;·&nbsp; Windows 10 / 11 (64-bit) &nbsp;·&nbsp; free, no account, works offline
</p>

<p align="center">
  <img src="docs/cat-moods.png" width="760" alt="The PawSip cat: idle, waving, walking, sleeping, drinking and happy" />
</p>

---

## Why PawSip?

Most reminder apps nag you with plain pop-ups that you learn to ignore. PawSip gives you a little companion
instead: a ginger cat that naps on your desktop, wakes up when it's time to drink, waves at you, meows, and
sips a glass of water with you when you do. It's charming, but it's also built to stay out of your way.
It never steals keyboard focus, waits while you're in a fullscreen game, video or presentation, and stays
quiet outside your active hours.

## Features

### 🐱 Your desktop cat
- **Desktop widget.** The cat sits on your desktop with a water gauge (`1,250 / 2,000 ml`). Hover it to log
  a glass, undo the last drink, or open the app.
- **Reminders right where it sits.** At reminder time the cat wakes up, waves, meows and asks:
  **I drank water**, **Remind me later** (5, 10, 15, 30 min or 1 hour), or **Dismiss**.
- **Fully animated.** It blinks, looks around, twitches its ears, waves, walks, naps with floating *Zzz*,
  drinks through a straw and celebrates with hearts. Click it to pet it.
- **Draggable.** Put it anywhere; it remembers the spot. Choose **Small** (default), **Medium** or **Large**.
- **Right-click menu.** Drink, undo, "remind me in…", cat size, keep on top, hide.
- **Calm by design.** Between reminders it naps. If you ignore a reminder it curls up and stops asking.

### 💧 Daily goal
- Set your goal in **glasses or milliliters** and choose your glass size (150–1000 ml).
- An animated **water ring** fills up as you drink.
- Log a glass with one click, or add 100 ml, 500 ml or any custom amount. Undo mistakes.
- **Resets automatically at midnight.** Every day is kept in your history.

### ⏰ Smart reminders
- **Follows you, not the clock.** Whenever you're at the PC (screen on, unlocked, keyboard or mouse in use),
  the cat can remind you, any time of day. While you're away, the screen is off or the PC is locked, it
  waits, and greets you with a "Welcome back!" reminder as soon as you return.
- **Doesn't give up.** Dismiss or ignore a reminder without drinking and the cat comes back after
  **15 minutes**, again and again, until you've had some water. It gets a little more insistent each time.
- Remind every **20, 30, 45, 60, 90 or 120 minutes**.
- **Smart timing.** Reminders come a bit more often when you're behind on your goal and relax when you're
  ahead. Once you hit your goal the cat rests until tomorrow (optional).
- **Your day** (for example 8:00 AM – 10:00 PM) paces your goal. Prefer strict quiet hours? Turn off
  "Remind me whenever I'm using my PC" and reminders only come inside those hours.
- **Never interrupts** fullscreen apps, games, videos or presentations.
- Pause for 30 min, 1 hour, 2 hours or until tomorrow from the tray.

### 📊 Dashboard and statistics
- Dashboard with today's intake, remaining glasses, next reminder, goal streak and today's pace.
- Timeline of today's sips and a weekly chart.
- **Weekly and monthly statistics.** Daily average, days the goal was met, best day, total liters, and a
  monthly hydration calendar.

<p align="center">
  <img src="docs/dashboard.png" width="760" alt="PawSip dashboard" />
</p>

### ⚙️ Runs quietly in the background
- **Starts with Windows** (can be turned off) and lives in the system tray.
- Keeps reminding you when the window is closed.
- Light on resources: about **65 MB** of memory with the desktop cat on screen (about 25 MB tray-only) and
  well under **1% CPU**. A tiny check runs every 20 seconds, and the cat animates at a gentle frame rate only
  while it's visible.
- Your preferences, progress and reminder schedule are restored after a restart.

### 🔊 Sounds
- A soft synthesized **meow** (default), a chime, a water drop, or silence, with adjustable volume.

## Install

1. Download **[PawSip-Setup-1.0.1.exe](release/PawSip-Setup-1.0.1.exe)**.
2. Run it. No admin rights needed; it installs just for your user account.
3. Keep **Start PawSip with Windows** ticked so your cat is always there.

Uninstall from *Settings › Apps*. Your history stays in `%APPDATA%\PawSip` in case you reinstall.

## Tips

| Where | What you can do |
|---|---|
| Desktop cat | Hover: drink a glass, undo, open the app · Right-click: remind me in…, cat size, keep on top · Drag: move it · Click: pet it |
| Reminder bubble | **I drank water** · **Remind me later** (pick the time) · ✕ dismiss |
| Tray icon | Open, log a glass, remind me now, show or hide the desktop cat, pause or resume reminders, exit |
| Settings | Goal and glass size, interval, activity-based reminders, your day's hours, smart timing, "ask again" time, snooze length, fullscreen behaviour, desktop cat, cat style and size, animations, sound and volume, start with Windows |

## Privacy

Everything stays on your PC in `%APPDATA%\PawSip\pawsip.json`. There are no accounts, no network access and
no tracking.

## Build from source

Requirements: [.NET 10 SDK](https://dot.net) and [Inno Setup 6](https://jrsoftware.org/isdl.php) (for the installer).

```powershell
powershell -ExecutionPolicy Bypass -File build.ps1                 # app + installer -> dist\
powershell -ExecutionPolicy Bypass -File build.ps1 -SkipInstaller  # app only -> publish\
```

The app is published self-contained, so the target PC doesn't need .NET installed.

### Project structure

```
src/PawSip/
  Core/       Hydration tracking, smart reminder scheduler, storage, startup, synthesized sounds
  Controls/   The animated vector cat, water ring, bar chart, month calendar, stepper
  Views/      Dashboard, statistics, settings, desktop widget, reminder cat, tray icon
  Themes/     Pastel theme: colors, icons, buttons, switches, chips
  Interop/    Windows helpers (fullscreen and idle detection, non-activating windows)
installer/    Inno Setup script
tools/        Icon builder
```

Built with C# and WPF on .NET 10. The cat is drawn entirely with vector shapes, so it stays crisp at any
size and DPI.

---

<p align="center">Made with 💧 and 🐾 by <a href="https://github.com/MuhammadNoman76">Muhammad Noman</a></p>
