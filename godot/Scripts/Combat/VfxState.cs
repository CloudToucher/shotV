using Godot;
using ShotV.Core;

namespace ShotV.Combat;

public class NeedleProjectile
{
    public Vector2 Start { get; set; }
    public Vector2 End { get; set; }
    public Vector2 Direction { get; set; }
    public float Age { get; set; }
    public float Duration { get; set; }
    public float Length { get; set; }
    public float Width { get; set; }
    public Color DrawColor { get; set; }
    public Color CoreColor { get; set; }
}

public class BurstRing
{
    public Vector2 Position { get; set; }
    public float Age { get; set; }
    public float Duration { get; set; }
    public float StartRadius { get; set; }
    public float EndRadius { get; set; }
    public Color DrawColor { get; set; }
    public float Width { get; set; }
}

public class MuzzleFlash
{
    public Vector2 Position { get; set; }
    public float Angle { get; set; }
    public float Age { get; set; }
    public float Duration { get; set; }
    public float Size { get; set; }
    public WeaponType WeaponType { get; set; }
}

public class GrenadeProjectile
{
    public Vector2 Start { get; set; }
    public Vector2 End { get; set; }
    public float Radius { get; set; }
    public float Damage { get; set; }
    public int ArmorPenetration { get; set; }
    public int PierceCount { get; set; }
    public float Age { get; set; }
    public float Duration { get; set; }
}

public class GrenadeDetonationPayload
{
    public Vector2 Position { get; set; }
    public float Radius { get; set; }
    public float Damage { get; set; }
    public int ArmorPenetration { get; set; }
    public int PierceCount { get; set; }
}

public class GrenadeExplosion
{
    public Vector2 Position { get; set; }
    public float Age { get; set; }
    public float Duration { get; set; }
    public float Radius { get; set; }
}

public class ImpactParticle
{
    public Vector2 Position { get; set; }
    public Vector2 Velocity { get; set; }
    public float Rotation { get; set; }
    public float Spin { get; set; }
    public float Age { get; set; }
    public float Duration { get; set; }
    public float Length { get; set; }
    public float Width { get; set; }
    public Color DrawColor { get; set; }
    public float Alpha { get; set; }
    public float Drag { get; set; }
}

public class DashAfterimage
{
    public Vector2 Position { get; set; }
    public float AimAngle { get; set; }
    public WeaponType WeaponType { get; set; }
    public float Age { get; set; }
    public float Duration { get; set; }
    public float Scale { get; set; }
}
