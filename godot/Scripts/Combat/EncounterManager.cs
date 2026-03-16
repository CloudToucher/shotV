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
    private readonly List<SpawnOrder> _pendingSpawns = new();

    private Rect2 _arenaBounds;
    private List<WorldObstacle> _obstacles = new();
    private List<Vector2> _spawnPoints = new();
    private Vector2 _bossSpawnPoint;
    private EncounterState _state = EncounterState.Active;
    private int _waveIndex;
    private int _killCount;
    private float _nextWaveDelay = CombatConstants.WaveStartDelay;
    private float _spawnTimer;
    private int _enemyId;

    public IReadOnlyList<EnemyActor> Enemies => _enemies;
    public IReadOnlyList<EnemyProjectile> Projectiles => _projectiles;
    public EncounterState State => _state;
    public int WaveIndex => _waveIndex;
    public int KillCount => _killCount;
    public int PendingSpawnCount => _pendingSpawns.Count;

    public void Resize(Rect2 arenaBounds, List<WorldObstacle> obstacles, List<Vector2> spawnPoints, Vector2 bossSpawnPoint)
    {
        _arenaBounds = arenaBounds;
        _obstacles = obstacles;
        _spawnPoints = spawnPoints;
        _bossSpawnPoint = bossSpawnPoint;
    }

    public void Reset()
    {
        _state = EncounterState.Active;
        _waveIndex = 0;
        _killCount = 0;
        _nextWaveDelay = CombatConstants.WaveStartDelay;
        _spawnTimer = 0f;
        _pendingSpawns.Clear();
        _projectiles.Clear();
        ClearEnemies();
    }

    public void MarkPlayerDown()
    {
        _state = EncounterState.Down;
        _pendingSpawns.Clear();
    }

    public void MarkEncounterClear()
    {
        _state = EncounterState.Clear;
        _pendingSpawns.Clear();
        _projectiles.Clear();
        ClearEnemies();
    }

    public EnemyActor? GetBoss() => _enemies.FirstOrDefault(e => e.Type == HostileType.Boss);

    public void Update(float delta, float elapsed, Vector2 playerPos, float playerRadius, bool playerDashing, IEncounterCallbacks callbacks)
    {
        if (_state != EncounterState.Active) return;
        AdvanceSpawnQueue(delta, callbacks);
        UpdateEnemies(delta, elapsed, playerPos, playerRadius, callbacks);
        UpdateProjectiles(delta, playerPos, playerRadius, playerDashing, callbacks);
    }

    public List<SegmentHit> ResolveSegmentHits(Vector2 origin, Vector2 target, float extraRadius)
    {
        var hits = new List<SegmentHit>();
        foreach (var enemy in _enemies)
        {
            var t = MathUtil.SegmentCircleIntersection(origin, target, new Vector2(enemy.X, enemy.Y), enemy.Definition.Radius + extraRadius);
            if (t == null) continue;
            hits.Add(new SegmentHit
            {
                Enemy = enemy,
                T = t.Value,
                PointX = MathUtil.Lerp(origin.X, target.X, t.Value),
                PointY = MathUtil.Lerp(origin.Y, target.Y, t.Value),
            });
        }
        hits.Sort((a, b) => a.T.CompareTo(b.T));
        return hits;
    }

    public void DamageEnemy(EnemyActor enemy, float amount, float impactX, float impactY, IEncounterCallbacks callbacks)
    {
        int index = _enemies.IndexOf(enemy);
        if (index == -1) return;

        enemy.Health = Mathf.Max(0f, enemy.Health - amount);
        enemy.DamageFlash = Mathf.Max(enemy.DamageFlash, amount >= CombatConstants.SniperDamage ? 1f : 0.74f);
        callbacks.OnEnemyHit(enemy, amount, impactX, impactY);

        if (enemy.Health > 0f) return;

        _killCount++;
        callbacks.OnEnemyKilled(enemy);
        _enemies.RemoveAt(index);

        if (enemy.Type == HostileType.Boss)
        {
            _state = EncounterState.Clear;
            _pendingSpawns.Clear();
            _projectiles.Clear();
            callbacks.OnBossDefeated(enemy);
        }
    }

    public void ApplyExplosionDamage(float x, float y, float radius, float damageAmount, IEncounterCallbacks callbacks)
    {
        var snapshot = new List<EnemyActor>(_enemies);
        foreach (var enemy in snapshot)
        {
            float dist = new Vector2(enemy.X - x, enemy.Y - y).Length();
            float reach = radius + enemy.Definition.Radius;
            if (dist > reach) continue;
            float falloff = 1f - dist / reach;
            float damage = damageAmount * (0.55f + falloff * 0.45f);
            DamageEnemy(enemy, damage, enemy.X, enemy.Y, callbacks);
        }
    }

    private void AdvanceSpawnQueue(float delta, IEncounterCallbacks callbacks)
    {
        if (_pendingSpawns.Count > 0)
        {
            _spawnTimer -= delta;
            while (_spawnTimer <= 0f && _pendingSpawns.Count > 0)
            {
                var next = _pendingSpawns[0];
                _pendingSpawns.RemoveAt(0);
                SpawnEnemy(next.Type, callbacks);
                _spawnTimer += next.Delay;
            }
            return;
        }

        if (_enemies.Count > 0) return;

        _nextWaveDelay -= delta;
        if (_nextWaveDelay <= 0f)
        {
            _waveIndex++;
            _nextWaveDelay = CombatConstants.NextWaveDelay;
            _spawnTimer = 0.2f;
            _pendingSpawns.AddRange(WaveData.BuildWaveOrders(_waveIndex));
            callbacks.OnWaveStarted(_waveIndex, WaveData.BuildWaveHint(_waveIndex));
        }
    }

    private void SpawnEnemy(HostileType type, IEncounterCallbacks callbacks)
    {
        var def = HostileData.ByType[type];
        var pos = PickSpawnPoint(type);
        var rng = new Random();
        var enemy = new EnemyActor
        {
            Id = _enemyId,
            Type = type,
            Definition = def,
            X = pos.X,
            Y = pos.Y,
            Health = def.MaxHealth,
            ContactCooldown = 0.1f + (float)rng.NextDouble() * 0.2f,
            AttackCooldown = 0.35f + (float)rng.NextDouble() * def.AttackCooldown,
            Mode = HostileMode.Advance,
            FacingAngle = -Mathf.Pi / 2f,
            Phase = 1,
            Pattern = BossPattern.Nova,
        };
        _enemyId++;
        _enemies.Add(enemy);
        callbacks.OnEnemySpawned(type);
        if (type == HostileType.Boss) callbacks.OnBossSpawned(enemy);
    }

    private Vector2 PickSpawnPoint(HostileType type)
    {
        if (type == HostileType.Boss) return _bossSpawnPoint;
        if (_spawnPoints.Count > 0)
        {
            var next = _spawnPoints[_enemyId % _spawnPoints.Count];
            return next;
        }
        var rng = new Random();
        int side = _enemyId % 4;
        if (side == 0) return new Vector2(_arenaBounds.Position.X + 24, MathUtil.Lerp(_arenaBounds.Position.Y, _arenaBounds.End.Y, (float)rng.NextDouble()));
        if (side == 1) return new Vector2(_arenaBounds.End.X - 24, MathUtil.Lerp(_arenaBounds.Position.Y, _arenaBounds.End.Y, (float)rng.NextDouble()));
        if (side == 2) return new Vector2(MathUtil.Lerp(_arenaBounds.Position.X, _arenaBounds.End.X, (float)rng.NextDouble()), _arenaBounds.Position.Y + 24);
        return new Vector2(MathUtil.Lerp(_arenaBounds.Position.X, _arenaBounds.End.X, (float)rng.NextDouble()), _arenaBounds.End.Y - 24);
    }

    private void UpdateEnemies(float delta, float elapsed, Vector2 playerPos, float playerRadius, IEncounterCallbacks callbacks)
    {
        foreach (var enemy in _enemies)
        {
            enemy.ContactCooldown = Mathf.Max(0f, enemy.ContactCooldown - delta);
            enemy.AttackCooldown = Mathf.Max(0f, enemy.AttackCooldown - delta);
            enemy.DamageFlash = Mathf.Max(0f, enemy.DamageFlash - delta * 5.5f);
            enemy.AttackPulse = Mathf.Max(0f, enemy.AttackPulse - delta * 4.4f);

            float dx = playerPos.X - enemy.X;
            float dy = playerPos.Y - enemy.Y;
            float dist = new Vector2(dx, dy).Length();
            float dirX = dist > 0.0001f ? dx / dist : 0f;
            float dirY = dist > 0.0001f ? dy / dist : -1f;
            enemy.FacingAngle = Mathf.Atan2(dy, dx);

            float vx = 0f, vy = 0f;

            switch (enemy.Type)
            {
                case HostileType.Melee:
                    vx = dirX * enemy.Definition.MoveSpeed;
                    vy = dirY * enemy.Definition.MoveSpeed;
                    break;

                case HostileType.Ranged:
                    UpdateRangedEnemy(enemy, delta, dist, dirX, dirY, ref vx, ref vy);
                    break;

                case HostileType.Charger:
                    UpdateChargerEnemy(enemy, delta, dist, dirX, dirY, ref vx, ref vy);
                    break;

                case HostileType.Boss:
                    UpdateBossEnemy(enemy, delta, dist, dirX, dirY, ref vx, ref vy, callbacks);
                    break;
            }

            var nextPos = WorldCollision.ResolveCircleWorldMovement(
                new Vector2(enemy.X, enemy.Y),
                enemy.X + vx * delta,
                enemy.Y + vy * delta,
                enemy.Definition.Radius,
                _arenaBounds,
                _obstacles);

            enemy.X = nextPos.X;
            enemy.Y = nextPos.Y;

            // Push apart from player
            float cdx = playerPos.X - enemy.X;
            float cdy = playerPos.Y - enemy.Y;
            float cdist = new Vector2(cdx, cdy).Length();
            float minDist = enemy.Definition.Radius + playerRadius + 2f;
            if (cdist > 0.0001f && cdist < minDist)
            {
                float scale = minDist / cdist;
                enemy.X = playerPos.X - cdx * scale;
                enemy.Y = playerPos.Y - cdy * scale;
            }

            // Contact damage
            if (enemy.ContactCooldown == 0f && cdist <= enemy.Definition.Radius + playerRadius + 6f)
            {
                callbacks.OnPlayerDamaged(enemy.Definition.ContactDamage, enemy.X, enemy.Y);
                enemy.ContactCooldown = enemy.Definition.ContactInterval;
                enemy.AttackPulse = Mathf.Max(enemy.AttackPulse, 0.78f);
            }
        }

        ResolveEnemySeparation();
    }

    private void UpdateRangedEnemy(EnemyActor enemy, float delta, float dist, float dirX, float dirY, ref float vx, ref float vy)
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
        }
        else
        {
            float pref = enemy.Definition.PreferredDistance;
            if (dist > pref + 48f)
            {
                vx = dirX * enemy.Definition.MoveSpeed;
                vy = dirY * enemy.Definition.MoveSpeed;
            }
            else if (dist < pref - 44f)
            {
                vx = -dirX * enemy.Definition.MoveSpeed * 0.85f;
                vy = -dirY * enemy.Definition.MoveSpeed * 0.85f;
            }
            else
            {
                int orbitSign = enemy.Id % 2 == 0 ? 1 : -1;
                vx = -dirY * orbitSign * enemy.Definition.MoveSpeed * 0.58f;
                vy = dirX * orbitSign * enemy.Definition.MoveSpeed * 0.58f;
            }

            if (enemy.AttackCooldown == 0f && dist <= enemy.Definition.AttackRange)
            {
                enemy.Mode = HostileMode.Aim;
                enemy.ModeTimer = enemy.Definition.AttackWindup;
                enemy.AttackPulse = 0.55f;
            }
        }
    }

    private void UpdateChargerEnemy(EnemyActor enemy, float delta, float dist, float dirX, float dirY, ref float vx, ref float vy)
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
                vx = enemy.ChargeDirX * enemy.Definition.ChargeSpeed;
                vy = enemy.ChargeDirY * enemy.Definition.ChargeSpeed;
                enemy.ModeTimer = Mathf.Max(0f, enemy.ModeTimer - delta);
                if (enemy.ModeTimer == 0f)
                {
                    enemy.Mode = HostileMode.Recover;
                    enemy.ModeTimer = enemy.Definition.RecoverDuration;
                }
                break;

            case HostileMode.Recover:
                enemy.ModeTimer = Mathf.Max(0f, enemy.ModeTimer - delta);
                if (enemy.ModeTimer == 0f) enemy.Mode = HostileMode.Advance;
                break;

            default: // Advance
                vx = dirX * enemy.Definition.MoveSpeed;
                vy = dirY * enemy.Definition.MoveSpeed;
                if (enemy.AttackCooldown == 0f && dist <= enemy.Definition.ChargeTriggerDistance)
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

    private void UpdateBossEnemy(EnemyActor enemy, float delta, float dist, float dirX, float dirY, ref float vx, ref float vy, IEncounterCallbacks callbacks)
    {
        float targetAngle = Mathf.Atan2(dirY, dirX);
        float pref = enemy.Definition.PreferredDistance - (enemy.Phase == 2 ? 22f : 0f);
        int orbitSign = enemy.Id % 2 == 0 ? 1 : -1;
        enemy.FacingAngle = targetAngle;

        if (!enemy.PhaseShifted && enemy.Health <= enemy.Definition.MaxHealth * 0.5f)
        {
            enemy.PhaseShifted = true;
            enemy.Phase = 2;
            enemy.Mode = HostileMode.Recover;
            enemy.ModeTimer = 1.05f;
            enemy.AttackCooldown = 0.45f;
            callbacks.OnBossPhaseShift(enemy);
            return;
        }

        if (enemy.Mode == HostileMode.Aim)
        {
            enemy.ModeTimer = Mathf.Max(0f, enemy.ModeTimer - delta);
            vx = -dirY * orbitSign * enemy.Definition.MoveSpeed * 0.2f;
            vy = dirX * orbitSign * enemy.Definition.MoveSpeed * 0.2f;
            if (enemy.ModeTimer == 0f)
            {
                if (enemy.Pattern == BossPattern.Fan)
                {
                    FireBossFan(enemy, targetAngle);
                    enemy.AttackCooldown = enemy.Phase == 1 ? 1.35f : 0.92f;
                    callbacks.OnBossAttack(BossPattern.Fan, enemy, targetAngle);
                }
                else
                {
                    FireBossNova(enemy);
                    enemy.AttackCooldown = enemy.Phase == 1 ? 1.65f : 1.18f;
                    callbacks.OnBossAttack(BossPattern.Nova, enemy, null);
                }
                enemy.Mode = HostileMode.Recover;
                enemy.ModeTimer = enemy.Phase == 1 ? 0.3f : 0.2f;
            }
            return;
        }

        if (enemy.Mode == HostileMode.Recover)
        {
            enemy.ModeTimer = Mathf.Max(0f, enemy.ModeTimer - delta);
            if (enemy.ModeTimer == 0f) enemy.Mode = HostileMode.Advance;
            return;
        }

        // Advance
        if (dist > pref + 42f)
        {
            vx = dirX * enemy.Definition.MoveSpeed;
            vy = dirY * enemy.Definition.MoveSpeed;
        }
        else if (dist < pref - 32f)
        {
            vx = -dirX * enemy.Definition.MoveSpeed * 0.85f;
            vy = -dirY * enemy.Definition.MoveSpeed * 0.85f;
        }
        else
        {
            vx = -dirY * orbitSign * enemy.Definition.MoveSpeed * 0.62f;
            vy = dirX * orbitSign * enemy.Definition.MoveSpeed * 0.62f;
        }

        if (enemy.AttackCooldown == 0f)
        {
            enemy.Pattern = enemy.Pattern == BossPattern.Fan ? BossPattern.Nova : BossPattern.Fan;
            enemy.Mode = HostileMode.Aim;
            enemy.ModeTimer = enemy.Pattern == BossPattern.Fan
                ? (enemy.Phase == 1 ? 0.66f : 0.48f)
                : (enemy.Phase == 1 ? 0.92f : 0.68f);
            enemy.AttackPulse = 1f;
        }
    }

    private void FireBossFan(EnemyActor enemy, float targetAngle)
    {
        int bulletCount = enemy.Phase == 1 ? 7 : 11;
        float spread = enemy.Phase == 1 ? 0.95f : 1.28f;
        int fanCount = enemy.Phase == 1 ? 1 : 2;
        float speed = enemy.Definition.ProjectileSpeed;

        for (int f = 0; f < fanCount; f++)
        {
            float offset = fanCount == 1 ? 0f : (f == 0 ? -0.08f : 0.08f);
            for (int i = 0; i < bulletCount; i++)
            {
                float t = (float)i / (bulletCount - 1);
                float angle = targetAngle - spread * 0.5f + spread * t + offset;
                SpawnHostileProjectile(enemy, angle, speed + f * 18f, Palette.EnemyBoss, Palette.EnemyBossGlow);
            }
        }
    }

    private void FireBossNova(EnemyActor enemy)
    {
        int bulletCount = enemy.Phase == 1 ? 18 : 28;
        int ringCount = enemy.Phase == 1 ? 1 : 2;
        float speed = enemy.Definition.ProjectileSpeed;

        for (int r = 0; r < ringCount; r++)
        {
            float offset = r * (Mathf.Pi / bulletCount);
            for (int i = 0; i < bulletCount; i++)
            {
                float angle = offset + i * (Mathf.Pi * 2f / bulletCount);
                SpawnHostileProjectile(enemy, angle, speed - r * 24f, Palette.Danger, Palette.EnemyBossGlow);
            }
        }
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
        for (int i = _projectiles.Count - 1; i >= 0; i--)
        {
            var p = _projectiles[i];
            p.Age += delta;
            p.X += p.Vx * delta;
            p.Y += p.Vy * delta;

            bool hitPlayer = !playerDashing && new Vector2(p.X - playerPos.X, p.Y - playerPos.Y).Length() <= p.Radius + playerRadius;
            bool outside = p.X < _arenaBounds.Position.X - 20 || p.X > _arenaBounds.End.X + 20
                        || p.Y < _arenaBounds.Position.Y - 20 || p.Y > _arenaBounds.End.Y + 20;
            bool hitObstacle = false;
            foreach (var obs in _obstacles)
            {
                if (p.X >= obs.X && p.X <= obs.X + obs.Width && p.Y >= obs.Y && p.Y <= obs.Y + obs.Height)
                { hitObstacle = true; break; }
            }

            if (hitPlayer)
            {
                callbacks.OnPlayerDamaged(p.Damage, p.X, p.Y);
                _projectiles.RemoveAt(i);
                continue;
            }
            if (outside || hitObstacle || p.Age >= p.Duration)
                _projectiles.RemoveAt(i);
        }
    }

    private void ResolveEnemySeparation()
    {
        for (int i = 0; i < _enemies.Count; i++)
        {
            var left = _enemies[i];
            for (int j = i + 1; j < _enemies.Count; j++)
            {
                var right = _enemies[j];
                float dx = right.X - left.X;
                float dy = right.Y - left.Y;
                float dist = new Vector2(dx, dy).Length();
                float minimum = left.Definition.Radius + right.Definition.Radius + 6f;
                if (dist <= 0.0001f || dist >= minimum) continue;
                float overlap = (minimum - dist) * 0.5f;
                float nx = dx / dist;
                float ny = dy / dist;
                left.X = Mathf.Clamp(left.X - nx * overlap, _arenaBounds.Position.X, _arenaBounds.End.X);
                left.Y = Mathf.Clamp(left.Y - ny * overlap, _arenaBounds.Position.Y, _arenaBounds.End.Y);
                right.X = Mathf.Clamp(right.X + nx * overlap, _arenaBounds.Position.X, _arenaBounds.End.X);
                right.Y = Mathf.Clamp(right.Y + ny * overlap, _arenaBounds.Position.Y, _arenaBounds.End.Y);
            }
        }
    }

    private void ClearEnemies() => _enemies.Clear();
}
