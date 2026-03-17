using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using ShotV.Core;
using ShotV.Data;
using ShotV.World;

namespace ShotV.Combat;

public interface IEncounterCallbacks
{
    void OnWaveStarted(int waveIndex, string hint);
    void OnEnemySpawned(HostileType type);
    void OnBossSpawned(EnemyActor enemy);
    void OnBossPhaseShift(EnemyActor enemy);
    void OnBossAttack(BossPattern pattern, EnemyActor enemy, float? targetAngle);
    void OnEnemyHit(EnemyActor enemy, float amount, float impactX, float impactY);
    void OnEnemyKilled(EnemyActor enemy);
    void OnPlayerDamaged(float amount, float sourceX, float sourceY);
    void OnBossDefeated(EnemyActor enemy);
}

public class EncounterManager
{
    private readonly List<EnemyActor> _enemies = new();
    private readonly List<EnemyProjectile> _projectiles = new();
    private readonly Random _rng = new();

    private Rect2 _arenaBounds;
    private List<WorldObstacle> _obstacles = new();
    private List<WorldRegion> _regions = new();
    private List<WorldSpawnAnchor> _spawnAnchors = new();
    private EncounterState _state = EncounterState.Active;
    private int _killCount;
    private int _enemyId;
    private float _spawnCooldown;
    private int _pendingSpawnCount;

    public IReadOnlyList<EnemyActor> Enemies => _enemies;
    public IReadOnlyList<EnemyProjectile> Projectiles => _projectiles;
    public EncounterState State => _state;
    public int WaveIndex => 0;
    public int KillCount => _killCount;
    public int PendingSpawnCount => _pendingSpawnCount;

    public void Resize(Rect2 arenaBounds, List<WorldObstacle> obstacles, List<WorldRegion> regions, List<WorldSpawnAnchor> spawnAnchors)
    {
        _arenaBounds = arenaBounds;
        _obstacles = obstacles;
        _regions = regions;
        _spawnAnchors = spawnAnchors;
    }

    public void Reset()
    {
        _state = EncounterState.Active;
        _killCount = 0;
        _enemyId = 0;
        _spawnCooldown = 0f;
        _pendingSpawnCount = 0;
        _projectiles.Clear();
        _enemies.Clear();
    }

    public void MarkPlayerDown()
    {
        _state = EncounterState.Down;
        _pendingSpawnCount = 0;
    }

    public void MarkEncounterClear()
    {
        _state = EncounterState.Clear;
        _pendingSpawnCount = 0;
        _projectiles.Clear();
        _enemies.Clear();
    }

    public EnemyActor? GetBoss() => null;

    public void Update(float delta, float elapsed, Vector2 playerPos, float playerRadius, bool playerDashing, IEncounterCallbacks callbacks)
    {
        if (_state != EncounterState.Active)
            return;

        AdvanceNaturalSpawning(delta, playerPos, callbacks);
        UpdateEnemies(delta, elapsed, playerPos, playerRadius, callbacks);
        UpdateProjectiles(delta, playerPos, playerRadius, playerDashing, callbacks);
    }

    public void NotifyStimulus(Vector2 source, float radius)
    {
        foreach (var enemy in _enemies)
        {
            float distance = new Vector2(enemy.X, enemy.Y).DistanceTo(source);
            float alertRadius = enemy.Definition.AlertRadius * enemy.AlertRadiusScale;
            if (distance <= radius + alertRadius * 0.5f)
                AlertEnemy(enemy, source, Mathf.Max(2.6f, enemy.AlertDuration * 0.82f), propagate: true);
        }
    }

    public List<SegmentHit> ResolveSegmentHits(Vector2 origin, Vector2 target, float extraRadius)
    {
        var hits = new List<SegmentHit>();
        foreach (var enemy in _enemies)
        {
            var t = MathUtil.SegmentCircleIntersection(origin, target, new Vector2(enemy.X, enemy.Y), enemy.Definition.Radius + extraRadius);
            if (t == null)
                continue;

            hits.Add(new SegmentHit
            {
                Enemy = enemy,
                T = t.Value,
                PointX = MathUtil.Lerp(origin.X, target.X, t.Value),
                PointY = MathUtil.Lerp(origin.Y, target.Y, t.Value),
            });
        }

        hits.Sort((left, right) => left.T.CompareTo(right.T));
        return hits;
    }

