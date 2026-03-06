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
#endif
    bool _advanceHeld;

    public GamePage() : this(GameMode.SinglePlayer, null) { }

    public GamePage(GameMode mode, SoundManager? soundManager)
    {
        _gameMode = mode;
        _soundManager = soundManager;
        _state = new GameState(mode);

        InitializeComponent();
        _drawable = new GameDrawable(_state);
        GameView.Drawable = _drawable;

        _state.GameEvent += OnGameEvent;

        SizeChanged += (_, __) => _state.ViewSize = new Size(Width, Height);

        Joystick.ValueChanged += (_, p) =>
        {
            const double maxSpeed = 110;
            _state.ActiveMoveInput = new Point(p.X * maxSpeed, p.Y * maxSpeed);
        };

#if WINDOWS
        Loaded += (_, __) =>
        {
            if (GameView?.Handler?.PlatformView is UIElement element)
            {
                element.KeyDown += OnWinKeyDown;
                element.KeyUp += OnWinKeyUp;
                element.Focus(FocusState.Programmatic);
            }
        };
#endif

        _timer = Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(16);
        _timer.Tick += (_, __) =>
        {
            if (_advanceHeld)
                _state.AdvanceHeld();
            var defending = _state.IsHomeDefending;
            PassUpButton.IsVisible = !defending;
            PassDownButton.IsVisible = !defending;
            ShootButton.IsVisible = !defending;
            SwitchDefenderButton.IsVisible = defending;
            DefenderSideButtons.IsVisible = defending;
            AttackDiagonalButtons.IsVisible = !defending;
            StatusLabel.Text = _state.StatusText;
            _state.Update(0.016f);
            GameView.Invalidate();
        };
        _timer.Start();
    }

    void OnTapped(object? sender, TappedEventArgs e)
    {
        var pos = e.GetPosition(GameView);
        if (pos is Point p)
            _state.TargetPoint = p;
    }

    void OnPassUp(object? sender, EventArgs e)
        => _state.QueuePassVertical(-1);

    void OnPassDown(object? sender, EventArgs e)
        => _state.QueuePassVertical(1);

    void OnSwitchDefender(object? sender, EventArgs e) => _state.SwitchControlledDefender();
    void OnDefenderSideUpPressed(object? sender, EventArgs e) => _state.DefenderSideUpPressed();
    void OnDefenderSideDownPressed(object? sender, EventArgs e) => _state.DefenderSideDownPressed();
    void OnDefenderSideReleased(object? sender, EventArgs e) => _state.DefenderSideReleased();
    void OnDefenderDiagUpPressed(object? sender, EventArgs e) => _state.DefenderDiagUpPressed();
    void OnDefenderDiagDownPressed(object? sender, EventArgs e) => _state.DefenderDiagDownPressed();
    void OnDefenderDiagReleased(object? sender, EventArgs e) => _state.DefenderDiagReleased();
    void OnAttackDiagUpPressed(object? sender, EventArgs e) => _state.AttackDiagonalUpPressed();
    void OnAttackDiagDownPressed(object? sender, EventArgs e) => _state.AttackDiagonalDownPressed();
    void OnAttackDiagReleased(object? sender, EventArgs e) => _state.AttackDiagonalReleased();
    void OnAdvancePressed(object? sender, EventArgs e) { _advanceHeld = true; }
    void OnAdvanceReleased(object? sender, EventArgs e) { _advanceHeld = false; _state.AdvanceReleased(); }
    void OnShoot(object? sender, EventArgs e) => _state.QueueShoot();

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
            case GameEventType.Interception:
                _soundManager.PlayWhistle();
                break;
            case GameEventType.Whistle:
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
}

public class GameState
{
    const double GoalCenterInset = 20;
    const double GoalAreaRadius = 120;
    const double FreeThrowRadius = 168;

    public Size ViewSize { get; set; }
    public readonly Actor[] HomePlayers = new Actor[7];
    public readonly Actor[] AwayPlayers = new Actor[7];
    public readonly GameMode Mode;

    /// <summary>Raised when a notable game event occurs (goal, shot, pass, etc.).</summary>
    public event Action<GameEventType>? GameEvent;

    // Ownership
    public int BallOwnerPlayerIndex { get; private set; } = 1;
    public BallOwnershipType BallOwnerType { get; private set; } = BallOwnershipType.Player;
    public int BallOwnerAwayIndex { get; private set; } = -1;
    public int ControlledDefenderIndex { get; private set; } = 1;
    public bool IsHomeDefending => BallOwnerType != BallOwnershipType.Player;
    public Point BallPos { get; private set; } = new(100, 300);

