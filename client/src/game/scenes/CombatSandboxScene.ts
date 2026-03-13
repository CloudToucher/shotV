import { Container, Graphics, Text } from 'pixi.js'

import { clamp, clampToDistance, clipToArena, easeOutCubic, lerp, segmentCircleIntersection } from '../combat/combatMath'
import type { ArenaBounds, GameScene, InputSnapshot, ViewportSize } from '../core/contracts'
import { EnemyAvatar } from '../entities/EnemyAvatar'
import { PlayerAvatar } from '../entities/PlayerAvatar'
import { hostileByType, type HostileDefinition, type HostileMode, type HostileType } from '../hostileConfig'
import { palette } from '../theme/palette'
import { buildWaveHint, buildWaveOrders, type SpawnOrder } from '../waves'
import { weaponBySlot, weaponLoadout, type WeaponDefinition, type WeaponType } from '../weapons/weaponConfig'

const FRAME_MARGIN = 72
const PLAYER_PADDING = 56
const GRID_SIZE = 56
const MACHINE_GUN_SPEED = 960
const SNIPER_SPEED = 1560
const PLAYER_MAX_HEALTH = 100
const MACHINE_GUN_DAMAGE = 11
const GRENADE_DAMAGE = 34
const SNIPER_DAMAGE = 48
const WAVE_START_DELAY = 0.75
const NEXT_WAVE_DELAY = 1.1

interface NeedleProjectile {
  startX: number
  startY: number
  endX: number
  endY: number
  dirX: number
  dirY: number
  age: number
  duration: number
  length: number
  width: number
  color: number
  coreColor: number
}

interface BurstRing {
  x: number
  y: number
  age: number
  duration: number
  startRadius: number
  endRadius: number
  color: number
  width: number
}

interface MuzzleFlash {
  x: number
  y: number
  angle: number
  age: number
  duration: number
  size: number
  weaponType: WeaponType
}

interface GrenadeProjectile {
  startX: number
  startY: number
  endX: number
  endY: number
  age: number
  duration: number
}

interface GrenadeExplosion {
  x: number
  y: number
  age: number
  duration: number
  radius: number
}

interface ImpactParticle {
  x: number
  y: number
  vx: number
  vy: number
  rotation: number
  spin: number
  age: number
  duration: number
  length: number
  width: number
  color: number
  alpha: number
  drag: number
}

interface DashAfterimage {
  x: number
  y: number
  aimAngle: number
  weaponType: WeaponType
  age: number
  duration: number
  scale: number
}

interface EnemyProjectile {
  x: number
  y: number
  vx: number
  vy: number
  radius: number
  damage: number
  age: number
  duration: number
  color: number
  glowColor: number
}

interface EnemyActor {
  id: number
  type: HostileType
  definition: HostileDefinition
  avatar: EnemyAvatar
  x: number
  y: number
  health: number
  contactCooldown: number
  attackCooldown: number
  mode: HostileMode
  modeTimer: number
  chargeDirX: number
  chargeDirY: number
  facingAngle: number
  phase: 1 | 2
  pattern: 'fan' | 'nova'
  phaseShifted: boolean
}

interface SegmentHit {
  enemy: EnemyActor
  t: number
  pointX: number
  pointY: number
}

type EncounterState = 'active' | 'down' | 'clear'

export class CombatSandboxScene implements GameScene {
  readonly container = new Container()

  private readonly cameraRoot = new Container()
  private readonly backdrop = new Graphics()
  private readonly grid = new Graphics()
  private readonly frame = new Graphics()
  private readonly world = new Container()
  private readonly enemyLayer = new Container()
  private readonly aimGuide = new Graphics()
  private readonly effects = new Graphics()
  private readonly reticle = new Graphics()
  private readonly hud = new Container()
  private readonly hudGraphics = new Graphics()
  private readonly toastTitle = new Text({
    text: '',
    style: {
      fontFamily: 'Bahnschrift, Segoe UI, sans-serif',
      fontSize: 24,
      fontWeight: '700',
      fill: palette.uiText,
    },
  })
  private readonly toastHint = new Text({
    text: '',
    style: {
      fontFamily: 'Bahnschrift, Segoe UI, sans-serif',
      fontSize: 13,
      fill: palette.uiMuted,
      letterSpacing: 0.6,
    },
  })
  private readonly healthText = new Text({
    text: '',
    style: {
      fontFamily: 'Bahnschrift, Segoe UI, sans-serif',
      fontSize: 14,
      fontWeight: '700',
      fill: palette.uiText,
      letterSpacing: 0.3,
    },
  })
  private readonly waveText = new Text({
    text: '',
    style: {
      fontFamily: 'Bahnschrift, Segoe UI, sans-serif',
      fontSize: 14,
      fontWeight: '700',
      fill: palette.uiText,
      letterSpacing: 0.3,
    },
  })
  private readonly enemyText = new Text({
    text: '',
    style: {
      fontFamily: 'Bahnschrift, Segoe UI, sans-serif',
      fontSize: 12,
      fill: palette.uiMuted,
      letterSpacing: 0.4,
    },
  })
  private readonly bossText = new Text({
    text: '',
    style: {
      fontFamily: 'Bahnschrift, Segoe UI, sans-serif',
      fontSize: 13,
      fontWeight: '700',
      fill: palette.uiText,
      letterSpacing: 0.7,
    },
  })
  private readonly centerTitle = new Text({
    text: '',
    style: {
      fontFamily: 'Bahnschrift, Segoe UI, sans-serif',
      fontSize: 28,
      fontWeight: '700',
      fill: palette.uiText,
    },
  })
  private readonly centerHint = new Text({
    text: '',
    style: {
      fontFamily: 'Bahnschrift, Segoe UI, sans-serif',
      fontSize: 13,
      fill: palette.uiMuted,
      letterSpacing: 0.6,
    },
  })
  private readonly weaponLabels = weaponLoadout.map(
    (weapon) =>
      new Text({
        text: `${weapon.slot} ${weapon.label}`,
        style: {
          fontFamily: 'Bahnschrift, Segoe UI, sans-serif',
          fontSize: 16,
          fontWeight: '700',
          fill: palette.uiText,
        },
      }),
  )
  private readonly player = new PlayerAvatar()
  private readonly needleProjectiles: NeedleProjectile[] = []
  private readonly burstRings: BurstRing[] = []
  private readonly muzzleFlashes: MuzzleFlash[] = []
  private readonly grenadeProjectiles: GrenadeProjectile[] = []
  private readonly grenadeExplosions: GrenadeExplosion[] = []
  private readonly impactParticles: ImpactParticle[] = []
  private readonly dashAfterimages: DashAfterimage[] = []
  private readonly enemyProjectiles: EnemyProjectile[] = []
  private readonly enemies: EnemyActor[] = []
  private readonly pendingSpawns: SpawnOrder[] = []

  private viewport: ViewportSize = { width: 0, height: 0 }
  private arenaBounds: ArenaBounds = { left: 0, top: 0, right: 0, bottom: 0 }
  private currentWeapon: WeaponDefinition = weaponBySlot[1]
  private lastAimPoint = { x: 0, y: 0 }
  private shotCooldown = 0
  private playerHealth = PLAYER_MAX_HEALTH
  private waveIndex = 0
  private killCount = 0
  private nextWaveDelay = WAVE_START_DELAY
  private spawnTimer = 0
  private enemyId = 0
  private messageTimer = 0
  private encounterState: EncounterState = 'active'
  private visualTime = 0
  private shakeTrauma = 0
  private dashGhostTimer = 0
  private healthHudPulse = 0
  private infoHudPulse = 0
  private weaponHudPulse = 0
  private hasPlacedPlayer = false

