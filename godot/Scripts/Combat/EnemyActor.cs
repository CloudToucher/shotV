using Godot;
using ShotV.Core;
using ShotV.Data;

namespace ShotV.Combat;

public class EnemyActor
{
    public int Id { get; set; }
    public HostileType Type { get; set; }
    public HostileDefinition Definition { get; set; } = null!;
    public float X { get; set; }
    public float Y { get; set; }
    public float Health { get; set; }
    public float ContactCooldown { get; set; }
    public float AttackCooldown { get; set; }
    public HostileMode Mode { get; set; } = HostileMode.Advance;
    public float ModeTimer { get; set; }
    public float ChargeDirX { get; set; }
    public float ChargeDirY { get; set; } = 1f;
    public float FacingAngle { get; set; } = -Mathf.Pi / 2f;
    public int Phase { get; set; } = 1;
    public BossPattern Pattern { get; set; } = BossPattern.Nova;
    public bool PhaseShifted { get; set; }
    public float HomeX { get; set; }
    public float HomeY { get; set; }
    public string SpawnRegionId { get; set; } = "";
    public WorldZoneKind SpawnRegionKind { get; set; } = WorldZoneKind.Perimeter;
    public bool Alerted { get; set; }
    public float AlertTimer { get; set; }
    public float LastKnownPlayerX { get; set; }
    public float LastKnownPlayerY { get; set; }
    public float PatrolAngle { get; set; }
    public float PatrolTimer { get; set; }
    public float PatrolRadius { get; set; } = 46f;
    public float PatrolCadence { get; set; } = 1.2f;
    public float AlertRadiusScale { get; set; } = 1f;
    public float LeashRadiusScale { get; set; } = 1f;
    public float AlertDuration { get; set; } = 3f;
    public float SupportAlertRadius { get; set; } = 180f;

    // Visual state
    public float DamageFlash { get; set; }
    public float AttackPulse { get; set; }
    public float LifeRatio => Definition.MaxHealth > 0 ? Health / Definition.MaxHealth : 0f;
}

public class EnemyProjectile
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Vx { get; set; }
    public float Vy { get; set; }
    public float Radius { get; set; }
    public float Damage { get; set; }
    public float Age { get; set; }
    public float Duration { get; set; } = 2.2f;
    public Color DrawColor { get; set; }
    public Color GlowColor { get; set; }
}

public struct SegmentHit
{
    public EnemyActor Enemy;
    public float T;
    public float PointX;
    public float PointY;
}
