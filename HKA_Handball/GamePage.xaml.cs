using Microsoft.Maui;
using Microsoft.Maui.Graphics;
using HKA_Handball.Controls;
using HKA_Handball.Services;
using ControlsApplication = Microsoft.Maui.Controls.Application;
using G = Microsoft.Maui.Graphics;
#if WINDOWS
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Windows.System;
#endif

namespace HKA_Handball;

public partial class GamePage : ContentPage
{
    readonly GameState _state;
    readonly GameDrawable _drawable;
    readonly IDispatcherTimer _timer;
    readonly SoundManager? _soundManager;
    readonly GameMode _gameMode;
#if WINDOWS
    HashSet<VirtualKey> _keysDown = new();
    UIElement? _winKeyTarget;
#endif
    bool _advanceHeld;

    /// <summary>Parameterless constructor for XAML previewer. Sound effects are disabled.</summary>
    public GamePage() : this(GameMode.SinglePlayer, Difficulty.Medium, null, null, null) { }

    public GamePage(GameMode mode, Difficulty difficulty, SoundManager? soundManager,
        TeamColorOption? homeColors = null, TeamColorOption? awayColors = null)
    {
        _gameMode = mode;
        _soundManager = soundManager;
        _state = new GameState(mode, difficulty);

        InitializeComponent();
        _drawable = new GameDrawable(_state, homeColors, awayColors);
        GameView.Drawable = _drawable;

        _state.GameEvent += OnGameEvent;

        SizeChanged += (_, __) => _state.OnViewSizeChanged(new Size(Width, Height));

        Joystick.ValueChanged += (_, p) =>
        {
            const double maxSpeed = 110;
            _state.ActiveMoveInput = new Point(p.X * maxSpeed, p.Y * maxSpeed);
        };

        Joystick2.ValueChanged += (_, p) =>
        {
            const double maxSpeed = 110;
            _state.AwayActiveMoveInput = new Point(p.X * maxSpeed, p.Y * maxSpeed);
        };

        if (mode == GameMode.TwoPlayerLocal)
        {
            // In two-player mode, each player's controls must be on the same side:
            // Player 1 (home) on the left, Player 2 (away) on the right.
            Player1Buttons.HorizontalOptions = LayoutOptions.Start;
            Player1Buttons.Margin = new Thickness(164, 0, 0, 12);

            Player2Buttons.HorizontalOptions = LayoutOptions.End;
            Player2Buttons.Margin = new Thickness(0, 0, 164, 12);

            Joystick2.Margin = new Thickness(0, 0, 12, 12);
        }

#if WINDOWS
        Loaded += (_, __) =>
        {
            // Attach keyboard handlers to the Window content root so key events
            // are received regardless of which child control has focus.
            var nativeWindow = (ControlsApplication.Current?.Windows.FirstOrDefault()
                ?.Handler?.PlatformView as Microsoft.UI.Xaml.Window);
            if (nativeWindow?.Content is UIElement root)
            {
                _winKeyTarget = root;
                root.KeyDown += OnWinKeyDown;
                root.KeyUp += OnWinKeyUp;
            }
        };
#endif

        _timer = Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(16);
        _timer.Tick += OnTimerTick;
        _timer.Start();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (!_timer.IsRunning)
            _timer.Start();

        // Subscribe to window lifecycle to auto-pause when minimized
        if (Window is not null)
        {
            Window.Stopped += OnWindowStopped;
            Window.Resumed += OnWindowResumed;
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _timer.Stop();

        if (Window is not null)
        {
            Window.Stopped -= OnWindowStopped;
            Window.Resumed -= OnWindowResumed;
        }
#if WINDOWS
        if (_winKeyTarget is not null)
        {
            _winKeyTarget.KeyDown -= OnWinKeyDown;
            _winKeyTarget.KeyUp -= OnWinKeyUp;
            _winKeyTarget = null;
        }
        _keysDown.Clear();
#endif
    }

    void OnWindowStopped(object? sender, EventArgs e)
    {
        // Auto-pause when the app goes to background (minimized)
        if (!_state.IsMatchOver && !_state.IsHalfTime)
            _state.IsPaused = true;
    }

    void OnWindowResumed(object? sender, EventArgs e)
    {
        // Don't auto-resume — let the user tap to resume
    }

    void OnTimerTick(object? sender, EventArgs e)
    {
        if (_state.IsPaused)
        {
            // Hide all controls while paused
            PassUpButton.IsVisible = false;
            PassDownButton.IsVisible = false;
            ShootButton.IsVisible = false;
            SwitchDefenderButton.IsVisible = false;
            Joystick.IsVisible = false;
            if (_gameMode == GameMode.TwoPlayerLocal)
            {
                AwayPassUpButton.IsVisible = false;
                AwayPassDownButton.IsVisible = false;
                AwayShootButton.IsVisible = false;
                AwaySwitchDefenderButton.IsVisible = false;
                Joystick2.IsVisible = false;
                Player2Buttons.IsVisible = false;
            }
            StatusLabel.Text = "";
            GameView.Invalidate();
            return;
        }

        // Advance boost from joystick (X > 0.5) or keyboard (Space on Windows)
        var jv = Joystick.Value;
        if (jv.X > 0.5 || _advanceHeld)
            _state.AdvanceHeld();
        else
            _state.AdvanceReleased();

        // Player 2 advance boost from joystick2 (X < -0.5 = forward for away team)
        if (_gameMode == GameMode.TwoPlayerLocal)
        {
            var jv2 = Joystick2.Value;
            if (jv2.X < -0.5)
                _state.AwayAdvanceHeld();
            else
                _state.AwayAdvanceReleased();
        }

        var defending = _state.IsHomeDefending;
        bool awayAttacking = _state.BallOwnerType == BallOwnershipType.Opponent;
        bool controlsActive = !_state.IsMatchOver && !_state.IsHalfTime && !_state.IsGoalCelebration;

        // Player 1 controls
        PassUpButton.IsVisible = !defending && controlsActive;
        PassDownButton.IsVisible = !defending && controlsActive;
        ShootButton.IsVisible = !defending && controlsActive;
        SwitchDefenderButton.IsVisible = defending && controlsActive;
        Joystick.IsVisible = controlsActive;

        // Player 2 controls (two-player mode only)
        if (_gameMode == GameMode.TwoPlayerLocal)
        {
            bool awayDefending = !awayAttacking && !_state.IsMatchOver;
            AwayPassUpButton.IsVisible = awayAttacking && controlsActive;
            AwayPassDownButton.IsVisible = awayAttacking && controlsActive;
            AwayShootButton.IsVisible = awayAttacking && controlsActive;
            AwaySwitchDefenderButton.IsVisible = awayDefending && controlsActive;
            Joystick2.IsVisible = controlsActive;
            Player2Buttons.IsVisible = controlsActive;
        }

        StatusLabel.Text = _state.StatusText;
        _state.Update(0.016f);
        GameView.Invalidate();
    }

    void OnTapped(object? sender, TappedEventArgs e)
    {
        var pos = e.GetPosition(GameView);
        if (pos is Point p)
        {
            // Resume from pause on tap
            if (_state.IsPaused)
            {
                _state.IsPaused = false;
                return;
            }
            // If match is over, restart on tap
            if (_state.IsMatchOver)
            {
                _state.RestartMatch();
                return;
            }
            _state.TargetPoint = p;
        }
    }

    void OnPassUp(object? sender, EventArgs e)
        => _state.QueuePassVertical(-1);

    void OnPassDown(object? sender, EventArgs e)
        => _state.QueuePassVertical(1);

    void OnSwitchDefender(object? sender, EventArgs e) => _state.SwitchControlledDefender();
    void OnShoot(object? sender, EventArgs e) => _state.QueueShoot();

    void OnAwayPassUp(object? sender, EventArgs e)
        => _state.AwayQueuePassVertical(-1);

    void OnAwayPassDown(object? sender, EventArgs e)
        => _state.AwayQueuePassVertical(1);

    void OnAwaySwitchDefender(object? sender, EventArgs e) => _state.AwaySwitchControlledDefender();
    void OnAwayShoot(object? sender, EventArgs e) => _state.AwayQueueShoot();

#if WINDOWS
    void OnWinKeyDown(object sender, KeyRoutedEventArgs e)
    {
        _keysDown.Add(e.Key);
        UpdateKeyboardInput();
    }
    void OnWinKeyUp(object sender, KeyRoutedEventArgs e)
    {
        _keysDown.Remove(e.Key);
        UpdateKeyboardInput();
    }
    void UpdateKeyboardInput()
    {
        double x = 0, y = 0;
        if (_keysDown.Contains(VirtualKey.W) || _keysDown.Contains(VirtualKey.Up)) y -= 1;
        if (_keysDown.Contains(VirtualKey.S) || _keysDown.Contains(VirtualKey.Down)) y += 1;
        if (_keysDown.Contains(VirtualKey.A) || _keysDown.Contains(VirtualKey.Left)) x -= 1;
        if (_keysDown.Contains(VirtualKey.D) || _keysDown.Contains(VirtualKey.Right)) x += 1;
        var len = Math.Sqrt(x * x + y * y);
        if (len > 0) { x /= len; y /= len; }
        const double maxSpeed = 110;
        _state.ActiveMoveInput = new Point(x * maxSpeed, y * maxSpeed);
        if (_keysDown.Contains(VirtualKey.Space)) _advanceHeld = true; else _advanceHeld = false;

        // Keyboard actions: Remove key from set after triggering to ensure single-fire per press.
        // Keys re-enter the set only via OnWinKeyDown (new key press event).
        if (_keysDown.Contains(VirtualKey.Q)) { _keysDown.Remove(VirtualKey.Q); _state.QueuePassVertical(-1); }
        if (_keysDown.Contains(VirtualKey.E)) { _keysDown.Remove(VirtualKey.E); _state.QueuePassVertical(1); }
        if (_keysDown.Contains(VirtualKey.F)) { _keysDown.Remove(VirtualKey.F); _state.QueueShoot(); }
        if (_keysDown.Contains(VirtualKey.R)) { _keysDown.Remove(VirtualKey.R); _state.SwitchControlledDefender(); }
        if (_keysDown.Contains(VirtualKey.H)) { _keysDown.Remove(VirtualKey.H); _state.ShowKeyboardHelp = !_state.ShowKeyboardHelp; }
        if (_keysDown.Contains(VirtualKey.Escape)) { _keysDown.Remove(VirtualKey.Escape); _state.IsPaused = !_state.IsPaused; }
    }
#endif

    void OnGameEvent(GameEventType eventType)
    {
        if (_soundManager is null) return;
        switch (eventType)
        {
            case GameEventType.GoalHome:
            case GameEventType.GoalAway:
                _soundManager.PlayGoal();
                break;
            case GameEventType.Shoot:
            case GameEventType.AwayShoot:
                _soundManager.PlayShoot();
                break;
            case GameEventType.Pass:
            case GameEventType.AwayPass:
                _soundManager.PlayPass();
                break;
            case GameEventType.Save:
                break;
            case GameEventType.Interception:
                break;
            case GameEventType.Whistle:
            case GameEventType.HalfTime:
            case GameEventType.FullTime:
            case GameEventType.PenaltyAwarded:
            case GameEventType.Suspension:
                _soundManager.PlayWhistle();
                break;
        }
    }
}

public enum BallOwnershipType { Player, Teammate, Opponent, Loose }

public class Actor
{
    public Point Position;
    public Point Velocity;
    public bool IsGoalkeeper;
    public double BaseY;
    public double BaseX;
    public bool WasAdvancing; // track if this actor initiated advance
    public int SuspensionTicks; // remaining ticks of 2-minute suspension (0 = active)
    public bool IsSuspended => SuspensionTicks > 0;
}

// ── Confetti particle for goal celebration ──
public struct ConfettiParticle
{
    public float X, Y, VX, VY;
    public int ColorIndex;
    public int LifeTicks;
}

public class GameState
{
    public const double GoalCenterInset = 20;
    public const double GoalAreaRadius = 160;
    public const double FreeThrowRadius = 240;
    public const double FieldMargin = 14;

    // Goalkeeper vertical range: half the goal-mouth height (goal is 160px tall)
    const double GoalMouthHalf = 80;

    // Gameplay tuning constants (base values — adjusted by difficulty)
    const double PassInterceptDistance = 34;
    const double PassInterceptChance = 0.12;
    const double ShotInterceptChance = 0.25;
    const double GoalkeeperAdvanceOffset = 40;
    const double AwayPassClosestChance = 0.70;
    const double AwayPassSecondClosestChance = 0.95; // cumulative (25% for second closest)

    // Match clock constants (game-time seconds per half)
    public const double HalfDurationSeconds = 300; // 5 min per half (scaled from 30 real min)
    const double GameTimeMultiplier = 6.0; // 1 real second = 6 game seconds (so 5 min = 30 game-min)
    const int HalfTimeDisplayTicks = 180; // ~3 seconds display at 60fps
    const int FullTimeDisplayTicks = 300; // ~5 seconds display

    // Passive play constants
    const double PassivePlayWarningSeconds = 35; // warn after 35 game-seconds of possession
    const double PassivePlayTurnoverSeconds = 45; // turnover after 45 game-seconds

    // Fast break constants
    const int FastBreakDurationTicks = 90; // ~1.5 seconds of speed boost
    const double FastBreakSpeedMultiplier = 1.6;

    // Penalty (7m) constants
    const double PenaltyFoulZoneRadius = 50; // foul within this distance of goal area triggers 7m
    const double PenaltyShotDuration = 0.7f;
    const double PenaltySaveChance = 0.45; // 45% GK save on penalties
    const double PenaltyAwardChance = 0.4; // 40% chance foul near goal area awards penalty

    // 2-minute suspension constants (approximate at ~62.5fps from 0.016f dt)
    const int SuspensionDurationTicks = 7500; // ~2 real minutes at ~62.5fps
    const double SuspensionChance = 0.15; // 15% chance a collision results in a suspension

    // Shot distance factor constants
    const double MaxShotDistance = 400; // beyond this distance, shots are very unlikely to score
    const double CloseRangeShotBonus = 0.15; // bonus save reduction for close shots

    // Away AI attack constants
    const double AwayPushForwardThreshold = 40; // distance from arc before AI settles and starts passing
    const int ThrowOffCarrierIndex = 1; // field player index used for throw-off ball carrier

    // Pivot (circle runner) positioning constants
    const double PivotOscillationPeriod = 800.0; // milliseconds per oscillation cycle
    const double PivotDriftAmplitude = 60; // pixels of vertical drift between defenders

    // Defensive tackle constants
    const double TackleDistance = 22;
    const double TackleStealChance = 0.30;
    const double TackleFoulChance = 0.20;
    const double ControlledDefenderInterceptBonus = 0.25;
    const double GoalkeeperSaveRadius = 32;
    const int TackleCooldownDuration = 25;

    // Goalkeeper on-target save chances
    const double OnTargetSaveBase = 0.30;
    const double OnTargetDistBonus = 0.25;
    const double OnTargetCloseRangePenalty = 0.10;
    const double OnTargetPositionBonus = 0.20;

    // ── Difficulty multipliers (set in constructor) ──
    readonly double _diffInterceptMult;        // multiplier on AI intercept chances
    readonly double _diffBreakthroughMult;     // multiplier on AI breakthrough chance
    readonly int _diffPassCooldownBase;        // base ticks for AI pass cooldown
    readonly int _diffPassCooldownRange;       // random range added to pass cooldown
    readonly double _diffHomeGkBonus;          // bonus save chance for player's GK
    readonly double _diffAwayGkBonus;          // bonus save chance for opponent GK
    readonly double _diffTackleStealBonus;     // bonus steal chance for player tackles

    // ── Match statistics ──
    public int ShotsHome { get; private set; }
    public int ShotsAway { get; private set; }
    public int SavesHome { get; private set; }  // saves by home GK (against away shots)
    public int SavesAway { get; private set; }  // saves by away GK (against home shots)
    public int PassesHome { get; private set; }
    public int PassesAway { get; private set; }

    // ── Confetti system ──
    public const int MaxConfetti = 60;
    public readonly ConfettiParticle[] Confetti = new ConfettiParticle[MaxConfetti];
    public int ConfettiCount { get; private set; }

    public Size ViewSize { get; set; }
    public readonly Actor[] HomePlayers = new Actor[7];
    public readonly Actor[] AwayPlayers = new Actor[7];
    public readonly GameMode Mode;
    public readonly Difficulty Difficulty;

    /// <summary>Raised when a notable game event occurs (goal, shot, pass, etc.).</summary>
    public event Action<GameEventType>? GameEvent;

    // Ownership
    public int BallOwnerPlayerIndex { get; private set; } = 1;
    public BallOwnershipType BallOwnerType { get; private set; } = BallOwnershipType.Player;
    public int BallOwnerAwayIndex { get; private set; } = -1;
    public int ControlledDefenderIndex { get; private set; } = 1;
    public bool IsHomeDefending => BallOwnerType != BallOwnershipType.Player && !IsMatchOver;
    public Point BallPos { get; private set; } = new(100, 300);

    // Input
    public Point ActiveMoveInput { get; set; }
    public Point? TargetPoint { get; set; } // tap target (optional future use)

    // Pass state
    bool _passActive;
    int _passTargetHomeIndex = -1;
    Point _passStartPos; // position where pass originated
    public bool IsPassActive => _passActive;
    public int PassTargetTeammateIndex => _passTargetHomeIndex; // expose for drawable

    // Shoot state
    bool _shootActive;
    float _shootTime;
    Point _shootStart;
    Point _shootEnd;
    public bool IsShootActive => _shootActive;
    public Point ShootEnd => _shootEnd;

    // Away shoot state
    bool _awayShootActive;
    float _awayShootTime;
    Point _awayShootStart;
    Point _awayShootEnd;
    public bool IsAwayShootActive => _awayShootActive;
    public Point AwayShootEnd => _awayShootEnd;

