# 🌌 Reverie

**Reverie** is a minimalist, premium music-synced lyrics screensaver for Windows. It transforms your desktop into an ethereal audio-visual experience, blending real-time audio reactivity with perfectly timed lyrics.

## ✨ Features

- **Synced Lyrics**: Automatic fetching and pixel-perfect synchronization of lyrics using [LRCLIB](https://lrclib.net/).
- **Windows Media Integration**: Seamlessly detects music from Spotify, Apple Music, YouTube, and more via the Global System Media Transport Controls (GSMTC).
- **Premium Aesthetics**: High-performance 60fps rendering, smooth easing animations, and a curated neon-to-blue color palette.
- **Minimalist Design**: Zero UI clutter. Just the time, the track info, and the music.

## 🚀 Tech Stack

- **Framework**: .NET 10.0 + WPF
- **Audio Engine**: NAudio (WASAPI Loopback Capture)
- **DSP**: Custom Fast Fourier Transform (FFT) with Hann windowing
- **Lyrics API**: LRCLIB
- **OS**: Windows 10/11 (Requires GSMTC support)

## 🛠️ How it Works

1. **Audio Capture**: Uses WASAPI Loopback to intercept system audio without needing virtual cables.
2. **Spectrum Analysis**: Processes raw audio into 64 frequency bands using a high-performance FFT algorithm.
3. **Orb Modulation**: Maps specific frequency ranges to the latitude of the particle sphere, creating a "wave" effect.
4. **Lyric Sync**: Fetches `.lrc` data and uses an interpolated timing engine to account for processing latency (400ms buffer).

## 📥 Installation

1. Clone the repository:
   ```bash
   git clone https://github.com/yourusername/reverie.git
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
- **Auto-Exit**: (Optional) Exit on mouse movement.

---

Built with ❤️ by **Antigravity AI** for the ultimate music listening experience.
