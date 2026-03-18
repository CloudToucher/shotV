using System.Collections.Generic;
using Godot;
using ShotV.Core;
using ShotV.Data;
using ShotV.State;
using ShotV.World;

namespace ShotV.Combat;

public partial class CombatRenderer : Node2D
{
    private static readonly Vector2[] MeleeGlowShape =
    {
        new(0f, -20f), new(18f, 0f), new(0f, 20f), new(-18f, 0f),
    };

    private static readonly Vector2[] MeleeShellShape =
    {
        new(0f, -15f), new(14f, 0f), new(0f, 15f), new(-14f, 0f),
    };

    private static readonly Vector2[] RangedGlowShape =
    {
        new(-18f, -18f), new(18f, -18f), new(18f, 18f), new(-18f, 18f),
    };

    private static readonly Vector2[] RangedShellShape =
    {
        new(-12f, -12f), new(12f, -12f), new(12f, 12f), new(-12f, 12f),
    };

    private static readonly Vector2[] ChargerGlowShape =
    {
        new(18f, 0f), new(-4f, -19f), new(-12f, -8f), new(-12f, 8f), new(-4f, 19f),
    };

    private static readonly Vector2[] ChargerShellShape =
    {
        new(15f, 0f), new(-6f, -14f), new(-10f, -6f), new(-10f, 6f), new(-6f, 14f),
    };

    private static readonly Vector2[] StalkerGlowShape =
    {
        new(0f, -20f), new(16f, -8f), new(10f, 18f), new(-10f, 18f), new(-16f, -8f),
    };

    private static readonly Vector2[] StalkerShellShape =
    {
        new(0f, -15f), new(12f, -6f), new(8f, 14f), new(-8f, 14f), new(-12f, -6f),
    };

    private static readonly Vector2[] SuppressorGlowShape =
    {
        new(-18f, -16f), new(10f, -20f), new(20f, 0f), new(10f, 20f), new(-18f, 16f),
    };

    private static readonly Vector2[] SuppressorShellShape =
    {
        new(-13f, -12f), new(8f, -15f), new(15f, 0f), new(8f, 15f), new(-13f, 12f),
    };

    private static readonly Vector2[] BossGlowShape =
    {
        new(22f, 0f), new(10f, -22f), new(-12f, -22f), new(-24f, 0f), new(-12f, 22f), new(10f, 22f),
    };

    private static readonly Vector2[] BossShellShape =
    {
        new(19f, 0f), new(8f, -18f), new(-10f, -18f), new(-20f, 0f), new(-10f, 18f), new(8f, 18f),
    };

    private static readonly Vector2[] MeleeCoreShape =
    {
        new(-4f, -4f), new(4f, -4f), new(4f, 4f), new(-4f, 4f),
    };

    private static readonly Vector2[] RangedCoreShape =
    {
        new(-5f, -5f), new(5f, -5f), new(5f, 5f), new(-5f, 5f),
    };

    private static readonly Vector2[] ChargerCoreShape =
    {
        new(6f, 0f), new(-4f, -5f), new(-1f, 0f), new(-4f, 5f),
    };

    private static readonly Vector2[] StalkerCoreShape =
    {
        new(0f, -6f), new(5f, 2f), new(0f, 6f), new(-5f, 2f),
    };

    private static readonly Vector2[] SuppressorCoreShape =
    {
        new(7f, 0f), new(-3f, -6f), new(-6f, 0f), new(-3f, 6f),
    };

    private static readonly Vector2[] BossCoreShape =
    {
        new(10f, 0f), new(-6f, -7f), new(-1f, 0f), new(-6f, 7f),
    };

    private static readonly Vector2[] MeleeAimShape =
    {
        new(10f, 0f), new(-4f, -4f), new(-1f, 0f), new(-4f, 4f),
    };

    private static readonly Vector2[] RangedAimShape =
    {
        new(12f, 0f), new(-5f, -3f), new(-2f, 0f), new(-5f, 3f),
    };

    private static readonly Vector2[] ChargerAimShape =
    {
        new(13f, 0f), new(-5f, -4f), new(-1f, 0f), new(-5f, 4f),
    };