    // Away pass state
    bool _awayPassActive;
    int _awayPassTargetIndex = -1;
    int _awayPassCooldownTicks;
    int _awayBuildupPasses;       // total passes completed in this possession
    bool _awayBreakthrough;       // an attacker is breaking through to shoot

    // Retreat state
    bool _retreatingFormerOwner;
    int _formerOwnerIndex = -1;

    // Score / status
    public int ScoreHome { get; private set; }
    public int ScoreAway { get; private set; }
    public string StatusText { get; private set; } = "";
    bool _advanceBoost;
    string _statusOverrideText = "";
    int _statusOverrideTicks;
    double _attackDiagonalBoostY;
    Rect _rightGoal;
    Rect _leftGoal;
    double _defenderSideBoostY;
    double _defenderDiagBoostY;
    bool _defenderAdvanceBoost;
    bool _resettingAfterGoal;
    int _resetCountdown;
    bool _viewInitialized;

    // Match clock state
    public int CurrentHalf { get; private set; } = 1; // 1 or 2
    public double MatchClockSeconds { get; private set; } // game-time elapsed in current half
    public bool IsHalfTime { get; private set; }
    public bool IsMatchOver { get; private set; }
    public bool IsPaused { get; set; }
    public bool ShowKeyboardHelp { get; set; }
    int _halfTimeCountdown;

    // Passive play state
    double _possessionTimer; // game-time seconds since last ownership change
    public bool PassivePlayWarningActive { get; private set; }

    // Fast break state
    int _homeFastBreakTicks;
    int _awayFastBreakTicks;
    public bool IsHomeFastBreak => _homeFastBreakTicks > 0;
    public bool IsAwayFastBreak => _awayFastBreakTicks > 0;

    // Penalty (7m) state
    bool _penaltyActive;
    bool _penaltyIsHome; // true = home team shoots penalty
    float _penaltyTime;
    Point _penaltyStart;
    Point _penaltyEnd;
    public bool IsPenaltyActive => _penaltyActive;
    public bool PenaltyIsHome => _penaltyIsHome;

    // Ball height simulation for visual arcs
    public double BallHeight { get; private set; } // 0 = ground, 1 = max height

    // Goalkeeper hold after save
    int _keeperHoldTicks; // ticks remaining for keeper to hold ball before auto-throw

    // Tackle cooldown
    int _tackleCooldownTicks;

    // Smoothed press line to prevent abrupt target jumps on possession change
    double _smoothedPressLineX = 350;

    // Player 2 input (two-player local mode)
    public Point AwayActiveMoveInput { get; set; }
    bool _awayAdvanceBoost2;
    public int ControlledAwayAttackerIndex { get; private set; } = 1; // away ball carrier index player 2 controls
    public int ControlledAwayDefenderIndex { get; private set; } = 1; // away defender player 2 controls when home has ball

    // Goal celebration
    int _goalCelebrationTicks;
    public bool IsGoalCelebration => _goalCelebrationTicks > 0;
    public string GoalCelebrationText { get; private set; } = "";

    // Suspension display
    public int HomeSuspensionCount => HomePlayers.Count(p => p.IsSuspended);
    public int AwaySuspensionCount => AwayPlayers.Count(p => p.IsSuspended);

    public GameState(GameMode mode = GameMode.SinglePlayer, Difficulty difficulty = Difficulty.Medium)
    {
        Mode = mode;
        Difficulty = difficulty;

        // Set difficulty multipliers
        switch (difficulty)
        {
            case Difficulty.Easy:
                _diffInterceptMult = 0.5;
                _diffBreakthroughMult = 0.5;
                _diffPassCooldownBase = 50;
                _diffPassCooldownRange = 30;
                _diffHomeGkBonus = 0.10;
                _diffAwayGkBonus = -0.08;
                _diffTackleStealBonus = 0.10;
                break;
            case Difficulty.Hard:
                _diffInterceptMult = 1.5;
                _diffBreakthroughMult = 1.5;
                _diffPassCooldownBase = 20;
                _diffPassCooldownRange = 15;
                _diffHomeGkBonus = -0.05;
                _diffAwayGkBonus = 0.10;
                _diffTackleStealBonus = -0.08;
                break;
            default: // Medium
                _diffInterceptMult = 1.0;
                _diffBreakthroughMult = 1.0;
                _diffPassCooldownBase = 30;
                _diffPassCooldownRange = 20;
                _diffHomeGkBonus = 0.0;
                _diffAwayGkBonus = 0.0;
                _diffTackleStealBonus = 0.0;
                break;
        }

        _rightGoal = new Rect(0, 120, 12, 160);
        _leftGoal = new Rect(8, 120, 12, 160);
        InitTeam(HomePlayers, GoalCenterInset + GoalAreaRadius + 30, true);
        InitTeam(AwayPlayers, 700, false);
        BallPos = HomePlayers[BallOwnerPlayerIndex].Position;
        UpdateStatus();
    }

    void InitTeam(Actor[] team, double startX, bool leftToRight)
    {
        double topY = 80;
        double bottomY = ViewSize.Height > 0 ? ViewSize.Height - 120 : 520;
        if (bottomY <= topY) bottomY = topY + 240;

        for (int i = 0; i < team.Length; i++)
        {
            var laneY = i == 0
                ? (ViewSize.Height > 0 ? ViewSize.Height / 2 : 300)
                : topY + (i - 1) * ((bottomY - topY) / (team.Length - 2));
            double posX;
            if (i == 0) // Goalkeeper: close to goal line
                posX = leftToRight
                    ? GoalCenterInset + 20
                    : (ViewSize.Width > 0 ? ViewSize.Width - GoalCenterInset - 20 : startX);
            else
                posX = startX;
            team[i] = new Actor
            {
                Position = new Point(posX, laneY),
                Velocity = Point.Zero,
                IsGoalkeeper = i == 0,
                BaseY = laneY,
                BaseX = posX,
                WasAdvancing = false
            };
        }
    }

    void SetTeamBasePositions(Actor[] team, double startX, bool leftToRight)
    {
        double topY = 80;
        double bottomY = ViewSize.Height > 0 ? ViewSize.Height - 120 : 520;
        if (bottomY <= topY) bottomY = topY + 240;

        for (int i = 0; i < team.Length; i++)
        {
            var laneY = i == 0
                ? (ViewSize.Height > 0 ? ViewSize.Height / 2 : 300)
                : topY + (i - 1) * ((bottomY - topY) / (team.Length - 2));
            double posX;
            if (i == 0) // Goalkeeper: close to goal line
                posX = leftToRight
                    ? GoalCenterInset + 20
                    : (ViewSize.Width > 0 ? ViewSize.Width - GoalCenterInset - 20 : startX);
            else
                posX = startX;
            team[i].BaseX = posX;
            team[i].BaseY = laneY;
            team[i].WasAdvancing = false;
        }
    }

    /// <summary>Called when the rendering area size changes. Updates ViewSize and recalculates base positions.</summary>
    public void OnViewSizeChanged(Size newSize)
    {
        ViewSize = newSize;
        if (ViewSize.Width <= 0 || ViewSize.Height <= 0) return;
        double homeStartX = GoalCenterInset + GoalAreaRadius + 30;
        double awayStartX = ViewSize.Width - GoalCenterInset - GoalAreaRadius - 30;
        SetTeamBasePositions(HomePlayers, homeStartX, true);
        SetTeamBasePositions(AwayPlayers, awayStartX, false);
    }

    public void SwitchControlledDefender()
    {
        int startIdx = ControlledDefenderIndex;
        for (int attempt = 0; attempt < HomePlayers.Length - 1; attempt++)
        {
            ControlledDefenderIndex++;
            if (ControlledDefenderIndex >= HomePlayers.Length)
                ControlledDefenderIndex = 1;
            if (!HomePlayers[ControlledDefenderIndex].IsSuspended)
                break;
        }
        SetStatusOverride($"Försvarare #{ControlledDefenderIndex}", 45);
    }

    public void DefenderSideUpPressed()
    {
        if (!IsHomeDefending) return;
        _defenderSideBoostY = -140;
    }

    public void DefenderSideDownPressed()
    {
        if (!IsHomeDefending) return;
        _defenderSideBoostY = 140;
    }

    public void DefenderSideReleased() => _defenderSideBoostY = 0;

    public void AwayAdvanceHeld()
    {
        if (Mode != GameMode.TwoPlayerLocal) return;
        _awayAdvanceBoost2 = true;
    }
    public void AwayAdvanceReleased()
    {
        _awayAdvanceBoost2 = false;
    }

    public void AwaySwitchControlledAttacker()
    {
        if (Mode != GameMode.TwoPlayerLocal) return;
        for (int attempt = 0; attempt < AwayPlayers.Length - 1; attempt++)
        {
            ControlledAwayAttackerIndex++;
            if (ControlledAwayAttackerIndex >= AwayPlayers.Length)
                ControlledAwayAttackerIndex = 1;
            if (!AwayPlayers[ControlledAwayAttackerIndex].IsSuspended)
                break;
        }
        SetStatusOverride($"Borta #{ControlledAwayAttackerIndex}", 45);
    }

    public void AwaySwitchControlledDefender()
    {
        if (Mode != GameMode.TwoPlayerLocal) return;
        for (int attempt = 0; attempt < AwayPlayers.Length - 1; attempt++)
        {
            ControlledAwayDefenderIndex++;
            if (ControlledAwayDefenderIndex >= AwayPlayers.Length)
                ControlledAwayDefenderIndex = 1;
            if (!AwayPlayers[ControlledAwayDefenderIndex].IsSuspended)
                break;
        }
        SetStatusOverride($"Borta försvarare #{ControlledAwayDefenderIndex}", 45);
    }

    public void AwayQueuePassVertical(int dirY)
    {
        if (Mode != GameMode.TwoPlayerLocal) return;
        if (BallOwnerType != BallOwnershipType.Opponent) return;
        if (IsMatchOver || IsHalfTime) return;
        var owner = AwayPlayers[BallOwnerAwayIndex];
        (int idx, Actor actor)? best = null; double bestMetric = double.MaxValue;
        for (int i = 0; i < AwayPlayers.Length; i++)
        {
            if (i == BallOwnerAwayIndex) continue;
            if (AwayPlayers[i].IsSuspended) continue;
            var dy = AwayPlayers[i].Position.Y - owner.Position.Y;
            if (dirY < 0 && dy >= 0) continue;
            if (dirY > 0 && dy <= 0) continue;
            var metric = Distance(owner.Position, AwayPlayers[i].Position);
            if (metric < bestMetric) { bestMetric = metric; best = (i, AwayPlayers[i]); }
        }
        if (best is null) return;
        _awayPassActive = true;
        _awayPassTargetIndex = best.Value.idx;
        _awayPassCooldownTicks = _diffPassCooldownBase;
        _awayBuildupPasses++;
        BallOwnerType = BallOwnershipType.Loose;
        BallOwnerAwayIndex = -1;
        BallOwnerPlayerIndex = -1;
        PassesAway++;
        GameEvent?.Invoke(GameEventType.AwayPass);
    }

    public void AwayQueueShoot()
    {
        if (Mode != GameMode.TwoPlayerLocal) return;
        if (BallOwnerType != BallOwnershipType.Opponent) return;
        if (IsMatchOver || IsHalfTime) return;
        StartAwayShoot(AwayPlayers[BallOwnerAwayIndex].Position);
    }

    public void DefenderDiagUpPressed()
    {
        if (!IsHomeDefending) return;
        _defenderDiagBoostY = -140;
    }

    public void DefenderDiagDownPressed()
    {
        if (!IsHomeDefending) return;
        _defenderDiagBoostY = 140;
    }

    public void DefenderDiagReleased() => _defenderDiagBoostY = 0;

    public void AttackDiagonalUpPressed()
    {
        if (BallOwnerType != BallOwnershipType.Player) return;
        _attackDiagonalBoostY = -140;
    }

    public void AttackDiagonalDownPressed()
    {
        if (BallOwnerType != BallOwnershipType.Player) return;
        _attackDiagonalBoostY = 140;
    }

    public void AttackDiagonalReleased() => _attackDiagonalBoostY = 0;

    public void QueuePassVertical(int dirY)
    {
        if (BallOwnerType != BallOwnershipType.Player) return;
        if (IsMatchOver || IsHalfTime) return;
        var owner = HomePlayers[BallOwnerPlayerIndex];
        (int idx, Actor actor)? best = null; double bestMetric = double.MaxValue;
        // Allow passing to any teammate including goalkeeper (index 0)
        for (int i = 0; i < HomePlayers.Length; i++)
        {
            if (i == BallOwnerPlayerIndex) continue;
            if (HomePlayers[i].IsSuspended) continue;
            var dy = HomePlayers[i].Position.Y - owner.Position.Y;
            if (dirY < 0 && dy >= 0) continue;
            if (dirY > 0 && dy <= 0) continue;
            // Use full distance (not just Y) so passes go to the closest teammate
            var metric = Distance(owner.Position, HomePlayers[i].Position);
            if (metric < bestMetric) { bestMetric = metric; best = (i, HomePlayers[i]); }
        }
        if (best is null) return;
        _passActive = true;
        _passTargetHomeIndex = best.Value.idx;
        _passStartPos = BallPos;
        _formerOwnerIndex = BallOwnerPlayerIndex;
        _retreatingFormerOwner = true;
        _advanceBoost = false; // stop boost on pass
        HomePlayers[_formerOwnerIndex].WasAdvancing = true;
        BallOwnerType = BallOwnershipType.Loose;
        BallOwnerPlayerIndex = -1;
        BallOwnerAwayIndex = -1;
        _possessionTimer = 0; // reset passive play on pass attempt
        PassesHome++;
        GameEvent?.Invoke(GameEventType.Pass);
    }

    public void QueueShoot()
    {
        if (BallOwnerType != BallOwnershipType.Player) return;
        if (IsMatchOver || IsHalfTime) return;
        _formerOwnerIndex = BallOwnerPlayerIndex;
        _retreatingFormerOwner = true;
        _advanceBoost = false;
        _shootStart = BallPos;
        // Wider shot spread: use ±75px (was ±60) to cover more of the 160px goal
        var shootOffsetY = (Random.Shared.NextDouble() - 0.5) * 150;
        _shootEnd = new Point(ViewSize.Width - 14, ViewSize.Height / 2 + shootOffsetY);
        _shootTime = 0f;
        _shootActive = true;
        BallOwnerType = BallOwnershipType.Loose;
        BallOwnerPlayerIndex = -1;
        BallOwnerAwayIndex = -1;
        _possessionTimer = 0;
        PassivePlayWarningActive = false;
        ShotsHome++;
        GameEvent?.Invoke(GameEventType.Shoot);
    }

    public void AdvanceHeld()
    {
        if (BallOwnerType == BallOwnershipType.Player)
        {
            _advanceBoost = true;
            return;
        }

        if (IsHomeDefending)
            _defenderAdvanceBoost = true;
    }
    public void AdvanceReleased()
    {
        _advanceBoost = false;
        _defenderAdvanceBoost = false;
    }

    public void Update(double dt)
    {
        if (_awayPassCooldownTicks > 0)
            _awayPassCooldownTicks--;

        // Away goalkeeper hold: auto-throw to nearest field player after hold expires
        if (_keeperHoldTicks > 0)
        {
            _keeperHoldTicks--;
            if (_keeperHoldTicks == 0 && BallOwnerType == BallOwnershipType.Opponent && BallOwnerAwayIndex == 0)
            {
                int nearestField = GetNearestAwayIndex(AwayPlayers[0].Position);
                if (nearestField >= 1)
                {
                    _awayPassTargetIndex = nearestField;
                    _awayPassActive = true;
                    _awayPassCooldownTicks = _diffPassCooldownBase;
                    BallOwnerType = BallOwnershipType.Loose;
                    BallOwnerAwayIndex = -1;
                    BallOwnerPlayerIndex = -1;
                    GameEvent?.Invoke(GameEventType.AwayPass);
                }
            }
        }

        // Match clock
        if (!IsMatchOver && !IsHalfTime && !_resettingAfterGoal)
        {
            MatchClockSeconds += dt * GameTimeMultiplier;
            if (MatchClockSeconds >= HalfDurationSeconds)
            {
                MatchClockSeconds = HalfDurationSeconds;
                if (CurrentHalf == 1)
                {
                    IsHalfTime = true;
                    _halfTimeCountdown = HalfTimeDisplayTicks;
                    SetStatusOverride("HALVTID", HalfTimeDisplayTicks);
                    GameEvent?.Invoke(GameEventType.HalfTime);
                    ClearAllActiveActions();
                }
                else
                {
                    IsMatchOver = true;
                    SetStatusOverride("SLUTSIGNAL", FullTimeDisplayTicks);
                    GameEvent?.Invoke(GameEventType.FullTime);
                    ClearAllActiveActions();
                }
            }
        }

        // Half-time countdown
        if (IsHalfTime)
        {
            _halfTimeCountdown--;
            if (_halfTimeCountdown <= 0)
            {
                IsHalfTime = false;
                StartSecondHalf();
            }
            return;
        }

        // Full-time: stop the game
        if (IsMatchOver) return;

        // Goal celebration pause
        if (_goalCelebrationTicks > 0)
        {
            _goalCelebrationTicks--;
            UpdateConfetti(dt);
            return;
        }

        // Fast break timers
        if (_homeFastBreakTicks > 0) _homeFastBreakTicks--;
        if (_awayFastBreakTicks > 0) _awayFastBreakTicks--;

        // Suspension timers
        foreach (var team in new[] { HomePlayers, AwayPlayers })
            foreach (var a in team)
                if (a.SuspensionTicks > 0) a.SuspensionTicks--;

        // Passive play tracking
        if (BallOwnerType == BallOwnershipType.Player && !_shootActive && !_passActive)
        {
            _possessionTimer += dt * GameTimeMultiplier;
            if (_possessionTimer >= PassivePlayTurnoverSeconds)
            {
                // Passive play turnover
                _possessionTimer = 0;
                PassivePlayWarningActive = false;
                GiveBallToOpponent(GetNearestAwayIndex(BallPos), "Passivt spel: motståndarboll");
                GameEvent?.Invoke(GameEventType.Whistle);
                return;
            }
            else if (_possessionTimer >= PassivePlayWarningSeconds && !PassivePlayWarningActive)
            {
                PassivePlayWarningActive = true;
                SetStatusOverride("⚠ Passivt spel - varning!", 90);
                GameEvent?.Invoke(GameEventType.Whistle);
            }
        }

        // Penalty shot processing
        if (_penaltyActive)
        {
            UpdatePenalty(dt);
            return;
        }

        if (ViewSize.Width > 0)
        {
            var centerY = ViewSize.Height / 2 - _rightGoal.Height / 2;
            _rightGoal = new Rect(ViewSize.Width - _rightGoal.Width - 8, centerY, _rightGoal.Width, _rightGoal.Height);
            _leftGoal = new Rect(8, centerY, _leftGoal.Width, _leftGoal.Height);
            if (!_viewInitialized)
            {
                InitTeam(HomePlayers, GoalCenterInset + GoalAreaRadius + 30, true);
                InitTeam(AwayPlayers, ViewSize.Width - GoalCenterInset - GoalAreaRadius - 30, false);
                if (BallOwnerType == BallOwnershipType.Player && BallOwnerPlayerIndex >= 0)
                    BallPos = HomePlayers[BallOwnerPlayerIndex].Position;
                _viewInitialized = true;
            }
        }
        UpdatePlayers(dt);
        UpdateBall(dt);
        UpdateBallHeight(dt);
        UpdateConfetti(dt);
        UpdateStatus();
    }

