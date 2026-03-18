using System.Collections.Generic;
using Godot;
using ShotV.Core;
using ShotV.Data;
using ShotV.World;

namespace ShotV.Combat;

public partial class PlayerAvatar : Node2D
{
    private readonly struct ArrowShape
    {
        public ArrowShape(float distance, Vector2[] points, Vector2[] glowPoints)
        {
            Distance = distance;
            Points = points;
            GlowPoints = glowPoints;
        }

        public float Distance { get; }
        public Vector2[] Points { get; }
        public Vector2[] GlowPoints { get; }
    }

    private static readonly Dictionary<WeaponType, ArrowShape> ArrowShapes = new()
    {
        {
            WeaponType.MachineGun,
            new ArrowShape(
                38f,
                new[] { new Vector2(12f, 0f), new Vector2(-10f, -9f), new Vector2(-2f, 0f), new Vector2(-10f, 9f) },
                new[] { new Vector2(16f, 0f), new Vector2(-7f, -11f), new Vector2(1f, 0f), new Vector2(-7f, 11f) })
        },
        {
            WeaponType.Grenade,
            new ArrowShape(
                42f,
                new[] { new Vector2(13f, 0f), new Vector2(4f, -11f), new Vector2(-10f, -5f), new Vector2(-5f, 0f), new Vector2(-10f, 5f), new Vector2(4f, 11f) },
                new[] { new Vector2(17f, 0f), new Vector2(6f, -13f), new Vector2(-13f, -6f), new Vector2(-7f, 0f), new Vector2(-13f, 6f), new Vector2(6f, 13f) })
        },
        {
            WeaponType.Sniper,
            new ArrowShape(
                48f,
                new[] { new Vector2(19f, 0f), new Vector2(-13f, -6f), new Vector2(-4f, 0f), new Vector2(-13f, 6f) },
                new[] { new Vector2(24f, 0f), new Vector2(-15f, -8f), new Vector2(-2f, 0f), new Vector2(-15f, 8f) })
        },
    };

    private static readonly Vector2[] GlowShape = BuildRoundedRectShape(new Vector2(22f, 22f), 9f, 5);
    private static readonly Vector2[] ShellShape = BuildRoundedRectShape(new Vector2(14f, 14f), 6f, 4);
    private static readonly Vector2[] CoreShape = BuildRoundedRectShape(new Vector2(7f, 7f), 3.5f, 4);

    private Vector2 _position;
    private Vector2 _velocity;
    private Vector2 _moveIntent;
    private Vector2 _dashDirection = new(0f, -1f);
    private Vector2 _visualOffset;
    private Vector2 _aimTarget;

    private float _aimAngle = -Mathf.Pi / 2f;
    private float _visualHeading = -Mathf.Pi / 2f;
    private float _moveBobPhase;
    private float _moveBobStrength;
    private float _dashTime;
    private float _dashCooldown;
    private float _shotKick;
    private float _shotTwist;
    private float _dashPulse;
    private float _damagePulse;
    private float _reticleBloom;
    private float _reticleBloomTarget;
    private float _hitConfirmPulse;
    private float _lifeRatio = 1f;
    private float _arrowDistanceBase = 38f;
    private WeaponType _weaponType = WeaponData.GetDefaultWeapon().Id;
    private bool _hasAimTarget;

    private Color _glowColor = Palette.PlayerEdge;
    private Color _shellColor = Palette.PlayerBody;
    private Color _edgeColor = Palette.PlayerEdge;
    private Color _coreColor = Palette.PlayerCore;

    public Vector2 PlayerPosition => _position;
    public float CollisionRadius => CombatConstants.PlayerRadius;
    public bool IsDashing => _dashTime > 0f;
    public float AimAngle => _aimAngle;
    public float LifeRatio => _lifeRatio;
    public float DashCooldownRatio => _dashCooldown / CombatConstants.DashCooldown;

    public void SetPlayerPosition(float x, float y)
    {
        _position = new Vector2(x, y);
        Position = _position;
    }

    public void Reset()
    {
        _velocity = Vector2.Zero;
        _moveIntent = Vector2.Zero;
        _dashDirection = new Vector2(0f, -1f);
        _visualOffset = Vector2.Zero;
        _aimAngle = -Mathf.Pi / 2f;
        _visualHeading = _aimAngle;
        _moveBobPhase = 0f;
        _moveBobStrength = 0f;
        _dashTime = 0f;
        _dashCooldown = 0f;
        _shotKick = 0f;
        _shotTwist = 0f;
        _dashPulse = 0f;
        _damagePulse = 0f;
        _reticleBloom = 0f;
        _reticleBloomTarget = 0f;
        _hitConfirmPulse = 0f;
        _lifeRatio = 1f;
        _hasAimTarget = false;
        _aimTarget = Vector2.Zero;
        QueueRedraw();
    }

