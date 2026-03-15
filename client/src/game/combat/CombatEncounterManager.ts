import type { Container } from 'pixi.js'

import { clamp, lerp, segmentCircleIntersection } from '../combat/combatMath'
import { NEXT_WAVE_DELAY, SNIPER_DAMAGE, WAVE_START_DELAY } from '../combat/constants'
import type { ArenaBounds, WorldObstacle } from '../core/contracts'
import { hostileByType } from '../data/hostiles'
import { buildWaveHint, buildWaveOrders, type SpawnOrder } from '../data/waves'
import type { HostileType } from '../data/types'
import { EnemyAvatar } from '../entities/EnemyAvatar'
import { palette } from '../theme/palette'
import { resolveCircleWorldMovement } from '../world/collision'
import type { EncounterState, EnemyActor, EnemyProjectile, SegmentHit } from '../combat/types'

export interface CombatPlayerState {
  x: number
  y: number
  radius: number
  isDashing: boolean
}

export interface CombatEncounterCallbacks {
  onWaveStarted: (waveIndex: number, hint: string) => void
  onEnemySpawned: (type: HostileType) => void
  onBossSpawned: (enemy: EnemyActor) => void
  onBossPhaseShift: (enemy: EnemyActor) => void
  onBossAttack: (pattern: 'fan' | 'nova', enemy: EnemyActor, targetAngle?: number) => void
  onEnemyHit: (enemy: EnemyActor, amount: number, impactX: number, impactY: number) => void
  onEnemyKilled: (enemy: EnemyActor) => void
  onPlayerDamaged: (amount: number, sourceX: number, sourceY: number) => void
  onBossDefeated: (enemy: EnemyActor) => void
}

export class CombatEncounterManager {
  private readonly enemies: EnemyActor[] = []
  private readonly enemyProjectiles: EnemyProjectile[] = []
  private readonly pendingSpawns: SpawnOrder[] = []
  private readonly enemyLayer: Container

  private arenaBounds: ArenaBounds = { left: 0, top: 0, right: 0, bottom: 0 }
  private worldObstacles: readonly WorldObstacle[] = []
  private spawnPoints: Array<{ x: number; y: number }> = []
  private bossSpawnPoint = { x: 0, y: 0 }
  private encounterState: EncounterState = 'active'
  private waveIndex = 0
  private killCount = 0
  private nextWaveDelay = WAVE_START_DELAY
  private spawnTimer = 0
  private enemyId = 0

  constructor(enemyLayer: Container) {
    this.enemyLayer = enemyLayer
  }

  resize(
    arenaBounds: ArenaBounds,
    worldObstacles: readonly WorldObstacle[],
    spawnPoints: Array<{ x: number; y: number }>,
    bossSpawnPoint: { x: number; y: number },
  ): void {
    this.arenaBounds = arenaBounds
    this.worldObstacles = worldObstacles
    this.spawnPoints = spawnPoints
    this.bossSpawnPoint = bossSpawnPoint
  }

  reset(): void {
    this.encounterState = 'active'
    this.waveIndex = 0
    this.killCount = 0
    this.nextWaveDelay = WAVE_START_DELAY
    this.spawnTimer = 0
    this.pendingSpawns.length = 0
    this.enemyProjectiles.length = 0
    this.clearEnemies()
  }

  destroy(): void {
    this.clearEnemies()
    this.enemyProjectiles.length = 0
    this.pendingSpawns.length = 0
  }

  markPlayerDown(): void {
    this.encounterState = 'down'
    this.pendingSpawns.length = 0
  }

  markEncounterClear(): void {
    this.encounterState = 'clear'
    this.pendingSpawns.length = 0
    this.enemyProjectiles.length = 0
    this.clearEnemies()
  }