    void UpdatePlayers(double dt)
    {
        if (_resettingAfterGoal)
        {
            _resetCountdown--;
            bool allArrived = true;
            foreach (var team in new[] { HomePlayers, AwayPlayers })
            {
                for (int pi = 0; pi < team.Length; pi++)
                {
                    var actor = team[pi];
                    var dx = actor.BaseX - actor.Position.X;
                    var dy = actor.BaseY - actor.Position.Y;
                    var dist = Math.Sqrt(dx * dx + dy * dy);
                    if (dist > 3)
                    {
                        allArrived = false;
                        double speed;
                        if (actor.IsGoalkeeper)
                            speed = 600;
                        else if ((BallOwnerPlayerIndex >= 0 && team == HomePlayers && pi == BallOwnerPlayerIndex)
                              || (BallOwnerAwayIndex >= 0 && team == AwayPlayers && pi == BallOwnerAwayIndex))
                            speed = 500;
                        else
                            speed = 300 + (pi * 25);
                        var step = Math.Min(speed * dt, dist);
                        double wobble = Math.Sin(Environment.TickCount / 200.0 + pi * 1.7) * 1.2;
                        actor.Position = new Point(
                            actor.Position.X + dx / dist * step,
                            actor.Position.Y + dy / dist * step + wobble);
                    }
                    else
                    {
                        actor.Position = new Point(actor.BaseX, actor.BaseY);
                    }
                }
            }

            if (BallOwnerPlayerIndex >= 0)
                BallPos = HomePlayers[BallOwnerPlayerIndex].Position;
            else if (BallOwnerAwayIndex >= 0)
                BallPos = AwayPlayers[BallOwnerAwayIndex].Position;

            if (allArrived || _resetCountdown <= 0)
            {
                _resettingAfterGoal = false;
                // No instant snap — remaining players will transition smoothly
                // via normal game logic (lerp-based movement)
            }

            return;
        }

        double defenseFrontX = AwayPlayers.Skip(1).Min(a => a.Position.X);
        double rawPressLineX = defenseFrontX - 36;
        _smoothedPressLineX = Lerp(_smoothedPressLineX, rawPressLineX, 0.08);
        double pressLineX = _smoothedPressLineX;

        // Owner movement: joystick gives direct control; otherwise auto-advance smoothly
        if (BallOwnerType == BallOwnershipType.Player && BallOwnerPlayerIndex >= 0)
        {
            var owner = HomePlayers[BallOwnerPlayerIndex];
            double fastBreakMult = _homeFastBreakTicks > 0 ? FastBreakSpeedMultiplier : 1.0;
            bool hasManualInput = Math.Abs(ActiveMoveInput.X) > 5 || Math.Abs(ActiveMoveInput.Y) > 5
                                  || _advanceBoost || Math.Abs(_attackDiagonalBoostY) > 0.1;

            if (hasManualInput)
            {
                // Direct joystick / button control
                double forwardExtra = _advanceBoost ? 220 : 0;
                double diagonalForwardExtra = _attackDiagonalBoostY == 0 ? 0 : 140;
                var nextPos = new Point(
                    owner.Position.X + (ActiveMoveInput.X + forwardExtra + diagonalForwardExtra) * dt * fastBreakMult,
                    owner.Position.Y + (ActiveMoveInput.Y + _attackDiagonalBoostY) * dt * fastBreakMult);
                owner.Position = nextPos;
            }
            else
            {
                // No manual input: auto-advance naturally toward the press line
                double targetX = Math.Min(pressLineX - 40, ViewSize.Width - 250);
                targetX = Math.Max(owner.BaseX + 20, targetX);
                double centerY = ViewSize.Height > 0 ? ViewSize.Height / 2 : 300;
                double lerpRate = _homeFastBreakTicks > 0 ? 0.04 : 0.025;
                owner.Position = new Point(
                    Lerp(owner.Position.X, targetX, lerpRate),
                    Lerp(owner.Position.Y, centerY, 0.01));
            }

            if (IsInsideRightGoalArea(owner.Position))
            {
                GiveBallToOpponent(GetNearestAwayIndex(owner.Position), "Målgård: motståndarboll");
                return;
            }

            ClampActor(owner);

            if (TryHandleFrontalDefense(owner))
            {
                // event handled in helper
            }
        }

        // Supporting players
        for (int i = 1; i < HomePlayers.Length; i++)
        {
            // Suspended players stay off-field
            if (HomePlayers[i].IsSuspended)
            {
                HomePlayers[i].Position = new Point(20, 30); // bench area top-left
                continue;
            }
            if (BallOwnerType == BallOwnershipType.Player && i == BallOwnerPlayerIndex) continue;
            if (IsHomeDefending && i == ControlledDefenderIndex)
            {
                var hasManualDefenseInput = Math.Abs(ActiveMoveInput.X) > 0.1
                                            || Math.Abs(ActiveMoveInput.Y) > 0.1
                                            || _defenderAdvanceBoost
                                            || Math.Abs(_defenderSideBoostY) > 0.1
                                            || Math.Abs(_defenderDiagBoostY) > 0.1;
                if (hasManualDefenseInput)
                {
                    var c = HomePlayers[i];
                    const double defenderControlBoost = 1.7;
                    double forwardBoost = _defenderAdvanceBoost ? 180 : 0;
                    double sideBoost = _defenderSideBoostY;
                    double diagForwardBoost = Math.Abs(_defenderDiagBoostY) > 0.1 ? 140 : 0;
                    double diagSideBoost = _defenderDiagBoostY;
                    double sideForwardBoost = Math.Abs(_defenderSideBoostY) > 0.1 ? 120 : 0;
                    c.Position = new Point(
                        c.Position.X + (ActiveMoveInput.X * defenderControlBoost + forwardBoost + sideForwardBoost + diagForwardBoost) * dt,
                        c.Position.Y + (ActiveMoveInput.Y * defenderControlBoost + sideBoost + diagSideBoost) * dt);
                    ClampActor(c);
                    continue;
                }
            }
            var p = HomePlayers[i];
            bool groupRetreat = (BallOwnerType == BallOwnershipType.Opponent) || (BallOwnerType == BallOwnershipType.Loose && !_passActive) || _shootActive;
            double desiredX = p.Position.X, desiredY = p.Position.Y;
            if (groupRetreat)
            {
                // Form defensive arc just outside the goal area, between attackers and goalkeeper
                var defPos = GetDefensiveArcPosition(i - 1, HomePlayers.Length - 1);
                desiredX = defPos.X;
                desiredY = defPos.Y;
            }
            else
            {
                // Attacking: spread evenly based on field index, shift with ball carrier
                // Only count non-suspended active field players for spacing
                int activeFieldCount = 0;
                int activeFieldIdx = 0;
                for (int j = 1; j < HomePlayers.Length; j++)
                {
                    if (HomePlayers[j].IsSuspended) continue;
                    if (BallOwnerType == BallOwnershipType.Player && j == BallOwnerPlayerIndex) continue;
                    if (j == i) activeFieldIdx = activeFieldCount;
                    activeFieldCount++;
                }
                if (activeFieldCount == 0) activeFieldCount = 1;

                double topY = 60;
                double bottomY = ViewSize.Height > 0 ? ViewSize.Height - 60 : 540;
                double slotY = topY + activeFieldIdx * ((bottomY - topY) / Math.Max(activeFieldCount - 1, 1));

                // Shift toward ball carrier Y position (gentle attraction to keep formation wide)
                double carrierY = BallOwnerPlayerIndex >= 0 ? HomePlayers[BallOwnerPlayerIndex].Position.Y : ViewSize.Height / 2;
                slotY = Lerp(slotY, carrierY, 0.10);

                // Differentiate run-up: 6m players (wings 1,5 + pivot 6) go all the way,
                // 9m players (backs 2,3,4) stay approximately with the ball carrier
                bool isPivot = (i == 6);
                bool is6mPlayer = (i == 1 || i == 5 || isPivot);
                double carrierX = BallOwnerPlayerIndex >= 0 ? HomePlayers[BallOwnerPlayerIndex].Position.X : ViewSize.Width / 2;
                if (isPivot)
                {
                    // Pivot positions just outside the opponent's 6m zone, between defenders
                    double rightGoalAreaEdge = ViewSize.Width - GoalCenterInset - GoalAreaRadius;
                    desiredX = Math.Min(rightGoalAreaEdge + 10, ViewSize.Width - 200);
                    desiredX = Math.Max(p.BaseX + 30, desiredX);
                    // Pivot stays central, oscillating between defenders to create gaps
                    double pivotDrift = Math.Sin((double)Environment.TickCount * (2 * Math.PI / PivotOscillationPeriod)) * PivotDriftAmplitude;
                    desiredY = Lerp(ViewSize.Height / 2 + pivotDrift, carrierY, 0.25);
                }
                else if (is6mPlayer)
                {
                    desiredX = Math.Min(pressLineX - 20, ViewSize.Width - 200);
                    desiredX = Math.Max(p.BaseX + 30, desiredX);
                }
                else
                {
                    // Backs: advance roughly with the ball carrier, slightly ahead
                    double backMaxX = Math.Min(carrierX + 40, ViewSize.Width - 200);
                    desiredX = Math.Max(p.BaseX + 20, backMaxX);
                }
                if (!isPivot)
                {
                    // Wings stay wide near the sidelines for authentic handball positioning
                    if (i == 1) // Left wing (VY) — near top sideline
                        desiredY = Lerp(topY, carrierY, 0.12);
                    else if (i == 5) // Right wing (HY) — near bottom sideline
                        desiredY = Lerp(bottomY, carrierY, 0.12);
                    else
                        desiredY = slotY;
                }
            }
            double newX = p.Position.X + (desiredX - p.Position.X) * 0.04;
            double newY = p.Position.Y + (desiredY - p.Position.Y) * 0.04;
            p.Position = new Point(newX, newY);
            ClampActor(p);
        }

        // Goalkeeper AI
        var homeGK = HomePlayers[0];
        double gkCenterY = ViewSize.Height > 0 ? ViewSize.Height / 2 : 300;
        double gkMinY = gkCenterY - GoalMouthHalf;
        double gkMaxY = gkCenterY + GoalMouthHalf;

        // Home goalkeeper reacts to away shots
        bool homeGKHasBall = BallOwnerType == BallOwnershipType.Player && BallOwnerPlayerIndex == 0;
        if (!homeGKHasBall)
        {
            if (_awayShootActive)
            {
                double reactionNoise = Math.Sin(Environment.TickCount / 120.0) * 12;
                double targetY = Math.Clamp(_awayShootEnd.Y + reactionNoise, gkMinY, gkMaxY);
                homeGK.Position = new Point(homeGK.BaseX, Lerp(homeGK.Position.Y, targetY, 0.12));
            }
            else if (BallOwnerType == BallOwnershipType.Opponent && BallOwnerAwayIndex >= 0)
            {
                // Opponent attacking: stay near goal and track ball carrier within goal mouth
                double targetY = Math.Clamp(AwayPlayers[BallOwnerAwayIndex].Position.Y, gkMinY, gkMaxY);
                homeGK.Position = new Point(homeGK.BaseX, Lerp(homeGK.Position.Y, targetY, 0.08));
            }
            else if (BallOwnerType == BallOwnershipType.Player)
            {
                // Home team attacking: advance forward to act as outlet pass option
                double advancedX = GoalCenterInset + GoalAreaRadius + GoalkeeperAdvanceOffset;
                var gSwing = Math.Sin(Environment.TickCount / 700.0) * 30;
                double targetY = Math.Clamp(gkCenterY + gSwing, gkMinY, gkMaxY);
                homeGK.Position = new Point(
                    Lerp(homeGK.Position.X, advancedX, 0.03),
                    Lerp(homeGK.Position.Y, targetY, 0.04));
            }
            else
            {
                // Neutral / loose ball: retreat toward goal
                var gSwing = Math.Sin(Environment.TickCount / 700.0) * 30;
                double targetY = Math.Clamp(gkCenterY + gSwing, gkMinY, gkMaxY);
                homeGK.Position = new Point(
                    Lerp(homeGK.Position.X, homeGK.BaseX, 0.06),
                    Lerp(homeGK.Position.Y, targetY, 0.05));
            }
        }
        ClampActor(homeGK);

        // Away goalkeeper reacts to home shots
        // (skip AI movement when GK has the ball — auto-throw handles transition)
        var awayGK = AwayPlayers[0];
        bool awayGKHasBall = BallOwnerType == BallOwnershipType.Opponent && BallOwnerAwayIndex == 0;
        if (!awayGKHasBall)
        {
            if (_shootActive)
            {
                double reactionNoise = Math.Sin(Environment.TickCount / 120.0 + 2) * 12;
                double targetY = Math.Clamp(_shootEnd.Y + reactionNoise, gkMinY, gkMaxY);
                awayGK.Position = new Point(awayGK.BaseX, Lerp(awayGK.Position.Y, targetY, 0.12));
            }
            else if (BallOwnerType == BallOwnershipType.Player && BallOwnerPlayerIndex >= 0)
            {
                // Home team attacking: stay near goal and track ball carrier within goal mouth
                double targetY = Math.Clamp(HomePlayers[BallOwnerPlayerIndex].Position.Y, gkMinY, gkMaxY);
                awayGK.Position = new Point(awayGK.BaseX, Lerp(awayGK.Position.Y, targetY, 0.08));
            }
            else if (BallOwnerType == BallOwnershipType.Opponent)
            {
                // Away team attacking: advance forward from goal
                double advancedX = ViewSize.Width > 0
                    ? ViewSize.Width - GoalCenterInset - GoalAreaRadius - GoalkeeperAdvanceOffset
                    : awayGK.BaseX;
                var gSwing = Math.Sin(Environment.TickCount / 700.0 + 3) * 30;
                double targetY = Math.Clamp(gkCenterY + gSwing, gkMinY, gkMaxY);
                awayGK.Position = new Point(
                    Lerp(awayGK.Position.X, advancedX, 0.03),
                    Lerp(awayGK.Position.Y, targetY, 0.04));
            }
            else
            {
                // Neutral / loose ball: retreat toward goal
                var gSwing = Math.Sin(Environment.TickCount / 700.0 + 3) * 30;
                double targetY = Math.Clamp(gkCenterY + gSwing, gkMinY, gkMaxY);
                awayGK.Position = new Point(
                    Lerp(awayGK.Position.X, awayGK.BaseX, 0.06),
                    Lerp(awayGK.Position.Y, targetY, 0.05));
            }
        }
        ClampActor(awayGK);

        // Former owner retreat only
        if (_retreatingFormerOwner && _formerOwnerIndex >= 0)
        {
            var r = HomePlayers[_formerOwnerIndex];
            double targetX = Math.Min(pressLineX - (_formerOwnerIndex * 18), ViewSize.Width - 200);
            targetX = Math.Max(r.BaseX + 30, targetX);
            r.Position = new Point(r.Position.X + (targetX - r.Position.X) * 0.01, r.Position.Y + (r.BaseY - r.Position.Y) * 0.04);
            ClampActor(r);
            if (Math.Abs(r.Position.X - targetX) < 2 && Math.Abs(r.Position.Y - r.BaseY) < 2)
            {
                _retreatingFormerOwner = false;
                _formerOwnerIndex = -1;
            }
        }

        bool awayAttacking = (BallOwnerType == BallOwnershipType.Opponent && BallOwnerAwayIndex >= 0) || _awayPassActive || _awayShootActive;

        // Away attack: handball build-up around the free-throw arc
        double arcCenterX = GoalCenterInset;
        double arcRadius = FreeThrowRadius + 30;

        for (int i = 0; i < AwayPlayers.Length; i++)
        {
            var a = AwayPlayers[i];
            if (a.IsGoalkeeper)
            {
                // handled separately above
                ClampActor(a);
                continue;
            }

            if (!awayAttacking)
            {
                // Suspended away players stay off-field
                if (a.IsSuspended)
                {
                    a.Position = new Point(ViewSize.Width > 0 ? ViewSize.Width - 20 : 700, 30); // bench area top-right
                    continue;
                }

                // In two-player mode, player 2 can control a specific defender
                if (Mode == GameMode.TwoPlayerLocal && i == ControlledAwayDefenderIndex)
                {
                    bool hasManualDefense2 = Math.Abs(AwayActiveMoveInput.X) > 0.1
                                              || Math.Abs(AwayActiveMoveInput.Y) > 0.1
                                              || _awayAdvanceBoost2;
                    if (hasManualDefense2)
                    {
                        const double defenderControlBoost = 1.7;
                        double forwardBoost = _awayAdvanceBoost2 ? -180 : 0; // negative = step out leftward to pressure home attackers
                        a.Position = new Point(
                            a.Position.X + (AwayActiveMoveInput.X * defenderControlBoost + forwardBoost) * dt,
                            a.Position.Y + AwayActiveMoveInput.Y * defenderControlBoost * dt);
                        ClampActor(a);
                        continue;
                    }
                }

                // Defending: 6-0 formation — defenders form an arc along the free-throw line
                // Track the ball carrier, or during a home pass, track the pass target
                bool trackingPlayer = (BallOwnerType == BallOwnershipType.Player && BallOwnerPlayerIndex >= 0)
                    || (_passActive && _passTargetHomeIndex >= 0);
                int trackedIdx = BallOwnerPlayerIndex >= 0 ? BallOwnerPlayerIndex : _passTargetHomeIndex;
                if (trackingPlayer && trackedIdx >= 0 && trackedIdx < HomePlayers.Length)
                {
                    // Count non-suspended field defenders for proper arc spacing
                    int activeDefCount = 0;
                    int activeDefIdx = 0;
                    for (int j = 1; j < AwayPlayers.Length; j++)
                    {
                        if (AwayPlayers[j].IsSuspended) continue;
                        if (j == i) activeDefIdx = activeDefCount;
                        activeDefCount++;
                    }
                    if (activeDefCount == 0) activeDefCount = 1;

                    double defArcCenterX = ViewSize.Width > 0 ? ViewSize.Width - GoalCenterInset : 700;
                    double defArcRadius = GoalAreaRadius + 25;

                    // Shift arc toward tracked player
                    double arcCenterY = ViewSize.Height > 0 ? ViewSize.Height / 2 : 300;
                    double carrierY = HomePlayers[trackedIdx].Position.Y;
                    arcCenterY += (carrierY - arcCenterY) * 0.35;

                    double angleRange = 120.0;
                    double startAngle = 180.0 - angleRange / 2; // face left (toward home goal)
                    double angleDeg = startAngle + activeDefIdx * (angleRange / Math.Max(activeDefCount - 1, 1));
                    double angleRad = angleDeg * Math.PI / 180.0;

                    double defX = defArcCenterX + defArcRadius * Math.Cos(angleRad);
                    double defY = arcCenterY + defArcRadius * Math.Sin(angleRad);

                    // Step out to pressure ball carrier when nearby
                    double carrierX = HomePlayers[trackedIdx].Position.X;
                    double distToCarrier = Distance(a.Position, HomePlayers[trackedIdx].Position);
                    if (distToCarrier < 100)
                    {
                        // Pressure: move toward carrier
                        defX = Lerp(defX, carrierX + 20, 0.3);
                        defY = Lerp(defY, carrierY, 0.2);
                    }

                    double drift = Math.Sin(Environment.TickCount / 600.0 + i * 1.2) * 10;

                    a.Position = new Point(
                        Lerp(a.Position.X, defX, 0.06),
                        Lerp(a.Position.Y, defY + drift, 0.06));
                }
                else
                {
                    // No specific attacker: smoothly sway toward base position
                    var swing = Math.Sin(Environment.TickCount / 600.0 + i) * 40;
                    a.Position = new Point(
                        Lerp(a.Position.X, a.BaseX, 0.06),
                        Lerp(a.Position.Y, a.BaseY + swing, 0.06));
                }
                ClampActor(a);
                continue;
            }

            if (awayAttacking && i == BallOwnerAwayIndex)
            {
                double fastBreakMult = _awayFastBreakTicks > 0 ? FastBreakSpeedMultiplier : 1.0;

                if (Mode == GameMode.TwoPlayerLocal)
                {
                    // Player 2 direct control of away ball carrier
                    bool hasManualInput2 = Math.Abs(AwayActiveMoveInput.X) > 5 || Math.Abs(AwayActiveMoveInput.Y) > 5 || _awayAdvanceBoost2;
                    if (hasManualInput2)
                    {
                        double forwardExtra = _awayAdvanceBoost2 ? -220 : 0; // negative = attacking left
                        var nextPos = new Point(
                            a.Position.X + (AwayActiveMoveInput.X + forwardExtra) * dt * fastBreakMult,
                            a.Position.Y + AwayActiveMoveInput.Y * dt * fastBreakMult);
                        a.Position = nextPos;
                    }
                    // else: hold position

                    // In two-player mode, shooting is manual — no auto-shoot at attack stop line

                    if (IsInsideLeftGoalArea(a.Position))
                    {
                        GiveBallToPlayer(GetNearestHomeIndex(a.Position), "Målgård: motståndarboll");
                        ClampActor(a);
                        return;
                    }
                }
                else if (_awayBreakthrough)
                {
                    // Breaking through: charge toward goal area with varied angles
                    double attackStopX = GoalCenterInset + GoalAreaRadius + 30;
                    double newX = a.Position.X;
                    if (a.Position.X > attackStopX)
                        newX -= 160 * dt * fastBreakMult;
                    // Varied breakthrough angles: wings go wide, backs cut inside
                    bool isWing = (i == 1 || i == 6); // top/bottom wing
                    double targetY;
                    if (isWing)
                    {
                        // Wing attacks from wide angle
                        double wingY = i == 1 ? ViewSize.Height * 0.2 : ViewSize.Height * 0.8;
                        targetY = Lerp(wingY, ViewSize.Height / 2, 0.3);
                    }
                    else
                    {
                        // Backs cut toward center with slight offset
                        double offset = (a.Position.Y > ViewSize.Height / 2 ? 40 : -40);
                        targetY = ViewSize.Height / 2 + offset;
                    }
                    double newY = Lerp(a.Position.Y, targetY, 0.08);
                    a.Position = new Point(newX, newY);

                    var awayShootLineX = attackStopX + 4;
                    if (!_awayShootActive && a.Position.X <= awayShootLineX)
                        StartAwayShoot(a.Position);
                }
                else
                {
                    // Build-up: push forward first, then hold arc position
                    var arcPos = GetArcPosition(i, arcCenterX, arcRadius);
                    if (a.Position.X > arcPos.X + AwayPushForwardThreshold)
                    {
                        // Push forward toward the goal before settling into arc
                        double pushSpeed = 120 * dt * fastBreakMult;
                        double pushTargetY = Lerp(a.Position.Y, arcPos.Y, 0.04);
                        a.Position = new Point(
                            a.Position.X - pushSpeed,
                            pushTargetY);
                    }
                    else
                    {
                        // Near arc: hold position and move laterally
                        a.Position = new Point(
                            Lerp(a.Position.X, arcPos.X, 0.06),
                            Lerp(a.Position.Y, arcPos.Y, 0.06));
                    }

                    // Pass or breakthrough decision (only when near arc) — difficulty adjusted
                    if (!_awayPassActive && _awayPassCooldownTicks == 0 && a.Position.X <= arcPos.X + AwayPushForwardThreshold)
                    {
                        // After enough passes, chance to break through
                        double breakChance = _awayBuildupPasses >= 3 ? 0.015 * _diffBreakthroughMult : 0.0;
                        if (_awayBuildupPasses >= 6) breakChance = 0.04 * _diffBreakthroughMult;
                        // Fast break: higher breakthrough chance when on fast break
                        if (_awayFastBreakTicks > 0) breakChance = 0.08 * _diffBreakthroughMult;

                        if (Random.Shared.NextDouble() < breakChance)
                        {
                            _awayBreakthrough = true;
                            if (_awayFastBreakTicks > 0)
                                SetStatusOverride("Kontring!", 50);
                            else
                                SetStatusOverride("Genombrott!", 50);
                        }
                        else
                        {
                            StartAwayPass(i);
                        }
                    }
                }
            }
            else
            {
                // Support: hold arc formation position, shift toward ball carrier
                var arcPos = GetArcPosition(i, arcCenterX, arcRadius);
                double shiftY = 0;
                if (BallOwnerAwayIndex >= 0)
                {
                    var carrierY = AwayPlayers[BallOwnerAwayIndex].Position.Y;
                    shiftY = (carrierY - arcPos.Y) * 0.15;
                }
                a.Position = new Point(
                    Lerp(a.Position.X, arcPos.X, 0.05),
                    Lerp(a.Position.Y, arcPos.Y + shiftY, 0.05));
            }
            ClampActor(a);
        }

        // Tackle cooldown tick
        if (_tackleCooldownTicks > 0) _tackleCooldownTicks--;

        // Defensive tackle: home defenders can stop the away ball carrier
        if (BallOwnerType == BallOwnershipType.Opponent && BallOwnerAwayIndex >= 1 && _tackleCooldownTicks == 0)
            TryHandleDefensiveTackle();
    }

