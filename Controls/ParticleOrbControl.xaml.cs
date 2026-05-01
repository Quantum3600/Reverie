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
    }

    private AudioSpectrumService? _spectrumService;
    private List<Particle> _particles = new();
    private float[] _smoothedBands = new float[64];
    private double _angleX = 0;
    private double _angleY = 0;
    private bool _isAnimating = false;
    private const int ParticleCount = 800;
    private const double SphereRadius = 180;

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

            // Map particles to frequency bands (0-63)
            // We'll use a distribution where most particles are in the mid-bands
            int bandIndex = (int)(Math.Abs(yRel) * 63);

            var ellipse = new Ellipse
            {
                Width = 2,
                Height = 2,
                Fill = new SolidColorBrush(Color.FromArgb(255, 150, 220, 255)),
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Color.FromArgb(255, 100, 180, 255),
                    BlurRadius = 10,
                    ShadowDepth = 0
                }
            };

            OrbCanvas.Children.Add(ellipse);

            _particles.Add(new Particle
            {
                Shape = ellipse,
                Theta = theta,
                YRel = yRel,
                BaseRadius = 2,
                BandIndex = bandIndex
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
        
        // Smooth the bands for the orb
        for (int i = 0; i < 64; i++)
        {
            if (spectrum[i] > _smoothedBands[i])
                _smoothedBands[i] = spectrum[i];
            else
                _smoothedBands[i] *= 0.92f;
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
            double displacement = bandVal * 150; // Max displacement
            
            // Pulse effect based on overall energy
            double pulse = _smoothedBands.Average() * 50;
            
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
            
            Panel.SetZIndex(p.Shape, (int)z2);

            Canvas.SetLeft(p.Shape, centerX + x1 - (p.BaseRadius * scale) / 2);
            Canvas.SetTop(p.Shape, centerY + y1 - (p.BaseRadius * scale) / 2);

            p.Shape.Width = p.BaseRadius * scale;
            p.Shape.Height = p.BaseRadius * scale;
            
            double opacity = (z2 + SphereRadius * 1.5) / (SphereRadius * 3);
            p.Shape.Opacity = Math.Clamp(opacity, 0.15, 1.0);
            
            // Color shift based on intensity
            if (bandVal > 0.5)
            {
                ((SolidColorBrush)p.Shape.Fill).Color = Color.FromRgb(200, 230, 255);
            }
            else
            {
                ((SolidColorBrush)p.Shape.Fill).Color = Color.FromRgb(150, 200, 255);
            }
        }
    }
}