    public void DamageEnemy(EnemyActor enemy, float amount, float impactX, float impactY, IEncounterCallbacks callbacks)
    {
        int index = _enemies.IndexOf(enemy);
        if (index == -1)
            return;

        AlertEnemy(enemy, new Vector2(impactX, impactY), Mathf.Max(4.2f, enemy.AlertDuration), propagate: true);
        enemy.Health = Mathf.Max(0f, enemy.Health - amount);
        enemy.DamageFlash = Mathf.Max(enemy.DamageFlash, amount >= CombatConstants.SniperDamage ? 1f : 0.74f);
        callbacks.OnEnemyHit(enemy, amount, impactX, impactY);

        if (enemy.Health > 0f)
            return;

        _killCount++;
        callbacks.OnEnemyKilled(enemy);
        _enemies.RemoveAt(index);
    }

    public void ApplyExplosionDamage(float x, float y, float radius, float damageAmount, IEncounterCallbacks callbacks)
    {
        ApplyExplosionDamage(x, y, radius, damageAmount, 1, int.MaxValue, callbacks);
    }

    public void ApplyExplosionDamage(float x, float y, float radius, float damageAmount, int armorPenetration, int pierceCount, IEncounterCallbacks callbacks)
    {
        NotifyStimulus(new Vector2(x, y), radius * 1.4f);

        int remainingTargets = pierceCount <= 0 ? 1 : pierceCount + 1;
        var snapshot = new List<EnemyActor>(_enemies)
            .OrderBy(enemy => new Vector2(enemy.X - x, enemy.Y - y).LengthSquared())
            .ToList();
        foreach (var enemy in snapshot)
        {
            if (remainingTargets <= 0)
                break;

            float distance = new Vector2(enemy.X - x, enemy.Y - y).Length();
            float reach = radius + enemy.Definition.Radius;
            if (distance > reach)
                continue;

            float falloff = 1f - distance / reach;
            float armorScale = ResolveArmorScale(armorPenetration, enemy.Definition.ArmorLevel);
            float damage = damageAmount * (0.55f + falloff * 0.45f) * armorScale;
            DamageEnemy(enemy, damage, enemy.X, enemy.Y, callbacks);
            remainingTargets--;
        }
    }

    private void AdvanceNaturalSpawning(float delta, Vector2 playerPos, IEncounterCallbacks callbacks)
    {
        if (_regions.Count == 0 || _spawnAnchors.Count == 0)
        {
            _pendingSpawnCount = 0;
            return;
        }

        var currentRegion = ResolveRegion(playerPos);
        if (currentRegion == null)
        {
            _pendingSpawnCount = 0;
            return;
        }

        var profile = GetEncounterProfile(currentRegion);
        int currentCount = _enemies.Count(enemy => enemy.SpawnRegionId == currentRegion.Id);
        _pendingSpawnCount = Mathf.Max(0, profile.DesiredPopulation - currentCount);

        _spawnCooldown = Mathf.Max(0f, _spawnCooldown - delta);
        if (_spawnCooldown > 0f || currentCount >= profile.DesiredPopulation)
            return;

        var anchor = PickSpawnAnchor(currentRegion, playerPos, profile);
        if (anchor == null)
            return;

        SpawnEnemy(anchor, currentRegion, PickHostileType(profile), callbacks);
        _spawnCooldown = profile.SpawnCooldownMin + (float)_rng.NextDouble() * (profile.SpawnCooldownMax - profile.SpawnCooldownMin);
    }