  constructor() {
    this.toastTitle.anchor.set(0.5)
    this.toastHint.anchor.set(0.5)
    this.bossText.anchor.set(0.5, 0)
    this.centerTitle.anchor.set(0.5)
    this.centerHint.anchor.set(0.5)

    this.container.addChild(this.cameraRoot, this.hud)
    this.cameraRoot.addChild(this.backdrop, this.grid, this.frame, this.world, this.aimGuide, this.effects, this.reticle)
    this.world.addChild(this.enemyLayer, this.player.container)
    this.hud.addChild(
      this.hudGraphics,
      ...this.weaponLabels,
      this.toastTitle,
      this.toastHint,
      this.healthText,
      this.waveText,
      this.enemyText,
      this.bossText,
      this.centerTitle,
      this.centerHint,
    )

    this.applyWeapon(1, true)
    this.resetEncounter(true)
  }

  resize(viewport: ViewportSize): void {
    this.viewport = viewport

    const arenaWidth = Math.max(360, viewport.width - FRAME_MARGIN * 2)
    const arenaHeight = Math.max(260, viewport.height - FRAME_MARGIN * 2)
    const arenaLeft = (viewport.width - arenaWidth) / 2
    const arenaTop = (viewport.height - arenaHeight) / 2

    this.arenaBounds = {
      left: arenaLeft + PLAYER_PADDING,
      top: arenaTop + PLAYER_PADDING,
      right: arenaLeft + arenaWidth - PLAYER_PADDING,
      bottom: arenaTop + arenaHeight - PLAYER_PADDING,
    }

    this.drawBackdrop(arenaLeft, arenaTop, arenaWidth, arenaHeight)
    this.layoutHud()

    if (!this.hasPlacedPlayer) {
      this.player.setPosition(viewport.width / 2, viewport.height / 2)
      this.lastAimPoint = { x: viewport.width / 2, y: viewport.height / 2 - 120 }
      this.hasPlacedPlayer = true
    }
  }

  update(deltaSeconds: number, elapsedSeconds: number, input: InputSnapshot): void {
    this.visualTime = elapsedSeconds
    this.shotCooldown = Math.max(0, this.shotCooldown - deltaSeconds)
    this.messageTimer = Math.max(0, this.messageTimer - deltaSeconds)
    this.healthHudPulse = Math.max(0, this.healthHudPulse - deltaSeconds * 3.1)
    this.infoHudPulse = Math.max(0, this.infoHudPulse - deltaSeconds * 2.8)
    this.weaponHudPulse = Math.max(0, this.weaponHudPulse - deltaSeconds * 4.2)
    this.shakeTrauma = Math.max(0, this.shakeTrauma - deltaSeconds * 2.6)

    if (input.restartPressed && this.encounterState !== 'active') {
      this.resetEncounter(false)
    }

    if (this.encounterState === 'active') {
      this.updatePlayerControl(input)
    } else {
      this.player.setMoveIntent(0, 0)
    }

    this.player.update(deltaSeconds, this.arenaBounds, elapsedSeconds)

    if (this.encounterState === 'active') {
      this.advanceSpawnQueue(deltaSeconds)
      this.updateEnemies(deltaSeconds, elapsedSeconds)
      this.updateEnemyProjectiles(deltaSeconds)
    }

    this.updateEffects(deltaSeconds)
    this.updateFeedbackState(deltaSeconds)
    this.drawAimLayer(this.encounterState === 'active' && input.hasPointer)
    this.drawEffects()
    this.drawHud()
  }

  destroy(): void {
    this.clearEnemies()
    this.container.destroy({ children: true })
  }

  private updatePlayerControl(input: InputSnapshot): void {
    if (input.weaponSwitch) {
      this.applyWeapon(input.weaponSwitch)
    }

    const aimPoint = this.resolveAimPoint(input)
    const playerPosition = this.player.getPosition()
    const aimAngle = Math.atan2(aimPoint.y - playerPosition.y, aimPoint.x - playerPosition.x)

    this.lastAimPoint = aimPoint
    this.player.setAimAngle(aimAngle)
    this.player.setMoveIntent(input.moveX, input.moveY)

    if (input.dashPressed && this.player.requestDash()) {
      this.burstRings.push({
        x: playerPosition.x,
        y: playerPosition.y,
        age: 0,
        duration: 0.22,
        startRadius: 18,
        endRadius: 76,
        color: palette.dash,
        width: 3,
      })
      this.spawnDashGhost()
      this.addShake(0.08)
      this.weaponHudPulse = Math.max(this.weaponHudPulse, 0.55)
    }

    if (input.shootHeld && this.shotCooldown === 0 && input.hasPointer) {
      this.fireWeapon(aimPoint)
      this.shotCooldown = this.currentWeapon.cooldown
    }
  }

  private resetEncounter(showIntro: boolean): void {
    this.encounterState = 'active'
    this.playerHealth = PLAYER_MAX_HEALTH
    this.waveIndex = 0
    this.killCount = 0
    this.nextWaveDelay = WAVE_START_DELAY
    this.spawnTimer = 0
    this.shotCooldown = 0
    this.pendingSpawns.length = 0
    this.needleProjectiles.length = 0
    this.burstRings.length = 0
    this.muzzleFlashes.length = 0
    this.grenadeProjectiles.length = 0
    this.grenadeExplosions.length = 0
    this.impactParticles.length = 0
    this.dashAfterimages.length = 0
    this.enemyProjectiles.length = 0
    this.clearEnemies()
    this.player.reset()
    this.player.setLifeRatio(1)
    this.shakeTrauma = 0
    this.dashGhostTimer = 0
    this.healthHudPulse = 0
    this.infoHudPulse = 0
    this.weaponHudPulse = 0
    this.cameraRoot.position.set(0, 0)

    if (this.viewport.width > 0 && this.viewport.height > 0) {
      this.player.setPosition(this.viewport.width / 2, this.viewport.height / 2)
      this.lastAimPoint = { x: this.viewport.width / 2, y: this.viewport.height / 2 - 120 }
    }

    this.showToast(showIntro ? 'Combat Online' : 'Redeploy', showIntro ? 'Survive the waves. Break Aegis Prime.' : 'Wave chain reset', 1.1)
  }

  private clearEnemies(): void {
    for (const enemy of this.enemies) {
      this.enemyLayer.removeChild(enemy.avatar.container)
      enemy.avatar.destroy()
    }

    this.enemies.length = 0
  }

  private updateFeedbackState(deltaSeconds: number): void {
    if (this.player.isDashing()) {
      this.dashGhostTimer -= deltaSeconds

      while (this.dashGhostTimer <= 0) {
        this.spawnDashGhost()
        this.dashGhostTimer += 0.026
      }
    } else {
      this.dashGhostTimer = 0
    }

    for (const particle of this.impactParticles) {
      particle.age += deltaSeconds
      particle.x += particle.vx * deltaSeconds
      particle.y += particle.vy * deltaSeconds
      particle.vx *= Math.max(0, 1 - particle.drag * deltaSeconds)
      particle.vy *= Math.max(0, 1 - particle.drag * deltaSeconds)
      particle.rotation += particle.spin * deltaSeconds
    }

    for (const ghost of this.dashAfterimages) {
      ghost.age += deltaSeconds
    }

    removeExpired(this.impactParticles)
    removeExpired(this.dashAfterimages)

    if (this.shakeTrauma > 0.0001) {
      const magnitude = this.shakeTrauma * this.shakeTrauma * 14
      const swayX = Math.sin(this.visualTime * 64) * magnitude * 0.7
      const swayY = Math.cos(this.visualTime * 52) * magnitude

      this.cameraRoot.position.set(swayX, swayY)
    } else {
      this.cameraRoot.position.set(0, 0)
    }
  }

  private addShake(intensity: number): void {
    this.shakeTrauma = Math.min(1, this.shakeTrauma + intensity)
  }

  private spawnDashGhost(): void {
    const position = this.player.getPosition()

    this.dashAfterimages.push({
      x: position.x,
      y: position.y,
      aimAngle: this.player.getAimAngle(),
      weaponType: this.currentWeapon.id,
      age: 0,
      duration: 0.18,
      scale: 1 + this.weaponHudPulse * 0.08,
    })
  }

