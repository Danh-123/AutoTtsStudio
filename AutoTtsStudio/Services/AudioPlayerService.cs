using NAudio.Wave;

namespace AutoTtsStudio.Services;

/// <summary>
/// Direct audio playback service using NAudio (WaveOutEvent + AudioFileReader).
/// Supports both .mp3 and .wav formats with zero file locking.
/// </summary>
public sealed class AudioPlayerService : IDisposable
{
    private WaveOutEvent? _waveOut;
    private AudioFileReader? _reader;
    private readonly object _lock = new();

    public bool IsPlaying
    {
        get
        {
            lock (_lock)
            {
                return _waveOut != null && _waveOut.PlaybackState == PlaybackState.Playing;
            }
        }
    }

    public string? CurrentPlayingPath { get; private set; }

    public event EventHandler? PlaybackStarted;
    public event EventHandler? PlaybackStopped;

    /// <summary>
    /// Plays the specified audio file. If the same file is already playing, stops it (toggle).
    /// If another file was playing, stops and disposes it first, then plays the new file.
    /// </summary>
    public void PlayOrToggle(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            StopAndDispose();
            return;
        }

        lock (_lock)
        {
            // If already playing this exact file -> toggle off
            if (IsPlaying && string.Equals(CurrentPlayingPath, filePath, StringComparison.OrdinalIgnoreCase))
            {
                StopAndDisposeInternal();
                return;
            }

            // Otherwise, cleanly stop any previous playback
            StopAndDisposeInternal();

            try
            {
                _reader = new AudioFileReader(filePath);
                _waveOut = new WaveOutEvent();
                _waveOut.PlaybackStopped += OnWaveOutPlaybackStopped;
                _waveOut.Init(_reader);
                _waveOut.Play();

                CurrentPlayingPath = filePath;
            }
            catch
            {
                StopAndDisposeInternal();
                throw;
            }
        }

        PlaybackStarted?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Immediately stops playback and fully disposes both WaveOutEvent and AudioFileReader
    /// to guarantee no file locks (IOException).
    /// </summary>
    public void StopAndDispose()
    {
        lock (_lock)
        {
            StopAndDisposeInternal();
        }
    }

    private void StopAndDisposeInternal()
    {
        CurrentPlayingPath = null;

        if (_waveOut != null)
        {
            try
            {
                _waveOut.PlaybackStopped -= OnWaveOutPlaybackStopped;
                _waveOut.Stop();
            }
            catch { }
            finally
            {
                _waveOut.Dispose();
                _waveOut = null;
            }
        }

        if (_reader != null)
        {
            try
            {
                _reader.Dispose();
            }
            catch { }
            finally
            {
                _reader = null;
            }
        }

        PlaybackStopped?.Invoke(this, EventArgs.Empty);
    }

    private void OnWaveOutPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        lock (_lock)
        {
            StopAndDisposeInternal();
        }
    }

    public void Dispose()
    {
        StopAndDispose();
    }
}