    private void SpawnEnemy(WorldSpawnAnchor anchor, WorldRegion region, HostileType type, IEncounterCallbacks callbacks)
    {
        var definition = HostileData.ByType[type];
        var profile = GetEncounterProfile(region);
        var enemy = new EnemyActor
        {
            Id = _enemyId,
            Type = type,
            Definition = definition,
            X = anchor.Position.X,
            Y = anchor.Position.Y,
            HomeX = anchor.Position.X,
            HomeY = anchor.Position.Y,
            SpawnRegionId = anchor.RegionId,
            SpawnRegionKind = region.Kind,
            Health = definition.MaxHealth,
            ContactCooldown = 0.18f + (float)_rng.NextDouble() * 0.16f,
            AttackCooldown = 0.4f + (float)_rng.NextDouble() * Mathf.Max(0.35f, definition.AttackCooldown * GetAttackCooldownScale(region)),
            Mode = HostileMode.Advance,
            FacingAngle = -Mathf.Pi / 2f,
            Phase = 1,
            Pattern = BossPattern.Nova,
            PatrolAngle = (float)(_rng.NextDouble() * Mathf.Tau),
            PatrolTimer = profile.PatrolCadence * (0.82f + (float)_rng.NextDouble() * 0.48f),
            PatrolRadius = profile.PatrolRadius * (0.88f + (float)_rng.NextDouble() * 0.24f),
            PatrolCadence = profile.PatrolCadence,
            AlertRadiusScale = profile.AlertRadiusScale,
            LeashRadiusScale = profile.LeashRadiusScale,
            AlertDuration = profile.AlertDuration,
            SupportAlertRadius = profile.SupportAlertRadius,
        };

        _enemyId++;
        _enemies.Add(enemy);
        callbacks.OnEnemySpawned(type);
    }

    private void UpdateEnemies(float delta, float elapsed, Vector2 playerPos, float playerRadius, IEncounterCallbacks callbacks)
    {
        foreach (var enemy in _enemies)
        {
            enemy.ContactCooldown = Mathf.Max(0f, enemy.ContactCooldown - delta);
            enemy.AttackCooldown = Mathf.Max(0f, enemy.AttackCooldown - delta);
            enemy.AlertTimer = Mathf.Max(0f, enemy.AlertTimer - delta);
            enemy.DamageFlash = Mathf.Max(0f, enemy.DamageFlash - delta * 5.5f);
            enemy.AttackPulse = Mathf.Max(0f, enemy.AttackPulse - delta * 4.4f);
            enemy.PatrolTimer = Mathf.Max(0f, enemy.PatrolTimer - delta);

            var enemyPos = new Vector2(enemy.X, enemy.Y);
            float directDistance = enemyPos.DistanceTo(playerPos);
            float alertRadius = enemy.Definition.AlertRadius * enemy.AlertRadiusScale;
            if (!enemy.Alerted && directDistance <= alertRadius)
                AlertEnemy(enemy, playerPos, enemy.AlertDuration, propagate: true);
            else if (enemy.Alerted && directDistance <= alertRadius * 1.15f)
                AlertEnemy(enemy, playerPos, enemy.AlertDuration * 0.72f);

            Vector2 targetPos = ResolveEnemyTarget(enemy, playerPos);
            float dx = targetPos.X - enemy.X;
            float dy = targetPos.Y - enemy.Y;
            float distance = new Vector2(dx, dy).Length();
            float dirX = distance > 0.0001f ? dx / distance : 0f;
            float dirY = distance > 0.0001f ? dy / distance : -1f;
            enemy.FacingAngle = Mathf.Atan2(dy, dx);

            float velocityX = 0f;
            float velocityY = 0f;

            if (!enemy.Alerted && enemy.AlertTimer <= 0f)
            {
                UpdateIdleEnemy(enemy, ref velocityX, ref velocityY);
            }
            else
            {
                switch (enemy.Type)
                {
                    case HostileType.Melee:
                        velocityX = dirX * enemy.Definition.MoveSpeed;
                        velocityY = dirY * enemy.Definition.MoveSpeed;
                        break;
                    case HostileType.Ranged:
                        UpdateRangedEnemy(enemy, delta, distance, dirX, dirY, ref velocityX, ref velocityY);
                        break;
                    case HostileType.Charger:
                        UpdateChargerEnemy(enemy, delta, distance, dirX, dirY, ref velocityX, ref velocityY);
                        break;
                    case HostileType.Boss:
                        velocityX = dirX * enemy.Definition.MoveSpeed;
                        velocityY = dirY * enemy.Definition.MoveSpeed;
                        break;
                }
            }

            var nextPos = WorldCollision.ResolveCircleWorldMovement(
                new Vector2(enemy.X, enemy.Y),
                enemy.X + velocityX * delta,
                enemy.Y + velocityY * delta,
                enemy.Definition.Radius,
                _arenaBounds,
                _obstacles);

            enemy.X = nextPos.X;
            enemy.Y = nextPos.Y;

            float playerDistance = playerPos.DistanceTo(new Vector2(enemy.X, enemy.Y));
            float minimumDistance = enemy.Definition.Radius + playerRadius + 2f;
            if (playerDistance > 0.0001f && playerDistance < minimumDistance)
            {
                float scale = minimumDistance / playerDistance;
                float pushX = playerPos.X - enemy.X;
                float pushY = playerPos.Y - enemy.Y;
                enemy.X = playerPos.X - pushX * scale;
                enemy.Y = playerPos.Y - pushY * scale;
            }

            if (enemy.Alerted && enemy.AlertTimer <= 0f)
            {
                float leashDistance = new Vector2(enemy.X, enemy.Y).DistanceTo(new Vector2(enemy.HomeX, enemy.HomeY));
                float leashRadius = enemy.Definition.LeashRadius * enemy.LeashRadiusScale;
                if (leashDistance > leashRadius || directDistance > alertRadius * 1.45f)
                {
                    enemy.Alerted = false;
                    enemy.Mode = HostileMode.Advance;
                }
            }

            if (enemy.Alerted && enemy.ContactCooldown == 0f && playerDistance <= enemy.Definition.Radius + playerRadius + 6f)
            {
                callbacks.OnPlayerDamaged(enemy.Definition.ContactDamage, enemy.X, enemy.Y);
                enemy.ContactCooldown = enemy.Definition.ContactInterval;
                enemy.AttackPulse = Mathf.Max(enemy.AttackPulse, 0.78f);
            }
        }

        ResolveEnemySeparation();
    }

