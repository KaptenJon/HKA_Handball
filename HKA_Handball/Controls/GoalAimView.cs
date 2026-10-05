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

    static RectF GetGoalBounds(float width, float height)
    {
        const float pad = 4;
        float goalHeight = Math.Max(0, Math.Min(height - 22, (width - pad * 2) * 2 / 3));
        float goalWidth = goalHeight * 1.5f;
        return new RectF((width - goalWidth) / 2, pad, goalWidth, goalHeight);
    }

    void OnTapped(object? sender, TappedEventArgs e)
    {
        var pos = e.GetPosition(_view);
        if (pos is not Point p) return;
        double viewW = _view.Width > 0 ? _view.Width : Width;
        double viewH = _view.Height > 0 ? _view.Height : Height;
        if (viewW <= 0 || viewH <= 0) return;

        // Map tap X to normalized goal position (0=left, 1=right in front view)
        var goal = GetGoalBounds((float)viewW, (float)viewH);
        if (goal.Width <= 0) return;
        double normalizedX = Math.Clamp((p.X - goal.Left) / goal.Width, 0, 1);
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

            var goal = GetGoalBounds(w, h);
            float goalLeft = goal.Left;
            float goalTop = goal.Top;
            float goalWidth = goal.Width;
            float goalHeight = goal.Height;
            if (goalWidth <= 20 || goalHeight <= 12) return;

            // Net background
            canvas.FillColor = Color.FromArgb("#243D56");
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
            canvas.StrokeColor = Colors.White;
            canvas.StrokeSize = 3;
            canvas.DrawRoundedRectangle(goalLeft, goalTop, goalWidth, goalHeight, 2);
            canvas.StrokeColor = _owner.GoalColor;
            canvas.StrokeSize = 3;
            for (float x = goalLeft + 8; x < goalLeft + goalWidth - 4; x += 16)
            {
                canvas.DrawLine(x, goalTop, Math.Min(x + 8, goalLeft + goalWidth), goalTop);
            }
            canvas.FontColor = Color.FromArgb("#BBDDFF");
            canvas.FontSize = 10;
            canvas.DrawString("SIKTA / SKJUT", 0, h - 14, w, 14,
                HorizontalAlignment.Center, VerticalAlignment.Center);
        }
    }
}
