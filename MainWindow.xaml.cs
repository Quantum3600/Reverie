using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Threading.Tasks;
using Reverie.Services;
using Reverie.ViewModels;

namespace Reverie;

public static class ScrollAnimationBehavior
{
    public static readonly DependencyProperty VerticalOffsetProperty = DependencyProperty.RegisterAttached(
        "VerticalOffset", typeof(double), typeof(ScrollAnimationBehavior),
        new PropertyMetadata(0.0, OnVerticalOffsetChanged));

    public static void SetVerticalOffset(DependencyObject target, double value) => target.SetValue(VerticalOffsetProperty, value);
    public static double GetVerticalOffset(DependencyObject target) => (double)target.GetValue(VerticalOffsetProperty);

    private static void OnVerticalOffsetChanged(DependencyObject target, DependencyPropertyChangedEventArgs e)
    {
        if (target is ScrollViewer scrollViewer)
        {
            scrollViewer.ScrollToVerticalOffset((double)e.NewValue);
        }
    }
}

public static class VsmBehavior
{
    public static readonly DependencyProperty StateProperty = DependencyProperty.RegisterAttached(
        "State", typeof(LineState), typeof(VsmBehavior),
        new PropertyMetadata(LineState.Upcoming, OnStateChanged));

    public static void SetState(DependencyObject target, LineState value) => target.SetValue(StateProperty, value);
    public static LineState GetState(DependencyObject target) => (LineState)target.GetValue(StateProperty);

    private static void OnStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is FrameworkElement fe)
        {
            System.Windows.VisualStateManager.GoToElementState(fe, e.NewValue.ToString(), true);
        }
    }
}

public partial class MainWindow : Window
{
    private DispatcherTimer _timer;
    private MediaService _mediaService;
    private LyricsService _lyricsService;
    private string _currentTrackTitle = string.Empty;
    private string _currentArtist = string.Empty;
    private ObservableCollection<LyricLineViewModel> _lyricLines = new();
    private int _currentActiveLineIndex = -1;
    private TimeSpan _lastValidPosition = TimeSpan.Zero;
    private AudioSpectrumService _spectrumService;
    private bool _isFetchingLyrics = false;
    private bool _lastFetchSuccessful = true;
    private bool _isUpdating = false;
    private bool _isNetworkAvailable = true;
    private Random _glowRandom = new Random();

    public MainWindow()
    {
        InitializeComponent();
        _mediaService = new MediaService();
        _lyricsService = new LyricsService();

        // Setup a timer to update the clock and fetch media info frequently (100ms)
        _timer = new DispatcherTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(100);
        _timer.Tick += Timer_Tick;
        _timer.Start();

        _spectrumService = new AudioSpectrumService();
        IdleOrb.SetService(_spectrumService);

        LyricsItemsControl.ItemsSource = _lyricLines;

        // Force an immediate update
        Timer_Tick(null, EventArgs.Empty);

        StartGlowAnimation();
    }