    private static readonly Vector2[] StalkerAimShape =
    {
        new(11f, 0f), new(-4f, -5f), new(-2f, 0f), new(-4f, 5f),
    };

    private static readonly Vector2[] SuppressorAimShape =
    {
        new(15f, 0f), new(-6f, -4f), new(-2f, 0f), new(-6f, 4f),
    };

    private static readonly Vector2[] BossAimShape =
    {
        new(18f, 0f), new(-6f, -5f), new(0f, 0f), new(-6f, 5f),
    };

    private WorldMapLayout? _layout;
    private EncounterManager? _encounter;
    private PlayerAvatar? _player;
    private List<GroundLootDrop>? _groundLoot;

    public void Bind(WorldMapLayout layout, EncounterManager encounter, PlayerAvatar player)
    {
        _layout = layout;
        _encounter = encounter;
        _player = player;
    }

    public void BindGroundLoot(List<GroundLootDrop> groundLoot)
    {
        _groundLoot = groundLoot;
    }

    public override void _Draw()
    {
        if (_layout == null || _encounter == null)
            return;

        DrawFloor();
        DrawObstacles();
        DrawMarkers();
        DrawGroundLoot();
        DrawEnemies();
        DrawProjectiles();
    }

    public void Refresh() => QueueRedraw();

    private void DrawFloor()
    {
        var bounds = _layout!.Bounds;
        DrawRect(bounds, Palette.WorldFloor);

        foreach (var region in _layout.Regions)
        {
            Color regionColor = region.Kind switch
            {
                WorldZoneKind.HighValue => Palette.Warning,
                WorldZoneKind.HighRisk => Palette.Danger,
                WorldZoneKind.Extraction => Palette.MinimapMarker,
                _ => Palette.Frame,
            };

            DrawRect(region.Bounds, new Color(regionColor, 0.06f + region.ThreatLevel * 0.015f));
            DrawRect(region.Bounds, new Color(regionColor, 0.22f), false, 2f);

            for (float x = region.Bounds.Position.X - region.Bounds.Size.Y; x < region.Bounds.End.X; x += 42f)
            {
                var start = new Vector2(Mathf.Max(region.Bounds.Position.X, x), Mathf.Clamp(region.Bounds.Position.Y + (region.Bounds.Position.X - x), region.Bounds.Position.Y, region.Bounds.End.Y));
                var end = new Vector2(Mathf.Min(region.Bounds.End.X, x + region.Bounds.Size.Y), Mathf.Clamp(region.Bounds.End.Y + (region.Bounds.Position.X - x), region.Bounds.Position.Y, region.Bounds.End.Y));
                DrawLine(start, end, new Color(regionColor, 0.05f), 1f);
            }
        }

        float majorStep = CombatConstants.GridSize * 4f;
        var gridColor = new Color(Palette.WorldLineStrong, 0.24f);
        for (float x = bounds.Position.X + majorStep; x < bounds.End.X; x += majorStep)
            DrawLine(new Vector2(x, bounds.Position.Y), new Vector2(x, bounds.End.Y), gridColor, 1f);
        for (float y = bounds.Position.Y + majorStep; y < bounds.End.Y; y += majorStep)
            DrawLine(new Vector2(bounds.Position.X, y), new Vector2(bounds.End.X, y), gridColor, 1f);

        for (float x = bounds.Position.X + majorStep; x < bounds.End.X; x += majorStep)
        {
            for (float y = bounds.Position.Y + majorStep; y < bounds.End.Y; y += majorStep)
            {
                DrawLine(new Vector2(x - 4f, y), new Vector2(x + 4f, y), new Color(Palette.WorldLine, 0.28f), 1f);
                DrawLine(new Vector2(x, y - 4f), new Vector2(x, y + 4f), new Color(Palette.WorldLine, 0.28f), 1f);
            }
        }

        const float inset = 12f;
        var warningRect = new Rect2(bounds.Position + new Vector2(inset, inset), bounds.Size - new Vector2(inset * 2f, inset * 2f));
        DrawRect(warningRect, new Color(Palette.Warning, 0.28f), false, 2f);

        for (float x = warningRect.Position.X; x < warningRect.End.X; x += 60f)
        {
            DrawRect(new Rect2(x, warningRect.Position.Y - 4f, 20f, 8f), new Color(Palette.Warning, 0.32f));
            DrawRect(new Rect2(x, warningRect.End.Y - 4f, 20f, 8f), new Color(Palette.Warning, 0.32f));
        }

        for (float y = warningRect.Position.Y; y < warningRect.End.Y; y += 60f)
        {
            DrawRect(new Rect2(warningRect.Position.X - 4f, y, 8f, 20f), new Color(Palette.Warning, 0.32f));
            DrawRect(new Rect2(warningRect.End.X - 4f, y, 8f, 20f), new Color(Palette.Warning, 0.32f));
        }

        DrawRect(bounds, new Color(Palette.WorldLineStrong, 0.58f), false, 2f);
    }