    // Input
    public Point ActiveMoveInput { get; set; }
    public Point? TargetPoint { get; set; } // tap target (optional future use)

    // Pass state
    bool _passActive;
    int _passTargetHomeIndex = -1;
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
    int _statusOverrideTicks;
    string _statusOverrideText = "";

    // Goals
    Rect _rightGoal = new(0,0,12,160);
    Rect _leftGoal = new(8,200,12,160);

    bool _advanceBoost; // internal flag for held advance
    bool _defenderAdvanceBoost;
    double _defenderSideBoostY;
    double _defenderDiagBoostY;
    double _attackDiagonalBoostY;
    bool _resettingAfterGoal;
    bool _viewInitialized;

    public GameState(GameMode mode = GameMode.SinglePlayer)
    {
        Mode = mode;
        InitTeam(HomePlayers, 100, true);
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
            var offsetX = (i == 0 ? (leftToRight ? -40 : 40) : 0);
            team[i] = new Actor
            {
                Position = new Point(startX + offsetX, laneY),
                Velocity = Point.Zero,
                IsGoalkeeper = i == 0,
                BaseY = laneY,
                BaseX = startX + offsetX,
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
            var offsetX = (i == 0 ? (leftToRight ? -40 : 40) : 0);
            team[i].BaseX = startX + offsetX;
            team[i].BaseY = laneY;
            team[i].WasAdvancing = false;
        }
    }

    public void SwitchControlledDefender()
    {
        ControlledDefenderIndex++;
        if (ControlledDefenderIndex >= HomePlayers.Length)
            ControlledDefenderIndex = 1;
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
        var owner = HomePlayers[BallOwnerPlayerIndex];
        (int idx, Actor actor)? best = null; double bestMetric = double.MaxValue;
        for (int i = 1; i < HomePlayers.Length; i++)
        {
            if (i == BallOwnerPlayerIndex) continue;
            var dy = HomePlayers[i].Position.Y - owner.Position.Y;
            if (dirY < 0 && dy >= 0) continue;
            if (dirY > 0 && dy <= 0) continue;
            var metric = Math.Abs(dy);
            if (metric < bestMetric) { bestMetric = metric; best = (i, HomePlayers[i]); }
        }
        if (best is null) return;
        _passActive = true;
        _passTargetHomeIndex = best.Value.idx;
        _formerOwnerIndex = BallOwnerPlayerIndex;
        _retreatingFormerOwner = true;
        _advanceBoost = false; // stop boost on pass
        HomePlayers[_formerOwnerIndex].WasAdvancing = true;
        BallOwnerType = BallOwnershipType.Loose;
        BallOwnerPlayerIndex = -1;
        BallOwnerAwayIndex = -1;
        GameEvent?.Invoke(GameEventType.Pass);
    }

    public void QueueShoot()
    {
        if (BallOwnerType != BallOwnershipType.Player) return;
        _formerOwnerIndex = BallOwnerPlayerIndex;
        _retreatingFormerOwner = true;
        _advanceBoost = false;
        _shootStart = BallPos;
        var shootOffsetY = (Random.Shared.NextDouble() - 0.5) * 120;
        _shootEnd = new Point(ViewSize.Width - 14, ViewSize.Height / 2 + shootOffsetY);
        _shootTime = 0f;
        _shootActive = true;
        BallOwnerType = BallOwnershipType.Loose;
        BallOwnerPlayerIndex = -1;
        BallOwnerAwayIndex = -1;
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
        _defenderSideBoostY = 0;
        _defenderDiagBoostY = 0;
        _attackDiagonalBoostY = 0;
    }

    public void Update(double dt)
    {
        if (_awayPassCooldownTicks > 0)
            _awayPassCooldownTicks--;

        if (ViewSize.Width > 0)
        {
            var centerY = ViewSize.Height / 2 - _rightGoal.Height / 2;
            _rightGoal = new Rect(ViewSize.Width - _rightGoal.Width - 8, centerY, _rightGoal.Width, _rightGoal.Height);
            _leftGoal = new Rect(8, centerY, _leftGoal.Width, _leftGoal.Height);
            if (!_viewInitialized)
            {
                InitTeam(HomePlayers, 100, true);
                InitTeam(AwayPlayers, ViewSize.Width - 100, false);
                if (BallOwnerType == BallOwnershipType.Player && BallOwnerPlayerIndex >= 0)
                    BallPos = HomePlayers[BallOwnerPlayerIndex].Position;
                _viewInitialized = true;
            }
        }
        UpdatePlayers(dt);
        UpdateBall(dt);
        UpdateStatus();
    }