    private static void UpdateIdleEnemy(EnemyActor enemy, ref float velocityX, ref float velocityY)
    {
        if (enemy.PatrolTimer <= 0f)
        {
            enemy.PatrolTimer = enemy.PatrolCadence * (0.82f + (enemy.Id % 5) * 0.1f);
            enemy.PatrolAngle += 0.72f + (enemy.PatrolRadius / 140f) + (enemy.Id % 3) * 0.16f;
        }

        var home = new Vector2(enemy.HomeX, enemy.HomeY);
        var patrolTarget = home + new Vector2(Mathf.Cos(enemy.PatrolAngle), Mathf.Sin(enemy.PatrolAngle)) * enemy.PatrolRadius;
        var toPatrol = patrolTarget - new Vector2(enemy.X, enemy.Y);
        float patrolDistance = toPatrol.Length();
        if (patrolDistance <= 4f)
            return;

        float speed = enemy.Definition.MoveSpeed * Mathf.Clamp(0.2f + enemy.PatrolRadius / 180f, 0.24f, 0.46f);
        velocityX = toPatrol.X / patrolDistance * speed;
        velocityY = toPatrol.Y / patrolDistance * speed;
    }

    private void UpdateRangedEnemy(EnemyActor enemy, float delta, float distance, float dirX, float dirY, ref float velocityX, ref float velocityY)
    {
        if (enemy.Mode == HostileMode.Aim)
        {
            enemy.ModeTimer = Mathf.Max(0f, enemy.ModeTimer - delta);
            if (enemy.ModeTimer == 0f)
            {
                SpawnEnemyProjectile(enemy, dirX, dirY);
                enemy.Mode = HostileMode.Advance;
                enemy.AttackCooldown = enemy.Definition.AttackCooldown;
                enemy.AttackPulse = 1f;
            }
            return;
        }

        float preferredDistance = enemy.Definition.PreferredDistance;
        if (distance > preferredDistance + 48f)
        {
            velocityX = dirX * enemy.Definition.MoveSpeed;
            velocityY = dirY * enemy.Definition.MoveSpeed;
        }
        else if (distance < preferredDistance - 44f)
        {
            velocityX = -dirX * enemy.Definition.MoveSpeed * 0.85f;
            velocityY = -dirY * enemy.Definition.MoveSpeed * 0.85f;
        }
        else
        {
            int orbitSign = enemy.Id % 2 == 0 ? 1 : -1;
            velocityX = -dirY * orbitSign * enemy.Definition.MoveSpeed * 0.58f;
            velocityY = dirX * orbitSign * enemy.Definition.MoveSpeed * 0.58f;
        }

        if (enemy.AttackCooldown == 0f && distance <= enemy.Definition.AttackRange)
        {
            enemy.Mode = HostileMode.Aim;
            enemy.ModeTimer = enemy.Definition.AttackWindup;
            enemy.AttackPulse = 0.55f;
        }
    }

