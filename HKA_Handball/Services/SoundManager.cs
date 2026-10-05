using Plugin.Maui.Audio;
using Microsoft.Extensions.Logging;

namespace HKA_Handball.Services;

/// <summary>
/// Manages game sound effects using Plugin.Maui.Audio.
/// Preloads short audio clips and exposes fire-and-forget Play methods.
/// </summary>
public sealed class SoundManager : IDisposable
{
    static readonly string[] SoundNames = ["whistle", "goal", "shoot", "pass", "crowd", "click"];
    readonly IAudioManager _audioManager;
    readonly ILogger<SoundManager> _logger;
    readonly Dictionary<string, IAudioPlayer> _players = new();
    readonly Dictionary<string, Stream> _streams = new();
    readonly SemaphoreSlim _preloadLock = new(1, 1);
    bool _enabled = true;
    bool _preloaded;
    bool _disposed;

    /// <summary>Whether sound effects are enabled.</summary>
    public bool Enabled
    {
        get => _enabled;
        set
        {
            _enabled = value;
            if (!value)
                foreach (var (name, player) in _players)
                    try
                    {
                        player.Stop();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to stop sound {SoundName}", name);
                    }
        }
    }

    public SoundManager(IAudioManager audioManager, ILogger<SoundManager> logger)
    {
        _audioManager = audioManager;
        _logger = logger;
    }

    /// <summary>
    /// Preloads all game sound effects from the Raw/Sounds folder.
    /// Call once during app startup.
    /// </summary>
    public async Task PreloadAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _preloadLock.WaitAsync();
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_preloaded) return;

            foreach (var name in SoundNames)
            {
                if (_players.ContainsKey(name)) continue;
                Stream? stream = null;
                try
                {
                    stream = await FileSystem.OpenAppPackageFileAsync($"Sounds/{name}.wav");
                    if (_disposed) return;
                    var player = _audioManager.CreatePlayer(stream);
                    _players.Add(name, player);
                    // Keep the input alive: some platform players consume it lazily.
                    _streams.Add(name, stream);
                    stream = null;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to load packaged sound Sounds/{SoundName}.wav", name);
                }
                finally
                {
                    stream?.Dispose();
                }
            }
            // A failed clip remains retryable on the next preload call.
            _preloaded = _players.Count == SoundNames.Length;
        }
        finally
        {
            _preloadLock.Release();
        }
    }

    /// <summary>
    /// Play a named sound effect (fire-and-forget).
    /// </summary>
    public void Play(string name, double volume = 1.0)
    {
        if (!_enabled || _disposed) return;
        if (!_players.TryGetValue(name, out var player))
        {
            _logger.LogWarning("Sound {SoundName} is not loaded", name);
            return;
        }
        try
        {
            // Seek to start if still playing a previous instance
            if (player.IsPlaying)
                player.Stop();
            player.Seek(0);
            var clampedVolume = double.IsFinite(volume) ? Math.Clamp(volume, 0.0, 1.0) : 0.0;
            player.Volume = clampedVolume;
            player.Play();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to play sound {SoundName}", name);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var (name, player) in _players)
            try
            {
                player.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to dispose sound {SoundName}", name);
            }
        _players.Clear();
        foreach (var (name, stream) in _streams)
            try
            {
                stream.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to dispose input stream for sound {SoundName}", name);
            }
        _streams.Clear();
    }

    public void PlayGoal() => Play("goal");
    public void PlayShoot() => Play("shoot");
    public void PlayPass() => Play("pass");
    public void PlayWhistle() => Play("whistle", volume: 0.35);
    public void PlayCrowd() => Play("crowd");
    public void PlayClick() => Play("click");
}