    void UpdatePlayers(double dt)
    {
        if (_resettingAfterGoal)
        {
            const double resetSpeed = 200;
            bool allArrived = true;
            foreach (var team in new[] { HomePlayers, AwayPlayers })
            {
                foreach (var a in team)
                {
                    var dx = a.BaseX - a.Position.X;
                    var dy = a.BaseY - a.Position.Y;
                    var dist = Math.Sqrt(dx * dx + dy * dy);
                    if (dist > 3)
                    {
                        allArrived = false;
                        var step = Math.Min(resetSpeed * dt, dist);
                        a.Position = new Point(
                            a.Position.X + dx / dist * step,
                            a.Position.Y + dy / dist * step);
                    }
                    else
                    {
                        a.Position = new Point(a.BaseX, a.BaseY);
                    }
                }
            }

            if (BallOwnerPlayerIndex >= 0)
                BallPos = HomePlayers[BallOwnerPlayerIndex].Position;
            else if (BallOwnerAwayIndex >= 0)
                BallPos = AwayPlayers[BallOwnerAwayIndex].Position;

            if (allArrived)
                _resettingAfterGoal = false;

            return;
        }

        double defenseFrontX = AwayPlayers.Skip(1).Min(a => a.Position.X);
        double pressLineX = defenseFrontX - 36;

        // Owner auto forward until press line
        if (BallOwnerType == BallOwnershipType.Player && BallOwnerPlayerIndex >= 0)
        {
            var owner = HomePlayers[BallOwnerPlayerIndex];
            double forwardExtra = _advanceBoost ? 220 : 0;
            double diagonalForwardExtra = _attackDiagonalBoostY == 0 ? 0 : 140;
            var nextPos = new Point(
                owner.Position.X + (ActiveMoveInput.X + forwardExtra + diagonalForwardExtra) * dt,
                owner.Position.Y + (ActiveMoveInput.Y + _attackDiagonalBoostY) * dt);

            owner.Position = nextPos;

            if (IsInsideRightGoalArea(nextPos))
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
            double desiredX = Math.Min(pressLineX - (i * 18), ViewSize.Width - 200);
            desiredX = Math.Max(p.BaseX + 30, desiredX);
            bool groupRetreat = (BallOwnerType == BallOwnershipType.Opponent) || (BallOwnerType == BallOwnershipType.Loose && !_passActive) || _shootActive;
            if (groupRetreat) desiredX = p.BaseX;
            double newX = p.Position.X + (desiredX - p.Position.X) * 0.02;
            p.Position = new Point(newX, Lerp(p.Position.Y, p.BaseY, 0.05));
            ClampActor(p);
        }

        // Home goalkeeper reacts to shots and ball carrier
        var homeGK = HomePlayers[0];
        if (_awayShootActive)
        {
            homeGK.Position = new Point(homeGK.BaseX, Lerp(homeGK.Position.Y, _awayShootEnd.Y, 0.18));
        }
        else if (BallOwnerType == BallOwnershipType.Opponent && BallOwnerAwayIndex >= 0)
        {
            var carrierY = AwayPlayers[BallOwnerAwayIndex].Position.Y;
            homeGK.Position = new Point(homeGK.BaseX, Lerp(homeGK.Position.Y, carrierY, 0.06));
        }
        else
        {
            var gSwing = Math.Sin(Environment.TickCount / 700.0) * 50;
            homeGK.Position = new Point(homeGK.BaseX, Lerp(homeGK.Position.Y, ViewSize.Height / 2 + gSwing, 0.05));
        }
        ClampActor(homeGK);

        // Away goalkeeper reacts to home shots
        var awayGK = AwayPlayers[0];
        if (_shootActive)
        {
            awayGK.Position = new Point(awayGK.BaseX, Lerp(awayGK.Position.Y, _shootEnd.Y, 0.18));
        }
        else if (BallOwnerType == BallOwnershipType.Player && BallOwnerPlayerIndex >= 0)
        {
            var carrierY = HomePlayers[BallOwnerPlayerIndex].Position.Y;
            awayGK.Position = new Point(awayGK.BaseX, Lerp(awayGK.Position.Y, carrierY, 0.06));
        }
        else
        {
            var gSwing = Math.Sin(Environment.TickCount / 700.0 + 3) * 50;
            awayGK.Position = new Point(awayGK.BaseX, Lerp(awayGK.Position.Y, ViewSize.Height / 2 + gSwing, 0.05));
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
                // Idle: gentle sway at base position
                var swing = Math.Sin(Environment.TickCount / 600.0 + i) * 40;
                a.Position = new Point(a.BaseX, a.BaseY + swing);
                ClampActor(a);
                continue;
            }

            if (awayAttacking && i == BallOwnerAwayIndex)
            {
                if (_awayBreakthrough)
                {
                    // Breaking through: charge toward goal area
                    double attackStopX = GoalCenterInset + GoalAreaRadius + 30;
                    double newX = a.Position.X;
                    if (a.Position.X > attackStopX)
                        newX -= 160 * dt;
                    double targetY = ViewSize.Height / 2 + (a.Position.Y > ViewSize.Height / 2 ? 30 : -30);
                    double newY = Lerp(a.Position.Y, targetY, 0.08);
                    a.Position = new Point(newX, newY);

                    var awayShootLineX = attackStopX + 4;
                    if (!_awayShootActive && a.Position.X <= awayShootLineX)
                        StartAwayShoot(a.Position);
                }
                else
                {
                    // Build-up: hold position on the arc, move laterally
                    var arcPos = GetArcPosition(i, arcCenterX, arcRadius);
                    a.Position = new Point(
                        Lerp(a.Position.X, arcPos.X, 0.06),
                        Lerp(a.Position.Y, arcPos.Y, 0.06));

                    // Pass or breakthrough decision
                    if (!_awayPassActive && _awayPassCooldownTicks == 0)
                    {
                        // After enough passes, chance to break through
                        double breakChance = _awayBuildupPasses >= 3 ? 0.015 : 0.0;
                        if (_awayBuildupPasses >= 6) breakChance = 0.04;

                        if (Random.Shared.NextDouble() < breakChance)
                        {
                            _awayBreakthrough = true;
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
    }

    void UpdateBall(double dt)
    {
        if (_resettingAfterGoal) return;

        if (_awayPassActive && _awayPassTargetIndex >= 0)
        {
            var target = AwayPlayers[_awayPassTargetIndex].Position;
            BallPos = LerpPoint(BallPos, target, 0.32);
            if (Distance(BallPos, target) < 14)
            {
                BallOwnerType = BallOwnershipType.Opponent;
                BallOwnerAwayIndex = _awayPassTargetIndex;
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

                if (_leftGoal.Contains(BallPos))
                {
                    ScoreAway++;
                    SetStatusOverride("Motståndaren skjuter och gör mål", 120);
                    GameEvent?.Invoke(GameEventType.GoalAway);
                    ResetAfterScore(homeScored: false);
                    return;
                }

                var homeKeeper = HomePlayers[0];
                bool keeperSave = IsShotNearKeeperLine(_awayShootStart, _awayShootEnd, homeKeeper.Position) && Random.Shared.NextDouble() < 0.80;
                if (keeperSave)
                {
                    GameEvent?.Invoke(GameEventType.Save);
                    GiveBallToPlayer(1, "Målvaktsräddning");
                    return;
                }

                var interceptor = TryGetInterception(HomePlayers, 1, _awayShootStart, _awayShootEnd, 0.10);
                if (interceptor >= 1)
                {
                    GameEvent?.Invoke(GameEventType.Interception);
                    GiveBallToPlayer(interceptor, "Brytning av försvarare");
                    return;
                }

                GiveBallToPlayer(GetNearestHomeIndex(BallPos), "Skott räddat: hemmaboll");
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

                if (_rightGoal.Contains(BallPos)) { ScoreHome++; SetStatusOverride("MÅL!", 120); GameEvent?.Invoke(GameEventType.GoalHome); ResetAfterScore(homeScored: true); return; }

                var awayKeeper = AwayPlayers[0];
                bool keeperSave = IsShotNearKeeperLine(_shootStart, _shootEnd, awayKeeper.Position) && Random.Shared.NextDouble() < 0.80;
                if (keeperSave)
                {
                    GameEvent?.Invoke(GameEventType.Save);
                    GiveBallToOpponent(1, "Målvaktsräddning");
                    return;
                }

                var interceptor = TryGetInterception(AwayPlayers, 1, _shootStart, _shootEnd, 0.10);
                if (interceptor >= 1)
                {
                    GameEvent?.Invoke(GameEventType.Interception);
                    GiveBallToOpponent(interceptor, "Brytning av försvarare");
                    return;
                }

                GiveBallToOpponent(GetNearestAwayIndex(BallPos), "Skott missat/räddat: motståndarboll");
            }
            return;
        }

        if (_passActive)
        {
            var target = HomePlayers[_passTargetHomeIndex].Position;
            BallPos = LerpPoint(BallPos, target, 0.35);
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
            Actor nearest = HomePlayers[1]; double best = Distance(BallPos, nearest.Position);
            for (int i = 1; i < HomePlayers.Length; i++)
            {
                var d = Distance(BallPos, HomePlayers[i].Position); if (d < best) { best = d; nearest = HomePlayers[i]; }
            }
            BallPos = LerpPoint(BallPos, nearest.Position, 0.08);
            if (best < 18) { BallOwnerType = BallOwnershipType.Player; BallOwnerPlayerIndex = Array.IndexOf(HomePlayers, nearest); }
        }
    }

    void ResetAfterScore(bool homeScored)
    {
        SetTeamBasePositions(HomePlayers, 100, true);
        SetTeamBasePositions(AwayPlayers, ViewSize.Width - 100, false);
        ControlledDefenderIndex = 1;
        _passActive = false; _shootActive = false; _awayShootActive = false; _awayPassActive = false; _awayPassTargetIndex = -1; _awayBuildupPasses = 0; _awayBreakthrough = false; _retreatingFormerOwner = false; _formerOwnerIndex = -1;
        _defenderSideBoostY = 0;
        _defenderDiagBoostY = 0;
        _attackDiagonalBoostY = 0;
        _advanceBoost = false;
        _defenderAdvanceBoost = false;
        _viewInitialized = true;
        _resettingAfterGoal = true;

        if (homeScored)
        {
            BallOwnerType = BallOwnershipType.Opponent;
            BallOwnerAwayIndex = 1;
            BallOwnerPlayerIndex = -1;
            _awayBuildupPasses = 0;
            _awayBreakthrough = false;
            _awayPassCooldownTicks = 120;
        }
        else
        {
            BallOwnerType = BallOwnershipType.Player;
            BallOwnerPlayerIndex = 1;
            BallOwnerAwayIndex = -1;
        }
    }

    void UpdateStatus()
    {
        if (_statusOverrideTicks > 0)
        {
            _statusOverrideTicks--;
            StatusText = _statusOverrideText;
            return;
        }

        if (_shootActive) { StatusText = "Shooting"; return; }
        if (_passActive) { StatusText = "Pass in air"; return; }
        StatusText = BallOwnerType switch
        {
            BallOwnershipType.Player => $"Owner: Home #{BallOwnerPlayerIndex}",
            BallOwnershipType.Opponent => $"Försvarar med #{ControlledDefenderIndex} | Borta #{BallOwnerAwayIndex}",
            _ => "Loose ball"
        };
    }

    void ClampActor(Actor a)
    {
        if (ViewSize.Width <= 0) return;
        a.Position = new Point(Math.Clamp(a.Position.X, 20, ViewSize.Width - 20), Math.Clamp(a.Position.Y, 40, ViewSize.Height - 40));

        if (!a.IsGoalkeeper)
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
        BallOwnerPlayerIndex = -1;
        _advanceBoost = false;
        _passActive = false;
        _awayPassActive = false;
        _awayPassTargetIndex = -1;
        _awayBuildupPasses = 0;
        _awayBreakthrough = false;
        _awayPassCooldownTicks = 40;
        _attackDiagonalBoostY = 0;
        SetStatusOverride(reason);
    }

    void GiveBallToPlayer(int homeIndex, string reason)
    {
        BallOwnerType = BallOwnershipType.Player;
        BallOwnerPlayerIndex = homeIndex;
        BallOwnerAwayIndex = -1;
        _advanceBoost = false;
        _passActive = false;
        _awayPassActive = false;
        _awayPassTargetIndex = -1;
        _awayBuildupPasses = 0;
        _awayBreakthrough = false;
        _defenderSideBoostY = 0;
        SetStatusOverride(reason);
    }

    void StartAwayPass(int ownerIndex)
    {
        var candidates = Enumerable.Range(1, AwayPlayers.Length - 1).Where(i => i != ownerIndex).ToArray();
        if (candidates.Length == 0) return;

        _awayPassTargetIndex = candidates[Random.Shared.Next(candidates.Length)];
        _awayPassActive = true;
        _awayPassCooldownTicks = 30 + Random.Shared.Next(20);
        _awayBuildupPasses++;
        BallOwnerType = BallOwnershipType.Loose;
        BallOwnerAwayIndex = -1;
        BallOwnerPlayerIndex = -1;
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
        double angleRange = 140.0;
        double startAngle = -angleRange / 2;
        double angleDeg = startAngle + slot * (angleRange / (fieldCount - 1));
        double angleRad = angleDeg * Math.PI / 180.0;

        double x = arcCenterX + arcRadius * Math.Cos(angleRad);
        double y = ViewSize.Height / 2 + arcRadius * Math.Sin(angleRad);

        // Add subtle lateral drift for realism
        double drift = Math.Sin(Environment.TickCount / 800.0 + playerIndex * 1.5) * 18;
        return new Point(x, y + drift);
    }

    void StartAwayShoot(Point from)
    {
        _awayShootActive = true;
        _awayShootTime = 0f;
        _awayShootStart = from;
        var shootOffsetY = (Random.Shared.NextDouble() - 0.5) * 120;
        _awayShootEnd = new Point(14, ViewSize.Height / 2 + shootOffsetY);
        _awayPassActive = false;
        BallOwnerType = BallOwnershipType.Loose;
        BallOwnerAwayIndex = -1;
        BallOwnerPlayerIndex = -1;
        SetStatusOverride("Motståndaren skjuter", 60);
        GameEvent?.Invoke(GameEventType.AwayShoot);
    }

    int TryGetInterception(Actor[] team, int startIndex, Point from, Point to, double chance)
    {
        for (int i = startIndex; i < team.Length; i++)
        {
            var d = DistanceToSegment(team[i].Position, from, to);
            if (d < 20 && Random.Shared.NextDouble() < chance)
                return i;
        }
        return -1;
    }

    bool IsShotNearKeeperLine(Point from, Point to, Point keeperPos) => DistanceToSegment(keeperPos, from, to) < 24;

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
        const double collisionRadius = 24;
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

                if (Random.Shared.NextDouble() < 0.5)
                {
                    var freeThrowX = Math.Max(40, ViewSize.Width - GoalCenterInset - FreeThrowRadius - 8);
                    owner.Position = new Point(freeThrowX, Math.Clamp(owner.Position.Y, 70, ViewSize.Height - 70));
                    BallPos = owner.Position;
                    _advanceBoost = false;
                    _attackDiagonalBoostY = 0;
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
}

public class GameDrawable : IDrawable
{
    readonly GameState _state;
    readonly Color AranasBlue;
    readonly Color AranasBlueLight;
    readonly Color AranasWhite;
    readonly Color AwayRed = Colors.Crimson;

    public GameDrawable(GameState state)
    {
        _state = state;
        AranasBlue = ControlsApplication.Current?.Resources.TryGetValue("AranasBlue", out var b) == true ? (Color)b : Color.FromArgb("#003DA5");
        AranasBlueLight = ControlsApplication.Current?.Resources.TryGetValue("AranasBlueLight", out var bl) == true ? (Color)bl : Color.FromArgb("#2E7CF6");
        AranasWhite = ControlsApplication.Current?.Resources.TryGetValue("AranasWhite", out var w) == true ? (Color)w : Colors.White;
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        DrawField(canvas, dirtyRect);
        DrawPlayers(canvas);
        DrawBall(canvas);
        DrawPassIndicator(canvas);
        DrawScore(canvas, dirtyRect);
    }

    void DrawField(ICanvas canvas, RectF dirtyRect)
    {
        // Arena background (dark surround like spectator area)
        canvas.FillColor = Color.FromArgb("#2C1B0E");
        canvas.FillRectangle(dirtyRect);

        var fieldMargin = 14f;
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
        var goalAreaRadius = 120f;
        var freeThrowRadius = 168f;
        var leftGoalCenterX = 20f;
        var rightGoalCenterX = dirtyRect.Width - 20f;

        // Goal area fill (light tint)
        canvas.FillColor = Color.FromArgb("#22003DA5");
        canvas.FillCircle(leftGoalCenterX, centerY, goalAreaRadius);
        canvas.FillColor = Color.FromArgb("#22DC143C");
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
        DrawGoal(canvas, new RectF(4, centerY - 80, 16, 160), AranasBlue);
        DrawGoal(canvas, new RectF(dirtyRect.Width - 20, centerY - 80, 16, 160), AwayRed);
    }

    void DrawPlayers(ICanvas canvas)
    {
        for (int i = 0; i < _state.HomePlayers.Length; i++)
        {
            var p = _state.HomePlayers[i];
            bool isActive = _state.BallOwnerType == BallOwnershipType.Player && _state.BallOwnerPlayerIndex == i;
            bool isDefender = _state.IsHomeDefending && i == _state.ControlledDefenderIndex;
            var jerseyColor = i == 0 ? AranasBlueLight : AranasBlue;
            DrawPlayerFigure(canvas, p.Position, jerseyColor, i, isActive, isDefender, p.IsGoalkeeper);
        }

        for (int i = 0; i < _state.AwayPlayers.Length; i++)
        {
            var a = _state.AwayPlayers[i];
            bool isActive = _state.BallOwnerType == BallOwnershipType.Opponent && _state.BallOwnerAwayIndex == i;
            var jerseyColor = i == 0 ? Color.FromArgb("#FF5555") : AwayRed;
            DrawPlayerFigure(canvas, a.Position, jerseyColor, i, isActive, false, a.IsGoalkeeper);
        }
    }

    void DrawPlayerFigure(ICanvas canvas, Point pos, Color jerseyColor, int number,
        bool isActive, bool isDefender, bool isGoalkeeper)
    {
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

        // Selection ring
        float ringR = bodyW / 2 + 5;
        if (isActive)
        {
            canvas.StrokeColor = Colors.White;
            canvas.StrokeSize = 2.5f;
            canvas.DrawCircle(x, y + 2, ringR);
        }
        else if (isDefender)
        {
            canvas.StrokeColor = Colors.Gold;
            canvas.StrokeSize = 3;
            canvas.DrawCircle(x, y + 2, ringR);
        }
    }

    void DrawBall(ICanvas canvas)
    {
        float bx = (float)_state.BallPos.X;
        float by = (float)_state.BallPos.Y;

        // Ball shadow
        canvas.FillColor = Color.FromArgb("#33000000");
        canvas.FillCircle(bx + 1, by + 2, 7);

        // Ball glow during shots
        if (_state.IsShootActive || _state.IsAwayShootActive)
        {
            canvas.FillColor = Colors.Yellow.WithAlpha(0.25f);
            canvas.FillCircle(bx, by, 14);
        }

        // Ball
        canvas.FillColor = Color.FromArgb("#FF8C00");
        canvas.FillCircle(bx, by, 7);

        // Highlight
        canvas.FillColor = Colors.White.WithAlpha(0.4f);
        canvas.FillCircle(bx - 2, by - 2, 3);
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
        float pillW = 100, pillH = 28;
        float pillX = dirtyRect.Center.X - pillW / 2;
        canvas.FillColor = Color.FromArgb("#CC2C1B0E");
        canvas.FillRoundedRectangle(pillX, 6, pillW, pillH, 14);

        canvas.FontColor = AranasWhite;
        canvas.FontSize = 18;
        canvas.DrawString($"{_state.ScoreHome} - {_state.ScoreAway}",
            new RectF(0, 8, dirtyRect.Width, 24),
            G.HorizontalAlignment.Center, G.VerticalAlignment.Top);
    }

    void DrawGoal(ICanvas canvas, RectF rect, Color color)
    {
        // Goal depth/net background
        canvas.FillColor = Color.FromArgb("#44000000");
        canvas.FillRoundedRectangle(rect, 2);

        // Net pattern (horizontal lines)
        canvas.StrokeColor = Colors.White.WithAlpha(0.15f);
        canvas.StrokeSize = 1;
        for (float ny = rect.Top + 10; ny < rect.Bottom; ny += 10)
        {
            canvas.DrawLine(rect.Left + 1, ny, rect.Right - 1, ny);
        }

        // Goal frame (posts + crossbar)
        canvas.StrokeColor = color;
        canvas.StrokeSize = 4;
        canvas.DrawRoundedRectangle(rect, 2);
    }
}
