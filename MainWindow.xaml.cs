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
            TrackText.Text = trackInfo.Title;
            ArtistText.Text = trackInfo.Artist;

            if (trackInfo.Title != _currentTrackTitle || trackInfo.Artist != _currentArtist)
            {
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
                        dominantColor.A = 0x33;
                        ColorAnimation colorAnim = new ColorAnimation
                        {
                            To = dominantColor,
                            Duration = TimeSpan.FromSeconds(2),
                            EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseOut }
                        };
                        GlowColorStop.BeginAnimation(System.Windows.Media.GradientStop.ColorProperty, colorAnim);
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
        var defaultColor = System.Windows.Media.Color.FromArgb(0x33, 0xFF, 0x00, 0x55);
        ColorAnimation defaultAnim = new ColorAnimation
        {
            To = defaultColor,
            Duration = TimeSpan.FromSeconds(2),
            EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseOut }
        };
        GlowColorStop.BeginAnimation(System.Windows.Media.GradientStop.ColorProperty, defaultAnim);
    }

    private System.Windows.Media.Color GetDominantColor(System.Windows.Media.Imaging.BitmapSource bitmap)
    {
        try
        {
            if (bitmap.PixelWidth == 0 || bitmap.PixelHeight == 0) return System.Windows.Media.Color.FromRgb(255, 0, 85);
            var resized = new System.Windows.Media.Imaging.TransformedBitmap(bitmap, new System.Windows.Media.ScaleTransform(1.0 / bitmap.PixelWidth, 1.0 / bitmap.PixelHeight));
            var formatConverted = new System.Windows.Media.Imaging.FormatConvertedBitmap(resized, System.Windows.Media.PixelFormats.Bgra32, null, 0);
            byte[] pixels = new byte[4];
            formatConverted.CopyPixels(pixels, 4, 0);
            return System.Windows.Media.Color.FromRgb(pixels[2], pixels[1], pixels[0]);
        }
        catch
        {
            return System.Windows.Media.Color.FromRgb(255, 0, 85);
        }
    }

    private void StartGlowAnimation()
    {
        // Screen usually 1920x1080. The element is 3000x3000 at margin -1500,-1500. 
        // X = 0 puts its center at 0,0.
        // We want it to drift freely around the screen. Center can be anywhere from 0 to 1920 in X, 0 to 1080 in Y.
        double targetX = _glowRandom.Next(0, 1920);
        double targetY = _glowRandom.Next(0, 1080);

        // Deform the blob to make shape dynamic
        double scaleX = 0.7 + (_glowRandom.NextDouble() * 1.5); // 0.7 to 2.2
        double scaleY = 0.7 + (_glowRandom.NextDouble() * 1.5); // 0.7 to 2.2

        // Duration for this morph/move
        double durationSeconds = 15 + (_glowRandom.NextDouble() * 20); // 15 to 35 seconds

        var ease = new SineEase { EasingMode = EasingMode.EaseInOut };

        var animX = new DoubleAnimation { To = targetX, Duration = TimeSpan.FromSeconds(durationSeconds), EasingFunction = ease };
        var animY = new DoubleAnimation { To = targetY, Duration = TimeSpan.FromSeconds(durationSeconds), EasingFunction = ease };
        var animScaleX = new DoubleAnimation { To = scaleX, Duration = TimeSpan.FromSeconds(durationSeconds), EasingFunction = ease };
        var animScaleY = new DoubleAnimation { To = scaleY, Duration = TimeSpan.FromSeconds(durationSeconds), EasingFunction = ease };

        // When complete, loop by picking new targets
        animX.Completed += (s, e) => StartGlowAnimation();

        GlowTransform.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, animX);
        GlowTransform.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, animY);
        GlowScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, animScaleX);
        GlowScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, animScaleY);
    }
}