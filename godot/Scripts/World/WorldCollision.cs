using System.Collections.Generic;
using Godot;

namespace ShotV.World;

public static class WorldCollision
{
    public static Vector2 ResolveCircleWorldMovement(Vector2 position, float nextX, float nextY, float radius, Rect2 bounds, List<WorldObstacle> obstacles)
    {
        float startX = Mathf.Clamp(position.X, bounds.Position.X + radius, bounds.End.X - radius);
        float startY = Mathf.Clamp(position.Y, bounds.Position.Y + radius, bounds.End.Y - radius);
        float x = Mathf.Clamp(nextX, bounds.Position.X + radius, bounds.End.X - radius);

        if (CollidesAnyObstacle(x, startY, radius, obstacles))
            x = startX;

        float y = Mathf.Clamp(nextY, bounds.Position.Y + radius, bounds.End.Y - radius);

        if (CollidesAnyObstacle(x, y, radius, obstacles))
            y = startY;

        return new Vector2(x, y);
    }

    public static Vector2 ClipSegmentToWorld(Vector2 origin, Vector2 target, Rect2 bounds, List<WorldObstacle> obstacles, float padding = 0f)
    {
        float bestT = ClipSegmentToBoundsT(origin, target, bounds);

        foreach (var obs in obstacles)
        {
            var expanded = new Rect2(obs.X - padding, obs.Y - padding, obs.Width + padding * 2, obs.Height + padding * 2);
            float? hit = SegmentRectIntersection(origin, target, expanded);
            if (hit.HasValue && hit.Value >= 0f && hit.Value < bestT)
                bestT = hit.Value;
        }

        return origin + (target - origin) * bestT;
    }

    public static bool PointInsideObstacle(float x, float y, WorldObstacle obs)
    {
        return x >= obs.X && x <= obs.X + obs.Width && y >= obs.Y && y <= obs.Y + obs.Height;
    }

    private static float ClipSegmentToBoundsT(Vector2 origin, Vector2 target, Rect2 bounds)
    {
        float bestT = 1f;
        if (target.X != origin.X)
        {
            bestT = ResolveIntersection(bestT, (bounds.Position.X - origin.X) / (target.X - origin.X), origin, target, bounds, true);
            bestT = ResolveIntersection(bestT, (bounds.End.X - origin.X) / (target.X - origin.X), origin, target, bounds, true);
        }
        if (target.Y != origin.Y)
        {
            bestT = ResolveIntersection(bestT, (bounds.Position.Y - origin.Y) / (target.Y - origin.Y), origin, target, bounds, false);
            bestT = ResolveIntersection(bestT, (bounds.End.Y - origin.Y) / (target.Y - origin.Y), origin, target, bounds, false);
        }
        return bestT;
    }

    private static float? SegmentRectIntersection(Vector2 origin, Vector2 target, Rect2 rect)
    {
        float dx = target.X - origin.X;
        float dy = target.Y - origin.Y;
        float entry = 0f;
        float exit = 1f;

        float[][] checks = new[]
        {
            new[] { -dx, origin.X - rect.Position.X },
            new[] { dx, rect.End.X - origin.X },
            new[] { -dy, origin.Y - rect.Position.Y },
            new[] { dy, rect.End.Y - origin.Y },
        };

        foreach (var check in checks)
        {
            float p = check[0];
            float q = check[1];
            if (p == 0f)
            {
                if (q < 0f) return null;
                continue;
            }
            float ratio = q / p;
            if (p < 0f)
                entry = Mathf.Max(entry, ratio);
            else
                exit = Mathf.Min(exit, ratio);
            if (entry > exit) return null;
        }

        return entry > 0f ? entry : exit >= 0f ? 0f : null;
    }

    private static float ResolveIntersection(float currentBest, float candidateT, Vector2 origin, Vector2 target, Rect2 bounds, bool xAxis)
    {
        if (candidateT <= 0f || candidateT > currentBest) return currentBest;
        float px = origin.X + (target.X - origin.X) * candidateT;
        float py = origin.Y + (target.Y - origin.Y) * candidateT;
        if (xAxis && py >= bounds.Position.Y && py <= bounds.End.Y) return candidateT;
        if (!xAxis && px >= bounds.Position.X && px <= bounds.End.X) return candidateT;
        return currentBest;
    }

    private static bool CollidesAnyObstacle(float x, float y, float padding, List<WorldObstacle> obstacles)
    {
        foreach (var obs in obstacles)
        {
            float left = obs.X - padding;
            float top = obs.Y - padding;
            float right = obs.X + obs.Width + padding;
            float bottom = obs.Y + obs.Height + padding;
            if (x >= left && x <= right && y >= top && y <= bottom) return true;
        }
        return false;
    }
}
