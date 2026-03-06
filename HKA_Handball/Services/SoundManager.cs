using Plugin.Maui.Audio;

namespace HKA_Handball.Services;

/// <summary>
/// Manages game sound effects using Plugin.Maui.Audio.
/// Preloads short audio clips and exposes fire-and-forget Play methods.
/// </summary>
public sealed class SoundManager
{
    readonly IAudioManager _audioManager;
    readonly Dictionary<string, IAudioPlayer> _players = new();
    bool _enabled = true;
    bool _preloaded;

    /// <summary>Whether sound effects are enabled.</summary>
    public bool Enabled
    {
        get => _enabled;
        set => _enabled = value;
    }

    public SoundManager(IAudioManager audioManager)
    {
        _audioManager = audioManager;
    }

    /// <summary>
    /// Preloads all game sound effects from the Raw/Sounds folder.
    /// Call once during app startup.
    /// </summary>
    public async Task PreloadAsync()
    {
        if (_preloaded) return;
        _preloaded = true;

        string[] sounds = ["whistle", "goal", "shoot", "pass", "crowd", "click"];
        foreach (var name in sounds)
        {
            try
            {
                var stream = await FileSystem.OpenAppPackageFileAsync($"Sounds/{name}.wav");
                var player = _audioManager.CreatePlayer(stream);

                // Dispose any previously loaded player for this name
                if (_players.TryGetValue(name, out var old))
                    old.Dispose();

                _players[name] = player;
            }
            catch (FileNotFoundException)
            {
                // Sound file not bundled – continue without it
                System.Diagnostics.Debug.WriteLine($"[SoundManager] Sound file not found: Sounds/{name}.wav");
            }
            catch (Exception ex)
            {
                // Unsupported format or platform issue – continue without it
                System.Diagnostics.Debug.WriteLine($"[SoundManager] Failed to load Sounds/{name}.wav: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Play a named sound effect (fire-and-forget).
    /// </summary>
    public void Play(string name)
    {
        if (!_enabled) return;
        if (_players.TryGetValue(name, out var player))
        {
            // Seek to start if still playing a previous instance
            if (player.IsPlaying)
                player.Stop();
            player.Seek(0);
            player.Play();
        }
    }

    public void PlayGoal() => Play("goal");
    public void PlayShoot() => Play("shoot");
    public void PlayPass() => Play("pass");
    public void PlayWhistle() => Play("whistle");
    public void PlayCrowd() => Play("crowd");
    public void PlayClick() => Play("click");
}