    private void DrawObstacles()
    {
        foreach (var obstacle in _layout!.Obstacles)
        {
            var rect = new Rect2(obstacle.X, obstacle.Y, obstacle.Width, obstacle.Height);
            Color accent = obstacle.Kind switch
            {
                ObstacleKind.Cover => Palette.Frame,
                ObstacleKind.Station => Palette.MinimapMarker,
                ObstacleKind.Locker => Palette.Warning,
                _ => Palette.FrameSoft,
            };

            DrawRect(new Rect2(rect.Position + new Vector2(6f, 6f), rect.Size), new Color(accent, 0.12f));
            DrawRect(rect, new Color(1f, 1f, 1f, 0.95f));

            float hatchStep = obstacle.Kind == ObstacleKind.Cover ? 8f : 12f;
            for (float i = 0f; i < rect.Size.X + rect.Size.Y; i += hatchStep)
            {
                float startX = Mathf.Max(0f, i - rect.Size.Y);
                float startY = Mathf.Min(rect.Size.Y, i);
                float endX = Mathf.Min(rect.Size.X, i);
                float endY = Mathf.Max(0f, i - rect.Size.X);
                DrawLine(
                    rect.Position + new Vector2(startX, startY),
                    rect.Position + new Vector2(endX, endY),
                    new Color(accent, 0.14f),
                    1f);
            }

            DrawRect(rect, new Color(accent, 0.84f), false, 1.5f);
            DrawLine(rect.Position + new Vector2(2f, rect.Size.Y - 2f), rect.Position + new Vector2(2f, 2f), new Color(1f, 1f, 1f, 0.82f), 2f);
            DrawLine(rect.Position + new Vector2(2f, 2f), rect.Position + new Vector2(rect.Size.X - 2f, 2f), new Color(1f, 1f, 1f, 0.82f), 2f);

            if (obstacle.Kind == ObstacleKind.Locker)
            {
                DrawRect(new Rect2(rect.Position + new Vector2(6f, 6f), new Vector2(rect.Size.X * 0.35f, rect.Size.Y - 12f)), new Color(accent, 0.08f));
                DrawRect(new Rect2(rect.Position + new Vector2(rect.Size.X * 0.35f + 12f, rect.Size.Y * 0.5f - 6f), new Vector2(4f, 12f)), new Color(accent, 0.8f));
            }
            else if (obstacle.Kind == ObstacleKind.Station)
            {
                var center = rect.GetCenter();
                DrawCircle(center, 10f, new Color(1f, 1f, 1f, 0.96f));
                DrawArc(center, 10f, 0f, Mathf.Tau, 24, new Color(accent, 0.8f), 1.5f);
                DrawCircle(center, 4f, new Color(accent, 0.9f));
                DrawLine(new Vector2(rect.Position.X + 6f, center.Y), center + new Vector2(-14f, 0f), new Color(accent, 0.4f), 1f);
                DrawLine(center + new Vector2(14f, 0f), new Vector2(rect.End.X - 6f, center.Y), new Color(accent, 0.4f), 1f);
            }

            DrawCornerBrackets(rect, accent, 8f, 2.5f, 1f);
        }
    }