  restoreCheckpoint(
    checkpoint: {
      currentWave: number
      hostilesRemaining: number
      boss: {
        spawned: boolean
        defeated: boolean
        phase: 1 | 2 | null
        health: number | null
      }
      kills: number
    },
    callbacks: CombatEncounterCallbacks,
  ): void {
    this.reset()
    this.waveIndex = Math.max(0, checkpoint.currentWave)
    this.killCount = Math.max(0, checkpoint.kills)

    if (checkpoint.boss.defeated) {
      this.markEncounterClear()
      return
    }

    if (checkpoint.boss.spawned) {
      this.spawnEnemy('boss', callbacks)

      const boss = this.getBoss()

      if (!boss) {
        return
      }

      boss.health = clamp(checkpoint.boss.health ?? boss.definition.maxHealth, 0, boss.definition.maxHealth)
      boss.phase = checkpoint.boss.phase ?? 1
      boss.phaseShifted = boss.phase === 2
      boss.mode = 'advance'
      boss.attackCooldown = boss.phase === 2 ? 0.55 : 0.9
      boss.avatar.setLifeRatio(boss.health / boss.definition.maxHealth)
      return
    }

    if (this.waveIndex > 0 && checkpoint.hostilesRemaining > 0) {
      this.pendingSpawns.push(...buildWaveOrders(this.waveIndex))
      this.spawnTimer = 0.2
      this.nextWaveDelay = NEXT_WAVE_DELAY
      return
    }

    this.nextWaveDelay = this.waveIndex === 0 ? WAVE_START_DELAY : 0.35
  }

  getEncounterState(): EncounterState {
    return this.encounterState
  }

  getWaveIndex(): number {
    return this.waveIndex
  }

  getKillCount(): number {
    return this.killCount
  }

  getPendingSpawnCount(): number {
    return this.pendingSpawns.length
  }

  getEnemies(): readonly EnemyActor[] {
    return this.enemies
  }

  getEnemyProjectiles(): readonly EnemyProjectile[] {
    return this.enemyProjectiles
  }

  getBoss(): EnemyActor | null {
    return this.enemies.find((enemy) => enemy.type === 'boss') ?? null
  }

  update(
    deltaSeconds: number,
    elapsedSeconds: number,
    player: CombatPlayerState,
    callbacks: CombatEncounterCallbacks,
  ): void {
    if (this.encounterState !== 'active') {
      return
    }

    this.advanceSpawnQueue(deltaSeconds, callbacks)
    this.updateEnemies(deltaSeconds, elapsedSeconds, player, callbacks)
    this.updateEnemyProjectiles(deltaSeconds, player, callbacks)
  }

  resolveSegmentHits(origin: { x: number; y: number }, target: { x: number; y: number }, extraRadius: number): SegmentHit[] {
    const hits: SegmentHit[] = []

    for (const enemy of this.enemies) {
      const t = segmentCircleIntersection(origin, target, enemy, enemy.definition.radius + extraRadius)

      if (t === null) {
        continue
      }

      hits.push({
        enemy,
        t,
        pointX: lerp(origin.x, target.x, t),
        pointY: lerp(origin.y, target.y, t),
      })
    }

    hits.sort((left, right) => left.t - right.t)

    return hits
  }

  damageEnemy(enemy: EnemyActor, amount: number, impactX: number, impactY: number, callbacks: CombatEncounterCallbacks): void {
    const index = this.enemies.indexOf(enemy)

    if (index === -1) {
      return
    }

    enemy.health = Math.max(0, enemy.health - amount)
    enemy.avatar.triggerDamageFlash(amount >= SNIPER_DAMAGE ? 1 : 0.74)
    callbacks.onEnemyHit(enemy, amount, impactX, impactY)

    if (enemy.health > 0) {
      return
    }

    this.killCount += 1
    callbacks.onEnemyKilled(enemy)
    this.enemyLayer.removeChild(enemy.avatar.container)
    enemy.avatar.destroy()
    this.enemies.splice(index, 1)

    if (enemy.type === 'boss') {
      this.encounterState = 'clear'
      this.pendingSpawns.length = 0
      this.enemyProjectiles.length = 0
      callbacks.onBossDefeated(enemy)
    }
  }

  applyExplosionDamage(x: number, y: number, radius: number, damageAmount: number, callbacks: CombatEncounterCallbacks): void {
    for (const enemy of [...this.enemies]) {
      const distance = Math.hypot(enemy.x - x, enemy.y - y)
      const reach = radius + enemy.definition.radius

      if (distance > reach) {
        continue
      }

      const falloff = 1 - distance / reach
      const damage = damageAmount * (0.55 + falloff * 0.45)

      this.damageEnemy(enemy, damage, enemy.x, enemy.y, callbacks)
    }
  }

