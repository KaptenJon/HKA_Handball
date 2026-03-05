using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace HKA_Handball.Controls;

public class JoystickView : ContentView
{
    readonly GraphicsView _view;
    readonly JoystickDrawable _drawable;
    readonly PanGestureRecognizer _pan;

    public event EventHandler<Point>? ValueChanged;

    public static readonly BindableProperty ValueProperty = BindableProperty.Create(
        nameof(Value), typeof(Point), typeof(JoystickView), default(Point), BindingMode.OneWayToSource);

    public Point Value
    {
        get => (Point)GetValue(ValueProperty);
        private set => SetValue(ValueProperty, value);
    }

    public JoystickView()
    {
        _drawable = new JoystickDrawable(() => _knobOffset, () => _pressed);
        _view = new GraphicsView { Drawable = _drawable, HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.End };

        _pan = new PanGestureRecognizer();
        _pan.PanUpdated += OnPanUpdated;
        _view.GestureRecognizers.Add(_pan);

        Content = _view;
    }

    bool _pressed;
    Point _knobOffset = Point.Zero;

    void OnPanUpdated(object? sender, PanUpdatedEventArgs e)
    {
        double radius = Math.Max(0, Math.Min(Width, Height) * 0.5 - 16);
        if (radius < 1) radius = 1;
        double max = radius; // max knob distance from center

        switch (e.StatusType)
        {
            case GestureStatus.Started:
                _pressed = true;
                _knobOffset = Point.Zero;
                Value = Point.Zero;
                ValueChanged?.Invoke(this, Value);
                _view.Invalidate();
                break;
            case GestureStatus.Running:
                var dx = e.TotalX;
                var dy = e.TotalY;
                var len = Math.Sqrt(dx * dx + dy * dy);
                if (len > max && len > 0)
                {
                    dx = dx * max / len;
                    dy = dy * max / len;
                }
                _knobOffset = new Point(dx, dy);
                Value = new Point(dx / max, dy / max);
                ValueChanged?.Invoke(this, Value);
                _view.Invalidate();
                break;
            case GestureStatus.Completed:
            case GestureStatus.Canceled:
                _pressed = false;
                _knobOffset = Point.Zero;
                Value = Point.Zero;
                ValueChanged?.Invoke(this, Value);
                _view.Invalidate();
                break;
        }
    }

    class JoystickDrawable : IDrawable
    {
        readonly Func<Point> _getOffset;
        readonly Func<bool> _getPressed;
        public JoystickDrawable(Func<Point> getOffset, Func<bool> getPressed)
        {
            _getOffset = getOffset;
            _getPressed = getPressed;
        }

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            var center = new Point(dirtyRect.Width / 2, dirtyRect.Height / 2);
            var radius = Math.Min(dirtyRect.Width, dirtyRect.Height) / 2 - 8;
            var knobRadius = Math.Min(24, radius * 0.4f);

            var blue = Application.Current?.Resources.TryGetValue("AranasBlue", out var b) == true ? (Color)b : Color.FromArgb("#003DA5");
            var blueLight = Application.Current?.Resources.TryGetValue("AranasBlueLight", out var bl) == true ? (Color)bl : Color.FromArgb("#2E7CF6");

            // base circle
            canvas.FillColor = Color.FromArgb("#22003DA5");
            canvas.FillCircle((float)center.X, (float)center.Y, (float)radius);
            canvas.StrokeColor = blueLight;
            canvas.StrokeSize = 2;
            canvas.DrawCircle((float)center.X, (float)center.Y, (float)radius);

            // cross lines
            canvas.StrokeColor = Color.FromArgb("#44FFFFFF");
            canvas.DrawLine((float)(center.X - radius), (float)center.Y, (float)(center.X + radius), (float)center.Y);
            canvas.DrawLine((float)center.X, (float)(center.Y - radius), (float)center.X, (float)(center.Y + radius));

            // knob
            var offset = _getOffset();
            var knob = new Point(center.X + offset.X, center.Y + offset.Y);
            canvas.FillColor = _getPressed() ? blue : blueLight;
            canvas.FillCircle((float)knob.X, (float)knob.Y, (float)knobRadius);
            canvas.StrokeColor = Colors.White;
            canvas.StrokeSize = 2;
            canvas.DrawCircle((float)knob.X, (float)knob.Y, (float)knobRadius);
        }
    }
}