    void UpdateBall(double dt)
    {
        if (_resettingAfterGoal) return;

        if (_awayPassActive && _awayPassTargetIndex >= 0)
        {
            var target = AwayPlayers[_awayPassTargetIndex].Position;
            BallPos = LerpPoint(BallPos, target, 0.32);

            // Home defenders can intercept away passes — difficulty adjusted
            for (int i = 1; i < HomePlayers.Length; i++)
            {
                if (HomePlayers[i].IsSuspended) continue;
                double interceptDist = Distance(BallPos, HomePlayers[i].Position);
                if (interceptDist < PassInterceptDistance)
                {
                    double chance = PassInterceptChance;
                    if (IsHomeDefending && i == ControlledDefenderIndex)
                        chance += ControlledDefenderInterceptBonus;
                    // Difficulty: easier = lower chance for AI passes to be intercepted? No — this is
                    // the player intercepting AI passes, so easier = higher chance
                    chance += _diffTackleStealBonus * 0.5;
                    if (Random.Shared.NextDouble() < chance)
                    {
                        _awayPassActive = false;
                        _awayPassTargetIndex = -1;
                        GameEvent?.Invoke(GameEventType.Interception);
                        GiveBallToPlayer(i, "Passbrytning!");
                        return;
                    }
                }
            }

            if (Distance(BallPos, target) < 14)
            {
                BallOwnerType = BallOwnershipType.Opponent;
                BallOwnerAwayIndex = _awayPassTargetIndex;
                ControlledAwayAttackerIndex = _awayPassTargetIndex;
                BallOwnerPlayerIndex = -1;
                _awayPassActive = false;
            }
            return;
        }

        if (_awayShootActive)
        {
            _awayShootTime += (float)dt;
            var t = Math.Clamp(_awayShootTime / 0.55f, 0f, 1f);
            var ease = t * t * (3 - 2 * t);
            BallPos = LerpPoint(_awayShootStart, _awayShootEnd, ease);
            if (t >= 1f)
            {
                _awayShootActive = false;
                bool onTarget = _leftGoal.Contains(BallPos);

                // GK save check FIRST — difficulty adjusted
                var homeKeeper = HomePlayers[0];
                double awayShotDist = Distance(_awayShootStart, _awayShootEnd);
                double gkDist = DistanceToSegment(homeKeeper.Position, _awayShootStart, _awayShootEnd);
                if (gkDist < GoalkeeperSaveRadius)
                {
                    double distFactor = Math.Clamp(awayShotDist / MaxShotDistance, 0, 1);
                    double saveChance = OnTargetSaveBase + distFactor * OnTargetDistBonus + _diffHomeGkBonus;
                    saveChance -= (awayShotDist < 200 ? OnTargetCloseRangePenalty : 0);
                    double posBonus = Math.Clamp(1.0 - gkDist / GoalkeeperSaveRadius, 0, 1) * OnTargetPositionBonus;
                    saveChance += posBonus;
                    if (Random.Shared.NextDouble() < saveChance)
                    {
                        GameEvent?.Invoke(GameEventType.Save);
                        SavesHome++;
                        BallPos = homeKeeper.Position;
                        GiveBallToPlayer(0, "Målvaktsräddning!");
                        return;
                    }
                }

                var interceptor = TryGetInterception(HomePlayers, 1, _awayShootStart, _awayShootEnd, ShotInterceptChance);
                if (interceptor >= 1)
                {
                    GameEvent?.Invoke(GameEventType.Interception);
                    GiveBallToPlayer(interceptor, "Brytning av försvarare!");
                    return;
                }

                if (onTarget)
                {
                    ScoreAway++;
                    SetStatusOverride("Borta gör mål!", 120);
                    GameEvent?.Invoke(GameEventType.GoalAway);
                    ResetAfterScore(homeScored: false);
                }
                else
                {
                    GiveBallToPlayer(GetNearestHomeIndex(BallPos), "Skott utanför");
                }
            }
            return;
        }

        if (_shootActive)
        {
            _shootTime += (float)dt;
            var t = Math.Clamp(_shootTime / 0.6f, 0f, 1f);
            var ease = t * t * (3 - 2 * t);
            BallPos = LerpPoint(_shootStart, _shootEnd, ease);
            if (t >= 1f)
            {
                _shootActive = false;
                bool onTarget = _rightGoal.Contains(BallPos);

                // Away GK save check — difficulty adjusted
                var awayKeeper = AwayPlayers[0];
                double homeShotDist = Distance(_shootStart, _shootEnd);
                double gkDist = DistanceToSegment(awayKeeper.Position, _shootStart, _shootEnd);
                if (gkDist < GoalkeeperSaveRadius)
                {
                    double distFactor = Math.Clamp(homeShotDist / MaxShotDistance, 0, 1);
                    double saveChance = OnTargetSaveBase + distFactor * OnTargetDistBonus + _diffAwayGkBonus;
                    saveChance -= (homeShotDist < 200 ? OnTargetCloseRangePenalty : 0);
                    double posBonus = Math.Clamp(1.0 - gkDist / GoalkeeperSaveRadius, 0, 1) * OnTargetPositionBonus;
                    saveChance += posBonus;
                    if (Random.Shared.NextDouble() < saveChance)
                    {
                        GameEvent?.Invoke(GameEventType.Save);
                        SavesAway++;
                        BallPos = awayKeeper.Position;
                        GiveBallToOpponent(0, "Målvaktsräddning");
                        _keeperHoldTicks = 40;
                        return;
                    }
                }

                var interceptor = TryGetInterception(AwayPlayers, 1, _shootStart, _shootEnd, ShotInterceptChance);
                if (interceptor >= 1)
                {
                    GameEvent?.Invoke(GameEventType.Interception);
                    GiveBallToOpponent(interceptor, "Brytning av försvarare");
                    return;
                }

                if (onTarget)
                {
                    ScoreHome++;
                    SetStatusOverride("MÅL! ðŸŽ‰", 120);
                    GameEvent?.Invoke(GameEventType.GoalHome);
                    ResetAfterScore(homeScored: true);
                }
                else
                {
                    GiveBallToOpponent(GetNearestAwayIndex(BallPos), "Skott utanför");
                }
            }
            return;
        }

        if (_passActive)
        {
            var target = HomePlayers[_passTargetHomeIndex].Position;
            BallPos = LerpPoint(BallPos, target, 0.35);

            // Away defenders can intercept home passes — difficulty adjusted
            for (int i = 1; i < AwayPlayers.Length; i++)
            {
                if (AwayPlayers[i].IsSuspended) continue;
                double interceptDist = Distance(BallPos, AwayPlayers[i].Position);
                if (interceptDist < PassInterceptDistance)
                {
                    double chance = PassInterceptChance * _diffInterceptMult;
                    if (interceptDist < PassInterceptDistance * 0.6)
                        chance += 0.08 * _diffInterceptMult;
                    if (Random.Shared.NextDouble() < chance)
                    {
                        _passActive = false;
                        _passTargetHomeIndex = -1;
                        GameEvent?.Invoke(GameEventType.Interception);
                        GiveBallToOpponent(i, "Passbrytning!");
                        return;
                    }
                }
            }

            if (Distance(BallPos, target) < 14)
            {
                BallOwnerType = BallOwnershipType.Player;
                BallOwnerPlayerIndex = _passTargetHomeIndex;
                _passActive = false;
            }
            return;
        }

        if (BallOwnerType == BallOwnershipType.Player && BallOwnerPlayerIndex >= 0)
        {
            BallPos = LerpPoint(BallPos, HomePlayers[BallOwnerPlayerIndex].Position, 0.25);
        }
        else if (BallOwnerType == BallOwnershipType.Opponent && BallOwnerAwayIndex >= 0)
        {
            BallPos = LerpPoint(BallPos, AwayPlayers[BallOwnerAwayIndex].Position, 0.25);
        }
        else if (BallOwnerType == BallOwnershipType.Loose)
        {
            // Both teams compete for loose ball
            Actor? nearestHome = null; double bestHomeDist = double.MaxValue;
            int nearestHomeIdx = -1;
            for (int i = 1; i < HomePlayers.Length; i++)
            {
                if (HomePlayers[i].IsSuspended) continue;
                var d = Distance(BallPos, HomePlayers[i].Position);
                if (d < bestHomeDist) { bestHomeDist = d; nearestHome = HomePlayers[i]; nearestHomeIdx = i; }
            }

            Actor? nearestAway = null; double bestAwayDist = double.MaxValue;
            int nearestAwayIdx = -1;
            for (int i = 1; i < AwayPlayers.Length; i++)
            {
                if (AwayPlayers[i].IsSuspended) continue;
                var d = Distance(BallPos, AwayPlayers[i].Position);
                if (d < bestAwayDist) { bestAwayDist = d; nearestAway = AwayPlayers[i]; nearestAwayIdx = i; }
            }

            // Move ball toward whichever player is closer
            double pickupDist = 18;
            if (nearestHome != null && bestHomeDist <= bestAwayDist)
            {
                BallPos = LerpPoint(BallPos, nearestHome.Position, 0.08);
                if (bestHomeDist < pickupDist)
                {
                    BallOwnerType = BallOwnershipType.Player;
                    BallOwnerPlayerIndex = nearestHomeIdx;
                }
            }
            else if (nearestAway != null)
            {
                BallPos = LerpPoint(BallPos, nearestAway.Position, 0.08);
                if (bestAwayDist < pickupDist)
                {
                    BallOwnerType = BallOwnershipType.Opponent;
                    BallOwnerAwayIndex = nearestAwayIdx;
                    BallOwnerPlayerIndex = -1;
                    _awayPassCooldownTicks = _diffPassCooldownBase;
                }
            }
        }
    }