  private spawnImpactParticles(
    x: number,
    y: number,
    count: number,
    color: number,
    speedMin: number,
    speedMax: number,
    durationMin: number,
    durationMax: number,
    baseAngle?: number,
    spread = Math.PI,
  ): void {
    for (let index = 0; index < count; index += 1) {
      const angle = baseAngle === undefined ? Math.random() * Math.PI * 2 : baseAngle + (Math.random() - 0.5) * spread
      const speed = lerp(speedMin, speedMax, Math.random())
      const duration = lerp(durationMin, durationMax, Math.random())
      const length = lerp(7, 16, Math.random())

      this.impactParticles.push({
        x,
        y,
        vx: Math.cos(angle) * speed,
        vy: Math.sin(angle) * speed,
        rotation: angle,
        spin: (Math.random() - 0.5) * 12,
        age: 0,
        duration,
        length,
        width: lerp(1.6, 4.4, Math.random()),
        color,
        alpha: lerp(0.42, 0.94, Math.random()),
        drag: lerp(2.8, 5.4, Math.random()),
      })
    }
  }

  private spawnHitFeedback(x: number, y: number, impactScale: number, angle: number): void {
    const ringSize = 18 + impactScale * 18

    this.burstRings.push({
      x,
      y,
      age: 0,
      duration: 0.12 + impactScale * 0.04,
      startRadius: 6,
      endRadius: ringSize,
      color: palette.arenaCore,
      width: 2.6,
    })
    this.spawnImpactParticles(x, y, 5 + Math.round(impactScale * 4), palette.arenaCore, 80, 220 + impactScale * 80, 0.08, 0.18, angle, 1.3)
    this.spawnImpactParticles(x, y, 4 + Math.round(impactScale * 5), palette.accentSoft, 60, 190 + impactScale * 90, 0.1, 0.22, angle, 1.7)
    this.weaponHudPulse = Math.max(this.weaponHudPulse, 0.35 + impactScale * 0.35)
    this.infoHudPulse = Math.max(this.infoHudPulse, 0.2 + impactScale * 0.2)
    this.addShake(0.035 + impactScale * 0.05)
  }

  private spawnExplosionFeedback(x: number, y: number, radius: number): void {
    this.burstRings.push({
      x,
      y,
      age: 0,
      duration: 0.16,
      startRadius: 10,
      endRadius: radius * 0.82,
      color: palette.arenaCore,
      width: 3,
    })
    this.spawnImpactParticles(x, y, 18, palette.accent, 110, 300, 0.16, 0.34)
    this.spawnImpactParticles(x, y, 12, palette.accentSoft, 80, 240, 0.12, 0.26)
    this.addShake(0.24)
    this.infoHudPulse = Math.max(this.infoHudPulse, 0.8)
  }

  private spawnKillFragments(enemy: EnemyActor): void {
    const bodyCount = enemy.type === 'boss' ? 22 : 10
    const edgeCount = enemy.type === 'boss' ? 12 : 6

    this.spawnImpactParticles(enemy.x, enemy.y, bodyCount, enemy.definition.colors.body, 90, enemy.type === 'boss' ? 260 : 210, 0.18, 0.38)
    this.spawnImpactParticles(enemy.x, enemy.y, edgeCount, enemy.definition.colors.edge, 70, enemy.type === 'boss' ? 220 : 180, 0.16, 0.32)
    this.infoHudPulse = Math.max(this.infoHudPulse, 1)
    this.addShake(0.1)
  }

  private drawBackdrop(left: number, top: number, width: number, height: number): void {
    this.backdrop
      .clear()
      .rect(0, 0, this.viewport.width, this.viewport.height)
      .fill({ color: palette.bgOuter })
      .rect(0, 0, this.viewport.width, this.viewport.height * 0.62)
      .fill({ color: palette.bgInner, alpha: 0.95 })
      .roundRect(left, top, width, height, 28)
      .fill({ color: palette.arenaFill, alpha: 0.98 })
      .roundRect(left + 18, top + 18, width - 36, height - 36, 22)
      .fill({ color: palette.arenaCore, alpha: 0.92 })

    this.grid.clear()

    for (let x = left + GRID_SIZE; x < left + width; x += GRID_SIZE) {
      this.grid.moveTo(x, top)
      this.grid.lineTo(x, top + height)
    }

    for (let y = top + GRID_SIZE; y < top + height; y += GRID_SIZE) {
      this.grid.moveTo(left, y)
      this.grid.lineTo(left + width, y)
    }

    this.grid.stroke({ width: 1, color: palette.grid, alpha: 0.32 })

    this.frame
      .clear()
      .roundRect(left, top, width, height, 28)
      .stroke({ width: 2, color: palette.frame, alpha: 0.42, alignment: 0.5 })
      .roundRect(left + 18, top + 18, width - 36, height - 36, 22)
      .stroke({ width: 1, color: palette.frameSoft, alpha: 0.9, alignment: 0.5 })

    this.drawCornerMarks(left, top, width, height)
  }

  private drawCornerMarks(left: number, top: number, width: number, height: number): void {
    const mark = 28

    this.frame
      .moveTo(left + 22, top + mark)
      .lineTo(left + 22, top + 22)
      .lineTo(left + mark, top + 22)
      .moveTo(left + width - mark, top + 22)
      .lineTo(left + width - 22, top + 22)
      .lineTo(left + width - 22, top + mark)
      .moveTo(left + width - 22, top + height - mark)
      .lineTo(left + width - 22, top + height - 22)
      .lineTo(left + width - mark, top + height - 22)
      .moveTo(left + mark, top + height - 22)
      .lineTo(left + 22, top + height - 22)
      .lineTo(left + 22, top + height - mark)
      .stroke({ width: 3, color: palette.frame, alpha: 0.72, alignment: 0.5 })
  }

  private resolveAimPoint(input: InputSnapshot): { x: number; y: number } {
    const playerPosition = this.player.getPosition()

    if (input.hasPointer) {
      const clamped = {
        x: clamp(input.pointerX, this.arenaBounds.left, this.arenaBounds.right),
        y: clamp(input.pointerY, this.arenaBounds.top, this.arenaBounds.bottom),
      }

      return this.currentWeapon.id === 'grenade' ? clampToDistance(clamped, playerPosition, this.currentWeapon.range) : clamped
    }

    const aimAngle = this.player.getAimAngle()
    const idleDistance = this.currentWeapon.id === 'grenade' ? Math.min(this.currentWeapon.range, 120) : 120

    return {
      x: playerPosition.x + Math.cos(aimAngle) * idleDistance,
      y: playerPosition.y + Math.sin(aimAngle) * idleDistance,
    }
  }

