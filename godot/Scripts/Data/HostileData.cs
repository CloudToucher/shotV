using System.Collections.Generic;
using Godot;
using ShotV.Core;

namespace ShotV.Data;

public class HostileColors
{
    public Color Body { get; init; }
    public Color Edge { get; init; }
    public Color Glow { get; init; }
}

public class HostileDefinition
{
    public HostileType Type { get; init; }
    public string Label => GameText.Text($"hostile.{Type}.label");
    public float Radius { get; init; }
    public float MaxHealth { get; init; }
    public float MoveSpeed { get; init; }
    public float ContactDamage { get; init; }
    public float ContactInterval { get; init; }
    public float AttackCooldown { get; init; }
    public float AttackWindup { get; init; }
    public float AttackRange { get; init; }
    public float PreferredDistance { get; init; }
    public float ProjectileSpeed { get; init; }
    public float ProjectileRadius { get; init; }
    public float ProjectileDamage { get; init; }
    public float ChargeTriggerDistance { get; init; }
    public float ChargeSpeed { get; init; }
    public float ChargeDuration { get; init; }
    public float RecoverDuration { get; init; }
    public int ProjectileCount { get; init; } = 1;
    public float ProjectileSpreadDegrees { get; init; }
    public float AlertRadius { get; init; }
    public float LeashRadius { get; init; }
    public int ArmorLevel { get; init; }
    public HostileColors Colors { get; init; } = new();
}

public static class HostileData
{
    public static readonly Dictionary<HostileType, HostileDefinition> ByType = new()
    {
        {
            HostileType.Melee, new HostileDefinition
            {
                Type = HostileType.Melee,
                Radius = 18f,
                MaxHealth = 34f,
                MoveSpeed = 142f,
                ContactDamage = 12f,
                ContactInterval = 0.58f,
                AttackCooldown = 0.24f,
                AlertRadius = 240f,
                LeashRadius = 720f,
                ArmorLevel = 0,
                Colors = new HostileColors
                {
                    Body = Palette.EnemyMelee,
                    Edge = Palette.EnemyEdge,
                    Glow = Palette.EnemyMeleeGlow,
                },
            }
        },
        {
            HostileType.Ranged, new HostileDefinition
            {
                Type = HostileType.Ranged,
                Radius = 17f,
                MaxHealth = 30f,
                MoveSpeed = 96f,
                ContactDamage = 10f,
                ContactInterval = 0.8f,
                AttackCooldown = 1.45f,
                AttackWindup = 0.42f,
                AttackRange = 420f,
                PreferredDistance = 250f,
                ProjectileSpeed = 320f,
                ProjectileRadius = 7f,
                ProjectileDamage = 12f,
                AlertRadius = 320f,
                LeashRadius = 760f,
                ArmorLevel = 1,
                Colors = new HostileColors
                {
                    Body = Palette.EnemyRanged,
                    Edge = Palette.EnemyEdge,
                    Glow = Palette.EnemyRangedGlow,
                },
            }
        },
        {
            HostileType.Charger, new HostileDefinition
            {
                Type = HostileType.Charger,
                Radius = 20f,
                MaxHealth = 48f,
                MoveSpeed = 94f,
                ContactDamage = 20f,
                ContactInterval = 0.72f,
                AttackCooldown = 2.4f,
                AttackWindup = 0.56f,
                ChargeTriggerDistance = 240f,
                ChargeSpeed = 560f,
                ChargeDuration = 0.28f,
                RecoverDuration = 0.5f,
                AlertRadius = 280f,
                LeashRadius = 820f,
                ArmorLevel = 2,
                Colors = new HostileColors
                {
                    Body = Palette.EnemyCharger,
                    Edge = Palette.EnemyEdge,
                    Glow = Palette.EnemyChargerGlow,
                },
            }
        },
        {
            HostileType.Stalker, new HostileDefinition
            {
                Type = HostileType.Stalker,
                Radius = 16f,
                MaxHealth = 28f,
                MoveSpeed = 168f,
                ContactDamage = 10f,
                ContactInterval = 0.54f,
                AttackCooldown = 1.7f,
                AttackWindup = 0.28f,
                ChargeTriggerDistance = 190f,
                ChargeSpeed = 480f,
                ChargeDuration = 0.18f,
                RecoverDuration = 0.22f,
                AlertRadius = 280f,
                LeashRadius = 760f,
                ArmorLevel = 0,
                Colors = new HostileColors
                {
                    Body = Palette.EnemyStalker,
                    Edge = Palette.EnemyEdge,
                    Glow = Palette.EnemyStalkerGlow,
                },
            }
        },
        {
            HostileType.Suppressor, new HostileDefinition
            {
                Type = HostileType.Suppressor,
                Radius = 19f,
                MaxHealth = 54f,
                MoveSpeed = 88f,
                ContactDamage = 12f,
                ContactInterval = 0.8f,
                AttackCooldown = 1.92f,
                AttackWindup = 0.64f,
                AttackRange = 520f,
                PreferredDistance = 320f,
                ProjectileSpeed = 360f,
                ProjectileRadius = 7.5f,
                ProjectileDamage = 13f,
                ProjectileCount = 3,
                ProjectileSpreadDegrees = 16f,
                AlertRadius = 360f,
                LeashRadius = 860f,
                ArmorLevel = 2,
                Colors = new HostileColors
                {
                    Body = Palette.EnemySuppressor,
                    Edge = Palette.EnemyEdge,
                    Glow = Palette.EnemySuppressorGlow,
                },
            }
        },
        {
            HostileType.Boss, new HostileDefinition
            {
                Type = HostileType.Boss,
                Radius = 34f,
                MaxHealth = 620f,
                MoveSpeed = 86f,
                ContactDamage = 26f,
                ContactInterval = 0.8f,
                AttackCooldown = 1.42f,
                AttackWindup = 0.72f,
                AttackRange = 500f,
                PreferredDistance = 250f,
                ProjectileSpeed = 290f,
                ProjectileRadius = 8f,
                ProjectileDamage = 17f,
                ProjectileCount = 5,
                ProjectileSpreadDegrees = 18f,
                AlertRadius = 420f,
                LeashRadius = 980f,
                ArmorLevel = 5,
                Colors = new HostileColors
                {
                    Body = Palette.EnemyBoss,
                    Edge = Palette.EnemyEdge,
                    Glow = Palette.EnemyBossGlow,
                },
            }
        },
    };
}
