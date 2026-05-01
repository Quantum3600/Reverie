using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Media.Animation;
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
                    }
                    catch
                    {
                        AlbumArtImage.Source = null;
                        AlbumArtImage.Visibility = Visibility.Collapsed;
                    }
                }
                else
                {
                    AlbumArtImage.Source = null;
                    AlbumArtImage.Visibility = Visibility.Collapsed;
                }
                
                if (!string.IsNullOrWhiteSpace(_currentTrackTitle) && _currentTrackTitle != "No Track")
                {
                    _isFetchingLyrics = true;
                    var result = await _lyricsService.GetLyricsAsync(_currentTrackTitle, _currentArtist);
                    _isFetchingLyrics = false;
                    _lastFetchSuccessful = result.IsSuccessful;
                    
                    foreach (var line in result.Lines)
                    {
                        _lyricLines.Add(new LyricLineViewModel { StartTime = line.StartTime, Text = line.Text });
                    }
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
                var position = actualPosition.Add(TimeSpan.FromMilliseconds(500));
                
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
            bool showLoading = false;

            if (rawPosition != null && !string.IsNullOrWhiteSpace(_currentTrackTitle) && _currentTrackTitle != "No Track")
            {
                if (_isFetchingLyrics)
                {
                    showLoading = true;
                }
            }

            LyricsScrollViewer.Visibility = showLyrics ? Visibility.Visible : Visibility.Collapsed;
            NoTextIndicator.Visibility = showLoading ? Visibility.Visible : Visibility.Collapsed;
            
            // Show orb ONLY if:
            // 1. Not currently fetching
            // 2. We don't have lyrics (either fetch failed or no lyrics found)
            // 3. We ARE in the middle of a track (not an intro where we have lyrics waiting)
            // 4. OR if we are offline
            IdleOrb.Visibility = (!_isNetworkAvailable || (!hasLyrics && !showLoading && (!_lastFetchSuccessful || _lyricLines.Count == 0))) 
                                ? Visibility.Visible : Visibility.Collapsed;
            
            // If we have lyrics but are in the intro, show the scroll viewer (it will show upcoming blurred lyrics)
            // Only show if network is available, otherwise the orb takes priority
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
}