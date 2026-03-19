using Godot;
using ShotV.Combat;
using ShotV.Core;
using ShotV.Data;
using ShotV.World;

namespace ShotV.UI;

public partial class OverviewMapControl : Control
{
    private OverlayWorldSnapshot? _snapshot;

    public void SetSnapshot(OverlayWorldSnapshot? snapshot)
    {
        _snapshot = snapshot;
        Visible = snapshot != null;
        QueueRedraw();
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public override void _Draw()
    {
        if (_snapshot == null)
            return;

        var bounds = _snapshot.Bounds;
        if (bounds.Size.X <= 0f || bounds.Size.Y <= 0f)
            return;

        var rect = GetRect();
        DrawRect(rect, new Color(Palette.WorldFloor, 0.98f));

        float padding = 22f;
        float availableWidth = Mathf.Max(1f, rect.Size.X - padding * 2f);
        float availableHeight = Mathf.Max(1f, rect.Size.Y - padding * 2f);
        float scale = Mathf.Min(availableWidth / bounds.Size.X, availableHeight / bounds.Size.Y);
        var mapSize = bounds.Size * scale;
        var origin = rect.Position + (rect.Size - mapSize) * 0.5f - bounds.Position * scale;
        var worldRect = new Rect2(origin + bounds.Position * scale, mapSize);

        DrawRect(worldRect, new Color(Palette.WorldFloorDeep, 0.82f));
        DrawRect(worldRect, new Color(Palette.Frame, 0.28f), false, 2f);

        DrawGrid(worldRect, bounds, scale);
        DrawObstacles(origin, scale);
        DrawMarkers(origin, scale);
        DrawEnemies(origin, scale);
        DrawCamera(origin, scale);
        DrawPlayer(origin, scale);
    }

    private void DrawGrid(Rect2 worldRect, Rect2 bounds, float scale)
    {
        const float step = CombatConstants.GridSize * 4f;
        var lineColor = new Color(Palette.WorldLineStrong, 0.2f);

        for (float x = bounds.Position.X + step; x < bounds.End.X; x += step)
        {
            float px = worldRect.Position.X + (x - bounds.Position.X) * scale;
            DrawLine(new Vector2(px, worldRect.Position.Y), new Vector2(px, worldRect.End.Y), lineColor, 1f);
        }

        for (float y = bounds.Position.Y + step; y < bounds.End.Y; y += step)
        {
            float py = worldRect.Position.Y + (y - bounds.Position.Y) * scale;
            DrawLine(new Vector2(worldRect.Position.X, py), new Vector2(worldRect.End.X, py), lineColor, 1f);
        }
    }

    private void DrawObstacles(Vector2 origin, float scale)
    {
        foreach (var obstacle in _snapshot!.Obstacles)
        {
            var rect = new Rect2(
                origin.X + obstacle.X * scale,
                origin.Y + obstacle.Y * scale,
                obstacle.Width * scale,
                obstacle.Height * scale);

            Color color = obstacle.Kind switch
            {
                ObstacleKind.Station => Palette.ObstacleStation,
                ObstacleKind.Locker => Palette.ObstacleLocker,
                ObstacleKind.Cover => Palette.ObstacleCover,
                _ => Palette.ObstacleFill,
            };

            DrawRect(rect, new Color(color, 0.84f));
            DrawRect(rect, new Color(Palette.ObstacleEdge, 0.5f), false, 1f);
        }
    }

    private void DrawMarkers(Vector2 origin, float scale)
    {
        foreach (var marker in _snapshot!.Markers)
        {
            var center = new Vector2(origin.X + marker.X * scale, origin.Y + marker.Y * scale);
            Color color = marker.Kind switch
            {
                MarkerKind.Objective => Palette.Warning,
                MarkerKind.Extraction => Palette.MinimapExtraction,
                MarkerKind.Locker => Palette.PanelWarm,
                MarkerKind.Station => Palette.Frame,
                _ => Palette.Frame,
            };

            float radius = marker.Id == _snapshot.HighlightedMarkerId ? 8f : 6f;
            var points = new Vector2[]
            {
                center + new Vector2(0f, -radius),
                center + new Vector2(radius, 0f),
                center + new Vector2(0f, radius),
                center + new Vector2(-radius, 0f),
            };

            DrawColoredPolygon(points, new Color(color, 0.22f));
            DrawPolyline(new[] { points[0], points[1], points[2], points[3], points[0] }, new Color(color, 0.8f), marker.Id == _snapshot.HighlightedMarkerId ? 2f : 1.4f);
        }
    }

    private void DrawEnemies(Vector2 origin, float scale)
    {
        foreach (var enemyPos in _snapshot!.EnemyPositions)
        {
            var center = new Vector2(origin.X + enemyPos.X * scale, origin.Y + enemyPos.Y * scale);
            DrawCircle(center, 3.2f, new Color(Palette.MinimapEnemy, 0.92f));
        }
    }

    private void DrawCamera(Vector2 origin, float scale)
    {
        var camera = _snapshot!.CameraBounds;
        if (camera.Size.X <= 0f || camera.Size.Y <= 0f)
            return;

        var rect = new Rect2(
            origin.X + camera.Position.X * scale,
            origin.Y + camera.Position.Y * scale,
            camera.Size.X * scale,
            camera.Size.Y * scale);

        DrawRect(rect, new Color(Palette.Frame, 0.18f));
        DrawRect(rect, new Color(Palette.Frame, 0.78f), false, 2f);
    }

    private void DrawPlayer(Vector2 origin, float scale)
    {
        var player = new Vector2(origin.X + _snapshot!.PlayerPosition.X * scale, origin.Y + _snapshot.PlayerPosition.Y * scale);
        DrawCircle(player, 4.6f, Palette.MinimapPlayer);
        DrawCircle(player, 8.4f, new Color(Palette.MinimapPlayer, 0.22f));
    }
}
