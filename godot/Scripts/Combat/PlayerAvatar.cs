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

    private Vector2 _position;
    private Vector2 _velocity;
    private Vector2 _moveIntent;
    private Vector2 _dashDirection = new(0f, -1f);

    private float _aimAngle = -Mathf.Pi / 2f;
    private float _dashTime;
    private float _dashCooldown;
    private float _shotKick;
    private float _dashPulse;
    private float _damagePulse;
    private float _lifeRatio = 1f;
    private float _arrowDistanceBase = 38f;
    private WeaponType _weaponType = WeaponType.MachineGun;

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
        _aimAngle = -Mathf.Pi / 2f;
        _dashTime = 0f;
        _dashCooldown = 0f;
        _shotKick = 0f;
        _dashPulse = 0f;
        _damagePulse = 0f;
        _lifeRatio = 1f;
    }

    public void SetWeaponStyle(WeaponType weaponType)
    {
        _weaponType = weaponType;
        if (ArrowShapes.TryGetValue(weaponType, out var shape))
            _arrowDistanceBase = shape.Distance;
    }

    public void SetMoveIntent(float x, float y)
    {
        float magnitude = new Vector2(x, y).Length();
        if (magnitude <= 0.0001f)
        {
            _moveIntent = Vector2.Zero;
            return;
        }

        _moveIntent = new Vector2(x / magnitude, y / magnitude);
    }

    public void SetAimAngle(float angle) => _aimAngle = angle;
    public void SetLifeRatio(float ratio) => _lifeRatio = Mathf.Clamp(ratio, 0f, 1f);

    public void FlashDamage(float intensity = 1f)
        => _damagePulse = Mathf.Max(_damagePulse, intensity);

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
    }

    public void TriggerShot(float intensity = 1f)
        => _shotKick = Mathf.Max(_shotKick, intensity);

    public void TriggerWeaponSwap()
        => _dashPulse = Mathf.Max(_dashPulse, 0.36f);

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
        _dashPulse = Mathf.Max(0f, _dashPulse - delta * 4.5f);
        _damagePulse = Mathf.Max(0f, _damagePulse - delta * 5f);

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

        Position = _position;
    }

    public override void _Draw()
    {
        float speed = _velocity.Length();
        float speedFactor = Mathf.Min(1f, speed / CombatConstants.MaxSpeed);
        float dashFactor = _dashTime > 0f ? 1f : _dashPulse;
        float vitality = 0.7f + _lifeRatio * 0.3f;
        float shellRotation = _velocity.X * 0.0009f;

        float glowAlpha = 0.14f + speedFactor * 0.1f + dashFactor * 0.18f + _damagePulse * 0.18f;
        float glowScale = 1f + speedFactor * 0.2f + dashFactor * 0.16f + _damagePulse * 0.12f;
        DrawSetTransform(Vector2.Zero, shellRotation * 0.4f, new Vector2(glowScale, glowScale));
        DrawRect(new Rect2(-22f, -22f, 44f, 44f), new Color(_glowColor, glowAlpha));

        float shellAlpha = 0.84f + _lifeRatio * 0.16f;
        DrawSetTransform(Vector2.Zero, shellRotation, new Vector2(1f + dashFactor * 0.05f - _damagePulse * 0.02f, 1f + dashFactor * 0.05f + _damagePulse * 0.03f));
        DrawRect(new Rect2(-14f, -14f, 28f, 28f), new Color(_shellColor, shellAlpha));

        float edgeAlpha = 0.66f + dashFactor * 0.26f + _damagePulse * 0.26f;
        DrawRect(new Rect2(-14f, -14f, 28f, 28f), new Color(_edgeColor, edgeAlpha), false, 2f);

        float coreAlpha = 0.72f + vitality * 0.2f + _damagePulse * 0.22f;
        float coreScale = 1f + _damagePulse * 0.26f;
        DrawSetTransform(Vector2.Zero, -shellRotation * 1.4f, new Vector2(coreScale, coreScale));
        DrawRect(new Rect2(-5f, -5f, 10f, 10f), new Color(_coreColor, coreAlpha));

        var arrowShape = ArrowShapes.TryGetValue(_weaponType, out var shape)
            ? shape
            : ArrowShapes[WeaponType.MachineGun];
        float arrowDistance = _arrowDistanceBase + _shotKick * 8f + dashFactor * 10f;
        float arrowScale = 1f + _shotKick * 0.16f + dashFactor * 0.16f;
        var arrowPos = new Vector2(Mathf.Cos(_aimAngle), Mathf.Sin(_aimAngle)) * arrowDistance;

        DrawSetTransform(arrowPos, _aimAngle, new Vector2(arrowScale, arrowScale));
        DrawColoredPolygon(shape.GlowPoints, new Color(Palette.AccentSoft, 0.22f + dashFactor * 0.24f + _damagePulse * 0.1f));
        DrawColoredPolygon(shape.Points, new Color(Palette.Accent, 0.98f));

        DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
        QueueRedraw();
    }
}
