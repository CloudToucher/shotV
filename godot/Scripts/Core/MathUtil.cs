using Godot;

namespace ShotV.Core;

public static class MathUtil
{
    public static float Clamp(float value, float min, float max)
        => Mathf.Clamp(value, min, max);

    public static float Lerp(float start, float end, float t)
        => start + (end - start) * t;

    public static float EaseOutCubic(float value)
        => 1f - Mathf.Pow(1f - value, 3f);

    public static Vector2 ClampToDistance(Vector2 point, Vector2 origin, float maxDistance)
    {
        var delta = point - origin;
        float dist = delta.Length();
        if (dist <= maxDistance || dist <= 0.0001f) return point;
        return origin + delta * (maxDistance / dist);
    }

    public static Vector2 ClipToArena(Vector2 origin, Vector2 target, Rect2 arena)
    {
        float bestT = 1f;
        if (target.X != origin.X)
        {
            bestT = ResolveIntersection(bestT, (arena.Position.X - origin.X) / (target.X - origin.X), origin, target, arena, true);
            bestT = ResolveIntersection(bestT, (arena.End.X - origin.X) / (target.X - origin.X), origin, target, arena, true);
        }
        if (target.Y != origin.Y)
        {
            bestT = ResolveIntersection(bestT, (arena.Position.Y - origin.Y) / (target.Y - origin.Y), origin, target, arena, false);
            bestT = ResolveIntersection(bestT, (arena.End.Y - origin.Y) / (target.Y - origin.Y), origin, target, arena, false);
        }
        return origin + (target - origin) * bestT;
    }

    public static float? SegmentCircleIntersection(Vector2 origin, Vector2 target, Vector2 circle, float radius)
    {
        float dx = target.X - origin.X;
        float dy = target.Y - origin.Y;
        float ox = origin.X - circle.X;
        float oy = origin.Y - circle.Y;
        float a = dx * dx + dy * dy;
        float b = 2f * (ox * dx + oy * dy);
        float c = ox * ox + oy * oy - radius * radius;
        float disc = b * b - 4f * a * c;
        if (a <= 0.0001f || disc < 0f) return null;
        float root = Mathf.Sqrt(disc);
        float near = (-b - root) / (2f * a);
        float far = (-b + root) / (2f * a);
        if (near >= 0f && near <= 1f) return near;
        if (far >= 0f && far <= 1f) return far;
        return null;
    }

    public static uint BuildLayoutSeedFromText(string text)
    {
        uint hash = 2166136261u;
        foreach (char ch in text)
        {
            hash ^= ch;
            hash *= 16777619u;
        }
        return hash;
    }

    private static float ResolveIntersection(float currentBest, float candidateT, Vector2 origin, Vector2 target, Rect2 arena, bool xAxis)
    {
        if (candidateT <= 0f || candidateT > currentBest) return currentBest;
        float px = origin.X + (target.X - origin.X) * candidateT;
        float py = origin.Y + (target.Y - origin.Y) * candidateT;
        if (xAxis && py >= arena.Position.Y && py <= arena.End.Y) return candidateT;
        if (!xAxis && px >= arena.Position.X && px <= arena.End.X) return candidateT;
        return currentBest;
    }
}

public class SeededRng
{
    private uint _state;

    public SeededRng(uint seed)
    {
        _state = seed;
    }

    public float Next()
    {
        _state = (_state ^ (_state >> 15)) * (1u | _state);
        _state ^= _state + (_state ^ (_state >> 7)) * (61u | _state);
        return ((_state ^ (_state >> 14)) >> 0) / 4294967296f;
    }
}