    private static void UpdateChargerEnemy(EnemyActor enemy, float delta, float distance, float dirX, float dirY, ref float velocityX, ref float velocityY)
    {
        switch (enemy.Mode)
        {
            case HostileMode.Windup:
                enemy.FacingAngle = Mathf.Atan2(enemy.ChargeDirY, enemy.ChargeDirX);
                enemy.ModeTimer = Mathf.Max(0f, enemy.ModeTimer - delta);
                if (enemy.ModeTimer == 0f)
                {
                    enemy.Mode = HostileMode.Charge;
                    enemy.ModeTimer = enemy.Definition.ChargeDuration;
                    enemy.AttackCooldown = enemy.Definition.AttackCooldown;
                    enemy.AttackPulse = 1f;
                }
                break;

            case HostileMode.Charge:
                enemy.FacingAngle = Mathf.Atan2(enemy.ChargeDirY, enemy.ChargeDirX);
                velocityX = enemy.ChargeDirX * enemy.Definition.ChargeSpeed;
                velocityY = enemy.ChargeDirY * enemy.Definition.ChargeSpeed;
                enemy.ModeTimer = Mathf.Max(0f, enemy.ModeTimer - delta);
                if (enemy.ModeTimer == 0f)
                {
                    enemy.Mode = HostileMode.Recover;
                    enemy.ModeTimer = enemy.Definition.RecoverDuration;
                }
                break;

            case HostileMode.Recover:
                enemy.ModeTimer = Mathf.Max(0f, enemy.ModeTimer - delta);
                if (enemy.ModeTimer == 0f)
                    enemy.Mode = HostileMode.Advance;
                break;

            default:
                velocityX = dirX * enemy.Definition.MoveSpeed;
                velocityY = dirY * enemy.Definition.MoveSpeed;
                if (enemy.AttackCooldown == 0f && distance <= enemy.Definition.ChargeTriggerDistance)
                {
                    enemy.Mode = HostileMode.Windup;
                    enemy.ModeTimer = enemy.Definition.AttackWindup;
                    enemy.ChargeDirX = dirX;
                    enemy.ChargeDirY = dirY;
                    enemy.AttackPulse = 0.75f;
                }
                break;
        }
    }

    private static Vector2 ResolveEnemyTarget(EnemyActor enemy, Vector2 playerPos)
    {
        if (enemy.Alerted)
        {
            enemy.LastKnownPlayerX = playerPos.X;
            enemy.LastKnownPlayerY = playerPos.Y;
            return playerPos;
        }

        if (enemy.AlertTimer > 0f)
            return new Vector2(enemy.LastKnownPlayerX, enemy.LastKnownPlayerY);

        return new Vector2(enemy.HomeX, enemy.HomeY);
    }

    private void SpawnEnemyProjectile(EnemyActor enemy, float dirX, float dirY)
    {
        float angle = Mathf.Atan2(dirY, dirX);
        SpawnHostileProjectile(enemy, angle, enemy.Definition.ProjectileSpeed, Palette.EnemyProjectile, Palette.AccentSoft);
    }

    private void SpawnHostileProjectile(EnemyActor enemy, float angle, float speed, Color color, Color glowColor)
    {
        float radius = enemy.Definition.ProjectileRadius > 0 ? enemy.Definition.ProjectileRadius : 7f;
        float dx = Mathf.Cos(angle);
        float dy = Mathf.Sin(angle);
        _projectiles.Add(new EnemyProjectile
        {
            X = enemy.X + dx * (enemy.Definition.Radius + 10f),
            Y = enemy.Y + dy * (enemy.Definition.Radius + 10f),
            Vx = dx * speed,
            Vy = dy * speed,
            Radius = radius,
            Damage = enemy.Definition.ProjectileDamage > 0 ? enemy.Definition.ProjectileDamage : 12f,
            Duration = 2.2f,
            DrawColor = color,
            GlowColor = glowColor,
        });
    }