    public void SetWeaponStyle(WeaponType weaponType)
    {
        _weaponType = weaponType;
        _arrowDistanceBase = ResolveArrowShape(weaponType).Distance;
        QueueRedraw();
    }

    public void SetMoveIntent(float x, float y)
    {
        float magnitude = new Vector2(x, y).Length();
        if (magnitude <= 0.0001f)
        {
            _moveIntent = Vector2.Zero;
            QueueRedraw();
            return;
        }

        _moveIntent = new Vector2(x / magnitude, y / magnitude);
        QueueRedraw();
    }

    public void SetAimAngle(float angle)
    {
        _aimAngle = angle;
        _hasAimTarget = false;
        QueueRedraw();
    }

    public void SetAimTarget(Vector2 target)
    {
        _aimTarget = target;
        _hasAimTarget = true;

        var delta = target - _position;
        if (delta.LengthSquared() > 0.0001f)
            _aimAngle = Mathf.Atan2(delta.Y, delta.X);
        QueueRedraw();
    }

    public void SetLifeRatio(float ratio) => _lifeRatio = Mathf.Clamp(ratio, 0f, 1f);

    public void SetReticleBloom(float ratio)
    {
        _reticleBloomTarget = Mathf.Clamp(ratio, 0f, 1f);
        QueueRedraw();
    }

    public void FlashDamage(float intensity = 1f)
    {
        _damagePulse = Mathf.Max(_damagePulse, intensity);
        QueueRedraw();
    }

    public bool RequestDash()
    {
        if (_dashCooldown > 0f || _dashTime > 0f)
            return false;

        if (_moveIntent.Length() > 0.0001f)
            _dashDirection = _moveIntent;
        else
            _dashDirection = new Vector2(Mathf.Cos(_aimAngle), Mathf.Sin(_aimAngle));

        _dashTime = CombatConstants.DashDuration;
        _dashCooldown = CombatConstants.DashCooldown;
        _dashPulse = 1f;
        return true;
    }

    public void RefreshDashCharge()
    {
        _dashCooldown = 0f;
        _dashPulse = Mathf.Max(_dashPulse, 0.75f);
        QueueRedraw();
    }

    public void TriggerShot(float intensity = 1f, float twistDegrees = 0f)
    {
        _shotKick = Mathf.Max(_shotKick, intensity);
        _shotTwist = Mathf.Clamp(_shotTwist + Mathf.DegToRad(twistDegrees), -0.32f, 0.32f);
        QueueRedraw();
    }

    public void TriggerHitConfirm(float intensity = 1f)
    {
        _hitConfirmPulse = Mathf.Max(_hitConfirmPulse, intensity);
        QueueRedraw();
    }

    public void TriggerWeaponSwap()
    {
        _dashPulse = Mathf.Max(_dashPulse, 0.36f);
        QueueRedraw();
    }

    public Vector2 GetShotOrigin(float fallbackDistance = 38f)
    {
        float reachBase = _arrowDistanceBase > 0f ? _arrowDistanceBase : fallbackDistance;
        float reach = reachBase + _shotKick * 6f + _dashPulse * 5f;
        return _position + new Vector2(Mathf.Cos(_aimAngle), Mathf.Sin(_aimAngle)) * reach;
    }

