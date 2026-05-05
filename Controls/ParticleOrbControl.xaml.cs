using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Linq;
using Reverie.Services;

namespace Reverie.Controls;

public partial class ParticleOrbControl : UserControl
{
    private class Particle
    {
        public Ellipse Shape { get; set; } = null!;
        public double Theta { get; set; }
        public double YRel { get; set; }
        public double BaseRadius { get; set; }
        public int BandIndex { get; set; }
        public bool IsPrimarySpike { get; set; }
    }

    private AudioSpectrumService? _spectrumService;
    private List<Particle> _particles = new();
    private Color _accentColor = Color.FromRgb(150, 220, 255);

    public void SetAccentColor(Color color)
    {
        _accentColor = color;
    }
    private float[] _smoothedBands = new float[64];
    private double _angleX = 0;
    private double _angleY = 0;
    private bool _isAnimating = false;
    private const int ParticleCount = 800;
    private const double SphereRadius = 100; // Base core radius

    public ParticleOrbControl()
    {
        InitializeComponent();
        InitializeParticles();
    }

    public void SetService(AudioSpectrumService service)
    {
        _spectrumService = service;
    }

    private void InitializeParticles()
    {
        double goldenAngle = Math.PI * (3 - Math.Sqrt(5));

        for (int i = 0; i < ParticleCount; i++)
        {
            double yRel = 1 - (i / (double)(ParticleCount - 1)) * 2; // y goes from 1 to -1
            double radiusAtY = Math.Sqrt(1 - yRel * yRel);
            double theta = goldenAngle * i;

            // Distribute frequency bands with a skew towards the mid-range (clustering around index 32)
            double rawNormalized = (double)((i * 37) % 64) / 63.0;
            double skewed = 0.5 + Math.Sign(rawNormalized - 0.5) * Math.Pow(Math.Abs(rawNormalized - 0.5) * 2.0, 1.5) / 2.0;
            int bandIndex = (int)(skewed * 63);
            
            // Apply inversion to keep the requested direction
            //bandIndex = 63 - bandIndex;

            var ellipse = new Ellipse
            {
                Width = 2,
                Height = 2,
                Fill = new SolidColorBrush(_accentColor)
                // Removed heavy DropShadowEffect for massive FPS boost
            };

            OrbCanvas.Children.Add(ellipse);

            _particles.Add(new Particle
            {
                Shape = ellipse,
                Theta = theta,
                YRel = yRel,
                BaseRadius = 2,
                BandIndex = bandIndex,
                IsPrimarySpike = (i % 4 == 0) // Only 1 in 7 particles reacts fully to spikes
            });
        }
    }

    private void UserControl_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (Visibility == Visibility.Visible)
        {
            if (!_isAnimating)
            {
                CompositionTarget.Rendering += OnRendering;
                _isAnimating = true;
            }
        }
        else
        {
            if (_isAnimating)
            {
                CompositionTarget.Rendering -= OnRendering;
                _isAnimating = false;
            }
        }
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        _angleY += 0.008;
        _angleX += 0.004;

        float[] spectrum = _spectrumService?.Spectrum ?? new float[64];
        
        for (int i = 0; i < 64; i++)
        {
            // Boost higher frequencies so they can break the orb shape
            float freqBoost = 1.0f + (i / 16.0f); 
            float rawTarget = spectrum[i] * 2.0f * freqBoost;
            
            // Soft power curve isolates spikes gently
            float target = (float)Math.Pow(rawTarget, 1.5);
            
            // Frequency amplitude cap to keep the spikes within screen bounds
            target = Math.Min(target, 1.5f);

            if (target > _smoothedBands[i])
                _smoothedBands[i] += (target - _smoothedBands[i]) * 0.5f; // Responsive attack
            else
                _smoothedBands[i] *= 0.7f; // Slightly longer decay so spikes are visible
        }

        double cosY = Math.Cos(_angleY);
        double sinY = Math.Sin(_angleY);
        double cosX = Math.Cos(_angleX);
        double sinX = Math.Sin(_angleX);

        double centerX = OrbCanvas.Width / 2;
        double centerY = OrbCanvas.Height / 2;

        foreach (var p in _particles)
        {
            // Audio displacement (WAVE EFFECT)
            float bandVal = _smoothedBands[p.BandIndex];
            
            // All particles now react fully to their assigned frequency bands
            double displacement = bandVal * 80; 
            
            // Soft breathing effect for the whole orb
            double pulse = _smoothedBands.Average() * 10;
            
            double currentRadius = SphereRadius + displacement + pulse;
            double radiusAtY = Math.Sqrt(1 - p.YRel * p.YRel);

            double x0 = Math.Cos(p.Theta) * radiusAtY * currentRadius;
            double z0 = Math.Sin(p.Theta) * radiusAtY * currentRadius;
            double y0 = p.YRel * currentRadius;

            // Rotate around Y
            double x1 = x0 * cosY - z0 * sinY;
            double z1 = z0 * cosY + x0 * sinY;
            
            // Rotate around X
            double y1 = y0 * cosX - z1 * sinX;
            double z2 = z1 * cosX + y0 * sinX;

            // Perspective scale
            double scale = (SphereRadius * 1.5 + z2) / (SphereRadius * 3) * 1.5 + 0.5;
            scale = Math.Max(0.01, scale);
            
            Panel.SetZIndex(p.Shape, (int)z2);

            Canvas.SetLeft(p.Shape, centerX + x1 - (p.BaseRadius * scale) / 2);
            Canvas.SetTop(p.Shape, centerY + y1 - (p.BaseRadius * scale) / 2);

            p.Shape.Width = p.BaseRadius * scale;
            p.Shape.Height = p.BaseRadius * scale;
            
            double opacity = (z2 + SphereRadius * 1.5) / (SphereRadius * 3);
            p.Shape.Opacity = Math.Clamp(opacity, 0.15, 1.0);
            
            // Color shift based on intensity using the accent color
            if (bandVal > 0.5)
            {
                // Brighter variant of accent color for peaks
                ((SolidColorBrush)p.Shape.Fill).Color = Color.FromArgb(
                    255, 
                    (byte)Math.Min(255, _accentColor.R * 1.5), 
                    (byte)Math.Min(255, _accentColor.G * 1.5), 
                    (byte)Math.Min(255, _accentColor.B * 1.5));
            }
            else
            {
                ((SolidColorBrush)p.Shape.Fill).Color = _accentColor;
            }
        }
    }
}