    private void UpdateProjectiles(float delta, Vector2 playerPos, float playerRadius, bool playerDashing, IEncounterCallbacks callbacks)
    {
        for (int index = _projectiles.Count - 1; index >= 0; index--)
        {
            var projectile = _projectiles[index];
            projectile.Age += delta;
            projectile.X += projectile.Vx * delta;
            projectile.Y += projectile.Vy * delta;

            bool hitPlayer = !playerDashing && new Vector2(projectile.X - playerPos.X, projectile.Y - playerPos.Y).Length() <= projectile.Radius + playerRadius;
            bool outside = projectile.X < _arenaBounds.Position.X - 20f || projectile.X > _arenaBounds.End.X + 20f
                        || projectile.Y < _arenaBounds.Position.Y - 20f || projectile.Y > _arenaBounds.End.Y + 20f;
            bool hitObstacle = false;
            foreach (var obstacle in _obstacles)
            {
                if (projectile.X >= obstacle.X && projectile.X <= obstacle.X + obstacle.Width
                    && projectile.Y >= obstacle.Y && projectile.Y <= obstacle.Y + obstacle.Height)
                {
                    hitObstacle = true;
                    break;
                }
            }

            if (hitPlayer)
            {
                callbacks.OnPlayerDamaged(projectile.Damage, projectile.X, projectile.Y);
                _projectiles.RemoveAt(index);
                continue;
            }

            if (outside || hitObstacle || projectile.Age >= projectile.Duration)
                _projectiles.RemoveAt(index);
        }
    }

    private void ResolveEnemySeparation()
    {
        for (int leftIndex = 0; leftIndex < _enemies.Count; leftIndex++)
        {
            var left = _enemies[leftIndex];
            for (int rightIndex = leftIndex + 1; rightIndex < _enemies.Count; rightIndex++)
            {
                var right = _enemies[rightIndex];
                float dx = right.X - left.X;
                float dy = right.Y - left.Y;
                float distance = new Vector2(dx, dy).Length();
                float minimumDistance = left.Definition.Radius + right.Definition.Radius + 6f;
                if (distance <= 0.0001f || distance >= minimumDistance)
                    continue;

                float overlap = (minimumDistance - distance) * 0.5f;
                float nx = dx / distance;
                float ny = dy / distance;
                left.X = Mathf.Clamp(left.X - nx * overlap, _arenaBounds.Position.X, _arenaBounds.End.X);
                left.Y = Mathf.Clamp(left.Y - ny * overlap, _arenaBounds.Position.Y, _arenaBounds.End.Y);
                right.X = Mathf.Clamp(right.X + nx * overlap, _arenaBounds.Position.X, _arenaBounds.End.X);
                right.Y = Mathf.Clamp(right.Y + ny * overlap, _arenaBounds.Position.Y, _arenaBounds.End.Y);
            }
        }
    }

    private WorldRegion? ResolveRegion(Vector2 position)
    {
        foreach (var region in _regions)
        {
            if (region.Bounds.HasPoint(position))
                return region;
        }

        return _regions.Count > 0
            ? _regions.OrderBy(region => region.Bounds.GetCenter().DistanceSquaredTo(position)).First()
            : null;
    }

    private WorldSpawnAnchor? PickSpawnAnchor(WorldRegion region, Vector2 playerPos, RegionEncounterProfile profile)
    {
        var candidates = _spawnAnchors
            .Where(anchor => anchor.RegionId == region.Id)
            .Where(anchor =>
            {
                float distance = anchor.Position.DistanceTo(playerPos);
                return distance >= profile.SpawnMinDistance && distance <= profile.SpawnMaxDistance;
            })
            .OrderBy(_ => _rng.Next())
            .ToList();

        foreach (var anchor in candidates)
        {
            bool occupied = _enemies.Any(enemy => new Vector2(enemy.X, enemy.Y).DistanceTo(anchor.Position) < 90f);
            if (!occupied)
                return anchor;
        }

        return null;
    }

