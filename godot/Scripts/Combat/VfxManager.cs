using System;
using System.Collections.Generic;
using Godot;
using ShotV.Core;
using ShotV.Data;

namespace ShotV.Combat;

public partial class VfxManager : Node2D
{
    private readonly List<NeedleProjectile> _needles = new();
    private readonly List<GrenadeProjectile> _grenades = new();
    private readonly List<GrenadeExplosion> _explosions = new();
    private readonly List<BurstRing> _rings = new();
    private readonly List<ImpactParticle> _particles = new();
    private readonly List<DashAfterimage> _afterimages = new();

    private static readonly Random _rng = new();

    public void Reset()
    {
        _needles.Clear();
        _grenades.Clear();
        _explosions.Clear();
        _rings.Clear();
        _particles.Clear();
        _afterimages.Clear();
    }

    public void SpawnNeedle(Vector2 start, Vector2 end, float width, float duration, Color color, Color coreColor)
    {
        var dir = (end - start).Normalized();
        _needles.Add(new NeedleProjectile
        {
            Start = start, End = end, Direction = dir,
            Age = 0f, Duration = duration,
            Length = start.DistanceTo(end),
            Width = width,
            DrawColor = color,
            CoreColor = coreColor,
        });
    }

    public void SpawnGrenade(Vector2 start, Vector2 end, float duration = 0.34f)
    {
        _grenades.Add(new GrenadeProjectile
        {
            Start = start, End = end, Age = 0f, Duration = duration,
        });
    }

    public void SpawnExplosion(Vector2 position, float radius, float duration = 0.32f)
    {
        _explosions.Add(new GrenadeExplosion
        {
            Position = position, Age = 0f, Duration = duration, Radius = radius,
        });
    }

    public void SpawnRing(float x, float y, float startRadius, float endRadius, float duration, Color color, float width)
    {
        _rings.Add(new BurstRing
        {
            Position = new Vector2(x, y),
            Age = 0f, Duration = duration,
            StartRadius = startRadius, EndRadius = endRadius,
            DrawColor = color, Width = width,
        });
    }

    public void SpawnParticles(float x, float y, int count, Color color)
    {
        for (int i = 0; i < count; i++)
        {
            float angle = (float)(_rng.NextDouble() * Mathf.Tau);
            float speed = 60f + (float)_rng.NextDouble() * 180f;
            float life = 0.3f + (float)_rng.NextDouble() * 0.35f;
            float length = 4f + (float)_rng.NextDouble() * 8f;
            float width = 1.5f + (float)_rng.NextDouble() * 2f;
            _particles.Add(new ImpactParticle
            {
                Position = new Vector2(x + (float)(_rng.NextDouble() * 8 - 4), y + (float)(_rng.NextDouble() * 8 - 4)),
                Velocity = new Vector2(Mathf.Cos(angle) * speed, Mathf.Sin(angle) * speed),
                Rotation = angle,
                Spin = (float)(_rng.NextDouble() * 6 - 3),
                Age = 0f, Duration = life,
                Length = length, Width = width,
                DrawColor = color,
                Alpha = 0.85f + (float)_rng.NextDouble() * 0.15f,
                Drag = 0.88f,
            });
        }
    }

    public void SpawnAfterimage(Vector2 position, float aimAngle, WeaponType weaponType)
    {
        _afterimages.Add(new DashAfterimage
        {
            Position = position, AimAngle = aimAngle,
            WeaponType = weaponType,
            Age = 0f, Duration = 0.28f, Scale = 1f,
        });
    }

    public void Tick(float delta)
    {
        TickNeedles(delta);
        TickGrenades(delta);
        TickExplosions(delta);
        TickRings(delta);
        TickParticles(delta);
        TickAfterimages(delta);
        QueueRedraw();
    }

    // Called by grenade projectile completion
    public event Action<Vector2>? GrenadeDetonated;

    public override void _Draw()
    {
        DrawRings();
        DrawParticles();
        DrawAfterimages();
        DrawNeedles();
        DrawGrenades();
        DrawExplosions();
    }

    private void TickNeedles(float delta)
    {
        for (int i = _needles.Count - 1; i >= 0; i--)
        {
            _needles[i].Age += delta;
            if (_needles[i].Age >= _needles[i].Duration)
                _needles.RemoveAt(i);
        }
    }

    private void TickGrenades(float delta)
    {
        for (int i = _grenades.Count - 1; i >= 0; i--)
        {
            _grenades[i].Age += delta;
            if (_grenades[i].Age >= _grenades[i].Duration)
            {
                var g = _grenades[i];
                _grenades.RemoveAt(i);
                SpawnExplosion(g.End, WeaponData.Grenade.SplashRadius);
                SpawnRing(g.End.X, g.End.Y, 12, WeaponData.Grenade.SplashRadius * 1.2f, 0.28f, Palette.Accent, 3.5f);
                SpawnParticles(g.End.X, g.End.Y, 18, Palette.AccentSoft);
                GrenadeDetonated?.Invoke(g.End);
            }
        }
    }

