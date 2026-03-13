import { Container, Graphics, Text } from 'pixi.js'

import type { ArenaBounds, GameScene, InputSnapshot, ViewportSize } from '../core/contracts'
import { PlayerAvatar } from '../entities/PlayerAvatar'
import { palette } from '../theme/palette'
import { weaponBySlot, weaponLoadout, type WeaponDefinition, type WeaponType } from '../weapons/weaponConfig'

const FRAME_MARGIN = 72
const PLAYER_PADDING = 56
const GRID_SIZE = 56
const MACHINE_GUN_SPEED = 960
const SNIPER_SPEED = 1560

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

export class CombatSandboxScene implements GameScene {
  readonly container = new Container()

  private readonly backdrop = new Graphics()
  private readonly grid = new Graphics()
  private readonly frame = new Graphics()
  private readonly world = new Container()
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
    anchor: 0.5,
  })
  private readonly toastHint = new Text({
    text: '',
    style: {
      fontFamily: 'Bahnschrift, Segoe UI, sans-serif',
      fontSize: 13,
      fill: palette.uiMuted,
      letterSpacing: 0.6,
    },
    anchor: 0.5,
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

  private viewport: ViewportSize = { width: 0, height: 0 }
  private arenaBounds: ArenaBounds = { left: 0, top: 0, right: 0, bottom: 0 }
  private readonly needleProjectiles: NeedleProjectile[] = []
  private readonly burstRings: BurstRing[] = []
  private readonly muzzleFlashes: MuzzleFlash[] = []
  private readonly grenadeProjectiles: GrenadeProjectile[] = []
  private readonly grenadeExplosions: GrenadeExplosion[] = []
  private hasPlacedPlayer = false
  private lastAimPoint = { x: 0, y: 0 }
  private shotCooldown = 0
  private switchToast = 1.4
  private currentWeapon: WeaponDefinition = weaponBySlot[1]

  constructor() {
    this.container.addChild(this.backdrop, this.grid, this.frame, this.world, this.aimGuide, this.effects, this.reticle, this.hud)
    this.world.addChild(this.player.container)
    this.hud.addChild(this.hudGraphics, ...this.weaponLabels, this.toastTitle, this.toastHint)
    this.applyWeapon(1, true)
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
      this.hasPlacedPlayer = true
    }
  }

  update(deltaSeconds: number, elapsedSeconds: number, input: InputSnapshot): void {
    this.shotCooldown = Math.max(0, this.shotCooldown - deltaSeconds)
    this.switchToast = Math.max(0, this.switchToast - deltaSeconds)

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
    }

    if (input.shootHeld && this.shotCooldown === 0 && input.hasPointer) {
      this.fireWeapon(aimPoint)
      this.shotCooldown = this.currentWeapon.cooldown
    }

    this.player.update(deltaSeconds, this.arenaBounds, elapsedSeconds)
    this.updateEffects(deltaSeconds)
    this.drawAimLayer(input.hasPointer)
    this.drawEffects()
    this.drawHud()
  }

  destroy(): void {
    this.container.destroy({ children: true })
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

      if (this.currentWeapon.id === 'grenade') {
        return clampToDistance(clamped, playerPosition, this.currentWeapon.range)
      }

      return clamped
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
      return
    }

    const range = this.currentWeapon.id === 'sniper' ? 100000 : this.currentWeapon.range
    const target = {
      x: origin.x + dirX * range,
      y: origin.y + dirY * range,
    }
    const clipped = clipToArena(origin, target, this.arenaBounds)
    const distance = Math.hypot(clipped.x - origin.x, clipped.y - origin.y)
    const isSniper = this.currentWeapon.id === 'sniper'
    const speed = isSniper ? SNIPER_SPEED : MACHINE_GUN_SPEED
    const duration = clamp(distance / speed, isSniper ? 0.08 : 0.1, isSniper ? 0.28 : 0.32)

    this.player.triggerShot(isSniper ? 1.45 : 0.9)
    this.spawnMuzzleFlash(origin.x, origin.y, aimAngle, isSniper ? 38 : 16, isSniper ? 0.12 : 0.085, this.currentWeapon.id)
    this.needleProjectiles.push({
      startX: origin.x,
      startY: origin.y,
      endX: clipped.x,
      endY: clipped.y,
      dirX,
      dirY,
      age: 0,
      duration,
      length: isSniper ? 28 : 14,
      width: isSniper ? 4.8 : 2.8,
      color: isSniper ? palette.playerEdge : palette.accent,
      coreColor: isSniper ? palette.accentSoft : palette.arenaCore,
    })
  }

  private spawnMuzzleFlash(x: number, y: number, angle: number, size: number, duration: number, weaponType: WeaponType): void {
    this.muzzleFlashes.push({
      x,
      y,
      angle,
      age: 0,
      duration,
      size,
      weaponType,
    })
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
        this.grenadeProjectiles.splice(index, 1)
        this.grenadeExplosions.push({
          x: projectile.endX,
          y: projectile.endY,
          age: 0,
          duration: 0.34,
          radius: weaponBySlot[2].splashRadius ?? 66,
        })
        this.burstRings.push({
          x: projectile.endX,
          y: projectile.endY,
          age: 0,
          duration: 0.24,
          startRadius: 18,
          endRadius: weaponBySlot[2].splashRadius ?? 66,
          color: palette.accent,
          width: 4,
        })
        this.burstRings.push({
          x: projectile.endX,
          y: projectile.endY,
          age: 0,
          duration: 0.2,
          startRadius: 12,
          endRadius: (weaponBySlot[2].splashRadius ?? 66) * 0.72,
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

  private drawAimLayer(showReticle: boolean): void {
    const playerPosition = this.player.getPosition()

    this.aimGuide.clear()
    this.reticle.clear()

    if (!showReticle) {
      return
    }

    if (this.currentWeapon.id === 'machineGun') {
      this.aimGuide
        .moveTo(playerPosition.x, playerPosition.y)
        .lineTo(this.lastAimPoint.x, this.lastAimPoint.y)
        .stroke({ width: 1.5, color: palette.reticle, alpha: 0.16 })

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

      for (let index = 0; index < 7; index += 1) {
        const angle = progress * 0.45 + index * ((Math.PI * 2) / 7)
        const dirX = Math.cos(angle)
        const dirY = Math.sin(angle)
        const inner = radius * 0.18
        const outer = radius * (0.72 + (index % 2) * 0.08)

        this.effects
          .moveTo(explosion.x + dirX * inner, explosion.y + dirY * inner)
          .lineTo(explosion.x + dirX * outer, explosion.y + dirY * outer)
          .stroke({ width: 2.8 - progress * 1.5, color: palette.accentSoft, alpha: alpha * 0.68, cap: 'round' })
      }
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

    let x = 42
    const y = this.viewport.height - 60

    for (const label of this.weaponLabels) {
      label.position.set(x + 16, y + 12)
      x += 130
    }
  }

  private drawHud(): void {
    this.hudGraphics.clear()

    let x = 42
    const y = this.viewport.height - 60

    for (let index = 0; index < weaponLoadout.length; index += 1) {
      const weapon = weaponLoadout[index]
      const active = weapon.id === this.currentWeapon.id
      const label = this.weaponLabels[index]

      label.tint = active ? palette.uiText : palette.uiMuted
      label.alpha = active ? 1 : 0.85

      this.hudGraphics
        .roundRect(x, y, 118, 40, 16)
        .fill({ color: active ? palette.uiActive : palette.uiPanel, alpha: 0.88 })
        .roundRect(x, y, 118, 40, 16)
        .stroke({ width: 1.5, color: active ? palette.frame : palette.frameSoft, alpha: active ? 0.7 : 0.42, alignment: 0.5 })

      x += 130
    }

    if (this.switchToast > 0) {
      const alpha = Math.min(1, this.switchToast * 1.8)

      this.hudGraphics
        .roundRect(this.viewport.width / 2 - 144, 22, 288, 68, 22)
        .fill({ color: palette.uiPanel, alpha: alpha * 0.94 })
        .roundRect(this.viewport.width / 2 - 144, 22, 288, 68, 22)
        .stroke({ width: 1.5, color: palette.frame, alpha: alpha * 0.5, alignment: 0.5 })

      this.toastTitle.alpha = alpha
      this.toastHint.alpha = alpha * 0.88
    } else {
      this.toastTitle.alpha = 0
      this.toastHint.alpha = 0
    }
  }

  private applyWeapon(slot: 1 | 2 | 3, silent = false): void {
    this.currentWeapon = weaponBySlot[slot]
    this.player.setWeaponStyle(this.currentWeapon.id)
    this.player.triggerWeaponSwap()
    this.toastTitle.text = `${slot} · ${this.currentWeapon.label}`
    this.toastHint.text = this.currentWeapon.hint
    this.switchToast = silent ? this.switchToast : 1.15
    this.shotCooldown = 0
  }
}

function removeExpired<T extends { age: number; duration: number }>(items: T[]): void {
  for (let index = items.length - 1; index >= 0; index -= 1) {
    if (items[index].age >= items[index].duration) {
      items.splice(index, 1)
    }
  }
}

function clipToArena(origin: { x: number; y: number }, target: { x: number; y: number }, arena: ArenaBounds): { x: number; y: number } {
  let bestT = 1

  if (target.x !== origin.x) {
    const leftT = (arena.left - origin.x) / (target.x - origin.x)
    const rightT = (arena.right - origin.x) / (target.x - origin.x)
    bestT = resolveIntersection(bestT, leftT, origin, target, arena, 'x')
    bestT = resolveIntersection(bestT, rightT, origin, target, arena, 'x')
  }

  if (target.y !== origin.y) {
    const topT = (arena.top - origin.y) / (target.y - origin.y)
    const bottomT = (arena.bottom - origin.y) / (target.y - origin.y)
    bestT = resolveIntersection(bestT, topT, origin, target, arena, 'y')
    bestT = resolveIntersection(bestT, bottomT, origin, target, arena, 'y')
  }

  return {
    x: origin.x + (target.x - origin.x) * bestT,
    y: origin.y + (target.y - origin.y) * bestT,
  }
}

function resolveIntersection(
  currentBest: number,
  candidateT: number,
  origin: { x: number; y: number },
  target: { x: number; y: number },
  arena: ArenaBounds,
  axis: 'x' | 'y',
): number {
  if (candidateT <= 0 || candidateT > currentBest) {
    return currentBest
  }

  const pointX = origin.x + (target.x - origin.x) * candidateT
  const pointY = origin.y + (target.y - origin.y) * candidateT

  if (axis === 'x' && pointY >= arena.top && pointY <= arena.bottom) {
    return candidateT
  }

  if (axis === 'y' && pointX >= arena.left && pointX <= arena.right) {
    return candidateT
  }

  return currentBest
}

function clamp(value: number, min: number, max: number): number {
  return Math.min(max, Math.max(min, value))
}

function clampToDistance(point: { x: number; y: number }, origin: { x: number; y: number }, maxDistance: number): { x: number; y: number } {
  const deltaX = point.x - origin.x
  const deltaY = point.y - origin.y
  const distance = Math.hypot(deltaX, deltaY)

  if (distance <= maxDistance || distance <= 0.0001) {
    return point
  }

  const scale = maxDistance / distance

  return {
    x: origin.x + deltaX * scale,
    y: origin.y + deltaY * scale,
  }
}

function lerp(start: number, end: number, t: number): number {
  return start + (end - start) * t
}

function easeOutCubic(value: number): number {
  return 1 - Math.pow(1 - value, 3)
}
