using System;
using System.Linq;
using NAudio.Wave;

namespace Reverie.Services;

public class AudioSpectrumService : IDisposable
{
    private WasapiLoopbackCapture? _capture;
    private readonly int _fftSize = 1024;
    private readonly float[] _fftBuffer;
    private readonly float[] _spectrumData;
    private readonly float[] _previousSpectrum;
    private readonly object _lock = new();
    
    public int BandCount { get; } = 64;
    public float[] Spectrum => _spectrumData;

    public AudioSpectrumService()
    {
        _fftBuffer = new float[_fftSize];
        _spectrumData = new float[BandCount];
        _previousSpectrum = new float[BandCount];
        
        try
        {
            _capture = new WasapiLoopbackCapture();
            _capture.DataAvailable += OnDataAvailable;
            _capture.StartRecording();
        }
        catch
        {
            // Fallback if no audio device is available
        }
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (e.BytesRecorded == 0) return;

        lock (_lock)
        {
            // Convert bytes to floats (assuming 32-bit float IEEE format, which is standard for WASAPI Loopback)
            int sampleCount = e.BytesRecorded / 4;
            if (sampleCount < _fftSize) return;

            // Take the most recent samples
            int offset = e.BytesRecorded - (_fftSize * 4);
            for (int i = 0; i < _fftSize; i++)
            {
                _fftBuffer[i] = BitConverter.ToSingle(e.Buffer, offset + (i * 4));
            }

            // Apply Hann Window
            for (int i = 0; i < _fftSize; i++)
            {
                float window = 0.5f * (1f - (float)Math.Cos(2 * Math.PI * i / (_fftSize - 1)));
                _fftBuffer[i] *= window;
            }

            // Simple FFT
            var fftResult = CalculateFFT(_fftBuffer);

            // Group into 64 bands with logarithmic scaling
            ProcessBands(fftResult);
        }
    }

    private Complex[] CalculateFFT(float[] samples)
    {
        int n = samples.Length;
        Complex[] data = new Complex[n];
        for (int i = 0; i < n; i++)
            data[i] = new Complex(samples[i], 0);

        FFTRecursive(data);
        return data;
    }

    private void FFTRecursive(Complex[] data)
    {
        int n = data.Length;
        if (n <= 1) return;

        Complex[] even = new Complex[n / 2];
        Complex[] odd = new Complex[n / 2];
        for (int i = 0; i < n / 2; i++)
        {
            even[i] = data[2 * i];
            odd[i] = data[2 * i + 1];
        }

        FFTRecursive(even);
        FFTRecursive(odd);

        for (int k = 0; k < n / 2; k++)
        {
            double angle = -2 * Math.PI * k / n;
            Complex t = new Complex(Math.Cos(angle), Math.Sin(angle)) * odd[k];
            data[k] = even[k] + t;
            data[k + n / 2] = even[k] - t;
        }
    }

    private void ProcessBands(Complex[] fft)
    {
        // Only take the first half (Nyquist limit)
        int half = fft.Length / 2;
        
        for (int i = 0; i < BandCount; i++)
        {
            // Logarithmic mapping of bands
            int start = (int)Math.Pow(2, (double)i / BandCount * Math.Log2(half));
            int end = (int)Math.Pow(2, (double)(i + 1) / BandCount * Math.Log2(half));
            if (end <= start) end = start + 1;
            if (end > half) end = half;

            float sum = 0;
            for (int j = start; j < end; j++)
            {
                sum += (float)fft[j].Magnitude;
            }
            float avg = sum / (end - start);
            
            // Apply gain and scaling
            float val = avg * 45.0f; 
            if (val > 1.0f) val = 1.0f;
            if (val < 0.01f) val = 0;

            // Smoothing (Linear interpolation with previous frame)
            _spectrumData[i] = _previousSpectrum[i] * 0.6f + val * 0.4f;
            _previousSpectrum[i] = _spectrumData[i];
        }
    }

    public void Dispose()
    {
        _capture?.StopRecording();
        _capture?.Dispose();
    }

    private struct Complex
    {
        public double Real;
        public double Imaginary;
        public Complex(double real, double imaginary) { Real = real; Imaginary = imaginary; }
        public double Magnitude => Math.Sqrt(Real * Real + Imaginary * Imaginary);
        public static Complex operator +(Complex a, Complex b) => new Complex(a.Real + b.Real, a.Imaginary + b.Imaginary);
        public static Complex operator -(Complex a, Complex b) => new Complex(a.Real - b.Real, a.Imaginary - b.Imaginary);
        public static Complex operator *(Complex a, Complex b) => new Complex(a.Real * b.Real - a.Imaginary * b.Imaginary, a.Real * b.Imaginary + a.Imaginary * b.Real);
    }
}
