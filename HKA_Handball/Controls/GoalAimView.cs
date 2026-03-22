using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace HKA_Handball.Controls;

/// <summary>
/// A front-view mini goal that shows the goalkeeper position and lets the player aim shots by tapping.
/// Replaces the shoot button with a visual aiming interface.
/// </summary>
public class GoalAimView : ContentView
{
    readonly GraphicsView _view;
    readonly GoalAimDrawable _drawable;

    /// <summary>Normalized goalkeeper horizontal position in the goal (0=left, 1=right).</summary>
    public double GoalkeeperNormalizedX { get; set; } = 0.5;

    /// <summary>Color of the goal frame (posts and crossbar).</summary>
    public Color GoalColor { get; set; } = Colors.White;

    /// <summary>Color of the goalkeeper's jersey.</summary>
    public Color GoalkeeperJerseyColor { get; set; } = Colors.Red;

    /// <summary>Whether a shot is currently in flight (shows aim indicator).</summary>
    public bool ShowShotInProgress { get; set; }

    /// <summary>Normalized aim position (0=left, 1=right) for the active shot.</summary>
    public double ShotAimNormalizedX { get; set; }

    /// <summary>Fired when the player taps to aim a shot. The double value is the normalized aim position (0=left, 1=right).</summary>
    public event EventHandler<double>? ShotAimed;

    public GoalAimView()
    {
        _drawable = new GoalAimDrawable(this);
        _view = new GraphicsView
        {
            Drawable = _drawable,
            BackgroundColor = Colors.Transparent,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill
        };

        var tap = new TapGestureRecognizer();
        tap.Tapped += OnTapped;
        _view.GestureRecognizers.Add(tap);

        Content = _view;
    }

    /// <summary>Invalidate the internal graphics view to trigger a redraw.</summary>
    public void InvalidateView() => _view.Invalidate();

    void OnTapped(object? sender, TappedEventArgs e)
    {
        var pos = e.GetPosition(_view);
        if (pos is not Point p) return;
        double viewW = _view.Width > 0 ? _view.Width : Width;
        double viewH = _view.Height > 0 ? _view.Height : Height;
        if (viewW <= 0 || viewH <= 0) return;

        // Map tap X to normalized goal position (0=left, 1=right in front view)
        const float pad = 4f;
        double goalLeft = pad;
        double goalWidth = viewW - pad * 2;
        double normalizedX = Math.Clamp((p.X - goalLeft) / goalWidth, 0, 1);
        ShotAimed?.Invoke(this, normalizedX);
    }

    class GoalAimDrawable : IDrawable
    {
        readonly GoalAimView _owner;
        public GoalAimDrawable(GoalAimView owner) => _owner = owner;

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            float w = dirtyRect.Width;
            float h = dirtyRect.Height;
            if (w <= 0 || h <= 0) return;

            const float pad = 4f;
            float goalLeft = pad;
            float goalTop = pad;
            float goalWidth = w - pad * 2;
            float goalHeight = h - pad * 2;

            // Net background
            canvas.FillColor = Color.FromArgb("#55000000");
            canvas.FillRoundedRectangle(goalLeft, goalTop, goalWidth, goalHeight, 3);

            // Net mesh pattern
            canvas.StrokeColor = Colors.White.WithAlpha(0.15f);
            canvas.StrokeSize = 0.5f;
            for (float ny = goalTop + 8; ny < goalTop + goalHeight; ny += 8)
                canvas.DrawLine(goalLeft + 1, ny, goalLeft + goalWidth - 1, ny);
            for (float nx = goalLeft + 8; nx < goalLeft + goalWidth; nx += 8)
                canvas.DrawLine(nx, goalTop + 1, nx, goalTop + goalHeight - 1);

            // Aim indicator when shot is in progress
            if (_owner.ShowShotInProgress)
            {
                float aimX = goalLeft + (float)(_owner.ShotAimNormalizedX * goalWidth);
                float aimY = goalTop + goalHeight * 0.4f;
                canvas.FillColor = Colors.Yellow.WithAlpha(0.7f);
                canvas.FillCircle(aimX, aimY, 4);
                canvas.StrokeColor = Colors.Yellow;
                canvas.StrokeSize = 1;
                canvas.DrawCircle(aimX, aimY, 6);
            }

            // Goalkeeper figure — positioned based on normalized X
            float gkX = goalLeft + (float)(_owner.GoalkeeperNormalizedX * goalWidth);
            gkX = Math.Clamp(gkX, goalLeft + 10, goalLeft + goalWidth - 10);
            float gkBottomY = goalTop + goalHeight - 3;

            // GK body (jersey torso)
            float gkBodyW = 14f;
            float gkBodyH = goalHeight * 0.45f;
            float gkBodyTop = gkBottomY - gkBodyH;
            canvas.FillColor = _owner.GoalkeeperJerseyColor;
            canvas.FillRoundedRectangle(gkX - gkBodyW / 2, gkBodyTop, gkBodyW, gkBodyH, 3);

            // GK jersey stripe
            canvas.FillColor = Colors.White.WithAlpha(0.3f);
            canvas.FillRoundedRectangle(gkX - gkBodyW / 2 + 2, gkBodyTop + gkBodyH * 0.55f, gkBodyW - 4, 4, 1);

            // GK head
            float headR = 4f;
            canvas.FillColor = Color.FromArgb("#FFDAB9");
            canvas.FillCircle(gkX, gkBodyTop - headR + 1, headR);

            // GK hair
            canvas.FillColor = Color.FromArgb("#3E2723");
            canvas.FillCircle(gkX, gkBodyTop - headR - 0.5f, headR * 0.6f);

            // GK arms (spread out in ready position)
            canvas.StrokeColor = _owner.GoalkeeperJerseyColor;
            canvas.StrokeSize = 2.5f;
            float armY = gkBodyTop + gkBodyH * 0.25f;
            canvas.DrawLine(gkX - gkBodyW / 2, armY, gkX - gkBodyW / 2 - 8, armY - 6);
            canvas.DrawLine(gkX + gkBodyW / 2, armY, gkX + gkBodyW / 2 + 8, armY - 6);

            // GK legs
            canvas.StrokeColor = Color.FromArgb("#1A237E");
            canvas.StrokeSize = 2;
            canvas.DrawLine(gkX - 3, gkBottomY - 1, gkX - 5, gkBottomY + 2);
            canvas.DrawLine(gkX + 3, gkBottomY - 1, gkX + 5, gkBottomY + 2);

            // Goal frame (posts + crossbar) — draw on top
            canvas.StrokeColor = _owner.GoalColor;
            canvas.StrokeSize = 3;
            canvas.DrawRoundedRectangle(goalLeft, goalTop, goalWidth, goalHeight, 2);

            // Subtle interactive hint — pulsing border glow (only when not shooting)
            if (!_owner.ShowShotInProgress)
            {
                float pulse = (float)(0.2 + 0.15 * Math.Sin(Environment.TickCount / 300.0));
                canvas.StrokeColor = Colors.Yellow.WithAlpha(pulse);
                canvas.StrokeSize = 1.5f;
                canvas.DrawRoundedRectangle(goalLeft - 1, goalTop - 1, goalWidth + 2, goalHeight + 2, 3);
            }
        }
    }
}
