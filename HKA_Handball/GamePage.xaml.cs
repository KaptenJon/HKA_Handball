using Microsoft.Maui;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Graphics.Platform;
using HKA_Handball.Controls;
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
    readonly GameState _state = new();
    readonly GameDrawable _drawable;
    readonly IDispatcherTimer _timer;
#if WINDOWS
    HashSet<VirtualKey> _keysDown = new();
#endif
    bool _advanceHeld;

    public GamePage()
    {
        InitializeComponent();
        _drawable = new GameDrawable(_state);
        GameView.Drawable = _drawable;

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
    public readonly Actor[] HomePlayers = new Actor[6];
    public readonly Actor[] AwayPlayers = new Actor[6];

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

    // Away shoot state
    bool _awayShootActive;
    float _awayShootTime;
    Point _awayShootStart;
    Point _awayShootEnd;

    // Away pass state
    bool _awayPassActive;
    int _awayPassTargetIndex = -1;
    int _awayPassesRemaining;
    int _awayPassCooldownTicks;

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

    public GameState()
    {
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

        // Away movement
        for (int i = 0; i < AwayPlayers.Length; i++)
        {
            var a = AwayPlayers[i];
            if (awayAttacking && i == BallOwnerAwayIndex)
            {
                double attackStopX = GoalCenterInset + GoalAreaRadius + 30;
                double newX = a.Position.X;
                if (a.Position.X > attackStopX)
                    newX -= 120 * dt;
                double targetY = ViewSize.Height / 2 + Math.Sin(Environment.TickCount / 500.0) * 70;
                double newY = a.Position.Y + (targetY - a.Position.Y) * 0.05;
                a.Position = new Point(newX, newY);

                if (_awayPassesRemaining > 0 && !_awayPassActive && _awayPassCooldownTicks == 0 && a.Position.X <= ViewSize.Width * 0.72)
                {
                    StartAwayPass(i);
                }

                var awayShootLineX = attackStopX + 4;
                if (!_awayPassActive && !_awayShootActive && !_shootActive && a.Position.X <= awayShootLineX)
                    StartAwayShoot(a.Position);
            }
            else if (awayAttacking && !a.IsGoalkeeper)
            {
                var carrierPos = BallOwnerAwayIndex >= 0 ? AwayPlayers[BallOwnerAwayIndex].Position : BallPos;
                double supportX = Math.Clamp(carrierPos.X + 60 + i * 4, 70, ViewSize.Width - 60);
                double supportY = a.BaseY + (carrierPos.Y - a.BaseY) * 0.25;
                double newX = a.Position.X + (supportX - a.Position.X) * 0.10;
                double newY = a.Position.Y + (supportY - a.Position.Y) * 0.08;
                a.Position = new Point(newX, newY);
            }
            else if (a.IsGoalkeeper)
            {
                var gSwing = Math.Sin(Environment.TickCount / 700.0) * 50;
                a.Position = new Point(a.BaseX, ViewSize.Height / 2 + gSwing);
            }
            else
            {
                var swing = Math.Sin(Environment.TickCount / 600.0 + i) * 40;
                a.Position = new Point(a.BaseX, a.BaseY + swing);
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
                    ResetAfterScore(homeScored: false);
                    return;
                }

                var homeKeeper = HomePlayers[0];
                bool keeperSave = IsShotNearKeeperLine(_awayShootStart, _awayShootEnd, homeKeeper.Position) && Random.Shared.NextDouble() < 0.80;
                if (keeperSave)
                {
                    GiveBallToPlayer(1, "Målvaktsräddning");
                    return;
                }

                var interceptor = TryGetInterception(HomePlayers, 1, _awayShootStart, _awayShootEnd, 0.10);
                if (interceptor >= 1)
                {
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

                if (_rightGoal.Contains(BallPos)) { ScoreHome++; SetStatusOverride("MÅL!", 120); ResetAfterScore(homeScored: true); return; }

                var awayKeeper = AwayPlayers[0];
                bool keeperSave = IsShotNearKeeperLine(_shootStart, _shootEnd, awayKeeper.Position) && Random.Shared.NextDouble() < 0.80;
                if (keeperSave)
                {
                    GiveBallToOpponent(1, "Målvaktsräddning");
                    return;
                }

                var interceptor = TryGetInterception(AwayPlayers, 1, _shootStart, _shootEnd, 0.10);
                if (interceptor >= 1)
                {
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
        _passActive = false; _shootActive = false; _awayShootActive = false; _awayPassActive = false; _awayPassTargetIndex = -1; _awayPassesRemaining = 0; _retreatingFormerOwner = false; _formerOwnerIndex = -1;
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
            _awayPassesRemaining = Random.Shared.Next(1, 4);
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
        _awayPassesRemaining = Random.Shared.Next(1, 4);
        _awayPassCooldownTicks = 25;
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
        _awayPassesRemaining = 0;
        _defenderSideBoostY = 0;
        SetStatusOverride(reason);
    }

    void StartAwayPass(int ownerIndex)
    {
        if (_awayPassesRemaining <= 0) return;

        var candidates = Enumerable.Range(1, AwayPlayers.Length - 1).Where(i => i != ownerIndex).ToArray();
        if (candidates.Length == 0) return;

        _awayPassTargetIndex = candidates[Random.Shared.Next(candidates.Length)];
        _awayPassActive = true;
        _awayPassesRemaining--;
        _awayPassCooldownTicks = 35;
        BallOwnerType = BallOwnershipType.Loose;
        BallOwnerAwayIndex = -1;
        BallOwnerPlayerIndex = -1;
        SetStatusOverride("Motståndaren passar", 40);
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

    Microsoft.Maui.Graphics.IImage? _imgPlayerHome;
    Microsoft.Maui.Graphics.IImage? _imgPlayerAway;
    Microsoft.Maui.Graphics.IImage? _imgKeeperHome;
    Microsoft.Maui.Graphics.IImage? _imgKeeperAway;
    Microsoft.Maui.Graphics.IImage? _imgBall;
    bool _imagesLoaded;

    public GameDrawable(GameState state)
    {
        _state = state;
        AranasBlue = ControlsApplication.Current?.Resources.TryGetValue("AranasBlue", out var b) == true ? (Color)b : Color.FromArgb("#003DA5");
        AranasBlueLight = ControlsApplication.Current?.Resources.TryGetValue("AranasBlueLight", out var bl) == true ? (Color)bl : Color.FromArgb("#2E7CF6");
        AranasWhite = ControlsApplication.Current?.Resources.TryGetValue("AranasWhite", out var w) == true ? (Color)w : Colors.White;
        _ = LoadImagesAsync();
    }

    async Task LoadImagesAsync()
    {
        try
        {
            _imgPlayerHome = await LoadImageAsync("player_home.png");
            _imgPlayerAway = await LoadImageAsync("player_away.png");
            _imgKeeperHome = await LoadImageAsync("keeper_home.png");
            _imgKeeperAway = await LoadImageAsync("keeper_away.png");
            _imgBall = await LoadImageAsync("ball.png");
            _imagesLoaded = true;
        }
        catch
        {
            // Fallback to drawing primitives if images fail to load
        }
    }

    static async Task<Microsoft.Maui.Graphics.IImage?> LoadImageAsync(string filename)
    {
        using var stream = await FileSystem.OpenAppPackageFileAsync(filename);
        return Microsoft.Maui.Graphics.Platform.PlatformImage.FromStream(stream);
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        DrawField(canvas, dirtyRect);
        DrawPlayers(canvas, dirtyRect);
        DrawBall(canvas);
        DrawPassIndicator(canvas);
        DrawScore(canvas, dirtyRect);
    }

    void DrawField(ICanvas canvas, RectF dirtyRect)
    {
        // Field background
        canvas.FillColor = Color.FromArgb("#14532D");
        canvas.FillRectangle(dirtyRect);

        // Subtle inner field
        var fieldMargin = 14f;
        canvas.FillColor = Color.FromArgb("#166534");
        canvas.FillRoundedRectangle(fieldMargin, fieldMargin,
            dirtyRect.Width - fieldMargin * 2, dirtyRect.Height - fieldMargin * 2, 6);

        var centerX = dirtyRect.Center.X;
        var centerY = dirtyRect.Center.Y;

        // Field lines
        canvas.StrokeColor = Color.FromArgb("#88FFFFFF");
        canvas.StrokeSize = 2;
        canvas.DrawRoundedRectangle(fieldMargin, fieldMargin,
            dirtyRect.Width - fieldMargin * 2, dirtyRect.Height - fieldMargin * 2, 6);
        canvas.DrawLine(centerX, fieldMargin, centerX, dirtyRect.Height - fieldMargin);
        canvas.DrawCircle(centerX, centerY, 34);

        // Center dot
        canvas.FillColor = Color.FromArgb("#88FFFFFF");
        canvas.FillCircle(centerX, centerY, 4);

        var goalAreaRadius = 120f;
        var freeThrowRadius = 168f;
        var leftGoalCenterX = 20f;
        var rightGoalCenterX = dirtyRect.Width - 20f;

        // Goal areas (solid line)
        canvas.StrokeColor = Color.FromArgb("#66FFFFFF");
        canvas.StrokeSize = 2;
        canvas.DrawCircle(leftGoalCenterX, centerY, goalAreaRadius);
        canvas.DrawCircle(rightGoalCenterX, centerY, goalAreaRadius);

        // Free throw lines (dashed)
        canvas.StrokeDashPattern = [8, 6];
        canvas.StrokeColor = Color.FromArgb("#44FFFFFF");
        canvas.DrawCircle(leftGoalCenterX, centerY, freeThrowRadius);
        canvas.DrawCircle(rightGoalCenterX, centerY, freeThrowRadius);
        canvas.StrokeDashPattern = null;

        // Goals with fill
        DrawGoal(canvas, new RectF(8, centerY - 80, 12, 160), AranasBlue);
        DrawGoal(canvas, new RectF(dirtyRect.Width - 20, centerY - 80, 12, 160), AwayRed);
    }

    void DrawPlayers(ICanvas canvas, RectF dirtyRect)
    {
        for (int i = 0; i < _state.HomePlayers.Length; i++)
        {
            var p = _state.HomePlayers[i];
            var img = i == 0 ? _imgKeeperHome : _imgPlayerHome;
            var size = i == 0 ? 36f : 28f;
            bool isActive = _state.BallOwnerType == BallOwnershipType.Player && _state.BallOwnerPlayerIndex == i;
            bool isDefender = _state.IsHomeDefending && i == _state.ControlledDefenderIndex;

            DrawPlayer(canvas, p.Position, img, size, isActive, isDefender,
                i == 0 ? AranasBlueLight : AranasBlue, i);
        }

        for (int i = 0; i < _state.AwayPlayers.Length; i++)
        {
            var a = _state.AwayPlayers[i];
            var img = i == 0 ? _imgKeeperAway : _imgPlayerAway;
            var size = i == 0 ? 36f : 28f;
            bool isActive = _state.BallOwnerType == BallOwnershipType.Opponent && _state.BallOwnerAwayIndex == i;

            DrawPlayer(canvas, a.Position, img, size, isActive, false,
                i == 0 ? Color.FromArgb("#FF5555") : AwayRed, i);
        }
    }

    void DrawPlayer(ICanvas canvas, Point pos, Microsoft.Maui.Graphics.IImage? img, float size,
        bool isActive, bool isDefender, Color fallbackColor, int number)
    {
        float x = (float)pos.X;
        float y = (float)pos.Y;
        float half = size / 2;

        if (_imagesLoaded && img is not null)
        {
            canvas.DrawImage(img, x - half, y - half, size, size);
        }
        else
        {
            // Fallback: filled circle
            canvas.FillColor = fallbackColor;
            canvas.FillCircle(x, y, half);
        }

        // Selection ring
        if (isActive)
        {
            canvas.StrokeColor = AranasWhite;
            canvas.StrokeSize = 2.5f;
            canvas.DrawCircle(x, y, half + 3);
        }
        else if (isDefender)
        {
            canvas.StrokeColor = Colors.Gold;
            canvas.StrokeSize = 3;
            canvas.DrawCircle(x, y, half + 3);
        }

        // Player number
        if (number > 0)
        {
            canvas.FontColor = AranasWhite;
            canvas.FontSize = 9;
            canvas.DrawString(number.ToString(), x - 6, y + half + 1, 12, 12,
                G.HorizontalAlignment.Center, G.VerticalAlignment.Top);
        }
    }

    void DrawBall(ICanvas canvas)
    {
        float bx = (float)_state.BallPos.X;
        float by = (float)_state.BallPos.Y;

        if (_imagesLoaded && _imgBall is not null)
        {
            canvas.DrawImage(_imgBall, bx - 7, by - 7, 14, 14);
        }
        else
        {
            canvas.FillColor = Colors.Orange;
            canvas.FillCircle(bx, by, 6);
        }
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
        // Score background pill
        float pillW = 100, pillH = 28;
        float pillX = dirtyRect.Center.X - pillW / 2;
        canvas.FillColor = Color.FromArgb("#CC1A1A2E");
        canvas.FillRoundedRectangle(pillX, 6, pillW, pillH, 14);

        canvas.FontColor = AranasWhite;
        canvas.FontSize = 18;
        canvas.DrawString($"{_state.ScoreHome} - {_state.ScoreAway}",
            new RectF(0, 8, dirtyRect.Width, 24),
            G.HorizontalAlignment.Center, G.VerticalAlignment.Top);
    }

    void DrawGoal(ICanvas canvas, RectF rect, Color color)
    {
        canvas.FillColor = color.WithAlpha(0.3f);
        canvas.FillRoundedRectangle(rect, 2);
        canvas.StrokeColor = color;
        canvas.StrokeSize = 3;
        canvas.DrawRoundedRectangle(rect, 2);
    }
}