    private HostileType PickHostileType(RegionEncounterProfile profile)
    {
        float roll = (float)_rng.NextDouble();
        if (roll < profile.MeleeWeight)
            return HostileType.Melee;
        if (roll < profile.MeleeWeight + profile.RangedWeight)
            return HostileType.Ranged;
        return HostileType.Charger;
    }

    private void AlertEnemy(EnemyActor enemy, Vector2 source, float duration, bool propagate = false)
    {
        bool newlyAlerted = !enemy.Alerted;
        enemy.Alerted = true;
        enemy.AlertTimer = Mathf.Max(enemy.AlertTimer, duration);
        enemy.LastKnownPlayerX = source.X;
        enemy.LastKnownPlayerY = source.Y;

        if (propagate && newlyAlerted)
            SpreadRegionalAlert(enemy, source, duration * 0.72f);
    }

    private void SpreadRegionalAlert(EnemyActor sourceEnemy, Vector2 source, float duration)
    {
        foreach (var enemy in _enemies)
        {
            if (enemy == sourceEnemy || enemy.SpawnRegionId != sourceEnemy.SpawnRegionId)
                continue;

            float distance = new Vector2(enemy.X, enemy.Y).DistanceTo(new Vector2(sourceEnemy.X, sourceEnemy.Y));
            if (distance > sourceEnemy.SupportAlertRadius)
                continue;

            enemy.Alerted = true;
            enemy.AlertTimer = Mathf.Max(enemy.AlertTimer, duration);
            enemy.LastKnownPlayerX = source.X;
            enemy.LastKnownPlayerY = source.Y;
        }
    }

    private static float GetAttackCooldownScale(WorldRegion region)
    {
        float scale = region.ThreatLevel switch
        {
            >= 3 => 0.86f,
            2 => 0.94f,
            _ => 1.02f,
        };

        return region.Kind switch
        {
            WorldZoneKind.HighRisk => scale - 0.08f,
            WorldZoneKind.HighValue => scale - 0.05f,
            WorldZoneKind.Extraction => scale - 0.02f,
            _ => scale,
        };
    }

    private static float ResolveArmorScale(int armorPenetration, int armorLevel)
    {
        int gap = Mathf.Max(0, armorLevel - armorPenetration);
        return gap switch
        {
            0 => 1f,
            1 => 0.78f,
            2 => 0.58f,
            3 => 0.4f,
            _ => 0.28f,
        };
    }