  private advanceSpawnQueue(deltaSeconds: number, callbacks: CombatEncounterCallbacks): void {
    if (this.pendingSpawns.length > 0) {
      this.spawnTimer -= deltaSeconds

      while (this.spawnTimer <= 0 && this.pendingSpawns.length > 0) {
        const next = this.pendingSpawns.shift()

        if (!next) {
          break
        }

        this.spawnEnemy(next.type, callbacks)
        this.spawnTimer += next.delay
      }
      return
    }

    if (this.enemies.length > 0) {
      return
    }

    this.nextWaveDelay -= deltaSeconds

    if (this.nextWaveDelay <= 0) {
      this.waveIndex += 1
      this.nextWaveDelay = NEXT_WAVE_DELAY
      this.spawnTimer = 0.2
      this.pendingSpawns.push(...buildWaveOrders(this.waveIndex))
      callbacks.onWaveStarted(this.waveIndex, buildWaveHint(this.waveIndex))
    }
  }

  private spawnEnemy(type: HostileType, callbacks: CombatEncounterCallbacks): void {
    const definition = hostileByType[type]
    const position = this.pickSpawnPoint(type)
    const enemy: EnemyActor = {
      id: this.enemyId,
      type,
      definition,
      avatar: new EnemyAvatar(type, this.enemyId * 0.37 + Math.random()),
      x: position.x,
      y: position.y,
      health: definition.maxHealth,
      contactCooldown: 0.1 + Math.random() * 0.2,
      attackCooldown: 0.35 + Math.random() * definition.attackCooldown,
      mode: 'advance',
      modeTimer: 0,
      chargeDirX: 0,
      chargeDirY: 1,
      facingAngle: -Math.PI / 2,
      phase: 1,
      pattern: 'nova',
      phaseShifted: false,
    }

    this.enemyId += 1
    enemy.avatar.setPosition(position.x, position.y)
    enemy.avatar.setLifeRatio(1)
    this.enemyLayer.addChild(enemy.avatar.container)
    this.enemies.push(enemy)
    callbacks.onEnemySpawned(type)

    if (type === 'boss') {
      callbacks.onBossSpawned(enemy)
    }
  }

  private pickSpawnPoint(type: HostileType): { x: number; y: number } {
    if (type === 'boss') {
      return this.bossSpawnPoint
    }

    const next = this.spawnPoints[this.enemyId % Math.max(1, this.spawnPoints.length)]

    if (next) {
      return next
    }

    const side = this.enemyId % 4

    if (side === 0) {
      return { x: this.arenaBounds.left + 24, y: lerp(this.arenaBounds.top, this.arenaBounds.bottom, Math.random()) }
    }

    if (side === 1) {
      return { x: this.arenaBounds.right - 24, y: lerp(this.arenaBounds.top, this.arenaBounds.bottom, Math.random()) }
    }

    if (side === 2) {
      return { x: lerp(this.arenaBounds.left, this.arenaBounds.right, Math.random()), y: this.arenaBounds.top + 24 }
    }

    return { x: lerp(this.arenaBounds.left, this.arenaBounds.right, Math.random()), y: this.arenaBounds.bottom - 24 }
  }

  private updateBossEnemy(
    enemy: EnemyActor,
    deltaSeconds: number,
    distance: number,
    dirX: number,
    dirY: number,
    callbacks: CombatEncounterCallbacks,
  ): { velocityX: number; velocityY: number } {
    const targetAngle = Math.atan2(dirY, dirX)
    const preferredDistance = (enemy.definition.preferredDistance ?? 250) - (enemy.phase === 2 ? 22 : 0)
    const orbitSign = enemy.id % 2 === 0 ? 1 : -1
    let velocityX = 0
    let velocityY = 0

    enemy.facingAngle = targetAngle

    if (!enemy.phaseShifted && enemy.health <= enemy.definition.maxHealth * 0.5) {
      enemy.phaseShifted = true
      enemy.phase = 2
      enemy.mode = 'recover'
      enemy.modeTimer = 1.05
      enemy.attackCooldown = 0.45
      callbacks.onBossPhaseShift(enemy)

      return { velocityX: 0, velocityY: 0 }
    }

    if (enemy.mode === 'aim') {
      enemy.modeTimer = Math.max(0, enemy.modeTimer - deltaSeconds)
      velocityX = -dirY * orbitSign * enemy.definition.moveSpeed * 0.2
      velocityY = dirX * orbitSign * enemy.definition.moveSpeed * 0.2

      if (enemy.modeTimer === 0) {
        if (enemy.pattern === 'fan') {
          this.fireBossFan(enemy, targetAngle)
          enemy.attackCooldown = enemy.phase === 1 ? 1.35 : 0.92
          callbacks.onBossAttack('fan', enemy, targetAngle)
        } else {
          this.fireBossNova(enemy)
          enemy.attackCooldown = enemy.phase === 1 ? 1.65 : 1.18
          callbacks.onBossAttack('nova', enemy)
        }

        enemy.mode = 'recover'
        enemy.modeTimer = enemy.phase === 1 ? 0.3 : 0.2
      }

      return { velocityX, velocityY }
    }

    if (enemy.mode === 'recover') {
      enemy.modeTimer = Math.max(0, enemy.modeTimer - deltaSeconds)

      if (enemy.modeTimer === 0) {
        enemy.mode = 'advance'
      }

      return { velocityX: 0, velocityY: 0 }
    }

    if (distance > preferredDistance + 42) {
      velocityX = dirX * enemy.definition.moveSpeed
      velocityY = dirY * enemy.definition.moveSpeed
    } else if (distance < preferredDistance - 32) {
      velocityX = -dirX * enemy.definition.moveSpeed * 0.85
      velocityY = -dirY * enemy.definition.moveSpeed * 0.85
    } else {
      velocityX = -dirY * orbitSign * enemy.definition.moveSpeed * 0.62
      velocityY = dirX * orbitSign * enemy.definition.moveSpeed * 0.62
    }

    if (enemy.attackCooldown === 0) {
      enemy.pattern = enemy.pattern === 'fan' ? 'nova' : 'fan'
      enemy.mode = 'aim'
      enemy.modeTimer = enemy.pattern === 'fan' ? (enemy.phase === 1 ? 0.66 : 0.48) : enemy.phase === 1 ? 0.92 : 0.68
      enemy.avatar.triggerAttackPulse(1)
    }

    return { velocityX, velocityY }
  }