  private fireWeapon(aimPoint: { x: number; y: number }): void {
    const playerPosition = this.player.getPosition()
    const aimAngle = Math.atan2(aimPoint.y - playerPosition.y, aimPoint.x - playerPosition.x)
    const dirX = Math.cos(aimAngle)
    const dirY = Math.sin(aimAngle)
    const origin = this.player.getShotOrigin()

    if (this.currentWeapon.id === 'grenade') {
      this.player.triggerShot(0.78)
      this.spawnMuzzleFlash(origin.x, origin.y, aimAngle, 28, 0.16, 'grenade')
      this.grenadeProjectiles.push({
        startX: origin.x,
        startY: origin.y,
        endX: aimPoint.x,
        endY: aimPoint.y,
        age: 0,
        duration: 0.34,
      })
      this.burstRings.push({
        x: origin.x,
        y: origin.y,
        age: 0,
        duration: 0.16,
        startRadius: 10,
        endRadius: 30,
        color: palette.accentSoft,
        width: 3,
      })
      this.weaponHudPulse = Math.max(this.weaponHudPulse, 0.72)
      this.addShake(0.07)
      return
    }

    const range = this.currentWeapon.id === 'sniper' ? 100000 : this.currentWeapon.range
    const target = { x: origin.x + dirX * range, y: origin.y + dirY * range }
    const clipped = clipToArena(origin, target, this.arenaBounds)
    const hits = this.resolveSegmentHits(origin, clipped, this.currentWeapon.effectWidth * 0.65)
    let trailEndX = clipped.x
    let trailEndY = clipped.y

    if (this.currentWeapon.id === 'machineGun' && hits.length > 0) {
      const hit = hits[0]

      trailEndX = hit.pointX
      trailEndY = hit.pointY
      this.damageEnemy(hit.enemy, MACHINE_GUN_DAMAGE, hit.pointX, hit.pointY)
    }

    if (this.currentWeapon.id === 'sniper' && hits.length > 0) {
      for (const hit of hits) {
        this.damageEnemy(hit.enemy, SNIPER_DAMAGE, hit.pointX, hit.pointY)
      }
    }

    const distance = Math.hypot(trailEndX - origin.x, trailEndY - origin.y)
    const isSniper = this.currentWeapon.id === 'sniper'
    const speed = isSniper ? SNIPER_SPEED : MACHINE_GUN_SPEED
    const duration = clamp(distance / speed, isSniper ? 0.08 : 0.1, isSniper ? 0.28 : 0.32)

    this.player.triggerShot(isSniper ? 1.45 : 0.9)
    this.spawnMuzzleFlash(origin.x, origin.y, aimAngle, isSniper ? 38 : 16, isSniper ? 0.12 : 0.085, this.currentWeapon.id)
    this.needleProjectiles.push({
      startX: origin.x,
      startY: origin.y,
      endX: trailEndX,
      endY: trailEndY,
      dirX,
      dirY,
      age: 0,
      duration,
      length: isSniper ? 28 : 14,
      width: isSniper ? 4.8 : 2.8,
      color: isSniper ? palette.playerEdge : palette.accent,
      coreColor: isSniper ? palette.accentSoft : palette.arenaCore,
    })
    this.weaponHudPulse = Math.max(this.weaponHudPulse, isSniper ? 1 : 0.45)
    this.addShake(isSniper ? 0.14 : 0.04)
  }

  private resolveSegmentHits(origin: { x: number; y: number }, target: { x: number; y: number }, extraRadius: number): SegmentHit[] {
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

  private spawnMuzzleFlash(x: number, y: number, angle: number, size: number, duration: number, weaponType: WeaponType): void {
    this.muzzleFlashes.push({ x, y, angle, age: 0, duration, size, weaponType })
  }

  private advanceSpawnQueue(deltaSeconds: number): void {
    if (this.pendingSpawns.length > 0) {
      this.spawnTimer -= deltaSeconds

      while (this.spawnTimer <= 0 && this.pendingSpawns.length > 0) {
        const next = this.pendingSpawns.shift()

        if (!next) {
          break
        }

        this.spawnEnemy(next.type)
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
      this.showToast(`Wave ${String(this.waveIndex).padStart(2, '0')}`, buildWaveHint(this.waveIndex), 1.15)
      this.infoHudPulse = Math.max(this.infoHudPulse, 0.9)
    }
  }

  private spawnEnemy(type: HostileType): void {
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
    this.infoHudPulse = Math.max(this.infoHudPulse, type === 'boss' ? 1 : 0.24)

    if (type === 'boss') {
      this.showToast('Mini Boss', 'Aegis Prime locking the arena', 1.35)
      this.addShake(0.18)
    }
  }

  private pickSpawnPoint(type: HostileType): { x: number; y: number } {
    if (type === 'boss') {
      return {
        x: this.viewport.width / 2,
        y: this.arenaBounds.top + 72,
      }
    }

    const side = this.enemyId % 4

    if (side === 0) {
      return { x: this.arenaBounds.left, y: lerp(this.arenaBounds.top, this.arenaBounds.bottom, Math.random()) }
    }

    if (side === 1) {
      return { x: this.arenaBounds.right, y: lerp(this.arenaBounds.top, this.arenaBounds.bottom, Math.random()) }
    }

    if (side === 2) {
      return { x: lerp(this.arenaBounds.left, this.arenaBounds.right, Math.random()), y: this.arenaBounds.top }
    }

    return { x: lerp(this.arenaBounds.left, this.arenaBounds.right, Math.random()), y: this.arenaBounds.bottom }
  }

  private updateBossEnemy(
    enemy: EnemyActor,
    deltaSeconds: number,
    distance: number,
    dirX: number,
    dirY: number,
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
      this.showToast('Phase 2', 'Aegis Prime enraged', 1.2)
      this.spawnExplosionFeedback(enemy.x, enemy.y, 92)
      this.addShake(0.34)

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
        } else {
          this.fireBossNova(enemy)
          enemy.attackCooldown = enemy.phase === 1 ? 1.65 : 1.18
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
        const t = bulletCount === 1 ? 0.5 : index / (bulletCount - 1)
        const angle = targetAngle - spread * 0.5 + spread * t + offset

        this.spawnHostileProjectile(enemy, angle, (enemy.definition.projectileSpeed ?? 290) + fanIndex * 18, palette.enemyBoss, palette.enemyBossGlow)
      }
    }

    this.spawnImpactParticles(enemy.x, enemy.y, 10, palette.enemyBossGlow, 60, 180, 0.12, 0.22, targetAngle, 1.5)
    this.addShake(enemy.phase === 1 ? 0.1 : 0.16)
    this.infoHudPulse = Math.max(this.infoHudPulse, 0.75)
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

    this.spawnExplosionFeedback(enemy.x, enemy.y, 74)
    this.addShake(enemy.phase === 1 ? 0.16 : 0.24)
  }

