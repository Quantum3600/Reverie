# 🌌 Reverie

**Reverie** is a minimalist screensaver designed for Windows. It provides a clean desktop experience by displaying a clock and seamlessly fetching and displaying synchronized lyrics for the currently playing track. If lyrics are not found, it gracefully falls back to a subtle audio visualizer.

## ✨ Features

- **Dynamic Display**: Shows the time alongside the track's synchronized lyrics. If lyrics are unavailable, it transitions to a minimalist audio visualizer.
- **Synced Lyrics**: Automatically fetches and synchronizes lyrics using [LRCLIB](https://lrclib.net/), featuring smooth, modern easing animations inspired by premium media players.
- **Windows Media Integration**: Seamlessly detects music from Spotify, YouTube, and other media players via Windows Global System Media Transport Controls (GSMTC).
- **Clean Design**: Zero UI clutter. Just the time, the track info, and your music.

## 🚀 Tech Stack

- **Framework**: .NET 10.0 + WPF
- **Audio Engine**: NAudio
- **Lyrics API**: LRCLIB
- **OS**: Windows 10/11 (Requires GSMTC support)

## 🛠️ How it Works

1. **Media Detection**: Connects with Windows media controls to fetch current track metadata.
2. **Lyric Sync**: Retrieves `.lrc` data and utilizes an interpolated timing engine to display fluid lyric transitions.
3. **Audio Visualization**: Captures system audio to display a reactive visualizer when lyrics cannot be found.

## 📥 Installation

1. Clone the repository:
   ```bash
   git clone https://github.com/Quantum3600/Reverie.git
   ```
2. Navigate to the project directory:
   ```bash
   cd Reverie
   ```
3. Run the application:
   ```bash
   dotnet run
   ```

## ⌨️ Controls

- **Any Key**: Exit the screensaver.
- **Mouse Movement**: Exit the screensaver.

---

Built with ❤️ by **Antigravity** for an elegant music listening experience.
