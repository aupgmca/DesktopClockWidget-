# Desktop Clock Widget
**Powered by Tech House** | Version 3.0

A beautiful desktop clock widget for Windows with a Fliqlo-style flip-clock screensaver and a study stopwatch — all in one lightweight app.

![Platform](https://img.shields.io/badge/platform-Windows%2010%2F11-blue) ![License](https://img.shields.io/badge/license-Free-green)

---

## Features

- **Desktop clock widget** — always visible, resizable, movable, HH:MM:SS with optional date
- **12-hour / 24-hour** format (double-click the clock to switch)
- **Always on top (Topmost)** — on/off
- **Backgrounds** — transparent, any solid color, or your own image
- **Text styling** — italic digits with gradient fill (2 colors) + edge outline color, 6 ready-made color presets
- **Flip-clock screensaver** — Fliqlo-style dark cards with animated flipping digits; auto-starts after 10 sec – 10 min of inactivity; covers all monitors in jet black
- **Stopwatch (study timer)** — laps, keyboard shortcuts, rectangle or circular ring-dial shape
- **Auto-start with Windows** — set it once, runs on every boot
- **Remembers everything** — position, size, colors, and settings survive restarts

---

## Requirements

- Windows 10 or Windows 11 (64-bit or 32-bit)
- No downloads needed — the app is compiled using the .NET Framework compiler already built into Windows

---

## Installation

You need these 4 files in one folder:

| File | Purpose |
|------|---------|
| `Clock.cs` | The full source code of the app |
| `Build.bat` | Compiles the source into `GNSClock.exe` |
| `Install.bat` | Installs the exe properly on your PC |
| `Uninstall.bat` | Removes everything cleanly |

### Steps

1. **Put all 4 files in one folder** (e.g. `D:\GNSClock`).
2. **Double-click `Build.bat`** — a black window compiles the app. Wait for **SUCCESS! GNSClock.exe created**.
3. **Double-click `Install.bat`** — this:
   - Copies the app to `%LOCALAPPDATA%\GNSClock`
   - Registers it in **Settings → Apps → Installed apps** as *"GNS Clock (powered by Tech House)"*
   - Creates a **Start Menu shortcut** ("GNS Clock")
   - Enables **auto-start with Windows**
   - Starts the clock immediately
4. Done! You can delete the build folder — the installed app is independent of it.

### Updating

Replace `Clock.cs` with a newer version, then run `Build.bat` → `Install.bat` again. The installer replaces the old version automatically. Your settings are kept.

### Uninstalling

Either uninstall **GNS Clock** from **Settings → Apps → Installed apps**, or run `Uninstall.bat`. Both remove the app, the startup entry, the Start Menu shortcut, and saved settings.

---

## How It Works

### The clock widget

| Action | Result |
|--------|--------|
| Left-drag anywhere | Move the clock |
| Drag any edge/corner | Resize (digits scale automatically) |
| Double-click | Toggle 12h / 24h |
| Right-click | Open the settings menu |
| Double-click tray icon | Hide / show the clock |

### Right-click menu

- **24-hour format** — switch between `02:30:45 PM` and `14:30:45`
- **Show date** — show/hide the date line under the time
- **Always on top (Topmost)** — keep the clock above all windows, or let windows cover it
- **Screensaver now (fullscreen)** — jump straight into the flip-clock screensaver
- **Auto screensaver after** — Off / 10 s / 20 s / 30 s / 1 min / 5 min / 10 min of no mouse & keyboard activity
- **Stopwatch (study timer)** — open the stopwatch window
- **Background** — *Transparent* (only digits float on your desktop), *Choose color*, *Choose image*, *Remove image*
- **Text colors** — *Gradient fill* on/off, *Fill color 1 (top)*, *Fill color 2 (bottom)*, *Edge color*, plus 6 presets (Neon Cyan→Blue, Gold→Amber, White→Silver, Pink→Violet, Lime→Green, Orange→Red)
- **Start with Windows** — tick = the clock launches automatically at every boot
- **Exit** — close the clock

### The flip-clock screensaver

When your PC is idle for the chosen time (or you pick *Screensaver now*):

1. Every monitor turns jet black.
2. The main screen shows three rounded dark cards — **HH | MM | SS** — with big light digits and a hinge line, just like a classic flip clock.
3. Each time a digit changes, the card **flips**: the top flap folds down over the hinge and the new number unfolds below — a smooth 0.3 s animation.
4. AM/PM sits in the corner of the hour card; the date (if enabled) appears below in soft grey.
5. **Move the mouse or press any key** — the screensaver closes instantly and your normal clock (with all its colors) returns.

### The stopwatch (study timer)

Built for students timing themselves per question.

| Control | Keyboard | What it does |
|---------|----------|--------------|
| Start / Pause | `Space` | Run or pause the timer |
| Lap | `L` | Record the current question's time (split + total shown in the lap list) |
| Reset | `R` | Back to 00:00:00.0 and clear laps |

Right-click the stopwatch for its own options:

- **Always on top (Topmost)** — on/off
- **Shape** — *Rectangle* (freely resizable, with lap list) or *Circle (ring dial)* — a round stopwatch with a glowing ring that sweeps as seconds pass, round buttons inside the dial, drag to move, **scroll wheel to resize**
- **Background** — transparent / color / image (independent from the main clock)
- **Timer text color** — also colors the circle's ring and buttons
- **Close stopwatch**

The stopwatch and the clock run independently — you can use both at once.

---

## Where things are stored

| Item | Location |
|------|----------|
| Installed program | `%LOCALAPPDATA%\GNSClock\GNSClock.exe` |
| Your settings | `%APPDATA%\GNSClock\settings.ini` |
| Auto-start entry | Registry: `HKCU\...\CurrentVersion\Run` → `GNSClock` |
| Installed-apps entry | Registry: `HKCU\...\CurrentVersion\Uninstall\GNSClock` |

Everything is per-user — no administrator rights are required for install or uninstall.

---

## Troubleshooting

- **"GNSClock.exe not found" when installing** → run `Build.bat` first, then `Install.bat`.
- **Clock doesn't start after reboot** → right-click the clock → make sure **Start with Windows** is ticked. If you moved the exe manually, re-run `Install.bat`.
- **Can't click the clock in transparent mode** → only the digits are clickable in transparent mode; right-click directly on a digit.
- **Two clocks appear** → an old version is still running; right-click the old one → Exit, then re-run `Install.bat` (it removes old versions automatically).

---

*Made with C# WinForms. Compiled locally on your own PC — no external downloads, no internet needed.*