  private updateEnemies(deltaSeconds: number, elapsedSeconds: number): void {
    const playerPosition = this.player.getPosition()
    const playerRadius = this.player.getCollisionRadius()

    for (const enemy of this.enemies) {
      enemy.contactCooldown = Math.max(0, enemy.contactCooldown - deltaSeconds)
      enemy.attackCooldown = Math.max(0, enemy.attackCooldown - deltaSeconds)

      const deltaX = playerPosition.x - enemy.x
      const deltaY = playerPosition.y - enemy.y
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
        const bossMotion = this.updateBossEnemy(enemy, deltaSeconds, distance, dirX, dirY)

        velocityX = bossMotion.velocityX
        velocityY = bossMotion.velocityY
      }

      enemy.avatar.setAimAngle(enemy.facingAngle)

      enemy.x = clamp(enemy.x + velocityX * deltaSeconds, this.arenaBounds.left, this.arenaBounds.right)
      enemy.y = clamp(enemy.y + velocityY * deltaSeconds, this.arenaBounds.top, this.arenaBounds.bottom)

      const currentDeltaX = playerPosition.x - enemy.x
      const currentDeltaY = playerPosition.y - enemy.y
      const currentDistance = Math.hypot(currentDeltaX, currentDeltaY)
      const minDistance = enemy.definition.radius + playerRadius + 2

      if (currentDistance > 0.0001 && currentDistance < minDistance) {
        const scale = minDistance / currentDistance

        enemy.x = playerPosition.x - currentDeltaX * scale
        enemy.y = playerPosition.y - currentDeltaY * scale
      }

      if (enemy.contactCooldown === 0 && currentDistance <= enemy.definition.radius + playerRadius + 6) {
        this.applyPlayerDamage(enemy.definition.contactDamage, enemy.x, enemy.y)
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

  private updateEnemyProjectiles(deltaSeconds: number): void {
    const playerPosition = this.player.getPosition()
    const playerRadius = this.player.getCollisionRadius()

    for (let index = this.enemyProjectiles.length - 1; index >= 0; index -= 1) {
      const projectile = this.enemyProjectiles[index]

      projectile.age += deltaSeconds
      projectile.x += projectile.vx * deltaSeconds
      projectile.y += projectile.vy * deltaSeconds

      const hitPlayer =
        !this.player.isDashing() &&
        Math.hypot(projectile.x - playerPosition.x, projectile.y - playerPosition.y) <= projectile.radius + playerRadius

      const outsideArena =
        projectile.x < this.arenaBounds.left - 20 ||
        projectile.x > this.arenaBounds.right + 20 ||
        projectile.y < this.arenaBounds.top - 20 ||
        projectile.y > this.arenaBounds.bottom + 20

      if (hitPlayer) {
        this.applyPlayerDamage(projectile.damage, projectile.x, projectile.y)
        this.enemyProjectiles.splice(index, 1)
        continue
      }

      if (outsideArena || projectile.age >= projectile.duration) {
        this.enemyProjectiles.splice(index, 1)
      }
    }
  }

  private applyPlayerDamage(amount: number, sourceX: number, sourceY: number): void {
    if (this.player.isDashing() || this.encounterState !== 'active') {
      return
    }

    this.playerHealth = Math.max(0, this.playerHealth - amount)
    this.player.setLifeRatio(this.playerHealth / PLAYER_MAX_HEALTH)
    this.player.flashDamage(amount >= 16 ? 1 : 0.72)
    this.healthHudPulse = Math.max(this.healthHudPulse, 1)
    this.infoHudPulse = Math.max(this.infoHudPulse, 0.5)
    this.addShake(amount >= 16 ? 0.2 : 0.12)

    this.burstRings.push({
      x: sourceX,
      y: sourceY,
      age: 0,
      duration: 0.16,
      startRadius: 12,
      endRadius: 34,
      color: palette.danger,
      width: 3,
    })

    if (this.playerHealth === 0) {
      this.encounterState = 'down'
      this.pendingSpawns.length = 0
      this.showToast('Signal Lost', 'Press R to redeploy', 99)
    }
  }

  private damageEnemy(enemy: EnemyActor, amount: number, impactX: number, impactY: number): void {
    const index = this.enemies.indexOf(enemy)

    if (index === -1) {
      return
    }

    enemy.health = Math.max(0, enemy.health - amount)
    enemy.avatar.triggerDamageFlash(amount >= SNIPER_DAMAGE ? 1 : 0.74)
    this.spawnHitFeedback(impactX, impactY, Math.min(1.1, amount / 36), Math.atan2(impactY - enemy.y, impactX - enemy.x))

    this.burstRings.push({
      x: impactX,
      y: impactY,
      age: 0,
      duration: 0.14,
      startRadius: 8,
      endRadius: 24,
      color: palette.accentSoft,
      width: 2,
    })

    if (enemy.health > 0) {
      return
    }

    this.killCount += 1
    this.spawnKillFragments(enemy)
    this.burstRings.push({
      x: enemy.x,
      y: enemy.y,
      age: 0,
      duration: 0.22,
      startRadius: 14,
      endRadius: 42,
      color: enemy.definition.colors.glow,
      width: 3,
    })
    this.enemyLayer.removeChild(enemy.avatar.container)
    enemy.avatar.destroy()
    this.enemies.splice(index, 1)

    if (enemy.type === 'boss') {
      this.encounterState = 'clear'
      this.pendingSpawns.length = 0
      this.enemyProjectiles.length = 0
      this.showToast('Aegis Prime Down', 'Press R to rerun the boss route', 99)
      this.infoHudPulse = Math.max(this.infoHudPulse, 1)
      this.addShake(0.4)
    }
  }

  private updateEffects(deltaSeconds: number): void {
    for (const projectile of this.needleProjectiles) {
      projectile.age += deltaSeconds
    }

    for (const ring of this.burstRings) {
      ring.age += deltaSeconds
    }

    for (const flash of this.muzzleFlashes) {
      flash.age += deltaSeconds
    }

    for (const explosion of this.grenadeExplosions) {
      explosion.age += deltaSeconds
    }

    for (let index = this.grenadeProjectiles.length - 1; index >= 0; index -= 1) {
      const projectile = this.grenadeProjectiles[index]

      projectile.age += deltaSeconds

      if (projectile.age >= projectile.duration) {
        const radius = weaponBySlot[2].splashRadius ?? 66

        this.grenadeProjectiles.splice(index, 1)
        this.grenadeExplosions.push({
          x: projectile.endX,
          y: projectile.endY,
          age: 0,
          duration: 0.34,
          radius,
        })
        this.applyExplosionDamage(projectile.endX, projectile.endY, radius)
        this.spawnExplosionFeedback(projectile.endX, projectile.endY, radius)
        this.burstRings.push({
          x: projectile.endX,
          y: projectile.endY,
          age: 0,
          duration: 0.24,
          startRadius: 18,
          endRadius: radius,
          color: palette.accent,
          width: 4,
        })
        this.burstRings.push({
          x: projectile.endX,
          y: projectile.endY,
          age: 0,
          duration: 0.2,
          startRadius: 12,
          endRadius: radius * 0.72,
          color: palette.dash,
          width: 2,
        })
      }
    }

    removeExpired(this.needleProjectiles)
    removeExpired(this.burstRings)
    removeExpired(this.muzzleFlashes)
    removeExpired(this.grenadeExplosions)
  }

  private applyExplosionDamage(x: number, y: number, radius: number): void {
    for (const enemy of [...this.enemies]) {
      const distance = Math.hypot(enemy.x - x, enemy.y - y)
      const reach = radius + enemy.definition.radius

      if (distance > reach) {
        continue
      }

      const falloff = 1 - distance / reach
      const damage = GRENADE_DAMAGE * (0.55 + falloff * 0.45)

      this.damageEnemy(enemy, damage, enemy.x, enemy.y)
    }
  }

  private drawAimLayer(showReticle: boolean): void {
    const playerPosition = this.player.getPosition()

    this.aimGuide.clear()
    this.reticle.clear()

    if (!showReticle) {
      return
    }

    if (this.currentWeapon.id === 'machineGun') {
      this.aimGuide.moveTo(playerPosition.x, playerPosition.y).lineTo(this.lastAimPoint.x, this.lastAimPoint.y).stroke({
        width: 1.5,
        color: palette.reticle,
        alpha: 0.16,
      })

      this.reticle
        .circle(this.lastAimPoint.x, this.lastAimPoint.y, 11)
        .stroke({ width: 2, color: palette.reticle, alpha: 0.55, alignment: 0.5 })
        .circle(this.lastAimPoint.x, this.lastAimPoint.y, 2)
        .fill({ color: palette.accent, alpha: 0.96 })
        .moveTo(this.lastAimPoint.x - 16, this.lastAimPoint.y)
        .lineTo(this.lastAimPoint.x - 7, this.lastAimPoint.y)
        .moveTo(this.lastAimPoint.x + 7, this.lastAimPoint.y)
        .lineTo(this.lastAimPoint.x + 16, this.lastAimPoint.y)
        .moveTo(this.lastAimPoint.x, this.lastAimPoint.y - 16)
        .lineTo(this.lastAimPoint.x, this.lastAimPoint.y - 7)
        .moveTo(this.lastAimPoint.x, this.lastAimPoint.y + 7)
        .lineTo(this.lastAimPoint.x, this.lastAimPoint.y + 16)
        .stroke({ width: 2, color: palette.accentSoft, alpha: 0.82 })
      return
    }

    if (this.currentWeapon.id === 'grenade') {
      this.aimGuide
        .circle(playerPosition.x, playerPosition.y, this.currentWeapon.range)
        .stroke({ width: 2, color: palette.frame, alpha: 0.24, alignment: 0.5 })
        .moveTo(playerPosition.x, playerPosition.y)
        .lineTo(this.lastAimPoint.x, this.lastAimPoint.y)
        .stroke({ width: 2, color: palette.accentSoft, alpha: 0.28 })

      this.reticle
        .circle(this.lastAimPoint.x, this.lastAimPoint.y, this.currentWeapon.splashRadius ?? 66)
        .stroke({ width: 2, color: palette.accent, alpha: 0.42, alignment: 0.5 })
        .circle(this.lastAimPoint.x, this.lastAimPoint.y, 18)
        .stroke({ width: 2, color: palette.reticle, alpha: 0.58, alignment: 0.5 })
        .circle(this.lastAimPoint.x, this.lastAimPoint.y, 4)
        .fill({ color: palette.accent, alpha: 0.94 })
      return
    }

    this.aimGuide
      .moveTo(playerPosition.x, playerPosition.y)
      .lineTo(this.lastAimPoint.x, this.lastAimPoint.y)
      .stroke({ width: 1.2, color: palette.playerEdge, alpha: 0.26, cap: 'round' })

    this.reticle
      .moveTo(this.lastAimPoint.x - 26, this.lastAimPoint.y)
      .lineTo(this.lastAimPoint.x - 10, this.lastAimPoint.y)
      .moveTo(this.lastAimPoint.x + 10, this.lastAimPoint.y)
      .lineTo(this.lastAimPoint.x + 26, this.lastAimPoint.y)
      .moveTo(this.lastAimPoint.x, this.lastAimPoint.y - 26)
      .lineTo(this.lastAimPoint.x, this.lastAimPoint.y - 10)
      .moveTo(this.lastAimPoint.x, this.lastAimPoint.y + 10)
      .lineTo(this.lastAimPoint.x, this.lastAimPoint.y + 26)
      .stroke({ width: 2, color: palette.reticle, alpha: 0.84 })
      .circle(this.lastAimPoint.x, this.lastAimPoint.y, 3)
      .fill({ color: palette.accent, alpha: 0.96 })
  }

  private drawEffects(): void {
    this.effects.clear()

    const playerPosition = this.player.getPosition()

    for (const ghost of this.dashAfterimages) {
      const progress = ghost.age / ghost.duration
      const alpha = (1 - progress) * 0.26

      drawDashGhost(this.effects, ghost, alpha)
    }

    for (const enemy of this.enemies) {
      if (enemy.type === 'ranged' && enemy.mode === 'aim') {
        const windup = enemy.definition.attackWindup ?? 0.42
        const progress = 1 - enemy.modeTimer / windup

        this.effects
          .moveTo(enemy.x, enemy.y)
          .lineTo(playerPosition.x, playerPosition.y)
          .stroke({ width: 2.2, color: palette.warning, alpha: 0.14 + progress * 0.24, cap: 'round' })
      }

      if (enemy.type === 'charger' && enemy.mode === 'windup') {
        const windup = enemy.definition.attackWindup ?? 0.56
        const progress = 1 - enemy.modeTimer / windup

        this.effects
          .moveTo(enemy.x, enemy.y)
          .lineTo(enemy.x + enemy.chargeDirX * 110, enemy.y + enemy.chargeDirY * 110)
          .stroke({ width: 3, color: palette.danger, alpha: 0.12 + progress * 0.3, cap: 'round' })
      }

      if (enemy.type === 'boss' && enemy.mode === 'aim') {
        const windup = enemy.pattern === 'fan' ? (enemy.phase === 1 ? 0.66 : 0.48) : enemy.phase === 1 ? 0.92 : 0.68
        const progress = 1 - enemy.modeTimer / windup

        if (enemy.pattern === 'fan') {
          const spread = enemy.phase === 1 ? 0.95 : 1.28

          for (const offset of [-spread * 0.5, 0, spread * 0.5]) {
            const angle = enemy.facingAngle + offset

            this.effects
              .moveTo(enemy.x, enemy.y)
              .lineTo(enemy.x + Math.cos(angle) * 188, enemy.y + Math.sin(angle) * 188)
              .stroke({ width: 2.4, color: palette.warning, alpha: 0.14 + progress * 0.28, cap: 'round' })
          }
        } else {
          const radius = enemy.phase === 1 ? 62 : 84

          this.effects
            .circle(enemy.x, enemy.y, radius)
            .stroke({ width: 3, color: palette.danger, alpha: 0.16 + progress * 0.3, alignment: 0.5 })
            .circle(enemy.x, enemy.y, radius * 0.56)
            .stroke({ width: 2, color: palette.warning, alpha: 0.12 + progress * 0.22, alignment: 0.5 })
        }
      }
    }

    for (const particle of this.impactParticles) {
      const progress = particle.age / particle.duration
      const alpha = particle.alpha * (1 - progress)
      const points = buildParticleQuad(particle)

      this.effects.poly(points).fill({ color: particle.color, alpha })
    }

    for (const projectile of this.needleProjectiles) {
      const progress = projectile.age / projectile.duration
      const alpha = 1 - progress * 0.78
      const headX = lerp(projectile.startX, projectile.endX, progress)
      const headY = lerp(projectile.startY, projectile.endY, progress)
      const baseX = headX - projectile.dirX * projectile.length
      const baseY = headY - projectile.dirY * projectile.length
      const perpX = -projectile.dirY
      const perpY = projectile.dirX
      const halfWidth = projectile.width
      const bodyPoints = [
        headX + perpX * halfWidth,
        headY + perpY * halfWidth,
        headX - perpX * halfWidth,
        headY - perpY * halfWidth,
        baseX - perpX * halfWidth,
        baseY - perpY * halfWidth,
        baseX + perpX * halfWidth,
        baseY + perpY * halfWidth,
      ]

      this.effects
        .poly(bodyPoints)
        .fill({ color: projectile.color, alpha: alpha * 0.98 })
        .poly(bodyPoints)
        .stroke({
          width: Math.max(1, projectile.width * 0.28),
          color: projectile.coreColor,
          alpha: alpha * 0.72,
          alignment: 0.5,
          join: 'miter',
        })
    }

    for (const projectile of this.enemyProjectiles) {
      this.effects
        .circle(projectile.x, projectile.y, projectile.radius * 1.9)
        .fill({ color: projectile.glowColor, alpha: 0.16 })
        .circle(projectile.x, projectile.y, projectile.radius)
        .fill({ color: projectile.color, alpha: 0.94 })
        .circle(projectile.x, projectile.y, projectile.radius * 0.46)
        .fill({ color: palette.arenaCore, alpha: 0.84 })
    }

    for (const projectile of this.grenadeProjectiles) {
      const progress = projectile.age / projectile.duration
      const travelX = lerp(projectile.startX, projectile.endX, progress)
      const travelY = lerp(projectile.startY, projectile.endY, progress) - Math.sin(progress * Math.PI) * 34
      const tailProgress = Math.max(0, progress - 0.06)
      const tailX = lerp(projectile.startX, projectile.endX, tailProgress)
      const tailY = lerp(projectile.startY, projectile.endY, tailProgress) - Math.sin(tailProgress * Math.PI) * 34

      this.effects
        .moveTo(tailX, tailY)
        .lineTo(travelX, travelY)
        .stroke({ width: 4, color: palette.accentSoft, alpha: 0.48, cap: 'round', join: 'round' })
        .circle(travelX, travelY, 8)
        .fill({ color: palette.accent, alpha: 0.96 })
        .circle(travelX, travelY, 3)
        .fill({ color: palette.arenaCore, alpha: 0.95 })
    }

    for (const flash of this.muzzleFlashes) {
      const progress = flash.age / flash.duration
      const alpha = 1 - progress
      const forwardX = Math.cos(flash.angle)
      const forwardY = Math.sin(flash.angle)
      const perpX = -forwardY
      const perpY = forwardX

      if (flash.weaponType === 'machineGun') {
        const reach = flash.size * (1.1 + alpha * 0.25)

        this.effects
          .moveTo(flash.x, flash.y)
          .lineTo(flash.x + forwardX * reach, flash.y + forwardY * reach)
          .stroke({ width: 2.2, color: palette.accent, alpha: alpha * 0.95, cap: 'round' })
          .moveTo(flash.x + perpX * 2, flash.y + perpY * 2)
          .lineTo(flash.x + forwardX * (reach * 0.72) + perpX * 5, flash.y + forwardY * (reach * 0.72) + perpY * 5)
          .moveTo(flash.x - perpX * 2, flash.y - perpY * 2)
          .lineTo(flash.x + forwardX * (reach * 0.72) - perpX * 5, flash.y + forwardY * (reach * 0.72) - perpY * 5)
          .stroke({ width: 1.6, color: palette.accentSoft, alpha: alpha * 0.82, cap: 'round' })
        continue
      }

      if (flash.weaponType === 'grenade') {
        const tipX = flash.x + forwardX * flash.size * (1.05 + alpha * 0.2)
        const tipY = flash.y + forwardY * flash.size * (1.05 + alpha * 0.2)
        const baseX = flash.x - forwardX * flash.size * 0.18
        const baseY = flash.y - forwardY * flash.size * 0.18
        const spread = flash.size * 0.42

        this.effects
          .poly([
            tipX,
            tipY,
            baseX + perpX * spread,
            baseY + perpY * spread,
            baseX - forwardX * flash.size * 0.14,
            baseY - forwardY * flash.size * 0.14,
            baseX - perpX * spread,
            baseY - perpY * spread,
          ])
          .fill({ color: palette.accentSoft, alpha: alpha * 0.42 })
          .circle(flash.x, flash.y, flash.size * (0.22 + alpha * 0.08))
          .fill({ color: palette.accent, alpha: alpha * 0.42 })
        continue
      }

      const reach = flash.size * (1.2 + alpha * 0.3)

      this.effects
        .moveTo(flash.x, flash.y)
        .lineTo(flash.x + forwardX * reach, flash.y + forwardY * reach)
        .stroke({ width: 4.8, color: palette.playerEdge, alpha: alpha * 0.95, cap: 'round' })
        .moveTo(flash.x - forwardX * flash.size * 0.18, flash.y - forwardY * flash.size * 0.18)
        .lineTo(flash.x + perpX * flash.size * 0.62, flash.y + perpY * flash.size * 0.62)
        .moveTo(flash.x - forwardX * flash.size * 0.18, flash.y - forwardY * flash.size * 0.18)
        .lineTo(flash.x - perpX * flash.size * 0.62, flash.y - perpY * flash.size * 0.62)
        .stroke({ width: 2.2, color: palette.accentSoft, alpha: alpha * 0.82, cap: 'round' })
        .circle(flash.x, flash.y, flash.size * 0.12)
        .fill({ color: palette.accent, alpha: alpha * 0.62 })
    }

    for (const explosion of this.grenadeExplosions) {
      const progress = explosion.age / explosion.duration
      const alpha = 1 - progress
      const radius = lerp(10, explosion.radius, easeOutCubic(progress))

      this.effects
        .star(explosion.x, explosion.y, 8, radius * 0.46 + alpha * 6, radius * 0.18 + alpha * 2, progress * 0.9)
        .fill({ color: palette.accentSoft, alpha: alpha * 0.22 })
        .circle(explosion.x, explosion.y, radius * (0.22 + alpha * 0.12))
        .fill({ color: palette.accent, alpha: alpha * 0.24 })
        .circle(explosion.x, explosion.y, radius)
        .stroke({ width: 5 - progress * 2.5, color: palette.accent, alpha: alpha * 0.88, alignment: 0.5 })
        .circle(explosion.x, explosion.y, radius * 0.64)
        .stroke({ width: 3 - progress * 1.4, color: palette.dash, alpha: alpha * 0.7, alignment: 0.5 })
    }

    for (const ring of this.burstRings) {
      const progress = ring.age / ring.duration
      const alpha = 1 - progress
      const radius = ring.startRadius + (ring.endRadius - ring.startRadius) * progress

      this.effects
        .circle(ring.x, ring.y, radius)
        .stroke({ width: Math.max(1, ring.width - progress * 2), color: ring.color, alpha: alpha * 0.72, alignment: 0.5 })
    }
  }

  private layoutHud(): void {
    this.toastTitle.position.set(this.viewport.width / 2, 46)
    this.toastHint.position.set(this.viewport.width / 2, 72)
    this.healthText.position.set(56, 36)
    this.waveText.position.set(this.viewport.width - 270, 36)
    this.enemyText.position.set(this.viewport.width - 270, 58)
    this.bossText.position.set(this.viewport.width / 2, 106)
    this.centerTitle.position.set(this.viewport.width / 2, this.viewport.height / 2 - 16)
    this.centerHint.position.set(this.viewport.width / 2, this.viewport.height / 2 + 14)

    const totalWidth = weaponLoadout.length * 128 + (weaponLoadout.length - 1) * 12
    let x = (this.viewport.width - totalWidth) / 2
    const y = this.viewport.height - 72

    for (const label of this.weaponLabels) {
      label.position.set(x + 18, y + 14)
      x += 140
    }
  }

  private drawHud(): void {
    this.hudGraphics.clear()

    const playerHealthRatio = this.playerHealth / PLAYER_MAX_HEALTH
    const lowHealthPulse =
      playerHealthRatio < 0.45 ? ((Math.sin(this.visualTime * 7.4) + 1) * 0.5) * (1 - playerHealthRatio) : 0
    const healthShake = Math.sin(this.visualTime * 46) * this.healthHudPulse * 2.4
    const infoLift = this.infoHudPulse * 4
    const totalWidth = weaponLoadout.length * 128 + (weaponLoadout.length - 1) * 12
    const beltX = (this.viewport.width - totalWidth) / 2
    const beltY = this.viewport.height - 72 - this.weaponHudPulse * 4
    const displayWave = Math.max(1, this.waveIndex)
    const activeHealthColor = playerHealthRatio > 0.32 ? palette.dash : palette.danger
    const boss = this.enemies.find((enemy) => enemy.type === 'boss') ?? null

    this.healthText.position.set(56 + healthShake, 36)
    this.waveText.position.set(this.viewport.width - 270, 36 - infoLift)
    this.enemyText.position.set(this.viewport.width - 270, 58 - infoLift)
    this.bossText.position.set(this.viewport.width / 2, 106)

    this.healthText.text = `HULL ${Math.ceil(this.playerHealth)} / ${PLAYER_MAX_HEALTH}`
    this.waveText.text = `WAVE ${String(displayWave).padStart(2, '0')}   KILLS ${this.killCount}`
    this.enemyText.text = `HOSTILES ${this.enemies.length}   INBOUND ${this.pendingSpawns.length}`
    this.bossText.text = boss ? `MINI BOSS  //  ${boss.definition.label.toUpperCase()}  //  PHASE ${boss.phase}` : ''

    this.hudGraphics
      .roundRect(28 + healthShake, 20, 276, 72, 22)
      .fill({ color: palette.uiPanel, alpha: 0.88 })
      .roundRect(28 + healthShake, 20, 276, 72, 22)
      .stroke({ width: 1.5, color: palette.frameSoft, alpha: 0.42, alignment: 0.5 })
      .roundRect(48 + healthShake, 60, 236, 12, 999)
      .fill({ color: palette.uiText, alpha: 0.12 })
      .roundRect(48 + healthShake, 60, 236 * playerHealthRatio, 12, 999)
      .fill({ color: activeHealthColor, alpha: 0.92 })
      .roundRect(48 + healthShake, 60, 236 * Math.min(1, playerHealthRatio + lowHealthPulse * 0.08), 12, 999)
      .fill({ color: palette.arenaCore, alpha: 0.16 + lowHealthPulse * 0.24 + this.healthHudPulse * 0.18 })
      .roundRect(this.viewport.width - 288, 20 - infoLift, 260, 72, 22)
      .fill({ color: palette.uiPanel, alpha: 0.88 })
      .roundRect(this.viewport.width - 288, 20 - infoLift, 260, 72, 22)
      .stroke({ width: 1.5, color: palette.frameSoft, alpha: 0.42, alignment: 0.5 })
      .roundRect(beltX - 18, beltY - 10, totalWidth + 36, 64, 26)
      .fill({ color: palette.uiPanel, alpha: 0.68 })
      .roundRect(beltX - 18, beltY - 10, totalWidth + 36, 64, 26)
      .stroke({ width: 1.5, color: palette.frameSoft, alpha: 0.34, alignment: 0.5 })

    if (boss) {
      const ratio = boss.health / boss.definition.maxHealth

      this.bossText.alpha = 1
      this.hudGraphics
        .roundRect(this.viewport.width / 2 - 178, 96, 356, 40, 18)
        .fill({ color: palette.uiPanel, alpha: 0.88 })
        .roundRect(this.viewport.width / 2 - 178, 96, 356, 40, 18)
        .stroke({ width: 1.5, color: palette.frameSoft, alpha: 0.38, alignment: 0.5 })
        .roundRect(this.viewport.width / 2 - 160, 118, 320, 8, 999)
        .fill({ color: palette.uiText, alpha: 0.14 })
        .roundRect(this.viewport.width / 2 - 160, 118, 320 * ratio, 8, 999)
        .fill({ color: boss.phase === 1 ? palette.warning : palette.danger, alpha: 0.94 })
    } else {
      this.bossText.alpha = 0
    }

    let x = beltX
    const y = beltY

    for (let index = 0; index < weaponLoadout.length; index += 1) {
      const weapon = weaponLoadout[index]
      const active = weapon.id === this.currentWeapon.id
      const label = this.weaponLabels[index]
      const activeRise = active ? this.weaponHudPulse * 6 : 0
      const activeGlow = active ? 0.18 + this.weaponHudPulse * 0.18 : 0

      label.tint = active ? palette.uiText : palette.uiMuted
      label.alpha = active ? 1 : 0.78
      label.position.set(x + 18, y - activeRise + 14)

      this.hudGraphics
        .roundRect(x, y - activeRise, 128, 44 + activeRise, 18)
        .fill({ color: palette.accentSoft, alpha: activeGlow })
        .roundRect(x, y - activeRise, 128, 44 + activeRise, 18)
        .fill({ color: active ? palette.uiActive : palette.uiPanel, alpha: 0.88 })
        .roundRect(x, y - activeRise, 128, 44 + activeRise, 18)
        .stroke({ width: 1.5, color: active ? palette.frame : palette.frameSoft, alpha: active ? 0.82 : 0.42, alignment: 0.5 })

      x += 140
    }

    if (this.messageTimer > 0) {
      const alpha = Math.min(1, this.messageTimer * 1.8)

      this.hudGraphics
        .roundRect(this.viewport.width / 2 - 154, 20, 308, 72, 24)
        .fill({ color: palette.uiPanel, alpha: alpha * 0.94 })
        .roundRect(this.viewport.width / 2 - 154, 20, 308, 72, 24)
        .stroke({ width: 1.5, color: palette.frame, alpha: alpha * 0.54, alignment: 0.5 })

      this.toastTitle.alpha = alpha
      this.toastHint.alpha = alpha * 0.88
    } else {
      this.toastTitle.alpha = 0
      this.toastHint.alpha = 0
    }

    if (this.encounterState === 'down' || this.encounterState === 'clear') {
      this.centerTitle.text = this.encounterState === 'down' ? 'Signal Lost' : 'Run Clear'
      this.centerHint.text =
        this.encounterState === 'down' ? 'Press R to redeploy from wave 1' : 'Aegis Prime destroyed. Press R to rerun.'
      this.centerTitle.alpha = 1
      this.centerHint.alpha = 0.92

      this.hudGraphics
        .roundRect(this.viewport.width / 2 - 196, this.viewport.height / 2 - 76, 392, 148, 32)
        .fill({ color: palette.uiPanel, alpha: 0.94 })
        .roundRect(this.viewport.width / 2 - 196, this.viewport.height / 2 - 76, 392, 148, 32)
        .stroke({
          width: 1.5,
          color: this.encounterState === 'down' ? palette.danger : palette.frame,
          alpha: 0.36,
          alignment: 0.5,
        })
    } else {
      this.centerTitle.alpha = 0
      this.centerHint.alpha = 0
    }
  }

  private applyWeapon(slot: 1 | 2 | 3, silent = false): void {
    this.currentWeapon = weaponBySlot[slot]
    this.player.setWeaponStyle(this.currentWeapon.id)
    this.player.triggerWeaponSwap()
    this.toastTitle.text = `${slot} / ${this.currentWeapon.label}`
    this.toastHint.text = this.currentWeapon.hint
    this.messageTimer = silent ? this.messageTimer : 1.15
    this.shotCooldown = 0
    this.weaponHudPulse = Math.max(this.weaponHudPulse, 1)
    this.infoHudPulse = Math.max(this.infoHudPulse, 0.32)
  }

  private showToast(title: string, hint: string, duration: number): void {
    this.toastTitle.text = title
    this.toastHint.text = hint
    this.messageTimer = duration
  }
}

function removeExpired<T extends { age: number; duration: number }>(items: T[]): void {
  for (let index = items.length - 1; index >= 0; index -= 1) {
    if (items[index].age >= items[index].duration) {
      items.splice(index, 1)
    }
  }
}

function drawDashGhost(graphics: Graphics, ghost: DashAfterimage, alpha: number): void {
  const bodyPoints = transformPoints(playerGhostBody, ghost.x, ghost.y, ghost.aimAngle * 0.18, ghost.scale)
  const arrowOriginX = ghost.x + Math.cos(ghost.aimAngle) * 26 * ghost.scale
  const arrowOriginY = ghost.y + Math.sin(ghost.aimAngle) * 26 * ghost.scale
  const arrowPoints = transformPoints(weaponGhostArrow[ghost.weaponType], arrowOriginX, arrowOriginY, ghost.aimAngle, ghost.scale)

  graphics
    .poly(bodyPoints)
    .fill({ color: palette.playerEdge, alpha })
    .poly(arrowPoints)
    .fill({ color: palette.accentSoft, alpha: alpha * 0.88 })
}

function buildParticleQuad(particle: ImpactParticle): number[] {
  const dirX = Math.cos(particle.rotation)
  const dirY = Math.sin(particle.rotation)
  const perpX = -dirY
  const perpY = dirX
  const halfLength = particle.length * 0.5
  const halfWidth = particle.width * 0.5

  return [
    particle.x + dirX * halfLength + perpX * halfWidth,
    particle.y + dirY * halfLength + perpY * halfWidth,
    particle.x + dirX * halfLength - perpX * halfWidth,
    particle.y + dirY * halfLength - perpY * halfWidth,
    particle.x - dirX * halfLength - perpX * halfWidth,
    particle.y - dirY * halfLength - perpY * halfWidth,
    particle.x - dirX * halfLength + perpX * halfWidth,
    particle.y - dirY * halfLength + perpY * halfWidth,
  ]
}

function transformPoints(points: number[], x: number, y: number, rotation: number, scale: number): number[] {
  const cos = Math.cos(rotation)
  const sin = Math.sin(rotation)
  const transformed: number[] = []

  for (let index = 0; index < points.length; index += 2) {
    const px = points[index] * scale
    const py = points[index + 1] * scale

    transformed.push(x + px * cos - py * sin, y + px * sin + py * cos)
  }

  return transformed
}

const playerGhostBody = [-14, -14, 14, -14, 14, 14, -14, 14]

const weaponGhostArrow: Record<WeaponType, number[]> = {
  machineGun: [12, 0, -10, -9, -2, 0, -10, 9],
  grenade: [13, 0, 4, -11, -10, -5, -5, 0, -10, 5, 4, 11],
  sniper: [19, 0, -13, -6, -4, 0, -13, 6],
}