    void ResetAfterScore(bool homeScored)
    {
        _goalCelebrationTicks = 75;
        GoalCelebrationText = homeScored ? "MÅL! ðŸŽ‰" : "Motståndarenmål!";

        // Spawn confetti from the goal area
        SpawnConfetti(homeScored);

        double homeFieldBase = GoalCenterInset + GoalAreaRadius + 30;
        double awayFieldBase = ViewSize.Width > 0 ? ViewSize.Width - GoalCenterInset - GoalAreaRadius - 30 : 700;
        SetTeamBasePositions(HomePlayers, homeFieldBase, true);
        SetTeamBasePositions(AwayPlayers, awayFieldBase, false);
        ControlledDefenderIndex = 1;
        ClearAllActiveActions();
        _viewInitialized = true;
        _resettingAfterGoal = true;
        _resetCountdown = 150;
        _possessionTimer = 0;
        PassivePlayWarningActive = false;

        double throwOffCenterX = ViewSize.Width > 0 ? ViewSize.Width / 2 : 350;
        double throwOffCenterY = ViewSize.Height > 0 ? ViewSize.Height / 2 : 300;
        if (homeScored)
        {
            BallOwnerType = BallOwnershipType.Opponent;
            BallOwnerAwayIndex = ThrowOffCarrierIndex;
            BallOwnerPlayerIndex = -1;
            _awayBuildupPasses = 0;
            _awayBreakthrough = false;
            _awayPassCooldownTicks = 50;
            PositionForThrowOff(AwayPlayers[ThrowOffCarrierIndex], throwOffCenterX, throwOffCenterY);
            for (int i = 1; i < AwayPlayers.Length; i++)
            {
                if (i == ThrowOffCarrierIndex) continue;
                double staggerX = ViewSize.Width > 0 ? ViewSize.Width * 0.6 + i * 15 : 500;
                AwayPlayers[i].BaseX = Math.Min(staggerX, AwayPlayers[i].BaseX);
            }
        }
        else
        {
            BallOwnerType = BallOwnershipType.Player;
            BallOwnerPlayerIndex = ThrowOffCarrierIndex;
            BallOwnerAwayIndex = -1;
            PositionForThrowOff(HomePlayers[ThrowOffCarrierIndex], throwOffCenterX, throwOffCenterY);
            for (int i = 1; i < HomePlayers.Length; i++)
            {
                if (i == ThrowOffCarrierIndex) continue;
                double staggerX = ViewSize.Width > 0 ? ViewSize.Width * 0.4 - i * 15 : 200;
                HomePlayers[i].BaseX = Math.Max(staggerX, HomePlayers[i].BaseX);
            }
        }
        SetStatusOverride("Avkast", 75);
    }

    void UpdateStatus()
    {
        if (_statusOverrideTicks > 0)
        {
            _statusOverrideTicks--;
            StatusText = _statusOverrideText;
            return;
        }

        if (IsMatchOver) { StatusText = ScoreHome > ScoreAway ? "Seger!" : ScoreHome < ScoreAway ? "Förlust" : "Oavgjort"; return; }
        if (IsHalfTime) { StatusText = "HALVTID"; return; }
        if (_penaltyActive) { StatusText = _penaltyIsHome ? "Straffskytte - Hemma" : "Straffskytte - Borta"; return; }
        if (_shootActive) { StatusText = "Skott!"; return; }
        if (_awayShootActive) { StatusText = "Motståndaren skjuter!"; return; }
        if (_passActive) { StatusText = "Pass i luften"; return; }
        if (PassivePlayWarningActive) { StatusText = "⚠ Passivt spel - skjut!"; return; }

        string fastBreakIndicator = _homeFastBreakTicks > 0 ? " ⚡ Kontring!" : "";
        StatusText = BallOwnerType switch
        {
            BallOwnershipType.Player => $"Boll: Hemma #{BallOwnerPlayerIndex}{fastBreakIndicator}",
            BallOwnershipType.Opponent => $"Försvarar med #{ControlledDefenderIndex} | Borta #{BallOwnerAwayIndex}" + (_awayFastBreakTicks > 0 ? " ⚡" : ""),
            _ => "Lös boll"
        };
    }

    void ClampActor(Actor a)
    {
        if (ViewSize.Width <= 0 || ViewSize.Height <= 0) return;
        // Keep players inside the court boundaries (fieldMargin from each edge, accounting for player radius)
        const double PlayerRadius = 14;
        a.Position = new Point(
            Math.Clamp(a.Position.X, FieldMargin + PlayerRadius, ViewSize.Width - FieldMargin - PlayerRadius),
            Math.Clamp(a.Position.Y, FieldMargin + PlayerRadius, ViewSize.Height - FieldMargin - PlayerRadius));

        if (a.IsGoalkeeper)
        {
            // Keep goalkeeper vertically within the goal-mouth range
            double centerY = ViewSize.Height / 2;
            a.Position = new Point(a.Position.X, Math.Clamp(a.Position.Y, centerY - GoalMouthHalf, centerY + GoalMouthHalf));
        }
        else
        {
            var leftCenter = new Point(GoalCenterInset, ViewSize.Height / 2);
            var rightCenter = new Point(ViewSize.Width - GoalCenterInset, ViewSize.Height / 2);
            a.Position = PushOutsideGoalArea(a.Position, leftCenter, GoalAreaRadius + 2);
            a.Position = PushOutsideGoalArea(a.Position, rightCenter, GoalAreaRadius + 2);
        }
    }

    static double Distance(Point a, Point b) => Math.Sqrt((a.X - b.X)*(a.X - b.X) + (a.Y - b.Y)*(a.Y - b.Y));
    static Point LerpPoint(Point a, Point b, double t) => new(a.X + (b.X - a.X)*t, a.Y + (b.Y - a.Y)*t);
    static double Lerp(double a, double b, double t) => a + (b - a) * t;

    void SetStatusOverride(string text, int ticks = 90)
    {
        _statusOverrideText = text;
        _statusOverrideTicks = ticks;
    }

    int GetNearestAwayIndex(Point position)
    {
        int bestIndex = 1;
        double best = Distance(position, AwayPlayers[bestIndex].Position);
        for (int i = 1; i < AwayPlayers.Length; i++)
        {
            var d = Distance(position, AwayPlayers[i].Position);
            if (d < best)
            {
                best = d;
                bestIndex = i;
            }
        }
        return bestIndex;
    }

    int GetNearestHomeIndex(Point position)
    {
        int bestIndex = 1;
        double best = Distance(position, HomePlayers[bestIndex].Position);
        for (int i = 1; i < HomePlayers.Length; i++)
        {
            var d = Distance(position, HomePlayers[i].Position);
            if (d < best)
            {
                best = d;
                bestIndex = i;
            }
        }
        return bestIndex;
    }

    void GiveBallToOpponent(int awayIndex, string reason)
    {
        BallOwnerType = BallOwnershipType.Opponent;
        BallOwnerAwayIndex = awayIndex;
        ControlledAwayAttackerIndex = awayIndex;
        BallOwnerPlayerIndex = -1;
        _advanceBoost = false;
        _passActive = false;
        _awayPassActive = false;
        _awayPassTargetIndex = -1;
        _awayBuildupPasses = 0;
        _awayBreakthrough = false;
        _awayPassCooldownTicks = _diffPassCooldownBase + 10;
        _attackDiagonalBoostY = 0;
        _possessionTimer = 0;
        PassivePlayWarningActive = false;
        // Fast break for away team on turnover
        _awayFastBreakTicks = FastBreakDurationTicks;
        _homeFastBreakTicks = 0;
        // Auto-switch to nearest non-suspended defender
        AutoSwitchDefender(AwayPlayers[awayIndex].Position);
        SetStatusOverride(reason);
        // Reset smoothed press line for opponent fast break
        if (ViewSize.Width > 0)
            _smoothedPressLineX = ViewSize.Width * 0.5;
    }

    void GiveBallToPlayer(int homeIndex, string reason)
    {
        BallOwnerType = BallOwnershipType.Player;
        BallOwnerPlayerIndex = homeIndex;
        BallOwnerAwayIndex = -1;
        _advanceBoost = false;
        _awayAdvanceBoost2 = false;
        _passActive = false;
        _awayPassActive = false;
        _awayPassTargetIndex = -1;
        _awayBuildupPasses = 0;
        _awayBreakthrough = false;
        _defenderSideBoostY = 0;
        _possessionTimer = 0;
        PassivePlayWarningActive = false;
        // Fast break for home team on turnover
        _homeFastBreakTicks = FastBreakDurationTicks;
        _awayFastBreakTicks = 0;
        SetStatusOverride(reason);
        // Reset smoothed press line so ball carrier doesn't immediately push backward
        if (ViewSize.Width > 0)
            _smoothedPressLineX = ViewSize.Width * 0.5;
    }

    void AutoSwitchDefender(Point ballCarrierPos)
    {
        int bestIdx = -1;
        double bestDist = double.MaxValue;
        for (int i = 1; i < HomePlayers.Length; i++)
        {
            if (HomePlayers[i].IsSuspended) continue;
            var d = Distance(HomePlayers[i].Position, ballCarrierPos);
            if (d < bestDist) { bestDist = d; bestIdx = i; }
        }
        if (bestIdx > 0)
            ControlledDefenderIndex = bestIdx;
    }

    void StartAwayPass(int ownerIndex)
    {
        var candidates = Enumerable.Range(1, AwayPlayers.Length - 1).Where(i => i != ownerIndex).ToArray();
        if (candidates.Length == 0) return;

        // Prefer closest teammates with a small chance to pick a further player for variety
        var ownerPos = AwayPlayers[ownerIndex].Position;
        var sorted = candidates.OrderBy(i => Distance(ownerPos, AwayPlayers[i].Position)).ToArray();
        double roll = Random.Shared.NextDouble();
        if (roll < AwayPassClosestChance || sorted.Length == 1)
            _awayPassTargetIndex = sorted[0];
        else if (roll < AwayPassSecondClosestChance && sorted.Length >= 2)
            _awayPassTargetIndex = sorted[1];
        else
            _awayPassTargetIndex = sorted[Random.Shared.Next(sorted.Length)];
        _awayPassActive = true;
        _awayPassCooldownTicks = _diffPassCooldownBase + Random.Shared.Next(_diffPassCooldownRange);
        _awayBuildupPasses++;
        BallOwnerType = BallOwnershipType.Loose;
        BallOwnerAwayIndex = -1;
        BallOwnerPlayerIndex = -1;
        PassesAway++;
        GameEvent?.Invoke(GameEventType.AwayPass);
    }

    /// <summary>
    /// Returns the attacking arc position for field player index i (1-6).
    /// Positions are spread in a semicircle to the left of the home goal.
    /// </summary>
    Point GetArcPosition(int playerIndex, double arcCenterX, double arcRadius)
    {
        int fieldCount = AwayPlayers.Length - 1; // exclude GK
        int slot = playerIndex - 1; // 0-based field slot
        // Spread from ~-70° to +70° (top to bottom)
        double angleRange = 160.0;
        double startAngle = -angleRange / 2;
        double angleDeg = startAngle + slot * (angleRange / (fieldCount - 1));
        double angleRad = angleDeg * Math.PI / 180.0;

        double x = arcCenterX + arcRadius * Math.Cos(angleRad);
        double y = ViewSize.Height / 2 + arcRadius * Math.Sin(angleRad);

        // Add subtle lateral drift for realism
        double drift = Math.Sin(Environment.TickCount / 800.0 + playerIndex * 1.5) * 18;
        return new Point(x, y + drift);
    }

    /// <summary>
    /// Returns the defensive arc position for home field player at the given slot.
    /// Defenders form a semicircle just outside the goal area, shifted toward the ball.
    /// </summary>
    Point GetDefensiveArcPosition(int fieldIndex, int fieldCount)
    {
        double defArcCenterX = GoalCenterInset;
        double defArcRadius = GoalAreaRadius + 25;

        // Shift arc center toward ball carrier vertically
        double arcCenterY = ViewSize.Height > 0 ? ViewSize.Height / 2 : 300;
        arcCenterY += (BallPos.Y - arcCenterY) * 0.3;

        double angleRange = 120.0;
        double startAngle = -angleRange / 2;
        double angleDeg = startAngle + fieldIndex * (angleRange / Math.Max(fieldCount - 1, 1));
        double angleRad = angleDeg * Math.PI / 180.0;

        double x = defArcCenterX + defArcRadius * Math.Cos(angleRad);
        double y = arcCenterY + defArcRadius * Math.Sin(angleRad);

        // Subtle defensive sway
        double drift = Math.Sin(Environment.TickCount / 600.0 + fieldIndex * 1.2) * 12;
        return new Point(x, y + drift);
    }

    void StartAwayShoot(Point from)
    {
        _awayShootActive = true;
        _awayShootTime = 0f;
        _awayShootStart = from;
        // Wider shot spread matching home team (±75px)
        var shootOffsetY = (Random.Shared.NextDouble() - 0.5) * 150;
        _awayShootEnd = new Point(14, ViewSize.Height / 2 + shootOffsetY);
        _awayPassActive = false;
        BallOwnerType = BallOwnershipType.Loose;
        BallOwnerAwayIndex = -1;
        BallOwnerPlayerIndex = -1;
        ShotsAway++;
        SetStatusOverride("Motståndaren skjuter!", 60);
        GameEvent?.Invoke(GameEventType.AwayShoot);
    }

    int TryGetInterception(Actor[] team, int startIndex, Point from, Point to, double chance)
    {
        for (int i = startIndex; i < team.Length; i++)
        {
            if (team[i].IsSuspended) continue;
            var d = DistanceToSegment(team[i].Position, from, to);
            if (d < 20 && Random.Shared.NextDouble() < chance)
                return i;
        }
        return -1;
    }

    bool IsShotNearKeeperLine(Point from, Point to, Point keeperPos) => DistanceToSegment(keeperPos, from, to) < GoalkeeperSaveRadius;

    static double DistanceToSegment(Point p, Point a, Point b)
    {
        var abx = b.X - a.X;
        var aby = b.Y - a.Y;
        var apx = p.X - a.X;
        var apy = p.Y - a.Y;
        var ab2 = abx * abx + aby * aby;
        if (ab2 <= 0.0001) return Distance(p, a);
        var t = Math.Clamp((apx * abx + apy * aby) / ab2, 0, 1);
        var cx = a.X + t * abx;
        var cy = a.Y + t * aby;
        return Math.Sqrt((p.X - cx) * (p.X - cx) + (p.Y - cy) * (p.Y - cy));
    }

    bool IsInsideRightGoalArea(Point p)
    {
        if (ViewSize.Width <= 0 || ViewSize.Height <= 0) return false;
        var center = new Point(ViewSize.Width - GoalCenterInset, ViewSize.Height / 2);
        return Distance(p, center) < GoalAreaRadius;
    }

    bool IsInsideLeftGoalArea(Point p)
    {
        if (ViewSize.Width <= 0 || ViewSize.Height <= 0) return false;
        var center = new Point(GoalCenterInset, ViewSize.Height / 2);
        return Distance(p, center) < GoalAreaRadius;
    }

