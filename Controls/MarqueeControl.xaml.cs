using System;
using System.Windows;
using System.Windows.Controls;
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

    private void UpdateMarquee()
    {
        if (!IsLoaded) return;

        // Reset
        MarqueeText.BeginAnimation(Canvas.LeftProperty, null);
        Canvas.SetLeft(MarqueeText, 0);

        // Measure text
        MarqueeText.UpdateLayout();
        double textWidth = MarqueeText.ActualWidth;
        double containerWidth = MainGrid.ActualWidth;

        if (textWidth > containerWidth + 5 && containerWidth > 0)
        {
            // Calculate duration based on width (speed)
            double scrollDistance = textWidth - containerWidth + 50; // Increased padding at end
            double durationSeconds = scrollDistance / 40.0; // Slightly faster for responsiveness

            Storyboard storyboard = new Storyboard();

            DoubleAnimation scrollAnim = new DoubleAnimation
            {
                From = 0,
                To = -scrollDistance,
                Duration = TimeSpan.FromSeconds(durationSeconds),
                BeginTime = TimeSpan.FromSeconds(2) // Initial pause
            };

            Storyboard.SetTarget(scrollAnim, MarqueeText);
            Storyboard.SetTargetProperty(scrollAnim, new PropertyPath(Canvas.LeftProperty));

            storyboard.Children.Add(scrollAnim);
            storyboard.RepeatBehavior = RepeatBehavior.Forever;
            
            // Add a pause at the end by adding a dummy animation or using a KeyFrame animation
            // Using ObjectAnimationUsingKeyFrames for a simple wait at the end is overkill, 
            // let's just use a longer duration in the storyboard if possible, or just accept the loop.
            // Actually, we can use a DoubleAnimationUsingKeyFrames.
            
            DoubleAnimationUsingKeyFrames keyFrames = new DoubleAnimationUsingKeyFrames();
            keyFrames.KeyFrames.Add(new DiscreteDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            keyFrames.KeyFrames.Add(new DiscreteDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(2)))); // Pause at start
            keyFrames.KeyFrames.Add(new LinearDoubleKeyFrame(-scrollDistance, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(2 + durationSeconds)))); // Scroll
            keyFrames.KeyFrames.Add(new DiscreteDoubleKeyFrame(-scrollDistance, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(4 + durationSeconds)))); // Pause at end
            
            keyFrames.RepeatBehavior = RepeatBehavior.Forever;

            MarqueeText.BeginAnimation(Canvas.LeftProperty, keyFrames);
        }
    }
}