  private fireBossFan(enemy: EnemyActor, targetAngle: number): void {
    const bulletCount = enemy.phase === 1 ? 7 : 11
    const spread = enemy.phase === 1 ? 0.95 : 1.28
    const fanCount = enemy.phase === 1 ? 1 : 2

    for (let fanIndex = 0; fanIndex < fanCount; fanIndex += 1) {
      const offset = fanCount === 1 ? 0 : (fanIndex === 0 ? -0.08 : 0.08)

      for (let index = 0; index < bulletCount; index += 1) {
        const t = index / (bulletCount - 1)
        const angle = targetAngle - spread * 0.5 + spread * t + offset

        this.spawnHostileProjectile(enemy, angle, (enemy.definition.projectileSpeed ?? 290) + fanIndex * 18, palette.enemyBoss, palette.enemyBossGlow)
      }
    }
  }

  private fireBossNova(enemy: EnemyActor): void {
    const bulletCount = enemy.phase === 1 ? 18 : 28
    const ringCount = enemy.phase === 1 ? 1 : 2

    for (let ringIndex = 0; ringIndex < ringCount; ringIndex += 1) {
      const offset = ringIndex * (Math.PI / bulletCount)

      for (let index = 0; index < bulletCount; index += 1) {
        const angle = offset + index * ((Math.PI * 2) / bulletCount)

        this.spawnHostileProjectile(enemy, angle, (enemy.definition.projectileSpeed ?? 290) - ringIndex * 24, palette.danger, palette.enemyBossGlow)
      }
    }
  }