    private void DrawMarkers()
    {
        float elapsed = Time.GetTicksMsec() * 0.001f;

        for (int index = 0; index < _layout!.Markers.Count; index++)
        {
            var marker = _layout.Markers[index];
            Color color = marker.Kind switch
            {
                MarkerKind.Objective => Palette.Warning,
                MarkerKind.Extraction => Palette.MinimapMarker,
                MarkerKind.Locker => Palette.Warning,
                MarkerKind.Station => Palette.Frame,
                _ => Palette.Frame,
            };

            float baseRadius = marker.Kind == MarkerKind.Objective ? 16f : 12f;
            float pulse = (Mathf.Sin(elapsed * 4f + index) + 1f) * 0.5f;
            float size = baseRadius + pulse * 4f;
            var center = new Vector2(marker.X, marker.Y);

            var outer = new[]
            {
                center + new Vector2(0f, -size),
                center + new Vector2(size, 0f),
                center + new Vector2(0f, size),
                center + new Vector2(-size, 0f),
            };
            var inner = new[]
            {
                center + new Vector2(0f, -size * 0.6f),
                center + new Vector2(size * 0.6f, 0f),
                center + new Vector2(0f, size * 0.6f),
                center + new Vector2(-size * 0.6f, 0f),
            };

            DrawPolyline(new[] { outer[0], outer[1], outer[2], outer[3], outer[0] }, new Color(color, 0.82f), 1.6f);
            DrawColoredPolygon(inner, new Color(color, 0.2f + pulse * 0.08f));
            DrawMarkerGlyph(center, marker.Kind, color, size * 0.4f);
        }
    }

    private void DrawEnemies()
    {
        float elapsed = Time.GetTicksMsec() * 0.001f;

        foreach (var enemy in _encounter!.Enemies)
        {
            var colors = enemy.Definition.Colors;
            float modePulse = GetModeStrength(enemy.Mode) + (enemy.Alerted ? 0.18f : 0f);
            float sway = Mathf.Sin(elapsed * 4.4f + enemy.Id * 0.71f) * 0.035f;
            float shellScale = 1f + enemy.AttackPulse * 0.06f + modePulse * 0.04f;
            float glowScale = 1f + modePulse * 0.16f + enemy.DamageFlash * 0.12f;
            float bodyFacing = enemy.Type is HostileType.Charger or HostileType.Stalker or HostileType.Suppressor or HostileType.Boss ? enemy.FacingAngle : 0f;
            float bodyWobble = enemy.Type is HostileType.Charger or HostileType.Stalker or HostileType.Suppressor or HostileType.Boss
                ? sway * 0.35f
                : sway + (enemy.LifeRatio - 0.5f) * 0.03f;

            var glow = TransformShape(GetGlowShape(enemy.Type), new Vector2(enemy.X, enemy.Y), bodyFacing + bodyWobble * 0.4f, glowScale);
            var shell = TransformShape(GetShellShape(enemy.Type), new Vector2(enemy.X, enemy.Y), bodyFacing + bodyWobble, shellScale);
            var core = TransformShape(GetCoreShape(enemy.Type), new Vector2(enemy.X, enemy.Y), bodyFacing + bodyWobble * 1.18f, 1f + enemy.DamageFlash * 0.24f + enemy.AttackPulse * 0.14f);

            DrawColoredPolygon(glow, new Color(colors.Glow, 0.12f + modePulse * 0.16f + enemy.DamageFlash * 0.18f));
            DrawColoredPolygon(shell, new Color(colors.Body, 0.9f + enemy.LifeRatio * 0.1f));
            DrawPolyline(CloseShape(shell), new Color(colors.Edge, 0.72f + enemy.DamageFlash * 0.18f + modePulse * 0.12f), enemy.Type == HostileType.Boss ? 2.2f : 2f);
            DrawColoredPolygon(core, new Color(Palette.ArenaCore, 0.76f + enemy.AttackPulse * 0.14f + enemy.DamageFlash * 0.18f));

            if (enemy.Alerted)
                DrawArc(new Vector2(enemy.X, enemy.Y), enemy.Definition.Radius + 10f + enemy.AttackPulse * 6f, 0f, Mathf.Tau, 24, new Color(Palette.Warning, 0.26f), 1.2f);

            float aimDistance = enemy.Definition.Radius + 10f + modePulse * 6f;
            var aimCenter = new Vector2(enemy.X, enemy.Y) + new Vector2(Mathf.Cos(enemy.FacingAngle), Mathf.Sin(enemy.FacingAngle)) * aimDistance;
            var aimShape = TransformShape(GetAimShape(enemy.Type), aimCenter, enemy.FacingAngle, 1f + modePulse * 0.12f + enemy.AttackPulse * 0.08f);
            DrawColoredPolygon(aimShape, new Color(colors.Edge, 0.28f + modePulse * 0.32f + enemy.AttackPulse * 0.18f));

            DrawEnemyHealth(enemy);
        }
    }