    private static RegionEncounterProfile GetEncounterProfile(WorldRegion region)
    {
        int threat = Mathf.Clamp(region.ThreatLevel, 1, 3);
        int desiredPopulation = 2 + threat * 2;
        float spawnCooldownMin = 1.3f - threat * 0.1f;
        float spawnCooldownMax = 1.95f - threat * 0.12f;
        float spawnMinDistance = 240f;
        float spawnMaxDistance = 1040f;
        float patrolRadius = 34f + threat * 11f;
        float patrolCadence = 1.35f - threat * 0.08f;
        float alertRadiusScale = 0.96f + threat * 0.08f;
        float leashRadiusScale = 0.94f + threat * 0.1f;
        float alertDuration = 2.8f + threat * 0.45f;
        float supportAlertRadius = 150f + threat * 34f;
        float meleeWeight = threat >= 3 ? 0.34f : 0.46f;
        float rangedWeight = threat >= 2 ? 0.34f : 0.28f;
        float chargerWeight = 1f - meleeWeight - rangedWeight;

        switch (region.Kind)
        {
            case WorldZoneKind.Perimeter:
                desiredPopulation = Mathf.Max(3, desiredPopulation - 1);
                spawnCooldownMin += 0.24f;
                spawnCooldownMax += 0.34f;
                spawnMinDistance += 36f;
                patrolRadius -= 8f;
                alertRadiusScale -= 0.08f;
                leashRadiusScale -= 0.04f;
                alertDuration -= 0.2f;
                supportAlertRadius -= 42f;
                meleeWeight = 0.68f;
                rangedWeight = 0.27f;
                chargerWeight = 0.05f;
                break;
            case WorldZoneKind.HighRisk:
                desiredPopulation += 2;
                spawnCooldownMin -= 0.18f;
                spawnCooldownMax -= 0.2f;
                spawnMinDistance -= 10f;
                patrolRadius += 18f;
                alertRadiusScale += 0.18f;
                leashRadiusScale += 0.16f;
                alertDuration += 0.6f;
                supportAlertRadius += 72f;
                meleeWeight = threat >= 3 ? 0.28f : 0.36f;
                rangedWeight = threat >= 3 ? 0.28f : 0.32f;
                chargerWeight = 1f - meleeWeight - rangedWeight;
                break;
            case WorldZoneKind.HighValue:
                desiredPopulation += 1;
                spawnCooldownMin -= 0.06f;
                spawnCooldownMax -= 0.08f;
                patrolRadius += 12f;
                alertRadiusScale += 0.12f;
                leashRadiusScale += 0.1f;
                alertDuration += 0.4f;
                supportAlertRadius += 56f;
                meleeWeight = 0.22f;
                rangedWeight = 0.48f;
                chargerWeight = 0.3f;
                break;
            case WorldZoneKind.Extraction:
                desiredPopulation = Mathf.Max(3, desiredPopulation);
                spawnCooldownMin += 0.04f;
                spawnCooldownMax += 0.08f;
                patrolRadius += 4f;
                alertRadiusScale += 0.03f;
                leashRadiusScale += 0.04f;
                supportAlertRadius += 18f;
                meleeWeight = 0.52f;
                rangedWeight = 0.34f;
                chargerWeight = 0.14f;
                break;
        }

        return new RegionEncounterProfile(
            Mathf.Clamp(desiredPopulation, 3, 12),
            Mathf.Clamp(spawnCooldownMin, 0.55f, 2.1f),
            Mathf.Clamp(spawnCooldownMax, 0.85f, 2.8f),
            Mathf.Clamp(spawnMinDistance, 180f, 360f),
            Mathf.Clamp(spawnMaxDistance, 820f, 1180f),
            Mathf.Clamp(patrolRadius, 24f, 96f),
            Mathf.Clamp(patrolCadence, 0.8f, 1.6f),
            Mathf.Clamp(alertRadiusScale, 0.78f, 1.4f),
            Mathf.Clamp(leashRadiusScale, 0.82f, 1.45f),
            Mathf.Clamp(alertDuration, 2.1f, 4.8f),
            Mathf.Clamp(supportAlertRadius, 96f, 320f),
            meleeWeight,
            rangedWeight,
            chargerWeight);
    }

    private readonly struct RegionEncounterProfile
    {
        public RegionEncounterProfile(
            int desiredPopulation,
            float spawnCooldownMin,
            float spawnCooldownMax,
            float spawnMinDistance,
            float spawnMaxDistance,
            float patrolRadius,
            float patrolCadence,
            float alertRadiusScale,
            float leashRadiusScale,
            float alertDuration,
            float supportAlertRadius,
            float meleeWeight,
            float rangedWeight,
            float chargerWeight)
        {
            DesiredPopulation = desiredPopulation;
            SpawnCooldownMin = spawnCooldownMin;
            SpawnCooldownMax = spawnCooldownMax;
            SpawnMinDistance = spawnMinDistance;
            SpawnMaxDistance = spawnMaxDistance;
            PatrolRadius = patrolRadius;
            PatrolCadence = patrolCadence;
            AlertRadiusScale = alertRadiusScale;
            LeashRadiusScale = leashRadiusScale;
            AlertDuration = alertDuration;
            SupportAlertRadius = supportAlertRadius;
            MeleeWeight = meleeWeight;
            RangedWeight = rangedWeight;
            ChargerWeight = chargerWeight;
        }

        public int DesiredPopulation { get; }
        public float SpawnCooldownMin { get; }
        public float SpawnCooldownMax { get; }
        public float SpawnMinDistance { get; }
        public float SpawnMaxDistance { get; }
        public float PatrolRadius { get; }
        public float PatrolCadence { get; }
        public float AlertRadiusScale { get; }
        public float LeashRadiusScale { get; }
        public float AlertDuration { get; }
        public float SupportAlertRadius { get; }
        public float MeleeWeight { get; }
        public float RangedWeight { get; }
        public float ChargerWeight { get; }
    }
}