    private async void Timer_Tick(object? sender, EventArgs e)
    {
        if (_isUpdating) return;
        _isUpdating = true;

        try 
        {
            // Update Time
            ClockText.Text = DateTime.Now.ToString("HH:mm");
            DateText.Text = DateTime.Now.ToString("dddd, MMMM d");

            // Check Network
            _isNetworkAvailable = System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable();

        // Update Media Info (Requires Windows 10/11)
        try 
        {
            var trackInfo = await _mediaService.GetCurrentTrackAsync();
            if (trackInfo.Title != _currentTrackTitle || trackInfo.Artist != _currentArtist)
            {
                TrackText.Text = trackInfo.Title;
                ArtistText.Text = trackInfo.Artist;
                _currentTrackTitle = trackInfo.Title;
                _currentArtist = trackInfo.Artist;
                _lyricLines.Clear();
                _currentActiveLineIndex = -1;
                _lastValidPosition = TimeSpan.Zero;
                
                if (trackInfo.Thumbnail != null)
                {
                    try
                    {
                        var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                        using (var stream = await trackInfo.Thumbnail.OpenReadAsync())
                        {
                            bitmap.BeginInit();
                            bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                            bitmap.StreamSource = stream.AsStream();
                            bitmap.EndInit();
                        }
                        bitmap.Freeze();
                        AlbumArtImage.Source = bitmap;
                        AlbumArtImage.Visibility = Visibility.Visible;

                        var dominantColor = GetDominantColor(bitmap);
                        IdleOrb.SetAccentColor(dominantColor);
                        
                        // Glow 1: Base color
                        var color1 = dominantColor; color1.A = 0x77;
                        var mid1 = dominantColor; mid1.A = 0x22;
                        
                        // Glow 2: Slightly lighter/shifted
                        var color2 = System.Windows.Media.Color.FromArgb(0x55, (byte)Math.Min(255, dominantColor.R * 1.2), (byte)Math.Min(255, dominantColor.G * 1.2), (byte)Math.Min(255, dominantColor.B * 1.2));
                        var mid2 = color2; mid2.A = 0x11;
                        
                        // Glow 3: Slightly darker
                        var color3 = System.Windows.Media.Color.FromArgb(0x33, (byte)(dominantColor.R * 0.8), (byte)(dominantColor.G * 0.8), (byte)(dominantColor.B * 0.8));
                        var mid3 = color3; mid3.A = 0x08;

                        var ease = new QuarticEase { EasingMode = EasingMode.EaseOut };
                        
                        Glow1ColorStop.BeginAnimation(System.Windows.Media.GradientStop.ColorProperty, new ColorAnimation { To = color1, Duration = TimeSpan.FromSeconds(2), EasingFunction = ease });
                        Glow1MidStop.BeginAnimation(System.Windows.Media.GradientStop.ColorProperty, new ColorAnimation { To = mid1, Duration = TimeSpan.FromSeconds(2), EasingFunction = ease });

                        Glow2ColorStop.BeginAnimation(System.Windows.Media.GradientStop.ColorProperty, new ColorAnimation { To = color2, Duration = TimeSpan.FromSeconds(2), EasingFunction = ease });
                        Glow2MidStop.BeginAnimation(System.Windows.Media.GradientStop.ColorProperty, new ColorAnimation { To = mid2, Duration = TimeSpan.FromSeconds(2), EasingFunction = ease });

                        Glow3ColorStop.BeginAnimation(System.Windows.Media.GradientStop.ColorProperty, new ColorAnimation { To = color3, Duration = TimeSpan.FromSeconds(2), EasingFunction = ease });
                        Glow3MidStop.BeginAnimation(System.Windows.Media.GradientStop.ColorProperty, new ColorAnimation { To = mid3, Duration = TimeSpan.FromSeconds(2), EasingFunction = ease });
                    }
                    catch
                    {
                        AlbumArtImage.Source = null;
                        AlbumArtImage.Visibility = Visibility.Collapsed;
                        SetDefaultGlowColor();
                    }
                }
                else
                {
                    AlbumArtImage.Source = null;
                    AlbumArtImage.Visibility = Visibility.Collapsed;
                    SetDefaultGlowColor();
                }
                
                if (!string.IsNullOrWhiteSpace(_currentTrackTitle) && _currentTrackTitle != "No Track")
                {
                    _isFetchingLyrics = true;
                    // Fire and forget the fetch task to avoid blocking the UI thread
                    _ = Task.Run(async () => {
                        try 
                        {
                            var result = await _lyricsService.GetLyricsAsync(_currentTrackTitle, _currentArtist);
                            
                            // Marshall back to UI thread to update collection
                            await Dispatcher.InvokeAsync(() => {
                                if (_currentTrackTitle == trackInfo.Title) // Ensure we are still on the same track
                                {
                                    _lastFetchSuccessful = result.IsSuccessful;
                                    _lyricLines.Clear();
                                    
                                    // Only add if we have synced lyrics (more than 1 line)
                                    if (result.Lines.Count > 1)
                                    {
                                        foreach (var line in result.Lines)
                                        {
                                            _lyricLines.Add(new LyricLineViewModel 
                                            { 
                                                StartTime = line.StartTime, 
                                                Text = line.Text,
                                                IsInstrumental = line.IsInstrumental
                                            });
                                        }
                                    }
                                    else 
                                    {
                                        _lastFetchSuccessful = false; // Treat unsynced/empty as failure for fallback
                                    }
                                }
                                _isFetchingLyrics = false;
                            });
                        }
                        catch 
                        {
                            await Dispatcher.InvokeAsync(() => {
                                _lastFetchSuccessful = false;
                                _isFetchingLyrics = false;
                            });
                        }
                    });
                }
            }

            // Sync Lyrics
            var rawPosition = await _mediaService.GetPlaybackPositionAsync();
            if (rawPosition != null && _lyricLines.Count > 0)
            {
                var actualPosition = rawPosition.Value;
                
                // Prevent jitter: if the new position went slightly backwards (less than 1 second), ignore the backward jump
                if (actualPosition < _lastValidPosition && (_lastValidPosition - actualPosition).TotalSeconds < 1.0)
                {
                    actualPosition = _lastValidPosition;
                }
                else
                {
                    _lastValidPosition = actualPosition;
                }

                // Add a small offset (e.g., 400ms) to make lyrics appear slightly earlier
                var position = actualPosition.Add(TimeSpan.FromMilliseconds(800));
                
                int newActiveLineIndex = -1;
                for (int i = 0; i < _lyricLines.Count; i++)
                {
                    if (position >= _lyricLines[i].StartTime && (i == _lyricLines.Count - 1 || position < _lyricLines[i + 1].StartTime))
                    {
                        newActiveLineIndex = i;
                        break;
                    }
                }

                if (newActiveLineIndex != _currentActiveLineIndex)
                {
                    for (int i = 0; i < _lyricLines.Count; i++)
                    {
                        if (i == newActiveLineIndex)
                            _lyricLines[i].State = LineState.Current;
                        else if (i == newActiveLineIndex - 1)
                            _lyricLines[i].State = LineState.Previous;
                        else if (i < newActiveLineIndex - 1)
                            _lyricLines[i].State = LineState.Played;
                        else
                            _lyricLines[i].State = LineState.Upcoming;
                    }

                    if (newActiveLineIndex != -1)
                    {
                        var container = LyricsItemsControl.ItemContainerGenerator.ContainerFromIndex(newActiveLineIndex) as FrameworkElement;
                        if (container != null && container.IsDescendantOf(LyricsItemsControl))
                        {
                            var transform = container.TransformToAncestor(LyricsItemsControl);
                            var point = transform.Transform(new Point(0, 0));
                            
                            double absoluteY = point.Y + LyricsItemsControl.Margin.Top;
                            double targetOffset = absoluteY - (LyricsScrollViewer.ViewportHeight / 2) + (container.ActualHeight / 2);
                            if (targetOffset < 0) targetOffset = 0;
                            
                            double currentOffset = LyricsScrollViewer.VerticalOffset;
                            
                            if (Math.Abs(targetOffset - currentOffset) > 2000)
                            {
                                LyricsScrollViewer.BeginAnimation(ScrollAnimationBehavior.VerticalOffsetProperty, null);
                                LyricsScrollViewer.ScrollToVerticalOffset(targetOffset);
                            }
                            else 
                            {
                                ScrollAnimationBehavior.SetVerticalOffset(LyricsScrollViewer, currentOffset);
                                DoubleAnimation anim = new DoubleAnimation
                                {
                                    To = targetOffset,
                                    Duration = TimeSpan.FromMilliseconds(800),
                                    EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseOut }
                                };
                                LyricsScrollViewer.BeginAnimation(ScrollAnimationBehavior.VerticalOffsetProperty, anim);
                            }
                        }
                    }
                    
                    _currentActiveLineIndex = newActiveLineIndex;
                }
            }

            // Update Visibility States
            bool hasLyrics = _lyricLines.Count > 0;
            bool showLyrics = hasLyrics && _currentActiveLineIndex != -1;
            bool showLoading = _isFetchingLyrics;

            LyricsScrollViewer.Visibility = showLyrics ? Visibility.Visible : Visibility.Collapsed;
            NoTextIndicator.Visibility = showLoading ? Visibility.Visible : Visibility.Collapsed;
            
            // Show orb ONLY if:
            // 1. Not currently fetching
            // 2. We don't have lyrics
            // 3. OR if we are offline
            bool shouldShowOrb = !_isNetworkAvailable || (!hasLyrics && !showLoading);
            
            if (shouldShowOrb)
            {
                if (IdleOrb.Visibility != Visibility.Visible)
                    IdleOrb.Visibility = Visibility.Visible;
            }
            else
            {
                if (IdleOrb.Visibility != Visibility.Collapsed)
                    IdleOrb.Visibility = Visibility.Collapsed;
            }
            
            // If we have lyrics but are in the intro, show the scroll viewer
            if (_isNetworkAvailable && hasLyrics && _currentActiveLineIndex == -1 && !showLoading)
            {
                LyricsScrollViewer.Visibility = Visibility.Visible;
            }
        }
        catch 
        {
            _lyricLines.Clear();
            _currentTrackTitle = string.Empty;
            _currentArtist = string.Empty;
            
            LyricsScrollViewer.Visibility = Visibility.Collapsed;
            NoTextIndicator.Visibility = Visibility.Collapsed;
            IdleOrb.Visibility = Visibility.Visible;
            AlbumArtImage.Visibility = Visibility.Collapsed;
            
            TrackText.Text = "";
            ArtistText.Text = "";
        }
    }
    finally
    {
        _isUpdating = false;
    }
}

    private DateTime _startTime = DateTime.Now;

    private void Window_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        // Logic disabled by user request
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Exit on any key press
        Application.Current.Shutdown();
    }

    private void SetDefaultGlowColor()
    {
        var ease = new QuarticEase { EasingMode = EasingMode.EaseOut };
        
        Glow1ColorStop.BeginAnimation(System.Windows.Media.GradientStop.ColorProperty, new ColorAnimation { To = System.Windows.Media.Color.FromArgb(0x77, 0xFF, 0x00, 0x55), Duration = TimeSpan.FromSeconds(2), EasingFunction = ease });
        Glow1MidStop.BeginAnimation(System.Windows.Media.GradientStop.ColorProperty, new ColorAnimation { To = System.Windows.Media.Color.FromArgb(0x22, 0xFF, 0x00, 0x55), Duration = TimeSpan.FromSeconds(2), EasingFunction = ease });

        Glow2ColorStop.BeginAnimation(System.Windows.Media.GradientStop.ColorProperty, new ColorAnimation { To = System.Windows.Media.Color.FromArgb(0x55, 0xFF, 0x33, 0x77), Duration = TimeSpan.FromSeconds(2), EasingFunction = ease });
        Glow2MidStop.BeginAnimation(System.Windows.Media.GradientStop.ColorProperty, new ColorAnimation { To = System.Windows.Media.Color.FromArgb(0x11, 0xFF, 0x33, 0x77), Duration = TimeSpan.FromSeconds(2), EasingFunction = ease });

        Glow3ColorStop.BeginAnimation(System.Windows.Media.GradientStop.ColorProperty, new ColorAnimation { To = System.Windows.Media.Color.FromArgb(0x33, 0xCC, 0x00, 0x44), Duration = TimeSpan.FromSeconds(2), EasingFunction = ease });
        Glow3MidStop.BeginAnimation(System.Windows.Media.GradientStop.ColorProperty, new ColorAnimation { To = System.Windows.Media.Color.FromArgb(0x08, 0xCC, 0x00, 0x44), Duration = TimeSpan.FromSeconds(2), EasingFunction = ease });
    }

    private System.Windows.Media.Color GetDominantColor(System.Windows.Media.Imaging.BitmapSource bitmap)
    {
        try
        {
            if (bitmap.PixelWidth == 0 || bitmap.PixelHeight == 0) return System.Windows.Media.Color.FromRgb(255, 0, 85);

            // Scale down to 32x32 to read pixels quickly and force proper averaging
            var renderTarget = new System.Windows.Media.Imaging.RenderTargetBitmap(32, 32, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
            var drawingVisual = new System.Windows.Media.DrawingVisual();
            using (var drawingContext = drawingVisual.RenderOpen())
            {
                drawingContext.DrawImage(bitmap, new Rect(0, 0, 32, 32));
            }
            renderTarget.Render(drawingVisual);

            byte[] pixels = new byte[32 * 32 * 4];
            renderTarget.CopyPixels(pixels, 32 * 4, 0);

            long r = 0, g = 0, b = 0;
            long count = 0;

            // To find an "accent" color, we weight pixels by their saturation
            for (int i = 0; i < pixels.Length; i += 4)
            {
                byte pb = pixels[i];
                byte pg = pixels[i + 1];
                byte pr = pixels[i + 2];
                // pixels[i+3] is Alpha, but we assume opaque for album art

                // Simple saturation estimation: difference between max and min channel
                int max = Math.Max(pr, Math.Max(pg, pb));
                int min = Math.Min(pr, Math.Min(pg, pb));
                int sat = max - min;

                // Ignore very dark, very bright, or grayscale pixels
                if (max < 30 || min > 225 || sat < 20) continue;

                // Weight by saturation to strongly favor vibrant colors
                int weight = sat;
                r += pr * weight;
                g += pg * weight;
                b += pb * weight;
                count += weight;
            }

            if (count > 0)
            {
                return System.Windows.Media.Color.FromRgb((byte)(r / count), (byte)(g / count), (byte)(b / count));
            }

            // Fallback: simple average if no vibrant pixels found
            r = g = b = count = 0;
            for (int i = 0; i < pixels.Length; i += 4)
            {
                r += pixels[i + 2];
                g += pixels[i + 1];
                b += pixels[i];
                count++;
            }
            
            if (count > 0)
                return System.Windows.Media.Color.FromRgb((byte)(r / count), (byte)(g / count), (byte)(b / count));

            return System.Windows.Media.Color.FromRgb(255, 0, 85);
        }
        catch
        {
            return System.Windows.Media.Color.FromRgb(255, 0, 85);
        }
    }

    private void StartGlowAnimation()
    {
        AnimateBlob(Glow1Transform, Glow1Scale, Glow1Rotate);
        AnimateBlob(Glow2Transform, Glow2Scale, Glow2Rotate);
        AnimateBlob(Glow3Transform, Glow3Scale, Glow3Rotate);
    }

    private void AnimateBlob(System.Windows.Media.TranslateTransform translate, System.Windows.Media.ScaleTransform scale, System.Windows.Media.RotateTransform rotate)
    {
        double targetX = _glowRandom.Next(-200, 1920);
        double targetY = _glowRandom.Next(-200, 1080);
        
        // Asymmetrical scaling creates the morphing effect
        double scaleX = 0.8 + (_glowRandom.NextDouble() * 1.5);
        double scaleY = 0.8 + (_glowRandom.NextDouble() * 1.5);
        
        // Continuous rotation over the duration adds to the liquid look
        double targetAngle = rotate.Angle + _glowRandom.Next(90, 270);
        
        double durationSeconds = 15 + (_glowRandom.NextDouble() * 20);
        var ease = new SineEase { EasingMode = EasingMode.EaseInOut };

        var animX = new DoubleAnimation { To = targetX, Duration = TimeSpan.FromSeconds(durationSeconds), EasingFunction = ease };
        var animY = new DoubleAnimation { To = targetY, Duration = TimeSpan.FromSeconds(durationSeconds), EasingFunction = ease };
        var animScaleX = new DoubleAnimation { To = scaleX, Duration = TimeSpan.FromSeconds(durationSeconds), EasingFunction = ease };
        var animScaleY = new DoubleAnimation { To = scaleY, Duration = TimeSpan.FromSeconds(durationSeconds), EasingFunction = ease };
        var animRotate = new DoubleAnimation { To = targetAngle, Duration = TimeSpan.FromSeconds(durationSeconds), EasingFunction = ease };

        animX.Completed += (s, e) => AnimateBlob(translate, scale, rotate);

        translate.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, animX);
        translate.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, animY);
        scale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, animScaleX);
        scale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, animScaleY);
        rotate.BeginAnimation(System.Windows.Media.RotateTransform.AngleProperty, animRotate);
    }
}