    private void DrawEnemyHealth(EnemyActor enemy)
    {
        bool visible = enemy.Type == HostileType.Boss || enemy.LifeRatio < 0.999f || enemy.DamageFlash > 0.08f;
        if (!visible)
            return;

        float width = enemy.Type == HostileType.Boss ? 62f : 30f;
        float y = enemy.Y - enemy.Definition.Radius - 14f;
        var back = new Rect2(enemy.X - width * 0.5f, y, width, 4f);
        DrawRect(back, new Color(Palette.UiText, 0.18f));
        DrawRect(new Rect2(back.Position, new Vector2(width * enemy.LifeRatio, 4f)), new Color(Palette.Dash, 0.92f));
    }

    private void DrawProjectiles()
    {
        foreach (var projectile in _encounter!.Projectiles)
        {
            var position = new Vector2(projectile.X, projectile.Y);
            DrawCircle(position, projectile.Radius + 3f, new Color(projectile.GlowColor, 0.3f));
            DrawCircle(position, projectile.Radius, projectile.DrawColor);
        }
    }

    private void DrawGroundLoot()
    {
        if (_groundLoot == null)
            return;

        float elapsed = Time.GetTicksMsec() * 0.001f;
        foreach (var drop in _groundLoot)
        {
            var center = new Vector2(drop.X, drop.Y);
            float pulse = Mathf.Sin(elapsed * 3.2f + drop.X * 0.01f) * 0.5f + 0.5f;
            float bob = Mathf.Sin(elapsed * 4.6f + drop.Y * 0.012f) * 2.4f;
            Color fill = drop.Source == LootSource.Boss ? Palette.Warning : Palette.MinimapMarker;
            float nearby = _player != null && _player.PlayerPosition.DistanceTo(center) <= 72f ? 1f : 0f;
            float size = 8f + pulse * 2.2f + nearby * 1.2f;
            var drawCenter = center + new Vector2(0f, bob);
            var diamond = new[]
            {
                drawCenter + new Vector2(0f, -size),
                drawCenter + new Vector2(size * 0.72f, 0f),
                drawCenter + new Vector2(0f, size),
                drawCenter + new Vector2(-size * 0.72f, 0f),
            };

            DrawCircle(center + new Vector2(0f, 12f), 7f + pulse * 1.4f, new Color(Palette.WorldLineStrong, 0.14f));
            DrawCircle(drawCenter, 16f + pulse * 3f + nearby * 3f, new Color(fill, 0.1f + pulse * 0.08f + nearby * 0.14f));
            DrawArc(drawCenter, 14f + nearby * 4f, 0f, Mathf.Tau, 24, new Color(fill, 0.24f + pulse * 0.18f + nearby * 0.18f), 1.6f);
            DrawColoredPolygon(diamond, new Color(fill, 0.88f));
            DrawPolyline(CloseShape(diamond), new Color(Palette.Reticle, 0.34f + nearby * 0.16f), 1.4f);
            DrawCircle(drawCenter, 3.4f, new Color(Palette.BgInner, 0.96f));

            if (nearby > 0f)
                DrawArc(drawCenter, 20f + pulse * 3f, 0f, Mathf.Tau, 28, new Color(Palette.Accent, 0.34f), 1.2f);
        }
    }

    private static Vector2[] GetGlowShape(HostileType type) => type switch
    {
        HostileType.Melee => MeleeGlowShape,
        HostileType.Ranged => RangedGlowShape,
        HostileType.Charger => ChargerGlowShape,
        HostileType.Stalker => StalkerGlowShape,
        HostileType.Suppressor => SuppressorGlowShape,
        _ => BossGlowShape,
    };