    public void UpdatePhysics(float delta, Rect2 arena, List<WorldObstacle> obstacles)
    {
        _dashCooldown = Mathf.Max(0f, _dashCooldown - delta);
        _shotKick = Mathf.Max(0f, _shotKick - delta * 9f);
        _shotTwist = Mathf.Lerp(_shotTwist, 0f, Mathf.Min(1f, delta * 13f));
        _dashPulse = Mathf.Max(0f, _dashPulse - delta * 4.5f);
        _damagePulse = Mathf.Max(0f, _damagePulse - delta * 5f);
        _hitConfirmPulse = Mathf.Max(0f, _hitConfirmPulse - delta * 5.8f);
        _reticleBloom = Mathf.Lerp(_reticleBloom, _reticleBloomTarget, Mathf.Min(1f, delta * 10f));

        if (_dashTime > 0f)
        {
            _dashTime = Mathf.Max(0f, _dashTime - delta);
            _velocity = _dashDirection * CombatConstants.DashSpeed;
        }
        else
        {
            bool hasIntent = _moveIntent.X != 0f || _moveIntent.Y != 0f;
            float desiredVx = _moveIntent.X * CombatConstants.MaxSpeed;
            float desiredVy = _moveIntent.Y * CombatConstants.MaxSpeed;
            float response = hasIntent ? CombatConstants.Acceleration : CombatConstants.Brake;
            float blend = Mathf.Min(1f, response * delta);
            _velocity.X += (desiredVx - _velocity.X) * blend;
            _velocity.Y += (desiredVy - _velocity.Y) * blend;
        }

        var nextPos = WorldCollision.ResolveCircleWorldMovement(
            _position,
            _position.X + _velocity.X * delta,
            _position.Y + _velocity.Y * delta,
            CombatConstants.PlayerRadius,
            arena,
            obstacles);

        float previousX = _position.X;
        float previousY = _position.Y;
        _position = nextPos;

        if (Mathf.Abs(_position.X - previousX) < 0.001f && _velocity.X != 0f)
            _velocity.X = 0f;
        if (Mathf.Abs(_position.Y - previousY) < 0.001f && _velocity.Y != 0f)
            _velocity.Y = 0f;

        var aimVector = ResolveAimVector();
        if (aimVector.LengthSquared() > 0.0001f)
            _aimAngle = Mathf.Atan2(aimVector.Y, aimVector.X);

        float speed = _velocity.Length();
        float speedFactor = Mathf.Min(1f, speed / CombatConstants.MaxSpeed);
        float dashVisual = _dashTime > 0f ? 1f : _dashPulse * 0.45f;
        Vector2 driveDirection = speed > 0.01f
            ? _velocity / speed
            : new Vector2(Mathf.Cos(_aimAngle), Mathf.Sin(_aimAngle));
        Vector2 targetOffset = driveDirection * (speedFactor * 7f + dashVisual * 6f);
        _visualOffset = _visualOffset.Lerp(targetOffset, Mathf.Min(1f, delta * (_dashTime > 0f ? 15f : 8.5f)));

        if (speed > 4f)
        {
            _moveBobPhase += delta * (4.8f + speedFactor * 6.8f);
            _moveBobStrength = Mathf.Lerp(_moveBobStrength, 0.24f + speedFactor * 0.38f, Mathf.Min(1f, delta * 8f));
        }
        else
        {
            _moveBobStrength = Mathf.Lerp(_moveBobStrength, 0f, Mathf.Min(1f, delta * 10f));
        }

        float targetHeading = speed > 8f
            ? Mathf.Atan2(_velocity.Y, _velocity.X)
            : _aimAngle;
        _visualHeading = Mathf.LerpAngle(_visualHeading, targetHeading, Mathf.Min(1f, delta * 7.5f));

        Position = _position;
        QueueRedraw();
    }

