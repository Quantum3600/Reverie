using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Reverie.Controls;

public partial class MarqueeControl : UserControl
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        "Text", typeof(string), typeof(MarqueeControl), new PropertyMetadata(string.Empty, OnTextChanged));

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public static readonly DependencyProperty SpeedProperty = DependencyProperty.Register(
        "Speed", typeof(double), typeof(MarqueeControl), new PropertyMetadata(40.0, OnMarqueePropertyChanged));

    public double Speed
    {
        get => (double)GetValue(SpeedProperty);
        set => SetValue(SpeedProperty, value);
    }

    private Storyboard? _storyboard;

    public MarqueeControl()
    {
        InitializeComponent();
        this.Loaded += (s, e) => UpdateMarquee();
        this.SizeChanged += (s, e) => UpdateMarquee();
    }

    private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MarqueeControl marquee)
        {
            marquee.UpdateMarquee();
        }
    }

    private static void OnMarqueePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MarqueeControl marquee)
        {
            marquee.UpdateMarquee();
        }
    }

    private void UpdateMarquee()
    {
        if (!IsLoaded) return;

        // Reset
        StopMarquee();
        
        // Measure text precisely
        double textWidth = MeasureTextWidth(Text, FontSize, FontFamily, FontWeight, FontStyle);
        double containerWidth = ActualWidth;

        // Ensure we only marquee if text is larger than container
        if (textWidth > containerWidth + 2 && containerWidth > 0)
        {
            StartMarquee(textWidth, containerWidth);
        }
        else
        {
            Canvas.SetLeft(MarqueeText, 0);
            MarqueeText2.Visibility = Visibility.Collapsed;
        }
    }

    private double MeasureTextWidth(string text, double fontSize, FontFamily fontFamily, FontWeight fontWeight, FontStyle fontStyle)
    {
        if (string.IsNullOrEmpty(text)) return 0;

        var formattedText = new FormattedText(
            text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface(fontFamily, fontStyle, fontWeight, FontStretches.Normal),
            fontSize,
            Brushes.Black,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);

        return formattedText.Width;
    }

    private void StartMarquee(double textWidth, double containerWidth)
    {
        MarqueeText2.Visibility = Visibility.Visible;
        
        double gap = 100.0;
        double totalWidth = textWidth + gap;
        
        double durationSeconds = totalWidth / Speed;
        double pauseSeconds = 2.0;

        _storyboard = new Storyboard();

        // Animation for the first text block
        DoubleAnimationUsingKeyFrames anim1 = new DoubleAnimationUsingKeyFrames();
        anim1.KeyFrames.Add(new DiscreteDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        anim1.KeyFrames.Add(new DiscreteDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(pauseSeconds))));
        anim1.KeyFrames.Add(new LinearDoubleKeyFrame(-totalWidth, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(pauseSeconds + durationSeconds))));

        // Animation for the second text block
        DoubleAnimationUsingKeyFrames anim2 = new DoubleAnimationUsingKeyFrames();
        anim2.KeyFrames.Add(new DiscreteDoubleKeyFrame(totalWidth, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        anim2.KeyFrames.Add(new DiscreteDoubleKeyFrame(totalWidth, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(pauseSeconds))));
        anim2.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(pauseSeconds + durationSeconds))));

        Storyboard.SetTarget(anim1, MarqueeText);
        Storyboard.SetTargetProperty(anim1, new PropertyPath(Canvas.LeftProperty));

        Storyboard.SetTarget(anim2, MarqueeText2);
        Storyboard.SetTargetProperty(anim2, new PropertyPath(Canvas.LeftProperty));

        _storyboard.Children.Add(anim1);
        _storyboard.Children.Add(anim2);
        _storyboard.RepeatBehavior = RepeatBehavior.Forever;
        
        Timeline.SetDesiredFrameRate(_storyboard, 60);
        
        _storyboard.Begin();
    }

    private void StopMarquee()
    {
        if (_storyboard != null)
        {
            _storyboard.Stop();
            _storyboard = null;
        }
        
        MarqueeText.BeginAnimation(Canvas.LeftProperty, null);
        MarqueeText2.BeginAnimation(Canvas.LeftProperty, null);
        Canvas.SetLeft(MarqueeText, 0);
        Canvas.SetLeft(MarqueeText2, 0);
        MarqueeText2.Visibility = Visibility.Collapsed;
    }
}
