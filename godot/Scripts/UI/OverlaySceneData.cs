using System.Collections.Generic;
using Godot;
using ShotV.World;

namespace ShotV.UI;

public sealed class OverlayWorldSnapshot
{
    public Rect2 Bounds { get; init; }
    public Rect2 CameraBounds { get; init; }
    public Vector2 PlayerPosition { get; init; }
    public string? HighlightedMarkerId { get; init; }
    public List<WorldObstacle> Obstacles { get; init; } = new();
    public List<WorldMarker> Markers { get; init; } = new();
    public List<Vector2> EnemyPositions { get; init; } = new();
}

public interface IOverlaySceneDataProvider
{
    OverlayWorldSnapshot? BuildOverlayWorldSnapshot();
}