    public override void _Draw()
    {
        float elapsed = Time.GetTicksMsec() * 0.001f;
        float speed = _velocity.Length();
        float speedFactor = Mathf.Min(1f, speed / CombatConstants.MaxSpeed);
        float dashFactor = _dashTime > 0f ? 1f : _dashPulse;
        float vitality = 0.7f + _lifeRatio * 0.3f;
        var fallbackAimDir = new Vector2(Mathf.Cos(_aimAngle), Mathf.Sin(_aimAngle));
        var aimVector = ResolveAimVector();
        if (aimVector.LengthSquared() <= 0.0001f)
            aimVector = fallbackAimDir * 160f;
        float aimDistance = aimVector.Length();
        var aimDir = aimDistance > 0.001f ? aimVector / aimDistance : fallbackAimDir;
        float aimRotation = Mathf.Atan2(aimDir.Y, aimDir.X) + _shotTwist;
        var recoilOffset = -aimDir * (_shotKick * 5.5f + dashFactor * 1.8f);
        var motionDir = speed > 0.01f ? _velocity / speed : aimDir;
        var sideDir = new Vector2(-motionDir.Y, motionDir.X);
        float stride = Mathf.Sin(_moveBobPhase);
        float strideAlt = Mathf.Sin(_moveBobPhase + Mathf.Pi * 0.5f);
        float bobStrength = _moveBobStrength;
        float idleSway = Mathf.Sin(elapsed * 5.4f) * (0.015f + dashFactor * 0.012f);
        float shellSwing = stride * bobStrength * 0.14f + strideAlt * bobStrength * 0.05f;
        float coreSwing = -stride * bobStrength * 0.07f + strideAlt * bobStrength * 0.02f;
        float shellRotation = Mathf.LerpAngle(aimRotation, _visualHeading, 0.22f + speedFactor * 0.34f) + shellSwing + idleSway;
        float coreRotation = Mathf.LerpAngle(aimRotation, _visualHeading, 0.08f + speedFactor * 0.12f) + coreSwing;

        var glowCenter = _visualOffset * 0.62f + sideDir * stride * bobStrength * 1.1f + recoilOffset * 0.14f;
        var shellCenter = _visualOffset * 0.82f
            + motionDir * strideAlt * bobStrength * 0.95f
            + sideDir * stride * bobStrength * 1.55f
            + recoilOffset * 0.26f;
        var coreCenter = _visualOffset * 0.28f
            - motionDir * strideAlt * bobStrength * 0.45f
            - sideDir * stride * bobStrength * 0.8f
            + recoilOffset * 0.48f;

        var glow = TransformShape(
            GlowShape,
            glowCenter,
            shellRotation * 0.65f,
            Vector2.One * (1.04f + speedFactor * 0.05f + dashFactor * 0.12f + _damagePulse * 0.08f));
        var shell = TransformShape(
            ShellShape,
            shellCenter,
            shellRotation,
            Vector2.One * (1f + dashFactor * 0.03f + _damagePulse * 0.02f));
        var core = TransformShape(
            CoreShape,
            coreCenter,
            coreRotation,
            Vector2.One * (1f + _damagePulse * 0.08f));

        DrawColoredPolygon(glow, new Color(_glowColor, 0.14f + speedFactor * 0.12f + dashFactor * 0.18f + _damagePulse * 0.18f));
        DrawColoredPolygon(shell, new Color(_shellColor, 0.84f + _lifeRatio * 0.16f));
        DrawPolyline(CloseShape(shell), new Color(_edgeColor, 0.7f + dashFactor * 0.24f + _damagePulse * 0.24f), 2f);
        DrawColoredPolygon(core, new Color(_coreColor, 0.72f + vitality * 0.2f + _damagePulse * 0.22f));

        var shape = ResolveArrowShape(_weaponType);
        float distanceFactor = Mathf.Clamp((aimDistance - 56f) / 220f, 0f, 1f);
        float arrowDistance = 24f + distanceFactor * 6f + _shotKick * 2.5f + dashFactor * 3.5f;
        float arrowScale = 1f + _shotKick * 0.16f + dashFactor * 0.16f;
        var arrowCenter = aimDir * arrowDistance + _visualOffset * 0.18f + sideDir * stride * bobStrength * 0.35f + recoilOffset * 0.12f;

        if (_hasAimTarget && aimDistance > 28f)
            DrawAimReticle(aimVector, aimDir, aimDistance, dashFactor);

        DrawLine(
            shellCenter + aimDir * 18f,
            arrowCenter - aimDir * 8f,
            new Color(Palette.Reticle, 0.18f + dashFactor * 0.1f),
            1.4f);

        DrawColoredPolygon(
            TransformShape(shape.GlowPoints, arrowCenter, aimRotation, new Vector2(arrowScale, arrowScale)),
            new Color(Palette.AccentSoft, 0.24f + dashFactor * 0.22f + _damagePulse * 0.1f));
        DrawColoredPolygon(
            TransformShape(shape.Points, arrowCenter, aimRotation, new Vector2(arrowScale, arrowScale)),
            new Color(Palette.Accent, 0.98f));

    }

    private static ArrowShape ResolveArrowShape(WeaponType weaponType)
    {
        if (ArrowShapes.TryGetValue(weaponType, out var explicitShape))
            return explicitShape;

        if (WeaponData.ById.TryGetValue(weaponType, out var weapon))
        {
            return weapon.FireMode switch
            {
                WeaponFireMode.Launcher => ArrowShapes[WeaponType.Grenade],
                WeaponFireMode.Precision => ArrowShapes[WeaponType.Sniper],
                _ => ArrowShapes[WeaponType.MachineGun],
            };
        }

        return ArrowShapes[WeaponType.MachineGun];
    }