    private static Vector2[] GetShellShape(HostileType type) => type switch
    {
        HostileType.Melee => MeleeShellShape,
        HostileType.Ranged => RangedShellShape,
        HostileType.Charger => ChargerShellShape,
        HostileType.Stalker => StalkerShellShape,
        HostileType.Suppressor => SuppressorShellShape,
        _ => BossShellShape,
    };

    private static Vector2[] GetCoreShape(HostileType type) => type switch
    {
        HostileType.Melee => MeleeCoreShape,
        HostileType.Ranged => RangedCoreShape,
        HostileType.Charger => ChargerCoreShape,
        HostileType.Stalker => StalkerCoreShape,
        HostileType.Suppressor => SuppressorCoreShape,
        _ => BossCoreShape,
    };

    private static Vector2[] GetAimShape(HostileType type) => type switch
    {
        HostileType.Melee => MeleeAimShape,
        HostileType.Ranged => RangedAimShape,
        HostileType.Charger => ChargerAimShape,
        HostileType.Stalker => StalkerAimShape,
        HostileType.Suppressor => SuppressorAimShape,
        _ => BossAimShape,
    };

    private static Vector2[] TransformShape(Vector2[] source, Vector2 center, float rotation, float scale)
    {
        float cos = Mathf.Cos(rotation) * scale;
        float sin = Mathf.Sin(rotation) * scale;
        var result = new Vector2[source.Length];
        for (int index = 0; index < source.Length; index++)
        {
            var point = source[index];
            result[index] = new Vector2(
                center.X + point.X * cos - point.Y * sin,
                center.Y + point.X * sin + point.Y * cos);
        }
        return result;
    }

    private static Vector2[] CloseShape(Vector2[] source)
    {
        var result = new Vector2[source.Length + 1];
        for (int index = 0; index < source.Length; index++)
            result[index] = source[index];
        result[^1] = source[0];
        return result;
    }

    private void DrawMarkerGlyph(Vector2 center, MarkerKind kind, Color color, float size)
    {
        switch (kind)
        {
            case MarkerKind.Entry:
            case MarkerKind.Extraction:
                DrawLine(center + new Vector2(-size, 0f), center + new Vector2(size, 0f), new Color(color, 0.86f), 1.5f);
                DrawLine(center + new Vector2(0f, -size), center + new Vector2(0f, size), new Color(color, 0.86f), 1.5f);
                break;
            case MarkerKind.Objective:
                DrawCircle(center, size * 0.7f, new Color(color, 0.18f));
                DrawArc(center, size * 0.7f, 0f, Mathf.Tau, 18, new Color(color, 0.88f), 1.4f);
                break;
            default:
                DrawRect(new Rect2(center - new Vector2(size * 0.5f, size * 0.5f), new Vector2(size, size)), new Color(color, 0.72f));
                break;
        }
    }

    private void DrawCornerBrackets(Rect2 rect, Color color, float bracketLength, float width, float alpha)
    {
        var tinted = new Color(color, alpha);
        DrawLine(rect.Position, rect.Position + new Vector2(bracketLength, 0f), tinted, width);
        DrawLine(rect.Position, rect.Position + new Vector2(0f, bracketLength), tinted, width);

        var topRight = new Vector2(rect.End.X, rect.Position.Y);
        DrawLine(topRight, topRight + new Vector2(-bracketLength, 0f), tinted, width);
        DrawLine(topRight, topRight + new Vector2(0f, bracketLength), tinted, width);

        var bottomRight = rect.End;
        DrawLine(bottomRight, bottomRight + new Vector2(-bracketLength, 0f), tinted, width);
        DrawLine(bottomRight, bottomRight + new Vector2(0f, -bracketLength), tinted, width);

        var bottomLeft = new Vector2(rect.Position.X, rect.End.Y);
        DrawLine(bottomLeft, bottomLeft + new Vector2(bracketLength, 0f), tinted, width);
        DrawLine(bottomLeft, bottomLeft + new Vector2(0f, -bracketLength), tinted, width);
    }

    private static float GetModeStrength(HostileMode mode) => mode switch
    {
        HostileMode.Advance => 0f,
        HostileMode.Aim => 0.48f,
        HostileMode.Windup => 0.74f,
        HostileMode.Charge => 1f,
        HostileMode.Recover => 0.24f,
        _ => 0f,
    };
}