    private void TickExplosions(float delta)
    {
        for (int i = _explosions.Count - 1; i >= 0; i--)
        {
            _explosions[i].Age += delta;
            if (_explosions[i].Age >= _explosions[i].Duration)
                _explosions.RemoveAt(i);
        }
    }

    private void TickRings(float delta)
    {
        for (int i = _rings.Count - 1; i >= 0; i--)
        {
            _rings[i].Age += delta;
            if (_rings[i].Age >= _rings[i].Duration)
                _rings.RemoveAt(i);
        }
    }

    private void TickParticles(float delta)
    {
        for (int i = _particles.Count - 1; i >= 0; i--)
        {
            var p = _particles[i];
            p.Age += delta;
            if (p.Age >= p.Duration) { _particles.RemoveAt(i); continue; }
            p.Position += p.Velocity * delta;
            p.Velocity *= p.Drag;
            p.Rotation += p.Spin * delta;
        }
    }

    private void TickAfterimages(float delta)
    {
        for (int i = _afterimages.Count - 1; i >= 0; i--)
        {
            _afterimages[i].Age += delta;
            if (_afterimages[i].Age >= _afterimages[i].Duration)
                _afterimages.RemoveAt(i);
        }
    }

    private void DrawNeedles()
    {
        foreach (var n in _needles)
        {
            float t = n.Age / n.Duration;
            float alpha = 1f - MathUtil.EaseOutCubic(t);
            float headOffset = n.Length * Mathf.Min(1f, t * 3.2f);
            float tailOffset = n.Length * Mathf.Max(0f, t * 3.2f - 0.7f);
            var head = n.Start + n.Direction * headOffset;
            var tail = n.Start + n.Direction * tailOffset;

            // Core line
            DrawLine(tail, head, new Color(n.CoreColor, alpha * 0.95f), n.Width * 0.5f);
            // Outer glow
            DrawLine(tail, head, new Color(n.DrawColor, alpha * 0.45f), n.Width * 1.6f);
        }
    }

    private void DrawGrenades()
    {
        foreach (var g in _grenades)
        {
            float t = g.Age / g.Duration;
            float arc = Mathf.Sin(t * Mathf.Pi) * 48f;
            var pos = g.Start.Lerp(g.End, t) + new Vector2(0, -arc);
            float size = 5f + t * 2f;

            DrawCircle(pos, size + 3f, new Color(Palette.AccentSoft, 0.3f * (1f - t)));
            DrawCircle(pos, size, new Color(Palette.Accent, 0.85f * (1f - t * 0.4f)));
        }
    }

    private void DrawExplosions()
    {
        foreach (var e in _explosions)
        {
            float t = e.Age / e.Duration;
            float radius = e.Radius * MathUtil.EaseOutCubic(t);
            float alpha = (1f - t) * 0.55f;

            DrawCircle(e.Position, radius, new Color(Palette.Warning, alpha * 0.4f));
            DrawArc(e.Position, radius, 0f, Mathf.Tau, 32, new Color(Palette.Accent, alpha * 0.8f), 3f);
            DrawCircle(e.Position, radius * 0.35f, new Color(Palette.Shot, alpha));
        }
    }

    private void DrawRings()
    {
        foreach (var r in _rings)
        {
            float t = r.Age / r.Duration;
            float radius = MathUtil.Lerp(r.StartRadius, r.EndRadius, MathUtil.EaseOutCubic(t));
            float alpha = 1f - t;
            DrawArc(r.Position, radius, 0f, Mathf.Tau, 32, new Color(r.DrawColor, alpha * 0.8f), r.Width * (1f - t * 0.5f));
        }
    }

    private void DrawParticles()
    {
        foreach (var p in _particles)
        {
            float t = p.Age / p.Duration;
            float alpha = p.Alpha * (1f - t);
            var transform = Transform2D.Identity.Rotated(p.Rotation).Translated(p.Position);
            DrawSetTransformMatrix(transform);
            DrawRect(new Rect2(-p.Length * 0.5f, -p.Width * 0.5f, p.Length, p.Width), new Color(p.DrawColor, alpha));
        }
        DrawSetTransformMatrix(Transform2D.Identity);
    }

    private void DrawAfterimages()
    {
        foreach (var a in _afterimages)
        {
            float t = a.Age / a.Duration;
            float alpha = (1f - t) * 0.35f;
            float scale = a.Scale * (1f + t * 0.12f);
            DrawCircle(a.Position, 14f * scale, new Color(Palette.Dash, alpha));
        }
    }
}
