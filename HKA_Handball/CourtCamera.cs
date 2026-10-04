using System.Numerics;
using Microsoft.Maui.Graphics;

namespace HKA_Handball;

public enum CourtCameraMode { BroadcastFixed, BroadcastFollow, Tactical }

public readonly struct CourtCamera
{
    public const float ArenaVerticalScale = 0.55f;
    public const float ArenaShear = -0.10f;

    public float Scale { get; }
    public Matrix3x2 Transform { get; }

    public CourtCamera(RectF viewport, bool tilted, float zoom = 1, Point? focus = null)
    {
        float verticalScale = tilted ? ArenaVerticalScale : 1;
        float shear = tilted ? ArenaShear : 0;
        float projectedWidth = (float)GameState.CourtWidth + Math.Abs(shear) * (float)GameState.CourtHeight;
        float projectedHeight = (float)GameState.CourtHeight * verticalScale;
        Scale = Math.Max(0.01f, Math.Min(viewport.Width / projectedWidth, viewport.Height / projectedHeight)) * zoom;
        float halfWidth = viewport.Width / (2 * Scale);
        float halfHeight = viewport.Height / (2 * Scale * verticalScale);
        float focusY = halfHeight >= GameState.CourtHeight / 2 ? (float)GameState.CourtHeight / 2
            : Math.Clamp((float)(focus?.Y ?? GameState.CourtHeight / 2),
                halfHeight, (float)GameState.CourtHeight - halfHeight);
        float horizontalPadding = halfWidth + Math.Abs(shear) * Math.Min(halfHeight, (float)GameState.CourtHeight / 2);
        float focusX = horizontalPadding >= GameState.CourtWidth / 2 ? (float)GameState.CourtWidth / 2
            : Math.Clamp((float)(focus?.X ?? GameState.CourtWidth / 2),
                horizontalPadding, (float)GameState.CourtWidth - horizontalPadding);
        Transform = new Matrix3x2(
            Scale, 0, shear * Scale, verticalScale * Scale,
            viewport.Center.X - Scale * (focusX + shear * focusY),
            viewport.Center.Y - Scale * verticalScale * focusY);
    }

    public Point Project(Point world)
    {
        var screen = Vector2.Transform(new Vector2((float)world.X, (float)world.Y), Transform);
        return new Point(screen.X, screen.Y);
    }

    public Point Unproject(Point screen)
    {
        Matrix3x2.Invert(Transform, out var inverse);
        var world = Vector2.Transform(new Vector2((float)screen.X, (float)screen.Y), inverse);
        return new Point(world.X, world.Y);
    }
}