  private updateEnemies(
    deltaSeconds: number,
    elapsedSeconds: number,
    player: CombatPlayerState,
    callbacks: CombatEncounterCallbacks,
  ): void {
    for (const enemy of this.enemies) {
      enemy.contactCooldown = Math.max(0, enemy.contactCooldown - deltaSeconds)
      enemy.attackCooldown = Math.max(0, enemy.attackCooldown - deltaSeconds)

      const deltaX = player.x - enemy.x
      const deltaY = player.y - enemy.y
      const distance = Math.hypot(deltaX, deltaY)
      const dirX = distance > 0.0001 ? deltaX / distance : 0
      const dirY = distance > 0.0001 ? deltaY / distance : -1
      enemy.facingAngle = Math.atan2(deltaY, deltaX)

      let velocityX = 0
      let velocityY = 0

      if (enemy.type === 'melee') {
        velocityX = dirX * enemy.definition.moveSpeed
        velocityY = dirY * enemy.definition.moveSpeed
      } else if (enemy.type === 'ranged') {
        if (enemy.mode === 'aim') {
          enemy.modeTimer = Math.max(0, enemy.modeTimer - deltaSeconds)

          if (enemy.modeTimer === 0) {
            this.spawnEnemyProjectile(enemy, dirX, dirY)
            enemy.mode = 'advance'
            enemy.attackCooldown = enemy.definition.attackCooldown
            enemy.avatar.triggerAttackPulse(1)
          }
        } else {
          const preferredDistance = enemy.definition.preferredDistance ?? 250

          if (distance > preferredDistance + 48) {
            velocityX = dirX * enemy.definition.moveSpeed
            velocityY = dirY * enemy.definition.moveSpeed
          } else if (distance < preferredDistance - 44) {
            velocityX = -dirX * enemy.definition.moveSpeed * 0.85
            velocityY = -dirY * enemy.definition.moveSpeed * 0.85
          } else {
            const orbitSign = enemy.id % 2 === 0 ? 1 : -1

            velocityX = -dirY * orbitSign * enemy.definition.moveSpeed * 0.58
            velocityY = dirX * orbitSign * enemy.definition.moveSpeed * 0.58
          }

          if (enemy.attackCooldown === 0 && distance <= (enemy.definition.attackRange ?? 420)) {
            enemy.mode = 'aim'
            enemy.modeTimer = enemy.definition.attackWindup ?? 0.42
            enemy.avatar.triggerAttackPulse(0.55)
          }
        }
      } else if (enemy.type === 'charger') {
        if (enemy.mode === 'windup') {
          enemy.facingAngle = Math.atan2(enemy.chargeDirY, enemy.chargeDirX)
          enemy.modeTimer = Math.max(0, enemy.modeTimer - deltaSeconds)

          if (enemy.modeTimer === 0) {
            enemy.mode = 'charge'
            enemy.modeTimer = enemy.definition.chargeDuration ?? 0.28
            enemy.attackCooldown = enemy.definition.attackCooldown
            enemy.avatar.triggerAttackPulse(1)
          }
        } else if (enemy.mode === 'charge') {
          enemy.facingAngle = Math.atan2(enemy.chargeDirY, enemy.chargeDirX)
          velocityX = enemy.chargeDirX * (enemy.definition.chargeSpeed ?? 560)
          velocityY = enemy.chargeDirY * (enemy.definition.chargeSpeed ?? 560)
          enemy.modeTimer = Math.max(0, enemy.modeTimer - deltaSeconds)

          if (enemy.modeTimer === 0) {
            enemy.mode = 'recover'
            enemy.modeTimer = enemy.definition.recoverDuration ?? 0.5
          }
        } else if (enemy.mode === 'recover') {
          enemy.modeTimer = Math.max(0, enemy.modeTimer - deltaSeconds)

          if (enemy.modeTimer === 0) {
            enemy.mode = 'advance'
          }
        } else {
          velocityX = dirX * enemy.definition.moveSpeed
          velocityY = dirY * enemy.definition.moveSpeed

          if (enemy.attackCooldown === 0 && distance <= (enemy.definition.chargeTriggerDistance ?? 240)) {
            enemy.mode = 'windup'
            enemy.modeTimer = enemy.definition.attackWindup ?? 0.56
            enemy.chargeDirX = dirX
            enemy.chargeDirY = dirY
            enemy.avatar.triggerAttackPulse(0.75)
          }
        }
      } else {
        const bossMotion = this.updateBossEnemy(enemy, deltaSeconds, distance, dirX, dirY, callbacks)

        velocityX = bossMotion.velocityX
        velocityY = bossMotion.velocityY
      }

      enemy.avatar.setAimAngle(enemy.facingAngle)

      const nextPosition = resolveCircleWorldMovement(
        { x: enemy.x, y: enemy.y },
        enemy.x + velocityX * deltaSeconds,
        enemy.y + velocityY * deltaSeconds,
        enemy.definition.radius,
        this.arenaBounds,
        this.worldObstacles,
      )

      enemy.x = nextPosition.x
      enemy.y = nextPosition.y

      const currentDeltaX = player.x - enemy.x
      const currentDeltaY = player.y - enemy.y
      const currentDistance = Math.hypot(currentDeltaX, currentDeltaY)
      const minDistance = enemy.definition.radius + player.radius + 2

      if (currentDistance > 0.0001 && currentDistance < minDistance) {
        const scale = minDistance / currentDistance

        enemy.x = player.x - currentDeltaX * scale
        enemy.y = player.y - currentDeltaY * scale
      }

      if (enemy.contactCooldown === 0 && currentDistance <= enemy.definition.radius + player.radius + 6) {
        callbacks.onPlayerDamaged(enemy.definition.contactDamage, enemy.x, enemy.y)
        enemy.contactCooldown = enemy.definition.contactInterval
        enemy.avatar.triggerAttackPulse(0.78)
      }

      enemy.avatar.setPosition(enemy.x, enemy.y)
      enemy.avatar.setMotion(Math.min(1, Math.hypot(velocityX, velocityY) / Math.max(1, enemy.definition.moveSpeed)))
      enemy.avatar.setMode(enemy.mode)
      enemy.avatar.setLifeRatio(enemy.health / enemy.definition.maxHealth)
      enemy.avatar.update(deltaSeconds, elapsedSeconds)
    }

    this.resolveEnemySeparation()
  }

