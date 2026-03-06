using HKA_Handball.Services;

namespace HKA_Handball;

public partial class MainMenuPage : ContentPage
{
    readonly SoundManager _soundManager;
    CancellationTokenSource? _animationCts;
    bool _initialized;

    public MainMenuPage(SoundManager soundManager)
    {
        InitializeComponent();
        _soundManager = soundManager;

        Loaded += OnPageLoaded;
    }

    async void OnPageLoaded(object? sender, EventArgs e)
    {
        // Guard against multiple Loaded fires (hot reload, reattachment)
        if (_initialized)
            return;
        _initialized = true;
        Loaded -= OnPageLoaded;

        // Preload sounds
        await _soundManager.PreloadAsync();

        // Animate the ball icon with a gentle bounce
        _animationCts = new CancellationTokenSource();
        await AnimateBallAsync(_animationCts.Token);
    }

    async Task AnimateBallAsync(CancellationToken ct)
    {
        // Initial entrance animation
        BallFrame.Opacity = 0;
        BallFrame.Scale = 0.3;
        await Task.WhenAll(
            BallFrame.FadeToAsync(1, 400, Easing.CubicOut),
            BallFrame.ScaleToAsync(1, 500, Easing.SpringOut)
        );

        // Continuous gentle bounce
        while (!ct.IsCancellationRequested)
        {
            await BallFrame.TranslateToAsync(0, -8, 800, Easing.SinInOut);
            await BallFrame.TranslateToAsync(0, 0, 800, Easing.SinInOut);
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _animationCts?.Cancel();
    }

    async void OnSinglePlayer(object? sender, EventArgs e)
    {
        _soundManager.PlayClick();
        await Navigation.PushAsync(new GamePage(GameMode.SinglePlayer, _soundManager));
    }

    async void OnTwoPlayerLocal(object? sender, EventArgs e)
    {
        _soundManager.PlayClick();
        await Navigation.PushAsync(new GamePage(GameMode.TwoPlayerLocal, _soundManager));
    }

    void OnSoundToggled(object? sender, ToggledEventArgs e)
    {
        _soundManager.Enabled = e.Value;
    }
}