    private void DrawAimReticle(Vector2 aimVector, Vector2 aimDir, float aimDistance, float dashFactor)
    {
        float pulse = (Mathf.Sin(Time.GetTicksMsec() * 0.008f) + 1f) * 0.5f;
        float bloomRadius = _reticleBloom * 16f;
        float confirmTighten = _hitConfirmPulse * 1.2f;
        float radius = 10f + bloomRadius + pulse * 1.8f + dashFactor * 1.4f - confirmTighten;
        var reticleCenter = aimVector;
        var tetherStart = aimDir * 22f + _visualOffset * 0.24f;
        var tetherEnd = reticleCenter - aimDir * (radius + 5f);

        if ((tetherEnd - tetherStart).LengthSquared() > 144f)
        {
            float tetherAlpha = Mathf.Clamp(0.12f + aimDistance / 540f * 0.1f, 0.12f, 0.22f);
            DrawLine(tetherStart, tetherEnd, new Color(Palette.Reticle, tetherAlpha), 1.1f);
        }

        DrawCircle(reticleCenter, radius + 2f + _hitConfirmPulse * 2f, new Color(Palette.AccentSoft, 0.08f + pulse * 0.08f + _hitConfirmPulse * 0.12f));
        DrawArc(reticleCenter, radius, 0f, Mathf.Tau, 28, new Color(Palette.Reticle, 0.34f + dashFactor * 0.18f + _reticleBloom * 0.18f), 1.4f + _hitConfirmPulse * 0.5f);

        float outerRadius = radius + 8f + _reticleBloom * 9f;
        DrawArc(reticleCenter, outerRadius, -0.5f, 0.5f, 10, new Color(Palette.AccentSoft, 0.18f + _reticleBloom * 0.18f), 1.2f);
        DrawArc(reticleCenter, outerRadius, Mathf.Pi - 0.5f, Mathf.Pi + 0.5f, 10, new Color(Palette.AccentSoft, 0.18f + _reticleBloom * 0.18f), 1.2f);

        float inner = radius * 0.32f;
        Color reticleColor = _hitConfirmPulse > 0.01f ? Palette.Accent : Palette.Reticle;
        DrawLine(reticleCenter + new Vector2(-radius, 0f), reticleCenter + new Vector2(-inner, 0f), new Color(reticleColor, 0.52f + _hitConfirmPulse * 0.24f), 1.2f);
        DrawLine(reticleCenter + new Vector2(radius, 0f), reticleCenter + new Vector2(inner, 0f), new Color(reticleColor, 0.52f + _hitConfirmPulse * 0.24f), 1.2f);
        DrawLine(reticleCenter + new Vector2(0f, -radius), reticleCenter + new Vector2(0f, -inner), new Color(reticleColor, 0.52f + _hitConfirmPulse * 0.24f), 1.2f);
        DrawLine(reticleCenter + new Vector2(0f, radius), reticleCenter + new Vector2(0f, inner), new Color(reticleColor, 0.52f + _hitConfirmPulse * 0.24f), 1.2f);
        DrawCircle(reticleCenter, 2.4f + _hitConfirmPulse * 1.5f, new Color(Palette.Accent, 0.9f));
    }

    private Vector2 ResolveAimVector()
    {
        if (IsInsideTree())
        {
            var localMouse = GetLocalMousePosition();
            if (localMouse.LengthSquared() > 0.0001f)
                return localMouse;
        }

        if (_hasAimTarget)
            return _aimTarget - _position;

        return new Vector2(Mathf.Cos(_aimAngle), Mathf.Sin(_aimAngle)) * 160f;
    }

    private static Vector2[] BuildRoundedRectShape(Vector2 halfSize, float radius, int cornerSegments)
    {
        radius = Mathf.Min(radius, Mathf.Min(halfSize.X, halfSize.Y));
        var centers = new[]
        {
            new Vector2(halfSize.X - radius, -halfSize.Y + radius),
            new Vector2(halfSize.X - radius, halfSize.Y - radius),
            new Vector2(-halfSize.X + radius, halfSize.Y - radius),
            new Vector2(-halfSize.X + radius, -halfSize.Y + radius),
        };
        var startAngles = new[] { -Mathf.Pi / 2f, 0f, Mathf.Pi / 2f, Mathf.Pi };
        var points = new List<Vector2>(cornerSegments * 4 + 4);

        for (int corner = 0; corner < centers.Length; corner++)
        {
            for (int step = 0; step <= cornerSegments; step++)
            {
                if (corner > 0 && step == 0)
                    continue;

                float angle = startAngles[corner] + (Mathf.Pi / 2f) * (step / (float)cornerSegments);
                points.Add(centers[corner] + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
            }
        }

        return points.ToArray();
    }

    private static Vector2[] TransformShape(Vector2[] source, Vector2 center, float rotation, Vector2 scale)
    {
        float cos = Mathf.Cos(rotation);
        float sin = Mathf.Sin(rotation);
        var result = new Vector2[source.Length];

        for (int index = 0; index < source.Length; index++)
        {
            float px = source[index].X * scale.X;
            float py = source[index].Y * scale.Y;
            result[index] = new Vector2(
                center.X + px * cos - py * sin,
                center.Y + px * sin + py * cos);
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
}