  private resolveEnemySeparation(): void {
    for (let leftIndex = 0; leftIndex < this.enemies.length; leftIndex += 1) {
      const left = this.enemies[leftIndex]

      for (let rightIndex = leftIndex + 1; rightIndex < this.enemies.length; rightIndex += 1) {
        const right = this.enemies[rightIndex]
        const deltaX = right.x - left.x
        const deltaY = right.y - left.y
        const distance = Math.hypot(deltaX, deltaY)
        const minimum = left.definition.radius + right.definition.radius + 6

        if (distance <= 0.0001 || distance >= minimum) {
          continue
        }

        const overlap = (minimum - distance) * 0.5
        const normalX = deltaX / distance
        const normalY = deltaY / distance

        left.x = clamp(left.x - normalX * overlap, this.arenaBounds.left, this.arenaBounds.right)
        left.y = clamp(left.y - normalY * overlap, this.arenaBounds.top, this.arenaBounds.bottom)
        right.x = clamp(right.x + normalX * overlap, this.arenaBounds.left, this.arenaBounds.right)
        right.y = clamp(right.y + normalY * overlap, this.arenaBounds.top, this.arenaBounds.bottom)
        left.avatar.setPosition(left.x, left.y)
        right.avatar.setPosition(right.x, right.y)
      }
    }
  }

  private spawnEnemyProjectile(enemy: EnemyActor, dirX: number, dirY: number): void {
    this.spawnHostileProjectile(enemy, Math.atan2(dirY, dirX), enemy.definition.projectileSpeed ?? 320, palette.enemyProjectile, palette.accentSoft)
  }

  private spawnHostileProjectile(enemy: EnemyActor, angle: number, speed: number, color: number, glowColor: number): void {
    const radius = enemy.definition.projectileRadius ?? 7
    const dirX = Math.cos(angle)
    const dirY = Math.sin(angle)

    this.enemyProjectiles.push({
      x: enemy.x + dirX * (enemy.definition.radius + 10),
      y: enemy.y + dirY * (enemy.definition.radius + 10),
      vx: dirX * speed,
      vy: dirY * speed,
      radius,
      damage: enemy.definition.projectileDamage ?? 12,
      age: 0,
      duration: 2.2,
      color,
      glowColor,
    })
  }

  private updateEnemyProjectiles(deltaSeconds: number, player: CombatPlayerState, callbacks: CombatEncounterCallbacks): void {
    for (let index = this.enemyProjectiles.length - 1; index >= 0; index -= 1) {
      const projectile = this.enemyProjectiles[index]

      projectile.age += deltaSeconds
      projectile.x += projectile.vx * deltaSeconds
      projectile.y += projectile.vy * deltaSeconds

      const hitPlayer = !player.isDashing && Math.hypot(projectile.x - player.x, projectile.y - player.y) <= projectile.radius + player.radius
      const outsideArena =
        projectile.x < this.arenaBounds.left - 20 ||
        projectile.x > this.arenaBounds.right + 20 ||
        projectile.y < this.arenaBounds.top - 20 ||
        projectile.y > this.arenaBounds.bottom + 20
      const hitObstacle = this.worldObstacles.some(
        (obstacle) =>
          projectile.x >= obstacle.x &&
          projectile.x <= obstacle.x + obstacle.width &&
          projectile.y >= obstacle.y &&
          projectile.y <= obstacle.y + obstacle.height,
      )

      if (hitPlayer) {
        callbacks.onPlayerDamaged(projectile.damage, projectile.x, projectile.y)
        this.enemyProjectiles.splice(index, 1)
        continue
      }

      if (outsideArena || hitObstacle || projectile.age >= projectile.duration) {
        this.enemyProjectiles.splice(index, 1)
      }
    }
  }

  private clearEnemies(): void {
    for (const enemy of this.enemies) {
      this.enemyLayer.removeChild(enemy.avatar.container)
      enemy.avatar.destroy()
    }

    this.enemies.length = 0
  }
}
