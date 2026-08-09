using HKA_Handball.Services;

namespace HKA_Handball;

public partial class MainMenuPage : ContentPage
{
    readonly SoundManager _soundManager;
    CancellationTokenSource? _animationCts;
    bool _initialized;
    Difficulty _selectedDifficulty = Difficulty.Medium;

    // Available team color presets
    static readonly TeamColorOption[] ColorPresets =
    [
        new("Blå",    "#003DA5", "#2E7CF6"),
        new("Röd",    "#DC143C", "#FF5555"),
        new("Grön",   "#1B5E20", "#43A047"),
        new("Gul",    "#F9A825", "#FDD835"),
        new("Lila",   "#6A1B9A", "#AB47BC"),
        new("Orange", "#E65100", "#FF8C00"),
        new("Svart",  "#212121", "#616161"),
        new("Vit",    "#CFD8DC", "#FFFFFF"),
    ];

    int _selectedHomeColorIndex = 0; // default: Blue
    int _selectedAwayColorIndex = 1; // default: Red

    public MainMenuPage(SoundManager soundManager)
    {
        InitializeComponent();
        _soundManager = soundManager;

        VersionLabel.Text = $"v{AppInfo.Current.VersionString}  •  HK Aranäs  •  Open Source";

        Loaded += OnPageLoaded;
        UpdateDifficultyButtons();
        BuildColorSwatches();
    }

    async void OnPageLoaded(object? sender, EventArgs e)
    {
        // Guard against multiple Loaded fires (hot reload, reattachment)
        if (_initialized)
            return;
        _initialized = true;
        Loaded -= OnPageLoaded;

        // Run preload and entrance animation concurrently
        _animationCts = new CancellationTokenSource();
        var preloadTask = _soundManager.PreloadAsync();
        var animateTask = AnimateBallAsync(_animationCts.Token);
        await Task.WhenAll(preloadTask, animateTask);
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
        _animationCts?.Dispose();
        _animationCts = null;
    }

    void OnSelectEasy(object? sender, EventArgs e)
    {
        _selectedDifficulty = Difficulty.Easy;
        _soundManager.PlayClick();
        UpdateDifficultyButtons();
    }

    void OnSelectMedium(object? sender, EventArgs e)
    {
        _selectedDifficulty = Difficulty.Medium;
        _soundManager.PlayClick();
        UpdateDifficultyButtons();
    }

    void OnSelectHard(object? sender, EventArgs e)
    {
        _selectedDifficulty = Difficulty.Hard;
        _soundManager.PlayClick();
        UpdateDifficultyButtons();
    }

    void UpdateDifficultyButtons()
    {
        UpdateDifficultyButton(EasyButton, _selectedDifficulty == Difficulty.Easy, "#2E7D32");
        UpdateDifficultyButton(MediumButton, _selectedDifficulty == Difficulty.Medium, "#003DA5");
        UpdateDifficultyButton(HardButton, _selectedDifficulty == Difficulty.Hard, "#B71C1C");
    }

    static void UpdateDifficultyButton(Button button, bool selected, string selectedBackground)
    {
        const string unselectedBackground = "#253038";
        const string selectedBorder = "#EFFFFFFF";
        const string unselectedBorder = "#75FFFFFF";

        button.BackgroundColor = Color.FromArgb(selected ? selectedBackground : unselectedBackground);
        button.BorderColor = Color.FromArgb(selected ? selectedBorder : unselectedBorder);
        button.BorderWidth = selected ? 3 : 1;
        button.Opacity = selected ? 1.0 : 0.78;
        button.Scale = selected ? 1.0 : 0.96;
    }

    async void OnSinglePlayer(object? sender, EventArgs e)
    {
        _soundManager.PlayClick();
        await Navigation.PushAsync(new GamePage(GameMode.SinglePlayer, _selectedDifficulty, _soundManager,
            ColorPresets[_selectedHomeColorIndex], ColorPresets[_selectedAwayColorIndex]));
    }

    async void OnTwoPlayerLocal(object? sender, EventArgs e)
    {
        _soundManager.PlayClick();
        await Navigation.PushAsync(new GamePage(GameMode.TwoPlayerLocal, _selectedDifficulty, _soundManager,
            ColorPresets[_selectedHomeColorIndex], ColorPresets[_selectedAwayColorIndex]));
    }

    void OnSoundToggled(object? sender, ToggledEventArgs e)
    {
        _soundManager.Enabled = e.Value;
    }

    void BuildColorSwatches()
    {
        HomeColorStack.Children.Clear();
        AwayColorStack.Children.Clear();
        for (int i = 0; i < ColorPresets.Length; i++)
        {
            HomeColorStack.Children.Add(CreateSwatch(i, isHome: true));
            AwayColorStack.Children.Add(CreateSwatch(i, isHome: false));
        }
        UpdateColorSwatches();
    }

    Border CreateSwatch(int index, bool isHome)
    {
        var swatch = new Border
        {
            WidthRequest = 30,
            HeightRequest = 30,
            BackgroundColor = Color.FromArgb(ColorPresets[index].Primary),
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
            Stroke = Colors.Transparent,
            StrokeThickness = 1.5f,
        };
        var tap = new TapGestureRecognizer();
        int capturedIndex = index;
        bool capturedIsHome = isHome;
        tap.Tapped += (_, _) =>
        {
            _soundManager.PlayClick();
            if (capturedIsHome)
                _selectedHomeColorIndex = capturedIndex;
            else
                _selectedAwayColorIndex = capturedIndex;
            UpdateColorSwatches();
        };
        swatch.GestureRecognizers.Add(tap);
        return swatch;
    }

    void UpdateColorSwatches()
    {
        for (int i = 0; i < HomeColorStack.Children.Count; i++)
        {
            if (HomeColorStack.Children[i] is Border b)
            {
                var selected = i == _selectedHomeColorIndex;
                b.Stroke = selected ? Colors.White : Color.FromArgb("#55FFFFFF");
                b.StrokeThickness = selected ? 2.5f : 1.0f;
                b.Scale = selected ? 1.08 : 1.0;
                // Dim already-picked color by blending with dark background instead of Opacity
                // (Opacity changes trigger layout recalculation causing swatches to jump)
                var baseColor = Color.FromArgb(ColorPresets[i].Primary);
                b.BackgroundColor = i == _selectedAwayColorIndex
                    ? baseColor.WithAlpha(0.3f)
                    : baseColor;
            }
        }
        for (int i = 0; i < AwayColorStack.Children.Count; i++)
        {
            if (AwayColorStack.Children[i] is Border b)
            {
                var selected = i == _selectedAwayColorIndex;
                b.Stroke = selected ? Colors.White : Color.FromArgb("#55FFFFFF");
                b.StrokeThickness = selected ? 2.5f : 1.0f;
                b.Scale = selected ? 1.08 : 1.0;
                var baseColor = Color.FromArgb(ColorPresets[i].Primary);
                b.BackgroundColor = i == _selectedHomeColorIndex
                    ? baseColor.WithAlpha(0.3f)
                    : baseColor;
            }
        }
    }
}