    bool TryHandleFrontalDefense(Actor owner)
    {
        const double collisionRadius = 18;
        for (int i = 1; i < AwayPlayers.Length; i++)
        {
            var defender = AwayPlayers[i];
            var dist = Distance(owner.Position, defender.Position);
            if (dist < collisionRadius)
            {
                // Push attacker back out of collision
                if (dist > 0.001)
                {
                    var pushDx = owner.Position.X - defender.Position.X;
                    var pushDy = owner.Position.Y - defender.Position.Y;
                    var s = collisionRadius / dist;
                    owner.Position = new Point(defender.Position.X + pushDx * s, defender.Position.Y + pushDy * s);
                    ClampActor(owner);
                    BallPos = owner.Position;
                }

                // Check if foul is near goal area → 7-meter penalty
                var rightGoalCenter = new Point(ViewSize.Width - GoalCenterInset, ViewSize.Height / 2);
                double distToGoalArea = Distance(owner.Position, rightGoalCenter) - GoalAreaRadius;
                bool nearGoalArea = distToGoalArea < PenaltyFoulZoneRadius && distToGoalArea >= 0;

                if (nearGoalArea && Random.Shared.NextDouble() < PenaltyAwardChance)
                {
                    if (Random.Shared.NextDouble() < SuspensionChance)
                    {
                        defender.SuspensionTicks = SuspensionDurationTicks;
                        SetStatusOverride("7-meterstraff + 2 min utvisning!", 120);
                        GameEvent?.Invoke(GameEventType.Suspension);
                    }
                    StartPenalty(isHome: true);
                    return true;
                }

                // Clear all boost states on collision
                _advanceBoost = false;
                _attackDiagonalBoostY = 0;

                if (Random.Shared.NextDouble() < SuspensionChance)
                {
                    defender.SuspensionTicks = SuspensionDurationTicks;
                    // Free throw at foul position, but no closer than the 9m line
                    var freeThrowLineX = Math.Max(40, ViewSize.Width - GoalCenterInset - FreeThrowRadius - 8);
                    var freeThrowX = Math.Min(owner.Position.X, freeThrowLineX);
                    owner.Position = new Point(freeThrowX, Math.Clamp(owner.Position.Y, 70, ViewSize.Height - 70));
                    BallPos = owner.Position;
                    SetStatusOverride("2 min utvisning + frikast!", 120);
                    GameEvent?.Invoke(GameEventType.Suspension);
                    return true;
                }

                if (Random.Shared.NextDouble() < 0.30)
                {
                    // Free throw at foul position, but no closer than the 9m line
                    var freeThrowLineX = Math.Max(40, ViewSize.Width - GoalCenterInset - FreeThrowRadius - 8);
                    var freeThrowX = Math.Min(owner.Position.X, freeThrowLineX);
                    owner.Position = new Point(freeThrowX, Math.Clamp(owner.Position.Y, 70, ViewSize.Height - 70));
                    BallPos = owner.Position;
                    SetStatusOverride("Frikast - börja om utanför");
                }
                else
                {
                    GiveBallToOpponent(i, "Tappad boll: motståndarboll");
                }
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Checks if any home field player is close enough to the away ball carrier
    /// to attempt a tackle. Outcomes: clean steal, foul (free throw), or push.
    /// </summary>
    bool TryHandleDefensiveTackle()
    {
        if (BallOwnerType != BallOwnershipType.Opponent || BallOwnerAwayIndex < 1) return false;
        var carrier = AwayPlayers[BallOwnerAwayIndex];

        for (int i = 1; i < HomePlayers.Length; i++)
        {
            if (HomePlayers[i].IsSuspended) continue;
            var dist = Distance(HomePlayers[i].Position, carrier.Position);
            if (dist >= TackleDistance) continue;

            _tackleCooldownTicks = TackleCooldownDuration;

            var leftGoalCenter = new Point(GoalCenterInset, ViewSize.Height / 2);
            double distToGoalArea = Distance(carrier.Position, leftGoalCenter) - GoalAreaRadius;
            bool nearGoalArea = distToGoalArea < PenaltyFoulZoneRadius && distToGoalArea >= 0;

            if (nearGoalArea && Random.Shared.NextDouble() < PenaltyAwardChance)
            {
                if (Random.Shared.NextDouble() < SuspensionChance)
                {
                    HomePlayers[i].SuspensionTicks = SuspensionDurationTicks;
                    SetStatusOverride("7m + 2 min utvisning!", 120);
                    GameEvent?.Invoke(GameEventType.Suspension);
                }
                StartPenalty(isHome: false);
                return true;
            }

            double roll = Random.Shared.NextDouble();
            double stealChance = TackleStealChance + _diffTackleStealBonus;
            if (i == ControlledDefenderIndex) stealChance += 0.12;

            if (roll < stealChance)
            {
                GameEvent?.Invoke(GameEventType.Interception);
                GiveBallToPlayer(i, "Boll vunnen!");
                return true;
            }
            else if (roll < stealChance + TackleFoulChance)
            {
                if (Random.Shared.NextDouble() < SuspensionChance)
                {
                    HomePlayers[i].SuspensionTicks = SuspensionDurationTicks;
                    SetStatusOverride("2 min utvisning + frikast!", 120);
                    GameEvent?.Invoke(GameEventType.Suspension);
                }
                else
                {
                    SetStatusOverride("Frikast - motståndarboll");
                    GameEvent?.Invoke(GameEventType.Whistle);
                }
                var freeThrowX = GoalCenterInset + FreeThrowRadius + 8;
                carrier.Position = new Point(Math.Max(freeThrowX, carrier.Position.X),
                    Math.Clamp(carrier.Position.Y, 70, ViewSize.Height - 70));
                BallPos = carrier.Position;
                _awayPassCooldownTicks = 50;
                _awayBreakthrough = false;
                return true;
            }
            else
            {
                if (dist > 0.001)
                {
                    var pushDx = carrier.Position.X - HomePlayers[i].Position.X;
                    var pushDy = carrier.Position.Y - HomePlayers[i].Position.Y;
                    var s = TackleDistance / dist;
                    carrier.Position = new Point(
                        HomePlayers[i].Position.X + pushDx * s,
                        HomePlayers[i].Position.Y + pushDy * s);
                    ClampActor(carrier);
                    BallPos = carrier.Position;
                }
                return true;
            }
        }
        return false;
    }

    static Point PushOutsideGoalArea(Point p, Point center, double minRadius)
    {
        var dx = p.X - center.X;
        var dy = p.Y - center.Y;
        var dist = Math.Sqrt(dx * dx + dy * dy);
        if (dist >= minRadius) return p;

        if (dist < 0.001)
            return new Point(center.X + minRadius, center.Y);

        var s = minRadius / dist;
        return new Point(center.X + dx * s, center.Y + dy * s);
    }

    // ── Helper methods ──

    void ClearAllActiveActions()
    {
        _passActive = false;
        _shootActive = false;
        _awayShootActive = false;
        _awayPassActive = false;
        _awayPassTargetIndex = -1;
        _awayBuildupPasses = 0;
        _awayBreakthrough = false;
        _retreatingFormerOwner = false;
        _formerOwnerIndex = -1;
        _defenderSideBoostY = 0;
        _defenderDiagBoostY = 0;
        _attackDiagonalBoostY = 0;
        _advanceBoost = false;
        _defenderAdvanceBoost = false;
        _penaltyActive = false;
        _possessionTimer = 0;
        PassivePlayWarningActive = false;
        BallHeight = 0;
        _keeperHoldTicks = 0;
        _tackleCooldownTicks = 0;
    }

    /// <summary>Sets a player's base position to center court for a throw-off.</summary>
    static void PositionForThrowOff(Actor player, double centerX, double centerY)
    {
        player.BaseX = centerX;
        player.BaseY = centerY;
    }

    void StartSecondHalf()
    {
        CurrentHalf = 2;
        MatchClockSeconds = 0;

        // Center throw-off for second half: away team starts
        double homeFieldBase = GoalCenterInset + GoalAreaRadius + 30;
        double awayFieldBase = ViewSize.Width > 0 ? ViewSize.Width - GoalCenterInset - GoalAreaRadius - 30 : 700;
        SetTeamBasePositions(HomePlayers, homeFieldBase, true);
        SetTeamBasePositions(AwayPlayers, awayFieldBase, false);

        // Place players at their base positions
        foreach (var team in new[] { HomePlayers, AwayPlayers })
            foreach (var a in team)
                a.Position = new Point(a.BaseX, a.BaseY);

        BallOwnerType = BallOwnershipType.Opponent;
        BallOwnerAwayIndex = ThrowOffCarrierIndex;
        BallOwnerPlayerIndex = -1;
        ControlledDefenderIndex = 1;
        ClearAllActiveActions();
        _awayPassCooldownTicks = _diffPassCooldownBase + 10;
        _viewInitialized = true;
        // Throw-off: away team starts at center
        double centerX = ViewSize.Width > 0 ? ViewSize.Width / 2 : 350;
        double centerY = ViewSize.Height > 0 ? ViewSize.Height / 2 : 300;
        PositionForThrowOff(AwayPlayers[ThrowOffCarrierIndex], centerX, centerY);
        AwayPlayers[ThrowOffCarrierIndex].Position = new Point(centerX, centerY);
        BallPos = AwayPlayers[ThrowOffCarrierIndex].Position;
        SetStatusOverride("Avkast - Andra halvlek", 90);
    }

    /// <summary>Restart the match (e.g. after full time).</summary>
    public void RestartMatch()
    {
        ScoreHome = 0;
        ScoreAway = 0;
        CurrentHalf = 1;
        MatchClockSeconds = 0;
        IsHalfTime = false;
        IsMatchOver = false;
        _goalCelebrationTicks = 0;
        _homeFastBreakTicks = 0;
        _awayFastBreakTicks = 0;
        // Reset stats
        ShotsHome = 0; ShotsAway = 0;
        SavesHome = 0; SavesAway = 0;
        PassesHome = 0; PassesAway = 0;
        ConfettiCount = 0;

        double homeFieldBase = GoalCenterInset + GoalAreaRadius + 30;
        double awayFieldBase = ViewSize.Width > 0 ? ViewSize.Width - GoalCenterInset - GoalAreaRadius - 30 : 700;
        SetTeamBasePositions(HomePlayers, homeFieldBase, true);
        SetTeamBasePositions(AwayPlayers, awayFieldBase, false);
        foreach (var team in new[] { HomePlayers, AwayPlayers })
            foreach (var a in team)
            {
                a.Position = new Point(a.BaseX, a.BaseY);
                a.SuspensionTicks = 0;
            }

        BallOwnerType = BallOwnershipType.Player;
        BallOwnerPlayerIndex = ThrowOffCarrierIndex;
        BallOwnerAwayIndex = -1;
        ControlledDefenderIndex = 1;
        ClearAllActiveActions();
        _viewInitialized = true;
        // Center throw-off: home team starts at center
        double centerX = ViewSize.Width > 0 ? ViewSize.Width / 2 : 350;
        double centerY = ViewSize.Height > 0 ? ViewSize.Height / 2 : 300;
        PositionForThrowOff(HomePlayers[ThrowOffCarrierIndex], centerX, centerY);
        HomePlayers[ThrowOffCarrierIndex].Position = new Point(centerX, centerY);
        BallPos = HomePlayers[ThrowOffCarrierIndex].Position;
        SetStatusOverride("Avkast - Ny match!", 90);
    }

    void StartPenalty(bool isHome)
    {
        ClearAllActiveActions();
        _penaltyActive = true;
        _penaltyIsHome = isHome;
        _penaltyTime = 0f;

        double penaltyX, goalX;
        if (isHome)
        {
            // Home shoots at right goal
            penaltyX = ViewSize.Width - GoalCenterInset - GoalAreaRadius - 48;
            goalX = ViewSize.Width - 14;
            var shootOffsetY = (Random.Shared.NextDouble() - 0.5) * 140;
            _penaltyStart = new Point(penaltyX, ViewSize.Height / 2);
            _penaltyEnd = new Point(goalX, ViewSize.Height / 2 + shootOffsetY);
        }
        else
        {
            // Away shoots at left goal
            penaltyX = GoalCenterInset + GoalAreaRadius + 48;
            goalX = 14;
            var shootOffsetY = (Random.Shared.NextDouble() - 0.5) * 140;
            _penaltyStart = new Point(penaltyX, ViewSize.Height / 2);
            _penaltyEnd = new Point(goalX, ViewSize.Height / 2 + shootOffsetY);
        }

        BallPos = _penaltyStart;
        BallOwnerType = BallOwnershipType.Loose;
        BallOwnerPlayerIndex = -1;
        BallOwnerAwayIndex = -1;
        if (isHome) ShotsHome++; else ShotsAway++;
        SetStatusOverride(_penaltyIsHome ? "7-meterstraff! Hemma skjuter" : "7-meterstraff! Borta skjuter", 90);
        GameEvent?.Invoke(GameEventType.PenaltyAwarded);
        GameEvent?.Invoke(GameEventType.Whistle);
    }

    void UpdatePenalty(double dt)
    {
        _penaltyTime += (float)dt;

        // Short delay before shot fires
        if (_penaltyTime < 0.5f)
        {
            BallPos = _penaltyStart;
            return;
        }

        float shotProgress = (_penaltyTime - 0.5f) / (float)PenaltyShotDuration;
        shotProgress = Math.Clamp(shotProgress, 0f, 1f);
        var ease = shotProgress * shotProgress * (3 - 2 * shotProgress);
        BallPos = LerpPoint(_penaltyStart, _penaltyEnd, ease);
        BallHeight = Math.Sin(shotProgress * Math.PI) * 0.8;

        if (shotProgress >= 1f)
        {
            _penaltyActive = false;
            BallHeight = 0;

            if (_penaltyIsHome)
            {
                // Home shot at right goal
                if (_rightGoal.Contains(BallPos))
                {
                    bool keeperSave = Random.Shared.NextDouble() < PenaltySaveChance;
                    if (keeperSave)
                    {
                        GameEvent?.Invoke(GameEventType.Save);
                        SavesAway++;
                        BallPos = AwayPlayers[0].Position;
                        GiveBallToOpponent(0, "Straffskytte: Målvaktsräddning!");
                        _keeperHoldTicks = 40;
                    }
                    else
                    {
                        ScoreHome++;
                        SetStatusOverride("STRAFFMÅL! ðŸŽ‰", 120);
                        GameEvent?.Invoke(GameEventType.GoalHome);
                        ResetAfterScore(homeScored: true);
                    }
                }
                else
                {
                    GiveBallToOpponent(GetNearestAwayIndex(BallPos), "Straff missat");
                }
            }
            else
            {
                // Away shot at left goal
                if (_leftGoal.Contains(BallPos))
                {
                    bool keeperSave = Random.Shared.NextDouble() < PenaltySaveChance;
                    if (keeperSave)
                    {
                        GameEvent?.Invoke(GameEventType.Save);
                        SavesHome++;
                        BallPos = HomePlayers[0].Position;
                        GiveBallToPlayer(0, "Straffskytte: Målvaktsräddning!");
                    }
                    else
                    {
                        ScoreAway++;
                        SetStatusOverride("Borta straffmål!", 120);
                        GameEvent?.Invoke(GameEventType.GoalAway);
                        ResetAfterScore(homeScored: false);
                    }
                }
                else
                {
                    GiveBallToPlayer(GetNearestHomeIndex(BallPos), "Straff missat");
                }
            }
        }
    }

    void UpdateBallHeight(double dt)
    {
        // Simulate ball height for visual arc during shots and passes
        if (_shootActive)
        {
            var t = Math.Clamp(_shootTime / 0.6f, 0f, 1f);
            BallHeight = Math.Sin(t * Math.PI) * 0.7;
        }
        else if (_awayShootActive)
        {
            var t = Math.Clamp(_awayShootTime / 0.55f, 0f, 1f);
            BallHeight = Math.Sin(t * Math.PI) * 0.7;
        }
        else if (_passActive || _awayPassActive)
        {
            // Passes have a lower arc
            BallHeight = Math.Max(0, BallHeight - dt * 3);
            if (_passActive && _passTargetHomeIndex >= 0)
            {
                var target = HomePlayers[_passTargetHomeIndex].Position;
                var total = Distance(_passStartPos, target);
                if (total > 1)
                {
                    var current = Distance(BallPos, target);
                    var progress = 1.0 - (current / total);
                    BallHeight = Math.Sin(Math.Clamp(progress, 0, 1) * Math.PI) * 0.4;
                }
            }
            else if (_awayPassActive && _awayPassTargetIndex >= 0)
            {
                BallHeight = 0.3; // simpler arc for away passes
            }
        }
        else
        {
            // Decay height to 0 when ball is held
            BallHeight = Math.Max(0, BallHeight - dt * 5);
        }
    }

    // ── Confetti system ──

    void SpawnConfetti(bool homeScored)
    {
        float goalX = homeScored
            ? (float)(ViewSize.Width - GoalCenterInset - 10)
            : (float)(GoalCenterInset + 10);
        float centerY = ViewSize.Height > 0 ? (float)(ViewSize.Height / 2) : 300f;

        ConfettiCount = MaxConfetti;
        for (int i = 0; i < MaxConfetti; i++)
        {
            float angle = (float)(Random.Shared.NextDouble() * Math.PI * 2);
            float speed = 60f + (float)(Random.Shared.NextDouble() * 120);
            Confetti[i] = new ConfettiParticle
            {
                X = goalX + (float)(Random.Shared.NextDouble() - 0.5) * 40,
                Y = centerY + (float)(Random.Shared.NextDouble() - 0.5) * 100,
                VX = (float)Math.Cos(angle) * speed,
                VY = (float)Math.Sin(angle) * speed - 60f, // upward bias
                ColorIndex = Random.Shared.Next(5),
                LifeTicks = 60 + Random.Shared.Next(60)
            };
        }
    }

    void UpdateConfetti(double dt)
    {
        int alive = 0;
        for (int i = 0; i < ConfettiCount; i++)
        {
            if (Confetti[i].LifeTicks <= 0) continue;
            Confetti[i].LifeTicks--;
            Confetti[i].X += Confetti[i].VX * (float)dt;
            Confetti[i].Y += Confetti[i].VY * (float)dt;
            Confetti[i].VY += 120f * (float)dt; // gravity
            Confetti[i].VX *= 0.98f; // air resistance
            if (Confetti[i].LifeTicks > 0) alive++;
        }
        if (alive == 0) ConfettiCount = 0;
    }

    /// <summary>
    /// Returns the match clock formatted as MM:SS for display.
    /// </summary>
    public string GetMatchClockDisplay()
    {
        double displayMinutes = (CurrentHalf - 1) * 30 + MatchClockSeconds * GameTimeMultiplier / 60.0;
        int mins = (int)displayMinutes;
        int secs = (int)((displayMinutes - mins) * 60);
        return $"{mins:D2}:{secs:D2}";
    }

    /// <summary>Returns a short half indicator string.</summary>
    public string GetHalfDisplay() => CurrentHalf == 1 ? "1:a" : "2:a";

    /// <summary>Returns the difficulty label for display.</summary>
    public string GetDifficultyLabel() => Difficulty switch
    {
        Difficulty.Easy => "Lätt",
        Difficulty.Hard => "Svår",
        _ => "Normal"
    };
}

public class GameDrawable : IDrawable
{
    readonly GameState _state;
    readonly Color HomeColor;
    readonly Color HomeColorLight;
    readonly Color AwayColor;
    readonly Color AwayColorLight;
    readonly Color AranasWhite;

    // Confetti colors (updated per-game based on team colors)
    readonly Color[] _confettiColors;

    public GameDrawable(GameState state, TeamColorOption? homeColors = null, TeamColorOption? awayColors = null)
    {
        _state = state;

        // Home team colors
        if (homeColors != null)
        {
            HomeColor = Color.FromArgb(homeColors.Primary);
            HomeColorLight = Color.FromArgb(homeColors.Light);
        }
        else
        {
            HomeColor = ControlsApplication.Current?.Resources.TryGetValue("AranasBlue", out var b) == true ? (Color)b : Color.FromArgb("#003DA5");
            HomeColorLight = ControlsApplication.Current?.Resources.TryGetValue("AranasBlueLight", out var bl) == true ? (Color)bl : Color.FromArgb("#2E7CF6");
        }

        // Away team colors
        if (awayColors != null)
        {
            AwayColor = Color.FromArgb(awayColors.Primary);
            AwayColorLight = Color.FromArgb(awayColors.Light);
        }
        else
        {
            AwayColor = Colors.Crimson;
            AwayColorLight = Color.FromArgb("#FF5555");
        }

        AranasWhite = ControlsApplication.Current?.Resources.TryGetValue("AranasWhite", out var w) == true ? (Color)w : Colors.White;

        _confettiColors =
        [
            Colors.Gold,
            HomeColor,
            Colors.White,
            AwayColor,
            HomeColorLight
        ];
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        // Sync game state ViewSize with actual render area to prevent
        // coordinate mismatch between drawing and game logic (fixes players outside field)
        if (_state.ViewSize.Width != dirtyRect.Width || _state.ViewSize.Height != dirtyRect.Height)
            _state.OnViewSizeChanged(new Size(dirtyRect.Width, dirtyRect.Height));

        DrawField(canvas, dirtyRect);
        DrawPlayers(canvas);
        DrawShotTrail(canvas);
        DrawBall(canvas);
        DrawPassIndicator(canvas);
        DrawPenaltySpotIndicator(canvas, dirtyRect);
        DrawConfetti(canvas);
        DrawScore(canvas, dirtyRect);
        DrawSuspensionIndicator(canvas, dirtyRect);
        DrawPassivePlayIndicator(canvas, dirtyRect);
        DrawGoalCelebration(canvas, dirtyRect);
        DrawMatchOverlay(canvas, dirtyRect);
        DrawPauseOverlay(canvas, dirtyRect);
        DrawKeyboardHelp(canvas, dirtyRect);
    }

    void DrawField(ICanvas canvas, RectF dirtyRect)
    {
        // Arena background (dark surround like spectator area)
        canvas.FillColor = Color.FromArgb("#2C1B0E");
        canvas.FillRectangle(dirtyRect);

        var fieldMargin = (float)GameState.FieldMargin;
        var courtLeft = fieldMargin;
        var courtTop = fieldMargin;
        var courtW = dirtyRect.Width - fieldMargin * 2;
        var courtH = dirtyRect.Height - fieldMargin * 2;
        var centerX = dirtyRect.Center.X;
        var centerY = dirtyRect.Center.Y;

        // Wooden floor base
        canvas.FillColor = Color.FromArgb("#C8956C");
        canvas.FillRoundedRectangle(courtLeft, courtTop, courtW, courtH, 4);

        // Wood plank lines (horizontal grain)
        canvas.StrokeColor = Color.FromArgb("#18000000");
        canvas.StrokeSize = 1;
        float plankHeight = 28f;
        for (float py = courtTop + plankHeight; py < courtTop + courtH; py += plankHeight)
        {
            canvas.DrawLine(courtLeft, py, courtLeft + courtW, py);
        }

        // Slight color variation on alternating planks
        canvas.FillColor = Color.FromArgb("#08000000");
        for (float py = courtTop; py < courtTop + courtH; py += plankHeight * 2)
        {
            float h = Math.Min(plankHeight, courtTop + courtH - py);
            canvas.FillRectangle(courtLeft, py, courtW, h);
        }

        // Court boundary (thick line)
        canvas.StrokeColor = Color.FromArgb("#DDFFFFFF");
        canvas.StrokeSize = 3;
        canvas.DrawRoundedRectangle(courtLeft, courtTop, courtW, courtH, 4);

        // Center line
        canvas.StrokeColor = Color.FromArgb("#CCFFFFFF");
        canvas.StrokeSize = 2;
        canvas.DrawLine(centerX, courtTop, centerX, courtTop + courtH);

        // Center circle (substitution area)
        canvas.StrokeColor = Color.FromArgb("#99FFFFFF");
        canvas.DrawCircle(centerX, centerY, 34);
        canvas.FillColor = Color.FromArgb("#44FFFFFF");
        canvas.FillCircle(centerX, centerY, 4);

        // Goal area (6m) — solid, colored fill + line
        var goalAreaRadius = (float)GameState.GoalAreaRadius;
        var freeThrowRadius = (float)GameState.FreeThrowRadius;
        var leftGoalCenterX = (float)GameState.GoalCenterInset;
        var rightGoalCenterX = dirtyRect.Width - (float)GameState.GoalCenterInset;

        // Goal area fill (light tint)
        canvas.FillColor = HomeColor.WithAlpha(0.13f);
        canvas.FillCircle(leftGoalCenterX, centerY, goalAreaRadius);
        canvas.FillColor = AwayColor.WithAlpha(0.13f);
        canvas.FillCircle(rightGoalCenterX, centerY, goalAreaRadius);

        // Goal area line (solid)
        canvas.StrokeColor = Color.FromArgb("#BBFFFFFF");
        canvas.StrokeSize = 2.5f;
        canvas.DrawCircle(leftGoalCenterX, centerY, goalAreaRadius);
        canvas.DrawCircle(rightGoalCenterX, centerY, goalAreaRadius);

        // Free throw line (9m, dashed)
        canvas.StrokeDashPattern = [10, 6];
        canvas.StrokeColor = Color.FromArgb("#77FFFFFF");
        canvas.StrokeSize = 2;
        canvas.DrawCircle(leftGoalCenterX, centerY, freeThrowRadius);
        canvas.DrawCircle(rightGoalCenterX, centerY, freeThrowRadius);
        canvas.StrokeDashPattern = null;

        // 7-meter penalty marks
        float penaltyLeftX = leftGoalCenterX + goalAreaRadius + 48;
        float penaltyRightX = rightGoalCenterX - goalAreaRadius - 48;
        canvas.StrokeColor = Color.FromArgb("#CCFFFFFF");
        canvas.StrokeSize = 3;
        canvas.DrawLine(penaltyLeftX, centerY - 8, penaltyLeftX, centerY + 8);
        canvas.DrawLine(penaltyRightX, centerY - 8, penaltyRightX, centerY + 8);

        // Goals (nets with depth)
        DrawGoal(canvas, new RectF(4, centerY - 80, 16, 160), HomeColor);
        DrawGoal(canvas, new RectF(dirtyRect.Width - 20, centerY - 80, 16, 160), AwayColor);
    }

    void DrawPlayers(ICanvas canvas)
    {
        for (int i = 0; i < _state.HomePlayers.Length; i++)
        {
            var p = _state.HomePlayers[i];
            bool isActive = _state.BallOwnerType == BallOwnershipType.Player && _state.BallOwnerPlayerIndex == i;
            bool isDefender = _state.IsHomeDefending && i == _state.ControlledDefenderIndex;
            var jerseyColor = i == 0 ? HomeColorLight : HomeColor;
            DrawPlayerFigure(canvas, p.Position, jerseyColor, i, isActive, isDefender, p.IsGoalkeeper, p.IsSuspended);
        }

        for (int i = 0; i < _state.AwayPlayers.Length; i++)
        {
            var a = _state.AwayPlayers[i];
            bool isActive = _state.BallOwnerType == BallOwnershipType.Opponent && _state.BallOwnerAwayIndex == i;
            var jerseyColor = i == 0 ? AwayColorLight : AwayColor;
            DrawPlayerFigure(canvas, a.Position, jerseyColor, i, isActive, false, a.IsGoalkeeper, a.IsSuspended);
        }
    }

    void DrawPlayerFigure(ICanvas canvas, Point pos, Color jerseyColor, int number,
        bool isActive, bool isDefender, bool isGoalkeeper, bool isSuspended = false)
    {
        // Suspended players are drawn faded at bench position
        if (isSuspended)
        {
            float sx = (float)pos.X;
            float sy = (float)pos.Y;
            canvas.FillColor = jerseyColor.WithAlpha(0.3f);
            canvas.FillRoundedRectangle(sx - 6, sy - 4, 12, 8, 3);
            canvas.FontColor = Colors.Red.WithAlpha(0.7f);
            canvas.FontSize = 7;
            canvas.DrawString("UT", sx - 5, sy - 8, 10, 10,
                G.HorizontalAlignment.Center, G.VerticalAlignment.Center);
            return;
        }

        float x = (float)pos.X;
        float y = (float)pos.Y;
        float bodyW = isGoalkeeper ? 22f : 18f;
        float bodyH = isGoalkeeper ? 18f : 14f;
        float headR = isGoalkeeper ? 7f : 6f;

        // Shadow
        canvas.FillColor = Color.FromArgb("#33000000");
        canvas.FillEllipse(x - bodyW / 2 + 1, y - bodyH / 2 + 2, bodyW, bodyH);

        // Body (jersey torso)
        canvas.FillColor = jerseyColor;
        canvas.FillRoundedRectangle(x - bodyW / 2, y - bodyH / 2 + 2, bodyW, bodyH, 4);

        // Jersey stripe for goalkeepers
        if (isGoalkeeper)
        {
            canvas.FillColor = Colors.White.WithAlpha(0.3f);
            canvas.FillRoundedRectangle(x - bodyW / 2 + 2, y + 1, bodyW - 4, 5, 2);
        }

        // Head
        canvas.FillColor = Color.FromArgb("#FFDAB9");
        canvas.FillCircle(x, y - bodyH / 2 - headR + 4, headR);

        // Hair
        canvas.FillColor = Color.FromArgb("#3E2723");
        canvas.FillCircle(x, y - bodyH / 2 - headR + 2, headR * 0.65f);

        // Number on jersey
        canvas.FontColor = Colors.White;
        canvas.FontSize = 8;
        canvas.DrawString(number.ToString(), x - 5, y - 1, 10, 10,
            G.HorizontalAlignment.Center, G.VerticalAlignment.Center);

        // Position label below player
        string posLabel = GetPositionLabel(number, isGoalkeeper);
        canvas.FontColor = Colors.White.WithAlpha(0.6f);
        canvas.FontSize = 6;
        canvas.DrawString(posLabel, x - 8, y + bodyH / 2 + 3, 16, 8,
            G.HorizontalAlignment.Center, G.VerticalAlignment.Center);

        // Selection ring — pulsing for active player
        float ringR = bodyW / 2 + 5;
        if (isActive)
        {
            float pulse = (float)(0.7 + 0.3 * Math.Sin(Environment.TickCount / 150.0));
            canvas.StrokeColor = Colors.White.WithAlpha(pulse);
            canvas.StrokeSize = 2.5f;
            canvas.DrawCircle(x, y + 2, ringR);

            // Direction arrow showing movement input
            if (Math.Abs(_state.ActiveMoveInput.X) > 5 || Math.Abs(_state.ActiveMoveInput.Y) > 5)
            {
                double len = Math.Sqrt(_state.ActiveMoveInput.X * _state.ActiveMoveInput.X + _state.ActiveMoveInput.Y * _state.ActiveMoveInput.Y);
                if (len > 0)
                {
                    float arrowLen = 16f;
                    float ax = x + (float)(_state.ActiveMoveInput.X / len) * arrowLen;
                    float ay = y + 2 + (float)(_state.ActiveMoveInput.Y / len) * arrowLen;
                    canvas.StrokeColor = Colors.Yellow.WithAlpha(0.7f);
                    canvas.StrokeSize = 2;
                    canvas.DrawLine(x, y + 2, ax, ay);
                }
            }
        }
        else if (isDefender)
        {
            float pulse = (float)(0.7 + 0.3 * Math.Sin(Environment.TickCount / 200.0));
            canvas.StrokeColor = Colors.Gold.WithAlpha(pulse);
            canvas.StrokeSize = 3;
            canvas.DrawCircle(x, y + 2, ringR);
        }
    }

    static string GetPositionLabel(int number, bool isGoalkeeper)
    {
        if (isGoalkeeper) return "MV";
        return number switch
        {
            1 => "VY", // Vänster ytter (Left wing)
            2 => "VB", // Vänster back (Left back)
            3 => "M",  // Mittsexa (Center back)
            4 => "HB", // Höger back (Right back)
            5 => "HY", // Höger ytter (Right wing)
            6 => "PV", // Pivot
            _ => ""
        };
    }

    void DrawBall(ICanvas canvas)
    {
        float bx = (float)_state.BallPos.X;
        float by = (float)_state.BallPos.Y;
        float height = (float)_state.BallHeight;

        // Ball shadow on ground (offset increases with height)
        float shadowOffset = height * 12;
        float shadowScale = 1f + height * 0.3f;
        canvas.FillColor = Color.FromArgb("#33000000");
        canvas.FillEllipse(bx - 5 * shadowScale + 1, by + 2 + shadowOffset, 10 * shadowScale, 6 * shadowScale);

        // Lift ball visually based on height
        float visualBy = by - height * 20;
        float ballRadius = 7f + height * 2f; // ball appears slightly larger when high

        // Ball glow during shots
        if (_state.IsShootActive || _state.IsAwayShootActive || _state.IsPenaltyActive)
        {
            canvas.FillColor = Colors.Yellow.WithAlpha(0.25f);
            canvas.FillCircle(bx, visualBy, ballRadius + 7);
        }

        // Fast break indicator glow
        if (_state.IsHomeFastBreak || _state.IsAwayFastBreak)
        {
            canvas.FillColor = Colors.Cyan.WithAlpha(0.15f);
            canvas.FillCircle(bx, visualBy, ballRadius + 5);
        }

        // Ball
        canvas.FillColor = Color.FromArgb("#FF8C00");
        canvas.FillCircle(bx, visualBy, ballRadius);

        // Ball seam lines for realism
        canvas.StrokeColor = Color.FromArgb("#44000000");
        canvas.StrokeSize = 0.8f;
        canvas.DrawLine(bx - ballRadius * 0.5f, visualBy - ballRadius * 0.3f,
                        bx + ballRadius * 0.5f, visualBy + ballRadius * 0.3f);

        // Highlight
        canvas.FillColor = Colors.White.WithAlpha(0.4f);
        canvas.FillCircle(bx - 2, visualBy - 2, 3);
    }

    void DrawPassIndicator(ICanvas canvas)
    {
        if (!_state.IsPassActive || _state.PassTargetTeammateIndex < 0) return;

        var target = _state.HomePlayers[_state.PassTargetTeammateIndex].Position;
        canvas.StrokeColor = Color.FromArgb("#AAFFFFFF");
        canvas.StrokeSize = 2;
        canvas.StrokeDashPattern = [4, 4];
        canvas.DrawLine((float)_state.BallPos.X, (float)_state.BallPos.Y, (float)target.X, (float)target.Y);
        canvas.StrokeDashPattern = null;
    }

    void DrawScore(ICanvas canvas, RectF dirtyRect)
    {
        // Enhanced score display with team names, clock, half indicator, and difficulty
        float pillW = 220, pillH = 42;
        float pillX = dirtyRect.Center.X - pillW / 2;
        canvas.FillColor = Color.FromArgb("#DD2C1B0E");
        canvas.FillRoundedRectangle(pillX, 4, pillW, pillH, 18);

        // Subtle border
        canvas.StrokeColor = Color.FromArgb("#44FFFFFF");
        canvas.StrokeSize = 1;
        canvas.DrawRoundedRectangle(pillX, 4, pillW, pillH, 18);

        // Score with team names
        canvas.FontColor = HomeColor;
        canvas.FontSize = 11;
        canvas.DrawString("HEM",
            new RectF(pillX + 8, 8, 40, 14),
            G.HorizontalAlignment.Left, G.VerticalAlignment.Center);

        canvas.FontColor = AwayColor;
        canvas.DrawString("BOR",
            new RectF(pillX + pillW - 48, 8, 40, 14),
            G.HorizontalAlignment.Right, G.VerticalAlignment.Center);

        // Score numbers (larger and bolder)
        canvas.FontColor = AranasWhite;
        canvas.FontSize = 22;
        canvas.DrawString($"{_state.ScoreHome} - {_state.ScoreAway}",
            new RectF(pillX, 6, pillW, 22),
            G.HorizontalAlignment.Center, G.VerticalAlignment.Center);

        // Match clock and difficulty below score
        canvas.FontSize = 9;
        canvas.FontColor = Color.FromArgb("#AAFFFFFF");
        string clockText = $"{_state.GetMatchClockDisplay()}  {_state.GetHalfDisplay()}  •  {_state.GetDifficultyLabel()}";
        canvas.DrawString(clockText,
            new RectF(pillX, 28, pillW, 14),
            G.HorizontalAlignment.Center, G.VerticalAlignment.Center);
    }

    void DrawPassivePlayIndicator(ICanvas canvas, RectF dirtyRect)
    {
        if (!_state.PassivePlayWarningActive) return;

        // Flashing warning border
        float alpha = (float)(0.3 + 0.3 * Math.Sin(Environment.TickCount / 200.0));
        canvas.StrokeColor = Colors.Orange.WithAlpha(alpha);
        canvas.StrokeSize = 4;
        canvas.DrawRoundedRectangle(16, 16, dirtyRect.Width - 32, dirtyRect.Height - 32, 4);
    }

    void DrawGoalCelebration(ICanvas canvas, RectF dirtyRect)
    {
        if (!_state.IsGoalCelebration) return;

        // Semi-transparent overlay flash — pulsing
        float flashAlpha = (float)(0.1 + 0.08 * Math.Sin(Environment.TickCount / 100.0));
        canvas.FillColor = Colors.Gold.WithAlpha(flashAlpha);
        canvas.FillRectangle(dirtyRect);

        // Large celebration text with shadow
        canvas.FontColor = Color.FromArgb("#88000000");
        canvas.FontSize = 38;
        canvas.DrawString(_state.GoalCelebrationText,
            new RectF(2, dirtyRect.Height * 0.35f + 2, dirtyRect.Width, 50),
            G.HorizontalAlignment.Center, G.VerticalAlignment.Center);
        canvas.FontColor = Colors.Gold;
        canvas.FontSize = 38;
        canvas.DrawString(_state.GoalCelebrationText,
            new RectF(0, dirtyRect.Height * 0.35f, dirtyRect.Width, 50),
            G.HorizontalAlignment.Center, G.VerticalAlignment.Center);
    }

    void DrawConfetti(ICanvas canvas)
    {
        if (_state.ConfettiCount == 0) return;
        for (int i = 0; i < _state.ConfettiCount; i++)
        {
            ref readonly var p = ref _state.Confetti[i];
            if (p.LifeTicks <= 0) continue;

            float alpha = Math.Clamp(p.LifeTicks / 40f, 0f, 1f);
            var color = _confettiColors[p.ColorIndex % _confettiColors.Length].WithAlpha(alpha);
            canvas.FillColor = color;

            // Rotate confetti pieces slightly based on position
            float w = 4f + (p.ColorIndex % 3) * 2f;
            float h = 3f + (p.ColorIndex % 2) * 2f;
            canvas.FillRoundedRectangle(p.X - w / 2, p.Y - h / 2, w, h, 1);
        }
    }

    void DrawMatchOverlay(ICanvas canvas, RectF dirtyRect)
    {
        if (_state.IsHalfTime)
        {
            // Half-time overlay
            canvas.FillColor = Color.FromArgb("#CC2C1B0E");
            canvas.FillRectangle(dirtyRect);

            canvas.FontColor = Colors.Gold;
            canvas.FontSize = 32;
            canvas.DrawString("HALVTID",
                new RectF(0, dirtyRect.Height * 0.3f, dirtyRect.Width, 40),
                G.HorizontalAlignment.Center, G.VerticalAlignment.Center);

            canvas.FontColor = AranasWhite;
            canvas.FontSize = 24;
            canvas.DrawString($"{_state.ScoreHome} - {_state.ScoreAway}",
                new RectF(0, dirtyRect.Height * 0.45f, dirtyRect.Width, 36),
                G.HorizontalAlignment.Center, G.VerticalAlignment.Center);

            canvas.FontSize = 14;
            canvas.FontColor = Color.FromArgb("#AAFFFFFF");
            canvas.DrawString("Andra halvlek börjar snart...",
                new RectF(0, dirtyRect.Height * 0.55f, dirtyRect.Width, 24),
                G.HorizontalAlignment.Center, G.VerticalAlignment.Center);
        }

        if (_state.IsMatchOver)
        {
            // Full-time overlay
            canvas.FillColor = Color.FromArgb("#DD2C1B0E");
            canvas.FillRectangle(dirtyRect);

            canvas.FontColor = Colors.Gold;
            canvas.FontSize = 32;
            canvas.DrawString("SLUTSIGNAL",
                new RectF(0, dirtyRect.Height * 0.12f, dirtyRect.Width, 40),
                G.HorizontalAlignment.Center, G.VerticalAlignment.Center);

            // Result
            string result = _state.ScoreHome > _state.ScoreAway ? "SEGER!" :
                            _state.ScoreHome < _state.ScoreAway ? "FÖRLUST" : "OAVGJORT";
            canvas.FontColor = _state.ScoreHome >= _state.ScoreAway ? Colors.Gold : Colors.White;
            canvas.FontSize = 28;
            canvas.DrawString(result,
                new RectF(0, dirtyRect.Height * 0.22f, dirtyRect.Width, 36),
                G.HorizontalAlignment.Center, G.VerticalAlignment.Center);

            // Final score large
            canvas.FontColor = AranasWhite;
            canvas.FontSize = 48;
            canvas.DrawString($"{_state.ScoreHome} - {_state.ScoreAway}",
                new RectF(0, dirtyRect.Height * 0.33f, dirtyRect.Width, 56),
                G.HorizontalAlignment.Center, G.VerticalAlignment.Center);

            // Team labels
            canvas.FontSize = 16;
            canvas.FontColor = HomeColor;
            canvas.DrawString("Hemma",
                new RectF(0, dirtyRect.Height * 0.44f, dirtyRect.Width / 2, 24),
                G.HorizontalAlignment.Center, G.VerticalAlignment.Center);
            canvas.FontColor = AwayColor;
            canvas.DrawString("Borta",
                new RectF(dirtyRect.Width / 2, dirtyRect.Height * 0.44f, dirtyRect.Width / 2, 24),
                G.HorizontalAlignment.Center, G.VerticalAlignment.Center);

            // ── Match statistics table ──
            float statsY = dirtyRect.Height * 0.52f;
            float statsH = 18;
            float leftColX = dirtyRect.Width * 0.2f;
            float centerColX = dirtyRect.Width * 0.35f;
            float rightColX = dirtyRect.Width * 0.65f;
            float colW = dirtyRect.Width * 0.15f;
            float labelW = dirtyRect.Width * 0.3f;

            // Header
            canvas.FontSize = 11;
            canvas.FontColor = Colors.Gold;
            canvas.DrawString("STATISTIK",
                new RectF(0, statsY - statsH, dirtyRect.Width, statsH),
                G.HorizontalAlignment.Center, G.VerticalAlignment.Center);

            // Stats rows
            DrawStatRow(canvas, leftColX, centerColX, rightColX, colW, labelW, ref statsY, statsH,
                "Skott", _state.ShotsHome, _state.ShotsAway);
            DrawStatRow(canvas, leftColX, centerColX, rightColX, colW, labelW, ref statsY, statsH,
                "Räddningar", _state.SavesAway, _state.SavesHome); // opponent GK saves
            DrawStatRow(canvas, leftColX, centerColX, rightColX, colW, labelW, ref statsY, statsH,
                "Passningar", _state.PassesHome, _state.PassesAway);

            // Restart hint
            canvas.FontSize = 14;
            canvas.FontColor = Color.FromArgb("#AAFFFFFF");
            canvas.DrawString("Tryck för ny match",
                new RectF(0, dirtyRect.Height * 0.82f, dirtyRect.Width, 24),
                G.HorizontalAlignment.Center, G.VerticalAlignment.Center);
        }
    }

    void DrawPauseOverlay(ICanvas canvas, RectF dirtyRect)
    {
        if (!_state.IsPaused) return;

        // Semi-transparent overlay
        canvas.FillColor = Color.FromArgb("#CC2C1B0E");
        canvas.FillRectangle(dirtyRect);

        // Pause icon (two vertical bars)
        float cx = dirtyRect.Width / 2;
        float cy = dirtyRect.Height * 0.32f;
        float barW = 14, barH = 50, gap = 10;
        canvas.FillColor = Colors.White;
        canvas.FillRoundedRectangle(cx - gap - barW, cy - barH / 2, barW, barH, 4);
        canvas.FillRoundedRectangle(cx + gap, cy - barH / 2, barW, barH, 4);

        // "PAUS" text
        canvas.FontColor = Colors.Gold;
        canvas.FontSize = 32;
        canvas.DrawString("PAUS",
            new RectF(0, dirtyRect.Height * 0.48f, dirtyRect.Width, 40),
            G.HorizontalAlignment.Center, G.VerticalAlignment.Center);

        // Current score
        canvas.FontColor = AranasWhite;
        canvas.FontSize = 24;
        canvas.DrawString($"{_state.ScoreHome} - {_state.ScoreAway}",
            new RectF(0, dirtyRect.Height * 0.58f, dirtyRect.Width, 36),
            G.HorizontalAlignment.Center, G.VerticalAlignment.Center);

        // Resume hint
        canvas.FontSize = 14;
        canvas.FontColor = Color.FromArgb("#AAFFFFFF");
        canvas.DrawString("Tryck för att fortsätta",
            new RectF(0, dirtyRect.Height * 0.70f, dirtyRect.Width, 24),
            G.HorizontalAlignment.Center, G.VerticalAlignment.Center);
    }

    static void DrawStatRow(ICanvas canvas, float leftX, float centerX, float rightX, float colW, float labelW,
        ref float y, float h, string label, int homeVal, int awayVal)
    {
        canvas.FontSize = 12;

        // Home value
        canvas.FontColor = Color.FromArgb("#DDFFFFFF");
        canvas.DrawString(homeVal.ToString(),
            new RectF(leftX, y, colW, h),
            G.HorizontalAlignment.Center, G.VerticalAlignment.Center);

        // Label
        canvas.FontColor = Color.FromArgb("#AAFFFFFF");
        canvas.DrawString(label,
            new RectF(centerX, y, labelW, h),
            G.HorizontalAlignment.Center, G.VerticalAlignment.Center);

        // Away value
        canvas.FontColor = Color.FromArgb("#DDFFFFFF");
        canvas.DrawString(awayVal.ToString(),
            new RectF(rightX, y, colW, h),
            G.HorizontalAlignment.Center, G.VerticalAlignment.Center);

        y += h;
    }

    void DrawGoal(ICanvas canvas, RectF rect, Color color)
    {
        // Goal depth/net background
        canvas.FillColor = Color.FromArgb("#55000000");
        canvas.FillRoundedRectangle(rect.X - 2, rect.Y + 2, rect.Width + 4, rect.Height - 4, 3);

        canvas.FillColor = Color.FromArgb("#44000000");
        canvas.FillRoundedRectangle(rect, 2);

        // Net pattern (horizontal + vertical lines for mesh effect)
        canvas.StrokeColor = Colors.White.WithAlpha(0.18f);
        canvas.StrokeSize = 1;
        for (float ny = rect.Top + 10; ny < rect.Bottom; ny += 10)
        {
            canvas.DrawLine(rect.Left + 1, ny, rect.Right - 1, ny);
        }
        for (float nx = rect.Left + 4; nx < rect.Right; nx += 6)
        {
            canvas.DrawLine(nx, rect.Top + 1, nx, rect.Bottom - 1);
        }

        // Goal celebration flash (net shakes on goal)
        if (_state.IsGoalCelebration)
        {
            float flash = (float)(0.25 + 0.25 * Math.Sin(Environment.TickCount / 80.0));
            canvas.FillColor = Colors.Gold.WithAlpha(flash);
            canvas.FillRoundedRectangle(rect, 2);
        }

        // Goal frame (posts + crossbar)
        canvas.StrokeColor = color;
        canvas.StrokeSize = 4;
        canvas.DrawRoundedRectangle(rect, 2);

        // Inner frame highlight
        canvas.StrokeColor = Colors.White.WithAlpha(0.15f);
        canvas.StrokeSize = 1;
        canvas.DrawRoundedRectangle(rect.X + 2, rect.Y + 2, rect.Width - 4, rect.Height - 4, 1);
    }

    void DrawShotTrail(ICanvas canvas)
    {
        // Draw trail behind ball during shots
        if (!_state.IsShootActive && !_state.IsAwayShootActive && !_state.IsPenaltyActive) return;

        Point end;
        if (_state.IsShootActive)
        {
            end = _state.ShootEnd;
        }
        else if (_state.IsAwayShootActive)
        {
            end = _state.AwayShootEnd;
        }
        else return;

        // Trailing dots behind ball
        float bx = (float)_state.BallPos.X;
        float by = (float)_state.BallPos.Y;
        float ex = (float)end.X;
        float ey = (float)end.Y;

        // Direction from ball to target
        float dx = ex - bx;
        float dy = ey - by;
        float dist = (float)Math.Sqrt(dx * dx + dy * dy);
        if (dist < 1) return;

        // Draw 5 trailing dots behind the ball
        for (int t = 1; t <= 5; t++)
        {
            float trailX = bx - (dx / dist) * t * 7;
            float trailY = by - (dy / dist) * t * 7;
            float alpha = 0.35f - t * 0.06f;
            float radius = 4.5f - t * 0.7f;
            if (alpha <= 0 || radius <= 0) break;
            canvas.FillColor = Color.FromArgb("#FF8C00").WithAlpha(alpha);
            canvas.FillCircle(trailX, trailY, radius);
        }
    }

    void DrawSuspensionIndicator(ICanvas canvas, RectF dirtyRect)
    {
        int homeSus = _state.HomeSuspensionCount;
        int awaySus = _state.AwaySuspensionCount;
        if (homeSus == 0 && awaySus == 0) return;

        float indicatorY = 50;
        float pillW = 160, pillH = 16;
        float pillX = dirtyRect.Center.X - pillW / 2;

        canvas.FillColor = Color.FromArgb("#88000000");
        canvas.FillRoundedRectangle(pillX, indicatorY, pillW, pillH, 8);

        canvas.FontSize = 9;
        if (homeSus > 0)
        {
            canvas.FontColor = HomeColor;
            canvas.DrawString($"HEM -{homeSus}",
                new RectF(pillX + 4, indicatorY, pillW / 2 - 4, pillH),
                G.HorizontalAlignment.Left, G.VerticalAlignment.Center);
        }
        if (awaySus > 0)
        {
            canvas.FontColor = AwayColor;
            canvas.DrawString($"BOR -{awaySus}",
                new RectF(pillX + pillW / 2, indicatorY, pillW / 2 - 4, pillH),
                G.HorizontalAlignment.Right, G.VerticalAlignment.Center);
        }
    }

    void DrawPenaltySpotIndicator(ICanvas canvas, RectF dirtyRect)
    {
        if (!_state.IsPenaltyActive) return;

        float centerY = dirtyRect.Center.Y;

        // Animate the penalty spot with a pulsing ring
        float pulse = (float)(8 + 4 * Math.Sin(Environment.TickCount / 150.0));
        float spotX;
        if (_state.PenaltyIsHome)
        {
            // Right penalty spot
            float rightGoalCenterX = dirtyRect.Width - (float)GameState.GoalCenterInset;
            spotX = rightGoalCenterX - (float)GameState.GoalAreaRadius - 48;
        }
        else
        {
            // Left penalty spot
            float leftGoalCenterX = (float)GameState.GoalCenterInset;
            spotX = leftGoalCenterX + (float)GameState.GoalAreaRadius + 48;
        }

        canvas.StrokeColor = Colors.Yellow.WithAlpha(0.6f);
        canvas.StrokeSize = 2;
        canvas.DrawCircle(spotX, centerY, pulse);

        canvas.FillColor = Colors.Yellow.WithAlpha(0.3f);
        canvas.FillCircle(spotX, centerY, 6);
    }

    void DrawKeyboardHelp(ICanvas canvas, RectF dirtyRect)
    {
#if WINDOWS
        if (!_state.ShowKeyboardHelp) return;

        // Semi-transparent backdrop
        canvas.FillColor = Color.FromArgb("#CC1A1A2E");
        canvas.FillRectangle(dirtyRect);

        float panelW = 320, panelH = 340;
        float px = dirtyRect.Center.X - panelW / 2;
        float py = dirtyRect.Center.Y - panelH / 2;

        // Panel background
        canvas.FillColor = Color.FromArgb("#EE1E1E3A");
        canvas.FillRoundedRectangle(px, py, panelW, panelH, 16);

        // Panel border
        canvas.StrokeColor = Colors.Gold.WithAlpha(0.6f);
        canvas.StrokeSize = 2;
        canvas.DrawRoundedRectangle(px, py, panelW, panelH, 16);

        // Title
        canvas.FontColor = Colors.Gold;
        canvas.FontSize = 20;
        canvas.DrawString("TANGENTBORD",
            new RectF(px, py + 12, panelW, 28),
            G.HorizontalAlignment.Center, G.VerticalAlignment.Center);

        float lineY = py + 48;
        float lineH = 22;
        float keyX = px + 20;
        float descX = px + 110;
        float keyW = 80;
        float descW = panelW - 120;

        // Section: Alla lägen
        canvas.FontColor = Colors.CornflowerBlue;
        canvas.FontSize = 12;
        canvas.DrawString("Alla lägen",
            new RectF(keyX, lineY, panelW - 40, lineH),
            G.HorizontalAlignment.Left, G.VerticalAlignment.Center);
        lineY += lineH + 2;

        DrawHelpLine(canvas, keyX, descX, ref lineY, lineH, keyW, descW, "W A S D", "Rörelse");
        DrawHelpLine(canvas, keyX, descX, ref lineY, lineH, keyW, descW, "Pilar", "Rörelse (alt)");
        DrawHelpLine(canvas, keyX, descX, ref lineY, lineH, keyW, descW, "Mellanslag", "Framryckning");
        DrawHelpLine(canvas, keyX, descX, ref lineY, lineH, keyW, descW, "H", "Visa/dölj hjälp");

        lineY += 8;

        // Section: Anfall
        canvas.FontColor = Colors.LimeGreen;
        canvas.FontSize = 12;
        canvas.DrawString("Anfall",
            new RectF(keyX, lineY, panelW - 40, lineH),
            G.HorizontalAlignment.Left, G.VerticalAlignment.Center);
        lineY += lineH + 2;

        DrawHelpLine(canvas, keyX, descX, ref lineY, lineH, keyW, descW, "Q", "Passa uppåt");
        DrawHelpLine(canvas, keyX, descX, ref lineY, lineH, keyW, descW, "E", "Passa nedåt");
        DrawHelpLine(canvas, keyX, descX, ref lineY, lineH, keyW, descW, "F", "Skjut");

        lineY += 8;

        // Section: Försvar
        canvas.FontColor = Colors.Orange;
        canvas.FontSize = 12;
        canvas.DrawString("Försvar",
            new RectF(keyX, lineY, panelW - 40, lineH),
            G.HorizontalAlignment.Left, G.VerticalAlignment.Center);
        lineY += lineH + 2;

        DrawHelpLine(canvas, keyX, descX, ref lineY, lineH, keyW, descW, "R", "Byt försvarare");

        // Footer
        canvas.FontColor = Color.FromArgb("#AAFFFFFF");
        canvas.FontSize = 10;
        canvas.DrawString("Tryck H för att stänga",
            new RectF(px, py + panelH - 28, panelW, 20),
            G.HorizontalAlignment.Center, G.VerticalAlignment.Center);
#endif
    }

    static void DrawHelpLine(ICanvas canvas, float keyX, float descX, ref float y, float h, float keyW, float descW, string key, string desc)
    {
        // Key badge
        canvas.FillColor = Color.FromArgb("#44FFFFFF");
        float badgeW = Math.Min(keyW, key.Length * 10 + 16);
        canvas.FillRoundedRectangle(keyX, y + 2, badgeW, h - 4, 6);

        canvas.FontColor = Colors.White;
        canvas.FontSize = 11;
        canvas.DrawString(key,
            new RectF(keyX, y, badgeW, h),
            G.HorizontalAlignment.Center, G.VerticalAlignment.Center);

        // Description
        canvas.FontColor = Color.FromArgb("#DDFFFFFF");
        canvas.FontSize = 11;
        canvas.DrawString(desc,
            new RectF(descX, y, descW, h),
            G.HorizontalAlignment.Left, G.VerticalAlignment.Center);

        y += h;
    }

}
