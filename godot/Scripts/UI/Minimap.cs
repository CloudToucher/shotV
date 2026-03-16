using System.Collections.Generic;
using Godot;
using ShotV.Combat;
using ShotV.Core;
using ShotV.World;

namespace ShotV.UI;

public partial class Minimap : Control
{
    private Rect2 _worldBounds;
    private Vector2 _playerPos;
    private List<EnemyActor> _enemies = new();
    private List<WorldObstacle> _obstacles = new();
    private List<WorldMarker> _markers = new();

    private const float MinimapSize = 160f;
    private const float MinimapPadding = 12f;

    public void UpdateData(Rect2 worldBounds, Vector2 playerPos, IReadOnlyList<EnemyActor> enemies, List<WorldObstacle> obstacles, List<WorldMarker> markers)
    {
        _worldBounds = worldBounds;
        _playerPos = playerPos;
        _enemies = new List<EnemyActor>(enemies);
        _obstacles = obstacles;
        _markers = markers;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_worldBounds.Size.X <= 0 || _worldBounds.Size.Y <= 0) return;

        float aspect = _worldBounds.Size.X / _worldBounds.Size.Y;
        float mapW, mapH;
        if (aspect >= 1f) { mapW = MinimapSize; mapH = MinimapSize / aspect; }
        else { mapH = MinimapSize; mapW = MinimapSize * aspect; }

        var origin = new Vector2(MinimapPadding, MinimapPadding);

        // Background
        DrawRect(new Rect2(origin, new Vector2(mapW, mapH)), new Color(Palette.MinimapBg, 0.85f));
        DrawRect(new Rect2(origin, new Vector2(mapW, mapH)), new Color(Palette.MinimapBorder, 0.6f), false, 1.5f);

        // Obstacles
        foreach (var obs in _obstacles)
        {
            var r = WorldToMinimap(obs.X, obs.Y, obs.Width, obs.Height, origin, mapW, mapH);
            DrawRect(r, new Color(Palette.MinimapObstacle, 0.5f));
        }

        // Markers
        foreach (var marker in _markers)
        {
            var pt = WorldToMinimapPoint(marker.X, marker.Y, origin, mapW, mapH);
            DrawCircle(pt, 3f, new Color(Palette.MinimapMarker, 0.7f));
        }

        // Enemies
        foreach (var enemy in _enemies)
        {
            var pt = WorldToMinimapPoint(enemy.X, enemy.Y, origin, mapW, mapH);
            DrawCircle(pt, 2.5f, new Color(Palette.MinimapEnemy, 0.8f));
        }

        // Player
        var playerPt = WorldToMinimapPoint(_playerPos.X, _playerPos.Y, origin, mapW, mapH);
        DrawCircle(playerPt, 3.5f, Palette.MinimapPlayer);
    }

    private Rect2 WorldToMinimap(float wx, float wy, float ww, float wh, Vector2 origin, float mapW, float mapH)
    {
        float sx = (wx - _worldBounds.Position.X) / _worldBounds.Size.X * mapW;
        float sy = (wy - _worldBounds.Position.Y) / _worldBounds.Size.Y * mapH;
        float sw = ww / _worldBounds.Size.X * mapW;
        float sh = wh / _worldBounds.Size.Y * mapH;
        return new Rect2(origin.X + sx, origin.Y + sy, sw, sh);
    }

    private Vector2 WorldToMinimapPoint(float wx, float wy, Vector2 origin, float mapW, float mapH)
    {
        float sx = (wx - _worldBounds.Position.X) / _worldBounds.Size.X * mapW;
        float sy = (wy - _worldBounds.Position.Y) / _worldBounds.Size.Y * mapH;
        return new Vector2(origin.X + sx, origin.Y + sy);
    }
}
